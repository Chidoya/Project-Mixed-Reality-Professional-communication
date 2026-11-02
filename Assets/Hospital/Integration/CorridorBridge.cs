using ProCommVR.Shift;
using UnityEngine;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// Taak 2 (gang van verdieping 3): als de countdown afloopt ("the gurney appears when the timer runs out") verschijnt
    /// de Porter en loopt hij achter de brancard die door de gang rijdt. Jalils countdown laat zijn eigen brancard al
    /// rijden; de Porter volgt die, zodat er geen tweede brancard bijkomt. Zonder brancard van Jalil rijdt de eigen
    /// Gurney (PassingCart) en volgt de Porter die.
    /// Animatie: "Walking" als de controller die state heeft, anders Idle.
    /// </summary>
    public class CorridorBridge : MonoBehaviour
    {
        private static readonly int WalkingState = Animator.StringToHash("Walking");
        private static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField] private TaskCountdown _countdown;
        [Tooltip("De brancard die rijdt (Jalils PathMover-brancard of de eigen Gurney).")]
        [SerializeField] private Transform _gurney;
        [Tooltip("Optioneel: de eigen Gurney met PassingCart, enkel als Jalil geen brancard heeft.")]
        [SerializeField] private PassingCart _ownGurney;
        [SerializeField] private Animator _porter;
        [Tooltip("Afstand tussen het midden van de brancard en de Porter erachter.")]
        [SerializeField] private float _followDistance = 1.5f;

        private bool _following;
        private bool _walking;
        private Vector3 _lastGurneyPosition;
        private Vector3 _direction;

        public void Configure(TaskCountdown countdown, Transform gurney, PassingCart ownGurney, Animator porter, float followDistance)
        {
            _countdown = countdown;
            _gurney = gurney;
            _ownGurney = ownGurney;
            _porter = porter;
            _followDistance = followDistance;
        }

        private void OnEnable()
        {
            if (_countdown != null)
                _countdown.Expired += OnExpired;
        }

        private void OnDisable()
        {
            if (_countdown != null)
                _countdown.Expired -= OnExpired;
        }

        private void Start()
        {
            if (_porter != null)
                _porter.gameObject.SetActive(false);
        }

        private void OnExpired(TaskCountdown countdown)
        {
            if (_porter == null || _gurney == null)
                return;
            if (_ownGurney != null)
                _ownGurney.Play();
            _lastGurneyPosition = _gurney.position;
            _direction = Vector3.ProjectOnPlane(_gurney.forward, Vector3.up).normalized;
            _porter.gameObject.SetActive(true);
            _following = true;
            Follow();
        }

        private void LateUpdate()
        {
            if (_following)
                Follow();
        }

        private void Follow()
        {
            var moved = Vector3.ProjectOnPlane(_gurney.position - _lastGurneyPosition, Vector3.up);
            var moving = moved.sqrMagnitude > 0.000001f;
            if (moving)
                _direction = moved.normalized;
            _lastGurneyPosition = _gurney.position;

            // De oorsprong van de brancard en van de Porter liggen allebei op de vloer
            _porter.transform.SetPositionAndRotation(_gurney.position - _direction * _followDistance, Quaternion.LookRotation(_direction, Vector3.up));
            SetWalking(moving);
        }

        private void SetWalking(bool walking)
        {
            if (walking == _walking)
                return;
            _walking = walking;
            var state = walking && _porter.HasState(0, WalkingState) ? WalkingState : IdleState;
            if (_porter.HasState(0, state))
                _porter.Play(state, 0, 0f);
        }
    }
}
