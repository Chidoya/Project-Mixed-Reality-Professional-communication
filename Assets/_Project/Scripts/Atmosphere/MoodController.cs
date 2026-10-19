using System.Collections;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Sets the atmosphere (light, ambient color, music) to the mood of the scenario that starts,
    /// with a short fade. Listens to <see cref="ScenarioPresenter.ScenarioStarted"/>.
    /// </summary>
    public class MoodController : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("One profile per mood. A mood without a profile leaves the atmosphere unchanged.")]
        [SerializeField] private MoodProfile[] _profiles;

        [Tooltip("The main light whose color and intensity follow the mood.")]
        [SerializeField] private Light _light;

        [Tooltip("Plays the background music. Leave empty for no music.")]
        [SerializeField] private AudioSource _musicSource;

        [Tooltip("Fade time between moods, in seconds.")]
        [SerializeField, Min(0f)] private float _fadeDuration = 1.5f;

        private Coroutine _lightFade;
        private Coroutine _musicFade;

        public Mood? CurrentMood { get; private set; }

        private void OnEnable()
        {
            if (_presenter != null)
                _presenter.ScenarioStarted += OnScenarioStarted;
        }

        private void OnDisable()
        {
            if (_presenter != null)
                _presenter.ScenarioStarted -= OnScenarioStarted;
        }

        public void ApplyMood(Mood mood)
        {
            if (CurrentMood == mood)
                return;

            var profile = FindProfile(mood);
            if (profile == null)
            {
                Debug.LogWarning($"[ProCommVR] No mood profile for '{mood}'; atmosphere unchanged.", this);
                return;
            }
            CurrentMood = mood;

            if (_lightFade != null)
                StopCoroutine(_lightFade);
            _lightFade = StartCoroutine(FadeLight(profile));

            if (_musicSource != null)
            {
                if (_musicFade != null)
                    StopCoroutine(_musicFade);
                _musicFade = StartCoroutine(FadeMusic(profile));
            }
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            ApplyMood(scenario.Mood);
        }

        private MoodProfile FindProfile(Mood mood)
        {
            foreach (var profile in _profiles)
            {
                if (profile != null && profile.Mood == mood)
                    return profile;
            }
            return null;
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

        /// <summary>
        /// Fades the current track out, swaps the clip and fades the new one in.
        /// </summary>
        private IEnumerator FadeMusic(MoodProfile profile)
        {
            var halfFade = _fadeDuration * 0.5f;

            if (_musicSource.isPlaying && _musicSource.clip != profile.Music)
            {
                var startVolume = _musicSource.volume;
                for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(halfFade, 0.0001f))
                {
                    _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                    yield return null;
                }
                _musicSource.Stop();
            }

            if (profile.Music == null)
            {
                _musicFade = null;
                yield break;
            }

            if (_musicSource.clip != profile.Music || !_musicSource.isPlaying)
            {
                _musicSource.clip = profile.Music;
                _musicSource.loop = true;
                _musicSource.volume = 0f;
                _musicSource.Play();
            }

            var fromVolume = _musicSource.volume;
            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(halfFade, 0.0001f))
            {
                _musicSource.volume = Mathf.Lerp(fromVolume, profile.MusicVolume, t);
                yield return null;
            }
            _musicSource.volume = profile.MusicVolume;
            _musicFade = null;
        }
    }
}
