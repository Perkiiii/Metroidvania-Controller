using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGraphEditor
{
    public abstract class GraphEngine<TSceneData, TPortData, TConnectionData, TTransitionData> 
        where TSceneData : ISceneData<TPortData>
        where TConnectionData : IConnectionData
        where TTransitionData : ITransitionData
        where TPortData : IPortData
    {
        private Dictionary<string, TTransitionData> _guidToTransitionDictionary;
        private Dictionary<string, TConnectionData> _guidToConnectionDictionary;
        private Dictionary<int, TSceneData> _buildIndexToSceneDictionary;
        private Dictionary<string, TSceneData> _portGuidToSceneDictionary;
        
#if WGE_ADDRESSABLES
        private Dictionary<string, TSceneData> _addressToSceneDictionary;
#endif
        
        protected IReadOnlyDictionary<string, TConnectionData> GuidToConnectionDictionary => _guidToConnectionDictionary;
        protected IReadOnlyDictionary<string, TTransitionData> GuidToTransitionDictionary => _guidToTransitionDictionary;
        
        public abstract IReadOnlyList<TSceneData> GetScenesData();
        public abstract IReadOnlyList<TConnectionData> GetEdgesData();
        protected abstract TTransitionData CreatePassageTransitionData(string portGuid);
        protected abstract TTransitionData CreateTeleportTransitionData(string currentPortGuid, string targetPortGuid);
        
        protected virtual void Initialize()
        {
            FillPortToSceneDataDictionary();
            FillEdgesDictionary();
            FillTransitionsDictionary();
            FillScenesDictionary();

#if WGE_ADDRESSABLES
            FillAddressToSceneDictionary();
#endif
        }
        
        public bool TryGetPassageTransitionData(string currentPortGuid, out TTransitionData data)
        {
            if (string.IsNullOrEmpty(currentPortGuid))
            {
                data = default;
                return false;
            }

            return _guidToTransitionDictionary.TryGetValue(currentPortGuid, out data);
        }
        
        public TTransitionData GetTeleportTransitionData(string currentPortGuid, string targetPortGuid)
        {
            return CreateTeleportTransitionData(currentPortGuid, targetPortGuid);
        }

        public bool TryGetPortData(string guid, out TPortData data)
        {
            data = default;
            
            if (!TryGetSceneDataByPortGuid(guid, out var sceneData))
                return false;
            
            if (sceneData.GetPortsData() != null)
            {
                data = sceneData.GetPortsData().FirstOrDefault(port => port.GetGuid() == guid);
                return true;
            }

            return false;
        }
        
        public List<TSceneData> GetNeighboursData(IEnumerable<string> passageGuids, bool ignoreShortcuts)
        {
            var neighbors = new List<TSceneData>();

            foreach (var portGuid in passageGuids)
            {
                if (!CanPassTransition(portGuid, ignoreShortcuts))
                    continue;

                if (!TryGetOppositePassageGuid(portGuid, out var oppositePortGuid))
                    continue;
                
                if (!TryGetSceneDataByPortGuid(oppositePortGuid, out var neighbourScene))
                    continue;
                
                neighbors.Add(neighbourScene);
            }

            return neighbors;
        }
        
        public List<TSceneData> GetNeighboursData(TSceneData sceneData, bool ignoreShortcuts)
        {
            return GetNeighboursData(sceneData.PortGuids, ignoreShortcuts);
        }
        
        public bool CanPassTransition(string guid, bool ignoreShortcuts) => CanPassTransition(guid, ignoreShortcuts, out _);
        
        public bool CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var edgeData))
            {
                status = _portGuidToSceneDictionary.ContainsKey(guid)
                    ? TransitionPassStatusType.BlockedByAdditionalPort
                    : TransitionPassStatusType.BlockedByPortNotFound;

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

        public bool TryGetOppositePassageGuid(string guid, out string opposite)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var edgeData) || string.IsNullOrEmpty(guid))
            {
                opposite = string.Empty;
                return false;
            }

            opposite = edgeData.GetFromPortGuid() == guid ? edgeData.GetToPortGuid() : edgeData.GetFromPortGuid();
            return true;
        }

        public bool TryGetSceneDataByPortGuid(string guid, out TSceneData data)
        {
            if (!string.IsNullOrEmpty(guid) && _portGuidToSceneDictionary != null)
                return _portGuidToSceneDictionary.TryGetValue(guid, out data);
            
            data = default;
            return false;
        }

