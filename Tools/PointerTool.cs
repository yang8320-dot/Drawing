using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    public class PointerTool : ToolBase
    {
        private enum PointerState { Idle, Moving, Resizing, Rotating, BoxSelecting }
        private PointerState _state = PointerState.Idle;

        private PointF _lastMouseRealPt;
        private float _dragTotalDx = 0;
        private float _dragTotalDy = 0;

        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private RectangleF _initialBounds;
        private float _initialAngle;

        private Guid _oldSrcId, _oldTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA;
        private PointF _oldStart, _oldEnd;

        private RectangleF _boxSelectRect;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;
            
            _lastMouseRealPt = realPt;
            _dragTotalDx = 0; 
            _dragTotalDy = 0;

            if (canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                var handle = canvas.SelectedShapes[0].HitTestHandle(realPt);
                if (handle == App_Shapes.HandlePosition.Rotate)
                {
                    _state = PointerState.Rotating;
                    _initialAngle = canvas.SelectedShapes[0].RotationAngle;
                    return;
                }
                else if (handle != App_Shapes.HandlePosition.None)
                {
                    _state = PointerState.Resizing;
                    _resizingHandle = handle;
                    
                    if (canvas.SelectedShapes[0] is App_Shapes.ConnectorShape conn)
                    {
                        _oldSrcId = conn.SourceId; _oldTgtId = conn.TargetId;
                        _oldSA = conn.SourceAnchor; _oldTA = conn.TargetAnchor;
                        _oldStart = conn.StartPt; _oldEnd = conn.EndPt;
                    }
                    else
                    {
                        _initialBounds = canvas.SelectedShapes[0].Bounds;
                    }
                    return;
                }
            }

            App_Shapes.ShapeBase hitShape = canvas.GetShapeAtPoint(realPt);

            if (hitShape != null)
            {
                bool isMultiSelectKey = (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift);
                
                if (isMultiSelectKey)
                {
                    if (canvas.SelectedShapes.Contains(hitShape))
                    {
                        hitShape.IsSelected = false;
                        canvas.SelectedShapes.Remove(hitShape);
                    }
                    else
                    {
                        hitShape.IsSelected = true;
                        canvas.SelectedShapes.Add(hitShape);
                    }
                }
                else
                {
                    if (!canvas.SelectedShapes.Contains(hitShape))
                    {
                        canvas.ClearSelection();
                        hitShape.IsSelected = true;
                        canvas.SelectedShapes.Add(hitShape);
                    }
                }
                
                canvas.TriggerSelectionChanged();

                if (canvas.SelectedShapes.Any(s => !s.IsLocked))
                {
                    _state = PointerState.Moving;
                }
            }
            else
            {
                canvas.ClearSelection();
                canvas.TriggerSelectionChanged();
                
                _state = PointerState.BoxSelecting;
                _boxSelectRect = new RectangleF(realPt.X, realPt.Y, 0, 0);
            }
        }

        public override void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            float dx = realPt.X - _lastMouseRealPt.X;
            float dy = realPt.Y - _lastMouseRealPt.Y;

            if (_state == PointerState.Moving)
            {
                var movableShapes = canvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
                RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(movableShapes);

                if (movableShapes.Count == 1)
                {
                    var me = movableShapes[0];
                    float snapThreshold = 5.0f / canvas.ZoomFactor;

                    float bestDx = dx, bestDy = dy;
                    PointF myCenter = me.GetCenter();
                    float futureCenterX = myCenter.X + dx;
                    float futureCenterY = myCenter.Y + dy;

                    var nearShapes = canvas.GetShapesInRect(new RectangleF(futureCenterX - 200, futureCenterY - 200, 400, 400));
                    
                    canvas.ClearSmartGuides();

                    foreach (var other in nearShapes)
                    {
                        if (other == me || other is App_Shapes.ConnectorShape) continue;

                        PointF otherCenter = other.GetCenter();
                        
                        // 【新增：中心點對齊】
                        if (Math.Abs(futureCenterX - otherCenter.X) < snapThreshold)
                        {
                            bestDx = otherCenter.X - myCenter.X;
                            canvas.AddSmartGuide(new PointF(otherCenter.X, -10000), new PointF(otherCenter.X, 10000));
                        }
                        if (Math.Abs(futureCenterY - otherCenter.Y) < snapThreshold)
                        {
                            bestDy = otherCenter.Y - myCenter.Y;
                            canvas.AddSmartGuide(new PointF(-10000, otherCenter.Y), new PointF(10000, otherCenter.Y));
                        }

                        // 【新增：等距與等寬對齊提示 (Snap to Distance / Size)】
                        if (Math.Abs((me.Bounds.Right + dx) - other.Bounds.Left) < snapThreshold)
                        {
                            bestDx = other.Bounds.Left - me.Bounds.Width - me.Bounds.X;
                            canvas.AddSmartGuide(new PointF(other.Bounds.Left, -10000), new PointF(other.Bounds.Left, 10000));
                        }
                        if (Math.Abs((me.Bounds.Left + dx) - other.Bounds.Right) < snapThreshold)
                        {
                            bestDx = other.Bounds.Right - me.Bounds.X;
                            canvas.AddSmartGuide(new PointF(other.Bounds.Right, -10000), new PointF(other.Bounds.Right, 10000));
                        }
                    }
                    dx = bestDx;
                    dy = bestDy;
                }

                _dragTotalDx += dx;
                _dragTotalDy += dy;
                for (int i = 0; i < movableShapes.Count; i++) movableShapes[i].Move(dx, dy);

                RectangleF newBounds = canvas.GetShapesAndConnectorsBounds(movableShapes);

                // 【新增：畫布自動擴張 (Auto-Expand Canvas)】
                if (newBounds.Right > canvas.PageSize.Width - 100)
                    canvas.PageSize = new SizeF(Math.Max(canvas.PageSize.Width, newBounds.Right + 500), canvas.PageSize.Height);
                if (newBounds.Bottom > canvas.PageSize.Height - 100)
                    canvas.PageSize = new SizeF(canvas.PageSize.Width, Math.Max(canvas.PageSize.Height, newBounds.Bottom + 500));

                if (canvas.HasSmartGuides()) canvas.Invalidate();
                else
                {
                    canvas.InvalidateWorldRect(oldBounds);
                    canvas.InvalidateWorldRect(newBounds);
                    canvas.InvalidateMinimap();
                }
            }
            else if (_state == PointerState.Rotating && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                var me = canvas.SelectedShapes[0];
                RectangleF oldBounds = me.Bounds;
                PointF center = me.GetCenter();
                float angle = (float)(Math.Atan2(realPt.Y - center.Y, realPt.X - center.X) * 180 / Math.PI) + 90;
                me.RotationAngle = canvas.SnapAngle(angle, 15f); 
                
                canvas.InvalidateWorldRect(oldBounds);
                canvas.InvalidateWorldRect(me.Bounds);
            }
            else if (_state == PointerState.BoxSelecting)
            {
                RectangleF oldBox = _boxSelectRect;
                _boxSelectRect = new RectangleF(
                    Math.Min(_boxSelectRect.X, realPt.X),
                    Math.Min(_boxSelectRect.Y, realPt.Y),
                    Math.Abs(realPt.X - _boxSelectRect.X),
                    Math.Abs(realPt.Y - _boxSelectRect.Y)
                );
                
                bool isMultiSelectKey = (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift);
                if (!isMultiSelectKey) canvas.ClearSelection();

                var nearShapes = canvas.GetShapesInRect(_boxSelectRect);

                foreach (var s in nearShapes)
                {
                    if (s.HitTest(new PointF(_boxSelectRect.X + _boxSelectRect.Width/2, _boxSelectRect.Y + _boxSelectRect.Height/2)) || _boxSelectRect.IntersectsWith(s.Bounds))
                    {
                        if (!canvas.SelectedShapes.Contains(s))
                        {
                            s.IsSelected = true;
                            canvas.SelectedShapes.Add(s);
                        }
                    }
                }
                
                canvas.TriggerSelectionChanged();
                
                canvas.InvalidateWorldRect(oldBox);
                canvas.InvalidateWorldRect(_boxSelectRect);
            }
            else if (_state == PointerState.Resizing && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                var shape = canvas.SelectedShapes[0];
                RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });

                if (shape is App_Shapes.ConnectorShape conn)
                {
                    canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);

                    if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                    {
                        conn.StartPt = realPt;
                        conn.SourceId = Guid.Empty;
                    }
                    else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                    {
                        conn.EndPt = realPt;
                        conn.TargetId = Guid.Empty;
                    }

                    var nearShapes = canvas.GetShapesInRect(new RectangleF(realPt.X - 5, realPt.Y - 5, 10, 10));

                    foreach (var other in nearShapes)
                    {
                        if (other != conn && other.HitTest(realPt))
                        {
                            var hoverAnchor = DetectAnchor(other, realPt);
                            canvas.SetHoveredConnectionTarget(other, hoverAnchor);

                            if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                            {
                                conn.SourceId = other.Id;
                                conn.SourceAnchor = hoverAnchor;
                            }
                            else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                            {
                                conn.TargetId = other.Id;
                                conn.TargetAnchor = hoverAnchor;
                            }
                            break;
                        }
                    }
                }
                else 
                {
                    PointF center = shape.GetCenter();
                    PointF lastLocal = App_Shapes.ShapeBase.RotatePoint(_lastMouseRealPt, center, -shape.RotationAngle);
                    PointF currentLocal = App_Shapes.ShapeBase.RotatePoint(realPt, center, -shape.RotationAngle);
                    
                    float ldx = currentLocal.X - lastLocal.X;
                    float ldy = currentLocal.Y - lastLocal.Y;

                    RectangleF b = shape.Bounds;
                    bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
                    float ratio = 1.0f;
                    if (keepRatio && _initialBounds.Height != 0) 
                        ratio = _initialBounds.Width / _initialBounds.Height;

                    // 【新增：調整大小時捕捉周圍物件相同長寬 (Snap to Size)】
                    float snapThreshold = 5.0f / canvas.ZoomFactor;
                    var nearShapes = canvas.GetShapesInRect(new RectangleF(center.X - 200, center.Y - 200, 400, 400));
                    canvas.ClearSmartGuides();

                    foreach (var other in nearShapes)
                    {
                        if (other == shape || other is App_Shapes.ConnectorShape) continue;
                        
                        float futureWidth = b.Width;
                        float futureHeight = b.Height;

                        if (Math.Abs((b.Width + ldx) - other.Bounds.Width) < snapThreshold)
                        {
                            ldx = other.Bounds.Width - b.Width;
                            canvas.AddSmartGuide(new PointF(shape.Bounds.X, shape.Bounds.Bottom + 10), new PointF(shape.Bounds.Right + ldx, shape.Bounds.Bottom + 10));
                        }
                        if (Math.Abs((b.Height + ldy) - other.Bounds.Height) < snapThreshold)
                        {
                            ldy = other.Bounds.Height - b.Height;
                            canvas.AddSmartGuide(new PointF(shape.Bounds.Right + 10, shape.Bounds.Y), new PointF(shape.Bounds.Right + 10, shape.Bounds.Bottom + ldy));
                        }
                    }

                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: 
                            float newW_NW = b.Width - ldx;
                            float newH_NW = b.Height - ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NW = newW_NW / ratio; else newW_NW = newH_NW * ratio; }
                            b = new RectangleF(b.Right - newW_NW, b.Bottom - newH_NW, newW_NW, newH_NW);
                            break;
                        case App_Shapes.HandlePosition.N:  
                            b = new RectangleF(b.X, b.Y + ldy, b.Width, b.Height - ldy); 
                            if (keepRatio) { float oldC = b.X + b.Width/2; b.Width = b.Height * ratio; b.X = oldC - b.Width/2; }
                            break;
                        case App_Shapes.HandlePosition.NE: 
                            float newW_NE = b.Width + ldx;
                            float newH_NE = b.Height - ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NE = newW_NE / ratio; else newW_NE = newH_NE * ratio; }
                            b = new RectangleF(b.X, b.Bottom - newH_NE, newW_NE, newH_NE);
                            break;
                        case App_Shapes.HandlePosition.E:  
                            b = new RectangleF(b.X, b.Y, b.Width + ldx, b.Height); 
                            if (keepRatio) { float oldC = b.Y + b.Height/2; b.Height = b.Width / ratio; b.Y = oldC - b.Height/2; }
                            break;
                        case App_Shapes.HandlePosition.SE: 
                            float newW_SE = b.Width + ldx;
                            float newH_SE = b.Height + ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SE = newW_SE / ratio; else newW_SE = newH_SE * ratio; }
                            b = new RectangleF(b.X, b.Y, newW_SE, newH_SE);
                            break;
                        case App_Shapes.HandlePosition.S:  
                            b = new RectangleF(b.X, b.Y, b.Width, b.Height + ldy); 
                            if (keepRatio) { float oldC = b.X + b.Width/2; b.Width = b.Height * ratio; b.X = oldC - b.Width/2; }
                            break;
                        case App_Shapes.HandlePosition.SW: 
                            float newW_SW = b.Width - ldx;
                            float newH_SW = b.Height + ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SW = newW_SW / ratio; else newW_SW = newH_SW * ratio; }
                            b = new RectangleF(b.Right - newW_SW, b.Y, newW_SW, newH_SW);
                            break;
                        case App_Shapes.HandlePosition.W:  
                            b = new RectangleF(b.X + ldx, b.Y, b.Width - ldx, b.Height); 
                            if (keepRatio) { float oldC = b.Y + b.Height/2; b.Height = b.Width / ratio; b.Y = oldC - b.Height/2; }
                            break;
                    }

                    if (b.Width > 5 && b.Height > 5) shape.SetBounds(b);
                }

                RectangleF newBounds = canvas.GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });

                if (canvas.HasSmartGuides()) canvas.Invalidate();
                else
                {
                    canvas.InvalidateWorldRect(oldBounds);
                    canvas.InvalidateWorldRect(newBounds);
                    canvas.InvalidateMinimap();
                }
            }

            _lastMouseRealPt = realPt;
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            canvas.ClearSmartGuides();
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);

            var movableShapes = canvas.SelectedShapes.Where(s => !s.IsLocked).ToList();

            if (_state == PointerState.Moving && movableShapes.Count > 0 && (_dragTotalDx != 0 || _dragTotalDy != 0))
            {
                for (int i = 0; i < movableShapes.Count; i++) movableShapes[i].Move(-_dragTotalDx, -_dragTotalDy);
                canvas.CmdManager.ExecuteCommand(new MoveShapesCommand(movableShapes, _dragTotalDx, _dragTotalDy));
            }
            else if (_state == PointerState.Resizing && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                if (canvas.SelectedShapes[0] is App_Shapes.ConnectorShape conn)
                {
                    canvas.CmdManager.ExecuteCommand(new AdjustConnectorCommand(
                        conn, 
                        _oldSrcId, _oldTgtId, _oldSA, _oldTA, _oldStart, _oldEnd,
                        conn.SourceId, conn.TargetId, conn.SourceAnchor, conn.TargetAnchor, conn.StartPt, conn.EndPt));
                }
                else
                {
                    canvas.CmdManager.ExecuteCommand(new ResizeShapeCommand(canvas.SelectedShapes[0], _initialBounds, canvas.SelectedShapes[0].Bounds));
                }
            }
            else if (_state == PointerState.Rotating && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                canvas.CmdManager.ExecuteCommand(new RotateShapeCommand(canvas.SelectedShapes[0], _initialAngle, canvas.SelectedShapes[0].RotationAngle));
            }

            _state = PointerState.Idle;
            canvas.Invalidate();
        }

        public override void OnPaint(App_CanvasControl canvas, Graphics g)
        {
            if (_state == PointerState.BoxSelecting)
            {
                using (Pen p = new Pen(Color.CornflowerBlue) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                using (Brush b = new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)))
                {
                    g.FillRectangle(b, _boxSelectRect);
                    g.DrawRectangle(p, Rectangle.Round(_boxSelectRect));
                }
            }
        }

        public override void OnToolDeactivated(App_CanvasControl canvas)
        {
            _state = PointerState.Idle;
            canvas.ClearSmartGuides();
        }
    }
}
