using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal abstract class InterfaceReferenceUtility
    {
        private static GUIStyle _labelStyle;

        public static void OnGUI(Rect position, SerializedProperty property, GUIContent label, InterfaceArgs args)
        {
            InitStyleIfNeeded();
            
            var controlId = GUIUtility.GetControlID(FocusType.Passive) - 1;
            var isHovering = position.Contains(Event.current.mousePosition);
            var displayString = property.objectReferenceValue == null || isHovering
                ? $"({args.InterfaceType.Name})"
                : "*";

            DrawInterfaceNameLabel(position, displayString, controlId);
        }

        private static void DrawInterfaceNameLabel(Rect position, string displayString, int controlId)
        {
            if (Event.current.type != UnityEngine.EventType.Repaint)
                return;
            
            const int additionalLeftWidth = 3;
            const int verticalIndent = 1;

            var content = EditorGUIUtility.TrTextContent(displayString);
            var size = _labelStyle.CalcSize(content);
            var labelPos = position;

            labelPos.width = size.x + additionalLeftWidth;
            labelPos.x += position.width - labelPos.width - 18;
            labelPos.height -= verticalIndent * 2;
            labelPos.y += verticalIndent;
            _labelStyle.Draw(labelPos, EditorGUIUtility.TrTextContent(displayString), controlId, DragAndDrop.activeControlID == controlId, false);
        }

        private static void InitStyleIfNeeded()
        {
            if (_labelStyle != null)
                return;

            var style = new GUIStyle(EditorStyles.label)
            {
                font = EditorStyles.objectField.font,
                fontSize = EditorStyles.objectField.fontSize,
                fontStyle = EditorStyles.objectField.fontStyle,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 2, 0, 0),
            };

            _labelStyle = style;
        }
    }
}