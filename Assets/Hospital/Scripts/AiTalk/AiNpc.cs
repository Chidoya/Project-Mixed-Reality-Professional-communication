using System;
using ProCommVR.Hospital.Face;
using UnityEngine;
using UnityEngine.Events;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Personage dat vrij praat via Tools/AiServer. Zet op de root van het personage (naast FaceController,
    /// FaceFromAnimator en HeadLookAt). Flow per beurt:
    /// knop los -> meteen een luisterreactie (gezicht, kijken, optioneel "hmm") -> /turn/audio ->
    /// antwoord afspelen op het hoofd (3D), gezicht, Animator-state, kijkrichting en mond volgen het antwoord.
    /// Terwijl de server rekent, kan er niet opgenomen worden. Drukt de speler terwijl het personage praat,
    /// dan stopt het (onderbreken) en start de opname.
    /// </summary>
    public class AiNpc : MonoBehaviour
    {
        private const string LogPrefix = "[AiTalk] ";

        [Serializable] public class MoodEvent : UnityEvent<float, float> { }
        [Serializable] public class EndedEvent : UnityEvent<string> { }

        [SerializeField] private AiTalkSettings _settings;
        [Tooltip("Leeg = zoekt PushToTalk in de scene.")]
        [SerializeField] private PushToTalk _pushToTalk;
        [Tooltip("Start het gesprek (en de openingszin) zelf bij Start.")]
        [SerializeField] private bool _autoStart = true;

        [Header("Personage (leeg = zelf zoeken)")]
        [SerializeField] private FaceController _face;
        [SerializeField] private FaceFromAnimator _faceFromAnimator;
        [SerializeField] private HeadLookAt _headLook;
        [SerializeField] private Animator _animator;
        [Tooltip("Leeg = een 3D AudioSource op het Head-bot.")]
        [SerializeField] private AudioSource _voice;

        [Header("Wachten")]
        [Tooltip("Korte geluidjes ('hmm') terwijl de server rekent. Optioneel.")]
        [SerializeField] private AudioClip[] _listeningClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float _listeningClipChance = 0.6f;
        [Tooltip("Ingesproken versie van settings.fallbackReply ('Sorry, what did you say?'). Optioneel.")]
        [SerializeField] private AudioClip _fallbackClip;
        [Tooltip("Ingesproken time_up_line van het scenario ('They're taking him away now...'). Optioneel.")]
        [SerializeField] private AudioClip _timeUpClip;

        [Header("Mond")]
        [SerializeField] private float _mouthGain = 9f;
        [SerializeField] private float _mouthOpenSpeed = 30f;
        [SerializeField] private float _mouthCloseSpeed = 9f;
        [SerializeField] private float _crossFade = 0.25f;

        [Header("Events")]
        [Tooltip("Boosheid en vertrouwen (0..100) na elke beurt, bv. voor licht, geluid of de hartmonitor.")]
        public MoodEvent OnMoodChanged = new();
        [Tooltip("Einde van het gesprek: resolved, time_up of stopped.")]
        public EndedEvent OnConversationEnded = new();

        /// <summary>Elke beurt (ook de opening en lokale reserveantwoorden), na het toepassen.</summary>
        public event Action<AiTurn> TurnApplied;
        /// <summary>Korte status voor het debugpaneel: Listening, Thinking, Speaking, Ended...</summary>
        public event Action<string> StatusChanged;

        private AiTalkClient _client;
        private readonly float[] _samples = new float[256];
        private float _mouth;
        private bool _wasSpeaking;
        private string _lastAnimation;
        private string _pendingEnd;
        private bool _endedRaised;
        private float _timeLimit;
        private float _conversationStart;

        public bool Busy { get; private set; }
        public bool IsSpeaking => _voice != null && _voice.isPlaying;
        public bool HasEnded => _endedRaised || !string.IsNullOrEmpty(_pendingEnd);
        public float Anger { get; private set; }
        public float Trust { get; private set; }
        public string Band { get; private set; } = "";
        public string Phase { get; private set; } = "";
        public AiTurn LastTurn { get; private set; }
        public AiTalkSettings Settings => _settings;
        public string EndReason { get; private set; }
        /// <summary>Seconden sinds de openingszin (0 voor de start).</summary>
        public float ConversationSeconds => _conversationStart > 0f ? Time.time - _conversationStart : 0f;
        /// <summary>Tijdslimiet van het scenario in seconden (0 = geen).</summary>
        public float TimeLimit => _timeLimit;

        private void Awake()
        {
            if (_face == null) _face = GetComponentInChildren<FaceController>();
            if (_faceFromAnimator == null) _faceFromAnimator = GetComponentInChildren<FaceFromAnimator>();
            if (_headLook == null) _headLook = GetComponentInChildren<HeadLookAt>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_pushToTalk == null) _pushToTalk = FindAnyObjectByType<PushToTalk>();
            if (_voice == null) _voice = CreateVoice();
            if (_settings == null)
            {
                Debug.LogError(LogPrefix + "Geen AiTalkSettings op " + name, this);
                enabled = false;
                return;
            }
            _client = new AiTalkClient(_settings);
#if UNITY_EDITOR
            if (_settings.editorTargetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = _settings.editorTargetFrameRate;
            }
#endif
        }

        private void OnEnable()
        {
            if (_pushToTalk == null)
                return;
            _pushToTalk.CanStart = CanRecord;
            _pushToTalk.RecordingStarted += OnRecordingStarted;
            _pushToTalk.RecordingFinished += OnRecordingFinished;
            _pushToTalk.RecordingDiscarded += OnRecordingDiscarded;
        }

        private void OnDisable()
        {
            if (_pushToTalk == null)
                return;
            _pushToTalk.CanStart = null;
            _pushToTalk.RecordingStarted -= OnRecordingStarted;
            _pushToTalk.RecordingFinished -= OnRecordingFinished;
            _pushToTalk.RecordingDiscarded -= OnRecordingDiscarded;
        }

        private void Start()
        {
            if (_autoStart)
                _ = StartConversation();
        }

        private void OnDestroy()
        {
            // Zonder te wachten: de server bewaart het transcript zodra het verzoek aankomt
            if (_client != null && _client.HasSession && !_client.IsEnded)
                _ = _client.EndSession();
        }

        // --- Gesprek ---------------------------------------------------------------------------

        /// <summary>Start een sessie en speelt de openingszin.</summary>
        public async Awaitable StartConversation()
        {
            Busy = true;
            SetStatus("Connecting...");
            var opening = await _client.StartSession();
            if (this == null)
                return;
            Busy = false;
            if (!opening.localFallback)
            {
                _timeLimit = opening.time_limit_seconds;
                _conversationStart = Time.time;
            }
            Apply(opening);
        }

        /// <summary>
        /// Beëindigt het gesprek nu (bv. "time_up" of "stopped" via de testtoets E). Sarah stopt met praten,
        /// zegt bij time_up haar laatste zin (als er een clip is) en OnConversationEnded wordt gemeld.
        /// </summary>
        public void EndNow(string reason)
        {
            if (HasEnded)
                return;
            if (_pushToTalk != null && _pushToTalk.IsRecording)
                _pushToTalk.Cancel();
            _voice.Stop();
            _pendingEnd = reason;
            if (reason == "time_up" && _timeUpClip != null)
            {
                PlayClip(_timeUpClip);
                SetStatus("Time is up");
                return; // het einde volgt als de clip gedaan is (Update)
            }
            RaiseEndIfPending();
        }

        /// <summary>Opnieuw proberen: nieuwe sessie met hetzelfde scenario, vanaf de openingszin.</summary>
        public async Awaitable Restart()
        {
            if (Busy)
                return;
            _voice.Stop();
            _pendingEnd = null;
            _endedRaised = false;
            EndReason = null;
            LastTurn = null;
            _timeLimit = 0f;
            _conversationStart = 0f;
            Anger = Trust = 0f;
            Band = Phase = "";
            await StartConversation();
        }

        /// <summary>Rustig staan (na het gesprek, tijdens de nabespreking).</summary>
        public void SetIdle()
        {
            _voice.Stop();
            PlayState("Idle");
            if (_headLook != null)
                _headLook.Mode = HeadLookAt.LookMode.LookAtPlayer;
        }

        /// <summary>Beoordeling van dit gesprek door de server (rubric). Duurt 20 tot 40 s.</summary>
        public Awaitable<AiDebrief> RequestDebrief() => _client.RequestDebrief();

        /// <summary>Stuurt een opname (wav, 16 kHz mono) zoals PushToTalk dat doet. Ook voor tests.</summary>
        public async Awaitable<AiTurn> SendStudentAudio(byte[] wav)
        {
            if (Busy || HasEnded)
                return null;
            Busy = true;
            ShowListening();
            var turn = await _client.SendAudio(wav);
            if (this == null)
                return turn;
            Busy = false;
            Apply(turn);
            return turn;
        }

        private bool CanRecord() => !Busy && !HasEnded && _client != null && _client.HasSession;

        private void OnRecordingStarted()
        {
            if (IsSpeaking)
            {
                _voice.Stop(); // onderbroken door de speler
                Debug.Log(LogPrefix + "Onderbroken door de speler.");
            }
            SetStatus("Recording...");
        }

        private void OnRecordingFinished(byte[] wav, float seconds) => _ = SendStudentAudio(wav);

        private void OnRecordingDiscarded() => SetStatus("Too short, hold the button while you talk.");

        /// <summary>Meteen na het loslaten: het personage luistert zichtbaar, zodat het wachten niet leeg voelt.</summary>
        private void ShowListening()
        {
            SetStatus("Thinking...");
            if (_face != null)
            {
                var expression = Band is "HIGH" or "VERY_HIGH" ? FaceExpression.Angry : FaceExpression.Worried;
                _face.SetExpression(expression, 0.5f, 0.25f);
            }
            if (_headLook != null)
                _headLook.Mode = HeadLookAt.LookMode.LookAtPlayer;
            if (_listeningClips.Length > 0 && UnityEngine.Random.value < _listeningClipChance && !IsSpeaking)
            {
                var clip = _listeningClips[UnityEngine.Random.Range(0, _listeningClips.Length)];
                if (clip != null)
                    _voice.PlayOneShot(clip, 0.8f);
            }
        }

        private void Apply(AiTurn turn)
        {
            LastTurn = turn;
            if (turn.localFallback)
            {
                // Server niet bereikbaar of niet verstaan: lokaal antwoord, toestand blijft
                if (_fallbackClip != null)
                    PlayClip(_fallbackClip);
                SetStatus(turn.retry ? "Not understood" : "Server error");
                TurnApplied?.Invoke(turn);
                return;
            }

            var changed = !Mathf.Approximately(Anger, turn.anger) || !Mathf.Approximately(Trust, turn.trust);
            Anger = turn.anger;
            Trust = turn.trust;
            Band = turn.band ?? "";
            Phase = turn.phase ?? "";
            if (changed)
                OnMoodChanged.Invoke(Anger, Trust);

            if (Enum.TryParse(turn.emotion, true, out FaceExpression expression) && _face != null)
            {
                _face.SetExpression(expression, Mathf.Max(0.4f, Anger / 100f), 0.3f);
                if (_faceFromAnimator != null)
                    _faceFromAnimator.RestExpression = expression; // blijft na de 5 s van SetExpression
            }
            PlayState(turn.animation);
            if (_headLook != null)
            {
                if (turn.pose is "TurnsAway" or "Withdrawn")
                    _headLook.Mode = HeadLookAt.LookMode.AvoidGaze;
                else if (turn.pose == "Engaged")
                    _headLook.Mode = HeadLookAt.LookMode.LookAtPlayer;
            }

            var clip = WavUtility.ToAudioClip(Decode(turn.audio_wav_base64), "reply");
            if (clip != null)
                PlayClip(clip);
            if (turn.HasEnded)
                _pendingEnd = turn.ended;
            SetStatus(clip != null ? "Speaking" : "Your turn");
            TurnApplied?.Invoke(turn);
            if (clip == null)
                RaiseEndIfPending();
        }

        private void PlayState(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state) || _animator.runtimeAnimatorController == null)
                return;
            var hash = Animator.StringToHash(state);
            if (!_animator.HasState(0, hash))
            {
                Debug.LogWarning($"{LogPrefix}Animator van {name} heeft geen state '{state}'.");
                return;
            }
            _lastAnimation = state;
            _animator.CrossFadeInFixedTime(hash, _crossFade, 0);
        }

        private void PlayClip(AudioClip clip)
        {
            var old = _voice.clip;
            _voice.Stop();
            _voice.clip = clip;
            _voice.Play();
            if (old != null && old != clip && old.name == "reply") // alleen zelf gemaakte clips opruimen
                Destroy(old);
        }

        private static byte[] Decode(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return null;
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // --- Per frame: mond en einde van de zin --------------------------------------------------

        private void Update()
        {
            // Tijdslimiet van het scenario (de brancard vertrekt): niet midden in een serveroproep
            if (_timeLimit > 0f && !HasEnded && !Busy && ConversationSeconds >= _timeLimit)
                EndNow("time_up");

            var speaking = IsSpeaking;
            var target = 0f;
            if (speaking)
            {
                _voice.GetOutputData(_samples, 0);
                var sum = 0f;
                foreach (var s in _samples)
                    sum += s * s;
                target = Mathf.Clamp01(Mathf.Sqrt(sum / _samples.Length) * _mouthGain);
            }
            var speed = target > _mouth ? _mouthOpenSpeed : _mouthCloseSpeed;
            _mouth = Mathf.MoveTowards(_mouth, target, speed * Time.deltaTime);
            if (_face != null)
                _face.TalkLevel = _mouth;

            if (_wasSpeaking && !speaking)
            {
                // Zin uitgesproken: van Talking terug naar Idle, en het einde melden als het gesprek klaar is
                if (_lastAnimation == "Talking")
                    PlayState("Idle");
                if (!Busy && !HasEnded)
                    SetStatus("Your turn");
                RaiseEndIfPending();
            }
            _wasSpeaking = speaking;
        }

        private void RaiseEndIfPending()
        {
            if (_endedRaised || string.IsNullOrEmpty(_pendingEnd))
                return;
            _endedRaised = true;
            EndReason = _pendingEnd;
            SetStatus("Ended: " + _pendingEnd);
            Debug.Log($"{LogPrefix}Gesprek afgelopen: {_pendingEnd}");
            _ = _client.EndSession(_pendingEnd);
            OnConversationEnded.Invoke(_pendingEnd);
        }

        private void SetStatus(string status) => StatusChanged?.Invoke(status);

        private AudioSource CreateVoice()
        {
            var head = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var go = new GameObject("Voice");
            go.transform.SetParent(head != null ? head : transform, false);
            if (head == null)
                go.transform.localPosition = Vector3.up * 1.6f;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
