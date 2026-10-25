using System.Text;
using TMPro;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// A board in the world with the patients of the shift, which ones are cared for (or failed), and the time left.
    /// Put one in the briefing room and one in the corridor; they all read the same <see cref="ShiftManager"/>.
    /// </summary>
    public class ShiftBoard : MonoBehaviour
    {
        [SerializeField] private ShiftManager _shiftManager;
        [SerializeField] private TMP_Text _label;

        [SerializeField] private string _startHint = "Start your shift at the computer in the briefing room.";

        private int _shownSeconds = -1;
        private int _shownCompleted = -1;
        private ShiftState _shownState;
        private int _shownRooms = -1;
        private int _shownFailed = -1;

        private void Update()
        {
            var seconds = Mathf.CeilToInt(_shiftManager.TimeRemaining);
            if (seconds == _shownSeconds && _shiftManager.CompletedCount == _shownCompleted && _shiftManager.FailedCount == _shownFailed &&
                _shiftManager.State == _shownState && _shiftManager.AssignedRooms.Count == _shownRooms)
                return;

            _shownSeconds = seconds;
            _shownCompleted = _shiftManager.CompletedCount;
            _shownFailed = _shiftManager.FailedCount;
            _shownState = _shiftManager.State;
            _shownRooms = _shiftManager.AssignedRooms.Count;
            _label.text = BuildText(seconds);
        }

        private string BuildText(int seconds)
        {
            var text = new StringBuilder();
            text.Append("<b>Your shift</b>   ");
            switch (_shiftManager.State)
            {
                case ShiftState.Briefing:
                    text.Append("<size=70%>").Append(_startHint).Append("</size>");
                    break;
                case ShiftState.Running:
                    text.Append("Time left ").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"));
                    break;
                default:
                    text.Append("Shift ended");
                    break;
            }
            text.Append("\n\n");

            foreach (var room in _shiftManager.AssignedRooms)
            {
                text.Append(room.RoomName).Append(": ").Append(room.Task.PatientName).Append("   ")
                    .Append(Status(_shiftManager.GetOutcome(room.PatientId)))
                    .Append('\n');
            }
            text.Append("\nCared for: ").Append(_shiftManager.CompletedCount).Append(" / ").Append(_shiftManager.AssignedRooms.Count);
            if (_shiftManager.FailedCount > 0)
                text.Append("   <color=#C62828>Failed: ").Append(_shiftManager.FailedCount).Append("</color>");
            return text.ToString();
        }

        private static string Status(TaskOutcome outcome)
        {
            switch (outcome)
            {
                case TaskOutcome.Completed:
                    return "<color=#1E8C3A><b>Cared for</b></color>";
                case TaskOutcome.FailedTimeUp:
                    return "<color=#C62828><b>Failed: time ran out</b></color>";
                default:
                    return "<color=#8A8A8A>Waiting</color>";
            }
        }
    }
}
