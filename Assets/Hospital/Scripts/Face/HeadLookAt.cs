using UnityEngine;

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Draait het hoofd (en een beetje de nek) naar de speler, bovenop de animatie (LateUpdate).
    /// Max 60 graden links/rechts en 25 graden op/neer, alleen binnen 3 m. Ligt het personage
    /// (state Laying), dan max 30 graden. Modus AvoidGaze kijkt weg en iets naar beneden.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class HeadLookAt : MonoBehaviour
    {
        public enum LookMode
        {
            LookAtPlayer,
            AvoidGaze,
        }

        [SerializeField] private LookMode _mode = LookMode.LookAtPlayer;
        [SerializeField, Range(0f, 1f)] private float _weight = 1f;
        [Tooltip("Leeg = Camera.main (de headset).")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _range = 3f;
        [SerializeField] private float _maxYaw = 60f;
        [SerializeField] private float _maxPitch = 25f;
        [SerializeField] private float _layingMaxYaw = 30f;
        [SerializeField] private string _layingState = "Laying";
        [SerializeField] private float _smoothTime = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _neckShare = 0.35f;

        // Kijkrichting in de ruimte van het Head-bot, uit de bind pose van de mesh (CharacterSetup -> Bake).
        // De bot-oriëntatie zelf is onbetrouwbaar: Humanoid-retargeting draait het bot rond zijn as.
        [SerializeField, HideInInspector] private Vector3 _localForward;
        [SerializeField, HideInInspector] private Vector3 _localUp;

        private Animator _animator;
        private Transform _head, _neck;
        private float _yaw, _pitch, _yawVel, _pitchVel;
        private int _layingHash;
        private bool _wrote;
        private Quaternion _headBase, _headWritten, _neckBase, _neckWritten;

        public LookMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public float Weight
        {
            get => _weight;
            set => _weight = Mathf.Clamp01(value);
        }

        /// <summary>Hoek naar het doel (graden) van de laatste frame, voor tests.</summary>
        public float LastTargetYaw { get; private set; }

        public float CurrentYaw => _yaw;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                enabled = false;
                return;
            }
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            if (_head == null)
            {
                enabled = false;
                return;
            }
            if (_localForward.sqrMagnitude < 0.5f && !Bake())
            {
                // Laatste uitweg: rustpose vóór de eerste animatieframe
                var inv = Quaternion.Inverse(_head.rotation);
                _localForward = inv * transform.forward;
                _localUp = inv * transform.up;
            }
            _layingHash = Animator.StringToHash(_layingState);
        }

        /// <summary>
        /// Berekent de kijkrichting van het gezicht in de ruimte van het Head-bot uit de bind pose
        /// (zelfde relatie die de skinning gebruikt). Roep aan in de editor; geeft false als het niet lukt.
        /// </summary>
        public bool Bake()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
                return false;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (head == null || neck == null)
                return false;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || mesh.GetBlendShapeIndex("Blink") < 0)
                    continue;
                // Midden van de ogen (vertices die Blink verschuift), in wereldruimte van de rustpose
                var verts = mesh.vertices;
                if (verts.Length != mesh.vertexCount)
                    return false;
                var deltas = new Vector3[verts.Length];
                mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex("Blink"), 0, deltas,
                    new Vector3[verts.Length], new Vector3[verts.Length]);
                var sum = Vector3.zero;
                var n = 0;
                for (var i = 0; i < verts.Length; i++)
                {
                    if (deltas[i].sqrMagnitude < 1e-8f)
                        continue;
                    sum += verts[i];
                    n++;
                }
                if (n == 0)
                    return false;
                var eyes = smr.transform.TransformPoint(sum / n);
                var up = (head.position - neck.position).normalized;
                var fwd = Vector3.ProjectOnPlane(eyes - head.position, up).normalized;
                _localForward = head.InverseTransformDirection(fwd);
                _localUp = head.InverseTransformDirection(up);
                return _localForward.sqrMagnitude > 0.5f;
            }
            return false;
        }

        private void LateUpdate() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (_head == null)
                return;

            // Schreef de Animator deze frame niets (bv. gecullt), zet dan eerst onze vorige draaiing terug,
            // anders stapelt ze zich op.
            if (_wrote)
            {
                // Eerst beide vergelijken: de nek terugzetten verdraait ook het hoofd (kind)
                var headSame = Quaternion.Angle(_head.rotation, _headWritten) < 0.01f;
                var neckSame = _neck != null && Quaternion.Angle(_neck.rotation, _neckWritten) < 0.01f;
                if (neckSame)
                    _neck.rotation = _neckBase;
                if (headSame)
                    _head.rotation = _headBase;
            }

            var fwd = _head.rotation * _localForward;
            var up = _head.rotation * _localUp;
            var right = Vector3.Cross(up, fwd).normalized;

            float wantYaw = 0f, wantPitch = 0f;
            var target = _target != null ? _target : (Camera.main != null ? Camera.main.transform : null);
            if (target != null && _weight > 0f)
            {
                var d = target.position - _head.position;
                var flat = Vector3.ProjectOnPlane(d, up);
                var yaw = Vector3.SignedAngle(fwd, flat, up);
                var pitch = Mathf.Atan2(Vector3.Dot(d, up), flat.magnitude) * Mathf.Rad2Deg;
                LastTargetYaw = yaw;
                var laying = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _layingHash;
                var maxYaw = laying ? _layingMaxYaw : _maxYaw;

                if (_mode == LookMode.AvoidGaze)
                {
                    if (d.magnitude < _range * 2f)
                    {
                        wantYaw = -(yaw >= 0f ? 1f : -1f) * Mathf.Min(40f, maxYaw);
                        wantPitch = -15f;
                    }
                }
                else if (d.magnitude <= _range && Mathf.Abs(yaw) < 110f)
                {
                    wantYaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
                    wantPitch = Mathf.Clamp(pitch, -_maxPitch, _maxPitch);
                }
            }

            if (dt > 0f)
            {
                _yaw = Mathf.SmoothDamp(_yaw, wantYaw, ref _yawVel, _smoothTime, Mathf.Infinity, dt);
                _pitch = Mathf.SmoothDamp(_pitch, wantPitch, ref _pitchVel, _smoothTime, Mathf.Infinity, dt);
            }
            else
            {
                _yaw = wantYaw;
                _pitch = wantPitch;
            }

            var q = Quaternion.AngleAxis(_yaw * _weight, up) * Quaternion.AngleAxis(-_pitch * _weight, right);
            var headRot = _head.rotation;
            if (_neck != null)
            {
                _neckBase = _neck.rotation;
                if (_neckShare > 0f)
                    _neck.rotation = Quaternion.Slerp(Quaternion.identity, q, _neckShare) * _neck.rotation;
                _neckWritten = _neck.rotation;
            }
            _headBase = headRot;
            _head.rotation = q * headRot;
            _headWritten = _head.rotation;
            _wrote = true;
        }
    }
}
