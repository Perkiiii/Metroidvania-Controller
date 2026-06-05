using UnityEditor;

namespace WorldGraphEditor.Editor
{
    internal readonly struct NodeChangesData
    {
        public readonly SceneAsset SceneAsset;
        public readonly string NodeName;
        public readonly PortData[] PortsData;

        public NodeChangesData(string nodeName, SceneAsset sceneAsset, PortData[] portsData)
        {
            NodeName = nodeName;
            SceneAsset = sceneAsset;
            PortsData = portsData;
        }
    }
}