using System;
using System.Collections.Generic;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// One patient the player has to care for during a shift: the scenario that plays in the room and the
    /// patient's mood (door sign color and room light).
    /// </summary>
    [Serializable]
    public class ShiftTask
    {
        [SerializeField] private Scenario _scenario;

        [Tooltip("Name on the door sign and the shift board. Leave empty to use the NPC name of the scenario.")]
        [SerializeField] private string _patientName;

        [Tooltip("Mood of the patient: the color of the door sign and the room light.")]
        [SerializeField] private Mood _patientMood = Mood.Happy;

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
    }
}
