using System;

[Serializable]
public readonly struct SceneTransitionRequest
{
    public readonly string TargetScene;
    public readonly string DestinationPassageGuid;
    public readonly FadeProfile FadeOverride;
    public readonly SceneTransitionKind Kind;
    public readonly string SourceDescription;

    public SceneTransitionRequest(
        string targetScene,
        string destinationPassageGuid = "",
        FadeProfile fadeOverride = null,
        SceneTransitionKind kind = SceneTransitionKind.Gate,
        string sourceDescription = "")
    {
        TargetScene = targetScene;
        DestinationPassageGuid = destinationPassageGuid ?? "";
        FadeOverride = fadeOverride;
        Kind = kind;
        SourceDescription = sourceDescription ?? "";
    }
}
