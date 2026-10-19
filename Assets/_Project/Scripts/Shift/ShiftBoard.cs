using System.Text;
using TMPro;
using UnityEngine;

namespace ProCommVR.Shift
{
    /// <summary>
    /// A board in the world with the patients of the shift, which ones are cared for, and the time left.
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

        private void Update()
        {
            var seconds = Mathf.CeilToInt(_shiftManager.TimeRemaining);
            if (seconds == _shownSeconds && _shiftManager.CompletedCount == _shownCompleted &&
                _shiftManager.State == _shownState && _shiftManager.AssignedRooms.Count == _shownRooms)
                return;

            _shownSeconds = seconds;
            _shownCompleted = _shiftManager.CompletedCount;
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
                var caredFor = _shiftManager.IsCompleted(room);
                text.Append(room.RoomName).Append(": ").Append(room.Task.PatientName).Append("   ")
                    .Append(caredFor ? "<color=#1E8C3A><b>Cared for</b></color>" : "<color=#8A8A8A>Waiting</color>")
                    .Append('\n');
            }
            text.Append("\nCared for: ").Append(_shiftManager.CompletedCount).Append(" / ").Append(_shiftManager.AssignedRooms.Count);
            return text.ToString();
        }
    }
}
