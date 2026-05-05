using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, ArrowLine, StraightLine, OrthogonalLine, Rectangle, Circle, Diamond, Triangle, TextNode, Text, Image }
        public enum HandlePosition { None, NW, NE, SW, SE }

        public abstract class ShapeBase
        {
            public RectangleF Bounds;
            public Color ShapeColor { get; set; }
            
            // 新增專業外觀屬性
            public float StrokeWidth { get; set; } = 2f;
            public DashStyle StrokeDashStyle { get; set; } = DashStyle.Solid;
            
            [JsonIgnore] 
            public bool IsSelected { get; set; }
            
            public Guid Id { get; set; } = Guid.NewGuid();

            public string Text { get; set; } = "";
            public string FontName { get; set; } = "Arial";
            public float FontSize { get; set; } = 12f;
            public Color FontColor { get; set; } = Color.Black;

            public ShapeBase() { }

            public ShapeBase(PointF start, Color color)
            {
                Bounds = new RectangleF(start.X, start.Y, 0, 0);
                ShapeColor = color;
            }

            public abstract void Draw(Graphics g);

            protected Pen CreatePen()
            {
                return new Pen(ShapeColor, StrokeWidth) { DashStyle = StrokeDashStyle };
            }

            protected void DrawText(Graphics g)
            {
                if (string.IsNullOrEmpty(Text)) return;
                
                using (Font font = new Font(FontName, FontSize))
                using (Brush b = new SolidBrush(FontColor))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(Text, font, b, Bounds, sf);
                }
            }

            public virtual void UpdateEndPoint(PointF pt)
            {
                Bounds = new RectangleF(Bounds.X, Bounds.Y, pt.X - Bounds.X, pt.Y - Bounds.Y);
                NormalizeBounds();
            }

            public void NormalizeBounds()
            {
                float x = Math.Min(Bounds.X, Bounds.Right);
                float y = Math.Min(Bounds.Y, Bounds.Bottom);
                float w = Math.Abs(Bounds.Width);
                float h = Math.Abs(Bounds.Height);
                Bounds = new RectangleF(x, y, w, h);
            }

            public virtual void Move(float dx, float dy)
            {
                Bounds.Offset(dx, dy);
            }

            public virtual bool HitTest(PointF pt)
            {
                RectangleF hitBounds = Bounds;
                hitBounds.Inflate(5, 5);
                return hitBounds.Contains(pt);
            }
            
            public void DrawSelection(Graphics g)
            {
                if (!IsSelected) return;

                using (Pen p = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, Rectangle.Round(Bounds));
                }

                float s = 6;
                PointF[] corners = new PointF[]
                {
                    new PointF(Bounds.Left, Bounds.Top),
                    new PointF(Bounds.Right, Bounds.Top),
                    new PointF(Bounds.Left, Bounds.Bottom),
                    new PointF(Bounds.Right, Bounds.Bottom)
                };

                foreach (var pt in corners)
                {
                    g.FillRectangle(Brushes.White, pt.X - s/2, pt.Y - s/2, s, s);
                    g.DrawRectangle(Pens.DodgerBlue, pt.X - s/2, pt.Y - s/2, s, s);
                }
            }

            public HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected) return HandlePosition.None;

                float s = 6;
                if (new RectangleF(Bounds.Left - s/2, Bounds.Top - s/2, s, s).Contains(pt)) return HandlePosition.NW;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Top - s/2, s, s).Contains(pt)) return HandlePosition.NE;
                if (new RectangleF(Bounds.Left - s/2, Bounds.Bottom - s/2, s, s).Contains(pt)) return HandlePosition.SW;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Bottom - s/2, s, s).Contains(pt)) return HandlePosition.SE;
                
                return HandlePosition.None;
            }

            public virtual PointF[] GetAnchors()
            {
                return new PointF[]
                {
                    new PointF(Bounds.Left + Bounds.Width/2, Bounds.Top),     // 上
                    new PointF(Bounds.Left + Bounds.Width/2, Bounds.Bottom),  // 下
                    new PointF(Bounds.Left, Bounds.Top + Bounds.Height/2),    // 左
                    new PointF(Bounds.Right, Bounds.Top + Bounds.Height/2)    // 右
                };
            }
        }

        // --- 實體圖形 ---

        public class RectShape : ShapeBase
        { 
            public RectShape() { } 
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                using (Pen p = CreatePen()) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawText(g);
            }
        }

        public class CircleShape : ShapeBase
        {
            public CircleShape() { } 
            public CircleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                using (Pen p = CreatePen()) g.DrawEllipse(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawText(g);
            }
        }

        // 新增：菱形 (流程圖的判斷式)
        public class DiamondShape : ShapeBase
        {
            public DiamondShape() { }
            public DiamondShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                PointF[] pts = new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Y + Bounds.Height / 2),
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Y + Bounds.Height / 2)
                };
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        // 新增：三角形
        public class TriangleShape : ShapeBase
        {
            public TriangleShape() { }
            public TriangleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                PointF[] pts = new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Bottom)
                };
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class TextNodeShape : ShapeBase
        {
            public bool IsTransparent { get; set; } = false;
            public TextNodeShape() { } 
            public TextNodeShape(PointF start, Color color, bool transparent) : base(start, color)
            {
                IsTransparent = transparent;
                Text = "點兩下編輯";
            }
            public override void Draw(Graphics g)
            {
                if (!IsTransparent)
                {
                    using (Pen p = CreatePen()) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                }
                DrawText(g);
            }
        }

        public class ImageShape : ShapeBase
        {
            public string Base64Image { get; set; }
            [JsonIgnore] private Bitmap _imgCache;
            
            public ImageShape() { }
            public ImageShape(PointF start, Bitmap img) : base(start, Color.Black)
            {
                _imgCache = img;
                using (var ms = new System.IO.MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    Base64Image = Convert.ToBase64String(ms.ToArray());
                }
                Bounds.Width = img.Width; Bounds.Height = img.Height;
            }
            public override void Draw(Graphics g)
            {
                if (_imgCache == null && !string.IsNullOrEmpty(Base64Image))
                {
                    using (var ms = new System.IO.MemoryStream(Convert.FromBase64String(Base64Image)))
                        _imgCache = new Bitmap(ms);
                }
                if (_imgCache != null) g.DrawImage(_imgCache, Bounds);
                DrawText(g);
            }
        }

        public class ConnectorShape : ShapeBase
        {
            public Guid SourceId { get; set; }
            public Guid TargetId { get; set; }
            public PointF StartPt { get; set; }
            public PointF EndPt { get; set; }
            public bool HasArrow { get; set; }
            public bool IsOrthogonal { get; set; } // 90度折線

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow, bool orthogonal = false) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow; IsOrthogonal = orthogonal;
            }
            
            public override void UpdateEndPoint(PointF pt) { EndPt = pt; }
            public override bool HitTest(PointF pt) { return false; } // 線條不開放點選
            
            public void DrawDynamic(Graphics g, PointF p1, PointF p2)
            {
                using (Pen p = CreatePen())
                {
                    if (HasArrow)
                    {
                        GraphicsPath capPath = new GraphicsPath();
                        capPath.AddLine(new PointF(-2, -2), new PointF(0, 0));
                        capPath.AddLine(new PointF(0, 0), new PointF(2, -2));
                        p.CustomEndCap = new CustomLineCap(null, capPath);
                    }

                    if (IsOrthogonal)
                    {
                        // 畫 90 度折線 (找中點轉彎)
                        float midX = p1.X + (p2.X - p1.X) / 2;
                        PointF[] pts = new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                        g.DrawLines(p, pts);
                    }
                    else
                    {
                        // 畫直線
                        g.DrawLine(p, p1, p2);
                    }
                }
            }
            public override void Draw(Graphics g) { }
        }

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
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.Diamond: return new DiamondShape(start, color);
                    case ShapeType.Triangle: return new TriangleShape(start, color);
                    case ShapeType.TextNode: return new TextNodeShape(start, color, false);
                    case ShapeType.Text: return new TextNodeShape(start, color, true);
                    case ShapeType.Image: return new ImageShape(start, img);
                    default: return null;
                }
            }
        }
    }
}
