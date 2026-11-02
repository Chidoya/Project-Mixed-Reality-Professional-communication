using ProCommVR.Scenarios;
using ProCommVR.Shift;
using UnityEngine;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// Verpleegpost in de lobby volgt de shift van Jalil:
    /// ShiftStarted: de lamp van de Badge_Reader wordt groen en het klembord verschijnt op Anchor_Clipboard.
    /// ShiftEnded: de Care_Tablet gaat rechtop in het Tablet_Dock, en Jalils eindscherm komt op Anchor_EndScreen
    /// zodra de speler in de lobby is (ResultsScreen zet zich zelf voor de speler; enkel de positie wordt nadien verzet).
    /// </summary>
    public class StaffStationBridge : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Care_Tablet (0,25 x 0,17 m): rechtop in het dock is de rotatie van de prefab-root precies de identiteit
        private static readonly Vector3 TabletInDock = new Vector3(0f, 0.087f, 0.004f);

        [SerializeField] private ShiftManager _shift;
        [SerializeField] private Renderer _badgeLamp;
        [SerializeField] private Color _lampIdle = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color _lampActive = new Color(0.1f, 0.9f, 0.2f);
        [SerializeField] private Transform _clipboard;
        [SerializeField] private Transform _clipboardAnchor;
        [SerializeField] private Transform _tablet;
        [SerializeField] private Transform _tabletDockAnchor;
        [SerializeField] private ResultsScreen _results;
        [SerializeField] private Transform _endScreenAnchor;
        [Tooltip("De speler is in de lobby als hij binnen deze horizontale afstand van Anchor_EndScreen staat (en op dezelfde verdieping).")]
        [SerializeField] private float _lobbyRadius = 9f;

        private MaterialPropertyBlock _block;
        private bool _ended;
        private bool _screenMoved;

        public void Configure(ShiftManager shift, Renderer badgeLamp, Transform clipboard, Transform clipboardAnchor,
            Transform tablet, Transform tabletDockAnchor, ResultsScreen results, Transform endScreenAnchor)
        {
            _shift = shift;
            _badgeLamp = badgeLamp;
            _clipboard = clipboard;
            _clipboardAnchor = clipboardAnchor;
            _tablet = tablet;
            _tabletDockAnchor = tabletDockAnchor;
            _results = results;
            _endScreenAnchor = endScreenAnchor;
        }

        private void OnEnable()
        {
            if (_shift == null)
                return;
            _shift.ShiftStarted += OnShiftStarted;
            _shift.ShiftEnded += OnShiftEnded;
        }

        private void OnDisable()
        {
            if (_shift == null)
                return;
            _shift.ShiftStarted -= OnShiftStarted;
            _shift.ShiftEnded -= OnShiftEnded;
        }

        private void Start()
        {
            SetLamp(_lampIdle);
            if (_clipboard != null && (_shift == null || _shift.State == ShiftState.Briefing))
                _clipboard.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_ended && !_screenMoved)
                TryMoveEndScreen();
        }

        private void OnShiftStarted()
        {
            SetLamp(_lampActive);
            if (_clipboard == null)
                return;
            if (_clipboardAnchor != null)
                _clipboard.position = _clipboardAnchor.position + Vector3.down * 0.01f;
            _clipboard.gameObject.SetActive(true);
        }

        private void OnShiftEnded(ShiftEndReason reason)
        {
            SetLamp(_lampIdle);
            if (_tablet != null && _tabletDockAnchor != null)
                _tablet.SetPositionAndRotation(_tabletDockAnchor.TransformPoint(TabletInDock), Quaternion.identity);
            _ended = true;
            TryMoveEndScreen();
        }

        /// <summary>Verzet het eindscherm naar de muur van de post, maar enkel als de speler in de lobby staat.</summary>
        private void TryMoveEndScreen()
        {
            if (_results == null || _endScreenAnchor == null || !_results.gameObject.activeInHierarchy)
                return;
            var player = Camera.main;
            if (player == null)
                return;
            var offset = player.transform.position - _endScreenAnchor.position;
            if (Mathf.Abs(offset.y) > 2.5f || new Vector2(offset.x, offset.z).magnitude > _lobbyRadius)
                return;
            _results.transform.SetPositionAndRotation(_endScreenAnchor.position, _endScreenAnchor.rotation);
            _screenMoved = true;
        }

        private void SetLamp(Color color)
        {
            if (_badgeLamp == null)
                return;
            _block ??= new MaterialPropertyBlock();
            _badgeLamp.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 1.5f);
            _badgeLamp.SetPropertyBlock(_block);
        }
    }
}
