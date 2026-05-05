using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    public class App_CanvasControl : Panel
    {
        public List<App_Shapes.ShapeBase> Shapes { get; set; } = new List<App_Shapes.ShapeBase>();
        public SizeF PageSize { get; set; } = new SizeF(2100, 2970);
        
        public CommandManager CmdManager { get; } = new CommandManager();

        // 縮放與平移
        public float ZoomFactor { get; private set; } = 1.0f;
        private PointF _cameraOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;
        
        private float _dragTotalDx = 0;
        private float _dragTotalDy = 0;

        private enum InteractionState { Idle, Drawing, Moving, Resizing, Connecting, BoxSelecting }
        private InteractionState _currentState = InteractionState.Idle;

        public App_Shapes.ShapeType CurrentTool { get; set; } = App_Shapes.ShapeType.Pointer;
        public Color CurrentColor { get; set; } = Color.Black;
        
        private App_Shapes.ShapeBase _tempShape = null;
        private RectangleF _boxSelectRect;
        
        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private List<App_Shapes.ShapeBase> _clipboard = new List<App_Shapes.ShapeBase>();
        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;

        public event Action<App_Shapes.ShapeBase> OnShapeDoubleClicked;
        public event Action<PointF> OnImageInsertRequested;
        public event Action OnToolResetRequested;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            
            this.ContextMenuStrip = CreateContextMenu();
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.MouseWheel += Canvas_MouseWheel;
            this.DoubleClick += Canvas_DoubleClick;

            CmdManager.OnStateChanged += () => this.Invalidate();
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("複製 (Ctrl+C)", null, (s, e) => Copy());
            menu.Items.Add("貼上 (Ctrl+V)", null, (s, e) => Paste());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0));
            menu.Items.Add("移到最下層", null, (s, e) => ChangeZIndex(-99));
            return menu;
        }

        private void ChangeZIndex(int direction)
        {
            if (SelectedShapes.Count == 0) return;
            foreach (var s in SelectedShapes)
            {
                int index = Shapes.IndexOf(s);
                if (index == -1) continue;
                
                Shapes.RemoveAt(index);
                if (direction == 0) Shapes.Add(s);
                else if (direction == -99) Shapes.Insert(0, s);
            }
            this.Invalidate();
        }

        private void Canvas_DoubleClick(object sender, EventArgs e)
        {
            if (SelectedShapes.Count == 1) OnShapeDoubleClicked?.Invoke(SelectedShapes[0]);
        }
        
        public void SetZoom(float zoom)
        {
            ZoomFactor = Math.Max(0.2f, Math.Min(zoom, 5.0f));
            this.Invalidate();
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                float oldZoom = ZoomFactor;
                float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
                ZoomFactor = Math.Max(0.2f, Math.Min(ZoomFactor * zoomDelta, 5.0f));

                _cameraOffset.X = e.X - (e.X - _cameraOffset.X) * (ZoomFactor / oldZoom);
                _cameraOffset.Y = e.Y - (e.Y - _cameraOffset.Y) * (ZoomFactor / oldZoom);
                
                this.Invalidate();
            }
            else
            {
                // 普通滾輪上下平移畫布
                _cameraOffset.Y += e.Delta > 0 ? 50 : -50;
                this.Invalidate();
            }
        }

        private PointF GetRealPoint(Point screenPt)
        {
            return new PointF((screenPt.X - _cameraOffset.X) / ZoomFactor, (screenPt.Y - _cameraOffset.Y) / ZoomFactor);
        }

        private float Snap(float value, float gridSize = 20f)
        {
            return (float)Math.Round(value / gridSize) * gridSize;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { OnToolResetRequested?.Invoke(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { CmdManager.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { CmdManager.Redo(); return true; }

            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                var toRemove = new List<App_Shapes.ShapeBase>(SelectedShapes);
                foreach (var s in SelectedShapes)
                {
                    toRemove.AddRange(Shapes.OfType<App_Shapes.ConnectorShape>().Where(c => c.SourceId == s.Id || c.TargetId == s.Id));
                }
                CmdManager.ExecuteCommand(new RemoveShapesCommand(Shapes, toRemove));
                SelectedShapes.Clear();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Copy() { if (SelectedShapes.Count > 0) _clipboard = App_SaveLoad.CloneShapes(SelectedShapes); }
        private void Paste()
        {
            if (_clipboard.Count == 0) return;
            SelectedShapes.ForEach(s => s.IsSelected = false);
            SelectedShapes.Clear();
            
            var newClones = App_SaveLoad.CloneShapes(_clipboard);
            foreach (var s in newClones)
            {
                s.Id = Guid.NewGuid();
                s.Move(20, 20);
                s.IsSelected = true;
                CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, s));
                SelectedShapes.Add(s);
            }
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
            PointF realPt = GetRealPoint(e.Location);
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Space))
            {
                _isPanning = true;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (CurrentTool == App_Shapes.ShapeType.Pointer)
                {
                    if (SelectedShapes.Count == 1)
                    {
                        var handle = SelectedShapes[0].HitTestHandle(realPt);
                        if (handle != App_Shapes.HandlePosition.None)
                        {
                            _currentState = InteractionState.Resizing;
                            _resizingHandle = handle;
                            return;
                        }
                    }

                    var hit = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    if (hit != null)
                    {
                        if (!SelectedShapes.Contains(hit) && Control.ModifierKeys != Keys.Control)
                        {
                            SelectedShapes.ForEach(s => s.IsSelected = false);
                            SelectedShapes.Clear();
                        }
                        hit.IsSelected = true;
                        if (!SelectedShapes.Contains(hit)) SelectedShapes.Add(hit);
                        
                        _currentState = InteractionState.Moving;
                        _dragTotalDx = 0; _dragTotalDy = 0;
                    }
                    else
                    {
                        SelectedShapes.ForEach(s => s.IsSelected = false);
                        SelectedShapes.Clear();
                        _currentState = InteractionState.BoxSelecting;
                        _boxSelectRect = new RectangleF(realPt.X, realPt.Y, 0, 0);
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    OnImageInsertRequested?.Invoke(realPt);
                    OnToolResetRequested?.Invoke();
                }
                else if (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.StraightLine || CurrentTool == App_Shapes.ShapeType.OrthogonalLine)
                {
                    _currentState = InteractionState.Connecting;
                    bool isArrow = (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.OrthogonalLine);
                    bool isOrtho = (CurrentTool == App_Shapes.ShapeType.OrthogonalLine);
                    
                    _tempShape = new App_Shapes.ConnectorShape(realPt, CurrentColor, isArrow, isOrtho);
                    var srcShape = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    if (srcShape != null) ((App_Shapes.ConnectorShape)_tempShape).SourceId = srcShape.Id;
                }
                else
                {
                    _currentState = InteractionState.Drawing;
                    PointF snapPt = new PointF(Snap(realPt.X), Snap(realPt.Y));
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, snapPt, CurrentColor);
                }
                this.Invalidate();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);

            if (_isPanning)
            {
                _cameraOffset.X += e.X - _lastMousePos.X;
                _cameraOffset.Y += e.Y - _lastMousePos.Y;
                _lastMousePos = e.Location;
                this.Invalidate();
                return;
            }
            
            if (e.Button == MouseButtons.Left)
            {
                float dx = (e.X - _lastMousePos.X) / ZoomFactor;
                float dy = (e.Y - _lastMousePos.Y) / ZoomFactor;

                if (_currentState == InteractionState.Moving)
                {
                    _dragTotalDx += dx;
                    _dragTotalDy += dy;
                    foreach (var s in SelectedShapes) s.Move(dx, dy);
                }
                else if (_currentState == InteractionState.BoxSelecting)
                {
                    _boxSelectRect = new RectangleF(
                        Math.Min(_boxSelectRect.X, realPt.X),
                        Math.Min(_boxSelectRect.Y, realPt.Y),
                        Math.Abs(realPt.X - _boxSelectRect.X),
                        Math.Abs(realPt.Y - _boxSelectRect.Y)
                    );
                    SelectedShapes.ForEach(s => s.IsSelected = false);
                    SelectedShapes = Shapes.Where(s => _boxSelectRect.IntersectsWith(s.Bounds)).ToList();
                    SelectedShapes.ForEach(s => s.IsSelected = true);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1)
                {
                    var b = SelectedShapes[0].Bounds;
                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: b = new RectangleF(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.SE: b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height + dy); break;
                    }
                    if (b.Width > 5 && b.Height > 5) SelectedShapes[0].Bounds = b;
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.UpdateEndPoint(new PointF(Snap(realPt.X), Snap(realPt.Y)));
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
                    c.UpdateEndPoint(realPt);
                    _hoveredShapeForConnection = Shapes.LastOrDefault(s => s.Id != c.SourceId && s.HitTest(realPt));
                }
                
                _lastMousePos = e.Location;
                this.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.Cursor = Cursors.Default;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (_currentState == InteractionState.Moving && SelectedShapes.Count > 0 && (_dragTotalDx != 0 || _dragTotalDy != 0))
                {
                    foreach (var s in SelectedShapes) s.Move(-_dragTotalDx, -_dragTotalDy);
                    CmdManager.ExecuteCommand(new MoveShapesCommand(SelectedShapes, _dragTotalDx, _dragTotalDy));
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5)
                    {
                        CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, _tempShape));
                    }
                    if (_tempShape is App_Shapes.TextNodeShape) OnToolResetRequested?.Invoke();
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
                    if (_hoveredShapeForConnection != null) c.TargetId = _hoveredShapeForConnection.Id;
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, c));
                    OnToolResetRequested?.Invoke();
                }

                _tempShape = null;
                _hoveredShapeForConnection = null;
                _currentState = InteractionState.Idle;
                this.Invalidate();
            }
        }

        // 用於跳線計算的內部類別
        private class LineSegment { public PointF P1; public PointF P2; public LineSegment(PointF p1, PointF p2) { P1 = p1; P2 = p2; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

            DrawGrid(g);

            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, PageSize.Width, PageSize.Height);

            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape))) shape.Draw(g);

            // 恢復被遺漏的跳線邏輯 (Jump Lines)
            List<LineSegment> drawnLines = new List<LineSegment>();
            
            foreach (var shape in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                var src = Shapes.FirstOrDefault(x => x.Id == shape.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == shape.TargetId);
                PointF p1 = src != null ? GetClosestAnchor(src, tgt != null ? GetCenter(tgt.Bounds) : shape.EndPt) : shape.StartPt;
                PointF p2 = tgt != null ? GetClosestAnchor(tgt, p1) : shape.EndPt;
                
                if (!shape.IsOrthogonal)
                {
                    foreach (var oldLine in drawnLines)
                    {
                        if (Math.Abs(p1.X - p2.X) < 10 && Math.Abs(oldLine.P1.Y - oldLine.P2.Y) < 10)
                        {
                            float minX = Math.Min(oldLine.P1.X, oldLine.P2.X);
                            float maxX = Math.Max(oldLine.P1.X, oldLine.P2.X);
                            float minY = Math.Min(p1.Y, p2.Y);
                            float maxY = Math.Max(p1.Y, p2.Y);
                            
                            if (p1.X > minX && p1.X < maxX && oldLine.P1.Y > minY && oldLine.P1.Y < maxY)
                            {
                                float ix = p1.X; float iy = oldLine.P1.Y;
                                g.FillEllipse(Brushes.White, ix - 8, iy - 8, 16, 16);
                                using (Pen arcPen = new Pen(shape.ShapeColor, shape.StrokeWidth)) g.DrawArc(arcPen, ix - 8, iy - 8, 16, 16, 180, 180);
                            }
                        }
                    }
                }
                
                shape.DrawDynamic(g, p1, p2);
                if (!shape.IsOrthogonal) drawnLines.Add(new LineSegment(p1, p2));
            }

            _tempShape?.Draw(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt);
            
            foreach (var s in SelectedShapes) s.DrawSelection(g);
            
            if (_currentState == InteractionState.BoxSelecting)
            {
                using (Pen p = new Pen(Color.CornflowerBlue) { DashStyle = DashStyle.Dash })
                using (Brush b = new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)))
                {
                    g.FillRectangle(b, _boxSelectRect);
                    g.DrawRectangle(p, Rectangle.Round(_boxSelectRect));
                }
            }
            g.ResetTransform();
        }

        private void DrawGrid(Graphics g)
        {
            int gridSize = 20;
            RectangleF viewRect = new RectangleF(
                -_cameraOffset.X / ZoomFactor, 
                -_cameraOffset.Y / ZoomFactor, 
                this.Width / ZoomFactor, 
                this.Height / ZoomFactor);

            int startX = (int)(Math.Floor(viewRect.Left / gridSize) * gridSize);
            int startY = (int)(Math.Floor(viewRect.Top / gridSize) * gridSize);

            using (Pen gridPen = new Pen(Color.FromArgb(230, 230, 230)))
            {
                for (float x = startX; x < viewRect.Right; x += gridSize) g.DrawLine(gridPen, x, viewRect.Top, x, viewRect.Bottom);
                for (float y = startY; y < viewRect.Bottom; y += gridSize) g.DrawLine(gridPen, viewRect.Left, y, viewRect.Right, y);
            }
        }

        private PointF GetCenter(RectangleF r) => new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);

        private PointF GetClosestAnchor(App_Shapes.ShapeBase shape, PointF target)
        {
            PointF best = new PointF(); float min = float.MaxValue;
            foreach (var a in shape.GetAnchors())
            {
                float d = (a.X - target.X) * (a.X - target.X) + (a.Y - target.Y) * (a.Y - target.Y);
                if (d < min) { min = d; best = a; }
            }
            return best;
        }

        public Bitmap GetTransparentCanvasRender()
        {
            SelectedShapes.ForEach(s => s.IsSelected = false);
            float maxX = Shapes.Count > 0 ? Shapes.Max(s => s.Bounds.Right) : PageSize.Width;
            float maxY = Shapes.Count > 0 ? Shapes.Max(s => s.Bounds.Bottom) : PageSize.Height;
            Bitmap bmp = new Bitmap(Math.Max((int)maxX + 50, (int)PageSize.Width), Math.Max((int)maxY + 50, (int)PageSize.Height)); 
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var s in Shapes) s.Draw(g);
            }
            return bmp;
        }
    }
}
