using System;
using System.Collections.Generic;

namespace WorldGraphEditor
{
    [Serializable]
    public struct SceneRuntimeData : ISceneData
    {
        public int BuildIndex;
        public string[] PortsGuid;
        
        public int GetBuildIndex() => BuildIndex;
        public IEnumerable<string> GetPortsGuid() => PortsGuid;
    }
}