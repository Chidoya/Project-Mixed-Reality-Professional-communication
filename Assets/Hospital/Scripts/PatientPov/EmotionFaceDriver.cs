using System;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using ProCommVR.Pov;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// Shows the patient's emotions on his face, from the same <see cref="EmotionHolder"/> that drives the room in the
    /// patient view. Mixed emotions mix on the face (sad and a little angry: drooping mouth with frowning brows). Each
    /// emotion grows from the holder's threshold (0.15) to the world's Full Strength At (0.6), so the face is "full"
    /// exactly when the room reacts fully. Strong Sadness turns into Crying (tears via TearDrops). The gaze follows the
    /// emotion too (HeadLookAt): sad looks to the window and down, angry stares at the nurse, afraid glances at the door,
    /// happy looks at the nurse.
    /// <para>
    /// While active it wins over FaceFromAnimator (that one is turned off), so the Animator states of NpcReactor do
    /// not fight the face. Talking (TalkLevel), blinking and micro motion keep working.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class EmotionFaceDriver : MonoBehaviour
    {
        private static readonly int ExpressionCount = Enum.GetValues(typeof(FaceExpression)).Length;

        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private FaceController _face;
        [SerializeField] private EmotionFaceMapping _mapping;

        [Tooltip("The room's controller: its Full Strength At is used, so face and room agree. Empty: 0.6.")]
        [SerializeField] private PatientWorldController _world;

        [Tooltip("In the patient view the camera is inside his head: his tear drops are hidden there.")]
        [SerializeField] private PovController _pov;

        [Header("Gaze (optional)")]
        [SerializeField] private HeadLookAt _look;
        [Tooltip("A point in the window the patient looks out of.")]
        [SerializeField] private Transform _window;
        [Tooltip("A point in the doorway.")]
        [SerializeField] private Transform _door;
        [Tooltip("Moved by this component: the point HeadLookAt looks at when not at the nurse.")]
        [SerializeField] private Transform _gazePoint;

        [Header("Debug (read only)")]
        [SerializeField] private FaceExpression _dominantFace;
        [SerializeField] private float[] _mixShown = new float[0];

        private float[] _mix;
        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;
        private FaceFromAnimator _fromAnimator;
        private bool _fromAnimatorWasEnabled;
        private Animator _animator;
        private Transform _head;
        private float _nextGlance;
        private float _glanceUntil;

        /// <summary>The face expression with the largest weight now (Neutral when nothing shows).</summary>
        public FaceExpression DominantFace => _dominantFace;

        /// <summary>Weight (0..1) of an expression in the current mix.</summary>
        public float GetMix(FaceExpression expression) => _mix != null ? _mix[(int)expression] : 0f;

        public float FullStrengthAt => _fullStrengthAt;
        public FaceController Face => _face;
        public EmotionHolder Holder => _holder;
        public bool LooksAtNurse => _look != null && _look.Target == null;
        public bool GlancingAtDoor => Time.time < _glanceUntil;

        private void Awake()
        {
            _mix = new float[ExpressionCount];
            _mixShown = new float[ExpressionCount];
            if (_face == null)
                _face = GetComponentInChildren<FaceController>();
            if (_look == null)
                _look = GetComponentInChildren<HeadLookAt>();
            _fromAnimator = GetComponentInChildren<FaceFromAnimator>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.isHuman)
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
            if (_mapping == null)
                _mapping = ScriptableObject.CreateInstance<EmotionFaceMapping>();
        }

        private void OnEnable()
        {
            if (_fromAnimator != null)
            {
                _fromAnimatorWasEnabled = _fromAnimator.enabled;
                _fromAnimator.enabled = false;
            }
            if (_holder != null)
                _holder.EmotionChanged += OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
            Refresh(0f);
        }

        private void OnPovChanged(PovMode mode)
        {
            // The PovAnchor hides his body, but the tear drops are made later (TearDrops pool): hide them too.
            var tears = GetComponentInChildren<TearDrops>(true);
            if (tears != null)
                tears.Hidden = mode == PovMode.Patient;
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.EmotionChanged -= OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            if (_face != null)
                _face.ClearMix(0.3f);
            if (_fromAnimator != null && _fromAnimatorWasEnabled)
                _fromAnimator.enabled = true;
            if (_look != null)
            {
                _look.Target = null;
                _look.Weight = 1f;
            }
        }

        private void OnEmotionChanged(EmotionState previous, EmotionState current)
        {
            Refresh(_mapping.BlendTime);
        }

        /// <summary>Recomputes the mix from the holder now (also used by tests).</summary>
        public void Refresh(float blendTime)
        {
            if (_holder == null || _face == null || _mix == null)
                return;
            Array.Clear(_mix, 0, _mix.Length);
            var state = _holder.Current;
            var k = _mapping.Intensity;
            Add(_mapping.Joy, PatientPovMath.Strength(state.Joy, _fullStrengthAt) * k);
            Add(_mapping.Anger, PatientPovMath.Strength(state.Anger, _fullStrengthAt) * k);
            Add(_mapping.Fear, PatientPovMath.Strength(state.Fear, _fullStrengthAt) * k);
            var sad = PatientPovMath.Strength(state.Sadness, _fullStrengthAt) * k;
            var crying = Mathf.InverseLerp(_mapping.CryingFrom, Mathf.Max(_mapping.CryingFull, _mapping.CryingFrom + 0.01f), state.Sadness);
            Add(_mapping.Sadness, sad * (1f - crying));
            Add(FaceExpression.Crying, sad * crying);

            _face.SetMix(_mix, blendTime);
            _dominantFace = _face.DominantExpression;
            Array.Copy(_mix, _mixShown, _mix.Length);
        }

        private void Add(FaceExpression expression, float weight)
        {
            _mix[(int)expression] = Mathf.Clamp01(_mix[(int)expression] + weight);
        }

        private void Update()
        {
            if (_look == null || _holder == null)
                return;
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            if (!dominant.HasValue || strength < _mapping.GazeFrom)
            {
                LookAtNurse(1f);
                return;
            }

            switch (dominant.Value)
            {
                case Emotion.Sadness:
                    LookAtPoint(_window, -_mapping.SadLookDownDegrees, Mathf.Lerp(0.6f, 1f, strength));
                    break;
                case Emotion.Fear:
                    if (Time.time >= _nextGlance)
                    {
                        _glanceUntil = Time.time + _mapping.FearGlanceSeconds;
                        _nextGlance = _glanceUntil + UnityEngine.Random.Range(_mapping.FearGlanceInterval.x, _mapping.FearGlanceInterval.y);
                    }
                    if (Time.time < _glanceUntil && _door != null)
                        LookAtPoint(_door, 0f, 1f);
                    else
                        LookAtNurse(1f);
                    break;
                default:
                    // Anger stares, Joy looks warmly: both at the nurse.
                    LookAtNurse(1f);
                    break;
            }
        }

        private void LookAtNurse(float weight)
        {
            _look.Target = null; // HeadLookAt: null = the player's camera
            _look.Mode = HeadLookAt.LookMode.LookAtPlayer;
            _look.Weight = weight;
        }

        /// <summary>Looks toward a far point through a gaze point within HeadLookAt's range.</summary>
        private void LookAtPoint(Transform point, float pitchDegrees, float weight)
        {
            if (point == null || _gazePoint == null || _head == null)
            {
                LookAtNurse(weight);
                return;
            }
            var direction = point.position - _head.position;
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f)
                flat = transform.forward;
            var elevation = Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg + pitchDegrees;
            var dir = Quaternion.AngleAxis(-elevation, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
            _gazePoint.position = _head.position + dir * _mapping.GazePointDistance;
            _look.Target = _gazePoint;
            _look.Mode = HeadLookAt.LookMode.LookAtPlayer;
            _look.Weight = weight;
        }
    }
}
