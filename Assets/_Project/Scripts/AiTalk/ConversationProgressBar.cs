using ProCommVR.Hospital.AiTalk;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// An orange bar at the bottom of the player's view during the AI conversation: how far the conversation is on its
    /// way to an end. It fills when the patient calms down (trust up, tension down, toward the "resolved" end of the
    /// scenario) and it fills faster when the player makes him angry (toward the "escalated" end), so the player knows
    /// roughly how much more there is to say.
    /// <para>
    /// The targets come from the scenario file, sent by the AI server in the opening (meta.progress: the phases, the
    /// condition of the last phase, "escalation" and the minimum turns), so they never have to be copied here. The
    /// inspector values are only used when the server sends no meta (an older server). The start is the patient's state
    /// after his opening line.
    /// </para>
    /// The UI is made at runtime: a small world-space canvas in front of the camera, drawn on top (overlay materials).
    /// </summary>
    public class ConversationProgressBar : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;
        [SerializeField] private Camera _camera;
        [SerializeField] private Material _imageMaterial;
        [SerializeField] private Material _textMaterial;

        [Header("Targets when the server sends no meta.progress (older server)")]
        [Tooltip("Resolved: trust at least this (condition trust_gte of the last phase).")]
        [SerializeField, Range(0f, 100f)] private float _resolveTrust = 66f;
        [Tooltip("Resolved: tension below this (condition tension_lt of the last phase).")]
        [SerializeField, Range(0f, 100f)] private float _resolveTension = 36f;
        [Tooltip("Escalated: tension at least this (escalation tension_gte).")]
        [SerializeField, Range(0f, 100f)] private float _escalateTension = 92f;
        [Tooltip("Escalated: trust at most this (escalation trust_lte).")]
        [SerializeField, Range(0f, 100f)] private float _escalateTrust = 5f;
        [Tooltip("The phases of the scenario in order (the last one before END last): reaching a phase fills the bar at least to its share.")]
        [SerializeField] private string[] _phases = { "B1_withdrawn", "B2_opening_up", "B3_considering" };
        [Tooltip("Shown at most this far: the last step is a concrete proposal, the bar cannot know when it comes.")]
        [SerializeField, Range(0.5f, 1f)] private float _maxBeforeEnd = 0.92f;

        [Header("Look")]
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Vector3 _offset = new Vector3(0f, -0.3f, 1.0f);
        [SerializeField] private Vector2 _size = new Vector2(0.42f, 0.022f);
        [SerializeField, Min(0.01f)] private float _smoothSeconds = 0.6f;

        private GameObject _root;
        private RectTransform _fill;
        private TextMeshProUGUI _label;
        private bool _hasStart;
        private float _startTrust;
        private float _startTension;
        private float _shown;
        private float _velocity;
        private float _hideAt = -1f;

        /// <summary>0..1: how far the conversation is to its end (what the bar is heading to).</summary>
        public float Progress { get; private set; }
        public float Shown => _shown;
        public bool IsVisible => _root != null && _root.activeSelf;
        public Color Color => _color;

        private void Awake()
        {
            if (_npc == null)
                _npc = FindAnyObjectByType<AiNpc>();
            if (_camera == null)
                _camera = Camera.main;
            Build();
            _root.SetActive(false);
        }

        private void Update()
        {
            if (_npc == null || _root == null)
                return;
            var running = _npc.ConversationSeconds > 0f;
            if (!running)
            {
                _hasStart = false;
                _shown = Progress = 0f;
                _hideAt = -1f;
                _root.SetActive(false);
                return;
            }
            // The first values after the opening line are the start.
            if (!_hasStart && (_npc.Trust > 0f || _npc.Anger > 0f))
            {
                _hasStart = true;
                _startTrust = _npc.Trust;
                _startTension = _npc.Anger;
            }
            if (!_hasStart)
                return;
            ReadTargets(_npc.Meta?.progress);

            if (_npc.HasEnded)
            {
                Progress = 1f;
                if (_hideAt < 0f)
                    _hideAt = Time.time + 3f;
            }
            else
            {
                // Both trust and tension count (the average), so every step forward shows; a reached phase is a floor.
                var calm = (Mathf.InverseLerp(_startTrust, _resolveTrust, _npc.Trust)
                            + Mathf.InverseLerp(_startTension, _resolveTension, _npc.Anger)) / 2f;
                var phase = _npc.LastTurn != null ? System.Array.IndexOf(_phases, _npc.LastTurn.phase) : -1;
                if (phase > 0)
                    calm = Mathf.Max(calm, phase / (float)_phases.Length);
                var angry = Mathf.Max(Mathf.InverseLerp(_startTension, _escalateTension, _npc.Anger),
                    Mathf.InverseLerp(_startTrust, _escalateTrust, _npc.Trust));
                // A scenario with a minimum number of turns cannot be further than its share of those turns.
                if (_minTurns > 0)
                    calm = Mathf.Min(calm, (_npc.LastTurn != null ? _npc.LastTurn.turn : 0) / (float)_minTurns);
                Progress = Mathf.Min(Mathf.Max(calm, angry), _maxBeforeEnd);
            }
            _root.SetActive(_hideAt < 0f || Time.time < _hideAt);
            _shown = Mathf.SmoothDamp(_shown, Progress, ref _velocity, _smoothSeconds);
            _fill.anchorMax = new Vector2(_shown, 1f);
            _label.text = _npc.HasEnded ? "Conversation finished" : "Conversation";
        }

        private int _minTurns;

        /// <summary>The limits of the conversation from the scenario (meta.progress), when the server sent them.</summary>
        private void ReadTargets(AiProgressMeta progress)
        {
            if (progress == null || !progress.Present)
                return;
            _phases = progress.phases;
            _resolveTrust = progress.resolve_trust;
            _resolveTension = progress.resolve_tension;
            _escalateTension = progress.escalate_tension;
            _escalateTrust = progress.escalate_trust;
            _minTurns = progress.min_turns_total;
        }

        /// <summary>The limits in use (for tests): resolve trust, resolve tension, escalate tension, escalate trust.</summary>
        public Vector4 Targets => new Vector4(_resolveTrust, _resolveTension, _escalateTension, _escalateTrust);

        private void Build()
        {
            const float pixels = 1000f; // canvas units per meter
            _root = new GameObject("Conversation Progress (orange)", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            if (_camera != null)
                rect.SetParent(_camera.transform, false);
            rect.localPosition = _offset;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one / pixels;
            rect.sizeDelta = new Vector2(_size.x, _size.y + 0.034f) * pixels;
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var track = Image("Track", rect, new Color(0f, 0f, 0f, 0.55f));
            var trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.sizeDelta = new Vector2(0f, _size.y * pixels);

            var fill = Image("Fill", trackRect, _color);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = new Vector2(3f, 3f);
            _fill.offsetMax = new Vector2(-3f, -3f);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(0f, 26f);
            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.text = "Conversation";
            _label.fontSize = 22f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = _color;
            _label.raycastTarget = false;
            if (_textMaterial != null)
                _label.fontSharedMaterial = _textMaterial;
        }

        private Image Image(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (_imageMaterial != null)
                image.material = _imageMaterial;
            return image;
        }
    }
}
