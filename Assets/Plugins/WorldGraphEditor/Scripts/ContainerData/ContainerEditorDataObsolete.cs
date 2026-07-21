#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace WorldGraphEditor
{
    public sealed partial class ContainerEditorData
    {
        // TODO: Used only for obsolete methods. Will be removed in next updates (1.3.1 or higher)
        private EditorGraph GetOrCreateGraph()
        {
            if (_editorGraph != null) 
                return _editorGraph;
            
            Initialize();

            return _editorGraph;
        }
        
        [Obsolete("Use EditorGraph.GetAllPortsDropdownData() instead.")]
        public IEnumerable<(string Guid, string Path)> GetAllPortsDropdownData()
        {
            return GetOrCreateGraph().GetAllPortsDropdownData();
        }

        [Obsolete("Use EditorGraph.GetPortsDropdownData(string) instead.")]
        public IEnumerable<(string Guid, string Name)> GetPortsDropdownData(string scenePath)
        {
            return GetOrCreateGraph().GetPortsDropdownData(scenePath);
        }
        
        [Obsolete("Use EditorGraph.TryGetSceneDataByPath(path, out SceneNodeData) instead.")]
        public bool TryGetSceneDataByPath(string path, out SceneNodeData data)
        {
            return GetOrCreateGraph().TryGetSceneDataByPath(path, out data);
        }
        
        [Obsolete("Use EditorGraph.TryGetSceneDataBySceneAssetGuid(sceneAssetGuid, out SceneNodeData) instead.")]
        public bool TryGetSceneDataBySceneAssetGuid(string sceneAssetGuid, out SceneNodeData data)
        {
            return GetOrCreateGraph().TryGetSceneDataBySceneAssetGuid(sceneAssetGuid, out data);
        }
        
        [Obsolete("Use EditorGraph.TryGetPassageTransitionData(string, out EditorTransitionData) instead.")]
        public bool TryGetPassageTransitionData(string currentPortGuid, out EditorTransitionData data)
        {
            return GetOrCreateGraph().TryGetPassageTransitionData(currentPortGuid, out data);
        }
        
        [Obsolete("Use EditorGraph.GetTeleportTransitionData(string, string) instead.")]
        public EditorTransitionData GetTeleportTransitionData(string currentPortGuid, string targetPortGuid)
        {
            return GetOrCreateGraph().GetTeleportTransitionData(currentPortGuid, targetPortGuid);
        }
        
        [Obsolete("Use EditorGraph.TryGetPortData(string, out PortData) instead.")]
        public PortData GetPortData(string guid)
        {
            if (GetOrCreateGraph().TryGetPortData(guid, out var data))
                return data;

            return default;
        }
        
        [Obsolete("Use EditorGraph.TryGetPassageTransitionData(string, out EditorTransitionData) for GoFromAsync or GetTeleportTransitionData(string, string) for GoToAsync.")]
        public EditorTransitionData GetTransitionData(string guid, bool isTargetPassage)
        {
            return GetOrCreateGraph().GetTransitionData(guid, isTargetPassage);
        }
        
        [Obsolete("Use EditorGraph.GetNeighboursData(IEnumerable<string>, bool) instead.")]
        public List<SceneNodeData> GetNeighboursData(IEnumerable<string> passageGuids, bool ignoreShortcuts)
        {
            return GetOrCreateGraph().GetNeighboursData(passageGuids, ignoreShortcuts);
        }
        
        [Obsolete("Use EditorGraph.GetNeighboursData(SceneNodeData, bool) instead.")]
        public List<SceneNodeData> GetNeighboursData(SceneNodeData sceneData, bool ignoreShortcuts)
        {
            return GetOrCreateGraph().GetNeighboursData(sceneData, ignoreShortcuts);
        }

        [Obsolete("Use EditorGraph.GetNeighboursData(SceneNodeData, bool) or EditorGraph.GetNeighboursData(IEnumerable<string>, bool).")]
        public List<SceneNodeData> GetNeighboursData(int buildIndex, bool ignoreShortcuts)
        {
            if (!GetOrCreateGraph().TryGetSceneDataByBuildIndex(buildIndex, out var data))
                return null;
            
            return GetOrCreateGraph().GetNeighboursData(data, ignoreShortcuts);
        }
        
        [Obsolete("Use EditorGraph.CanPassTransition(string, bool) instead.")]
        public bool CanPassTransition(string guid, bool ignoreShortcuts)
        {
            return GetOrCreateGraph().CanPassTransition(guid, ignoreShortcuts);
        }

        [Obsolete("Use EditorGraph.CanPassTransition(string, bool, out TransitionPassStatusType) instead.")]
        public bool CanPassTransition(string guid, bool ignoreShortcuts, out TransitionPassStatusType status)
        {
            return GetOrCreateGraph().CanPassTransition(guid, ignoreShortcuts, out status);
        }
        
        [Obsolete("Use EditorGraph.TryGetOppositePassageGuid(string, out string) instead.")]
        public string GetOppositePassageGuid(string guid)
        {
            if (GetOrCreateGraph().TryGetOppositePassageGuid(guid, out var result))
                return result;

            return string.Empty;
        }

        [Obsolete("Use EditorGraph.TryGetOppositePassageGuid(string, out string) instead.")]
        public bool TryGetOppositePassageGuid(string guid, out string opposite)
        {
            return GetOrCreateGraph().TryGetOppositePassageGuid(guid, out opposite);
        }
        
        [Obsolete("Use EditorGraph.TryGetSceneDataByPortGuid(string, out SceneNodeData) instead.")]
        public SceneNodeData GetSceneDataByPortGuid(string guid)
        {
            if (GetOrCreateGraph().TryGetSceneDataByPortGuid(guid, out var data))
                return data;

            return default;
        }

        [Obsolete("Use EditorGraph.TryGetSceneDataByPortGuid(string, out SceneNodeData) instead.")]
        public bool TryGetSceneDataByPortGuid(string guid, out SceneNodeData data)
        {
            return GetOrCreateGraph().TryGetSceneDataByPortGuid(guid, out data);
        }

        [Obsolete("Use EditorGraph.TryGetSceneDataByBuildIndex(index, out SceneNodeData) instead.")]
        public SceneNodeData GetSceneDataByBuildIndex(int buildIndex, out bool hasData)
        {
             hasData = GetOrCreateGraph().TryGetSceneDataByBuildIndex(buildIndex, out var data);
             return data;
        }
        
        [Obsolete("Use EditorGraph.IsShortcutDestination(string) instead.")]
        public bool IsShortcutOutput(string guid)
        {
            return GetOrCreateGraph().IsShortcutDestination(guid);
        }
        
        [Obsolete("Use EditorGraph.IsShortcutOrigin(string) instead.")]
        public bool IsShortcutInput(string guid)
        {
            return GetOrCreateGraph().IsShortcutOrigin(guid);
        }
    }
}
#endif