using ProCommVR.Atmosphere;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Shift
{
    /// <summary>
    /// The sign next to a patient room door: room, patient and scenario, and a color patch showing the
    /// patient's current mood (read from a <see cref="MoodController"/>). Once the task ends it shows the outcome:
    /// "Cared for", or in red "Time ran out" or "Not resolved".
    /// </summary>
    public class RoomSign : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        [Tooltip("Takes the color of the patient's mood.")]
        [SerializeField] private Graphic _moodPatch;

        [SerializeField] private MoodController _moodController;

        [SerializeField] private string _caredForText = "Cared for";
        [SerializeField] private string _failedText = "Time ran out";
        [SerializeField] private string _failedAnswerText = "Not resolved";

        private string _text = "";
        private TaskOutcome _outcome;

        private void OnEnable()
        {
            if (_moodController == null)
                return;
            _moodController.MoodChanged += OnMoodChanged;
            if (_moodController.CurrentProfile != null)
                OnMoodChanged(_moodController);
        }

        private void OnDisable()
        {
            if (_moodController != null)
                _moodController.MoodChanged -= OnMoodChanged;
        }

        public void Show(string room, string patient, string scenario)
        {
            _text = $"<b>{room}</b>\n{patient}\n<size=60%>{scenario}</size>";
            _outcome = TaskOutcome.None;
            Refresh();
        }

        public void SetOutcome(TaskOutcome outcome)
        {
            _outcome = outcome;
            Refresh();
        }

        private void Refresh()
        {
            switch (_outcome)
            {
                case TaskOutcome.Completed:
                    _label.text = $"{_text}\n<size=60%><b>{_caredForText}</b></size>";
                    break;
                case TaskOutcome.FailedTimeUp:
                    _label.text = $"{_text}\n<size=60%><b><color=#C62828>{_failedText}</color></b></size>";
                    break;
                case TaskOutcome.FailedAnswer:
                    _label.text = $"{_text}\n<size=60%><b><color=#C62828>{_failedAnswerText}</color></b></size>";
                    break;
                default:
                    _label.text = _text;
                    break;
            }
        }

        private void OnMoodChanged(MoodController moodController)
        {
            if (_moodPatch != null)
                _moodPatch.color = moodController.CurrentColor;
        }
    }
}
