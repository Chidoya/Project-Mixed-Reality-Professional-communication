using System;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// One place where the player can choose to continue a conversation, e.g. "in the corridor" or "in the
    /// consultation room". Part of the <see cref="Scenario"/> data; the scene decides where that place is
    /// (<see cref="ConversationLocationMover"/>).
    /// </summary>
    [Serializable]
    public class ConversationLocation
    {
        [Tooltip("Short unique id within the scenario, e.g. \"consultation-room\". The scene finds the place by this id.")]
        [SerializeField] private string _id;

        [Tooltip("Name of the place, shown on the results screen, e.g. \"Consultation room\".")]
        [SerializeField] private string _name;

        [Tooltip("What the player says to choose this place.")]
        [SerializeField, TextArea(1, 3)] private string _choiceText;

        [Tooltip("Hidden score for this choice, added to the score of the option. 0 = the place does not count (yet).")]
        [SerializeField] private int _score;

        [Tooltip("Optional. Shown on the results screen: why this place was a good choice or not.")]
        [SerializeField, TextArea(2, 6)] private string _feedback;

        public string Id => _id;
        public string Name => _name;
        public string ChoiceText => _choiceText;
        public int Score => _score;
        public string Feedback => _feedback;
    }
}
