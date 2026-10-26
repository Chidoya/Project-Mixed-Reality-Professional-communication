using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// The speech bubble of the scenario's bystander: a second character who is part of the conversation without
    /// being the one the player answers (e.g. the family member waiting at the nurse's station while the player
    /// talks to the doctor). Shows the scenario's Bystander Line once the bystander pays attention (an
    /// <see cref="NpcAttention"/>, or right away without one) and the chosen option's Bystander Reaction after the
    /// choice: right away, or only once the player comes back near (React When Near), e.g. to tell the family the
    /// outcome. The bystander's body language (an <see cref="NpcReactor"/> with React As Bystander) changes at the same moment.
    /// </summary>
    public class BystanderSpeech : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;
        [SerializeField] private DialogueBubble _bubblePrefab;

        [Tooltip("Where the bubble appears, e.g. above the bystander's head.")]
        [SerializeField] private Transform _anchor;

        [Tooltip("Turn the bubble to the player each time it shows a line, so it can be read from wherever the player comes.")]
        [SerializeField] private bool _faceReader = true;

        [Tooltip("Optional. The line appears when the bystander pays attention. Empty: it appears when the scenario starts.")]
        [SerializeField] private NpcAttention _attention;

        [Tooltip("Optional. The bystander's body language (an NpcReactor with React As Bystander), shown together with the reaction line.")]
        [SerializeField] private NpcReactor _reactor;

        [Header("Reaction")]
        [Tooltip("Show the reaction only once the player is within Reaction Range of the bystander (e.g. to tell the family). Off: right after the choice.")]
        [SerializeField] private bool _reactWhenNear;

        [SerializeField, Min(0f)] private float _reactionRange = 2.5f;

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        private DialogueBubble _bubble;
        private Scenario _scenario;
        private DialogueOption _pendingReaction;

        /// <summary>
        /// The text in the bubble now, or empty.
        /// </summary>
        public string CurrentLine { get; private set; } = "";

        /// <summary>
        /// True once the bystander showed its reaction to the choice.
        /// </summary>
        public bool HasReacted { get; private set; }

        private Transform Player
        {
            get
            {
                if (_player == null && Camera.main != null)
                    _player = Camera.main.transform;
                return _player;
            }
        }

        private void OnEnable()
        {
            if (_presenter != null)
            {
                _presenter.ScenarioStarted += OnScenarioStarted;
                _presenter.OptionChosen += OnOptionChosen;
            }
            if (_attention != null)
                _attention.Attended += OnAttended;
        }

        private void OnDisable()
        {
            if (_presenter != null)
            {
                _presenter.ScenarioStarted -= OnScenarioStarted;
                _presenter.OptionChosen -= OnOptionChosen;
            }
            if (_attention != null)
                _attention.Attended -= OnAttended;
        }

        private void Update()
        {
            if (_pendingReaction != null && PlayerNear())
                ShowReaction();
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            _scenario = scenario;
            _pendingReaction = null;
            HasReacted = false;
            Hide();
            if (_attention == null && scenario.HasBystander)
                Show(scenario.BystanderLine);
        }

        private void OnAttended(NpcAttention attention)
        {
            if (_scenario != null && _scenario.HasBystander && !HasReacted)
                Show(_scenario.BystanderLine);
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            if (!scenario.HasBystander)
                return;
            _pendingReaction = option;
            if (!_reactWhenNear || PlayerNear())
                ShowReaction();
        }

        private void ShowReaction()
        {
            HasReacted = true;
            if (!string.IsNullOrWhiteSpace(_pendingReaction.BystanderReaction))
                Show($"<i>{_pendingReaction.BystanderReaction}</i>");
            if (_reactor != null)
                _reactor.React(_pendingReaction);
            _pendingReaction = null;
        }

        private void Show(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            if (_bubble == null)
            {
                _bubble = Instantiate(_bubblePrefab, _anchor);
                _bubble.transform.localPosition = Vector3.zero;
                _bubble.transform.localRotation = Quaternion.identity;
                _bubble.SetClickable(false);
            }
            if (_faceReader && Player != null)
            {
                // A bubble's front faces against its forward: look from the player to the bubble.
                var look = _anchor.position - Player.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    _bubble.transform.rotation = Quaternion.LookRotation(look);
            }
            CurrentLine = line;
            _bubble.SetText($"<b>{_scenario.BystanderName}</b>\n{line}");
        }

        private void Hide()
        {
            if (_bubble != null)
                Destroy(_bubble.gameObject);
            _bubble = null;
            CurrentLine = "";
        }

        private bool PlayerNear()
        {
            if (Player == null)
                return false;
            var offset = Player.position - _anchor.position;
            var height = offset.y;
            offset.y = 0f;
            // The anchor hangs above the head: the player's head is between about 1.5 m below it and level with it.
            return offset.magnitude <= _reactionRange && height > -1.5f && height < 0.5f;
        }
    }
}
