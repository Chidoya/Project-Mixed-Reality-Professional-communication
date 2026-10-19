namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Body language of the NPC after a choice. <see cref="NpcReactor"/> moves the NPC into the pose
    /// (and fires an Animator trigger with the same name once there is a real character).
    /// Add new values at the end, otherwise existing scenarios shift to another pose.
    /// </summary>
    public enum NpcPose
    {
        Unchanged,
        Engaged,
        TurnsAway,
        Withdrawn
    }
}
