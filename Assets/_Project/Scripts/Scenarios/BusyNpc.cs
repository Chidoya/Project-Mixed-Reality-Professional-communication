using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    public enum BusyState
    {
        Idle,
        Rushing,
        GoingToDesk,
        AtDesk,
        LeavingDesk,
        Engaged
    }

    /// <summary>
    /// An NPC who is busy and can only be spoken to at the right moment, e.g. the doctor in Scenario 3. While busy
    /// he rushes along a round (between the rooms), the presenter holds the options and his bubble shows the
    /// scenario's Busy Line ("I don't have time right now."); when the player comes near he stops for a moment,
    /// turns to the player and gestures, then hurries on. After Busy Time he walks to his desk to review paperwork:
    /// now he is reachable, the bubble shows the NPC Line and the options appear once the player is within range.
    /// If the player does not come within Desk Time, he leaves for another round. Once the options are shown he stays
    /// (Engaged) and turns to the player. Not a navigation AI: he walks straight lines between waypoints.
    /// </summary>
    public class BusyNpc : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("The NPC root that walks (with its bubble anchor and pose targets as children). Leave empty to use this object.")]
        [SerializeField] private Transform _root;

        [Tooltip("The round while busy, walked in a loop (e.g. from door to door). The NPC starts at the first point.")]
        [SerializeField] private Transform[] _round = Array.Empty<Transform>();

        [Tooltip("From the round to the desk; the last point is where he stands at the desk (its rotation: facing the desk).")]
        [SerializeField] private Transform[] _toDesk = Array.Empty<Transform>();

        [Tooltip("Meters per second.")]
        [SerializeField, Min(0.1f)] private float _speed = 1.5f;

        [Tooltip("Seconds on the round before he goes to his desk.")]
        [SerializeField, Min(0f)] private float _busyTime = 14f;

        [Tooltip("Seconds at the desk before he leaves again, unless the player comes to talk.")]
        [SerializeField, Min(0f)] private float _deskTime = 20f;

        [Header("While busy")]
        [Tooltip("When the player comes this close (meters) on the round, he stops briefly and turns to the player.")]
        [SerializeField, Min(0f)] private float _pauseRange = 1.8f;

        [SerializeField, Min(0f)] private float _pauseTime = 2.5f;

        [Header("Animation (optional)")]
        [SerializeField] private Animator _animator;

        [Tooltip("State while walking and at the desk.")]
        [SerializeField] private string _walkState = "Idle";

        [Tooltip("State while he stops to say he has no time, and once he talks with the player.")]
        [SerializeField] private string _talkState = "Talking";

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Coroutine _cycle;
        private int _roundIndex;
        private bool _pausedForPlayer;

        public BusyState State { get; private set; }

        /// <summary>
        /// True while the NPC stops on his round because the player came near.
        /// </summary>
        public bool IsPausedForPlayer => _pausedForPlayer;

        public event Action<BusyNpc> StateChanged;

        private Transform Root => _root != null ? _root : transform;

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
            _startPosition = Root.position;
            _startRotation = Root.rotation;
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

        private void OnScenarioStarted(Scenario scenario)
        {
            if (_cycle != null)
                StopCoroutine(_cycle);
            Root.SetPositionAndRotation(_startPosition, _startRotation);
            _roundIndex = 0;
            _pausedForPlayer = false;
            _cycle = StartCoroutine(Cycle());
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            Engage();
        }

        private IEnumerator Cycle()
        {
            while (true)
            {
                SetBusy(true);
                SetState(BusyState.Rushing);
                for (var time = 0f; time < _busyTime || _pausedForPlayer;)
                {
                    if (_round.Length == 0)
                    {
                        time += Time.deltaTime;
                        yield return null;
                        continue;
                    }
                    var target = _round[_roundIndex].position;
                    while (!Reached(target))
                    {
                        if (!_pausedForPlayer && PlayerWithin(_pauseRange))
                            yield return PauseForPlayer();
                        Step(target);
                        time += Time.deltaTime;
                        yield return null;
                    }
                    _roundIndex = (_roundIndex + 1) % _round.Length;
                }

                SetState(BusyState.GoingToDesk);
                for (var i = 0; i < _toDesk.Length; i++)
                {
                    while (!Reached(_toDesk[i].position))
                    {
                        Step(_toDesk[i].position);
                        yield return null;
                    }
                }
                if (_toDesk.Length > 0)
                    yield return TurnTo(_toDesk[_toDesk.Length - 1].rotation);

                // At the desk: reachable now.
                SetBusy(false);
                SetState(BusyState.AtDesk);
                for (var time = 0f; time < _deskTime; time += Time.deltaTime)
                {
                    if (_presenter.OptionsVisible)
                    {
                        Engage();
                        yield break;
                    }
                    yield return null;
                }

                SetState(BusyState.LeavingDesk);
                SetBusy(true);
                for (var i = _toDesk.Length - 2; i >= 0; i--)
                {
                    while (!Reached(_toDesk[i].position))
                    {
                        Step(_toDesk[i].position);
                        yield return null;
                    }
                }
            }
        }

        /// <summary>
        /// Stops the cycle and turns to the player: the conversation goes on, the options stay available.
        /// </summary>
        private void Engage()
        {
            if (State == BusyState.Engaged)
                return;
            if (_cycle != null)
                StopCoroutine(_cycle);
            _cycle = null;
            _pausedForPlayer = false;
            SetBusy(false);
            SetState(BusyState.Engaged);
            if (Player != null)
                StartCoroutine(TurnTo(LookAtPlayer()));
            Play(_talkState);
        }

        private IEnumerator PauseForPlayer()
        {
            _pausedForPlayer = true;
            Play(_talkState);
            yield return TurnTo(LookAtPlayer());
            yield return new WaitForSeconds(_pauseTime);
            Play(_walkState);
            _pausedForPlayer = false;
            // Wait until the player steps aside or he has passed, so he doesn't stop again right away.
            for (var time = 0f; time < 3f && PlayerWithin(_pauseRange); time += Time.deltaTime)
            {
                Step(_round[_roundIndex].position);
                yield return null;
            }
        }

        private void SetBusy(bool busy)
        {
            _presenter.HoldChoices = busy;
            var scenario = _presenter.CurrentScenario;
            if (scenario != null)
                _presenter.Say(busy && !string.IsNullOrWhiteSpace(scenario.BusyLine) ? scenario.BusyLine : scenario.NpcLine);
            if (busy)
                Play(_walkState);
        }

        private void SetState(BusyState state)
        {
            State = state;
            StateChanged?.Invoke(this);
        }

        private void Step(Vector3 target)
        {
            var direction = target - Root.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                Root.rotation = Quaternion.RotateTowards(Root.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
            Root.position = Vector3.MoveTowards(Root.position, target, _speed * Time.deltaTime);
        }

        private bool Reached(Vector3 target)
        {
            return (Root.position - target).sqrMagnitude < 0.0001f;
        }

        private IEnumerator TurnTo(Quaternion rotation)
        {
            while (Quaternion.Angle(Root.rotation, rotation) > 0.5f)
            {
                Root.rotation = Quaternion.RotateTowards(Root.rotation, rotation, 300f * Time.deltaTime);
                yield return null;
            }
            Root.rotation = rotation;
        }

        private Quaternion LookAtPlayer()
        {
            if (Player == null)
                return Root.rotation;
            var look = Player.position - Root.position;
            look.y = 0f;
            return look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Root.rotation;
        }

        private bool PlayerWithin(float range)
        {
            if (Player == null)
                return false;
            var offset = Player.position - Root.position;
            var height = offset.y;
            offset.y = 0f;
            return offset.magnitude <= range && height > -0.5f && height < 2.5f;
        }

        private void Play(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state))
                return;
            var hash = Animator.StringToHash(state);
            if (_animator.HasState(0, hash))
                _animator.CrossFadeInFixedTime(hash, 0.25f);
            else
                Debug.LogWarning($"[ProCommVR] Animator of '{_animator.name}' has no state '{state}'.", this);
        }
    }
}
