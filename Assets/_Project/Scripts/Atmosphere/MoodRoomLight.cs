using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Gives one light in a room the color of the current mood of a <see cref="MoodController"/>,
    /// e.g. green in the room of a happy patient. Only the color changes, not the intensity, unless the mood's
    /// profile has a Flash Rate (e.g. Alarm): then the light flashes between brighter than its own intensity and almost off.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MoodRoomLight : MonoBehaviour
    {
        [SerializeField] private MoodController _moodController;

        [Tooltip("How strongly the light takes the mood color: 0 = white, 1 = the full mood color.")]
        [SerializeField, Range(0f, 1f)] private float _strength = 1f;

        [Tooltip("Intensity at the bright moment of a flash, as a multiple of the light's own intensity.")]
        [SerializeField, Range(1f, 3f)] private float _flashHigh = 2f;

        [Tooltip("Intensity at the dark moment of a flash, as a fraction of the light's own intensity.")]
        [SerializeField, Range(0f, 1f)] private float _flashLow = 0.1f;

        private Light _light;
        private float _intensity;
        private float _flashRate;
        private float _flashTime;

        public MoodController MoodController => _moodController;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _intensity = _light.intensity;
        }

        private void Update()
        {
            if (_flashRate <= 0f)
                return;
            _flashTime += Time.deltaTime;
            var on = Mathf.Repeat(_flashTime * _flashRate, 1f) < 0.5f;
            _light.intensity = _intensity * (on ? _flashHigh : _flashLow);
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
            _flashRate = moodController.CurrentProfile.FlashRate;
            _flashTime = 0f;
            _light.intensity = _intensity;
        }
    }
}
