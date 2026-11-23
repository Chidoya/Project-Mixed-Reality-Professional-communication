using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using UnityEngine;
using UnityEngine.XR;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// In the patient view, with Fear or Anger dominant: a heartbeat in the player's ears (2D) and a short pulse in both
    /// controllers on every beat, faster and louder the stronger the emotion (70 bpm at the threshold, 120 bpm at full
    /// strength). Off in the nurse view and when the emotion fades.
    /// </summary>
    public class HeartbeatFeedback : MonoBehaviour
    {
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private PatientWorldController _world;
        [SerializeField] private AudioSource _source;
        [Tooltip("One beat (lub-dub).")]
        [SerializeField] private AudioClip _beat;

        [SerializeField] private float _minBpm = 70f;
        [SerializeField] private float _maxBpm = 120f;
        [SerializeField, Range(0f, 1f)] private float _minVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _maxVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _minAmplitude = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _maxAmplitude = 0.5f;
        [SerializeField, Min(0.01f)] private float _pulseSeconds = 0.05f;
        [Tooltip("Below this strength there is no heartbeat.")]
        [SerializeField, Range(0f, 1f)] private float _startStrength = 0.05f;

        private readonly List<InputDevice> _controllers = new List<InputDevice>(4);
        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;
        private float _nextBeat;
        private float _strength;

        public bool IsBeating { get; private set; }
        public int Beats { get; private set; }
        public float Bpm => Mathf.Lerp(_minBpm, _maxBpm, _strength);

        private void Awake()
        {
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
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
            Stop();
        }

        private void OnPovChanged(PovMode mode)
        {
            if (mode == PovMode.Patient)
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, _controllers);
            else
                Stop();
        }

        private void Update()
        {
            if (_pov == null || _pov.Mode != PovMode.Patient)
            {
                if (IsBeating)
                    Stop();
                return;
            }
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            if ((dominant != Emotion.Fear && dominant != Emotion.Anger) || strength < _startStrength)
            {
                if (IsBeating)
                    Stop();
                return;
            }
            _strength = strength;
            if (!IsBeating)
            {
                IsBeating = true;
                _nextBeat = Time.time;
            }
            if (Time.time < _nextBeat)
                return;
            _nextBeat = Time.time + 60f / Bpm;
            Beat();
        }

        private void Beat()
        {
            Beats++;
            if (_source != null && _beat != null)
            {
                _source.pitch = Mathf.Lerp(1f, 1.08f, _strength);
                _source.PlayOneShot(_beat, Mathf.Lerp(_minVolume, _maxVolume, _strength));
            }
            var amplitude = Mathf.Lerp(_minAmplitude, _maxAmplitude, _strength);
            for (var i = 0; i < _controllers.Count; i++)
            {
                var device = _controllers[i];
                if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                    device.SendHapticImpulse(0, amplitude, _pulseSeconds);
            }
        }

        private void Stop()
        {
            IsBeating = false;
            _strength = 0f;
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }
    }
}
