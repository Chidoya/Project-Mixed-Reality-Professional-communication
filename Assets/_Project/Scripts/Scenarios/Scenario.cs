using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A conversation scenario stored as data: the NPC's opening line, exactly four player options
    /// and a mood. Optional: a choice of where to talk first (<see cref="Locations"/>), a time limit
    /// (<see cref="TimeLimit"/>), a line for while the NPC is busy (<see cref="BusyLine"/>) and a bystander
    /// (<see cref="BystanderName"/>). Create one via Create > ProCommVR > Scenario; no code changes needed.
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

        [Tooltip("Optional. What the NPC says while it is busy and cannot talk yet, e.g. \"I don't have time right now.\" " +
                 "The scene decides when the NPC is busy (BusyNpc).")]
        [SerializeField, TextArea(1, 3)] private string _busyLine;

        [Header("Bystander (optional)")]
        [Tooltip("A second character in the scene who is part of the conversation without being the one the player answers, " +
                 "e.g. the family member at the nurse's station. Leave empty for none.")]
        [SerializeField] private string _bystanderName;

        [Tooltip("What the bystander says when the player comes near (BystanderSpeech).")]
        [SerializeField, TextArea(2, 6)] private string _bystanderLine;

        [Header("Player options (exactly 4, shown in random order)")]
        [SerializeField] private DialogueOption[] _options = NewOptions();

        [Header("Where to talk (optional)")]
        [Tooltip("Said by the NPC before the options, while the player chooses where to continue the conversation. " +
                 "The NPC Line above follows at the chosen place.")]
        [SerializeField, TextArea(2, 6)] private string _locationLine;

        [Tooltip("The places the player can choose from. Leave empty to go straight to the options.")]
        [SerializeField] private ConversationLocation[] _locations = Array.Empty<ConversationLocation>();

        [Header("Time limit (optional)")]
        [Tooltip("Seconds the player has to answer, counted from the moment the encounter starts. 0 = no time limit.")]
        [SerializeField, Min(0f)] private float _timeLimit;

        [Tooltip("Shown above the countdown, e.g. \"Surgical gurney arrives in\".")]
        [SerializeField] private string _timeLimitLabel;

        [Tooltip("What the NPC says when the time is up.")]
        [SerializeField, TextArea(1, 3)] private string _timeUpLine;

        [Tooltip("Shown on the results screen when the time ran out.")]
        [SerializeField, TextArea(2, 6)] private string _timeUpFeedback;

        public string Id => _id;
        public string Title => _title;
        public Mood Mood => _mood;
        public string NpcName => _npcName;
        public string NpcLine => _npcLine;
        public string BusyLine => _busyLine;
        public string BystanderName => _bystanderName;
        public string BystanderLine => _bystanderLine;
        public bool HasBystander => !string.IsNullOrWhiteSpace(_bystanderName);
        public IReadOnlyList<DialogueOption> Options => _options;
        public string LocationLine => _locationLine;
        public IReadOnlyList<ConversationLocation> Locations => _locations ?? Array.Empty<ConversationLocation>();
        public bool HasLocationChoice => Locations.Count > 0;
        public float TimeLimit => _timeLimit;
        public bool HasTimeLimit => _timeLimit > 0f;
        public string TimeLimitLabel => _timeLimitLabel;
        public string TimeUpLine => _timeUpLine;
        public string TimeUpFeedback => _timeUpFeedback;

        /// <summary>
        /// Score of the best option (plus the best place, if there is a choice of where to talk): the maximum a
        /// player can get in this scenario.
        /// </summary>
        public int BestScore => _options.Max(o => o.Score) + (HasLocationChoice ? Locations.Max(l => l.Score) : 0);

        private void OnValidate()
        {
            ValidateLocations();
            if (_options != null && _options.Length == OptionCount)
                return;

            Debug.LogWarning($"[ProCommVR] Scenario '{name}' must have exactly {OptionCount} options; resizing.", this);
            var oldLength = _options?.Length ?? 0;
            Array.Resize(ref _options, OptionCount);
            for (var i = oldLength; i < OptionCount; i++)
                _options[i] = new DialogueOption();
        }

        private void ValidateLocations()
        {
            var ids = new HashSet<string>();
            foreach (var location in Locations)
            {
                if (location == null || string.IsNullOrWhiteSpace(location.Id))
                    Debug.LogWarning($"[ProCommVR] Scenario '{name}' has a place to talk without an id.", this);
                else if (!ids.Add(location.Id))
                    Debug.LogWarning($"[ProCommVR] Scenario '{name}' has two places with the id '{location.Id}'.", this);
            }
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
