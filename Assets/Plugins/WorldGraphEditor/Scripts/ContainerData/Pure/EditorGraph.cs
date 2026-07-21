#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    public sealed class EditorGraph : GraphEngine<SceneNodeData, PortData, EdgeData, EditorTransitionData>
    {
        private Dictionary<string, SceneNodeData> _sceneAssetGuidToNodeData;

        private EditorGraphSnapshot _snapshot;

        private readonly Func<EditorGraphSnapshot> _snapshotFactory;

        internal EditorGraph(Func<EditorGraphSnapshot> snapshotFactory)
        {
            _snapshotFactory = snapshotFactory;
            Rebuild();
        }

        internal void Rebuild()
        {
            _snapshot = _snapshotFactory();
            Initialize();
        }
        
        #region GraphEngine Overrides

        protected override void Initialize()
        {
            base.Initialize();
            FillGuidToScenesDictionary();
        }

        public override IReadOnlyList<SceneNodeData> GetScenesData()
        {
            return _snapshot.SceneNodesData;
        }

        public override IReadOnlyList<EdgeData> GetEdgesData()
        {
            return _snapshot.EdgesData;
        }

        protected override EditorTransitionData CreatePassageTransitionData(string portGuid)
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

            if (!TryGetPortData(portGuid, out var portData))
            {
                WGEConsole.Error($"Failed to get port data, port guid: {portGuid}");
                return default;
            }

#if WGE_ADDRESSABLES
            return new EditorTransitionData(portData.Name, targetScene.ScenePath, portGuid, targetPortGuid, targetScene.BuildIndex, targetScene.GetAddress());
#else
            return new EditorTransitionData(portData.Name, targetScene.ScenePath, portGuid, targetPortGuid, targetScene.BuildIndex);
#endif
        }

        protected override EditorTransitionData CreateTeleportTransitionData(string currentPortGuid, string targetPortGuid)
        {
            if (!TryGetSceneDataByPortGuid(targetPortGuid, out var targetScene))
            {
                WGEConsole.Error($"Failed to get scene data, port guid: {targetPortGuid}");
                return default;
            }
            
            if (!TryGetPortData(currentPortGuid, out var portData))
            {
                WGEConsole.Error($"Failed to get port data, port guid: {currentPortGuid}");
                return default;
            }
            
#if WGE_ADDRESSABLES
            return new EditorTransitionData(portData.Name, targetScene.ScenePath, currentPortGuid, targetPortGuid, targetScene.BuildIndex, targetScene.GetAddress());
#else
            return new EditorTransitionData(portData.Name, targetScene.ScenePath, currentPortGuid, targetPortGuid, targetScene.BuildIndex);
#endif
        }
        
        #endregion
        
        /// <summary>
        /// Builds dropdown entries for every port in the editor graph.
        /// </summary>
        /// <returns>Tuples ordered as <see cref="SceneNodeData.NodeName"/> / port display name versus port GUID.</returns>
        public IEnumerable<(string Guid, string Path)> GetAllPortsDropdownData()
        {
            return from nodeData in _snapshot.SceneNodesData
                from portData in nodeData.PortsData
                let path = $"{nodeData.NodeName}/{portData.Name}"
                select (portData.Guid, path);
        }
        
        /// <summary>
        /// Ports on a single scene for <see cref="PortsDropdown"/> style pickers.
        /// </summary>
        /// <param name="scenePath">Unity project path to the target scene asset.</param>
        /// <returns>Port GUID/name pairs when the scene exists; otherwise <see langword="null"/>.</returns>
        public IEnumerable<(string Guid, string Name)> GetPortsDropdownData(string scenePath)
        {
            if (!TryGetSceneDataByPath(scenePath, out var data) || data.PortsData == null || data.PortsData.Length == 0)
                return null;
            
            return data.PortsData.Select(item => (item.Guid, item.Name));
        }
        
        public bool TryGetSceneDataBySceneAssetGuid(string sceneAssetGuid, out SceneNodeData data)
        {
            data = default;

            if (_sceneAssetGuidToNodeData == null)
                return false;

            if (!_sceneAssetGuidToNodeData.TryGetValue(sceneAssetGuid, out data))
                return false;

            return true;
        }

        public bool TryGetSceneDataByPath(string path, out SceneNodeData data)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            return TryGetSceneDataBySceneAssetGuid(guid, out data);
        }

        private void FillGuidToScenesDictionary()
        {
            _sceneAssetGuidToNodeData = new Dictionary<string, SceneNodeData>();

            try
            {
                foreach (var sceneNodeData in GetScenesData())
                {
                    var guid = sceneNodeData.SceneAssetGuid == ""
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneNodeData.SceneAsset))
                        : sceneNodeData.SceneAssetGuid;

                    _sceneAssetGuidToNodeData.Add(guid, sceneNodeData);
                }
            }
            catch (Exception e)
            {
                WGEConsole.Error($"Trying to add Scene GUID to the dictionary, but it already exists. {e.Message}");
            }
        }
    }
}

#endif