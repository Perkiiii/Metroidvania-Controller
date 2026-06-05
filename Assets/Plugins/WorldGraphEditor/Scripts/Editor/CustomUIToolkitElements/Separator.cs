using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class Separator : VisualElement
    {
        public Separator()
        {
            style.height = 1;
            style.marginTop = 4;
            style.marginBottom = 4;
            style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1);
        }
    }
}