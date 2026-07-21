using System;
using System.Collections.Generic;

namespace WorldGraphEditor
{
    public sealed partial class WorldGraphContainer
    {
        [Obsolete("Use bool WorldGraph.TryGetPassageTransitionData(string, out RuntimeTransitionData) instead.")]
        public bool TryGetPassageTransitionData(string currentPortGuid, out RuntimeTransitionData data)
        {
            return _worldGraph.TryGetPassageTransitionData(currentPortGuid, out data);
        }
        
        [Obsolete("Use WorldGraph.GetTeleportTransitionData(string, string) instead.")]
        public RuntimeTransitionData GetTeleportTransitionData(string currentPortGuid, string targetPortGuid)
        {
            return _worldGraph.GetTeleportTransitionData(currentPortGuid, targetPortGuid);
        }
        
        [Obsolete("Use bool WorldGraph.TryGetPortData(string, out PortRuntimeData) instead.")]
        public PortRuntimeData GetPortData(string guid)
        {
            if (_worldGraph.TryGetPortData(guid, out var data))
                return data;

            return default;
        }
        
        [Obsolete("Use WorldGraph.GetPassageTransitionData(string) for GoFromAsync or WorldGraph.GetTeleportTransitionData(string, string) for GoToAsync instead.")]
        public RuntimeTransitionData GetTransitionData(string guid, bool isTargetPassage)
        {
            return _worldGraph.GetTransitionData(guid, isTargetPassage);
        }
        
        [Obsolete("Use WorldGraph.GetNeighboursData(IEnumerable<string>, bool) instead.")]
        public List<SceneRuntimeData> GetNeighboursData(IEnumerable<string> passageGuids, bool ignoreShortcuts)
        {
            return _worldGraph.GetNeighboursData(passageGuids, ignoreShortcuts);
        }
        
        [Obsolete("Use WorldGraph.GetNeighboursData(SceneRuntimeData, bool) instead.")]
        public List<SceneRuntimeData> GetNeighboursData(SceneRuntimeData sceneData, bool ignoreShortcuts)
        {
            return _worldGraph.GetNeighboursData(sceneData, ignoreShortcuts);
        }

        [Obsolete("Use WorldGraph.GetNeighboursData(SceneRuntimeData, bool) or WorldGraph.GetNeighboursData(IEnumerable<string>, bool) instead.")]
        public List<SceneRuntimeData> GetNeighboursData(int buildIndex, bool ignoreShortcuts)
        {
            if (!_worldGraph.TryGetSceneDataByBuildIndex(buildIndex, out var data))
                return null;
            
            return _worldGraph.GetNeighboursData(data, ignoreShortcuts);
        }
        
        [Obsolete("Use bool WorldGraph.CanPassTransition(string, bool) instead.")]
        public bool CanPassTransition(string guid, bool ignoreShortcuts)
        {
            return _worldGraph.CanPassTransition(guid, ignoreShortcuts);
        }

        [Obsolete("Use bool WorldGraph.CanPassTransition(string, bool, out TransitionPassStatusType) instead.")]
        public bool CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status)
        {
            return _worldGraph.CanPassTransition(guid, ignoreShortcuts, out status);
        }
        
        [Obsolete("Use bool WorldGraph.TryGetOppositePassageGuid(string, bool, out TransitionPassStatusType) instead.")]
        public string GetOppositePassageGuid(string guid)
        {
            if (_worldGraph.TryGetOppositePassageGuid(guid, out var opposite))
                return opposite;

            return string.Empty;
        }

        [Obsolete("Use bool WorldGraph.TryGetOppositePassageGuid(string, out string) instead.")]
        public bool TryGetOppositePassageGuid(string guid, out string opposite)
        {
            return _worldGraph.TryGetOppositePassageGuid(guid, out opposite);
        }
        
        [Obsolete("Use bool WorldGraph.TryGetSceneDataByPortGuid(string, out SceneRuntimeData) instead.")]
        public SceneRuntimeData GetSceneDataByPortGuid(string guid)
        {
            if (_worldGraph.TryGetSceneDataByPortGuid(guid, out var data))
                return data;

            return default;
        }

        [Obsolete("Use bool WorldGraph.TryGetSceneDataByPortGuid(string, out SceneRuntimeData) instead.")]
        public bool TryGetSceneDataByPortGuid(string guid, out SceneRuntimeData data)
        {
            return _worldGraph.TryGetSceneDataByPortGuid(guid, out data);
        }

        [Obsolete("Use bool WorldGraph.TryGetSceneDataByBuildIndex(int, out SceneRuntimeData) instead.")]
        public SceneRuntimeData GetSceneDataByBuildIndex(int buildIndex, out bool hasData)
        {
            hasData = _worldGraph.TryGetSceneDataByBuildIndex(buildIndex, out var data);
            return data;
        }
        
        [Obsolete("Use bool WorldGraph.IsShortcutDestination(string) instead.")]
        public bool IsShortcutOutput(string guid)
        {
            return _worldGraph.IsShortcutDestination(guid);
        }
        
        [Obsolete("Use bool WorldGraph.IsShortcutOrigin(string) instead.")]
        public bool IsShortcutInput(string guid)
        {
            return _worldGraph.IsShortcutOrigin(guid);
        }
    }
}