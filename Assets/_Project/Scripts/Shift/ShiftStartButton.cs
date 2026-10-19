using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProCommVR.Shift
{
    /// <summary>
    /// An object in the briefing room (e.g. the computer) that starts the shift when the player points at it
    /// with the controller ray and pulls the trigger.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ShiftStartButton : MonoBehaviour
    {
        [SerializeField] private ShiftManager _shiftManager;

        [Tooltip("Optional label above the object.")]
        [SerializeField] private TMP_Text _label;

        [SerializeField] private string _startText = "Start shift\n<size=60%>point and pull the trigger</size>";
        [SerializeField] private string _runningText = "Shift started";
        [SerializeField] private Color _idleColor = new Color(0.12f, 0.12f, 0.14f);
        [SerializeField] private Color _hoverColor = new Color(0.1f, 0.45f, 0.9f);

        private XRSimpleInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            SetLabel(_startText, _idleColor);
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelectEntered);
            _interactable.hoverEntered.AddListener(OnHoverEntered);
            _interactable.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
            _interactable.hoverEntered.RemoveListener(OnHoverEntered);
            _interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (_shiftManager.State != ShiftState.Briefing)
                return;
            _shiftManager.StartShift();
            if (_shiftManager.State == ShiftState.Running)
                SetLabel(_runningText, _idleColor);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (_shiftManager.State == ShiftState.Briefing)
                SetLabel(_startText, _hoverColor);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (_shiftManager.State == ShiftState.Briefing)
                SetLabel(_startText, _idleColor);
        }

        private void SetLabel(string text, Color color)
        {
            if (_label == null)
                return;
            _label.text = text;
            _label.color = color;
        }
    }
}
