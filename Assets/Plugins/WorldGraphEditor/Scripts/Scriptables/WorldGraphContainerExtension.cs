#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    public partial class WorldGraphContainer
    {
        [SerializeField] private bool _hasErrors;
        [SerializeField, ReadOnlyField] private ContainerEditorData _editorData;

        public ContainerEditorData EditorData => _editorData;
        public bool HasData => EditorData != null && GetScenesData() != null && GetEdgesData() != null;

        public void SetEditorData(ContainerEditorData newEditorData) => _editorData = newEditorData;

        public void SaveNodes(IEnumerable<SceneRuntimeData> runtimeData) => _scenesData = runtimeData.ToArray();

        public void SaveEdges(IEnumerable<ConnectionRuntimeData> edgesData) => _connectionsData = edgesData.ToArray();

        public void SaveErrors(bool hasErrors) => _hasErrors = hasErrors;

        public bool ContainsErrors() => _hasErrors;
        
        [Obsolete("Use EditorData.GetPortsDropdownData(string) instead")]
        public IEnumerable<(string Guid, string Name)> GetPortsDropdownData(int buildIndex) => 
            EditorData.GetPortsDropdownData(buildIndex);

        [Obsolete("Use EditorData.GetAllPortsDropdownData() instead")]
        public IEnumerable<(string Guid, string Path)> GetAllPortsDropdownData() =>
            EditorData.GetAllPortsDropdownData();
        
        [ContextMenu("Load From Json")]
        private void LoadFromJson()
        {
            var path = AssetDatabase.GetAssetPath(this);
            WorldGraphContainerExporter.Import(this, _editorData, path);
        }

        [ContextMenu("To Json")]
        private void ExportToJson()
        {
            WorldGraphContainerExporter.ExportToJson(this);
        }
        
        private void DeleteAllNestedAssets()
        {
            var path = AssetDatabase.GetAssetPath(this);

            if (string.IsNullOrEmpty(path))
                return;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var deletedCount = 0;

            foreach (var asset in allAssets)
            {
                if (asset == this)
                    continue;

                DestroyImmediate(asset, true);
                deletedCount++;
            }

            AssetDatabase.ImportAsset(path);
            Debug.Log($"Deleted {deletedCount} nested assets from {name}");
        }
        
        public static ContainerEditorData GetOrCreateNestedEditorData(WorldGraphContainer container)
        {
            var assetPath = AssetDatabase.GetAssetPath(container);
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            
            foreach (var asset in assets)
            {
                if (asset is ContainerEditorData existingEditorData)
                {
                    return existingEditorData;
                }
            }
            
            var newEditorData = CreateInstance<ContainerEditorData>();
            newEditorData.name = "Editor Only Data";
    
            AssetDatabase.AddObjectToAsset(newEditorData, container);
            container.SetEditorData(newEditorData);
            EditorUtility.SetDirty(container);

            return newEditorData;
        }
    }
}

#endif