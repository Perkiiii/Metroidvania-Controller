using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor
{
    public abstract class GraphContainerBase<TScene, TConnection, TTransitionData> : ScriptableObject 
        where TScene : ISceneData
        where TConnection : IConnectionData
        where TTransitionData : ITransitionData
    {
        private Dictionary<string, TTransitionData> _guidToTransitionDictionary;
        private Dictionary<string, TConnection> _guidToConnectionDictionary;
        private Dictionary<int, TScene> _buildIndexToSceneDictionary;
        private Dictionary<string, TScene> _portGuidToSceneDictionary;

        protected IReadOnlyDictionary<string, TConnection> GuidToConnectionDictionary => _guidToConnectionDictionary;
        protected IReadOnlyDictionary<string, TTransitionData> GuidToTransitionDictionary => _guidToTransitionDictionary;
        
        protected abstract IEnumerable<TScene> GetScenesData();
        protected abstract IEnumerable<TConnection> GetEdgesData();

        protected abstract TTransitionData CreateNewTransitionData(string portGuid);

        public virtual bool IsInitialized() => _guidToTransitionDictionary != null && _guidToConnectionDictionary != null && _buildIndexToSceneDictionary != null && _portGuidToSceneDictionary != null;
        
        public virtual void Initialize()
        {
            FillPortToSceneDataDictionary();
            FillEdgesDictionary();
            FillTransitionsDictionary();
            FillScenesDictionary();
        }

        public TTransitionData GetTransitionData(string guid, bool isTargetPassage)
        {
            if (guid == null)
                return default;
            
            var resultGuid = isTargetPassage ? _guidToTransitionDictionary[guid].GetTargetPassageGuid() : guid;
            return _guidToTransitionDictionary[resultGuid];
        }

        public List<TScene> GetNeighboursData(int buildIndex, bool ignoreShortcuts)
        {
            var sceneData = GetSceneDataByBuildIndex(buildIndex, out var hasData);

            if (!hasData)
                return new List<TScene>(); 
                    
            var neighbors = new List<TScene>();
            
            foreach (var portGuid in sceneData.GetPortsGuid())
            {
                if (!CanPassTransition(portGuid, ignoreShortcuts)) 
                    continue;
                
                var sceneNodeData = GetSceneDataByBuildIndex(_guidToTransitionDictionary[portGuid].GetTargetSceneBuildIndex(), out _);
                neighbors.Add(sceneNodeData);
            }

            return neighbors;
        }

        public bool CanPassTransition(string guid, bool ignoreShortcuts) => CanPassTransition(guid, ignoreShortcuts, out _);
        
        public bool CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var edgeData))
            {
                status = TransitionPassStatusType.BlockedByAdditionalPort;
                return false;
            }

            var isFromLeft = edgeData.GetFromPortGuid() == guid;
            
            var isDirectionValid = (isFromLeft || edgeData.GetIsFacingRight()) && (!isFromLeft || !edgeData.GetIsFacingRight());
            var isEdgeTypeValid = edgeData.GetTransitionType() != TransitionType.OneWay && (edgeData.GetTransitionType() != TransitionType.Shortcut || ignoreShortcuts);
            
            if (isDirectionValid || isEdgeTypeValid)
                status = TransitionPassStatusType.Allowed;
            else
                status = edgeData.GetTransitionType() == TransitionType.OneWay
                    ? TransitionPassStatusType.BlockedByDirection
                    : TransitionPassStatusType.BlockedByShortcut;
            
            return isDirectionValid || isEdgeTypeValid;
        }
        
        public string GetOppositePassageGuid(string guid)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var edgeData))
                return guid;
               
            return edgeData.GetFromPortGuid() == guid ? edgeData.GetToPortGuid() : edgeData.GetFromPortGuid();
        }
        
        public TScene GetSceneDataByPortGuid(string guid)
        {
            if (guid == null)
                return default;
            
            return _portGuidToSceneDictionary.GetValueOrDefault(guid);
        }

        public virtual TScene GetSceneDataByBuildIndex(int buildIndex, out bool hasData)
        {
            hasData = false;

            if (_buildIndexToSceneDictionary == null) 
                return default;
            
            if (!_buildIndexToSceneDictionary.TryGetValue(buildIndex, out var value))
                return default;
                
            hasData = true;
            return value;
        }
        
        public bool IsShortcutOutput(string guid)
        {
            if (_guidToConnectionDictionary[guid].GetTransitionType() == TransitionType.Shortcut)
            {
                return _guidToConnectionDictionary[guid].GetToPortGuid() == guid;
            }

            return false;
        }

        public bool IsShortcutInput(string guid)
        {
            if (_guidToConnectionDictionary[guid].GetTransitionType() == TransitionType.Shortcut)
            {
                return _guidToConnectionDictionary[guid].GetFromPortGuid() == guid;
            }

            return false;
        }
        
        #region Fill Dictionaries

        private void FillScenesDictionary()
        {
            _buildIndexToSceneDictionary = new Dictionary<int, TScene>();

            try
            {
                foreach (var sceneNodeData in GetScenesData())
                {
                    _buildIndexToSceneDictionary.Add(sceneNodeData.GetBuildIndex(), sceneNodeData);
                }
            }
            catch (Exception e)
            {
                WGEConsole.Error($"Trying to add Scene Build Index to the dictionary, but it already exists. {e.Message}");
            }
        }

        private void FillEdgesDictionary()
        {
            _guidToConnectionDictionary = new Dictionary<string, TConnection>();
            
            foreach (var edgeData in GetEdgesData())
            {
                _guidToConnectionDictionary.Add(edgeData.GetFromPortGuid(), edgeData);
                _guidToConnectionDictionary.Add(edgeData.GetToPortGuid(), edgeData);
            }
        }
        
        private void FillPortToSceneDataDictionary()
        {
            _portGuidToSceneDictionary = new Dictionary<string, TScene>();
            
            foreach (var sceneNodeData in GetScenesData())
            {
                foreach (var portGuid in sceneNodeData.GetPortsGuid())
                {
                    _portGuidToSceneDictionary.Add(portGuid, sceneNodeData);
                }
            }
        }

        private void FillTransitionsDictionary()
        {
            _guidToTransitionDictionary = new Dictionary<string, TTransitionData>();

            foreach (var sceneData in GetScenesData())
            {
                foreach (var portGuid in sceneData.GetPortsGuid())
                {
                    _guidToTransitionDictionary.Add(portGuid, CreateNewTransitionData(portGuid));
                }
            }
        }
        #endregion
    }
}