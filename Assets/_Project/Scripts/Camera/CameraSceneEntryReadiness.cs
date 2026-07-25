public readonly struct CameraSceneEntryReadiness
{
    public CameraSceneEntryReadiness(string sceneName, bool isReady, bool usedFallback, string detail)
    {
        SceneName = sceneName ?? "";
        IsReady = isReady;
        UsedFallback = usedFallback;
        Detail = detail ?? "";
    }

    public string SceneName { get; }
    public bool IsReady { get; }
    public bool UsedFallback { get; }
    public string Detail { get; }
}
