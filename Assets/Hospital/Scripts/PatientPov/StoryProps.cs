using ProCommVR.Emotions;
using ProCommVR.Pov;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// The granddaughter's card, photo and balloon in Scenario 1. In the nurse view they simply stand there (a good
    /// student notices the card). In the patient view they react to the patient's dominant emotion
    /// (<see cref="ScenarioOneStoryProps"/>): Anger crumples the card; Joy brightens the photo and lets the balloon rise
    /// and sway; Sadness turns the photo grey (and above Tip Over At it falls face down) and lets the balloon deflate and
    /// drift away. Fear needs nothing here: the room goes dark and the props are only seen in the flashlight.
    /// Back in the nurse view everything is at its built state at once.
    /// </summary>
    public class StoryProps : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private PatientWorldController _world;
        [SerializeField] private ScenarioOneStoryProps _settings;

        [Header("Props (built by PatientPovSetup)")]
        [SerializeField] private Transform _card;
        [SerializeField] private Transform _photoFrame;
        [Tooltip("The color photo on top of the grey one: its alpha fades with Sadness.")]
        [SerializeField] private Renderer _photoColor;
        [SerializeField] private Transform _balloon;
        [Tooltip("Direction (world) the balloon drifts away to.")]
        [SerializeField] private Vector3 _driftDirection = Vector3.right;

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;

            public static Pose Of(Transform t) => new Pose { Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale };

            public void ApplyTo(Transform t)
            {
                t.localPosition = Position;
                t.localRotation = Rotation;
                t.localScale = Scale;
            }
        }

        private Pose _cardPose, _framePose, _balloonPose;
        private MaterialPropertyBlock _block;
        private Color _photoBase = Color.white;
        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;
        private Emotion? _shown;
        private float _weight;
        private float _tip;
        private bool _modified;

        public Emotion? ShownEmotion => _weight > 0f ? _shown : null;
        public float Weight => _weight;
        /// <summary>0 standing, 1 face down.</summary>
        public float PhotoTip => _tip;
        /// <summary>1 = full color, 0 = grey.</summary>
        public float PhotoColorAmount { get; private set; } = 1f;
        public float BalloonScale => _balloon != null && _balloonPose.Scale.x > 0f ? _balloon.localScale.x / _balloonPose.Scale.x : 1f;
        public float BalloonRise => _balloon != null ? _balloon.localPosition.y - _balloonPose.Position.y : 0f;
        public Transform Card => _card;
        public Transform PhotoFrame => _photoFrame;
        public Transform Balloon => _balloon;

        /// <summary>True when every prop is exactly at its built state.</summary>
        public bool AtBuiltState =>
            Same(_card, _cardPose) && Same(_photoFrame, _framePose) && Same(_balloon, _balloonPose) && PhotoColorAmount >= 1f && !_modified;

        private void Awake()
        {
            if (_card != null) _cardPose = Pose.Of(_card);
            if (_photoFrame != null) _framePose = Pose.Of(_photoFrame);
            if (_balloon != null) _balloonPose = Pose.Of(_balloon);
            _block = new MaterialPropertyBlock();
            if (_photoColor != null && _photoColor.sharedMaterial != null && _photoColor.sharedMaterial.HasProperty(BaseColorId))
                _photoBase = _photoColor.sharedMaterial.GetColor(BaseColorId);
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
            if (_settings == null)
                _settings = ScriptableObject.CreateInstance<ScenarioOneStoryProps>();
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
            ResetProps();
        }

        private void OnPovChanged(PovMode mode)
        {
            if (mode == PovMode.Nurse)
                ResetProps();
        }

        private void Update()
        {
            if (_pov == null || _pov.Mode != PovMode.Patient)
                return;
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            var shown = PatientPovMath.StepExclusive(ref _shown, ref _weight, dominant, strength, Time.deltaTime / _settings.BlendSeconds);
            var w = Mathf.SmoothStep(0f, 1f, _weight);

            var wantTip = shown == Emotion.Sadness && _holder.Current.Sadness >= _settings.TipOverAt ? 1f : 0f;
            _tip = Mathf.MoveTowards(_tip, wantTip, Time.deltaTime / _settings.TipSeconds);

            Show(shown, w);
        }

        private void Show(Emotion? emotion, float w)
        {
            _modified = true;
            var anger = emotion == Emotion.Anger ? w : 0f;
            var joy = emotion == Emotion.Joy ? w : 0f;
            var sad = emotion == Emotion.Sadness ? w : 0f;

            if (_card != null)
            {
                _card.localPosition = _cardPose.Position;
                _card.localScale = Vector3.Scale(_cardPose.Scale, Vector3.Lerp(Vector3.one, _settings.CrumpledScale, anger));
                _card.localRotation = _cardPose.Rotation * Quaternion.Euler(_settings.CrumpleTiltDegrees * 0.6f * anger, 0f, _settings.CrumpleTiltDegrees * anger);
            }

            if (_photoFrame != null)
            {
                // The frame's pivot is its bottom front edge and the photo faces +z: turning around x lays it face down.
                var tip = Mathf.SmoothStep(0f, 1f, _tip);
                _photoFrame.localRotation = _framePose.Rotation * Quaternion.Euler(80f * tip, 0f, 0f);
            }

            PhotoColorAmount = 1f - sad;
            if (_photoColor != null)
            {
                var c = _photoBase * Mathf.Lerp(1f, _settings.JoyBrightness, joy);
                c.a = PhotoColorAmount;
                _block.Clear();
                _block.SetColor(BaseColorId, c);
                _photoColor.SetPropertyBlock(_block);
            }

            if (_balloon != null)
            {
                var sway = Mathf.Sin(Time.time * Mathf.PI) * _settings.Sway * joy;
                var drift = _driftDirection.normalized * (_settings.DriftDistance * sad);
                var position = _balloonPose.Position
                               + Vector3.up * (_settings.RiseHeight * joy - _settings.SinkDistance * sad)
                               + (_balloon.parent != null ? _balloon.parent.InverseTransformVector(drift) : drift);
                position.x += sway;
                _balloon.localPosition = position;
                _balloon.localScale = _balloonPose.Scale * Mathf.Lerp(1f, _settings.DeflatedScale, sad);
                _balloon.localRotation = _balloonPose.Rotation * Quaternion.Euler(0f, 0f, sway * 120f);
            }
        }

        /// <summary>Everything back to the built state.</summary>
        public void ResetProps()
        {
            _shown = null;
            _weight = 0f;
            _tip = 0f;
            PhotoColorAmount = 1f;
            if (_card != null) _cardPose.ApplyTo(_card);
            if (_photoFrame != null) _framePose.ApplyTo(_photoFrame);
            if (_balloon != null) _balloonPose.ApplyTo(_balloon);
            if (_photoColor != null)
                _photoColor.SetPropertyBlock(null);
            _modified = false;
        }

        private static bool Same(Transform t, Pose pose)
        {
            return t == null || (t.localPosition == pose.Position && t.localRotation == pose.Rotation && t.localScale == pose.Scale);
        }
    }
}
