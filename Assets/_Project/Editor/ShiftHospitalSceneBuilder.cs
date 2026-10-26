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
    /// patients are in rooms 1A-1C (floor 1; Scenario 4, the family of a patient with a poor prognosis, in 1B),
    /// Scenario 2 (the angry daughter) plays in the corridor of floor 3 with
    /// room 3B as the consultation room, Scenario 3 (the doctor and the family member) at the nurse's station of
    /// floor 2 with room 2C as the doctor's office, and the lift buttons take the player between floors.
    /// Also creates the shift plan if it is missing (Scenario 2, 3 and 4 as task 2, 3 and 4, Scenario 1 as placeholder for the others).
    /// Batchmode: -executeMethod ProCommVR.Editor.ShiftHospitalSceneBuilder.BuildScene
    /// Requires the prefabs from ScenarioTestSceneBuilder (bubble, results screen), the XR rig variant and Liam's scene.
    /// </summary>
    public static class ShiftHospitalSceneBuilder
    {
        private const string HospitalScenePath = "Assets/Scenes/liam_test_scene.unity";
        private const string ScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const string PlanPath = "Assets/_Project/Dialogue/Shifts/Shift_Placeholder.asset";
        private const string ScenarioPath = "Assets/_Project/Dialogue/Scenarios/Scenario01_DisappointedPatient.asset";
        private const string Scenario2Path = "Assets/_Project/Dialogue/Scenarios/Scenario02_AngryFamilyMember.asset";
        private const string Scenario3Path = "Assets/_Project/Dialogue/Scenarios/Scenario03_DoctorAndFamily.asset";
        private const string Scenario4Path = "Assets/_Project/Dialogue/Scenarios/Scenario04_PoorPrognosis.asset";
        private const string GurneyModelPath = "Assets/ThirdParty/Sketchfab/HospitalBed_anshartis/source/BED ASSEST_2.fbx";
        private const string TickSoundPath = "Assets/_Project/Audio/Placeholder/Countdown_Tick_Placeholder.wav";
        private const string CharacterMaterialFolder = "Assets/_Project/Materials/Characters";
        private const string PaperMaterialPath = "Assets/_Project/Materials/Paper.mat";
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
        // Liam's floors have a 1 cm finish on top of the slab: the rig starts and arrives just above it, so its
        // CharacterController never starts inside the floor collider.
        private const float LiftArrivalHeight = 0.03f;
        private const float CorridorBoardX = 9f;    // opposite door B, between the staff Liam placed in the corridors
        private const float LobbyBoardX = 7.3f;     // on the north wall behind the reception desk, right of the "Lobby" sign
        private const float LobbyBoardScale = 1.4f; // read from the start position, about 3 m away
        // The places of the shift tasks, in task order: task 1 goes to room 1A, task 2 to the corridor of floor 3
        // (Scenario 2), task 3 to the nurse's station of floor 2 (Scenario 3), task 4 to room 1B, task 5 to 1C.
        // The other rooms keep Liam's cards (2A and 2B are Liam's rooms again).
        private const string CorridorTask = "Corridor 3";
        private const string StationTask = "Station 2";
        private static readonly string[] TaskLocations = { "1A", CorridorTask, StationTask, "1B", "1C" };
        private static readonly string[] RoomLabels = TaskLocations.Where(l => l != CorridorTask && l != StationTask).ToArray();

        // One patient model per room, in the order of RoomLabels; these rooms play Scenario 1 for now.
        private static readonly string[] PatientModels = { "Patient_Male", "Patient_Female_Old", "Patient_Male_Old", "Patient_Male" };

        // Scenario 2 (Liam's floor 3): the daughter blocks the corridor between the lift and room 3B, which is the
        // consultation room. The countdown hangs from the ceiling between the lift and the daughter; the gurney
        // comes from the east end of the corridor, on the north side (Liam's staff stand on the south side).
        private const int EncounterFloor = 3;
        private const string ConsultationRoomLabel = "3B";
        private const string DaughterModel = "Patient_Female";
        private static readonly Vector3 DaughterSpot = new Vector3(5f, 0f, 1.6f);
        private static readonly Vector3 CountdownPosition = new Vector3(3.6f, 2.6f, 1.5f);
        private static readonly Vector3 EncounterSignPosition = new Vector3(2.6f, 1.5f, 0f);
        private static readonly Vector3 GurneyStart = new Vector3(17f, 0f, 2.15f);
        private static readonly Vector3 GurneyEnd = new Vector3(11.3f, 0f, 2.15f);
        private static readonly string[] EncounterCorridorLights = { "Light_3", "Light_9" };

        // Scenario 3 (Liam's floor 2): the nurse's station stands against the south wall near the lift, where the
        // patient's son waits. The doctor rushes between the doors of 2A and 2C on the north side (Liam's staff stand on
        // the south side) and reviews paperwork at his desk in room 2C, the doctor's office.
        private const int StationFloor = 2;
        private const string OfficeLabel = "2C";
        private const string SonModel = "Patient_Male";
        private const string DoctorModel = "Doctor_Male";
        private static readonly Vector3 StationDeskSpot = new Vector3(3.4f, 0f, 0.1f);
        private static readonly Vector3 SonSpot = new Vector3(3.9f, 0f, 1.05f);
        private static readonly Vector3 StationSignPosition = new Vector3(2.15f, 1.55f, 0f);
        private static readonly Vector3[] DoctorRound = { new Vector3(13f, 0f, 2.3f), new Vector3(5.6f, 0f, 2.3f) };
        private static readonly string[] StationCorridorLights = { "Light_3" };

        // Liam's patient models wear pyjamas; visitors (the daughter, the son) get everyday colors for top and trousers.
        private static readonly Color VisitorTopColor = new Color(0.24f, 0.34f, 0.56f);
        private static readonly Color VisitorBottomColor = new Color(0.17f, 0.19f, 0.26f);
        private static readonly Color SonTopColor = new Color(0.55f, 0.33f, 0.2f);
        private static readonly Color SonBottomColor = new Color(0.22f, 0.24f, 0.2f);

        // Scenario 4 (room 1B): the patient sleeps in bed (on the mattress, posed on the first frame of "Laying") and his
        // wife keeps watch on the chair next to the bed.
        private const string BedsideRoomLabel = "1B";
        private const string SleepingPatientModel = "Patient_Male_Old";
        private const string WifeModel = "Patient_Female_Old";
        private static readonly Color WifeTopColor = new Color(0.45f, 0.38f, 0.52f);
        private static readonly Color WifeBottomColor = new Color(0.2f, 0.2f, 0.24f);

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
            var roomIndex = 0;
            foreach (var location in TaskLocations)
            {
                if (location == CorridorTask)
                    rooms.Add(BuildCorridorEncounter(hospital, shift));
                else if (location == StationTask)
                    rooms.Add(BuildStationEncounter(hospital, shift));
                else if (location == BedsideRoomLabel)
                    rooms.Add(BuildBedsideRoom(hospital, location));
                else
                    rooms.Add(BuildPatientRoom(hospital, roomIndex++, location));
            }
            CloseOtherDoors(hospital);
            HideMisplacedPatients(hospital);
            BuildLift(hospital, shift);

            // A shift board in the corridor of every floor with a task, readable while walking between the rooms.
            foreach (var floor in TaskLocations.Select(FloorOf).Distinct())
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

            return new Vector3(desk.center.x, LiftArrivalHeight, desk.min.z - 1.3f);
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
            var room = RequireChild(hospital, $"Floor{FloorOf(label)}/Room_{label}");
            var signPosition = ReplaceCardAndOpenDoor(hospital, room, label);

            // The patient sits on the chair, turned to the door where the player comes in (as Liam's own patients).
            var chair = RequireChild(room, "Furniture/Chair");
            var seat = Scenario01SceneBuilder.TightBounds(chair.gameObject).center;
            var towardDoor = Flat(room.position - seat).normalized;
            chair.RotateAround(seat, Vector3.up, Vector3.SignedAngle(RequireChild(room, "Anchor_Sitting").forward, towardDoor, Vector3.up));
            var patient = PlaceCharacter(room, PatientModels[index % PatientModels.Length],
                new Vector3(seat.x, room.position.y, seat.z), Quaternion.LookRotation(towardDoor), "Sitting");

            var mood = AddRoomMood(room);
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
            var serialized = new SerializedObject(patientRoom);
            serialized.FindProperty("_roomName").stringValue = $"Room {label}";
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.FindProperty("_sign").objectReferenceValue = sign;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patientRoom;
        }

        /// <summary>
        /// Liam's patient card and his patient for this door make way for the shift: returns where the card was (the
        /// place of the door sign). The door swings open.
        /// </summary>
        private static Vector3 ReplaceCardAndOpenDoor(Transform hospital, Transform room, string label)
        {
            var floor = $"Floor{FloorOf(label)}";
            var card = RequireChild(hospital, $"{floor}/Card_{label}");
            var signPosition = card.position - room.forward * 0.03f;
            Object.DestroyImmediate(card.gameObject);
            var liamsPatient = hospital.Find($"Characters/Patient_{label}");
            if (liamsPatient != null)
                Object.DestroyImmediate(liamsPatient.gameObject);
            OpenDoor(RequireChild(hospital, $"{floor}/Door_{label}"), room);
            return signPosition;
        }

        /// <summary>
        /// The mood of a room: the data (MoodController), its sound (3D, only audible in and right outside this room) and
        /// the room's own ceiling light, which takes the mood color.
        /// </summary>
        private static MoodController AddRoomMood(Transform room)
        {
            var moodObject = new GameObject("Patient Mood");
            moodObject.transform.SetParent(room, false);
            moodObject.transform.localPosition = new Vector3(0.5f, 1.5f, 2.2f);
            var mood = ScenarioTestSceneBuilder.AddMoodController(moodObject, null);
            ScenarioTestSceneBuilder.AddMoodAudio(moodObject, mood, true, 4f);
            AddMoodRoomLight(RequireChild(room, "Light"), mood);
            return mood;
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
            var open = RoomLabels.Append(ConsultationRoomLabel).Append(OfficeLabel).Select(label => $"Door_{label}").ToHashSet();
            foreach (var door in hospital.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Door_") && !open.Contains(t.name)))
                AddBoxCollider(door.gameObject, Scenario01SceneBuilder.TightBounds(door.gameObject));
        }

        /// <summary>
        /// One of Liam's characters in an Animator state (e.g. "Sitting"), root on the floor at the position.
        /// </summary>
        private static Transform PlaceCharacter(Transform parent, string model, Vector3 position, Quaternion facing, string animatorState)
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
            serialized.FindProperty("_stateName").stringValue = animatorState;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patient.transform;
        }

        private static void AddReactor(Transform root, ScenarioPresenter presenter, Transform patient,
            Quaternion engaged, Quaternion turnsAway, Quaternion withdrawn)
        {
            AddReactor(root, presenter, patient, null,
                new PoseSpec(NpcPose.Engaged, engaged, null),
                new PoseSpec(NpcPose.TurnsAway, turnsAway, null),
                new PoseSpec(NpcPose.Withdrawn, withdrawn, null));
        }

        /// <summary>
        /// A pose of the NPC after a choice: where it turns to (null = it stays as it is), the Animator state
        /// it plays (null = none) and optionally where it moves to (null = where the NPC is now).
        /// </summary>
        private readonly struct PoseSpec
        {
            public readonly NpcPose Pose;
            public readonly Quaternion? Rotation;
            public readonly string AnimatorState;
            public readonly Vector3? Position;

            public PoseSpec(NpcPose pose, Quaternion? rotation, string animatorState, Vector3? position = null)
            {
                Pose = pose;
                Rotation = rotation;
                AnimatorState = animatorState;
                Position = position;
            }
        }

        private static void AddReactor(Transform root, ScenarioPresenter presenter, Transform patient, Animator animator, params PoseSpec[] poses)
        {
            var poseParent = new GameObject("Poses").transform;
            poseParent.SetParent(root, false);

            var reactor = patient.gameObject.AddComponent<NpcReactor>();
            var voice = patient.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            var serialized = new SerializedObject(reactor);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_body").objectReferenceValue = patient;
            serialized.FindProperty("_voice").objectReferenceValue = voice;
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            var poseList = serialized.FindProperty("_poses");
            poseList.arraySize = poses.Length;
            for (var i = 0; i < poses.Length; i++)
            {
                var element = poseList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Pose").enumValueIndex = (int)poses[i].Pose;
                element.FindPropertyRelative("Target").objectReferenceValue = poses[i].Rotation.HasValue
                    ? CreatePose(poseParent, ObjectNames.NicifyVariableName(poses[i].Pose.ToString()), poses[i].Position ?? patient.position, poses[i].Rotation.Value)
                    : null;
                element.FindPropertyRelative("AnimatorState").stringValue = poses[i].AnimatorState ?? "";
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

        // ---- Scenario 2: corridor encounter (floor 3) --------------------------------------------------------------

        /// <summary>
        /// Scenario 2: the patient's angry daughter blocks the corridor of floor 3. The task countdown (the surgical
        /// gurney) starts when the player comes near her; she asks to be heard and the player chooses where to
        /// talk: right there, or in the consultation room (room 3B), where she walks ahead. When the countdown runs
        /// out, the gurney comes down the corridor and the corridor and room lights turn to Alarm.
        /// </summary>
        private static PatientRoom BuildCorridorEncounter(Transform hospital, Transform shift)
        {
            var level = RequireChild(hospital, $"Floor{EncounterFloor}");
            var floorY = EncounterFloor * FloorHeight;
            var encounter = new GameObject($"Encounter ({CorridorTask})").transform;
            encounter.SetParent(shift, false);
            encounter.position = DaughterSpot + Vector3.up * floorY;

            // The daughter faces the lift, where the player comes from. Her root moves to the consultation room
            // with everything that belongs to her: the NPC bubble, the pose targets and the mood sound.
            var daughter = new GameObject("Daughter").transform;
            daughter.SetParent(encounter, false);
            daughter.SetPositionAndRotation(encounter.position, Quaternion.LookRotation(Vector3.left));
            var body = PlaceCharacter(daughter, DaughterModel, daughter.position, daughter.rotation, "Angry");
            DressAsVisitor(body, "Daughter", VisitorTopColor, VisitorBottomColor);
            AddBlocker(daughter.gameObject);

            var mood = ScenarioTestSceneBuilder.AddMoodController(new GameObject("Mood"), null);
            mood.transform.SetParent(encounter, false);
            var moodSound = new GameObject("Mood Sound");
            moodSound.transform.SetParent(daughter, false);
            moodSound.transform.localPosition = Vector3.up * 1.5f;
            ScenarioTestSceneBuilder.AddMoodAudio(moodSound, mood, true, 6f);
            foreach (var lightName in EncounterCorridorLights)
                AddMoodRoomLight(RequireChild(level, lightName), mood);

            var room = BuildConsultationRoom(level, mood);
            var sign = BuildRoomSign(encounter, new Vector3(EncounterSignPosition.x, floorY + EncounterSignPosition.y,
                RendererBounds(RequireChild(level, "Wall_South").gameObject).max.z + 0.02f), Quaternion.LookRotation(Vector3.back), mood);

            // Bubble above her head, readable from where she looks (it turns with her).
            var head = body.position + Vector3.up * 2.1f;
            var presenter = ScenarioTestSceneBuilder.CreatePresenter("Scenario Presenter", body, head, head + Vector3.left);
            presenter.transform.SetParent(encounter, true);
            var npcAnchor = presenter.transform.Find("NPC Bubble Anchor");
            npcAnchor.rotation = Quaternion.LookRotation(-daughter.forward);
            npcAnchor.SetParent(daughter, true);

            // Body language: talking to the nurse (calmer), shouting with her back half turned, or sad and turned aside.
            var animator = body.GetComponentInChildren<Animator>();
            AddReactor(daughter, presenter, body, animator,
                new PoseSpec(NpcPose.Unchanged, null, "Angry"),
                new PoseSpec(NpcPose.Engaged, daughter.rotation, "Talking"),
                new PoseSpec(NpcPose.TurnsAway, daughter.rotation * Quaternion.Euler(0f, 150f, 0f), "Angry"),
                new PoseSpec(NpcPose.Withdrawn, daughter.rotation * Quaternion.Euler(0f, 70f, 0f), "Sad"));

            // Where to talk: "corridor" keeps her here, "consultation-room" walks her through the door of room 3B.
            var paths = new GameObject("Paths").transform;
            paths.SetParent(encounter, false);
            var spot = room.TransformPoint(0.75f, 0f, 1.5f);
            var toRoom = AddPath(daughter.gameObject, paths, "To Consultation Room", 1.1f, false,
                new Vector3(8f, floorY, 1.9f),
                new Vector3(room.position.x, floorY, room.position.z - 0.6f),
                new Vector3(room.position.x, floorY, room.position.z + 0.5f),
                spot);
            paths.Find("To Consultation Room/Waypoint 4").rotation = Quaternion.LookRotation(Flat(room.TransformPoint(0f, 0f, 0.4f) - spot));
            var mover = daughter.gameObject.AddComponent<ConversationLocationMover>();
            var serialized = new SerializedObject(mover);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            var places = serialized.FindProperty("_places");
            places.arraySize = 2;
            places.GetArrayElementAtIndex(0).FindPropertyRelative("LocationId").stringValue = "corridor";
            places.GetArrayElementAtIndex(1).FindPropertyRelative("LocationId").stringValue = "consultation-room";
            places.GetArrayElementAtIndex(1).FindPropertyRelative("Path").objectReferenceValue = toRoom;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var countdown = BuildCountdown(encounter, floorY, body, BuildGurney(encounter, paths, floorY));

            var patientRoom = encounter.gameObject.AddComponent<PatientRoom>();
            serialized = new SerializedObject(patientRoom);
            serialized.FindProperty("_roomName").stringValue = CorridorTask;
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.FindProperty("_sign").objectReferenceValue = sign;
            serialized.FindProperty("_countdown").objectReferenceValue = countdown;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patientRoom;
        }

        /// <summary>
        /// Liam's room 3B as an empty consultation room: his bed, nightstand and chair are hidden, a Kenney round
        /// table with two chairs takes their place, and his card makes way for a "Consultation room" sign. The room
        /// light follows the mood of the encounter (red on Alarm).
        /// </summary>
        private static Transform BuildConsultationRoom(Transform level, MoodController mood)
        {
            var label = ConsultationRoomLabel;
            var room = RequireChild(level, $"Room_{label}");
            foreach (var item in new[] { "Bed", "Nightstand", "Chair" })
                RequireChild(room, $"Furniture/{item}").gameObject.SetActive(false);

            var furniture = new GameObject("Consultation Furniture").transform;
            furniture.SetParent(room, false);
            var table = room.TransformPoint(-0.3f, 0f, 2.7f);
            PlaceKenney(furniture, "tableRound", "Table", table, room.rotation, 0.75f, false);
            PlaceKenney(furniture, "chairCushion", "Chair (left)", room.TransformPoint(-1.15f, 0f, 2.7f), Quaternion.LookRotation(room.right), 0.85f, false);
            PlaceKenney(furniture, "chairCushion", "Chair (right)", room.TransformPoint(0.55f, 0f, 2.7f), Quaternion.LookRotation(-room.right), 0.85f, false);

            var card = RequireChild(level, $"Card_{label}");
            var signPosition = card.position - room.forward * 0.03f;
            Object.DestroyImmediate(card.gameObject);
            var canvas = CreateCanvas(room, "Consultation Room Sign", new Vector2(420f, 140f), signPosition, Quaternion.LookRotation(room.forward), Color.white);
            var text = CreateLabel(canvas, "Label", 20f, 46f, TextAlignmentOptions.Center);
            text.text = "<b>Consultation room</b>\n<size=60%>Free</size>";

            OpenDoor(RequireChild(level, $"Door_{label}"), room);
            AddMoodRoomLight(RequireChild(room, "Light"), mood);
            return room;
        }

        /// <summary>
        /// The countdown display hanging from the ceiling above the corridor, like a digital wall clock: dark panel,
        /// amber digits (red in the last seconds), and the ticking sound.
        /// </summary>
        private static TaskCountdown BuildCountdown(Transform parent, float floorY, Transform npc, PathMover gurney)
        {
            var root = new GameObject("Countdown").transform;
            root.SetParent(parent, false);
            var canvas = CreateCanvas(root, "Countdown Display", new Vector2(700f, 260f), CountdownPosition + Vector3.up * floorY,
                Quaternion.LookRotation(Vector3.right), new Color(0.06f, 0.06f, 0.07f, 1f));
            var label = CreateLabel(canvas, "Time", 30f, 150f, TextAlignmentOptions.Center);
            label.rectTransform.offsetMin = new Vector2(20f, 10f);
            label.rectTransform.offsetMax = new Vector2(-20f, -10f);

            var ticking = canvas.gameObject.AddComponent<AudioSource>();
            ticking.clip = ScenarioTestSceneBuilder.LoadRequired<AudioClip>(TickSoundPath, "Placeholder tick sound missing.");
            ticking.playOnAwake = false;
            ticking.loop = true;
            ticking.volume = 0.5f;
            ticking.spatialBlend = 1f;
            ticking.rolloffMode = AudioRolloffMode.Linear;
            ticking.minDistance = 1f;
            ticking.maxDistance = 8f;

            var countdown = root.gameObject.AddComponent<TaskCountdown>();
            var serialized = new SerializedObject(countdown);
            serialized.FindProperty("_npc").objectReferenceValue = npc;
            serialized.FindProperty("_display").objectReferenceValue = canvas.gameObject;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.FindProperty("_ticking").objectReferenceValue = ticking;
            serialized.FindProperty("_arrival").objectReferenceValue = gurney;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return countdown;
        }

        /// <summary>
        /// The surgical gurney (hospital bed with wheels, Sketchfab), hidden until the countdown runs out. Then it
        /// rolls from the east end of the corridor toward the lift, on its way to the next patient.
        /// </summary>
        private static PathMover BuildGurney(Transform parent, Transform paths, float floorY)
        {
            var start = GurneyStart + Vector3.up * floorY;
            var gurney = new GameObject("Surgical Gurney");
            gurney.transform.SetParent(parent, false);
            gurney.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.left));
            var model = Scenario01SceneBuilder.PlaceModel(GurneyModelPath, gurney.transform, "Model", 0f);
            // Long side along the direction it rolls (the gurney's forward).
            var bounds = Scenario01SceneBuilder.TightBounds(model);
            if (bounds.size.z > bounds.size.x)
                model.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.rotation;
            Scenario01SceneBuilder.Align(model, new Vector3(0.5f, 0f, 0.5f), start);

            var end = GurneyEnd + Vector3.up * floorY;
            return AddPath(gurney, paths, "Gurney", 1f, true, end);
        }

        /// <summary>
        /// A PathMover on the object with its waypoints under Paths/name (not under the moving object). The last
        /// waypoint keeps the object's rotation unless the caller turns it.
        /// </summary>
        private static PathMover AddPath(GameObject target, Transform paths, string name, float speed, bool hideUntilPlayed, params Vector3[] points)
        {
            var path = new GameObject(name).transform;
            path.SetParent(paths, false);
            var waypoints = new Transform[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                waypoints[i] = new GameObject($"Waypoint {i + 1}").transform;
                waypoints[i].SetParent(path, false);
                waypoints[i].SetPositionAndRotation(points[i], target.transform.rotation);
            }

            var mover = target.AddComponent<PathMover>();
            var serialized = new SerializedObject(mover);
            var list = serialized.FindProperty("_waypoints");
            list.arraySize = waypoints.Length;
            for (var i = 0; i < waypoints.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            serialized.FindProperty("_speed").floatValue = speed;
            serialized.FindProperty("_hideUntilPlayed").boolValue = hideUntilPlayed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return mover;
        }

        /// <summary>
        /// Gives a character other clothes colors: copies of its "_Top" and "_Bottom" materials, saved under
        /// Materials/Characters (Liam's materials stay as they are).
        /// </summary>
        private static void DressAsVisitor(Transform character, string name, Color topColor, Color bottomColor)
        {
            foreach (var renderer in character.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;
                    if (materials[i].name.EndsWith("_Top"))
                        materials[i] = GetOrCreateClothes(materials[i], $"{name}_Top", topColor);
                    else if (materials[i].name.EndsWith("_Bottom"))
                        materials[i] = GetOrCreateClothes(materials[i], $"{name}_Bottom", bottomColor);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material GetOrCreateClothes(Material source, string name, Color color)
        {
            var path = $"{CharacterMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            XRTestSceneBuilder.EnsureFolder(CharacterMaterialFolder);
            material = new Material(source) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddMoodRoomLight(Transform light, MoodController mood)
        {
            var serialized = new SerializedObject(light.gameObject.AddComponent<MoodRoomLight>());
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Scenario 3: nurse's station and doctor's office (floor 2) ----------------------------------------------

        /// <summary>
        /// Scenario 3: the patient's son waits at the nurse's station of floor 2 and keeps asking for the doctor, who
        /// rushes between the rooms ("I don't have time right now.", <see cref="BusyNpc"/>) and from time to time reviews
        /// paperwork at his desk in room 2C. Only there can he be spoken to: the 4 options are addressed to him. The son
        /// is the scenario's bystander: he complains when the player comes near and reacts to the outcome once the player
        /// comes back to tell him.
        /// </summary>
        private static PatientRoom BuildStationEncounter(Transform hospital, Transform shift)
        {
            var level = RequireChild(hospital, $"Floor{StationFloor}");
            var up = Vector3.up * (StationFloor * FloorHeight);
            var encounter = new GameObject($"Encounter ({StationTask})").transform;
            encounter.SetParent(shift, false);
            encounter.position = StationDeskSpot + up;
            var wallZ = RendererBounds(RequireChild(level, "Wall_South").gameObject).max.z;

            var mood = ScenarioTestSceneBuilder.AddMoodController(new GameObject("Mood"), null);
            mood.transform.SetParent(encounter, false);
            var moodSound = new GameObject("Mood Sound");
            moodSound.transform.SetParent(encounter, false);
            moodSound.transform.position = SonSpot + up + Vector3.up * 1.5f;
            ScenarioTestSceneBuilder.AddMoodAudio(moodSound, mood, true, 6f);
            foreach (var lightName in StationCorridorLights)
                AddMoodRoomLight(RequireChild(level, lightName), mood);

            BuildNursesStation(encounter, StationDeskSpot + up, wallZ);
            var office = BuildDoctorsOffice(level, mood);
            var sign = BuildRoomSign(encounter, new Vector3(StationSignPosition.x, up.y + StationSignPosition.y, wallZ + 0.02f),
                Quaternion.LookRotation(Vector3.back), mood);

            // The doctor: his root walks the round (BusyNpc) with the NPC bubble and the pose targets; the model is its child.
            var doctor = new GameObject("Doctor").transform;
            doctor.SetParent(encounter, false);
            doctor.SetPositionAndRotation(DoctorRound[0] + up, Quaternion.LookRotation(Vector3.left));
            var doctorBody = PlaceCharacter(doctor, DoctorModel, doctor.position, doctor.rotation, "Idle");
            AddBlocker(doctor.gameObject);

            var head = doctorBody.position + Vector3.up * 2.1f;
            var presenter = ScenarioTestSceneBuilder.CreatePresenter("Scenario Presenter", doctorBody, head, head + Vector3.left);
            presenter.transform.SetParent(encounter, true);
            var npcAnchor = presenter.transform.Find("NPC Bubble Anchor");
            npcAnchor.rotation = Quaternion.LookRotation(-doctor.forward);
            npcAnchor.SetParent(doctor, true);
            // He walks around and stands at his desk side-on to the door: his bubble turns to the player.
            npcAnchor.gameObject.AddComponent<FacePlayer>();

            // Body language after the choice: talking it through with the nurse, short and businesslike, or turning
            // back to his paperwork (annoyed).
            var doctorAnimator = doctorBody.GetComponentInChildren<Animator>();
            AddReactor(doctor, presenter, doctorBody, doctorAnimator,
                new PoseSpec(NpcPose.Unchanged, null, "Idle"),
                new PoseSpec(NpcPose.Engaged, doctor.rotation, "Talking"),
                new PoseSpec(NpcPose.TurnsAway, doctor.rotation * Quaternion.Euler(0f, 150f, 0f), "Idle"),
                new PoseSpec(NpcPose.Withdrawn, doctor.rotation * Quaternion.Euler(0f, 60f, 0f), "Idle"));

            // His round between the doors of 2A and 2C, and the way into his office to the desk.
            var paths = new GameObject("Paths").transform;
            paths.SetParent(encounter, false);
            var round = CreateWaypoints(paths, "Doctor Round", doctor.rotation, DoctorRound.Select(p => p + up).ToArray());
            var deskSpot = office.TransformPoint(-0.85f, 0f, 2.4f);
            var toDesk = CreateWaypoints(paths, "Doctor To Desk", Quaternion.LookRotation(-office.right),
                new Vector3(office.position.x, up.y, DoctorRound[0].z),
                office.position + office.forward * 0.6f,
                deskSpot);
            var busy = doctor.gameObject.AddComponent<BusyNpc>();
            var serialized = new SerializedObject(busy);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_animator").objectReferenceValue = doctorAnimator;
            SetArray(serialized.FindProperty("_round"), round);
            SetArray(serialized.FindProperty("_toDesk"), toDesk);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BuildSon(encounter, presenter, up);

            var patientRoom = encounter.gameObject.AddComponent<PatientRoom>();
            serialized = new SerializedObject(patientRoom);
            serialized.FindProperty("_roomName").stringValue = "Nurses' station 2";
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_moodController").objectReferenceValue = mood;
            serialized.FindProperty("_sign").objectReferenceValue = sign;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return patientRoom;
        }

        /// <summary>
        /// The patient's son, a visitor waiting at the nurse's station, facing the lift where the player comes from. When
        /// the player comes near he steps forward, turns to the player and complains (the scenario's Bystander Line);
        /// after the choice he reacts once the player is back near him to tell him the outcome.
        /// </summary>
        private static void BuildSon(Transform encounter, ScenarioPresenter presenter, Vector3 up)
        {
            var son = new GameObject("Son").transform;
            son.SetParent(encounter, false);
            son.SetPositionAndRotation(SonSpot + up, Quaternion.LookRotation(Vector3.left));
            var body = PlaceCharacter(son, SonModel, son.position, son.rotation, "Idle");
            DressAsVisitor(body, "Son", SonTopColor, SonBottomColor);
            AddBlocker(son.gameObject);
            var animator = body.GetComponentInChildren<Animator>();

            var attention = body.gameObject.AddComponent<NpcAttention>();
            var serialized = new SerializedObject(attention);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_body").objectReferenceValue = body;
            serialized.FindProperty("_range").floatValue = 3f;
            serialized.FindProperty("_attentionPose").objectReferenceValue = CreatePose(son, "Attention", son.position + son.forward * 0.35f, son.rotation);
            serialized.FindProperty("_facePlayer").boolValue = true;
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_animatorState").stringValue = "Angry";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // His reaction to the outcome: calmer and talking, still angry, or sad and turned aside.
            AddReactor(son, presenter, body, animator,
                new PoseSpec(NpcPose.Unchanged, null, "Angry"),
                new PoseSpec(NpcPose.Engaged, null, "Talking"),
                new PoseSpec(NpcPose.TurnsAway, son.rotation * Quaternion.Euler(0f, 150f, 0f), "Angry"),
                new PoseSpec(NpcPose.Withdrawn, null, "Sad"));
            var reactor = body.GetComponent<NpcReactor>();
            serialized = new SerializedObject(reactor);
            serialized.FindProperty("_reactAsBystander").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var anchor = new GameObject("Bystander Bubble Anchor").transform;
            anchor.SetParent(son, false);
            anchor.position = body.position + Vector3.up * 2.1f;
            anchor.rotation = Quaternion.LookRotation(-son.forward);
            anchor.localScale = Vector3.one * 1.3f;
            var speech = son.gameObject.AddComponent<BystanderSpeech>();
            serialized = new SerializedObject(speech);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_bubblePrefab").objectReferenceValue =
                ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.BubblePrefabPath, "Run 'Build Dialogue Bubble Prefab' first.").GetComponent<DialogueBubble>();
            serialized.FindProperty("_anchor").objectReferenceValue = anchor;
            serialized.FindProperty("_attention").objectReferenceValue = attention;
            serialized.FindProperty("_reactor").objectReferenceValue = reactor;
            serialized.FindProperty("_reactWhenNear").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The nurse's station against the south wall: a Kenney desk with a computer, a keyboard and a stack of
        /// files, and a "Nurses' station" sign on the wall above it.
        /// </summary>
        private static void BuildNursesStation(Transform parent, Vector3 spot, float wallZ)
        {
            var root = new GameObject("Nurses' Station").transform;
            root.SetParent(parent, false);
            var towardCorridor = Quaternion.LookRotation(Vector3.forward);
            var desk = PlaceKenney(root, "desk", "Desk", new Vector3(spot.x, spot.y, wallZ + 0.4f), towardCorridor, 0.78f, false);
            var top = Scenario01SceneBuilder.TightBounds(desk);
            PlaceKenney(root, "computerScreen", "Computer", new Vector3(top.center.x - 0.25f, top.max.y, top.center.z - 0.05f), towardCorridor, 0.4f, false);
            PlaceKenney(root, "computerKeyboard", "Keyboard", new Vector3(top.center.x - 0.25f, top.max.y, top.max.z - 0.15f), towardCorridor, 0.03f, false);
            CreatePaper(root, "Files", new Vector3(top.center.x + 0.35f, top.max.y, top.center.z), 6);

            var canvas = CreateCanvas(root, "Station Sign", new Vector2(700f, 130f), new Vector3(spot.x, spot.y + 1.75f, wallZ + 0.02f),
                Quaternion.LookRotation(Vector3.back), Color.white);
            CreateLabel(canvas, "Label", 20f, 60f, TextAlignmentOptions.Center).text = "<b>Nurses' station</b>";
        }

        /// <summary>
        /// Liam's room 2C as the doctor's office: his bed, nightstand and chair are hidden, a desk with a computer and
        /// files stands against the left wall, his card makes way for a "Doctor's office" sign and the door is open.
        /// The room light follows the mood of the task. Returns the room.
        /// </summary>
        private static Transform BuildDoctorsOffice(Transform level, MoodController mood)
        {
            var room = RequireChild(level, $"Room_{OfficeLabel}");
            foreach (var item in new[] { "Bed", "Nightstand", "Chair" })
                RequireChild(room, $"Furniture/{item}").gameObject.SetActive(false);

            var furniture = new GameObject("Office Furniture").transform;
            furniture.SetParent(room, false);
            var desk = PlaceKenney(furniture, "desk", "Desk", room.TransformPoint(-1.55f, 0f, 2.4f), Quaternion.LookRotation(room.right), 0.78f, false);
            var top = Scenario01SceneBuilder.TightBounds(desk);
            PlaceKenney(furniture, "computerScreen", "Computer", new Vector3(top.min.x + 0.15f, top.max.y, top.center.z + 0.3f),
                Quaternion.LookRotation(room.right), 0.4f, false);
            CreatePaper(furniture, "Paperwork", new Vector3(top.center.x + 0.05f, top.max.y, top.center.z - 0.15f), 3);
            PlaceKenney(furniture, "chairDesk", "Desk Chair", room.TransformPoint(-0.9f, 0f, 3.3f), Quaternion.LookRotation(-room.right), 0.95f, false);

            var card = RequireChild(level, $"Card_{OfficeLabel}");
            var signPosition = card.position - room.forward * 0.03f;
            Object.DestroyImmediate(card.gameObject);
            var canvas = CreateCanvas(room, "Office Sign", new Vector2(420f, 140f), signPosition, Quaternion.LookRotation(room.forward), Color.white);
            CreateLabel(canvas, "Label", 20f, 46f, TextAlignmentOptions.Center).text = "<b>Doctor's office</b>\n<size=60%>Dr. Maes</size>";

            OpenDoor(RequireChild(level, $"Door_{OfficeLabel}"), room);
            AddMoodRoomLight(RequireChild(room, "Light"), mood);
            return room;
        }

        /// <summary>
        /// A stack of sheets of paper (thin white boxes without colliders), slightly rotated, on a desk.
        /// </summary>
        private static void CreatePaper(Transform parent, string name, Vector3 point, int sheets)
        {
            var material = XRTestSceneBuilder.GetOrCreateMaterial(PaperMaterialPath, new Color(0.95f, 0.95f, 0.92f));
            var stack = new GameObject(name).transform;
            stack.SetParent(parent, false);
            stack.position = point;
            for (var i = 0; i < sheets; i++)
            {
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(sheet.GetComponent<Collider>());
                sheet.name = $"Sheet {i + 1}";
                sheet.transform.SetParent(stack, false);
                sheet.transform.localScale = new Vector3(0.21f, 0.004f, 0.297f);
                sheet.transform.position = point + Vector3.up * (0.002f + i * 0.005f);
                sheet.transform.rotation = Quaternion.Euler(0f, (i * 37 % 11) - 5f, 0f);
                sheet.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        /// <summary>
        /// Empty waypoints under Paths/name, all with the given rotation (the last one is where the walker ends up facing).
        /// </summary>
        private static Transform[] CreateWaypoints(Transform paths, string name, Quaternion rotation, params Vector3[] points)
        {
            var path = new GameObject(name).transform;
            path.SetParent(paths, false);
            var waypoints = new Transform[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                waypoints[i] = new GameObject($"Waypoint {i + 1}").transform;
                waypoints[i].SetParent(path, false);
                waypoints[i].SetPositionAndRotation(points[i], rotation);
            }
            return waypoints;
        }

        private static void SetArray(SerializedProperty list, Object[] items)
        {
            list.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        /// <summary>
        /// A capsule on a character's root, so the player does not walk through it.
        /// </summary>
        private static void AddBlocker(GameObject character)
        {
            var blocker = character.AddComponent<CapsuleCollider>();
            blocker.center = new Vector3(0f, 0.85f, 0f);
            blocker.height = 1.7f;
            blocker.radius = 0.3f;
        }

        // ---- Scenario 4: the family of a patient with a poor prognosis (room 1B) ---------------------------------

        /// <summary>
        /// Puts a character on the bed of a room, lying on the first frame of its "Laying" animation: the body along the
        /// bed (head at the headboard), centered on the mattress and with its lowest point just on it. Measured on a
        /// temporary copy in the posed animation, as Liam's HospitalBuilder does for his own patients.
        /// </summary>
        private static void LayOnBed(Transform character, Transform room)
        {
            var anchor = RequireChild(room, "Anchor_Laying");
            var probe = Object.Instantiate(character.gameObject);
            probe.hideFlags = HideFlags.DontSave;
            try
            {
                var animator = probe.GetComponentInChildren<Animator>();
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Laying");
                if (clip == null)
                    throw new System.InvalidOperationException($"[ProCommVR] {character.name} has no 'Laying' animation.");
                var start = new Vector3(anchor.position.x, room.position.y, anchor.position.z);
                probe.transform.SetPositionAndRotation(start, room.rotation);
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0f);
                AnimationMode.EndSampling();
                probe.transform.SetPositionAndRotation(start, room.rotation);

                // Feet to head along the bed, toward the headboard at the back wall.
                probe.transform.Rotate(0f, Vector3.SignedAngle(Flat(Head(animator) - Feet(animator)), room.forward, Vector3.up), 0f, Space.World);
                var middle = (Head(animator) + Feet(animator)) / 2f;
                probe.transform.position += new Vector3(anchor.position.x - middle.x, 0f, anchor.position.z - middle.z);
                probe.transform.position += Vector3.up * (anchor.position.y + 0.01f - LowestPoint(probe));
                character.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(probe);
            }
        }

        private static Vector3 Head(Animator animator)
        {
            return animator.GetBoneTransform(HumanBodyBones.Head).position;
        }

        private static Vector3 Feet(Animator animator)
        {
            return (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
        }

        /// <summary>
        /// Lowest point of the posed (skinned) body in world space.
        /// </summary>
        private static float LowestPoint(GameObject character)
        {
            var lowest = float.MaxValue;
            var mesh = new Mesh();
            foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (var vertex in mesh.vertices)
                    lowest = Mathf.Min(lowest, matrix.MultiplyPoint3x4(vertex).y);
            }
            Object.DestroyImmediate(mesh);
            return lowest;
        }

        /// <summary>
        /// In Liam's saved scene his own patients (rooms 1A, 2A, 3A) stand at the origin, in the lobby wall next to the
        /// lift, instead of in their rooms: his builder only poses them for its screenshots. The ones the shift does not
        /// replace are hidden here (his scene itself is not changed), with a warning, until his scene places them.
        /// </summary>
        private static void HideMisplacedPatients(Transform hospital)
        {
            var characters = hospital.Find("Characters");
            if (characters == null)
                return;
            foreach (Transform patient in characters)
            {
                if (!patient.name.StartsWith("Patient_"))
                    continue;
                var label = patient.name.Substring("Patient_".Length);
                var room = hospital.Find($"Floor{label.Substring(0, 1)}/Room_{label}");
                if (room == null)
                    continue;
                var local = room.InverseTransformPoint(patient.position);
                if (local.z > 0f && local.z < 4.2f && Mathf.Abs(local.x) < 2.1f && Mathf.Abs(local.y) < 1f)
                    continue;
                patient.gameObject.SetActive(false);
                Debug.LogWarning($"[ProCommVR] Liam's {patient.name} is not in room {label} (at {patient.position}); hidden in the shift scene. " +
                                 "Run 'ProCommVR > Hospital > Place Characters' in his scene once his builder saves the poses.");
            }
        }

        /// <summary>
        /// Scenario 4: a quiet private room in a dimmed, soft purple light (mood Distressed). The patient sleeps in bed, his
        /// wife keeps watch on the chair next to the bed (Liam's chair faces the bed) and a monitor on the nightstand shows
        /// fluctuating vital signs. When the player comes near she stands up and reaches toward the nurse; after the best
        /// answer she sits down again and the room turns blue (Somber).
        /// </summary>
        private static PatientRoom BuildBedsideRoom(Transform hospital, string label)
        {
            var room = RequireChild(hospital, $"Floor{FloorOf(label)}/Room_{label}");
            var signPosition = ReplaceCardAndOpenDoor(hospital, room, label);

            var patient = PlaceCharacter(room, SleepingPatientModel, room.position, room.rotation, "Laying");
            patient.name = "Patient (asleep)";
            LayOnBed(patient, room);
            var asleep = new SerializedObject(patient.GetComponentInChildren<NpcAnimationState>());
            asleep.FindProperty("_randomStart").boolValue = false;
            asleep.ApplyModifiedPropertiesWithoutUndo();

            // The wife on Liam's chair, facing the bed.
            var seat = Scenario01SceneBuilder.TightBounds(RequireChild(room, "Furniture/Chair").gameObject).center;
            var towardBed = Flat(RequireChild(room, "Anchor_Sitting").forward).normalized;
            var wife = new GameObject("Wife").transform;
            wife.SetParent(room, false);
            wife.SetPositionAndRotation(new Vector3(seat.x, room.position.y, seat.z), Quaternion.LookRotation(towardBed));
            var body = PlaceCharacter(wife, WifeModel, wife.position, wife.rotation, "Sitting");
            DressAsVisitor(body, "Wife", WifeTopColor, WifeBottomColor);
            var animator = body.GetComponentInChildren<Animator>();

            // Standing up: in front of the chair, toward the door.
            var towardDoor = Flat(room.position - wife.position).normalized;
            var standing = wife.position + towardDoor * 0.7f;

            var mood = AddRoomMood(room);
            BuildVitalSignsMonitor(room);
            var sign = BuildRoomSign(room, signPosition, Quaternion.LookRotation(room.forward), mood);

            var head = wife.position + Vector3.up * 1.9f;
            var presenter = ScenarioTestSceneBuilder.CreatePresenter("Scenario Presenter", body, head, room.TransformPoint(0.3f, 1.15f, 1.6f));
            presenter.transform.SetParent(room, true);
            presenter.transform.Find("NPC Bubble Anchor").gameObject.AddComponent<FacePlayer>();

            var attention = body.gameObject.AddComponent<NpcAttention>();
            var serialized = new SerializedObject(attention);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_body").objectReferenceValue = body;
            serialized.FindProperty("_range").floatValue = 2.4f;
            serialized.FindProperty("_attentionPose").objectReferenceValue = CreatePose(wife, "Attention", standing, Quaternion.LookRotation(towardDoor));
            serialized.FindProperty("_facePlayer").boolValue = true;
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_animatorState").stringValue = "Talking";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // After the choice: sits down again (calmer), keeps talking hopefully, turns to her husband (sad), or turns
            // away to the window in tears.
            AddReactor(wife, presenter, body, animator,
                new PoseSpec(NpcPose.Unchanged, null, "Talking"),
                new PoseSpec(NpcPose.Engaged, wife.rotation, "Sitting", wife.position),
                new PoseSpec(NpcPose.TurnsAway, Quaternion.LookRotation(room.right), "Crying", standing),
                new PoseSpec(NpcPose.Withdrawn, Quaternion.LookRotation(-towardDoor), "Sad", standing));

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
        /// A patient monitor on the nightstand next to the head of the bed, turned to the room: dark screen with heart
        /// rate, oxygen saturation and blood pressure that fluctuate (<see cref="VitalSignsMonitor"/>).
        /// </summary>
        private static void BuildVitalSignsMonitor(Transform room)
        {
            var stand = Scenario01SceneBuilder.TightBounds(RequireChild(room, "Furniture/Nightstand").gameObject);
            var facing = Quaternion.LookRotation(-Flat(room.position - stand.center).normalized);
            var canvas = CreateCanvas(room, "Vital Signs Monitor", new Vector2(360f, 260f),
                new Vector3(stand.center.x, stand.max.y + 0.2f, stand.center.z), facing, new Color(0.04f, 0.05f, 0.06f, 1f));
            var monitor = canvas.gameObject.AddComponent<VitalSignsMonitor>();
            var serialized = new SerializedObject(monitor);
            var rows = new[] { ("_heartRate", new Color(0.3f, 0.95f, 0.4f)), ("_saturation", new Color(0.35f, 0.8f, 1f)), ("_bloodPressure", new Color(1f, 0.45f, 0.4f)) };
            for (var i = 0; i < rows.Length; i++)
            {
                var label = CreateLabel(canvas, rows[i].Item1.TrimStart('_'), 20f, 70f, TextAlignmentOptions.Left);
                label.color = rows[i].Item2;
                label.rectTransform.anchorMin = new Vector2(0f, 1f - (i + 1) / 3f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f - i / 3f);
                label.rectTransform.offsetMin = new Vector2(24f, 4f);
                label.rectTransform.offsetMax = new Vector2(-24f, -4f);
                serialized.FindProperty(rows[i].Item1).objectReferenceValue = label;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
                destinations[level].SetPositionAndRotation(new Vector3(LiftArrivalX, level * FloorHeight + LiftArrivalHeight, LiftCenterZ), Quaternion.LookRotation(Vector3.right));
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

        private static int FloorOf(string location)
        {
            if (location == CorridorTask)
                return EncounterFloor;
            return location == StationTask ? StationFloor : int.Parse(location.Substring(0, 1));
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
        /// The shift: Scenario 2 as task 2 (the daughter, Tense), Scenario 3 as task 3 (the doctor, Neutral), Scenario 4 as
        /// task 4 (the wife, Distressed) and Scenario 1 as placeholder for the other tasks (every patient Happy), ids
        /// patient-01 to patient-05. An existing plan is kept; only empty patient ids are filled in.
        /// </summary>
        private static ShiftPlan GetOrCreatePlan()
        {
            var plan = AssetDatabase.LoadAssetAtPath<ShiftPlan>(PlanPath);
            if (plan != null)
                return FillMissingPatientIds(plan);

            var scenario = ScenarioTestSceneBuilder.LoadRequired<Scenario>(ScenarioPath, "The Scenario 1 asset is missing.");
            var scenario2 = ScenarioTestSceneBuilder.LoadRequired<Scenario>(Scenario2Path, "The Scenario 2 asset is missing.");
            var scenario3 = ScenarioTestSceneBuilder.LoadRequired<Scenario>(Scenario3Path, "The Scenario 3 asset is missing.");
            var scenario4 = ScenarioTestSceneBuilder.LoadRequired<Scenario>(Scenario4Path, "The Scenario 4 asset is missing.");
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PlanPath));
            plan = ScriptableObject.CreateInstance<ShiftPlan>();
            AssetDatabase.CreateAsset(plan, PlanPath);

            var serialized = new SerializedObject(plan);
            var tasks = serialized.FindProperty("_tasks");
            tasks.arraySize = TaskLocations.Length;
            for (var i = 0; i < TaskLocations.Length; i++)
            {
                var location = TaskLocations[i];
                var task = tasks.GetArrayElementAtIndex(i);
                task.FindPropertyRelative("_patientId").stringValue = PatientId(i);
                task.FindPropertyRelative("_scenario").objectReferenceValue =
                    location == CorridorTask ? scenario2 : location == StationTask ? scenario3 : location == BedsideRoomLabel ? scenario4 : scenario;
                // Scenario 3 and 4: a family member is the NPC, but the task is about the patient.
                task.FindPropertyRelative("_patientName").stringValue =
                    location == StationTask ? "Patient X's family" : location == BedsideRoomLabel ? "Patient X" : "";
                task.FindPropertyRelative("_patientMood").enumValueIndex = (int)(location == CorridorTask ? Mood.Tense
                    : location == StationTask ? Mood.Neutral
                    : location == BedsideRoomLabel ? Mood.Distressed
                    : Mood.Happy);
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
