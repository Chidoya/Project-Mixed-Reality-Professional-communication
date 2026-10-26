using System.Collections.Generic;
using System.Linq;
using ProCommVR.Editor;
using ProCommVR.Navigation;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProCommVR.Hospital.Integration.Editor
{
    /// <summary>
    /// Koppelt Liam's stukken aan Jalils shift in Assets/_Project/Scenes/ShiftHospital.unity: zoekt Jalils objecten op naam
    /// of type, zet de bruggen (en de props die ze nodig hebben) onder één nieuw root-object "Liam_Integration" en bewaart
    /// de scene. Draai het na elke rebuild: Hospital > Build, Setup > 8. Build Shift Hospital, Hospital > Wire Shift Integration.
    /// Mag meerdere keren draaien (het oude Liam_Integration gaat eerst weg). Verder verandert er niets in de scene:
    /// Jalils objecten worden enkel gelezen.
    /// </summary>
    public static class ShiftIntegrationBuilder
    {
        private const string ShiftScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const string RootName = "Liam_Integration";
        private const string Case1PropsPath = "Assets/Hospital/Prefabs/Case1_Props.prefab";
        private const string PorterPath = "Assets/Character/Porter/Porter.prefab";
        private const string LogPrefix = "[ShiftIntegration] ";

        // Layout van Liam's ziekenhuis (HospitalBuilder)
        private const float FloorHeight = 3.5f;
        private const float FloorFinish = 0.01f;
        private static readonly float[] DoorCentersX = { 4f, 9f, 14f };
        private static readonly string[] DoorLetters = { "A", "B", "C" };

        // Scenario-id's van Jalil
        private const string Scenario1 = "disappointed-patient";
        private const string Scenario2 = "angry-family-member";
        private const string Scenario5 = "aggressive-patient-privacy";

        // Taken die niet in een Room_* zitten: welke deur hun strook krijgt (2: spreekkamer 3B, 3: bureau van de arts 2C)
        private static readonly Dictionary<string, string> GuideByScenario = new Dictionary<string, string>
        {
            { Scenario2, "3B" },
            { "doctor-and-family", "2C" },
        };

        // Ambient kar: langs de gang, tussen Liam's personeel (zuidkant) en de deuren (noordkant)
        private const float AmbientLaneZ = 1.45f;
        private const float AmbientStartX = 16.5f;
        private const float AmbientEndX = 1.6f;

        [MenuItem("ProCommVR/Hospital/Wire Shift Integration")]
        public static void Wire()
        {
            PropSetup.EnsureSetup();
            var scene = EditorSceneManager.OpenScene(ShiftScenePath, OpenSceneMode.Single);
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            var hospital = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Hospital")?.transform;
            var shift = Object.FindFirstObjectByType<ShiftManager>(FindObjectsInactive.Include);
            if (hospital == null || shift == null)
            {
                Debug.LogError($"{LogPrefix}Geen Hospital of ShiftManager in {ShiftScenePath}. Draai eerst Hospital > Build en Setup > 8. Build Shift Hospital.");
                return;
            }

            var root = new GameObject(RootName).transform;
            var tasks = ReadTasks(shift);
            Debug.Log($"{LogPrefix}{tasks.Count} taken: {string.Join(", ", tasks.Select(t => $"{t.Room.name} ({t.ScenarioId})"))}");

            WireStaffStation(root, hospital, shift);
            WireGuideLights(root, hospital, shift, tasks);
            WireCase1(root, tasks);
            WireCorridor(root, tasks);
            WireEavesdrop(root, tasks);
            WireAmbient(root, scene, shift);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{LogPrefix}Klaar: {RootName} in {ShiftScenePath}.");
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Hospital.Integration.Editor.ShiftIntegrationBuilder.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Wire();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        // --- Jalils taken lezen ----------------------------------------------------------

        private struct TaskInfo
        {
            public PatientRoom Room;
            public string ScenarioId;
        }

        /// <summary>Taak i van het plan hoort bij kamer i van de ShiftManager (zoals ShiftManager.AssignTasks).</summary>
        private static List<TaskInfo> ReadTasks(ShiftManager shift)
        {
            var serialized = new SerializedObject(shift);
            var plan = serialized.FindProperty("_plan").objectReferenceValue as ShiftPlan;
            var rooms = serialized.FindProperty("_rooms");
            var result = new List<TaskInfo>();
            for (var i = 0; plan != null && i < plan.Tasks.Count && i < rooms.arraySize; i++)
            {
                var room = rooms.GetArrayElementAtIndex(i).objectReferenceValue as PatientRoom;
                if (room != null)
                    result.Add(new TaskInfo { Room = room, ScenarioId = plan.Tasks[i].Scenario != null ? plan.Tasks[i].Scenario.Id : "" });
            }
            return result;
        }

        private static PatientRoom RoomFor(List<TaskInfo> tasks, string scenarioId)
        {
            var room = tasks.FirstOrDefault(t => t.ScenarioId == scenarioId).Room;
            if (room == null)
                Debug.LogWarning($"{LogPrefix}Geen taak met scenario '{scenarioId}' in het shift-plan.");
            return room;
        }

        private static T Read<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T;

        // --- 1. Verpleegpost ----------------------------------------------------------------

        private static void WireStaffStation(Transform root, Transform hospital, ShiftManager shift)
        {
            var station = hospital.Find("Lobby/StaffStation");
            if (station == null)
            {
                Debug.LogWarning($"{LogPrefix}Lobby/StaffStation ontbreekt; StaffStationBridge overgeslagen.");
                return;
            }
            var results = Object.FindFirstObjectByType<ResultsScreen>(FindObjectsInactive.Include);
            var bridge = new GameObject("StaffStationBridge").AddComponent<StaffStationBridge>();
            bridge.transform.SetParent(root, false);
            bridge.Configure(shift, station.Find("Badge_Reader/Lamp")?.GetComponent<Renderer>(),
                station.Find("Clipboard"), station.Find("Anchor_Clipboard"),
                station.Find("Care_Tablet"), station.Find("Anchor_TabletDock"),
                results, station.Find("Anchor_EndScreen"));
            if (results == null)
                Debug.LogWarning($"{LogPrefix}Geen ResultsScreen gevonden; het eindscherm wordt niet verzet.");
        }

        // --- 2. Vloerstroken ----------------------------------------------------------------

        private static void WireGuideLights(Transform root, Transform hospital, ShiftManager shift, List<TaskInfo> tasks)
        {
            var all = Enumerable.Range(1, 3).Select(f => hospital.Find($"Floor{f}/GuideLights")).Where(g => g != null)
                .SelectMany(g => g.GetComponentsInChildren<FloorGuideLight>(true)).ToArray();
            var pairs = new List<GuideLightBridge.RoomGuide>();
            foreach (var task in tasks)
            {
                var label = GuideLabel(task);
                var guide = hospital.Find($"Floor{label[0]}/GuideLights/Guide_{label}")?.GetComponent<FloorGuideLight>();
                if (guide == null)
                {
                    Debug.LogWarning($"{LogPrefix}Geen vloerstrook Guide_{label} voor {task.Room.name}.");
                    continue;
                }
                pairs.Add(new GuideLightBridge.RoomGuide { Room = task.Room, Guide = guide });
                Debug.Log($"{LogPrefix}Vloerstrook: {task.Room.name} ({task.ScenarioId}) -> Guide_{label}");
            }
            var bridge = new GameObject("GuideLightBridge").AddComponent<GuideLightBridge>();
            bridge.transform.SetParent(root, false);
            bridge.Configure(shift, pairs, all);
        }

        /// <summary>Room_1A -> "1A"; anders volgens het scenario; anders de dichtste deur op dezelfde verdieping.</summary>
        private static string GuideLabel(TaskInfo task)
        {
            var name = task.Room.name;
            if (name.StartsWith("Room_") && name.Length == 7)
                return name.Substring(5);
            if (GuideByScenario.TryGetValue(task.ScenarioId, out var label))
                return label;
            var p = task.Room.transform.position;
            var floor = Mathf.Clamp(Mathf.RoundToInt(p.y / FloorHeight), 1, 3);
            var door = Enumerable.Range(0, DoorCentersX.Length).OrderBy(i => Mathf.Abs(DoorCentersX[i] - p.x)).First();
            return $"{floor}{DoorLetters[door]}";
        }

        // --- 3. Taak 1: pillenstrip --------------------------------------------------------------

        private static void WireCase1(Transform root, List<TaskInfo> tasks)
        {
            var room = RoomFor(tasks, Scenario1);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Case1PropsPath);
            if (room == null || prefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Case1Bridge overgeslagen (kamer of {Case1PropsPath} ontbreekt; draai Hospital > Build Props Test).");
                return;
            }
            // In de ruimte van de kamer (deurmuur op z = 0), maar onder Liam_Integration
            var props = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            props.transform.SetPositionAndRotation(room.transform.position, room.transform.rotation);
            var bridge = props.AddComponent<Case1Bridge>();
            bridge.Configure(Read<ScenarioPresenter>(room, "_presenter"), props.GetComponent<ItemSpots>());
        }

        // --- 4. Taak 2: Porter achter de brancard -------------------------------------------------

        private static void WireCorridor(Transform root, List<TaskInfo> tasks)
        {
            var room = RoomFor(tasks, Scenario2);
            var porterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PorterPath);
            if (room == null || room.Countdown == null || porterPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix}CorridorBridge overgeslagen (taak 2, countdown of Porter ontbreekt).");
                return;
            }
            var gurney = Read<PathMover>(room.Countdown, "_arrival");
            if (gurney == null)
                Debug.LogWarning($"{LogPrefix}De countdown van taak 2 heeft geen brancard; de Porter volgt niets.");

            var porter = (GameObject)PrefabUtility.InstantiatePrefab(porterPrefab, root);
            porter.name = "Porter";
            var follow = 1.5f;
            if (gurney != null)
            {
                porter.transform.SetPositionAndRotation(gurney.transform.position, gurney.transform.rotation);
                var bounds = HospitalAssetPrep.WorldBounds(gurney.gameObject);
                follow = Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.45f;
            }
            var bridge = new GameObject("CorridorBridge").AddComponent<CorridorBridge>();
            bridge.transform.SetParent(root, false);
            bridge.Configure(room.Countdown, gurney != null ? gurney.transform : null, null, porter.GetComponent<Animator>(), follow);
        }

        // --- 5. Taak 5: de buurman luistert mee -----------------------------------------------------

        private static void WireEavesdrop(Transform root, List<TaskInfo> tasks)
        {
            var room = RoomFor(tasks, Scenario5);
            var neighbour = room != null ? room.transform.Find("Next Patient") : null;
            var animator = neighbour != null ? neighbour.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman) : null;
            if (animator == null)
            {
                Debug.LogWarning($"{LogPrefix}EavesdropBridge overgeslagen (taak 5 of 'Next Patient' met een Humanoid Animator ontbreekt).");
                return;
            }
            var papers = neighbour.GetComponentsInChildren<Transform>(true);
            var bridge = new GameObject("EavesdropBridge").AddComponent<EavesdropBridge>();
            bridge.transform.SetParent(root, false);
            bridge.Configure(Read<ScenarioPresenter>(room, "_presenter"), animator,
                papers.FirstOrDefault(t => t.name == "Newspaper (reading)")?.gameObject,
                papers.FirstOrDefault(t => t.name == "Newspaper (lowered)")?.gameObject);
        }

        // --- 6. Rondrijdende kar ------------------------------------------------------------------

        private static void WireAmbient(Transform root, UnityEngine.SceneManagement.Scene scene, ShiftManager shift)
        {
            var driver = new GameObject("AmbientBridge");
            driver.transform.SetParent(root, false);
            var paths = new GameObject("AmbientPaths").transform;
            paths.SetParent(root, false);

            // Jalils personages: een gang waar er een op de route staat, valt af
            var blockers = scene.GetRootGameObjects().Where(g => g.name != "Hospital" && g.name != RootName)
                .SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Select(r => r.bounds).ToList();
            var corridors = new List<AmbientBridge.CorridorPath>();
            for (var floor = 1; floor <= 3; floor++)
            {
                var y = floor * FloorHeight + FloorFinish;
                var lane = new Bounds(new Vector3((AmbientStartX + AmbientEndX) / 2f, y + 0.6f, AmbientLaneZ),
                    new Vector3(AmbientStartX - AmbientEndX, 1.2f, 0.7f));
                if (blockers.Any(b => b.Intersects(lane)))
                {
                    Debug.Log($"{LogPrefix}Gang van verdieping {floor}: een personage van Jalil staat op de route; geen kar.");
                    continue;
                }
                var path = new GameObject($"Corridor_Floor{floor}").transform;
                path.SetParent(paths, false);
                var start = new GameObject("Waypoint_1").transform;
                start.SetParent(path, false);
                start.position = new Vector3(AmbientStartX, y, AmbientLaneZ);
                var end = new GameObject("Waypoint_2").transform;
                end.SetParent(path, false);
                end.position = new Vector3(AmbientEndX, y, AmbientLaneZ);
                corridors.Add(new AmbientBridge.CorridorPath { Name = path.name, Waypoints = new[] { start, end } });
            }

            var cartPrefab = PropSetup.LoadPrefab("Supply_Cart");
            var cart = (GameObject)PrefabUtility.InstantiatePrefab(cartPrefab, root);
            cart.name = "Ambient_Supply_Cart";
            if (corridors.Count > 0)
                cart.transform.position = corridors[0].Waypoints[0].position;
            var passing = driver.AddComponent<PassingCart>();
            driver.AddComponent<AmbientBridge>().Configure(shift, passing, cart.transform, corridors.ToArray());
            Debug.Log($"{LogPrefix}Ambient kar in {corridors.Count} gang(en): {string.Join(", ", corridors.Select(c => c.Name))}");
        }
    }
}
