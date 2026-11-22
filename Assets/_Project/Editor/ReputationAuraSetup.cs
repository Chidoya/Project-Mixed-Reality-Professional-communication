using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds the <see cref="ReputationAura"/> of the player in a scene: the white material for the figure and one child
    /// per tier with its particle systems (the soft dot particles of the patient view), a light and, for Radiant, two
    /// wings. Everything is in local space around the avatar's feet, +Z is where the figure looks. The particle counts
    /// stay small for the Quest 3.
    /// </summary>
    public static class ReputationAuraSetup
    {
        private const string WhiteMaterialPath = "Assets/_Project/Materials/Player/AvatarWhite.mat";

        internal static ReputationAura AddToScene(PatientReputation reputation, PlayerAvatar avatar)
        {
            var aura = new GameObject("Reputation Aura (player)").AddComponent<ReputationAura>();
            var looks = new List<ReputationAura.TierLook>
            {
                BuildAbyss(aura.transform),
                BuildLiquid(aura.transform),
                BuildGolden(aura.transform),
                BuildRadiant(aura.transform),
            };

            var serialized = new SerializedObject(aura);
            serialized.FindProperty("_reputation").objectReferenceValue = reputation;
            serialized.FindProperty("_avatar").objectReferenceValue = avatar;
            serialized.FindProperty("_whiteMaterial").objectReferenceValue = WhiteMaterial();
            var tiers = serialized.FindProperty("_tiers");
            tiers.arraySize = looks.Count;
            for (var i = 0; i < looks.Count; i++)
            {
                var element = tiers.GetArrayElementAtIndex(i);
                var look = looks[i];
                element.FindPropertyRelative("tier").enumValueIndex = (int)look.tier;
                element.FindPropertyRelative("root").objectReferenceValue = look.root;
                element.FindPropertyRelative("light").objectReferenceValue = look.light;
                SetArray(element.FindPropertyRelative("particles"), look.particles);
                SetArray(element.FindPropertyRelative("wings"), look.wings);
                look.root.SetActive(false);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return aura;
        }

        // ---- Very low trust: black waves pour down from above the head over the figure (and pool on the floor) ----
        private static ReputationAura.TierLook BuildAbyss(Transform parent)
        {
            var root = TierRoot(parent, ReputationTier.Abyss);
            var black = EmotionWorldSetup.AlphaBlended();
            var down = Quaternion.Euler(90f, 0f, 0f); // the shape's +Z points down

            var torrent = EmotionWorldSetup.Particles(root, "Torrent", new Vector3(0f, 2.55f, 0f), down, black, 160);
            Main(torrent, new Vector2(0.8f, 0.95f), new Vector2(2.2f, 3.0f), new Vector2(0.08f, 0.2f), new Color(0f, 0f, 0f, 0.85f));
            Cone(torrent, 4f, 0.34f);
            Rate(torrent, 140f);
            Stretch(torrent, 0.12f, 3.5f);
            Noise(torrent, 0.03f, 2f);
            EmotionWorldSetup.FadeInOut(torrent, Color.black);

            var sheets = EmotionWorldSetup.Particles(root, "Sheets", new Vector3(0f, 2.3f, 0f), down, black, 70);
            Main(sheets, new Vector2(1.0f, 1.3f), new Vector2(1.4f, 2.0f), new Vector2(0.25f, 0.45f), new Color(0.01f, 0.01f, 0.01f, 0.55f));
            Box(sheets, new Vector3(0.6f, 0.4f, 0.1f));
            Rate(sheets, 45f);
            Stretch(sheets, 0.1f, 2.5f);
            Noise(sheets, 0.03f, 2f);
            EmotionWorldSetup.FadeInOut(sheets, Color.black);

            // Over the body (in front of it too): the figure itself is covered by the black waves.
            var drape = EmotionWorldSetup.Particles(root, "Drape", new Vector3(0f, 1.0f, 0f), Quaternion.identity, black, 160);
            Main(drape, new Vector2(0.5f, 0.8f), new Vector2(0f, 0f), new Vector2(0.1f, 0.22f), new Color(0f, 0f, 0f, 0.8f));
            Box(drape, new Vector3(0.55f, 1.7f, 0.42f));
            Rate(drape, 150f);
            Velocity(drape, new Vector2(-2.6f, -1.6f));
            Stretch(drape, 0.12f, 2.5f);
            EmotionWorldSetup.FadeInOut(drape, Color.black);

            var cloud = EmotionWorldSetup.Particles(root, "Cloud", new Vector3(0f, 2.3f, 0f), Quaternion.identity, black, 24);
            Main(cloud, new Vector2(2.2f, 3f), new Vector2(0f, 0.05f), new Vector2(0.9f, 1.3f), new Color(0f, 0f, 0f, 0.4f));
            Sphere(cloud, 0.25f);
            Rate(cloud, 7f);
            EmotionWorldSetup.FadeInOut(cloud, Color.black);

            var pool = Pool(root, black, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.1f, 1.5f));
            return Look(ReputationTier.Abyss, root, null, torrent, sheets, drape, cloud, pool);
        }

        // ---- Low trust: a black, see-through liquid flows slowly down the figure ----
        private static ReputationAura.TierLook BuildLiquid(Transform parent)
        {
            var root = TierRoot(parent, ReputationTier.Liquid);
            var black = EmotionWorldSetup.AlphaBlended();

            var flow = EmotionWorldSetup.Particles(root, "Flow", new Vector3(0f, 1.0f, 0f), Quaternion.identity, black, 180);
            Main(flow, new Vector2(1.4f, 2.0f), new Vector2(0f, 0f), new Vector2(0.1f, 0.22f), new Color(0.01f, 0.01f, 0.02f, 0.7f));
            Box(flow, new Vector3(0.5f, 1.5f, 0.4f));
            Rate(flow, 90f);
            Velocity(flow, new Vector2(-0.45f, -0.22f));
            Stretch(flow, 0.6f, 1.6f);
            Noise(flow, 0.06f, 1.6f);
            var size = flow.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));
            EmotionWorldSetup.FadeInOut(flow, Color.black);

            // Drops from the head and shoulders that run down.
            var drops = EmotionWorldSetup.Particles(root, "Drops", new Vector3(0f, 1.6f, 0f), Quaternion.identity, black, 60);
            Main(drops, new Vector2(1.2f, 1.6f), new Vector2(0f, 0f), new Vector2(0.05f, 0.1f), new Color(0f, 0f, 0f, 0.85f));
            Box(drops, new Vector3(0.45f, 0.15f, 0.35f));
            Rate(drops, 30f);
            Velocity(drops, new Vector2(-1.1f, -0.6f));
            Stretch(drops, 0.25f, 2.5f);
            EmotionWorldSetup.FadeInOut(drops, Color.black);

            var haze = EmotionWorldSetup.Particles(root, "Haze", new Vector3(0f, 1.0f, 0f), Quaternion.identity, black, 16);
            Main(haze, new Vector2(2.5f, 3.5f), new Vector2(0f, 0.03f), new Vector2(0.9f, 1.2f), new Color(0f, 0f, 0f, 0.25f));
            Box(haze, new Vector3(0.3f, 1.0f, 0.2f));
            Rate(haze, 4f);
            EmotionWorldSetup.FadeInOut(haze, Color.black);

            var pool = Pool(root, black, new Color(0f, 0f, 0f, 0.4f), new Vector2(0.8f, 1.1f));
            return Look(ReputationTier.Liquid, root, null, flow, drops, haze, pool);
        }

        // ---- Good trust: a dark golden aura ----
        private static ReputationAura.TierLook BuildGolden(Transform parent)
        {
            var root = TierRoot(parent, ReputationTier.Golden);
            var glow = EmotionWorldSetup.Additive();
            var gold = new Color(0.85f, 0.55f, 0.12f);

            var halo = Halo(root, glow, new Color(gold.r, gold.g, gold.b, 0.4f), new Vector2(1.6f, 2.0f), 6f);
            var motes = Motes(root, glow, new Color(1f, 0.68f, 0.2f, 1f), 32f, new Vector2(0.02f, 0.045f));
            var light = AuraLight(root, new Color(1f, 0.62f, 0.2f), 0.9f, 2.5f);
            return Look(ReputationTier.Golden, root, light, halo, motes);
        }

        // ---- Very good trust: a shining aura of light with wings ----
        private static ReputationAura.TierLook BuildRadiant(Transform parent)
        {
            var root = TierRoot(parent, ReputationTier.Radiant);
            var glow = EmotionWorldSetup.Additive();

            var halo = Halo(root, glow, new Color(1f, 0.94f, 0.78f, 0.3f), new Vector2(1.8f, 2.3f), 5f);
            var motes = Motes(root, glow, new Color(1f, 0.95f, 0.8f, 1f), 40f, new Vector2(0.02f, 0.05f));

            var rays = EmotionWorldSetup.Particles(root, "Rays", new Vector3(0f, 1.3f, -0.15f), Quaternion.identity, glow, 30);
            Main(rays, new Vector2(0.9f, 1.2f), new Vector2(0.6f, 1.0f), new Vector2(0.06f, 0.1f), new Color(1f, 0.92f, 0.7f, 0.3f));
            Sphere(rays, 0.1f);
            Rate(rays, 14f);
            Stretch(rays, 0.3f, 6f);
            EmotionWorldSetup.FadeInOut(rays, new Color(1f, 0.92f, 0.7f));

            var light = AuraLight(root, new Color(1f, 0.93f, 0.75f), 1.8f, 3.5f);

            var material = EmotionWorldSetup.Additive();
            var wings = new List<Transform>();
            foreach (var side in new[] { -1f, 1f })
            {
                var wing = new GameObject(side < 0f ? "Wing (left)" : "Wing (right)");
                wing.transform.SetParent(root, false);
                // From the shoulder blades, swept a little back.
                wing.transform.localPosition = new Vector3(0.09f * side, 1.33f, -0.15f);
                wing.transform.localRotation = Quaternion.Euler(0f, -25f * side, 0f);
                wing.AddComponent<MeshFilter>(); // the mesh is generated on Awake (ReputationAura.WingMesh)
                var renderer = wing.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                wings.Add(wing.transform);
            }

            var look = Look(ReputationTier.Radiant, root, light, halo, motes, rays);
            look.wings = wings.ToArray();
            return look;
        }

        // ---- Parts ----

        private static ParticleSystem Halo(Transform root, Material material, Color color, Vector2 size, float rate)
        {
            var halo = EmotionWorldSetup.Particles(root, "Halo", new Vector3(0f, 1.0f, -0.05f), Quaternion.identity, material, 20);
            Main(halo, new Vector2(2f, 2.6f), new Vector2(0f, 0.02f), size, color);
            Box(halo, new Vector3(0.15f, 0.7f, 0.05f));
            Rate(halo, rate);
            EmotionWorldSetup.FadeInOut(halo, new Color(color.r, color.g, color.b));
            return halo;
        }

        private static ParticleSystem Motes(Transform root, Material material, Color color, float rate, Vector2 size)
        {
            var motes = EmotionWorldSetup.Particles(root, "Motes", new Vector3(0f, 0.95f, 0f), Quaternion.identity, material, 100);
            Main(motes, new Vector2(1.5f, 2.5f), new Vector2(0f, 0f), size, color);
            Box(motes, new Vector3(0.65f, 1.7f, 0.4f));
            Rate(motes, rate);
            Velocity(motes, new Vector2(0.2f, 0.45f));
            Noise(motes, 0.05f, 1f);
            EmotionWorldSetup.FadeInOut(motes, new Color(color.r, color.g, color.b));
            return motes;
        }

        private static ParticleSystem Pool(Transform root, Material material, Color color, Vector2 size)
        {
            var pool = EmotionWorldSetup.Particles(root, "Pool (floor)", new Vector3(0f, 0.01f, 0f), Quaternion.identity, material, 8);
            Main(pool, new Vector2(2.5f, 3.5f), new Vector2(0f, 0f), size, color);
            Sphere(pool, 0.08f);
            Rate(pool, 2.5f);
            pool.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            EmotionWorldSetup.FadeInOut(pool, new Color(color.r, color.g, color.b));
            return pool;
        }

        private static Light AuraLight(Transform root, Color color, float intensity, float range)
        {
            var light = new GameObject("Aura Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 1.3f, 0.45f);
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // ---- Particle settings ----

        private static void Main(ParticleSystem particles, Vector2 lifetime, Vector2 speed, Vector2 size, Color color)
        {
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        private static void Rate(ParticleSystem particles, float rate)
        {
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
        }

        private static void Cone(ParticleSystem particles, float angle, float radius)
        {
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        private static void Box(ParticleSystem particles, Vector3 size)
        {
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
        }

        private static void Sphere(ParticleSystem particles, float radius)
        {
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static void Velocity(ParticleSystem particles, Vector2 upward)
        {
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(upward.x, upward.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void Stretch(ParticleSystem particles, float velocityScale, float lengthScale)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
            var main = particles.main;
            main.startRotation = 0f; // stretched particles follow their velocity
        }

        private static void Noise(ParticleSystem particles, float strength, float frequency)
        {
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        // ---- Helpers ----

        private static Transform TierRoot(Transform parent, ReputationTier tier)
        {
            var root = new GameObject(tier.ToString()).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static ReputationAura.TierLook Look(ReputationTier tier, Transform root, Light light, params ParticleSystem[] particles) =>
            new ReputationAura.TierLook { tier = tier, root = root.gameObject, light = light, particles = particles, wings = new Transform[0] };

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>A blank white figure (URP Lit, a little emission so it stays white in a dim room).</summary>
        private static Material WhiteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
            if (material == null)
            {
                XRTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(WhiteMaterialPath).Replace('\\', '/'));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "AvatarWhite" };
                AssetDatabase.CreateAsset(material, WhiteMaterialPath);
            }
            material.SetColor("_BaseColor", new Color(0.93f, 0.93f, 0.92f));
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.12f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
