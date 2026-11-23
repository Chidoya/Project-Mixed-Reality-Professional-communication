using System;
using ProCommVR.Hospital.AiTalk;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// v2: the granddaughter's birthday card on the nightstand starts CLOSED (only "HAPPY BIRTHDAY" on the front), the way
    /// the closing card of the debrief tells it ("The birthday card is still closed"). It opens when the student gives him
    /// a reason to read it again: one of the <see cref="_openOnLabels"/> (a video call, a small step) or a conversation
    /// that ends well. A new conversation (Restart) closes it again. Before, the card stood open from the start, so the
    /// debrief said "still closed" about a card the player had seen open.
    /// </summary>
    public class BirthdayCard : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;

        [Tooltip("The hinge of the front panel (HAPPY BIRTHDAY).")]
        [SerializeField] private Transform _front;
        [Tooltip("The hinge of the inside panel (the invitation).")]
        [SerializeField] private Transform _inside;

        [Header("Angles (degrees around the fold)")]
        [SerializeField] private float _frontClosed = -8f;
        [SerializeField] private float _insideClosed = 188f;
        [SerializeField] private float _frontOpen = -25f;
        [SerializeField] private float _insideOpen = 25f;
        [SerializeField, Min(0.05f)] private float _openSeconds = 1.2f;

        [Header("When it opens")]
        [SerializeField] private string[] _openOnLabels = { "offered_alternative_connection", "motivated_small_step" };
        [Tooltip("End reasons of the AI server that open it.")]
        [SerializeField] private string[] _openOnEnd = { "resolved" };

        private float _amount;
        private float _target;

        public bool IsOpen => _target >= 1f;
        /// <summary>0 closed, 1 open.</summary>
        public float Amount => _amount;

        public event Action Opened;

        private void Awake() => Apply(0f);

        private void OnEnable()
        {
            if (_npc == null)
                return;
            _npc.TurnApplied += OnTurn;
            _npc.ConversationStarting += Close;
        }

        private void OnDisable()
        {
            if (_npc == null)
                return;
            _npc.TurnApplied -= OnTurn;
            _npc.ConversationStarting -= Close;
        }

        private void OnTurn(AiTurn turn)
        {
            if (turn == null || turn.localFallback || IsOpen)
                return;
            if (Matches(turn.labels, _openOnLabels) || (!string.IsNullOrEmpty(turn.ended) && Matches(new[] { turn.ended }, _openOnEnd)))
                Open();
        }

        private static bool Matches(string[] values, string[] wanted)
        {
            if (values == null || wanted == null)
                return false;
            foreach (var v in values)
                foreach (var w in wanted)
                    if (string.Equals(v, w, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        public void Open()
        {
            if (IsOpen)
                return;
            _target = 1f;
            Opened?.Invoke();
        }

        public void Close()
        {
            _target = 0f;
            Apply(_amount = 0f);
        }

        private void Update()
        {
            if (Mathf.Approximately(_amount, _target))
                return;
            _amount = Mathf.MoveTowards(_amount, _target, Time.deltaTime / _openSeconds);
            Apply(Mathf.SmoothStep(0f, 1f, _amount));
        }

        private void Apply(float t)
        {
            if (_front != null)
                _front.localRotation = Quaternion.Euler(0f, Mathf.Lerp(_frontClosed, _frontOpen, t), 0f);
            if (_inside != null)
                _inside.localRotation = Quaternion.Euler(0f, Mathf.Lerp(_insideClosed, _insideOpen, t), 0f);
        }
    }
}
