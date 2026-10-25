using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ProCommVR.Navigation
{
    /// <summary>
    /// A button on a lift panel: point at it with the controller ray and pull the trigger to go to that floor.
    /// The ride is a teleport of the XR Origin to the destination in front of the lift on that floor.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class LiftButton : MonoBehaviour
    {
        [Tooltip("Where the player arrives: in front of the lift on the target floor, facing out of the lift.")]
        [SerializeField] private Transform _destination;

        [Tooltip("Optional. Found in the scene (the XR rig's Teleportation provider) when empty.")]
        [SerializeField] private TeleportationProvider _teleportationProvider;

        private XRSimpleInteractable _interactable;

        public Transform Destination => _destination;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            if (_teleportationProvider == null)
                _teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (_destination == null || _teleportationProvider == null)
            {
                Debug.LogWarning($"[ProCommVR] Lift button {name} has no destination or no teleportation provider.", this);
                return;
            }

            _teleportationProvider.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = _destination.position,
                destinationRotation = _destination.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward,
            });
        }
    }
}
