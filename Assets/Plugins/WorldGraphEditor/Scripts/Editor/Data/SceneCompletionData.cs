using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGraphEditor.Editor
{
    [Serializable]
    internal struct SceneCompletionData
    {
        public string SceneName;
        public string NodeName;
        public PortData[] PortsAtNode;
        public ITransitionComponent[] PortsAtScene;
        public ITransitionComponent[] Duplicates;
        public PortData[] Missing;

        public string Ratio => $"{PortsAtScene.Length}/{PortsAtNode.Length}";

        public SceneCompletionData(IEnumerable<PortData> portsAtNode,
            IEnumerable<ITransitionComponent> portsAtScene,
            IEnumerable<ITransitionComponent> duplicates, 
            IEnumerable<PortData> missing, 
            string sceneName,
            string nodeName)
        {
            PortsAtNode = portsAtNode.ToArray();
            PortsAtScene = portsAtScene.ToArray();
            Duplicates = duplicates.ToArray();
            Missing = missing.ToArray();
            SceneName = sceneName;
            NodeName = nodeName;
        }
    }
}