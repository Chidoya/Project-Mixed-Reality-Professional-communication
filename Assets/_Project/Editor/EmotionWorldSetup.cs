using System.Collections.Generic;
using System.IO;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProCommVR.Editor
{
    /// <summary>
    /// The room reacting to the patient's emotion (patient view only): creates the emotion world profiles
    /// (Settings/Emotions), the volume profile (Color Adjustments and Vignette, no Bloom), the particle materials, and
    /// adds the <see cref="PatientWorldController"/> and the effect anchors to a scene. Existing profile assets are kept
    /// (they are meant to be tuned by hand); delete one to get its default values back.
    /// </summary>
    public static class EmotionWorldSetup
    {
        internal const string ProfileFolder = "Assets/_Project/Settings/Emotions";
        internal const string VolumeProfilePath = ProfileFolder + "/EmotionWorldVolume.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/Emotions";
        private const string SoftDotPath = MaterialFolder + "/SoftDot.png";
        private const string SoundFolder = "Assets/_Project/Audio/Placeholder";
        private const string ParticleShader = "ProCommVR/Particle";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";

        /// <summary>The most particles one effect may have alive (Quest 3 budget).</summary>
        internal const int MaxParticlesPerSystem = 200;

        private struct ProfileValues
        {
            public Color ColorFilter;
            public float Saturation;
            public float PostExposure;
            public float Vignette;
            public Color VignetteColor;
            public float PulseRate;
            public float PulseAmount;
            public Color LightTint;
            public float LightIntensity;
            public Color AmbientTint;
            public float AmbientIntensity;
            public bool Fog;
            public Color FogColor;
            public float FogDensity;
            public EmotionEffectKind[] Effects;
            public float SoundVolume;
        }

        private static readonly Dictionary<Emotion, ProfileValues> Defaults = new Dictionary<Emotion, ProfileValues>
        {
            // Joy: slightly green and warm, a bit brighter, plants and sunlight.
            [Emotion.Joy] = new ProfileValues
            {
                ColorFilter = new Color(0.97f, 1f, 0.92f), Saturation = 25f, PostExposure = 0.45f,
                Vignette = 0.1f, VignetteColor = Color.black,
                LightTint = new Color(1f, 0.95f, 0.82f), LightIntensity = 1.6f,
                AmbientTint = new Color(1.05f, 1.05f, 0.9f), AmbientIntensity = 1.5f,
                Effects = new[] { EmotionEffectKind.Plant, EmotionEffectKind.SunBeam }, SoundVolume = 0.25f,
            },
            // Anger: reddish, a red vignette that pulses like a heartbeat, fire and a flickering light.
            [Emotion.Anger] = new ProfileValues
            {
                ColorFilter = new Color(1f, 0.82f, 0.72f), Saturation = 15f,
                Vignette = 0.32f, VignetteColor = new Color(0.4f, 0.02f, 0f), PulseRate = 1.2f, PulseAmount = 0.1f,
                LightTint = new Color(1f, 0.62f, 0.45f), LightIntensity = 0.9f,
                AmbientTint = new Color(1f, 0.7f, 0.6f), AmbientIntensity = 0.9f,
                Effects = new[] { EmotionEffectKind.Fire, EmotionEffectKind.FireLight }, SoundVolume = 0.4f,
            },
            // Fear: the room goes dark (lights and ambient almost off, black fog), only the head flashlight lights it.
            [Emotion.Fear] = new ProfileValues
            {
                // Darker after the playtest of 2026-11-22 (was exposure -0.5, vignette 0.55, light 0.03, ambient 0.06, fog 0.3).
                ColorFilter = new Color(0.85f, 0.9f, 1f), Saturation = -35f, PostExposure = -0.9f,
                Vignette = 0.65f, VignetteColor = Color.black,
                LightTint = new Color(0.7f, 0.75f, 1f), LightIntensity = 0.01f,
                AmbientTint = new Color(0.8f, 0.85f, 1f), AmbientIntensity = 0.02f,
                Fog = true, FogColor = new Color(0.01f, 0.01f, 0.02f), FogDensity = 0.45f,
                Effects = new[] { EmotionEffectKind.Flashlight }, SoundVolume = 0.45f,
            },
            // Sadness: grey-blue and less colorful, rain on the window and a few drips inside.
            [Emotion.Sadness] = new ProfileValues
            {
                ColorFilter = new Color(0.8f, 0.88f, 1f), Saturation = -45f, PostExposure = -0.2f,
                Vignette = 0.25f, VignetteColor = new Color(0.02f, 0.04f, 0.08f),
                LightTint = new Color(0.75f, 0.82f, 1f), LightIntensity = 0.7f,
                AmbientTint = new Color(0.8f, 0.86f, 1f), AmbientIntensity = 0.75f,
                Fog = true, FogColor = new Color(0.42f, 0.47f, 0.55f), FogDensity = 0.08f,
                Effects = new[] { EmotionEffectKind.Rain, EmotionEffectKind.Drips }, SoundVolume = 0.4f,
            },
        };

        /// <summary>
        /// The four profiles, created with the default values when missing.
        /// </summary>
        internal static EmotionWorldProfile[] GetOrCreateProfiles()
        {
            var profiles = new List<EmotionWorldProfile>();
            foreach (var pair in Defaults)
                profiles.Add(GetOrCreateProfile(pair.Key, pair.Value));
            return profiles.ToArray();
        }

        private static EmotionWorldProfile GetOrCreateProfile(Emotion emotion, ProfileValues values)
        {
            var path = $"{ProfileFolder}/EmotionWorld_{emotion}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<EmotionWorldProfile>(path);
            if (profile != null)
                return profile;
            XRTestSceneBuilder.EnsureFolder(ProfileFolder);
            profile = ScriptableObject.CreateInstance<EmotionWorldProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_emotion").enumValueIndex = (int)emotion;
            serialized.FindProperty("_colorFilter").colorValue = values.ColorFilter;
            serialized.FindProperty("_saturation").floatValue = values.Saturation;
            serialized.FindProperty("_postExposure").floatValue = values.PostExposure;
            serialized.FindProperty("_vignetteIntensity").floatValue = values.Vignette;
            serialized.FindProperty("_vignetteColor").colorValue = values.VignetteColor;
            serialized.FindProperty("_vignettePulseRate").floatValue = values.PulseRate;
            serialized.FindProperty("_vignettePulseAmount").floatValue = values.PulseAmount;
            serialized.FindProperty("_lightTint").colorValue = values.LightTint;
            serialized.FindProperty("_lightIntensity").floatValue = values.LightIntensity;
            serialized.FindProperty("_ambientTint").colorValue = values.AmbientTint;
            serialized.FindProperty("_ambientIntensity").floatValue = values.AmbientIntensity;
            serialized.FindProperty("_fog").boolValue = values.Fog;
            serialized.FindProperty("_fogColor").colorValue = values.FogColor;
            serialized.FindProperty("_fogDensity").floatValue = values.FogDensity;
            var effects = serialized.FindProperty("_effects");
            effects.arraySize = values.Effects.Length;
            for (var i = 0; i < values.Effects.Length; i++)
                effects.GetArrayElementAtIndex(i).enumValueIndex = (int)values.Effects[i];
            serialized.FindProperty("_sound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}/Emotion_{emotion}_Placeholder.wav");
            serialized.FindProperty("_soundVolume").floatValue = values.SoundVolume;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        /// <summary>
        /// A volume profile with only Color Adjustments and Vignette (all values overridden, neutral by default).
        /// </summary>
        internal static VolumeProfile GetOrCreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                XRTestSceneBuilder.EnsureFolder(ProfileFolder);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            if (!profile.Has<ColorAdjustments>())
            {
                var colorAdjustments = profile.Add<ColorAdjustments>(true);
                colorAdjustments.name = nameof(ColorAdjustments);
                colorAdjustments.postExposure.value = 0f;
                colorAdjustments.contrast.overrideState = false;
                colorAdjustments.hueShift.overrideState = false;
                colorAdjustments.colorFilter.value = Color.white;
                colorAdjustments.saturation.value = 0f;
                AssetDatabase.AddObjectToAsset(colorAdjustments, profile);
            }
            if (!profile.Has<Vignette>())
            {
                var vignette = profile.Add<Vignette>(true);
                vignette.name = nameof(Vignette);
                vignette.intensity.value = 0f;
                vignette.smoothness.value = 0.45f;
                vignette.color.value = Color.black;
                vignette.center.overrideState = false;
                vignette.rounded.overrideState = false;
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// Adds the controller (with the global volume and the sound) to a scene. Room lights: the lights that the
        /// effects tint and dim (e.g. the main light). Effects: the parent of the anchors (from the Add* methods).
        /// </summary>
        internal static PatientWorldController AddToScene(EmotionHolder holder, PovController pov, Light[] roomLights)
        {
            var root = new GameObject("Patient World (emotion effects)");
            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = GetOrCreateVolumeProfile();

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = 0f;
            audio.volume = 0f;

            var controller = root.AddComponent<PatientWorldController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_holder").objectReferenceValue = holder;
            serialized.FindProperty("_pov").objectReferenceValue = pov;
            var profiles = GetOrCreateProfiles();
            var profileList = serialized.FindProperty("_profiles");
            profileList.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
                profileList.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.FindProperty("_volume").objectReferenceValue = volume;
            var lights = serialized.FindProperty("_roomLights");
            lights.arraySize = roomLights.Length;
            for (var i = 0; i < roomLights.Length; i++)
                lights.GetArrayElementAtIndex(i).objectReferenceValue = roomLights[i];
            serialized.FindProperty("_audio").objectReferenceValue = audio;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        // ---- Effect anchors -------------------------------------------------------------------------------------

        /// <summary>
        /// Joy: a plant that grows from nothing (Kenney model, height in meters) at a point on the floor or a sill.
        /// </summary>
        internal static EmotionEffectAnchor AddPlant(Transform parent, string name, Vector3 position, string model, float height, float yaw)
        {
            var anchor = NewAnchor(parent, name, position, Quaternion.identity, Emotion.Joy, EmotionEffectKind.Plant, out var content);
            content.SetActive(true); // the bounds only see active meshes
            var plant = Scenario01SceneBuilder.PlaceModel($"{KenneyFolder}/{model}.fbx", content.transform, "Plant Model", yaw);
            Scenario01SceneBuilder.ScaleToHeight(plant, height);
            Scenario01SceneBuilder.Align(plant, new Vector3(0.5f, 0f, 0.5f), position);
            content.SetActive(false);
            foreach (var renderer in plant.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            Set(anchor, "_grow", content.transform);
            return anchor;
        }

        /// <summary>
        /// Joy: warm sunlight through a window (a spot light from outside, the one extra light of Joy) with a few
        /// dust motes in the beam. Position: outside the window; target: the floor inside.
        /// </summary>
        internal static EmotionEffectAnchor AddSunBeam(Transform parent, Vector3 position, Vector3 target, Vector3 dustCenter, Vector3 dustSize)
        {
            var rotation = Quaternion.LookRotation(target - position);
            var anchor = NewAnchor(parent, "Sun Beam", position, rotation, Emotion.Joy, EmotionEffectKind.SunBeam, out var content);
            var light = content.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.intensity = 12f;
            light.range = Vector3.Distance(position, target) * 1.6f;
            light.spotAngle = 50f;
            light.innerSpotAngle = 30f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            Set(anchor, "_light", light);

            var dust = Particles(content.transform, "Dust Motes", dustCenter, Quaternion.identity, Additive(), 40);
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            main.startColor = new Color(1f, 0.9f, 0.65f, 0.6f);
            var emission = dust.emission;
            emission.rateOverTime = 8f;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustSize;
            FadeInOut(dust, new Color(1f, 0.9f, 0.65f));
            Set(anchor, "_particles", dust);
            return anchor;
        }

        /// <summary>
        /// Anger: a small fire (additive particles) on a piece of furniture or a curtain. Size: about the flame width in meters.
        /// </summary>
        internal static EmotionEffectAnchor AddFire(Transform parent, string name, Vector3 position, float size)
        {
            var anchor = NewAnchor(parent, name, position, Quaternion.identity, Emotion.Anger, EmotionEffectKind.Fire, out var content);
            var fire = Particles(content.transform, "Flames", Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), Additive(), 60, true);
            var main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * size / 0.3f, 0.7f * size / 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size * 0.85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            var emission = fire.emission;
            emission.rateOverTime = 45f;
            var shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = size * 0.35f;
            var colors = fire.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.08f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
            var sizes = fire.sizeOverLifetime;
            sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            Set(anchor, "_particles", fire);
            return anchor;
        }

        /// <summary>
        /// Anger: the red-orange flickering light of the fires (the one extra light of Anger).
        /// </summary>
        internal static EmotionEffectAnchor AddFireLight(Transform parent, Vector3 position)
        {
            var anchor = NewAnchor(parent, "Fire Light", position, Quaternion.identity, Emotion.Anger, EmotionEffectKind.FireLight, out var content);
            var light = content.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.42f, 0.12f);
            light.intensity = 1.6f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            Set(anchor, "_light", light);
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("_flicker").floatValue = 0.55f;
            serialized.FindProperty("_flickerSpeed").floatValue = 9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return anchor;
        }

        /// <summary>
        /// Fear: a flashlight on the player's head (a narrow spot with a soft edge, the one extra light of Fear). It is
        /// a child of the XR camera, so it moves with the head in the patient view.
        /// </summary>
        internal static EmotionEffectAnchor AddFlashlight(Transform parent, Transform camera)
        {
            var anchor = NewAnchor(parent, "Head Flashlight", camera.position, camera.rotation, Emotion.Fear, EmotionEffectKind.Flashlight, out var content);
            anchor.transform.SetParent(camera, false);
            anchor.transform.localPosition = new Vector3(0.04f, -0.06f, 0.05f);
            anchor.transform.localRotation = Quaternion.identity;
            var light = content.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 4f;
            light.range = 8f;
            light.spotAngle = 38f;
            light.innerSpotAngle = 14f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            Set(anchor, "_light", light);
            return anchor;
        }

        /// <summary>
        /// Sadness: rain streaks falling over a window, from the top of the window down to the sill.
        /// Sill: the middle of the window sill; size: width (x), height (y) and depth (z) in the window's own axes;
        /// yaw: turns the window's width (x) along the wall.
        /// </summary>
        internal static EmotionEffectAnchor AddRain(Transform parent, Vector3 sill, Vector3 size, float yaw)
        {
            var anchor = NewAnchor(parent, "Rain", sill, Quaternion.Euler(0f, yaw, 0f), Emotion.Sadness, EmotionEffectKind.Rain, out var content);
            const float speed = 5f;
            var rain = Particles(content.transform, "Rain Streaks", new Vector3(0f, size.y, 0f), Quaternion.Euler(90f, 0f, 0f),
                AlphaBlended(), MaxParticlesPerSystem);
            var main = rain.main;
            main.startLifetime = size.y / speed;
            main.startSpeed = speed;
            main.startSize = 0.012f;
            main.startColor = new Color(0.62f, 0.7f, 0.82f, 0.55f);
            var emission = rain.emission;
            emission.rateOverTime = 260f;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.z, 0.02f);
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.05f;
            Set(anchor, "_particles", rain);
            return anchor;
        }

        /// <summary>
        /// Sadness: rain inside the room, from just under the ceiling down to the floor, over an area of the room.
        /// Center: the floor point under the middle of the area; size: width (x) and depth (z) in the area's own axes
        /// (turned by yaw); height: from the floor to where the rain starts.
        /// </summary>
        internal static EmotionEffectAnchor AddIndoorRain(Transform parent, string name, Vector3 center, Vector2 size, float height, float yaw)
        {
            var anchor = NewAnchor(parent, name, center, Quaternion.Euler(0f, yaw, 0f), Emotion.Sadness, EmotionEffectKind.Rain, out var content);
            const float speed = 6f;
            // Emitted from a flat box under the ceiling, straight down; each streak lives exactly until it reaches the floor.
            var rain = Particles(content.transform, "Indoor Rain", new Vector3(0f, height, 0f), Quaternion.Euler(90f, 0f, 0f),
                AlphaBlended(), MaxParticlesPerSystem);
            var main = rain.main;
            main.startLifetime = height / speed;
            main.startSpeed = speed;
            main.startSize = 0.014f;
            main.startColor = new Color(0.66f, 0.74f, 0.86f, 0.5f);
            var emission = rain.emission;
            // About 160 streaks alive at full weight (the Quest 3 budget is 200 per system).
            emission.rateOverTime = Mathf.Min(MaxParticlesPerSystem * 0.8f / main.startLifetime.constant, 400f);
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.y, 0.02f);
            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.2f;
            renderer.velocityScale = 0.05f;
            Set(anchor, "_particles", rain);

            // Small splashes where the rain hits the floor.
            var splash = Particles(content.transform, "Splashes", new Vector3(0f, 0.01f, 0f), Quaternion.Euler(-90f, 0f, 0f),
                AlphaBlended(), 60, true);
            var splashMain = splash.main;
            splashMain.startLifetime = 0.25f;
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            splashMain.gravityModifier = 1f;
            splashMain.startColor = new Color(0.75f, 0.82f, 0.92f, 0.6f);
            var splashEmission = splash.emission;
            splashEmission.rateOverTime = 120f;
            var splashShape = splash.shape;
            splashShape.shapeType = ParticleSystemShapeType.Box;
            splashShape.scale = new Vector3(size.x, size.y, 0.01f);
            splashShape.randomDirectionAmount = 0.6f;
            FadeInOut(splash, splashMain.startColor.color);
            Set(anchor, "_particles", splash);
            return anchor;
        }

        /// <summary>
        /// Sadness: a puddle on the floor that grows with the sadness (a flat, dark, glossy disc). Position: the floor;
        /// radius in meters, stretch: how much longer than wide (1 = round), yaw: its direction.
        /// </summary>
        internal static EmotionEffectAnchor AddPuddle(Transform parent, string name, Vector3 position, float radius, float stretch, float yaw)
        {
            var anchor = NewAnchor(parent, name, position + Vector3.up * 0.004f, Quaternion.Euler(0f, yaw, 0f), Emotion.Sadness,
                EmotionEffectKind.Drips, out var content);
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = "Puddle";
            Object.DestroyImmediate(puddle.GetComponent<Collider>());
            puddle.transform.SetParent(content.transform, false);
            // A cylinder is 2 m high and 1 m wide: scale y to 2 mm.
            puddle.transform.localScale = new Vector3(radius * 2f, 0.001f, radius * 2f * stretch);
            var renderer = puddle.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PuddleMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            Set(anchor, "_grow", content.transform);
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("_swayDegrees").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return anchor;
        }

        private const string PuddleMaterialPath = "Assets/_Project/Materials/Emotions/Puddle.mat";

        /// <summary>
        /// A dark, wet look: URP Lit, transparent, very smooth (it reflects the room lights).
        /// </summary>
        private static Material PuddleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PuddleMaterialPath);
            if (material != null)
                return material;
            XRTestSceneBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PuddleMaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Puddle" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.16f, 0.2f, 0.27f, 0.62f));
            material.SetFloat("_Smoothness", 0.95f);
            material.SetFloat("_Metallic", 0.1f);
            AssetDatabase.CreateAsset(material, PuddleMaterialPath);
            return material;
        }

        /// <summary>
        /// The floor under a point: the first collider below it (not a trigger), or the point itself when there is none.
        /// So an effect never floats when the room's floor is not where the builder expects it.
        /// </summary>
        internal static Vector3 OnFloor(Vector3 point, float searchUp = 0.5f, float searchDown = 3f)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(point + Vector3.up * searchUp, Vector3.down, searchUp + searchDown, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var floor = point;
            foreach (var hit in hits)
            {
                if (hit.distance < best && hit.normal.y > 0.7f)
                {
                    best = hit.distance;
                    floor = hit.point;
                }
            }
            return floor;
        }

        /// <summary>
        /// Sadness: a few drops falling from the ceiling.
        /// </summary>
        internal static EmotionEffectAnchor AddDrips(Transform parent, string name, Vector3 position, float fallHeight)
        {
            var anchor = NewAnchor(parent, name, position, Quaternion.identity, Emotion.Sadness, EmotionEffectKind.Drips, out var content);
            var drips = Particles(content.transform, "Drops", Vector3.zero, Quaternion.identity, AlphaBlended(), 10, true);
            var main = drips.main;
            main.startLifetime = Mathf.Sqrt(2f * fallHeight / 9.81f);
            main.startSpeed = 0f;
            main.gravityModifier = 1f;
            main.startSize = 0.025f;
            main.startColor = new Color(0.72f, 0.8f, 0.92f, 0.85f);
            var emission = drips.emission;
            emission.rateOverTime = 1.2f;
            var shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            var renderer = drips.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.01f;
            Set(anchor, "_particles", drips);
            return anchor;
        }

        // ---- Helpers --------------------------------------------------------------------------------------------

        private static EmotionEffectAnchor NewAnchor(Transform parent, string name, Vector3 position, Quaternion rotation, Emotion emotion,
            EmotionEffectKind kind, out GameObject content)
        {
            var go = new GameObject($"{name} ({emotion})");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            content = new GameObject("Content");
            content.transform.SetParent(go.transform, false);
            var anchor = go.AddComponent<EmotionEffectAnchor>();
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("_emotion").enumValueIndex = (int)emotion;
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.FindProperty("_content").objectReferenceValue = content;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.SetActive(false);
            return anchor;
        }

        private static void Set(EmotionEffectAnchor anchor, string property, Object value)
        {
            var serialized = new SerializedObject(anchor);
            var field = serialized.FindProperty(property);
            if (field.isArray)
            {
                field.arraySize++;
                field.GetArrayElementAtIndex(field.arraySize - 1).objectReferenceValue = value;
            }
            else
                field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem Particles(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Material material,
            int maxParticles, bool worldSpace = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = false;
            main.maxParticles = Mathf.Min(maxParticles, MaxParticlesPerSystem);
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return particles;
        }

        private static void FadeInOut(ParticleSystem particles, Color color)
        {
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colors.color = gradient;
        }

        private static Material Additive() => ParticleMaterial("Particle_Additive", BlendMode.SrcAlpha, BlendMode.One);

        private static Material AlphaBlended() => ParticleMaterial("Particle_Alpha", BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

        private static Material ParticleMaterial(string name, BlendMode source, BlendMode destination)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                XRTestSceneBuilder.EnsureFolder(MaterialFolder);
                material = new Material(Shader.Find(ParticleShader)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(ParticleShader);
            material.mainTexture = SoftDot();
            material.SetFloat("_SrcBlend", (float)source);
            material.SetFloat("_DstBlend", (float)destination);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A 64x64 soft white dot (alpha falls off to the edge), the texture of every particle.
        /// </summary>
        private static Texture2D SoftDot()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
            if (texture != null)
                return texture;
            XRTestSceneBuilder.EnsureFolder(MaterialFolder);
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255f));
            }
            var generated = new Texture2D(size, size, TextureFormat.RGBA32, false);
            generated.SetPixels32(pixels);
            generated.Apply();
            File.WriteAllBytes(SoftDotPath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(SoftDotPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftDotPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        }
    }
}
