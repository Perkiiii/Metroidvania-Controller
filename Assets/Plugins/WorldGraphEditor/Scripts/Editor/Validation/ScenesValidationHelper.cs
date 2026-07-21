using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WorldGraphEditor.Editor
{
    internal static class ScenesValidationHelper
    {
        public static bool IsAllScenesValid(WorldGraphContainer container)
        {
            if (container == null || container.EditorGraph == null)
                return false;
            
            var sceneNodeData = container.EditorGraph.GetScenesData();
            var enabledScenes = EditorBuildSettings.scenes.Where(item => item.enabled).ToArray();

            return IsAllScenesValid(sceneNodeData, enabledScenes);
        }
        
        public static bool IsAllScenesValid(IReadOnlyList<SceneNodeData> nodeData, EditorBuildSettingsScene[] buildScenes)
        {
            var buildScenePaths = buildScenes.Select(scene => scene.path).ToHashSet();

#if WGE_ADDRESSABLES
            var nonAddressableNodes = nodeData
                .Where(data => !AddressablesAddressResolver.IsAddressableScene(data.SceneAssetGuid))
                .ToList();
#else
            var nonAddressableNodes = nodeData;
#endif

            var allPathsMatch = nonAddressableNodes
                .Select(data => AssetDatabase.GetAssetPath(data.SceneAsset))
                .All(path => buildScenePaths.Contains(path));

            var indicesMatch = nonAddressableNodes
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
#if WGE_ADDRESSABLES
                if (AddressablesAddressResolver.IsAddressableScene(node.SceneAssetGuid))
                    continue;
#endif
                var scenePath = AssetDatabase.GetAssetPath(node.SceneAsset);
                if (!buildScenePaths.Contains(scenePath))
                {
                    node.SceneAsset.AddToBuild();
                }
            }
        }
        
        public static bool IsHandledAndEnabledInBuild(this SceneAsset sceneAsset)
        {
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            return EditorBuildSettings.scenes.Any(scene => scene.path == path && scene.enabled);
        }

        public static void AddToBuild(this SceneAsset sceneAsset)
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

#if WGE_ADDRESSABLES
        public static void DisableInBuild(this SceneAsset sceneAsset)
        {
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            var scenes = EditorBuildSettings.scenes.ToList();
            var changed = false;

            foreach (var scene in scenes.Where(scene => scene.path == path))
            {
                if (!scene.enabled)
                    continue;

                scene.enabled = false;
                changed = true;
            }

            if (changed)
                EditorBuildSettings.scenes = scenes.ToArray();
        }
#endif
    }
}
