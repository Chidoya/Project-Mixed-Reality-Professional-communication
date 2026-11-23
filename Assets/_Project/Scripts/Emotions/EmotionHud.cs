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
    /// about 1.1 m away, to the left of the center of the view, lagging slightly behind head turns, always facing the
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

        [Tooltip("Optional. How the patient thinks about the nurse: the trust meter under the emotions.")]
        [SerializeField] private PatientReputation _reputation;

        [Tooltip("Empty: the main camera.")]
        [SerializeField] private Transform _camera;

        [Header("Layout")]
        [SerializeField] private Row[] _rows = Array.Empty<Row>();
        [SerializeField] private TMP_Text _header;
        [SerializeField] private TMP_Text _hint;

        [Header("Trust meter (optional)")]
        [Tooltip("Shown only when a reputation is connected.")]
        [SerializeField] private GameObject _trustGroup;
        [SerializeField] private RectTransform _trustFill;
        [SerializeField] private Image _trustFillImage;
        [SerializeField] private TMP_Text _trustNumber;
        [SerializeField] private TMP_Text _trustLabel;
        [SerializeField, Min(1f)] private float _trustBarWidth = 448f;
        [SerializeField] private Color _trustLowColor = new Color(0.95f, 0.38f, 0.32f);
        [SerializeField] private Color _trustMidColor = new Color(0.98f, 0.76f, 0.3f);
        [SerializeField] private Color _trustHighColor = new Color(0.37f, 0.82f, 0.5f);
        [Tooltip("How fast the trust bar follows a new value (seconds).")]
        [SerializeField, Min(0.01f)] private float _trustSmoothTime = 0.6f;

        [Tooltip("Width of a full bar (value 1), in canvas units.")]
        [SerializeField, Min(1f)] private float _barWidth = 180f;

        [Header("Follow the head")]
        [SerializeField, Min(0.3f)] private float _distance = 1.1f;

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

        [Header("Last change (what the last sentence did)")]
        [Tooltip("An up or down arrow next to Trust and next to the dominant emotion after every change of the patient.")]
        [SerializeField] private bool _showLastChange;
        [SerializeField] private Color _upColor = new Color(0.37f, 0.85f, 0.5f);
        [SerializeField] private Color _downColor = new Color(0.98f, 0.42f, 0.36f);

        private Canvas _canvas;
        private CanvasGroup _group;
        private Coroutine _fade;
        private float _yaw;
        private float _yawVelocity;
        private bool _shown;
        private Emotion? _dominant;
        private bool _dominantSet;
        private float _trustShown = -1f;
        private float _trustVelocity;
        private float _lastTrust = -1f;
        private EmotionState _lastTarget;
        private bool _lastTargetSet;
        private string _trustLabelBase;
        private string[] _rowLabelBase = Array.Empty<string>();

        /// <summary>What the last change of the trust did: +1 up, -1 down, 0 nothing yet.</summary>
        public int TrustTrend { get; private set; }
        /// <summary>The last change of the trust, in points.</summary>
        public int TrustDelta { get; private set; }
        /// <summary>The emotion of the last emotion change (the patient's new dominant emotion) and its direction.</summary>
        public Emotion? EmotionTrendFor { get; private set; }
        public int EmotionTrend { get; private set; }
        public string TrustLabelText => _trustLabel != null ? _trustLabel.text : null;

        public bool IsShown => _shown;
        public bool IsCanvasOn => _canvas != null && _canvas.enabled;
        public Emotion? Dominant => _dominant;
        public float Alpha => _group != null ? _group.alpha : 0f;
        public string HintText => _hint != null ? _hint.text : null;
        public float BarWidth => _barWidth;
        public float MinAngleFromCenter => _minAngleFromCenter;
        public float YawOffset => _yawOffset;
        public float Distance => _distance;
        public PatientReputation Reputation => _reputation;
        /// <summary>The trust value the bar shows right now (it follows the reputation smoothly), or -1 without a reputation.</summary>
        public float TrustShown => _trustShown;
        public string TrustText => _trustNumber != null ? _trustNumber.text : null;

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
            if (_trustGroup != null)
                _trustGroup.SetActive(_reputation != null);
            if (_camera == null && Camera.main != null)
                _camera = Camera.main.transform;
            _trustLabelBase = _trustLabel != null ? _trustLabel.text : "";
            _rowLabelBase = new string[_rows.Length];
            for (var i = 0; i < _rows.Length; i++)
            {
                _rowLabelBase[i] = _rows[i].Label != null ? _rows[i].Label.text : _rows[i].Emotion.ToString();
                if (_showLastChange && _rows[i].Number != null)
                {
                    _rows[i].Number.textWrappingMode = TextWrappingModes.NoWrap;
                    _rows[i].Number.overflowMode = TextOverflowModes.Overflow;
                }
            }
            if (_showLastChange && _trustLabel != null)
                _trustLabel.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void Update()
        {
            if (_showLastChange)
                TrackLastChange();
        }

        /// <summary>
        /// Remembers what the last change of the patient was (a new trust value, a new emotion target) and shows it as
        /// an arrow next to Trust and next to the emotion it is about. Also while the HUD is hidden, so the arrows tell
        /// what the last sentence did when the nurse switches to the patient view.
        /// </summary>
        private void TrackLastChange()
        {
            if (_reputation != null)
            {
                var trust = _reputation.Value;
                if (_lastTrust < 0f)
                    _lastTrust = trust;
                else if (Mathf.Abs(trust - _lastTrust) >= 0.5f)
                {
                    TrustDelta = Mathf.RoundToInt(trust - _lastTrust);
                    TrustTrend = TrustDelta > 0 ? 1 : TrustDelta < 0 ? -1 : 0;
                    _lastTrust = trust;
                    ShowTrends();
                }
            }
            if (_holder != null)
            {
                var target = _holder.Target;
                if (!_lastTargetSet)
                {
                    _lastTarget = target;
                    _lastTargetSet = true;
                }
                else if (!Same(target, _lastTarget))
                {
                    var dominant = target.Dominant;
                    if (dominant.HasValue)
                    {
                        var delta = target.Get(dominant.Value) - _lastTarget.Get(dominant.Value);
                        EmotionTrendFor = dominant;
                        EmotionTrend = delta > 0.005f ? 1 : delta < -0.005f ? -1 : 0;
                    }
                    else
                    {
                        EmotionTrendFor = null;
                        EmotionTrend = 0;
                    }
                    _lastTarget = target;
                    ShowTrends();
                }
            }
        }

        private static bool Same(EmotionState a, EmotionState b)
        {
            foreach (Emotion e in Enum.GetValues(typeof(Emotion)))
            {
                if (Mathf.Abs(a.Get(e) - b.Get(e)) > 0.005f)
                    return false;
            }
            return true;
        }

        private string Arrow(TMP_Text text, int trend)
        {
            if (trend == 0)
                return "";
            var color = ColorUtility.ToHtmlStringRGB(trend > 0 ? _upColor : _downColor);
            var glyph = trend > 0 ? '\u25B2' : '\u25BC';
            // tryAddCharacter: the glyph comes from the dynamic fallback font.
            var hasGlyph = text != null && text.font != null && text.font.HasCharacter(glyph, true, true);
            return $"<color=#{color}>{(hasGlyph ? glyph.ToString() : trend > 0 ? "+" : "-")}</color>";
        }

        private void ShowTrends()
        {
            if (_trustLabel != null)
            {
                var delta = TrustDelta > 0 ? $"+{TrustDelta}" : TrustDelta.ToString();
                _trustLabel.text = TrustTrend == 0 ? _trustLabelBase
                    : $"{_trustLabelBase}  {Arrow(_trustLabel, TrustTrend)} <size=70%>{delta}</size>";
            }
            if (_holder != null)
                ShowValues(_holder.Current);
        }

        /// <summary>The arrow in front of an emotion's number (only for the emotion of the last change).</summary>
        private string RowTrend(Row row)
        {
            if (!_showLastChange || EmotionTrendFor != row.Emotion || EmotionTrend == 0)
                return "";
            return $"<size=75%>{Arrow(row.Number, EmotionTrend)}</size> ";
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
            if (!_canvas.enabled)
                return;
            Follow(false);
            UpdateTrust(false);
        }

        /// <summary>
        /// The trust bar follows the reputation smoothly (snap: at once), colored red, amber or green.
        /// </summary>
        private void UpdateTrust(bool snap)
        {
            if (_reputation == null || _trustFill == null)
                return;
            var target = _reputation.Value;
            _trustShown = snap || _trustShown < 0f
                ? target
                : Mathf.SmoothDamp(_trustShown, target, ref _trustVelocity, _trustSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            var size = _trustFill.sizeDelta;
            size.x = Mathf.InverseLerp(PatientReputation.Min, PatientReputation.Max, _trustShown) * _trustBarWidth;
            _trustFill.sizeDelta = size;
            if (_trustNumber != null)
                _trustNumber.SetText("{0}", Mathf.RoundToInt(_trustShown));
            if (_trustFillImage != null)
            {
                _trustFillImage.color = _trustShown < _reputation.LowThreshold ? _trustLowColor
                    : _trustShown >= _reputation.HighThreshold ? _trustHighColor
                    : _trustMidColor;
            }
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
                UpdateTrust(true);
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
                var trend = RowTrend(row);
                if (trend.Length == 0)
                    row.Number.SetText("{0}", Mathf.RoundToInt(value * 100f));
                else
                    row.Number.text = trend + Mathf.RoundToInt(value * 100f);
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
