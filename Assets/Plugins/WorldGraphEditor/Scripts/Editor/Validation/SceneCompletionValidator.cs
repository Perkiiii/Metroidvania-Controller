using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using WorldGraphEditor.Editor.Tests;

namespace WorldGraphEditor.Editor
{
    internal static class SceneCompletionValidator
    {
        public static SceneCompletionData GetCurrentSceneCompletionData(SceneNodeData nodeData)
        {
            var portsAtScene = ObjectUtility.FindObjectsByInterface<ITransitionComponent>().ToArray();
            var duplicates = portsAtScene
                .GroupBy(item => item.GetGuid())
                .Where(group => group.Count() > 1)
                .SelectMany(group => group);

            var missing = nodeData.PortsData.Where(data => portsAtScene.All(port => port.GetGuid() != data.Guid));

            return new SceneCompletionData(nodeData.PortsData, portsAtScene, duplicates, missing, nodeData.SceneAsset.name, nodeData.NodeName);
        }

        public static IEnumerable<SceneTestResult> GetAllScenesTestsResults(IEnumerable<SceneNodeData> nodesData, IEnumerable<ISceneTest> tests)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) 
                return null;
            
            var originalScenePath = SceneManager.GetActiveScene().path;
            var results = GetAllSceneTestResultsWithoutOriginSceneLoading(nodesData, tests);
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

            return results;
        }

        private static IEnumerable<SceneTestResult> GetAllSceneTestResultsWithoutOriginSceneLoading(
            IEnumerable<SceneNodeData> nodesData, IEnumerable<ISceneTest> tests)
        {
            List<SceneTestResult> results = new();
            var testsArr = tests.ToArray();

            foreach (var nodeData in nodesData)
            {
                var scenePath = AssetDatabase.GetAssetPath(nodeData.SceneAsset);

                if (string.IsNullOrEmpty(scenePath))
                    continue;

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                
                var sceneTestResults = testsArr.Select(test => test.Run(new TestContext(nodeData)));
                var testResult = new SceneTestResult(sceneTestResults, nodeData.NodeName, scenePath);
                results.Add(testResult);
            }

            return results;
        }

        public static IEnumerable<SceneTestResult> GetAllSceneTestResults(IEnumerable<SceneNodeData> nodesData, IEnumerable<ISceneTest> tests)
        {
            if (!EditorUtility.DisplayDialog(
                    "Validate Scenes",
                    "All scenes will be loaded one by one and validated using the defined tests.\n\nThis may take some time. Continue?",
                    "Continue",
                    "Cancel"))
                return null;
            
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) 
                return null;
            
            var originalScenePath = SceneManager.GetActiveScene().path;
            var testsArr = tests.ToArray();
            var cancelRequested = false;
            var sceneNodesData = nodesData as SceneNodeData[] ?? nodesData.ToArray();
            List<SceneTestResult> results = new();
            
            foreach (var sceneTest in testsArr)
            {
                sceneTest.Init();
            }
            
            try
            {
                for (var index = 0; index < sceneNodesData.Length; index++)
                {
                    var nodeData = sceneNodesData[index];
                    if (EditorUtility.DisplayCancelableProgressBar(
                            $"Validate Scenes ({index + 1}/{sceneNodesData.Length})",
                            $"Processing {nodeData.SceneAsset.name}",
                            (float) (index + 1) / sceneNodesData.Length))
                    {
                        cancelRequested = true;
                        break;
                    }

                    var scenePath = AssetDatabase.GetAssetPath(nodeData.SceneAsset);

                    if (string.IsNullOrEmpty(scenePath))
                        continue;

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var sceneTestResults = testsArr.Select(test => test.Run(new TestContext(nodeData)));
                    var testResult = new SceneTestResult(sceneTestResults, nodeData.NodeName, scenePath);

                    results.Add(testResult);
                }
            }
            finally
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                EditorUtility.ClearProgressBar();

                if (cancelRequested)
                    WGEConsole.Warning("Validation cancelled by user.");
                else
                    WGEConsole.Log("Validation finished.");
            }
            
            return results;
        }

        public static IEnumerable<SCCGroup> GetSccFormattedData(WorldGraphContainer container, bool ignoreAdditionalPorts, bool ignoreShortcuts)
        {
            var tarjan = new TarjanSCC();
            var components = tarjan.GetStronglyConnectedComponents(container, ignoreAdditionalPorts, ignoreShortcuts);
            var results = new List<SCCGroup>();
            var sceneComponents = components.Select(item =>
                (item.Select(scene => scene.NodeName), item.Select(scene => scene).ToList()));
            var index = 0;

            foreach (var (nodeNameComponent, scenesComponent) in sceneComponents)
            {
                var sceneTestResults = new List<SceneTestResult>();

                foreach (var nodeName in nodeNameComponent)
                {
#pragma warning disable CS0618
                    foreach (var testResult in container.EditorData.SceneTestResults)
                    {
#pragma warning restore CS0618
                        
                        if (testResult.NodeName != nodeName) 
                            continue;
                        
                        scenesComponent.RemoveAll(scene => scene.NodeName == nodeName);
                        sceneTestResults.Add(testResult);
                    }
                }

                sceneTestResults.AddRange(scenesComponent.Select(sceneNodeData => new SceneTestResult(sceneNodeData)));

                results.Add(new SCCGroup(index, sceneTestResults
                        .OrderByDescending(static testResult => testResult.ErrorsCount)
                        .ThenByDescending(static testResult => testResult.WarningsCount)));
                index++;
            }
            
            return results;
        }
    }
}