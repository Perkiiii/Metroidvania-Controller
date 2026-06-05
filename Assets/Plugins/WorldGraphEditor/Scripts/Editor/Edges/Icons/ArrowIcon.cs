using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class ArrowIcon : EdgeIcon
    {
        public ArrowIcon(float arrowSize) : base(arrowSize)
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;

            var p1 = new Vector2(-HalfSize, HalfSize);
            var p2 = new Vector2(-HalfSize, -HalfSize);
            var p3 = new Vector2(HalfSize, 0f);

            p1 = RotatePointAroundPivot(p1) + Position;
            p2 = RotatePointAroundPivot(p2) + Position;
            p3 = RotatePointAroundPivot(p3) + Position;

            painter.fillColor = Color;

            painter.BeginPath();
            painter.MoveTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.ClosePath();
            painter.Fill();
        }
    }
}