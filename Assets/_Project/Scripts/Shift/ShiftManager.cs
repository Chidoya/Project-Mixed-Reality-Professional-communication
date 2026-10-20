using System;
using System.Collections.Generic;
using System.Text;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Shift
{
    public enum ShiftState
    {
        Briefing,
        Running,
        Ended
    }

    public enum ShiftEndReason
    {
        None,
        AllTasksDone,
        TimeUp
    }

    /// <summary>
    /// The game loop of one shift. In the briefing room the tasks of the <see cref="ShiftPlan"/> are assigned to the
    /// patient rooms (task 1 to room 1, ...). <see cref="StartShift"/> starts the conversations and the timer.
    /// The shift ends when every task is done or the timer reaches zero, whichever comes first. Then the results
    /// screen appears and the game stops: its button closes the game (no next shift yet).
    /// </summary>
    public class ShiftManager : MonoBehaviour
    {
        [SerializeField] private ShiftPlan _plan;

        [Tooltip("The patient rooms, in the order the tasks are assigned.")]
        [SerializeField] private PatientRoom[] _rooms = Array.Empty<PatientRoom>();

        [Tooltip("Length of the shift in seconds. Test value for now.")]
        [SerializeField, Min(1f)] private float _shiftDuration = 600f;

        [SerializeField] private ResultsScreen _resultsScreen;

        private readonly List<PatientRoom> _assignedRooms = new List<PatientRoom>();
        private readonly Dictionary<string, PatientRoom> _roomsByPatient = new Dictionary<string, PatientRoom>();
        private readonly HashSet<string> _completedPatients = new HashSet<string>();

        public ShiftState State { get; private set; } = ShiftState.Briefing;
        public ShiftEndReason EndReason { get; private set; }
        public float ShiftDuration => _shiftDuration;
        public float TimeRemaining { get; private set; }

        /// <summary>
        /// The rooms with a patient this shift, in task order.
        /// </summary>
        public IReadOnlyList<PatientRoom> AssignedRooms => _assignedRooms;

        public int CompletedCount => _completedPatients.Count;

        public event Action ShiftStarted;
        public event Action<PatientRoom> TaskCompleted;
        public event Action<ShiftEndReason> ShiftEnded;

        private void Start()
        {
            TimeRemaining = _shiftDuration;
            if (_resultsScreen != null)
            {
                _resultsScreen.Hide();
                _resultsScreen.RestartRequested += QuitGame;
            }
            AssignTasks();
        }

        private void OnDestroy()
        {
            if (_resultsScreen != null)
                _resultsScreen.RestartRequested -= QuitGame;
            foreach (var room in _assignedRooms)
            {
                if (room != null)
                    room.TaskCompleted -= OnTaskCompleted;
            }
        }

        private void Update()
        {
            if (State != ShiftState.Running)
                return;

            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f)
                EndShift(ShiftEndReason.TimeUp);
        }

        public bool IsCompleted(PatientRoom room)
        {
            return room != null && room.PatientId != null && _completedPatients.Contains(room.PatientId);
        }

        public bool IsCompleted(string patientId)
        {
            return patientId != null && _completedPatients.Contains(patientId);
        }

        /// <summary>
        /// The room of a patient this shift, or null.
        /// </summary>
        public PatientRoom FindRoom(string patientId)
        {
            return patientId != null && _roomsByPatient.TryGetValue(patientId, out var room) ? room : null;
        }

        /// <summary>
        /// Changes the mood of one patient (door sign, room light and sound of that room follow).
        /// </summary>
        public bool SetPatientMood(string patientId, Mood mood)
        {
            var room = FindRoom(patientId);
            if (room == null || room.MoodController == null)
            {
                Debug.LogWarning($"[ProCommVR] No patient '{patientId}' with a mood this shift.", this);
                return false;
            }
            room.MoodController.ApplyMood(mood);
            return true;
        }

        /// <summary>
        /// Starts the conversations in all assigned rooms and the timer. Only works during the briefing.
        /// </summary>
        public void StartShift()
        {
            if (State != ShiftState.Briefing)
                return;
            if (_assignedRooms.Count == 0)
            {
                Debug.LogWarning("[ProCommVR] The shift has no tasks; check the Shift Plan and the rooms.", this);
                return;
            }

            State = ShiftState.Running;
            TimeRemaining = _shiftDuration;
            foreach (var room in _assignedRooms)
                room.Begin();
            Debug.Log($"[ProCommVR] Shift started: {_assignedRooms.Count} patients, {_shiftDuration:F0} s.", this);
            ShiftStarted?.Invoke();
        }

        private void AssignTasks()
        {
            if (_plan == null)
            {
                Debug.LogWarning("[ProCommVR] No Shift Plan assigned to the ShiftManager.", this);
                return;
            }

            var tasks = _plan.Tasks;
            if (tasks.Count > _rooms.Length)
                Debug.LogWarning($"[ProCommVR] The shift plan has {tasks.Count} tasks but there are only {_rooms.Length} rooms; the extra tasks are skipped.", this);

            for (var i = 0; i < tasks.Count && i < _rooms.Length; i++)
            {
                var room = _rooms[i];
                var problem = room == null ? $"Task {i + 1} has no room." : _plan.Validate(i);
                if (problem != null)
                {
                    Debug.LogWarning($"[ProCommVR] {problem} Skipped.", this);
                    continue;
                }
                room.Assign(tasks[i]);
                room.TaskCompleted += OnTaskCompleted;
                _assignedRooms.Add(room);
                _roomsByPatient.Add(tasks[i].PatientId, room);
            }
        }

        private void OnTaskCompleted(PatientRoom room)
        {
            if (State != ShiftState.Running || !_completedPatients.Add(room.PatientId))
                return;

            Debug.Log($"[ProCommVR] {room.PatientId} ({room.RoomName}) cared for.", this);
            TaskCompleted?.Invoke(room);
            if (_completedPatients.Count >= _assignedRooms.Count)
                EndShift(ShiftEndReason.AllTasksDone);
        }

        private void EndShift(ShiftEndReason reason)
        {
            State = ShiftState.Ended;
            EndReason = reason;
            foreach (var room in _assignedRooms)
                room.Close();

            Debug.Log($"[ProCommVR] Shift ended ({reason}): {_completedPatients.Count} / {_assignedRooms.Count} patients cared for.", this);
            if (_resultsScreen != null)
                _resultsScreen.ShowText(BuildSubtitle(), BuildReport());
            ShiftEnded?.Invoke(reason);
        }

        private string BuildSubtitle()
        {
            var total = 0;
            var best = 0;
            foreach (var room in _assignedRooms)
            {
                if (!IsCompleted(room) || room.ChosenOption == null)
                    continue;
                total += room.ChosenOption.Score;
                best += room.Task.Scenario.BestScore;
            }
            var reason = EndReason == ShiftEndReason.TimeUp ? "Time is up" : "All patients cared for";
            return $"{reason}. Patients cared for: {_completedPatients.Count} / {_assignedRooms.Count}. Score: {total} / {best}";
        }

        /// <summary>
        /// Per room: the patient, the score and the feedback on the choice, or that the patient was not cared for.
        /// </summary>
        private string BuildReport()
        {
            var text = new StringBuilder();
            foreach (var room in _assignedRooms)
            {
                text.Append("<b>").Append(room.RoomName).Append(" - ").Append(room.Task.PatientName).Append("</b>  ");
                if (IsCompleted(room) && room.ChosenOption != null)
                {
                    text.Append(room.ChosenOption.Score).Append(" / ").Append(room.Task.Scenario.BestScore).Append('\n');
                    text.Append("<i>").Append(room.ChosenOption.Feedback).Append("</i>\n\n");
                }
                else
                {
                    text.Append("not cared for\n\n");
                }
            }
            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// End of the game: there is no next shift yet.
        /// </summary>
        private void QuitGame()
        {
            Debug.Log("[ProCommVR] Closing the game after the shift.", this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
