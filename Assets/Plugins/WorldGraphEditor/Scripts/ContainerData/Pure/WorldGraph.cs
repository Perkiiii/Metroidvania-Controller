using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor
{
    public sealed class WorldGraph : GraphEngine<SceneRuntimeData, PortRuntimeData, ConnectionRuntimeData, RuntimeTransitionData>, IWorldGraph
    {
        private WorldGraphSnapshot _snapshot;

#if UNITY_EDITOR
        private readonly Func<WorldGraphSnapshot> _snapshotFactory;

        internal WorldGraph(Func<WorldGraphSnapshot> snapshotFactory)
        {
            _snapshotFactory = snapshotFactory;
            Rebuild();
        }

        internal void Rebuild()
        {
            _snapshot = _snapshotFactory();
            Initialize();
        }
#endif
        public WorldGraph(WorldGraphSnapshot snapshot)
        {
            _snapshot = snapshot;
            Initialize();
        }
        
        public override IReadOnlyList<SceneRuntimeData> GetScenesData() => _snapshot.SceneRuntimeData;
        public override IReadOnlyList<ConnectionRuntimeData> GetEdgesData() => _snapshot.ConnectionRuntimeData;

        protected override RuntimeTransitionData CreatePassageTransitionData(string portGuid)
        {
            if (!TryGetOppositePassageGuid(portGuid, out var targetPortGuid))
            {
                WGEConsole.Error($"Failed to get opposite passage guid, port guid: {portGuid}");
                return default;
            }

            if (!TryGetSceneDataByPortGuid(targetPortGuid, out var targetScene))
            {
                WGEConsole.Error($"Failed to get scene data, port guid: {targetPortGuid}");
                return default;
            }
            
#if WGE_ADDRESSABLES
            return new RuntimeTransitionData(portGuid, targetPortGuid, targetScene.BuildIndex, targetScene.GetAddress());
#else
            return new RuntimeTransitionData(portGuid, targetPortGuid, targetScene.BuildIndex);
#endif
        }

        protected override RuntimeTransitionData CreateTeleportTransitionData(string currentPortGuid, string targetPortGuid)
        {
            if (!TryGetSceneDataByPortGuid(targetPortGuid, out var targetScene))
            {
                WGEConsole.Error($"Failed to get scene data, port guid: {targetPortGuid}");
                return default;
            }
            
#if WGE_ADDRESSABLES
            return new RuntimeTransitionData(currentPortGuid, targetPortGuid, targetScene.BuildIndex, targetScene.GetAddress());
#else
            return new RuntimeTransitionData(currentPortGuid, targetPortGuid, targetScene.BuildIndex);
#endif
        }
    } 
}
