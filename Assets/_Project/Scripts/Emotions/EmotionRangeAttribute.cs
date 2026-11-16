using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// Draws an <see cref="EmotionState"/> field as four sliders between Min and Max: 0..1 for a state, -1..1 for a
    /// delta. Read Only shows the values without letting you change them (e.g. the current, blending state).
    /// </summary>
    public class EmotionRangeAttribute : PropertyAttribute
    {
        public float Min { get; }
        public float Max { get; }
        public bool ReadOnly { get; }

        public EmotionRangeAttribute(float min, float max, bool readOnly = false)
        {
            Min = min;
            Max = max;
            ReadOnly = readOnly;
        }
    }
}
