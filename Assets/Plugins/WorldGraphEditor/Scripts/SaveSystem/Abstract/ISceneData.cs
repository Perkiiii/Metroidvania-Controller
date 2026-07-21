using System.Collections.Generic;
using System.Linq;

namespace WorldGraphEditor
{
    public interface ISceneData<out TPortData> where TPortData : IPortData
    {
        public int GetBuildIndex();
        public IEnumerable<TPortData> GetPortsData();
        public IEnumerable<string> PortGuids => GetPortsData()?.Select(item => item.GetGuid());

#if WGE_ADDRESSABLES
        public string GetAddress();
#endif
    }
}
