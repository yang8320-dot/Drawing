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

            PointF snapPt = realPt;
            if (canvas.EnableObjectSnap) snapPt = FindSnapPoint(canvas, realPt);
            else canvas.ActiveSnapPoint = null;
            
            _startRealPt = snapPt;

            bool isArrow = (canvas.CurrentShapeType == App_Shapes.ShapeType.ArrowLine || canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            bool isOrtho = (canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            
            _tempConn = new App_Shapes.ConnectorShape(snapPt, canvas.CurrentColor, isArrow, isOrtho);
            _tempConn.ApplyFormatFrom(canvas.DefaultFormatTemplate);

            canvas.SetTempShape(_tempConn);
            
            if (canvas.EnableObjectSnap)
            {
                var hitShape = canvas.GetShapeAtPoint(snapPt);
                if (hitShape != null)
                {
                    _tempConn.SourceId = hitShape.Id; 
                    _tempConn.SourceAnchor = DetectAnchor(hitShape, snapPt);
                }
            }
            canvas.Invalidate();
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempConn == null) 
            {
                if (canvas.EnableObjectSnap) FindSnapPoint(canvas, realPt);
                else canvas.ActiveSnapPoint = null;
                
                if (canvas.ActiveSnapPoint != null) canvas.Invalidate();
                return;
            }

            RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(new System.Collections.Generic.List<App_Shapes.ShapeBase> { _tempConn });
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
            
            PointF targetPt = realPt;
            if (canvas.EnableObjectSnap) targetPt = FindSnapPoint(canvas, realPt, _tempConn);
            else canvas.ActiveSnapPoint = null;

            if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift))
            {
                targetPt = ApplyOrtho(_startRealPt, targetPt);
            }

            App_Shapes.ShapeBase snapTarget = null;
            App_Shapes.AnchorPosition snapAnchor = App_Shapes.AnchorPosition.Auto;

            if (canvas.EnableObjectSnap)
            {
                var nearShapes = canvas.GetShapesInRect(new RectangleF(targetPt.X - 15, targetPt.Y - 15, 30, 30));
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
            }

            if (snapTarget != null && snapAnchor != App_Shapes.AnchorPosition.Auto)
                _tempConn.UpdateEndPoint(snapTarget.GetAnchorPoint(snapAnchor));
            else if (canvas.ActiveSnapPoint != null)
                _tempConn.UpdateEndPoint(canvas.ActiveSnapPoint.Value);
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
            if (hoverTarget != null && canvas.EnableObjectSnap)
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
