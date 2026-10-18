using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A conversation scenario stored as data: the NPC's opening line, exactly four player options
    /// and a mood. Create one via Create > ProCommVR > Scenario; no code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewScenario", menuName = "ProCommVR/Scenario")]
    public class Scenario : ScriptableObject
    {
        public const int OptionCount = 4;

        [Tooltip("Short unique id, e.g. \"discharged-patient\". Used for results and logging.")]
        [SerializeField] private string _id;

        [Tooltip("Title shown in menus and on the results screen.")]
        [SerializeField] private string _title;

        [SerializeField] private Mood _mood = Mood.Neutral;

        [Header("NPC")]
        [SerializeField] private string _npcName;

        [Tooltip("What the NPC says to open the conversation.")]
        [SerializeField, TextArea(3, 8)] private string _npcLine;

        [Header("Player options (exactly 4, shown in random order)")]
        [SerializeField] private DialogueOption[] _options = NewOptions();

        public string Id => _id;
        public string Title => _title;
        public Mood Mood => _mood;
        public string NpcName => _npcName;
        public string NpcLine => _npcLine;
        public IReadOnlyList<DialogueOption> Options => _options;

        /// <summary>
        /// Score of the best option: the maximum a player can get in this scenario.
        /// </summary>
        public int BestScore => _options.Max(o => o.Score);

        private void OnValidate()
        {
            if (_options != null && _options.Length == OptionCount)
                return;

            Debug.LogWarning($"[ProCommVR] Scenario '{name}' must have exactly {OptionCount} options; resizing.", this);
            var oldLength = _options?.Length ?? 0;
            Array.Resize(ref _options, OptionCount);
            for (var i = oldLength; i < OptionCount; i++)
                _options[i] = new DialogueOption();
        }

        private static DialogueOption[] NewOptions()
        {
            var options = new DialogueOption[OptionCount];
            for (var i = 0; i < OptionCount; i++)
                options[i] = new DialogueOption();
            return options;
        }
    }
}
