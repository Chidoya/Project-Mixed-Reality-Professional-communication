using System;
using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// How the patient thinks about the player (the nurse), from 0 (no trust at all) to 100 (full trust). Shown as the
    /// trust meter in the emotion HUD. Put it on the patient, next to the <see cref="EmotionHolder"/>.
    /// <para>
    /// The value is set by whatever drives the conversation: the AI conversation bridge copies the trust of Liam's AI
    /// server after every turn. It is only feedback for the player; it never changes a score.
    /// </para>
    /// </summary>
    public class PatientReputation : MonoBehaviour
    {
        public const float Min = 0f;
        public const float Max = 100f;

        [Tooltip("The value at the start of every conversation (before the patient's first line).")]
        [SerializeField, Range(Min, Max)] private float _initial = 50f;

        [SerializeField, Range(Min, Max)] private float _value = 50f;

        [Tooltip("Below this the meter is red (the patient does not trust the nurse).")]
        [SerializeField, Range(Min, Max)] private float _lowThreshold = 35f;

        [Tooltip("From this on the meter is green (the patient trusts the nurse).")]
        [SerializeField, Range(Min, Max)] private float _highThreshold = 65f;

        /// <summary>Raised with the previous and the new value.</summary>
        public event Action<float, float> Changed;

        public float Value => _value;
        public float Initial => _initial;
        public float LowThreshold => _lowThreshold;
        public float HighThreshold => _highThreshold;

        /// <summary>0..1, for a bar.</summary>
        public float Normalized => Mathf.InverseLerp(Min, Max, _value);

        public void Set(float value)
        {
            value = Mathf.Clamp(value, Min, Max);
            if (Mathf.Approximately(value, _value))
                return;
            var previous = _value;
            _value = value;
            Changed?.Invoke(previous, _value);
        }

        public void Add(float delta) => Set(_value + delta);

        /// <summary>Back to the initial value, e.g. when the conversation starts again.</summary>
        public void ResetToInitial() => Set(_initial);
    }
}
