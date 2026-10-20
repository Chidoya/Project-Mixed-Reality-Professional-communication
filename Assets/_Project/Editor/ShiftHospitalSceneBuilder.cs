using System.Collections.Generic;
using ProCommVR.Atmosphere;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds ShiftHospital: a small hospital ward for the shift game loop. A corridor with a briefing room and
    /// five patient rooms, built from the Kenney Furniture Kit (walls, floors, furniture); hospital-specific props
    /// (bed, IV pole, drawers) come from the GRADD set, the patients are Liam's characters (Assets/Character).
    /// Also creates the placeholder shift plan (Scenario 1 five times).
    /// Batchmode: -executeMethod ProCommVR.Editor.ShiftHospitalSceneBuilder.BuildScene
    /// Requires the prefabs from ScenarioTestSceneBuilder (bubble, results screen) and the XR rig variant.
    ///
    /// Layout (meters, y up): corridor x 0..12, z 0..2.5. North of it rooms 1-3 (z 2.5..6.5), south of it the
    /// briefing room (x 0..4) and rooms 4-5 (z -4..0). Every room is 4 x 4 m with a 1 m doorway to the corridor.
    /// </summary>
    public static class ShiftHospitalSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const string PlanPath = "Assets/_Project/Dialogue/Shifts/Shift_Placeholder.asset";
        private const string ScenarioPath = "Assets/_Project/Dialogue/Scenarios/Scenario01_DisappointedPatient.asset";
        private const string ResultsPrefabPath = "Assets/_Project/Prefabs/UI/ResultsScreen.prefab";
        private const string MaterialFolder = "Assets/_Project/Materials/Hospital";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";
        private const string CharacterFolder = "Assets/Character";

        private const float RoomSize = 4f;
        private const float CorridorWidth = 2.5f;
        private const float WallHeight = 2.7f;
        private const float OpeningWidth = 1f;
        private const float ColliderThickness = 0.15f;
        private const float KenneyDepthScale = 0.2f; // Kenney walls are 10 units wide and 0.5 deep: 0.2 makes them 10 cm thick
        private const int Columns = 3;
        private const int TaskCount = 5;

        // One patient model per room; the shift plan uses Scenario 1 everywhere for now.
        private static readonly string[] PatientModels =
            { "Patient_Male", "Patient_Female_Old", "Patient_Male_Old", "Patient_Female", "Patient_Male_Old" };

        private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.14f);

        /// <summary>
        /// A room seen from its doorway: origin in the middle of the doorway, Forward into the room, Right along the
        /// corridor (+x, so rooms on both sides share the same lx range -1.5..2.5). lz runs 0..4 from door to back wall.
        /// </summary>
        private readonly struct RoomFrame
        {
            public RoomFrame(Vector3 door, Vector3 forward)
            {
                Door = door;
                Forward = forward;
            }

            public Vector3 Door { get; }
            public Vector3 Forward { get; }
            public Vector3 Right => Vector3.right;

            public Vector3 Point(float lx, float y, float lz) => Door + Right * lx + Vector3.up * y + Forward * lz;

            /// <summary>
            /// Rotation that looks along a direction given in room coordinates.
            /// </summary>
            public Quaternion Facing(float dx, float dz) => Quaternion.LookRotation(Right * dx + Forward * dz, Vector3.up);
        }

        [MenuItem("ProCommVR/Setup/8. Build Shift Hospital")]
        public static void BuildScene()
        {
            // Create the scene first: NewScene unloads assets that were loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rigVariant = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.RigVariantPath, "Run 'Build XR Test Scene' first.");
            var resultsPrefab = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ResultsPrefabPath, "Run 'Build Results Screen Prefab' first.");
            var plan = GetOrCreatePlan();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.52f);
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;

            // The ward's own mood (Neutral) sets the main light and ambient light at runtime, through the same mood
            // system as the scenario scenes. The patient moods only color the room lights and door signs.
            var wardMood = new GameObject("Ward Mood");
            var mood = ScenarioTestSceneBuilder.AddMoodController(wardMood, null);
            var serializedMood = new SerializedObject(mood);
            serializedMood.FindProperty("_applyInitialMood").boolValue = true;
            serializedMood.FindProperty("_initialMood").enumValueIndex = (int)Mood.Neutral;
            serializedMood.ApplyModifiedPropertiesWithoutUndo();
            ScenarioTestSceneBuilder.AddMoodAtmosphere(wardMood, mood, sun);

            var building = new GameObject("Building").transform;
            BuildShell(building);

            var briefing = new RoomFrame(new Vector3(1.5f, 0f, 0f), Vector3.back);
            var shiftManager = new GameObject("Shift Manager").AddComponent<ShiftManager>();
            BuildBriefingRoom(building, briefing, shiftManager);

            var rooms = new List<PatientRoom>();
            var number = 1;
            for (var column = 0; column < Columns; column++)
                rooms.Add(BuildPatientRoom(building, number++, new RoomFrame(new Vector3(column * RoomSize + 1.5f, 0f, CorridorWidth), Vector3.forward)));
            for (var column = 1; column < Columns; column++)
                rooms.Add(BuildPatientRoom(building, number++, new RoomFrame(new Vector3(column * RoomSize + 1.5f, 0f, 0f), Vector3.back)));

            // A second shift board at the east end of the corridor, readable while walking between the rooms.
            BuildShiftBoard(building, "Shift Board (corridor)", shiftManager,
                new Vector3(Columns * RoomSize - 0.08f, 1.55f, CorridorWidth * 0.5f), Quaternion.LookRotation(Vector3.right));

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

            // The player starts in the briefing room, facing the desk with the computer that starts the shift.
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, scene);
            rig.transform.SetPositionAndRotation(briefing.Point(0.3f, 0f, 1.4f), briefing.Facing(0f, 1f));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            MarkStatic(building);
            Debug.Log($"[ProCommVR] Shift hospital built: {CountTriangles()} triangles in the scene (Quest 3 target: under about 100k).");
            ScenarioTestSceneBuilder.SaveSceneFirstInBuild(scene, ScenePath);
        }

        // ---- Shell: floors, walls, ceiling, colliders ----------------------------------------------------------

        private static void BuildShell(Transform building)
        {
            var shell = new GameObject("Shell").transform;
            shell.SetParent(building, false);
            var width = Columns * RoomSize;
            var south = -RoomSize;
            var north = CorridorWidth + RoomSize;

            // Floors: one scaled Kenney tile per room and one for the corridor.
            for (var column = 0; column < Columns; column++)
            {
                PlaceFloor(shell, $"Floor Room North {column + 1}", column * RoomSize, CorridorWidth, RoomSize, RoomSize);
                PlaceFloor(shell, $"Floor Room South {column + 1}", column * RoomSize, south, RoomSize, RoomSize);
            }
            PlaceFloor(shell, "Floor Corridor", 0f, 0f, width, CorridorWidth);

            // Doorway centers along the corridor walls, windows in the outer walls (one per room, beside the bed).
            var doors = new float[Columns];
            var windows = new float[Columns];
            for (var column = 0; column < Columns; column++)
            {
                doors[column] = column * RoomSize + 1.5f;
                windows[column] = column * RoomSize + 1f;
            }

            var walls = new GameObject("Walls").transform;
            walls.SetParent(shell, false);
            BuildWallRun(walls, "Corridor Wall North", new Vector3(0f, 0f, CorridorWidth), new Vector3(width, 0f, CorridorWidth), doors, null);
            BuildWallRun(walls, "Corridor Wall South", new Vector3(0f, 0f, 0f), new Vector3(width, 0f, 0f), doors, null);
            BuildWallRun(walls, "Outer Wall North", new Vector3(0f, 0f, north), new Vector3(width, 0f, north), null, windows);
            BuildWallRun(walls, "Outer Wall South", new Vector3(0f, 0f, south), new Vector3(width, 0f, south), null, windows);
            BuildWallRun(walls, "Outer Wall West", new Vector3(0f, 0f, south), new Vector3(0f, 0f, north), null, null);
            BuildWallRun(walls, "Outer Wall East", new Vector3(width, 0f, south), new Vector3(width, 0f, north), null, null);
            for (var column = 1; column < Columns; column++)
            {
                var x = column * RoomSize;
                BuildWallRun(walls, $"Room Wall North {column}", new Vector3(x, 0f, CorridorWidth), new Vector3(x, 0f, north), null, null);
                BuildWallRun(walls, $"Room Wall South {column}", new Vector3(x, 0f, south), new Vector3(x, 0f, 0f), null, null);
            }

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(shell, false);
            ceiling.transform.SetPositionAndRotation(new Vector3(width * 0.5f, WallHeight, (south + north) * 0.5f), Quaternion.Euler(180f, 0f, 0f));
            ceiling.transform.localScale = new Vector3(width / 10f, 1f, (north - south) / 10f);
            Object.DestroyImmediate(ceiling.GetComponent<Collider>());
            ceiling.GetComponent<Renderer>().sharedMaterial = XRTestSceneBuilder.GetOrCreateMaterial($"{MaterialFolder}/Ceiling.mat", new Color(0.93f, 0.93f, 0.91f));

            var floorCollider = new GameObject("Floor Collider").AddComponent<BoxCollider>();
            floorCollider.transform.SetParent(shell, false);
            floorCollider.transform.position = new Vector3(width * 0.5f, -0.1f, (south + north) * 0.5f);
            floorCollider.size = new Vector3(width, 0.2f, north - south);

            // Corridor ceiling lamps (visual only, the light comes from the directional light and ambient).
            for (var x = 2f; x < width; x += 4f)
                PlaceKenney(shell, "lampSquareCeiling", "Corridor Lamp", new Vector3(x, WallHeight, CorridorWidth * 0.5f), Quaternion.identity, 0.5f, true);
        }

        private static void PlaceFloor(Transform parent, string name, float x, float z, float sizeX, float sizeZ)
        {
            var floor = InstantiateKenney("floorFull", parent, name);
            var size = Scenario01SceneBuilder.TightBounds(floor).size;
            floor.transform.localScale = Vector3.Scale(floor.transform.localScale, new Vector3(sizeX / size.x, KenneyDepthScale, sizeZ / size.z));
            Scenario01SceneBuilder.Align(floor, new Vector3(0.5f, 1f, 0.5f), new Vector3(x + sizeX * 0.5f, 0f, z + sizeZ * 0.5f));
        }

        /// <summary>
        /// A straight wall from start to end: solid Kenney wall pieces, with a 1 m doorway piece at every door
        /// and a 1 m window piece at every window (distances measured from start along the wall). Adds box colliders
        /// for the solid parts and windows.
        /// </summary>
        private static void BuildWallRun(Transform parent, string name, Vector3 start, Vector3 end, float[] doors, float[] windows)
        {
            var run = new GameObject(name).transform;
            run.SetParent(parent, false);
            var direction = (end - start).normalized;
            var length = Vector3.Distance(start, end);

            var openings = new List<(float Center, string Model)>();
            if (doors != null)
                foreach (var door in doors)
                    openings.Add((AlongRun(start, direction, door), "wallDoorway"));
            if (windows != null)
                foreach (var window in windows)
                    openings.Add((AlongRun(start, direction, window), "wallWindow"));
            openings.Sort((a, b) => a.Center.CompareTo(b.Center));

            var cursor = 0f;
            foreach (var (center, model) in openings)
            {
                PlaceWallPiece(run, "wall", start, direction, cursor, center - OpeningWidth * 0.5f, true);
                PlaceWallPiece(run, model, start, direction, center - OpeningWidth * 0.5f, center + OpeningWidth * 0.5f, model == "wallWindow");
                cursor = center + OpeningWidth * 0.5f;
            }
            PlaceWallPiece(run, "wall", start, direction, cursor, length, true);
        }

        /// <summary>
        /// Converts a world x or z coordinate of an opening into a distance along the run.
        /// </summary>
        private static float AlongRun(Vector3 start, Vector3 direction, float coordinate)
        {
            return Mathf.Abs(direction.x) > 0.5f ? (coordinate - start.x) * Mathf.Sign(direction.x) : (coordinate - start.z) * Mathf.Sign(direction.z);
        }

        private static void PlaceWallPiece(Transform parent, string model, Vector3 start, Vector3 direction, float from, float to, bool collider)
        {
            var length = to - from;
            if (length < 0.01f)
                return;

            var piece = InstantiateKenney(model, parent, model);
            // Kenney wall pieces are modelled along x; scale to the segment length and the wall height, then turn.
            var size = Scenario01SceneBuilder.TightBounds(piece).size;
            piece.transform.localScale = Vector3.Scale(piece.transform.localScale, new Vector3(length / size.x, WallHeight / size.y, KenneyDepthScale));
            piece.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction) * piece.transform.rotation;
            var middle = start + direction * ((from + to) * 0.5f);
            Scenario01SceneBuilder.Align(piece, new Vector3(0.5f, 0f, 0.5f), middle);

            if (!collider)
                return;
            var box = new GameObject($"{model} Collider").AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(middle + Vector3.up * WallHeight * 0.5f, Quaternion.FromToRotation(Vector3.right, direction));
            box.size = new Vector3(length, WallHeight, ColliderThickness);
        }

        // ---- Rooms ------------------------------------------------------------------------------------------

        private static void BuildBriefingRoom(Transform building, RoomFrame room, ShiftManager shiftManager)
        {
            var root = new GameObject("Briefing Room").transform;
            root.SetParent(building, false);

            // Desk against the back wall with the computer that starts the shift.
            var desk = PlaceKenney(root, "desk", "Desk", room.Point(0.3f, 0f, 3.55f), room.Facing(0f, -1f), 0.76f, false);
            var deskTop = Scenario01SceneBuilder.TightBounds(desk).max.y;
            PlaceKenney(root, "chairDesk", "Desk Chair", room.Point(0.3f, 0f, 2.85f), room.Facing(0f, 1f), 0.9f, false);
            BuildStartComputer(root, room, deskTop, shiftManager);

            PlaceKenney(root, "bookcaseClosedWide", "Bookcase", room.Point(-1.2f, 0f, 2.6f), room.Facing(1f, 0f), 1.9f, false);
            PlaceKenney(root, "table", "Meeting Table", room.Point(1.4f, 0f, 1.6f), room.Facing(0f, 1f), 0.75f, false);
            PlaceKenney(root, "chair", "Meeting Chair", room.Point(1.4f, 0f, 1.0f), room.Facing(0f, 1f), 0.9f, false);
            PlaceKenney(root, "chair", "Meeting Chair", room.Point(1.4f, 0f, 2.2f), room.Facing(0f, -1f), 0.9f, false);
            PlaceKenney(root, "coatRackStanding", "Coat Rack", room.Point(-1.2f, 0f, 0.4f), room.Facing(1f, 0f), 1.8f, false);
            PlaceKenney(root, "pottedPlant", "Plant", room.Point(2.2f, 0f, 3.7f), Quaternion.identity, 0.8f, false);
            PlaceKenney(root, "lampSquareCeiling", "Ceiling Lamp", room.Point(0.5f, WallHeight, 2f), Quaternion.identity, 0.5f, true);

            // The shift board on the right-hand wall, readable from the start position.
            BuildShiftBoard(root, "Shift Board (briefing)", shiftManager, room.Point(2.42f, 1.5f, 2.3f), room.Facing(1f, 0f));
        }

        /// <summary>
        /// The computer on the desk: point at it with the controller ray and pull the trigger to start the shift.
        /// </summary>
        private static void BuildStartComputer(Transform parent, RoomFrame room, float deskTop, ShiftManager shiftManager)
        {
            var screen = PlaceKenney(parent, "computerScreen", "Computer (start shift)", room.Point(0.3f, deskTop, 3.65f), room.Facing(0f, -1f), 0.45f, false);
            PlaceKenney(parent, "computerKeyboard", "Keyboard", room.Point(0.3f, deskTop, 3.4f), room.Facing(0f, -1f), 0.03f, false);

            var bounds = Scenario01SceneBuilder.TightBounds(screen);
            var collider = screen.AddComponent<BoxCollider>();
            collider.center = screen.transform.InverseTransformPoint(bounds.center);
            var localSize = screen.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            screen.AddComponent<XRSimpleInteractable>();

            var label = CreateWorldText(parent, "Start Shift Label", room.Point(0.3f, bounds.max.y + 0.2f, 3.6f), room.Facing(0f, 1f), 0.6f);
            var button = screen.AddComponent<ShiftStartButton>();
            var serialized = new SerializedObject(button);
            serialized.FindProperty("_shiftManager").objectReferenceValue = shiftManager;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PatientRoom BuildPatientRoom(Transform building, int number, RoomFrame room)
        {
            var root = new GameObject($"Room {number}").transform;
            root.SetParent(building, false);

            // Hospital bed (GRADD) with the head end against the back wall, IV pole and drawers beside it.
            var bed = Scenario01SceneBuilder.PlaceGradd(root, "Hospital Bed", "Hospital_Bed_001", 0f);
            AlignLongSide(bed, room, 2.1f);
            Scenario01SceneBuilder.Align(bed, new Vector3(0.5f, 0f, 0.5f), room.Point(1.35f, 0f, 2.9f));
            var iv = Scenario01SceneBuilder.PlaceGradd(root, "IV Pole", "IV_Bag_Holder_001", 0f);
            Scenario01SceneBuilder.Align(iv, new Vector3(0.5f, 0f, 0.5f), room.Point(2.2f, 0f, 3.65f));
            var drawers = Scenario01SceneBuilder.PlaceGradd(root, "Drawers On Wheels", "Drawers_on_Wheels_001", 0f);
            Scenario01SceneBuilder.Align(drawers, new Vector3(0.5f, 0f, 0.5f), room.Point(0.35f, 0f, 3.7f));

            // The patient sits in a chair beside the bed, not on the bed edge: the Sitting animation expects a seat of
            // about 0.45 m, the mattress is 0.66 m high and the side rails (up to 0.83 m) run along both long sides.
            var chairPosition = room.Point(0.3f, 0f, 2.9f);
            var patientFacing = room.Facing(-0.3f, -1f);
            PlaceKenney(root, "chairCushion", "Visitor Chair", chairPosition, patientFacing, 0.9f, false);
            PlaceKenney(root, "pottedPlant", "Plant", room.Point(-1.2f, 0f, 0.4f), Quaternion.identity, 0.7f, false);
            PlaceKenney(root, "lampSquareCeiling", "Ceiling Lamp", room.Point(0.5f, WallHeight, 2f), Quaternion.identity, 0.5f, true);

            var patient = PlacePatient(root, PatientModels[(number - 1) % PatientModels.Length], chairPosition, patientFacing);

            // Mood: the data (MoodController) and the colored room light that reads it.
            var moodObject = new GameObject("Patient Mood");
            moodObject.transform.SetParent(root, false);
            var mood = ScenarioTestSceneBuilder.AddMoodController(moodObject, null);
            // The mood's sound, 3D and only audible in and right outside this room.
            moodObject.transform.position = room.Point(0.5f, 1.5f, 2.2f);
            ScenarioTestSceneBuilder.AddMoodAudio(moodObject, mood, true, 4f);
            var lightObject = new GameObject("Mood Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.position = room.Point(0.5f, WallHeight - 0.3f, 2.4f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.3f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            var roomLight = lightObject.AddComponent<MoodRoomLight>();
            var serialized = new SerializedObject(roomLight);
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var sign = BuildRoomSign(root, room, mood);

            var head = patient.position + Vector3.up * 1.75f;
            var presenter = ScenarioTestSceneBuilder.CreatePresenter("Scenario Presenter", patient, head, room.Point(-0.2f, 1.15f, 1.5f));
            presenter.transform.SetParent(root, true);
            presenter.transform.Find("NPC Bubble Anchor").rotation = Quaternion.LookRotation(Flat(head - room.Point(0f, 0f, 0.3f)));
            AddReactor(root, presenter, patient, room);

            var patientRoom = root.gameObject.AddComponent<PatientRoom>();
            serialized = new SerializedObject(patientRoom);
            serialized.FindProperty("_roomName").stringValue = $"Room {number}";
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.FindProperty("_sign").objectReferenceValue = sign;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patientRoom;
        }

        /// <summary>
        /// Turns a GRADD model so its longest horizontal side runs into the room, and scales it to that length.
        /// </summary>
        private static void AlignLongSide(GameObject go, RoomFrame room, float length)
        {
            var size = Scenario01SceneBuilder.TightBounds(go).size;
            var alongX = size.x > size.z;
            go.transform.rotation = Quaternion.FromToRotation(alongX ? Vector3.right : Vector3.forward, room.Forward) * go.transform.rotation;
            go.transform.localScale *= length / Mathf.Max(size.x, size.z);
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

        /// <summary>
        /// Body language after a choice: turns to the nurse, away from the nurse, or to the window.
        /// </summary>
        private static void AddReactor(Transform root, ScenarioPresenter presenter, Transform patient, RoomFrame room)
        {
            var poses = new GameObject("Poses").transform;
            poses.SetParent(root, false);
            var targets = new List<KeyValuePair<NpcPose, Transform>>
            {
                new KeyValuePair<NpcPose, Transform>(NpcPose.Engaged, CreatePose(poses, "Engaged", patient.position, room.Facing(0.2f, -1f))),
                new KeyValuePair<NpcPose, Transform>(NpcPose.TurnsAway, CreatePose(poses, "Turns Away", patient.position, room.Facing(1f, 0.3f))),
                new KeyValuePair<NpcPose, Transform>(NpcPose.Withdrawn, CreatePose(poses, "Withdrawn", patient.position, room.Facing(-0.3f, 1f))),
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

        // ---- World UI -----------------------------------------------------------------------------------------

        /// <summary>
        /// Door sign on the corridor side, right of the doorway: room, patient, scenario and a mood color patch.
        /// </summary>
        private static RoomSign BuildRoomSign(Transform parent, RoomFrame room, MoodController mood)
        {
            // Faces the corridor: the canvas front looks back out of the room.
            var canvas = CreateCanvas(parent, "Door Sign", new Vector2(420f, 220f),
                room.Point(0.95f, 1.5f, -0.07f), Quaternion.LookRotation(room.Forward), Color.white);

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

        private static void BuildShiftBoard(Transform parent, string name, ShiftManager shiftManager, Vector3 position, Quaternion rotation)
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
            XRTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PlanPath));
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

        /// <summary>
        /// The Kenney model inside an empty parent, so scaling and turning the parent works in world-aligned axes
        /// whatever rotation the model's own root has.
        /// </summary>
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

        private static void MarkStatic(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<MeshRenderer>() != null)
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
