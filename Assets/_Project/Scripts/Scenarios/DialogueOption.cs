using System;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// One answer the player can choose in a scenario, with its (hidden) score, the NPC's reaction
    /// right after the choice and the feedback shown on the results screen.
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

        public string Text => _text;
        public int Score => _score;
        public string Feedback => _feedback;
        public string NpcReaction => _npcReaction;
        public NpcPose NpcPose => _npcPose;
        public AudioClip NpcVoice => _npcVoice;
    }
}
