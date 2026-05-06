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
        public SizeF PageSize { get; set; } = new SizeF(2100, 2970); // 預設 A4 放大 10 倍
        
        public CommandManager CmdManager { get; } = new CommandManager();

        // 縮放與平移攝影機
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

        // 進階功能：畫布內建文字輸入框
        private TextBox _inlineTextBox;
        private App_Shapes.ShapeBase _editingShape = null;

        // 進階功能：對齊輔助線
        private List<Tuple<PointF, PointF>> _smartGuides = new List<Tuple<PointF, PointF>>();

        public event Action<App_Shapes.ShapeBase> OnShapePropertyRequested;
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

            // 初始化畫布內建編輯器
            InitializeInlineEditor();
        }

        private void InitializeInlineEditor()
        {
            _inlineTextBox = new TextBox();
            _inlineTextBox.Multiline = true;
            _inlineTextBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineTextBox.TextAlign = HorizontalAlignment.Center;
            _inlineTextBox.Visible = false;
            
            // 編輯完成的觸發條件
            _inlineTextBox.Leave += (s, e) => CommitInlineText();
            _inlineTextBox.KeyDown += (s, e) =>
            {
                // 按下 Shift+Enter 換行，單按 Enter 完成編輯
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    CommitInlineText();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    CancelInlineText();
                }
            };
            
            this.Controls.Add(_inlineTextBox);
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("複製 (Ctrl+C)", null, (s, e) => Copy());
            menu.Items.Add("貼上 (Ctrl+V)", null, (s, e) => Paste());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("群組 (Ctrl+G)", null, (s, e) => GroupSelected());
            menu.Items.Add("解除群組 (Ctrl+U)", null, (s, e) => UngroupSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0));
            menu.Items.Add("移到最下層", null, (s, e) => ChangeZIndex(-99));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("進階屬性設定...", null, (s, e) => {
                if (SelectedShapes.Count == 1) OnShapePropertyRequested?.Invoke(SelectedShapes[0]);
            });
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

        // --- 進階功能：群組與解除群組 ---
        private void GroupSelected()
        {
            if (SelectedShapes.Count < 2) return;
            
            var group = new App_Shapes.GroupShape(SelectedShapes.ToList());
            CmdManager.ExecuteCommand(new GroupCommand(Shapes, SelectedShapes, group));
            
            SelectedShapes.Clear();
            SelectedShapes.Add(group);
            group.IsSelected = true;
            this.Invalidate();
        }

        private void UngroupSelected()
        {
            if (SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape group)
            {
                CmdManager.ExecuteCommand(new UngroupCommand(Shapes, group));
                
                SelectedShapes.Clear();
                foreach (var child in group.Children)
                {
                    child.IsSelected = true;
                    SelectedShapes.Add(child);
                }
                this.Invalidate();
            }
        }

        // --- 進階功能：畫布內文字編輯器 ---
        private void Canvas_DoubleClick(object sender, EventArgs e)
        {
            if (SelectedShapes.Count == 1 && !(SelectedShapes[0] is App_Shapes.ConnectorShape))
            {
                StartInlineEditing(SelectedShapes[0]);
            }
        }

        private void StartInlineEditing(App_Shapes.ShapeBase shape)
        {
            _editingShape = shape;
            _inlineTextBox.Text = shape.Text;
            _inlineTextBox.Font = new Font(shape.FontName, shape.FontSize * ZoomFactor);
            _inlineTextBox.ForeColor = shape.FontColor;

            // 將虛擬座標轉換為實體螢幕控制項座標
            int screenX = (int)(shape.Bounds.X * ZoomFactor + _cameraOffset.X);
            int screenY = (int)(shape.Bounds.Y * ZoomFactor + _cameraOffset.Y);
            int screenW = (int)(shape.Bounds.Width * ZoomFactor);
            int screenH = (int)(shape.Bounds.Height * ZoomFactor);

            // 稍微縮小輸入框使其完美覆蓋在圖形內
            _inlineTextBox.Bounds = new Rectangle(screenX + 5, screenY + 5, screenW - 10, screenH - 10);
            _inlineTextBox.Visible = true;
            _inlineTextBox.Focus();
            _inlineTextBox.SelectAll();
        }

        private void CommitInlineText()
        {
            if (_editingShape != null)
            {
                _editingShape.Text = _inlineTextBox.Text;
                _editingShape = null;
            }
            _inlineTextBox.Visible = false;
            this.Focus();
            this.Invalidate();
        }

        private void CancelInlineText()
        {
            _editingShape = null;
            _inlineTextBox.Visible = false;
            this.Focus();
        }

        public void SetZoom(float zoom)
        {
            ZoomFactor = Math.Max(0.2f, Math.Min(zoom, 5.0f));
            if (_inlineTextBox.Visible) CancelInlineText(); // 縮放時關閉編輯器防錯位
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
                
                if (_inlineTextBox.Visible) CancelInlineText();
                this.Invalidate();
            }
            else
            {
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
            // 如果正在輸入文字，不要攔截快捷鍵
            if (_inlineTextBox.Focused) return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == Keys.Escape) { OnToolResetRequested?.Invoke(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { CmdManager.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { CmdManager.Redo(); return true; }
            if (keyData == (Keys.Control | Keys.G)) { GroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.U)) { UngroupSelected(); return true; }

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
            if (_inlineTextBox.Visible && !_inlineTextBox.Bounds.Contains(e.Location))
            {
                CommitInlineText();
            }

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

                    // 碰撞偵測改為從上層往下尋找
                    App_Shapes.ShapeBase hit = null;
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) { hit = Shapes[i]; break; }
                    }

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
                    
                    // 尋找起始圖形
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) { ((App_Shapes.ConnectorShape)_tempShape).SourceId = Shapes[i].Id; break; }
                    }
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
            _smartGuides.Clear(); // 每次移動清空輔助線

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
                    // --- 進階功能：自動對齊磁吸 (Smart Guides) ---
                    if (SelectedShapes.Count == 1)
                    {
                        var me = SelectedShapes[0];
                        float snapThreshold = 5.0f / ZoomFactor; // 縮放時保持吸附手感

                        float bestDx = dx, bestDy = dy;
                        RectangleF futureBounds = new RectangleF(me.Bounds.X + dx, me.Bounds.Y + dy, me.Bounds.Width, me.Bounds.Height);
                        float myCenterX = futureBounds.X + futureBounds.Width / 2;
                        float myCenterY = futureBounds.Y + futureBounds.Height / 2;

                        foreach (var other in Shapes.Where(s => s != me && !(s is App_Shapes.ConnectorShape)))
                        {
                            float otherCenterX = other.Bounds.X + other.Bounds.Width / 2;
                            float otherCenterY = other.Bounds.Y + other.Bounds.Height / 2;

                            // X 軸對齊 (中心點對齊)
                            if (Math.Abs(myCenterX - otherCenterX) < snapThreshold)
                            {
                                bestDx = otherCenterX - (me.Bounds.X + me.Bounds.Width / 2);
                                _smartGuides.Add(new Tuple<PointF, PointF>(new PointF(otherCenterX, -10000), new PointF(otherCenterX, 10000)));
                            }
                            // Y 軸對齊 (中心點對齊)
                            if (Math.Abs(myCenterY - otherCenterY) < snapThreshold)
                            {
                                bestDy = otherCenterY - (me.Bounds.Y + me.Bounds.Height / 2);
                                _smartGuides.Add(new Tuple<PointF, PointF>(new PointF(-10000, otherCenterY), new PointF(10000, otherCenterY)));
                            }
                        }
                        
                        dx = bestDx;
                        dy = bestDy;
                    }

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
                    _hoveredShapeForConnection = null;
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].Id != c.SourceId && Shapes[i].HitTest(realPt))
                        {
                            _hoveredShapeForConnection = Shapes[i]; break;
                        }
                    }
                }
                
                // 防止吸附造成滑鼠真實位置偏移，手動重算
                _lastMousePos = new Point((int)(_lastMousePos.X + dx * ZoomFactor), (int)(_lastMousePos.Y + dy * ZoomFactor));
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

            _smartGuides.Clear(); // 放開滑鼠清空輔助線

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
                    
                    if (_tempShape is App_Shapes.TextNodeShape)
                    {
                        StartInlineEditing(_tempShape); // 畫完自動進入文字編輯
                        OnToolResetRequested?.Invoke();
                    }
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

        private class LineSegment { public PointF P1; public PointF P2; public LineSegment(PointF p1, PointF p2) { P1 = p1; P2 = p2; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

            // --- 進階效能：Viewport 剔除過濾 (Culling) ---
            RectangleF viewRect = new RectangleF(
                -_cameraOffset.X / ZoomFactor, 
                -_cameraOffset.Y / ZoomFactor, 
                this.Width / ZoomFactor, 
                this.Height / ZoomFactor);

            DrawGrid(g, viewRect);

            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, PageSize.Width, PageSize.Height);

            // 只繪製在可見範圍內的圖形
            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape)))
            {
                if (viewRect.IntersectsWith(shape.Bounds))
                {
                    shape.Draw(g);
                }
            }

            // 連線繪製與跳線偵測
            List<LineSegment> drawnLines = new List<LineSegment>();
            foreach (var shape in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                var src = Shapes.FirstOrDefault(x => x.Id == shape.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == shape.TargetId);
                
                // 套用全新的動態錨點演算法 GetIntersection()
                PointF p1 = src != null ? src.GetIntersection(tgt != null ? GetCenter(tgt.Bounds) : shape.EndPt) : shape.StartPt;
                PointF p2 = tgt != null ? tgt.GetIntersection(p1) : shape.EndPt;
                
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

            // 繪製粉紅色的 Smart Guides 對齊輔助線
            using (Pen guidePen = new Pen(Color.DeepPink, 1.5f) { DashStyle = DashStyle.Dash })
            {
                foreach (var line in _smartGuides)
                {
                    g.DrawLine(guidePen, line.Item1, line.Item2);
                }
            }

            g.ResetTransform();
        }

        private void DrawGrid(Graphics g, RectangleF viewRect)
        {
            int gridSize = 20;
            int startX = (int)(Math.Floor(viewRect.Left / gridSize) * gridSize);
            int startY = (int)(Math.Floor(viewRect.Top / gridSize) * gridSize);

            using (Pen gridPen = new Pen(Color.FromArgb(230, 230, 230)))
            {
                for (float x = startX; x < viewRect.Right; x += gridSize) g.DrawLine(gridPen, x, viewRect.Top, x, viewRect.Bottom);
                for (float y = startY; y < viewRect.Bottom; y += gridSize) g.DrawLine(gridPen, viewRect.Left, y, viewRect.Right, y);
            }
        }

        private PointF GetCenter(RectangleF r) => new PointF(r.X + r.Width / 2, r.Y + r.Height / 2);

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
