using UnityEngine;

namespace ProCommVR.Pov
{
    /// <summary>
    /// The limits of the POV switch, so it does not become a cheat button. Tune them in the asset, no code needed.
    /// Create one via Create > ProCommVR > POV Switch Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "PovSwitchSettings", menuName = "ProCommVR/POV Switch Settings")]
    public class PovSwitchSettings : ScriptableObject
    {
        [Tooltip("How often the player may switch to the patient view in one conversation. Switching back is always allowed.")]
        [SerializeField, Min(0)] private int _maxSwitchesPerConversation = 3;

        [Tooltip("Minimum seconds between two switches (both ways), so a quick double press is ignored.")]
        [SerializeField, Min(0f)] private float _minSecondsBetweenSwitches = 2f;

        [Tooltip("Seconds of the fade to black, and the same back, around the move.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.25f;

        public int MaxSwitchesPerConversation => _maxSwitchesPerConversation;
        public float MinSecondsBetweenSwitches => _minSecondsBetweenSwitches;
        public float FadeSeconds => _fadeSeconds;
    }
}
