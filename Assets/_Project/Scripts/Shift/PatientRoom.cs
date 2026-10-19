using System;
using ProCommVR.Atmosphere;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// One patient room of the hospital. The <see cref="ShiftManager"/> assigns a <see cref="ShiftTask"/> to it:
    /// the room sets the patient's mood and door sign, and plays the scenario once the shift starts. The task is
    /// done when the player has made a choice and clicks the continue bubble ("Finish").
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

        private ShiftTask _task;
        private bool _active;

        public string RoomName => _roomName;
        public ShiftTask Task => _task;

        /// <summary>
        /// The choice the player made in this room, or null.
        /// </summary>
        public DialogueOption ChosenOption { get; private set; }

        /// <summary>
        /// Raised when the player finishes the task in this room.
        /// </summary>
        public event Action<PatientRoom> TaskCompleted;

        private void OnEnable()
        {
            _presenter.OptionChosen += OnOptionChosen;
            _presenter.Continued += OnContinued;
        }

        private void OnDisable()
        {
            _presenter.OptionChosen -= OnOptionChosen;
            _presenter.Continued -= OnContinued;
        }

        /// <summary>
        /// Puts the patient in the room: mood and door sign. The conversation starts with <see cref="Begin"/>.
        /// </summary>
        public void Assign(ShiftTask task)
        {
            _task = task;
            ChosenOption = null;
            if (_moodController != null)
                _moodController.ApplyMood(task.PatientMood);
            if (_sign != null)
                _sign.Show(_roomName, task.PatientName, task.Scenario != null ? task.Scenario.Title : "");
        }

        public void Begin()
        {
            if (_task == null || _task.Scenario == null)
            {
                Debug.LogWarning($"[ProCommVR] {_roomName} has no scenario to play.", this);
                return;
            }
            _active = true;
            _presenter.ContinueLabel = _finishLabel;
            _presenter.Play(_task.Scenario);
        }

        /// <summary>
        /// Stops the conversation, e.g. when the shift ends.
        /// </summary>
        public void Close()
        {
            _active = false;
            _presenter.Clear();
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            if (_active)
                ChosenOption = option;
        }

        private void OnContinued()
        {
            if (!_active)
                return;
            Close();
            if (_sign != null)
                _sign.SetCaredFor(true);
            TaskCompleted?.Invoke(this);
        }
    }
}
