using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Shows a <see cref="Scenario"/> in the world: the NPC line above the NPC and the four options
    /// as clickable bubbles in front of the player. After a choice the options disappear and the
    /// feedback is shown. Scoring and scenario flow listen to <see cref="OptionChosen"/>.
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

        [Header("Testing")]
        [Tooltip("Show a bubble after the feedback to play the scenario again. Handy while testing.")]
        [SerializeField] private bool _allowRestart = true;
        [SerializeField] private string _restartLabel = "Try again";

        private readonly List<DialogueBubble> _bubbles = new List<DialogueBubble>();
        private bool _waitingForChoice;

        public event Action<Scenario, DialogueOption> OptionChosen;

        public Scenario CurrentScenario => _scenario;

        private void Start()
        {
            if (_playOnStart && _scenario != null)
                Play(_scenario);
        }

        public void Play(Scenario scenario)
        {
            ClearBubbles();
            _scenario = scenario;

            var npcBubble = CreateBubble(_npcBubbleAnchor, Vector3.zero, false);
            npcBubble.SetText($"<b>{scenario.NpcName}</b>\n{scenario.NpcLine}");

            var options = new List<DialogueOption>(scenario.Options);
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

            if (_allowRestart)
            {
                var restartBubble = CreateBubble(_optionsAnchor, new Vector3(0f, -_optionSpacing.y * 0.5f, 0f), true);
                restartBubble.SetText(_restartLabel);
                restartBubble.Clicked += () => Play(_scenario);
            }

            OptionChosen?.Invoke(_scenario, option);
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
