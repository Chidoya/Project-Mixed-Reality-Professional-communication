using System;
using ProCommVR.Navigation;
using TMPro;
using UnityEngine;

namespace ProCommVR.Shift
{
    public enum CountdownState
    {
        Idle,
        Armed,
        Running,
        Stopped,
        Expired
    }

    /// <summary>
    /// The countdown of one task, next to the shift timer: e.g. the surgical gurney that arrives in 90 seconds.
    /// <see cref="PatientRoom"/> arms it with the scenario's Time Limit when the shift starts. It starts running when
    /// the player comes within Start Range of the NPC (the encounter), shows the time on the display and plays the
    /// ticking sound. When it reaches zero, Arrival plays (the gurney comes) and <see cref="Expired"/> is raised.
    /// </summary>
    public class TaskCountdown : MonoBehaviour
    {
        [Tooltip("The countdown starts when the player's head comes within Start Range of this NPC, on the same floor.")]
        [SerializeField] private Transform _npc;

        [SerializeField, Min(0f)] private float _startRange = 4f;

        [Tooltip("Height of the player's head above the NPC (meters, min and max) that counts as the same floor.")]
        [SerializeField] private Vector2 _headHeightRange = new Vector2(-0.5f, 2.5f);

        [Tooltip("The player's head. Leave empty to use the main camera.")]
        [SerializeField] private Transform _player;

        [Header("Display")]
        [Tooltip("Shown while the countdown runs, e.g. the panel above the corridor.")]
        [SerializeField] private GameObject _display;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _color = new Color(1f, 0.75f, 0.2f);

        [Tooltip("Below this many seconds the time turns red.")]
        [SerializeField, Min(0f)] private float _urgentBelow = 15f;
        [SerializeField] private Color _urgentColor = new Color(1f, 0.2f, 0.15f);

        [Header("Sound and arrival")]
        [Tooltip("Optional. Loops while the countdown runs (ticking).")]
        [SerializeField] private AudioSource _ticking;

        [Tooltip("Optional. Plays when the time is up, e.g. the surgical gurney coming down the corridor.")]
        [SerializeField] private PathMover _arrival;

        private string _caption = "";
        private int _shownSeconds = -1;

        public CountdownState State { get; private set; } = CountdownState.Idle;
        public float Duration { get; private set; }
        public float TimeRemaining { get; private set; }

        public event Action<TaskCountdown> Started;
        public event Action<TaskCountdown> Expired;

        private Transform Player
        {
            get
            {
                if (_player == null && Camera.main != null)
                    _player = Camera.main.transform;
                return _player;
            }
        }

        private void Awake()
        {
            if (_display != null)
                _display.SetActive(false);
        }

        /// <summary>
        /// Gets ready: the countdown starts once the player meets the NPC.
        /// </summary>
        public void Arm(float seconds, string caption)
        {
            Duration = seconds;
            TimeRemaining = seconds;
            _caption = caption ?? "";
            State = CountdownState.Armed;
        }

        /// <summary>
        /// Stops the countdown, e.g. because the player answered in time. Does nothing once the time is up.
        /// </summary>
        public void Stop()
        {
            if (State != CountdownState.Armed && State != CountdownState.Running)
                return;
            State = CountdownState.Stopped;
            if (_ticking != null)
                _ticking.Stop();
            if (_display != null)
                _display.SetActive(false);
        }

        private void Update()
        {
            if (State == CountdownState.Armed && PlayerInRange())
                Begin();
            if (State != CountdownState.Running)
                return;

            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            UpdateLabel();
            if (TimeRemaining <= 0f)
                Expire();
        }

        private void Begin()
        {
            State = CountdownState.Running;
            if (_display != null)
                _display.SetActive(true);
            if (_ticking != null)
                _ticking.Play();
            _shownSeconds = -1;
            UpdateLabel();
            Started?.Invoke(this);
        }

        private void Expire()
        {
            State = CountdownState.Expired;
            if (_ticking != null)
                _ticking.Stop();
            if (_arrival != null)
                _arrival.Play();
            Expired?.Invoke(this);
        }

        private void UpdateLabel()
        {
            var seconds = Mathf.CeilToInt(TimeRemaining);
            if (_label == null || seconds == _shownSeconds)
                return;
            _shownSeconds = seconds;
            _label.color = TimeRemaining < _urgentBelow ? _urgentColor : _color;
            _label.text = $"<size=45%>{_caption}</size>\n{seconds / 60}:{seconds % 60:00}";
        }

        private bool PlayerInRange()
        {
            if (_npc == null || Player == null)
                return true;
            var offset = Player.position - _npc.position;
            var height = offset.y;
            offset.y = 0f;
            return offset.magnitude <= _startRange && height >= _headHeightRange.x && height <= _headHeightRange.y;
        }
    }
}
