using System;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Holds the current <see cref="Mood"/> of one patient, room or scene, and the <see cref="MoodProfile"/>
    /// (color, light, music) that belongs to it. Data only: it does not change anything in the scene itself.
    /// Readers such as <see cref="MoodAtmosphere"/>, <see cref="MoodRoomLight"/> and the door sign listen to
    /// <see cref="MoodChanged"/>. The mood is set by code (<see cref="ApplyMood"/>), by
    /// <see cref="ScenarioPresenter.ScenarioStarted"/> if a presenter is assigned, or by Initial Mood.
    /// </summary>
    public class MoodController : MonoBehaviour
    {
        [Tooltip("Optional. Follows the mood of every scenario this presenter starts.")]
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("One profile per mood. A mood without a profile leaves the current mood unchanged.")]
        [SerializeField] private MoodProfile[] _profiles;

        [Tooltip("Apply Initial Mood when the scene starts.")]
        [SerializeField] private bool _applyInitialMood;
        [SerializeField] private Mood _initialMood = Mood.Neutral;

        public Mood? CurrentMood { get; private set; }

        /// <summary>
        /// Profile of the current mood, or null while no mood has been applied.
        /// </summary>
        public MoodProfile CurrentProfile { get; private set; }

        /// <summary>
        /// Color of the current mood, white while no mood has been applied.
        /// </summary>
        public Color CurrentColor => CurrentProfile != null ? CurrentProfile.Color : Color.white;

        public event Action<MoodController> MoodChanged;

        private void Awake()
        {
            if (_applyInitialMood && CurrentMood == null)
                ApplyMood(_initialMood);
        }

        private void OnEnable()
        {
            if (_presenter != null)
                _presenter.ScenarioStarted += OnScenarioStarted;
        }

        private void OnDisable()
        {
            if (_presenter != null)
                _presenter.ScenarioStarted -= OnScenarioStarted;
        }

        public void ApplyMood(Mood mood)
        {
            if (CurrentMood == mood)
                return;

            var profile = FindProfile(mood);
            if (profile == null)
            {
                Debug.LogWarning($"[ProCommVR] No mood profile for '{mood}'; mood unchanged.", this);
                return;
            }
            CurrentMood = mood;
            CurrentProfile = profile;
            MoodChanged?.Invoke(this);
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            ApplyMood(scenario.Mood);
        }

        private MoodProfile FindProfile(Mood mood)
        {
            foreach (var profile in _profiles)
            {
                if (profile != null && profile.Mood == mood)
                    return profile;
            }
            return null;
        }
    }
}
