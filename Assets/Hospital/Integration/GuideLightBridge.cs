using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Atmosphere;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using UnityEngine;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// Vloerstroken volgen de shift. Voor de shift: uit. ShiftStarted: de strook van elke taak gaat aan in de kleur van
    /// de mood van die kamer (Neutral en Happy = Calm groen; Tense, Somber en Distressed = Difficult paars; Alarm = rood).
    /// MoodChanged past de kleur aan. TaskCompleted: groen pulsen en daarna uit. TaskFailed: rood pulsen en daarna uit.
    /// De eerste taak die nog open staat (in de volgorde van het plan) pulst zacht.
    /// </summary>
    public class GuideLightBridge : MonoBehaviour
    {
        [Serializable]
        public class RoomGuide
        {
            public PatientRoom Room;
            public FloorGuideLight Guide;
        }

        [SerializeField] private ShiftManager _shift;
        [SerializeField] private List<RoomGuide> _rooms = new List<RoomGuide>();
        [Tooltip("Alle stroken; die zonder taak blijven uit tijdens de shift.")]
        [SerializeField] private FloorGuideLight[] _allGuides = Array.Empty<FloorGuideLight>();
        [SerializeField] private float _activePulseInterval = 3f;
        [SerializeField] private float _activePulseBoost = 0.8f;
        [Tooltip("Hoe lang de strook na het einde van een taak in zijn uitkomstkleur blijft voor ze uitgaat.")]
        [SerializeField] private float _resultHold = 2.5f;

        private readonly HashSet<PatientRoom> _finished = new HashSet<PatientRoom>();
        private bool _running;
        private float _nextActivePulse;

        public void Configure(ShiftManager shift, List<RoomGuide> rooms, FloorGuideLight[] allGuides)
        {
            _shift = shift;
            _rooms = rooms;
            _allGuides = allGuides;
        }

        public static Color ColorFor(Mood? mood)
        {
            switch (mood)
            {
                case Mood.Tense:
                case Mood.Somber:
                case Mood.Distressed:
                    return FloorGuideLight.Difficult;
                case Mood.Alarm:
                    return FloorGuideLight.Failed;
                default:
                    return FloorGuideLight.Calm;
            }
        }

        private void OnEnable()
        {
            if (_shift != null)
            {
                _shift.ShiftStarted += OnShiftStarted;
                _shift.TaskCompleted += OnTaskCompleted;
                _shift.TaskFailed += OnTaskFailed;
                _shift.ShiftEnded += OnShiftEnded;
            }
            foreach (var mood in Moods())
                mood.MoodChanged += OnMoodChanged;
        }

        private void OnDisable()
        {
            if (_shift != null)
            {
                _shift.ShiftStarted -= OnShiftStarted;
                _shift.TaskCompleted -= OnTaskCompleted;
                _shift.TaskFailed -= OnTaskFailed;
                _shift.ShiftEnded -= OnShiftEnded;
            }
            foreach (var mood in Moods())
                mood.MoodChanged -= OnMoodChanged;
        }

        private IEnumerable<MoodController> Moods() =>
            _rooms.Where(r => r.Room != null && r.Room.MoodController != null).Select(r => r.Room.MoodController).Distinct();

        private void Start()
        {
            if (_shift == null || _shift.State == ShiftState.Briefing)
                foreach (var guide in _allGuides.Where(g => g != null))
                    guide.SetColor(Color.black);
        }

        private void Update()
        {
            if (!_running || Time.time < _nextActivePulse)
                return;
            _nextActivePulse = Time.time + _activePulseInterval;
            var active = _shift.AssignedRooms.FirstOrDefault(r => r != null && r.Outcome == TaskOutcome.None && !_finished.Contains(r));
            GuideOf(active)?.Pulse(_activePulseBoost);
        }

        private void OnShiftStarted()
        {
            _running = true;
            _nextActivePulse = Time.time + 1f;
            foreach (var entry in _rooms.Where(r => r.Room != null && r.Guide != null))
                Show(entry);
        }

        private void OnMoodChanged(MoodController mood)
        {
            if (!_running)
                return;
            foreach (var entry in _rooms.Where(r => r.Room != null && r.Room.MoodController == mood && r.Guide != null))
                Show(entry);
        }

        private void OnTaskCompleted(PatientRoom room) => Finish(room, FloorGuideLight.Calm);
        private void OnTaskFailed(PatientRoom room) => Finish(room, FloorGuideLight.Failed);

        private void OnShiftEnded(ShiftEndReason reason)
        {
            _running = false;
            foreach (var entry in _rooms.Where(r => r.Guide != null && !_finished.Contains(r.Room)))
                StartCoroutine(TurnOffAfter(entry.Guide, _resultHold));
        }

        private void Show(RoomGuide entry)
        {
            if (_finished.Contains(entry.Room))
                return;
            var mood = entry.Room.MoodController != null ? entry.Room.MoodController.CurrentMood : entry.Room.Task?.PatientMood;
            entry.Guide.SetColor(ColorFor(mood));
        }

        private void Finish(PatientRoom room, Color color)
        {
            var guide = GuideOf(room);
            if (guide == null || !_finished.Add(room))
                return;
            guide.SetColor(color);
            guide.Pulse();
            StartCoroutine(TurnOffAfter(guide, _resultHold));
        }

        private FloorGuideLight GuideOf(PatientRoom room) =>
            room == null ? null : _rooms.FirstOrDefault(r => r.Room == room)?.Guide;

        private static IEnumerator TurnOffAfter(FloorGuideLight guide, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            guide.SetColor(Color.black);
        }
    }
}
