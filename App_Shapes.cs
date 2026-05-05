/*
 * 檔案功能：定義具備選取、縮放、移動與連線附著功能之圖形物件
 * 對應選單：無
 * 對應資料庫：無
 * 資料表名稱：無
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DrawingApp
{
    public static class App_Shapes
    {
        public enum ShapeType { Pointer, Line, Rectangle, Circle, TextNode }
        public enum HandlePosition { None, NW, NE, SW, SE }

        // 基礎圖形抽象類別
        public abstract class ShapeBase
        {
            public RectangleF Bounds;
            public Color ShapeColor { get; set; }
            public bool IsSelected { get; set; }
            public Guid Id { get; private set; } = Guid.NewGuid();

            public ShapeBase(PointF start, Color color)
            {
                Bounds = new RectangleF(start.X, start.Y, 0, 0);
                ShapeColor = color;
            }

            public abstract void Draw(Graphics g);

            // 更新建立時的終點
            public virtual void UpdateEndPoint(PointF pt)
            {
                float x = Math.Min(Bounds.X, pt.X);
                float y = Math.Min(Bounds.Y, pt.Y);
                float w = Math.Abs(Bounds.X - pt.X);
                float h = Math.Abs(Bounds.Y - pt.Y);
                // 保持起始錨點不動，只更新寬高與座標
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

            // 移動圖形
            public virtual void Move(float dx, float dy)
            {
                Bounds.Offset(dx, dy);
            }

            // 碰撞偵測 (滑鼠是否點中圖形)
            public virtual bool HitTest(PointF pt)
            {
                // 增加一點容錯率
                RectangleF hitBounds = Bounds;
                hitBounds.Inflate(5, 5);
                return hitBounds.Contains(pt);
            }

            // 繪製選取框與縮放控制點
            public void DrawSelection(Graphics g)
            {
                if (!IsSelected) return;
                using (Pen p = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, Rectangle.Round(Bounds));
                }
                // 繪製四個角落的控制點
                foreach (var handle in GetHandles())
                {
                    g.FillRectangle(Brushes.White, handle.Value);
                    g.DrawRectangle(Pens.DodgerBlue, Rectangle.Round(handle.Value));
                }
            }

            // 取得四個角落縮放控制點
            public Dictionary<HandlePosition, RectangleF> GetHandles()
            {
                float s = 6; // 控制點大小
                return new Dictionary<HandlePosition, RectangleF>
                {
                    { HandlePosition.NW, new RectangleF(Bounds.Left - s/2, Bounds.Top - s/2, s, s) },
                    { HandlePosition.NE, new RectangleF(Bounds.Right - s/2, Bounds.Top - s/2, s, s) },
                    { HandlePosition.SW, new RectangleF(Bounds.Left - s/2, Bounds.Bottom - s/2, s, s) },
                    { HandlePosition.SE, new RectangleF(Bounds.Right - s/2, Bounds.Bottom - s/2, s, s) }
                };
            }

            // 偵測是否點中縮放控制點
            public HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected) return HandlePosition.None;
                foreach (var handle in GetHandles())
                {
                    if (handle.Value.Contains(pt)) return handle.Key;
                }
                return HandlePosition.None;
            }

            // 取得四個方向的連線錨點 (上下左右中心)
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

        // --- 具體圖形實作 ---

        public class RectShape : ShapeBase
        {
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
            }
        }

        public class CircleShape : ShapeBase
        {
            public CircleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawEllipse(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
            }
        }

        public class TextNodeShape : ShapeBase
        {
            public string Text { get; set; } = "Node";
            public TextNodeShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                using (Pen p = new Pen(ShapeColor, 2)) g.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                using (Font font = new Font("Arial", 12))
                using (Brush b = new SolidBrush(ShapeColor))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(Text, font, b, Bounds, sf);
                }
            }
        }

        // 智慧連線實作 (兩端可附著於圖形錨點)
        public class ConnectorShape : ShapeBase
        {
            public ShapeBase SourceShape { get; set; }
            public ShapeBase TargetShape { get; set; }
            private PointF _startPt, _endPt;

            public ConnectorShape(PointF start, Color color) : base(start, color)
            {
                _startPt = start; _endPt = start;
            }

            public override void UpdateEndPoint(PointF pt) { _endPt = pt; }
            public override void Move(float dx, float dy) { /* 連線由附著點決定位置，手動移動無效 */ }
            public override PointF[] GetAnchors() { return new PointF[0]; } // 線條本身不提供錨點

            public override bool HitTest(PointF pt)
            {
                // 簡單的線段距離碰撞偵測
                PointF p1 = SourceShape != null ? GetClosestAnchor(SourceShape, _endPt) : _startPt;
                PointF p2 = TargetShape != null ? GetClosestAnchor(TargetShape, p1) : _endPt;
                float distance = Math.Abs((p2.Y - p1.Y) * pt.X - (p2.X - p1.X) * pt.Y + p2.X * p1.Y - p2.Y * p1.X) /
                                 (float)Math.Sqrt(Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.X - p1.X, 2));
                return distance < 5.0f;
            }

            private PointF GetClosestAnchor(ShapeBase shape, PointF target)
            {
                if (shape == null) return target;
                PointF bestAnchor = new PointF(0,0);
                float minDist = float.MaxValue;
                foreach (var anchor in shape.GetAnchors())
                {
                    float d = (anchor.X - target.X) * (anchor.X - target.X) + (anchor.Y - target.Y) * (anchor.Y - target.Y);
                    if (d < minDist) { minDist = d; bestAnchor = anchor; }
                }
                return bestAnchor;
            }

            public override void Draw(Graphics g)
            {
                PointF p1 = SourceShape != null ? GetClosestAnchor(SourceShape, _endPt) : _startPt;
                PointF p2 = TargetShape != null ? GetClosestAnchor(TargetShape, p1) : _endPt;

                // 畫箭頭
                using (Pen p = new Pen(ShapeColor, 2))
                {
                    p.CustomEndCap = new CustomLineCap(null, new GraphicsPath(new[] { new PointF(-2, -2), new PointF(0, 0), new PointF(2, -2) }, new[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line }));
                    g.DrawLine(p, p1, p2);
                }
            }
        }

        public static class ShapeFactory
        {
            public static ShapeBase CreateShape(ShapeType type, PointF start, Color color)
            {
                switch (type)
                {
                    case ShapeType.Line: return new ConnectorShape(start, color);
                    case ShapeType.Rectangle: return new RectShape(start, color);
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.TextNode: return new TextNodeShape(start, color);
                    default: return null;
                }
            }
        }
    }
}
