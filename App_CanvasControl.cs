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
        
        // 畫布狀態
        private PointF _canvasOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;

        private enum InteractionState { Idle, Drawing, Moving, Resizing, Connecting, BoxSelecting }
        private InteractionState _currentState = InteractionState.Idle;

        public App_Shapes.ShapeType CurrentTool { get; set; } = App_Shapes.ShapeType.Pointer;
        public Color CurrentColor { get; set; } = Color.Black;
        
        private App_Shapes.ShapeBase _tempShape = null;
        private RectangleF _boxSelectRect; // 框選範圍
        
        // 多選機制
        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;

        // 動態對齊線
        private float? _alignLineX = null;
        private float? _alignLineY = null;

        // 文字雙擊編輯事件
        public event Action<App_Shapes.TextNodeShape> OnTextShapeDoubleClicked;
        // 圖片插入事件
        public event Action<PointF> OnImageInsertRequested;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Default;
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.DoubleClick += Canvas_DoubleClick;
            // 右鍵選單 Z-index
            this.ContextMenuStrip = CreateZIndexMenu();
        }

        private ContextMenuStrip CreateZIndexMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0)); // 最後畫，索引最後
            menu.Items.Add("上一層", null, (s, e) => ChangeZIndex(1));
            menu.Items.Add("下一層", null, (s, e) => ChangeZIndex(-1));
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
                
                if (direction == 0) Shapes.Add(s); // 最上
                else if (direction == -99) Shapes.Insert(0, s); // 最下
                else if (direction == 1 && index < Shapes.Count) Shapes.Insert(index + 1, s); // 上一
                else if (direction == -1 && index > 0) Shapes.Insert(index - 1, s); // 下一
                else Shapes.Insert(index, s); // 邊界不動
            }
            this.Invalidate();
        }

        // 需求 1: Delete 鍵刪除多個選取物件
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                // 刪除相關連線
                var toRemove = new List<App_Shapes.ShapeBase>(SelectedShapes);
                foreach (var s in SelectedShapes)
                {
                    toRemove.AddRange(Shapes.OfType<App_Shapes.ConnectorShape>().Where(c => c.SourceId == s.Id || c.TargetId == s.Id));
                }
                Shapes.RemoveAll(x => toRemove.Contains(x));
                SelectedShapes.Clear();
                this.Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private PointF GetRealPoint(Point pt) => new PointF(pt.X - _canvasOffset.X, pt.Y - _canvasOffset.Y);

        private void Canvas_DoubleClick(object sender, EventArgs e)
        {
            if (SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.TextNodeShape ts)
            {
                OnTextShapeDoubleClicked?.Invoke(ts);
                this.Invalidate();
            }
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
            PointF realPt = GetRealPoint(e.Location);

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Space))
            {
                _isPanning = true;
                _lastMousePos = e.Location;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                _lastMousePos = e.Location;

                if (CurrentTool == App_Shapes.ShapeType.Pointer)
                {
                    // 1. 縮放檢查 (只允許單選時縮放)
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

                    // 2. 點選檢查
                    App_Shapes.ShapeBase hitShape = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    
                    if (hitShape != null)
                    {
                        // 若點中的不在選取清單，且沒按 Ctrl，則清空重選
                        if (!SelectedShapes.Contains(hitShape) && Control.ModifierKeys != Keys.Control)
                        {
                            SelectedShapes.ForEach(s => s.IsSelected = false);
                            SelectedShapes.Clear();
                        }
                        
                        hitShape.IsSelected = true;
                        if (!SelectedShapes.Contains(hitShape)) SelectedShapes.Add(hitShape);
                        _currentState = InteractionState.Moving;
                    }
                    else
                    {
                        // 點擊空白處，開始框選
                        SelectedShapes.ForEach(s => s.IsSelected = false);
                        SelectedShapes.Clear();
                        _currentState = InteractionState.BoxSelecting;
                        _boxSelectRect = new RectangleF(realPt.X, realPt.Y, 0, 0);
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.StraightLine)
                {
                    _currentState = InteractionState.Connecting;
                    bool isArrow = CurrentTool == App_Shapes.ShapeType.ArrowLine;
                    var connector = new App_Shapes.ConnectorShape(realPt, CurrentColor, isArrow);
                    var source = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    if (source != null) connector.SourceId = source.Id;
                    _tempShape = connector;
                }
                else if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    OnImageInsertRequested?.Invoke(realPt);
                    CurrentTool = App_Shapes.ShapeType.Pointer; // 插入後切回游標
                }
                else
                {
                    _currentState = InteractionState.Drawing;
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, realPt, CurrentColor);
                }
                this.Invalidate();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);

            if (_isPanning)
            {
                _canvasOffset.X += e.X - _lastMousePos.X;
                _canvasOffset.Y += e.Y - _lastMousePos.Y;
                _lastMousePos = e.Location;
                this.Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                float dx = e.X - _lastMousePos.X;
                float dy = e.Y - _lastMousePos.Y;

                if (_currentState == InteractionState.Moving && SelectedShapes.Count > 0)
                {
                    foreach (var s in SelectedShapes) s.Move(dx, dy);
                    
                    // 動態對齊線 (需求 12) 只對單一物件拖曳時顯示
                    _alignLineX = null; _alignLineY = null;
                    if (SelectedShapes.Count == 1)
                    {
                        var me = SelectedShapes[0].Bounds;
                        float threshold = 5f;
                        foreach (var other in Shapes.Where(s => s != SelectedShapes[0]))
                        {
                            var ob = other.Bounds;
                            // 中心點對齊
                            if (Math.Abs((me.X + me.Width/2) - (ob.X + ob.Width/2)) < threshold) _alignLineX = ob.X + ob.Width/2;
                            if (Math.Abs((me.Y + me.Height/2) - (ob.Y + ob.Height/2)) < threshold) _alignLineY = ob.Y + ob.Height/2;
                        }
                    }
                }
                else if (_currentState == InteractionState.BoxSelecting)
                {
                    _boxSelectRect = new RectangleF(
                        Math.Min(_boxSelectRect.X, realPt.X),
                        Math.Min(_boxSelectRect.Y, realPt.Y),
                        Math.Abs(realPt.X - _boxSelectRect.X),
                        Math.Abs(realPt.Y - _boxSelectRect.Y)
                    );
                    
                    // 即時選取框內的物件
                    SelectedShapes.ForEach(s => s.IsSelected = false);
                    SelectedShapes = Shapes.Where(s => _boxSelectRect.IntersectsWith(s.Bounds)).ToList();
                    SelectedShapes.ForEach(s => s.IsSelected = true);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1)
                {
                    var s = SelectedShapes[0];
                    RectangleF b = s.Bounds;
                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: b = new RectangleF(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.NE: b = new RectangleF(b.X, b.Y + dy, b.Width + dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.SW: b = new RectangleF(b.X + dx, b.Y, b.Width - dx, b.Height + dy); break;
                        case App_Shapes.HandlePosition.SE: b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height + dy); break;
                    }
                    if (b.Width > 5 && b.Height > 5) s.Bounds = b;
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.UpdateEndPoint(realPt);
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape conn)
                {
                    conn.UpdateEndPoint(realPt);
                    _hoveredShapeForConnection = Shapes.LastOrDefault(s => s != Shapes.FirstOrDefault(x=>x.Id == conn.SourceId) && s.HitTest(realPt));
                }
                
                _lastMousePos = e.Location;
                this.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            _isPanning = false;
            _alignLineX = null; _alignLineY = null; // 清除對齊線

            if (e.Button == MouseButtons.Left)
            {
                if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5) Shapes.Add(_tempShape);
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape conn)
                {
                    if (_hoveredShapeForConnection != null) conn.TargetId = _hoveredShapeForConnection.Id;
                    Shapes.Add(conn);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1)
                {
                    SelectedShapes[0].NormalizeBounds();
                }

                _tempShape = null;
                _hoveredShapeForConnection = null;
                _currentState = InteractionState.Idle;
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawGrid(g);

            g.TranslateTransform(_canvasOffset.X, _canvasOffset.Y);

            // 繪製一般圖形
            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape))) shape.Draw(g);

            // 繪製連線 (動態計算錨點)
            foreach (var conn in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                var src = Shapes.FirstOrDefault(x => x.Id == conn.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == conn.TargetId);
                PointF p1 = src != null ? GetClosestAnchor(src, tgt != null ? GetCenter(tgt.Bounds) : conn.EndPt) : conn.StartPt;
                PointF p2 = tgt != null ? GetClosestAnchor(tgt, p1) : conn.EndPt;
                conn.DrawDynamic(g, p1, p2);
            }

            _tempShape?.Draw(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt);

            if (_hoveredShapeForConnection != null)
                foreach (var anchor in _hoveredShapeForConnection.GetAnchors())
                    g.FillEllipse(Brushes.Red, anchor.X - 4, anchor.Y - 4, 8, 8);

            foreach (var s in SelectedShapes) s.DrawSelection(g);

            // 畫框選範圍
            if (_currentState == InteractionState.BoxSelecting)
                using (Pen p = new Pen(Color.CornflowerBlue, 1) { DashStyle = DashStyle.Dash })
                using (Brush b = new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)))
                {
                    g.FillRectangle(b, _boxSelectRect);
                    g.DrawRectangle(p, Rectangle.Round(_boxSelectRect));
                }

            // 畫對齊線
            using (Pen p = new Pen(Color.Orange, 1) { DashStyle = DashStyle.Dash })
            {
                if (_alignLineX.HasValue) g.DrawLine(p, _alignLineX.Value, -10000, _alignLineX.Value, 10000);
                if (_alignLineY.HasValue) g.DrawLine(p, -10000, _alignLineY.Value, 10000, _alignLineY.Value);
            }

            g.ResetTransform();
        }

        private PointF GetCenter(RectangleF r) => new PointF(r.X + r.Width/2, r.Y + r.Height/2);
        private PointF GetClosestAnchor(App_Shapes.ShapeBase shape, PointF target)
        {
            PointF best = new PointF(0,0);
            float min = float.MaxValue;
            foreach (var a in shape.GetAnchors())
            {
                float d = (a.X - target.X)*(a.X - target.X) + (a.Y - target.Y)*(a.Y - target.Y);
                if (d < min) { min = d; best = a; }
            }
            return best;
        }

        private void DrawGrid(Graphics g)
        {
            int gridSize = 20;
            float offsetX = _canvasOffset.X % gridSize;
            float offsetY = _canvasOffset.Y % gridSize;
            using (Pen p = new Pen(Color.FromArgb(230, 230, 230)))
            {
                for (float x = offsetX; x < this.Width; x += gridSize) g.DrawLine(p, x, 0, x, this.Height);
                for (float y = offsetY; y < this.Height; y += gridSize) g.DrawLine(p, 0, y, this.Width, y);
            }
        }

        public Bitmap GetTransparentCanvasRender()
        {
            SelectedShapes.ForEach(s => s.IsSelected = false);
            // 動態計算所有圖形的範圍
            float minX = 0, minY = 0, maxX = 800, maxY = 600;
            if(Shapes.Count > 0) {
                minX = Shapes.Min(s => s.Bounds.X); minY = Shapes.Min(s => s.Bounds.Y);
                maxX = Shapes.Max(s => s.Bounds.Right); maxY = Shapes.Max(s => s.Bounds.Bottom);
            }
            int w = Math.Max((int)maxX + 50, 100);
            int h = Math.Max((int)maxY + 50, 100);

            Bitmap bmp = new Bitmap(w, h); 
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape))) shape.Draw(g);
                foreach (var conn in Shapes.OfType<App_Shapes.ConnectorShape>())
                {
                    var src = Shapes.FirstOrDefault(x => x.Id == conn.SourceId);
                    var tgt = Shapes.FirstOrDefault(x => x.Id == conn.TargetId);
                    PointF p1 = src != null ? GetClosestAnchor(src, tgt != null ? GetCenter(tgt.Bounds) : conn.EndPt) : conn.StartPt;
                    PointF p2 = tgt != null ? GetClosestAnchor(tgt, p1) : conn.EndPt;
                    conn.DrawDynamic(g, p1, p2);
                }
            }
            return bmp;
        }
    }
}
