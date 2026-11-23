using System;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// The wall clock in Scenario 1's room. In the nurse view it shows the real time (WallClock). In the patient view it
    /// shows the time of the granddaughter's party getting closer (from 2:40 pm): with Sadness the second hand crawls,
    /// with Fear and Anger it races and ticks loudly. Back in the nurse view: the real time again.
    /// </summary>
    public class StoryClock : MonoBehaviour
    {
        [SerializeField] private WallClock _clock;
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private PatientWorldController _world;
        [SerializeField] private ScenarioOneStoryProps _settings;
        [SerializeField] private AudioSource _tickSource;
        [SerializeField] private AudioClip _tick;

        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;
        private double _seconds;
        private long _lastWholeSecond;
        private bool _active;
        private float _scale = 1f;

        /// <summary>How fast the clock runs now (1 = real time).</summary>
        public float TimeScale => _active ? _scale : 1f;
        public bool ShowsStoryTime => _active;
        public DateTime StoryTime => DateTime.Today.AddSeconds(_seconds);
        public int Ticks { get; private set; }

        private void Awake()
        {
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
            if (_settings == null)
                _settings = ScriptableObject.CreateInstance<ScenarioOneStoryProps>();
        }

        private void OnEnable()
        {
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
        }

        private void OnDisable()
        {
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            SetActive(false);
        }

        private void OnPovChanged(PovMode mode)
        {
            SetActive(mode == PovMode.Patient);
        }

        private void SetActive(bool active)
        {
            _active = active;
            if (_clock != null)
                _clock.UseDeviceTime = !active;
            if (!active)
                return;
            _seconds = _settings.StartHour * 3600.0 + _settings.StartMinute * 60.0;
            _lastWholeSecond = (long)_seconds;
            _scale = 1f;
            if (_clock != null)
                _clock.SetTime(StoryTime);
        }

        private void Update()
        {
            if (!_active)
                return;
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            var wanted = 1f;
            var loud = 0f;
            if (dominant == Emotion.Sadness)
                wanted = Mathf.Lerp(1f, _settings.SadTimeScale, strength);
            else if (dominant == Emotion.Fear || dominant == Emotion.Anger)
            {
                wanted = Mathf.Lerp(1f, _settings.FastTimeScale, strength);
                loud = strength;
            }
            _scale = Mathf.MoveTowards(_scale, wanted, Time.deltaTime / Mathf.Max(0.05f, _settings.BlendSeconds) * 4f);

            _seconds += Time.deltaTime * _scale;
            var whole = (long)_seconds;
            if (whole != _lastWholeSecond)
            {
                _lastWholeSecond = whole;
                Ticks++;
                if (_tickSource != null && _tick != null)
                    _tickSource.PlayOneShot(_tick, Mathf.Lerp(_settings.TickVolume, _settings.LoudTickVolume, loud));
            }
            if (_clock != null)
                _clock.SetTime(StoryTime);
        }
    }
}
