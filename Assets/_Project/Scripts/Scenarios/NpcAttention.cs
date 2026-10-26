using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// What a character does when the player comes near during a conversation, before any choice: e.g. the family
    /// member at the bedside stands up and reaches toward the nurse, or the patient in the next bed lowers his
    /// newspaper and starts listening. Moves the body to the Attention Pose, plays an Animator state and swaps
    /// objects (Show / Hide). Everything goes back when a scenario starts; after a choice nothing new is triggered.
    /// </summary>
    public class NpcAttention : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("The character that moves. Leave empty to use this object.")]
        [SerializeField] private Transform _body;

        [Tooltip("Horizontal distance (meters) between the player's head and the body within which it pays attention.")]
        [SerializeField, Min(0f)] private float _range = 2.5f;

        [Tooltip("Height of the player's head above the body's pivot (meters, min and max), so a player on another floor does not count.")]
        [SerializeField] private Vector2 _headHeightRange = new Vector2(-0.5f, 2.5f);

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        [Header("Attention")]
        [Tooltip("Where the body moves to (position and rotation). Empty: it stays where it is.")]
        [SerializeField] private Transform _attentionPose;

        [Tooltip("Turn the body toward the player instead of to the rotation of the Attention Pose.")]
        [SerializeField] private bool _facePlayer;

        [Tooltip("Optional. Animator state to play, e.g. \"Idle\" to stand up or \"Angry\".")]
        [SerializeField] private string _animatorState;

        [SerializeField] private Animator _animator;

        [Tooltip("Objects shown when paying attention, e.g. the lowered newspaper.")]
        [SerializeField] private GameObject[] _show = Array.Empty<GameObject>();

        [Tooltip("Objects hidden when paying attention, e.g. the newspaper held up to read.")]
        [SerializeField] private GameObject[] _hide = Array.Empty<GameObject>();

        [Tooltip("Time to move into the attention pose, in seconds.")]
        [SerializeField, Min(0f)] private float _duration = 0.8f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private string _startState;
        private bool _armed;
        private Coroutine _move;

        public bool IsAttending { get; private set; }

        public event Action<NpcAttention> Attended;

        private Transform Body => _body != null ? _body : transform;

        private Transform Player
        {
            get
            {
                if (_player == null && Camera.main != null)
                    _player = Camera.main.transform;
                return _player;
            }
        }

        private void Awake()
        {
            _startPosition = Body.localPosition;
            _startRotation = Body.localRotation;
            var state = _animator != null ? _animator.GetComponent<NpcAnimationState>() : null;
            _startState = state != null ? state.StateName : null;
        }

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.OptionChosen += OnOptionChosen;
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.OptionChosen -= OnOptionChosen;
        }

        private void Update()
        {
            if (_armed && !IsAttending && PlayerInRange())
                Attend();
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            if (_move != null)
                StopCoroutine(_move);
            _move = null;
            if (IsAttending)
            {
                Body.SetLocalPositionAndRotation(_startPosition, _startRotation);
                SetObjects(false);
                if (_animator != null && !string.IsNullOrEmpty(_startState))
                    _animator.Play(_startState);
            }
            IsAttending = false;
            _armed = true;
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            _armed = false;
        }

        /// <summary>
        /// Pays attention now, e.g. from a test or another script.
        /// </summary>
        public void Attend()
        {
            if (IsAttending)
                return;
            IsAttending = true;
            SetObjects(true);

            var position = _attentionPose != null ? _attentionPose.position : Body.position;
            var rotation = _attentionPose != null ? _attentionPose.rotation : Body.rotation;
            if (_facePlayer && Player != null)
            {
                var look = Player.position - position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    rotation = Quaternion.LookRotation(look);
            }
            _move = StartCoroutine(MoveTo(position, rotation));

            if (_animator != null && !string.IsNullOrEmpty(_animatorState))
            {
                var state = Animator.StringToHash(_animatorState);
                if (_animator.HasState(0, state))
                    _animator.CrossFadeInFixedTime(state, 0.25f);
                else
                    Debug.LogWarning($"[ProCommVR] Animator of '{_animator.name}' has no state '{_animatorState}'.", this);
            }
            Attended?.Invoke(this);
        }

        private void SetObjects(bool attending)
        {
            foreach (var go in _show)
            {
                if (go != null)
                    go.SetActive(attending);
            }
            foreach (var go in _hide)
            {
                if (go != null)
                    go.SetActive(!attending);
            }
        }

        private bool PlayerInRange()
        {
            if (Player == null)
                return false;
            var offset = Player.position - Body.position;
            var height = offset.y;
            offset.y = 0f;
            return offset.magnitude <= _range && height >= _headHeightRange.x && height <= _headHeightRange.y;
        }

        private IEnumerator MoveTo(Vector3 position, Quaternion rotation)
        {
            var fromPosition = Body.position;
            var fromRotation = Body.rotation;
            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(_duration, 0.0001f))
            {
                var eased = Mathf.SmoothStep(0f, 1f, t);
                Body.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, eased), Quaternion.Slerp(fromRotation, rotation, eased));
                yield return null;
            }
            Body.SetPositionAndRotation(position, rotation);
            _move = null;
        }
    }
}
