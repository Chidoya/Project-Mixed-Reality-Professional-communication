using ProCommVR.Atmosphere;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Shift
{
    /// <summary>
    /// The sign next to a patient room door: room, patient and scenario, and a color patch showing the
    /// patient's current mood (read from a <see cref="MoodController"/>).
    /// </summary>
    public class RoomSign : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        [Tooltip("Takes the color of the patient's mood.")]
        [SerializeField] private Graphic _moodPatch;

        [SerializeField] private MoodController _moodController;

        [SerializeField] private string _caredForText = "Cared for";

        private string _text = "";
        private bool _caredFor;

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
            _caredFor = false;
            Refresh();
        }

        public void SetCaredFor(bool caredFor)
        {
            _caredFor = caredFor;
            Refresh();
        }

        private void Refresh()
        {
            _label.text = _caredFor ? $"{_text}\n<size=60%><b>{_caredForText}</b></size>" : _text;
        }

        private void OnMoodChanged(MoodController moodController)
        {
            if (_moodPatch != null)
                _moodPatch.color = moodController.CurrentColor;
        }
    }
}
