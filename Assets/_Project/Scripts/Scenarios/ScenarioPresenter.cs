using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Shows a <see cref="Scenario"/> in the world: the NPC line above the NPC and the four options
    /// as clickable bubbles in front of the player. The options are only visible while the player is within
    /// Options Range of the NPC. After a choice the options disappear, the NPC bubble shows the NPC's reaction
    /// and a continue bubble appears. The feedback (why the choice was good or not) is only shown on the
    /// <see cref="ResultsScreen"/>. Scoring, scenario flow (<see cref="ScenarioSequence"/>) and the NPC's body
    /// language (<see cref="NpcReactor"/>) listen to <see cref="OptionChosen"/> and <see cref="Continued"/>.
    /// </summary>
    public class ScenarioPresenter : MonoBehaviour
    {
        [SerializeField] private Scenario _scenario;
        [SerializeField] private bool _playOnStart = true;

        [Header("Bubbles")]
        [SerializeField] private DialogueBubble _bubblePrefab;

        [Tooltip("Where the NPC line appears, e.g. above the NPC's head.")]
        [SerializeField] private Transform _npcBubbleAnchor;

        [Tooltip("Center of the 2x2 option grid, in front of the player. The continue bubble appears here too.")]
        [SerializeField] private Transform _optionsAnchor;

        [Tooltip("Distance between the centers of the option bubbles (x: columns, y: rows), in meters.")]
        [SerializeField] private Vector2 _optionSpacing = new Vector2(0.66f, 0.26f);

        [Tooltip("Shuffle the options so the best answer is not always in the same place.")]
        [SerializeField] private bool _shuffleOptions = true;

        [Header("Proximity")]
        [Tooltip("The NPC. The options appear once the player is within Options Range of it. Leave empty to show them immediately.")]
        [SerializeField] private Transform _npc;

        [Tooltip("Horizontal distance (meters) between the player's head and the NPC within which the options are visible.")]
        [SerializeField, Min(0f)] private float _optionsRange = 1.5f;

        [Tooltip("Extra distance (meters) before visible options hide again, so they don't flicker at the edge of the range.")]
        [SerializeField, Min(0f)] private float _optionsHideMargin = 0.1f;

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        [Tooltip("Move the options between the player and the NPC when they appear. Off: they stay at the Options Anchor.")]
        [SerializeField] private bool _placeOptionsInFrontOfPlayer = true;

        [Tooltip("How far in front of the player's head the options appear, in meters.")]
        [SerializeField, Min(0.3f)] private float _optionsDistance = 0.7f;

        [Tooltip("Height of the options relative to the player's eyes, in meters (negative is lower).")]
        [SerializeField] private float _optionsHeightOffset = -0.25f;

        [Header("After the choice")]
        [Tooltip("Label of the bubble shown after the choice. A ScenarioSequence overrides it. Without a sequence, the bubble replays the scenario.")]
        [SerializeField] private string _continueLabel = "Try again";

        private readonly List<DialogueBubble> _bubbles = new List<DialogueBubble>();
        private readonly List<DialogueBubble> _optionBubbles = new List<DialogueBubble>();
        private DialogueBubble _npcBubble;
        private bool _waitingForChoice;
        private bool _optionsVisible;

        public event Action<Scenario> ScenarioStarted;
        public event Action<Scenario, DialogueOption> OptionChosen;

        /// <summary>
        /// Raised when the player clicks the continue bubble after the choice.
        /// </summary>
        public event Action Continued;

        public Scenario CurrentScenario => _scenario;

        public string ContinueLabel
        {
            get => _continueLabel;
            set => _continueLabel = value;
        }

        private Transform Player
        {
            get
            {
                if (_player == null && Camera.main != null)
                    _player = Camera.main.transform;
                return _player;
            }
        }

        private void Start()
        {
            if (_playOnStart && _scenario != null)
                Play(_scenario);
        }

        private void Update()
        {
            if (_waitingForChoice)
                UpdateOptionsVisibility();
        }

        public void Play(Scenario scenario)
        {
            ClearBubbles();
            _scenario = scenario;

            _npcBubble = CreateBubble(_npcBubbleAnchor, Vector3.zero, false);
            SetNpcText(scenario.NpcLine);

            _waitingForChoice = true;
            _optionsVisible = false;
            ScenarioStarted?.Invoke(scenario);
            UpdateOptionsVisibility();
        }

        /// <summary>
        /// Removes all bubbles, e.g. before the results screen.
        /// </summary>
        public void Clear()
        {
            ClearBubbles();
            _waitingForChoice = false;
            _optionsVisible = false;
        }

        /// <summary>
        /// Shows the options when the player walks into range and hides them again when the player walks away.
        /// </summary>
        private void UpdateOptionsVisibility()
        {
            var range = _optionsVisible ? _optionsRange + _optionsHideMargin : _optionsRange;
            var inRange = PlayerInRange(range);
            if (inRange && !_optionsVisible)
                ShowOptions();
            else if (!inRange && _optionsVisible)
                HideOptions();
        }

        private void ShowOptions()
        {
            if (_placeOptionsInFrontOfPlayer)
                PlaceOptionsAnchor();

            if (_optionBubbles.Count == 0)
                CreateOptionBubbles();
            else
                SetOptionsActive(true);
            _optionsVisible = true;
        }

        private void HideOptions()
        {
            SetOptionsActive(false);
            _optionsVisible = false;
        }

        /// <summary>
        /// Created once per scenario, so the shuffled order stays the same when the options reappear.
        /// </summary>
        private void CreateOptionBubbles()
        {
            var options = new List<DialogueOption>(_scenario.Options);
            if (_shuffleOptions)
                Shuffle(options);

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var bubble = CreateBubble(_optionsAnchor, GridOffset(i), true);
                bubble.SetText(option.Text);
                bubble.Clicked += () => Choose(option);
                _optionBubbles.Add(bubble);
            }
        }

        private void SetOptionsActive(bool active)
        {
            foreach (var bubble in _optionBubbles)
                bubble.gameObject.SetActive(active);
        }

        private void Choose(DialogueOption option)
        {
            if (!_waitingForChoice || !_optionsVisible)
                return;
            _waitingForChoice = false;
            _optionsVisible = false;

            // No feedback text here: the player only sees the NPC's reaction. The explanation is on the results screen.
            DestroyOptionBubbles();
            if (!string.IsNullOrWhiteSpace(option.NpcReaction))
                SetNpcText($"<i>{option.NpcReaction}</i>");

            OptionChosen?.Invoke(_scenario, option);

            var continueBubble = CreateBubble(_optionsAnchor, Vector3.zero, true);
            continueBubble.SetText(_continueLabel);
            continueBubble.Clicked += OnContinueClicked;
        }

        private void SetNpcText(string line)
        {
            _npcBubble.SetText($"<b>{_scenario.NpcName}</b>\n{line}");
        }

        private void OnContinueClicked()
        {
            if (Continued != null)
                Continued.Invoke();
            else
                Play(_scenario);
        }

        private bool PlayerInRange(float range)
        {
            if (_npc == null || Player == null)
                return true;
            return HorizontalOffset(Player.position, _npc.position).magnitude <= range;
        }

        /// <summary>
        /// Puts the option grid between the player and the NPC, facing the player, slightly below eye height.
        /// </summary>
        private void PlaceOptionsAnchor()
        {
            if (_npc == null || Player == null)
                return;

            var toNpc = HorizontalOffset(Player.position, _npc.position);
            if (toNpc.sqrMagnitude < 0.0001f)
                return;

            var distance = Mathf.Min(_optionsDistance, toNpc.magnitude * 0.6f);
            var direction = toNpc.normalized;
            _optionsAnchor.position = Player.position + direction * distance + Vector3.up * _optionsHeightOffset;
            _optionsAnchor.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private static Vector3 HorizontalOffset(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            offset.y = 0f;
            return offset;
        }

        private Vector3 GridOffset(int index)
        {
            var column = index % 2 == 0 ? -0.5f : 0.5f;
            var row = index < 2 ? 0.5f : -0.5f;
            return new Vector3(column * _optionSpacing.x, row * _optionSpacing.y, 0f);
        }

        private DialogueBubble CreateBubble(Transform anchor, Vector3 localOffset, bool clickable)
        {
            var bubble = Instantiate(_bubblePrefab, anchor);
            bubble.transform.localPosition = localOffset;
            bubble.transform.localRotation = Quaternion.identity;
            bubble.SetClickable(clickable);
            _bubbles.Add(bubble);
            return bubble;
        }

        private void DestroyOptionBubbles()
        {
            foreach (var bubble in _optionBubbles)
            {
                _bubbles.Remove(bubble);
                Destroy(bubble.gameObject);
            }
            _optionBubbles.Clear();
        }

        private void ClearBubbles()
        {
            foreach (var bubble in _bubbles)
                Destroy(bubble.gameObject);
            _bubbles.Clear();
            _optionBubbles.Clear();
            _npcBubble = null;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
