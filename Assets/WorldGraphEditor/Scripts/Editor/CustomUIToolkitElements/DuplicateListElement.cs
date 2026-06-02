using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class DuplicateListElement : VisualElement
    {
        public DuplicateListElement(string text, GameObject obj)
        {
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.SpaceBetween;
            style.alignItems = Align.Center;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.fontSize = 12;

            var label = new Label(text)
            {
                style =
                {
                    marginTop = -1
                }
            };

            var button = new Button(() => EditorGUIUtility.PingObject(obj))
            {
                text = "Show",
                style =
                {
                    fontSize = 12,
                    height = 16,
                    marginLeft = 8
                }
            };
 
            Add(label);
            Add(button);
        }
    }
}