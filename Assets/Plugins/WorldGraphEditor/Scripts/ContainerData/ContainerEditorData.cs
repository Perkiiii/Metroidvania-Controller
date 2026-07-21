#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGraphEditor
{
    public sealed partial class ContainerEditorData : ScriptableObject
    {
        [SerializeField] private SceneNodeData[] _sceneNodeData;
        [SerializeField] private EdgeData[] _edgesData;
        [SerializeField] private SceneTestResult[] _testResults;
        
        public IReadOnlyList<SceneNodeData> SceneNodeData => _sceneNodeData;
        public IReadOnlyList<EdgeData> EdgesData => _edgesData;
        public IReadOnlyList<SceneTestResult> SceneTestResults => _testResults;
        
        private EditorGraph _editorGraph;

        public EditorGraph EditorGraph => GetOrCreateGraph();
        
        internal void DisposeGraph() => _editorGraph = null;

        internal void RebuildGraph()
        {
            if (_editorGraph == null)
            {
                Initialize();
                return;
            }

            _editorGraph.Rebuild();
        }

        private void Initialize()
        {
            _editorGraph = new EditorGraph(() => new EditorGraphSnapshot(_sceneNodeData, _edgesData));
        }
        
        public void SetNodeData(IEnumerable<SceneNodeData> data) => _sceneNodeData = data.ToArray();
        public void SetEdgesData(IEnumerable<EdgeData> edgesData) => _edgesData = edgesData.ToArray();
        public void SetTestsData(IEnumerable<SceneTestResult> testResults) => _testResults = testResults.ToArray();
    }
}

#endif
