using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal abstract class EdgeIcon : VisualElement
    {
        protected readonly float IconSize;
        protected readonly float HalfSize;

        protected Vector2 Position { get; private set; }

        protected Color Color { get; private set; }

        private float _rotation;
        private readonly Vector2 _pivot = Vector2.zero;

        protected EdgeIcon(float iconSize)
        {
            IconSize = iconSize;
            HalfSize = iconSize / 2;
            pickingMode = PickingMode.Ignore;
        }

        public void Update(Vector2 position, float rotation, Color color)
        {
            Position = position;
            _rotation = rotation;
            Color = color;
            MarkDirtyRepaint();
        }
        
        protected Vector2 RotatePointAroundPivot(Vector2 point)
        {
            var rad = _rotation * Mathf.Deg2Rad;
            var sin = Mathf.Sin(rad);
            var cos = Mathf.Cos(rad);
            
            point -= _pivot;
            
            var x = point.x * cos - point.y * sin;
            var y = point.x * sin + point.y * cos;
            
            point.x = x + _pivot.x;
            point.y = y + _pivot.y;
            return point;
        }
    }
}