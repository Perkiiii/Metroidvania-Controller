using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal readonly struct PortPreview
    {
        public readonly string Name;
        public readonly Color Color;

        public PortPreview(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}