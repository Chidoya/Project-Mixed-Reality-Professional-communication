using System.Reflection;
using ProCommVR.Emotions;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// Shared rules so the face, the nurse, the props and the sound agree with the room (PatientWorldController):
    /// an emotion starts to show at the holder's dominant threshold (0.15) and is at full strength at the world's
    /// Full Strength At (0.6).
    /// </summary>
    public static class PatientPovMath
    {
        public const float DefaultFullStrengthAt = 0.6f;

        private static FieldInfo _fullStrengthField;
        private static bool _looked;

        /// <summary>
        /// The world's Full Strength At. Read once per caller (Awake), not per frame. Jalil's controller keeps it private,
        /// so it is read by name; when that fails the default (0.6) is used.
        /// </summary>
        public static float ReadFullStrengthAt(PatientWorldController world)
        {
            if (world == null)
                return DefaultFullStrengthAt;
            if (!_looked)
            {
                _looked = true;
                _fullStrengthField = typeof(PatientWorldController).GetField("_fullStrengthAt", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            return _fullStrengthField != null && _fullStrengthField.GetValue(world) is float value ? value : DefaultFullStrengthAt;
        }

        /// <summary>0 at the dominant threshold, 1 at fullStrengthAt, smoothstepped (like the world's eased weight).</summary>
        public static float Strength(float value, float fullStrengthAt)
        {
            var threshold = EmotionState.DefaultDominantThreshold;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold, Mathf.Max(fullStrengthAt, threshold + 0.01f), value));
        }

        /// <summary>The strength of the holder's dominant emotion (0 when there is none).</summary>
        public static float DominantStrength(EmotionHolder holder, float fullStrengthAt, out Emotion? dominant)
        {
            dominant = holder != null ? holder.Dominant : null;
            return dominant.HasValue ? Strength(holder.Current.Get(dominant.Value), fullStrengthAt) : 0f;
        }

        /// <summary>
        /// Moves a weight per emotion toward the dominant emotion's strength, the old emotion first back to zero (twice as
        /// fast), like the world. Returns the emotion shown now (or null) and writes its weight.
        /// </summary>
        public static Emotion? StepExclusive(ref Emotion? shown, ref float weight, Emotion? wanted, float wantedWeight, float step)
        {
            if (weight > 0f && shown != wanted)
                weight = Mathf.MoveTowards(weight, 0f, step * 2f);
            else
            {
                if (weight <= 0f)
                    shown = wanted;
                weight = Mathf.MoveTowards(weight, shown == wanted ? wantedWeight : 0f, step);
            }
            return weight > 0f ? shown : null;
        }
    }
}
