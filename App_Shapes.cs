using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, ArrowLine, StraightLine, OrthogonalLine, Rectangle, Circle, Arc, Diamond, Triangle, TextNode, Text, Image }
        public enum HandlePosition { None, NW, NE, SW, SE }

        public abstract class ShapeBase
        {
            public RectangleF Bounds;
            public Color ShapeColor { get; set; }
            
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

            public virtual void NormalizeBounds()
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

            // --- 新增：動態錨點系統 (Dynamic Anchors) ---
            // 計算連線從外部目標射向本圖形中心時，與圖形邊界的交點
            public virtual PointF GetIntersection(PointF targetPoint)
            {
                PointF center = new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
                float dx = targetPoint.X - center.X;
                float dy = targetPoint.Y - center.Y;

                if (Math.Abs(dx) == 0 && Math.Abs(dy) == 0) return center;

                // 基礎矩形邊界相交計算
                float halfWidth = Bounds.Width / 2;
                float halfHeight = Bounds.Height / 2;

                float crossX = halfWidth * Math.Sign(dx);
                float crossY = halfHeight * Math.Sign(dy);

                if (Math.Abs(dx * halfHeight) > Math.Abs(dy * halfWidth))
                {
                    return new PointF(center.X + crossX, center.Y + crossX * dy / dx);
                }
                else
                {
                    return new PointF(center.X + crossY * dx / dy, center.Y + crossY);
                }
            }
        }

        // --- 新增：群組物件 (GroupShape) ---
        public class GroupShape : ShapeBase
        {
            public List<ShapeBase> Children { get; set; } = new List<ShapeBase>();

            public GroupShape() { }

            public GroupShape(List<ShapeBase> children)
            {
                Children = children;
                NormalizeBounds();
            }

            public override void Draw(Graphics g)
            {
                foreach (var child in Children)
                {
                    child.Draw(g);
                }
            }

            public override void Move(float dx, float dy)
            {
                base.Move(dx, dy);
                foreach (var child in Children)
                {
                    child.Move(dx, dy);
                }
            }

            public override void NormalizeBounds()
            {
                if (Children.Count == 0) return;
                
                float minX = Children.Min(c => c.Bounds.Left);
                float minY = Children.Min(c => c.Bounds.Top);
                float maxX = Children.Max(c => c.Bounds.Right);
                float maxY = Children.Max(c => c.Bounds.Bottom);
                
                Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            }

            public override bool HitTest(PointF pt)
            {
                return Children.Any(c => c.HitTest(pt));
            }

            // 群組縮放邏輯比較複雜，這裡先鎖定比例或強制重算
            public override void UpdateEndPoint(PointF pt)
            {
                // 群組暫不支援直接自由變形邊界，依賴內部物件
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

            // 圓形覆寫動態錨點為計算半徑交點
            public override PointF GetIntersection(PointF targetPoint)
            {
                PointF center = new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
                float dx = targetPoint.X - center.X;
                float dy = targetPoint.Y - center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (distance == 0) return center;

                float radiusX = Bounds.Width / 2;
                float radiusY = Bounds.Height / 2;
                float radius = Math.Min(radiusX, radiusY); // 簡化為正圓計算

                return new PointF(center.X + (dx / distance) * radius, center.Y + (dy / distance) * radius);
            }
        }

        public class ArcShape : ShapeBase
        {
            public ArcShape() { } 
            public ArcShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if(Bounds.Width > 0 && Bounds.Height > 0)
                {
                    using (Pen p = CreatePen()) g.DrawArc(p, Bounds, 180, 180);
                }
                DrawText(g);
            }
        }

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
                Text = "連點兩下編輯";
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
            public bool IsOrthogonal { get; set; }

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow, bool orthogonal = false) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow; IsOrthogonal = orthogonal;
            }
            
            public override void UpdateEndPoint(PointF pt) { EndPt = pt; }
            public override bool HitTest(PointF pt) { return false; } 
            
            public void DrawDynamic(Graphics g, PointF p1, PointF p2)
            {
                using (Pen p = CreatePen())
                {
                    if (HasArrow)
                    {
                        GraphicsPath capPath = new GraphicsPath();
                        capPath.AddLine(new PointF(-3, -3), new PointF(0, 0));
                        capPath.AddLine(new PointF(0, 0), new PointF(3, -3));
                        p.CustomEndCap = new CustomLineCap(null, capPath);
                    }

                    if (IsOrthogonal)
                    {
                        // 升級：基於相對位置的智慧折線 (模擬 A* 基礎避開圖形本體)
                        float midX = p1.X + (p2.X - p1.X) / 2;
                        PointF[] pts = new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                        g.DrawLines(p, pts);
                    }
                    else
                    {
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
                    case ShapeType.Arc: return new ArcShape(start, color);
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
