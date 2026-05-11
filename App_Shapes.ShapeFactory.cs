// ============================================================
// FILE: App_Shapes.ShapeFactory.cs
// ============================================================

using System;
using System.Drawing;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public static class ShapeFactory
        {
            public static ShapeBase CreateShape(ShapeType type, PointF start, Color color, Bitmap img = null)
            {
                switch (type)
                {
                    case ShapeType.ArrowLine: return new ConnectorShape(start, color, true, false);
                    case ShapeType.StraightLine: return new ConnectorShape(start, color, false, false);
                    case ShapeType.OrthogonalLine: return new ConnectorShape(start, color, true, true);
                    case ShapeType.Rectangle: return new RectShape(start, color);
                    case ShapeType.RoundedRectangle: return new RoundedRectShape(start, color);
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.Arc: return new ArcShape(start, color);
                    case ShapeType.Diamond: return new DiamondShape(start, color);
                    case ShapeType.Triangle: return new TriangleShape(start, color);
                    case ShapeType.Pentagon: return new PentagonShape(start, color);
                    case ShapeType.Hexagon: return new HexagonShape(start, color);
                    case ShapeType.Star: return new StarShape(start, color);
                    
                    case ShapeType.Parallelogram: return new ParallelogramShape(start, color);
                    case ShapeType.Cylinder: return new CylinderShape(start, color);
                    case ShapeType.Document: return new DocumentShape(start, color);
                    case ShapeType.BlockArrow: return new BlockArrowShape(start, color);
                    
                    case ShapeType.DoubleArrow: return new DoubleArrowShape(start, color);
                    case ShapeType.BraceLeft: return new BraceLeftShape(start, color);
                    case ShapeType.BraceRight: return new BraceRightShape(start, color);
                    case ShapeType.Branch1To2: return new Branch1To2Shape(start, color);
                    case ShapeType.Branch1To3: return new Branch1To3Shape(start, color);
                    case ShapeType.Branch1To4: return new Branch1To4Shape(start, color);

                    case ShapeType.TextNode: return new TextNodeShape(start, color, false);
                    case ShapeType.Text: return new TextNodeShape(start, color, true);
                    case ShapeType.Image: return new ImageShape(start, img);
                    case ShapeType.Freehand: return new FreehandShape(start, color);
                    case ShapeType.BezierPen: return new BezierShape(start, color);
                    default: return null;
                }
            }
        }
    }
}
