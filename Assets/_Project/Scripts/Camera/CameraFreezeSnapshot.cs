public readonly struct CameraFreezeSnapshot
{
    public CameraFreezeSnapshot(CameraFreezeKind kind, string sourceLabel, float remainingSeconds)
    {
        Kind = kind;
        SourceLabel = sourceLabel;
        RemainingSeconds = remainingSeconds;
    }

    public CameraFreezeKind Kind { get; }
    public string SourceLabel { get; }
    public float RemainingSeconds { get; }
}
