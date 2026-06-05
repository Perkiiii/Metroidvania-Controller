#if UNITY_EDITOR
namespace WorldGraphEditor
{
    public struct TestContext
    {
        public SceneNodeData SceneNodeData;

        public TestContext(SceneNodeData sceneNodeData)
        {
            SceneNodeData = sceneNodeData;
        }
    }
}
#endif