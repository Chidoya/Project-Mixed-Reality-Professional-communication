using System;
using System.Collections;
using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// Listens along with the AI conversation (next to <see cref="AiNpc"/>) and keeps every student turn for the debrief:
    /// the student's own recording, the patient's reply and voice, his emotion and trust after the turn and where the
    /// player stood. In memory only (never on disk), at most <see cref="MaxTurns"/> turns, cleared with the AudioClips
    /// when a new conversation starts (also on Try again).
    /// Allocations per turn (decoding the WAVs) are fine; nothing is allocated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConversationRecorder : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;
        [Tooltip("Our emotions of the patient (the AI bridge sets the target after every turn). Empty: on the same object.")]
        [SerializeField] private EmotionHolder _holder;
        [Tooltip("The player's head. Empty: Camera.main.")]
        [SerializeField] private Transform _head;
        [SerializeField, Range(1, 16)] private int _maxTurns = 8;
        [Tooltip("When there are too many turns, these many turns with the biggest change of the tension are always kept.")]
        [SerializeField, Range(0, 8)] private int _keepBiggestChanges = 3;

        [Header("Read only (this session)")]
        [SerializeField] private List<RecordedTurn> _turns = new();
        [SerializeField] private EmotionState _openingEmotion;
        [SerializeField] private float _openingTrust;

        private byte[] _pendingStudentWav;
        private float _start;

        public int MaxTurns => _maxTurns;
        public IReadOnlyList<RecordedTurn> Turns => _turns;
        public EmotionState OpeningEmotion => _openingEmotion;
        public float OpeningTrust => _openingTrust;
        public AiNpc Npc => _npc;

        /// <summary>A turn was stored (after the emotion of that turn is known).</summary>
        public event Action<RecordedTurn> TurnRecorded;
        /// <summary>Everything was cleared (a new conversation).</summary>
        public event Action Cleared;

        private void Awake()
        {
            if (_npc == null)
                _npc = GetComponent<AiNpc>();
            if (_holder == null)
                _holder = GetComponent<EmotionHolder>();
        }

        private void OnEnable()
        {
            if (_npc == null)
                return;
            _npc.TurnApplied += OnTurnApplied;
            _npc.StudentAudioSent += OnStudentAudio;
            _npc.ConversationStarting += Clear;
        }

        private void OnDisable()
        {
            if (_npc == null)
                return;
            _npc.TurnApplied -= OnTurnApplied;
            _npc.StudentAudioSent -= OnStudentAudio;
            _npc.ConversationStarting -= Clear;
        }

        private void OnDestroy() => Clear();

        /// <summary>Clears all turns and destroys their clips (a new conversation).</summary>
        public void Clear()
        {
            foreach (var turn in _turns)
                turn.Release();
            var had = _turns.Count > 0;
            _turns.Clear();
            _pendingStudentWav = null;
            _start = Time.time;
            if (had || isActiveAndEnabled)
                Cleared?.Invoke();
        }

        private void OnStudentAudio(byte[] wav) => _pendingStudentWav = wav;

        private void OnTurnApplied(AiTurn turn)
        {
            if (turn == null || turn.localFallback)
                return; // not understood or server down: the state did not change, no turn
            var wav = _pendingStudentWav;
            _pendingStudentWav = null;
            var head = HeadPose(out var position, out var yaw);
            if (turn.turn <= 0)
            {
                // The opening line: the starting point of the timeline, not a student turn.
                StartCoroutine(AfterOneFrame(() =>
                {
                    _openingEmotion = _holder != null ? _holder.Target : default;
                    _openingTrust = turn.trust;
                }));
                return;
            }
            var recorded = new RecordedTurn
            {
                Index = turn.turn,
                StudentText = turn.transcript ?? "",
                PatientText = turn.reply ?? "",
                AiEmotion = turn.emotion ?? "",
                Trust = turn.trust,
                Anger = turn.anger,
                AngerChange = turn.before != null ? turn.anger - turn.before.anger : 0f,
                HeadLocalPosition = position,
                HeadLocalYaw = yaw,
                HasPose = head,
                Time = Time.time - _start,
                StudentClip = wav != null ? WavUtility.ToAudioClip(wav, $"debrief_student_{turn.turn}") : null,
                PatientClip = ToClip(turn.audio_wav_base64, $"debrief_patient_{turn.turn}"),
            };
            // Wait one frame: the AI bridge sets the EmotionHolder's target on the same event.
            StartCoroutine(AfterOneFrame(() => Store(recorded)));
        }

        private void Store(RecordedTurn recorded)
        {
            recorded.Emotion = _holder != null ? _holder.Target : default;
            _turns.Add(recorded);
            Trim(_turns, _maxTurns, _keepBiggestChanges);
            TurnRecorded?.Invoke(recorded);
        }

        /// <summary>
        /// At most <paramref name="max"/> turns: drops the oldest turn that is not one of the <paramref name="keep"/> turns
        /// with the biggest change of the tension (those become the key moments of the debrief). Public for tests.
        /// </summary>
        public static void Trim(List<RecordedTurn> turns, int max, int keep)
        {
            while (turns.Count > max)
            {
                var important = new List<RecordedTurn>(turns);
                important.Sort((a, b) => Mathf.Abs(b.AngerChange).CompareTo(Mathf.Abs(a.AngerChange)));
                if (important.Count > keep)
                    important.RemoveRange(keep, important.Count - keep);
                var drop = turns.FindIndex(t => !important.Contains(t));
                if (drop < 0)
                    drop = 0;
                turns[drop].Release();
                turns.RemoveAt(drop);
            }
        }

        /// <summary>Adds a turn directly (tests and other sources).</summary>
        public void AddForTest(RecordedTurn turn)
        {
            _turns.Add(turn);
            Trim(_turns, _maxTurns, _keepBiggestChanges);
            TurnRecorded?.Invoke(turn);
        }

        private bool HeadPose(out Vector3 localPosition, out float localYaw)
        {
            localPosition = Vector3.zero;
            localYaw = 0f;
            var head = _head != null ? _head : Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return false;
            localPosition = transform.InverseTransformPoint(head.position);
            var forward = transform.InverseTransformDirection(head.forward);
            forward.y = 0f;
            localYaw = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward).eulerAngles.y : 0f;
            return true;
        }

        private static AudioClip ToClip(string base64, string name)
        {
            if (string.IsNullOrEmpty(base64))
                return null;
            try
            {
                return WavUtility.ToAudioClip(Convert.FromBase64String(base64), name);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static IEnumerator AfterOneFrame(Action action)
        {
            yield return null;
            action();
        }
    }
}
