using System;
using ProCommVR.Atmosphere;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// One place of the hospital where a task of the shift happens: usually a patient room, but it can also be a
    /// corridor (Scenario 2, the angry daughter). The <see cref="ShiftManager"/> assigns a <see cref="ShiftTask"/> to it:
    /// the room sets the patient's mood and door sign, and plays the scenario once the shift starts. The task is
    /// done (<see cref="TaskOutcome.Completed"/>) when the player has made a choice and clicks the continue bubble
    /// ("Finish"). A scenario with a Time Limit also needs a <see cref="TaskCountdown"/>: if it runs out before the
    /// player answers, the task fails (<see cref="TaskOutcome.FailedTimeUp"/>) and the mood turns to Failed Mood.
    /// </summary>
    public class PatientRoom : MonoBehaviour
    {
        [Tooltip("Shown on the door sign and the shift board, e.g. \"Room 1\".")]
        [SerializeField] private string _roomName = "Room";

        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("Holds the mood of the patient in this room.")]
        [SerializeField] private MoodController _moodController;

        [SerializeField] private RoomSign _sign;

        [Tooltip("Label of the bubble that finishes the task after the choice.")]
        [SerializeField] private string _finishLabel = "Finish";

        [Header("Time limit")]
        [Tooltip("Optional. Runs the scenario's Time Limit; needed when the scenario has one.")]
        [SerializeField] private TaskCountdown _countdown;

        [Tooltip("Mood when the task fails because the time ran out: red flashing light and a warning sound.")]
        [SerializeField] private Mood _failedMood = Mood.Alarm;

        private ShiftTask _task;
        private bool _active;

        public string RoomName => _roomName;
        public ShiftTask Task => _task;

        /// <summary>
        /// Unique id of the patient in this room this shift, or null before a task is assigned.
        /// </summary>
        public string PatientId => _task?.PatientId;

        public MoodController MoodController => _moodController;
        public TaskCountdown Countdown => _countdown;

        /// <summary>
        /// How the task in this room ended so far: None while it is still open.
        /// </summary>
        public TaskOutcome Outcome { get; private set; }

        /// <summary>
        /// The choice the player made in this room, or null.
        /// </summary>
        public DialogueOption ChosenOption { get; private set; }

        /// <summary>
        /// Where the player chose to talk, or null (no choice made, or the scenario has none).
        /// </summary>
        public ConversationLocation ChosenLocation { get; private set; }

        /// <summary>
        /// Score of this task: the chosen option plus the chosen place. 0 while the task is not completed.
        /// </summary>
        public int Score => Outcome == TaskOutcome.Completed && ChosenOption != null
            ? ChosenOption.Score + (ChosenLocation?.Score ?? 0)
            : 0;

        /// <summary>
        /// Raised when the task in this room ends: completed or failed (see <see cref="Outcome"/>).
        /// </summary>
        public event Action<PatientRoom> TaskFinished;

        private void OnEnable()
        {
            _presenter.LocationChosen += OnLocationChosen;
            _presenter.OptionChosen += OnOptionChosen;
            _presenter.Continued += OnContinued;
            if (_countdown != null)
                _countdown.Expired += OnCountdownExpired;
        }

        private void OnDisable()
        {
            _presenter.LocationChosen -= OnLocationChosen;
            _presenter.OptionChosen -= OnOptionChosen;
            _presenter.Continued -= OnContinued;
            if (_countdown != null)
                _countdown.Expired -= OnCountdownExpired;
        }

        /// <summary>
        /// Puts the patient in the room: mood and door sign. The conversation starts with <see cref="Begin"/>.
        /// </summary>
        public void Assign(ShiftTask task)
        {
            _task = task;
            ChosenOption = null;
            ChosenLocation = null;
            Outcome = TaskOutcome.None;
            if (_moodController != null)
                _moodController.ApplyMood(task.PatientMood);
            if (_sign != null)
                _sign.Show(_roomName, task.PatientName, task.Scenario != null ? task.Scenario.Title : "");
        }

        public void Begin()
        {
            if (_task == null || _task.Scenario == null)
            {
                Debug.LogWarning($"[ProCommVR] {_roomName} ({PatientId}) has no scenario to play.", this);
                return;
            }
            _active = true;
            _presenter.ContinueLabel = _finishLabel;
            _presenter.Play(_task.Scenario);

            var scenario = _task.Scenario;
            if (scenario.HasTimeLimit)
            {
                if (_countdown != null)
                    _countdown.Arm(scenario.TimeLimit, scenario.TimeLimitLabel);
                else
                    Debug.LogWarning($"[ProCommVR] Scenario '{scenario.Id}' has a time limit, but {_roomName} has no Task Countdown; it plays without one.", this);
            }
        }

        /// <summary>
        /// Stops the conversation, e.g. when the shift ends.
        /// </summary>
        public void Close()
        {
            _active = false;
            _presenter.Clear();
            if (_countdown != null)
                _countdown.Stop();
        }

        private void OnLocationChosen(Scenario scenario, ConversationLocation location)
        {
            if (_active)
                ChosenLocation = location;
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            if (!_active)
                return;
            // Answered in time: the countdown stops, "Finish" only closes the conversation.
            ChosenOption = option;
            if (_countdown != null)
                _countdown.Stop();
        }

        private void OnContinued()
        {
            if (!_active)
                return;
            Finish(TaskOutcome.Completed);
        }

        private void OnCountdownExpired(TaskCountdown countdown)
        {
            if (!_active || ChosenOption != null)
                return;
            _active = false;
            _presenter.Interrupt(_task.Scenario.TimeUpLine);
            if (_moodController != null)
                _moodController.ApplyMood(_failedMood);
            Finish(TaskOutcome.FailedTimeUp);
        }

        private void Finish(TaskOutcome outcome)
        {
            Outcome = outcome;
            if (outcome == TaskOutcome.Completed)
                Close();
            if (_sign != null)
                _sign.SetOutcome(outcome);
            TaskFinished?.Invoke(this);
        }
    }
}
