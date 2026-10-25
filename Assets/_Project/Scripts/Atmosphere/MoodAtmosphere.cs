using System.Collections;
using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Sets the light of the scene (main light and ambient color) to the profile of the current mood of a
    /// <see cref="MoodController"/>, with a short fade. The mood's sound is played by <see cref="MoodAudio"/>.
    /// </summary>
    public class MoodAtmosphere : MonoBehaviour
    {
        [SerializeField] private MoodController _moodController;

        [Tooltip("The main light whose color and intensity follow the mood.")]
        [SerializeField] private Light _light;

        [Tooltip("Fade time between moods, in seconds.")]
        [SerializeField, Min(0f)] private float _fadeDuration = 1.5f;

        private Coroutine _lightFade;

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
            var profile = moodController.CurrentProfile;

            if (_lightFade != null)
                StopCoroutine(_lightFade);
            _lightFade = StartCoroutine(FadeLight(profile));
        }

        private IEnumerator FadeLight(MoodProfile profile)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var startAmbient = RenderSettings.ambientLight;
            var startColor = _light != null ? _light.color : Color.white;
            var startIntensity = _light != null ? _light.intensity : 0f;

            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(_fadeDuration, 0.0001f))
            {
                SetLight(Color.Lerp(startAmbient, profile.AmbientColor, t),
                    Color.Lerp(startColor, profile.LightColor, t),
                    Mathf.Lerp(startIntensity, profile.LightIntensity, t));
                yield return null;
            }
            SetLight(profile.AmbientColor, profile.LightColor, profile.LightIntensity);
            _lightFade = null;
        }

        private void SetLight(Color ambient, Color color, float intensity)
        {
            RenderSettings.ambientLight = ambient;
            if (_light == null)
                return;
            _light.color = color;
            _light.intensity = intensity;
        }
    }
}
