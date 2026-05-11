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

        // ==========================================
        // 以下為新增的四種流程圖形
        // ==========================================
        public class ParallelogramShape : ShapeBase
        {
            public ParallelogramShape() { }
            public ParallelogramShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float offset = Bounds.Width * 0.2f;
                PointF[] pts = new PointF[] {
                    new PointF(Bounds.X + offset, Bounds.Y), 
                    new PointF(Bounds.Right, Bounds.Y),
                    new PointF(Bounds.Right - offset, Bounds.Bottom), 
                    new PointF(Bounds.X, Bounds.Bottom)
                };
                if (ShouldDrawShadow) { var m = g.Transform.Clone(); g.TranslateTransform(6, 6); g.FillPolygon(SharedShadowBrush, pts); g.Transform = m; }
                if (FillColor != Color.Transparent) g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class CylinderShape : ShapeBase
        {
            public CylinderShape() { }
            public CylinderShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float ellipseHeight = Math.Min(Bounds.Height * 0.25f, 20f);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(Bounds.X, Bounds.Bottom - ellipseHeight, Bounds.Width, ellipseHeight, 0, 180);
                    path.AddLine(Bounds.X, Bounds.Bottom - ellipseHeight / 2, Bounds.X, Bounds.Y + ellipseHeight / 2);
                    path.AddArc(Bounds.X, Bounds.Y, Bounds.Width, ellipseHeight, 180, 180);
                    path.AddLine(Bounds.Right, Bounds.Y + ellipseHeight / 2, Bounds.Right, Bounds.Bottom - ellipseHeight / 2);
                    if (ShouldDrawShadow) { var m = g.Transform.Clone(); g.TranslateTransform(6, 6); g.FillPath(SharedShadowBrush, path); g.Transform = m; }
                    if (FillColor != Color.Transparent) g.FillPath(GetCachedFillBrush(Bounds), path);
                    g.DrawPath(GetCachedPen(), path);
                    g.DrawEllipse(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, ellipseHeight);
                }
                DrawText(g);
            }
        }

        public class DocumentShape : ShapeBase
        {
            public DocumentShape() { }
            public DocumentShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float waveH = Math.Min(Bounds.Height * 0.15f, 15f);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(Bounds.X, Bounds.Y, Bounds.Right, Bounds.Y);
                    path.AddLine(Bounds.Right, Bounds.Y, Bounds.Right, Bounds.Bottom - waveH);
                    path.AddBezier(Bounds.Right, Bounds.Bottom - waveH, Bounds.X + Bounds.Width * 0.75f, Bounds.Bottom, 
                                   Bounds.X + Bounds.Width * 0.25f, Bounds.Bottom - waveH * 2, Bounds.X, Bounds.Bottom - waveH);
                    path.CloseFigure();
                    if (ShouldDrawShadow) { var m = g.Transform.Clone(); g.TranslateTransform(6, 6); g.FillPath(SharedShadowBrush, path); g.Transform = m; }
                    if (FillColor != Color.Transparent) g.FillPath(GetCachedFillBrush(Bounds), path);
                    g.DrawPath(GetCachedPen(), path);
                }
                DrawText(g);
            }
        }

        public class BlockArrowShape : ShapeBase
        {
            public BlockArrowShape() { }
            public BlockArrowShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float shaftH = Bounds.Height * 0.5f;
                float headW = Bounds.Width * 0.4f;
                PointF[] pts = new PointF[] {
                    new PointF(Bounds.X, Bounds.Y + Bounds.Height/2 - shaftH/2),
                    new PointF(Bounds.Right - headW, Bounds.Y + Bounds.Height/2 - shaftH/2),
                    new PointF(Bounds.Right - headW, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Y + Bounds.Height/2),
                    new PointF(Bounds.Right - headW, Bounds.Bottom),
                    new PointF(Bounds.Right - headW, Bounds.Y + Bounds.Height/2 + shaftH/2),
                    new PointF(Bounds.X, Bounds.Y + Bounds.Height/2 + shaftH/2)
                };
                if (ShouldDrawShadow) { var m = g.Transform.Clone(); g.TranslateTransform(6, 6); g.FillPolygon(SharedShadowBrush, pts); g.Transform = m; }
                if (FillColor != Color.Transparent) g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }
    }
}
