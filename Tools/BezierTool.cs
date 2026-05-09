using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public class BezierTool : ToolBase
    {
        private App_Shapes.BezierShape _bezierShape = null;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            if (_bezierShape == null)
            {
                _bezierShape = (App_Shapes.BezierShape)App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.BezierPen, realPt, canvas.CurrentColor);
                _bezierShape.FillColor = Color.Transparent;
                canvas.SetTempShape(_bezierShape);
            }
            else
            {
                _bezierShape.AddNode(realPt);
            }
            canvas.Invalidate();
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (_bezierShape == null || _bezierShape.LocalNodes.Count == 0) return;

            if (e.Button == MouseButtons.Left)
            {
                // 滑鼠按住拖曳時：拉出控制桿
                _bezierShape.UpdateLastControlPoint(realPt);
            }
            canvas.Invalidate(); // 持續更新以繪製懸停導引線
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            // 鋼筆工具在放開滑鼠時不會結束繪製，而是等待下一次點擊 (或 ESC/Enter 結束)
        }

        public override void OnPaint(App_CanvasControl canvas, Graphics g)
        {
            // 繪製滑鼠懸停導引線 (Rubber-band line)
            if (_bezierShape != null && _bezierShape.LocalNodes.Count > 0)
            {
                PointF lastPt = _bezierShape.LocalNodes.Last().Anchor;
                PointF currentMouseReal = canvas.GetRealPointFromMouse(); // 需要從 Canvas 取得當前滑鼠位置
                
                using (Pen guidePen = new Pen(Color.CornflowerBlue, 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(guidePen, _bezierShape.Bounds.X + lastPt.X, _bezierShape.Bounds.Y + lastPt.Y, currentMouseReal.X, currentMouseReal.Y);
                }
            }
        }

        public override bool OnKeyDown(App_CanvasControl canvas, Keys keyData)
        {
            // 按 ESC 或 Enter 結束鋼筆繪製並將結果存入
            if (keyData == Keys.Escape || keyData == Keys.Enter)
            {
                FinishDrawing(canvas);
                return true;
            }
            return false;
        }

        public override void OnToolDeactivated(App_CanvasControl canvas)
        {
            FinishDrawing(canvas);
        }

        private void FinishDrawing(App_CanvasControl canvas)
        {
            if (_bezierShape != null)
            {
                _bezierShape.NormalizeBounds();
                if (_bezierShape.LocalNodes.Count > 1)
                {
                    canvas.CmdManager.ExecuteCommand(new AddShapeCommand(canvas.Shapes, _bezierShape));
                }
                else
                {
                    _bezierShape.Dispose();
                }

                _bezierShape = null;
                canvas.SetTempShape(null);
                canvas.Invalidate();
            }
        }
    }
}
