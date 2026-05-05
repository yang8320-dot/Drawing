using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, ArrowLine, StraightLine, Rectangle, Circle, Arc, TextNode, Text, Image }
        public enum HandlePosition { None, NW, NE, SW, SE }

        public abstract class ShapeBase
        {
            public RectangleF Bounds;
            public Color ShapeColor { get; set; }
            [JsonIgnore] public bool IsSelected { get; set; }
            public Guid Id { get; set; } = Guid.NewGuid();

            public ShapeBase() { } // 給 Json 反序列化用
            public ShapeBase(PointF start, Color color)
            {
                Bounds = new RectangleF(start.X, start.Y, 0, 0);
                ShapeColor = color;
            }

            public abstract void Draw(Graphics g);

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

            public virtual void Move(float dx, float dy) { Bounds.Offset(dx, dy); }

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
                foreach (var handle in GetHandles())
                {
                    g.FillRectangle(Brushes.White, handle.Value);
                    g.DrawRectangle(Pens.DodgerBlue, Rectangle.Round(handle.Value));
                }
            }

            public Dictionary<HandlePosition, RectangleF> GetHandles()
            {
                float s = 6;
                return new Dictionary<HandlePosition, RectangleF>
                {
                    { HandlePosition.NW, new RectangleF(Bounds.Left - s/2, Bounds.Top - s/2, s, s) },
                    { HandlePosition.NE, new RectangleF(Bounds.Right - s/2, Bounds.Top - s/2, s, s) },
                    { HandlePosition.SW, new RectangleF(Bounds.Left - s/2, Bounds.Bottom - s/2, s, s) },
                    { HandlePosition.SE, new RectangleF(Bounds.Right - s/2, Bounds.Bottom - s/2, s, s) }
                };
            }

            public HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected) return HandlePosition.None;
                foreach (var handle in GetHandles())
                    if (handle.Value.Contains(pt)) return handle.Key;
                return HandlePosition.None;
            }

            public virtual PointF[] GetAnchors()
            {
                return new PointF[]
                {
                    new PointF(Bounds.Left + Bounds.Width / 2, Bounds.Top),
                    new PointF(Bounds.Left + Bounds.Width / 2, Bounds.Bottom),
                    new PointF(Bounds.Left, Bounds.Top + Bounds.Height / 2),
                    new PointF(Bounds.Right, Bounds.Top + Bounds.Height / 2)
                };
            }
        }

        // --- 具體形狀實作 ---
        public class RectShape : ShapeBase
        {
            public RectShape() { }
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g) { using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height); }
        }

        public class CircleShape : ShapeBase
        {
            public CircleShape() { }
            public CircleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g) { using (Pen p = new Pen(ShapeColor, 2)) g.DrawEllipse(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height); }
        }

        // 新增：三點圓線 (用外接矩形模擬圓弧)
        public class ArcShape : ShapeBase
        {
            public ArcShape() { }
            public ArcShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g) { if(Bounds.Width > 0 && Bounds.Height > 0) using (Pen p = new Pen(ShapeColor, 2)) g.DrawArc(p, Bounds, 180, 180); }
        }

        public class TextNodeShape : ShapeBase
        {
            public string Text { get; set; } = "連點兩下編輯";
            public string FontName { get; set; } = "Arial";
            public float FontSize { get; set; } = 12f;
            public bool IsTransparent { get; set; } = false; // 區分 TextNode 和純 Text

            public TextNodeShape() { }
            public TextNodeShape(PointF start, Color color, bool transparent) : base(start, color) { IsTransparent = transparent; }
            public override void Draw(Graphics g)
            {
                if (!IsTransparent) using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                using (Font font = new Font(FontName, FontSize))
                using (Brush b = new SolidBrush(ShapeColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(Text, font, b, Bounds, sf);
                }
            }
        }

        // 新增：圖片形狀
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
                    byte[] bytes = Convert.FromBase64String(Base64Image);
                    using (var ms = new System.IO.MemoryStream(bytes)) _imgCache = new Bitmap(ms);
                }
                if (_imgCache != null) g.DrawImage(_imgCache, Bounds);
            }
        }

        public class ConnectorShape : ShapeBase
        {
            public Guid SourceId { get; set; }
            public Guid TargetId { get; set; }
            public PointF StartPt { get; set; }
            public PointF EndPt { get; set; }
            public bool HasArrow { get; set; }

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow;
            }

            public override void UpdateEndPoint(PointF pt) { EndPt = pt; }
            public override void Move(float dx, float dy) { /* 連線由附著點決定 */ }
            public override PointF[] GetAnchors() { return new PointF[0]; }

            public override bool HitTest(PointF pt) { return false; /* 簡化: 線條暫不支援單獨點選移動，跟隨物件 */ }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2)
            {
                using (Pen p = new Pen(ShapeColor, 2))
                {
                    if (HasArrow) p.CustomEndCap = new CustomLineCap(null, new GraphicsPath(new[] { new PointF(-2, -2), new PointF(0, 0), new PointF(2, -2) }, new[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line }));
                    g.DrawLine(p, p1, p2);
                }
            }
            public override void Draw(Graphics g) { /* 在 Canvas 中會計算位置後呼叫 DrawDynamic */ }
        }

        public static class ShapeFactory
        {
            public static ShapeBase CreateShape(ShapeType type, PointF start, Color color, Bitmap img = null)
            {
                switch (type)
                {
                    case ShapeType.ArrowLine: return new ConnectorShape(start, color, true);
                    case ShapeType.StraightLine: return new ConnectorShape(start, color, false);
                    case ShapeType.Rectangle: return new RectShape(start, color);
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.Arc: return new ArcShape(start, color);
                    case ShapeType.TextNode: return new TextNodeShape(start, color, false);
                    case ShapeType.Text: return new TextNodeShape(start, color, true);
                    case ShapeType.Image: return new ImageShape(start, img);
                    default: return null;
                }
            }
        }
    }
}
