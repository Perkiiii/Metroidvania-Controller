using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct ConnectionRuntimeData : IConnectionData
    {
        public TransitionType TransitionType;
        public bool IsFacingRight;
        public string FromPortGuid;
        public string ToPortGuid;
        
        public TransitionType GetTransitionType() => TransitionType;
        public bool GetIsFacingRight() => IsFacingRight;
        public string GetFromPortGuid() => FromPortGuid;
        public string GetToPortGuid() => ToPortGuid;
    }
}