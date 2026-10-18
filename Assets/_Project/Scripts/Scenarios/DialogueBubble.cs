using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A text balloon in the world (world-space canvas). Used for the NPC line, the four options
    /// and the feedback. Clickable bubbles are selected with the controller ray and trigger.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class DialogueBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button _button;

        public event Action Clicked;

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        public void SetText(string text)
        {
            _label.text = text;
        }

        /// <summary>
        /// Non-clickable bubbles keep their look but ignore the ray (NPC line, feedback).
        /// </summary>
        public void SetClickable(bool clickable)
        {
            _button.enabled = clickable;
        }

        private void OnButtonClicked()
        {
            Clicked?.Invoke();
        }
    }
}
