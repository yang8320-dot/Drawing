// ============================================================
// FILE: Tools/PointerTool.cs
// ============================================================

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
        private PointF _dragStartPt; // 記錄拖曳起始點，用於正交模式計算
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
            _dragStartPt = realPt;
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
                    if (canvas.SelectedShapes.Contains(hitShape)) { hitShape.IsSelected = false; canvas.SelectedShapes.Remove(hitShape); }
                    else { hitShape.IsSelected = true; canvas.SelectedShapes.Add(hitShape); }
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
            if (e.Button != MouseButtons.Left) 
            {
                // 空手移動尋找對焦點 (連線修改或拖拉端點用)
                PointF hoverSnap = FindSnapPoint(canvas, realPt);
                if (canvas.ActiveSnapPoint != null) canvas.Invalidate();
                return;
            }

            PointF targetPt = realPt;

            if (_state == PointerState.Moving)
            {
                var movableShapes = canvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
                RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(movableShapes);

                // 如果只有一個物件且啟用正交，套用正交限制
                if (movableShapes.Count == 1 && (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift)))
                {
                    targetPt = ApplyOrtho(_dragStartPt, targetPt);
                }

                // 計算相對於上一個影格的移動量
                float dx = targetPt.X - _lastMouseRealPt.X;
                float dy = targetPt.Y - _lastMouseRealPt.Y;

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
                    canvas.ActiveSnapPoint = null;

                    foreach (var other in nearShapes)
                    {
                        if (other == me || other is App_Shapes.ConnectorShape) continue;

                        PointF otherCenter = other.GetCenter();
                        
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

                        // 捕捉對焦點邏輯 (綠點)
                        PointF[] anchors = new PointF[] {
                            other.GetAnchorPoint(App_Shapes.AnchorPosition.Top),
                            other.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom),
                            other.GetAnchorPoint(App_Shapes.AnchorPosition.Left),
                            other.GetAnchorPoint(App_Shapes.AnchorPosition.Right)
                        };

                        foreach (var anchor in anchors)
                        {
                            if (Distance(new PointF(futureCenterX, futureCenterY), anchor) < snapThreshold * 3)
                            {
                                bestDx = anchor.X - myCenter.X;
                                bestDy = anchor.Y - myCenter.Y;
                                canvas.ActiveSnapPoint = anchor;
                                break;
                            }
                        }
                    }
                    dx = bestDx;
                    dy = bestDy;
                }

                _dragTotalDx += dx;
                _dragTotalDy += dy;
                for (int i = 0; i < movableShapes.Count; i++) movableShapes[i].Move(dx, dy);

                RectangleF newBounds = canvas.GetShapesAndConnectorsBounds(movableShapes);

                if (newBounds.Right > canvas.PageSize.Width - 100) canvas.PageSize = new SizeF(Math.Max(canvas.PageSize.Width, newBounds.Right + 500), canvas.PageSize.Height);
                if (newBounds.Bottom > canvas.PageSize.Height - 100) canvas.PageSize = new SizeF(canvas.PageSize.Width, Math.Max(canvas.PageSize.Height, newBounds.Bottom + 500));

                _lastMouseRealPt = new PointF(_lastMouseRealPt.X + dx, _lastMouseRealPt.Y + dy); // 更新滑鼠位置基準
                
                canvas.Invalidate(); // 強制刷新綠色對焦點
            }
            else if (_state == PointerState.Resizing && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                var shape = canvas.SelectedShapes[0];
                RectangleF oldBounds = canvas.GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });

                if (shape is App_Shapes.ConnectorShape conn)
                {
                    // 連線端點拖曳時啟動對焦點
                    targetPt = FindSnapPoint(canvas, realPt, conn);
                    if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift)) targetPt = ApplyOrtho(_dragStartPt, targetPt);

                    canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);

                    if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                    {
                        conn.StartPt = targetPt;
                        conn.SourceId = Guid.Empty;
                    }
                    else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                    {
                        conn.EndPt = targetPt;
                        conn.TargetId = Guid.Empty;
                    }

                    var nearShapes = canvas.GetShapesInRect(new RectangleF(targetPt.X - 5, targetPt.Y - 5, 10, 10));

                    foreach (var other in nearShapes)
                    {
                        if (other != conn && other.HitTest(targetPt))
                        {
                            var hoverAnchor = DetectAnchor(other, targetPt);
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
                    // 一般物件拉伸
                    float ldx = targetPt.X - _lastMouseRealPt.X;
                    float ldy = targetPt.Y - _lastMouseRealPt.Y;

                    RectangleF b = shape.Bounds;
                    bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
                    float ratio = 1.0f;
                    if (keepRatio && _initialBounds.Height != 0) ratio = _initialBounds.Width / _initialBounds.Height;

                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: 
                            float newW_NW = b.Width - ldx; float newH_NW = b.Height - ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NW = newW_NW / ratio; else newW_NW = newH_NW * ratio; }
                            b = new RectangleF(b.Right - newW_NW, b.Bottom - newH_NW, newW_NW, newH_NW);
                            break;
                        case App_Shapes.HandlePosition.N:  
                            b = new RectangleF(b.X, b.Y + ldy, b.Width, b.Height - ldy); 
                            break;
                        case App_Shapes.HandlePosition.NE: 
                            float newW_NE = b.Width + ldx; float newH_NE = b.Height - ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NE = newW_NE / ratio; else newW_NE = newH_NE * ratio; }
                            b = new RectangleF(b.X, b.Bottom - newH_NE, newW_NE, newH_NE);
                            break;
                        case App_Shapes.HandlePosition.E:  
                            b = new RectangleF(b.X, b.Y, b.Width + ldx, b.Height); 
                            break;
                        case App_Shapes.HandlePosition.SE: 
                            float newW_SE = b.Width + ldx; float newH_SE = b.Height + ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SE = newW_SE / ratio; else newW_SE = newH_SE * ratio; }
                            b = new RectangleF(b.X, b.Y, newW_SE, newH_SE);
                            break;
                        case App_Shapes.HandlePosition.S:  
                            b = new RectangleF(b.X, b.Y, b.Width, b.Height + ldy); 
                            break;
                        case App_Shapes.HandlePosition.SW: 
                            float newW_SW = b.Width - ldx; float newH_SW = b.Height + ldy;
                            if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SW = newW_SW / ratio; else newW_SW = newH_SW * ratio; }
                            b = new RectangleF(b.Right - newW_SW, b.Y, newW_SW, newH_SW);
                            break;
                        case App_Shapes.HandlePosition.W:  
                            b = new RectangleF(b.X + ldx, b.Y, b.Width - ldx, b.Height); 
                            break;
                    }
                    if (b.Width > 5 && b.Height > 5) shape.SetBounds(b);
                }

                _lastMouseRealPt = targetPt;
                canvas.Invalidate();
            }
            // (其餘 Rotating 與 BoxSelecting 邏輯無關位置鎖定，省略贅述維持原樣)
            else if (_state == PointerState.Rotating && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                var me = canvas.SelectedShapes[0];
                RectangleF oldBounds = me.Bounds;
                PointF center = me.GetCenter();
                float angle = (float)(Math.Atan2(realPt.Y - center.Y, realPt.X - center.X) * 180 / Math.PI) + 90;
                me.RotationAngle = canvas.SnapAngle(angle, 15f); 
                
                canvas.InvalidateWorldRect(oldBounds);
                canvas.InvalidateWorldRect(me.Bounds);
                _lastMouseRealPt = realPt;
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
                _lastMouseRealPt = realPt;
            }
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            canvas.ClearSmartGuides();
            canvas.ActiveSnapPoint = null; // 清除對焦點視覺
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
            canvas.ActiveSnapPoint = null;
        }
    }
}
