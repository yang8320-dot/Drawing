using System;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public enum ShapeType { Pointer, HandPan, FormatPainter, ArrowLine, StraightLine, OrthogonalLine, Rectangle, RoundedRectangle, Circle, Arc, Diamond, Triangle, Pentagon, Hexagon, Star, Cloud, TextNode, Text, Image, Freehand, BezierPen }
        public enum HandlePosition { None, NW, N, NE, W, E, SW, S, SE, Rotate, StartPoint, EndPoint }
        public enum AnchorPosition { Auto, Top, Bottom, Left, Right }
        public enum TextAlign { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }
        public enum BrushType { Solid, LinearGradient }
    }
}
