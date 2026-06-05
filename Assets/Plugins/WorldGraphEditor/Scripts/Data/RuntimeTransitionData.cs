namespace WorldGraphEditor
{
    public readonly struct RuntimeTransitionData : ITransitionData
    {
        public readonly int TargetSceneBuildIndex;
        public readonly string TargetPassageGuid;
        public readonly string CurrentPassageGuid;

        public RuntimeTransitionData(string currentGuid, string oppositeGuid, int targetBuildIndex)
        {
            CurrentPassageGuid = currentGuid;
            TargetPassageGuid = oppositeGuid;
            TargetSceneBuildIndex = targetBuildIndex;
        }

        public string GetTargetPassageGuid() => TargetPassageGuid;
        public string GetCurrentPassageGuid() => CurrentPassageGuid;
        public int GetTargetSceneBuildIndex() => TargetSceneBuildIndex;
    }
}