using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProCommVR.XRTest
{
    /// <summary>
    /// Test-only target for the XRTest scene: toggles its color when selected with a controller ray
    /// (trigger) or when a UI button calls <see cref="ToggleColor"/>. Not meant for game content.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable), typeof(Renderer))]
    public class RayTestTarget : MonoBehaviour
    {
        [SerializeField] private Color _idleColor = Color.white;
        [SerializeField] private Color _toggledColor = new Color(0.2f, 0.75f, 0.35f);

        private Renderer _renderer;
        private XRSimpleInteractable _interactable;
        private bool _isToggled;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _interactable = GetComponent<XRSimpleInteractable>();
            _renderer.material.color = _idleColor;
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        public void ToggleColor()
        {
            _isToggled = !_isToggled;
            _renderer.material.color = _isToggled ? _toggledColor : _idleColor;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            ToggleColor();
        }
    }
}
