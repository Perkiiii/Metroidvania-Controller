namespace WorldGraphEditor
{
    public readonly struct RuntimeTransitionData : ITransitionData
    {
        public readonly int TargetSceneBuildIndex;
        public readonly string TargetPassageGuid;
        public readonly string CurrentPassageGuid;

#if WGE_ADDRESSABLES
        public readonly string TargetSceneAddress;
#endif

        public RuntimeTransitionData(string currentGuid, string oppositeGuid, int targetBuildIndex)
        {
            CurrentPassageGuid = currentGuid;
            TargetPassageGuid = oppositeGuid;
            TargetSceneBuildIndex = targetBuildIndex;
#if WGE_ADDRESSABLES
            TargetSceneAddress = null;
#endif
        }

#if WGE_ADDRESSABLES
        public RuntimeTransitionData(string currentGuid, string oppositeGuid, int targetBuildIndex, string targetSceneAddress)
        {
            CurrentPassageGuid = currentGuid;
            TargetPassageGuid = oppositeGuid;
            TargetSceneBuildIndex = targetBuildIndex;
            TargetSceneAddress = targetSceneAddress;
        }
#endif
        public string GetTargetPassageGuid() => TargetPassageGuid;
        public string GetCurrentPassageGuid() => CurrentPassageGuid;
        public int GetTargetSceneBuildIndex() => TargetSceneBuildIndex;
        
        public override string ToString()
        { 
             return $"Current: {CurrentPassageGuid}, Target {TargetPassageGuid}";
        }

#if WGE_ADDRESSABLES
        public string GetTargetSceneAddress() => TargetSceneAddress;
#endif
    }
}