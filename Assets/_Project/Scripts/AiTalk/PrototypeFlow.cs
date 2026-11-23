using System;
using System.Collections;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// The loop of Prototype1 (one patient, free speech through Liam's AI server):
    /// <list type="number">
    /// <item>Briefing: the player starts in the corridor in front of the patient's room, with a sign that says what to do.</item>
    /// <item>Talk: as soon as the player is near the patient, the patient says his first line. Just talk: the microphone is
    /// always on (VoiceActivityTalk), Y switches to the patient's eyes (the patient view, with the trust meter).</item>
    /// <item>The conversation ends when the AI server ends it (resolved or a critical error) or when the player holds B
    /// (F in the Editor) to finish it.</item>
    /// <item>To the doctor: a fade to black and the player stands in front of the doctor, who says a short line; then
    /// Liam's debrief panel (score, what went well, what can be better) opens.</item>
    /// <item>Retry (A) brings the player back to the patient for a new conversation; Done (B) ends the prototype.</item>
    /// </list>
    /// The texts are in the inspector (data, not code), so they can be changed without programming.
    /// </summary>
    public class PrototypeFlow : MonoBehaviour
    {
        public enum Stage { Briefing, Talking, ToDoctor, Debrief, Finished }

        [SerializeField] private AiNpc _patient;
        [SerializeField] private AiPatientBridge _bridge;
        [SerializeField] private AiDebriefPanel _debrief;
        [SerializeField] private PovController _pov;
        [SerializeField] private XROrigin _origin;
        [Tooltip("Black fade on the camera (the one of the patient view).")]
        [SerializeField] private ScreenFade _fade;

        [Header("Places")]
        [Tooltip("Where the player stands for the conversation (floor point, forward toward the patient). Used for Retry.")]
        [SerializeField] private Transform _patientSpot;
        [Tooltip("Where the player stands in front of the doctor (floor point, forward toward the doctor).")]
        [SerializeField] private Transform _doctorSpot;
        [Tooltip("The conversation starts when the player's head is this close (meters, horizontal) to the patient.")]
        [SerializeField, Min(0.5f)] private float _startDistance = 2.6f;

        [Header("Doctor")]
        [SerializeField] private DialogueBubble _doctorBubble;
        [TextArea, SerializeField] private string _doctorLine =
            "Thanks for coming by. Let's look back at your talk with the patient in room 1A.";
        [TextArea, SerializeField] private string _doctorRetryLine =
            "Go ahead, try again. He is still in room 1A.";
        [TextArea, SerializeField] private string _doctorDoneLine =
            "Good work today. This is the end of the prototype.";
        [Tooltip("Seconds between the doctor's line and the debrief panel.")]
        [SerializeField, Min(0f)] private float _debriefDelay = 2.5f;

        [Header("Ending on a black screen (every end of the conversation)")]
        [Tooltip("Shows the reason on the black screen. Empty: no card, straight to the doctor.")]
        [SerializeField] private EndingCard _endingCard;
        [Tooltip("Seconds the black screen stays after a critical error or an escalation.")]
        [SerializeField, Min(1f)] private float _cardSeconds = 6f;
        [Tooltip("Seconds the black screen stays after a resolved or a finished conversation.")]
        [SerializeField, Min(1f)] private float _shortCardSeconds = 3.5f;
        [SerializeField] private string _criticalTitle = "CRITICAL ERROR";
        [SerializeField] private string _escalatedTitle = "CONVERSATION ESCALATED";
        [TextArea, SerializeField] private string _escalatedText =
            "The patient does not want to talk to you any more. The doctor will go through what went wrong.";
        [SerializeField] private string _resolvedTitle = "CONVERSATION FINISHED";
        [TextArea, SerializeField] private string _resolvedText = "He agreed to try a small step. The doctor wants to hear how it went.";
        [SerializeField] private string _stoppedTitle = "CONVERSATION ENDED";
        [TextArea, SerializeField] private string _stoppedText = "You ended the conversation. The doctor wants to hear how it went.";

        [Header("Shift")]
        [Tooltip("The conversation only starts after the shift has started (AiShift sets Allow Conversation).")]
        [SerializeField] private bool _waitForShiftStart;

        [Header("Finish the conversation")]
        [Tooltip("Hold this long (seconds) to finish the conversation yourself.")]
        [SerializeField, Min(0.2f)] private float _finishHoldSeconds = 1.2f;
        [Tooltip("Seconds after the end of the conversation before the fade to the doctor (the last line can be heard).")]
        [SerializeField, Min(0f)] private float _delayAfterEnd = 2f;
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.6f;

        [Header("Signs (optional)")]
        [SerializeField] private TMP_Text _briefingSign;
        [SerializeField] private TMP_Text _talkHint;
        [TextArea, SerializeField] private string _talkHintText =
            "Just talk to the patient.  Y: patient view.  Hold B: finish the conversation.";
        [TextArea, SerializeField] private string _finishingHintText = "Finishing the conversation...";
        [Tooltip("Optional: the hint also shows what the microphone does (listening, hearing you, thinking).")]
        [SerializeField] private VoiceActivityTalk _voice;

        private InputAction _finishAction;
        private float _finishHeld;
        private bool _endHandled;
        private string _endReason;

        public Stage Current { get; private set; } = Stage.Briefing;

        /// <summary>False until the shift starts (with Wait For Shift Start): near the patient nothing happens yet.</summary>
        public bool AllowConversation { get; set; } = true;

        /// <summary>Why the last conversation ended (resolved, stopped, escalated, critical_error...).</summary>
        public string EndReason => _endReason;

        /// <summary>The title of the critical error of the last conversation (empty if there was none).</summary>
        public string CriticalTitle
        {
            get
            {
                var critical = _patient != null && _patient.LastTurn != null ? _patient.LastTurn.critical : null;
                return critical != null && critical.Present ? critical.title : "";
            }
        }
        public event Action<Stage> StageChanged;

        private void Awake()
        {
            if (_origin == null)
                _origin = FindAnyObjectByType<XROrigin>();
            if (_doctorBubble != null)
                _doctorBubble.gameObject.SetActive(false);
            if (_talkHint != null)
                _talkHint.gameObject.SetActive(false);
            AllowConversation = !_waitForShiftStart;
        }

        private void OnEnable()
        {
            _finishAction = new InputAction("FinishConversation", InputActionType.Button);
            _finishAction.AddBinding("<XRController>{RightHand}/{SecondaryButton}"); // B
            _finishAction.AddBinding("<Keyboard>/f");
            _finishAction.Enable();
            if (_patient != null)
                _patient.OnConversationEnded.AddListener(OnConversationEnded);
            if (_debrief != null)
            {
                _debrief.Retried += OnRetried;
                _debrief.Closed += OnDebriefClosed;
            }
        }

        private void OnDisable()
        {
            _finishAction?.Dispose();
            _finishAction = null;
            if (_patient != null)
                _patient.OnConversationEnded.RemoveListener(OnConversationEnded);
            if (_debrief != null)
            {
                _debrief.Retried -= OnRetried;
                _debrief.Closed -= OnDebriefClosed;
            }
        }

        private void Update()
        {
            switch (Current)
            {
                case Stage.Briefing:
                    if (AllowConversation && _patient != null && _bridge != null && _bridge.IsPlayerNear(_startDistance))
                    {
                        _endHandled = false;
                        SetStage(Stage.Talking);
                        _ = _patient.StartConversation();
                    }
                    break;
                case Stage.Talking:
                    UpdateFinishHold();
                    break;
            }
        }

        /// <summary>
        /// Holding B (F) finishes the conversation, only in the nurse view and while the patient is not busy.
        /// </summary>
        private void UpdateFinishHold()
        {
            var pressed = _finishAction != null && _finishAction.IsPressed();
            var canFinish = _patient != null && !_patient.HasEnded && !_patient.Busy
                            && (_pov == null || _pov.Mode == PovMode.Nurse);
            _finishHeld = pressed && canFinish ? _finishHeld + Time.deltaTime : 0f;
            if (_talkHint != null)
                _talkHint.text = _finishHeld > 0f ? _finishingHintText
                    : _voice != null ? $"{_talkHintText}\n<b>{_voice.StatusText}</b>" : _talkHintText;
            if (_finishHeld < _finishHoldSeconds)
                return;
            _finishHeld = 0f;
            Debug.Log("[ProCommVR] Prototype1: the player finished the conversation (hold B).", this);
            _patient.EndNow("stopped");
        }

        /// <summary>The finish action (B / F) is held right now. For tests.</summary>
        public float FinishHeld => _finishHeld;

        /// <summary>Finishes the conversation as holding B does. For tests.</summary>
        public void FinishConversation()
        {
            if (Current == Stage.Talking && _patient != null && !_patient.HasEnded)
                _patient.EndNow("stopped");
        }

        private void OnConversationEnded(string reason)
        {
            if (_endHandled || Current != Stage.Talking)
                return;
            _endHandled = true;
            _endReason = reason;
            Debug.Log($"[ProCommVR] Prototype1: conversation ended ({reason}), to the doctor.", this);
            StartCoroutine(ToDoctor());
        }

        private IEnumerator ToDoctor()
        {
            SetStage(Stage.ToDoctor);
            yield return new WaitForSeconds(_delayAfterEnd);
            // The patient view closes itself when the conversation ends; wait until the player is the nurse again.
            while (_pov != null && (_pov.Mode == PovMode.Patient || _pov.IsSwitching))
                yield return null;

            if (_endingCard != null)
                yield return BlackScreenToDoctor();
            else
                yield return Move(_doctorSpot);
            if (_doctorBubble != null)
            {
                _doctorBubble.gameObject.SetActive(true);
                _doctorBubble.SetClickable(false);
                _doctorBubble.SetText(_doctorLine);
            }
            yield return new WaitForSeconds(_debriefDelay);
            SetStage(Stage.Debrief);
            if (_debrief != null)
                _ = _debrief.Open();
        }

        /// <summary>
        /// A conversation does not just end: the view goes black with what happened (a critical error, escalated,
        /// resolved or ended by the player), and only then the player stands at the doctor.
        /// </summary>
        private IEnumerator BlackScreenToDoctor()
        {
            if (_fade != null)
                yield return _fade.FadeTo(1f, _fadeSeconds * 1.5f);
            var critical = _patient != null && _patient.LastTurn != null ? _patient.LastTurn.critical : null;
            var seconds = _cardSeconds;
            if (_endReason == "critical_error")
                _endingCard.Show(_criticalTitle, new Color(0.95f, 0.3f, 0.25f),
                    critical != null && critical.Present ? critical.title : "",
                    critical != null && critical.Present ? critical.feedback : "");
            else if (_endReason == "escalated")
                _endingCard.Show(_escalatedTitle, new Color(1f, 0.55f, 0.1f), "", _escalatedText);
            else
            {
                seconds = _shortCardSeconds;
                var resolved = _endReason == "resolved";
                _endingCard.Show(resolved ? _resolvedTitle : _stoppedTitle,
                    resolved ? new Color(0.45f, 0.85f, 0.55f) : new Color(0.85f, 0.85f, 0.9f), "",
                    resolved ? _resolvedText : _stoppedText);
            }
            Teleport(_doctorSpot);
            yield return new WaitForSeconds(seconds);
            _endingCard.Hide();
            if (_fade != null)
                yield return _fade.FadeTo(0f, _fadeSeconds);
        }

        /// <summary>The ending card is shown (for tests).</summary>
        public bool IsEndingCardShown => _endingCard != null && _endingCard.IsShown;

        private void OnRetried()
        {
            if (Current != Stage.Debrief)
                return;
            StartCoroutine(BackToPatient());
        }

        private IEnumerator BackToPatient()
        {
            if (_doctorBubble != null)
                _doctorBubble.SetText(_doctorRetryLine);
            yield return Move(_patientSpot);
            if (_doctorBubble != null)
                _doctorBubble.gameObject.SetActive(false);
            // The debrief panel restarts the conversation itself (AiNpc.Restart).
            _endHandled = false;
            SetStage(Stage.Talking);
        }

        private void OnDebriefClosed()
        {
            if (_doctorBubble != null)
            {
                _doctorBubble.gameObject.SetActive(true);
                _doctorBubble.SetText(_doctorDoneLine);
            }
            SetStage(Stage.Finished);
        }

        /// <summary>
        /// Fade to black, put the player's head above the spot (the player's own height above the floor stays),
        /// looking along the spot's forward, fade in.
        /// </summary>
        private IEnumerator Move(Transform spot)
        {
            if (_fade != null)
                yield return _fade.FadeTo(1f, _fadeSeconds);
            Teleport(spot);
            yield return null;
            if (_fade != null)
                yield return _fade.FadeTo(0f, _fadeSeconds);
        }

        /// <summary>Moves the player at once. Also for tests.</summary>
        public void Teleport(Transform spot)
        {
            if (spot == null || _origin == null)
                return;
            var rig = _origin.transform;
            var controller = rig.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;
            var eyeHeight = _origin.Camera.transform.position.y - rig.position.y;
            var forward = Vector3.ProjectOnPlane(spot.forward, Vector3.up);
            _origin.MatchOriginUpCameraForward(Vector3.up, forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward);
            _origin.MoveCameraToWorldLocation(spot.position + Vector3.up * eyeHeight);
            // The rig's floor stays at the spot's floor.
            var position = rig.position;
            position.y = spot.position.y;
            rig.position = position;
            Physics.SyncTransforms();
            if (controller != null)
                controller.enabled = wasEnabled;
        }

        private void SetStage(Stage stage)
        {
            if (Current == stage)
                return;
            Current = stage;
            if (_briefingSign != null)
                _briefingSign.gameObject.SetActive(stage == Stage.Briefing);
            if (_talkHint != null)
            {
                _talkHint.gameObject.SetActive(stage == Stage.Talking);
                _talkHint.text = _talkHintText;
            }
            StageChanged?.Invoke(stage);
        }
    }
}
