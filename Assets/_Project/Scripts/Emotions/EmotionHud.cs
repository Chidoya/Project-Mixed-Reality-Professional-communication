using System;
using System.Collections;
using ProCommVR.Pov;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// A small world-space panel with the patient's four emotions, only in the patient view. It soft-follows the head:
    /// about 1.6 m away, to the left of the center of the view, lagging slightly behind head turns, always facing the
    /// camera and never closer to the center than Min Angle From Center.
    /// <para>
    /// The values come from <see cref="EmotionHolder.EmotionChanged"/> (raised every frame while the emotions blend, so
    /// the bars move smoothly without polling). The dominant emotion's row is bright with a bold label, the others are
    /// dimmed. Shown on <see cref="PovController.PovChanged"/>(Patient), hidden on (Nurse), with a short fade; while
    /// hidden the canvas is off.
    /// </para>
    /// Built as a prefab by the editor (EmotionHudSetup); the scene builder connects the holder and the POV controller.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public class EmotionHud : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public Emotion Emotion;
            public TMP_Text Label;
            public RectTransform Fill;
            public Image FillImage;
            public TMP_Text Number;
        }

        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;

        [Tooltip("Empty: the main camera.")]
        [SerializeField] private Transform _camera;

        [Header("Layout")]
        [SerializeField] private Row[] _rows = Array.Empty<Row>();
        [SerializeField] private TMP_Text _header;
        [SerializeField] private TMP_Text _hint;

        [Tooltip("Width of a full bar (value 1), in canvas units.")]
        [SerializeField, Min(1f)] private float _barWidth = 180f;

        [Header("Follow the head")]
        [SerializeField, Min(0.3f)] private float _distance = 1.6f;

        [Tooltip("Degrees from the center of the view; negative is to the left.")]
        [SerializeField, Range(-60f, 60f)] private float _yawOffset = -22f;

        [Tooltip("Meters below (negative) or above eye height.")]
        [SerializeField] private float _heightOffset = -0.1f;

        [Tooltip("How long the panel lags behind a head turn, in seconds.")]
        [SerializeField, Min(0f)] private float _followSmoothTime = 0.25f;

        [Tooltip("The panel never comes closer than this to the center of the view (degrees), also during a fast turn.")]
        [SerializeField, Range(0f, 45f)] private float _minAngleFromCenter = 12f;

        [Tooltip("And never further away than this (degrees).")]
        [SerializeField, Range(10f, 90f)] private float _maxAngleFromCenter = 40f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.2f;

        [Tooltip("Alpha of the bars and labels that are not the dominant emotion.")]
        [SerializeField, Range(0f, 1f)] private float _dimmedAlpha = 0.55f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private Coroutine _fade;
        private float _yaw;
        private float _yawVelocity;
        private bool _shown;
        private Emotion? _dominant;
        private bool _dominantSet;

        public bool IsShown => _shown;
        public bool IsCanvasOn => _canvas != null && _canvas.enabled;
        public Emotion? Dominant => _dominant;
        public float Alpha => _group != null ? _group.alpha : 0f;
        public string HintText => _hint != null ? _hint.text : null;
        public float BarWidth => _barWidth;
        public float MinAngleFromCenter => _minAngleFromCenter;
        public float YawOffset => _yawOffset;
        public float Distance => _distance;

        public Row GetRow(Emotion emotion)
        {
            foreach (var row in _rows)
            {
                if (row.Emotion == emotion)
                    return row;
            }
            return null;
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _canvas.enabled = false;
            if (_camera == null && Camera.main != null)
                _camera = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (_holder != null)
                _holder.EmotionChanged += OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.EmotionChanged -= OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
        }

        private void LateUpdate()
        {
            if (_canvas.enabled)
                Follow(false);
        }

        /// <summary>
        /// Shows or hides the panel, with a fade.
        /// </summary>
        public void SetShown(bool shown)
        {
            if (_shown == shown)
                return;
            _shown = shown;
            if (shown)
            {
                if (_holder != null)
                    ShowValues(_holder.Current);
                _canvas.enabled = true;
                Follow(true);
            }
            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(FadeTo(shown ? 1f : 0f));
        }

        private void OnPovChanged(PovMode mode)
        {
            SetShown(mode == PovMode.Patient);
        }

        private void OnEmotionChanged(EmotionState previous, EmotionState current)
        {
            if (_shown)
                ShowValues(current);
        }

        private void ShowValues(EmotionState state)
        {
            var dominant = _holder != null ? _holder.Dominant : state.Dominant;
            var dominantChanged = !_dominantSet || dominant != _dominant;
            _dominant = dominant;
            _dominantSet = true;

            foreach (var row in _rows)
            {
                var value = state.Get(row.Emotion);
                var size = row.Fill.sizeDelta;
                size.x = value * _barWidth;
                row.Fill.sizeDelta = size;
                row.Number.SetText("{0}", Mathf.RoundToInt(value * 100f));
                if (!dominantChanged)
                    continue;

                var isDominant = row.Emotion == dominant;
                var color = row.FillImage.color;
                color.a = isDominant ? 1f : _dimmedAlpha;
                row.FillImage.color = color;
                row.Label.fontStyle = isDominant ? FontStyles.Bold : FontStyles.Normal;
                row.Label.alpha = isDominant ? 1f : 0.8f;
                row.Number.fontStyle = isDominant ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>
        /// Places the panel to the side of the view, lagging slightly behind head turns (snap: no lag).
        /// </summary>
        private void Follow(bool snap)
        {
            if (_camera == null)
                return;
            var forward = _camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = _camera.up;
            var cameraYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var target = cameraYaw + _yawOffset;

            if (snap)
            {
                _yaw = target;
                _yawVelocity = 0f;
            }
            else
                _yaw = Mathf.SmoothDampAngle(_yaw, target, ref _yawVelocity, _followSmoothTime);

            // Never in the way of the center of the view, and never lost behind the player.
            var side = Mathf.Sign(_yawOffset == 0f ? -1f : _yawOffset);
            var fromCenter = Mathf.DeltaAngle(cameraYaw, _yaw) * side;
            var clamped = Mathf.Clamp(fromCenter, _minAngleFromCenter, _maxAngleFromCenter);
            if (!Mathf.Approximately(clamped, fromCenter))
                _yaw = cameraYaw + clamped * side;

            var direction = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            var position = _camera.position + direction * _distance + Vector3.up * _heightOffset;
            var toPanel = position - _camera.position;
            toPanel.y = 0f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(toPanel, Vector3.up));
        }

        private IEnumerator FadeTo(float alpha)
        {
            var from = _group.alpha;
            for (var t = 0f; t < _fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, alpha, t / _fadeSeconds);
                yield return null;
            }
            _group.alpha = alpha;
            if (alpha <= 0f)
                _canvas.enabled = false;
            _fade = null;
        }
    }
}
