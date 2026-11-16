using System;
using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// How strongly a patient feels each of the four <see cref="Emotion"/>s. As a state every value is 0..1; as a
    /// delta (the change after a dialogue option) a value can be negative. The inspector shows four sliders, with the
    /// range of the <see cref="EmotionRangeAttribute"/> on the field.
    /// </summary>
    [Serializable]
    public struct EmotionState : IEquatable<EmotionState>
    {
        /// <summary>
        /// Below this value no emotion is dominant (<see cref="Dominant"/> is null).
        /// </summary>
        public const float DefaultDominantThreshold = 0.15f;

        public static readonly EmotionState Zero = default;

        [SerializeField] private float _joy;
        [SerializeField] private float _anger;
        [SerializeField] private float _fear;
        [SerializeField] private float _sadness;

        public EmotionState(float joy, float anger, float fear, float sadness)
        {
            _joy = joy;
            _anger = anger;
            _fear = fear;
            _sadness = sadness;
        }

        public float Joy => _joy;
        public float Anger => _anger;
        public float Fear => _fear;
        public float Sadness => _sadness;

        /// <summary>
        /// The strongest emotion, or null when none reaches <see cref="DefaultDominantThreshold"/>.
        /// </summary>
        public Emotion? Dominant => GetDominant(DefaultDominantThreshold);

        public float Get(Emotion emotion)
        {
            switch (emotion)
            {
                case Emotion.Joy: return _joy;
                case Emotion.Anger: return _anger;
                case Emotion.Fear: return _fear;
                case Emotion.Sadness: return _sadness;
                default: throw new ArgumentOutOfRangeException(nameof(emotion), emotion, null);
            }
        }

        /// <summary>
        /// A copy with one emotion changed.
        /// </summary>
        public EmotionState With(Emotion emotion, float value)
        {
            var result = this;
            switch (emotion)
            {
                case Emotion.Joy: result._joy = value; break;
                case Emotion.Anger: result._anger = value; break;
                case Emotion.Fear: result._fear = value; break;
                case Emotion.Sadness: result._sadness = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(emotion), emotion, null);
            }
            return result;
        }

        /// <summary>
        /// The strongest emotion, or null when none reaches the threshold. On a tie the first in the enum order wins.
        /// </summary>
        public Emotion? GetDominant(float threshold)
        {
            Emotion? dominant = null;
            var highest = threshold;
            for (var i = 0; i < 4; i++)
            {
                var value = Get((Emotion)i);
                if (value >= highest && (dominant == null || value > highest))
                {
                    dominant = (Emotion)i;
                    highest = value;
                }
            }
            return dominant;
        }

        /// <summary>
        /// This state plus a delta, every value clamped to 0..1.
        /// </summary>
        public EmotionState Add(EmotionState delta)
        {
            return new EmotionState(_joy + delta._joy, _anger + delta._anger, _fear + delta._fear, _sadness + delta._sadness)
                .Clamped();
        }

        /// <summary>
        /// Every value clamped to 0..1.
        /// </summary>
        public EmotionState Clamped()
        {
            return new EmotionState(Mathf.Clamp01(_joy), Mathf.Clamp01(_anger), Mathf.Clamp01(_fear), Mathf.Clamp01(_sadness));
        }

        public static EmotionState Lerp(EmotionState a, EmotionState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new EmotionState(
                Mathf.Lerp(a._joy, b._joy, t),
                Mathf.Lerp(a._anger, b._anger, t),
                Mathf.Lerp(a._fear, b._fear, t),
                Mathf.Lerp(a._sadness, b._sadness, t));
        }

        /// <summary>
        /// True when every value differs less than the tolerance.
        /// </summary>
        public bool Approximately(EmotionState other, float tolerance = 0.001f)
        {
            return Mathf.Abs(_joy - other._joy) <= tolerance && Mathf.Abs(_anger - other._anger) <= tolerance &&
                   Mathf.Abs(_fear - other._fear) <= tolerance && Mathf.Abs(_sadness - other._sadness) <= tolerance;
        }

        public bool Equals(EmotionState other)
        {
            return _joy == other._joy && _anger == other._anger && _fear == other._fear && _sadness == other._sadness;
        }

        public override bool Equals(object obj) => obj is EmotionState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _joy.GetHashCode();
                hash = hash * 397 ^ _anger.GetHashCode();
                hash = hash * 397 ^ _fear.GetHashCode();
                return hash * 397 ^ _sadness.GetHashCode();
            }
        }

        public static bool operator ==(EmotionState a, EmotionState b) => a.Equals(b);
        public static bool operator !=(EmotionState a, EmotionState b) => !a.Equals(b);

        public override string ToString()
        {
            return $"Joy {_joy:F2}, Anger {_anger:F2}, Fear {_fear:F2}, Sadness {_sadness:F2}";
        }
    }
}
