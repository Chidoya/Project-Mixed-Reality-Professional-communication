using System.Collections;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using ProCommVR.Testing;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The PatientViewTest scene (Scenario 1's room with the test panel): the panel shows the status, the Joy button
    /// (laser + trigger) makes Joy dominant, Y shows it in the patient view (HUD and room), the test controls switch to
    /// Anger while in the patient view, Y goes back, and Restart resets the conversation (Initial Emotion, switch count).
    /// The shift and Scenario01_PatientRoom do not have the panel. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPatientViewTest
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string PatientViewTestScenePath = "Assets/_Project/Scenes/PatientViewTest.unity";

        [MenuItem("ProCommVR/Tests/Patient View Test Scene")]
        public static void RunPatientViewTest()
        {
            Launch("pov-test", PatientViewTestScenePath);
        }

        private static IEnumerator PatientViewTest()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var controls = Object.FindAnyObjectByType<PatientViewTestControls>();
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var hud = Object.FindAnyObjectByType<EmotionHud>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(controls != null && presenter != null && holder != null && pov != null && world != null && hud != null && origin != null,
                "the test scene has the test panel and the patient view (presenter, EmotionHolder, PovController, world, HUD)");
            if (Errors.Count > 0)
                yield break;
            var scenario = presenter.CurrentScenario;
            Check(scenario != null && scenario.Id == Scenario01Id, $"Scenario 1 is playing ({(scenario != null ? scenario.Id : "none")})");
            if (scenario == null)
                yield break;
            var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
            var status = controls.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Status");

            AddPovDevices();
            try
            {
                var settle = EditorApplication.timeSinceStartup + 30f;
                while (Time.timeSinceLevelLoad < 3f && EditorApplication.timeSinceStartup < settle)
                    yield return null;
                Check(status.text.Contains("Patient: Sadness") && status.text.Contains("View: Nurse") && status.text.Contains("Y: ready"),
                    $"the status shows the patient, the view and that Y works (the start is within reach) ({status.text.Replace('\n', '|')})");

                // The Joy button, with laser + trigger, from the start position.
                var panel = controls.transform;
                Teleport(origin, origin.transform.position, panel.position - Vector3.up * 1.2f);
                _lastSpacePosition = null;
                yield return 0.5f;
                Screenshot("pov-test-panel");
                foreach (var step in Click(PanelButton(panel, "Joy"), () => holder.Target.Dominant == Emotion.Joy))
                    yield return step;
                Check(holder.Target.Dominant == Emotion.Joy, $"the Joy button makes Joy dominant ({holder.Target})");
                yield return holder.BlendSeconds + 0.3f;
                Check(holder.Dominant == Emotion.Joy, $"the patient blends to Joy ({holder.Current})");

                // Near the patient: Y works, the patient view shows Joy.
                Teleport(origin, patient.position + Flat(origin.Camera.transform.position - patient.position).normalized * 1.0f, patient.position);
                _lastSpacePosition = null;
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                yield return 0.3f;
                Check(status.text.Contains("Y: ready"), $"the status says Y is ready ({status.text.Replace('\n', '|')})");
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Patient, "Y switches to the patient view");
                if (pov.Mode != PovMode.Patient)
                    yield break;
                foreach (var step in WaitForWorld(world, Emotion.Joy))
                    yield return step;
                Check(world.ShownEmotion == Emotion.Joy && hud.IsShown && hud.Dominant == Emotion.Joy, "the patient view shows Joy (room and HUD)");
                Screenshot("pov-test-joy");

                // In the patient view the lasers are off: the test controls (A, keyboard) still change the emotion.
                controls.ShowAnger();
                foreach (var step in WaitForWorld(world, Emotion.Anger))
                    yield return step;
                Check(world.ShownEmotion == Emotion.Anger && hud.Dominant == Emotion.Anger, $"the room follows to Anger ({world.ShownEmotion})");
                Screenshot("pov-test-anger");
                controls.ShowNeutral();
                yield return holder.BlendSeconds + 0.3f;
                Check(holder.Current == EmotionState.Zero && holder.Dominant == null, $"Neutral sets every emotion to zero ({holder.Current})");

                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Nurse, "Y switches back to the nurse view");

                // Restart: the conversation starts again, at the Initial Emotion, with a fresh switch count.
                Check(pov.SwitchesThisConversation == 1, $"one switch used ({pov.SwitchesThisConversation})");
                Teleport(origin, origin.transform.position, panel.position - Vector3.up * 1.2f);
                _lastSpacePosition = null;
                yield return 0.5f;
                var restarted = false;
                void OnStarted(Scenario s) => restarted = true;
                presenter.ScenarioStarted += OnStarted;
                try
                {
                    foreach (var step in Click(PanelButton(panel, "Restart"), () => restarted))
                        yield return step;
                }
                finally
                {
                    presenter.ScenarioStarted -= OnStarted;
                }
                Check(restarted, "the Restart button restarts the conversation");
                Check(holder.Current == scenario.InitialEmotion && pov.SwitchesThisConversation == 0,
                    $"after Restart: Initial Emotion ({holder.Current}) and no switches used ({pov.SwitchesThisConversation})");
            }
            finally
            {
                RemovePovDevices();
            }
        }

        private static Vector3 PanelButton(Transform panel, string name)
        {
            var button = panel.GetComponentsInChildren<Button>().First(b => b.name == $"Button {name}");
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }
    }
}
