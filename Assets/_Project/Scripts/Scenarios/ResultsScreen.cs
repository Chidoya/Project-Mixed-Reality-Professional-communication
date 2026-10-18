using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// End screen in the world: total score, and per scenario the score, the chosen answer and its feedback.
    /// Appears in front of the player. The restart bubble starts the sequence again.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _bodyLabel;
        [SerializeField] private DialogueBubble _restartBubble;

        [SerializeField] private string _title = "Your results";
        [SerializeField] private string _restartLabel = "Start again";

        [Header("Placement")]
        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        [Tooltip("Distance in front of the player's head, in meters.")]
        [SerializeField, Min(0.5f)] private float _distance = 1.3f;

        [Tooltip("Height of the screen's center relative to the player's eyes, in meters.")]
        [SerializeField] private float _heightOffset = -0.1f;

        public event Action RestartRequested;

        private void Awake()
        {
            _restartBubble.SetText(_restartLabel);
            _restartBubble.Clicked += OnRestartClicked;
        }

        private void OnDestroy()
        {
            _restartBubble.Clicked -= OnRestartClicked;
        }

        public void Show(SessionScore score)
        {
            _titleLabel.text = $"{_title}\n<size=70%>Total score: {score.Total} / {score.BestTotal}</size>";
            _bodyLabel.text = BuildBody(score);
            PlaceInFrontOfPlayer();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static string BuildBody(SessionScore score)
        {
            var text = new StringBuilder();
            foreach (var result in score.Results)
            {
                text.Append("<b>").Append(result.Scenario.Title).Append("</b>  ")
                    .Append(result.Score).Append(" / ").Append(result.BestScore).Append('\n');
                text.Append("You said: \"").Append(result.Chosen.Text).Append("\"\n");
                text.Append("<i>").Append(result.Chosen.Feedback).Append("</i>\n\n");
            }
            return text.ToString().TrimEnd();
        }

        private void PlaceInFrontOfPlayer()
        {
            if (_player == null && Camera.main != null)
                _player = Camera.main.transform;
            if (_player == null)
                return;

            var forward = _player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            transform.position = _player.position + forward * _distance + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void OnRestartClicked()
        {
            RestartRequested?.Invoke();
        }
    }
}
