using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    /// <summary>
    /// 負責處理大部分一般圖形 (矩形、圓形等) 以及 Freehand 畫筆的拖曳建立邏輯
    /// </summary>
    public class DrawingTool : ToolBase
    {
        private App_Shapes.ShapeBase _tempShape = null;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            PointF snapPt = canvas.CurrentShapeType == App_Shapes.ShapeType.Freehand ? realPt : new PointF(canvas.Snap(realPt.X), canvas.Snap(realPt.Y));
            _tempShape = App_Shapes.ShapeFactory.CreateShape(canvas.CurrentShapeType, snapPt, canvas.CurrentColor);
            
            // 將暫存圖形交由 Canvas 統一渲染管理
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
                bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
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
            
            // 防止畫出太小 (誤點擊) 的無效圖形，除非是自由畫筆
            if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5 || _tempShape is App_Shapes.FreehandShape)
            {
                canvas.CmdManager.ExecuteCommand(new AddShapeCommand(canvas.Shapes, _tempShape));
            }
            
            // 如果是文字框，繪製結束後直接進入編輯模式
            if (_tempShape is App_Shapes.TextNodeShape)
            {
                canvas.StartInlineEditing(_tempShape);
                canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
            }

            canvas.SetTempShape(null);
            _tempShape = null;
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
