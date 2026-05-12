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

        private PointF _dragStartPt; 
        private float _dragTotalDx = 0;
        private float _dragTotalDy = 0;

        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private RectangleF _initialBounds;
        private List<RectangleF> _initialBoundsList; // 用於記憶所有選取物件的初始位置，消除浮點數誤差
        private float _initialAngle;

        private Guid _oldSrcId, _oldTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA;
        private PointF _oldStart, _oldEnd;

        private RectangleF _boxSelectRect;

        public override void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;
            
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
                    // 紀錄所有拖曳物件的「絕對原始座標」
                    _initialBoundsList = canvas.SelectedShapes.Where(s => !s.IsLocked).Select(s => s.Bounds).ToList();
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
                // 空手移動時尋找對焦點提示 (若鎖點開啟)
                if (canvas.EnableObjectSnap) FindSnapPoint(canvas, realPt);
                else canvas.ActiveSnapPoint = null;
                
                if (canvas.ActiveSnapPoint != null) canvas.Invalidate();
                return;
            }

            if (_state == PointerState.Moving)
            {
                var movableShapes = canvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
                
                // 1. 計算絕對位移量
                float totalDx = realPt.X - _dragStartPt.X;
                float totalDy = realPt.Y - _dragStartPt.Y;

                canvas.ClearSmartGuides();
                canvas.ActiveSnapPoint = null;

                // 2. 正交約束
                if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift)) {
                    PointF orthoPt = ApplyOrtho(_dragStartPt, realPt);
                    totalDx = orthoPt.X - _dragStartPt.X;
                    totalDy = orthoPt.Y - _dragStartPt.Y;
                }

                // 3. 精確鎖點 (基於原始座標計算，0誤差)
                if (movableShapes.Count == 1 && canvas.EnableObjectSnap)
                {
                    var me = movableShapes[0];
                    float snapThreshold = 15.0f / canvas.ZoomFactor;
                    PointF bestSnap = PointF.Empty;
                    float minDistance = float.MaxValue;
                    bool snapped = false;

                    RectangleF initialB = _initialBoundsList[0];
                    PointF initialCenter = new PointF(initialB.X + initialB.Width/2, initialB.Y + initialB.Height/2);
                    
                    PointF[] myInitialPoints = new PointF[] {
                        initialCenter, new PointF(initialB.Left, initialB.Top), new PointF(initialB.Right, initialB.Top),
                        new PointF(initialB.Left, initialB.Bottom), new PointF(initialB.Right, initialB.Bottom)
                    };

                    var nearShapes = canvas.GetShapesInRect(new RectangleF(initialB.X + totalDx - 200, initialB.Y + totalDy - 200, 400, 400));

                    foreach (var other in nearShapes)
                    {
                        if (other == me || other is App_Shapes.ConnectorShape) continue;

                        PointF[] otherAnchors = new PointF[] {
                            other.GetCenter(), other.GetAnchorPoint(App_Shapes.AnchorPosition.Top), other.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom),
                            other.GetAnchorPoint(App_Shapes.AnchorPosition.Left), other.GetAnchorPoint(App_Shapes.AnchorPosition.Right),
                            new PointF(other.Bounds.Left, other.Bounds.Top), new PointF(other.Bounds.Right, other.Bounds.Top),
                            new PointF(other.Bounds.Left, other.Bounds.Bottom), new PointF(other.Bounds.Right, other.Bounds.Bottom)
                        };

                        foreach (var myPt in myInitialPoints)
                        {
                            PointF futureMyPt = new PointF(myPt.X + totalDx, myPt.Y + totalDy);
                            foreach (var otherPt in otherAnchors)
                            {
                                float d = Distance(futureMyPt, otherPt);
                                if (d < snapThreshold && d < minDistance)
                                {
                                    minDistance = d; bestSnap = otherPt;
                                    // 直接覆蓋位移量，讓原始點 100% 貼合對焦點
                                    totalDx = otherPt.X - myPt.X; 
                                    totalDy = otherPt.Y - myPt.Y;
                                    snapped = true;
                                }
                            }
                        }
                    }
                    if (snapped) canvas.ActiveSnapPoint = bestSnap;
                }

                // 4. 套用全新的絕對位移 (完全消除誤差)
                for (int i = 0; i < movableShapes.Count; i++) {
                    RectangleF ib = _initialBoundsList[i];
                    movableShapes[i].SetBounds(new RectangleF(ib.X + totalDx, ib.Y + totalDy, ib.Width, ib.Height));
                }
                
                _dragTotalDx = totalDx;
                _dragTotalDy = totalDy;

                canvas.Invalidate(); 
            }
            else if (_state == PointerState.Resizing)
            {
                var shape = canvas.SelectedShapes[0];

                if (shape is App_Shapes.ConnectorShape conn)
                {
                    PointF targetPt = realPt;
                    
                    if (canvas.EnableObjectSnap) targetPt = FindSnapPoint(canvas, targetPt, conn);
                    else canvas.ActiveSnapPoint = null;
                    
                    if (canvas.EnableOrthoMode || Control.ModifierKeys.HasFlag(Keys.Shift)) 
                        targetPt = ApplyOrtho(_dragStartPt, targetPt);

                    canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);

                    if (_resizingHandle == App_Shapes.HandlePosition.StartPoint) { conn.StartPt = targetPt; conn.SourceId = Guid.Empty; } 
                    else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint) { conn.EndPt = targetPt; conn.TargetId = Guid.Empty; }

                    if (canvas.EnableObjectSnap)
                    {
                        var nearShapes = canvas.GetShapesInRect(new RectangleF(targetPt.X - 5, targetPt.Y - 5, 10, 10));
                        foreach (var other in nearShapes) {
                            if (other != conn && other.HitTest(targetPt)) {
                                var hoverAnchor = DetectAnchor(other, targetPt);
                                canvas.SetHoveredConnectionTarget(other, hoverAnchor);
                                if (_resizingHandle == App_Shapes.HandlePosition.StartPoint) { conn.SourceId = other.Id; conn.SourceAnchor = hoverAnchor; } 
                                else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint) { conn.TargetId = other.Id; conn.TargetAnchor = hoverAnchor; }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    float totalDx = realPt.X - _dragStartPt.X;
                    float totalDy = realPt.Y - _dragStartPt.Y;
                    
                    canvas.ActiveSnapPoint = null;

                    if (canvas.EnableObjectSnap)
                    {
                        PointF handlePt = GetHandlePoint(_initialBounds, _resizingHandle);
                        PointF futureHandle = new PointF(handlePt.X + totalDx, handlePt.Y + totalDy);
                        PointF snappedHandle = FindSnapPoint(canvas, futureHandle, shape);
                        
                        if (canvas.ActiveSnapPoint != null) {
                            totalDx = snappedHandle.X - handlePt.X;
                            totalDy = snappedHandle.Y - handlePt.Y;
                        }
                    }

                    RectangleF b = _initialBounds;
                    bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
                    float ratio = 1.0f;
                    if (keepRatio && _initialBounds.Height != 0) ratio = _initialBounds.Width / _initialBounds.Height;

                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: 
                            float newW_NW = b.Width - totalDx; float newH_NW = b.Height - totalDy;
                            if (keepRatio) { if (Math.Abs(totalDx) > Math.Abs(totalDy)) newH_NW = newW_NW / ratio; else newW_NW = newH_NW * ratio; }
                            b = new RectangleF(b.Right - newW_NW, b.Bottom - newH_NW, newW_NW, newH_NW); break;
                        case App_Shapes.HandlePosition.N:  
                            b = new RectangleF(b.X, b.Y + totalDy, b.Width, b.Height - totalDy); break;
                        case App_Shapes.HandlePosition.NE: 
                            float newW_NE = b.Width + totalDx; float newH_NE = b.Height - totalDy;
                            if (keepRatio) { if (Math.Abs(totalDx) > Math.Abs(totalDy)) newH_NE = newW_NE / ratio; else newW_NE = newH_NE * ratio; }
                            b = new RectangleF(b.X, b.Bottom - newH_NE, newW_NE, newH_NE); break;
                        case App_Shapes.HandlePosition.E:  
                            b = new RectangleF(b.X, b.Y, b.Width + totalDx, b.Height); break;
                        case App_Shapes.HandlePosition.SE: 
                            float newW_SE = b.Width + totalDx; float newH_SE = b.Height + totalDy;
                            if (keepRatio) { if (Math.Abs(totalDx) > Math.Abs(totalDy)) newH_SE = newW_SE / ratio; else newW_SE = newH_SE * ratio; }
                            b = new RectangleF(b.X, b.Y, newW_SE, newH_SE); break;
                        case App_Shapes.HandlePosition.S:  
                            b = new RectangleF(b.X, b.Y, b.Width, b.Height + totalDy); break;
                        case App_Shapes.HandlePosition.SW: 
                            float newW_SW = b.Width - totalDx; float newH_SW = b.Height + totalDy;
                            if (keepRatio) { if (Math.Abs(totalDx) > Math.Abs(totalDy)) newH_SW = newW_SW / ratio; else newW_SW = newH_SW * ratio; }
                            b = new RectangleF(b.Right - newW_SW, b.Y, newW_SW, newH_SW); break;
                        case App_Shapes.HandlePosition.W:  
                            b = new RectangleF(b.X + totalDx, b.Y, b.Width - totalDx, b.Height); break;
                    }
                    if (b.Width > 5 && b.Height > 5) shape.SetBounds(b);
                }
                canvas.Invalidate();
            }
            else if (_state == PointerState.Rotating)
            {
                var me = canvas.SelectedShapes[0];
                PointF center = me.GetCenter();
                float angle = (float)(Math.Atan2(realPt.Y - center.Y, realPt.X - center.X) * 180 / Math.PI) + 90;
                me.RotationAngle = canvas.SnapAngle(angle, 15f); 
                canvas.Invalidate();
            }
            else if (_state == PointerState.BoxSelecting)
            {
                _boxSelectRect = new RectangleF(
                    Math.Min(_boxSelectRect.X, realPt.X), Math.Min(_boxSelectRect.Y, realPt.Y),
                    Math.Abs(realPt.X - _boxSelectRect.X), Math.Abs(realPt.Y - _boxSelectRect.Y)
                );
                
                if (!Control.ModifierKeys.HasFlag(Keys.Control) && !Control.ModifierKeys.HasFlag(Keys.Shift)) canvas.ClearSelection();

                var nearShapes = canvas.GetShapesInRect(_boxSelectRect);
                foreach (var s in nearShapes) {
                    if (s.HitTest(new PointF(_boxSelectRect.X + _boxSelectRect.Width/2, _boxSelectRect.Y + _boxSelectRect.Height/2)) || _boxSelectRect.IntersectsWith(s.Bounds)) {
                        if (!canvas.SelectedShapes.Contains(s)) { s.IsSelected = true; canvas.SelectedShapes.Add(s); }
                    }
                }
                canvas.TriggerSelectionChanged();
                canvas.Invalidate();
            }
        }

        public override void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt)
        {
            if (e.Button != MouseButtons.Left) return;

            canvas.ClearSmartGuides();
            canvas.ActiveSnapPoint = null; 
            canvas.SetHoveredConnectionTarget(null, App_Shapes.AnchorPosition.Auto);

            var movableShapes = canvas.SelectedShapes.Where(s => !s.IsLocked).ToList();

            if (_state == PointerState.Moving && movableShapes.Count > 0 && (_dragTotalDx != 0 || _dragTotalDy != 0))
            {
                // 執行 Command 前，先把物件還原回一開始的位置，確保 Undo/Redo 也是絕對無誤差
                for (int i = 0; i < movableShapes.Count; i++) {
                    movableShapes[i].SetBounds(_initialBoundsList[i]);
                }
                canvas.CmdManager.ExecuteCommand(new MoveShapesCommand(movableShapes, _dragTotalDx, _dragTotalDy));
            }
            else if (_state == PointerState.Resizing && canvas.SelectedShapes.Count == 1 && !canvas.SelectedShapes[0].IsLocked)
            {
                if (canvas.SelectedShapes[0] is App_Shapes.ConnectorShape conn)
                {
                    canvas.CmdManager.ExecuteCommand(new AdjustConnectorCommand(
                        conn, _oldSrcId, _oldTgtId, _oldSA, _oldTA, _oldStart, _oldEnd,
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

        private PointF GetHandlePoint(RectangleF b, App_Shapes.HandlePosition handle) 
        {
            switch (handle) {
                case App_Shapes.HandlePosition.NW: return new PointF(b.Left, b.Top);
                case App_Shapes.HandlePosition.N: return new PointF(b.Left + b.Width/2, b.Top);
                case App_Shapes.HandlePosition.NE: return new PointF(b.Right, b.Top);
                case App_Shapes.HandlePosition.W: return new PointF(b.Left, b.Top + b.Height/2);
                case App_Shapes.HandlePosition.E: return new PointF(b.Right, b.Top + b.Height/2);
                case App_Shapes.HandlePosition.SW: return new PointF(b.Left, b.Bottom);
                case App_Shapes.HandlePosition.S: return new PointF(b.Left + b.Width/2, b.Bottom);
                case App_Shapes.HandlePosition.SE: return new PointF(b.Right, b.Bottom);
                default: return new PointF(b.Left, b.Top);
            }
        }
    }
}
