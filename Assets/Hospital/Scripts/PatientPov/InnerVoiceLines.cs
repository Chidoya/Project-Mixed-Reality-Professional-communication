using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// What the patient thinks when the player steps into his head, one line per emotion (voice: patientA, Piper
    /// en_US-norman, slower). Edit the text and clips in Assets/Hospital/Data/PatientPov/InnerVoiceLines.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Patient POV/Inner Voice Lines", fileName = "InnerVoiceLines")]
    public class InnerVoiceLines : ScriptableObject
    {
        [Serializable]
        public class Line
        {
            public Emotion Emotion;
            public AudioClip Clip;
            [TextArea] public string Text;
        }

        [Range(0f, 1f)] public float Volume = 0.7f;
        [Tooltip("Seconds after the fade before the thought starts.")]
        [Min(0f)] public float Delay = 0.4f;
        [Tooltip("Seconds the subtitle stays after the clip, then fades.")]
        [Min(0f)] public float SubtitleHold = 0.8f;

        public List<Line> Lines = new List<Line>
        {
            new Line { Emotion = Emotion.Sadness, Text = "She turns eight today. And I'm stuck in this bed." },
            new Line { Emotion = Emotion.Anger, Text = "Nobody listens. Nobody even asks." },
            new Line { Emotion = Emotion.Fear, Text = "What if this leg never gets better?" },
            new Line { Emotion = Emotion.Joy, Text = "Maybe I can still see her today." },
        };

        public Line Get(Emotion emotion)
        {
            foreach (var line in Lines)
            {
                if (line != null && line.Emotion == emotion)
                    return line;
            }
            return null;
        }
    }
}
