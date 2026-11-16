using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// A place in the room where an emotion shows itself in the patient view: a plant that grows, a fire, the rain
    /// outside the window, a light. Placed by the scene builder, never by hand. The <see cref="PatientWorldController"/>
    /// sets its <see cref="Weight"/> (0: off, 1: full); at 0 its content is inactive, so it costs nothing.
    /// <para>
    /// Each part is optional: Grow scales from nothing to its built size (with a small sway), the particle systems emit
    /// in proportion to the weight, the light fades in (with an optional flicker).
    /// </para>
    /// </summary>
    public class EmotionEffectAnchor : MonoBehaviour
    {
        [SerializeField] private Emotion _emotion;
        [SerializeField] private EmotionEffectKind _kind;

        [Tooltip("Switched on while the weight is above 0.")]
        [SerializeField] private GameObject _content;

        [Header("Grow (plants)")]
        [Tooltip("Scaled from nothing to its built size.")]
        [SerializeField] private Transform _grow;

        [Tooltip("Sway in degrees while grown.")]
        [SerializeField, Range(0f, 15f)] private float _swayDegrees = 3f;
        [SerializeField, Min(0f)] private float _swaySpeed = 0.6f;

        [Header("Particles")]
        [SerializeField] private ParticleSystem[] _particles = System.Array.Empty<ParticleSystem>();

        [Header("Light")]
        [SerializeField] private Light _light;

        [Tooltip("0: steady. 1: flickers between almost off and full.")]
        [SerializeField, Range(0f, 1f)] private float _flicker;
        [SerializeField, Min(0f)] private float _flickerSpeed = 9f;

        private float _weight;
        private bool _initialized;
        private Vector3 _growScale;
        private Quaternion _growRotation;
        private float[] _emissionRates;
        private float _lightIntensity;
        private float _seed;

        public Emotion Emotion => _emotion;
        public EmotionEffectKind Kind => _kind;
        public float Weight => _weight;
        public bool IsOn => _content != null && _content.activeSelf;
        public Light Light => _light;
        public ParticleSystem[] Particles => _particles;
        public Transform Grow => _grow;

        private void Awake()
        {
            Initialize();
            Apply();
        }

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _seed = Random.value * 100f;
            if (_grow != null)
            {
                _growScale = _grow.localScale;
                _growRotation = _grow.localRotation;
            }
            _emissionRates = new float[_particles.Length];
            for (var i = 0; i < _particles.Length; i++)
                _emissionRates[i] = _particles[i].emission.rateOverTimeMultiplier;
            if (_light != null)
                _lightIntensity = _light.intensity;
        }

        /// <summary>
        /// 0: off (content inactive), 1: full effect.
        /// </summary>
        public void SetWeight(float weight)
        {
            Initialize();
            weight = Mathf.Clamp01(weight);
            if (Mathf.Approximately(weight, _weight) && (weight > 0f) == IsOn)
                return;
            _weight = weight;
            Apply();
        }

        private void Apply()
        {
            var on = _weight > 0f;
            if (_content != null && _content.activeSelf != on)
                _content.SetActive(on);
            if (!on)
                return;

            for (var i = 0; i < _particles.Length; i++)
            {
                var emission = _particles[i].emission;
                emission.rateOverTimeMultiplier = _emissionRates[i] * _weight;
            }
            UpdateMotion();
        }

        private void Update()
        {
            if (_weight > 0f)
                UpdateMotion();
        }

        private void UpdateMotion()
        {
            if (_grow != null)
            {
                var size = Mathf.SmoothStep(0f, 1f, _weight);
                _grow.localScale = _growScale * Mathf.Max(size, 0.001f);
                var sway = Mathf.Sin((Time.time + _seed) * _swaySpeed * Mathf.PI * 2f) * _swayDegrees * size;
                _grow.localRotation = _growRotation * Quaternion.Euler(sway, 0f, sway * 0.5f);
            }
            if (_light != null)
            {
                var flicker = _flicker > 0f ? 1f - _flicker * Mathf.PerlinNoise(_seed, Time.time * _flickerSpeed) : 1f;
                _light.intensity = _lightIntensity * _weight * flicker;
            }
        }
    }
}
