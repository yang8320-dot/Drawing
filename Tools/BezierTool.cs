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
                
                // 【修正3】: 鋼筆工具創建時也套用預設格式
                _bezierShape.ApplyFormatFrom(canvas.DefaultFormatTemplate);
                _bezierShape.FillColor = Color.Transparent; // 鋼筆預設不填色

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
                _bezierShape.UpdateLastControlPoint(realPt);
            }
            canvas.Invalidate(); 
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt) { }

        public override void OnPaint(App_CanvasControl canvas, Graphics g)
        {
            if (_bezierShape != null && _bezierShape.LocalNodes.Count > 0)
            {
                PointF lastPt = _bezierShape.LocalNodes.Last().Anchor;
                PointF currentMouseReal = canvas.GetRealPointFromMouse(); 
                
                using (Pen guidePen = new Pen(Color.CornflowerBlue, 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(guidePen, _bezierShape.Bounds.X + lastPt.X, _bezierShape.Bounds.Y + lastPt.Y, currentMouseReal.X, currentMouseReal.Y);
                }
            }
        }

        public override bool OnKeyDown(App_CanvasControl canvas, Keys keyData)
        {
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
