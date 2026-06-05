using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class ColoredText : TextElement
    {
        public ColoredText(string text, Color barColor, float barWidth = 4)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 4;
        
            var bar = new VisualElement
            {
                style =
                {
                    width = barWidth,
                    backgroundColor = barColor,
                    height = Length.Percent(100),
                    marginRight = 6
                }
            };

            var label = new TextElement
            {
                text = text,
                
                style = 
                {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.UpperLeft,
                    backgroundColor = new StyleColor(ColorStyleUtility.ColoredTextBackgroundColor),
                    flexGrow = 1
                }
            };
            
            style.backgroundColor = new StyleColor(ColorStyleUtility.ColoredTextBackgroundColor);

            Add(bar);
            Add(label);
        }
    }
}