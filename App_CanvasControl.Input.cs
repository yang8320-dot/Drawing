// ============================================================
// FILE: App_CanvasControl.Input.cs
// ============================================================

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    public partial class App_CanvasControl
    {
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
            if (_inlineTextBox.Visible && !_inlineTextBox.Bounds.Contains(e.Location)) CommitInlineText();
            PointF realPt = GetRealPoint(e.Location);
            _lastMousePos = e.Location;

            if (ShowRulers && (e.X <= RULER_SIZE || e.Y <= RULER_SIZE)) return;

            if (e.Button == MouseButtons.Left && _minimapRect.Contains(e.Location))
            {
                _isDraggingMinimap = true;
                UpdateCameraFromMinimap(e.Location);
                return;
            }

            if (e.Button == MouseButtons.Middle || _isPanning || (e.Button == MouseButtons.Left && CurrentTool == App_Shapes.ShapeType.HandPan))
            {
                _isPanning = true;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    TriggerImageInsert(realPt);
                    RequestToolChange(App_Shapes.ShapeType.Pointer);
                    return;
                }
                else if (CurrentTool == App_Shapes.ShapeType.FormatPainter)
                {
                    if (FormatSourceShape != null)
                    {
                        var hit = GetShapeAtPoint(realPt);
                        if (hit != null && !hit.IsLocked)
                        {
                            var cmd = new ChangeFormatCommand(new System.Collections.Generic.List<App_Shapes.ShapeBase> { hit });
                            hit.ApplyFormatFrom(FormatSourceShape);
                            cmd.CaptureNewState();
                            CmdManager.ExecuteCommand(cmd);
                        }
                    }
                    return;
                }
                _currentToolInstance?.OnMouseDown(this, e, realPt);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            _currentMouseScreenPos = e.Location;

            if (_isDraggingMinimap) { UpdateCameraFromMinimap(e.Location); return; }

            if (_isPanning)
            {
                _cameraOffset.X += e.X - _lastMousePos.X;
                _cameraOffset.Y += e.Y - _lastMousePos.Y;
                _lastMousePos = e.Location;
                this.Invalidate();
                return;
            }

            UpdateCursor(realPt);
            _currentToolInstance?.OnMouseMove(this, e, realPt);

            if (e.Button == MouseButtons.None && ShowRulers)
            {
                this.Invalidate(new Rectangle(0, 0, this.Width, RULER_SIZE));
                this.Invalidate(new Rectangle(0, 0, RULER_SIZE, this.Height));
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDraggingMinimap) { _isDraggingMinimap = false; return; }

            if (_isPanning && e.Button != MouseButtons.Middle && Control.ModifierKeys != Keys.Space)
            {
                _isPanning = false;
                UpdateCursor(GetRealPoint(e.Location));
            }

            if (e.Button == MouseButtons.Left) _currentToolInstance?.OnMouseUp(this, e, GetRealPoint(e.Location));
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                float oldZoom = ZoomFactor;
                float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
                SetZoom(ZoomFactor * zoomDelta);

                _cameraOffset.X = e.X - (e.X - _cameraOffset.X) * (ZoomFactor / oldZoom);
                _cameraOffset.Y = e.Y - (e.Y - _cameraOffset.Y) * (ZoomFactor / oldZoom);
                this.Invalidate();
            }
            else if (Control.ModifierKeys == Keys.Shift)
            {
                _cameraOffset.X += e.Delta > 0 ? 50 : -50;
                if (_inlineTextBox.Visible) CancelInlineText();
                this.Invalidate();
            }
            else
            {
                _cameraOffset.Y += e.Delta > 0 ? 50 : -50;
                if (_inlineTextBox.Visible) CancelInlineText();
                this.Invalidate();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                _cameraOffset.X += delta > 0 ? -50 : 50; 
                if (_inlineTextBox.Visible) CancelInlineText();
                this.Invalidate();
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_inlineTextBox.Focused) return base.ProcessCmdKey(ref msg, keyData);

            if (_currentToolInstance != null && _currentToolInstance.OnKeyDown(this, keyData)) return true;

            if (keyData == Keys.Escape || keyData == Keys.Enter) { RequestToolChange(App_Shapes.ShapeType.Pointer); return true; }

            if (keyData == Keys.V) { RequestToolChange(App_Shapes.ShapeType.Pointer); return true; }
            if (keyData == Keys.H) { RequestToolChange(App_Shapes.ShapeType.HandPan); return true; }
            if (keyData == Keys.T) { RequestToolChange(App_Shapes.ShapeType.TextNode); return true; }
            if (keyData == Keys.P) { RequestToolChange(App_Shapes.ShapeType.Freehand); return true; }
            if (keyData == Keys.B) { RequestToolChange(App_Shapes.ShapeType.BezierPen); return true; }
            if (keyData == Keys.L) { RequestToolChange(App_Shapes.ShapeType.OrthogonalLine); return true; }
            if (keyData == Keys.R) { RequestToolChange(App_Shapes.ShapeType.Rectangle); return true; }

            // === 快捷鍵實作 ===
            if (keyData == (Keys.Control | Keys.A)) 
            { 
                ClearSelection(); 
                foreach (var s in Shapes) { s.IsSelected = true; SelectedShapes.Add(s); }
                TriggerSelectionChanged();
                this.Invalidate();
                return true; 
            }
            if (keyData == (Keys.Control | Keys.X)) 
            { 
                Copy(); 
                DeleteSelectedShapes();
                return true; 
            }
            if (keyData == (Keys.Control | Keys.Z)) { CmdManager.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { CmdManager.Redo(); return true; }
            if (keyData == (Keys.Control | Keys.G)) { GroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.U)) { UngroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            if (keyData == (Keys.Control | Keys.D)) { DuplicateSelected(); return true; }

            Keys baseKey = keyData & ~Keys.Modifiers;
            if (SelectedShapes.Count > 0 && (baseKey == Keys.Up || baseKey == Keys.Down || baseKey == Keys.Left || baseKey == Keys.Right))
            {
                bool isShift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                float nudgeAmount = isShift ? 10f : 1f;
                float dx = 0, dy = 0;

                if (baseKey == Keys.Up) dy = -nudgeAmount;
                if (baseKey == Keys.Down) dy = nudgeAmount;
                if (baseKey == Keys.Left) dx = -nudgeAmount;
                if (baseKey == Keys.Right) dx = nudgeAmount;

                var movableShapes = SelectedShapes.Where(s => !s.IsLocked).ToList();
                if (movableShapes.Count > 0) CmdManager.ExecuteCommand(new MoveShapesCommand(movableShapes, dx, dy));
                return true;
            }

            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                DeleteSelectedShapes();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DeleteSelectedShapes()
        {
            var toRemove = new System.Collections.Generic.List<App_Shapes.ShapeBase>();
            for (int i = 0; i < SelectedShapes.Count; i++) if (!SelectedShapes[i].IsLocked) toRemove.Add(SelectedShapes[i]);
            
            if (toRemove.Count == 0) return;

            int initialCount = toRemove.Count;
            for (int i = 0; i < initialCount; i++)
            {
                var s = toRemove[i];
                for (int j = 0; j < Shapes.Count; j++)
                {
                    if (Shapes[j] is App_Shapes.ConnectorShape c && (c.SourceId == s.Id || c.TargetId == s.Id))
                    {
                        if (!toRemove.Contains(c)) toRemove.Add(c);
                    }
                }
            }
            
            CmdManager.ExecuteCommand(new RemoveShapesCommand(Shapes, toRemove));
            SelectedShapes.RemoveAll(s => !s.IsLocked);
            TriggerSelectionChanged();
            this.Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_inlineTextBox.Focused) return;
            if (e.KeyCode == Keys.Space && !_isPanning)
            {
                _isPanning = true;
                this.Cursor = Cursors.Hand;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_inlineTextBox.Focused) return;
            if (e.KeyCode == Keys.Space)
            {
                _isPanning = false;
                UpdateCursor(GetRealPoint(_currentMouseScreenPos));
                CurrentTool = _previousToolType; 
            }
            base.OnKeyUp(e);
        }

        private void Canvas_DoubleClick(object sender, MouseEventArgs e)
        {
            if (SelectedShapes.Count == 1)
            {
                PointF realPt = GetRealPoint(e.Location);
                App_Shapes.ShapeBase targetShape = SelectedShapes[0];

                if (targetShape is App_Shapes.GroupShape group)
                {
                    var hitChild = GetHitChild(group, realPt);
                    if (hitChild != null && !(hitChild is App_Shapes.ConnectorShape))
                        targetShape = hitChild;
                }

                if (!(targetShape is App_Shapes.ConnectorShape) && !(targetShape is App_Shapes.GroupShape))
                {
                    StartInlineEditing(targetShape);
                }
            }
        }

        private App_Shapes.ShapeBase GetHitChild(App_Shapes.GroupShape group, PointF pt)
        {
            for (int i = group.Children.Count - 1; i >= 0; i--)
            {
                var child = group.Children[i];
                if (child is App_Shapes.GroupShape subGroup)
                {
                    var hit = GetHitChild(subGroup, pt);
                    if (hit != null) return hit;
                }
                else if (child.HitTest(pt)) return child;
            }
            return null;
        }

        private void UpdateCursor(PointF realPt)
        {
            if (CurrentTool == App_Shapes.ShapeType.HandPan || _isPanning) { this.Cursor = Cursors.Hand; return; }
            if (CurrentTool == App_Shapes.ShapeType.FormatPainter) { this.Cursor = Cursors.UpArrow; return; }
            if (CurrentTool != App_Shapes.ShapeType.Pointer) { this.Cursor = Cursors.Cross; return; }

            if (SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
            {
                var handle = SelectedShapes[0].HitTestHandle(realPt);
                switch (handle)
                {
                    case App_Shapes.HandlePosition.NW:
                    case App_Shapes.HandlePosition.SE: this.Cursor = Cursors.SizeNWSE; return;
                    case App_Shapes.HandlePosition.NE:
                    case App_Shapes.HandlePosition.SW: this.Cursor = Cursors.SizeNESW; return;
                    case App_Shapes.HandlePosition.N:
                    case App_Shapes.HandlePosition.S:  this.Cursor = Cursors.SizeNS; return;
                    case App_Shapes.HandlePosition.E:
                    case App_Shapes.HandlePosition.W:  this.Cursor = Cursors.SizeWE; return;
                    case App_Shapes.HandlePosition.Rotate: this.Cursor = Cursors.Hand; return; 
                    case App_Shapes.HandlePosition.StartPoint:
                    case App_Shapes.HandlePosition.EndPoint: this.Cursor = Cursors.SizeAll; return;
                }
            }

            var hitShape = GetShapeAtPoint(realPt);
            this.Cursor = hitShape != null ? (hitShape.IsLocked ? Cursors.No : Cursors.SizeAll) : Cursors.Default;
        }

        private void Canvas_DragEnter(object sender, DragEventArgs e) => e.Effect = e.Data.GetDataPresent(typeof(App_Shapes.ShapeType)) ? DragDropEffects.Copy : DragDropEffects.None;
        
        private void Canvas_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(App_Shapes.ShapeType)))
            {
                App_Shapes.ShapeType type = (App_Shapes.ShapeType)e.Data.GetData(typeof(App_Shapes.ShapeType));
                PointF realPt = GetRealPoint(this.PointToClient(new Point(e.X, e.Y)));
                PointF snapPt = new PointF(Snap(realPt.X), Snap(realPt.Y));
                var newShape = App_Shapes.ShapeFactory.CreateShape(type, snapPt, CurrentColor);
                if (newShape != null)
                {
                    newShape.UpdateEndPoint(new PointF(snapPt.X + 80, snapPt.Y + 80)); 
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newShape));
                    ClearSelection(); newShape.IsSelected = true; SelectedShapes.Add(newShape);
                    TriggerSelectionChanged();
                    this.Invalidate();
                }
            }
        }
    }
}
