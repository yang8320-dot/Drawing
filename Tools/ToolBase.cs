// ============================================================
// FILE: Tools/ToolBase.cs
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public abstract class ToolBase : ITool
    {
        public virtual void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnPaint(App_CanvasControl canvas, Graphics g) { }
        public virtual bool OnKeyDown(App_CanvasControl canvas, Keys keyData) { return false; }
        public virtual void OnToolDeactivated(App_CanvasControl canvas) { }

        protected float Distance(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        protected App_Shapes.AnchorPosition DetectAnchor(App_Shapes.ShapeBase shape, PointF pt)
        {
            float threshold = 10f;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Top), pt) < threshold) return App_Shapes.AnchorPosition.Top;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom), pt) < threshold) return App_Shapes.AnchorPosition.Bottom;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Left), pt) < threshold) return App_Shapes.AnchorPosition.Left;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Right), pt) < threshold) return App_Shapes.AnchorPosition.Right;
            return App_Shapes.AnchorPosition.Auto;
        }

        /// <summary>
        /// 提供正交模式強制約束 (嚴格 90 度：只能上下或左右)
        /// </summary>
        protected PointF ApplyOrtho(PointF startPt, PointF currentPt)
        {
            float dx = Math.Abs(currentPt.X - startPt.X);
            float dy = Math.Abs(currentPt.Y - startPt.Y);

            // 若橫向移動幅度大於縱向，強制水平線；否則強制垂直線
            if (dx > dy)
            {
                return new PointF(currentPt.X, startPt.Y);
            }
            else
            {
                return new PointF(startPt.X, currentPt.Y);
            }
        }

        /// <summary>
        /// 尋找並回傳最接近的物件鎖點，若找到則更新 canvas 的 ActiveSnapPoint 產生視覺回饋
        /// </summary>
        protected PointF FindSnapPoint(App_CanvasControl canvas, PointF pt, App_Shapes.ShapeBase ignoreShape = null)
        {
            if (!canvas.EnableObjectSnap)
            {
                canvas.ActiveSnapPoint = null;
                return pt;
            }

            float snapRadius = 15f / canvas.ZoomFactor;
            var nearShapes = canvas.GetShapesInRect(new RectangleF(pt.X - snapRadius, pt.Y - snapRadius, snapRadius * 2, snapRadius * 2));

            foreach (var shape in nearShapes)
            {
                if (shape == ignoreShape || shape is App_Shapes.ConnectorShape) continue;

                PointF[] anchors = new PointF[] {
                    shape.GetAnchorPoint(App_Shapes.AnchorPosition.Top),
                    shape.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom),
                    shape.GetAnchorPoint(App_Shapes.AnchorPosition.Left),
                    shape.GetAnchorPoint(App_Shapes.AnchorPosition.Right),
                    shape.GetCenter(),
                    new PointF(shape.Bounds.Left, shape.Bounds.Top),
                    new PointF(shape.Bounds.Right, shape.Bounds.Top),
                    new PointF(shape.Bounds.Left, shape.Bounds.Bottom),
                    new PointF(shape.Bounds.Right, shape.Bounds.Bottom)
                };

                foreach (var anchor in anchors)
                {
                    if (Distance(pt, anchor) < snapRadius)
                    {
                        canvas.ActiveSnapPoint = anchor;
                        return anchor;
                    }
                }
            }

            canvas.ActiveSnapPoint = null;
            return pt;
        }
    }
}
