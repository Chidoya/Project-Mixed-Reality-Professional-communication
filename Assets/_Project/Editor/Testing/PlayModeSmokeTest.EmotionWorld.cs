using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The room reacting to the patient's emotion in Scenario01_PatientRoom: within the Quest budget (no Bloom, at most
    /// one extra light per emotion, at most 200 particles per system, no shadows); in the nurse view nothing changes
    /// whatever the emotions are; in the patient view every emotion shows its own effects (volume, lights, anchors,
    /// sound) and only those; switching emotions blends through neutral without jumps and never has two emotions (or two
    /// extra lights) at once; a mood change of the room light during the patient view is kept; back in the nurse view
    /// everything is exactly as before. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunEmotionWorld
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private static readonly Emotion[] WorldEmotions = { Emotion.Joy, Emotion.Anger, Emotion.Fear, Emotion.Sadness };

        [MenuItem("ProCommVR/Tests/Emotion World (Scenario 01)")]
        public static void RunEmotionWorld()
        {
            Launch("world", Scenario01ScenePath);
        }

        private struct RoomSnapshot
        {
            public Color LightColor;
            public float LightIntensity;
            public Color Ambient;
            public bool Fog;
            public Color FogColor;
            public float FogDensity;
            public bool PostProcessing;

            public static RoomSnapshot Take(Light light, UniversalAdditionalCameraData camera) => new RoomSnapshot
            {
                LightColor = light.color,
                LightIntensity = light.intensity,
                Ambient = RenderSettings.ambientLight,
                Fog = RenderSettings.fog,
                FogColor = RenderSettings.fogColor,
                FogDensity = RenderSettings.fogDensity,
                PostProcessing = camera.renderPostProcessing,
            };

            public bool Same(RoomSnapshot other) =>
                LightColor == other.LightColor && Mathf.Approximately(LightIntensity, other.LightIntensity) && Ambient == other.Ambient &&
                Fog == other.Fog && FogColor == other.FogColor && Mathf.Approximately(FogDensity, other.FogDensity) &&
                PostProcessing == other.PostProcessing;

            public override string ToString() =>
                $"light {LightColor} x{LightIntensity:F2}, ambient {Ambient}, fog {Fog} {FogDensity:F3}, post {PostProcessing}";
        }

        private static IEnumerator EmotionWorldTest()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(world != null && holder != null && pov != null && presenter != null && origin != null,
                "scene has a PatientWorldController, EmotionHolder, PovController, presenter and XR Origin");
            if (Errors.Count > 0)
                yield break;
            var roomLight = world.RoomLights.FirstOrDefault();
            var cameraData = origin.Camera.GetUniversalAdditionalCameraData();
            Check(roomLight != null, "the controller knows the room light");
            if (Errors.Count > 0)
                yield break;

            CheckBudget(world);
            var anchors = world.Anchors;
            Check(anchors.All(a => !a.IsOn) && world.IsNeutral && world.Volume.weight == 0f && !cameraData.renderPostProcessing,
                "at the start: no effect on, volume weight 0, no post-processing on the camera");

            AddSimulatedDevices();
            AddLeftController();
            EditorApplication.update += KeepHeadLocal;
            EditorApplication.update += UpdateDevices;
            EditorApplication.update += UpdateLeftController;
            try
            {
                var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
                var toPlayer = Flat(origin.transform.position - patient.position).normalized;
                Teleport(origin, patient.position + toPlayer * 1.0f, patient.position);
                _lastSpacePosition = null;
                // Wait (in game time) until the scenario's mood fade (MoodAtmosphere, 1.5 s) is done and the light is steady.
                var waitUntil = EditorApplication.timeSinceStartup + 30f;
                while (EditorApplication.timeSinceStartup < waitUntil && Time.timeSinceLevelLoad < 3f)
                    yield return null;
                yield return 0.5f;

                // Nurse view: every emotion, nothing changes.
                var before = RoomSnapshot.Take(roomLight, cameraData);
                Screenshot("world-nurse-before");
                foreach (var emotion in WorldEmotions)
                {
                    holder.SetImmediate(Strong(emotion));
                    yield return 1f;
                    var now = RoomSnapshot.Take(roomLight, cameraData);
                    Check(now.Same(before) && anchors.All(a => !a.IsOn) && world.Volume.weight == 0f && world.IsNeutral,
                        $"nurse view, {emotion} dominant: the room is unchanged ({now})");
                }

                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Patient, "Y switches to the patient view");
                // Look straight ahead, toward the window.
                _headRotation = Quaternion.LookRotation(Flat(origin.Camera.transform.forward));

                foreach (var emotion in WorldEmotions)
                {
                    holder.SetImmediate(Strong(emotion));
                    foreach (var step in WaitForWorld(world, emotion))
                        yield return step;
                    CheckEmotionShown(world, emotion, roomLight, before, cameraData);
                    Screenshot($"world-{emotion.ToString().ToLowerInvariant()}");
                    // The room around the window: the floor, the chair and the cabinet.
                    var ahead = _headRotation;
                    _headRotation = Quaternion.AngleAxis(-35f, Vector3.up) * ahead * Quaternion.Euler(25f, 0f, 0f);
                    yield return 0.6f;
                    Screenshot($"world-{emotion.ToString().ToLowerInvariant()}-room");
                    _headRotation = ahead;
                    yield return 0.3f;
                }

                // Switching emotions: through neutral, smoothly, never two emotions or two extra lights at once.
                var from = WorldEmotions[WorldEmotions.Length - 1];
                var to = WorldEmotions[0];
                holder.SetImmediate(Strong(to));
                var lastWeight = world.Weight;
                var biggestStep = 0f;
                var mostEmotions = 0;
                var mostLights = 0;
                var sawNeutral = false;
                var until = EditorApplication.timeSinceStartup + world.BlendSeconds * 3f + 2f;
                while (EditorApplication.timeSinceStartup < until && !(world.ShownEmotion == to && world.Weight > 0.99f))
                {
                    yield return null;
                    // Per second, so slow batchmode frames do not count as jumps.
                    if (Time.deltaTime > 0f)
                        biggestStep = Mathf.Max(biggestStep, Mathf.Abs(world.Weight - lastWeight) / Time.deltaTime);
                    lastWeight = world.Weight;
                    mostEmotions = Mathf.Max(mostEmotions, anchors.Where(a => a.IsOn).Select(a => a.Emotion).Distinct().Count());
                    mostLights = Mathf.Max(mostLights, anchors.Count(a => a.IsOn && a.Light != null));
                    sawNeutral |= world.IsNeutral;
                }
                Check(world.ShownEmotion == to && world.Weight > 0.99f, $"{from} to {to}: {to} is shown in the end");
                Check(sawNeutral && mostEmotions <= 1 && mostLights <= 1,
                    $"{from} to {to}: through neutral (seen {sawNeutral}), at most one emotion ({mostEmotions}) and one extra light ({mostLights}) at a time");
                // Eased (smoothstep, slope at most 1.5), back to neutral twice as fast as in.
                var fastest = 1.5f * 2f / world.BlendSeconds;
                Check(biggestStep <= fastest * 1.1f, $"{from} to {to}: no jumps (fastest change {biggestStep:F2}/s, allowed {fastest:F2}/s)");

                // A mood change of the room light while in the patient view is kept on the way back.
                var moodIntensity = before.LightIntensity * 0.8f;
                roomLight.intensity = moodIntensity;
                yield return 0.3f;

                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Nurse, "Y back to the nurse view");
                Check(roomLight.color == before.LightColor, $"after a mood change of only the intensity the light color is restored ({roomLight.color})");
                Check(Mathf.Approximately(roomLight.intensity, moodIntensity),
                    $"a mood change during the patient view is kept (light x{roomLight.intensity:F2}, set to x{moodIntensity:F2})");
                roomLight.intensity = before.LightIntensity;
                yield return 0.5f;
                var after = RoomSnapshot.Take(roomLight, cameraData);
                var audio = world.GetComponent<AudioSource>();
                Check(after.Same(before), $"back in the nurse view the room is exactly as before ({after})");
                Check(world.IsNeutral && anchors.All(a => !a.IsOn) && world.Volume.weight == 0f && (audio == null || !audio.isPlaying),
                    "back in the nurse view: no effect, no volume, no sound");
                Screenshot("world-nurse-after");
            }
            finally
            {
                EditorApplication.update -= KeepHeadLocal;
                EditorApplication.update -= UpdateDevices;
                EditorApplication.update -= UpdateLeftController;
                if (_leftController != null && _leftController.added)
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(_leftController);
                _leftController = null;
                _lastSpacePosition = null;
                RemoveSimulatedDevices();
            }
        }

        private static EmotionState Strong(Emotion emotion) => EmotionState.Zero.With(emotion, 0.9f).With(
            emotion == Emotion.Sadness ? Emotion.Anger : Emotion.Sadness, 0.1f);

        private static IEnumerable WaitForWorld(PatientWorldController world, Emotion emotion)
        {
            var until = EditorApplication.timeSinceStartup + world.BlendSeconds * 3f + 3f;
            while (EditorApplication.timeSinceStartup < until && !(world.ShownEmotion == emotion && world.Weight > 0.99f))
                yield return null;
            yield return 0.3f;
        }

        private static void CheckBudget(PatientWorldController world)
        {
            var profile = world.Volume.sharedProfile;
            Check(profile.Has<ColorAdjustments>() && profile.Has<Vignette>() && !profile.Has<Bloom>(),
                "the volume has Color Adjustments and Vignette, and no Bloom");
            foreach (var emotion in WorldEmotions)
            {
                var anchors = world.Anchors.Where(a => a.Emotion == emotion).ToArray();
                Check(anchors.Length > 0 && world.GetProfile(emotion) != null, $"{emotion}: a profile and {anchors.Length} effect anchors");
                Check(anchors.Count(a => a.Light != null) <= 1, $"{emotion}: at most one extra light");
            }
            var systems = world.Anchors.SelectMany(a => a.Particles).ToArray();
            Check(systems.All(p => p.main.maxParticles <= EmotionWorldSetup.MaxParticlesPerSystem),
                $"all {systems.Length} particle systems have at most {EmotionWorldSetup.MaxParticlesPerSystem} particles (max {systems.Select(p => p.main.maxParticles).DefaultIfEmpty(0).Max()})");
            var lights = world.Anchors.Where(a => a.Light != null).Select(a => a.Light).ToArray();
            Check(lights.All(l => l.shadows == LightShadows.None), $"no extra light casts shadows ({lights.Length} lights)");
            var renderers = world.Anchors.SelectMany(a => a.GetComponentsInChildren<Renderer>(true)).ToArray();
            Check(renderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off),
                $"no effect renderer casts shadows ({renderers.Length} renderers)");
        }

        private static void CheckEmotionShown(PatientWorldController world, Emotion emotion, Light roomLight, RoomSnapshot nurse,
            UniversalAdditionalCameraData cameraData)
        {
            var profile = world.GetProfile(emotion);
            Check(world.ShownEmotion == emotion && world.Weight > 0.99f, $"patient view, {emotion}: shown at full weight ({world.Weight:F2})");
            Check(world.Volume.weight > 0.99f && cameraData.renderPostProcessing, $"{emotion}: volume on and post-processing on the camera");
            world.Volume.profile.TryGet(out ColorAdjustments color);
            Check(color != null && Mathf.Abs(color.saturation.value - profile.Saturation) < 0.5f &&
                  ((Vector4)(color.colorFilter.value - profile.ColorFilter)).magnitude < 0.02f,
                $"{emotion}: saturation {color?.saturation.value:F1} (profile {profile.Saturation:F1}), filter {color?.colorFilter.value}");
            var expected = nurse.LightIntensity * profile.LightIntensity;
            Check(Mathf.Abs(roomLight.intensity - expected) < 0.02f,
                $"{emotion}: room light x{roomLight.intensity:F2} (expected x{expected:F2})");
            var on = world.Anchors.Where(a => a.IsOn).ToList();
            var wanted = world.Anchors.Where(a => a.Emotion == emotion && profile.Enables(a.Kind)).ToList();
            Check(wanted.Count > 0 && on.Count == wanted.Count && wanted.All(on.Contains),
                $"{emotion}: exactly its {wanted.Count} effects are on ({string.Join(", ", on.Select(a => a.name))})");
            var particlesPlaying = wanted.SelectMany(a => a.Particles).All(p => p.isPlaying && p.emission.rateOverTimeMultiplier > 0f);
            Check(particlesPlaying, $"{emotion}: its particles are playing");
            var audio = world.GetComponent<AudioSource>();
            Check(audio != null && audio.isPlaying && audio.clip == profile.Sound && audio.volume > 0.01f,
                $"{emotion}: its sound is playing ({(audio != null && audio.clip != null ? audio.clip.name : "none")})");
        }
    }
}
