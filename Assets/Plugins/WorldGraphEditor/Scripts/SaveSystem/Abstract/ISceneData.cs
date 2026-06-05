using System.Collections.Generic;

namespace WorldGraphEditor
{
    public interface ISceneData
    {
        public int GetBuildIndex();
        public IEnumerable<string> GetPortsGuid();
    }
}