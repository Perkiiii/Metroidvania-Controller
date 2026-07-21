using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal sealed class SmartConnectionController
    {
        private readonly WorldBuilderGraphView _graphView;

        private Label _autoConnectHint;
        private IVisualElementScheduledItem _hintPoll;

        private enum EdgeDetachPositionMode
        {
            ControlPoints,
            CandidatePosition
        }

        private const EdgeDetachPositionMode DetachPositionMode = EdgeDetachPositionMode.ControlPoints;

        internal SmartConnectionController(WorldBuilderGraphView graphView)
        {
            _graphView = graphView;

            _hintPoll = _graphView.schedule.Execute(UpdateAutoConnectHint).Every(25);
            ContextualEdge.OnEdgeDetach += OnEdgeDetach;
        }

        internal void Unsubscribe()
        {
            _hintPoll?.Pause();
            ContextualEdge.OnEdgeDetach -= OnEdgeDetach;
        }

        internal List<Port> CollectCompatiblePorts(Port startPort)
        {
            var compatiblePorts = new List<Port>();

            _graphView.ports.ForEach(port =>
            {
                var isPortAndNodeDifferent = startPort != port && startPort.node != port.node;
                var isContainerConditionCorrect = startPort.direction != port.direction &&
                                                  startPort.orientation == port.orientation;

                if (isPortAndNodeDifferent && isContainerConditionCorrect && !port.IsAdditional())
                    compatiblePorts.Add(port);
            });

            return compatiblePorts;
        }

        internal void HideHint()
        {
            if (_autoConnectHint != null)
                _autoConnectHint.style.display = DisplayStyle.None;
        }

        private void OnEdgeDetach(Edge originEdge, DetachFromPanelEvent evt, Vector2 localMousePosition)
        {
            if (_graphView.contentViewContainer == null)
                return;

            var originNodePosition = ResolveDetachNodePosition(originEdge, localMousePosition);
            var targetNode = GraphUtility.GetNodeAtPosition<SceneNode>(_graphView.nodes, originNodePosition);

            if (!IsValidEdgeDetach(originEdge, targetNode))
                return;

            var isInputExists = originEdge.input != null;
            var existingPort = isInputExists ? originEdge.input : originEdge.output;

            if (!CanConnectToNode(existingPort, targetNode))
                return;

            var snapPort = GetEndPortAtPosition(existingPort, originEdge.candidatePosition);
            if (snapPort != null && snapPort.node == targetNode)
                return;

            DisconnectExistingEdge(existingPort);

            var newPort = targetNode.GetOppositePort(existingPort, originNodePosition);
            var newEdge = GraphUtility.CreateEdge(existingPort, newPort, isInputExists);

            _graphView.AddElement(newEdge);
            UndoRedoUtility.RemoveEdge(originEdge.GetGuid());
            GraphUtility.RemoveEdge(originEdge);
        }

        private Vector2 ResolveDetachNodePosition(Edge originEdge, Vector2 edgeLocalPosition)
        {
            return DetachPositionMode switch
            {
                EdgeDetachPositionMode.ControlPoints =>
                    originEdge.ChangeCoordinatesTo(_graphView.contentViewContainer, edgeLocalPosition),
                EdgeDetachPositionMode.CandidatePosition =>
                    _graphView.contentViewContainer.WorldToLocal(originEdge.candidatePosition),
                _ => originEdge.ChangeCoordinatesTo(_graphView.contentViewContainer, edgeLocalPosition)
            };
        }

        private void UpdateAutoConnectHint()
        {
            var dragged = GetActiveDraggedEdge();
            if (dragged == null)
            {
                HideHint();
                return;
            }

            var existingPort = dragged.input ?? dragged.output;
            if (existingPort == null)
            {
                HideHint();
                return;
            }

            var mouseWorld = dragged.candidatePosition;
            var cursorInContent = _graphView.contentViewContainer.WorldToLocal(mouseWorld);
            var snapPort = TryGetSnapPortFromGhostEdge(existingPort, _graphView.edges);

            if (snapPort != null)
            {
                if (snapPort.node is not SceneNode snapNode || !CanConnectToNode(existingPort, snapNode))
                {
                    HideHint();
                    return;
                }

                var snapPreview = snapNode.GetAutoConnectPreview(existingPort, cursorInContent, snapPort);
                ShowHint(FormatPreview(snapPreview), mouseWorld);
                return;
            }

            var targetNode = GraphUtility.GetNodeAtPosition<SceneNode>(_graphView.nodes, cursorInContent);
            if (targetNode == null || !CanConnectToNode(existingPort, targetNode))
            {
                HideHint();
                return;
            }

            var preview = targetNode.GetAutoConnectPreview(existingPort, cursorInContent);
            ShowHint(FormatPreview(preview), mouseWorld);
        }

        private static Port TryGetSnapPortFromGhostEdge(Port draggedPort, IEnumerable<Edge> graphEdges)
        {
            foreach (var edge in graphEdges)
            {
                if (!edge.isGhostEdge)
                    continue;

                if (edge.output == draggedPort)
                    return edge.input;

                if (edge.input == draggedPort)
                    return edge.output;
            }

            return null;
        }

        private Port GetEndPortAtPosition(Port startPort, Vector2 mouseWorldPosition)
        {
            foreach (var port in CollectCompatiblePorts(startPort))
            {
                var bounds = port.worldBound;

                if (port.orientation == Orientation.Horizontal)
                {
                    var padding = bounds.height;

                    if (port.direction == Direction.Input)
                    {
                        bounds.x -= padding;
                    }

                    bounds.width += padding;
                }

                if (bounds.Contains(mouseWorldPosition))
                    return port;
            }

            return null;
        }

        private ContextualEdge GetActiveDraggedEdge()
        {
            foreach (var edge in _graphView.edges.ToList())
            {
                if (edge is ContextualEdge contextualEdge && (contextualEdge.input == null ^ contextualEdge.output == null))
                    return contextualEdge;
            }

            return null;
        }

        private static string FormatPreview(AutoConnectPreview preview)
        {
            var portA = preview.PortA?.Name.SetColor(preview.PortA.Value.Color);
            var portB = preview.PortB?.Name.SetColor(preview.PortB.Value.Color);

            return preview.Kind switch
            {
                AutoConnectKind.ConnectOccupied => $"Connect to \"{portA}\"",
                AutoConnectKind.ConnectExisting => $"Connect to existing port \"{portA}\"",
                AutoConnectKind.InsertBetween => $"New port between \"{portA}\" and \"{portB}\"",
                AutoConnectKind.InsertAfter => $"New port after \"{portA}\"",
                AutoConnectKind.InsertBefore => $"New port before \"{portA}\"",
                _ => "New port (first)"
            };
        }

        private void ShowHint(string text, Vector2 mouseWorldPosition)
        {
            EnsureHintCreated();

            _autoConnectHint.text = text;

            var local = _graphView.WorldToLocal(mouseWorldPosition) + new Vector2(16f, 16f);
            _autoConnectHint.style.left = local.x;
            _autoConnectHint.style.top = local.y;
            _autoConnectHint.style.display = DisplayStyle.Flex;
        }

        private void EnsureHintCreated()
        {
            if (_autoConnectHint != null)
                return;

            _autoConnectHint = new Label
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    display = DisplayStyle.None,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 3,
                    paddingBottom = 3,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.92f),
                    color = Color.white,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    fontSize = 12,
                }
            };

            _graphView.Add(_autoConnectHint);
        }

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
