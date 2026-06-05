using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class ColoredFoldout : Foldout
    {
        public ColoredFoldout(Label label, Color barColor, int barWidth = 4)
        {
            value = false;
            text = "";

            var header = this.Q(className: "unity-base-field__input");

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
            
            header.Add(bar);
            header.Add(label);
        }
    }
}