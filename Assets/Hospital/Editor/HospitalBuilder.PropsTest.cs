using ProCommVR.Hospital;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Testscene Assets/Scenes/liam_props_test.unity voor de losse stukken van de 360°-afleidingen (ItemSpots, RoomTint,
    /// EarGrow, MonitorScreen, PassingCart), los van de kamers die Jalil gebruikt. Maakt ook de prefabs
    /// Case1_Props en Vitals_Monitor_Live. Elk stuk heeft testknoppen in de Inspector.
    /// </summary>
    public static partial class HospitalBuilder
    {
        private const string PropsTestScenePath = "Assets/Scenes/liam_props_test.unity";
        private const string Case1PropsPath = PrefabFolder + "/Case1_Props.prefab";
        private const string MonitorLivePath = PrefabFolder + "/Vitals_Monitor_Live.prefab";
        private const string NeighborPrefab = "Patient_Male_Old";

        // Lokale assen van een prop-root (root 90 graden rond X uit de FBX-conversie): Blender (x, y, z) = lokaal (x, y, -z)
        private static Vector3 PropLocal(float x, float y, float z) => new Vector3(x, y, -z);

        [MenuItem("ProCommVR/Hospital/Build Props Test")]
        public static void BuildPropsTest()
        {
            PropSetup.EnsureSetup();
            var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
            if (roomPrefab == null)
            {
                Debug.LogError($"{LogPrefix}{RoomPrefabPath} ontbreekt. Draai eerst ProCommVR > Hospital > Build.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PropsTest").transform;
            SetupLighting(scene, root);

            var room1 = PlaceTestRoom(roomPrefab, root, "Room_Case1", new Vector3(0f, 0f, 0f));
            BuildCase1Props(room1);
            room1.gameObject.AddComponent<RoomTint>();
            PlaceMonitor(room1, new Vector3(0.85f, 0f, 3.6f));

            var room5 = PlaceTestRoom(roomPrefab, root, "Room_Case5", new Vector3(6f, 0f, 0f));
            BuildCase5Neighbor(room5, root);

            BuildTestCorridor(root, new Vector3(0f, 0f, -5.5f));

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.SetParent(root, false);
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(3f, 5.5f, -10f), Quaternion.LookRotation(new Vector3(3f, -5f, 10f)));

            EditorSceneManager.SaveScene(scene, PropsTestScenePath);
            BeginPosePreview();
            try
            {
                Shot("props_test_case1.png", room1.TransformPoint(new Vector3(-0.3f, EyeHeight, 0.5f)), room1.TransformPoint(new Vector3(-0.6f, 0.8f, 3.2f)));
                Shot("props_test_case5.png", room5.TransformPoint(new Vector3(0.2f, 1.5f, 0.9f)), room5.TransformPoint(new Vector3(0.9f, 1.0f, 3.0f)));
                Shot("props_test_corridor.png", new Vector3(-2f, 2.4f, -6.5f), new Vector3(8f, 0.2f, -4f));
            }
            finally
            {
                EndPosePreview();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}Klaar: {PropsTestScenePath}, {Case1PropsPath}, {MonitorLivePath}.");
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.HospitalBuilder.RunPropsTestBatch</summary>
        public static void RunPropsTestBatch()
        {
            try
            {
                BuildPropsTest();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        private static Transform PlaceTestRoom(GameObject roomPrefab, Transform parent, string name, Vector3 position)
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, parent);
            room.name = name;
            room.transform.localPosition = position;
            // De deurmuur hoort bij de gang en zit niet in de prefab; in de testscene een effen muur
            Box("TestWall_Door", room.transform, new Vector3(0f, CeilingHeight / 2f, 0f),
                new Vector3(RoomWidth + 2f * WallThickness, CeilingHeight, WallThickness), Mat("RoomWall", new Color(0.92f, 0.93f, 0.90f)));
            Box("TestFloor", room.transform, new Vector3(0f, -0.05f, RoomDepth / 2f + WallThickness / 2f),
                new Vector3(RoomWidth, 0.1f, RoomDepth), Mat("Floor", new Color(0.72f, 0.75f, 0.78f)));
            Physics.SyncTransforms();
            return room.transform;
        }

        // --- Case 1: blister die verspringt --------------------------------------------------

        /// <summary>
        /// Case1_Props, in de ruimte van een PatientRoom (deurmuur op z = 0): tafel aan de linkermuur met de Lunch_Tray en de
        /// Birthday_Card, Photo_Frame aan de muur erboven, Water_Glass op het nachtkastje, en de Pill_Blister met ItemSpots
        /// tussen Spot_Tray, Spot_Nightstand en Spot_Windowsill.
        /// </summary>
        private static void BuildCase1Props(Transform room)
        {
            var props = new GameObject("Case1_Props").transform;
            props.SetParent(room, false);

            var half = RoomWidth / 2f;
            var tableCenter = new Vector3(-half + 0.4f, 0f, 1.9f);
            var table = PlaceKenney("table", props, "Table", tableCenter, 90f);
            var tableTop = TopAt(table, room.TransformPoint(tableCenter).x, room.TransformPoint(tableCenter).z);
            var tableBounds = HospitalAssetPrep.WorldBounds(table);
            tableCenter = room.InverseTransformPoint(new Vector3(tableBounds.center.x, tableTop, tableBounds.center.z));
            var towardBed = Vector3.right;

            var tray = PlaceProp("Lunch_Tray", props, tableCenter + new Vector3(0f, 0f, 0.2f), towardBed);
            PlaceProp("Birthday_Card", props, tableCenter + new Vector3(0f, 0f, -0.32f), towardBed);
            PlaceProp("Photo_Frame", props, new Vector3(-half + 0.06f, 1.45f, tableCenter.z), towardBed);

            var stand = room.Find("Furniture/Nightstand");
            var sb = HospitalAssetPrep.WorldBounds(stand.gameObject);
            var standTop = room.InverseTransformPoint(new Vector3(sb.center.x, TopAt(stand.gameObject, sb.center.x, sb.center.z), sb.center.z));
            PlaceProp("Water_Glass", props, standTop + new Vector3(0.08f, 0f, -0.05f), Vector3.back);

            var window = room.Find("Window");
            var sillPoint = room.TransformPoint(new Vector3(half + WallThickness * 0.4f, 0f, WindowCenterFromDoor));
            var sillTop = TopAt(window.gameObject, sillPoint.x, sillPoint.z);
            var sill = room.InverseTransformPoint(new Vector3(sillPoint.x, float.IsNaN(sillTop) ? WindowSill : sillTop, sillPoint.z));

            var spots = new[]
            {
                SpotOn("Spot_Tray", props, tray.transform.TransformPoint(PropLocal(-0.05f, -0.135f, 0.012f)), 90f),
                SpotOn("Spot_Nightstand", props, room.TransformPoint(standTop + new Vector3(-0.08f, 0f, 0.05f)), 15f),
                SpotOn("Spot_Windowsill", props, room.TransformPoint(sill), 0f),
            };
            var blister = PlaceProp("Pill_Blister", props, props.InverseTransformPoint(spots[0].position), spots[0].rotation * PropFront);
            props.gameObject.AddComponent<ItemSpots>().Configure(blister.transform, spots, 0);

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAssetAndConnect(props.gameObject, Case1PropsPath, InteractionMode.AutomatedAction);
        }

        private static Transform SpotOn(string name, Transform parent, Vector3 worldPosition, float yaw)
        {
            var spot = new GameObject(name).transform;
            spot.SetParent(parent, false);
            spot.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, yaw, 0f));
            return spot;
        }

        // --- Monitor met hartslaglijn --------------------------------------------------------

        /// <summary>Vitals_Monitor met MonitorScreen en AudioSource, bewaard als prefab-variant Vitals_Monitor_Live.</summary>
        private static void PlaceMonitor(Transform room, Vector3 localPosition)
        {
            var monitor = PlaceProp("Vitals_Monitor", room, localPosition, Vector3.back);
            monitor.name = "Vitals_Monitor_Live";
            var audio = monitor.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.maxDistance = 8f;
            var screen = monitor.AddComponent<MonitorScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("_screen").objectReferenceValue = monitor.transform.Find("Screen").GetComponent<Renderer>();
            serialized.FindProperty("_lineMaterial").objectReferenceValue = MonitorLineMaterial();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(monitor, MonitorLivePath, InteractionMode.AutomatedAction);
        }

        /// <summary>Unlit met vertexkleur, zodat de LineRenderer zijn kleur (groen of rood) zelf bepaalt.</summary>
        private static Material MonitorLineMaterial()
        {
            var path = $"{MaterialFolder}/MonitorLine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // --- Case 5: buurman met krant en tv --------------------------------------------------

        /// <summary>
        /// De buurman zit op de stoel van de kamer (zoals Liam's patiënten), met een krant voor zich en een Wall_TV op de
        /// deurmuur tegenover hem. EarGrow laat zijn rechteroor (naar het bed) groeien en de krant zakken.
        /// </summary>
        private static void BuildCase5Neighbor(Transform room, Transform root)
        {
            var group = new GameObject("Case5_Neighbor").transform;
            group.SetParent(root, false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{NeighborPrefab}/{NeighborPrefab}.prefab");
            var neighbor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            neighbor.name = "Neighbor";
            neighbor.AddComponent<CharacterPose>().Configure("Sitting", 0f);

            Transform paper = null;
            try
            {
                var probe = Probe(prefab, neighbor.name);
                if (SamplePose(probe, "Sitting", 0f))
                {
                    SitOnChair(probe, room, "Case5");
                    neighbor.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
                    paper = PlaceNewspaper(group, probe.GetComponent<Animator>());
                }
            }
            finally
            {
                StopSamplingKeepRoots();
                foreach (var p in _probes)
                    Object.DestroyImmediate(p);
                _probes.Clear();
            }

            var ear = neighbor.AddComponent<EarGrow>();
            var serialized = new SerializedObject(ear);
            serialized.FindProperty("_newspaper").objectReferenceValue = paper;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PlaceProp("Wall_TV", room, new Vector3(0.6f, 1.3f, WallThickness / 2f), Vector3.forward);
        }

        /// <summary>Krant in de handen: een houder voor de borst, rechtop gekanteld naar de lezer. EarGrow beweegt de houder.</summary>
        private static Transform PlaceNewspaper(Transform parent, Animator posed)
        {
            var left = posed.GetBoneTransform(HumanBodyBones.LeftHand).position;
            var right = posed.GetBoneTransform(HumanBodyBones.RightHand).position;
            var chest = posed.GetBoneTransform(HumanBodyBones.Chest).position;
            var forward = Vector3.ProjectOnPlane(Vector3.Cross(right - left, Vector3.up), Vector3.up).normalized;
            if (Vector3.Dot(forward, Vector3.ProjectOnPlane((left + right) / 2f - chest, Vector3.up)) < 0f)
                forward = -forward;

            var holder = new GameObject("NewspaperHolder").transform;
            holder.SetParent(parent, false);
            var hands = (left + right) / 2f;
            holder.SetPositionAndRotation(new Vector3(hands.x, chest.y - 0.05f, hands.z) + forward * 0.12f,
                Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-70f, 0f, 0f));
            // Krant plat in de houder (kop naar de +z van de houder); de houder kantelt hem rechtop, kop boven
            PlaceProp("Newspaper", holder, Vector3.zero, Vector3.back);
            return holder;
        }

        // --- Gang met rijdende karren ---------------------------------------------------------

        private static void BuildTestCorridor(Transform root, Vector3 origin)
        {
            var corridor = new GameObject("Corridor").transform;
            corridor.SetParent(root, false);
            corridor.localPosition = origin;
            Box("Floor", corridor, new Vector3(CorridorLength / 2f, -0.05f, CorridorWidth / 2f),
                new Vector3(CorridorLength, 0.1f, CorridorWidth), Mat("Floor", new Color(0.72f, 0.75f, 0.78f)));

            // Kar: handvat (Blender +X) achteraan, rijdt dus met lokaal -X voorop; maakt een bocht naar de overkant
            AddPassingCart(corridor, "Supply_Cart", new Vector3(-1f, 0f, 0f), 1f,
                new Vector3(1f, 0f, 0.9f), new Vector3(10f, 0f, 0.9f), new Vector3(15f, 0f, 2.0f));
            // Brancard: hoofdeinde (Blender +X) voorop
            AddPassingCart(corridor, "Gurney", new Vector3(1f, 0f, 0f), 0.8f,
                new Vector3(17f, 0f, 2.0f), new Vector3(2.5f, 0f, 2.0f));
        }

        private static void AddPassingCart(Transform corridor, string prop, Vector3 localForward, float speed, params Vector3[] points)
        {
            var path = new GameObject($"Path_{prop}").transform;
            path.SetParent(corridor, false);
            var waypoints = new Transform[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                waypoints[i] = new GameObject($"Waypoint_{i + 1}").transform;
                waypoints[i].SetParent(path, false);
                waypoints[i].localPosition = points[i];
            }
            var cart = PlaceProp(prop, corridor, points[0], Vector3.back);
            path.gameObject.AddComponent<PassingCart>().Configure(cart.transform, waypoints, localForward, speed);
        }
    }
}
