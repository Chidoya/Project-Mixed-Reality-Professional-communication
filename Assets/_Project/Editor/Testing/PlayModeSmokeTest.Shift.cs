using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ProCommVR.Atmosphere;
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
    /// computer and the dialogue bubbles and pull the trigger. The player is moved from room to room by
    /// teleporting the XR Origin. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftLoop     (all 5 tasks, then results)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftTimeout  (timer runs out after 1 task)
    /// Optional: -smokeTestScreenshots &lt;folder&gt; saves a few camera images along the way (needs graphics).
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string ShiftScenePath = "Assets/_Project/Scenes/ShiftHospital.unity";
        private const float HeadHeight = 1.6f;
        private const float ClickTimeout = 3f;

        private static XRSimulatedHMD _hmd;
        private static XRSimulatedController _controller;
        private static Vector3 _headWorld;
        private static Quaternion _headRotation = Quaternion.identity;
        private static Vector3 _aimTarget;
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
            var rooms = Object.FindObjectsByType<PatientRoom>(FindObjectsSortMode.None).OrderBy(r => r.RoomName).ToArray();
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
                foreach (var room in rooms)
                    CheckMood(room);
                Screenshot("briefing");

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
                    var sign = rooms[1].GetComponentInChildren<RoomSign>().transform;
                    _headWorld = sign.position - sign.forward * 1.6f + Vector3.up * 0.1f - sign.right * 0.6f;
                    _headRotation = Quaternion.LookRotation(sign.position - _headWorld);
                    yield return 0.3f;
                    Screenshot("corridor-door-sign");
                }

                var toDo = timeout ? rooms.Take(1).ToArray() : rooms;
                foreach (var room in toDo)
                {
                    foreach (var step in CareFor(manager, origin, room))
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
                Check(body.Any(t => t.Contains("Room 5")), "results list every room");
                var boards = Object.FindObjectsByType<ShiftBoard>(FindObjectsSortMode.None);
                Check(boards.Length == 2, "two shift boards");
                foreach (var board in boards)
                {
                    var text = board.GetComponentInChildren<TMPro.TMP_Text>().text;
                    Check(text.Contains($"Cared for: {manager.CompletedCount} / 5") && text.Contains("Shift ended"), $"{board.name} shows the end state");
                }
                // Look at the results screen (it was placed in front of the player).
                _headRotation = Quaternion.LookRotation(Flat(results.transform.position - _headWorld));
                yield return 0.3f;
                Screenshot(timeout ? "results-timeout" : "results");

                // The game is over: the start computer does nothing any more.
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

            // From the doorway, look at the patient (and the NPC bubble).
            var door = room.GetComponentInChildren<RoomSign>().transform.position;
            Teleport(origin, Vector3.Lerp(patient.position, door, 0.75f), patient.position);
            yield return 0.4f;
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options from the doorway");
            if (room.RoomName == "Room 1" && !string.IsNullOrEmpty(_screenshotFolder))
            {
                Screenshot("room-doorway");
                // Side view of the patient, to check the sitting pose (only when taking screenshots).
                var side = Vector3.Cross(Vector3.up, Flat(patient.position - door)).normalized;
                _headWorld = patient.position + side * 1.6f + Vector3.up * 1.2f;
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
            if (room.RoomName == "Room 1")
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
            if (room.RoomName == "Room 1")
                Screenshot("room-reaction");

            foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                yield return step;
            Check(manager.IsCompleted(room), $"{room.RoomName}: Finish marks the patient as cared for");
            yield return 0.2f;
            Check(presenter.GetComponentsInChildren<DialogueBubble>().Length == 0, $"{room.RoomName}: bubbles are cleared");
        }

        private static void CheckMood(PatientRoom room)
        {
            var mood = room.GetComponentInChildren<MoodController>();
            var light = room.GetComponentInChildren<MoodRoomLight>().GetComponent<Light>();
            var sign = room.GetComponentInChildren<RoomSign>();
            var patch = sign.GetComponentsInChildren<Image>().First(i => i.gameObject != sign.gameObject);
            var green = mood.CurrentColor;
            Check(mood.CurrentMood == Mood.Happy, $"{room.RoomName}: mood is Happy (got {mood.CurrentMood})");
            Check(green.g > green.r && green.g > green.b, $"{room.RoomName}: Happy color is green ({green})");
            Check(light.color == green, $"{room.RoomName}: room light has the mood color ({light.color})");
            Check(patch.color == green, $"{room.RoomName}: door sign shows the mood color ({patch.color})");
        }

        /// <summary>
        /// Aims the controller at a point, holds the trigger briefly and releases it. Stops early once done() is true.
        /// </summary>
        private static IEnumerable Click(Vector3 target, Func<bool> done, bool quiet = false)
        {
            _aimTarget = target;
            yield return 0.25f;
            _triggerDown = true;
            yield return 0.2f;
            _triggerDown = false;
            var waitUntil = EditorApplication.timeSinceStartup + ClickTimeout;
            while (!done() && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            if (!done() && !quiet)
                Log($"click at {target} had no effect; controller at {ControllerWorld()}. {DescribeInteractors()}");
        }

        private static string DescribeInteractors()
        {
            var text = new System.Text.StringBuilder();
            foreach (var interactor in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                text.Append($"[{interactor.transform.parent?.name}/{interactor.name} active {interactor.isActiveAndEnabled}, " +
                            $"pos {interactor.transform.position}, fwd {interactor.transform.forward}, hovered {interactor.interactablesHovered.Count}, " +
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
        }

        /// <summary>
        /// Queues the head and controller poses (in tracking space, relative to the Camera Offset) as input events on
        /// every editor tick. The Input System routes these XR device events to the game, also without focus.
        /// </summary>
        private static void UpdateDevices()
        {
            if (_hmd == null || !EditorApplication.isPlaying)
                return;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
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
