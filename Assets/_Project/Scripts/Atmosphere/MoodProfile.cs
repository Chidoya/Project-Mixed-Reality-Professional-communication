using ProCommVR.Scenarios;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// What one <see cref="Mood"/> looks and sounds like: its color (door sign, room light), the main light,
    /// ambient color and a looping sound (music or ambience, played by <see cref="MoodAudio"/>). Create one via Create > ProCommVR > Mood Profile and add it to
    /// the MoodController(s) in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMoodProfile", menuName = "ProCommVR/Mood Profile")]
    public class MoodProfile : ScriptableObject
    {
        [Tooltip("The mood this profile belongs to.")]
        [SerializeField] private Mood _mood = Mood.Neutral;

        [Tooltip("The color of this mood: shown on the door sign and used for the patient room light.")]
        [SerializeField] private Color _color = Color.white;

        [Tooltip("Flashes per second of the room light (MoodRoomLight), e.g. for Alarm. 0 = steady light.")]
        [SerializeField, Min(0f)] private float _flashRate;

        [Tooltip("Brightness of the room light (MoodRoomLight) as a multiple of its own intensity, e.g. 0.45 for a dim room. 1 = unchanged.")]
        [SerializeField, Range(0f, 2f)] private float _roomLightIntensity = 1f;

        [Header("Atmosphere (MoodAtmosphere)")]
        [SerializeField] private Color _lightColor = Color.white;
        [SerializeField, Min(0f)] private float _lightIntensity = 1f;

        [Tooltip("Flat ambient light color (fills the shadows).")]
        [SerializeField] private Color _ambientColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("Sound (MoodAudio)")]
        [Tooltip("Looping sound of this mood: music or ambience. Leave empty for silence.")]
        [FormerlySerializedAs("_music")]
        [SerializeField] private AudioClip _sound;
        [FormerlySerializedAs("_musicVolume")]
        [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.4f;

        public Mood Mood => _mood;
        public Color Color => _color;
        public float FlashRate => _flashRate;
        public float RoomLightIntensity => _roomLightIntensity;
        public Color LightColor => _lightColor;
        public float LightIntensity => _lightIntensity;
        public Color AmbientColor => _ambientColor;
        public AudioClip Sound => _sound;
        public float SoundVolume => _soundVolume;
    }
}
