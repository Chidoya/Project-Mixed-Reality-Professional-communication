using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Nabespreking na het gesprek: world-space paneel op leesafstand (1 px = 1 mm).
    /// Opent als het gesprek eindigt (resolved, tijd op, of de testtoets E), vraagt de beoordeling aan de server
    /// en toont de samenvatting, de lijn van de boosheid met de 3 belangrijke momenten, de rubric-items
    /// (score als bolletjes, onzeker in het grijs, citaat en betere zin) en de knoppen Opnieuw proberen en Klaar.
    /// Knoppen: A / B op de rechtercontroller (Quest, geen XR-ray nodig), R / Enter of de muis in de Editor.
    /// </summary>
    public class AiDebriefPanel : MonoBehaviour
    {
        public enum PanelState { Hidden, Waiting, Ready, Failed }

        private const float PxToM = 0.001f; // 1 px = 1 mm
        private const float Width = 1200f;
        private const float Height = 960f;

        [SerializeField] private AiNpc _npc;
        [Tooltip("Afstand van het paneel tot de camera bij het openen (m); altijd minstens 0,35 m vóór het personage.")]
        [SerializeField] private float _distance = 1.1f;
        [SerializeField] private float _minimumDistance = 0.7f;
        [Tooltip("Hoe ver het midden van het paneel onder ooghoogte hangt (m).")]
        [SerializeField] private float _belowEyes = 0.12f;
        [Tooltip("Alleen voor testen: E beëindigt het gesprek en opent de nabespreking.")]
        [SerializeField] private bool _testKeyE = true;

        private static readonly Color Background = new(0.09f, 0.11f, 0.14f, 1f);
        private static readonly Color TextMain = new(0.93f, 0.95f, 0.97f);
        private static readonly Color TextDim = new(0.66f, 0.7f, 0.76f);
        private static readonly Color Better = new(0.55f, 0.82f, 1f);
        private static readonly Color Good = new(0.36f, 0.8f, 0.45f);
        private static readonly Color Mid = new(1f, 0.72f, 0.25f);
        private static readonly Color Bad = new(0.95f, 0.35f, 0.3f);
        private static readonly Color Grey = new(0.55f, 0.57f, 0.6f);
        private static readonly Color Empty = new(1f, 1f, 1f, 0.12f);

        private Canvas _canvas;
        private RectTransform _content;
        private TextMeshProUGUI _waitingText;
        private Sprite _circle;
        private GameObject _background;
        private float _waitStarted;

        public PanelState State { get; private set; } = PanelState.Hidden;
        public AiDebrief Result { get; private set; }

        /// <summary>Na Klaar.</summary>
        public event Action Closed;

        // Knoppen zonder XR-ray: A / B op de rechtercontroller (Quest), R / Enter op het toetsenbord (Editor)
        public const string RetryLabel = "Opnieuw proberen (A / R)";
        public const string DoneLabel = "Klaar (B / Enter)";
        private InputAction _retryAction;
        private InputAction _doneAction;
        private InputAction _endAction;

        private void Start()
        {
            // Elk personage kan het gesprek beëindigen (scenariokiezer); wisselen sluit het paneel
            AiNpc.AnyConversationEnded += OnAnyEnded;
            AiNpc.ActiveChanged += OnActiveChanged;
        }

        private void OnAnyEnded(AiNpc npc, string reason)
        {
            _npc = npc;
            _ = Open();
        }

        private void OnActiveChanged(AiNpc npc)
        {
            if (npc != null)
                _npc = npc;
            if (State != PanelState.Hidden && npc != null)
                Hide();
        }

        private void OnEnable()
        {
            _retryAction = new InputAction("DebriefRetry", InputActionType.Button);
            _retryAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");   // A
            _retryAction.AddBinding("<Keyboard>/r");
            _doneAction = new InputAction("DebriefDone", InputActionType.Button);
            _doneAction.AddBinding("<XRController>{RightHand}/{SecondaryButton}");  // B
            _doneAction.AddBinding("<Keyboard>/enter");
            _doneAction.AddBinding("<Keyboard>/numpadEnter");
            _endAction = new InputAction("DebriefTestEnd", InputActionType.Button, "<Keyboard>/e");
            _retryAction.performed += OnRetryPressed;
            _doneAction.performed += OnDonePressed;
            _endAction.performed += OnEndPressed;
            _retryAction.Enable();
            _doneAction.Enable();
            _endAction.Enable();
        }

        private void OnDisable()
        {
            foreach (var action in new[] { _retryAction, _doneAction, _endAction })
                action?.Dispose();
            _retryAction = _doneAction = _endAction = null;
        }

        private void OnDestroy()
        {
            AiNpc.AnyConversationEnded -= OnAnyEnded;
            AiNpc.ActiveChanged -= OnActiveChanged;
        }

        private bool ButtonsActive => State is PanelState.Ready or PanelState.Failed;

        private void OnRetryPressed(InputAction.CallbackContext _)
        {
            if (ButtonsActive)
                Retry();
        }

        private void OnDonePressed(InputAction.CallbackContext _)
        {
            if (ButtonsActive)
                Done();
        }

        private void OnEndPressed(InputAction.CallbackContext _)
        {
            var active = AiNpc.Active != null ? AiNpc.Active : _npc;
            if (_testKeyE && active != null && !active.HasEnded && !active.Busy)
                active.EndNow("stopped");
        }

        private void Update()
        {
            if (State == PanelState.Waiting && _waitingText != null)
            {
                var dots = new string('.', 1 + (int)((Time.time - _waitStarted) * 2f) % 3);
                _waitingText.text = "Even nadenken over je gesprek" + dots;
            }
        }


        /// <summary>Opent het paneel, zet Sarah in Idle en vraagt de beoordeling op.</summary>
        public async Awaitable Open()
        {
            if (State == PanelState.Waiting)
                return;
            _npc.SetIdle();
            EnsureCanvas();
            PlaceInFront();
            Clear();
            State = PanelState.Waiting;
            _waitStarted = Time.time;
            // Tijdens het wachten alleen een smal vak onderaan, zodat Sarah (in Idle) zichtbaar blijft
            _background.SetActive(false);
            var box = new GameObject("WaitingBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(_content, false);
            TopLeft((RectTransform)box.transform, new Vector2(Width / 2f - 330f, -Height + 150f), new Vector2(660f, 80f));
            box.GetComponent<Image>().color = Background;
            _waitingText = Text((RectTransform)box.transform, "Even nadenken over je gesprek...", 28, TextMain, new Vector2(0f, -18f),
                new Vector2(660f, 50f), TextAlignmentOptions.Center);
            _canvas.gameObject.SetActive(true);

            var result = await _npc.RequestDebrief();
            if (this == null)
                return;
            Result = result;
            Clear();
            _background.SetActive(true);
            if (result.Failed)
            {
                State = PanelState.Failed;
                Text(_content, "De nabespreking lukte niet.\n<size=70%>" + result.error + "</size>", 26, Mid,
                    new Vector2(40f, -300f), new Vector2(Width - 80f, 200f), TextAlignmentOptions.Center);
                Buttons();
                return;
            }
            Build(result);
            State = PanelState.Ready;
        }

        public void Retry()
        {
            Hide();
            _ = _npc.Restart();
        }

        public void Done()
        {
            Hide();
            Closed?.Invoke();
        }

        private void Hide()
        {
            State = PanelState.Hidden;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // --- Opbouw -----------------------------------------------------------------------------

        private void EnsureCanvas()
        {
            if (_canvas != null)
                return;
            var go = new GameObject("AiDebriefCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = Camera.main;
            go.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one * PxToM;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(rt, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = Background;
            _background = bg;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(rt, false);
            _content = (RectTransform)content.transform;
            Stretch(_content);

            _circle = MakeCircle();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        private void PlaceInFront()
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            // Vóór het personage, zodat het niet door het paneel steekt
            var distance = _distance;
            if (_npc != null)
            {
                var toNpc = Vector3.Dot(_npc.transform.position - cam.transform.position, forward);
                if (toNpc > 0f)
                    distance = Mathf.Max(_minimumDistance, Mathf.Min(distance, toNpc - 0.35f));
            }
            // Midden in de kijkrichting (ook als de speler naar een zittend of liggend personage kijkt),
            // maar nooit meer dan 30 graden omhoog of omlaag; het paneel staat rechtop naar de speler gedraaid
            var view = cam.transform.forward;
            var pitch = Mathf.Clamp(Vector3.SignedAngle(forward, view, Vector3.Cross(Vector3.up, forward)), -30f, 30f);
            var dir = Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, forward)) * forward;
            var t = _canvas.transform;
            t.position = cam.transform.position + dir * distance + Vector3.down * (pitch > 5f ? 0f : _belowEyes);
            t.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(t.position - cam.transform.position, Vector3.up), Vector3.up);
        }

        private void Clear()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _waitingText = null;
        }

        private void Build(AiDebrief d)
        {
            // Kop: titel, score en einde
            var ending = d.end_reason switch
            {
                "resolved" => "Gesprek geslaagd",
                "time_up" => "Tijd op",
                "critical_error" => "<color=#f2594d>Kritieke fout</color>",
                _ => "Gesprek gestopt",
            };
            Text(_content, "Nabespreking", 34, TextMain, new Vector2(40f, -24f), new Vector2(500f, 44f));
            Text(_content, $"{ending}   <b>{d.total_score}/{d.max_score}</b>", 24, TextDim, new Vector2(Width - 540f, -30f),
                new Vector2(500f, 40f), TextAlignmentOptions.Right);
            if (d.critical != null && d.critical.Present)
            {
                // De kritieke fout bovenaan: rode balk met wat er gezegd werd; de uitleg staat in de samenvatting
                var bar = new GameObject("CriticalBar", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(_content, false);
                TopLeft((RectTransform)bar.transform, new Vector2(30f, -70f), new Vector2(Width - 60f, 30f));
                bar.GetComponent<Image>().color = new Color(0.62f, 0.13f, 0.12f);
                Text((RectTransform)bar.transform, $"<b>Kritieke fout (beurt {d.critical.turn}): {d.critical.title}</b>   \"{d.critical.quote}\"",
                    17, TextMain, new Vector2(10f, -4f), new Vector2(Width - 80f, 24f));
                Text(_content, d.summary, 16, TextMain, new Vector2(40f, -104f), new Vector2(Width - 80f, 42f));
            }
            else
            {
                Text(_content, d.summary, 19, TextMain, new Vector2(40f, -74f), new Vector2(Width - 80f, 64f));
            }

            Graph(d, new Vector2(40f, -146f), new Vector2(Width - 80f, 150f));

            // Items in twee kolommen
            var items = d.items ?? Array.Empty<AiDebriefItem>();
            var perColumn = Mathf.CeilToInt(items.Length / 2f);
            const float top = -366f;
            const float rowHeight = 86f;
            var columnWidth = (Width - 100f) / 2f;
            for (var i = 0; i < items.Length; i++)
            {
                var col = i / perColumn;
                var row = i % perColumn;
                Item(items[i], new Vector2(40f + col * (columnWidth + 20f), top - row * rowHeight), columnWidth);
            }
            Buttons();
        }

        private void Item(AiDebriefItem item, Vector2 pos, float width)
        {
            var colour = item.uncertain ? Grey : item.score == 2 ? Good : item.score == 1 ? Mid : Bad;
            var title = $"{item.id}. {item.criterion_nl}" + (item.uncertain ? "  <color=#8c9199><size=80%>onzeker</size></color>" : "");
            Text(_content, title, 16, TextMain, pos, new Vector2(width - 60f, 22f));
            for (var k = 0; k < 2; k++)
                Dot(_content, pos + new Vector2(width - 40f + k * 22f, -4f), 15f, k < item.score ? colour : Empty);
            Text(_content, item.feedback, 13, TextDim, pos + new Vector2(0f, -20f), new Vector2(width, 30f));
            var quote = string.IsNullOrEmpty(item.evidence_quote) ? "<color=#5d636b>(geen citaat)</color>" : $"<i>\"{item.evidence_quote}\"</i>";
            Text(_content, quote, 13, TextMain, pos + new Vector2(0f, -50f), new Vector2(width, 18f));
            Text(_content, "Beter: " + item.better_phrase, 13, Better, pos + new Vector2(0f, -66f), new Vector2(width, 18f));
        }

        /// <summary>Boosheid per beurt als lijn (LineRenderer), met bolletjes op de belangrijke momenten.</summary>
        private void Graph(AiDebrief d, Vector2 pos, Vector2 size)
        {
            var area = new GameObject("AngerGraph", typeof(RectTransform), typeof(Image));
            area.transform.SetParent(_content, false);
            var rt = (RectTransform)area.transform;
            TopLeft(rt, pos, size);
            area.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);

            // Grenzen van de banden (30, 55, 80)
            foreach (var limit in new[] { 30, 55, 80 })
            {
                var line = new GameObject($"Band_{limit}", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(rt, false);
                TopLeft((RectTransform)line.transform, new Vector2(0f, -(1f - limit / 100f) * size.y), new Vector2(size.x, 1f));
                line.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            }
            Text(rt, string.IsNullOrEmpty(d.tension_name_nl) ? "spanning" : d.tension_name_nl, 12, TextDim,
                new Vector2(6f, -4f), new Vector2(200f, 16f));

            var curve = d.anger_curve ?? Array.Empty<int>();
            if (curve.Length == 0)
                return;
            Vector2 Point(int turn) // lokaal in het grafiekvlak, oorsprong linksboven
            {
                var x = curve.Length == 1 ? 0f : turn / (float)(curve.Length - 1) * (size.x - 40f) + 20f;
                var y = -(1f - Mathf.Clamp(curve[turn], 0, 100) / 100f) * (size.y - 20f) - 10f;
                return new Vector2(x, y);
            }

            var lineGo = new GameObject("AngerLine", typeof(RectTransform));
            lineGo.transform.SetParent(rt, false);
            var lineRt = (RectTransform)lineGo.transform;
            lineRt.anchorMin = lineRt.anchorMax = lineRt.pivot = new Vector2(0f, 1f);
            lineRt.anchoredPosition = Vector2.zero;
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = curve.Length;
            for (var i = 0; i < curve.Length; i++)
            {
                var p = Point(i);
                lr.SetPosition(i, new Vector3(p.x, p.y, -2f)); // 2 mm voor het paneel
            }
            lr.widthMultiplier = 0.006f; // 6 mm (wereldeenheden)
            lr.numCapVertices = 3;
            lr.numCornerVertices = 3;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 0.42f, 0.36f);
            lr.sortingOrder = _canvas.sortingOrder + 1; // na het (ondoorzichtige) paneel tekenen

            // Elke beurt een klein puntje, de belangrijke momenten een groot bolletje met een label
            for (var i = 0; i < curve.Length; i++)
                Dot(rt, Point(i) + new Vector2(-4f, 4f), 8f, new Color(1f, 1f, 1f, 0.5f));
            var moments = d.key_moments ?? Array.Empty<AiKeyMoment>();
            for (var m = 0; m < moments.Length; m++)
            {
                var k = moments[m];
                if (k.turn_index < 0 || k.turn_index >= curve.Length)
                    continue;
                var p = Point(k.turn_index);
                // Marker naast de lijn (die na het paneel getekend wordt), met een steeltje naar het punt:
                // boven het punt in de onderste helft van de grafiek, eronder in de bovenste helft
                var up = p.y < -size.y / 2f ? 1f : -1f;
                Dot(rt, p + new Vector2(-4f, 4f), 8f, Mid);
                var stem = new GameObject("Stem", typeof(RectTransform), typeof(Image));
                stem.transform.SetParent(rt, false);
                TopLeft((RectTransform)stem.transform, p + new Vector2(-1f, up > 0f ? 24f : -4f), new Vector2(2f, 20f));
                stem.GetComponent<Image>().color = Mid;
                var center = p + new Vector2(0f, up * 33f);
                Dot(rt, center + new Vector2(-11f, 11f), 22f, Mid);
                Text(rt, (m + 1).ToString(), 14, Background, center + new Vector2(-11f, 9f), new Vector2(22f, 22f), TextAlignmentOptions.Center);
            }
            // Legende met de momenten rechts onder de grafiek
            var legend = new List<string>();
            for (var m = 0; m < moments.Length; m++)
                legend.Add($"<color=#ffb840><b>{m + 1}</b></color> beurt {moments[m].turn_index}: {moments[m].what_happened} <color=#8c9199>({moments[m].effect})</color>");
            Text(_content, string.Join("\n", legend), 13, TextDim, pos + new Vector2(0f, -size.y - 4f), new Vector2(size.x, 56f));
        }

        private void Buttons()
        {
            Button(RetryLabel, new Vector2(Width / 2f - 300f, -Height + 66f), Retry);
            Button(DoneLabel, new Vector2(Width / 2f + 20f, -Height + 66f), Done);
        }

        // --- Kleine UI-hulp ------------------------------------------------------------------------

        private void Button(string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            TopLeft((RectTransform)go.transform, pos, new Vector2(280f, 54f));
            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.42f, 0.62f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            Text((RectTransform)go.transform, label, 20, TextMain, Vector2.zero, new Vector2(280f, 54f), TextAlignmentOptions.Center);
        }

        private TextMeshProUGUI Text(RectTransform parent, string text, float size, Color colour, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TopLeft((RectTransform)go.transform, pos, box);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text ?? "";
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void Dot(RectTransform parent, Vector2 pos, float diameter, Color colour)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            TopLeft((RectTransform)go.transform, pos, new Vector2(diameter, diameter));
            var image = go.GetComponent<Image>();
            image.sprite = _circle;
            image.color = colour;
            image.raycastTarget = false;
        }

        private static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeCircle()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                var a = Mathf.Clamp01(n / 2f - d);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
