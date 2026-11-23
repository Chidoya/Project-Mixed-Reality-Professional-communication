using System.Collections.Generic;
using ProCommVR.Scenarios;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProCommVR.Hospital.Debrief.Testing
{
    /// <summary>
    /// Test scene of the head doctor (ProCommVR > Liam > Debrief > Build Doctor Test Scene): play every line of the
    /// DebriefScript and of the voice library. Bubbles Previous / Play / Next (laser + trigger), or the keys P, Enter, N.
    /// Every other line she points at the whiteboard. A skips the line (Space in the Editor).
    /// </summary>
    public class DebriefDoctorTestPanel : MonoBehaviour
    {
        [SerializeField] private DebriefScript _script;
        [SerializeField] private DoctorSpeaker _doctor;
        [SerializeField] private DoctorPointer _pointer;
        [SerializeField] private Transform _board;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private DialogueBubble _previous;
        [SerializeField] private DialogueBubble _play;
        [SerializeField] private DialogueBubble _next;

        private readonly List<DebriefLine> _lines = new();
        private int _index;
        private Coroutine _playing;
        private InputAction _keys;

        public IReadOnlyList<DebriefLine> Lines => _lines;
        public int Index => _index;

        private void Awake()
        {
            if (_script != null)
            {
                _lines.AddRange(_script.Lines);
                if (_script.Voices != null)
                    _lines.AddRange(_script.Voices.Entries);
            }
            if (_previous != null) _previous.Clicked += () => Step(-1);
            if (_play != null) _play.Clicked += Play;
            if (_next != null) _next.Clicked += () => Step(1);
            foreach (var bubble in new[] { _previous, _play, _next })
                bubble?.SetClickable(true);
            Show();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.nKey.wasPressedThisFrame) Step(1);
            if (keyboard.pKey.wasPressedThisFrame) Step(-1);
            if (keyboard.enterKey.wasPressedThisFrame) Play();
        }

        public void Step(int delta)
        {
            if (_lines.Count == 0)
                return;
            _index = (_index + delta + _lines.Count) % _lines.Count;
            Show();
        }

        public void Play()
        {
            if (_lines.Count == 0)
                return;
            if (_playing != null)
                StopCoroutine(_playing);
            _doctor.Stop();
            _playing = StartCoroutine(PlayLine(_lines[_index], _index % 2 == 1));
        }

        private System.Collections.IEnumerator PlayLine(DebriefLine line, bool point)
        {
            if (point && _pointer != null && _board != null)
                _pointer.PointAt(_board);
            yield return _doctor.Say(line);
            _pointer?.Stop();
            _playing = null;
        }

        private void Show()
        {
            if (_status == null || _lines.Count == 0)
                return;
            var line = _lines[_index];
            _status.text = $"<b>{_index + 1} / {_lines.Count}: {line.Key}</b>{(line.Clip == null ? "  <color=#c0392b>(no clip)</color>" : "")}\n{line.Text}";
        }
    }
}
