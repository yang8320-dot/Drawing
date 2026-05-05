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
            
            [JsonIgnore] 
            public bool IsSelected { get; set; }
            
            public Guid Id { get; set; } = Guid.NewGuid();

            // 支援所有圖形皆可加文字 (需求6)
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

            // 共同的文字繪製邏輯
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
                    new PointF(Bounds.Left + Bounds.Width/2, Bounds.Top),
                    new PointF(Bounds.Left + Bounds.Width/2, Bounds.Bottom),
                    new PointF(Bounds.Left, Bounds.Top + Bounds.Height/2),
                    new PointF(Bounds.Right, Bounds.Top + Bounds.Height/2)
                };
            }
        }

        public class RectShape : ShapeBase
        { 
            public RectShape() { } 
            public RectShape(PointF start, Color color) : base(start, color) { }
            
            public override void Draw(Graphics g)
            {
                using (Pen p = new Pen(ShapeColor, 2))
                {
                    g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
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
                using (Pen p = new Pen(ShapeColor, 2))
                {
                    g.DrawEllipse(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                }
                DrawText(g);
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
                    using (Pen p = new Pen(ShapeColor, 2))
                    {
                        g.DrawArc(p, Bounds, 180, 180);
                    }
                }
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
                    using (Pen p = new Pen(ShapeColor, 2))
                    {
                        g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                    }
                }
                DrawText(g);
            }
        }

        public class ImageShape : ShapeBase
        {
            public string Base64Image { get; set; }
            
            [JsonIgnore] 
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
                Bounds.Width = img.Width;
                Bounds.Height = img.Height;
            }
            
            public override void Draw(Graphics g)
            {
                if (_imgCache == null && !string.IsNullOrEmpty(Base64Image))
                {
                    byte[] bytes = Convert.FromBase64String(Base64Image);
                    using (var ms = new System.IO.MemoryStream(bytes))
                    {
                        _imgCache = new Bitmap(ms);
                    }
                }
                if (_imgCache != null)
                {
                    g.DrawImage(_imgCache, Bounds);
                }
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

            public ConnectorShape() { }
            
            public ConnectorShape(PointF start, Color color, bool arrow) : base(start, color)
            {
                StartPt = start;
                EndPt = start;
                HasArrow = arrow;
            }
            
            public override void UpdateEndPoint(PointF pt)
            {
                EndPt = pt;
            }
            
            public override bool HitTest(PointF pt)
            {
                // 線條由端點決定，不直接開放點選範圍，以免干擾選取
                return false;
            }
            
            public void DrawDynamic(Graphics g, PointF p1, PointF p2)
            {
                using (Pen p = new Pen(ShapeColor, 2))
                {
                    if (HasArrow)
                    {
                        GraphicsPath capPath = new GraphicsPath();
                        capPath.AddLine(new PointF(-2, -2), new PointF(0, 0));
                        capPath.AddLine(new PointF(0, 0), new PointF(2, -2));
                        p.CustomEndCap = new CustomLineCap(null, capPath);
                    }
                    g.DrawLine(p, p1, p2);
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
