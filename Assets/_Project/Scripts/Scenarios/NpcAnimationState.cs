using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Plays one state of the NPC's Animator when the scene starts, e.g. "Sitting" for a patient in a chair.
    /// The character controllers in Assets/Character have the states Idle, Talking, Sitting, Laying, Sad,
    /// Angry and Crying, without parameters.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcAnimationState : MonoBehaviour
    {
        [SerializeField] private string _stateName = "Idle";

        [Tooltip("Start at a random moment of the animation, so characters with the same animation don't move in sync. " +
                 "Off: start at the beginning, e.g. for a pose placed on the first frame (a patient lying in bed).")]
        [SerializeField] private bool _randomStart = true;

        public string StateName => _stateName;

        private void Start()
        {
            var animator = GetComponent<Animator>();
            var state = Animator.StringToHash(_stateName);
            if (animator.HasState(0, state))
                animator.Play(state, 0, _randomStart ? Random.value : 0f);
            else
                Debug.LogWarning($"[ProCommVR] Animator of '{name}' has no state '{_stateName}'.", this);
        }
    }
}
