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
        
        // 預設 A4 尺寸 (等比放大 10 倍，方便在螢幕繪製)
        public SizeF PageSize { get; set; } = new SizeF(2100, 2970);
        
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
        public event Action OnToolResetRequested;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.AutoScroll = true; // 啟用無限畫布
            this.ContextMenuStrip = CreateContextMenu();
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.DoubleClick += Canvas_DoubleClick;
        }

        private void Canvas_DoubleClick(object sender, EventArgs e)
        {
            if (SelectedShapes.Count == 1)
            {
                OnShapeDoubleClicked?.Invoke(SelectedShapes[0]);
            }
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
                
                if (direction == 0)
                {
                    Shapes.Add(s); // 最上層
                }
                else if (direction == -99)
                {
                    Shapes.Insert(0, s); // 最下層
                }
            }
            this.Invalidate();
        }

        // 捕捉全域按鍵 (支援 ESC、Delete、Ctrl+C、Ctrl+V)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                OnToolResetRequested?.Invoke();
                return true;
            }
            
            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                var toRemove = new List<App_Shapes.ShapeBase>(SelectedShapes);
                foreach (var s in SelectedShapes)
                {
                    var connectedLines = Shapes.OfType<App_Shapes.ConnectorShape>()
                                               .Where(c => c.SourceId == s.Id || c.TargetId == s.Id);
                    toRemove.AddRange(connectedLines);
                }
                
                Shapes.RemoveAll(x => toRemove.Contains(x));
                SelectedShapes.Clear();
                UpdateScrollSize();
                this.Invalidate();
                return true;
            }
            
            if (keyData == (Keys.Control | Keys.C))
            {
                Copy();
                return true;
            }
            
            if (keyData == (Keys.Control | Keys.V))
            {
                Paste();
                return true;
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Copy()
        {
            if (SelectedShapes.Count > 0)
            {
                _clipboard = App_SaveLoad.CloneShapes(SelectedShapes);
            }
        }

        private void Paste()
        {
            if (_clipboard.Count == 0) return;
            
            SelectedShapes.ForEach(s => s.IsSelected = false);
            SelectedShapes.Clear();
            
            var newClones = App_SaveLoad.CloneShapes(_clipboard);
            foreach (var s in newClones)
            {
                s.Id = Guid.NewGuid();
                s.Move(20, 20); // 貼上的偏移量
                s.IsSelected = true;
                Shapes.Add(s);
                SelectedShapes.Add(s);
            }
            
            UpdateScrollSize();
            this.Invalidate();
        }

        // 螢幕座標轉為虛擬捲軸座標
        private PointF GetRealPoint(Point pt)
        {
            return new PointF(pt.X - this.AutoScrollPosition.X, pt.Y - this.AutoScrollPosition.Y);
        }

        // 依據圖形邊界更新可捲動範圍
        private void UpdateScrollSize()
        {
            float maxX = PageSize.Width;
            float maxY = PageSize.Height;
            
            foreach (var s in Shapes)
            {
                if (s.Bounds.Right > maxX) maxX = s.Bounds.Right;
                if (s.Bounds.Bottom > maxY) maxY = s.Bounds.Bottom;
            }
            
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
                    }
                    else
                    {
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
                    
                    _tempShape = new App_Shapes.ConnectorShape(realPt, CurrentColor, isArrow);
                    var sourceShape = Shapes.LastOrDefault(s => s.HitTest(realPt));
                    if (sourceShape != null)
                    {
                        ((App_Shapes.ConnectorShape)_tempShape).SourceId = sourceShape.Id;
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    OnImageInsertRequested?.Invoke(realPt);
                    OnToolResetRequested?.Invoke();
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
            
            if (e.Button == MouseButtons.Left)
            {
                float dx = e.X - _lastMousePos.X;
                float dy = e.Y - _lastMousePos.Y;

                if (_currentState == InteractionState.Moving)
                {
                    foreach (var s in SelectedShapes)
                    {
                        s.Move(dx, dy);
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
                    if (b.Width > 5 && b.Height > 5)
                    {
                        SelectedShapes[0].Bounds = b;
                    }
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.UpdateEndPoint(realPt);
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
            if (e.Button == MouseButtons.Left)
            {
                if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5)
                    {
                        Shapes.Add(_tempShape);
                    }
                    
                    // 畫完文字相關圖框後，自動重設為游標 (需求3)
                    if (_tempShape is App_Shapes.TextNodeShape)
                    {
                        OnToolResetRequested?.Invoke();
                    }
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
                    if (_hoveredShapeForConnection != null)
                    {
                        c.TargetId = _hoveredShapeForConnection.Id;
                    }
                    Shapes.Add(c);
                    OnToolResetRequested?.Invoke(); // 畫完線自動回歸指標
                }

                _tempShape = null;
                _hoveredShapeForConnection = null;
                _currentState = InteractionState.Idle;
                
                UpdateScrollSize();
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            // 將畫布依照 Scroll 偏移量移動
            g.TranslateTransform(this.AutoScrollPosition.X, this.AutoScrollPosition.Y);

            // 繪製虛擬頁面邊界 (需求8)
            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
            {
                g.DrawRectangle(pPage, 0, 0, PageSize.Width, PageSize.Height);
            }

            // 1. 繪製所有一般圖形
            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape)))
            {
                shape.Draw(g);
            }

            // 2. 繪製連線，並偵測交錯繪製跳線 (需求7)
            List<LineSegment> drawnLines = new List<LineSegment>();
            
            foreach (var shape in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                var src = Shapes.FirstOrDefault(x => x.Id == shape.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == shape.TargetId);
                
                PointF p1 = src != null ? GetClosestAnchor(src, tgt != null ? GetCenter(tgt.Bounds) : shape.EndPt) : shape.StartPt;
                PointF p2 = tgt != null ? GetClosestAnchor(tgt, p1) : shape.EndPt;
                
                // 檢查跳線 (與已經畫過的線是否垂直交錯)
                foreach (var oldLine in drawnLines)
                {
                    if (Math.Abs(p1.X - p2.X) < 10 && Math.Abs(oldLine.P1.Y - oldLine.P2.Y) < 10)
                    {
                        // 判斷十字交集
                        float minX = Math.Min(oldLine.P1.X, oldLine.P2.X);
                        float maxX = Math.Max(oldLine.P1.X, oldLine.P2.X);
                        float minY = Math.Min(p1.Y, p2.Y);
                        float maxY = Math.Max(p1.Y, p2.Y);
                        
                        if (p1.X > minX && p1.X < maxX && oldLine.P1.Y > minY && oldLine.P1.Y < maxY)
                        {
                            float intersectX = p1.X;
                            float intersectY = oldLine.P1.Y;
                            
                            // 用背景色挖空，再畫半圓弧
                            g.FillEllipse(Brushes.White, intersectX - 8, intersectY - 8, 16, 16);
                            using (Pen arcPen = new Pen(shape.ShapeColor, 2))
                            {
                                g.DrawArc(arcPen, intersectX - 8, intersectY - 8, 16, 16, 180, 180);
                            }
                        }
                    }
                }
                
                shape.DrawDynamic(g, p1, p2);
                drawnLines.Add(new LineSegment(p1, p2));
            }

            // 3. 繪製互動中的暫存圖形
            if (_tempShape != null)
            {
                _tempShape.Draw(g);
                if (_tempShape is App_Shapes.ConnectorShape tc)
                {
                    tc.DrawDynamic(g, tc.StartPt, tc.EndPt);
                }
            }
            
            // 4. 繪製選取框
            foreach (var s in SelectedShapes)
            {
                s.DrawSelection(g);
            }
            
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

        // 用於紀錄線段以利跳線偵測的內部類別
        private class LineSegment
        {
            public PointF P1 { get; }
            public PointF P2 { get; }
            public LineSegment(PointF p1, PointF p2) { P1 = p1; P2 = p2; }
        }

        private PointF GetCenter(RectangleF r)
        {
            return new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);
        }

        private PointF GetClosestAnchor(App_Shapes.ShapeBase shape, PointF target)
        {
            PointF best = new PointF();
            float min = float.MaxValue;
            foreach (var anchor in shape.GetAnchors())
            {
                float d = (anchor.X - target.X) * (anchor.X - target.X) + (anchor.Y - target.Y) * (anchor.Y - target.Y);
                if (d < min)
                {
                    min = d;
                    best = anchor;
                }
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
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                
                foreach (var s in Shapes)
                {
                    s.Draw(g);
                }
            }
            return bmp;
        }
    }
}
