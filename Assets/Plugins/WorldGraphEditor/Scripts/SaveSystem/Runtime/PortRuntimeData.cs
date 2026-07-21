using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct PortRuntimeData : IPortData
    {
        public string Name;
        public string Guid;
        public bool IsAdditional;
        
        public string GetGuid() => Guid;
        public bool GetIsAdditional() => IsAdditional;
    }
}