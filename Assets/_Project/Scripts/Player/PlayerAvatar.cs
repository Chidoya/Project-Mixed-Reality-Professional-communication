using System;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ProCommVR.Player
{
    /// <summary>
    /// The nurse body of the player, seen from the outside (in the patient view). It lives in the XR rig prefab, but
    /// detaches from the rig when the scene starts, so it can stay behind when the rig moves elsewhere (the POV switch).
    /// <para>
    /// While <see cref="FollowHead"/> is on, it follows the XR camera every frame: under the head, turned with the
    /// head's yaw only, upright, feet on the floor of the XR Origin.
    /// </para>
    /// <para>
    /// The player never sees their own head: while the camera is within Hide Head Distance of the avatar's head, the
    /// Head bone is scaled to (almost) zero, which folds the head and hair away. The character is one skinned mesh,
    /// so a separate layer for the head is not possible. Further away (the patient view) the head is shown.
    /// </para>
    /// Hidden by default (<see cref="SetVisible"/>).
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        private static readonly Vector3 HiddenHeadScale = Vector3.one * 0.0001f;

        [Tooltip("Visible at the start. Off for the nurse view: only the patient view shows the avatar.")]
        [SerializeField] private bool _visible;

        [Tooltip("Follow the player's head. Turn off to let the avatar stay where it is (e.g. while in the patient view).")]
        [SerializeField] private bool _followHead = true;

        [Tooltip("How far the body stands behind the eyes, in meters (the eyes are in front of the neck).")]
        [SerializeField, Min(0f)] private float _bodyBehindHead = 0.1f;

        [Tooltip("Within this distance (meters) from the camera, the avatar's head is folded away.")]
        [SerializeField, Min(0f)] private float _hideHeadDistance = 0.6f;

        [Tooltip("The XR Origin to follow. Empty: the XR Origin this avatar is a child of in the rig prefab.")]
        [SerializeField] private XROrigin _origin;

        private Animator _animator;
        private Renderer[] _renderers;
        private Transform _head;
        private Vector3 _headScale = Vector3.one;
        private bool _appliedVisible;

        /// <summary>
        /// Raised with the new visibility when it changes.
        /// </summary>
        public event Action<bool> VisibilityChanged;

        public bool IsVisible => _visible;
        public bool IsHeadHidden { get; private set; }
        public XROrigin Origin => _origin;
        public Transform Head => _head;

        public bool FollowHead
        {
            get => _followHead;
            set => _followHead = value;
        }

        private void Awake()
        {
            if (_origin == null)
                _origin = GetComponentInParent<XROrigin>();
            _animator = GetComponentInChildren<Animator>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_animator != null && _animator.isHuman)
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (_head != null)
                _headScale = _head.localScale;

            // Leave the rig, so the avatar can stay behind when the rig moves.
            transform.SetParent(null, true);
            ApplyVisible();
        }

        private void LateUpdate()
        {
            if (!_visible)
                return;
            var camera = _origin != null ? _origin.Camera : null;
            if (camera == null)
                return;

            if (_followHead)
                FollowCamera(camera.transform);
            UpdateHead(camera.transform.position);
        }

        /// <summary>
        /// Shows or hides the avatar (renderers and animator).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;
            _visible = visible;
            ApplyVisible();
            if (visible)
                LateUpdate();
            VisibilityChanged?.Invoke(visible);
        }

        /// <summary>
        /// Puts the avatar under the player's head right now, also when it is hidden or Follow Head is off
        /// (e.g. just before it should stay behind where the player stood).
        /// </summary>
        public void PlaceAtHead()
        {
            var camera = _origin != null ? _origin.Camera : null;
            if (camera == null)
                return;
            FollowCamera(camera.transform);
            UpdateHead(camera.transform.position);
        }

        private void ApplyVisible()
        {
            _appliedVisible = _visible;
            foreach (var r in _renderers)
                r.enabled = _visible;
            if (_animator != null)
                _animator.enabled = _visible;
        }

        private void FollowCamera(Transform camera)
        {
            var forward = camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = camera.up; // looking straight down or up: the top of the head points forward
            forward.y = 0f;
            forward.Normalize();

            var position = camera.position - forward * _bodyBehindHead;
            position.y = _origin.transform.position.y;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void UpdateHead(Vector3 cameraPosition)
        {
            if (_head == null)
                return;
            // Measure with the head shown: the bone's position does not depend on its own scale.
            var hide = Vector3.Distance(cameraPosition, _head.position) < _hideHeadDistance;
            _head.localScale = hide ? HiddenHeadScale : _headScale;
            IsHeadHidden = hide;
        }

        private void OnValidate()
        {
            // The Visible checkbox in Play mode works like SetVisible (including the event).
            if (!Application.isPlaying || _renderers == null || _visible == _appliedVisible)
                return;
            var visible = _visible;
            _visible = _appliedVisible;
            SetVisible(visible);
        }
    }
}
