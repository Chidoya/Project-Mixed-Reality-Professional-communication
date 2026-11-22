using System;
using ProCommVR.Hospital.AiTalk;
using UnityEngine;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// Hands-free talking with the AI patient, instead of Liam's push-to-talk: the microphone is always on, and when the
    /// player stops speaking (a short silence), what was said is sent to the AI server (<see cref="AiNpc.SendStudentAudio"/>).
    /// <para>
    /// Why: starting the microphone on every press of the trigger froze the picture for a moment, and the first words were
    /// lost while it started. Here the microphone starts once, and a short piece from before the speech (pre-roll) is
    /// always included, so a sentence is never cut at the start.
    /// </para>
    /// <para>
    /// Speech is detected by loudness (voice activity detection): a frame is speech when it is clearly louder than the
    /// background noise, which is measured all the time. While the patient speaks or the AI is thinking, the microphone
    /// is ignored (no echo of the patient's own voice). M (keyboard) or <see cref="Listening"/> switches it off.
    /// </para>
    /// </summary>
    public class VoiceActivityTalk : MonoBehaviour
    {
        public enum State { Off, Waiting, Ready, Hearing, Sending }

        private const string LogPrefix = "[ProCommVR] Voice: ";
        private const int Rate = 16000;
        private const int BufferSeconds = 20;
        private const int FrameSamples = Rate / 50; // 20 ms

        [SerializeField] private AiNpc _npc;
        [Tooltip("Empty: the microphone of the Quest headset (name with \"Oculus\"), else the default microphone.")]
        [SerializeField] private string _microphoneDevice = "";
        [SerializeField] private bool _listening = true;

        [Header("Speech detection")]
        [Tooltip("Speech must be this many times louder than the background noise.")]
        [SerializeField, Range(1.5f, 10f)] private float _noiseFactor = 3f;
        [Tooltip("Speech is at least this loud (RMS, 0..1), also in a very quiet room.")]
        [SerializeField, Range(0.002f, 0.1f)] private float _minLevel = 0.012f;
        [Tooltip("Seconds of loud sound before it counts as speech (a click or a cough is shorter).")]
        [SerializeField, Range(0.05f, 0.5f)] private float _startSeconds = 0.15f;
        [Tooltip("Seconds of silence that end what the player says; then it is sent.")]
        [SerializeField, Range(0.3f, 3f)] private float _endSilenceSeconds = 0.9f;
        [Tooltip("Seconds from before the speech that are sent too, so the first word is complete.")]
        [SerializeField, Range(0f, 1f)] private float _preRollSeconds = 0.4f;
        [Tooltip("Shorter speech is ignored.")]
        [SerializeField, Range(0.1f, 2f)] private float _minSpeechSeconds = 0.5f;
        [Tooltip("Longer speech is sent anyway.")]
        [SerializeField, Range(3f, 30f)] private float _maxSpeechSeconds = 15f;
        [Tooltip("Seconds after the patient stopped speaking before the microphone listens again (echo of his voice).")]
        [SerializeField, Range(0f, 1.5f)] private float _afterPatientSeconds = 0.4f;

        private AudioClip _clip;
        private string _device;
        private int _readPosition;
        private float[] _ring;          // all samples of the last BufferSeconds, mono 16 kHz
        private long _written;          // samples written into the ring in total
        private readonly float[] _frame = new float[FrameSamples];
        private int _frameFill;
        private float[] _read = new float[Rate];

        private float _noise = 0.005f;
        private int _loudFrames;
        private int _silentFrames;
        private long _speechStart = -1; // ring index (total count) where the sent piece starts
        private int _speechFrames;
        private float _blockedUntil;

        public State Current { get; private set; } = State.Off;
        public float Level { get; private set; }
        public float NoiseLevel => _noise;

        /// <summary>Raised with the wav (16 kHz mono) and its length in seconds, right before it is sent.</summary>
        public event Action<byte[], float> SpeechSent;

        public bool Listening
        {
            get => _listening;
            set
            {
                _listening = value;
                if (!value)
                    ResetSpeech();
            }
        }

        private void Start()
        {
            if (_npc == null)
                _npc = FindAnyObjectByType<AiNpc>();
            _ring = new float[Rate * BufferSeconds];
            StartMicrophone();
        }

        private void OnDisable()
        {
            if (_clip != null)
            {
                Microphone.End(_device);
                Destroy(_clip);
                _clip = null;
            }
            Current = State.Off;
        }

        private void StartMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "no microphone found.");
                return;
            }
            _device = !string.IsNullOrEmpty(_microphoneDevice) ? _microphoneDevice : PickDevice();
            Microphone.GetDeviceCaps(_device, out var min, out var max);
            var rate = (min == 0 && max == 0) || (min <= Rate && Rate <= max) ? Rate : max;
            // Looping: the microphone runs for the whole scene, started once.
            _clip = Microphone.Start(_device, true, 2, rate);
            _readPosition = 0;
            Debug.Log($"{LogPrefix}listening on '{_device ?? "default microphone"}' ({rate} Hz), always on.");
        }

        private static string PickDevice()
        {
            foreach (var name in Microphone.devices)
                if (name.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0)
                    return name;
            return null;
        }

        private void Update()
        {
            if (Application.isEditor && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                Listening = !Listening;
            if (_clip == null)
            {
                Current = State.Off;
                return;
            }
            ReadMicrophone();
        }

        /// <summary>Reads the new samples of the looping clip, resampled to 16 kHz mono, frame by frame.</summary>
        private void ReadMicrophone()
        {
            var position = Microphone.GetPosition(_device);
            if (position < 0 || position == _readPosition)
                return;
            var available = position - _readPosition;
            if (available < 0)
                available += _clip.samples;
            ReadClip(_readPosition, available);
            _readPosition = position;

            var mono = _clip.channels == 1 ? _read : WavUtility.ToMono(_read, _clip.channels);
            var samples = _clip.frequency == Rate ? mono : WavUtility.Resample(mono, _clip.frequency, Rate);
            Process(samples, _clip.frequency == Rate && _clip.channels == 1 ? available : samples.Length);
        }

        /// <summary>
        /// Feeds sound as if it came from the microphone (mono, any rate), e.g. a recorded sentence in a test.
        /// </summary>
        public void Feed(float[] mono, int rate)
        {
            if (_ring == null)
                _ring = new float[Rate * BufferSeconds];
            var samples = rate == Rate ? mono : WavUtility.Resample(mono, rate, Rate);
            Process(samples, samples.Length);
        }

        /// <summary>Stops the microphone (e.g. in a test that feeds its own sound).</summary>
        public void StopMicrophone() => OnDisable();

        private void Process(float[] samples, int length)
        {
            for (var i = 0; i < length; i++)
            {
                _ring[_written % _ring.Length] = samples[i];
                _written++;
                _frame[_frameFill++] = samples[i];
                if (_frameFill == FrameSamples)
                {
                    _frameFill = 0;
                    OnFrame();
                }
            }
        }

        private void ReadClip(int start, int count)
        {
            // GetData wraps around the end of a looping clip by itself.
            if (_read.Length != count * _clip.channels)
                Array.Resize(ref _read, count * _clip.channels);
            _clip.GetData(_read, start);
        }

        private void OnFrame()
        {
            var sum = 0f;
            for (var i = 0; i < FrameSamples; i++)
                sum += _frame[i] * _frame[i];
            Level = Mathf.Sqrt(sum / FrameSamples);
            var threshold = Mathf.Max(_minLevel, _noise * _noiseFactor);
            var loud = Level > threshold;

            // The patient speaks or the AI thinks: do not listen (and do not learn his voice as noise).
            // No conversation yet (the patient has not started), or it has ended: nothing to send to.
            var noConversation = _npc == null || _npc.ConversationSeconds <= 0f || _npc.HasEnded;
            var blocked = !_listening || noConversation || _npc.Busy || _npc.IsSpeaking;
            if (_npc != null && _npc.IsSpeaking)
                _blockedUntil = Time.unscaledTime + _afterPatientSeconds;
            if (blocked || Time.unscaledTime < _blockedUntil)
            {
                if (_speechStart >= 0)
                    ResetSpeech();
                Current = !_listening || noConversation ? State.Off : _npc.Busy ? State.Sending : State.Waiting;
                return;
            }

            if (!loud)
                _noise = Mathf.Lerp(_noise, Level, 0.02f); // the background noise follows slowly
            const float frameSeconds = FrameSamples / (float)Rate;

            if (_speechStart < 0)
            {
                Current = State.Ready;
                _loudFrames = loud ? _loudFrames + 1 : 0;
                if (_loudFrames * frameSeconds >= _startSeconds)
                {
                    var preRoll = (long)((_preRollSeconds + _loudFrames * frameSeconds) * Rate);
                    _speechStart = Math.Max(Math.Max(0, _written - _ring.Length + Rate), _written - preRoll);
                    _speechFrames = _loudFrames;
                    _silentFrames = 0;
                    Current = State.Hearing;
                }
                return;
            }

            Current = State.Hearing;
            _silentFrames = loud ? 0 : _silentFrames + 1;
            if (loud)
                _speechFrames++;
            var seconds = (_written - _speechStart) / (float)Rate;
            if (_silentFrames * frameSeconds >= _endSilenceSeconds || seconds >= _maxSpeechSeconds)
                Send();
        }

        private void Send()
        {
            const float frameSeconds = FrameSamples / (float)Rate;
            var speech = _speechFrames * frameSeconds;
            if (speech < _minSpeechSeconds)
            {
                ResetSpeech();
                return;
            }
            // Keep 0.25 s of the silence at the end, cut the rest.
            var end = _written - (long)(Mathf.Max(0f, _silentFrames * frameSeconds - 0.25f) * Rate);
            var count = (int)(end - _speechStart);
            var samples = new float[count];
            for (var i = 0; i < count; i++)
                samples[i] = _ring[(_speechStart + i) % _ring.Length];
            ResetSpeech();
            var wav = WavUtility.Encode(samples, Rate);
            var seconds = count / (float)Rate;
            Debug.Log($"{LogPrefix}sent {seconds:F1} s of speech (level {Level:0.000}, noise {_noise:0.000}).");
            Current = State.Sending;
            SpeechSent?.Invoke(wav, seconds);
            _ = _npc.SendStudentAudio(wav);
        }

        private void ResetSpeech()
        {
            _speechStart = -1;
            _speechFrames = 0;
            _silentFrames = 0;
            _loudFrames = 0;
        }

        /// <summary>A short text for a hint: what the microphone does now.</summary>
        public string StatusText => Current switch
        {
            State.Off => "Microphone off",
            State.Waiting => "Wait, the patient is talking...",
            State.Ready => "Listening, just talk",
            State.Hearing => "Hearing you...",
            State.Sending => "The patient is thinking...",
            _ => "",
        };
    }
}
