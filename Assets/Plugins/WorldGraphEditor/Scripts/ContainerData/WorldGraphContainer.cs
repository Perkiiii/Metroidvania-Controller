using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor
{
    [CreateAssetMenu(fileName = "New World Graph Container", menuName = "World Graph Editor/World Graph Container", order = -1)]
    public sealed partial class WorldGraphContainer : ScriptableObject
    {
        [SerializeField] private SceneRuntimeData[] _scenesData;
        [SerializeField] private ConnectionRuntimeData[] _connectionsData;

        private WorldGraph _worldGraph;

        public IReadOnlyList<SceneRuntimeData> ScenesData => _scenesData;
        public IReadOnlyList<ConnectionRuntimeData> ConnectionsData => _connectionsData;

        public IEnumerable<SceneRuntimeData> GetScenesData() => _scenesData;
        public IEnumerable<ConnectionRuntimeData> GetEdgesData() => _connectionsData;

        public WorldGraph GetWorldGraph()
        {
#if UNITY_EDITOR
            _worldGraph ??= new WorldGraph(() => new WorldGraphSnapshot(_scenesData, _connectionsData));
#else
            _worldGraph ??= new WorldGraph(new WorldGraphSnapshot(_scenesData, _connectionsData));
#endif
            return _worldGraph;
        }
    }
}
