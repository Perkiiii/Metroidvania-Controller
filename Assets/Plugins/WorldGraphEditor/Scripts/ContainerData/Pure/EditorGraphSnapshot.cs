#if UNITY_EDITOR

namespace WorldGraphEditor
{
    public readonly struct EditorGraphSnapshot
    {
        public readonly SceneNodeData[] SceneNodesData;
        public readonly EdgeData[] EdgesData;

        public EditorGraphSnapshot(SceneNodeData[] sceneNodesData, EdgeData[] edgesData)
        {
            SceneNodesData = sceneNodesData;
            EdgesData= edgesData;
        }
    }
}

#endif