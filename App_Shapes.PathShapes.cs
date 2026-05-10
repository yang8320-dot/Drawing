using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace DrawingApp
{
    public partial class App_Shapes
    {
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

            private Pen _cachedFreehandShadowPen;

            public FreehandShape() { }

            public FreehandShape(PointF start, Color color) : base(start, color)
            {
                LocalPoints.Add(new PointF(0, 0));
            }

            public void AddPoint(PointF absolutePt)
            {
                LocalPoints.Add(new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y));
            }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedFreehandShadowPen != null) { _cachedFreehandShadowPen.Dispose(); _cachedFreehandShadowPen = null; }
            }

            public override void Draw(Graphics g)
            {
                if (LocalPoints.Count > 1)
                {
                    Pen p = GetCachedPen();
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    PointF[] absPts = LocalPoints.Select(pt => new PointF(Bounds.X + pt.X, Bounds.Y + pt.Y)).ToArray();
                    
                    if (ShouldDrawShadow)
                    {
                        if (_cachedFreehandShadowPen == null)
                            _cachedFreehandShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), p.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                        
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        if (absPts.Length > 2) g.DrawCurve(_cachedFreehandShadowPen, absPts); 
                        else g.DrawLines(_cachedFreehandShadowPen, absPts);
                        g.Transform = m;
                    }

                    if (absPts.Length > 2)
                        g.DrawCurve(p, absPts); 
                    else
                        g.DrawLines(p, absPts);
                }
            }

            public override void NormalizeBounds()
            {
                if (LocalPoints.Count == 0) return;
                float minX = LocalPoints.Min(pt => pt.X);
                float minY = LocalPoints.Min(pt => pt.Y);
                float maxX = LocalPoints.Max(pt => pt.X);
                float maxY = LocalPoints.Max(pt => pt.Y);

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
                base.SetBounds(newBounds);
            }
        }

        public class BezierNode
        {
            public PointF Anchor { get; set; }
            public PointF Control1 { get; set; }
            public PointF Control2 { get; set; }
            public BezierNode(PointF pt) { Anchor = pt; Control1 = pt; Control2 = pt; }
        }

        public class BezierShape : ShapeBase
        {
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)]
            public List<BezierNode> LocalNodes { get; set; } = new List<BezierNode>();

            private Pen _cachedShadowPen;

            public BezierShape() { }

            public BezierShape(PointF start, Color color) : base(start, color)
            {
                LocalNodes.Add(new BezierNode(new PointF(0, 0)));
            }

            public void AddNode(PointF absolutePt)
            {
                LocalNodes.Add(new BezierNode(new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y)));
            }

            public void UpdateLastControlPoint(PointF absoluteDragPt)
            {
                if (LocalNodes.Count == 0) return;
                var lastNode = LocalNodes.Last();
                PointF localDrag = new PointF(absoluteDragPt.X - Bounds.X, absoluteDragPt.Y - Bounds.Y);
                lastNode.Control2 = localDrag;
                lastNode.Control1 = new PointF(lastNode.Anchor.X - (localDrag.X - lastNode.Anchor.X), lastNode.Anchor.Y - (localDrag.Y - lastNode.Anchor.Y));
            }

            public void UpdateLastAnchorPoint(PointF absolutePt)
            {
                if (LocalNodes.Count == 0) return;
                var lastNode = LocalNodes.Last();
                PointF localPt = new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y);
                lastNode.Anchor = localPt;
                lastNode.Control1 = localPt;
                lastNode.Control2 = localPt;
            }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedShadowPen != null) { _cachedShadowPen.Dispose(); _cachedShadowPen = null; }
            }

            private GraphicsPath GetPath()
            {
                GraphicsPath path = new GraphicsPath();
                if (LocalNodes.Count < 2) return path;

                List<PointF> pts = new List<PointF>();
                pts.Add(new PointF(Bounds.X + LocalNodes[0].Anchor.X, Bounds.Y + LocalNodes[0].Anchor.Y));

                for (int i = 1; i < LocalNodes.Count; i++)
                {
                    pts.Add(new PointF(Bounds.X + LocalNodes[i - 1].Control2.X, Bounds.Y + LocalNodes[i - 1].Control2.Y));
                    pts.Add(new PointF(Bounds.X + LocalNodes[i].Control1.X, Bounds.Y + LocalNodes[i].Control1.Y));
                    pts.Add(new PointF(Bounds.X + LocalNodes[i].Anchor.X, Bounds.Y + LocalNodes[i].Anchor.Y));
                }

                if (pts.Count >= 4)
                {
                    path.AddBeziers(pts.ToArray());
                }
                return path;
            }

            public override void Draw(Graphics g)
            {
                if (LocalNodes.Count < 2) return;

                using (GraphicsPath path = GetPath())
                {
                    if (ShouldDrawShadow)
                    {
                        if (_cachedShadowPen == null)
                            _cachedShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), StrokeWidth) { LineJoin = LineJoin.Round };
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.DrawPath(_cachedShadowPen, path);
                        if (FillColor != Color.Transparent) g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent) g.FillPath(GetCachedFillBrush(Bounds), path);
                    g.DrawPath(GetCachedPen(), path);
                }
            }

            public override void DrawSelection(Graphics g)
            {
                base.DrawSelection(g);

                if (!IsSelected || IsLocked || LocalNodes.Count < 1) return;

                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                using (Pen handlePen = new Pen(Color.CornflowerBlue, 1) { DashStyle = DashStyle.Dot })
                {
                    foreach (var node in LocalNodes)
                    {
                        PointF absA = new PointF(Bounds.X + node.Anchor.X, Bounds.Y + node.Anchor.Y);
                        PointF absC1 = new PointF(Bounds.X + node.Control1.X, Bounds.Y + node.Control1.Y);
                        PointF absC2 = new PointF(Bounds.X + node.Control2.X, Bounds.Y + node.Control2.Y);

                        g.DrawLine(handlePen, absC1, absA);
                        g.DrawLine(handlePen, absA, absC2);

                        g.FillEllipse(Brushes.White, absA.X - 3, absA.Y - 3, 6, 6);
                        g.DrawEllipse(Pens.Blue, absA.X - 3, absA.Y - 3, 6, 6);

                        g.FillRectangle(Brushes.White, absC1.X - 2, absC1.Y - 2, 4, 4);
                        g.DrawRectangle(Pens.Gray, absC1.X - 2, absC1.Y - 2, 4, 4);
                        
                        g.FillRectangle(Brushes.White, absC2.X - 2, absC2.Y - 2, 4, 4);
                        g.DrawRectangle(Pens.Gray, absC2.X - 2, absC2.Y - 2, 4, 4);
                    }
                }

                g.Transform = oldMatrix;
            }

            public override void NormalizeBounds()
            {
                if (LocalNodes.Count == 0) return;

                float minX = LocalNodes.Min(n => Math.Min(n.Anchor.X, Math.Min(n.Control1.X, n.Control2.X)));
                float minY = LocalNodes.Min(n => Math.Min(n.Anchor.Y, Math.Min(n.Control1.Y, n.Control2.Y)));
                float maxX = LocalNodes.Max(n => Math.Max(n.Anchor.X, Math.Max(n.Control1.X, n.Control2.X)));
                float maxY = LocalNodes.Max(n => Math.Max(n.Anchor.Y, Math.Max(n.Control1.Y, n.Control2.Y)));

                float newWidth = maxX - minX;
                float newHeight = maxY - minY;

                float absMinX = Bounds.X + minX;
                float absMinY = Bounds.Y + minY;

                Bounds = new RectangleF(absMinX, absMinY, newWidth, newHeight);

                foreach (var node in LocalNodes)
                {
                    node.Anchor = new PointF(node.Anchor.X - minX, node.Anchor.Y - minY);
                    node.Control1 = new PointF(node.Control1.X - minX, node.Control1.Y - minY);
                    node.Control2 = new PointF(node.Control2.X - minX, node.Control2.Y - minY);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;

                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                foreach (var node in LocalNodes)
                {
                    node.Anchor = new PointF(node.Anchor.X * scaleX, node.Anchor.Y * scaleY);
                    node.Control1 = new PointF(node.Control1.X * scaleX, node.Control1.Y * scaleY);
                    node.Control2 = new PointF(node.Control2.X * scaleX, node.Control2.Y * scaleY);
                }
                base.SetBounds(newBounds);
            }
        }
    }
}
