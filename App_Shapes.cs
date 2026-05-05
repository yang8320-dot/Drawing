/*
 * 檔案功能：定義各種可繪製的圖形元件 (線條、矩形、圓形等)
 * 對應選單：無
 * 對應資料庫：無
 * 資料表名稱：無
 */
using System;
using System.Drawing;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, Line, Rectangle, Circle, TextNode }

        public interface IShape
        {
            Color ShapeColor { get; set; }
            void UpdateEndPoint(PointF point);
            void Draw(Graphics g);
        }

        public static class ShapeFactory
        {
            public static IShape CreateShape(ShapeType type, PointF start, Color color)
            {
                switch (type)
                {
                    case ShapeType.Line: return new LineShape(start, color);
                    case ShapeType.Rectangle: return new RectShape(start, color);
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.TextNode: return new TextNodeShape(start, color); // 用於組織圖/心智圖
                    default: return null;
                }
            }
        }

        // 線條類別
        public class LineShape : IShape
        {
            public PointF Start { get; set; }
            public PointF End { get; set; }
            public Color ShapeColor { get; set; }

            public LineShape(PointF start, Color color) { Start = start; End = start; ShapeColor = color; }
            public void UpdateEndPoint(PointF pt) { End = pt; }
            public void Draw(Graphics g) { using (Pen p = new Pen(ShapeColor, 2)) g.DrawLine(p, Start, End); }
        }

        // 矩形類別
        public class RectShape : IShape
        {
            public PointF Start { get; set; }
            public PointF End { get; set; }
            public Color ShapeColor { get; set; }

            public RectShape(PointF start, Color color) { Start = start; End = start; ShapeColor = color; }
            public void UpdateEndPoint(PointF pt) { End = pt; }
            public void Draw(Graphics g)
            {
                float x = Math.Min(Start.X, End.X);
                float y = Math.Min(Start.Y, End.Y);
                float w = Math.Abs(Start.X - End.X);
                float h = Math.Abs(Start.Y - End.Y);
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, x, y, w, h);
            }
        }
        
        // 圓形類別
        public class CircleShape : IShape
        {
            public PointF Start { get; set; }
            public PointF End { get; set; }
            public Color ShapeColor { get; set; }

            public CircleShape(PointF start, Color color) { Start = start; End = start; ShapeColor = color; }
            public void UpdateEndPoint(PointF pt) { End = pt; }
            public void Draw(Graphics g)
            {
                float x = Math.Min(Start.X, End.X);
                float y = Math.Min(Start.Y, End.Y);
                float w = Math.Abs(Start.X - End.X);
                float h = Math.Abs(Start.Y - End.Y);
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawEllipse(p, x, y, w, h);
            }
        }

        // 文字節點 (用於架構圖)
        public class TextNodeShape : IShape
        {
            public PointF Start { get; set; }
            public PointF End { get; set; }
            public Color ShapeColor { get; set; }
            public string Text { get; set; } = "Node";

            public TextNodeShape(PointF start, Color color) { Start = start; End = start; ShapeColor = color; }
            public void UpdateEndPoint(PointF pt) { End = pt; }
            public void Draw(Graphics g)
            {
                float x = Math.Min(Start.X, End.X);
                float y = Math.Min(Start.Y, End.Y);
                float w = Math.Abs(Start.X - End.X);
                float h = Math.Abs(Start.Y - End.Y);
                
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, x, y, w, h);
                using (Font font = new Font("Arial", 12))
                using (Brush b = new SolidBrush(ShapeColor))
                {
                    g.DrawString(Text, font, b, new RectangleF(x, y, w, h));
                }
            }
        }
    }
}
