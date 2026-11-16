using System;
using System.Collections.Generic;
using ProCommVR.Pov;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// Makes the room react to the patient's dominant emotion, only in the patient view. In the nurse view the room is
    /// exactly as it would be without this component.
    /// <para>
    /// In the patient view it blends (about 2 s) from neutral to the <see cref="EmotionWorldProfile"/> of the dominant
    /// emotion, weighted by that emotion's strength. When the dominant emotion changes, the old one first blends back
    /// to neutral and then the new one blends in, so at most one emotion (and one extra light) is active. Back in the
    /// nurse view (the switch happens behind the black fade) everything is reset at once.
    /// </para>
    /// <para>
    /// What it drives: a global URP Volume (Color Adjustments and Vignette, no Bloom), with post-processing on the camera
    /// only while an effect is shown; the room's lights and the ambient color, multiplied on top of whatever the Mood
    /// system set (it notices when the mood changes them); fog, restored afterwards; the
    /// <see cref="EmotionEffectAnchor"/>s of the emotion; and the emotion's sound loop.
    /// </para>
    /// </summary>
    public class PatientWorldController : MonoBehaviour
    {
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private EmotionWorldProfile[] _profiles = Array.Empty<EmotionWorldProfile>();

        [Header("Room")]
        [Tooltip("Global volume with Color Adjustments and Vignette overrides. Its weight is 0 in the nurse view.")]
        [SerializeField] private Volume _volume;

        [Tooltip("The room's own lights (main light, ceiling lights). Their color and intensity are multiplied by the profile.")]
        [SerializeField] private Light[] _roomLights = Array.Empty<Light>();

        [Tooltip("Empty: every EmotionEffectAnchor in the scene.")]
        [SerializeField] private EmotionEffectAnchor[] _anchors = Array.Empty<EmotionEffectAnchor>();

        [Tooltip("Empty: the main camera. Post-processing is on only while an effect is shown.")]
        [SerializeField] private Camera _camera;

        [SerializeField] private AudioSource _audio;

        [Header("Blend")]
        [Tooltip("Seconds from neutral to the full effect.")]
        [SerializeField, Min(0.01f)] private float _blendSeconds = 2f;

        [Tooltip("The dominant emotion's value that gives the full effect (from the holder's threshold up to this, the effect grows).")]
        [SerializeField, Range(0.2f, 1f)] private float _fullStrengthAt = 0.6f;

        private struct LightBase
        {
            public Color Color;
            public float Intensity;
            public Color Written;
            public float WrittenIntensity;
            public bool Modified;
        }

        private LightBase[] _lightBases = Array.Empty<LightBase>();
        private Color _ambientBase;
        private Color _ambientWritten;
        private bool _ambientModified;
        private bool _fogSaved;
        private bool _fogEnabled;
        private FogMode _fogMode;
        private Color _fogColor;
        private float _fogDensity;
        private bool _postProcessingSaved;
        private bool _postProcessingWas;
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private UniversalAdditionalCameraData _cameraData;

        private Emotion? _shown;
        private float _weight;
        private Emotion? _targetEmotion;
        private float _targetWeight;

        /// <summary>The emotion whose effects are in the room right now (null: neutral).</summary>
        public Emotion? ShownEmotion => _weight > 0f ? _shown : null;

        /// <summary>How strongly it is shown, 0 to 1 (after easing).</summary>
        public float Weight => Mathf.SmoothStep(0f, 1f, _weight);

        public bool IsNeutral => _weight <= 0f;
        public float BlendSeconds => _blendSeconds;
        public Volume Volume => _volume;
        public IReadOnlyList<EmotionEffectAnchor> Anchors => _anchors;
        public IReadOnlyList<Light> RoomLights => _roomLights;
        public bool PostProcessingOn => _cameraData != null && _cameraData.renderPostProcessing;

        public EmotionWorldProfile GetProfile(Emotion emotion)
        {
            foreach (var profile in _profiles)
            {
                if (profile != null && profile.Emotion == emotion)
                    return profile;
            }
            return null;
        }

        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera != null)
                _cameraData = _camera.GetUniversalAdditionalCameraData();
            if (_anchors.Length == 0)
                _anchors = FindObjectsByType<EmotionEffectAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _lightBases = new LightBase[_roomLights.Length];
            if (_volume != null)
            {
                // The instance copy, so Play mode never changes the profile asset.
                var profile = _volume.profile;
                profile.TryGet(out _colorAdjustments);
                profile.TryGet(out _vignette);
                _volume.weight = 0f;
            }
            foreach (var anchor in _anchors)
                anchor.SetWeight(0f);
        }

        private void OnEnable()
        {
            if (_holder != null)
                _holder.EmotionChanged += OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
            UpdateTarget();
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.EmotionChanged -= OnEmotionChanged;
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            ResetToNeutral();
        }

        private void OnEmotionChanged(EmotionState previous, EmotionState current)
        {
            UpdateTarget();
        }

        private void OnPovChanged(PovMode mode)
        {
            UpdateTarget();
            // Back to the nurse: the view is still black, so the room is reset at once; nothing leaks into the nurse view.
            if (mode == PovMode.Nurse)
                ResetToNeutral();
        }

        private void UpdateTarget()
        {
            var inPatientView = _pov != null && _pov.Mode == PovMode.Patient;
            var dominant = _holder != null ? _holder.Dominant : null;
            if (!inPatientView || dominant == null || GetProfile(dominant.Value) == null)
            {
                _targetEmotion = null;
                _targetWeight = 0f;
                return;
            }
            _targetEmotion = dominant;
            var threshold = EmotionState.DefaultDominantThreshold;
            _targetWeight = Mathf.InverseLerp(threshold, Mathf.Max(_fullStrengthAt, threshold + 0.01f), _holder.Current.Get(dominant.Value));
        }

        private void LateUpdate()
        {
            var step = Time.deltaTime / _blendSeconds;
            if (_weight > 0f && _shown != _targetEmotion)
            {
                // Another emotion (or none) is wanted: first back to neutral, twice as fast.
                _weight = Mathf.MoveTowards(_weight, 0f, step * 2f);
            }
            else
            {
                if (_weight <= 0f)
                    _shown = _targetEmotion;
                var target = _shown == _targetEmotion ? _targetWeight : 0f;
                _weight = Mathf.MoveTowards(_weight, target, step);
            }

            if (_weight > 0f && _shown != null)
                Show(GetProfile(_shown.Value), Weight);
            else
                ResetToNeutral();
        }

        private void Show(EmotionWorldProfile profile, float weight)
        {
            if (_cameraData != null)
            {
                if (!_postProcessingSaved)
                {
                    _postProcessingSaved = true;
                    _postProcessingWas = _cameraData.renderPostProcessing;
                }
                _cameraData.renderPostProcessing = true;
            }

            if (_volume != null)
            {
                _volume.weight = 1f;
                if (_colorAdjustments != null)
                {
                    _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, profile.ColorFilter, weight);
                    _colorAdjustments.saturation.value = profile.Saturation * weight;
                    _colorAdjustments.postExposure.value = profile.PostExposure * weight;
                }
                if (_vignette != null)
                {
                    var pulse = profile.VignettePulseRate > 0f
                        ? profile.VignettePulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * profile.VignettePulseRate * Mathf.PI * 2f))
                        : 0f;
                    _vignette.intensity.value = Mathf.Clamp01((profile.VignetteIntensity + pulse) * weight);
                    _vignette.color.value = profile.VignetteColor;
                }
            }

            var lightTint = Color.Lerp(Color.white, profile.LightTint, weight);
            var lightIntensity = Mathf.Lerp(1f, profile.LightIntensity, weight);
            for (var i = 0; i < _roomLights.Length; i++)
            {
                var light = _roomLights[i];
                if (light == null)
                    continue;
                ref var lightBase = ref _lightBases[i];
                // Someone else (the Mood system) changed the color or the intensity since we wrote it: that is the new base.
                if (!lightBase.Modified || light.color != lightBase.Written)
                    lightBase.Color = light.color;
                if (!lightBase.Modified || !Mathf.Approximately(light.intensity, lightBase.WrittenIntensity))
                    lightBase.Intensity = light.intensity;
                light.color = lightBase.Color * lightTint;
                light.intensity = lightBase.Intensity * lightIntensity;
                lightBase.Written = light.color;
                lightBase.WrittenIntensity = light.intensity;
                lightBase.Modified = true;
            }

            if (!_ambientModified || RenderSettings.ambientLight != _ambientWritten)
                _ambientBase = RenderSettings.ambientLight;
            var ambient = _ambientBase * Color.Lerp(Color.white, profile.AmbientTint * profile.AmbientIntensity, weight);
            ambient.a = _ambientBase.a;
            RenderSettings.ambientLight = ambient;
            _ambientWritten = RenderSettings.ambientLight;
            _ambientModified = true;

            if (profile.Fog)
            {
                SaveFog();
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = _fogEnabled ? Color.Lerp(_fogColor, profile.FogColor, weight) : profile.FogColor;
                RenderSettings.fogDensity = Mathf.Lerp(_fogEnabled ? _fogDensity : 0f, profile.FogDensity, weight);
            }
            else
                RestoreFog();

            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    anchor.SetWeight(anchor.Emotion == profile.Emotion && profile.Enables(anchor.Kind) ? weight : 0f);
            }

            if (_audio != null)
            {
                if (_audio.clip != profile.Sound)
                {
                    _audio.Stop();
                    _audio.clip = profile.Sound;
                }
                _audio.volume = profile.SoundVolume * weight;
                if (_audio.clip != null && !_audio.isPlaying)
                    _audio.Play();
            }
        }

        /// <summary>
        /// Puts everything back exactly as it was: volume, camera, lights, ambient, fog, effects, sound.
        /// </summary>
        private void ResetToNeutral()
        {
            _weight = 0f;
            if (_volume != null)
                _volume.weight = 0f;
            if (_postProcessingSaved && _cameraData != null)
                _cameraData.renderPostProcessing = _postProcessingWas;
            _postProcessingSaved = false;

            for (var i = 0; i < _roomLights.Length; i++)
            {
                ref var lightBase = ref _lightBases[i];
                if (!lightBase.Modified)
                    continue;
                var light = _roomLights[i];
                // Only restore what nobody changed after us (otherwise their value is already the right one).
                if (light != null && light.color == lightBase.Written)
                    light.color = lightBase.Color;
                if (light != null && Mathf.Approximately(light.intensity, lightBase.WrittenIntensity))
                    light.intensity = lightBase.Intensity;
                lightBase.Modified = false;
            }

            if (_ambientModified && RenderSettings.ambientLight == _ambientWritten)
                RenderSettings.ambientLight = _ambientBase;
            _ambientModified = false;
            RestoreFog();

            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    anchor.SetWeight(0f);
            }
            if (_audio != null && _audio.isPlaying)
                _audio.Stop();
        }

        private void SaveFog()
        {
            if (_fogSaved)
                return;
            _fogSaved = true;
            _fogEnabled = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
        }

        private void RestoreFog()
        {
            if (!_fogSaved)
                return;
            _fogSaved = false;
            RenderSettings.fog = _fogEnabled;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
        }
    }
}
