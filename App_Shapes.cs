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
        // 升級：擴充為 8 個縮放點與 1 個旋轉控制點
        public enum HandlePosition { None, NW, N, NE, W, E, SW, S, SE, Rotate }
        // 新增：固定連線錨點
        public enum AnchorPosition { Auto, Top, Bottom, Left, Right }

        public abstract class ShapeBase
        {
            public RectangleF Bounds;
            public Color ShapeColor { get; set; }
            
            public float StrokeWidth { get; set; } = 2f;
            public DashStyle StrokeDashStyle { get; set; } = DashStyle.Solid;
            
            // 新增：旋轉角度
            public float RotationAngle { get; set; } = 0f;
            
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

            // 新增：畫布轉換處理 (處理旋轉)
            public void DrawWithTransform(Graphics g)
            {
                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                Draw(g);

                g.Transform = oldMatrix;
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

            // 支援 8 點縮放的新版邊界更新
            public virtual void SetBounds(RectangleF newBounds)
            {
                Bounds = newBounds;
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

            public PointF GetCenter()
            {
                return new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
            }

            // 新增：處理旋轉後的碰撞偵測
            public virtual bool HitTest(PointF pt)
            {
                PointF rotatedPt = RotatePoint(pt, GetCenter(), -RotationAngle);
                RectangleF hitBounds = Bounds;
                hitBounds.Inflate(5, 5);
                return hitBounds.Contains(rotatedPt);
            }
            
            // 升級：繪製 8 個縮放點與旋轉點
            public void DrawSelection(Graphics g)
            {
                if (!IsSelected) return;

                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                using (Pen p = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, Rectangle.Round(Bounds));
                }

                float s = 8;
                PointF[] corners = new PointF[]
                {
                    new PointF(Bounds.Left, Bounds.Top),      // NW
                    new PointF(center.X, Bounds.Top),         // N
                    new PointF(Bounds.Right, Bounds.Top),     // NE
                    new PointF(Bounds.Right, center.Y),       // E
                    new PointF(Bounds.Right, Bounds.Bottom),  // SE
                    new PointF(center.X, Bounds.Bottom),      // S
                    new PointF(Bounds.Left, Bounds.Bottom),   // SW
                    new PointF(Bounds.Left, center.Y)         // W
                };

                foreach (var pt in corners)
                {
                    g.FillRectangle(Brushes.White, pt.X - s/2, pt.Y - s/2, s, s);
                    g.DrawRectangle(Pens.DodgerBlue, pt.X - s/2, pt.Y - s/2, s, s);
                }

                // 旋轉控制點
                PointF rotatePt = new PointF(center.X, Bounds.Top - 25);
                g.DrawLine(Pens.DodgerBlue, center.X, Bounds.Top, rotatePt.X, rotatePt.Y);
                g.FillEllipse(Brushes.LightGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);
                g.DrawEllipse(Pens.DarkGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);

                g.Transform = oldMatrix;
            }

            public HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected) return HandlePosition.None;

                PointF center = GetCenter();
                PointF rotatedPt = RotatePoint(pt, center, -RotationAngle);

                float s = 10;
                // 旋轉點
                if (new RectangleF(center.X - s, Bounds.Top - 25 - s, s * 2, s * 2).Contains(rotatedPt)) return HandlePosition.Rotate;

                // 8 個縮放點
                if (new RectangleF(Bounds.Left - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.NW;
                if (new RectangleF(center.X - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.N;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.NE;
                if (new RectangleF(Bounds.Right - s/2, center.Y - s/2, s, s).Contains(rotatedPt)) return HandlePosition.E;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.SE;
                if (new RectangleF(center.X - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.S;
                if (new RectangleF(Bounds.Left - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.SW;
                if (new RectangleF(Bounds.Left - s/2, center.Y - s/2, s, s).Contains(rotatedPt)) return HandlePosition.W;
                
                return HandlePosition.None;
            }

            // 新增：獲取固定錨點位置
            public PointF GetAnchorPoint(AnchorPosition pos)
            {
                PointF pt = GetCenter();
                switch (pos)
                {
                    case AnchorPosition.Top: pt = new PointF(pt.X, Bounds.Top); break;
                    case AnchorPosition.Bottom: pt = new PointF(pt.X, Bounds.Bottom); break;
                    case AnchorPosition.Left: pt = new PointF(Bounds.Left, pt.Y); break;
                    case AnchorPosition.Right: pt = new PointF(Bounds.Right, pt.Y); break;
                }
                return RotatePoint(pt, GetCenter(), RotationAngle);
            }

            public virtual PointF GetIntersection(PointF targetPoint)
            {
                PointF center = GetCenter();
                PointF localTarget = RotatePoint(targetPoint, center, -RotationAngle);
                
                float dx = localTarget.X - center.X;
                float dy = localTarget.Y - center.Y;

                if (Math.Abs(dx) == 0 && Math.Abs(dy) == 0) return center;

                float halfWidth = Bounds.Width / 2;
                float halfHeight = Bounds.Height / 2;

                float crossX = halfWidth * Math.Sign(dx);
                float crossY = halfHeight * Math.Sign(dy);

                PointF localIntersection;
                if (Math.Abs(dx * halfHeight) > Math.Abs(dy * halfWidth))
                {
                    localIntersection = new PointF(center.X + crossX, center.Y + crossX * dy / dx);
                }
                else
                {
                    localIntersection = new PointF(center.X + crossY * dx / dy, center.Y + crossY);
                }

                return RotatePoint(localIntersection, center, RotationAngle);
            }

            public static PointF RotatePoint(PointF pt, PointF center, float angleDegrees)
            {
                float angleRadians = angleDegrees * (float)Math.PI / 180f;
                float cosTheta = (float)Math.Cos(angleRadians);
                float sinTheta = (float)Math.Sin(angleRadians);
                return new PointF(
                    cosTheta * (pt.X - center.X) - sinTheta * (pt.Y - center.Y) + center.X,
                    sinTheta * (pt.X - center.X) + cosTheta * (pt.Y - center.Y) + center.Y
                );
            }
        }

        // 升級：支援等比例縮放的群組
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
                    child.DrawWithTransform(g);
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

            // 群組縮放核心邏輯
            public override void SetBounds(RectangleF newBounds)
            {
                if (Bounds.Width == 0 || Bounds.Height == 0) return;
                
                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                foreach (var child in Children)
                {
                    float newChildX = newBounds.X + (child.Bounds.X - Bounds.X) * scaleX;
                    float newChildY = newBounds.Y + (child.Bounds.Y - Bounds.Y) * scaleY;
                    float newChildW = child.Bounds.Width * scaleX;
                    float newChildH = child.Bounds.Height * scaleY;
                    child.SetBounds(new RectangleF(newChildX, newChildY, newChildW, newChildH));
                }
                Bounds = newBounds;
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

            public override PointF GetIntersection(PointF targetPoint)
            {
                PointF center = GetCenter();
                float dx = targetPoint.X - center.X;
                float dy = targetPoint.Y - center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (distance == 0) return center;

                float radiusX = Bounds.Width / 2;
                float radiusY = Bounds.Height / 2;
                float radius = Math.Min(radiusX, radiusY);

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
                Text = "雙擊編輯";
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
            // 新增：綁定特定的錨點
            public AnchorPosition SourceAnchor { get; set; } = AnchorPosition.Auto;
            public AnchorPosition TargetAnchor { get; set; } = AnchorPosition.Auto;

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
                        capPath.AddLine(new PointF(-4, -4), new PointF(0, 0));
                        capPath.AddLine(new PointF(0, 0), new PointF(4, -4));
                        p.CustomEndCap = new CustomLineCap(null, capPath);
                    }

                    if (IsOrthogonal)
                    {
                        // 改進的智慧折線 (中點分段法)
                        float midX = p1.X + (p2.X - p1.X) / 2;
                        float midY = p1.Y + (p2.Y - p1.Y) / 2;
                        
                        PointF[] pts;
                        if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Y - p1.Y))
                        {
                            pts = new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                        }
                        else
                        {
                            pts = new PointF[] { p1, new PointF(p1.X, midY), new PointF(p2.X, midY), p2 };
                        }
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
