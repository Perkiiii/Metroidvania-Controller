using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WorldGraphEditor.Editor
{
    internal static class ScenesValidationHelper
    {
        public static bool IsAllScenesValid(WorldGraphContainer container)
        {
            if (container == null || container.EditorData == null)
                return false;
            
            var sceneNodeData = container.EditorData.SceneNodeData;
            var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();

            return IsAllScenesValid(sceneNodeData, enabledScenes);
        }
        
        public static bool IsAllScenesValid(IReadOnlyList<SceneNodeData> nodeData, EditorBuildSettingsScene[] buildScenes)
        {
            var buildScenePaths = buildScenes.Select(scene => scene.path).ToHashSet();
            var allPathsMatch = nodeData
                .Select(data => AssetDatabase.GetAssetPath(data.SceneAsset))
                .All(path => buildScenePaths.Contains(path));

            var indicesMatch = nodeData
                .Select(data => (Path: AssetDatabase.GetAssetPath(data.SceneAsset), Index: data.BuildIndex))
                .All(tuple => buildScenes
                    .Select((scene, index) => (scene.path, index))
                    .Contains((tuple.Path, tuple.Index)));

            return allPathsMatch && indicesMatch;
        }

        public static void AddMissingScenesToBuild(IReadOnlyList<SceneNodeData> nodeData, EditorBuildSettingsScene[] buildScenes)
        {
            var buildScenePaths = buildScenes.Select(scene => scene.path).ToHashSet();

            foreach (var node in nodeData)
            {
                var scenePath = AssetDatabase.GetAssetPath(node.SceneAsset);
                if (!buildScenePaths.Contains(scenePath))
                {
                    node.SceneAsset.AddSceneToBuild();
                }
            }
        }
        
        public static bool IsHandledAndEnabledInBuild(this SceneAsset sceneAsset)
        {
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            return EditorBuildSettings.scenes.Any(scene => scene.path == path && scene.enabled);
        }

        public static void AddSceneToBuild(this SceneAsset sceneAsset)
        {
            if (sceneAsset.IsHandledAndEnabledInBuild())
                return;
            
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            var scenes = EditorBuildSettings.scenes.ToList();
            
            foreach (var scene in scenes.Where(scene => scene.path == path))
            {
                if (scene.enabled) 
                    return;

                scene.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
            
            var newScene = new EditorBuildSettingsScene(path, true);
            scenes.Add(newScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
