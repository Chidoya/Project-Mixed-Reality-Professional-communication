using System;
using System.Collections.Generic;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// One patient the player has to care for during a shift: a unique id, the scenario that plays in the room and
    /// the patient's mood (door sign color, room light and sound).
    /// </summary>
    [Serializable]
    public class ShiftTask
    {
        [Tooltip("Unique within the plan, e.g. \"patient-01\". Keeps mood, progress and results of each patient apart, also when patients share a name or scenario.")]
        [SerializeField] private string _patientId;

        [SerializeField] private Scenario _scenario;

        [Tooltip("Name on the door sign and the shift board. Leave empty to use the NPC name of the scenario.")]
        [SerializeField] private string _patientName;

        [Tooltip("Mood of the patient: the color of the door sign and the room light.")]
        [SerializeField] private Mood _patientMood = Mood.Happy;

        public string PatientId => _patientId;
        public Scenario Scenario => _scenario;
        public string PatientName => string.IsNullOrWhiteSpace(_patientName) && _scenario != null ? _scenario.NpcName : _patientName;
        public Mood PatientMood => _patientMood;
    }

    /// <summary>
    /// The patients of one shift, as data. Task 1 goes to the first room of the ShiftManager, task 2 to the
    /// second, and so on. Create one via Create > ProCommVR > Shift Plan; no code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewShiftPlan", menuName = "ProCommVR/Shift Plan")]
    public class ShiftPlan : ScriptableObject
    {
        [SerializeField] private ShiftTask[] _tasks = Array.Empty<ShiftTask>();

        public IReadOnlyList<ShiftTask> Tasks => _tasks;

        /// <summary>
        /// Why a task cannot be used (no id, duplicate id, no scenario), or null when it is fine.
        /// </summary>
        public string Validate(int index)
        {
            var task = _tasks[index];
            if (task == null || string.IsNullOrWhiteSpace(task.PatientId))
                return $"Task {index + 1} has no Patient Id.";
            for (var i = 0; i < index; i++)
            {
                if (_tasks[i] != null && _tasks[i].PatientId == task.PatientId)
                    return $"Task {index + 1} has the same Patient Id as task {i + 1} ('{task.PatientId}').";
            }
            return task.Scenario == null ? $"Task {index + 1} ('{task.PatientId}') has no scenario." : null;
        }

        private void OnValidate()
        {
            for (var i = 0; i < _tasks.Length; i++)
            {
                var problem = Validate(i);
                if (problem != null)
                    Debug.LogWarning($"[ProCommVR] Shift plan '{name}': {problem}", this);
            }
        }
    }
}
