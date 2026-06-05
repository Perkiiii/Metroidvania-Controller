using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Application = UnityEngine.Application;

namespace WorldGraphEditor.Editor
{
    internal class GraphSaveUtility
    {
        private WorldBuilderGraphView _targetGraphView;
        
        private WorldGraphContainer _container;

        internal static event Action OnContainerSaved;

        private List<Port> _ports => _targetGraphView.ports.ToList();
        private List<Edge> _edges => _targetGraphView.edges.ToList();
        private List<SceneNode> _nodes => _targetGraphView.nodes.Cast<SceneNode>().ToList();
        
        internal static GraphSaveUtility GetInstance(WorldBuilderGraphView graphView)
        {
            return new GraphSaveUtility
            {
                _targetGraphView = graphView,
            };
        }
        
        internal void Save(WorldGraphContainer existingContainer)
        {
            if (!_nodes.Any())
            {
                EditorUtility.DisplayDialog("No data to save", "The World Graph Container does not contain any data to save.", "OK");
                return;
            }

            var editorData = WorldGraphContainer.GetOrCreateNestedEditorData(existingContainer);
            
            SaveNodes(existingContainer, editorData);
            SaveEdges(existingContainer, editorData);
            SaveErrorStatus(existingContainer);
            
            EditorUtility.SetDirty(existingContainer);
            AssetDatabase.SaveAssetIfDirty(existingContainer);
            
            existingContainer.Initialize();
            RefreshPassages();
            OnContainerSaved?.Invoke();
        }

        internal static void RefreshPathAndBuildIndex(WorldGraphContainer container)
        {
            var graphEditorData = container.EditorData.SceneNodeData.Select(sceneNodeData => new SceneNodeData(
                sceneNodeData, 
                sceneNodeData.SceneAsset.GetFormattedPath(), 
                sceneNodeData.SceneAsset.GetBuildIndex())).ToArray();

            var runtimeData = graphEditorData.GetRuntimeData();
            var editorData = WorldGraphContainer.GetOrCreateNestedEditorData(container);
            
            editorData.SetNodeData(graphEditorData);
            container.SaveNodes(runtimeData);
            
            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssets();
            container.Initialize();
            RefreshPassages();
        }

        internal WorldGraphContainer LoadViaPath()
        {
            var startDirectory = WGEAssetPathUtility.GetPath("Resources");
            var absolutePath = EditorUtility.OpenFilePanel("Select World Container", startDirectory, "asset");
            var projectPath = Application.dataPath;
            var relativePath = "Assets" + absolutePath.Replace(projectPath, "").Replace("\\", "/");

            if (string.IsNullOrEmpty(absolutePath))
                return null;

            _container = AssetDatabase.LoadAssetAtPath<WorldGraphContainer>(relativePath);
            LoadInternal();

            return _container;
        }

        internal void LoadViaContainer(WorldGraphContainer container)
        {
            _container = container;
            LoadInternal();
        }
        
        private void LoadInternal()
        {
            ClearGraph();
            
            if (_container.EditorData != null)
            {
                CreateNodes();
                ConnectPorts();
            }
            else if (_container.ConnectionsData is {Count: > 0})
            {
                Debug.LogError("No editor data was found!");
            }

            _targetGraphView.SetContainer(_container);
        }
        
        private void ConnectPorts()
        {
            if (_container.EditorData?.EdgesData == null)
                return;
            
            foreach (var edgeData in _container.EditorData.EdgesData)
            {
                _targetGraphView.InstantiateEdge(edgeData);
            }
        }

        private void CreateNodes()
        {
            if (_container.EditorData?.SceneNodeData == null)
                return;
            
            foreach (var nodeData in _container.EditorData.SceneNodeData)
            {
                _targetGraphView.InstantiateNode(nodeData);
            }
        }

        private void ClearGraph()
        {
            _targetGraphView?.ClearGraph();
        }

        private void SaveEdges(WorldGraphContainer container, ContainerEditorData containerEditorData)
        {
            var edgesData = new List<EdgeData>();
            
            foreach (var edge in _edges)
            {
                edgesData.Add(edge.GetData());
            }

            var runtimeData = edgesData.GetRuntimeData();
            
            container.SaveEdges(runtimeData);
            containerEditorData.SetEdgesData(edgesData);
        }

        private void SaveNodes(WorldGraphContainer container, ContainerEditorData containerEditorData)
        {
            var graphNodesData = _nodes.Select(node => node.GetDataWithPorts(true)).ToArray();
            var nodesToAdd = graphNodesData.Where(item => item.BuildIndex == -1).ToArray();
            
            if (nodesToAdd.Any())
            {
                var displayDialog = EditorUtility.DisplayDialog("Graph Save Error", 
                    $"You are trying to save a graph that contains scenes missing from the build settings. Click \"Add to build\" to automatically add all scenes to the build.",
                    "Add to build");
                
                if (displayDialog)
                {
                    foreach (var nodeData in nodesToAdd)
                    {
                        nodeData.SceneAsset.AddSceneToBuild();
                    }
                    
                    graphNodesData = _nodes.Select(node => node.GetDataWithPorts(true)).ToArray();
                }
            }
            
            var runtimeData = graphNodesData.GetRuntimeData();
            
            container.SaveNodes(runtimeData);
            containerEditorData.SetNodeData(graphNodesData);
        }
        
        private void SaveErrorStatus(WorldGraphContainer container)
        {
            var isGraphContainsErrors = _nodes.Any(node => node.ErrorData.HasErrors);
            container.SaveErrors(isGraphContainsErrors);
        }
        
        private static void RefreshPassages()
        {
            TransitionComponentRefresher.RefreshPorts();
        }
    }
}
