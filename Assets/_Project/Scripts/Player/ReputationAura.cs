using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using UnityEngine;

namespace ProCommVR.Player
{
    /// <summary>How the patient sees the nurse, from the trust (<see cref="PatientReputation"/>).</summary>
    public enum ReputationTier
    {
        /// <summary>Very low trust: black waves pour down over the nurse.</summary>
        Abyss,
        /// <summary>Low trust: a black, see-through liquid flows down the nurse.</summary>
        Liquid,
        /// <summary>In between: no aura.</summary>
        Neutral,
        /// <summary>Good trust: a dark golden aura.</summary>
        Golden,
        /// <summary>Very good trust: a shining aura of light with wings.</summary>
        Radiant,
    }

    /// <summary>
    /// The player's body as the patient sees it (the patient view): a blank white figure with an aura that shows the
    /// patient's trust in the nurse. Very low trust pours black waves down over the figure, low trust lets a black
    /// see-through liquid flow down it, the middle shows nothing, good trust gives a dark golden aura and very good
    /// trust a shining aura with wings of light.
    /// <para>
    /// The Golden and Liquid tiers start at the thresholds of the <see cref="PatientReputation"/> (the same as the colors
    /// of the trust meter in the HUD), so the meter and the aura always agree.
    /// </para>
    /// <para>
    /// Lives next to the <see cref="PlayerAvatar"/> (not inside it) and follows it. Each tier is a child with its own
    /// particle systems, light and (Radiant) wings, built by the scene builder; the tiers cross-fade, and a tier at
    /// weight 0 is inactive, so it costs nothing. Only shown while the avatar is visible.
    /// </para>
    /// </summary>
    public class ReputationAura : MonoBehaviour
    {
        [Serializable]
        public class TierLook
        {
            public ReputationTier tier;
            [Tooltip("Switched on while the tier's weight is above 0.")]
            public GameObject root;
            public ParticleSystem[] particles = Array.Empty<ParticleSystem>();
            public Light light;
            [Tooltip("Wings (a MeshFilter without a mesh gets a generated wing). Unfold with the weight and flap.")]
            public Transform[] wings = Array.Empty<Transform>();
        }

        [SerializeField] private PatientReputation _reputation;
        [Tooltip("Empty: the player avatar in the scene.")]
        [SerializeField] private PlayerAvatar _avatar;

        [Header("White figure")]
        [Tooltip("Replaces every material of the avatar (a blank figure instead of the nurse's clothes). Empty: keep them.")]
        [SerializeField] private Material _whiteMaterial;

        [Header("Thresholds (trust 0..100)")]
        [Tooltip("From this on: the aura of light with wings. Golden starts at the reputation's High Threshold.")]
        [SerializeField, Range(0f, 100f)] private float _radiantFrom = 80f;
        [Tooltip("Below this: the black waves. The black liquid starts below the reputation's Low Threshold.")]
        [SerializeField, Range(0f, 100f)] private float _abyssBelow = 20f;

        [Header("Look")]
        [SerializeField] private TierLook[] _tiers = Array.Empty<TierLook>();
        [Tooltip("Seconds to fade from one tier to the next.")]
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 1.2f;
        [Tooltip("Strength of a tier at its edge next to Neutral (1: the whole tier is the same). Lower: a tier gets " +
                 "stronger the deeper the trust is in it, so every change of the trust shows, not only a change of tier.")]
        [SerializeField, Range(0.1f, 1f)] private float _edgeStrength = 1f;
        [SerializeField, Range(0f, 30f)] private float _wingFlapDegrees = 7f;
        [SerializeField, Min(0f)] private float _wingFlapSpeed = 1.3f;

        private readonly Dictionary<ReputationTier, float> _weights = new Dictionary<ReputationTier, float>();
        private float[][] _rates;
        private float[] _lightIntensities;
        private Vector3[][] _wingScales;
        private Quaternion[][] _wingRotations;

        public ReputationTier Current { get; private set; } = ReputationTier.Neutral;
        public PlayerAvatar Avatar => _avatar;
        public Material WhiteMaterial => _whiteMaterial;

        /// <summary>The look of a tier (for tests).</summary>
        public TierLook Look(ReputationTier tier) => Array.Find(_tiers, t => t.tier == tier);

        /// <summary>0: hidden, 1: fully shown.</summary>
        public float Weight(ReputationTier tier) => _weights.TryGetValue(tier, out var weight) ? weight : 0f;

        /// <summary>
        /// How deep the trust is in its tier (Edge Strength at the edge next to Neutral, 1 at the far end): Liquid and
        /// Abyss get darker as the trust goes down, Golden and Radiant brighter as it goes up.
        /// </summary>
        public float Depth
        {
            get
            {
                if (_reputation == null || _edgeStrength >= 1f)
                    return 1f;
                var trust = _reputation.Value;
                float t;
                switch (Current)
                {
                    case ReputationTier.Golden: t = Mathf.InverseLerp(GoldenFrom, _radiantFrom, trust); break;
                    case ReputationTier.Radiant: t = Mathf.InverseLerp(_radiantFrom, 100f, trust); break;
                    case ReputationTier.Liquid: t = Mathf.InverseLerp(LiquidBelow, _abyssBelow, trust); break;
                    case ReputationTier.Abyss: t = Mathf.InverseLerp(_abyssBelow, 0f, trust); break;
                    default: return 1f;
                }
                return Mathf.Lerp(_edgeStrength, 1f, t);
            }
        }

        private float GoldenFrom => _reputation != null ? _reputation.HighThreshold : 60f;
        private float LiquidBelow => _reputation != null ? _reputation.LowThreshold : 40f;

