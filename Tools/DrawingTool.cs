// ============================================================
// FILE: Tools/DrawingTool.cs
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public class DrawingTool : ToolBase
    {
        private App_Shapes.ShapeBase _tempShape = null;
        private PointF _startRealPt;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            // 第一下點擊也能抓取對焦點
            PointF snapPt = FindSnapPoint(canvas, realPt);
            
            // 如果沒抓到對焦點，才套用格線鎖點
            if (canvas.ActiveSnapPoint == null && canvas.CurrentShapeType != App_Shapes.ShapeType.Freehand)
            {
                snapPt = new PointF(canvas.Snap(snapPt.X), canvas.Snap(snapPt.Y));
            }
            
            _startRealPt = snapPt;
            _tempShape = App_Shapes.ShapeFactory.CreateShape(canvas.CurrentShapeType, snapPt, canvas.CurrentColor);
            _tempShape.ApplyFormatFrom(canvas.DefaultFormatTemplate);
            
            canvas.SetTempShape(_tempShape);
            canvas.Invalidate();
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempShape == null) 
            {
                // 空手移動時尋找對焦點提示
                FindSnapPoint(canvas, realPt);
                if (canvas.ActiveSnapPoint != null) canvas.Invalidate();
                return;
            }

            RectangleF oldBounds = _tempShape.Bounds;
            PointF targetPt = realPt;

            if (_tempShape is App_Shapes.FreehandShape fh)
            {
                fh.AddPoint(targetPt);
            }
            else
            {
                // 1. 尋找物件鎖點 (優先)
                targetPt = FindSnapPoint(canvas, realPt, _tempShape);
                
                // 2. 如果沒有吸附到物件，才套用格線
                if (canvas.ActiveSnapPoint == null)
                {
                    targetPt = new PointF(canvas.Snap(targetPt.X), canvas.Snap(targetPt.Y));
                }

                // 3. 套用正交約束 (Shift 或 UI 勾選)
                if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    targetPt = ApplyOrtho(_startRealPt, targetPt);
                }

                _tempShape.UpdateEndPoint(targetPt);
            }

            canvas.InvalidateWorldRect(oldBounds);
            canvas.InvalidateWorldRect(_tempShape.Bounds);
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempShape == null) return;

            _tempShape.NormalizeBounds();
            
            if (!(_tempShape is App_Shapes.FreehandShape) && _tempShape.Bounds.Width <= 5 && _tempShape.Bounds.Height <= 5)
            {
                _tempShape.SetBounds(new RectangleF(_tempShape.Bounds.X, _tempShape.Bounds.Y, 100, 100));
            }

            if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5 || _tempShape is App_Shapes.FreehandShape)
            {
                canvas.CmdManager.ExecuteCommand(new AddShapeCommand(canvas.Shapes, _tempShape));
            }
            
            if (_tempShape is App_Shapes.TextNodeShape)
            {
                canvas.StartInlineEditing(_tempShape);
            }

            canvas.SetTempShape(null);
            _tempShape = null;
            canvas.ActiveSnapPoint = null; // 清除視覺

            canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
            canvas.Invalidate();
        }

        public override bool OnKeyDown(App_CanvasControl canvas, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (_tempShape != null)
                {
                    _tempShape.Dispose();
                    _tempShape = null;
                    canvas.SetTempShape(null);
                    canvas.ActiveSnapPoint = null;
                    canvas.Invalidate();
                }
                canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
                return true;
            }
            return false;
        }

        public override void OnToolDeactivated(App_CanvasControl canvas)
        {
            if (_tempShape != null) { _tempShape.Dispose(); _tempShape = null; canvas.SetTempShape(null); }
            canvas.ActiveSnapPoint = null;
        }
    }
}
