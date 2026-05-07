using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, HandPan, ArrowLine, StraightLine, OrthogonalLine, Rectangle, RoundedRectangle, Circle, Arc, Diamond, Triangle, Pentagon, Hexagon, Star, Cloud, TextNode, Text, Image, Freehand }
        public enum HandlePosition { None, NW, N, NE, W, E, SW, S, SE, Rotate, StartPoint, EndPoint }
        public enum AnchorPosition { Auto, Top, Bottom, Left, Right }

        public abstract class ShapeBase : IDisposable
        {
            [Category("3. 座標與尺寸")]
            [DisplayName("物件邊界 (Bounds)")]
            [Description("修改物件的 X, Y 座標與寬高。")]
            public RectangleF Bounds { get; set; }

            [Category("1. 外觀屬性")]
            [DisplayName("外框/線條顏色")]
            public Color ShapeColor { get; set; }

            [Category("1. 外觀屬性")]
            [DisplayName("填充顏色")]
            [Description("圖形內部的顏色，預設為透明 (Transparent)。")]
            public virtual Color FillColor { get; set; } = Color.Transparent;
            
            [Category("1. 外觀屬性")]
            [DisplayName("線條粗細")]
            public float StrokeWidth { get; set; } = 2f;

            [Category("1. 外觀屬性")]
            [DisplayName("線條樣式")]
            public DashStyle StrokeDashStyle { get; set; } = DashStyle.Solid;
            
            [Category("3. 座標與尺寸")]
            [DisplayName("旋轉角度")]
            public virtual float RotationAngle { get; set; } = 0f;
            
            [Browsable(false)]
            [JsonIgnore] 
            public bool IsSelected { get; set; }

            [Category("4. 系統屬性")]
            [DisplayName("鎖定圖形")]
            [Description("鎖定後將無法被拖曳或修改大小。")]
            public bool IsLocked { get; set; } = false;
            
            [Browsable(false)]
            public Guid Id { get; set; } = Guid.NewGuid();

            [Category("2. 文字屬性")]
            [DisplayName("文字內容")]
            public virtual string Text { get; set; } = "";

            [Category("2. 文字屬性")]
            [DisplayName("字型名稱")]
            public virtual string FontName { get; set; } = "Arial";

            [Category("2. 文字屬性")]
            [DisplayName("字體大小")]
            public virtual float FontSize { get; set; } = 12f;

            [Category("2. 文字屬性")]
            [DisplayName("文字顏色")]
            public virtual Color FontColor { get; set; } = Color.Black;

            [Category("2. 文字屬性")]
            [DisplayName("粗體")]
            public virtual bool FontBold { get; set; } = false;

            [Category("2. 文字屬性")]
            [DisplayName("斜體")]
            public virtual bool FontItalic { get; set; } = false;

            [Category("2. 文字屬性")]
            [DisplayName("底線")]
            public virtual bool FontUnderline { get; set; } = false;

            public ShapeBase() { }

            public ShapeBase(PointF start, Color color)
            {
                Bounds = new RectangleF(start.X, start.Y, 0, 0);
                ShapeColor = color;
            }

            public virtual void Dispose() { }

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
                
                FontStyle style = FontStyle.Regular;
                if (FontBold) style |= FontStyle.Bold;
                if (FontItalic) style |= FontStyle.Italic;
                if (FontUnderline) style |= FontStyle.Underline;

                using (Font font = new Font(FontName, FontSize, style))
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
                if (IsLocked) return;
                Bounds = new RectangleF(Bounds.X, Bounds.Y, pt.X - Bounds.X, pt.Y - Bounds.Y);
                NormalizeBounds();
            }

            public virtual void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
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
                if (IsLocked) return;
                Bounds = new RectangleF(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
            }

            public PointF GetCenter()
            {
                return new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
            }

            public virtual bool HitTest(PointF pt)
            {
                PointF rotatedPt = RotatePoint(pt, GetCenter(), -RotationAngle);
                RectangleF hitBounds = Bounds;
                hitBounds.Inflate(5, 5);
                return hitBounds.Contains(rotatedPt);
            }
            
            public virtual void DrawSelection(Graphics g)
            {
                if (!IsSelected) return;

                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                Color outlineColor = IsLocked ? Color.Gray : Color.DodgerBlue;
                using (Pen p = new Pen(outlineColor, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, Rectangle.Round(Bounds));
                }

                float s = 8;
                PointF[] corners = new PointF[]
                {
                    new PointF(Bounds.Left, Bounds.Top),
                    new PointF(center.X, Bounds.Top),
                    new PointF(Bounds.Right, Bounds.Top),
                    new PointF(Bounds.Right, center.Y),
                    new PointF(Bounds.Right, Bounds.Bottom),
                    new PointF(center.X, Bounds.Bottom),
                    new PointF(Bounds.Left, Bounds.Bottom),
                    new PointF(Bounds.Left, center.Y)
                };

                Brush fillBrush = IsLocked ? Brushes.LightGray : Brushes.White;
                Pen borderPen = IsLocked ? Pens.Gray : Pens.DodgerBlue;

                foreach (var pt in corners)
                {
                    g.FillRectangle(fillBrush, pt.X - s/2, pt.Y - s/2, s, s);
                    g.DrawRectangle(borderPen, pt.X - s/2, pt.Y - s/2, s, s);
                }

                if (!IsLocked)
                {
                    PointF rotatePt = new PointF(center.X, Bounds.Top - 25);
                    g.DrawLine(Pens.DodgerBlue, center.X, Bounds.Top, rotatePt.X, rotatePt.Y);
                    g.FillEllipse(Brushes.LightGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.DarkGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);
                }

                g.Transform = oldMatrix;
            }

            public virtual HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected || IsLocked) return HandlePosition.None;

                PointF center = GetCenter();
                PointF rotatedPt = RotatePoint(pt, center, -RotationAngle);

                float s = 10;
                if (new RectangleF(center.X - s, Bounds.Top - 25 - s, s * 2, s * 2).Contains(rotatedPt)) return HandlePosition.Rotate;

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

        public class GroupShape : ShapeBase
        {
            [Browsable(false)]
            public List<ShapeBase> Children { get; set; } = new List<ShapeBase>();

            public GroupShape() { }

            public GroupShape(List<ShapeBase> children)
            {
                Children = children;
                NormalizeBounds();
            }

            public override void Dispose()
            {
                foreach (var child in Children) child.Dispose();
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
                if (IsLocked) return;
                base.Move(dx, dy);
                foreach (var child in Children)
                {
                    child.Move(dx, dy);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
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

        public class FreehandShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;

            [Browsable(false)]
            public List<PointF> LocalPoints { get; set; } = new List<PointF>();

            public FreehandShape() { }

            public FreehandShape(PointF start, Color color) : base(start, color)
            {
                LocalPoints.Add(new PointF(0, 0));
            }

            public void AddPoint(PointF absolutePt)
            {
                LocalPoints.Add(new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y));
            }

            public override void Draw(Graphics g)
            {
                if (LocalPoints.Count > 1)
                {
                    using (Pen p = CreatePen())
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        PointF[] absPts = LocalPoints.Select(pt => new PointF(Bounds.X + pt.X, Bounds.Y + pt.Y)).ToArray();
                        g.DrawLines(p, absPts);
                    }
                }
            }

            public override void NormalizeBounds()
            {
                if (LocalPoints.Count == 0) return;
                float minX = LocalPoints.Min(p => p.X);
                float minY = LocalPoints.Min(p => p.Y);
                float maxX = LocalPoints.Max(p => p.X);
                float maxY = LocalPoints.Max(p => p.Y);

                float newWidth = maxX - minX;
                float newHeight = maxY - minY;

                float absMinX = Bounds.X + minX;
                float absMinY = Bounds.Y + minY;

                Bounds = new RectangleF(absMinX, absMinY, newWidth, newHeight);

                for (int i = 0; i < LocalPoints.Count; i++)
                {
                    LocalPoints[i] = new PointF(LocalPoints[i].X - minX, LocalPoints[i].Y - minY);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;

                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                for (int i = 0; i < LocalPoints.Count; i++)
                {
                    LocalPoints[i] = new PointF(LocalPoints[i].X * scaleX, LocalPoints[i].Y * scaleY);
                }
                Bounds = newBounds;
            }
        }

        public class RectShape : ShapeBase
        { 
            public RectShape() { } 
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillRectangle(fillBrush, Bounds);
                }
                using (Pen p = CreatePen()) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawText(g);
            }
        }

        public class RoundedRectShape : ShapeBase
        {
            public RoundedRectShape() { }
            public RoundedRectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float radius = Math.Min(Bounds.Width, Bounds.Height) * 0.2f;
                if (radius <= 0) return;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(Bounds.X, Bounds.Y, radius * 2, radius * 2, 180, 90);
                    path.AddArc(Bounds.Right - radius * 2, Bounds.Y, radius * 2, radius * 2, 270, 90);
                    path.AddArc(Bounds.Right - radius * 2, Bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                    path.AddArc(Bounds.X, Bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                    path.CloseFigure();

                    if (FillColor != Color.Transparent)
                    {
                        using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPath(fillBrush, path);
                    }
                    using (Pen p = CreatePen()) g.DrawPath(p, path);
                }
                DrawText(g);
            }
        }

        public class CircleShape : ShapeBase
        {
            public CircleShape() { } 
            public CircleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillEllipse(fillBrush, Bounds);
                }
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
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
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

            public PointF[] GetPolygonPoints()
            {
                return new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Y + Bounds.Height / 2),
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Y + Bounds.Height / 2)
                };
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPolygon(fillBrush, pts);
                }
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class TriangleShape : ShapeBase
        {
            public TriangleShape() { }
            public TriangleShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                return new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Bottom)
                };
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPolygon(fillBrush, pts);
                }
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class PentagonShape : ShapeBase
        {
            public PentagonShape() { }
            public PentagonShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float rx = Bounds.Width / 2f;
                float ry = Bounds.Height / 2f;
                PointF[] pts = new PointF[5];
                for (int i = 0; i < 5; i++)
                {
                    double angle = Math.PI / 2 + (i * 2 * Math.PI / 5);
                    pts[i] = new PointF(center.X - (float)(rx * Math.Cos(angle)), center.Y - (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPolygon(fillBrush, pts);
                }
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class HexagonShape : ShapeBase
        {
            public HexagonShape() { }
            public HexagonShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float rx = Bounds.Width / 2f;
                float ry = Bounds.Height / 2f;
                PointF[] pts = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3;
                    pts[i] = new PointF(center.X + (float)(rx * Math.Cos(angle)), center.Y + (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPolygon(fillBrush, pts);
                }
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class StarShape : ShapeBase
        {
            public StarShape() { }
            public StarShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float outerRx = Bounds.Width / 2f;
                float outerRy = Bounds.Height / 2f;
                float innerRx = outerRx * 0.4f;
                float innerRy = outerRy * 0.4f;
                PointF[] pts = new PointF[10];

                for (int i = 0; i < 10; i++)
                {
                    double angle = Math.PI / 2 + (i * Math.PI / 5);
                    float rx = (i % 2 == 0) ? outerRx : innerRx;
                    float ry = (i % 2 == 0) ? outerRy : innerRy;
                    pts[i] = new PointF(center.X - (float)(rx * Math.Cos(angle)), center.Y - (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (FillColor != Color.Transparent)
                {
                    using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPolygon(fillBrush, pts);
                }
                using (Pen p = CreatePen()) g.DrawPolygon(p, pts);
                DrawText(g);
            }
        }

        public class CloudShape : ShapeBase
        {
            public CloudShape() { }
            public CloudShape(PointF start, Color color) : base(start, color) { }

            public override void Draw(Graphics g)
            {
                if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.FillMode = FillMode.Winding;
                    
                    float x = Bounds.X, y = Bounds.Y, w = Bounds.Width, h = Bounds.Height;
                    path.AddEllipse(x + w * 0.15f, y + h * 0.2f, w * 0.4f, h * 0.5f);
                    path.AddEllipse(x + w * 0.35f, y + h * 0.1f, w * 0.5f, h * 0.6f);
                    path.AddEllipse(x + w * 0.55f, y + h * 0.3f, w * 0.35f, h * 0.5f);
                    path.AddEllipse(x + w * 0.25f, y + h * 0.4f, w * 0.5f, h * 0.5f);

                    if (FillColor != Color.Transparent)
                    {
                        using (Brush fillBrush = new SolidBrush(FillColor)) g.FillPath(fillBrush, path);
                    }
                    using (Pen p = CreatePen()) g.DrawPath(p, path);
                }
                DrawText(g);
            }
        }

        public class TextNodeShape : ShapeBase
        {
            [Browsable(false)]
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
                    if (FillColor != Color.Transparent)
                    {
                        using (Brush fillBrush = new SolidBrush(FillColor)) g.FillRectangle(fillBrush, Bounds);
                    }
                    using (Pen p = CreatePen()) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                }
                DrawText(g);
            }
        }

        public class ImageShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;

            [Browsable(false)]
            public string Base64Image { get; set; }
            
            [JsonIgnore] 
            [Browsable(false)]
            private Bitmap _imgCache;
            
            public ImageShape() { }
            public ImageShape(PointF start, Bitmap img) : base(start, Color.Black)
            {
                _imgCache = img;
                using (var ms = new System.IO.MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    Base64Image = Convert.ToBase64String(ms.ToArray());
                }
                Bounds = new RectangleF(Bounds.X, Bounds.Y, img.Width, img.Height); 
            }

            public override void Dispose()
            {
                if (_imgCache != null)
                {
                    _imgCache.Dispose();
                    _imgCache = null;
                }
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
            // --- 屬性過濾：連線不需要內部填充與文字 ---
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override float RotationAngle { get; set; } = 0f;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;

            [Browsable(false)] public Guid SourceId { get; set; }
            [Browsable(false)] public Guid TargetId { get; set; }
            
            [Category("5. 連線屬性")]
            [DisplayName("起點錨點")]
            public AnchorPosition SourceAnchor { get; set; } = AnchorPosition.Auto;
            
            [Category("5. 連線屬性")]
            [DisplayName("終點錨點")]
            public AnchorPosition TargetAnchor { get; set; } = AnchorPosition.Auto;

            [Browsable(false)] public PointF StartPt { get; set; }
            [Browsable(false)] public PointF EndPt { get; set; }
            
            [Category("5. 連線屬性")]
            [DisplayName("顯示箭頭")]
            public bool HasArrow { get; set; }
            
            [Category("5. 連線屬性")]
            [DisplayName("直角折線")]
            public bool IsOrthogonal { get; set; }

            [JsonIgnore] 
            [Browsable(false)] 
            private PointF[] _cachedPath;

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow, bool orthogonal = false) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow; IsOrthogonal = orthogonal;
            }
            
            public override void UpdateEndPoint(PointF pt) { if (!IsLocked) EndPt = pt; }
            
            public override bool HitTest(PointF pt) 
            { 
                if (_cachedPath == null || _cachedPath.Length < 2) return false;
                for (int i = 0; i < _cachedPath.Length - 1; i++)
                {
                    if (DistancePointToSegment(pt, _cachedPath[i], _cachedPath[i+1]) < 8f) return true;
                }
                return false; 
            } 

            public override HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected || IsLocked || _cachedPath == null || _cachedPath.Length < 2) return HandlePosition.None;

                float s = 10;
                PointF start = _cachedPath[0];
                PointF end = _cachedPath[_cachedPath.Length - 1];

                if (new RectangleF(start.X - s, start.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.StartPoint;
                if (new RectangleF(end.X - s, end.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.EndPoint;

                return HandlePosition.None;
            }

            public override void Move(float dx, float dy)
            {
                if (IsLocked) return;
                StartPt = new PointF(StartPt.X + dx, StartPt.Y + dy);
                EndPt = new PointF(EndPt.X + dx, EndPt.Y + dy);
            }

            private float DistancePointToSegment(PointF pt, PointF p1, PointF p2)
            {
                float l2 = (p1.X - p2.X)*(p1.X - p2.X) + (p1.Y - p2.Y)*(p1.Y - p2.Y);
                if (l2 == 0) return (float)Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                float t = Math.Max(0, Math.Min(1, ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / l2));
                PointF projection = new PointF(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                return (float)Math.Sqrt(Math.Pow(pt.X - projection.X, 2) + Math.Pow(pt.Y - projection.Y, 2));
            }

            // 優化：A* 避障直角連線演算法 (Orthogonal Routing with Obstacle Avoidance)
            // 將原本簡單的 midX, midY 判斷替換為基於曼哈頓距離的網格尋路
            private PointF[] CalculateOrthogonalPath(PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes)
            {
                // 如果兩點距離太近，直接連過去避免計算浪費
                if (Math.Abs(p1.X - p2.X) < 20 && Math.Abs(p1.Y - p2.Y) < 20)
                {
                    return new PointF[] { p1, new PointF(p1.X, p2.Y), p2 };
                }

                List<RectangleF> obstacles = new List<RectangleF>();
                if (allShapes != null)
                {
                    foreach (var s in allShapes)
                    {
                        if (s is ConnectorShape || s.Id == this.SourceId || s.Id == this.TargetId) continue;
                        // 將障礙物範圍稍微擴大 (Padding)，避免線條貼緊邊緣
                        RectangleF obs = s.Bounds;
                        obs.Inflate(15, 15);
                        obstacles.Add(obs);
                    }
                }

                // 產生網格點 (Grid generation) - 使用關鍵點的 X 與 Y 交集來建立尋路網格
                List<float> xCoords = new List<float> { p1.X, p2.X, p1.X - 30, p1.X + 30, p2.X - 30, p2.X + 30 };
                List<float> yCoords = new List<float> { p1.Y, p2.Y, p1.Y - 30, p1.Y + 30, p2.Y - 30, p2.Y + 30 };

                foreach (var obs in obstacles)
                {
                    xCoords.Add(obs.Left); xCoords.Add(obs.Right);
                    yCoords.Add(obs.Top); yCoords.Add(obs.Bottom);
                }

                xCoords = xCoords.Distinct().OrderBy(x => x).ToList();
                yCoords = yCoords.Distinct().OrderBy(y => y).ToList();

                // 簡單的回退機制：如果圖形太多導致網格過度龐大，改用基本的三段折線避免卡頓
                if (xCoords.Count * yCoords.Count > 1000) 
                {
                    return BasicOrthogonalPath(p1, p2);
                }

                PointF startNode = GetClosestNode(p1, xCoords, yCoords);
                PointF endNode = GetClosestNode(p2, xCoords, yCoords);

                // A* 核心邏輯
                var openSet = new List<PointF> { startNode };
                var cameFrom = new Dictionary<PointF, PointF>();
                var gScore = new Dictionary<PointF, float> { [startNode] = 0 };
                var fScore = new Dictionary<PointF, float> { [startNode] = Heuristic(startNode, endNode) };

                while (openSet.Count > 0)
                {
                    PointF current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();
                    if (current == endNode) return ReconstructPath(cameFrom, current, p1, p2);

                    openSet.Remove(current);

                    foreach (var neighbor in GetNeighbors(current, xCoords, yCoords))
                    {
                        // 檢查這條連線是否穿過障礙物
                        if (LineIntersectsObstacles(current, neighbor, obstacles)) continue;

                        // 計算轉彎懲罰 (Turn penalty)，鼓勵直走
                        float penalty = 0;
                        if (cameFrom.ContainsKey(current))
                        {
                            PointF prev = cameFrom[current];
                            bool wasHorizontal = Math.Abs(current.Y - prev.Y) < 1f;
                            bool isHorizontal = Math.Abs(neighbor.Y - current.Y) < 1f;
                            if (wasHorizontal != isHorizontal) penalty = 50f; 
                        }

                        float tentativeG = gScore[current] + Heuristic(current, neighbor) + penalty;

                        if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                        {
                            cameFrom[neighbor] = current;
                            gScore[neighbor] = tentativeG;
                            fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, endNode);
                            if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                        }
                    }
                }

                // 如果找不到完全避障的路徑（例如被包圍），回退到基本連線
                return BasicOrthogonalPath(p1, p2);
            }

            private PointF[] BasicOrthogonalPath(PointF p1, PointF p2)
            {
                float midX = p1.X + (p2.X - p1.X) / 2;
                float midY = p1.Y + (p2.Y - p1.Y) / 2;
                
                if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Y - p1.Y))
                    return new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                else
                    return new PointF[] { p1, new PointF(p1.X, midY), new PointF(p2.X, midY), p2 };
            }

            private PointF GetClosestNode(PointF p, List<float> xCoords, List<float> yCoords)
            {
                float closeX = xCoords.OrderBy(x => Math.Abs(x - p.X)).First();
                float closeY = yCoords.OrderBy(y => Math.Abs(y - p.Y)).First();
                return new PointF(closeX, closeY);
            }

            private float Heuristic(PointF a, PointF b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

            private IEnumerable<PointF> GetNeighbors(PointF p, List<float> xCoords, List<float> yCoords)
            {
                int xi = xCoords.IndexOf(p.X);
                int yi = yCoords.IndexOf(p.Y);
                if (xi > 0) yield return new PointF(xCoords[xi - 1], p.Y);
                if (xi < xCoords.Count - 1) yield return new PointF(xCoords[xi + 1], p.Y);
                if (yi > 0) yield return new PointF(p.X, yCoords[yi - 1]);
                if (yi < yCoords.Count - 1) yield return new PointF(p.X, yCoords[yi + 1]);
            }

            private bool LineIntersectsObstacles(PointF p1, PointF p2, List<RectangleF> obstacles)
            {
                float minX = Math.Min(p1.X, p2.X);
                float maxX = Math.Max(p1.X, p2.X);
                float minY = Math.Min(p1.Y, p2.Y);
                float maxY = Math.Max(p1.Y, p2.Y);

                foreach (var obs in obstacles)
                {
                    if (p1.X == p2.X) // 垂直線
                    {
                        if (p1.X > obs.Left && p1.X < obs.Right && minY < obs.Bottom && maxY > obs.Top) return true;
                    }
                    else if (p1.Y == p2.Y) // 水平線
                    {
                        if (p1.Y > obs.Top && p1.Y < obs.Bottom && minX < obs.Right && maxX > obs.Left) return true;
                    }
                }
                return false;
            }

            private PointF[] ReconstructPath(Dictionary<PointF, PointF> cameFrom, PointF current, PointF start, PointF end)
            {
                List<PointF> path = new List<PointF> { end };
                while (cameFrom.ContainsKey(current))
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Add(start);
                path.Reverse();

                // 移除不需要的共線中繼點，使折線更乾淨
                List<PointF> cleanPath = new List<PointF> { path[0] };
                for (int i = 1; i < path.Count - 1; i++)
                {
                    PointF prev = cleanPath.Last();
                    PointF next = path[i + 1];
                    // 如果不是在同一直線上，才把這個點加進去 (轉折點)
                    if (prev.X != next.X && prev.Y != next.Y)
                    {
                        cleanPath.Add(path[i]);
                    }
                }
                cleanPath.Add(path.Last());

                return cleanPath.ToArray();
            }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes = null)
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
                        PointF[] pts = CalculateOrthogonalPath(p1, p2, allShapes);
                        _cachedPath = pts; 
                        g.DrawLines(p, pts);
                    }
                    else
                    {
                        _cachedPath = new PointF[] { p1, p2 }; 
                        g.DrawLine(p, p1, p2);
                    }
                }
            }

            // 為了相容原本介面，保留這兩個多載
            public void DrawDynamic(Graphics g, PointF p1, PointF p2) => DrawDynamic(g, p1, p2, null);
            public override void Draw(Graphics g) { }

            public override void DrawSelection(Graphics g)
            {
                if (!IsSelected || _cachedPath == null || _cachedPath.Length < 2) return;
                
                Color ptColor = IsLocked ? Color.LightGray : Color.White;
                Color borderColor = IsLocked ? Color.Gray : Color.DodgerBlue;

                foreach (var pt in _cachedPath)
                {
                    using (Brush b = new SolidBrush(ptColor)) g.FillRectangle(b, pt.X - 3, pt.Y - 3, 6, 6);
                    using (Pen p = new Pen(borderColor)) g.DrawRectangle(p, pt.X - 3, pt.Y - 3, 6, 6);
                }

                if (!IsLocked)
                {
                    PointF start = _cachedPath[0];
                    PointF end = _cachedPath[_cachedPath.Length - 1];

                    g.FillEllipse(Brushes.Yellow, start.X - 5, start.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, start.X - 5, start.Y - 5, 10, 10);

                    g.FillEllipse(Brushes.Yellow, end.X - 5, end.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, end.X - 5, end.Y - 5, 10, 10);
                }
            }
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
                    default: return null;
                }
            }
        }
    }
}
