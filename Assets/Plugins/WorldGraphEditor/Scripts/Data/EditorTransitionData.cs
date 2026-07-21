#if UNITY_EDITOR

namespace WorldGraphEditor
{
    public readonly struct EditorTransitionData : ITransitionData
    {
        public readonly int TargetSceneBuildIndex;
        public readonly string TargetPassageGuid;
        public readonly string CurrentPassageGuid;
        public readonly string TargetScenePath;
        public readonly string CurrentPassageName;

#if WGE_ADDRESSABLES
        public readonly string TargetSceneAddress;
#endif

#if WGE_ADDRESSABLES
        public EditorTransitionData(string portName, string targetScenePath, string currentGuid, string targetGuid, int targetBuildIndex, string targetSceneAddress)
        {
            CurrentPassageName = portName;
            TargetScenePath = targetScenePath;
            CurrentPassageGuid = currentGuid;
            TargetPassageGuid = targetGuid;
            TargetSceneBuildIndex = targetBuildIndex;
            TargetSceneAddress = targetSceneAddress;
        }
#else
        public EditorTransitionData(string portName, string targetScenePath, string currentGuid, string targetGuid, int targetBuildIndex)
        {
            CurrentPassageName = portName;
            TargetScenePath = targetScenePath;
            CurrentPassageGuid = currentGuid;
            TargetPassageGuid = targetGuid;
            TargetSceneBuildIndex = targetBuildIndex;
        }
#endif
        
        public string GetTargetPassageGuid() => TargetPassageGuid;
        public string GetCurrentPassageGuid() => CurrentPassageGuid;
        public int GetTargetSceneBuildIndex() => TargetSceneBuildIndex;

#if WGE_ADDRESSABLES
        public string GetTargetSceneAddress() => TargetSceneAddress;
#endif
    }
}

#endif