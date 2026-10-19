using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// What one <see cref="Mood"/> looks and sounds like: its color (door sign, room light), the main light,
    /// ambient color and background music. Create one via Create > ProCommVR > Mood Profile and add it to
    /// the MoodController(s) in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMoodProfile", menuName = "ProCommVR/Mood Profile")]
    public class MoodProfile : ScriptableObject
    {
        [Tooltip("The mood this profile belongs to.")]
        [SerializeField] private Mood _mood = Mood.Neutral;

        [Tooltip("The color of this mood: shown on the door sign and used for the patient room light.")]
        [SerializeField] private Color _color = Color.white;

        [Header("Atmosphere (MoodAtmosphere)")]
        [SerializeField] private Color _lightColor = Color.white;
        [SerializeField, Min(0f)] private float _lightIntensity = 1f;

        [Tooltip("Flat ambient light color (fills the shadows).")]
        [SerializeField] private Color _ambientColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("Music")]
        [Tooltip("Looping background music. Leave empty for silence.")]
        [SerializeField] private AudioClip _music;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.4f;

        public Mood Mood => _mood;
        public Color Color => _color;
        public Color LightColor => _lightColor;
        public float LightIntensity => _lightIntensity;
        public Color AmbientColor => _ambientColor;
        public AudioClip Music => _music;
        public float MusicVolume => _musicVolume;
    }
}
