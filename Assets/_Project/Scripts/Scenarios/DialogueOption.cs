using System;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// One answer the player can choose in a scenario, with its (hidden) score and the feedback
    /// shown after choosing it.
    /// </summary>
    [Serializable]
    public class DialogueOption
    {
        [Tooltip("What the player says.")]
        [SerializeField, TextArea(2, 4)] private string _text;

        [Tooltip("Hidden score for this choice. Higher is better. See ARCHITECTURE.md for the scale.")]
        [SerializeField] private int _score;

        [Tooltip("Shown after the choice: why this answer was good or less good.")]
        [SerializeField, TextArea(3, 8)] private string _feedback;

        public string Text => _text;
        public int Score => _score;
        public string Feedback => _feedback;
    }
}
