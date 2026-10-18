using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Shows a <see cref="Scenario"/> in the world: the NPC line above the NPC and the four options
    /// as clickable bubbles in front of the player. The options only appear once the player is within
    /// Options Range of the NPC. After a choice the options disappear and the feedback is shown.
    /// Below the feedback a continue bubble appears. Scoring and scenario flow (<see cref="ScenarioSequence"/>)
    /// listen to <see cref="OptionChosen"/> and <see cref="Continued"/>.
    /// </summary>
    public class ScenarioPresenter : MonoBehaviour
    {
        [SerializeField] private Scenario _scenario;
        [SerializeField] private bool _playOnStart = true;

        [Header("Bubbles")]
        [SerializeField] private DialogueBubble _bubblePrefab;

        [Tooltip("Where the NPC line appears, e.g. above the NPC's head.")]
        [SerializeField] private Transform _npcBubbleAnchor;

        [Tooltip("Center of the 2x2 option grid, in front of the player. Feedback appears here too.")]
        [SerializeField] private Transform _optionsAnchor;

        [Tooltip("Distance between the centers of the option bubbles (x: columns, y: rows), in meters.")]
        [SerializeField] private Vector2 _optionSpacing = new Vector2(0.66f, 0.26f);

        [Tooltip("Shuffle the options so the best answer is not always in the same place.")]
        [SerializeField] private bool _shuffleOptions = true;

        [Header("Proximity")]
        [Tooltip("The NPC. The options appear once the player is within Options Range of it. Leave empty to show them immediately.")]
        [SerializeField] private Transform _npc;

        [Tooltip("Horizontal distance (meters) between the player's head and the NPC at which the options appear.")]
        [SerializeField, Min(0f)] private float _optionsRange = 1.5f;

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        [Tooltip("Move the options between the player and the NPC when they appear. Off: they stay at the Options Anchor.")]
        [SerializeField] private bool _placeOptionsInFrontOfPlayer = true;

        [Tooltip("How far in front of the player's head the options appear, in meters.")]
        [SerializeField, Min(0.3f)] private float _optionsDistance = 0.7f;

        [Tooltip("Height of the options relative to the player's eyes, in meters (negative is lower).")]
        [SerializeField] private float _optionsHeightOffset = -0.25f;

        [Header("After the choice")]
        [Tooltip("Label of the bubble below the feedback. A ScenarioSequence overrides it. Without a sequence, the bubble replays the scenario.")]
        [SerializeField] private string _continueLabel = "Try again";

        private readonly List<DialogueBubble> _bubbles = new List<DialogueBubble>();
        private bool _optionsPending;
        private bool _waitingForChoice;

        public event Action<Scenario> ScenarioStarted;
        public event Action<Scenario, DialogueOption> OptionChosen;

        /// <summary>
        /// Raised when the player clicks the continue bubble below the feedback.
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
            if (_optionsPending && PlayerInRange())
                ShowOptions();
        }

        public void Play(Scenario scenario)
        {
            ClearBubbles();
            _scenario = scenario;

            var npcBubble = CreateBubble(_npcBubbleAnchor, Vector3.zero, false);
            npcBubble.SetText($"<b>{scenario.NpcName}</b>\n{scenario.NpcLine}");

            _waitingForChoice = false;
            _optionsPending = true;
            ScenarioStarted?.Invoke(scenario);
            if (PlayerInRange())
                ShowOptions();
        }

        /// <summary>
        /// Removes all bubbles, e.g. before the results screen.
        /// </summary>
        public void Clear()
        {
            ClearBubbles();
            _optionsPending = false;
            _waitingForChoice = false;
        }

        private void ShowOptions()
        {
            _optionsPending = false;
            if (_placeOptionsInFrontOfPlayer)
                PlaceOptionsAnchor();

            var options = new List<DialogueOption>(_scenario.Options);
            if (_shuffleOptions)
                Shuffle(options);

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var bubble = CreateBubble(_optionsAnchor, GridOffset(i), true);
                bubble.SetText(option.Text);
                bubble.Clicked += () => Choose(option);
            }
            _waitingForChoice = true;
        }

        private void Choose(DialogueOption option)
        {
            if (!_waitingForChoice)
                return;
            _waitingForChoice = false;

            // Keep the NPC line (first bubble), replace the options with the feedback.
            for (var i = _bubbles.Count - 1; i >= 1; i--)
                DestroyBubble(i);

            var feedbackBubble = CreateBubble(_optionsAnchor, new Vector3(0f, _optionSpacing.y * 0.5f, 0f), false);
            feedbackBubble.SetText(option.Feedback);

            OptionChosen?.Invoke(_scenario, option);

            var continueBubble = CreateBubble(_optionsAnchor, new Vector3(0f, -_optionSpacing.y * 0.5f, 0f), true);
            continueBubble.SetText(_continueLabel);
            continueBubble.Clicked += OnContinueClicked;
        }

        private void OnContinueClicked()
        {
            if (Continued != null)
                Continued.Invoke();
            else
                Play(_scenario);
        }

        private bool PlayerInRange()
        {
            if (_npc == null || Player == null)
                return true;
            return HorizontalOffset(Player.position, _npc.position).magnitude <= _optionsRange;
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

        private void DestroyBubble(int index)
        {
            Destroy(_bubbles[index].gameObject);
            _bubbles.RemoveAt(index);
        }

        private void ClearBubbles()
        {
            for (var i = _bubbles.Count - 1; i >= 0; i--)
                DestroyBubble(i);
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
