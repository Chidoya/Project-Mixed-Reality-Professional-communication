using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Eenvoudige speler-rig voor Quest Link en de Editor zonder headset:
    /// Player (dit object, op de vloer) > Camera Offset > Main Camera (TrackedPoseDriver).
    /// Met headset: vloerhoogte (Floor) als het kan en MoveTo zet het hoofd van de speler op het kijkpunt
    /// (positie en kijkrichting), waar de speler in de kamer ook staat. Linker joystick indrukken = opnieuw centreren.
    /// Zonder headset: de camera neemt het kijkpunt exact over (hoogte en kanteling), zoals de oude losse camera.
    /// </summary>
    public class XrPlayerRig : MonoBehaviour
    {
        public const string RecenterBinding = "<XRController>{LeftHand}/{Primary2DAxisClick}";

        [SerializeField] private Transform _cameraOffset;
        [SerializeField] private Camera _camera;
        [Tooltip("Ooghoogte zonder headset en als de headset geen vloerhoogte geeft.")]
        [SerializeField] private float _eyeHeight = 1.62f;

        private static readonly List<XRInputSubsystem> Subsystems = new();
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private bool _hasTarget;
        private bool _floorTried;
        private bool _floor;
        private bool _centeredOnTracking;
        private InputAction _recenter;
        private Vector3 _startLocalPosition;
        private int _trackedFrames;

        public Camera Camera => _camera;
        public static bool HeadsetActive => XRSettings.isDeviceActive;

        public void Setup(Transform cameraOffset, Camera camera, float eyeHeight)
        {
            _cameraOffset = cameraOffset;
            _camera = camera;
            _eyeHeight = eyeHeight;
        }

        private void Awake()
        {
            if (_camera != null)
                _startLocalPosition = _camera.transform.localPosition;
        }

        private void OnEnable()
        {
            _recenter = new InputAction("Recenter", InputActionType.Button, RecenterBinding);
            _recenter.performed += _ => Recenter();
            _recenter.Enable();
        }

        private void OnDisable()
        {
            _recenter?.Dispose();
            _recenter = null;
        }

        private void Update()
        {
            if (!HeadsetActive)
                return;
            if (!_floorTried)
            {
#if UNITY_EDITOR
                // Met een headset kijk je niet naar het Unity-venster: controllers ook zonder focus van de Game view
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
                TryFloor();
            }
            // Pas als de headset echte posities geeft (de camera is verschoven) en die een paar frames stabiel is: centreren
            if (_centeredOnTracking || !_hasTarget || _camera == null)
                return;
            if (_camera.transform.localPosition != _startLocalPosition)
                _trackedFrames++;
            if (_trackedFrames >= 5)
            {
                _centeredOnTracking = true;
                Recenter();
            }
        }

        /// <summary>Zet de speler op een kijkpunt (oogpositie en kijkrichting, zoals een camera).</summary>
        public void MoveTo(Transform viewpoint)
        {
            if (viewpoint == null)
                return;
            _targetPosition = viewpoint.position;
            _targetRotation = viewpoint.rotation;
            _hasTarget = true;
            Recenter();
        }

        /// <summary>Hoofd van de speler opnieuw op het laatste kijkpunt.</summary>
        public void Recenter()
        {
            if (!_hasTarget || _camera == null)
                return;
            var floorY = _targetPosition.y - _eyeHeight;
            if (!HeadsetActive)
            {
                transform.SetPositionAndRotation(new Vector3(_targetPosition.x, floorY, _targetPosition.z),
                    Quaternion.Euler(0f, _targetRotation.eulerAngles.y, 0f));
                if (_cameraOffset != null)
                    _cameraOffset.localPosition = Vector3.zero;
                _camera.transform.SetPositionAndRotation(_targetPosition, _targetRotation);
                return;
            }
            if (_cameraOffset != null)
                _cameraOffset.localPosition = _floor ? Vector3.zero : new Vector3(0f, _eyeHeight, 0f);

            // Draaien om de y-as zodat de kijkrichting klopt, daarna verschuiven zodat het hoofd op het kijkpunt staat
            var head = _camera.transform;
            var headYaw = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.up);
            var wantedYaw = _targetRotation.eulerAngles.y;
            transform.Rotate(0f, Mathf.DeltaAngle(headYaw, wantedYaw), 0f, Space.World);
            var offset = new Vector3(_targetPosition.x - head.position.x, 0f, _targetPosition.z - head.position.z);
            transform.position = new Vector3(transform.position.x + offset.x, floorY, transform.position.z + offset.z);
        }

        private void TryFloor()
        {
            SubsystemManager.GetSubsystems(Subsystems);
            foreach (var s in Subsystems)
            {
                if (!s.running)
                    continue;
                _floorTried = true;
                _floor = (s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0
                         && s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                Debug.Log($"[XrPlayerRig] Headset actief, trackingmodus: {(_floor ? "vloer" : "toestel (vaste ooghoogte)")}.");
                Recenter();
                return;
            }
        }
    }
}
