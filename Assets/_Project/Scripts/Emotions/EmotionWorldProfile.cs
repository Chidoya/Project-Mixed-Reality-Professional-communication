using System;
using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// The kinds of effect objects in the room (placed by the scene builder as <see cref="EmotionEffectAnchor"/>s).
    /// </summary>
    public enum EmotionEffectKind
    {
        Plant,
        SunBeam,
        Fire,
        FireLight,
        Flashlight,
        Rain,
        Drips,
    }

    /// <summary>
    /// How the room looks in the patient view when one emotion is dominant at full strength. The
    /// <see cref="PatientWorldController"/> blends between neutral (no change at all) and this profile, weighted by the
    /// emotion's strength. Screen values go to a URP Volume (Color Adjustments and Vignette, no Bloom); light values
    /// multiply the room's own lights and ambient color, so they stack on top of the Mood system without replacing it.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotionWorld", menuName = "ProCommVR/Emotion World Profile")]
    public class EmotionWorldProfile : ScriptableObject
    {
        [SerializeField] private Emotion _emotion;

        [Header("Screen (URP Volume)")]
        [Tooltip("Multiplies the colors on screen: white is no change.")]
        [SerializeField, ColorUsage(false)] private Color _colorFilter = Color.white;

        [Tooltip("-100 is grey, 0 no change, +100 twice as colorful.")]
        [SerializeField, Range(-100f, 100f)] private float _saturation;

        [Tooltip("Brightness of the whole picture in EV: 0 no change, -1 half as bright (also the sky through the window).")]
        [SerializeField, Range(-4f, 2f)] private float _postExposure;

        [SerializeField, Range(0f, 1f)] private float _vignetteIntensity;
        [SerializeField, ColorUsage(false)] private Color _vignetteColor = Color.black;

        [Tooltip("Vignette pulses per second (0: no pulse).")]
        [SerializeField, Min(0f)] private float _vignettePulseRate;

        [Tooltip("How much the vignette grows at the top of a pulse.")]
        [SerializeField, Range(0f, 0.5f)] private float _vignettePulseAmount;

        [Header("Room lights")]
        [Tooltip("Multiplies the color of the room's lights.")]
        [SerializeField, ColorUsage(false)] private Color _lightTint = Color.white;

        [Tooltip("Multiplies the intensity of the room's lights.")]
        [SerializeField, Min(0f)] private float _lightIntensity = 1f;

        [Tooltip("Multiplies the ambient color (tint times intensity).")]
        [SerializeField, ColorUsage(false)] private Color _ambientTint = Color.white;

        [SerializeField, Min(0f)] private float _ambientIntensity = 1f;

        [Header("Fog")]
        [SerializeField] private bool _fog;
        [SerializeField, ColorUsage(false)] private Color _fogColor = Color.grey;
        [SerializeField, Range(0f, 1f)] private float _fogDensity = 0.05f;

        [Header("Effects")]
        [Tooltip("The effect objects of this emotion that are switched on.")]
        [SerializeField] private EmotionEffectKind[] _effects = Array.Empty<EmotionEffectKind>();

        [Header("Sound")]
        [Tooltip("A loop, faded in with the effect.")]
        [SerializeField] private AudioClip _sound;
        [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.35f;

        public Emotion Emotion => _emotion;
        public Color ColorFilter => _colorFilter;
        public float Saturation => _saturation;
        public float PostExposure => _postExposure;
        public float VignetteIntensity => _vignetteIntensity;
        public Color VignetteColor => _vignetteColor;
        public float VignettePulseRate => _vignettePulseRate;
        public float VignettePulseAmount => _vignettePulseAmount;
        public Color LightTint => _lightTint;
        public float LightIntensity => _lightIntensity;
        public Color AmbientTint => _ambientTint;
        public float AmbientIntensity => _ambientIntensity;
        public bool Fog => _fog;
        public Color FogColor => _fogColor;
        public float FogDensity => _fogDensity;
        public AudioClip Sound => _sound;
        public float SoundVolume => _soundVolume;

        public bool Enables(EmotionEffectKind kind) => Array.IndexOf(_effects, kind) >= 0;
    }
}
