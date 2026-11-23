using ProCommVR.Hospital.Face;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// A simple procedural pose on top of the animation (no new art): the doctor points with a straight arm at a target
    /// (a sticky note, an object on the table), with the arm on the side of the target, and her head turns to it. Weight
    /// blends in and out. Runs after the Animator and after HeadLookAt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public class DoctorPointer : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private HeadLookAt _look;
        [SerializeField, Min(0.05f)] private float _blendSeconds = 0.35f;
        [Tooltip("How far the arm points below the target (degrees): a relaxed point, not a salute.")]
        [SerializeField] private float _lowerBy = 8f;

        private Transform _target;
        private float _weight;
        private Transform _rightUpper, _rightLower, _rightHand, _leftUpper, _leftLower, _leftHand;
        private Transform _savedLookTarget;
        private bool _lookTaken;

        /// <summary>Pointing (or blending to it).</summary>
        public bool IsPointing => _target != null;
        public float Weight => _weight;
        public Transform Target => _target;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_look == null) _look = GetComponentInChildren<HeadLookAt>();
            if (_animator == null || !_animator.isHuman)
            {
                enabled = false;
                return;
            }
            _rightUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        /// <summary>Points at the target (null: stop pointing). Her head looks at it while she points.</summary>
        public void PointAt(Transform target)
        {
            _target = target;
            if (_look == null)
                return;
            if (target != null)
            {
                if (!_lookTaken)
                {
                    _savedLookTarget = _look.Target;
                    _lookTaken = true;
                }
                _look.Target = target;
                _look.Mode = HeadLookAt.LookMode.LookAtPlayer;
            }
            else if (_lookTaken)
            {
                _look.Target = _savedLookTarget;
                _lookTaken = false;
            }
        }

        public void Stop() => PointAt(null);

        private void LateUpdate()
        {
            var goal = _target != null ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, goal, Time.deltaTime / _blendSeconds);
            if (_weight <= 0f || _target == null && _lastTargetPosition == null)
                return;
            var targetPosition = _target != null ? _target.position : _lastTargetPosition.Value;
            if (_target != null)
                _lastTargetPosition = targetPosition;
            else if (_weight <= 0f)
                _lastTargetPosition = null;

            // The arm on the side of the target.
            var right = Vector3.Dot(targetPosition - transform.position, transform.right) >= 0f;
            var upper = right ? _rightUpper : _leftUpper;
            var lower = right ? _rightLower : _leftLower;
            var hand = right ? _rightHand : _leftHand;
            if (upper == null || lower == null || hand == null)
                return;
            var shoulder = upper.position;
            var direction = (targetPosition - shoulder).normalized;
            var side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude > 0.0001f)
                direction = Quaternion.AngleAxis(_lowerBy, side.normalized) * direction;

            Aim(upper, lower.position - upper.position, direction);
            Aim(lower, hand.position - lower.position, direction);
        }

        private Vector3? _lastTargetPosition;

        private void Aim(Transform bone, Vector3 current, Vector3 wanted)
        {
            if (current.sqrMagnitude < 0.000001f)
                return;
            var full = Quaternion.FromToRotation(current.normalized, wanted) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, full, _weight);
        }
    }
}