#if WGE_ADDRESSABLES
        public bool TryGetSceneDataByAddress(string address, out TSceneData data)
        {
            if (!string.IsNullOrEmpty(address) && _addressToSceneDictionary != null)
                return _addressToSceneDictionary.TryGetValue(address, out data);

            data = default;
            return false;
        }
#endif

        public bool TryGetSceneDataByBuildIndex(int buildIndex, out TSceneData data)
        {
            return _buildIndexToSceneDictionary.TryGetValue(buildIndex, out data);
        }

        public bool IsShortcutOrigin(string guid) => IsConnectionOrigin(TransitionType.Shortcut, guid);

        public bool IsShortcutDestination(string guid) => IsConnectionDestination(TransitionType.Shortcut, guid);

        public bool IsOneWayOrigin(string guid) => IsConnectionOrigin(TransitionType.OneWay, guid);

        public bool IsOneWayDestination(string guid) => IsConnectionDestination(TransitionType.OneWay, guid);

        private bool IsConnectionOrigin(TransitionType originType, string guid)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var connection))
                return false;

            if (connection.GetTransitionType() != originType) 
                return false;
            
            var isFacingRight = connection.GetIsFacingRight();

            if (isFacingRight)
                return connection.GetToPortGuid() == guid;

            return connection.GetFromPortGuid() == guid;
        }

        private bool IsConnectionDestination(TransitionType originType, string guid)
        {
            if (!_guidToConnectionDictionary.TryGetValue(guid, out var connection))
                return false;

            if (connection.GetTransitionType() != originType) 
                return false;
            
            var isFacingRight = connection.GetIsFacingRight();

            if (isFacingRight)
                return connection.GetFromPortGuid() == guid;

            return connection.GetToPortGuid() == guid;
        }

        #region Fill Dictionaries

        private void FillScenesDictionary()
        {
            _buildIndexToSceneDictionary = new Dictionary<int, TSceneData>();

            try
            {
                foreach (var sceneNodeData in GetScenesData())
                {
                    var buildIndex = sceneNodeData.GetBuildIndex();

                    if (buildIndex < 0)
                        continue;

                    _buildIndexToSceneDictionary.Add(buildIndex, sceneNodeData);
                }
            }
            catch (Exception e)
            {
                WGEConsole.Error($"Trying to add Scene Build Index to the dictionary, but it already exists. {e.Message}");
            }
        }

        private void FillEdgesDictionary()
        {
            _guidToConnectionDictionary = new Dictionary<string, TConnectionData>();

            foreach (var edgeData in GetEdgesData())
            {
                _guidToConnectionDictionary.Add(edgeData.GetFromPortGuid(), edgeData);
                _guidToConnectionDictionary.Add(edgeData.GetToPortGuid(), edgeData);
            }
        }

        private void FillPortToSceneDataDictionary()
        {
            _portGuidToSceneDictionary = new Dictionary<string, TSceneData>();

            foreach (var sceneNodeData in GetScenesData())
            {
                foreach (var portData in sceneNodeData.GetPortsData())
                {
                    _portGuidToSceneDictionary.Add(portData.GetGuid(), sceneNodeData);
                }
            }
        }

        private void FillTransitionsDictionary()
        {
            _guidToTransitionDictionary = new Dictionary<string, TTransitionData>();

            foreach (var sceneData in GetScenesData())
            {
                foreach (var portData in sceneData.GetPortsData())
                {
                    if (!portData.GetIsAdditional())
                        _guidToTransitionDictionary.Add(portData.GetGuid(), CreatePassageTransitionData(portData.GetGuid()));
                }
            }
        }

#if WGE_ADDRESSABLES
        private void FillAddressToSceneDictionary()
        {
            _addressToSceneDictionary = new Dictionary<string, TSceneData>();

            foreach (var sceneData in GetScenesData())
            {
                if (!string.IsNullOrEmpty(sceneData.GetAddress()))
                    _addressToSceneDictionary.Add(sceneData.GetAddress(), sceneData);
            }
        }
#endif
        
        #endregion

        #region Obsolete
        
        [Obsolete("Use GetPassageTransitionData(string) for GoFrom and GetTeleportTransitionData(string, string) for GoTo.")]
        internal TTransitionData GetTransitionData(string guid, bool isTargetPassage)
        {
            if (string.IsNullOrEmpty(guid))
                return default;

            var resultGuid = isTargetPassage ? _guidToTransitionDictionary[guid].GetTargetPassageGuid() : guid;
            return _guidToTransitionDictionary[resultGuid];
        }
        
        #endregion
    }
}
