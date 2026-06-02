using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class ColoredLabel : VisualElement
    {
        public ColoredLabel(string text, Color barColor, int barWidth = 4)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            
            var label = new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Normal
                }
            };

            var colorBar = new VisualElement
            {
                style =
                {
                    backgroundColor = barColor,
                    width = barWidth,
                    height = 16,
                    marginRight = 4,
                    unityBackgroundImageTintColor = barColor
                }
            };

            style.backgroundColor = new StyleColor(new Color(0.21f, 0.21f, 0.21f));
            
            Add(colorBar);
            Add(label);
        }
    }
}