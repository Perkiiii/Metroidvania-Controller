public readonly struct HeroDashCompletion
{
    public HeroDashCompletion(
        int sequenceVersion,
        bool startedGrounded,
        int direction,
        HeroDashEndReason endReason)
    {
        SequenceVersion = sequenceVersion;
        StartedGrounded = startedGrounded;
        Direction = direction >= 0 ? 1 : -1;
        EndReason = endReason;
    }

    public int SequenceVersion { get; }
    public bool StartedGrounded { get; }
    public int Direction { get; }
    public HeroDashEndReason EndReason { get; }
    public bool CompletedNaturally => EndReason == HeroDashEndReason.Completed;
}
