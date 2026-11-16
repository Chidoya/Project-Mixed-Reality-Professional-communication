using System.Collections;
using System.Linq;
using ProCommVR.Atmosphere;
using ProCommVR.Emotions;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The emotion model in Scenario01_PatientRoom. The test plays a copy of Scenario 1 with known test values (the
    /// real asset is never changed): Initial Emotion is set at once on ScenarioStarted, the option chosen with laser +
    /// trigger adds its Emotion Delta to the target (clamped to 0..1), Current follows it smoothly in about 1.5 s and
    /// raises EmotionChanged on the way. The Mood stays untouched. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunEmotionModel
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string Scenario01ScenePath = "Assets/_Project/Scenes/Scenario01_PatientRoom.unity";

        // Test values, chosen so that some values clamp at 0 and at 1.
        private static readonly EmotionState TestInitial = new EmotionState(0.05f, 0.25f, 0.2f, 0.7f);

        private static readonly EmotionState[] TestDeltas =
        {
            new EmotionState(0.2f, -0.1f, 0f, -0.25f),
            new EmotionState(-0.1f, 0.3f, 0f, 0.1f),
            new EmotionState(0.15f, 0.1f, 0.1f, 0f),
            new EmotionState(0f, 0.15f, -0.5f, 0.4f),
        };

        [MenuItem("ProCommVR/Tests/Emotion Model (Scenario 01)")]
        public static void RunEmotionModel()
        {
            Launch("emotion", Scenario01ScenePath);
        }

        private static IEnumerator EmotionModel()
        {
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var mood = Object.FindAnyObjectByType<MoodController>();
            Check(presenter != null && holder != null && origin != null, "scene has a ScenarioPresenter, an EmotionHolder and an XR Origin");
            if (Errors.Count > 0)
                yield break;
            Check(holder.Presenter == presenter, "the EmotionHolder follows the scene's presenter");
            Check(holder.Current == presenter.CurrentScenario.InitialEmotion,
                $"the real Scenario 1 starts at its Initial Emotion ({holder.Current})");
            var moodBefore = mood != null ? mood.CurrentMood : null;

            // A copy of Scenario 1 with the test values.
            var scenario = Object.Instantiate(presenter.CurrentScenario);
            var serialized = new SerializedObject(scenario);
            WriteEmotion(serialized.FindProperty("_initialEmotion"), TestInitial);
            var options = serialized.FindProperty("_options");
            for (var i = 0; i < options.arraySize; i++)
                WriteEmotion(options.GetArrayElementAtIndex(i).FindPropertyRelative("_emotionDelta"), TestDeltas[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var changes = 0;
            EmotionState lastPrevious = default, lastCurrent = default;
            void OnChanged(EmotionState previous, EmotionState current)
            {
                changes++;
                lastPrevious = previous;
                lastCurrent = current;
            }

            DialogueOption chosen = null;
            void OnChosen(Scenario s, DialogueOption option) => chosen = option;

            holder.EmotionChanged += OnChanged;
            presenter.OptionChosen += OnChosen;
            AddSimulatedDevices();
            EditorApplication.update += UpdateDevices;
            try
            {
                presenter.Play(scenario);
                Check(holder.Current == TestInitial && holder.Target == TestInitial,
                    $"ScenarioStarted sets Initial Emotion at once ({holder.Current})");
                Check(changes == 1 && lastCurrent == TestInitial, $"ScenarioStarted raises EmotionChanged once ({changes})");
                Check(holder.Dominant == Emotion.Sadness, $"dominant emotion is Sadness ({holder.Dominant})");

                // Step within reach of the patient and choose an option with laser + trigger.
                // The presenter's NPC is the patient's body (the holder sits on its root, like the NpcReactor).
                var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
                Check(patient != null, "the presenter knows the patient's body");
                if (patient == null)
                    yield break;
                var toPlayer = Flat(origin.transform.position - patient.position).normalized;
                Teleport(origin, patient.position + toPlayer * 1.0f, patient.position);
                var waitUntil = EditorApplication.timeSinceStartup + 2f;
                while (OptionBubbles().Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                var bubbles = OptionBubbles();
                Check(bubbles.Length == 4, $"4 options within reach (got {bubbles.Length})");
                if (bubbles.Length == 0)
                {
                    Log($"camera {Camera.main.transform.position}, patient {patient.position}, hold choices {presenter.HoldChoices}");
                    yield break;
                }

                changes = 0;
                foreach (var step in Click(Center(bubbles[0]), () => chosen != null))
                    yield return step;
                Check(chosen != null, "laser + trigger picks an option");
                if (chosen == null)
                    yield break;

                var index = scenario.Options.ToList().IndexOf(chosen);
                var expected = TestInitial.Add(TestDeltas[index]);
                Log($"chose option {index}: delta {TestDeltas[index]}, expected target {expected}");
                Check(holder.Target.Approximately(expected), $"target is Initial + delta, clamped to 0..1 ({holder.Target})");
                Check(!holder.Current.Approximately(expected), $"current does not snap to the target ({holder.Current})");

                yield return holder.BlendSeconds * 0.4f;
                var halfway = holder.Current;
                Check(!halfway.Approximately(TestInitial) && !halfway.Approximately(expected),
                    $"current blends smoothly (after {holder.BlendSeconds * 0.4f:F1} s: {halfway})");
                Check(changes > 1, $"EmotionChanged is raised while blending ({changes} times)");

                yield return holder.BlendSeconds * 0.8f;
                Check(holder.Current.Approximately(expected), $"current reaches the target in about {holder.BlendSeconds} s ({holder.Current})");
                Check(lastCurrent == holder.Current && lastPrevious != lastCurrent, "the last EmotionChanged has the previous and the new value");

                // By hand (as the inspector sliders do): Apply clamps at 1 and at 0, SetImmediate does not blend.
                holder.Apply(new EmotionState(1f, 1f, 1f, 1f));
                Check(holder.Target == new EmotionState(1f, 1f, 1f, 1f), $"Apply clamps at 1 ({holder.Target})");
                holder.Apply(new EmotionState(-2f, -2f, -2f, -2f));
                Check(holder.Target == EmotionState.Zero, $"Apply clamps at 0 ({holder.Target})");
                yield return holder.BlendSeconds + 0.3f;
                Check(holder.Current == EmotionState.Zero && holder.Dominant == null, $"all zero: no dominant emotion ({holder.Dominant})");
                holder.SetImmediate(new EmotionState(0f, 0.9f, 0f, 0f));
                Check(holder.Current.Anger == 0.9f && holder.Dominant == Emotion.Anger, "SetImmediate sets the state at once");

                Check(mood == null || mood.CurrentMood == moodBefore, $"the Mood is unchanged ({(mood != null ? mood.CurrentMood : null)})");
            }
            finally
            {
                holder.EmotionChanged -= OnChanged;
                presenter.OptionChosen -= OnChosen;
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
                Object.Destroy(scenario);
            }
        }

        private static void WriteEmotion(SerializedProperty property, EmotionState state)
        {
            property.FindPropertyRelative("_joy").floatValue = state.Joy;
            property.FindPropertyRelative("_anger").floatValue = state.Anger;
            property.FindPropertyRelative("_fear").floatValue = state.Fear;
            property.FindPropertyRelative("_sadness").floatValue = state.Sadness;
        }

        private static DialogueBubble[] OptionBubbles()
        {
            // The option bubbles live under the presenter's options anchor, which is not always a child of the presenter.
            return Object.FindObjectsByType<DialogueBubble>(FindObjectsSortMode.None)
                .Where(b => b.GetComponent<Button>().enabled)
                .ToArray();
        }
    }
}
