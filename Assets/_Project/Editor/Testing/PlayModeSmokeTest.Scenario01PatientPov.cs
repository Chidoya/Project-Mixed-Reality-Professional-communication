using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The patient view in Scenario 1, in both places where it can be played:
    /// - Scenario01_PatientRoom (RunScenario01PatientPov): the patient starts mainly sad; the player chooses the best
    ///   option, the patient's Sadness drops and Joy becomes dominant; in the patient view (Y) the HUD shows it and the
    ///   room shows Joy (plants, sun); back in the nurse view the room is exactly as before; the score is the option's
    ///   score. Then the conversation restarts and the dismissive answer makes Anger dominant: the fires burn.
    /// - ShiftHospital room 1A (RunScenario01PatientPovShift): the same with the best option, plus: Y does nothing from
    ///   the lobby while the conversation of 1A is already open (too far away), and nothing after "Finish".
    /// Also checks the data: which emotion each option of Scenario 1 leads to. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario01PatientPov
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario01PatientPovShift
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        // Which emotion each option of Scenario 1 leads to, by the option's score (visual feedback only).
        private static readonly Dictionary<int, Emotion> Scenario01EmotionByScore = new Dictionary<int, Emotion>
        {
            { 3, Emotion.Joy },      // best: acknowledges the disappointment, a walk and a video call
            { 0, Emotion.Anger },    // dismissive: "there will be other birthdays"
            { -2, Emotion.Sadness }, // false hope: a little Joy and Fear, but he stays sad
            { 1, Emotion.Sadness },  // passive: hands it off
        };

        private static readonly EmotionState Scenario01Initial = new EmotionState(0.05f, 0.25f, 0.2f, 0.7f);

        /// <summary>
        /// The lights the patient view tints, the ambient light, fog and post-processing: what the nurse view must get back.
        /// </summary>
        private class WorldSnapshot
        {
            private Color[] _colors;
            private float[] _intensities;
            private Color _ambient;
            private bool _fog;
            private Color _fogColor;
            private float _fogDensity;
            private bool _post;

            public static WorldSnapshot Take(PatientWorldController world, UniversalAdditionalCameraData camera) => new WorldSnapshot
            {
                _colors = world.RoomLights.Select(l => l.color).ToArray(),
                _intensities = world.RoomLights.Select(l => l.intensity).ToArray(),
                _ambient = RenderSettings.ambientLight,
                _fog = RenderSettings.fog,
                _fogColor = RenderSettings.fogColor,
                _fogDensity = RenderSettings.fogDensity,
                _post = camera.renderPostProcessing,
            };

            public bool Same(WorldSnapshot other) =>
                _colors.SequenceEqual(other._colors) && _intensities.Zip(other._intensities, Mathf.Approximately).All(b => b) &&
                _ambient == other._ambient && _fog == other._fog && _fogColor == other._fogColor &&
                Mathf.Approximately(_fogDensity, other._fogDensity) && _post == other._post;

            public override string ToString() =>
                $"{_colors.Length} lights ({string.Join(", ", _intensities.Select(i => i.ToString("F2")))}), ambient {_ambient}, fog {_fog} {_fogDensity:F3}, post {_post}";
        }

        [MenuItem("ProCommVR/Tests/Scenario 01 Patient POV")]
        public static void RunScenario01PatientPov()
        {
            Launch("s1-pov", Scenario01ScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 01 Patient POV (Shift Hospital, room 1A)")]
        public static void RunScenario01PatientPovShift()
        {
            Launch("s1-pov-shift", ShiftScenePath);
        }

        private static IEnumerator Scenario01PatientPov()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var sequence = Object.FindAnyObjectByType<ScenarioSequence>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(presenter != null && sequence != null && origin != null, "scene has a presenter, a scenario sequence and an XR Origin");
            if (Errors.Count > 0)
                yield break;
            var scenario = presenter.CurrentScenario;
            Check(scenario != null && scenario.Id == Scenario01Id, $"Scenario 1 is playing ({(scenario != null ? scenario.Id : "none")})");
            if (scenario == null)
                yield break;
            CheckScenario01Data(scenario);

            AddPovDevices();
            try
            {
                var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
                var start = origin.transform.position;
                foreach (var step in PlayPatientPov(presenter, origin, patient, Best(scenario), "s1-pov-best", () => ClickableOptions(presenter)))
                    yield return step;
                Check(sequence.Score.Total == Best(scenario).Score && sequence.Score.Results.Count == 1,
                    $"the score is the option's score, as before ({sequence.Score.Total})");

                // Once more, with the dismissive answer: Anger, the fires burn.
                presenter.Play(scenario);
                Teleport(origin, start, patient.position);
                _lastSpacePosition = null;
                yield return 0.5f;
                var dismissive = scenario.Options.First(o => o.Score == 0);
                foreach (var step in PlayPatientPov(presenter, origin, patient, dismissive, "s1-pov-dismissive", () => ClickableOptions(presenter)))
                    yield return step;
            }
            finally
            {
                RemovePovDevices();
            }
        }

        private static IEnumerator Scenario01PatientPovShift()
        {
            return ScenarioInShift(Scenario01Id, CareForPatientPov, (body, room) =>
            {
                var scenario = room.Task.Scenario;
                Check(room.ChosenOption == Best(scenario) && room.Score == scenario.BestScore,
                    $"{room.RoomName}: the score is the option's score, as before ({room.Score})");
                Check(body.Contains($"{room.RoomName} - {room.Task.PatientName}") && body.Contains($"{scenario.BestScore} / {scenario.BestScore}"),
                    $"report: {room.RoomName} scored {scenario.BestScore} / {scenario.BestScore}");
            });
        }

        /// <summary>
        /// Room 1A in the shift (the shift has started, the player is in the lobby): Y does nothing from the lobby, then
        /// the patient view with the best answer, then "Finish" and Y does nothing any more.
        /// </summary>
        private static IEnumerable CareForPatientPov(ShiftManager manager, XROrigin origin, PatientRoom room)
        {
            var scenario = room.Task.Scenario;
            CheckScenario01Data(scenario);
            var presenter = room.GetComponentInChildren<ScenarioPresenter>();
            var pov = Object.FindAnyObjectByType<PovController>();
            Check(Object.FindObjectsByType<PovController>(FindObjectsSortMode.None).Length == 1 && pov != null,
                "the shift has one POV controller (room 1A)");
            if (pov == null)
                yield break;
            var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
            Check(presenter.IsPlaying, $"{room.RoomName}: the conversation is open since the start of the shift");

            AddPovDevices();
            try
            {
                Check(pov.WhyNot() == "too far from the patient", $"from the lobby the patient view is refused ({pov.WhyNot()})");
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient))
                    yield return step;
                yield return 0.5f;
                Check(pov.Mode == PovMode.Nurse && pov.SwitchesThisConversation == 0, "Y in the lobby does nothing");

                // The lift ride as in the other shift tests: the head follows the lift's teleport through UpdateDevices
                // only (KeepHeadLocal would move it a second time).
                RemovePovDevices();
                foreach (var step in RideLiftTo(origin, room.transform.position.y))
                    yield return step;
                AddPovDevices();
                Teleport(origin, Vector3.Lerp(patient.position, room.transform.position, 0.75f), patient.position);
                _lastSpacePosition = null;
                yield return 0.5f;

                foreach (var step in PlayPatientPov(presenter, origin, patient, Best(scenario), "s1-pov-shift-1a", () => ClickableBubbles(room)))
                    yield return step;

                // Finish closes the conversation: no more patient view.
                var finish = ClickableBubbles(room);
                Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
                if (finish.Length == 0)
                    yield break;
                foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                    yield return step;
                Check(manager.IsCompleted(room) && !presenter.IsPlaying, $"{room.RoomName}: Finish closes the conversation");
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                Check(pov.WhyNot() == "no conversation", $"after Finish the patient view is refused ({pov.WhyNot()})");
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient))
                    yield return step;
                yield return 0.5f;
                Check(pov.Mode == PovMode.Nurse, "Y after Finish does nothing");
            }
            finally
            {
                RemovePovDevices();
            }
        }

        /// <summary>
        /// Choose the option within reach, then look through the patient's eyes: the holder, the HUD and the room show
        /// the emotion the option leads to; back in the nurse view the room is exactly as before.
        /// </summary>
        private static IEnumerable PlayPatientPov(ScenarioPresenter presenter, XROrigin origin, Transform patient, DialogueOption option,
            string shot, Func<DialogueBubble[]> clickable)
        {
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var hud = Object.FindAnyObjectByType<EmotionHud>();
            var cameraData = origin.Camera.GetComponent<UniversalAdditionalCameraData>();
            Check(holder != null && pov != null && world != null && hud != null && cameraData != null,
                "the patient view is set up (EmotionHolder, PovController, PatientWorldController, EmotionHud)");
            if (holder == null || pov == null || world == null || hud == null || cameraData == null)
                yield break;
            Check(holder.Presenter == presenter, "the EmotionHolder follows this conversation's presenter");
            var scenario = presenter.CurrentScenario;
            var expected = Scenario01EmotionByScore[option.Score];

            // The mood fades in during the first seconds of the scene: wait for it, so the snapshot holds still.
            var settle = EditorApplication.timeSinceStartup + 30f;
            while (Time.timeSinceLevelLoad < 3f && EditorApplication.timeSinceStartup < settle)
                yield return null;
            Check(holder.Current.Approximately(scenario.InitialEmotion) && holder.Dominant == Emotion.Sadness,
                $"the patient starts at the Initial Emotion, mainly sad ({holder.Current})");
            var before = WorldSnapshot.Take(world, cameraData);
            Check(world.IsNeutral && world.Anchors.All(a => !a.IsOn), "nurse view: no emotion effects");

            // Step within reach and choose.
            Teleport(origin, patient.position + Flat(origin.Camera.transform.position - patient.position).normalized * 1.0f, patient.position);
            _lastSpacePosition = null;
            var waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (clickable().Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var bubble = clickable().FirstOrDefault(b => BubbleText(b).Contains(option.Text.Substring(0, 40)));
            Check(bubble != null, $"the option \"{option.Text.Substring(0, 40)}...\" is within reach");
            if (bubble == null)
                yield break;
            DialogueOption chosen = null;
            void OnChosen(Scenario s, DialogueOption o) => chosen = o;
            presenter.OptionChosen += OnChosen;
            try
            {
                foreach (var step in Click(Center(bubble), () => chosen != null))
                    yield return step;
            }
            finally
            {
                presenter.OptionChosen -= OnChosen;
            }
            Check(chosen == option, $"laser + trigger chose the option with score {option.Score}");
            if (chosen != option)
                yield break;

            var target = scenario.InitialEmotion.Add(option.EmotionDelta);
            yield return holder.BlendSeconds + 0.3f;
            Check(holder.Current.Approximately(target), $"the patient's emotions are Initial + the option's delta ({holder.Current})");
            Check(holder.Dominant == expected, $"dominant is now {expected} ({holder.Dominant})");
            if (expected == Emotion.Joy)
                Check(holder.Current.Sadness < scenario.InitialEmotion.Sadness,
                    $"Sadness dropped from {scenario.InitialEmotion.Sadness:F2} to {holder.Current.Sadness:F2}");
            Check(world.IsNeutral && WorldSnapshot.Take(world, cameraData).Same(before), "nurse view: the room does not react to the answer");

            // Through the patient's eyes.
            foreach (var step in WaitUntilAllowed(pov))
                yield return step;
            foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                yield return step;
            Check(pov.Mode == PovMode.Patient, "Y switches to the patient view");
            if (pov.Mode != PovMode.Patient)
                yield break;
            var shown = EditorApplication.timeSinceStartup + world.BlendSeconds * 2f + 2f;
            while ((world.ShownEmotion != expected || world.Weight < 0.999f) && EditorApplication.timeSinceStartup < shown)
                yield return null;
            Check(hud.IsShown && hud.Dominant == expected, $"the HUD shows {expected} as dominant ({hud.Dominant})");
            foreach (Emotion emotion in Enum.GetValues(typeof(Emotion)))
            {
                var number = hud.GetRow(emotion).Number.text;
                var value = Mathf.RoundToInt(holder.Current.Get(emotion) * 100f).ToString();
                Check(number == value, $"the HUD shows {emotion} {number} (holder {value})");
            }
            Check(world.ShownEmotion == expected && world.Weight > 0.999f && world.Volume.weight > 0.999f,
                $"the room shows {expected} at full strength ({world.ShownEmotion}, weight {world.Weight:F2})");
            var profile = world.GetProfile(expected);
            var wrong = world.Anchors.Where(a => a.IsOn != (a.Emotion == expected && profile.Enables(a.Kind))).ToArray();
            Check(wrong.Length == 0 && world.Anchors.Any(a => a.IsOn),
                $"exactly the {expected} effects are on ({string.Join(", ", world.Anchors.Where(a => a.IsOn).Select(a => a.name))})");
            // Look straight ahead (the test aimed the head up at the patient before), at the nurse.
            var camera = origin.Camera.transform;
            _headRotation = Quaternion.LookRotation(Flat(camera.forward));
            yield return 0.4f;
            Screenshot($"{shot}-ahead");
            var ahead = _headRotation;
            var effect = world.Anchors.Where(a => a.IsOn && a.Kind != EmotionEffectKind.Flashlight)
                .OrderBy(a => Vector3.Angle(camera.forward, a.transform.position - camera.position)).Skip(1).FirstOrDefault();
            if (effect != null)
            {
                _headRotation = Quaternion.LookRotation(effect.transform.position - camera.position);
                yield return 0.4f;
                Screenshot($"{shot}-{effect.Kind.ToString().ToLowerInvariant()}");
                _headRotation = ahead;
                yield return 0.2f;
            }

            // Back to the nurse: the room is exactly as before.
            foreach (var step in WaitUntilAllowed(pov))
                yield return step;
            foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                yield return step;
            Check(pov.Mode == PovMode.Nurse, "Y switches back to the nurse view");
            yield return 0.3f;
            var after = WorldSnapshot.Take(world, cameraData);
            Check(after.Same(before), $"back in the nurse view the room is exactly as before ({after}; before {before})");
            Check(world.IsNeutral && world.Anchors.All(a => !a.IsOn) && world.Volume.weight == 0f && !hud.IsShown,
                "back in the nurse view: no effects, no volume, no HUD");
            Check(chosen == option && presenter.IsPlaying, "the conversation is still open (Continue / Finish)");
        }

        /// <summary>
        /// Scenario 1's data: the Initial Emotion and which emotion each option leads to (the scores are unchanged).
        /// </summary>
        private static void CheckScenario01Data(Scenario scenario)
        {
            Check(scenario.InitialEmotion.Approximately(Scenario01Initial) && scenario.InitialEmotion.Dominant == Emotion.Sadness,
                $"Scenario 1 starts mainly sad ({scenario.InitialEmotion})");
            Check(scenario.Options.Select(o => o.Score).OrderBy(s => s).SequenceEqual(new[] { -2, 0, 1, 3 }),
                $"the scores are unchanged ({string.Join(", ", scenario.Options.Select(o => o.Score))})");
            foreach (var option in scenario.Options)
            {
                var after = scenario.InitialEmotion.Add(option.EmotionDelta);
                Check(after.Dominant == Scenario01EmotionByScore[option.Score],
                    $"option with score {option.Score} leads to {Scenario01EmotionByScore[option.Score]} ({after}, dominant {after.Dominant})");
            }
        }

        private static DialogueOption Best(Scenario scenario) => scenario.Options.OrderByDescending(o => o.Score).First();

        private static DialogueBubble[] ClickableOptions(ScenarioPresenter presenter) => OptionBubbles();

        private static void AddPovDevices()
        {
            if (_hmd == null)
            {
                // The head starts where the camera is, at standing height (as in ScenarioInShift), before the first
                // device update: otherwise it would start at the world origin and squash the character controller.
                var origin = Object.FindAnyObjectByType<XROrigin>();
                _headWorld = Camera.main.transform.position;
                _headWorld.y = origin.transform.position.y + HeadHeight;
                _headRotation = Quaternion.LookRotation(Flat(origin.transform.forward));
                AddSimulatedDevices();
            }
            AddLeftController();
            EditorApplication.update -= UpdateDevices;
            EditorApplication.update += KeepHeadLocal;
            EditorApplication.update += UpdateDevices;
            EditorApplication.update += UpdateLeftController;
        }

        private static void RemovePovDevices()
        {
            EditorApplication.update -= KeepHeadLocal;
            EditorApplication.update -= UpdateLeftController;
            if (_leftController != null && _leftController.added)
                InputSystem.RemoveDevice(_leftController);
            _leftController = null;
            _lastSpacePosition = null;
            // The standalone test added the head and right controller itself; in the shift ScenarioInShift removes them.
            if (EditorApplication.isPlaying && Object.FindAnyObjectByType<ShiftManager>() == null)
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }
    }
}
