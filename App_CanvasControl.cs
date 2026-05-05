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
        public SizeF PageSize { get; set; } = new SizeF(2100, 2970); // 預設A4虛擬邊界 (10倍 mm)
        
        private Point _lastMousePos;
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
        public event Action OnToolResetRequested; // 需求3: 畫完字回游標

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.AutoScroll = true; // 需求1: 無限卷軸
            this.ContextMenuStrip = CreateContextMenu();
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.DoubleClick += (s, e) => { if (SelectedShapes.Count == 1) OnShapeDoubleClicked?.Invoke(SelectedShapes[0]); };
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("複製 (Ctrl+C)", null, (s, e) => Copy());
            m.Items.Add("貼上 (Ctrl+V)", null, (s, e) => Paste());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0));
            m.Items.Add("移到最下層", null, (s, e) => ChangeZIndex(-99));
            return m;
        }

        private void ChangeZIndex(int dir) { /* ...與先前相同... */ }

        // 需求2, 4: 鍵盤快捷鍵
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { OnToolResetRequested?.Invoke(); return true; }
            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                var toRemove = new List<App_Shapes.ShapeBase>(SelectedShapes);
                foreach (var s in SelectedShapes) toRemove.AddRange(Shapes.OfType<App_Shapes.ConnectorShape>().Where(c => c.SourceId == s.Id || c.TargetId == s.Id));
                Shapes.RemoveAll(x => toRemove.Contains(x));
                SelectedShapes.Clear();
                UpdateScrollSize();
                this.Invalidate();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Copy() { if (SelectedShapes.Count > 0) _clipboard = App_SaveLoad.CloneShapes(SelectedShapes); }
        private void Paste() {
            if (_clipboard.Count == 0) return;
            SelectedShapes.ForEach(s => s.IsSelected = false);
            SelectedShapes.Clear();
            var newClones = App_SaveLoad.CloneShapes(_clipboard);
            foreach (var s in newClones) { s.Id = Guid.NewGuid(); s.Move(20, 20); s.IsSelected = true; Shapes.Add(s); SelectedShapes.Add(s); }
            UpdateScrollSize();
            this.Invalidate();
        }

        private PointF GetRealPoint(Point pt) => new PointF(pt.X - this.AutoScrollPosition.X, pt.Y - this.AutoScrollPosition.Y);

        private void UpdateScrollSize()
        {
            float maxX = PageSize.Width, maxY = PageSize.Height;
            foreach (var s in Shapes) { if (s.Bounds.Right > maxX) maxX = s.Bounds.Right; if (s.Bounds.Bottom > maxY) maxY = s.Bounds.Bottom; }
            this.AutoScrollMinSize = new Size((int)maxX + 200, (int)maxY + 200);
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
            PointF realPt = GetRealPoint(e.Location);
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                if (CurrentTool == App_Shapes.ShapeType.Pointer)
                {
                    if (SelectedShapes.Count == 1) {
                        var h = SelectedShapes[0].HitTestHandle(realPt);
                        if (h != App_Shapes.HandlePosition.None) { _currentState = InteractionState.Resizing; _resizingHandle = h; return; }
                    }
                    var hit = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    if (hit != null) {
                        if (!SelectedShapes.Contains(hit) && Control.ModifierKeys != Keys.Control) { SelectedShapes.ForEach(s => s.IsSelected = false); SelectedShapes.Clear(); }
                        hit.IsSelected = true; if (!SelectedShapes.Contains(hit)) SelectedShapes.Add(hit);
                        _currentState = InteractionState.Moving;
                    } else {
                        SelectedShapes.ForEach(s => s.IsSelected = false); SelectedShapes.Clear();
                        _currentState = InteractionState.BoxSelecting; _boxSelectRect = new RectangleF(realPt.X, realPt.Y, 0, 0);
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.StraightLine) {
                    _currentState = InteractionState.Connecting;
                    _tempShape = new App_Shapes.ConnectorShape(realPt, CurrentColor, CurrentTool == App_Shapes.ShapeType.ArrowLine) { SourceId = Shapes.LastOrDefault(s => s.HitTest(realPt))?.Id ?? Guid.Empty };
                }
                else if (CurrentTool == App_Shapes.ShapeType.Image) { OnImageInsertRequested?.Invoke(realPt); OnToolResetRequested?.Invoke(); }
                else {
                    _currentState = InteractionState.Drawing;
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, realPt, CurrentColor);
                }
                this.Invalidate();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            if (e.Button == MouseButtons.Left)
            {
                float dx = e.X - _lastMousePos.X, dy = e.Y - _lastMousePos.Y;
                if (_currentState == InteractionState.Moving) foreach (var s in SelectedShapes) s.Move(dx, dy);
                else if (_currentState == InteractionState.BoxSelecting) {
                    _boxSelectRect = new RectangleF(Math.Min(_boxSelectRect.X, realPt.X), Math.Min(_boxSelectRect.Y, realPt.Y), Math.Abs(realPt.X - _boxSelectRect.X), Math.Abs(realPt.Y - _boxSelectRect.Y));
                    SelectedShapes.ForEach(s => s.IsSelected = false); SelectedShapes = Shapes.Where(s => _boxSelectRect.IntersectsWith(s.Bounds)).ToList(); SelectedShapes.ForEach(s => s.IsSelected = true);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1) {
                    var b = SelectedShapes[0].Bounds;
                    switch (_resizingHandle) {
                        case App_Shapes.HandlePosition.NW: b = new RectangleF(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.SE: b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height + dy); break;
                    }
                    if (b.Width > 5 && b.Height > 5) SelectedShapes[0].Bounds = b;
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null) _tempShape.UpdateEndPoint(realPt);
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c) {
                    c.UpdateEndPoint(realPt);
                    _hoveredShapeForConnection = Shapes.LastOrDefault(s => s != Shapes.FirstOrDefault(x => x.Id == c.SourceId) && s.HitTest(realPt));
                }
                _lastMousePos = e.Location; this.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_currentState == InteractionState.Drawing && _tempShape != null) {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5) Shapes.Add(_tempShape);
                    
                    // 需求3: 畫完文字框返回游標
                    if (_tempShape is App_Shapes.TextNodeShape) OnToolResetRequested?.Invoke();
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c) {
                    if (_hoveredShapeForConnection != null) c.TargetId = _hoveredShapeForConnection.Id;
                    Shapes.Add(c);
                }
                _tempShape = null; _hoveredShapeForConnection = null; _currentState = InteractionState.Idle;
                UpdateScrollSize();
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(this.AutoScrollPosition.X, this.AutoScrollPosition.Y);

            // 需求8: 繪製虛擬頁面邊界
            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, PageSize.Width, PageSize.Height);

            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape))) shape.Draw(g);

            // 繪製連線 & 需求7: 交疊跳線 (簡易視覺處理)
            var lines = new List<Tuple<PointF, PointF>>();
            foreach (var conn in Shapes.OfType<App_Shapes.ConnectorShape>()) {
                var src = Shapes.FirstOrDefault(x => x.Id == conn.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == conn.TargetId);
                PointF p1 = src != null ? GetClosestAnchor(src, tgt != null ? GetCenter(tgt.Bounds) : conn.EndPt) : conn.StartPt;
                PointF p2 = tgt != null ? GetClosestAnchor(tgt, p1) : conn.EndPt;
                
                // 畫跳線弧度 (檢查是否與之前畫的線有十字交疊)
                foreach (var oldLine in lines) {
                    if (Math.Abs(p1.X - p2.X) < 10 && Math.Abs(oldLine.Item1.Y - oldLine.Item2.Y) < 10) { // 簡化: 垂直線遇到水平線
                        if (p1.X > Math.Min(oldLine.Item1.X, oldLine.Item2.X) && p1.X < Math.Max(oldLine.Item1.X, oldLine.Item2.X)) {
                            float ix = p1.X, iy = oldLine.Item1.Y;
                            g.FillEllipse(Brushes.White, ix - 6, iy - 6, 12, 12);
                            using (Pen p = new Pen(conn.ShapeColor, 2)) g.DrawArc(p, ix - 6, iy - 6, 12, 12, 180, 180);
                        }
                    }
                }
                conn.DrawDynamic(g, p1, p2);
                lines.Add(new Tuple<PointF, PointF>(p1, p2));
            }

            _tempShape?.Draw(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt);
            foreach (var s in SelectedShapes) s.DrawSelection(g);
            if (_currentState == InteractionState.BoxSelecting) using (Pen p = new Pen(Color.CornflowerBlue) { DashStyle = DashStyle.Dash }) { g.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), _boxSelectRect); g.DrawRectangle(p, Rectangle.Round(_boxSelectRect)); }
            g.ResetTransform();
        }

        private PointF GetCenter(RectangleF r) => new PointF(r.X + r.Width/2, r.Y + r.Height/2);
        private PointF GetClosestAnchor(App_Shapes.ShapeBase shape, PointF t) {
            PointF best = new PointF(); float min = float.MaxValue;
            foreach (var a in shape.GetAnchors()) { float d = (a.X-t.X)*(a.X-t.X) + (a.Y-t.Y)*(a.Y-t.Y); if(d<min){min=d;best=a;} }
            return best;
        }

        public Bitmap GetTransparentCanvasRender()
        {
            SelectedShapes.ForEach(s => s.IsSelected = false);
            float maxX = Shapes.Count > 0 ? Shapes.Max(s => s.Bounds.Right) : PageSize.Width;
            float maxY = Shapes.Count > 0 ? Shapes.Max(s => s.Bounds.Bottom) : PageSize.Height;
            Bitmap bmp = new Bitmap(Math.Max((int)maxX+50, (int)PageSize.Width), Math.Max((int)maxY+50, (int)PageSize.Height)); 
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias; foreach (var s in Shapes) s.Draw(g); }
            return bmp;
        }
    }
}
