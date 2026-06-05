#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor
{
    public static class WorldGraphContainerExporter
    {
        public static void ExportToJson(WorldGraphContainer container)
        {
            if (container == null)
            {
                WGEConsole.Error("Export failed: WorldGraphContainer is null.");
                return;
            }
            
            var assetPath = AssetDatabase.GetAssetPath(container);
            
            if (string.IsNullOrEmpty(assetPath))
            {
                WGEConsole.Error("Export failed: Could not determine asset path.");
                return;
            }
            
            var jsonPath = Path.ChangeExtension(assetPath, ".json");
            var json = JsonUtility.ToJson(container, true);
            
            File.WriteAllText(jsonPath, json);
            AssetDatabase.Refresh();
        }

        public static void Import(WorldGraphContainer worldGraphContainer, ContainerEditorData containerEditorData, string path)
        {
            if (worldGraphContainer == null) 
                WGEConsole.Error("Container not found");

            if (containerEditorData == null)
            {
                WGEConsole.Log("Container editor data not found, creating a new one");
                containerEditorData = WorldGraphContainer.GetOrCreateNestedEditorData(worldGraphContainer);
            }
            
            var jsonPath = Path.ChangeExtension(path, ".json");
            var text = File.ReadAllText(jsonPath);
            var result = JsonUtility.FromJson<JsonDataContainer>(text);
            var fixedNodeData = new List<SceneNodeData>();
            
            foreach (var sceneNodeData in result._sceneNodeData!)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(sceneNodeData.BuildIndex);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                sceneNodeData.RefreshSceneAsset(asset);
                
                fixedNodeData.Add(sceneNodeData);
            }
            
            containerEditorData.SetNodeData(fixedNodeData);
            containerEditorData.SetEdgesData(result._edgesData);
            worldGraphContainer.SaveNodes(GetRuntimeData(fixedNodeData));
            worldGraphContainer.SaveEdges(GetRuntimeData(result._edgesData));
            
            EditorUtility.SetDirty(containerEditorData);
            EditorUtility.SetDirty(worldGraphContainer);
            
            AssetDatabase.SaveAssetIfDirty(containerEditorData);
            AssetDatabase.SaveAssetIfDirty(worldGraphContainer);
        }

        public static IEnumerable<SceneRuntimeData> GetRuntimeData(IEnumerable<SceneNodeData> data)
        {
            return data.Select(static item => new SceneRuntimeData
            {
                BuildIndex = item.BuildIndex,
                PortsGuid = item.PortsData.Select(static item => item.Guid).ToArray()
            });
        }

        public static IEnumerable<ConnectionRuntimeData> GetRuntimeData(IEnumerable<EdgeData> data)
        {
            return data.Select(static item => new ConnectionRuntimeData
            {
                IsFacingRight = item.IsFacingRight,
                ToPortGuid = item.ToPortGuid,
                FromPortGuid = item.FromPortGuid,
                TransitionType = item.TransitionType
            });
        }

        private class JsonDataContainer
        {
            public SceneNodeData[] _sceneNodeData;
            public EdgeData[] _edgesData;
        }
    }
}
#endif