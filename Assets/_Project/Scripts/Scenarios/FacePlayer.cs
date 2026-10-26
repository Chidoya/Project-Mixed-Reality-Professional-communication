using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Turns this object (around the vertical axis only) so a bubble under it can be read from where the player is,
    /// e.g. the NPC bubble of a doctor who walks around and stands at his desk with his back half to the door.
    /// A bubble's front faces against its forward, so the object looks from the player to itself.
    /// </summary>
    public class FacePlayer : MonoBehaviour
    {
        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        private void LateUpdate()
        {
            if (_player == null && Camera.main != null)
                _player = Camera.main.transform;
            if (_player == null)
                return;
            var look = transform.position - _player.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
