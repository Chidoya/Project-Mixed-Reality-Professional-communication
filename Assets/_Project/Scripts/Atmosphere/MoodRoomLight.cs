using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Gives one light in a room the color of the current mood of a <see cref="MoodController"/>,
    /// e.g. green in the room of a happy patient. Only the color changes, not the intensity.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MoodRoomLight : MonoBehaviour
    {
        [SerializeField] private MoodController _moodController;

        [Tooltip("How strongly the light takes the mood color: 0 = white, 1 = the full mood color.")]
        [SerializeField, Range(0f, 1f)] private float _strength = 1f;

        private Light _light;

        private void Awake()
        {
            _light = GetComponent<Light>();
        }

        private void OnEnable()
        {
            if (_moodController == null)
                return;
            _moodController.MoodChanged += OnMoodChanged;
            if (_moodController.CurrentProfile != null)
                OnMoodChanged(_moodController);
        }

        private void OnDisable()
        {
            if (_moodController != null)
                _moodController.MoodChanged -= OnMoodChanged;
        }

        private void OnMoodChanged(MoodController moodController)
        {
            _light.color = Color.Lerp(Color.white, moodController.CurrentColor, _strength);
        }
    }
}
