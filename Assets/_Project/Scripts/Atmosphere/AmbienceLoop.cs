using UnityEngine;

namespace ProCommVR.Atmosphere
{
    /// <summary>
    /// A looping ambience sound (the murmur of people in the ward) that starts at a random point of its clip, so several
    /// sources with the same clip never sound in step.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbienceLoop : MonoBehaviour
    {
        private void Start()
        {
            var source = GetComponent<AudioSource>();
            if (source.clip == null)
                return;
            source.loop = true;
            source.time = Random.Range(0f, source.clip.length);
            source.Play();
        }
    }
}
