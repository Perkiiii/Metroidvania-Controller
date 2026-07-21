using System.Collections.Generic;

namespace WorldGraphEditor
{
    public interface IWorldGraph
    {
        public IReadOnlyList<SceneRuntimeData> GetScenesData();
        public IReadOnlyList<ConnectionRuntimeData> GetEdgesData();
        
        public bool TryGetPortData(string guid, out PortRuntimeData data);
        public bool TryGetOppositePassageGuid(string guid, out string opposite);
        public bool TryGetSceneDataByPortGuid(string guid, out SceneRuntimeData data);
        public bool TryGetSceneDataByBuildIndex(int buildIndex, out SceneRuntimeData data);
#if WGE_ADDRESSABLES
        public bool TryGetSceneDataByAddress(string address, out SceneRuntimeData data);
#endif
        public bool TryGetPassageTransitionData(string currentPortGuid, out RuntimeTransitionData data);
        
        public RuntimeTransitionData GetTeleportTransitionData(string currentPortGuid, string targetPortGuid);
        public List<SceneRuntimeData> GetNeighboursData(IEnumerable<string> passageGuids, bool ignoreShortcuts);
        public List<SceneRuntimeData> GetNeighboursData(SceneRuntimeData sceneData, bool ignoreShortcuts);
        
        public bool CanPassTransition(string guid, bool ignoreShortcuts);
        public bool CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status);

        public bool IsShortcutOrigin(string guid);
        public bool IsShortcutDestination(string guid);
        public bool IsOneWayOrigin(string guid);
        public bool IsOneWayDestination(string guid);
    }
}