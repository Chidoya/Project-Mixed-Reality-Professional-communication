using System;
using ProCommVR.Shift;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProCommVR.Hospital.Integration
{
    /// <summary>
    /// 360°-afleiding tijdens de shift: om de 60 tot 90 s rijdt de Supply_Cart door een willekeurige gang, in een
    /// willekeurige richting. Tussen de ritten is de kar verborgen. Stopt bij ShiftEnded.
    /// </summary>
    public class AmbientBridge : MonoBehaviour
    {
        [Serializable]
        public class CorridorPath
        {
            public string Name;
            public Transform[] Waypoints = Array.Empty<Transform>();
        }

        [SerializeField] private ShiftManager _shift;
        [SerializeField] private PassingCart _driver;
        [SerializeField] private Transform _cart;
        [SerializeField] private CorridorPath[] _corridors = Array.Empty<CorridorPath>();
        [SerializeField] private Vector2 _interval = new Vector2(60f, 90f);
        [SerializeField] private float _speed = 1f;
        [Tooltip("Lokale as van de prop-root die vooruit rijdt: de Supply_Cart rijdt met het handvat (Blender +X) achteraan.")]
        [SerializeField] private Vector3 _localForward = new Vector3(-1f, 0f, 0f);

        private bool _running;
        private bool _riding;
        private float _nextRide;

        public void Configure(ShiftManager shift, PassingCart driver, Transform cart, CorridorPath[] corridors)
        {
            _shift = shift;
            _driver = driver;
            _cart = cart;
            _corridors = corridors;
        }

        private void OnEnable()
        {
            if (_shift == null)
                return;
            _shift.ShiftStarted += OnShiftStarted;
            _shift.ShiftEnded += OnShiftEnded;
        }

        private void OnDisable()
        {
            if (_shift == null)
                return;
            _shift.ShiftStarted -= OnShiftStarted;
            _shift.ShiftEnded -= OnShiftEnded;
        }

        private void Start()
        {
            if (_cart != null)
                _cart.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_riding && !_driver.IsPlaying)
            {
                _riding = false;
                _cart.gameObject.SetActive(false);
                _nextRide = Time.time + Random.Range(_interval.x, _interval.y);
            }
            if (_running && !_riding && Time.time >= _nextRide)
                Ride();
        }

        /// <summary>Start meteen een rit (ook voor tests).</summary>
        public void Ride()
        {
            if (_driver == null || _cart == null || _corridors.Length == 0)
                return;
            var corridor = _corridors[Random.Range(0, _corridors.Length)];
            var points = (Transform[])corridor.Waypoints.Clone();
            if (Random.value < 0.5f)
                Array.Reverse(points);
            _cart.gameObject.SetActive(true);
            _driver.Configure(_cart, points, _localForward, _speed);
            _driver.Play();
            _riding = true;
        }

        private void OnShiftStarted()
        {
            _running = true;
            _nextRide = Time.time + Random.Range(_interval.x, _interval.y);
        }

        private void OnShiftEnded(ShiftEndReason reason)
        {
            _running = false;
            if (_driver != null)
                _driver.Stop();
            _riding = false;
            if (_cart != null)
                _cart.gameObject.SetActive(false);
        }
    }
}
