using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Laat het licht van een kamer langzaam dimmen, opwarmen of terugkeren naar de beginstand.
    /// Past enkel de bestaande lichten aan (intensiteit en kleur), geen extra lichtbronnen.
    /// Zonder lijst neemt hij alle lichten onder dit object (bv. op een Room_* het plafondlicht "Light").
    /// </summary>
    public class RoomTint : MonoBehaviour
    {
        [SerializeField] private Light[] _lights = new Light[0];
        [SerializeField] private float _duration = 3f;
        [Tooltip("Intensiteit bij Dim(), als fractie van de beginstand.")]
        [SerializeField, Range(0f, 1f)] private float _dimFactor = 0.3f;
        [SerializeField] private Color _warmColor = new Color(1f, 0.72f, 0.45f);
        [Tooltip("Intensiteit bij Warm(), als fractie van de beginstand.")]
        [SerializeField, Range(0f, 1.5f)] private float _warmFactor = 0.85f;

        private float[] _baseIntensity;
        private Color[] _baseColor;
        private float[] _fromIntensity;
        private Color[] _fromColor;
        private float _targetFactor = 1f;
        private Color? _targetColor;
        private float _time = -1f;

        private void Awake()
        {
            if (_lights.Length == 0)
                _lights = GetComponentsInChildren<Light>(true);
            _baseIntensity = new float[_lights.Length];
            _baseColor = new Color[_lights.Length];
            _fromIntensity = new float[_lights.Length];
            _fromColor = new Color[_lights.Length];
            for (var i = 0; i < _lights.Length; i++)
            {
                _baseIntensity[i] = _lights[i].intensity;
                _baseColor[i] = _lights[i].color;
            }
        }

        public void Dim() => StartTransition(_dimFactor, null);
        public void Warm() => StartTransition(_warmFactor, _warmColor);
        public void Restore() => StartTransition(1f, null);

        private void StartTransition(float factor, Color? color)
        {
            if (_baseIntensity == null)
                Awake();
            for (var i = 0; i < _lights.Length; i++)
            {
                _fromIntensity[i] = _lights[i].intensity;
                _fromColor[i] = _lights[i].color;
            }
            _targetFactor = factor;
            _targetColor = color;
            _time = 0f;
        }

        private void Update()
        {
            if (_time < 0f)
                return;
            _time += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_time / Mathf.Max(0.01f, _duration)));
            for (var i = 0; i < _lights.Length; i++)
            {
                _lights[i].intensity = Mathf.Lerp(_fromIntensity[i], _baseIntensity[i] * _targetFactor, t);
                _lights[i].color = Color.Lerp(_fromColor[i], _targetColor ?? _baseColor[i], t);
            }
            if (t >= 1f)
                _time = -1f;
        }
    }
}
