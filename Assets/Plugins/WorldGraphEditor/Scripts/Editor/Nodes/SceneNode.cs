using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class SceneNode : BaseNode
    {
        internal SceneAsset SceneAsset { get; private set; }
        internal IReadOnlyList<Port> Ports => _ports;
        internal NodeErrorData ErrorData { get; private set; }
        
        internal string SceneAssetGuid { get; private set; }
        
        internal static Action<SceneNode> OnSceneNodeDeleted;
        internal static Action<SceneNode, SceneAsset, SceneAsset> OnSceneAssetChanged;
        internal static Action<SceneNode, SceneAsset> OnBeforeSceneAssetChanged;
        
        private Vector2 _previousPosition;

        private const string _LEFT_PORT_NAME = "Left";
        private const string _RIGHT_PORT_NAME = "Right";
        private const string _TOP_PORT_NAME = "Top";
        private const string _BOTTOM_PORT_NAME = "Bottom";
        private const string _ADDITIONAL_PORT_NAME = "Additional";
        
        private readonly Label _handledScenesLabel;
        private readonly Vector2 _defaultSceneNodeSize = new(150, 200);
        private readonly List<Port> _ports = new();

        private VisualElement _topContainer { get; set; }
        private VisualElement _bottomContainer { get; set; }
        private VisualElement _leftContainer { get; set; }
        private VisualElement _rightContainer { get; set; }
        private VisualElement _topTitleContainer { get; set; }
        private VisualElement _bottomTitleContainer { get; set; }

#if WGE_ADDRESSABLES
        private Label _addressableBadge;
#endif

        public SceneNode(SceneAsset sceneAsset, string name, string guid, Vector2 position)
        {
            base.title = name;
            Name = name;
            Guid = guid;
            SceneAsset = sceneAsset;
            ErrorData = new NodeErrorData(this, "scene-node-duplicate");
            
            CreateContainers();
            CreateMarkup();
            SetPosition(position);

            var portButton = new Button(ShowPortTypeMenu)
            {
                text = "+"
            };

            _topTitleContainer.Add(portButton);
#if WGE_ADDRESSABLES
            CreateAddressableBadge();
#endif
            _handledScenesLabel = new Label();
            _bottomTitleContainer.Add(_handledScenesLabel);
            styleSheets.Add(
                WGEAssetPathUtility.LoadStyleSheet(
                    "Scripts/Editor/EditorWindows/Styles/SceneNode.uss"));

            RegisterCallback<ContextualMenuPopulateEvent>(OnContextManuPopulate);
            RefreshHandledScenesLabel(true);
            RefreshExpandedState();
            RefreshPorts();
        }

        public void AddScenePreview(float opacity)
        {
            if (!CanApplyScenePreview())
                return;

            if (!SceneScreenshotsData.Instance.IsAutoCaptureEnabled(SceneAssetGuid))
            {
                ClearScenePreview();
                return;
            }

            var texture = SceneScreenshotUtility.GetScreenshotTexture(SceneAssetGuid);
            
            if (texture == null)
            {
                ClearScenePreview();
                return;
            }

            var alpha = 1 - Mathf.Clamp01(opacity);
            mainContainer.style.backgroundImage = new StyleBackground(texture);
            mainContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 1f, 1f, alpha));
        }

        private bool CanApplyScenePreview()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;

            return !string.IsNullOrEmpty(SceneAssetGuid);
        }

        public void ClearScenePreview()
        {
            mainContainer.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }
        
        private void OnContextManuPopulate(ContextualMenuPopulateEvent evt)
        {
            evt.menu.ClearItems();
            evt.menu.AppendAction($"Load [{SceneAsset.name}]", LoadScene, DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete", _ => Delete());
            
            evt.StopImmediatePropagation();
        }

        private void LoadScene(DropdownMenuAction obj) => GraphUtility.LoadScene(SceneAsset);

        private void Delete() => OnSceneNodeDeleted?.Invoke(this);

        private void ClearPorts()
        {
            foreach (var port in _ports)
            {
                RemovePortFromContainer(port);
            }
            
            _ports.Clear();
        }

        private void RemovePortFromContainer(Port port)
        {
            var container = GetPortContainer(port.direction, port.orientation);
            container.Remove(port);
        }

        public void UpdateData(NodeChangesData changesData)
        {
            OnSceneAssetChanged?.Invoke(this, SceneAsset, changesData.SceneAsset);

            var isAssetChanged = SceneAsset.GetUnityGuid() != changesData.SceneAsset.GetUnityGuid();
            
            Name = changesData.NodeName;
            title = Name;
            SceneAsset = changesData.SceneAsset;

            for (var i = 0; i < _ports.Count; i++)
            {
                var port = _ports[i];
                port.portName = changesData.PortsData[i].Name;
            }

            UndoRedoUtility.Record(this, GetPosition().position, "Node changed");
            
            RefreshHandledScenesLabel(isAssetChanged);
        }
        
        public void UndoData(SceneNodeData sceneNodeData)
        {
            Name = sceneNodeData.NodeName;
            title = sceneNodeData.NodeName;
            SceneAsset = sceneNodeData.SceneAsset;
            SetPosition(sceneNodeData.Position);
            LoadPorts(sceneNodeData.PortsData);
            
            SceneNodeInspectorHelper.Instance?.Init(this);
        }

        private Port AddPort(Direction direction, Orientation orientation, VisualElement container, string directionName, bool isAdditional)
        {
            var guid = System.Guid.NewGuid().ToString();
            var portsCount = container.Query("connector").ToList().Count;
            var portName = $"{directionName} {portsCount}";
            
            var port = GraphUtility.CreatePort(direction, orientation, portName, guid, isAdditional);
            _ports.Add(port);
            container.Add(port);
            
            UndoRedoUtility.Record(this, GetPosition().position, "Node changed");
            SceneNodeInspectorHelper.Instance?.Init(this);
            
            RefreshExpandedState();
            RefreshPorts();
            
            return port;
        }
        
        public void LoadPorts(IList<PortData> portsData)
        {
            var data = GraphUtility.GetEdges(this);
            
            GraphUtility.ClearEdges(data);
            ClearPorts();
            
            foreach (var portData in portsData)
            {
                var direction = portData.IsInput ? Direction.Input : Direction.Output;
                var orientation = portData.IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;
            
                var guid = portData.Guid;
                var isAdditional = portData.IsAdditional;
                var portName = portData.Name;
                var container = direction switch
                {
                    Direction.Input when orientation == Orientation.Horizontal => _leftContainer,
                    Direction.Output when orientation == Orientation.Horizontal => _rightContainer,
                    Direction.Input when true => _topContainer,
                    _ => _bottomContainer
                };

                var port = GraphUtility.CreatePort(direction, orientation, portName, guid, isAdditional);
                _ports.Add(port);

                if (string.IsNullOrWhiteSpace(portName))
                    ErrorData.AddError(ErrorType.PortWrongName);
                
                container.Add(port);
            }
            
            if (_ports.HasDuplicatesPorts(item => item.portName))
                ErrorData.AddError(ErrorType.PortWrongName);
        }
        
        private void SetPosition(Vector2 position)
        {
            base.SetPosition(new Rect(position, _defaultSceneNodeSize));
        }
        
        private void ShowPortTypeMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Left Passage"), false, () => AddPort(Direction.Input, Orientation.Horizontal, _leftContainer, _LEFT_PORT_NAME, false));
            menu.AddItem(new GUIContent("Right Passage"), false, () => AddPort(Direction.Output, Orientation.Horizontal, _rightContainer, _RIGHT_PORT_NAME, false));
            menu.AddItem(new GUIContent("Top Passage"), false, () => AddPort(Direction.Input, Orientation.Vertical, _topContainer, _TOP_PORT_NAME, false));
            menu.AddItem(new GUIContent("Bottom Passage"), false, () => AddPort(Direction.Output, Orientation.Vertical, _bottomContainer, _BOTTOM_PORT_NAME, false));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Additional Port"), false, () => AddPort(Direction.Input, Orientation.Vertical, _topContainer, _ADDITIONAL_PORT_NAME, true));
            menu.ShowAsContext();
        }

        private void CreateContainers()
        {
            _topContainer = GraphUtility.GetVisualElement("top-container");
            _leftContainer = GraphUtility.GetVisualElement("left-container");
            _rightContainer = GraphUtility.GetVisualElement("right-container");
            _bottomContainer = GraphUtility.GetVisualElement("bottom-container");
            _topTitleContainer = GraphUtility.GetVisualElement("top-title-container");
            _bottomTitleContainer = GraphUtility.GetVisualElement("bottom-title-container");

            titleContainer.AddToClassList("title-container");
            mainContainer.AddToClassList("main-container");
        }

        private void CreateMarkup()
        {
            var topRow = GraphUtility.GetVisualElement("top-row");
            var centerRow = GraphUtility.GetVisualElement("center-row");
            var bottomRow = GraphUtility.GetVisualElement("bottom-row");
            var flexCol = GraphUtility.GetVisualElement("flex-column");
            var titleFlexCol = GraphUtility.GetVisualElement("title-flex-column");
            
            var titleLabel = titleContainer.Q<Label>("title-label");
            
            if (titleLabel != null)
            {
                titleContainer.Remove(titleLabel);
                _topTitleContainer.Add(titleLabel);
            }
            
            topRow.Add(_topContainer);
            centerRow.Add(_leftContainer);
            centerRow.Add(titleContainer);
            centerRow.Add(_rightContainer);
            bottomRow.Add(_bottomContainer);
            titleFlexCol.Add(_topTitleContainer);
            titleFlexCol.Add(_bottomTitleContainer);
            
            titleContainer.Add(titleFlexCol);

            flexCol.Add(topRow);
            flexCol.Add(centerRow);
            flexCol.Add(bottomRow);

            m_CollapseButton.parent.Remove(m_CollapseButton);
            
            mainContainer.Add(flexCol);
        }
        
        private VisualElement GetPortContainer(Direction direction, Orientation orientation)
        {
            return direction switch
            {
                Direction.Input when orientation == Orientation.Horizontal => _leftContainer,
                Direction.Output when orientation == Orientation.Horizontal => _rightContainer,
                Direction.Input when orientation == Orientation.Vertical => _topContainer,
                _ => _bottomContainer
            };
        }

        private static string GetPortName(Direction direction, Orientation orientation)
        {
            return direction switch
            {
                Direction.Input when orientation == Orientation.Horizontal => _LEFT_PORT_NAME,
                Direction.Output when orientation == Orientation.Horizontal => _RIGHT_PORT_NAME,
                Direction.Input when orientation == Orientation.Vertical => _TOP_PORT_NAME,
                _ => _BOTTOM_PORT_NAME
            };
        }

        private void RefreshHandledScenesLabel(bool isAssetChanged)
        {
            var isSceneAssetExists = SceneAsset != null;
            var message = isSceneAssetExists
                ? $"Handled scene:\n -{SceneAsset.name}"
                : "NO SCENE HANDLED";

            if (isSceneAssetExists)
            {
                ErrorData.RemoveError(ErrorType.EmptySceneAsset);
                SceneAssetGuid = SceneAsset.GetUnityGuid();
            }
            else
            {
                ErrorData.AddError(ErrorType.EmptySceneAsset);
                SceneAssetGuid = null;
            }

            if (isAssetChanged)
                AddScenePreview(WorldGraphEditorSettings.Instance.NodeBackgroundOpacity);
            
            _handledScenesLabel.text = message;
#if WGE_ADDRESSABLES
            RefreshAddressableBadge();
#endif
        }

