using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    /// <summary>
    /// 工具基底類別，提供預設空實作，讓繼承的具體工具只需覆寫需要的方法。
    /// </summary>
    public abstract class ToolBase : ITool
    {
        public virtual void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }
        public virtual void OnPaint(App_CanvasControl canvas, Graphics g) { }
        public virtual bool OnKeyDown(App_CanvasControl canvas, Keys keyData) { return false; }
        public virtual void OnToolDeactivated(App_CanvasControl canvas) { }

        /// <summary>
        /// 輔助方法：計算距離
        /// </summary>
        protected float Distance(PointF p1, PointF p2)
        {
            return (float)System.Math.Sqrt(System.Math.Pow(p1.X - p2.X, 2) + System.Math.Pow(p1.Y - p2.Y, 2));
        }

        /// <summary>
        /// 輔助方法：偵測目標形狀最接近的錨點
        /// </summary>
        protected App_Shapes.AnchorPosition DetectAnchor(App_Shapes.ShapeBase shape, PointF pt)
        {
            // 【修正2】: 縮小磁吸半徑由 15f 降為 10f，避免干擾游標繪製直線
            float threshold = 10f;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Top), pt) < threshold) return App_Shapes.AnchorPosition.Top;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom), pt) < threshold) return App_Shapes.AnchorPosition.Bottom;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Left), pt) < threshold) return App_Shapes.AnchorPosition.Left;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Right), pt) < threshold) return App_Shapes.AnchorPosition.Right;
            return App_Shapes.AnchorPosition.Auto;
        }
    }
}
