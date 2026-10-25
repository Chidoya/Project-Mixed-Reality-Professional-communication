using System.Collections;
using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// Plays the looping sound of the current mood of a <see cref="MoodController"/> (the profile's Sound) and
    /// crossfades to the next sound when the mood changes. Uses the AudioSource on this object and a copy of it,
    /// so the old and the new sound can overlap during the fade. A mood without a sound fades to silence.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MoodAudio : MonoBehaviour
    {
        [SerializeField] private MoodController _moodController;

        [Tooltip("Crossfade time between two mood sounds, in seconds.")]
        [SerializeField, Min(0f)] private float _fadeDuration = 2f;

        private AudioSource _current;
        private AudioSource _next;
        private Coroutine _fade;

        /// <summary>
        /// The source that plays (or fades in) the sound of the current mood.
        /// </summary>
        public AudioSource CurrentSource => _current;

        private void Awake()
        {
            _current = GetComponent<AudioSource>();
            _current.playOnAwake = false;
            _current.loop = true;
            _next = CopySource(_current);
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
            var profile = moodController.CurrentProfile;
            if (_current.isPlaying && _current.clip == profile.Sound)
                return;

            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(profile.Sound, profile.SoundVolume));
        }

        /// <summary>
        /// The new sound starts on the idle source and fades in while the old one fades out; then they swap roles.
        /// </summary>
        private IEnumerator Crossfade(AudioClip clip, float volume)
        {
            var fadingOut = _current;
            var fadingIn = _next;
            var startVolume = fadingOut.isPlaying ? fadingOut.volume : 0f;

            fadingIn.Stop();
            fadingIn.clip = clip;
            fadingIn.volume = 0f;
            if (clip != null)
                fadingIn.Play();
            _current = fadingIn;
            _next = fadingOut;

            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(_fadeDuration, 0.0001f))
            {
                fadingOut.volume = Mathf.Lerp(startVolume, 0f, t);
                fadingIn.volume = Mathf.Lerp(0f, volume, t);
                yield return null;
            }
            fadingOut.Stop();
            fadingOut.volume = 0f;
            fadingIn.volume = volume;
            _fade = null;
        }

        private static AudioSource CopySource(AudioSource source)
        {
            var copy = source.gameObject.AddComponent<AudioSource>();
            copy.outputAudioMixerGroup = source.outputAudioMixerGroup;
            copy.playOnAwake = false;
            copy.loop = true;
            copy.priority = source.priority;
            copy.spatialBlend = source.spatialBlend;
            copy.rolloffMode = source.rolloffMode;
            copy.minDistance = source.minDistance;
            copy.maxDistance = source.maxDistance;
            copy.dopplerLevel = source.dopplerLevel;
            copy.spread = source.spread;
            copy.volume = 0f;
            return copy;
        }
    }
}
