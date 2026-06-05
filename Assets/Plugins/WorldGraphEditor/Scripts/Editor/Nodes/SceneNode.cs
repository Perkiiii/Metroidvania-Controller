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
            _handledScenesLabel = new Label();
            _bottomTitleContainer.Add(_handledScenesLabel);
            GraphUtility.AddStyleSheet(this, "Scripts/Editor/EditorWindows/Styles/SceneNode.uss");

            RegisterCallback<ContextualMenuPopulateEvent>(OnContextManuPopulate);
            RefreshHandledScenesLabel(true);
            RefreshExpandedState();
            RefreshPorts();
        }

        public void TryAddScenePreview(float opacity)
        {
            var alpha = 1 - Mathf.Clamp01(opacity);
            var sprite = SceneScreenshotUtility.GetSpriteForScreenshot(SceneAssetGuid);

            if (sprite == null)
                return;
            
            mainContainer.style.backgroundImage = Background.FromSprite(sprite);
            mainContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 1f, 1f, alpha));

            // InvalidOperationException: EnsureRunningOnMainThread can only be called from the main thread

            /*mainContainer.style.backgroundImage = texture;
            mainContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 1f, 1f, 0.45f));*/
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
                ? $"Handled scene:\n -{SceneAsset.GetFormattedPath()}"
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
                TryAddScenePreview(WorldGraphEditorSettings.Instance.NodeBackgroundOpacity);
            
            _handledScenesLabel.text = message;
        }

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

        public Port GetOppositePort(Port existingPort)
        {
            var oppositeDirection = existingPort.direction == Direction.Input ? Direction.Output : Direction.Input;
            var orientation = existingPort.orientation;

            var container = GetPortContainer(oppositeDirection, orientation);
            var directionName = GetPortName(oppositeDirection, orientation);

            var unconnectedPorts = GetUnConnectedPorts(oppositeDirection, orientation).ToArray();
            
            if (unconnectedPorts.Length > 0)
                return unconnectedPorts.First();
                
            var port = AddPort(oppositeDirection, orientation, container, directionName, false);
            return port;
        }

        public bool ContainsUnConnectedPort(Direction direction, Orientation orientation)
        {
            return GetUnConnectedPorts(direction, orientation).Any();
        }

        public IEnumerable<Port> GetUnConnectedPorts(Direction direction, Orientation orientation)
        {
            var container = GetPortContainer(direction, orientation);
            return container.Query<Port>().Where(item => !item.connected && !item.IsAdditional()).ToList();
        }
    }
}
