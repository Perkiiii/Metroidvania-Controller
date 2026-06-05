namespace WorldGraphEditor
{
    public interface ITransitionData
    {
        public string GetTargetPassageGuid();
        public string GetCurrentPassageGuid();
        public int GetTargetSceneBuildIndex();
    }
}