namespace ProCommVR.Shift
{
    /// <summary>
    /// How a task of the shift ended. Completed: the player answered and clicked "Finish" (the score of the choice
    /// says whether that went well or not). FailedTimeUp: the task's own countdown ran out before the player answered.
    /// Add new values at the end only.
    /// </summary>
    public enum TaskOutcome
    {
        None,
        Completed,
        FailedTimeUp
    }
}
