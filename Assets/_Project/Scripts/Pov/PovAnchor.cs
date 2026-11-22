using UnityEngine;

namespace ProCommVR.Pov
{
    /// <summary>
    /// Where the camera goes in the patient view: the patient's eyes, looking where the patient looks. Put it on the
    /// NPC. With a Humanoid Animator the position follows the Head bone at the moment of the switch (sitting, lying
    /// or standing) and the view follows the body's facing. Without one (e.g. a placeholder), the Eyes transform is used:
    /// a child of the body, so it follows the NPC's pose. Also knows the NPC's renderers, which are hidden in the
    /// patient view so the player does not see the inside of the head.
    /// </summary>
    public class PovAnchor : MonoBehaviour
    {
        [Tooltip("The NPC's body. Its renderers are hidden in the patient view. Empty: this object.")]
        [SerializeField] private Transform _body;

        [Tooltip("Optional. The NPC's Animator: the eyes are at its Head bone. Empty: the first Animator under Body.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Optional. The eyes of an NPC without a Humanoid rig: position and (horizontal) facing. Used when there is no Head bone.")]
        [SerializeField] private Transform _eyes;

        [Tooltip("From the Head bone to the eyes, in meters: forward and up, in the facing direction of the body.")]
        [SerializeField] private Vector2 _eyeOffset = new Vector2(0.09f, 0.07f);

        [Tooltip("Optional. Where the patient looks (e.g. the door for a patient lying in bed): the view faces this point. Empty: the body's facing.")]
        [SerializeField] private Transform _lookAt;

        [Tooltip("With Look At: from the Head bone to the eyes, in meters, straight up (a lying patient's eyes are above the head bone).")]
        [SerializeField] private float _lookAtEyeHeight = 0.1f;

        private Renderer[] _renderers;
        private bool[] _wasEnabled;

        public Transform Body => _body != null ? _body : transform;

        private void Awake()
        {
            if (_animator == null)
                _animator = Body.GetComponentInChildren<Animator>();
            _renderers = Body.GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// The patient's eyes now, and the horizontal direction the patient faces.
        /// </summary>
        public void GetEyes(out Vector3 position, out Vector3 forward)
        {
            var head = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var facing = head == null && _eyes != null ? _eyes : Body;
            if (_lookAt != null && head != null)
            {
                position = head.position + Vector3.up * _lookAtEyeHeight;
                forward = _lookAt.position - position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                    return;
                }
            }
            forward = facing.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            if (head != null)
                position = head.position + forward * _eyeOffset.x + Vector3.up * _eyeOffset.y;
            else
                position = _eyes != null ? _eyes.position : transform.position;
        }

        /// <summary>
        /// Hides or shows the NPC's renderers. Showing only turns back on what was on before hiding.
        /// </summary>
        public void SetBodyHidden(bool hidden)
        {
            if (_renderers == null)
                Awake();
            if (hidden)
            {
                _wasEnabled = new bool[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                {
                    _wasEnabled[i] = _renderers[i].enabled;
                    _renderers[i].enabled = false;
                }
            }
            else if (_wasEnabled != null)
            {
                for (var i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].enabled = _wasEnabled[i];
                }
                _wasEnabled = null;
            }
        }
    }
}
