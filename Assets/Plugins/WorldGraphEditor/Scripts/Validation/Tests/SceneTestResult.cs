#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WorldGraphEditor
{
    [Serializable]
    public struct SceneTestResult
    {
        public bool Tested;
        public string ScenePath;
        public string NodeName;
        public int ErrorsCount;
        public int WarningsCount;
        
        public TestResult[] TestResults;
        
        public SceneTestResult(IEnumerable<TestResult> testResults, string nodeName, string scenePath)
        {
            NodeName = nodeName;
            ScenePath = scenePath;

            TestResults = testResults
                .OrderByDescending(static item => item.TestStatusType == TestStatusType.Error && item.TestStatusType != TestStatusType.Passed)
                .ThenByDescending(static item => item.TestStatusType == TestStatusType.Warning && item.TestStatusType != TestStatusType.Passed)
                .ToArray();
            
            ErrorsCount = TestResults.Count(static item => item.TestStatusType == TestStatusType.Error && item.TestStatusType != TestStatusType.Passed);
            WarningsCount = TestResults.Count(static item => item.TestStatusType == TestStatusType.Warning && item.TestStatusType != TestStatusType.Passed);

            Tested = true;
        }

        public SceneTestResult(SceneNodeData sceneNodeData)
        {
            NodeName = sceneNodeData.NodeName;
            ScenePath = AssetDatabase.GetAssetPath(sceneNodeData.SceneAsset);
            
            TestResults = null;
            ErrorsCount = -1;
            WarningsCount = -1;
            Tested = false;
        }
    }
}
#endif