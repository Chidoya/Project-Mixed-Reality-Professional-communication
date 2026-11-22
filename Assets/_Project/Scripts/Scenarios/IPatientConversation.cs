using System;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A conversation with a patient, as the patient view sees it: is it open, is the player close enough, and when
    /// does a new one start. Implemented by <see cref="ScenarioPresenter"/> (the four choices) and by the AI
    /// conversation bridge (free speech through Liam's AI server), so the POV switch works with both.
    /// </summary>
    public interface IPatientConversation
    {
        /// <summary>True while the conversation is open.</summary>
        bool IsPlaying { get; }

        /// <summary>True when the player's head is within range (meters) of the patient.</summary>
        bool IsPlayerNear(float range);

        /// <summary>Hides the conversation's floating text (bubbles, subtitles) in the patient view.</summary>
        bool BubblesHidden { set; }

        /// <summary>A new conversation started (also after a restart).</summary>
        event Action ConversationStarted;
    }
}
