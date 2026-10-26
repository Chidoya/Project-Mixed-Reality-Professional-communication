using System;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// One answer the player can choose in a scenario, with its (hidden) score, the NPC's reaction
    /// right after the choice and the feedback shown on the results screen. Optional: the mood of the room after
    /// the choice, and the reaction of a bystander (a second character in the scene).
    /// </summary>
    [Serializable]
    public class DialogueOption
    {
        [Tooltip("What the player says.")]
        [SerializeField, TextArea(2, 4)] private string _text;

        [Tooltip("Hidden score for this choice. Higher is better. See ARCHITECTURE.md for the scale.")]
        [SerializeField] private int _score;

        [Tooltip("Shown on the results screen: why this answer was good or less good.")]
        [SerializeField, TextArea(3, 8)] private string _feedback;

        [Header("NPC reaction (right after the choice)")]
        [Tooltip("Short emotional line the NPC says. Leave empty to keep the NPC's opening line.")]
        [SerializeField, TextArea(1, 3)] private string _npcReaction;

        [Tooltip("Body language of the NPC after this choice.")]
        [SerializeField] private NpcPose _npcPose;

        [Tooltip("Optional voice clip for the reaction.")]
        [SerializeField] private AudioClip _npcVoice;

        [Header("Mood after the choice (optional)")]
        [Tooltip("Change the mood of the room after this choice, e.g. to Happy when the problem is resolved.")]
        [SerializeField] private bool _changesMood;

        [Tooltip("The mood after this choice (only with Changes Mood).")]
        [SerializeField] private Mood _moodAfter = Mood.Neutral;

        [Header("Bystander reaction (optional, scenarios with a bystander)")]
        [Tooltip("Short line the bystander (a second character, see the scenario) says after this choice. Empty: the bystander says nothing new.")]
        [SerializeField, TextArea(1, 3)] private string _bystanderReaction;

        [Tooltip("Body language of the bystander after this choice.")]
        [SerializeField] private NpcPose _bystanderPose;

        public string Text => _text;
        public int Score => _score;
        public string Feedback => _feedback;
        public string NpcReaction => _npcReaction;
        public NpcPose NpcPose => _npcPose;
        public AudioClip NpcVoice => _npcVoice;
        public bool ChangesMood => _changesMood;
        public Mood MoodAfter => _moodAfter;
        public string BystanderReaction => _bystanderReaction;
        public NpcPose BystanderPose => _bystanderPose;
    }
}
