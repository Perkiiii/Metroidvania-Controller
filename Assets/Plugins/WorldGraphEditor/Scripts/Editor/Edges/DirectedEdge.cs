using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class DirectedEdge : Edge
    {
        public bool IsFacingRight { get; private set; } = true;
        public TransitionType TransitionType => _isShortcut ? TransitionType.Shortcut : TransitionType.OneWay;
        
        private const float _ICONS_SIZE = 15f;
        private const float _MIN_DISTANCE_BETWEEN_ICONS = 20f;
        private const float _FIRST_T_POSITION = .45f; 
        private const float _SECOND_T_POSITION = .55f; 
        
        private bool _isShortcut;

        private ArrowIcon _arrowIcon;
        private SquareIcon _squareIcon;

        private EdgeIcon _firstIcon;
        private EdgeIcon _secondIcon;

        public DirectedEdge(bool isShortcut, bool isFacingRight, string guid)
        {
            _isShortcut = isShortcut;
            IsFacingRight = isFacingRight;
            
            CreateIcons();
            ChangeIcons();
            
            edgeControl.RegisterCallback<GeometryChangedEvent>(OnEdgeGeometryChanged);
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextManuPopulate);
            this.SetGuid(guid);
            
            styleSheets.Add(WGEAssetPathUtility.LoadStyleSheet("Scripts/Editor/EditorWindows/Styles/CustomEdgeStyle.uss"));
            AddToClassList("selected-edge");
        }

        public override void OnSelected()
        {
            base.OnSelected();
            UndoRedoUtility.SetSelected(this.GetGuid());
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            UndoRedoUtility.SetUnselected(this.GetGuid());
        }

        private void CreateIcons()
        {
            _firstIcon = new ArrowIcon(_ICONS_SIZE);
            _arrowIcon = new ArrowIcon(_ICONS_SIZE);
            _squareIcon = new SquareIcon(_ICONS_SIZE);
            
            Add(_firstIcon);
        }

        private void OnContextManuPopulate(ContextualMenuPopulateEvent evt)
        {
            var shortcutStatus = _isShortcut ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
            var singleDirectedStatus = _isShortcut ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Checked;

            evt.menu.AppendAction("As Shortcut", _ => SetMode(true), shortcutStatus);
            evt.menu.AppendAction("As One-Way", _ => SetMode(false), singleDirectedStatus);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Change Direction", _ => ChangeDirection());
            evt.menu.AppendAction("Make Undirected", _ => MakeUndirected());
        }

        private void SetMode(bool isShortcut)
        {
            _isShortcut = isShortcut;
            ChangeIcons();
            UpdateIcons();

            var mode = isShortcut ? "Shortcut" : "One-Way";
            UndoRedoUtility.Record(this, $"Edge mode changed to '{mode}'");
        }

        private void ChangeIcons()
        {
            EdgeIcon toHide;

            if (_isShortcut)
            {
                _secondIcon = _squareIcon;
                toHide = _arrowIcon;
            }
            else
            {
                _secondIcon = _arrowIcon;
                toHide = _squareIcon;
            }
            
            Add(_secondIcon);
            toHide.RemoveFromHierarchy();
        }          
        
        private void ChangeDirection()
        {
            IsFacingRight = !IsFacingRight;
            UpdateIcons();

            var direction = IsFacingRight ? "Right" : "Left";
            UndoRedoUtility.Record(this, $"Edge direction changed to '{direction}'");
        }

        private void MakeUndirected()
        {
            var inputPort = input;
            var outputPort = output;
            
            var newEdge = new ContextualEdge(this.GetGuid())
            {
                input = inputPort,
                output = outputPort
            };

            inputPort?.Connect(newEdge);
            outputPort?.Connect(newEdge);
            
            UndoRedoUtility.Record(newEdge, "Edge marked as Contextual");
            UndoRedoUtility.Replace(newEdge);
            parent.Add(newEdge);
            this.Disconnect();
            RemoveFromHierarchy();
        }

        private void OnEdgeGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateIcons();
        }
        
        private void UpdateIcons()
        {
            var edgeStartPoint = edgeControl.controlPoints[1];
            var edgeEndPoint = edgeControl.controlPoints[^2];
            var startPortColor = edgeControl.fromCapColor;
            var endPortColor = edgeControl.toCapColor;
            
            var edgeLength = Vector2.Distance(edgeStartPoint, edgeEndPoint);
            var adjustedTPositions = AdjustTPositions(_FIRST_T_POSITION, _SECOND_T_POSITION, edgeLength, _MIN_DISTANCE_BETWEEN_ICONS);
            
            var firstTPosition = IsFacingRight ? adjustedTPositions.firstT : adjustedTPositions.secondT;
            var secondTPosition = IsFacingRight ? adjustedTPositions.secondT : adjustedTPositions.firstT;

            SetIconData(edgeStartPoint, edgeEndPoint, startPortColor, endPortColor, _firstIcon, firstTPosition);
            SetIconData(edgeStartPoint, edgeEndPoint, startPortColor, endPortColor, _secondIcon, secondTPosition);
        }
        
        private void SetIconData(Vector2 edgeStartPoint, Vector2 edgeEndPoint, Color startPortColor, Color endPortColor, EdgeIcon icon, float tPosition)
        {
            var arrowPosition = Vector2.Lerp(edgeStartPoint, edgeEndPoint, tPosition);
            var arrowColor = Color.Lerp(startPortColor, endPortColor, tPosition);

            var direction = (edgeEndPoint - edgeStartPoint).normalized;
            direction = IsFacingRight ? direction : -direction;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            icon.Update(arrowPosition, angle, arrowColor);
        }
        
        private static (float firstT, float secondT) AdjustTPositions(float firstT, float secondT, float edgeLength, float minDistance)
        {
            var distanceBetweenIcons = Mathf.Abs(secondT - firstT) * edgeLength;
            
            if (distanceBetweenIcons > minDistance)
                return (firstT, secondT);
            
            var adjustment = (minDistance - distanceBetweenIcons) / edgeLength;
            
            if (firstT < secondT)
            {
                firstT -= adjustment / 2;
                secondT += adjustment / 2;
            }
            else
            {
                firstT += adjustment / 2;
                secondT -= adjustment / 2;
            }
            
            firstT = Mathf.Clamp(firstT, 0f, 1f);
            secondT = Mathf.Clamp(secondT, 0f, 1f);

            return (firstT, secondT);
        }
    }
}