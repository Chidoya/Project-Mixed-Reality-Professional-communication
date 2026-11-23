using System;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// Connects Liam's AI conversation (<see cref="AiNpc"/>, free speech through Tools/AiServer) to our patient view.
    /// Put it on the patient, next to the AiNpc, the <see cref="EmotionHolder"/> and the <see cref="PatientReputation"/>.
    /// <list type="bullet">
    /// <item>It is the <see cref="IPatientConversation"/> of the POV switch: open from the patient's first line until
    /// the AI server ends the conversation (or the player finishes it), with a new count of switches after a restart.</item>
    /// <item>After every turn the AI server's emotion label (Happy, Sad, Angry, Worried...) and tension (0..100) set the
    /// patient's emotions, so the room in the patient view follows the conversation. The server's emotion is the only
    /// rule: no extra thresholds here (so the face, the voice and the room always say the same).</item>
    /// <item>The AI server's trust (0..100) is the patient's reputation: the trust meter in the emotion HUD.</item>
    /// </list>
    /// The mapping from the AI labels to our four emotions is in the inspector, so it can be tuned without code.
    /// </summary>
    public class AiPatientBridge : MonoBehaviour, IPatientConversation
    {
        [Serializable]
        public class LabelMapping
        {
            [Tooltip("The emotion label of the AI server (see emotion_to_animation in the scenario JSON).")]
            public string Label;
            [Tooltip("Our emotion for it. None: no dominant emotion (see Neutral Tension Emotion).")]
            public EmotionChoice Emotion;
        }

        public enum EmotionChoice
        {
            None = -1,
            Joy = (int)Emotions.Emotion.Joy,
            Anger = (int)Emotions.Emotion.Anger,
            Fear = (int)Emotions.Emotion.Fear,
            Sadness = (int)Emotions.Emotion.Sadness,
        }

        [SerializeField] private AiNpc _npc;
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PatientReputation _reputation;

        [Tooltip("Distance to the player is measured from here (the head). Empty: the AiNpc.")]
        [SerializeField] private Transform _head;

        [Header("AI emotion label to our emotion")]
        [SerializeField] private LabelMapping[] _mapping =
        {
            new LabelMapping { Label = "Happy", Emotion = EmotionChoice.Joy },
            new LabelMapping { Label = "Angry", Emotion = EmotionChoice.Anger },
            new LabelMapping { Label = "Worried", Emotion = EmotionChoice.Fear },
            new LabelMapping { Label = "Surprised", Emotion = EmotionChoice.Fear },
            new LabelMapping { Label = "Sad", Emotion = EmotionChoice.Sadness },
            new LabelMapping { Label = "Crying", Emotion = EmotionChoice.Sadness },
            new LabelMapping { Label = "Pain", Emotion = EmotionChoice.Sadness },
            new LabelMapping { Label = "Neutral", Emotion = EmotionChoice.None },
        };

        [Tooltip("Strength of the dominant emotion: from Min (tension or trust 0) to Max (100).")]
        [SerializeField, Range(0f, 1f)] private float _minStrength = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _maxStrength = 0.9f;

        [Tooltip("The other three emotions.")]
        [SerializeField, Range(0f, 1f)] private float _otherStrength = 0.05f;

        private bool _started;
        private bool _bubblesHidden;

        public event Action ConversationStarted;

        public AiNpc Npc => _npc;

        /// <summary>For testing (the dev clipboard): the AI turns no longer change the patient's emotions.</summary>
        public bool HoldEmotions { get; set; }

        /// <summary>For testing (the dev clipboard): the AI turns no longer change the trust.</summary>
        public bool HoldTrust { get; set; }
        public bool IsPlaying => _npc != null && _npc.isActiveAndEnabled && _started && !_npc.HasEnded;
        public bool BubblesHidden { set => _bubblesHidden = value; }
        public bool AreBubblesHidden => _bubblesHidden;

        /// <summary>The emotion the last turn mapped to (null: none dominant).</summary>
        public Emotion? LastEmotion { get; private set; }

        public bool IsPlayerNear(float range)
        {
            var camera = Camera.main;
            if (camera == null)
                return false;
            var head = _head != null ? _head : _npc != null ? _npc.transform : transform;
            var offset = camera.transform.position - head.position;
            offset.y = 0f;
            return offset.magnitude <= range;
        }

        private void Awake()
        {
            if (_npc == null)
                _npc = GetComponent<AiNpc>();
            if (_holder == null)
                _holder = GetComponent<EmotionHolder>();
            if (_reputation == null)
                _reputation = GetComponent<PatientReputation>();
        }

        private void OnEnable()
        {
            if (_npc != null)
                _npc.TurnApplied += OnTurnApplied;
        }

        private void OnDisable()
        {
            if (_npc != null)
                _npc.TurnApplied -= OnTurnApplied;
        }

        private void Update()
        {
            // A conversation runs from the opening line (ConversationSeconds > 0) until the server ends it. Restart
            // puts the clock back to 0 before the new opening, so a new start is seen even on a restart.
            var running = _npc != null && _npc.ConversationSeconds > 0f;
            if (running && !_started)
            {
                _started = true;
                ConversationStarted?.Invoke();
            }
            else if (!running && _started)
                _started = false;
        }

        private void OnTurnApplied(AiTurn turn)
        {
            // Not understood or the server is not reachable: the patient's state stays as it was.
            if (turn == null || turn.localFallback)
                return;
            if (_reputation != null && !HoldTrust)
                _reputation.Set(turn.trust);
            if (_holder != null && !HoldEmotions)
                _holder.SetTarget(ToEmotions(turn.emotion, turn.anger, turn.trust));
        }

        /// <summary>
        /// Our four emotions for an AI label, tension and trust (both 0..100). One rule: the AI server decides the
        /// emotion (it only allows emotions that fit the patient's band and never Happy right after a sentence that
        /// cost trust); here it is only mapped. Tension and trust only set how strong it is.
        /// </summary>
        public EmotionState ToEmotions(string label, float tension, float trust)
        {
            var choice = EmotionChoice.None;
            foreach (var entry in _mapping)
            {
                if (string.Equals(entry.Label, label, StringComparison.OrdinalIgnoreCase))
                {
                    choice = entry.Emotion;
                    break;
                }
            }
            if (choice == EmotionChoice.None)
            {
                LastEmotion = null;
                return new EmotionState(_otherStrength, _otherStrength, _otherStrength, _otherStrength);
            }
            var emotion = (Emotion)(int)choice;
            var drive = emotion == Emotions.Emotion.Joy ? trust : tension;
            var strength = Mathf.Lerp(_minStrength, _maxStrength, Mathf.Clamp01(drive / 100f));
            LastEmotion = emotion;
            return new EmotionState(_otherStrength, _otherStrength, _otherStrength, _otherStrength).With(emotion, strength);
        }
    }
}
