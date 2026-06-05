#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    public class ContainerEditorData : GraphContainerBase<SceneNodeData, EdgeData, EditorTransitionData>
    {
        [SerializeField] private SceneNodeData[] _sceneNodeData;
        [SerializeField] private EdgeData[] _edgesData;
        [SerializeField] private SceneTestResult[] _testResults;
        
        public IReadOnlyList<SceneNodeData> SceneNodeData => _sceneNodeData;
        public IReadOnlyList<EdgeData> EdgesData => _edgesData;
        public IReadOnlyList<SceneTestResult> SceneTestResults => _testResults;

        private Dictionary<string, SceneNodeData> _sceneAssetGuidToNodeData;

        public override void Initialize()
        {
            base.Initialize();
            FillGuidToScenesDictionary();
        }
        
        public override bool IsInitialized() => base.IsInitialized() && _sceneAssetGuidToNodeData != null;

        protected override IEnumerable<SceneNodeData> GetScenesData() => _sceneNodeData;

        protected override IEnumerable<EdgeData> GetEdgesData() => _edgesData;

        protected override EditorTransitionData CreateNewTransitionData(string portGuid)
        {
            var oppositePassageGuid = GetOppositePassageGuid(portGuid);
            var oppositeScene = GetSceneDataByPortGuid(oppositePassageGuid);
            var portData = GetPortData(portGuid);

            return new EditorTransitionData(portData.Name, oppositeScene.ScenePath, portGuid, oppositePassageGuid,
                oppositeScene.BuildIndex);
        }

        public void SetNodeData(IEnumerable<SceneNodeData> data) => _sceneNodeData = data.ToArray();

        public void SetEdgesData(IEnumerable<EdgeData> edgesData) => _edgesData = edgesData.ToArray();

        public void SetTestsData(IEnumerable<SceneTestResult> testResults) => _testResults = testResults?.ToArray();

        /// <summary>
        /// Should be used to fill the <see cref="PortsDropdown"/>.
        /// </summary>
        /// <returns>Data about all ports in the container.</returns>
        public IEnumerable<(string Guid, string Path)> GetAllPortsDropdownData()
        {
            return from sceneNodeData in SceneNodeData
                from portData in sceneNodeData.PortsData
                let path = $"{sceneNodeData.NodeName}/{portData.Name}"
                select (portData.Guid, path);
        }

        /// <summary>
        /// Should be used to fill the <see cref="PortsDropdown"/>.
        /// </summary>
        /// <returns>Data about all ports on the scene.</returns>
        [Obsolete("Use GetPortsDropdownData(string) instead")]
        public IEnumerable<(string Guid, string Name)> GetPortsDropdownData(int buildIndex)
        {
            var data = GetTransitionsDataByBuildIndex(buildIndex, out var hasData);
            return hasData ? data.Select(static item => (item.CurrentPassageGuid, item.CurrentPassageName)) : null;
        }
        
        /// <summary>
        /// Should be used to fill the <see cref="PortsDropdown"/>.
        /// </summary>
        /// <returns>Data about all ports on the scene.</returns>
        public IEnumerable<(string Guid, string Name)> GetPortsDropdownData(string scenePath)
        {
            var data = GetTransitionsDataByPath(scenePath, out var hasData);
            return hasData ? data.Select(static item => (item.CurrentPassageGuid, item.CurrentPassageName)) : null;
        }

        public List<EditorTransitionData> GetTransitionsDataByPath(string scenePath, out bool hasData)
        {
            var sceneData = GetSceneDataByPath(scenePath, out hasData);
            return sceneData.PortsData?.Select(portData => GetTransitionData(portData.Guid, false)).ToList();
        }

        [Obsolete("Use GetTransitionsDataByPath(string, out bool) instead.")]
        public List<EditorTransitionData> GetTransitionsDataByBuildIndex(int buildIndex, out bool hasData)
        {
            var sceneData = GetSceneDataByBuildIndex(buildIndex, out hasData);
            return sceneData.PortsData?.Select(portData => GetTransitionData(portData.Guid, false)).ToList();
        }

        public PortData GetPortData(string guid)
        {
            var sceneData = GetSceneDataByPortGuid(guid);

            if (sceneData.PortsData != null)
                return sceneData.PortsData.FirstOrDefault(port => port.Guid == guid);

            return default;
        }

        public EdgeData GetEdgeData(string portGuid, out bool hasData)
        {
            hasData = true;

            if (GuidToConnectionDictionary.TryGetValue(portGuid, out var edgeData))
                return edgeData;

            hasData = false;
            return default;
        }

        public void RefreshNodeData(SceneNodeData newData)
        {
            var index = Array.IndexOf(_sceneNodeData, newData);
            _sceneNodeData[index] = newData;
        }
        
        public SceneNodeData GetSceneDataByPath(string path, out bool hasData)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            return GetSceneDataByGuid(guid, out hasData);
        }

        public SceneNodeData GetSceneDataByGuid(string sceneAssetGuid, out bool hasData)
        {
            hasData = false;

            if (_sceneAssetGuidToNodeData == null) 
                return default;
            
            if (!_sceneAssetGuidToNodeData.TryGetValue(sceneAssetGuid, out var value))
                return default;
                
            hasData = true;
            return value;
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