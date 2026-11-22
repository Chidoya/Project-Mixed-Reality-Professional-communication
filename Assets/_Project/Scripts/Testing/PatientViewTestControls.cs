using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProCommVR.Testing
{
    /// <summary>
    /// Test controls for the patient view, only in the PatientViewTest scene (not in the shift). Sets the patient's
    /// emotion directly, so every emotion of the patient view can be seen without replaying the conversation, and
    /// restarts the conversation (which also resets the switch count and the Initial Emotion).
    /// <para>
    /// Three ways: the buttons on the test panel (laser + trigger, nurse view), A on the right controller (next
    /// emotion, also in the patient view, where the lasers are off) and B (restart), and the keyboard in the editor or
    /// via Quest Link: 1 Joy, 2 Anger, 3 Fear, 4 Sadness, 0 Neutral, R restart.
    /// </para>
    /// </summary>
    public class PatientViewTestControls : MonoBehaviour
    {
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private ScenarioSequence _sequence;
        [SerializeField] private PovController _pov;

        [Tooltip("Shows the patient's emotion, the view and whether Y works right now.")]
        [SerializeField] private TMP_Text _status;

        [Tooltip("How strong the chosen emotion is (0..1); the other three stay low.")]
        [Range(0f, 1f)]
        [SerializeField] private float _strength = 0.8f;

        [SerializeField] private InputAction _nextEmotion = new InputAction("Next Emotion", InputActionType.Button,
            "<XRController>{RightHand}/primaryButton");

        [SerializeField] private InputAction _restart = new InputAction("Restart Conversation", InputActionType.Button,
            "<XRController>{RightHand}/secondaryButton");

        private const float Low = 0.05f;
        private const float StatusInterval = 0.2f;

        // Neutral last: A cycles Joy, Anger, Fear, Sadness, Neutral.
        private static readonly Emotion?[] Cycle = { Emotion.Joy, Emotion.Anger, Emotion.Fear, Emotion.Sadness, null };
        private int _cycleIndex = -1;
        private float _nextStatus;

        private void OnEnable()
        {
            _nextEmotion.performed += OnNextEmotion;
            _restart.performed += OnRestart;
            _nextEmotion.Enable();
            _restart.Enable();
        }

        private void OnDisable()
        {
            _nextEmotion.performed -= OnNextEmotion;
            _restart.performed -= OnRestart;
            _nextEmotion.Disable();
            _restart.Disable();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) ShowJoy();
                if (keyboard.digit2Key.wasPressedThisFrame) ShowAnger();
                if (keyboard.digit3Key.wasPressedThisFrame) ShowFear();
                if (keyboard.digit4Key.wasPressedThisFrame) ShowSadness();
                if (keyboard.digit0Key.wasPressedThisFrame) ShowNeutral();
                if (keyboard.rKey.wasPressedThisFrame) RestartConversation();
            }

            if (_status != null && Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + StatusInterval;
                _status.text = DescribeStatus();
            }
        }

        public void ShowJoy() => Show(Emotion.Joy);
        public void ShowAnger() => Show(Emotion.Anger);
        public void ShowFear() => Show(Emotion.Fear);
        public void ShowSadness() => Show(Emotion.Sadness);
        public void ShowNeutral() => Show(null);

        /// <summary>
        /// Blends the patient to one dominant emotion (or to all zero), the same way an answer does.
        /// </summary>
        public void Show(Emotion? emotion)
        {
            if (_holder == null)
                return;
            _cycleIndex = System.Array.IndexOf(Cycle, emotion);
            var wanted = Preset(emotion);
            var target = _holder.Target;
            _holder.Apply(new EmotionState(wanted.Joy - target.Joy, wanted.Anger - target.Anger,
                wanted.Fear - target.Fear, wanted.Sadness - target.Sadness));
            Debug.Log($"[ProCommVR] Test controls: patient emotion set to {(emotion.HasValue ? emotion.ToString() : "Neutral")}.", this);
        }

        public void RestartConversation()
        {
            if (_sequence == null)
                return;
            _cycleIndex = -1;
            _sequence.Restart();
            Debug.Log("[ProCommVR] Test controls: conversation restarted.", this);
        }

        private EmotionState Preset(Emotion? emotion)
        {
            if (!emotion.HasValue)
                return EmotionState.Zero;
            float Value(Emotion e) => e == emotion.Value ? _strength : Low;
            return new EmotionState(Value(Emotion.Joy), Value(Emotion.Anger), Value(Emotion.Fear), Value(Emotion.Sadness));
        }

        private void OnNextEmotion(InputAction.CallbackContext context)
        {
            Show(Cycle[(_cycleIndex + 1) % Cycle.Length]);
        }

        private void OnRestart(InputAction.CallbackContext context)
        {
            RestartConversation();
        }

        private string DescribeStatus()
        {
            var text = new System.Text.StringBuilder();
            if (_holder != null)
            {
                var dominant = _holder.Dominant;
                text.Append($"Patient: {(dominant.HasValue ? dominant.ToString() : "Neutral")}  ");
                var c = _holder.Current;
                text.Append($"(J {Percent(c.Joy)}  A {Percent(c.Anger)}  F {Percent(c.Fear)}  S {Percent(c.Sadness)})\n");
            }
            if (_pov != null)
            {
                var max = _pov.Settings == null || _pov.Settings.Unlimited ? "no limit" : _pov.Settings.MaxSwitchesPerConversation.ToString();
                var why = _pov.WhyNot();
                text.Append($"View: {_pov.Mode}   Switches: {_pov.SwitchesThisConversation} / {max}\n");
                text.Append(why == null ? "Y: ready" : $"Y: {why}");
            }
            return text.ToString();
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);
    }
}
