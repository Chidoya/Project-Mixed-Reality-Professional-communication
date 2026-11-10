using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Echte tranen bij huilen: zodra de blendshape Tears (bijna) vol staat, rollen er kleine
    /// druppels langs het traanspoor over de wang en vallen ze van het gezicht.
    /// Het pad komt uit de mesh zelf (de vertices die Tears verschuift), omgerekend naar het Head-bot.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class TearDrops : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField, Range(0f, 100f)] private float _startWeight = 70f;
        [SerializeField] private Vector2 _interval = new(0.7f, 1.5f);
        [SerializeField] private float _size = 0.011f;
        [SerializeField] private int _maxPerEye = 3;

        private sealed class Drop
        {
            public Transform T;
            public int Side;
            public float Distance;
            public float Speed;
            public bool Falling;
            public float FallTime;
            public Vector3 Velocity;
            public float Scale;
        }

        // Vooraf berekend in de editor (CharacterSetup -> Bake), want de mesh is in een build niet leesbaar
        [SerializeField, HideInInspector] private Vector3[] _pathLeft = new Vector3[0];
        [SerializeField, HideInInspector] private Vector3[] _pathRight = new Vector3[0];
        [SerializeField, HideInInspector] private Vector3 _forward; // vooruit in Head-ruimte
        [SerializeField, HideInInspector] private Vector3 _up;      // omhoog in Head-ruimte
        [SerializeField, HideInInspector] private string _headBone = "mixamorig:Head";

        private FaceController _face;
        private Transform _head;
        private Transform _pool;
        private readonly Vector3[][] _paths = new Vector3[2][];
        private readonly float[] _lengths = new float[2];
        private readonly float[] _next = new float[2];
        private readonly List<Drop> _drops = new();
        private readonly Stack<Transform> _free = new();
        private static Mesh _dropMesh;
        private float _clock;
        private bool _ready;
        private bool _tried;

        public int ActiveDrops => _drops.Count;
        public bool IsBaked => _pathLeft != null && _pathLeft.Length > 1 && _pathRight != null && _pathRight.Length > 1;

        private void Start() => Setup();

        /// <summary>
        /// Berekent het traanpad per oog uit de blendshape Tears (de vertices die hij verschuift),
        /// omgerekend naar de ruimte van het Head-bot. Werkt enkel op een leesbare mesh (editor).
        /// </summary>
        public bool Bake(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null)
                return false;
            var mesh = smr.sharedMesh;
            var shape = mesh.GetBlendShapeIndex("Tears");
            var headIndex = HeadIndex(smr, null);
            if (shape < 0 || headIndex < 0)
                return false;
            var bind = mesh.bindposes[headIndex];

            var verts = mesh.vertices;
            if (verts.Length != mesh.vertexCount)
                return false;
            var deltas = new Vector3[verts.Length];
            mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape) - 1, deltas,
                new Vector3[verts.Length], new Vector3[verts.Length]);

            var tearPoints = new List<Vector3>();
            for (var i = 0; i < verts.Length; i++)
                if (deltas[i].sqrMagnitude > 1e-6f)
                    tearPoints.Add(verts[i] + deltas[i]);
            if (tearPoints.Count == 0)
                return false;

            // Vooruit = van het hoofdbot naar de tranen (horizontaal)
            var center = Vector3.zero;
            foreach (var p in tearPoints)
                center += p;
            center /= tearPoints.Count;
            var fwdMesh = center - bind.inverse.MultiplyPoint3x4(Vector3.zero);
            fwdMesh.y = 0f;
            _forward = bind.MultiplyVector(fwdMesh.normalized).normalized;
            _up = bind.MultiplyVector(Vector3.up).normalized;
            _headBone = smr.bones[headIndex].name;

            // Per kant: rijen met dezelfde hoogte -> middelpunt, van boven naar onder
            for (var side = 0; side < 2; side++)
            {
                var pts = tearPoints.FindAll(p => side == 0 ? p.x > 0f : p.x <= 0f);
                pts.Sort((a, b) => b.y.CompareTo(a.y));
                var path = new List<Vector3>();
                var i = 0;
                while (i < pts.Count)
                {
                    var sum = pts[i];
                    var n = 1;
                    var j = i + 1;
                    while (j < pts.Count && Mathf.Abs(pts[j].y - pts[i].y) < 0.001f)
                    {
                        sum += pts[j];
                        n++;
                        j++;
                    }
                    path.Add(bind.MultiplyPoint3x4(sum / n) + _forward * 0.0022f);
                    i = j;
                }
                if (side == 0)
                    _pathLeft = path.ToArray();
                else
                    _pathRight = path.ToArray();
            }
            return IsBaked;
        }

        private int HeadIndex(SkinnedMeshRenderer smr, string name)
        {
            for (var i = 0; i < smr.bones.Length; i++)
            {
                var b = smr.bones[i];
                if (b != null && (name != null ? b.name == name : b.name.EndsWith("Head")))
                    return i;
            }
            return -1;
        }

        private void Setup()
        {
            if (_ready || _tried)
                return;
            _tried = true;
            _face = GetComponent<FaceController>() ?? GetComponentInChildren<FaceController>();
            var smr = _face != null ? _face.Renderer : null;
            if (smr == null)
            {
                enabled = false;
                return;
            }
            if (!IsBaked && (smr.sharedMesh == null || !smr.sharedMesh.isReadable || !Bake(smr)))
            {
                Debug.LogWarning($"[TearDrops] {name}: geen traanpad (draai ProCommVR/Characters/Setup).", this);
                enabled = false;
                return;
            }
            var headIndex = HeadIndex(smr, _headBone);
            if (headIndex < 0)
                headIndex = HeadIndex(smr, null);
            if (headIndex < 0)
            {
                enabled = false;
                return;
            }
            _head = smr.bones[headIndex];
            _paths[0] = _pathLeft;
            _paths[1] = _pathRight;
            for (var side = 0; side < 2; side++)
            {
                _lengths[side] = 0f;
                for (var k = 1; k < _paths[side].Length; k++)
                    _lengths[side] += Vector3.Distance(_paths[side][k - 1], _paths[side][k]);
                _next[side] = Random.Range(0.2f, 0.8f) + side * 0.4f;
            }

            _pool = new GameObject("TearDrops").transform;
            _pool.SetParent(transform, false);
            if (_material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _material = new Material(shader) { color = new Color(0.62f, 0.85f, 0.98f) };
                _material.SetFloat("_Smoothness", 0.9f);
            }
            _ready = true;
        }

        private void LateUpdate() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (!_ready)
                Setup();
            if (!_ready)
                return;
            _clock += dt;
            var crying = _face.GetWeight("Tears") >= _startWeight;

            if (crying)
            {
                for (var side = 0; side < 2; side++)
                {
                    if (_paths[side].Length < 2 || _clock < _next[side])
                        continue;
                    if (CountSide(side) < _maxPerEye)
                        Spawn(side);
                    _next[side] = _clock + Random.Range(_interval.x, _interval.y);
                }
            }
            else
            {
                _next[0] = _clock + Random.Range(0.2f, 0.6f);
                _next[1] = _clock + Random.Range(0.5f, 1.0f);
            }

            for (var i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (!d.Falling)
                {
                    d.Speed += dt * 0.05f;
                    d.Distance += d.Speed * dt;
                    var grow = Mathf.Clamp01(d.Distance / (0.25f * _lengths[d.Side]));
                    d.T.localPosition = PointOnPath(d.Side, d.Distance);
                    d.T.localRotation = Quaternion.LookRotation(_forward, _up);
                    d.T.localScale = Vector3.one * (_size * Mathf.Lerp(0.3f, 1f, grow) / Mathf.Max(1e-4f, _head.lossyScale.x));
                    if (d.Distance >= _lengths[d.Side])
                    {
                        // Loskomen van het gezicht en vallen
                        var world = d.T.position;
                        var rot = d.T.rotation;
                        d.T.SetParent(_pool, true);
                        d.T.SetPositionAndRotation(world, rot);
                        d.Falling = true;
                        d.Velocity = (_head.rotation * _forward) * 0.05f + Vector3.down * 0.15f;
                        d.Scale = _size;
                    }
                }
                else
                {
                    d.FallTime += dt;
                    d.Velocity += Physics.gravity * (0.7f * dt);
                    d.T.position += d.Velocity * dt;
                    var s = d.Scale * (1f - Mathf.Clamp01(d.FallTime / 0.45f));
                    d.T.localScale = Vector3.one * (s / Mathf.Max(1e-4f, _pool.lossyScale.x));
                    if (d.FallTime > 0.45f)
                    {
                        d.T.gameObject.SetActive(false);
                        _free.Push(d.T);
                        _drops.RemoveAt(i);
                    }
                }
            }
        }

        private int CountSide(int side)
        {
            var n = 0;
            foreach (var d in _drops)
                if (d.Side == side && !d.Falling)
                    n++;
            return n;
        }

        private Vector3 PointOnPath(int side, float distance)
        {
            var path = _paths[side];
            for (var k = 1; k < path.Length; k++)
            {
                var seg = Vector3.Distance(path[k - 1], path[k]);
                if (distance <= seg)
                    return Vector3.Lerp(path[k - 1], path[k], seg > 0f ? distance / seg : 0f);
                distance -= seg;
            }
            return path[path.Length - 1];
        }

        private void Spawn(int side)
        {
            Transform t;
            if (_free.Count > 0)
                t = _free.Pop();
            else
            {
                var go = new GameObject("TearDrop", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = DropMesh();
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = go.transform;
            }
            t.SetParent(_head, false);
            t.gameObject.SetActive(true);
            _drops.Add(new Drop { T = t, Side = side, Speed = Random.Range(0.022f, 0.032f) });
        }

        /// <summary>Low-poly druppel: punt boven, ring van 5, punt onder (10 driehoeken, flat).</summary>
        private static Mesh DropMesh()
        {
            if (_dropMesh != null)
                return _dropMesh;
            var top = new Vector3(0f, 0.75f, 0f);
            var bottom = new Vector3(0f, -0.55f, 0f);
            var ring = new Vector3[5];
            for (var i = 0; i < 5; i++)
            {
                var a = i * Mathf.PI * 2f / 5f;
                ring[i] = new Vector3(Mathf.Sin(a) * 0.42f, -0.12f, Mathf.Cos(a) * 0.32f);
            }
            var v = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                tris.Add(v.Count);
                v.Add(a);
                tris.Add(v.Count);
                v.Add(b);
                tris.Add(v.Count);
                v.Add(c);
            }
            for (var i = 0; i < 5; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % 5];
                Tri(top, a, b);
                Tri(bottom, b, a);
            }
            _dropMesh = new Mesh { name = "TearDrop" };
            _dropMesh.SetVertices(v);
            _dropMesh.SetTriangles(tris, 0);
            _dropMesh.RecalculateNormals();
            _dropMesh.RecalculateBounds();
            return _dropMesh;
        }
    }
}
