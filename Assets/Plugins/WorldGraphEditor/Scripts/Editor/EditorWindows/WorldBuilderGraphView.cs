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
        
        private VisualElement _miniMap;
        private VisualElement _miniMapBody;
        private Button _miniMapMinimizeButton;
        private Label _miniMapZoomLabel;
        private Label _messageLabel;

        private bool _isMiniMapDragging;
        private bool _isMiniMapMinimized;
        private bool _isMiniMapPinnedToBottomRight = true;
        private Vector2 _miniMapDragStartMousePosition;
        private Vector2 _miniMapDragStartPosition;
        private bool _readyToWork;

        private const float _miniMapWidth = 200f;
        private const float _miniMapHeight = 150f;
        private const float _miniMapHeaderHeight = 24f;
        private const float _miniMapFooterHeight = 18f;
        private const float _miniMapMargin = 10f;

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

            AddMiniMap();

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerfomed);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<GeometryChangedEvent>(OnGraphViewGeometryChanged);

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
            UnregisterCallback<GeometryChangedEvent>(OnGraphViewGeometryChanged);
            
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
            foreach (var graphElement in graphElements.ToList())
            {
                if (graphElement == _miniMap)
                    continue;

                RemoveElement(graphElement);
            }

            ClearData();
        }

        private void AddMiniMap()
        {
            if (_miniMap != null)
            {
                if (_miniMap.parent == null)
                    Add(_miniMap);

                return;
            }

            _miniMap = new VisualElement
            {
                visible = false,
            };

            StyleMiniMap(_miniMap);
            var header = CreateMiniMapHeader();
            _miniMapBody = CreateMiniMapBody();
            _miniMapZoomLabel = CreateMiniMapZoomLabel();

            _miniMap.Add(header);
            _miniMap.Add(_miniMapBody);
            _miniMap.Add(_miniMapZoomLabel);
            _miniMap.RegisterCallback<MouseDownEvent>(OnMiniMapDragStart);
            _miniMap.RegisterCallback<MouseMoveEvent>(OnMiniMapDragMove);
            _miniMap.RegisterCallback<MouseUpEvent>(OnMiniMapDragEnd);
            _miniMap.RegisterCallback<MouseLeaveEvent>(OnMiniMapDragEnd);
            _miniMap.schedule.Execute(RefreshMiniMap).Every(100);

            Add(_miniMap);
            _miniMap.schedule.Execute(PlaceMiniMapBottomRight).ExecuteLater(0);
        }

        internal void ToggleMiniMap()
        {
            if (_miniMap == null || _miniMap.parent == null)
                AddMiniMap();

            _miniMap.visible = !_miniMap.visible;

            if (_miniMap.visible)
            {
                if (_isMiniMapPinnedToBottomRight)
                    PlaceMiniMapBottomRight();

                _miniMap.BringToFront();
            }
        }

        private static void StyleMiniMap(VisualElement miniMap)
        {
            var borderColor = new Color(0.04f, 0.04f, 0.04f, 1f);

            miniMap.style.position = Position.Absolute;
            miniMap.style.left = _miniMapMargin;
            miniMap.style.top = _miniMapMargin;
            miniMap.style.width = _miniMapWidth;
            miniMap.style.height = _miniMapHeight;
            miniMap.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 0.96f);
            miniMap.style.borderTopColor = borderColor;
            miniMap.style.borderRightColor = borderColor;
            miniMap.style.borderBottomColor = borderColor;
            miniMap.style.borderLeftColor = borderColor;
            miniMap.style.borderTopWidth = 1;
            miniMap.style.borderRightWidth = 1;
            miniMap.style.borderBottomWidth = 1;
            miniMap.style.borderLeftWidth = 1;
            miniMap.style.borderTopLeftRadius = 3;
            miniMap.style.borderTopRightRadius = 3;
            miniMap.style.borderBottomRightRadius = 3;
            miniMap.style.borderBottomLeftRadius = 3;
            miniMap.style.paddingTop = 0;
            miniMap.style.paddingRight = 0;
            miniMap.style.paddingBottom = 0;
            miniMap.style.paddingLeft = 0;
            miniMap.style.overflow = Overflow.Hidden;
        }

        private VisualElement CreateMiniMapHeader()
        {
            var header = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    height = 24,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = new Color(0.02f, 0.02f, 0.02f, 0.98f),
                    paddingLeft = 7,
                    paddingRight = 5
                }
            };

            header.Add(CreateMiniMapMenuIcon());
            header.Add(new Label("MiniMap")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = new Color(0.74f, 0.74f, 0.74f, 1f),
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Normal,
                    marginLeft = 5,
                    flexGrow = 1
                }
            });
            _miniMapMinimizeButton = CreateMiniMapMinimizeButton();
            header.Add(_miniMapMinimizeButton);

            return header;
        }

        private Button CreateMiniMapMinimizeButton()
        {
            var button = new Button(ToggleMiniMapMinimized)
            {
                text = "-"
            };

            button.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            button.RegisterCallback<MouseMoveEvent>(evt => evt.StopPropagation());
            button.RegisterCallback<MouseUpEvent>(evt => evt.StopPropagation());
            button.style.width = 18;
            button.style.height = 18;
            button.style.marginTop = 0;
            button.style.marginRight = 0;
            button.style.marginBottom = 0;
            button.style.marginLeft = 0;
            button.style.paddingTop = 0;
            button.style.paddingRight = 0;
            button.style.paddingBottom = 1;
            button.style.paddingLeft = 0;
            button.style.color = new Color(0.74f, 0.74f, 0.74f, 1f);
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderTopWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            return button;
        }

        private static VisualElement CreateMiniMapMenuIcon()
        {
            var icon = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 15,
                    height = 10,
                    justifyContent = Justify.SpaceBetween
                }
            };

            for (int i = 0; i < 3; i++)
            {
                icon.Add(new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        height = 1,
                        backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f)
                    }
                });
            }

            return icon;
        }

        private VisualElement CreateMiniMapBody()
        {
            var body = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 5,
                    right = 5,
                    top = _miniMapHeaderHeight,
                    bottom = _miniMapFooterHeight,
                    backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderTopColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                    borderRightColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                    borderBottomColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                    borderLeftColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    overflow = Overflow.Hidden
                }
            };

            body.generateVisualContent += OnGenerateMiniMapBodyVisualContent;
            return body;
        }

        private static Label CreateMiniMapZoomLabel()
        {
            return new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    right = 4,
                    bottom = 2,
                    color = new Color(0.74f, 0.74f, 0.74f, 1f),
                    fontSize = 9,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            };
        }

        private void RefreshMiniMap()
        {
            if (_miniMap is not {visible: true} || _miniMapBody == null)
                return;

            _miniMapZoomLabel.text = $"Zoom: {Mathf.RoundToInt(scale * 100f)}%";
            _miniMapBody.MarkDirtyRepaint();
        }

        private void OnGenerateMiniMapBodyVisualContent(MeshGenerationContext context)
        {
            var sceneNodes = nodes.ToList().OfType<SceneNode>().ToList();
            if (sceneNodes.Count == 0)
                return;

            var graphBounds = GetGraphBounds(sceneNodes);
            var viewportRect = GetViewportRectInContentSpace();
            if (viewportRect.width > 0f && viewportRect.height > 0f)
                graphBounds = Encapsulate(graphBounds, viewportRect);

            graphBounds.xMin -= 80f;
            graphBounds.xMax += 80f;
            graphBounds.yMin -= 80f;
            graphBounds.yMax += 80f;

            var bodyWidth = _miniMapBody.resolvedStyle.width;
            var bodyHeight = _miniMapBody.resolvedStyle.height;
            if (bodyWidth <= 0f || bodyHeight <= 0f)
                return;

            var scaleFactor = Mathf.Min(bodyWidth / graphBounds.width, bodyHeight / graphBounds.height);
            var offset = new Vector2(
                (bodyWidth - graphBounds.width * scaleFactor) * 0.5f,
                (bodyHeight - graphBounds.height * scaleFactor) * 0.5f);
            var painter = context.painter2D;

            foreach (var sceneNode in sceneNodes)
            {
                DrawMiniMapNodePreview(painter, sceneNode, graphBounds, offset, scaleFactor);
            }

            DrawMiniMapViewportPreview(painter, viewportRect, graphBounds, offset, scaleFactor);
        }

        private void DrawMiniMapNodePreview(Painter2D painter, SceneNode sceneNode, Rect graphBounds, Vector2 offset, float scaleFactor)
        {
            var position = sceneNode.GetPosition();
            var rect = ExpandMiniMapRect(ToMiniMapRect(position, graphBounds, offset, scaleFactor), 18f, 8f);
            var isSelected = selection.Contains(sceneNode);
            var nodeColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);
            var borderColor = isSelected
                ? new Color(0.27f, 0.75f, 1f, 1f)
                : nodeColor;

            PathRectangle(painter, rect);
            painter.fillColor = nodeColor;
            painter.Fill();
            painter.strokeColor = borderColor;
            painter.lineWidth = isSelected ? 2f : 1f;
            painter.Stroke();
        }

        private static void DrawMiniMapViewportPreview(Painter2D painter, Rect viewportRect, Rect graphBounds,
            Vector2 offset, float scaleFactor)
        {
            if (viewportRect.width <= 0f || viewportRect.height <= 0f)
                return;

            var rect = ExpandMiniMapRect(ToMiniMapRect(viewportRect, graphBounds, offset, scaleFactor), 8f, 8f);
            PathRectangle(painter, rect);
            painter.strokeColor = new Color(0.76f, 0.76f, 0.76f, 0.75f);
            painter.lineWidth = 1f;
            painter.Stroke();
        }

        private void ToggleMiniMapMinimized()
        {
            _isMiniMapMinimized = !_isMiniMapMinimized;
            _miniMapBody.visible = !_isMiniMapMinimized;
            _miniMapZoomLabel.visible = !_isMiniMapMinimized;
            _miniMapMinimizeButton.text = _isMiniMapMinimized ? "+" : "-";
            _miniMap.style.height = _isMiniMapMinimized ? _miniMapHeaderHeight : _miniMapHeight;

            if (_isMiniMapPinnedToBottomRight)
                PlaceMiniMapBottomRight();
            else
                ClampMiniMapToView();
        }

        private static Rect GetGraphBounds(IReadOnlyList<SceneNode> sceneNodes)
        {
            var bounds = sceneNodes[0].GetPosition();

            for (int i = 1; i < sceneNodes.Count; i++)
            {
                bounds = Encapsulate(bounds, sceneNodes[i].GetPosition());
            }

            return bounds;
        }

        private Rect GetViewportRectInContentSpace()
        {
            var topLeft = contentViewContainer.WorldToLocal(worldBound.position);
            var bottomRight = contentViewContainer.WorldToLocal(worldBound.position + worldBound.size);
            var min = Vector2.Min(topLeft, bottomRight);
            var max = Vector2.Max(topLeft, bottomRight);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect ToMiniMapRect(Rect graphRect, Rect graphBounds, Vector2 offset, float scaleFactor)
        {
            return new Rect(
                offset.x + (graphRect.x - graphBounds.xMin) * scaleFactor,
                offset.y + (graphRect.y - graphBounds.yMin) * scaleFactor,
                graphRect.width * scaleFactor,
                graphRect.height * scaleFactor);
        }

        private static Rect ExpandMiniMapRect(Rect rect, float minWidth, float minHeight)
        {
            if (rect.width < minWidth)
            {
                rect.x -= (minWidth - rect.width) * 0.5f;
                rect.width = minWidth;
            }

            if (rect.height < minHeight)
            {
                rect.y -= (minHeight - rect.height) * 0.5f;
                rect.height = minHeight;
            }

            return rect;
        }

        private static void PathRectangle(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.ClosePath();
        }

        private static Rect Encapsulate(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        private void OnMiniMapDragStart(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;

            if (evt.localMousePosition.y > _miniMapHeaderHeight)
                return;

            _isMiniMapPinnedToBottomRight = false;
            _isMiniMapDragging = true;
            _miniMapDragStartMousePosition = evt.mousePosition;
            _miniMapDragStartPosition = new Vector2(_miniMap.resolvedStyle.left, _miniMap.resolvedStyle.top);
            _miniMap.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMiniMapDragMove(MouseMoveEvent evt)
        {
            if (!_isMiniMapDragging)
                return;

            var delta = evt.mousePosition - _miniMapDragStartMousePosition;
            SetMiniMapPosition(_miniMapDragStartPosition + delta);
            evt.StopPropagation();
        }

        private void OnMiniMapDragEnd(EventBase evt)
        {
            if (!_isMiniMapDragging)
                return;

            _isMiniMapDragging = false;
            _miniMap.ReleaseMouse();
            evt.StopPropagation();
        }

        private void SetMiniMapPosition(Vector2 position)
        {
            var maxX = Mathf.Max(0f, resolvedStyle.width - _miniMap.resolvedStyle.width);
            var maxY = Mathf.Max(0f, resolvedStyle.height - _miniMap.resolvedStyle.height);

            _miniMap.style.left = Mathf.Clamp(position.x, 0f, maxX);
            _miniMap.style.top = Mathf.Clamp(position.y, 0f, maxY);
        }

        private void PlaceMiniMapBottomRight()
        {
            if (_miniMap == null)
                return;

            var height = _isMiniMapMinimized ? _miniMapHeaderHeight : _miniMapHeight;
            var x = Mathf.Max(0f, resolvedStyle.width - _miniMapWidth - _miniMapMargin);
            var y = Mathf.Max(0f, resolvedStyle.height - height - _miniMapMargin);

            _miniMap.style.left = x;
            _miniMap.style.top = y;
        }

        private void ClampMiniMapToView()
        {
            if (_miniMap == null)
                return;

            SetMiniMapPosition(new Vector2(_miniMap.resolvedStyle.left, _miniMap.resolvedStyle.top));
        }

        private void OnGraphViewGeometryChanged(GeometryChangedEvent _)
        {
            if (_isMiniMapPinnedToBottomRight)
                PlaceMiniMapBottomRight();
            else
                ClampMiniMapToView();
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
