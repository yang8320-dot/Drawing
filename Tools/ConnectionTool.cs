// ============================================================
// FILE: Tools/ConnectionTool.cs
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public class ConnectionTool : ToolBase
    {
        private App_Shapes.ConnectorShape _tempConn;
        private PointF _startRealPt;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            PointF snapPt = FindSnapPoint(canvas, realPt);
            _startRealPt = snapPt;

            bool isArrow = (canvas.CurrentShapeType == App_Shapes.ShapeType.ArrowLine || canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            bool isOrtho = (canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            
            _tempConn = new App_Shapes.ConnectorShape(snapPt, canvas.CurrentColor, isArrow, isOrtho);
            _tempConn.ApplyFormatFrom(canvas.DefaultFormatTemplate);

            canvas.SetTempShape(_tempConn);
            
            var hitShape = canvas.GetShapeAtPoint(snapPt);
            if (hitShape != null)
            {
                _tempConn.SourceId = hitShape.Id; 
                _tempConn.SourceAnchor = DetectAnchor(hitShape, snapPt);
            }
            canvas.Invalidate();
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempConn == null) 
            {
                FindSnapPoint(canvas, realPt);
                if (canvas.ActiveSnapPoint != null) canvas.Invalidate();
                return;
            }

            RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(new System.Collections.Generic.List<App_Shapes.ShapeBase> { _tempConn });
            
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
            
            // 尋找對焦點
            PointF targetPt = FindSnapPoint(canvas, realPt, _tempConn);

            // 正交約束
            if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift))
            {
                targetPt = ApplyOrtho(_startRealPt, targetPt);
            }

            var nearShapes = canvas.GetShapesInRect(new RectangleF(targetPt.X - 15, targetPt.Y - 15, 30, 30));
            
            App_Shapes.ShapeBase snapTarget = null;
            App_Shapes.AnchorPosition snapAnchor = App_Shapes.AnchorPosition.Auto;

            foreach (var other in nearShapes)
            {
                if (other.Id != _tempConn.SourceId)
                {
                    App_Shapes.AnchorPosition tempAnchor = DetectAnchor(other, targetPt);
                    if (tempAnchor != App_Shapes.AnchorPosition.Auto)
                    {
                        snapTarget = other;
                        snapAnchor = tempAnchor;
                        canvas.SetHoveredConnectionTarget(snapTarget, snapAnchor);
                        break;
                    }
                    else if (other.HitTest(targetPt))
                    {
                        snapTarget = other;
                        snapAnchor = App_Shapes.AnchorPosition.Auto;
                        canvas.SetHoveredConnectionTarget(snapTarget, snapAnchor);
                        break;
                    }
                }
            }

            // 確保綠色對焦點絕對精準
            if (snapTarget != null && snapAnchor != App_Shapes.AnchorPosition.Auto)
                _tempConn.UpdateEndPoint(snapTarget.GetAnchorPoint(snapAnchor));
            else if (canvas.ActiveSnapPoint != null)
                _tempConn.UpdateEndPoint(canvas.ActiveSnapPoint.Value); // 強制使用綠色對焦點
            else if (snapTarget != null)
                _tempConn.UpdateEndPoint(snapTarget.GetIntersection(targetPt));
            else
                _tempConn.UpdateEndPoint(targetPt);

            RectangleF newBounds = canvas.GetShapesAndConnectorsBounds(new System.Collections.Generic.List<App_Shapes.ShapeBase> { _tempConn });
            canvas.InvalidateWorldRect(oldBounds);
            canvas.InvalidateWorldRect(newBounds);
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempConn == null) return;

            var hoverTarget = canvas.GetHoveredConnectionTarget();
            if (hoverTarget != null)
            {
                _tempConn.TargetId = hoverTarget.Id;
                _tempConn.TargetAnchor = canvas.GetHoveredAnchor();
            }

            canvas.CmdManager.ExecuteCommand(new AddShapeCommand(canvas.Shapes, _tempConn));
            
            canvas.SetTempShape(null);
            _tempConn = null;
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
            canvas.ActiveSnapPoint = null;
            
            canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
            canvas.Invalidate();
        }

        public override bool OnKeyDown(App_CanvasControl canvas, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (_tempConn != null) { _tempConn.Dispose(); _tempConn = null; canvas.SetTempShape(null); }
                canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
                canvas.ActiveSnapPoint = null;
                canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
                canvas.Invalidate();
                return true;
            }
            return false;
        }

        public override void OnToolDeactivated(App_CanvasControl canvas)
        {
            if (_tempConn != null) { _tempConn.Dispose(); _tempConn = null; canvas.SetTempShape(null); }
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
            canvas.ActiveSnapPoint = null;
        }
    }
}
