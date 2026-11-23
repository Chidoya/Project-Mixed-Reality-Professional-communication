using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The debrief led by the head doctor, instead of a report panel. Runs the steps of the <see cref="DebriefScript"/> in
    /// order (each step a small class, the order is data):
    /// Greeting (already on arrival) -> ReflectionQuestion (four objects on the table: how did he feel?) -> Reveal (the real
    /// emotion glows) -> Timeline (the conversation as objects on the table, trust as the height) -> Feedback (max 3 points,
    /// sticky notes on the whiteboard) -> Memory (a key moment replayed in room 1A) -> Consequence ("Saturday, 3 pm") ->
    /// Board (score stamp on the patient file) -> Choice (A: try again, B: finish).
    /// Started by <see cref="AiDebriefPanel"/> in its Doctor Led mode; ends with Retry or Done, which the panel turns into
    /// its usual Retried / Closed, so PrototypeFlow and AiShift work unchanged. Changes no score.
    /// </summary>
    public class DebriefDirector : MonoBehaviour
    {
        public enum Result { None, Retry, Done }

        [SerializeField] private DebriefScript _script;
        [SerializeField] private DoctorSpeaker _doctor;
        [SerializeField] private DoctorPointer _pointer;

        [Header("Conversation")]
        [SerializeField] private AiNpc _patient;
        [SerializeField] private ConversationRecorder _recorder;
        [SerializeField] private EmotionHolder _holder;

        [Header("Player")]
        [Tooltip("Optional: Jalil's flow (its Teleport is used, its doctor bubble is hidden, the greeting starts on arrival).")]
        [SerializeField] private PrototypeFlow _flow;
        [SerializeField] private XROrigin _origin;
        [SerializeField] private ScreenFade _fade;

        [Header("Places (built by DebriefSetup)")]
        [SerializeField] private Transform _playerTableSpot;
        [SerializeField] private Transform _doctorTableSpot;
        [Tooltip("Middle of the reflection objects on the table (right = left to right for the player).")]
        [SerializeField] private Transform _reflectionRow;
        [Tooltip("Middle of the emotion timeline on the table.")]
        [SerializeField] private Transform _timelineRow;
        [Tooltip("The patient file with the score stamp (forward = away from the player).")]
        [SerializeField] private Transform _fileSpot;
        [Tooltip("Middle of the whiteboard's front (forward = out of the board).")]
        [SerializeField] private Transform _board;
        [Tooltip("Where the doctor's subtitle hangs at the table.")]
        [SerializeField] private Transform _subtitleSpot;
        [Tooltip("Where the two choice bubbles hang (middle, facing away from the player).")]
        [SerializeField] private Transform _choiceSpot;

        [Header("Hidden while the doctor talks")]
        [Tooltip("The flow's doctor bubble: the head doctor greets with her own voice.")]
        [SerializeField] private GameObject _flowBubble;
        [Tooltip("Shown only at the Board step (e.g. the shift report with the score).")]
        [SerializeField] private GameObject[] _hideUntilBoard = Array.Empty<GameObject>();

        [Header("Looks")]
        [SerializeField] private Material _propMaterial;
        [SerializeField] private Material _noteMaterial;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private GameObject _bubblePrefab;
        [SerializeField, Min(0.04f)] private float _reflectionPropSize = 0.13f;
        [SerializeField, Min(0.1f)] private float _reflectionSpacing = 0.34f;

        [Header("Parts")]
        [SerializeField] private EmotionTimeline _timeline;
        [SerializeField] private DebriefBoard _boardVisuals;
        [SerializeField] private MemoryReplay _memory;
        [SerializeField] private ConsequenceCard _consequence;

        [Header("Labels of the choice (empty: from the panel)")]
        [SerializeField] private string _retryLabel = "Try again (A)";
        [SerializeField] private string _doneLabel = "Finish (B)";

        private readonly List<EmotionProp> _reflectionProps = new();
        private readonly List<GameObject> _choiceBubbles = new();
        private readonly Dictionary<GameObject, bool> _heldBack = new();
        private Coroutine _run;
        private Coroutine _greeting;
        private bool _greeted;
        private bool _atTable;
        private bool _boardShown;
        private Action<Result> _onDone;
        private Vector3 _doctorHome;
        private Quaternion _doctorHomeRotation;
        private bool _homeSaved;
        private int _pickedTurn = -1;
        private Result _chosen;

        public bool Running { get; private set; }
        public DebriefStepKind CurrentStep { get; private set; }
        public DebriefData Data { get; private set; }
        public DebriefScript Script => _script;
        public DoctorSpeaker Doctor => _doctor;
        public DoctorPointer Pointer => _pointer;
        public bool HasPicked { get; private set; }
        public Emotion PickedEmotion { get; private set; }
        public EmotionProp RevealedProp { get; private set; }
        public IReadOnlyList<EmotionProp> ReflectionProps => _reflectionProps;
        public EmotionTimeline Timeline => _timeline;
        public DebriefBoard BoardVisuals => _boardVisuals;
        public MemoryReplay Memory => _memory;
        public ConsequenceCard Consequence => _consequence;
        public bool ChoiceOpen { get; private set; }
        public bool Greeted => _greeted;
        public bool AtTable => _atTable;
        public Result LastResult { get; private set; }
        public int FeedbackPoints { get; private set; }
        public Transform PlayerTableSpot => _playerTableSpot;
        public Transform DoctorTableSpot => _doctorTableSpot;
        public IReadOnlyList<GameObject> ChoiceBubbles => _choiceBubbles;

        public event Action<DebriefStepKind> StepStarted;
        public event Action<Result> Finished;

        public string RetryLabel { get => _retryLabel; set => _retryLabel = value; }
        public string DoneLabel { get => _doneLabel; set => _doneLabel = value; }

        private void Awake()
        {
            if (_origin == null)
                _origin = FindAnyObjectByType<XROrigin>();
            if (_fade == null && _origin != null && _origin.Camera != null)
                _fade = _origin.Camera.GetComponentInChildren<ScreenFade>(true);
            if (_timeline != null)
                _timeline.PointPicked += point =>
                {
                    // hovering works as soon as the timeline is there; picking a memory only from the board on
                    if (Running && CurrentStep is DebriefStepKind.Board or DebriefStepKind.Choice)
                        _pickedTurn = point.TurnIndex;
                };
        }

        // ---- Arrival: hide the flow's bubble and greet ----------------------------------------------------------------

        private void LateUpdate()
        {
            if (_flow != null)
            {
                var atDoctor = _flow.Current is PrototypeFlow.Stage.ToDoctor or PrototypeFlow.Stage.Debrief or PrototypeFlow.Stage.Finished;
                if (atDoctor && _flowBubble != null && _flowBubble.activeSelf)
                    _flowBubble.SetActive(false);
                if (_flow.Current == PrototypeFlow.Stage.Talking)
                    _greeted = false;
                if (_flow.Current == PrototypeFlow.Stage.ToDoctor && !_greeted && _greeting == null && PlayerNearDoctor()
                    && (_fade == null || _fade.Alpha < 0.05f))
                    _greeting = StartCoroutine(Greet());
            }
            if (Running && !_boardShown)
                foreach (var go in _hideUntilBoard)
                    if (go != null && go.activeSelf)
                    {
                        _heldBack[go] = true;
                        go.SetActive(false);
                    }
        }

        private bool PlayerNearDoctor()
        {
            var camera = _origin != null ? _origin.Camera : Camera.main;
            if (camera == null || _doctor == null)
                return false;
            var d = camera.transform.position - _doctor.transform.position;
            d.y = 0f;
            return d.magnitude < 3f;
        }

        private IEnumerator Greet()
        {
            _greeted = true;
            yield return _doctor.Say(_script.Line("greeting"));
            _greeting = null;
        }

        // ---- Start, stop ------------------------------------------------------------------------------------------------

        /// <summary>Starts the debrief from the AI server's result and the recorded turns.</summary>
        public void BeginFromAi(AiDebrief debrief, Action<Result> onDone) =>
            Begin(new AiDebriefSource(debrief, _recorder, _holder), onDone);

        /// <summary>Starts the debrief with any source (tests, the button version later).</summary>
        public void Begin(IDebriefSource source, Action<Result> onDone)
        {
            Abort();
            Data = source.Build();
            _onDone = onDone;
            Running = true;
            LastResult = Result.None;
            _boardShown = false;
            _heldBack.Clear();
            HasPicked = false;
            RevealedProp = null;
            FeedbackPoints = 0;
            _pickedTurn = -1;
            _chosen = Result.None;
            if (!_homeSaved && _doctor != null)
            {
                _doctorHome = _doctor.transform.position;
                _doctorHomeRotation = _doctor.transform.rotation;
                _homeSaved = true;
            }
            DebriefInput.ClearFake();
            _run = StartCoroutine(Run());
        }

        /// <summary>Stops at once and cleans up (e.g. Retry or Done was called from outside).</summary>
        public void Abort()
        {
            if (!Running && _run == null)
                return;
            if (_run != null)
                StopCoroutine(_run);
            _run = null;
            if (_memory != null)
                _memory.Restore();
            if (_consequence != null)
                _consequence.Hide();
            Cleanup();
            Running = false;
        }

        private IEnumerator Run()
        {
            if (_greeting != null)
                while (_greeting != null)
                    yield return null;
            foreach (var step in _script.Steps)
            {
                CurrentStep = step;
                StepStarted?.Invoke(step);
                if (step != DebriefStepKind.Greeting && !_atTable)
                    yield return MoveToTable();
                yield return Step(step);
                if (_chosen != Result.None)
                    break;
            }
            if (_chosen == Result.None)
                _chosen = Result.Done; // a script without a Choice step simply ends
            var result = _chosen;
            Cleanup();
            Running = false;
            _run = null;
            LastResult = result;
            Finished?.Invoke(result);
            _onDone?.Invoke(result);
        }

        private IEnumerator Step(DebriefStepKind step) => step switch
        {
            DebriefStepKind.Greeting => GreetingStep(),
            DebriefStepKind.ReflectionQuestion => ReflectionStep(),
            DebriefStepKind.Reveal => RevealStep(),
            DebriefStepKind.Timeline => TimelineStep(),
            DebriefStepKind.Feedback => FeedbackStep(),
            DebriefStepKind.Memory => MemoryStep(),
            DebriefStepKind.Consequence => ConsequenceStep(),
            DebriefStepKind.Board => BoardStep(),
            DebriefStepKind.Choice => ChoiceStep(),
            _ => null,
        };

        // ---- Steps ------------------------------------------------------------------------------------------------------

        private IEnumerator GreetingStep()
        {
            if (_greeted)
                yield break;
            _greeted = true;
            yield return _doctor.Say(_script.Line("greeting"));
        }

        private IEnumerator MoveToTable()
        {
            if (_playerTableSpot == null)
            {
                _atTable = true;
                yield break;
            }
            if (_fade != null)
                yield return _fade.FadeTo(1f, 0.5f);
            TeleportPlayer(_playerTableSpot);
            if (_doctor != null && _doctorTableSpot != null)
                _doctor.transform.SetPositionAndRotation(_doctorTableSpot.position, _doctorTableSpot.rotation);
            if (_doctor != null)
                _doctor.SubtitleAnchor = _subtitleSpot;
            _atTable = true;
            yield return null;
            if (_fade != null)
                yield return _fade.FadeTo(0f, 0.5f);
        }

        private IEnumerator ReflectionStep()
        {
            ClearReflection();
            var emotions = new[] { Emotion.Sadness, Emotion.Anger, Emotion.Fear, Emotion.Joy };
            for (var i = 0; i < emotions.Length; i++)
            {
                var prop = EmotionProp.Create(emotions[i], _reflectionPropSize, _propMaterial, transform, true, _font);
                prop.name = $"Reflection {emotions[i]}";
                var x = (i - 1.5f) * _reflectionSpacing;
                prop.transform.SetPositionAndRotation(_reflectionRow.position + _reflectionRow.right * x,
                    Quaternion.LookRotation(-_reflectionRow.forward, Vector3.up));
                prop.SetLabel(EmotionProp.NameOf(emotions[i]), true);
                prop.Picked += OnReflectionPicked;
                _reflectionProps.Add(prop);
            }
            yield return _doctor.Say(_script.Line("reflection_question"));
            while (!HasPicked)
                yield return null;
            foreach (var prop in _reflectionProps)
                prop.SetInteractable(false);
        }

        private void OnReflectionPicked(EmotionProp prop)
        {
            if (HasPicked || prop.Emotion == null)
                return;
            HasPicked = true;
            PickedEmotion = prop.Emotion.Value;
            prop.SetGlow(true, Color.white);
        }

        /// <summary>Picks one of the four reflection objects (as laser + trigger does). For tests.</summary>
        public bool PickReflection(Emotion emotion)
        {
            var prop = _reflectionProps.FirstOrDefault(p => p.Emotion == emotion);
            if (prop == null)
                return false;
            prop.Pick();
            return true;
        }

        private IEnumerator RevealStep()
        {
            if (!HasPicked)
                yield break;
            var real = Data.Final;
            foreach (var prop in _reflectionProps)
                prop.SetGlow(false, Color.white);
            if (real.HasValue)
            {
                RevealedProp = _reflectionProps.FirstOrDefault(p => p.Emotion == real);
                if (RevealedProp != null)
                {
                    RevealedProp.SetGlow(true, EmotionProp.ColorOf(real));
                    _pointer?.PointAt(RevealedProp.transform);
                }
            }
            yield return _doctor.Say(_script.Reveal(PickedEmotion, real));
            _pointer?.Stop();
            yield return Seconds(0.6f);
            ClearReflection();
        }

        private IEnumerator TimelineStep()
        {
            if (_timeline == null || Data.Timeline.Count == 0)
                yield break;
            _timeline.Build(Data.Timeline, _timelineRow, _propMaterial, _font);
            var grow = StartCoroutine(_timeline.GrowIn());
            yield return _doctor.Say(_script.Line("timeline_intro"));
            yield return grow;
        }

        private IEnumerator FeedbackStep()
        {
            var points = new List<(string intro, DebriefLine line, string note, Color color, Transform target, DebriefItem item, bool critical)>();
            var max = Mathf.Clamp(_script.MaxPoints, 1, 3);
            var lowest = Data.Lowest();
            if (Data.HasCritical)
            {
                var key = $"{_script.ScenarioPrefix}_critical_{Data.Critical.Label}";
                points.Add(("critical_intro", Voice(key, Data.Critical.Feedback), _script.CriticalNote, _script.CriticalColor,
                    _timeline != null ? _timeline.PropFor(Data.Critical.Turn)?.transform : null, CriticalItem(), true));
            }
            if (lowest != null && points.Count < max)
                points.Add(("low_intro", Voice(FeedbackKey(lowest), lowest.Feedback), _script.NoteFor(lowest.Id, false) ?? lowest.Criterion,
                    _script.ImproveColor, _timeline != null ? _timeline.PropFor(lowest.EvidenceTurn)?.transform : null, lowest, false));
            var highest = Data.Highest(lowest);
            if (highest != null && points.Count < max)
                points.Add(("high_intro", Voice(FeedbackKey(highest), highest.Feedback), _script.NoteFor(highest.Id, true) ?? highest.Criterion,
                    _script.WellDoneColor, _timeline != null ? _timeline.PropFor(highest.EvidenceTurn)?.transform : null, highest, false));

            var betterSaid = false;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                yield return _doctor.Say(_script.Line(p.intro));
                if (p.target != null)
                    _pointer?.PointAt(p.target);
                yield return _doctor.Say(p.line);
                // the better phrase for the critical error or the lowest item, once
                string better = null;
                if (!betterSaid && p.item != null && (p.critical || p.item.Score < p.item.Max) && !string.IsNullOrEmpty(p.item.BetterPhrase))
                    better = p.item.BetterPhrase;
                var note = _boardVisuals != null && _board != null
                    ? _boardVisuals.AddNote(_board, i, points.Count, p.note, better, p.color, _noteMaterial, _font)
                    : null;
                if (note != null)
                    _pointer?.PointAt(note);
                FeedbackPoints++;
                if (better != null)
                {
                    betterSaid = true;
                    yield return _doctor.Say(_script.Line("better_intro"));
                    yield return _doctor.Say(BetterLine(p.item));
                }
                else
                    yield return Seconds(0.8f);
                _pointer?.Stop();
            }
        }

        private IEnumerator MemoryStep()
        {
            if (_memory == null)
                yield break;
            var moment = Data.MostImportant();
            if (moment == null)
                yield break;
            yield return PlayMemory(moment.TurnIndex);
        }

        private IEnumerator PlayMemory(int turnIndex)
        {
            var moment = Data.Moments.FirstOrDefault(m => m.TurnIndex == turnIndex);
            var point = Data.PointAt(turnIndex);
            var turn = moment?.Turn ?? point?.Turn;
            var studentText = turn?.StudentText ?? point?.StudentText ?? Data.Critical?.Quote ?? "";
            if (_timeline != null)
                _pointer?.PointAt(_timeline.PropFor(turnIndex)?.transform);
            yield return _doctor.Say(_script.Line("memory_intro"));
            _pointer?.Stop();
            _doctor.SubtitleAnchor = null;
            var item = ItemForTurn(turnIndex);
            yield return _memory.Play(turn, studentText, _doctor, _script, item != null ? BetterLine(item) : null,
                _origin, _fade, _flow, () =>
                {
                    TeleportPlayer(_playerTableSpot);
                    if (_doctorTableSpot != null)
                        _doctor.transform.SetPositionAndRotation(_doctorTableSpot.position, _doctorTableSpot.rotation);
                });
            _doctor.SubtitleAnchor = _subtitleSpot;
            _pickedTurn = -1;
        }

        private IEnumerator ConsequenceStep()
        {
            if (_consequence == null || _board == null)
                yield break;
            yield return _consequence.Show(Data.Outcome(_script.GoodFraction), _script, _board, _doctor, _font);
        }

        private IEnumerator BoardStep()
        {
            _boardShown = true;
            foreach (var pair in _heldBack)
                if (pair.Key != null && pair.Value)
                    pair.Key.SetActive(true);
            _heldBack.Clear();
            if (_boardVisuals != null && _fileSpot != null)
                _boardVisuals.ShowStamp(_fileSpot, Data.TotalScore, Data.MaxScore, _propMaterial, _font);
            var outcome = Data.Outcome(_script.GoodFraction);
            yield return _doctor.Say(_script.Line(outcome switch
            {
                DebriefOutcome.Good => "closing_good",
                DebriefOutcome.Critical => "closing_critical",
                _ => "closing_mixed",
            }));
            if (_timeline != null && _timeline.IsBuilt && _memory != null)
            {
                _timeline.SetInteractable(true);
                yield return _doctor.Say(_script.Line("pick_moment"));
            }
        }

        private IEnumerator ChoiceStep()
        {
            ShowChoice();
            _timeline?.SetInteractable(true);
            yield return _doctor.Say(_script.Line("choice"));
            ChoiceOpen = true;
            DebriefInput.ClearFake();
            while (_chosen == Result.None)
            {
                if (DebriefInput.A)
                    _chosen = Result.Retry;
                else if (DebriefInput.B)
                    _chosen = Result.Done;
                else if (_pickedTurn >= 0 && _memory != null)
                {
                    // a moment on the table: replay it, then the choice again
                    ChoiceOpen = false;
                    HideChoice();
                    yield return PlayMemory(_pickedTurn);
                    ShowChoice();
                    _timeline?.SetInteractable(true);
                    ChoiceOpen = true;
                }
                yield return null;
            }
            ChoiceOpen = false;
            HideChoice();
            yield return _doctor.Say(_script.Line(_chosen == Result.Retry ? "retry" : "done"));
        }

        /// <summary>Chooses as A (Try again) or B (Finish) does. For tests and the bubbles.</summary>
        public void Choose(bool retry)
        {
            if (ChoiceOpen && _chosen == Result.None)
                _chosen = retry ? Result.Retry : Result.Done;
        }

        /// <summary>Asks for the memory of a turn (as picking it on the table does). For tests.</summary>
        public void RequestMemory(int turnIndex) => _pickedTurn = turnIndex;

        // ---- Helpers ----------------------------------------------------------------------------------------------------

        private void ShowChoice()
        {
            HideChoice();
            if (_bubblePrefab == null || _choiceSpot == null)
                return;
            for (var i = 0; i < 2; i++)
            {
                var retry = i == 0;
                var go = Instantiate(_bubblePrefab, transform);
                go.name = retry ? "Choice Try Again" : "Choice Finish";
                go.transform.SetPositionAndRotation(_choiceSpot.position + _choiceSpot.right * (retry ? -0.22f : 0.22f), _choiceSpot.rotation);
                go.transform.localScale *= 0.6f;
                var bubble = go.GetComponent<DialogueBubble>();
                bubble.SetText(retry ? _retryLabel : _doneLabel);
                bubble.SetClickable(true);
                bubble.Clicked += () => Choose(retry);
                _choiceBubbles.Add(go);
            }
        }

        private void HideChoice()
        {
            foreach (var go in _choiceBubbles)
                if (go != null)
                    Destroy(go);
            _choiceBubbles.Clear();
        }

        private DebriefLine Voice(string key, string text) =>
            _script.Voices != null ? _script.Voices.For(key, text) : new DebriefLine(key, text);

        private string FeedbackKey(DebriefItem item) => $"{_script.ScenarioPrefix}_item{item.Id}_score{item.Score}";

        private DebriefLine BetterLine(DebriefItem item) => Voice($"{_script.ScenarioPrefix}_item{item.Id}_better", item.BetterPhrase);

        /// <summary>The rubric item that the critical error broke (its quote is from the critical turn), else the lowest.</summary>
        private DebriefItem CriticalItem()
        {
            if (!Data.HasCritical)
                return null;
            return Data.Items.FirstOrDefault(i => i.Score == 0 && i.EvidenceTurn == Data.Critical.Turn) ?? Data.Lowest();
        }

        /// <summary>The item whose better phrase fits this turn: its own weak item, the critical item, else the lowest.</summary>
        private DebriefItem ItemForTurn(int turnIndex)
        {
            if (Data.HasCritical && Data.Critical.Turn == turnIndex)
                return CriticalItem();
            return Data.Items.FirstOrDefault(i => i.EvidenceTurn == turnIndex && i.Score < i.Max) ?? Data.Lowest();
        }

        private void ClearReflection()
        {
            foreach (var prop in _reflectionProps)
                if (prop != null)
                    Destroy(prop.gameObject);
            _reflectionProps.Clear();
        }

        private void Cleanup()
        {
            _doctor?.Stop();
            _pointer?.Stop();
            ClearReflection();
            HideChoice();
            ChoiceOpen = false;
            _timeline?.Clear();
            _boardVisuals?.Clear();
            foreach (var pair in _heldBack)
                if (pair.Key != null && pair.Value)
                    pair.Key.SetActive(true);
            _heldBack.Clear();
            if (_doctor != null)
            {
                _doctor.SubtitleAnchor = null;
                if (_homeSaved)
                    _doctor.transform.SetPositionAndRotation(_doctorHome, _doctorHomeRotation);
            }
            _atTable = false;
        }

        private void TeleportPlayer(Transform spot)
        {
            if (spot == null)
                return;
            if (_flow != null)
                _flow.Teleport(spot);
            else
                TeleportRig(_origin, spot);
        }

        /// <summary>
        /// The same as PrototypeFlow.Teleport (used when there is no flow, e.g. the test scene): the player's head above the
        /// spot, looking along its forward, the rig's floor on the spot's floor.
        /// </summary>
        public static void TeleportRig(XROrigin origin, Transform spot)
        {
            if (origin == null || spot == null)
                return;
            var rig = origin.transform;
            var controller = rig.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;
            var eyeHeight = origin.Camera.transform.position.y - rig.position.y;
            var forward = Vector3.ProjectOnPlane(spot.forward, Vector3.up);
            origin.MatchOriginUpCameraForward(Vector3.up, forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward);
            origin.MoveCameraToWorldLocation(spot.position + Vector3.up * eyeHeight);
            var position = rig.position;
            position.y = spot.position.y;
            rig.position = position;
            Physics.SyncTransforms();
            if (controller != null)
                controller.enabled = wasEnabled;
        }

        private static IEnumerator Seconds(float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
