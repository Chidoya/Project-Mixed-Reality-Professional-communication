using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// The computer on the reception desk that starts the shift of Prototype1: point with the controller ray and pull the
    /// trigger. The same idea as <see cref="Shift.ShiftStartButton"/> (ShiftHospital), for the AI shift (<see cref="AiShift"/>).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class AiShiftStartButton : MonoBehaviour
    {
        [SerializeField] private AiShift _shift;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private string _startText = "Start shift\n<size=60%>point and pull the trigger</size>";
        [SerializeField] private string _runningText = "Shift started\n<size=60%>Task 1: room 1A</size>";
        [SerializeField] private Color _idleColor = new Color(0.12f, 0.12f, 0.14f);
        [SerializeField] private Color _hoverColor = new Color(0.1f, 0.45f, 0.9f);

        private XRSimpleInteractable _interactable;

        public string LabelText => _label != null ? _label.text : "";

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

        private void OnSelectEntered(SelectEnterEventArgs args) => Press();

        /// <summary>Starts the shift, as the trigger does. Also for tests.</summary>
        public void Press()
        {
            if (_shift == null || _shift.Stage != AiShift.ShiftStage.Lobby)
                return;
            _shift.StartShift();
            if (_shift.Stage != AiShift.ShiftStage.Lobby)
                SetLabel(_runningText, _idleColor);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (_shift != null && _shift.Stage == AiShift.ShiftStage.Lobby)
                SetLabel(_startText, _hoverColor);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (_shift != null && _shift.Stage == AiShift.ShiftStage.Lobby)
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
