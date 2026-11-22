using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Player;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The POV switch in Scenario01_PatientRoom, with the Y button of a simulated left controller (through the real
    /// SwitchPov input action). Near the patient (options shown): Y moves the camera to the patient's eyes, the nurse
    /// avatar stays where the player stood, the patient's body, the bubbles, the lasers and locomotion are gone and the
    /// hint is shown; a quick second press is ignored; Y again (after the cooldown) puts the rig back on exactly the
    /// same pose with everything restored and the same 4 options in the same order. Three times; the fourth switch is
    /// refused; a new scenario resets the count. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPovSwitch
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private static XRSimulatedController _leftController;
        private static bool _yDown;
        private static Vector3? _lastSpacePosition;
        private static Quaternion _lastSpaceRotation;

        [MenuItem("ProCommVR/Tests/POV Switch (Scenario 01)")]
        public static void RunPovSwitch()
        {
            Launch("pov", Scenario01ScenePath);
        }

        private static IEnumerator PovSwitch()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var pov = Object.FindAnyObjectByType<PovController>();
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var avatar = Object.FindAnyObjectByType<PlayerAvatar>();
            var anchor = Object.FindAnyObjectByType<PovAnchor>();
            Check(pov != null && presenter != null && origin != null && avatar != null && anchor != null,
                "scene has a PovController, presenter, XR Origin, player avatar and POV anchor");
            if (Errors.Count > 0)
                yield break;
            var settings = pov.Settings;
            Check(settings != null && settings.Unlimited && Mathf.Approximately(settings.MinSecondsBetweenSwitches, 2f),
                "settings: no limit on the switches per conversation, 2 s between switches");
            var bodyRenderers = anchor.Body.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            Check(bodyRenderers.Length > 0, $"the patient has {bodyRenderers.Length} visible renderer(s)");

            var chosen = 0;
            void OnChosen(Scenario s, DialogueOption o) => chosen++;
            var changes = new List<PovMode>();
            void OnPovChanged(PovMode mode) => changes.Add(mode);
            presenter.OptionChosen += OnChosen;
            pov.PovChanged += OnPovChanged;

            AddSimulatedDevices();
            AddLeftController();
            EditorApplication.update += KeepHeadLocal;
            EditorApplication.update += UpdateDevices;
            EditorApplication.update += UpdateLeftController;
            try
            {
                // Step within reach of the patient: the 4 options appear.
                var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
                var toPlayer = Flat(origin.transform.position - patient.position).normalized;
                Teleport(origin, patient.position + toPlayer * 1.0f, patient.position);
                _lastSpacePosition = null; // the test moved the rig itself, the head was placed with it
                var waitUntil = EditorApplication.timeSinceStartup + 2f;
                while (OptionBubbles().Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                var optionsBefore = OptionTexts();
                Check(optionsBefore.Count == 4, $"4 options within reach ({optionsBefore.Count})");
                Screenshot("pov-nurse-view");

                var rigPosition = origin.transform.position;
                var rigRotation = origin.transform.rotation;
                var nursePosition = origin.Camera.transform.position;
                var controlsOn = PlayerControls(origin).Where(b => b.enabled).ToArray();
                Check(controlsOn.OfType<XRBaseInteractor>().Any() && controlsOn.OfType<LocomotionProvider>().Any(),
                    $"lasers and locomotion are on in the nurse view ({controlsOn.Length} components)");

                for (var round = 1; round <= 3; round++)
                {
                    foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                        yield return step;
                    Check(pov.Mode == PovMode.Patient && pov.SwitchesThisConversation == round,
                        $"round {round}: Y switches to the patient view (switch {pov.SwitchesThisConversation})");
                    if (pov.Mode != PovMode.Patient)
                        yield break;
                    yield return 0.2f;

                    anchor.GetEyes(out var eyes, out var forward);
                    var camera = origin.Camera.transform;
                    Check(Vector3.Distance(camera.position, eyes) < 0.05f, $"round {round}: the camera is at the patient's eyes ({Vector3.Distance(camera.position, eyes):F3} m off)");
                    Check(Vector3.Angle(Flat(camera.forward), forward) < 5f, $"round {round}: looking where the patient looks ({Vector3.Angle(Flat(camera.forward), forward):F1} deg)");
                    Check(avatar.IsVisible && !avatar.FollowHead && Flat(avatar.transform.position - nursePosition).magnitude < 0.2f,
                        $"round {round}: the nurse avatar stands where the player stood");
                    Check(bodyRenderers.All(r => !r.enabled), $"round {round}: the patient's body is hidden");
                    Check(!presenter.OptionsVisible && ActiveBubbles(presenter).Length == 0, $"round {round}: no dialogue bubbles");
                    Check(controlsOn.All(b => !b.enabled) && !origin.GetComponent<CharacterController>().enabled,
                        $"round {round}: lasers, locomotion and the character controller are off");
                    var hint = HintText(pov);
                    Check(hint == PovSetup.HintText, $"round {round}: the hint says \"{hint}\"");
                    if (round == 1)
                    {
                        Screenshot("pov-patient-view");
                        Check(camera.position.y > 1.1f && camera.position.y < 1.6f, $"the eyes of the seated patient are at {camera.position.y:F2} m");

                        // Turn the head (as the player would) to see the nurse through the patient's eyes.
                        var lookAhead = _headRotation;
                        _headRotation = Quaternion.LookRotation(avatar.transform.position + Vector3.up * 1.1f - camera.position);
                        yield return 0.3f;
                        Screenshot("pov-patient-sees-nurse");
                        _headRotation = lookAhead;
                        yield return 0.2f;

                        // A quick second press is ignored.
                        foreach (var step in PressY(() => pov.IsSwitching))
                            yield return step;
                        yield return 0.3f;
                        Check(pov.Mode == PovMode.Patient && !pov.IsSwitching, "a quick second press is ignored");
                    }

                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                    foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                        yield return step;
                    Check(pov.Mode == PovMode.Nurse, $"round {round}: Y switches back to the nurse view");
                    yield return 0.3f;

                    var positionOff = Vector3.Distance(origin.transform.position, rigPosition);
                    var rotationOff = Quaternion.Angle(origin.transform.rotation, rigRotation);
                    Check(positionOff < 0.0001f && rotationOff < 0.01f, $"round {round}: the rig is back on exactly the same pose ({positionOff:F5} m, {rotationOff:F3} deg)");
                    Check(!avatar.IsVisible && avatar.FollowHead, $"round {round}: the avatar is hidden and follows the head again");
                    Check(bodyRenderers.All(r => r.enabled), $"round {round}: the patient's body is back");
                    Check(controlsOn.All(b => b.enabled) && origin.GetComponent<CharacterController>().enabled,
                        $"round {round}: lasers, locomotion and the character controller are on again");
                    Check(HintText(pov) == "(no hint)", $"round {round}: the hint is hidden");
                    var optionsAfter = OptionTexts();
                    Check(optionsAfter.SequenceEqual(optionsBefore), $"round {round}: the same 4 options in the same order");
                    Check(chosen == 0, $"round {round}: no option chosen, the conversation is unchanged");

                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                }

                // No limit: a fourth switch is allowed (Jalil's choice, 2026-11-22).
                Check(pov.SwitchesThisConversation == 3 && pov.WhyNot() == null, $"a fourth switch is allowed ({pov.WhyNot() ?? "allowed"})");

                // A new conversation resets the count.
                presenter.Play(presenter.CurrentScenario);
                Check(pov.SwitchesThisConversation == 0 && pov.WhyNot() == null, "a new conversation resets the count");
                Check(changes.Count == 6 && changes.Where((m, i) => m == (i % 2 == 0 ? PovMode.Patient : PovMode.Nurse)).Count() == 6,
                    $"PovChanged was raised 6 times, alternating ({string.Join(", ", changes)})");
            }
            finally
            {
                presenter.OptionChosen -= OnChosen;
                pov.PovChanged -= OnPovChanged;
                EditorApplication.update -= KeepHeadLocal;
                EditorApplication.update -= UpdateDevices;
                EditorApplication.update -= UpdateLeftController;
                if (_leftController != null && _leftController.added)
                    InputSystem.RemoveDevice(_leftController);
                _leftController = null;
                _lastSpacePosition = null;
                RemoveSimulatedDevices();
            }
        }

        /// <summary>
        /// The rig's interactors and locomotion providers.
        /// </summary>
        private static Behaviour[] PlayerControls(XROrigin origin)
        {
            return origin.GetComponentsInChildren<XRBaseInteractor>(true).Cast<Behaviour>()
                .Concat(origin.GetComponentsInChildren<LocomotionProvider>(true))
                .ToArray();
        }

        private static DialogueBubble[] ActiveBubbles(ScenarioPresenter presenter)
        {
            return presenter.GetComponentsInChildren<DialogueBubble>(true).Where(b => b.gameObject.activeInHierarchy).ToArray();
        }

        /// <summary>
        /// The texts of the clickable option bubbles, in their (shuffled) order.
        /// </summary>
        private static List<string> OptionTexts()
        {
            return OptionBubbles()
                .Where(b => b.gameObject.activeInHierarchy)
                .OrderBy(b => b.transform.GetSiblingIndex())
                .Select(b => b.GetComponentInChildren<TMP_Text>().text)
                .ToList();
        }

        /// <summary>
        /// The return hint, in the emotion HUD when it is shown.
        /// </summary>
        private static string HintText(PovController pov)
        {
            var hud = Object.FindAnyObjectByType<ProCommVR.Emotions.EmotionHud>();
            return hud != null && hud.IsShown ? hud.HintText : "(no hint)";
        }

        private static IEnumerable WaitUntilAllowed(PovController pov)
        {
            var waitUntil = EditorApplication.timeSinceStartup + 8f;
            while (pov.WhyNot() != null && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
        }

        /// <summary>
        /// Holds Y on the left controller for at least 0.2 s and 3 frames, stops early once done() is true.
        /// </summary>
        private static IEnumerable PressY(Func<bool> done)
        {
            _yDown = true;
            var pressFrame = Time.frameCount;
            yield return 0.2f;
            while (Time.frameCount < pressFrame + 3)
                yield return null;
            _yDown = false;
            var waitUntil = EditorApplication.timeSinceStartup + 3f;
            while (!done() && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
        }

        private static void AddLeftController()
        {
            _leftController = InputSystem.AddDevice(new InputDeviceDescription
            {
                product = nameof(XRSimulatedController),
                capabilities = new XRDeviceDescriptor
                {
                    deviceName = $"{nameof(XRSimulatedController)} - {UnityEngine.InputSystem.CommonUsages.LeftHand}",
                    characteristics = XRInputTrackingAggregator.Characteristics.leftController,
                }.ToJson(),
            }) as XRSimulatedController;
            if (_leftController == null)
                throw new InvalidOperationException("Could not create the simulated left controller.");
            InputSystem.SetDeviceUsage(_leftController, UnityEngine.InputSystem.CommonUsages.LeftHand);
            Log("simulated left controller added.");
        }

        private static void UpdateLeftController()
        {
            if (_leftController == null || !EditorApplication.isPlaying)
                return;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
            var space = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
            var hand = _headWorld + _headRotation * new Vector3(-0.15f, -0.3f, 0.25f);
            var state = new XRSimulatedControllerState
            {
                devicePosition = space.InverseTransformPoint(hand),
                deviceRotation = Quaternion.Inverse(space.rotation) * _headRotation,
                isTracked = true,
                trackingState = 3,
                batteryLevel = 1f,
            };
            state = state.WithButton(ControllerButton.SecondaryButton, _yDown);
            InputSystem.QueueStateEvent(_leftController, state);
        }

        /// <summary>
        /// Like a real headset: when the game moves or turns the XR Origin, the head keeps its pose relative to the
        /// tracking space (UpdateDevices alone only follows a move, not a turn). Runs before UpdateDevices.
        /// </summary>
        private static void KeepHeadLocal()
        {
            if (!EditorApplication.isPlaying)
                return;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
            var space = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
            if (_lastSpacePosition.HasValue &&
                (space.position != _lastSpacePosition.Value || space.rotation != _lastSpaceRotation))
            {
                var localPosition = Quaternion.Inverse(_lastSpaceRotation) * (_headWorld - _lastSpacePosition.Value);
                var localRotation = Quaternion.Inverse(_lastSpaceRotation) * _headRotation;
                _headWorld = space.position + space.rotation * localPosition;
                _headRotation = space.rotation * localRotation;
                _lastOriginPosition = origin.transform.position; // already followed, UpdateDevices must not add it again
            }
            _lastSpacePosition = space.position;
            _lastSpaceRotation = space.rotation;
        }
    }
}
