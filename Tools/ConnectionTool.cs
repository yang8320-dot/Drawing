using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public class ConnectionTool : ToolBase
    {
        private App_Shapes.ConnectorShape _tempConn;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            bool isArrow = (canvas.CurrentShapeType == App_Shapes.ShapeType.ArrowLine || canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            bool isOrtho = (canvas.CurrentShapeType == App_Shapes.ShapeType.OrthogonalLine);
            
            _tempConn = new App_Shapes.ConnectorShape(realPt, canvas.CurrentColor, isArrow, isOrtho);
            canvas.SetTempShape(_tempConn);
            
            var hitShape = canvas.GetShapeAtPoint(realPt);
            if (hitShape != null)
            {
                _tempConn.SourceId = hitShape.Id; 
                _tempConn.SourceAnchor = DetectAnchor(hitShape, realPt);
            }
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left || _tempConn == null) return;

            RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(new System.Collections.Generic.List<App_Shapes.ShapeBase> { _tempConn });
            
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
            var nearShapes = canvas.GetShapesInRect(new RectangleF(realPt.X - 15, realPt.Y - 15, 30, 30));
            
            App_Shapes.ShapeBase snapTarget = null;
            App_Shapes.AnchorPosition snapAnchor = App_Shapes.AnchorPosition.Auto;

            foreach (var other in nearShapes)
            {
                if (other.Id != _tempConn.SourceId && other.HitTest(realPt))
                {
                    snapTarget = other;
                    snapAnchor = DetectAnchor(other, realPt);
                    canvas.SetHoveredConnectionTarget(snapTarget, snapAnchor);
                    break;
                }
            }

            // 【Req 6: 自動磁吸 - 如果偵測到目標，強制將線條末端吸附到錨點座標】
            if (snapTarget != null && snapAnchor != App_Shapes.AnchorPosition.Auto)
            {
                _tempConn.UpdateEndPoint(snapTarget.GetAnchorPoint(snapAnchor));
            }
            else if (snapTarget != null)
            {
                _tempConn.UpdateEndPoint(snapTarget.GetIntersection(realPt));
            }
            else
            {
                _tempConn.UpdateEndPoint(realPt);
            }

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
            
            canvas.RequestToolChange(App_Shapes.ShapeType.Pointer);
            canvas.Invalidate();
        }

        public override bool OnKeyDown(App_CanvasControl canvas, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (_tempConn != null) { _tempConn.Dispose(); _tempConn = null; canvas.SetTempShape(null); }
                canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);
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
        }
    }
}