        public ReputationTier TierFor(float trust)
        {
            if (trust >= _radiantFrom) return ReputationTier.Radiant;
            if (trust >= GoldenFrom) return ReputationTier.Golden;
            if (trust < _abyssBelow) return ReputationTier.Abyss;
            if (trust < LiquidBelow) return ReputationTier.Liquid;
            return ReputationTier.Neutral;
        }

        private void Awake()
        {
            if (_avatar == null)
                _avatar = FindAnyObjectByType<PlayerAvatar>(FindObjectsInactive.Include);
            if (_avatar != null && _whiteMaterial != null)
            {
                foreach (var renderer in _avatar.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (var i = 0; i < materials.Length; i++)
                        materials[i] = _whiteMaterial;
                    renderer.sharedMaterials = materials;
                }
            }

            _rates = new float[_tiers.Length][];
            _lightIntensities = new float[_tiers.Length];
            _wingScales = new Vector3[_tiers.Length][];
            _wingRotations = new Quaternion[_tiers.Length][];
            for (var i = 0; i < _tiers.Length; i++)
            {
                var look = _tiers[i];
                _rates[i] = new float[look.particles.Length];
                for (var p = 0; p < look.particles.Length; p++)
                    _rates[i][p] = look.particles[p].emission.rateOverTimeMultiplier;
                _lightIntensities[i] = look.light != null ? look.light.intensity : 0f;
                _wingScales[i] = new Vector3[look.wings.Length];
                _wingRotations[i] = new Quaternion[look.wings.Length];
                for (var w = 0; w < look.wings.Length; w++)
                {
                    var wing = look.wings[w];
                    _wingScales[i][w] = wing.localScale;
                    _wingRotations[i][w] = wing.localRotation;
                    var filter = wing.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh == null)
                        filter.sharedMesh = WingMesh(wing.localPosition.x < 0f);
                }
                _weights[look.tier] = 0f;
                if (look.root != null)
                    look.root.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            var shown = _avatar != null && _avatar.IsVisible;
            if (_reputation != null)
                Current = TierFor(_reputation.Value);
            if (_avatar != null)
                transform.SetPositionAndRotation(_avatar.transform.position, _avatar.transform.rotation);

            var step = Time.deltaTime / _fadeSeconds;
            for (var i = 0; i < _tiers.Length; i++)
            {
                var look = _tiers[i];
                var target = shown && look.tier == Current ? 1f : 0f;
                // Hidden avatar: off at once (the patient view closed behind a fade).
                var weight = shown ? Mathf.MoveTowards(Weight(look.tier), target, step) : 0f;
                _weights[look.tier] = weight;
                Apply(i, weight, look.tier == Current ? Depth : 1f);
            }
        }

        private void Apply(int index, float weight, float depth = 1f)
        {
            var look = _tiers[index];
            var on = weight > 0.001f;
            if (look.root != null && look.root.activeSelf != on)
                look.root.SetActive(on);
            if (!on)
                return;
            for (var p = 0; p < look.particles.Length; p++)
            {
                var emission = look.particles[p].emission;
                emission.rateOverTimeMultiplier = _rates[index][p] * weight * depth;
            }
            if (look.light != null)
                look.light.intensity = _lightIntensities[index] * weight * depth;
            var unfold = Mathf.SmoothStep(0f, 1f, weight);
            var flap = Mathf.Sin(Time.time * _wingFlapSpeed * Mathf.PI * 2f) * _wingFlapDegrees;
            for (var w = 0; w < look.wings.Length; w++)
            {
                var wing = look.wings[w];
                var side = wing.localPosition.x < 0f ? -1f : 1f;
                wing.localScale = _wingScales[index][w] * unfold;
                wing.localRotation = _wingRotations[index][w] * Quaternion.Euler(0f, flap * side, flap * 0.5f * side);
            }
        }

        /// <summary>
        /// A wing of light: a fan of soft feathers from the root (the shoulder blade) outward and upward, in the wing's
        /// XY plane (+X outward for the right wing, mirrored for the left). Each feather is a quad over the whole soft
        /// dot texture, so it is a soft ellipse; the vertex colors fade from the root to the tip.
        /// </summary>
        internal static Mesh WingMesh(bool left)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var root = new Color(1f, 0.97f, 0.88f, 0.75f);
            var tip = new Color(1f, 0.9f, 0.7f, 0.45f);

            // Long flight feathers (lower, outward) and shorter feathers on top (upward), then a row of coverts.
            const int feathers = 11;
            for (var i = 0; i < feathers; i++)
            {
                var t = i / (feathers - 1f);
                var angle = Mathf.Lerp(-30f, 70f, t);
                var length = Mathf.Lerp(1.15f, 0.7f, t * t) * (0.94f + 0.06f * Mathf.Sin(i * 2.3f));
                AddFeather(angle, 0.12f, length, 0.17f, root, tip);
            }
            for (var i = 0; i < 7; i++)
            {
                var t = i / 6f;
                AddFeather(Mathf.Lerp(-10f, 65f, t), 0.04f, 0.5f, 0.24f, root, root);
            }

            var mesh = new Mesh { name = left ? "Wing (left)" : "Wing (right)" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;

            void AddFeather(float degrees, float start, float end, float width, Color near, Color far)
            {
                var radians = degrees * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
                var across = new Vector3(-direction.y, direction.x, 0f) * (width * 0.5f);
                // A slight curve back, so the wing is not a flat sheet.
                var back = new Vector3(0f, 0f, -0.08f * end);
                var index = vertices.Count;
                foreach (var v in new[] { direction * start - across, direction * start + across, direction * end + across + back, direction * end - across + back })
                    vertices.Add(left ? new Vector3(-v.x, v.y, v.z) : v);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                colors.Add(near);
                colors.Add(near);
                colors.Add(far);
                colors.Add(far);
                triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
            }
        }
    }
}
