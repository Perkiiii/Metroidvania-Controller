namespace WorldGraphEditor
{
    public interface IConnectionData
    {
        public TransitionType GetTransitionType();
        public bool GetIsFacingRight();
        public string GetFromPortGuid();
        public string GetToPortGuid();
    }
}