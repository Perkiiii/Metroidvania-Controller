using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class FoldoutWithIcon : Foldout
    {
        public FoldoutWithIcon(VisualElement icon, Label label)
        {
            value = false;
            text = "";

            var header = this.Q(className: "unity-base-field__input");

            header.Add(icon);
            header.Add(label);
        }
    }
}