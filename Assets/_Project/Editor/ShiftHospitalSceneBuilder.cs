using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Atmosphere;
using ProCommVR.Navigation;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds ShiftHospital: the shift game loop in Liam's hospital. Opens Liam's scene (built by
    /// ProCommVR > Hospital > Build) as a template, adds the shift and saves the result as ShiftHospital; Liam's
    /// scene itself is not saved. The lobby is the briefing room (start computer on the reception desk), the
    /// patients are in rooms 1A-1C (floor 1) and 2A-2B (floor 2), and the lift buttons take the player between floors.
    /// Also creates the placeholder shift plan (Scenario 1 five times).
    /// Batchmode: -executeMethod ProCommVR.Editor.ShiftHospitalSceneBuilder.BuildScene
    /// Requires the prefabs from ScenarioTestSceneBuilder (bubble, results screen), the XR rig variant and Liam's scene.
    /// </summary>
    public static class ShiftHospitalSceneBuilder
    {
        private const string HospitalScenePath = "Assets/Scenes/liam_test_scene.unity";
        private const string ScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const string PlanPath = "Assets/_Project/Dialogue/Shifts/Shift_Placeholder.asset";
        private const string ScenarioPath = "Assets/_Project/Dialogue/Scenarios/Scenario01_DisappointedPatient.asset";
        private const string ResultsPrefabPath = "Assets/_Project/Prefabs/UI/ResultsScreen.prefab";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";
        private const string CharacterFolder = "Assets/Character";

        // Layout of Liam's hospital (HospitalBuilder): a root "Hospital" with the levels "Lobby", "Floor1".."Floor3",
        // 3.5 m apart. The lift is in the west wall (x = 0) at z = 1.5, the corridors run along +x.
        private const string HospitalRootName = "Hospital";
        private const float FloorHeight = 3.5f;
        private const int FloorCount = 3;
        private const float LiftCenterZ = 1.5f;
        private const float LiftArrivalX = 1.2f;
        private const float CorridorBoardX = 9f;    // opposite door B, between the staff Liam placed in the corridors
        private const float LobbyBoardX = 7.3f;     // on the north wall behind the reception desk, right of the "Lobby" sign
        private const float LobbyBoardScale = 1.4f; // read from the start position, about 3 m away
        private const int TaskCount = 5;

        // The shift rooms, in task order: task 1 goes to room 1A, ... The other rooms keep Liam's cards.
        private static readonly string[] RoomLabels = { "1A", "1B", "1C", "2A", "2B" };

        // One patient model per room; the shift plan uses Scenario 1 everywhere for now.
        private static readonly string[] PatientModels =
            { "Patient_Male", "Patient_Female_Old", "Patient_Male_Old", "Patient_Female", "Patient_Male_Old" };

        private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.14f);

        [MenuItem("ProCommVR/Setup/8. Build Shift Hospital")]
        public static void BuildScene()
        {
            if (!File.Exists(HospitalScenePath))
                throw new FileNotFoundException($"[ProCommVR] Liam's hospital scene is missing. Pull it from Liam's branch.", HospitalScenePath);

            // Open the scene first: opening a scene unloads assets that were loaded before it.
            var scene = EditorSceneManager.OpenScene(HospitalScenePath, OpenSceneMode.Single);
            var hospital = scene.GetRootGameObjects().FirstOrDefault(g => g.name == HospitalRootName)?.transform;
            if (hospital == null)
                throw new System.InvalidOperationException($"[ProCommVR] No '{HospitalRootName}' in {HospitalScenePath}. Run 'ProCommVR > Hospital > Build' first.");

            var rigVariant = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.RigVariantPath, "Run 'Build XR Test Scene' first.");
            var resultsPrefab = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ResultsPrefabPath, "Run 'Build Results Screen Prefab' first.");
            var plan = GetOrCreatePlan();

            // The XR rig brings its own camera.
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Object.DestroyImmediate(camera.gameObject);

            // The ward's own mood (Neutral) sets the main light and ambient light at runtime, through the same mood
            // system as the scenario scenes. The patient moods only color the room lights and door signs.
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            var wardMood = new GameObject("Ward Mood");
            var mood = ScenarioTestSceneBuilder.AddMoodController(wardMood, null);
            var serializedMood = new SerializedObject(mood);
            serializedMood.FindProperty("_applyInitialMood").boolValue = true;
            serializedMood.FindProperty("_initialMood").enumValueIndex = (int)Mood.Neutral;
            serializedMood.ApplyModifiedPropertiesWithoutUndo();
            ScenarioTestSceneBuilder.AddMoodAtmosphere(wardMood, mood, sun);

            var shift = new GameObject("Shift").transform;
            var shiftManager = new GameObject("Shift Manager").AddComponent<ShiftManager>();
            var start = BuildBriefing(hospital, shift, shiftManager);

            var rooms = new List<PatientRoom>();
            for (var i = 0; i < RoomLabels.Length; i++)
                rooms.Add(BuildPatientRoom(hospital, i, RoomLabels[i]));
            CloseOtherDoors(hospital);
            BuildLift(hospital, shift);

            // A shift board in the corridor of every floor with patients, readable while walking between the rooms.
            foreach (var floor in RoomLabels.Select(FloorOf).Distinct())
            {
                var wall = RendererBounds(RequireChild(hospital, $"Floor{floor}/Wall_South").gameObject);
                BuildShiftBoard(shift, $"Shift Board (floor {floor})", shiftManager,
                    new Vector3(CorridorBoardX, floor * FloorHeight + 1.55f, wall.max.z + 0.02f), Quaternion.LookRotation(Vector3.back));
            }

            var results = (GameObject)PrefabUtility.InstantiatePrefab(resultsPrefab, scene);
            results.SetActive(false);
            var serialized = new SerializedObject(results.GetComponent<ResultsScreen>());
            serialized.FindProperty("_title").stringValue = "Shift report";
            serialized.FindProperty("_restartLabel").stringValue = "Close game";
            // Closer than the default, so the report appears in front of the patient the player was talking to.
            serialized.FindProperty("_distance").floatValue = 0.85f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized = new SerializedObject(shiftManager);
            serialized.FindProperty("_plan").objectReferenceValue = plan;
            serialized.FindProperty("_resultsScreen").objectReferenceValue = results.GetComponent<ResultsScreen>();
            var roomList = serialized.FindProperty("_rooms");
            roomList.arraySize = rooms.Count;
            for (var i = 0; i < rooms.Count; i++)
                roomList.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The player starts in the lobby, facing the reception desk with the computer that starts the shift.
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, scene);
            rig.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.forward));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            Debug.Log($"[ProCommVR] Shift hospital built: {CountTriangles()} triangles in the scene (Quest 3 target: under about 100k).");
            // Saved under a new path: Liam's scene on disk stays as it is.
            ScenarioTestSceneBuilder.SaveSceneFirstInBuild(scene, ScenePath);
        }

        // ---- Briefing (lobby) ---------------------------------------------------------------------------------

        /// <summary>
        /// The lobby is the briefing room: the start computer on the visitor side of the reception desk and a shift
        /// board on the north wall. Returns the start position of the player, in front of the desk.
        /// </summary>
        private static Vector3 BuildBriefing(Transform hospital, Transform shift, ShiftManager shiftManager)
        {
            var root = new GameObject("Briefing (lobby)").transform;
            root.SetParent(shift, false);

            var desk = Scenario01SceneBuilder.TightBounds(RequireChild(hospital, "Lobby/Furniture/ReceptionDesk").gameObject);
            BuildStartComputer(root, desk, shiftManager);

            var northWall = RendererBounds(RequireChild(hospital, "Lobby/Wall_North").gameObject);
            var board = BuildShiftBoard(root, "Shift Board (briefing)", shiftManager,
                new Vector3(LobbyBoardX, 1.9f, northWall.min.z - 0.02f), Quaternion.LookRotation(Vector3.forward));
            board.localScale *= LobbyBoardScale;

            return new Vector3(desk.center.x, 0f, desk.min.z - 1.3f);
        }

        /// <summary>
        /// The computer on the desk, facing the player (south): point at it with the controller ray and pull the
        /// trigger to start the shift.
        /// </summary>
        private static void BuildStartComputer(Transform parent, Bounds desk, ShiftManager shiftManager)
        {
            var towardPlayer = Quaternion.LookRotation(Vector3.back);
            var screen = PlaceKenney(parent, "computerScreen", "Computer (start shift)",
                new Vector3(desk.center.x, desk.max.y, desk.center.z + 0.05f), towardPlayer, 0.45f, false);
            PlaceKenney(parent, "computerKeyboard", "Keyboard", new Vector3(desk.center.x, desk.max.y, desk.min.z + 0.15f), towardPlayer, 0.03f, false);

            var bounds = Scenario01SceneBuilder.TightBounds(screen);
            var collider = screen.AddComponent<BoxCollider>();
            collider.center = screen.transform.InverseTransformPoint(bounds.center);
            var localSize = screen.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            screen.AddComponent<XRSimpleInteractable>();

            var label = CreateWorldText(parent, "Start Shift Label", new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z),
                Quaternion.LookRotation(Vector3.forward), 0.6f);
            var button = screen.AddComponent<ShiftStartButton>();
            var serialized = new SerializedObject(button);
            serialized.FindProperty("_shiftManager").objectReferenceValue = shiftManager;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Patient rooms --------------------------------------------------------------------------------------

        /// <summary>
        /// Turns one of Liam's rooms into a shift room. Liam's room has its origin in the middle of the doorway,
        /// forward into the room, the bed against the back wall, a chair right of the bed and the window in the right
        /// wall. The room keeps its furniture and light; Liam's patient card and patient for this door make way
        /// for the door sign and the patient of the shift.
        /// </summary>
        private static PatientRoom BuildPatientRoom(Transform hospital, int index, string label)
        {
            var floor = $"Floor{FloorOf(label)}";
            var room = RequireChild(hospital, $"{floor}/Room_{label}");

            var card = RequireChild(hospital, $"{floor}/Card_{label}");
            var signPosition = card.position - room.forward * 0.03f;
            Object.DestroyImmediate(card.gameObject);
            var liamsPatient = hospital.Find($"Characters/Patient_{label}");
            if (liamsPatient != null)
                Object.DestroyImmediate(liamsPatient.gameObject);
            OpenDoor(RequireChild(hospital, $"{floor}/Door_{label}"), room);

            // The patient sits on the chair, turned to the door where the player comes in (as Liam's own patients).
            var chair = RequireChild(room, "Furniture/Chair");
            var seat = Scenario01SceneBuilder.TightBounds(chair.gameObject).center;
            var towardDoor = Flat(room.position - seat).normalized;
            chair.RotateAround(seat, Vector3.up, Vector3.SignedAngle(RequireChild(room, "Anchor_Sitting").forward, towardDoor, Vector3.up));
            var patient = PlacePatient(room, PatientModels[index % PatientModels.Length],
                new Vector3(seat.x, room.position.y, seat.z), Quaternion.LookRotation(towardDoor));

            // Mood: the data (MoodController), its sound (3D, only audible in and right outside this room) and the
            // room's own ceiling light, which takes the mood color.
            var moodObject = new GameObject("Patient Mood");
            moodObject.transform.SetParent(room, false);
            moodObject.transform.localPosition = new Vector3(0.5f, 1.5f, 2.2f);
            var mood = ScenarioTestSceneBuilder.AddMoodController(moodObject, null);
            ScenarioTestSceneBuilder.AddMoodAudio(moodObject, mood, true, 4f);
            var roomLight = RequireChild(room, "Light").gameObject.AddComponent<MoodRoomLight>();
            var serialized = new SerializedObject(roomLight);
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var sign = BuildRoomSign(room, signPosition, Quaternion.LookRotation(room.forward), mood);

            var head = patient.position + Vector3.up * 1.75f;
            var presenter = ScenarioTestSceneBuilder.CreatePresenter("Scenario Presenter", patient, head, room.TransformPoint(0.3f, 1.15f, 1.6f));
            presenter.transform.SetParent(room, true);
            presenter.transform.Find("NPC Bubble Anchor").rotation = Quaternion.LookRotation(Flat(head - room.TransformPoint(0f, 0f, 0.3f)));
            // Body language after a choice: to the nurse (the door), away from the nurse (to the bed), or to the window.
            AddReactor(room, presenter, patient,
                Quaternion.LookRotation(towardDoor),
                Quaternion.LookRotation(-room.right + room.forward * 0.3f),
                Quaternion.LookRotation(room.right - room.forward * 0.3f));

            var patientRoom = room.gameObject.AddComponent<PatientRoom>();
            serialized = new SerializedObject(patientRoom);
            serialized.FindProperty("_roomName").stringValue = $"Room {label}";
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.FindProperty("_sign").objectReferenceValue = sign;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patientRoom;
        }

        /// <summary>
        /// Liam's doors are closed models without a collider. A shift room's door swings open into the room, on the
        /// hinge at its left side seen from the corridor.
        /// </summary>
        private static void OpenDoor(Transform door, Transform room)
        {
            var bounds = Scenario01SceneBuilder.TightBounds(door.gameObject);
            var halfWidth = Mathf.Abs(room.right.x) > 0.5f ? bounds.extents.x : bounds.extents.z;
            var hinge = bounds.center - room.right * halfWidth;
            door.RotateAround(hinge, Vector3.up, Vector3.SignedAngle(room.right, room.forward, Vector3.up));
        }

        /// <summary>
        /// The doors of the rooms without a shift patient stay closed and get a collider, so nobody walks through them.
        /// </summary>
        private static void CloseOtherDoors(Transform hospital)
        {
            var open = RoomLabels.Select(label => $"Door_{label}").ToHashSet();
            foreach (var door in hospital.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Door_") && !open.Contains(t.name)))
                AddBoxCollider(door.gameObject, Scenario01SceneBuilder.TightBounds(door.gameObject));
        }

        /// <summary>
        /// One of Liam's characters, sitting (Animator state "Sitting"), root on the floor at the chair.
        /// </summary>
        private static Transform PlacePatient(Transform parent, string model, Vector3 position, Quaternion facing)
        {
            var path = $"{CharacterFolder}/{model}/{model}.prefab";
            var prefab = ScenarioTestSceneBuilder.LoadRequired<GameObject>(path, "Character missing; pull Assets/Character from Liam's branch.");
            var patient = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            patient.name = $"Patient ({model})";
            patient.transform.SetParent(parent, false);
            patient.transform.SetPositionAndRotation(position, facing);

            var animator = patient.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            var state = animator.gameObject.AddComponent<NpcAnimationState>();
            var serialized = new SerializedObject(state);
            serialized.FindProperty("_stateName").stringValue = "Sitting";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patient.transform;
        }

        private static void AddReactor(Transform root, ScenarioPresenter presenter, Transform patient,
            Quaternion engaged, Quaternion turnsAway, Quaternion withdrawn)
        {
            var poses = new GameObject("Poses").transform;
            poses.SetParent(root, false);
            var targets = new List<KeyValuePair<NpcPose, Transform>>
            {
                new KeyValuePair<NpcPose, Transform>(NpcPose.Engaged, CreatePose(poses, "Engaged", patient.position, engaged)),
                new KeyValuePair<NpcPose, Transform>(NpcPose.TurnsAway, CreatePose(poses, "Turns Away", patient.position, turnsAway)),
                new KeyValuePair<NpcPose, Transform>(NpcPose.Withdrawn, CreatePose(poses, "Withdrawn", patient.position, withdrawn)),
            };

            var reactor = patient.gameObject.AddComponent<NpcReactor>();
            var voice = patient.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            var serialized = new SerializedObject(reactor);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_body").objectReferenceValue = patient;
            serialized.FindProperty("_voice").objectReferenceValue = voice;
            var poseList = serialized.FindProperty("_poses");
            poseList.arraySize = targets.Count;
            for (var i = 0; i < targets.Count; i++)
            {
                var element = poseList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Pose").enumValueIndex = (int)targets[i].Key;
                element.FindPropertyRelative("Target").objectReferenceValue = targets[i].Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreatePose(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var pose = new GameObject(name).transform;
            pose.SetParent(parent, false);
            pose.SetPositionAndRotation(position, rotation);
            return pose;
        }

        // ---- Lift -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Makes the buttons of Liam's lift panels work: each one takes the player to the arrival point in front of
        /// the lift on its floor ("L" is the lobby).
        /// </summary>
        private static void BuildLift(Transform hospital, Transform shift)
        {
            var arrivals = new GameObject("Lift Arrivals").transform;
            arrivals.SetParent(shift, false);
            var destinations = new Transform[FloorCount + 1];
            for (var level = 0; level <= FloorCount; level++)
            {
                destinations[level] = new GameObject($"Arrival {LevelLabel(level)}").transform;
                destinations[level].SetParent(arrivals, false);
                destinations[level].SetPositionAndRotation(new Vector3(LiftArrivalX, level * FloorHeight, LiftCenterZ), Quaternion.LookRotation(Vector3.right));
            }

            for (var level = 0; level <= FloorCount; level++)
            {
                var lift = RequireChild(hospital, $"{(level == 0 ? "Lobby" : $"Floor{level}")}/Lift");
                for (var target = 0; target <= FloorCount; target++)
                {
                    var button = lift.Find($"LiftButton_{LevelLabel(target)}");
                    if (button == null)
                        continue;
                    button.gameObject.AddComponent<XRSimpleInteractable>();
                    var serialized = new SerializedObject(button.gameObject.AddComponent<LiftButton>());
                    serialized.FindProperty("_destination").objectReferenceValue = destinations[target];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static string LevelLabel(int level)
        {
            return level == 0 ? "L" : level.ToString();
        }

        private static int FloorOf(string roomLabel)
        {
            return int.Parse(roomLabel.Substring(0, 1));
        }

        // ---- World UI -----------------------------------------------------------------------------------------

        /// <summary>
        /// Door sign on the corridor side, right of the door (where Liam's patient card was): room, patient, scenario
        /// and a mood color patch.
        /// </summary>
        private static RoomSign BuildRoomSign(Transform parent, Vector3 position, Quaternion rotation, MoodController mood)
        {
            var canvas = CreateCanvas(parent, "Door Sign", new Vector2(420f, 220f), position, rotation, Color.white);

            var patch = new GameObject("Mood Color", typeof(RectTransform)).AddComponent<Image>();
            patch.transform.SetParent(canvas, false);
            var patchRect = patch.rectTransform;
            patchRect.anchorMin = new Vector2(0f, 0f);
            patchRect.anchorMax = new Vector2(0f, 1f);
            patchRect.pivot = new Vector2(0f, 0.5f);
            patchRect.offsetMin = new Vector2(14f, 14f);
            patchRect.offsetMax = new Vector2(104f, -14f);
            patch.color = Color.white;
            patch.raycastTarget = false;

            var label = CreateLabel(canvas, "Label", 20f, 40f, TextAlignmentOptions.Left);
            label.rectTransform.offsetMin = new Vector2(122f, 12f);
            label.rectTransform.offsetMax = new Vector2(-14f, -12f);

            var sign = canvas.gameObject.AddComponent<RoomSign>();
            var serialized = new SerializedObject(sign);
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.FindProperty("_moodPatch").objectReferenceValue = patch;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return sign;
        }

        private static RectTransform BuildShiftBoard(Transform parent, string name, ShiftManager shiftManager, Vector3 position, Quaternion rotation)
        {
            var canvas = CreateCanvas(parent, name, new Vector2(900f, 700f), position, rotation, new Color(1f, 1f, 1f, 0.97f));
            var label = CreateLabel(canvas, "Label", 18f, 44f, TextAlignmentOptions.TopLeft);
            label.rectTransform.offsetMin = new Vector2(40f, 30f);
            label.rectTransform.offsetMax = new Vector2(-40f, -30f);

            var board = canvas.gameObject.AddComponent<ShiftBoard>();
            var serialized = new SerializedObject(board);
            serialized.FindProperty("_shiftManager").objectReferenceValue = shiftManager;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return canvas;
        }

        /// <summary>
        /// A non-interactive world-space canvas (1 px = 1 mm) with a background. Rotation: the canvas front faces
        /// against its forward, so pass the direction the reader looks in.
        /// </summary>
        private static RectTransform CreateCanvas(Transform parent, string name, Vector2 size, Vector3 position, Quaternion rotation, Color background)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            var rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * 0.001f;
            rect.SetPositionAndRotation(position, rotation);
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var image = canvasObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, float minSize, float maxSize, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "";
            label.color = TextColor;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = maxSize;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Floating 3D text (no canvas), e.g. the label above the start computer.
        /// </summary>
        private static TextMeshPro CreateWorldText(Transform parent, string name, Vector3 position, Quaternion rotation, float width)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.SetPositionAndRotation(position, rotation);
            text.rectTransform.sizeDelta = new Vector2(width, 0.25f);
            text.fontSize = 1.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = TextColor;
            return text;
        }

        // ---- Assets ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The placeholder shift: Scenario 1 five times, every patient Happy, ids patient-01 to patient-05.
        /// An existing plan is kept; only empty patient ids are filled in.
        /// </summary>
        private static ShiftPlan GetOrCreatePlan()
        {
            var plan = AssetDatabase.LoadAssetAtPath<ShiftPlan>(PlanPath);
            if (plan != null)
                return FillMissingPatientIds(plan);

            var scenario = ScenarioTestSceneBuilder.LoadRequired<Scenario>(ScenarioPath, "The Scenario 1 asset is missing.");
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PlanPath));
            plan = ScriptableObject.CreateInstance<ShiftPlan>();
            AssetDatabase.CreateAsset(plan, PlanPath);

            var serialized = new SerializedObject(plan);
            var tasks = serialized.FindProperty("_tasks");
            tasks.arraySize = TaskCount;
            for (var i = 0; i < TaskCount; i++)
            {
                var task = tasks.GetArrayElementAtIndex(i);
                task.FindPropertyRelative("_patientId").stringValue = PatientId(i);
                task.FindPropertyRelative("_scenario").objectReferenceValue = scenario;
                task.FindPropertyRelative("_patientName").stringValue = "";
                task.FindPropertyRelative("_patientMood").enumValueIndex = (int)Mood.Happy;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(plan);
            return plan;
        }

        private static string PatientId(int index)
        {
            return $"patient-{index + 1:00}";
        }

        private static ShiftPlan FillMissingPatientIds(ShiftPlan plan)
        {
            var serialized = new SerializedObject(plan);
            var tasks = serialized.FindProperty("_tasks");
            for (var i = 0; i < tasks.arraySize; i++)
            {
                var id = tasks.GetArrayElementAtIndex(i).FindPropertyRelative("_patientId");
                if (string.IsNullOrWhiteSpace(id.stringValue))
                    id.stringValue = PatientId(i);
            }
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(plan);
            return plan;
        }

        /// <summary>
        /// The Kenney model inside an empty parent, so scaling and turning the parent works in world-aligned axes
        /// whatever rotation the model's own root has.
        /// </summary>
        private static GameObject InstantiateKenney(string model, Transform parent, string name)
        {
            var prefab = ScenarioTestSceneBuilder.LoadRequired<GameObject>($"{KenneyFolder}/{model}.fbx", "Kenney Furniture Kit missing, see CREDITS.md.");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(go.transform, false);
            return go;
        }

        /// <summary>
        /// A Kenney model scaled to a height, standing (or hanging, when fromTop) on the given point, its front
        /// looking along the rotation's forward. Kenney models have their front on -z, hence the half turn.
        /// </summary>
        private static GameObject PlaceKenney(Transform parent, string model, string name, Vector3 point, Quaternion rotation, float height, bool fromTop)
        {
            var go = InstantiateKenney(model, parent, name);
            go.transform.rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            Scenario01SceneBuilder.ScaleToHeight(go, height);
            Scenario01SceneBuilder.Align(go, new Vector3(0.5f, fromTop ? 1f : 0f, 0.5f), point);
            return go;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        private static Transform RequireChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child == null)
                throw new System.InvalidOperationException($"[ProCommVR] '{parent.name}/{path}' not found in Liam's hospital. Was the layout of HospitalBuilder changed?");
            return child;
        }

        private static void AddBoxCollider(GameObject go, Bounds worldBounds)
        {
            var collider = go.AddComponent<BoxCollider>();
            collider.center = go.transform.InverseTransformPoint(worldBounds.center);
            var localSize = go.transform.InverseTransformVector(worldBounds.size);
            collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static long CountTriangles()
        {
            long triangles = 0;
            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (filter.sharedMesh != null)
                    for (var i = 0; i < filter.sharedMesh.subMeshCount; i++)
                        triangles += filter.sharedMesh.GetIndexCount(i) / 3;
            }
            foreach (var skinned in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (skinned.sharedMesh != null)
                    for (var i = 0; i < skinned.sharedMesh.subMeshCount; i++)
                        triangles += skinned.sharedMesh.GetIndexCount(i) / 3;
            }
            return triangles;
        }
    }
}
