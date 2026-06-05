using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class WorldBuilderGraphView : GraphView
    {
        internal Action<List<ISelectable>> OnMouseSelected;
        
        private readonly WorldBuilderGraph _worldBuilderGraph;
        private readonly Dictionary<SceneAsset, List<SceneNode>> _nodeDuplicates = new();
        
        private Label _messageLabel;

        private bool _readyToWork;

        internal WorldBuilderGraphView(WorldBuilderGraph worldBuilderGraph, WorldGraphContainer container)
        {
            _worldBuilderGraph = worldBuilderGraph;
            SetContainer(container);
            GraphUtility.AddStyleSheet(this, "Scripts/Editor/EditorWindows/Styles/WorldBuilderGraph.uss");
            SetupZoom(.15f, 1.5f);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerfomed);
            RegisterCallback<MouseUpEvent>(OnMouseUp);

            graphViewChanged += OnGraphViewChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            ContextualEdge.OnEdgeDetach += OnEdgeDetach;
            SceneNode.OnSceneNodeDeleted += OnSceneNodeDeleted;
            SceneNode.OnSceneAssetChanged += OnSceneAssetChangedEvent;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        internal void Unsubscribe()
        {
            UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            UnregisterCallback<DragPerformEvent>(OnDragPerfomed);
            UnregisterCallback<MouseUpEvent>(OnMouseUp);
            
            graphViewChanged -= OnGraphViewChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ContextualEdge.OnEdgeDetach -= OnEdgeDetach;
            SceneNode.OnSceneNodeDeleted -= OnSceneNodeDeleted;
            SceneNode.OnSceneAssetChanged -= OnSceneAssetChangedEvent;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
        
        public void SetContainer(WorldGraphContainer container)
        {
            _readyToWork = container != null;
            _worldBuilderGraph.SetContainer(container);
            
            ToggleContainerMessage();
        }
        
        private void ToggleContainerMessage()
        {
            if (_readyToWork)
            {
                if (_messageLabel != null)
                    Remove(_messageLabel);
                
                _messageLabel = null;
                return;
            }

            _messageLabel = new Label("To start working, drag and drop your WorldGraphContainer here")
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    top = 0,

                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 32,
                    color = Color.gray,
                }
            };

            Add(_messageLabel);
        }
        
                public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            
            ports.ForEach(port =>
            {
                var isPortAndNodeDifferent = startPort != port && startPort.node != port.node;
                var isContainerConditionCorrect = startPort.direction != port.direction &&
                                                  startPort.orientation == port.orientation;
                
                if (isPortAndNodeDifferent && isContainerConditionCorrect && !port.IsAdditional())
                {
                    compatiblePorts.Add(port);
                }
            });

            return compatiblePorts;
        }

        public void InstantiateNode(SceneNodeData nodeData)
        {
            var scene = nodeData.SceneAsset;
            var node = CreateNodeInternal(scene, nodeData.NodeName, nodeData.Guid, nodeData.Position);
            node.LoadPorts(nodeData.PortsData);
        }
        
        public void InstantiateEdge(EdgeData edgeData)
        {
            var fromPort = ports.FirstOrDefault(port => port.GetGuid() == edgeData.FromPortGuid);
            var toPort = ports.FirstOrDefault(port => port.GetGuid() == edgeData.ToPortGuid);
            Edge edge;

            var guid = edgeData.EdgeGuid;
            
            if (edgeData.TransitionType == TransitionType.Undirected)
                edge = new ContextualEdge(guid);
            else
                edge = new DirectedEdge(edgeData.TransitionType == TransitionType.Shortcut, edgeData.IsFacingRight, guid);

            edge.output = toPort;
            edge.input = fromPort;
                
            fromPort?.Connect(edge);
            toPort?.Connect(edge);
            
            UndoRedoUtility.AddNew(edge);
            AddElement(edge);
        }
        
        public void ClearData()
        {
            _nodeDuplicates.Clear();
            UndoRedoUtility.ClearLookup();
        }

        public void ClearGraph()
        {
            graphElements.ToList().ForEach(RemoveElement);
            ClearData();
        }
        
        public void FindNodeDuplicates(SceneNode node, SceneAsset asset)
        {
            if (asset == null)
                return;
            
            if (_nodeDuplicates.TryGetValue(asset, out var duplicates))
            {
                duplicates.Add(node);
            }
            else
            {
                _nodeDuplicates.Add(asset, new List<SceneNode>{node});
            }
            
            if (_nodeDuplicates[asset].Count == 1)
                return;
            
            foreach (var sceneNode in _nodeDuplicates[asset])
            {
                sceneNode.ErrorData.AddError(ErrorType.HandledSceneDuplicate);
            }
        }
        
        public void OnSceneAssetChanged(SceneNode node, SceneAsset asset)
        {
            if (asset == null)
                return;
            
            if (!_nodeDuplicates.TryGetValue(asset, out var duplicates))
                return;

            duplicates.Remove(node);

            if (_nodeDuplicates[asset].Count == 1)
            {
                _nodeDuplicates[asset][0].ErrorData.RemoveError(ErrorType.HandledSceneDuplicate);
            }

            node.ErrorData.RemoveError(ErrorType.HandledSceneDuplicate);
        }
        
        public void RefreshScenePreviews()
        {
            var opacity = WorldGraphEditorSettings.Instance.NodeBackgroundOpacity;
            
            foreach (var node in nodes)
            {
                if (node is SceneNode sceneNode) 
                    sceneNode.TryAddScenePreview(opacity);
            }
        }
        
        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshScenePreviews();
        }
        
        private void InstantiateNodeFromAsset(SceneAsset sceneAsset, Vector2 position)
        {
            var node = CreateNodeInternal(sceneAsset, sceneAsset.name, Guid.NewGuid().ToString(), position);
            UndoRedoUtility.Record(node, position, "Scene Node added");
            
            DragAndDrop.AcceptDrag();
        }
        
        private void InstantiateNodesFromAssets(IList<SceneAsset> assets, Vector2 originNodePosition)
        {
            var offset = new Vector2(40, 40);

            for (int index = 0; index < assets.Count; index++)
            {
                InstantiateNodeFromAsset(assets[index],  originNodePosition + offset * index);
            }
        }
        
        private SceneNode CreateNodeInternal(SceneAsset sceneAsset, string nodeName, string guid, Vector2 position)
        {
            var node = new SceneNode(sceneAsset, nodeName, guid, position);
            
            FindNodeDuplicates(node, sceneAsset);
            AddElement(node);
            UndoRedoUtility.AddNew(node);
            
            return node;
        }
        
        #region UndoRedo
        
        private void OnUndoRedo()
        {
            if (!_worldBuilderGraph.hasFocus || !_worldBuilderGraph.IsContainerExists)
                return;
            
            UndoNodesFromDictionary();
            UndoEdgesFromDictionary();
            
            SelectItem();
            
            _worldBuilderGraph.SetContainer(UndoRedoUtility.Data.LastContainer);
        }

        private void SelectItem()
        {
            ClearSelection();
            
            foreach (var selectedItem in UndoRedoUtility.Data.SelectedElementGuids)
            {
                if (UndoRedoUtility.NodesLookup.TryGetValue(selectedItem, out var node))
                {
                    AddToSelection(node);
                    _worldBuilderGraph.ShowInInspector(node);
                }
            
                else if (UndoRedoUtility.EdgesLookup.TryGetValue(selectedItem, out var edge))
                {
                    AddToSelection(edge);
                }
            }
            
            UndoRedoUtility.Data.SelectedElementGuids.Clear();
        }

        private void UndoNodesFromDictionary()
        {
            var actualData = UndoRedoUtility.Data.SceneNodeData;
            var nodesToRemove = UndoRedoUtility.GetRemovedNodes().ToList();
            
            foreach (var guid in nodesToRemove)
            {
                if (!UndoRedoUtility.RemoveNode(guid, out var node)) 
                    continue;
                
                OnSceneAssetChanged(node, node.SceneAsset);
                node.RemoveFromHierarchy();
            }

            foreach (var sceneNodeData in actualData.Where(sceneNodeData => 
                         !UndoRedoUtility.NodesLookup.ContainsKey(sceneNodeData.Guid)))
            {
                InstantiateNode(sceneNodeData);
            }
            
            foreach (var sceneNodeData in actualData)
            {
                if (!UndoRedoUtility.IsChanged(sceneNodeData, out var existingNode)) 
                    continue;
                
                var nodeEdges = GraphUtility.GetEdges(existingNode);

                foreach (var edge in nodeEdges)
                {
                    GraphUtility.RemoveEdge(edge);
                }
                
                existingNode.UndoData(sceneNodeData);
            }
        }
        
        private void UndoEdgesFromDictionary()
        {
            var actualData = UndoRedoUtility.Data.EdgesData;
            var edgesToRemove = UndoRedoUtility.GetRemovedEdges().ToList();
            
            foreach (var guid in edgesToRemove)
            {
                if (!UndoRedoUtility.RemoveEdge(guid, out var edge)) 
                    continue;
                
                GraphUtility.RemoveEdge(edge);
            }

            foreach (var edgeData in actualData.Where(edgeData =>
                         !UndoRedoUtility.EdgesLookup.ContainsKey(edgeData.EdgeGuid)))
            {
                InstantiateEdge(edgeData);
            }

            foreach (var edgeData in actualData)
            {
                if (!UndoRedoUtility.IsChanged(edgeData, out var existingEdge))
                    continue;
                
                GraphUtility.RemoveEdge(existingEdge);
                InstantiateEdge(edgeData);
            }
        }
        
        #endregion
        
        #region Events

        private void OnSceneNodeDeleted(SceneNode obj) => DeleteElements(new[] {obj});

        private void OnSceneAssetChangedEvent(SceneNode node, SceneAsset oldAsset, SceneAsset newAsset)
        {
            OnSceneAssetChanged(node, oldAsset);
            FindNodeDuplicates(node, newAsset);
        }

        private void OnEdgeDetach(Edge originEdge, DetachFromPanelEvent evt, Vector2 localMousePosition)
        {
            if (contentViewContainer == null)
                return;
            
            var mousePosition = (evt.currentTarget as VisualElement).ChangeCoordinatesTo(this, localMousePosition);
            var originNodePosition = contentViewContainer.WorldToLocal(mousePosition);
            var targetNode = GraphUtility.GetNodeAtPosition<SceneNode>(nodes, originNodePosition);
            
            if (!IsValidEdgeDetach(originEdge, targetNode))
                return;
            
            var isInputExists = originEdge.input != null;
            var existingPort = isInputExists ? originEdge.input : originEdge.output;
            
            if (!CanConnectToNode(existingPort, targetNode))
                return;
            
            DisconnectExistingEdge(existingPort);
            
            var newPort = targetNode.GetOppositePort(existingPort);
            var newEdge = GraphUtility.CreateEdge(existingPort, newPort, isInputExists);
            
            AddElement(newEdge);
            UndoRedoUtility.RemoveEdge(originEdge.GetGuid());
            GraphUtility.RemoveEdge(originEdge);
        }
        
        private GraphViewChange OnGraphViewChanged(GraphViewChange changes)
        {
            var result = GraphUtility.RegisterGraphChanges(changes);
            
            if (_nodeDuplicates.Count == 0 || result.elementsToRemove == null)
                return result;

            foreach (var selectable in selection)
            {
                if (selectable is not SceneNode sceneNode) 
                    continue;

                if (sceneNode.SceneAsset == null)
                    continue;
                
                _nodeDuplicates[sceneNode.SceneAsset].Remove(sceneNode);
                
                if (_nodeDuplicates[sceneNode.SceneAsset].Count == 1)
                {
                    _nodeDuplicates[sceneNode.SceneAsset][0].ErrorData.RemoveError(ErrorType.HandledSceneDuplicate);
                }
            }

            return result;
        }

        private void OnMouseUp(MouseUpEvent _)
        {
            OnMouseSelected?.Invoke(selection);
        }

        private void OnDragPerfomed(DragPerformEvent evt)
        {
            switch (DragAndDrop.objectReferences[0])
            {
                case SceneAsset:
                {
                    var mousePosition =
                        (evt.currentTarget as VisualElement).ChangeCoordinatesTo(this, evt.localMousePosition);
                    var originNodePosition = contentViewContainer.WorldToLocal(mousePosition);

                    var list = new List<SceneAsset>();

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is SceneAsset sceneAsset)
                            list.Add(sceneAsset);
                    }

                    InstantiateNodesFromAssets(list, originNodePosition);
                    break;
                }
                case WorldGraphContainer worldContainer when DragAndDrop.objectReferences.Length == 1:
                {
                    UndoRedoUtility.Init(worldContainer);
                    GraphSaveUtility.GetInstance(this).LoadViaContainer(worldContainer);
                }
                    break;
            }
        }
        
        private void OnDragUpdated(DragUpdatedEvent _)
        {
            if (DragAndDrop.objectReferences[0] is not SceneAsset &&
                DragAndDrop.objectReferences[0] is not WorldGraphContainer) 
                return;

            if (DragAndDrop.objectReferences[0] is not WorldGraphContainer && !_readyToWork)
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            
            else if (DragAndDrop.objectReferences.Length > 1 && DragAndDrop.objectReferences[0] is WorldGraphContainer)
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;

            else
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }
        
        #endregion
        
        private static bool IsValidEdgeDetach(Edge originEdge, Node targetNode)
        {
            return targetNode != null && (originEdge.input == null || originEdge.output == null);
        }

        
        private static bool CanConnectToNode(Port existingPort, Node targetNode)
        {
            return existingPort != null && existingPort.node != targetNode;
        }
        
        private static void DisconnectExistingEdge(Port existingPort)
        {
            var existingConnection = existingPort.connections.FirstOrDefault();
            
            if (existingConnection != null)
                GraphUtility.RemoveEdge(existingConnection);
        }
    }
}
