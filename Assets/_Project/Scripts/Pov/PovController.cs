using System;
using System.Collections;
using System.Collections.Generic;
using ProCommVR.Player;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace ProCommVR.Pov
{
    public enum PovMode
    {
        Nurse,
        Patient
    }

    /// <summary>
    /// Switches the player between the nurse view and the patient view during a conversation (the Switch Pov action,
    /// Y on the left controller). Limits from <see cref="PovSwitchSettings"/>: at most N switches to the patient per
    /// conversation (reset on <see cref="IPatientConversation.ConversationStarted"/>; switching back is always allowed) and a
    /// minimum time between two switches, both ways.
    /// <para>
    /// To the patient: fade to black, remember the XR Origin pose, show the <see cref="PlayerAvatar"/> where the player
    /// stands (and let it stay there), move the rig so the camera is at the patient's eyes (<see cref="PovAnchor"/>),
    /// hide the patient's body and the dialogue bubbles, turn off locomotion and the interaction lasers, show the
    /// "Press Y to return" hint, fade in. Back: the same in reverse, and the rig returns to exactly the remembered pose.
    /// The conversation itself (options, order, score) does not change.
    /// </para>
    /// </summary>
    public class PovController : MonoBehaviour
    {
        [SerializeField] private PovSwitchSettings _settings;

        [Tooltip("The Switch Pov action (Y on the left controller) in the project's copy of the XRI input actions.")]
        [SerializeField] private InputActionReference _switchAction;

        [Tooltip("The conversation. Switching is only possible while it shows a scenario; a new scenario resets the count.")]
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("Optional, instead of Presenter: any conversation that implements IPatientConversation (e.g. the AI conversation bridge).")]
        [SerializeField] private MonoBehaviour _conversationSource;

        [Tooltip("The patient's eyes.")]
        [SerializeField] private PovAnchor _anchor;

        [Tooltip("Empty: the XR Origin in the scene.")]
        [SerializeField] private XROrigin _origin;

        [Tooltip("Empty: the player avatar in the scene (it comes with the XR rig).")]
        [SerializeField] private PlayerAvatar _avatar;

        [SerializeField] private ScreenFade _fade;

        [Tooltip("Shown in the patient view, in front of the patient's eyes.")]
        [SerializeField] private GameObject _hint;

        [Tooltip("Where the hint appears in the patient view: meters forward and up from the eyes.")]
        [SerializeField] private Vector2 _hintOffset = new Vector2(1.1f, -0.4f);

        private readonly List<Behaviour> _turnedOff = new List<Behaviour>();
        private CharacterController _characterController;
        private bool _characterControllerWasEnabled;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _lastSwitchTime = float.NegativeInfinity;
        private bool _busy;

        /// <summary>
        /// Raised with the new mode, right after the move (while the view is still black).
        /// </summary>
        public event Action<PovMode> PovChanged;

        public PovMode Mode { get; private set; } = PovMode.Nurse;
        public int SwitchesThisConversation { get; private set; }
        public bool IsSwitching => _busy;
        public PovSwitchSettings Settings => _settings;

        /// <summary>The conversation that allows the switch: Conversation Source if set, else the presenter.</summary>
        public IPatientConversation Conversation =>
            _conversationSource != null && _conversationSource is IPatientConversation source ? source
            : _presenter != null ? _presenter : null;

        private void Awake()
        {
            if (_origin == null)
                _origin = FindAnyObjectByType<XROrigin>();
            if (_avatar == null)
                _avatar = FindAnyObjectByType<PlayerAvatar>(FindObjectsInactive.Include);
            if (_hint != null)
                _hint.SetActive(false);
        }

        private void OnEnable()
        {
            if (Conversation != null)
                Conversation.ConversationStarted += OnConversationStarted;
            if (_switchAction != null && _switchAction.action != null)
            {
                _switchAction.action.performed += OnSwitchPerformed;
                _switchAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (Conversation != null)
                Conversation.ConversationStarted -= OnConversationStarted;
            if (_switchAction != null && _switchAction.action != null)
                _switchAction.action.performed -= OnSwitchPerformed;
        }

        /// <summary>
        /// Why a switch is not possible right now, or null when it is.
        /// </summary>
        public string WhyNot()
        {
            if (_busy)
                return "already switching";
            if (_origin == null || _anchor == null || _settings == null)
                return "not set up";
            if (Time.time - _lastSwitchTime < _settings.MinSecondsBetweenSwitches)
                return "too soon after the last switch";
            var conversation = Conversation;
            if (Mode == PovMode.Nurse && (conversation == null || !conversation.IsPlaying))
                return "no conversation";
            if (Mode == PovMode.Nurse && !conversation.IsPlayerNear(_settings.MaxDistanceToPatient))
                return "too far from the patient";
            if (Mode == PovMode.Nurse && !_settings.Unlimited && SwitchesThisConversation >= _settings.MaxSwitchesPerConversation)
                return "no switches left in this conversation";
            return null;
        }

        /// <summary>
        /// Switches to the other view if the limits allow it. Returns false when the switch is refused.
        /// </summary>
        public bool TrySwitch()
        {
            var reason = WhyNot();
            if (reason != null)
            {
                Debug.Log($"[ProCommVR] POV switch refused: {reason}.", this);
                return false;
            }
            _lastSwitchTime = Time.time;
            if (Mode == PovMode.Nurse)
                SwitchesThisConversation++;
            StartCoroutine(Switch(Mode == PovMode.Nurse ? PovMode.Patient : PovMode.Nurse));
            return true;
        }

        private void Update()
        {
            // The conversation was closed while the player looks through the patient's eyes (e.g. the shift ended):
            // back to the nurse, without waiting for the minimum time between switches.
            if (Mode == PovMode.Patient && !_busy && Conversation != null && !Conversation.IsPlaying)
            {
                Debug.Log("[ProCommVR] Conversation closed in the patient view: back to the nurse view.", this);
                _lastSwitchTime = Time.time;
                StartCoroutine(Switch(PovMode.Nurse));
            }
        }

        private void OnSwitchPerformed(InputAction.CallbackContext context)
        {
            TrySwitch();
        }

        private void OnConversationStarted()
        {
            SwitchesThisConversation = 0;
        }

        private IEnumerator Switch(PovMode to)
        {
            _busy = true;
            var fadeSeconds = _settings.FadeSeconds;
            if (_fade != null)
                yield return _fade.FadeTo(1f, fadeSeconds);

            if (to == PovMode.Patient)
                EnterPatientView();
            else
                ExitPatientView();
            Mode = to;
            PovChanged?.Invoke(to);

            if (_fade != null)
                yield return _fade.FadeTo(0f, fadeSeconds);
            _busy = false;
        }

        private void EnterPatientView()
        {
            // The nurse stays where the player stood.
            if (_avatar != null)
            {
                _avatar.FollowHead = false;
                _avatar.PlaceAtHead();
                _avatar.SetVisible(true);
            }

            var rig = _origin.transform;
            _savedPosition = rig.position;
            _savedRotation = rig.rotation;
            SetPlayerControlsEnabled(false);

            _anchor.GetEyes(out var eyes, out var forward);
            _origin.MatchOriginUpCameraForward(Vector3.up, forward);
            _origin.MoveCameraToWorldLocation(eyes);
            Physics.SyncTransforms();

            _anchor.SetBodyHidden(true);
            if (Conversation != null)
                Conversation.BubblesHidden = true;
            if (_hint != null)
            {
                _hint.transform.SetPositionAndRotation(eyes + forward * _hintOffset.x + Vector3.up * _hintOffset.y,
                    Quaternion.LookRotation(forward, Vector3.up));
                _hint.SetActive(true);
            }
        }

        private void ExitPatientView()
        {
            if (_hint != null)
                _hint.SetActive(false);
            if (Conversation != null)
                Conversation.BubblesHidden = false;
            _anchor.SetBodyHidden(false);

            _origin.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            Physics.SyncTransforms();
            SetPlayerControlsEnabled(true);

            if (_avatar != null)
            {
                _avatar.SetVisible(false);
                _avatar.FollowHead = true;
            }
        }

        /// <summary>
        /// Turns locomotion (providers and the character controller) and the interactors (lasers) of the rig off, and
        /// back on: only what was on before.
        /// </summary>
        private void SetPlayerControlsEnabled(bool enabled)
        {
            if (!enabled)
            {
                _turnedOff.Clear();
                foreach (var provider in _origin.GetComponentsInChildren<LocomotionProvider>(true))
                    TurnOff(provider);
                foreach (var interactor in _origin.GetComponentsInChildren<XRBaseInteractor>(true))
                    TurnOff(interactor);
                _characterController = _origin.GetComponent<CharacterController>();
                _characterControllerWasEnabled = _characterController != null && _characterController.enabled;
                if (_characterControllerWasEnabled)
                    _characterController.enabled = false;
                return;
            }

            foreach (var behaviour in _turnedOff)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
            _turnedOff.Clear();
            if (_characterControllerWasEnabled && _characterController != null)
                _characterController.enabled = true;
        }

        private void TurnOff(Behaviour behaviour)
        {
            if (!behaviour.enabled)
                return;
            behaviour.enabled = false;
            _turnedOff.Add(behaviour);
        }
    }
}
