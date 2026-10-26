using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ProCommVR.Atmosphere;
using ProCommVR.Navigation;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using InputDevice = UnityEngine.InputSystem.InputDevice;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Plays one shift in ShiftHospital the way a player would: a simulated headset and right controller
    /// (the XRI simulation devices that the XR Interaction Simulator also uses) point the laser at the start
    /// computer, the lift buttons and the dialogue bubbles and pull the trigger. The player rides the lift to the
    /// floor of each room and is moved from room to room by teleporting the XR Origin. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftLoop     (all 5 tasks, then results)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftTimeout  (timer runs out after 1 task)
    /// Scenario 2 (the corridor of floor 3), 3 (the nurse's station of floor 2), 4 (room 1B) and 5 (room 1C) have their
    /// own tests in PlayModeSmokeTest.Scenario02.cs to PlayModeSmokeTest.Scenario05.cs.
    /// Optional: -smokeTestScreenshots &lt;folder&gt; saves a few camera images along the way (needs graphics).
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string ShiftScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const float HeadHeight = 1.6f;
        private const float ClickTimeout = 3f;
        private const string Scenario01Id = "disappointed-patient";

        private static XRSimulatedHMD _hmd;
        private static XRSimulatedController _controller;
        private static Vector3 _headWorld;
        private static Quaternion _headRotation = Quaternion.identity;
        private static Vector3 _aimTarget;
        private static Vector3? _lastOriginPosition;
        private static bool _triggerDown;
        private static string _screenshotFolder;
        private static int _screenshotIndex;

        [MenuItem("ProCommVR/Tests/Shift Loop (Shift Hospital)")]
        public static void RunShiftLoop()
        {
            Launch("shift", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Shift Timeout (Shift Hospital)")]
        public static void RunShiftTimeout()
        {
            Launch("shift-timeout", ShiftScenePath);
        }

        private static IEnumerator ShiftLoop(bool timeout)
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var manager = Object.FindAnyObjectByType<ShiftManager>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var startButton = Object.FindAnyObjectByType<ShiftStartButton>();
            var results = Object.FindAnyObjectByType<ResultsScreen>(FindObjectsInactive.Include);
            Check(manager != null && origin != null && startButton != null && results != null, "scene has a ShiftManager, XR Origin, start computer and results screen");
            if (Errors.Count > 0)
                yield break;

            AddSimulatedDevices();
            EditorApplication.update += UpdateDevices;
            try
            {
                // Briefing: the player stands at the start position, rooms already show their patient and mood.
                _headWorld = Camera.main.transform.position;
                _headWorld.y = origin.transform.position.y + HeadHeight;
                _headRotation = Quaternion.LookRotation(Flat(origin.transform.forward));
                yield return 1f;
                Check(manager.State == ShiftState.Briefing, "shift waits in the briefing room");
                Check(manager.AssignedRooms.Count == 5, $"5 patients assigned (got {manager.AssignedRooms.Count})");
                // In task order: room 1A, the corridor of floor 3 (Scenario 2), the nurse's station of floor 2 (Scenario 3),
                // 1B (Scenario 4), 1C (Scenario 5).
                var rooms = manager.AssignedRooms.ToArray();
                var seated = rooms.Where(r => r.Task.Scenario.Id == Scenario01Id).ToArray();
                Check(rooms[1].Countdown != null && rooms[1].Task.Scenario.Id == Scenario02Id, $"task 2 is Scenario 2 in {rooms[1].RoomName}");
                Check(rooms[2].Task.Scenario.Id == Scenario03Id && rooms[2].GetComponentInChildren<BusyNpc>() != null, $"task 3 is Scenario 3 in {rooms[2].RoomName}");
                Check(rooms[3].Task.Scenario.Id == Scenario04Id && rooms[3].RoomName == "Room 1B", $"task 4 is Scenario 4 in {rooms[3].RoomName}");
                Check(rooms[4].Task.Scenario.Id == Scenario05Id && rooms[4].RoomName == "Room 1C", $"task 5 is Scenario 5 in {rooms[4].RoomName}");
                var ids = manager.AssignedRooms.Select(r => r.PatientId).ToArray();
                Check(ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct().Count() == ids.Length,
                    $"every patient has a unique id ({string.Join(", ", ids)})");
                Check(manager.AssignedRooms.All(r => manager.FindRoom(r.PatientId) == r), "rooms are found by patient id");
                Check(seated.Select(r => r.Task.PatientName).Distinct().Count() == 1, "display names may stay the same (all 'Patient X')");
                foreach (var room in rooms)
                    CheckMood(room, room.Task.PatientMood);
                // Animators of patients out of view may skip updating their bones; animate them all for the check.
                foreach (var animator in rooms.Select(r => r.GetComponentInChildren<Animator>()))
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                yield return 0.3f;
                foreach (var room in seated)
                    CheckSitting(room);
                yield return 2.5f; // the mood sounds fade in
                foreach (var room in rooms)
                    CheckMoodSound(room);
                Check(Object.FindObjectsByType<MoodAudio>(FindObjectsSortMode.None).Length == rooms.Length, "only the patient rooms play a mood sound");
                Screenshot("briefing");
                var start = origin.transform.position;
                var startRotation = _headRotation;

                // One patient's mood changes: only that room follows (sign, light, crossfade to the new sound).
                var changed = manager.AssignedRooms[2];
                var oldSource = changed.GetComponentInChildren<MoodAudio>().CurrentSource;
                Check(manager.SetPatientMood(changed.PatientId, Mood.Somber), $"set the mood of {changed.PatientId} by id");
                yield return 0.5f;
                var newSource = changed.GetComponentInChildren<MoodAudio>().CurrentSource;
                Check(newSource != oldSource && newSource.isPlaying && oldSource.isPlaying, "the old and the new sound overlap during the crossfade");
                yield return 2.5f;
                CheckMood(changed, Mood.Somber);
                CheckMoodSound(changed);
                Check(!oldSource.isPlaying, "the old sound stops after the crossfade");
                foreach (var room in manager.AssignedRooms.Where(r => r != changed))
                    Check(room.MoodController.CurrentMood == room.Task.PatientMood, $"{room.PatientId} keeps its own mood");
                manager.SetPatientMood(changed.PatientId, Mood.Happy);
                yield return 2.5f;
                CheckMood(changed, Mood.Happy);
                CheckMoodSound(changed);

                if (timeout)
                    SetPrivateField(manager, "_shiftDuration", 4f);

                foreach (var step in Click(startButton.GetComponent<Collider>().bounds.center, () => manager.State == ShiftState.Running))
                    yield return step;
                Check(manager.State == ShiftState.Running, "laser + trigger on the computer starts the shift");
                if (manager.State != ShiftState.Running)
                    yield break;

                if (!string.IsNullOrEmpty(_screenshotFolder))
                {
                    var briefingBoard = Object.FindObjectsByType<ShiftBoard>(FindObjectsSortMode.None).First(b => b.name.Contains("briefing"));
                    _headRotation = Quaternion.LookRotation(briefingBoard.transform.position - _headWorld);
                    yield return 1.5f;
                    Screenshot("briefing-board");
                }

                var toDo = timeout ? rooms.Take(1).ToArray() : rooms;
                foreach (var room in toDo)
                {
                    IEnumerable steps;
                    if (room.Task.Scenario.Id == Scenario02Id)
                        steps = CareForCorridor(manager, origin, room, "consultation-room", Answer.Best);
                    else if (room.Task.Scenario.Id == Scenario03Id)
                        steps = CareForStation(manager, origin, room, Answer.Best);
                    else if (room.Task.Scenario.Id == Scenario04Id)
                        steps = CareForBedside(manager, origin, room, Answer.Best);
                    else if (room.Task.Scenario.Id == Scenario05Id)
                        steps = CareForSharedRoom(manager, origin, room, true, Answer.Best);
                    else
                        steps = CareFor(manager, origin, room);
                    foreach (var step in steps)
                        yield return step;
                }

                if (timeout)
                {
                    var waitUntil = EditorApplication.timeSinceStartup + 6f;
                    while (manager.State == ShiftState.Running && EditorApplication.timeSinceStartup < waitUntil)
                        yield return null;
                    Check(manager.State == ShiftState.Ended && manager.EndReason == ShiftEndReason.TimeUp, $"shift ends when the timer runs out (state {manager.State}, reason {manager.EndReason})");
                    Check(manager.CompletedCount == 1, $"1 patient cared for before the time was up (got {manager.CompletedCount})");
                    Check(rooms.Skip(1).All(r => ClickableBubbles(r).Length == 0), "the other rooms stop their conversation");
                }
                else
                {
                    Check(manager.State == ShiftState.Ended && manager.EndReason == ShiftEndReason.AllTasksDone, $"shift ends after the 5th patient (state {manager.State}, reason {manager.EndReason})");
                    Check(manager.TimeRemaining > 0f, "timer had time left");
                }

                yield return 0.5f;
                Check(results.gameObject.activeInHierarchy, "results screen is shown");
                var body = results.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToArray();
                Log($"results: {string.Join(" | ", body).Replace("\n", " / ")}");
                Check(rooms.All(r => body.Any(t => t.Contains(r.RoomName))), "results list every room");
                var boards = Object.FindObjectsByType<ShiftBoard>(FindObjectsSortMode.None);
                Check(boards.Length == 4, $"four shift boards: lobby and the corridors of floor 1, 2 and 3 (got {boards.Length})");
                foreach (var board in boards)
                {
                    var text = board.GetComponentInChildren<TMPro.TMP_Text>().text;
                    Check(text.Contains($"Cared for: {manager.CompletedCount} / 5") && text.Contains("Shift ended"), $"{board.name} shows the end state");
                }
                // Look at the results screen (it was placed in front of the player).
                _headRotation = Quaternion.LookRotation(Flat(results.transform.position - _headWorld));
                yield return 0.3f;
                Screenshot(timeout ? "results-timeout" : "results");

                // The game is over: back in the lobby, the start computer does nothing any more.
                foreach (var step in RideLiftTo(origin, start.y))
                    yield return step;
                Teleport(origin, start, start);
                _headRotation = startRotation;
                foreach (var step in Click(startButton.GetComponent<Collider>().bounds.center, () => false, quiet: true))
                    yield return step;
                Check(manager.State == ShiftState.Ended, "nothing restarts after the shift");
            }
            finally
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }

        /// <summary>
        /// Walks (teleports) to the patient, waits for the 4 options, picks one with the laser, then clicks "Finish".
        /// </summary>
        private static IEnumerable CareFor(ShiftManager manager, XROrigin origin, PatientRoom room)
        {
            var patient = room.GetComponentInChildren<NpcReactor>().transform;
            var presenter = room.GetComponentInChildren<ScenarioPresenter>();
            var first = manager.AssignedRooms[0] == room;
            foreach (var step in RideLiftTo(origin, room.transform.position.y))
                yield return step;

            // The door sign in the corridor (screenshots only).
            var sign = room.GetComponentInChildren<RoomSign>().transform;
            if (first && !string.IsNullOrEmpty(_screenshotFolder))
            {
                _headWorld = sign.position - sign.forward * 1.6f + Vector3.up * 0.1f - sign.right * 0.6f;
                _headRotation = Quaternion.LookRotation(sign.position - _headWorld);
                yield return 0.3f;
                Screenshot("corridor-door-sign");
            }

            // From the doorway, look at the patient (and the NPC bubble).
            var door = room.transform.position;
            Teleport(origin, Vector3.Lerp(patient.position, door, 0.75f), patient.position);
            yield return 0.4f;
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options from the doorway");
            Screenshot($"{room.RoomName.ToLowerInvariant().Replace(' ', '-')}-doorway");
            if (first && !string.IsNullOrEmpty(_screenshotFolder))
            {
                // Side view of the patient, to check the sitting pose (only when taking screenshots).
                var side = Vector3.Cross(Vector3.up, Flat(patient.position - door)).normalized;
                _headWorld = patient.position - side * 1.6f + Vector3.up * 1.2f;
                _headRotation = Quaternion.LookRotation(patient.position + Vector3.up * 0.7f - _headWorld);
                yield return 0.3f;
                Screenshot("room-patient-side");
                Teleport(origin, Vector3.Lerp(patient.position, door, 0.75f), patient.position);
                yield return 0.3f;
            }

            // Step within 1.5 m of the patient.
            var toDoor = Flat(door - patient.position).normalized;
            Teleport(origin, patient.position + toDoor * 1.0f, patient.position);
            var waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var options = ClickableBubbles(room);
            Check(options.Length == 4, $"{room.RoomName}: 4 options within 1.5 m (got {options.Length})");
            if (options.Length == 0)
                yield break;
            if (first)
                Screenshot("room-options");

            var option = options[Array.IndexOf(manager.AssignedRooms.ToArray(), room) % options.Length];
            foreach (var step in Click(Center(option), () => room.ChosenOption != null))
                yield return step;
            Check(room.ChosenOption != null, $"{room.RoomName}: laser + trigger picks an option");

            var waitForContinue = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length != 1 && EditorApplication.timeSinceStartup < waitForContinue)
                yield return null;
            var finish = ClickableBubbles(room);
            Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
            if (finish.Length == 0)
                yield break;
            if (first)
                Screenshot("room-reaction");

            foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                yield return step;
            Check(manager.IsCompleted(room), $"{room.RoomName}: Finish marks the patient as cared for");
            Check(manager.IsCompleted(room.PatientId) && manager.CompletedCount == manager.AssignedRooms.Count(r => manager.IsCompleted(r.PatientId)),
                $"{room.RoomName}: progress is kept per patient id ({room.PatientId})");
            yield return 0.2f;
            Check(presenter.GetComponentsInChildren<DialogueBubble>().Length == 0, $"{room.RoomName}: bubbles are cleared");
        }

        private static void CheckMood(PatientRoom room, Mood expected)
        {
            var mood = room.MoodController;
            var lights = RoomLights(room);
            var sign = room.GetComponentInChildren<RoomSign>();
            var patch = sign.GetComponentsInChildren<Image>().First(i => i.gameObject != sign.gameObject);
            var color = mood.CurrentColor;
            Check(mood.CurrentMood == expected, $"{room.RoomName}: mood is {expected} (got {mood.CurrentMood})");
            if (expected == Mood.Happy)
                Check(color.g > color.r && color.g > color.b, $"{room.RoomName}: Happy color is green ({color})");
            if (expected == Mood.Alarm)
                Check(color.r > 0.8f && color.g < 0.2f && color.b < 0.2f, $"{room.RoomName}: Alarm color is red ({color})");
            Check(lights.Length > 0 && lights.All(l => l.color == color), $"{room.RoomName}: its {lights.Length} light(s) have the mood color ({string.Join(", ", lights.Select(l => l.color))})");
            Check(patch.color == color, $"{room.RoomName}: door sign shows the mood color ({patch.color})");
        }

        /// <summary>
        /// The lights that follow the mood of a room: its own room light, or for the corridor encounter the corridor
        /// lights and the light of the consultation room.
        /// </summary>
        private static Light[] RoomLights(PatientRoom room)
        {
            return Object.FindObjectsByType<MoodRoomLight>(FindObjectsSortMode.None)
                .Where(l => l.MoodController == room.MoodController)
                .Select(l => l.GetComponent<Light>())
                .ToArray();
        }

        /// <summary>
        /// The room's MoodAudio plays the sound of the current mood, 3D, at the profile's volume.
        /// </summary>
        private static void CheckMoodSound(PatientRoom room)
        {
            var profile = room.MoodController.CurrentProfile;
            var source = room.GetComponentInChildren<MoodAudio>().CurrentSource;
            Check(profile.Sound != null && source.clip == profile.Sound && source.isPlaying,
                $"{room.RoomName}: plays the {profile.Mood} sound ({(source.clip != null ? source.clip.name : "none")}, playing {source.isPlaying})");
            Check(Mathf.Approximately(source.volume, profile.SoundVolume) && source.spatialBlend == 1f,
                $"{room.RoomName}: sound at volume {source.volume:F2}, 3D");
        }

        /// <summary>
        /// The patient is visibly sitting: Animator in the Sitting state, hips and head clearly lower than when standing,
        /// and the hips above the seat of the chair next to the bed.
        /// </summary>
        private static void CheckSitting(PatientRoom room)
        {
            var animator = room.GetComponentInChildren<Animator>();
            var floor = room.transform.position.y;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var chair = RendererBounds(room.transform.Find("Furniture/Chair").gameObject);
            var bed = RendererBounds(room.transform.Find("Furniture/Bed").gameObject);
            var hipsOverSeat = new Vector2(hips.x - chair.center.x, hips.z - chair.center.z).magnitude;
            var chairToBed = Vector2.Distance(new Vector2(chair.center.x, chair.center.z), new Vector2(bed.ClosestPoint(chair.center).x, bed.ClosestPoint(chair.center).z));

            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Sitting"), $"{room.RoomName}: Animator plays Sitting");
            Check(hips.y - floor > 0.3f && hips.y - floor < 0.75f, $"{room.RoomName}: hips at sitting height ({hips.y - floor:F2} m)");
            Check(head.y - floor < 1.45f, $"{room.RoomName}: head lower than standing ({head.y - floor:F2} m)");
            Check(hipsOverSeat < 0.3f, $"{room.RoomName}: hips above the chair seat ({hipsOverSeat:F2} m from its center)");
            Check(chairToBed < 0.5f, $"{room.RoomName}: chair stands next to the bed ({chairToBed:F2} m)");
        }

        /// <summary>
        /// Rides the lift when the player is on another floor than floorY: walks (teleports) to the lift panel on the
        /// current floor and points the laser at the button of the target floor.
        /// </summary>
        private static IEnumerable RideLiftTo(XROrigin origin, float floorY)
        {
            var current = origin.transform.position.y;
            if (Mathf.Abs(current - floorY) < 0.5f)
                yield break;

            var button = Object.FindObjectsByType<LiftButton>(FindObjectsSortMode.None).FirstOrDefault(b =>
                b.transform.position.y > current && b.transform.position.y < current + 2.5f && Mathf.Abs(b.Destination.position.y - floorY) < 0.1f);
            Check(button != null, $"a lift button on the floor at {current:F1} m to the floor at {floorY:F1} m");
            if (button == null)
                yield break;

            var panel = button.transform.position;
            Teleport(origin, new Vector3(panel.x + 0.9f, 0f, panel.z), panel - Vector3.up * 1.2f);
            yield return 0.3f;
            foreach (var step in Click(button.GetComponent<Collider>().bounds.center, () => Mathf.Abs(origin.transform.position.y - floorY) < 0.15f))
                yield return step;
            Check(Mathf.Abs(origin.transform.position.y - floorY) < 0.15f,
                $"lift button {button.name} takes the player to the floor at {floorY:F1} m (origin at {origin.transform.position.y:F2} m)");

            // The simulated head follows the player to the new floor, looking out of the lift.
            var arrived = origin.transform.position;
            Teleport(origin, arrived, arrived + button.Destination.forward * 3f);
            yield return 0.3f;
            var standing = origin.transform.position.y - floorY;
            Check(standing > -0.02f && standing < 0.1f, $"the player stands on the floor after the ride ({standing:F2} m above it), not in it");
            Screenshot($"lift-arrival-{button.Destination.name.Replace("Arrival ", "").ToLowerInvariant()}");
        }

        /// <summary>
        /// World bounds of all renderers. In Play mode static batching replaces the meshes, so mesh-based bounds don't work.
        /// </summary>
        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>
        /// Aims the controller at a point, holds the trigger briefly and releases it. Stops early once done() is true.
        /// The trigger stays down for at least 0.2 s and 3 game frames: in batchmode a frame can take longer than
        /// 0.2 s, and a press and release in the same frame is never seen as a select. A press that still had no effect
        /// is repeated once, and the retry is logged.
        /// </summary>
        private static IEnumerable Click(Vector3 target, Func<bool> done, bool quiet = false)
        {
            _aimTarget = target;
            yield return 0.25f;
            for (var attempt = 1; attempt <= 2 && !done(); attempt++)
            {
                if (attempt > 1 && !quiet)
                    Log($"click at {target} had no effect, pressing the trigger once more.");
                _triggerDown = true;
                var pressFrame = Time.frameCount;
                yield return 0.2f;
                while (Time.frameCount < pressFrame + 3)
                    yield return null;
                _triggerDown = false;
                var waitUntil = EditorApplication.timeSinceStartup + ClickTimeout;
                while (!done() && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
            }
            if (!done() && !quiet)
                Log($"click at {target} had no effect; controller at {ControllerWorld()}. {DescribeBlockers(target)} {DescribeInteractors()}");
        }

        /// <summary>
        /// The colliders between the controller and the target, which would catch the ray before it reaches the target.
        /// </summary>
        private static string DescribeBlockers(Vector3 target)
        {
            var from = ControllerWorld();
            var hits = Physics.RaycastAll(from, target - from, Vector3.Distance(from, target) - 0.01f, ~0, QueryTriggerInteraction.Collide)
                .OrderBy(h => h.distance)
                .Select(h => $"{h.collider.transform.parent?.name}/{h.collider.name} at {h.distance:F2} m");
            return $"Colliders on the way: [{string.Join(", ", hits)}].";
        }

        private static string DescribeInteractors()
        {
            var text = new System.Text.StringBuilder();
            foreach (var interactor in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                text.Append($"[{interactor.transform.parent?.name}/{interactor.name} active {interactor.isActiveAndEnabled}, " +
                            $"pos {interactor.transform.position}, fwd {interactor.transform.forward}, hovered {interactor.interactablesHovered.Count} " +
                            $"({string.Join(", ", interactor.interactablesHovered.Select(i => $"{i.transform.parent?.name}/{i.transform.name}"))}), " +
                            $"selectInput {interactor.selectInput.ReadValue():F2}, uiPress {interactor.uiPressInput.ReadValue():F2}] ");
            }
            text.Append($"controller device tracked {_controller?.isTracked.isPressed}, enabled {_controller?.enabled}, trigger {_controller?.trigger.ReadValue():F2}, pos {_controller?.devicePosition.ReadValue()}; ");
            text.Append($"settings: updateMode {InputSystem.settings.updateMode}, background {InputSystem.settings.backgroundBehavior}, editorPlayMode {InputSystem.settings.editorInputBehaviorInPlayMode}, focused {Application.isFocused}; ");
            foreach (var manager in Object.FindObjectsByType<XRInputModalityManager>(FindObjectsSortMode.None))
                text.Append($"modality {manager.name} enabled {manager.isActiveAndEnabled} rightController {(manager.rightController != null ? manager.rightController.activeSelf.ToString() : "none")}; ");
            text.Append($"camera {Camera.main.transform.position}; devices: {string.Join(", ", InputSystem.devices.Select(d => $"{d.name}[{string.Join("/", d.usages)}]{(d.enabled ? "" : " disabled")}"))}");
            return text.ToString();
        }

        private static DialogueBubble[] ClickableBubbles(PatientRoom room)
        {
            return room.GetComponentsInChildren<DialogueBubble>()
                .Where(b => b.GetComponent<Button>().enabled)
                .ToArray();
        }

        private static Vector3 Center(DialogueBubble bubble)
        {
            var corners = new Vector3[4];
            ((RectTransform)bubble.transform).GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        /// <summary>
        /// Moves the XR Origin so the head stands at the point, looking at the target.
        /// </summary>
        private static void Teleport(XROrigin origin, Vector3 point, Vector3 lookAt)
        {
            var controller = origin.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            origin.transform.position = new Vector3(point.x, origin.transform.position.y, point.z);
            _lastOriginPosition = origin.transform.position;
            Physics.SyncTransforms();
            if (controller != null)
                controller.enabled = true;

            _headWorld = new Vector3(point.x, origin.transform.position.y + HeadHeight, point.z);
            var look = lookAt + Vector3.up * 1.2f - _headWorld;
            _headRotation = Quaternion.LookRotation(look);
        }

        private static Vector3 ControllerWorld()
        {
            return _headWorld + _headRotation * new Vector3(0.15f, -0.3f, 0.25f);
        }

        // ---- Simulated devices ---------------------------------------------------------------------------------

        private static void AddSimulatedDevices()
        {
            _hmd = InputSystem.AddDevice(new InputDeviceDescription
            {
                product = nameof(XRSimulatedHMD),
                capabilities = new XRDeviceDescriptor { characteristics = XRInputTrackingAggregator.Characteristics.hmd }.ToJson(),
            }) as XRSimulatedHMD;
            _controller = InputSystem.AddDevice(new InputDeviceDescription
            {
                product = nameof(XRSimulatedController),
                capabilities = new XRDeviceDescriptor
                {
                    deviceName = $"{nameof(XRSimulatedController)} - {UnityEngine.InputSystem.CommonUsages.RightHand}",
                    characteristics = XRInputTrackingAggregator.Characteristics.rightController,
                }.ToJson(),
            }) as XRSimulatedController;
            if (_hmd == null || _controller == null)
                throw new InvalidOperationException("Could not create the simulated XR devices.");
            InputSystem.SetDeviceUsage(_controller, UnityEngine.InputSystem.CommonUsages.RightHand);
            Log("simulated HMD and right controller added.");
        }

        private static void RemoveSimulatedDevices()
        {
            foreach (InputDevice device in new InputDevice[] { _hmd, _controller })
            {
                if (device != null && device.added)
                    InputSystem.RemoveDevice(device);
            }
            _hmd = null;
            _controller = null;
            _lastOriginPosition = null;
        }

        /// <summary>
        /// Queues the head and controller poses (in tracking space, relative to the Camera Offset) as input events on
        /// every editor tick. The Input System routes these XR device events to the game, also without focus.
        /// When the game moves the XR Origin (the lift's teleport, gravity), the head moves along, as a real headset
        /// does; otherwise the head would stay behind on the old floor.
        /// </summary>
        private static void UpdateDevices()
        {
            if (_hmd == null || !EditorApplication.isPlaying)
                return;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
            if (_lastOriginPosition.HasValue)
                _headWorld += origin.transform.position - _lastOriginPosition.Value;
            _lastOriginPosition = origin.transform.position;
            var space = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;

            var headPosition = space.InverseTransformPoint(_headWorld);
            var headRotation = Quaternion.Inverse(space.rotation) * _headRotation;
            var hmdState = new XRSimulatedHMDState
            {
                centerEyePosition = headPosition,
                centerEyeRotation = headRotation,
                devicePosition = headPosition,
                deviceRotation = headRotation,
                leftEyePosition = headPosition,
                leftEyeRotation = headRotation,
                rightEyePosition = headPosition,
                rightEyeRotation = headRotation,
                isTracked = true,
                trackingState = 3, // position and rotation
            };
            InputSystem.QueueStateEvent(_hmd, hmdState);

            var hand = ControllerWorld();
            var aim = _aimTarget == Vector3.zero ? _headRotation : Quaternion.LookRotation(_aimTarget - hand);
            var controllerState = new XRSimulatedControllerState
            {
                devicePosition = space.InverseTransformPoint(hand),
                deviceRotation = Quaternion.Inverse(space.rotation) * aim,
                isTracked = true,
                trackingState = 3,
                trigger = _triggerDown ? 1f : 0f,
                batteryLevel = 1f,
            };
            controllerState = controllerState.WithButton(ControllerButton.TriggerButton, _triggerDown);
            InputSystem.QueueStateEvent(_controller, controllerState);
        }

        // ---- Helpers ---------------------------------------------------------------------------------------------

        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException($"Field {field} not found on {target.GetType().Name}.");
            info.SetValue(target, value);
            Log($"{target.GetType().Name}.{field} set to {value} for this test.");
        }

        /// <summary>
        /// Sets a property with a private setter, e.g. the time left of a countdown, to shorten a test.
        /// </summary>
        private static void SetPrivateProperty(object target, string property, object value)
        {
            var setter = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);
            if (setter == null)
                throw new InvalidOperationException($"Property {property} with a setter not found on {target.GetType().Name}.");
            setter.Invoke(target, new[] { value });
            Log($"{target.GetType().Name}.{property} set to {value} for this test.");
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        /// <summary>
        /// Renders the main camera (the player's view) to a PNG in the screenshot folder, if one was given.
        /// </summary>
        private static void Screenshot(string name)
        {
            if (string.IsNullOrEmpty(_screenshotFolder) || Camera.main == null)
                return;
            Directory.CreateDirectory(_screenshotFolder);
            var camera = Camera.main;
            var texture = new RenderTexture(1280, 960, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            var path = Path.Combine(_screenshotFolder, $"{++_screenshotIndex:00}-{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
            Log($"screenshot {path}");
        }
    }
}
