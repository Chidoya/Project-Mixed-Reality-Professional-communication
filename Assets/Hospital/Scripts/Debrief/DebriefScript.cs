using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>The steps of the doctor-led debrief; their order is data (<see cref="DebriefScript.Steps"/>).</summary>
    public enum DebriefStepKind { Greeting, ReflectionQuestion, Reveal, Timeline, Feedback, Memory, Consequence, Board, Choice }

    /// <summary>One spoken line: a stable key, the text (subtitle) and the voice clip.</summary>
    [Serializable]
    public class DebriefLine
    {
        public string Key;
        [TextArea] public string Text;
        public AudioClip Clip;

        public DebriefLine() { }

        public DebriefLine(string key, string text, AudioClip clip = null)
        {
            Key = key;
            Text = text;
            Clip = clip;
        }
    }

    /// <summary>The short texts on the sticky notes for one rubric item (max 8 words).</summary>
    [Serializable]
    public class DebriefNote
    {
        public int ItemId;
        public string Improve;
        public string WellDone;
    }

    /// <summary>
    /// Everything the head doctor says and shows in the debrief of one scenario: lines with their voice clips, the order of
    /// the steps, the sticky notes, colors, thresholds and the closing card. Data only (imported from
    /// Assets/Hospital/Data/Debrief/DebriefLines_Scenario1.json by ProCommVR > Liam > Debrief > Import Doctor Lines), so the
    /// texts can change without code. The feedback per rubric item comes from the AI server (feedback_en) with its clip in
    /// the <see cref="DebriefVoiceLibrary"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Debrief Script", fileName = "DebriefScript")]
    public class DebriefScript : ScriptableObject
    {
        [Header("Scenario")]
        public string ScenarioId = "disappointed_patient";
        [Tooltip("Prefix of the keys in the voice library (s1_item2_score0).")]
        public string ScenarioPrefix = "s1";
        public string DoctorName = "Dr. Peeters";
        [TextArea] public string PagerText = "Dr. Peeters\nBriefing room";

        [Header("Steps (drag to reorder)")]
        public List<DebriefStepKind> Steps = new()
        {
            DebriefStepKind.Greeting, DebriefStepKind.ReflectionQuestion, DebriefStepKind.Reveal, DebriefStepKind.Timeline,
            DebriefStepKind.Feedback, DebriefStepKind.Memory, DebriefStepKind.Consequence, DebriefStepKind.Board,
            DebriefStepKind.Choice,
        };
        [Tooltip("At most this many points in the feedback (critical error, lowest item, highest item).")]
        [Range(1, 3)] public int MaxPoints = 3;

        [Header("Lines (key, text, clip)")]
        public List<DebriefLine> Lines = new();
        public DebriefVoiceLibrary Voices;

        [Header("Sticky notes on the whiteboard")]
        public List<DebriefNote> Notes = new();
        public string CriticalNote = "Never promise a discharge";
        public Color ImproveColor = new(1f, 0.86f, 0.3f);
        public Color WellDoneColor = new(0.62f, 0.9f, 0.55f);
        public Color CriticalColor = new(1f, 0.55f, 0.5f);

        [Header("The better phrase in the memory: where the patient's face goes")]
        public FaceExpression BetterAimExpression = FaceExpression.Neutral;
        [Range(0f, 1f)] public float BetterAimIntensity = 0.5f;
        public EmotionState BetterAim = new(0.25f, 0f, 0f, 0.3f);

        [Header("Closing card (Saturday, 3 pm)")]
        [Tooltip("Good: no critical error and at least this part of the max score.")]
        [Range(0f, 1f)] public float GoodFraction = 0.7f;
        public string ConsequenceTitle = "Saturday, 3 pm";
        public string GoodCaption = "A video call from the party.";
        public string MixedCaption = "The birthday card is still closed.";
        public string CriticalCaption = "An empty chair at the party.";
        public Texture2D GoodImage;
        public Texture2D MixedImage;
        public Texture2D CriticalImage;
        public AudioClip GranddaughterClip;
        [TextArea] public string GranddaughterText = "Grandpa! Look, I'm six! We saved you a piece of cake!";
        [Min(2f)] public float ConsequenceSeconds = 10f;

        [Header("Sounds")]
        public AudioClip PagerClip;
        public AudioClip FootstepsClip;

        private Dictionary<string, DebriefLine> _byKey;

        /// <summary>The line with this key, or null.</summary>
        public DebriefLine Line(string key)
        {
            if (_byKey == null || _byKey.Count != Lines.Count)
            {
                _byKey = new Dictionary<string, DebriefLine>();
                foreach (var line in Lines)
                    if (line != null && !string.IsNullOrEmpty(line.Key))
                        _byKey[line.Key] = line;
            }
            return key != null && _byKey.TryGetValue(key, out var found) ? found : null;
        }

        /// <summary>The reveal line for the picked emotion and the real one (null = no strong feeling).</summary>
        public DebriefLine Reveal(Emotion picked, Emotion? real) => Line($"reveal_{picked}_{(real.HasValue ? real.Value.ToString() : "None")}");

        public string NoteFor(int itemId, bool wellDone)
        {
            foreach (var note in Notes)
                if (note.ItemId == itemId)
                    return wellDone ? note.WellDone : note.Improve;
            return null;
        }

        private void OnValidate() => _byKey = null;
    }
}
