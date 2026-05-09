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
                    case ShapeType.Cloud: return new CloudShape(start, color);
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
