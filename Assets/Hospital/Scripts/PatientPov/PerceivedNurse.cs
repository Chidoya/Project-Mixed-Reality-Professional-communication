using System;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using ProCommVR.Pov;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// On the player's nurse avatar (added per scene, Jalil's prefab stays unchanged): in the patient view the nurse
    /// looks the way the patient perceives her, from the patient's dominant emotion (<see cref="PerceivedNurseProfile"/>):
    /// stern and bigger when he is angry, dark when he is afraid, grey and smaller when he is sad, warm and smiling when
    /// he is happy. Tints use a MaterialPropertyBlock (no new materials). Back in the nurse view (behind the black fade)
    /// scale, pose, colors and face are restored at once.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerceivedNurse : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ExpressionCount = Enum.GetValues(typeof(FaceExpression)).Length;

        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private PerceivedNurseProfile _profile;
        [Tooltip("The patient's world controller: its Full Strength At is used. Empty: 0.6.")]
        [SerializeField] private PatientWorldController _world;
        [Tooltip("The patient (she leans toward him).")]
        [SerializeField] private Transform _patient;
        [SerializeField] private FaceController _face;

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Color[][] _baseColors = Array.Empty<Color[]>();
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _empty;
        private float[] _mix;
        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;

        private bool _active;
        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private Emotion? _shown;
        private float _weight;
        private bool _tinted;

        /// <summary>The emotion whose look is shown now (null: as she is).</summary>
        public Emotion? ShownEmotion => _weight > 0f ? _shown : null;
        public float Weight => _weight;
        public bool IsTinted => _tinted;
        public FaceController Face => _face;
        public float ScaleFactor => _active && _baseScale.x > 0f ? transform.localScale.x / _baseScale.x : 1f;

        private void Awake()
        {
            if (_face == null)
                _face = GetComponentInChildren<FaceController>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length][];
            for (var i = 0; i < _renderers.Length; i++)
            {
                var materials = _renderers[i].sharedMaterials;
                _baseColors[i] = new Color[materials.Length];
                for (var m = 0; m < materials.Length; m++)
                    _baseColors[i][m] = materials[m] != null && materials[m].HasProperty(BaseColorId) ? materials[m].GetColor(BaseColorId) : Color.white;
            }
            _block = new MaterialPropertyBlock();
            _empty = new MaterialPropertyBlock();
            _mix = new float[ExpressionCount];
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
            if (_profile == null)
                _profile = ScriptableObject.CreateInstance<PerceivedNurseProfile>();
        }

        private void OnEnable()
        {
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
        }

        private void OnDisable()
        {
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            Restore();
        }

        private void OnPovChanged(PovMode mode)
        {
            if (mode == PovMode.Patient)
            {
                // The avatar has just been placed where the player stood: that is the normal look.
                _baseScale = transform.localScale;
                _baseRotation = transform.rotation;
                _active = true;
                _shown = null;
                _weight = 0f;
            }
            else
                Restore();
        }

        private void LateUpdate()
        {
            if (!_active)
                return;
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            if (dominant.HasValue && _profile.Get(dominant.Value) == null)
                dominant = null;
            var shown = PatientPovMath.StepExclusive(ref _shown, ref _weight, dominant, dominant.HasValue ? strength : 0f,
                Time.deltaTime / _profile.BlendSeconds);
            Apply(shown.HasValue ? _profile.Get(shown.Value) : null, Mathf.SmoothStep(0f, 1f, _weight));
        }

        private void Apply(PerceivedNurseProfile.Look look, float w)
        {
            if (look == null || w <= 0f)
            {
                transform.localScale = _baseScale;
                transform.rotation = _baseRotation;
                ClearTint();
                if (_face != null)
                    _face.ClearMix(0.3f);
                return;
            }

            transform.localScale = _baseScale * Mathf.Lerp(1f, look.Scale, w);
            var lean = Quaternion.identity;
            if (_patient != null && look.LeanDegrees > 0f)
            {
                var toPatient = _patient.position - transform.position;
                toPatient.y = 0f;
                if (toPatient.sqrMagnitude > 0.0001f)
                    lean = Quaternion.AngleAxis(look.LeanDegrees * w, Vector3.Cross(Vector3.up, toPatient.normalized));
            }
            transform.rotation = lean * _baseRotation;

            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;
                var colors = _baseColors[i];
                for (var m = 0; m < colors.Length; m++)
                {
                    var c = colors[m];
                    var grey = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    var target = Color.Lerp(c, new Color(grey, grey, grey, c.a), look.Desaturate) * look.Tint;
                    target.a = c.a;
                    _block.Clear();
                    _block.SetColor(BaseColorId, Color.Lerp(c, target, w));
                    renderer.SetPropertyBlock(_block, m);
                }
            }
            _tinted = true;

            if (_face != null)
            {
                Array.Clear(_mix, 0, _mix.Length);
                _mix[(int)look.Face] = look.FaceIntensity * w;
                _face.SetMix(_mix, 0.3f);
            }
        }

        private void ClearTint()
        {
            if (!_tinted)
                return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;
                for (var m = 0; m < _baseColors[i].Length; m++)
                    _renderers[i].SetPropertyBlock(_empty, m);
            }
            _tinted = false;
        }

        /// <summary>Back to the nurse exactly as she was.</summary>
        private void Restore()
        {
            if (_active)
            {
                transform.localScale = _baseScale;
                transform.rotation = _baseRotation;
            }
            _active = false;
            _shown = null;
            _weight = 0f;
            ClearTint();
            if (_face != null)
                _face.ClearMix(0f);
        }
    }
}
