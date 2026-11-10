using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Neemt op met de microfoon zolang de knop ingedrukt is: spatiebalk in de Editor, trigger van de
    /// rechtercontroller op de Quest (beide bindings in de InputActionReference; zonder referentie maakt het
    /// script zelf een actie met dezelfde bindings). Bij loslaten: afknippen op de echte lengte, omzetten naar
    /// 16 kHz mono wav en RecordingFinished. Korter dan minRecordSeconds: genegeerd.
    /// De opname bestaat alleen in het geheugen.
    /// </summary>
    public class PushToTalk : MonoBehaviour
    {
        public const string KeyboardBinding = "<Keyboard>/space";
        public const string RightTriggerBinding = "<XRController>{RightHand}/{TriggerButton}";
        private const string LogPrefix = "[AiTalk] ";

        [SerializeField] private AiTalkSettings _settings;
        [Tooltip("Actie 'PushToTalk' uit Assets/Hospital/Data/AiTalkInput.inputactions (spatie + rechtertrigger).")]
        [SerializeField] private InputActionReference _talkAction;
        [Tooltip("Leeg = standaardmicrofoon.")]
        [SerializeField] private string _microphoneDevice = "";

        private InputAction _action;
        private bool _ownsAction;
        private AudioClip _clip;
        private string _device;
        private int _frequency;
        private float _startTime;

        public bool IsRecording { get; private set; }
        public float RecordingSeconds => IsRecording ? Time.unscaledTime - _startTime : 0f;

        /// <summary>Vraagt de eigenaar (AiNpc) of er nu opgenomen mag worden.</summary>
        public Func<bool> CanStart;

        /// <summary>Knop ingedrukt en opname gestart.</summary>
        public event Action RecordingStarted;

        /// <summary>Knop losgelaten: wav (16 kHz mono) en de lengte in seconden.</summary>
        public event Action<byte[], float> RecordingFinished;

        /// <summary>Opname was te kort en wordt genegeerd.</summary>
        public event Action RecordingDiscarded;

        public AiTalkSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        private void OnEnable()
        {
            if (_talkAction != null && _talkAction.action != null)
            {
                _action = _talkAction.action;
                _ownsAction = false;
            }
            else
            {
                _action = new InputAction("PushToTalk", InputActionType.Button);
                _action.AddBinding(KeyboardBinding);
                _action.AddBinding(RightTriggerBinding);
                _ownsAction = true;
            }
            _action.started += OnPressed;
            _action.canceled += OnReleased;
            _action.Enable();
        }

        private void OnDisable()
        {
            if (_action != null)
            {
                _action.started -= OnPressed;
                _action.canceled -= OnReleased;
                if (_ownsAction)
                    _action.Dispose();
                else
                    _action.Disable();
                _action = null;
            }
            if (IsRecording)
                Abort();
        }

        private void Update()
        {
            // Langer dan het maximum: zelf afsluiten en versturen
            if (IsRecording && _settings != null && RecordingSeconds >= _settings.maxRecordSeconds)
                Finish();
        }

        private void OnPressed(InputAction.CallbackContext _) => Begin();
        private void OnReleased(InputAction.CallbackContext _) => Finish();

        /// <summary>Start een opname (ook bruikbaar vanuit een UI-knop).</summary>
        public void Begin()
        {
            if (IsRecording || (CanStart != null && !CanStart()))
                return;
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "Geen microfoon gevonden.");
                return;
            }
            _device = string.IsNullOrEmpty(_microphoneDevice) ? null : _microphoneDevice;
            Microphone.GetDeviceCaps(_device, out var min, out var max);
            _frequency = (min == 0 && max == 0) || (min <= WavUtility.ServerSampleRate && WavUtility.ServerSampleRate <= max)
                ? WavUtility.ServerSampleRate
                : max;
            var seconds = Mathf.CeilToInt(_settings != null ? _settings.maxRecordSeconds : 15f) + 1;
            _clip = Microphone.Start(_device, false, seconds, _frequency);
            if (_clip == null)
            {
                Debug.LogWarning(LogPrefix + "Microfoon kon niet starten.");
                return;
            }
            _startTime = Time.unscaledTime;
            IsRecording = true;
            RecordingStarted?.Invoke();
        }

        /// <summary>Stopt de opname en verstuurt ze (via RecordingFinished) als ze lang genoeg is.</summary>
        public void Finish()
        {
            if (!IsRecording)
                return;
            IsRecording = false;
            var stillRecording = Microphone.IsRecording(_device);
            var position = stillRecording ? Microphone.GetPosition(_device) : _clip.samples;
            Microphone.End(_device);

            var seconds = position / (float)_clip.frequency;
            var minimum = _settings != null ? _settings.minRecordSeconds : 0.4f;
            if (position <= 0 || seconds < minimum)
            {
                Destroy(_clip);
                _clip = null;
                RecordingDiscarded?.Invoke();
                return;
            }
            var samples = new float[position * _clip.channels];
            _clip.GetData(samples, 0);
            var wav = WavUtility.ToWav16kMono(samples, _clip.channels, _clip.frequency);
            Destroy(_clip);
            _clip = null;
            RecordingFinished?.Invoke(wav, seconds);
        }

        private void Abort()
        {
            IsRecording = false;
            Microphone.End(_device);
            if (_clip != null)
                Destroy(_clip);
            _clip = null;
        }
    }
}
