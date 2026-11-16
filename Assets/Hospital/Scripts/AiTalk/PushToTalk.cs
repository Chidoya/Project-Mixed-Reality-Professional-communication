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
        /// <summary>Trekker van de Quest 3 (Touch Plus) op naam, voor als de usage TriggerButton niet gevonden wordt.</summary>
        public const string RightTriggerPressedBinding = "<XRController>{RightHand}/triggerPressed";
        private const string LogPrefix = "[AiTalk] ";

        [SerializeField] private AiTalkSettings _settings;
        [Tooltip("Actie 'PushToTalk' uit Assets/Hospital/Data/AiTalkInput.inputactions (spatie + rechtertrigger).")]
        [SerializeField] private InputActionReference _talkAction;
        [Tooltip("Leeg = de microfoon van de Quest-headset (Oculus).")]
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
                _action.AddBinding(RightTriggerPressedBinding);
                _ownsAction = true;
            }
            _action.started += OnPressed;
            _action.canceled += OnReleased;
            _action.Enable();
            Debug.Log($"{LogPrefix}Push-to-talk klaar. Microfoons: {(Microphone.devices.Length == 0 ? "geen" : string.Join(", ", Microphone.devices))}. " +
                      $"Knoppen: {string.Join(", ", _action.controls)}");
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

        private void OnPressed(InputAction.CallbackContext context)
        {
            Debug.Log($"{LogPrefix}Praatknop ingedrukt ({context.control?.path}).");
            Begin();
        }

        private void OnReleased(InputAction.CallbackContext context) => Finish();

        /// <summary>Start een opname (ook bruikbaar vanuit een UI-knop).</summary>
        public void Begin()
        {
            if (IsRecording)
                return;
            if (CanStart != null && !CanStart())
            {
                Debug.Log(LogPrefix + "Opname niet gestart: het personage is nog bezig, het gesprek is afgelopen of er is geen sessie.");
                return;
            }
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "Geen microfoon gevonden.");
                return;
            }
            _device = string.IsNullOrEmpty(_microphoneDevice) ? PickDevice() : _microphoneDevice;
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
            Debug.Log($"{LogPrefix}Opname gestart ({_device ?? "standaardmicrofoon"}, {_frequency} Hz).");
            RecordingStarted?.Invoke();
        }

        /// <summary>Altijd de microfoon van de Quest-headset (naam bevat "Oculus"); niet gevonden: standaardmicrofoon.</summary>
        private static string PickDevice()
        {
            foreach (var name in Microphone.devices)
                if (name.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0)
                    return name;
            Debug.LogWarning(LogPrefix + "Geen Quest-microfoon gevonden (is de headset via Link verbonden?), standaardmicrofoon gebruikt.");
            return null;
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
            Debug.Log($"{LogPrefix}Opname gestopt na {seconds:F1} s.");
            if (position <= 0 || seconds < minimum)
            {
                Destroy(_clip);
                _clip = null;
                RecordingDiscarded?.Invoke();
                return;
            }
            var samples = new float[position * _clip.channels];
            _clip.GetData(samples, 0);
            var peak = 0f;
            foreach (var v in samples)
                peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak < 0.02f)
                Debug.LogWarning($"{LogPrefix}Opname is (bijna) stil: piek {peak * 100f:F1}% op '{_device ?? "standaardmicrofoon"}'. " +
                                 "Verkeerde microfoon, gedempt, of headset niet op (Quest-microfoon werkt alleen met de headset op).");
            else
                Debug.Log($"{LogPrefix}Opnamevolume: piek {peak * 100f:F0}%.");
            var wav = WavUtility.ToWav16kMono(samples, _clip.channels, _clip.frequency);
            Destroy(_clip);
            _clip = null;
            RecordingFinished?.Invoke(wav, seconds);
        }

        /// <summary>Stopt een lopende opname zonder ze te versturen.</summary>
        public void Cancel()
        {
            if (IsRecording)
                Abort();
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
