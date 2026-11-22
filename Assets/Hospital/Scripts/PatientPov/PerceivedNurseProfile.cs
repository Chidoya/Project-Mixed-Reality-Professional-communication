using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// How the patient perceives the nurse, per dominant emotion (patient view only). Small on purpose: it must feel
    /// different, not become a caricature. Tune it in Assets/Hospital/Data/PatientPov/PerceivedNurseProfile.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Patient POV/Perceived Nurse Profile", fileName = "PerceivedNurseProfile")]
    public class PerceivedNurseProfile : ScriptableObject
    {
        [Serializable]
        public class Look
        {
            public Emotion Emotion;
            [Tooltip("The nurse's face as the patient sees it.")]
            public FaceExpression Face = FaceExpression.Neutral;
            [Range(0f, 1f)] public float FaceIntensity;
            [Tooltip("Uniform scale at full strength (1 = as she is).")]
            [Range(0.8f, 1.2f)] public float Scale = 1f;
            [Tooltip("Lean toward the patient at full strength, in degrees.")]
            [Range(0f, 10f)] public float LeanDegrees;
            [Tooltip("Multiplied with her material colors at full strength.")]
            public Color Tint = Color.white;
            [Tooltip("How grey her colors get at full strength.")]
            [Range(0f, 1f)] public float Desaturate;
        }

        [Tooltip("Seconds from normal to the full look (same as the room).")]
        [Min(0.05f)] public float BlendSeconds = 2f;

        public List<Look> Looks = DefaultLooks();

        public Look Get(Emotion emotion)
        {
            foreach (var look in Looks)
            {
                if (look != null && look.Emotion == emotion)
                    return look;
            }
            return null;
        }

        public static List<Look> DefaultLooks()
        {
            return new List<Look>
            {
                // Anger: stern, a bit bigger, leaning in.
                new Look { Emotion = Emotion.Anger, Face = FaceExpression.Angry, FaceIntensity = 0.45f, Scale = 1.08f, LeanDegrees = 4f, Tint = new Color(1f, 0.9f, 0.88f) },
                // Fear: a dark, vague figure, a bit bigger; only clear in the flashlight.
                new Look { Emotion = Emotion.Fear, Face = FaceExpression.Neutral, FaceIntensity = 0f, Scale = 1.05f, Tint = new Color(0.4f, 0.42f, 0.5f), Desaturate = 0.6f },
                // Sadness: grey-blue and smaller, as if far away.
                new Look { Emotion = Emotion.Sadness, Face = FaceExpression.Neutral, FaceIntensity = 0f, Scale = 0.95f, Tint = new Color(0.72f, 0.78f, 0.9f), Desaturate = 0.5f },
                // Joy: warm, with a smile.
                new Look { Emotion = Emotion.Joy, Face = FaceExpression.Happy, FaceIntensity = 0.6f, Scale = 1f, Tint = new Color(1.08f, 1f, 0.86f) },
            };
        }

        private void Reset() => Looks = DefaultLooks();
    }
}
