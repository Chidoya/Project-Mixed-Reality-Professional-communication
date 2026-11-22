using System.Text;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Pov;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProCommVR.Testing
{
    /// <summary>
    /// The clipboard on the desk in the lobby (Prototype1): a task list and the developer controls, on pages.
    /// <list type="bullet">
    /// <item>Page 1, Tasks: a template for the tasks of the shift (the text is in the inspector, no code).</item>
    /// <item>Page 2, Dev: the patient's emotion (Joy, Anger, Fear, Sadness, Neutral), a slider for the trust (0 to 100),
    /// "AI drives" (the AI turns change emotion and trust again) and a restart of the conversation.</item>
    /// </list>
    /// A dev control holds its value: the AI turns no longer overwrite it (<see cref="AiPatientBridge.HoldEmotions"/>,
    /// <see cref="AiPatientBridge.HoldTrust"/>) until "AI drives" is pressed.
    /// <para>
    /// It starts in the left hand (Start In Hand); "Put down" puts it on the desk in the lobby, "Take" in the hand again.
    /// In the patient view it is hidden (the lasers are off there).
    /// </para>
    /// </summary>
    public class DevClipboard : MonoBehaviour
    {
        [Header("Patient")]
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PatientReputation _reputation;
        [SerializeField] private AiPatientBridge _bridge;
        [SerializeField] private PovController _pov;
        [Tooltip("Strength of the chosen emotion (0..1); the others are almost 0.")]
        [SerializeField, Range(0.1f, 1f)] private float _strength = 0.85f;

        [Header("Pages")]
        [SerializeField] private GameObject[] _pages = new GameObject[0];
        [SerializeField] private TMP_Text _pageLabel;
        [SerializeField] private string[] _pageNames = { "Tasks", "Dev" };

        [Header("Tasks page")]
        [SerializeField] private TMP_Text _tasksBody;
        [TextArea(6, 20), SerializeField] private string _tasksText =
            "<b>#<pos=8%>Task<pos=68%>Room<pos=84%>Status</b>\n" +
            "1<pos=8%>Talk to the patient<pos=68%>1A<pos=84%>Open\n" +
            "2<pos=8%>...<pos=68%>...<pos=84%>...\n" +
            "3<pos=8%>...<pos=68%>...<pos=84%>...\n" +
            "4<pos=8%>...<pos=68%>...<pos=84%>...\n" +
            "5<pos=8%>...<pos=68%>...<pos=84%>...\n\n" +
            "<b>Notes</b>\n...";

        [Header("Dev page")]
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Slider _trustSlider;
        [SerializeField] private TMP_Text _trustValue;

        [Header("Carry")]
        [Tooltip("The visible part (model and canvas), hidden in the patient view.")]
        [SerializeField] private GameObject _visual;
        [Tooltip("The left controller; Take puts the clipboard there.")]
        [SerializeField] private Transform _hand;
        [SerializeField] private Vector3 _handPosition = new Vector3(0.06f, 0.06f, 0.1f);
        [SerializeField] private TMP_Text _carryLabel;
        [Tooltip("X (left controller) takes it in the hand or puts it back on the desk. C in the editor.")]
        [SerializeField] private InputAction _carryAction = new InputAction("Carry Clipboard", InputActionType.Button,
            "<XRController>{LeftHand}/{PrimaryButton}");

        [Header("Microphone (optional)")]
        [SerializeField] private ProCommVR.AiTalk.VoiceActivityTalk _voice;
        [SerializeField] private TMP_Text _micLabel;

        [Tooltip("In the left hand from the start (else on the desk in the lobby).")]
        [SerializeField] private bool _startInHand = true;

        private int _page;
        private Transform _homeParent;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public int Page => _page;
        public bool IsCarried => _hand != null && transform.parent == _hand;
        public Transform Hand => _hand;
        public Slider TrustSlider => _trustSlider;

        private void Awake()
        {
            _homeParent = transform.parent;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;
            if (_tasksBody != null)
                _tasksBody.text = _tasksText;
            if (_trustSlider != null)
            {
                _trustSlider.minValue = PatientReputation.Min;
                _trustSlider.maxValue = PatientReputation.Max;
                _trustSlider.wholeNumbers = true;
                _trustSlider.onValueChanged.AddListener(SetTrust);
            }
            ShowPage(0);
            UpdateCarryLabel();
        }

        private void Start()
        {
            if (_startInHand)
                Take();
        }

        private void OnEnable()
        {
            if (_carryAction.bindings.Count < 2)
                _carryAction.AddBinding("<Keyboard>/c");
            _carryAction.performed += OnCarryPressed;
            _carryAction.Enable();
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
        }

        private void OnDisable()
        {
            _carryAction.performed -= OnCarryPressed;
            _carryAction.Disable();
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
        }

        private void OnCarryPressed(InputAction.CallbackContext context) => ToggleCarry();

        /// <summary>The next microphone of the computer (the AI must hear you).</summary>
        public void NextMicrophone()
        {
            if (_voice != null)
                _voice.NextMicrophone();
        }

        private void OnPovChanged(PovMode mode)
        {
            if (_visual != null)
                _visual.SetActive(mode == PovMode.Nurse);
        }

        private void Update()
        {
            if (_reputation != null && _trustSlider != null && !Mathf.Approximately(_trustSlider.value, _reputation.Value))
                _trustSlider.SetValueWithoutNotify(_reputation.Value);
            if (_trustValue != null && _reputation != null)
                _trustValue.text = Mathf.RoundToInt(_reputation.Value).ToString();
            if (_status != null && _page < _pages.Length && _pages[_page].activeSelf)
                _status.text = Status();
            if (_micLabel != null && _voice != null)
            {
                var heard = _voice.RecentPeak >= _voice.Threshold;
                _micLabel.text = $"<b>Mic:</b> {_voice.DeviceName}\n" +
                                 $"level {_voice.RecentPeak * 1000f:0}  (speech above {_voice.Threshold * 1000f:0})  " +
                                 (heard ? "<color=#2e8b3e><b>HEARD</b></color>" : "<color=#9a9a9a>quiet</color>") +
                                 $"   {_voice.StatusText}";
            }
        }

        // ---- Pages ----

        public void NextPage() => ShowPage((_page + 1) % Mathf.Max(1, _pages.Length));

        public void PreviousPage() => ShowPage((_page - 1 + _pages.Length) % Mathf.Max(1, _pages.Length));

        public void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 0, Mathf.Max(0, _pages.Length - 1));
            for (var i = 0; i < _pages.Length; i++)
                _pages[i].SetActive(i == _page);
            if (_pageLabel != null)
            {
                var name = _page < _pageNames.Length ? _pageNames[_page] : "";
                _pageLabel.text = $"{name}  {_page + 1}/{_pages.Length}";
            }
        }

        // ---- Dev controls ----

        public void ShowJoy() => SetEmotion(Emotion.Joy);
        public void ShowAnger() => SetEmotion(Emotion.Anger);
        public void ShowFear() => SetEmotion(Emotion.Fear);
        public void ShowSadness() => SetEmotion(Emotion.Sadness);
        public void ShowNeutral() => SetEmotion(null);

        /// <summary>The patient feels this emotion (null: nothing), and the AI no longer changes it.</summary>
        public void SetEmotion(Emotion? emotion)
        {
            if (_holder == null)
                return;
            if (_bridge != null)
                _bridge.HoldEmotions = true;
            const float low = 0.05f;
            float Value(Emotion e) => emotion == e ? _strength : low;
            _holder.SetTarget(emotion == null
                ? new EmotionState(low, low, low, low)
                : new EmotionState(Value(Emotion.Joy), Value(Emotion.Anger), Value(Emotion.Fear), Value(Emotion.Sadness)));
        }

        /// <summary>The trust (0..100), and the AI no longer changes it.</summary>
        public void SetTrust(float value)
        {
            if (_reputation == null)
                return;
            if (_bridge != null)
                _bridge.HoldTrust = true;
            _reputation.Set(value);
        }

        /// <summary>The AI turns change the emotion and the trust again.</summary>
        public void LetAiDrive()
        {
            if (_bridge == null)
                return;
            _bridge.HoldEmotions = false;
            _bridge.HoldTrust = false;
        }

        /// <summary>Starts the conversation with the patient again (only while it runs or has ended).</summary>
        public void RestartConversation()
        {
            var npc = _bridge != null ? _bridge.Npc : null;
            if (npc != null && npc.isActiveAndEnabled)
                _ = npc.Restart();
        }

        // ---- Carry ----

        public void ToggleCarry()
        {
            if (IsCarried)
                PutDown();
            else
                Take();
        }

        public void Take()
        {
            if (_hand == null)
            {
                Debug.LogWarning("[ProCommVR] Dev clipboard: no hand to hold it.", this);
                return;
            }
            transform.SetParent(_hand, false);
            transform.localPosition = _handPosition;
            // Facing the eyes, tilted like a tablet held in the hand; the text up points away from the player.
            transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -1f, -1f), new Vector3(0f, 1f, -1f));
            UpdateCarryLabel();
        }

        public void PutDown()
        {
            transform.SetParent(_homeParent, false);
            transform.localPosition = _homePosition;
            transform.localRotation = _homeRotation;
            UpdateCarryLabel();
        }

        private void UpdateCarryLabel()
        {
            if (_carryLabel != null)
                _carryLabel.text = IsCarried ? "<b>Put down (X)</b>" : "<b>Take (X)</b>";
        }

        private string Status()
        {
            var text = new StringBuilder();
            if (_holder != null)
            {
                var c = _holder.Current;
                var dominant = _holder.Dominant?.ToString() ?? "Neutral";
                text.Append($"Patient: <b>{dominant}</b>  (J {Percent(c.Joy)}  A {Percent(c.Anger)}  F {Percent(c.Fear)}  S {Percent(c.Sadness)})\n");
            }
            if (_bridge != null)
            {
                var emotions = _bridge.HoldEmotions ? "you" : "AI";
                var trust = _bridge.HoldTrust ? "you" : "AI";
                text.Append($"Emotion set by: {emotions}   Trust set by: {trust}\n");
                text.Append(_bridge.IsPlaying ? "Conversation: running\n" : "Conversation: not running\n");
            }
            if (_pov != null)
            {
                var why = _pov.WhyNot();
                text.Append(why == null ? "Y: ready" : $"Y: {why}");
            }
            return text.ToString();
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f).ToString();
    }
}
