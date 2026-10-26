using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Lichtstrook in de gangvloer, van de lift tot aan één deur (Hospital/FloorN/GuideLights/Guide_&lt;deur&gt;).
    /// Enkel een emissive materiaal, geen lichtbron. Standaard neutraal (zwak wit).
    /// Afspraak: groen (Calm) = rustige kamer, paars (Difficult) = moeilijke kamer.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloorGuideLight : MonoBehaviour
    {
        public static readonly Color Calm = new Color(0.20f, 0.90f, 0.35f);
        public static readonly Color Difficult = new Color(0.42f, 0.12f, 1f);
        public static readonly Color Neutral = new Color(0.55f, 0.57f, 0.60f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Color _color = Neutral;
        [Tooltip("Helderheid van de strook bij een kleur (Neutral krijgt de helft).")]
        [SerializeField] private float _intensity = 1.5f;
        [SerializeField] private float _pulseDuration = 1.5f;
        [SerializeField] private int _pulseCount = 3;
        [Tooltip("Extra helderheid op de top van een puls.")]
        [SerializeField] private float _pulseBoost = 2f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _pulseTime = -1f;

        public Color Color => _color;

        private void Awake()
        {
            Apply(1f);
        }

        /// <summary>Kleurt de strook, bv. FloorGuideLight.Calm of FloorGuideLight.Difficult. Color.black zet hem uit.</summary>
        public void SetColor(Color color)
        {
            _color = color;
            Apply(1f);
        }

        public void SetNeutral() => SetColor(Neutral);

        /// <summary>Laat de strook een paar keer oplichten in zijn huidige kleur (bv. om de speler naar een deur te leiden).</summary>
        public void Pulse()
        {
            _pulseTime = 0f;
        }

        private void Update()
        {
            if (_pulseTime < 0f)
                return;
            _pulseTime += Time.deltaTime;
            if (_pulseTime >= _pulseDuration)
            {
                _pulseTime = -1f;
                Apply(1f);
                return;
            }
            var wave = Mathf.Sin(Mathf.PI * _pulseCount * _pulseTime / _pulseDuration);
            Apply(1f + _pulseBoost * wave * wave);
        }

        private void Apply(float boost)
        {
            if (_renderers == null)
                _renderers = GetComponentsInChildren<Renderer>(true);
            _block ??= new MaterialPropertyBlock();

            var intensity = (_color == Neutral ? _intensity * 0.5f : _intensity) * boost;
            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Color.Lerp(new Color(0.3f, 0.3f, 0.3f), _color, 0.5f));
                _block.SetColor(EmissionColorId, _color * intensity);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
