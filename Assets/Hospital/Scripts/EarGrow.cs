using System.Linq;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Case 5: de buurman luistert mee. Zijn oor wordt langzaam groter (schaal 1 tot MaxScale) en zijn krant zakt.
    /// Het oor is een klein apart object aan het hoofdbot ("Ear_Grow"), in de huidskleur van het personage;
    /// het wordt bij de start aangemaakt als het nog niet bestaat. Werkt op elk Humanoid-personage.
    /// </summary>
    public class EarGrow : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [Tooltip("Optioneel: de krant (of een houder ervan) die zakt en kantelt.")]
        [SerializeField] private Transform _newspaper;
        [Tooltip("Rechteroor (gezien vanuit het personage), anders het linker.")]
        [SerializeField] private bool _rightEar = true;
        [SerializeField] private float _maxScale = 2.5f;
        [Tooltip("Dikte (naar buiten), hoogte en breedte van het oor op schaal 1, in meter.")]
        [SerializeField] private Vector3 _earSize = new Vector3(0.03f, 0.06f, 0.045f);
        [Tooltip("Plek van de binnenkant van het oor vanaf het hoofdbot: opzij, omhoog (meter, in bind pose).")]
        [SerializeField] private Vector2 _earOffset = new Vector2(0.085f, 0.07f);
        [SerializeField] private float _newspaperDrop = 0.25f;
        [SerializeField] private float _newspaperTilt = 40f;

        private Transform _ear;
        private Vector3 _paperPosition;
        private Quaternion _paperRotation;
        private float _amount;
        private float _from;
        private float _duration;
        private float _time = -1f;
        private bool _ready;

        public float Amount => _amount;
        public Transform Ear => _ear;

        private void Awake()
        {
            Init();
        }

        /// <summary>Oor groeit (en krant zakt) in `seconds` seconden tot het maximum.</summary>
        public void Grow(float seconds)
        {
            Init();
            _from = _amount;
            _duration = Mathf.Max(0.01f, seconds);
            _time = 0f;
        }

        /// <summary>Oor terug op schaal 1, krant terug omhoog. (Ook Unity's Reset-bericht in de editor: dan gebeurt er niets.)</summary>
        public void Reset()
        {
            _time = -1f;
            _amount = 0f;
            if (_ready)
                Apply();
        }

        private void Update()
        {
            if (_time < 0f)
                return;
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / _duration);
            _amount = Mathf.Lerp(_from, 1f, Mathf.SmoothStep(0f, 1f, t));
            Apply();
            if (t >= 1f)
                _time = -1f;
        }

        private void Init()
        {
            if (_ready)
                return;
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogWarning($"[EarGrow] {name}: geen Humanoid Animator.", this);
                return;
            }
            if (_newspaper != null)
            {
                _paperPosition = _newspaper.localPosition;
                _paperRotation = _newspaper.localRotation;
            }
            _ear = CreateEar();
            _ready = true;
        }

        private void Apply()
        {
            if (_ear != null)
                _ear.localScale = Vector3.one * Mathf.Lerp(1f, _maxScale, _amount);
            if (_newspaper != null)
            {
                var down = _newspaper.parent != null ? _newspaper.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
                _newspaper.localPosition = _paperPosition + down * (_newspaperDrop * _amount);
                _newspaper.localRotation = _paperRotation * Quaternion.Euler(_newspaperTilt * _amount, 0f, 0f);
            }
        }

        /// <summary>
        /// Oor naast het hoofd, aan het hoofdbot. Richtingen komen uit de schouders (de assen van de botten verschillen
        /// per rig), dus het oor staat juist ongeacht de rotatie van de root.
        /// </summary>
        private Transform CreateEar()
        {
            var head = _animator.GetBoneTransform(HumanBodyBones.Head);
            var existing = head.Find("Ear_Grow");
            if (existing != null)
                return existing;

            var right = (_animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position -
                         _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position);
            right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            var side = _rightEar ? right : -right;

            var ear = new GameObject("Ear_Grow");
            ear.transform.SetParent(head, false);
            // Lokaal: x = naar buiten (weg van het hoofd), y = omhoog, z = voor/achter
            ear.transform.SetPositionAndRotation(
                head.position + side * _earOffset.x + Vector3.up * _earOffset.y,
                Quaternion.LookRotation(Vector3.Cross(side, Vector3.up), Vector3.up));
            ear.transform.localScale = Vector3.one;
            var lossy = ear.transform.lossyScale;
            ear.AddComponent<MeshFilter>().sharedMesh = EarMesh(new Vector3(_earSize.x / lossy.x, _earSize.y / lossy.y, _earSize.z / lossy.z));
            var skin = GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m != null && m.name.Contains("Skin"));
            var renderer = ear.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = skin;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ear.transform;
        }

        /// <summary>
        /// Low-poly oor: een afgeplatte ruit (8 driehoeken), flat shaded. size = dikte (x), hoogte (y), breedte (z).
        /// De oorsprong ligt aan de binnenkant (tegen het hoofd), zodat het oor bij het schalen naar buiten groeit.
        /// </summary>
        private static Mesh EarMesh(Vector3 size)
        {
            var mid = size.x * 0.35f;
            var hy = size.y / 2f;
            var hz = size.z / 2f;
            Vector3 top = new Vector3(mid, hy, 0f), bottom = new Vector3(mid, -hy * 0.8f, 0f);
            Vector3 front = new Vector3(mid, 0f, hz), back = new Vector3(mid, 0f, -hz * 0.8f);
            Vector3 outer = new Vector3(size.x, 0.1f * hy, 0f), inner = Vector3.zero;
            var tris = new[]
            {
                (top, front, outer), (top, outer, back), (bottom, outer, front), (bottom, back, outer),
                (top, inner, front), (top, back, inner), (bottom, front, inner), (bottom, inner, back),
            };
            // Wikkeling zo kiezen dat elke normaal van het midden van het oor weg wijst
            var center = new Vector3(mid, 0f, 0f);
            var vertices = tris.SelectMany(t =>
            {
                var (a, b, c) = t;
                return Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) >= 0f ? new[] { a, b, c } : new[] { a, c, b };
            }).ToArray();
            var mesh = new Mesh { name = "Ear_Grow", vertices = vertices, triangles = Enumerable.Range(0, vertices.Length).ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
