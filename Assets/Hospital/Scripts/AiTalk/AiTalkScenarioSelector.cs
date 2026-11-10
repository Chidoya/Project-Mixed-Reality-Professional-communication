using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Testscene: kies welk scenario je speelt. Toetsen 1 t.e.m. 5 (Editor) of de linker joystick naar links/rechts
    /// (Quest) starten een nieuw gesprek met het personage van dat scenario. Alleen het AiNpc van het gekozen scenario
    /// staat aan; de camera (of XR-rig) gaat naar het kijkpunt van dat scenario. Geen scenario-specifieke code:
    /// de lijst met personages en kijkpunten zet de scene-builder.
    /// </summary>
    public class AiTalkScenarioSelector : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public int Number;
            public string Title;
            public AiNpc Npc;
            [Tooltip("Waar de speler staat (positie en kijkrichting).")]
            public Transform Viewpoint;
        }

        [SerializeField] private List<Entry> _entries = new();
        [Tooltip("Leeg = Camera.main. Op de Quest: de root van de XR-rig.")]
        [SerializeField] private Transform _player;
        [SerializeField] private int _startNumber = 2;
        [SerializeField, Range(0.3f, 0.95f)] private float _stickThreshold = 0.7f;

        private readonly List<InputAction> _keys = new();
        private InputAction _stick;
        private bool _stickReleased = true;

        public int Current { get; private set; }
        public IReadOnlyList<Entry> Entries => _entries;
        public event Action<Entry> Selected;

        private void OnEnable()
        {
            for (var n = 1; n <= 9; n++)
            {
                var number = n;
                var action = new InputAction($"Scenario{n}", InputActionType.Button, $"<Keyboard>/{n}");
                action.AddBinding($"<Keyboard>/numpad{n}");
                action.performed += _ => Select(number);
                action.Enable();
                _keys.Add(action);
            }
            _stick = new InputAction("ScenarioStick", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}");
            _stick.Enable();
        }

        private void OnDisable()
        {
            foreach (var key in _keys)
                key.Dispose();
            _keys.Clear();
            _stick?.Dispose();
            _stick = null;
        }

        private void Start()
        {
            foreach (var e in _entries)
                if (e.Npc != null)
                    e.Npc.enabled = false;
            Select(_startNumber);
        }

        private void Update()
        {
            if (_stick == null || _entries.Count == 0)
                return;
            var x = _stick.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < _stickThreshold * 0.5f)
            {
                _stickReleased = true;
                return;
            }
            if (!_stickReleased || Mathf.Abs(x) < _stickThreshold)
                return;
            _stickReleased = false;
            var index = Mathf.Max(0, _entries.FindIndex(e => e.Number == Current));
            index = (index + (x > 0f ? 1 : -1) + _entries.Count) % _entries.Count;
            Select(_entries[index].Number);
        }

        /// <summary>Start een nieuw gesprek voor scenario `number` (ook als het al gekozen was: opnieuw beginnen).</summary>
        public void Select(int number)
        {
            var entry = _entries.Find(e => e.Number == number);
            if (entry == null || entry.Npc == null)
                return;
            foreach (var e in _entries)
                if (e != entry && e.Npc != null && e.Npc.enabled)
                    e.Npc.enabled = false; // sluit ook een lopend gesprek af
            Current = number;
            MovePlayer(entry.Viewpoint);
            if (entry.Npc.enabled)
                entry.Npc.enabled = false; // opnieuw kiezen = opnieuw beginnen
            entry.Npc.enabled = true;
            _ = entry.Npc.Restart();
            Debug.Log($"[AiTalk] Scenario {number}: {entry.Title}");
            Selected?.Invoke(entry);
        }

        private void MovePlayer(Transform viewpoint)
        {
            if (viewpoint == null)
                return;
            var player = _player != null ? _player : Camera.main != null ? Camera.main.transform : null;
            if (player == null)
                return;
            player.SetPositionAndRotation(viewpoint.position, viewpoint.rotation);
        }
    }
}
