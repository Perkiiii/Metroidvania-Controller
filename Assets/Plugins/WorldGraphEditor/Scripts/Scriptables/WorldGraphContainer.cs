using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor
{
    [CreateAssetMenu(fileName = "New World Graph Container", menuName = "World Graph Editor/World Graph Container", order = -1)]
    public partial class WorldGraphContainer : GraphContainerBase<SceneRuntimeData, ConnectionRuntimeData, RuntimeTransitionData>
    {
        [SerializeField] private SceneRuntimeData[] _scenesData;
        [SerializeField] private ConnectionRuntimeData[] _connectionsData;

        public IReadOnlyList<SceneRuntimeData> ScenesData => _scenesData;
        public IReadOnlyList<ConnectionRuntimeData> ConnectionsData => _connectionsData;
        
        protected override IEnumerable<SceneRuntimeData> GetScenesData() => _scenesData;

        protected override IEnumerable<ConnectionRuntimeData> GetEdgesData() => _connectionsData;

        protected override RuntimeTransitionData CreateNewTransitionData(string portGuid)
        {
            var oppositePassageGuid = GetOppositePassageGuid(portGuid);
            var oppositeScene = GetSceneDataByPortGuid(oppositePassageGuid);

            return new RuntimeTransitionData(portGuid, oppositePassageGuid, oppositeScene.BuildIndex);
        }

        public override void Initialize()
        {
            base.Initialize();
            
#if UNITY_EDITOR
            EditorData?.Initialize();
#endif
        }
    }
}