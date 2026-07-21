using System;
using System.Collections.Generic;

namespace WorldGraphEditor
{
    [Serializable]
    public struct SceneRuntimeData : ISceneData<PortRuntimeData>
    {
        public string SceneName;
        public string NodeName;
        public int BuildIndex;
        public PortRuntimeData[] PortsData;

#if WGE_ADDRESSABLES
        public string Address;
        
        public string GetAddress() => Address;
#endif

        public int GetBuildIndex() => BuildIndex;
        public IEnumerable<PortRuntimeData> GetPortsData() => PortsData;
    }
}