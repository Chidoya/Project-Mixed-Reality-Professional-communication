using System;
using System.Text;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Testing;
using TMPro;
using UnityEngine;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// The shift around the AI conversation of Prototype1, kept minimal:
    /// <list type="number">
    /// <item>Lobby: the clipboard shows the task (Waiting). Nothing runs until the player starts the shift at the reception
    /// desk (<see cref="AiShiftStartButton"/>).</item>
    /// <item>Start: the task is In progress, the door sign and the floor plan point to the room.</item>
    /// <item>The conversation and the doctor's debrief are <see cref="PrototypeFlow"/> (unchanged).</item>
    /// <item>After the debrief the task is Done (or Failed, with the reason) and a short shift report shows its score.
    /// Try again (the debrief's A) starts a new conversation, the task is In progress again; Finish shift (B) shows
    /// the end screen with the total score.</item>
    /// </list>
    /// The tasks are data (<see cref="Tasks"/> in the inspector): one now, more later without code. Only the first task is
    /// played by the flow (one patient in this prototype).
    /// </summary>
    public class AiShift : MonoBehaviour
    {
        public enum TaskStatus { Waiting, InProgress, Done, Failed }

        public enum ShiftStage { Lobby, Running, Report, Finished }

        [Serializable]
        public class AiShiftTask
        {
            [Tooltip("The AI scenario (Tools/AiServer/scenarios/<id>.json).")]
            public string ScenarioId = "disappointed_patient";
            public string Room = "1A";
            [Tooltip("As on the clipboard, e.g. \"Room 1A, Mr. X\".")]
            public string Title = "Room 1A, Mr. X";
            [Tooltip("One line: what the nurse should achieve.")]
            public string Goal = "Help him find a little courage, without promises about discharge.";

            [NonSerialized] public TaskStatus Status;
            [NonSerialized] public string Reason = "";
            [NonSerialized] public int Score;
            [NonSerialized] public int MaxScore;
            [NonSerialized] public int Attempts;
        }

        [SerializeField] private PrototypeFlow _flow;
        [SerializeField] private AiDebriefPanel _debrief;
        [SerializeField] private DevClipboard _clipboard;
        [SerializeField] private EndingCard _endingCard;
        [Tooltip("Black fade of the camera, for the end screen.")]
        [SerializeField] private Pov.ScreenFade _fade;

        [Header("Tasks (data)")]
        [SerializeField] private AiShiftTask[] _tasks = { new AiShiftTask() };

        [Header("Shown only after the start (the door sign, the marker on the floor plan)")]
        [SerializeField] private GameObject[] _showWhenRunning = Array.Empty<GameObject>();
        [Tooltip("Shown only before the start (e.g. \"start your shift at the reception desk\").")]
        [SerializeField] private GameObject[] _showBeforeStart = Array.Empty<GameObject>();

        [Header("Shift report (next to the doctor)")]
        [SerializeField] private TMP_Text _report;

        [Header("Texts")]
        [SerializeField] private string _finishedTitle = "SHIFT FINISHED";
        [TextArea, SerializeField] private string _resolvedReason = "He agreed to try a small step.";
        [TextArea, SerializeField] private string _stoppedReason = "You ended the conversation yourself.";
        [TextArea, SerializeField] private string _escalatedReason = "He did not want to talk to you any more.";
        [TextArea, SerializeField] private string _criticalReason = "Critical error";

        private bool _recorded;

        public ShiftStage Stage { get; private set; } = ShiftStage.Lobby;
        public bool IsRunning => Stage != ShiftStage.Lobby && Stage != ShiftStage.Finished;
        public int TaskCount => _tasks.Length;
        public AiShiftTask Task(int index) => _tasks[index];
        public string ReportText => _report != null ? _report.text : "";
        public int TotalScore { get; private set; }
        public int TotalMax { get; private set; }

        public event Action<ShiftStage> StageChanged;

        private void Awake()
        {
            if (_flow == null)
                _flow = FindAnyObjectByType<PrototypeFlow>();
            foreach (var task in _tasks)
                task.Status = TaskStatus.Waiting;
            SetShown(_showWhenRunning, false);
            SetShown(_showBeforeStart, true);
            if (_report != null)
                _report.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_flow != null)
                _flow.StageChanged += OnFlowStage;
            if (_debrief != null)
                _debrief.Closed += OnDebriefClosed;
        }

        private void OnDisable()
        {
            if (_flow != null)
                _flow.StageChanged -= OnFlowStage;
            if (_debrief != null)
                _debrief.Closed -= OnDebriefClosed;
        }

        private void Start() => ShowTasks();

        /// <summary>Starts the shift (the button at the reception desk). Before this nothing runs.</summary>
        public void StartShift()
        {
            if (Stage != ShiftStage.Lobby || _tasks.Length == 0)
                return;
            _tasks[0].Status = TaskStatus.InProgress;
            SetShown(_showWhenRunning, true);
            SetShown(_showBeforeStart, false);
            if (_flow != null)
                _flow.AllowConversation = true;
            SetStage(ShiftStage.Running);
            ShowTasks();
            Debug.Log($"[ProCommVR] Shift started: task 1, {_tasks[0].Title}.", this);
        }

        private void Update()
        {
            // After the debrief: record the task once, then the report with the two choices.
            if (Stage == ShiftStage.Running && !_recorded && _debrief != null && _flow != null
                && _flow.Current == PrototypeFlow.Stage.Debrief
                && (_debrief.State == AiDebriefPanel.PanelState.Ready || _debrief.State == AiDebriefPanel.PanelState.Failed))
                Record(_debrief.Result);
        }

        private void Record(AiDebrief result)
        {
            _recorded = true;
            var task = _tasks[0];
            var reason = _flow.EndReason;
            task.Attempts++;
            task.Score = result != null && !result.Failed ? result.total_score : 0;
            task.MaxScore = result != null && !result.Failed ? result.max_score : 0;
            var failed = reason == "critical_error" || reason == "escalated";
            task.Status = failed ? TaskStatus.Failed : TaskStatus.Done;
            task.Reason = reason switch
            {
                "resolved" => _resolvedReason,
                "stopped" => _stoppedReason,
                "escalated" => _escalatedReason,
                "critical_error" => $"{_criticalReason}: {_flow.CriticalTitle}",
                _ => reason ?? "",
            };
            SetStage(ShiftStage.Report);
            ShowTasks();
            ShowReport();
        }

        private void OnFlowStage(PrototypeFlow.Stage stage)
        {
            // Try again: a new conversation, the task is in progress again.
            if (stage == PrototypeFlow.Stage.Talking && Stage == ShiftStage.Report)
            {
                _tasks[0].Status = TaskStatus.InProgress;
                _recorded = false;
                if (_report != null)
                    _report.gameObject.SetActive(false);
                SetStage(ShiftStage.Running);
                ShowTasks();
            }
        }

        /// <summary>Finish shift (the debrief's B): the end screen with the total score.</summary>
        private void OnDebriefClosed()
        {
            if (Stage != ShiftStage.Report)
                return;
            TotalScore = TotalMax = 0;
            var done = 0;
            foreach (var task in _tasks)
            {
                TotalScore += task.Score;
                TotalMax += task.MaxScore;
                if (task.Status == TaskStatus.Done)
                    done++;
            }
            if (_report != null)
                _report.gameObject.SetActive(false);
            SetStage(ShiftStage.Finished);
            ShowTasks();
            if (_fade != null)
                StartCoroutine(_fade.FadeTo(1f, 0.6f));
            if (_endingCard != null)
                _endingCard.Show(_finishedTitle, new Color(0.45f, 0.85f, 0.55f), $"Total score: {TotalScore} / {TotalMax}",
                    $"Tasks done: {done} of {_tasks.Length}.\n{TaskLines(false)}\nThank you for your shift.");
            Debug.Log($"[ProCommVR] Shift finished: {TotalScore}/{TotalMax}, {done}/{_tasks.Length} tasks done.", this);
        }

        private void ShowReport()
        {
            if (_report == null)
                return;
            _report.text = $"<b>SHIFT REPORT</b>\n{TaskLines(true)}\n<b>A</b>: Try again    <b>B</b>: Finish shift";
            _report.gameObject.SetActive(true);
        }

        /// <summary>The tasks on the clipboard (Tasks page).</summary>
        private void ShowTasks()
        {
            if (_clipboard == null)
                return;
            var text = new StringBuilder();
            text.Append(Stage == ShiftStage.Lobby
                ? "<b>Start your shift</b> at the computer on the reception desk (point and pull the trigger).\n\n"
                : Stage == ShiftStage.Finished ? "<b>Shift finished.</b>\n\n" : "");
            for (var i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                text.Append($"<b>Task {i + 1}: {task.Title}</b>\n");
                text.Append($"Status: {StatusText(task)}\n");
                text.Append($"Goal: {task.Goal}\n");
                if (!string.IsNullOrEmpty(task.Reason) && task.Status is TaskStatus.Done or TaskStatus.Failed)
                    text.Append($"<i>{task.Reason}</i>\n");
                text.Append('\n');
            }
            _clipboard.SetTasksText(text.ToString().TrimEnd());
        }

        private string TaskLines(bool withReason)
        {
            var text = new StringBuilder();
            for (var i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                text.Append($"Task {i + 1}, {task.Title}: {StatusText(task)}");
                if (task.MaxScore > 0)
                    text.Append($", score {task.Score} / {task.MaxScore}");
                if (withReason && !string.IsNullOrEmpty(task.Reason))
                    text.Append($"\n<size=80%>{task.Reason}</size>");
                if (i < _tasks.Length - 1)
                    text.Append('\n');
            }
            return text.ToString();
        }

        private static string StatusText(AiShiftTask task) => task.Status switch
        {
            TaskStatus.Waiting => "<color=#7a7a7a>Waiting</color>",
            TaskStatus.InProgress => "<color=#c77700>In progress</color>",
            TaskStatus.Done => "<color=#2e8b3e>Done</color>",
            _ => "<color=#c0392b>Failed</color>",
        };

        public string StatusName(int index) => _tasks[index].Status switch
        {
            TaskStatus.InProgress => "In progress",
            _ => _tasks[index].Status.ToString(),
        };

        private void SetStage(ShiftStage stage)
        {
            if (Stage == stage)
                return;
            Stage = stage;
            StageChanged?.Invoke(stage);
        }

        private static void SetShown(GameObject[] objects, bool shown)
        {
            foreach (var go in objects)
            {
                if (go != null)
                    go.SetActive(shown);
            }
        }
    }
}
