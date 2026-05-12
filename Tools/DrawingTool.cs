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

            PointF snapPt = realPt;
            
            // 只有開啟鎖點，才去尋找對焦點
            if (canvas.EnableObjectSnap) snapPt = FindSnapPoint(canvas, realPt);
            else canvas.ActiveSnapPoint = null;
            
            // 若沒鎖到物件，套用網格鎖點
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
                if (canvas.EnableObjectSnap) FindSnapPoint(canvas, realPt);
                else canvas.ActiveSnapPoint = null;
                
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
                if (canvas.EnableObjectSnap) targetPt = FindSnapPoint(canvas, realPt, _tempShape);
                else canvas.ActiveSnapPoint = null;
                
                if (canvas.ActiveSnapPoint == null)
                {
                    targetPt = new PointF(canvas.Snap(targetPt.X), canvas.Snap(targetPt.Y));
                }

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
            canvas.ActiveSnapPoint = null;

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
