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
            
            // 【修正3】: 新增連線時也要套用預設格式 (粗細/顏色/字型等)
            _tempConn.ApplyFormatFrom(canvas.DefaultFormatTemplate);

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

            // 【修正2】: 優化尋找錨點邏輯，優先判斷錨點，且不符合時不隨意磁吸整個物件邊框
            foreach (var other in nearShapes)
            {
                if (other.Id != _tempConn.SourceId)
                {
                    App_Shapes.AnchorPosition tempAnchor = DetectAnchor(other, realPt);
                    if (tempAnchor != App_Shapes.AnchorPosition.Auto)
                    {
                        snapTarget = other;
                        snapAnchor = tempAnchor;
                        canvas.SetHoveredConnectionTarget(snapTarget, snapAnchor);
                        break;
                    }
                    else if (other.HitTest(realPt))
                    {
                        snapTarget = other;
                        snapAnchor = App_Shapes.AnchorPosition.Auto;
                        canvas.SetHoveredConnectionTarget(snapTarget, snapAnchor);
                        break;
                    }
                }
            }

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
