namespace WorldGraphEditor
{
    public readonly struct WorldGraphSnapshot
    {
        public readonly SceneRuntimeData[] SceneRuntimeData;
        public readonly ConnectionRuntimeData[] ConnectionRuntimeData;

        public WorldGraphSnapshot(SceneRuntimeData[] sceneRuntimeData, ConnectionRuntimeData[] connectionRuntimeData)
        {
            SceneRuntimeData = sceneRuntimeData;
            ConnectionRuntimeData= connectionRuntimeData;
        }
    }
}