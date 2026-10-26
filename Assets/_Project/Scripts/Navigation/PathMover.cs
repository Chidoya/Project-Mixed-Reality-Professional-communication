using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Navigation
{
    /// <summary>
    /// Moves this object along a list of waypoints at walking speed, turned in the direction it moves, and ends in
    /// the rotation of the last waypoint. Used for the surgical gurney that arrives when a countdown runs out and
    /// for an NPC who walks to the place where the conversation continues. The waypoints must not be children of
    /// this object.
    /// </summary>
    public class PathMover : MonoBehaviour
    {
        [SerializeField] private Transform[] _waypoints = Array.Empty<Transform>();

        [Tooltip("Meters per second.")]
        [SerializeField, Min(0.1f)] private float _speed = 1.2f;

        [Tooltip("Hide this object until Play is called, e.g. a gurney that only appears when the time is up.")]
        [SerializeField] private bool _hideUntilPlayed;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Coroutine _move;

        public bool IsMoving => _move != null;

        /// <summary>
        /// True once the object has reached the last waypoint.
        /// </summary>
        public bool HasArrived { get; private set; }

        public event Action<PathMover> Arrived;

        private void Awake()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            if (_hideUntilPlayed)
                gameObject.SetActive(false);
        }

        public void Play()
        {
            gameObject.SetActive(true);
            if (_move != null)
                StopCoroutine(_move);
            HasArrived = false;
            _move = StartCoroutine(Move());
        }

        /// <summary>
        /// Back to where the object stood when the scene started (and hidden again, with Hide Until Played).
        /// </summary>
        public void ResetToStart()
        {
            if (_move != null)
                StopCoroutine(_move);
            _move = null;
            HasArrived = false;
            transform.SetPositionAndRotation(_startPosition, _startRotation);
            if (_hideUntilPlayed)
                gameObject.SetActive(false);
        }

        private IEnumerator Move()
        {
            foreach (var waypoint in _waypoints)
            {
                if (waypoint == null)
                    continue;
                while ((transform.position - waypoint.position).sqrMagnitude > 0.0001f)
                {
                    var direction = waypoint.position - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
                    transform.position = Vector3.MoveTowards(transform.position, waypoint.position, _speed * Time.deltaTime);
                    yield return null;
                }
            }

            var last = _waypoints.Length > 0 ? _waypoints[_waypoints.Length - 1] : null;
            if (last != null)
            {
                while (Quaternion.Angle(transform.rotation, last.rotation) > 0.5f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, last.rotation, 240f * Time.deltaTime);
                    yield return null;
                }
                transform.rotation = last.rotation;
            }
            _move = null;
            HasArrived = true;
            Arrived?.Invoke(this);
        }
    }
}
