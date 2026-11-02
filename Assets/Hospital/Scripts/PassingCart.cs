using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Laat een kar (Supply_Cart, Gurney, ...) over een pad van punten rijden, bv. door de gang. Play() start vanaf het
    /// eerste punt. De kar draait enkel rond de wereld-y; welke kant "vooruit" is, staat in de lokale assen van de prop-root,
    /// zodat de rotatie uit de FBX-conversie behouden blijft. Bij de props (root 90 graden rond X) is lokaal:
    /// voorkant (0, -1, 0), boven (0, 0, -1), Blender +X = (1, 0, 0).
    /// </summary>
    public class PassingCart : MonoBehaviour
    {
        [SerializeField] private Transform _cart;
        [SerializeField] private Transform[] _waypoints = new Transform[0];
        [SerializeField] private float _speed = 1f;
        [SerializeField] private float _turnSpeed = 180f;
        [Tooltip("Lokale as van de prop-root die vooruit rijdt. Supply_Cart: (-1, 0, 0), handvat achteraan. Gurney: (1, 0, 0), hoofdeinde voorop.")]
        [SerializeField] private Vector3 _localForward = new Vector3(0f, -1f, 0f);
        [Tooltip("Lokale as van de prop-root die naar boven wijst; voor de props (0, 0, -1).")]
        [SerializeField] private Vector3 _localUp = new Vector3(0f, 0f, -1f);
        [SerializeField] private bool _loop;
        [SerializeField] private bool _playOnStart;

        private int _target = -1;

        public bool IsPlaying => _target >= 0;

        public void Configure(Transform cart, Transform[] waypoints, Vector3 localForward, float speed)
        {
            _cart = cart;
            _waypoints = waypoints;
            _localForward = localForward;
            _localUp = Quaternion.Inverse(cart.rotation) * Vector3.up;   // gemeten op de geplaatste prop
            _speed = speed;
            ResetToStart();
        }

        private void Start()
        {
            if (_playOnStart)
                Play();
        }

        /// <summary>Zet de kar op het eerste punt, gericht naar het tweede, en laat hem rijden.</summary>
        public void Play()
        {
            ResetToStart();
            _target = _waypoints.Length > 1 ? 1 : -1;
        }

        public void Stop() => _target = -1;

        public void ResetToStart()
        {
            _target = -1;
            if (_cart == null || _waypoints.Length == 0)
                return;
            _cart.position = _waypoints[0].position;
            if (_waypoints.Length > 1)
                _cart.rotation = Facing(_waypoints[1].position - _waypoints[0].position);
        }

        private void Update()
        {
            if (_target < 0 || _cart == null)
                return;
            var goal = _waypoints[_target].position;
            var step = _speed * Time.deltaTime;
            var toGoal = goal - _cart.position;
            if (Vector3.ProjectOnPlane(toGoal, Vector3.up).sqrMagnitude > 0.0001f)
                _cart.rotation = Quaternion.RotateTowards(_cart.rotation, Facing(toGoal), _turnSpeed * Time.deltaTime);
            _cart.position = Vector3.MoveTowards(_cart.position, goal, step);
            if ((_cart.position - goal).sqrMagnitude > 0.0001f)
                return;
            _target++;
            if (_target >= _waypoints.Length)
                _target = _loop ? 0 : -1;
        }

        /// <summary>Rotatie waarbij de lokale vooruit-as van de prop naar `direction` wijst en de lokale boven-as naar boven.</summary>
        private Quaternion Facing(Vector3 direction)
        {
            var flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                return _cart.rotation;
            return Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(_localForward, _localUp));
        }
    }
}
