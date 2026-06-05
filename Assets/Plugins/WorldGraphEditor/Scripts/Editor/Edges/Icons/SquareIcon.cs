using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class SquareIcon : EdgeIcon
    {
        public SquareIcon(float iconSize) : base(iconSize)
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;

            var topLeft = new Vector2(-HalfSize, -HalfSize);
            var topRight = new Vector2(HalfSize, -HalfSize);
            var bottomRight = new Vector2(HalfSize, HalfSize);
            var bottomLeft = new Vector2(-HalfSize, HalfSize);

            topLeft = RotatePointAroundPivot(topLeft) + Position;
            topRight = RotatePointAroundPivot(topRight) + Position;
            bottomRight = RotatePointAroundPivot(bottomRight) + Position;
            bottomLeft = RotatePointAroundPivot(bottomLeft) + Position;

            painter.fillColor = Color;

            painter.BeginPath();
            painter.MoveTo(topLeft);
            painter.LineTo(topRight);
            painter.LineTo(bottomRight);
            painter.LineTo(bottomLeft);
            painter.ClosePath();
            painter.Fill();
        }
    }
}