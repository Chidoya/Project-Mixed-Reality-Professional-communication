namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Mood of a scenario or of a patient. A <see cref="ProCommVR.Atmosphere.MoodController"/> holds the current
    /// mood; its <see cref="ProCommVR.Atmosphere.MoodProfile"/> gives the color, light and music.
    /// Add new values at the end only, otherwise existing assets shift to another mood.
    /// </summary>
    public enum Mood
    {
        Neutral,
        Tense,
        Somber,
        Happy,

        /// <summary>
        /// A task failed, e.g. the time ran out: red flashing light and a warning sound.
        /// </summary>
        Alarm
    }
}