#if WGE_ADDRESSABLES
        private void CreateAddressableBadge()
        {
            _addressableBadge = new Label("ADR")
            {
                tooltip = "Loaded via Addressables"
            };
            _addressableBadge.AddToClassList("scene-node-addressable-badge");
            _addressableBadge.style.display = DisplayStyle.None;
            _topTitleContainer.Insert(0, _addressableBadge);
        }

        internal void RefreshAddressableBadge()
        {
            if (_addressableBadge == null)
                return;

            var isAddressable = !string.IsNullOrEmpty(SceneAssetGuid) &&
                                AddressablesAddressResolver.IsAddressableScene(SceneAssetGuid);

            _addressableBadge.style.display = isAddressable ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isAddressable)
                return;

            var address = AddressablesAddressResolver.TryResolveSceneAddress(SceneAssetGuid);
            _addressableBadge.tooltip = string.IsNullOrEmpty(address)
                ? "Loaded via Addressables"
                : $"Loaded via Addressables.\nAddress: {address}";
        }
#endif

        public void RemovePort(string guid)
        {
            var port = _ports.FirstOrDefault(item => item.GetGuid() == guid);
            var edges = port.connections.ToList();
            
            foreach (var edge in edges)
            {
                edge.Disconnect();
                edge.RemoveFromHierarchy();
                
                UndoRedoUtility.RemoveEdge(edge.GetGuid());
                UndoRedoUtility.UnRecord(edge, "Edge deleted");
            }

            RemovePortFromContainer(port);
            _ports.Remove(port);
            
            UndoRedoUtility.Record(this, GetPosition().position, "Node changed");
        }

        internal AutoConnectPreview GetAutoConnectPreview(Port existingPort, Vector2 cursorPositionInContent,
            Port snapTarget = null)
        {
            if (snapTarget != null && snapTarget.node == this && !snapTarget.IsAdditional())
            {
                return snapTarget.connected
                    ? new AutoConnectPreview(AutoConnectKind.ConnectOccupied, new PortPreview(snapTarget.portName, snapTarget.portColor))
                    : new AutoConnectPreview(AutoConnectKind.ConnectExisting, new PortPreview(snapTarget.portName, snapTarget.portColor));
            }

            var decision = ResolveAutoConnectDecision(existingPort, cursorPositionInContent);

            if (decision.HasUnconnected)
            {
                var nearest = decision.NearestUnconnected;

                if (nearest != null)
                    return new AutoConnectPreview(AutoConnectKind.ConnectExisting, new PortPreview(nearest.portName, nearest.portColor));

                return new AutoConnectPreview(AutoConnectKind.ConnectExisting);
            }

            var (prev, next) = (decision.Prev, decision.Next);

            if (prev != null && next != null)
                return new AutoConnectPreview(AutoConnectKind.InsertBetween,
                    new PortPreview(prev.portName, prev.portColor), 
                    new PortPreview(next.portName, next.portColor));

            if (prev != null)
                return new AutoConnectPreview(AutoConnectKind.InsertAfter, new PortPreview(prev.portName, prev.portColor));

            if (next != null)
                return new AutoConnectPreview(AutoConnectKind.InsertBefore, new PortPreview(next.portName, next.portColor));

            return new AutoConnectPreview(AutoConnectKind.InsertFirst);
        }

        public Port GetOppositePort(Port existingPort, Vector2 cursorPositionInContent)
        {
            var decision = ResolveAutoConnectDecision(existingPort, cursorPositionInContent);

            if (decision.HasUnconnected)
                return decision.NearestUnconnected;

            var directionName = GetPortName(decision.OppositeDirection, decision.Orientation);
            var insertIndex = GetInsertIndex(decision.Container, decision.Next);

            return InsertPortAt(decision.OppositeDirection, decision.Orientation, decision.Container, directionName,
                insertIndex, false, decision.Prev, decision.Next);
        }

        private readonly struct AutoConnectDecision
        {
            public readonly Direction OppositeDirection;
            public readonly Orientation Orientation;
            public readonly VisualElement Container;
            public readonly bool HasUnconnected;
            public readonly Port NearestUnconnected;
            public readonly Port Prev;
            public readonly Port Next;

            public AutoConnectDecision(Direction oppositeDirection, Orientation orientation, VisualElement container,
                bool hasUnconnected, Port nearestUnconnected, Port prev, Port next)
            {
                OppositeDirection = oppositeDirection;
                Orientation = orientation;
                Container = container;
                HasUnconnected = hasUnconnected;
                NearestUnconnected = nearestUnconnected;
                Prev = prev;
                Next = next;
            }
        }

        private AutoConnectDecision ResolveAutoConnectDecision(Port existingPort, Vector2 cursorPositionInContent)
        {
            var oppositeDirection = existingPort.direction == Direction.Input ? Direction.Output : Direction.Input;
            var orientation = existingPort.orientation;
            var isVertical = orientation == Orientation.Vertical;
            var container = GetPortContainer(oppositeDirection, orientation);

            var unconnected = GetUnConnectedPorts(oppositeDirection, orientation).ToArray();
            Port nearest = null;

            if (unconnected.Length > 0)
            {
                var cursorInContainer = GetCursorInContainerSpace(container, cursorPositionInContent);
                nearest = GetNearestUnconnectedPort(unconnected, container, cursorInContainer, isVertical);
            }

            var (prev, next) = FindNeighbourPorts(container, cursorPositionInContent, isVertical);

            return new AutoConnectDecision(oppositeDirection, orientation, container,
                unconnected.Length > 0, nearest, prev, next);
        }

        private Port InsertPortAt(Direction direction, Orientation orientation, VisualElement container,
            string directionName, int insertIndex, bool isAdditional, Port prev, Port next)
        {
            var guid = System.Guid.NewGuid().ToString();
            var portsCount = container.Query("connector").ToList().Count;
            var portName = $"{directionName} {portsCount}";

            var port = GraphUtility.CreatePort(direction, orientation, portName, guid, isAdditional);
            container.Insert(insertIndex, port);
            InsertPortInList(port, prev, next);

            UndoRedoUtility.Record(this, GetPosition().position, "Node changed");
            SceneNodeInspectorHelper.Instance?.Init(this);

            RefreshExpandedState();
            RefreshPorts();

            return port;
        }

        private void InsertPortInList(Port port, Port prev, Port next)
        {
            if (prev != null)
            {
                var prevIndex = _ports.IndexOf(prev);
                if (prevIndex != -1)
                {
                    _ports.Insert(prevIndex + 1, port);
                    return;
                }
            }

            if (next != null)
            {
                var nextIndex = _ports.IndexOf(next);
                if (nextIndex != -1)
                {
                    _ports.Insert(nextIndex, port);
                    return;
                }
            }

            _ports.Add(port);
        }

        private static int GetInsertIndex(VisualElement container, Port next)
        {
            if (next == null)
                return container.childCount;

            return container.IndexOf(next);
        }

        private Vector2 GetCursorInContainerSpace(VisualElement container, Vector2 cursorPositionInContent)
        {
            var hierarchyParent = hierarchy.parent;
            if (hierarchyParent == null)
                return cursorPositionInContent;

            return hierarchyParent.ChangeCoordinatesTo(container, cursorPositionInContent);
        }

        private static Port GetNearestUnconnectedPort(IReadOnlyList<Port> unconnectedPorts, VisualElement container,
            Vector2 cursorInContainer, bool isVertical)
        {
            var cursorAxis = isVertical ? cursorInContainer.x : cursorInContainer.y;
            Port nearest = null;
            var minDistance = float.MaxValue;

            foreach (var port in unconnectedPorts)
            {
                var capPos = GetPortCapCenterInContainerSpace(port, container);
                var portAxis = isVertical ? capPos.x : capPos.y;
                var distance = Mathf.Abs(portAxis - cursorAxis);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = port;
                }
            }

            return nearest;
        }

        private (Port prev, Port next) FindNeighbourPorts(VisualElement container,
            Vector2 cursorPositionInContent, bool isVertical)
        {
            var cursorInContainer = GetCursorInContainerSpace(container, cursorPositionInContent);
            var cursorX = cursorInContainer.x;
            var cursorY = cursorInContainer.y;

            var ports = isVertical
                ? container.Children().OfType<Port>()
                    .OrderBy(p => GetPortCapCenterInContainerSpace(p, container).x)
                    .ToList()
                : container.Children().OfType<Port>()
                    .OrderBy(p => GetPortCapCenterInContainerSpace(p, container).y)
                    .ToList();

            for (var i = 0; i < ports.Count; i++)
            {
                var capPos = GetPortCapCenterInContainerSpace(ports[i], container);

                if (isVertical && cursorX < capPos.x)
                {
                    var next = ports[i];
                    var previous = i > 0 ? ports[i - 1] : null;
                    return (previous, next);
                }

                if (!isVertical && cursorY < capPos.y)
                {
                    var next = ports[i];
                    var previous = i > 0 ? ports[i - 1] : null;
                    return (previous, next);
                }
            }

            return (ports.LastOrDefault(), null);
        }

        private static Vector2 GetPortCapCenterInContainerSpace(Port port, VisualElement container)
        {
            var cap = port.Q("cap") ?? port.Q(null, "connectorCap");

            if (cap == null)
                return container.WorldToLocal(port.LocalToWorld(port.layout.center));

            var centerInCap = new Vector2(
                cap.layout.xMin + cap.layout.width * 0.5f,
                cap.layout.yMin + cap.layout.height * 0.5f);

            return container.WorldToLocal(cap.LocalToWorld(centerInCap));
        }

        public IEnumerable<Port> GetUnConnectedPorts(Direction direction, Orientation orientation)
        {
            var container = GetPortContainer(direction, orientation);
            return container.Query<Port>().Where(item => !item.connected && !item.IsAdditional()).ToList();
        }
    }
}