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

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            PointF snapPt = canvas.CurrentShapeType == App_Shapes.ShapeType.Freehand ? realPt : new PointF(canvas.Snap(realPt.X), canvas.Snap(realPt.Y));
            _tempShape = App_Shapes.ShapeFactory.CreateShape(canvas.CurrentShapeType, snapPt, canvas.CurrentColor);
            
            // 【Req 9: 新增圖形時，自動套用使用者先前的設定格式】
            _tempShape.ApplyFormatFrom(canvas.DefaultFormatTemplate);
            
            canvas.SetTempShape(_tempShape);
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempShape == null) return;

            RectangleF oldBounds = _tempShape.Bounds;

            if (_tempShape is App_Shapes.FreehandShape fh)
            {
                fh.AddPoint(realPt);
            }
            else
            {
                // 【Req 1: 整合 Shift 鍵與 UI 的正交模式選項】
                bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift) || canvas.EnableOrthoMode;
                float snapX = canvas.Snap(realPt.X);
                float snapY = canvas.Snap(realPt.Y);
                
                if (keepRatio)
                {
                    float diffX = snapX - _tempShape.Bounds.X;
                    float diffY = snapY - _tempShape.Bounds.Y;
                    float maxDim = (float)Math.Max(Math.Abs(diffX), Math.Abs(diffY));
                    snapX = _tempShape.Bounds.X + maxDim * Math.Sign(diffX);
                    snapY = _tempShape.Bounds.Y + maxDim * Math.Sign(diffY);
                }
                _tempShape.UpdateEndPoint(new PointF(snapX, snapY));
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
                    canvas.Invalidate();
                }
                canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
                return true;
            }
            return false;
        }

        public override void OnToolDeactivated(App_CanvasControl canvas)
        {
            if (_tempShape != null)
            {
                _tempShape.Dispose();
                _tempShape = null;
                canvas.SetTempShape(null);
            }
        }
    }
}
