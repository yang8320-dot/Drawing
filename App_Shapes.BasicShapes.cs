using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public class RectShape : ShapeBase
        { 
            public RectShape() { } 
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if (ShouldDrawShadow)
                    g.FillRectangle(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                
                if (FillColor != Color.Transparent)
                    g.FillRectangle(GetCachedFillBrush(Bounds), Bounds);
                
                g.DrawRectangle(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
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

                    if (ShouldDrawShadow)
                    {
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent)
                        g.FillPath(GetCachedFillBrush(Bounds), path);
                    
                    g.DrawPath(GetCachedPen(), path);
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
                if (ShouldDrawShadow)
                    g.FillEllipse(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                
                if (FillColor != Color.Transparent)
                    g.FillEllipse(GetCachedFillBrush(Bounds), Bounds);
                
                g.DrawEllipse(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
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
            
            private Pen _cachedArcShadowPen;

            public ArcShape() { } 
            public ArcShape(PointF start, Color color) : base(start, color) { }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedArcShadowPen != null) { _cachedArcShadowPen.Dispose(); _cachedArcShadowPen = null; }
            }

            public override void Draw(Graphics g)
            {
                if(Bounds.Width > 0 && Bounds.Height > 0)
                {
                    if (ShouldDrawShadow)
                    {
                        if (_cachedArcShadowPen == null)
                            _cachedArcShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), StrokeWidth);
                        g.DrawArc(_cachedArcShadowPen, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height, 180, 180);
                    }
                    g.DrawArc(GetCachedPen(), Bounds, 180, 180);
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
                if (ShouldDrawShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
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
                if (ShouldDrawShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
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
                if (ShouldDrawShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
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
                if (ShouldDrawShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
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
                if (ShouldDrawShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
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

                    if (ShouldDrawShadow)
                    {
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent)
                        g.FillPath(GetCachedFillBrush(Bounds), path);
                    
                    g.DrawPath(GetCachedPen(), path);
                }
                DrawText(g);
            }
        }
    }
}
