using WorldGraphEditor;

public readonly struct WorldGraphTransitionRequest
{
    public readonly bool IsValid;
    public readonly string SourcePortGuid;
    public readonly string TargetPortGuid;
    public readonly int TargetSceneBuildIndex;
    public readonly string TargetSceneName;
    public readonly TransitionPassStatusType PassStatus;
    public readonly string FailureReason;

    public WorldGraphTransitionRequest(
        string sourcePortGuid,
        string targetPortGuid,
        int targetSceneBuildIndex,
        string targetSceneName,
        TransitionPassStatusType passStatus)
    {
        IsValid = true;
        SourcePortGuid = sourcePortGuid;
        TargetPortGuid = targetPortGuid;
        TargetSceneBuildIndex = targetSceneBuildIndex;
        TargetSceneName = targetSceneName;
        PassStatus = passStatus;
        FailureReason = null;
    }

    public WorldGraphTransitionRequest(string sourcePortGuid, string failureReason)
    {
        IsValid = false;
        SourcePortGuid = sourcePortGuid;
        TargetPortGuid = null;
        TargetSceneBuildIndex = -1;
        TargetSceneName = null;
        PassStatus = TransitionPassStatusType.BlockedByAdditionalPort;
        FailureReason = failureReason;
    }
}
