using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DrawingApp.Tools; // 引入新建立的 Tools 命名空間

namespace DrawingApp
{
    public class App_CanvasControl : Panel
    {
        public List<App_Shapes.ShapeBase> Shapes { get; set; } = new List<App_Shapes.ShapeBase>();
        
        private SizeF _basePageSize = new SizeF(2100, 2970);
        public SizeF PageSize 
        { 
            get => _basePageSize; 
            set 
            { 
                _basePageSize = value; 
                _isQuadTreeDirty = true;
                this.Invalidate(); 
            } 
        }

        public SizeF ActualPageSize 
        {
            get 
            {
                if (Shapes == null || Shapes.Count == 0) return _basePageSize;
                
                float maxX = _basePageSize.Width;
                float maxY = _basePageSize.Height;

                for (int i = 0; i < Shapes.Count; i++)
                {
                    var s = Shapes[i];
                    if (s is App_Shapes.ConnectorShape cs)
                    {
                        maxX = Math.Max(maxX, Math.Max(cs.StartPt.X, cs.EndPt.X) + 100);
                        maxY = Math.Max(maxY, Math.Max(cs.StartPt.Y, cs.EndPt.Y) + 100);
                    }
                    else
                    {
                        maxX = Math.Max(maxX, s.Bounds.Right + 100);
                        maxY = Math.Max(maxY, s.Bounds.Bottom + 100);
                    }
                }
                return new SizeF(maxX, maxY);
            }
        }
        
        public CommandManager CmdManager { get; } = new CommandManager();

        public float ZoomFactor { get; private set; } = 1.0f;
        private PointF _cameraOffset = new PointF(0, 0);
        
        // 平移狀態
        private bool _isPanning = false;
        private Point _lastMousePos;
        private Point _currentMouseScreenPos;

        // --- 工具狀態管理 (Tool Pattern) ---
        private ITool _currentToolInstance;
        private App_Shapes.ShapeType _currentToolType = App_Shapes.ShapeType.Pointer;
        private App_Shapes.ShapeType _previousToolType = App_Shapes.ShapeType.Pointer;
        
        public App_Shapes.ShapeType CurrentShapeType => _currentToolType;
        
        public App_Shapes.ShapeType CurrentTool 
        { 
            get => _currentToolType; 
            set 
            { 
                if (_currentToolType != App_Shapes.ShapeType.HandPan) _previousToolType = _currentToolType;
                
                // 通知舊工具關閉
                _currentToolInstance?.OnToolDeactivated(this);
                
                _currentToolType = value;
                
                // 根據類型切換實體
                switch (_currentToolType)
                {
                    case App_Shapes.ShapeType.Pointer:
                        _currentToolInstance = new PointerTool();
                        this.Cursor = Cursors.Default;
                        break;
                    case App_Shapes.ShapeType.HandPan:
                        _currentToolInstance = null; // 內部處理
                        this.Cursor = Cursors.Hand;
                        break;
                    case App_Shapes.ShapeType.FormatPainter:
                        _currentToolInstance = null; // 在 MouseDown 特殊處理
                        this.Cursor = Cursors.UpArrow;
                        break;
                    case App_Shapes.ShapeType.ArrowLine:
                    case App_Shapes.ShapeType.StraightLine:
                    case App_Shapes.ShapeType.OrthogonalLine:
                        _currentToolInstance = new ConnectionTool();
                        this.Cursor = Cursors.Cross;
                        break;
                    case App_Shapes.ShapeType.BezierPen:
                        _currentToolInstance = new BezierTool();
                        this.Cursor = Cursors.Cross;
                        break;
                    case App_Shapes.ShapeType.Image:
                        _currentToolInstance = null; // 點擊時觸發委派
                        this.Cursor = Cursors.Cross;
                        break;
                    default: // 其餘幾何圖形與文字、畫筆
                        _currentToolInstance = new DrawingTool();
                        this.Cursor = Cursors.Cross;
                        break;
                }
            } 
        }

        public Color CurrentColor { get; set; } = Color.Black;
        
        public bool SnapToGrid { get; set; } = true;
        public float GridSize { get; set; } = 20f;
        public bool ShowRulers { get; set; } = true;
        private const int RULER_SIZE = 25;

        // 共享給 Tool 使用的狀態
        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private App_Shapes.ShapeBase _tempShape = null;
        public App_Shapes.ShapeBase FormatSourceShape { get; set; }

        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;
        private App_Shapes.AnchorPosition _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

        private List<Tuple<PointF, PointF>> _smartGuides = new List<Tuple<PointF, PointF>>();

        // 小地圖與編輯器
        private Rectangle _minimapRect;
        private bool _isDraggingMinimap = false;
        private const int MINIMAP_WIDTH = 200;

        private TextBox _inlineTextBox;
        private App_Shapes.ShapeBase _editingShape = null;

        private App_Shapes.QuadTree _quadTree;
        private bool _isQuadTreeDirty = true;
        
        // 剪貼簿
        private List<App_Shapes.ShapeBase> _clipboard = new List<App_Shapes.ShapeBase>();

        public event Action<PointF> OnImageInsertRequested;
        public event Action<App_Shapes.ShapeType> OnToolChangedRequested;
        public event Action OnSelectionChanged;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            
            this.AllowDrop = true;
            this.DragEnter += Canvas_DragEnter;
            this.DragDrop += Canvas_DragDrop;
            
            this.ContextMenuStrip = CreateContextMenu();
            this.ContextMenuStrip.Opening += ContextMenuStrip_Opening; 
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.MouseWheel += Canvas_MouseWheel;
            this.MouseDoubleClick += Canvas_DoubleClick;

            CmdManager.OnStateChanged += () => {
                _isQuadTreeDirty = true;
                this.Invalidate();
            };

            InitializeInlineEditor();
            
            // 預設為 Pointer
            CurrentTool = App_Shapes.ShapeType.Pointer;
        }

        // --- 提供給 Tool 呼叫的公開 API ---
        public void RequestToolChange(App_Shapes.ShapeType type) => OnToolChangedRequested?.Invoke(type);
        public void SetTempShape(App_Shapes.ShapeBase shape) { _tempShape = shape; }
        public void TriggerSelectionChanged() => OnSelectionChanged?.Invoke();
        public void ClearSelection() { foreach (var s in SelectedShapes) s.IsSelected = false; SelectedShapes.Clear(); }
        
        public void SetHoveredConnectionTarget(App_Shapes.ShapeBase shape, App_Shapes.AnchorPosition anchor) 
        { 
            _hoveredShapeForConnection = shape; 
            _hoveredAnchor = anchor; 
        }
        public App_Shapes.ShapeBase GetHoveredConnectionTarget() => _hoveredShapeForConnection;
        public App_Shapes.AnchorPosition GetHoveredAnchor() => _hoveredAnchor;

        public void AddSmartGuide(PointF p1, PointF p2) => _smartGuides.Add(new Tuple<PointF, PointF>(p1, p2));
        public void ClearSmartGuides() => _smartGuides.Clear();
        public bool HasSmartGuides() => _smartGuides.Count > 0;
        
        public PointF GetRealPointFromMouse() => GetRealPoint(_currentMouseScreenPos);
        public void InvalidateMinimap() { if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect); }

        public float Snap(float value)
        {
            if (!SnapToGrid) return value;
            return (float)Math.Round(value / GridSize) * GridSize;
        }

        public float SnapAngle(float angle, float step)
        {
            if (Control.ModifierKeys == Keys.Alt) return angle; 
            return (float)Math.Round(angle / step) * step;
        }

        public void InvalidateWorldRect(RectangleF worldRect)
        {
            if (worldRect == RectangleF.Empty) return;
            float x = worldRect.X * ZoomFactor + _cameraOffset.X;
            float y = worldRect.Y * ZoomFactor + _cameraOffset.Y;
            float w = worldRect.Width * ZoomFactor;
            float h = worldRect.Height * ZoomFactor;
            
            Rectangle screenRect = new Rectangle((int)x - 50, (int)y - 50, (int)w + 100, (int)h + 100);
            this.Invalidate(screenRect);
        }

        public RectangleF GetShapesAndConnectorsBounds(List<App_Shapes.ShapeBase> shapes)
        {
            if (shapes == null || shapes.Count == 0) return RectangleF.Empty;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            
            var allAffected = new HashSet<App_Shapes.ShapeBase>(shapes);
            for (int i = 0; i < Shapes.Count; i++)
            {
                if (Shapes[i] is App_Shapes.ConnectorShape c)
                {
                    if (shapes.Any(s => s.Id == c.SourceId || s.Id == c.TargetId))
                        allAffected.Add(c);
                }
            }

            bool hasValidPoints = false;
            foreach (var s in allAffected)
            {
                if (s is App_Shapes.ConnectorShape cs)
                {
                    minX = Math.Min(minX, Math.Min(cs.StartPt.X, cs.EndPt.X));
                    minY = Math.Min(minY, Math.Min(cs.StartPt.Y, cs.EndPt.Y));
                    maxX = Math.Max(maxX, Math.Max(cs.StartPt.X, cs.EndPt.X));
                    maxY = Math.Max(maxY, Math.Max(cs.StartPt.Y, cs.EndPt.Y));
                    hasValidPoints = true;
                }
                else
                {
                    minX = Math.Min(minX, s.Bounds.Left);
                    minY = Math.Min(minY, s.Bounds.Top);
                    maxX = Math.Max(maxX, s.Bounds.Right);
                    maxY = Math.Max(maxY, s.Bounds.Bottom);
                    hasValidPoints = true;
                }
            }
            if (!hasValidPoints) return RectangleF.Empty;
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        private void EnsureQuadTree()
        {
            if (_isQuadTreeDirty || _quadTree == null)
            {
                SizeF size = ActualPageSize;
                _quadTree = new App_Shapes.QuadTree(0, new RectangleF(0, 0, size.Width, size.Height));
                for (int i = 0; i < Shapes.Count; i++)
                {
                    _quadTree.Insert(Shapes[i]);
                }
                _isQuadTreeDirty = false;
            }
        }

        public List<App_Shapes.ShapeBase> GetShapesInRect(RectangleF rect)
        {
            EnsureQuadTree();
            List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
            if (_quadTree != null)
            {
                _quadTree.Retrieve(nearShapes, rect);
                return nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();
            }
            return new List<App_Shapes.ShapeBase>();
        }

        public App_Shapes.ShapeBase GetShapeAtPoint(PointF pt)
        {
            var nearShapes = GetShapesInRect(new RectangleF(pt.X - 5, pt.Y - 5, 10, 10));
            foreach (var shape in nearShapes)
            {
                if (shape.HitTest(pt)) return shape;
            }
            return null;
        }

        private PointF GetRealPoint(Point screenPt)
        {
            return new PointF((screenPt.X - _cameraOffset.X) / ZoomFactor, (screenPt.Y - _cameraOffset.Y) / ZoomFactor);
        }

        // --- 滑鼠事件路由 (Delegation) ---
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
            if (_inlineTextBox.Visible && !_inlineTextBox.Bounds.Contains(e.Location)) CommitInlineText();
            PointF realPt = GetRealPoint(e.Location);
            _lastMousePos = e.Location;

            // 忽略點擊尺規區域
            if (ShowRulers && (e.X <= RULER_SIZE || e.Y <= RULER_SIZE)) return;

            // 點擊小地圖
            if (e.Button == MouseButtons.Left && _minimapRect.Contains(e.Location))
            {
                _isDraggingMinimap = true;
                UpdateCameraFromMinimap(e.Location);
                return;
            }

            // 平移判斷
            if (e.Button == MouseButtons.Middle || _isPanning || (e.Button == MouseButtons.Left && CurrentTool == App_Shapes.ShapeType.HandPan))
            {
                _isPanning = true;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 處理特殊的 Image 工具與格式刷
                if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    OnImageInsertRequested?.Invoke(realPt);
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
                            var cmd = new ChangeFormatCommand(new List<App_Shapes.ShapeBase> { hit });
                            hit.ApplyFormatFrom(FormatSourceShape);
                            cmd.CaptureNewState();
                            CmdManager.ExecuteCommand(cmd);
                        }
                    }
                    return;
                }

                // 將事件交給當前的 Tool
                _currentToolInstance?.OnMouseDown(this, e, realPt);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            _currentMouseScreenPos = e.Location;

            if (_isDraggingMinimap)
            {
                UpdateCameraFromMinimap(e.Location);
                return;
            }

            if (_isPanning)
            {
                _cameraOffset.X += e.X - _lastMousePos.X;
                _cameraOffset.Y += e.Y - _lastMousePos.Y;
                _lastMousePos = e.Location;
                this.Invalidate();
                return;
            }

            // 更新游標樣式
            UpdateCursor(realPt);

            // 將事件交給當前的 Tool
            _currentToolInstance?.OnMouseMove(this, e, realPt);

            // 尺規重繪與滑鼠追蹤
            if (e.Button == MouseButtons.None)
            {
                if (ShowRulers)
                {
                    this.Invalidate(new Rectangle(0, 0, this.Width, RULER_SIZE));
                    this.Invalidate(new Rectangle(0, 0, RULER_SIZE, this.Height));
                }
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDraggingMinimap)
            {
                _isDraggingMinimap = false;
                return;
            }

            if (_isPanning && e.Button != MouseButtons.Middle && Control.ModifierKeys != Keys.Space)
            {
                _isPanning = false;
                UpdateCursor(GetRealPoint(e.Location)); // 恢復正常游標
            }

            if (e.Button == MouseButtons.Left)
            {
                _currentToolInstance?.OnMouseUp(this, e, GetRealPoint(e.Location));
            }
        }

        // --- 鍵盤事件路由 ---
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_inlineTextBox.Focused) return base.ProcessCmdKey(ref msg, keyData);

            // 優先讓 Tool 處理鍵盤事件 (例如 ESC 取消)
            if (_currentToolInstance != null && _currentToolInstance.OnKeyDown(this, keyData))
            {
                return true;
            }

            if (keyData == Keys.Escape || keyData == Keys.Enter) 
            { 
                RequestToolChange(App_Shapes.ShapeType.Pointer);
                return true; 
            }

            if (keyData == Keys.V) { RequestToolChange(App_Shapes.ShapeType.Pointer); return true; }
            if (keyData == Keys.H) { RequestToolChange(App_Shapes.ShapeType.HandPan); return true; }
            if (keyData == Keys.T) { RequestToolChange(App_Shapes.ShapeType.TextNode); return true; }
            if (keyData == Keys.P) { RequestToolChange(App_Shapes.ShapeType.Freehand); return true; }
            if (keyData == Keys.B) { RequestToolChange(App_Shapes.ShapeType.BezierPen); return true; }
            if (keyData == Keys.L) { RequestToolChange(App_Shapes.ShapeType.OrthogonalLine); return true; }
            if (keyData == Keys.R) { RequestToolChange(App_Shapes.ShapeType.Rectangle); return true; }

            if (keyData == (Keys.Control | Keys.Z)) { CmdManager.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { CmdManager.Redo(); return true; }
            if (keyData == (Keys.Control | Keys.G)) { GroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.U)) { UngroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            if (keyData == (Keys.Control | Keys.D)) { DuplicateSelected(); return true; }

            // 鍵盤微調移動
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
                if (movableShapes.Count > 0)
                {
                    CmdManager.ExecuteCommand(new MoveShapesCommand(movableShapes, dx, dy));
                }
                return true;
            }

            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                var toRemove = new List<App_Shapes.ShapeBase>();
                for (int i = 0; i < SelectedShapes.Count; i++)
                {
                    if (!SelectedShapes[i].IsLocked) toRemove.Add(SelectedShapes[i]);
                }
                
                if (toRemove.Count == 0) return true;

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
                OnSelectionChanged?.Invoke();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
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

        // --- 游標更新邏輯 ---
        private void UpdateCursor(PointF realPt)
        {
            if (CurrentTool == App_Shapes.ShapeType.HandPan || _isPanning)
            {
                this.Cursor = Cursors.Hand;
                return;
            }
            if (CurrentTool == App_Shapes.ShapeType.FormatPainter)
            {
                this.Cursor = Cursors.UpArrow; 
                return;
            }
            if (CurrentTool != App_Shapes.ShapeType.Pointer)
            {
                this.Cursor = Cursors.Cross; 
                return;
            }

            // Pointer 狀態下偵測 Handles
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
            if (hitShape != null)
            {
                this.Cursor = hitShape.IsLocked ? Cursors.No : Cursors.SizeAll;
            }
            else
            {
                this.Cursor = Cursors.Default;
            }
        }

        // --- 渲染引擎 (OnPaint) ---
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            EnsureQuadTree();
            var oldTransform = g.Transform;

            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

            RectangleF clipWorldBounds = new RectangleF(
                (e.ClipRectangle.X - _cameraOffset.X) / ZoomFactor,
                (e.ClipRectangle.Y - _cameraOffset.Y) / ZoomFactor,
                e.ClipRectangle.Width / ZoomFactor,
                e.ClipRectangle.Height / ZoomFactor
            );

            SizeF currentCanvasSize = ActualPageSize;

            RectangleF viewRect = new RectangleF(
                -_cameraOffset.X / ZoomFactor, 
                -_cameraOffset.Y / ZoomFactor, 
                this.Width / ZoomFactor, 
                this.Height / ZoomFactor);

            if (SnapToGrid) DrawGrid(g, viewRect);

            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, currentCanvasSize.Width, currentCanvasSize.Height);

            List<App_Shapes.ShapeBase> visibleShapes = new List<App_Shapes.ShapeBase>();
            if (_quadTree != null) _quadTree.Retrieve(visibleShapes, clipWorldBounds);

            // 強制包含選取中或暫存的圖形，確保它們在拖曳時不消失
            foreach (var s in SelectedShapes) if (!visibleShapes.Contains(s)) visibleShapes.Add(s);
            if (_tempShape != null && !visibleShapes.Contains(_tempShape)) visibleShapes.Add(_tempShape);

            var sortedVisibleShapes = visibleShapes.Distinct().OrderBy(s => Shapes.IndexOf(s)).ToList();

            // 1. 繪製非連線圖形
            for (int i = 0; i < sortedVisibleShapes.Count; i++)
            {
                var shape = sortedVisibleShapes[i];
                if (!(shape is App_Shapes.ConnectorShape))
                {
                    shape.DrawWithTransform(g);
                }
            }

            // 2. 繪製連線圖形 (包含跳線與避障運算)
            bool isFastMode = false; // 可擴充判定是否正在拖曳
            for (int i = 0; i < Shapes.Count; i++)
            {
                if (Shapes[i] is App_Shapes.ConnectorShape shape)
                {
                    App_Shapes.ShapeBase src = null, tgt = null;
                    for (int j = 0; j < Shapes.Count; j++)
                    {
                        if (Shapes[j].Id == shape.SourceId) src = Shapes[j];
                        if (Shapes[j].Id == shape.TargetId) tgt = Shapes[j];
                        if (src != null && tgt != null) break;
                    }

                    PointF p1 = shape.StartPt, p2 = shape.EndPt;

                    if (src != null)
                        p1 = shape.SourceAnchor == App_Shapes.AnchorPosition.Auto ? src.GetIntersection(tgt != null ? tgt.GetCenter() : shape.EndPt) : src.GetAnchorPoint(shape.SourceAnchor);
                    
                    if (tgt != null)
                        p2 = shape.TargetAnchor == App_Shapes.AnchorPosition.Auto ? tgt.GetIntersection(p1) : tgt.GetAnchorPoint(shape.TargetAnchor);

                    shape.DrawDynamic(g, p1, p2, Shapes, isFastMode, _quadTree);
                }
            }

            // 3. 繪製暫存圖形
            _tempShape?.DrawWithTransform(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt, Shapes, true, _quadTree); 
            
            // 4. 交給 Tool 繪製額外的 UI (例如懸停導引線、框選框)
            _currentToolInstance?.OnPaint(this, g);

            // 5. 繪製連線吸附提示紅點
            if (_hoveredShapeForConnection != null)
            {
                PointF anchorPt = _hoveredAnchor == App_Shapes.AnchorPosition.Auto 
                    ? _hoveredShapeForConnection.GetIntersection(GetRealPointFromMouse()) 
                    : _hoveredShapeForConnection.GetAnchorPoint(_hoveredAnchor);
                
                g.FillEllipse(Brushes.LightCoral, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
                g.DrawEllipse(Pens.Red, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
            }

            // 6. 繪製選取框
            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].DrawSelection(g);

            // 7. 繪製智慧導引線
            using (Pen guidePen = new Pen(Color.DeepPink, 1.5f) { DashStyle = DashStyle.Dash })
            {
                for (int i = 0; i < _smartGuides.Count; i++) g.DrawLine(guidePen, _smartGuides[i].Item1, _smartGuides[i].Item2);
            }

            g.Transform = oldTransform; 

            // 8. 繪製小地圖與尺規
            DrawMinimap(g, currentCanvasSize);
            if (ShowRulers) DrawRulers(g);
        }

        // --- 其他輔助方法 (文字編輯、剪貼簿、縮放等) ---
        // (此部分大多與原邏輯相同，僅為保持結構完整而保留)
        private void InitializeInlineEditor()
        {
            _inlineTextBox = new TextBox();
            _inlineTextBox.Multiline = true;
            _inlineTextBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineTextBox.Visible = false;
            
            _inlineTextBox.Leave += (s, e) => CommitInlineText();
            _inlineTextBox.KeyDown += (s, e) =>
            {
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

        public void StartInlineEditing(App_Shapes.ShapeBase shape)
        {
            _editingShape = shape;
            _inlineTextBox.Text = shape.Text;
            
            FontStyle style = FontStyle.Regular;
            if (shape.FontBold) style |= FontStyle.Bold;
            if (shape.FontItalic) style |= FontStyle.Italic;
            if (shape.FontUnderline) style |= FontStyle.Underline;
            
            _inlineTextBox.Font = new Font(shape.FontName, shape.FontSize * ZoomFactor, style);
            _inlineTextBox.ForeColor = shape.FontColor;

            if (shape.TextAlignment == App_Shapes.TextAlign.TopLeft || shape.TextAlignment == App_Shapes.TextAlign.MiddleLeft || shape.TextAlignment == App_Shapes.TextAlign.BottomLeft)
                _inlineTextBox.TextAlign = HorizontalAlignment.Left;
            else if (shape.TextAlignment == App_Shapes.TextAlign.TopRight || shape.TextAlignment == App_Shapes.TextAlign.MiddleRight || shape.TextAlignment == App_Shapes.TextAlign.BottomRight)
                _inlineTextBox.TextAlign = HorizontalAlignment.Right;
            else
                _inlineTextBox.TextAlign = HorizontalAlignment.Center;

            PointF center = shape.GetCenter();
            PointF screenCenter = new PointF(center.X * ZoomFactor + _cameraOffset.X, center.Y * ZoomFactor + _cameraOffset.Y);
            int screenW = (int)(shape.Bounds.Width * ZoomFactor);
            int screenH = (int)(shape.Bounds.Height * ZoomFactor);

            _inlineTextBox.Bounds = new Rectangle((int)screenCenter.X - screenW/2 + 5, (int)screenCenter.Y - screenH/2 + 5, screenW - 10, screenH - 10);
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

        public void SetZoom(float zoom)
        {
            ZoomFactor = Math.Max(0.2f, Math.Min(zoom, 5.0f));
            if (_inlineTextBox.Visible) CancelInlineText();
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

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("複製 (Ctrl+C)", null, (s, e) => Copy());
            menu.Items.Add("貼上 (Ctrl+V)", null, (s, e) => Paste());
            menu.Items.Add("原地複製 (Ctrl+D)", null, (s, e) => DuplicateSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("群組 (Ctrl+G)", null, (s, e) => GroupSelected());
            menu.Items.Add("解除群組 (Ctrl+U)", null, (s, e) => UngroupSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0));
            menu.Items.Add("移到最下層", null, (s, e) => ChangeZIndex(-99));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("鎖定/解鎖圖形", null, (s, e) => ToggleLock());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("匯出選取物件 (PNG)", null, async (s, e) => {
                if (SelectedShapes.Count == 0) return;
                using (var sfd = new SaveFileDialog() { Filter = "PNG 圖片|*.png" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportSelectionToPngAsync(SelectedShapes, sfd.FileName);
                        MessageBox.Show("局部匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            });
            return menu;
        }

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var menu = (ContextMenuStrip)sender;
            bool hasSelection = SelectedShapes.Count > 0;
            bool hasGroupSelection = SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape;
            bool hasClipboard = _clipboard.Count > 0 || Clipboard.ContainsImage();

            menu.Items[0].Enabled = hasSelection; 
            menu.Items[1].Enabled = hasClipboard; 
            menu.Items[2].Enabled = hasSelection; 
            menu.Items[4].Enabled = SelectedShapes.Count > 1; 
            menu.Items[5].Enabled = hasGroupSelection; 
            menu.Items[7].Enabled = hasSelection; 
            menu.Items[8].Enabled = hasSelection; 
            menu.Items[10].Enabled = hasSelection; 
            menu.Items[12].Enabled = hasSelection; 
            
            if (hasSelection)
            {
                bool isAllLocked = SelectedShapes.All(s => s.IsLocked);
                menu.Items[10].Text = isAllLocked ? "解鎖圖形" : "鎖定圖形";
            }
        }
        
        // --- 基本剪貼與群組邏輯 ---
        private void ToggleLock() { if (SelectedShapes.Count > 0) { bool isAllLocked = SelectedShapes.All(s => s.IsLocked); foreach (var s in SelectedShapes) s.IsLocked = !isAllLocked; this.Invalidate(); } }
        public void ChangeZIndex(int direction) { if (SelectedShapes.Count > 0) CmdManager.ExecuteCommand(new ChangeZIndexCommand(Shapes, SelectedShapes, direction)); }
        private void GroupSelected() { if (SelectedShapes.Count < 2) return; var group = new App_Shapes.GroupShape(SelectedShapes.ToList()); CmdManager.ExecuteCommand(new GroupCommand(Shapes, SelectedShapes, group)); ClearSelection(); SelectedShapes.Add(group); group.IsSelected = true; TriggerSelectionChanged(); }
        private void UngroupSelected() { if (SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape group && !group.IsLocked) { CmdManager.ExecuteCommand(new UngroupCommand(Shapes, group)); ClearSelection(); foreach (var child in group.Children) { child.IsSelected = true; SelectedShapes.Add(child); } TriggerSelectionChanged(); } }

        private void Copy() { if (SelectedShapes.Count > 0) { _clipboard = App_SaveLoad.CloneShapes(SelectedShapes); } }
        private void Paste() {
            if (_clipboard.Count > 0) {
                ClearSelection();
                var newClones = App_SaveLoad.CloneShapes(_clipboard);
                foreach (var s in newClones) { s.Id = Guid.NewGuid(); s.IsLocked = false; s.Move(20, 20); s.IsSelected = true; SelectedShapes.Add(s); }
                CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, newClones));
                TriggerSelectionChanged();
            }
        }
        private void DuplicateSelected() {
            if (SelectedShapes.Count == 0) return;
            var clones = App_SaveLoad.CloneShapes(SelectedShapes);
            foreach (var c in clones) { c.Id = Guid.NewGuid(); c.IsLocked = false; c.Move(10, 10); }
            CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, clones));
            ClearSelection();
            foreach (var c in clones) { c.IsSelected = true; SelectedShapes.Add(c); }
            TriggerSelectionChanged();
        }

        private void Canvas_DragEnter(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(typeof(App_Shapes.ShapeType)) ? DragDropEffects.Copy : DragDropEffects.None; }
        private void Canvas_DragDrop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(typeof(App_Shapes.ShapeType))) {
                App_Shapes.ShapeType type = (App_Shapes.ShapeType)e.Data.GetData(typeof(App_Shapes.ShapeType));
                PointF realPt = GetRealPoint(this.PointToClient(new Point(e.X, e.Y)));
                PointF snapPt = new PointF(Snap(realPt.X), Snap(realPt.Y));
                var newShape = App_Shapes.ShapeFactory.CreateShape(type, snapPt, CurrentColor);
                if (newShape != null) {
                    newShape.UpdateEndPoint(new PointF(snapPt.X + 80, snapPt.Y + 80)); 
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newShape));
                    ClearSelection(); newShape.IsSelected = true; SelectedShapes.Add(newShape);
                    TriggerSelectionChanged();
                    _isQuadTreeDirty = true; this.Invalidate();
                }
            }
        }
        
        // --- 小地圖與尺規繪製邏輯 ---
        private void UpdateCameraFromMinimap(Point mouseLoc) {
            float minimapScale = MINIMAP_WIDTH / ActualPageSize.Width;
            float targetX = (mouseLoc.X - _minimapRect.X) / minimapScale;
            float targetY = (mouseLoc.Y - _minimapRect.Y) / minimapScale;
            _cameraOffset.X = -(targetX * ZoomFactor - this.Width / 2f);
            _cameraOffset.Y = -(targetY * ZoomFactor - this.Height / 2f);
            this.Invalidate();
        }

        private void DrawMinimap(Graphics g, SizeF currentCanvasSize) {
            float minimapScale = MINIMAP_WIDTH / currentCanvasSize.Width;
            int minimapHeight = (int)(currentCanvasSize.Height * minimapScale);
            _minimapRect = new Rectangle(this.Width - MINIMAP_WIDTH - 20, this.Height - minimapHeight - 20, MINIMAP_WIDTH, minimapHeight);

            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 245, 245, 245))) g.FillRectangle(bgBrush, _minimapRect);
            g.DrawRectangle(Pens.Gray, _minimapRect);

            for (int i = 0; i < Shapes.Count; i++) {
                var shape = Shapes[i];
                if (!(shape is App_Shapes.ConnectorShape)) {
                    float sx = _minimapRect.X + shape.Bounds.X * minimapScale, sy = _minimapRect.Y + shape.Bounds.Y * minimapScale;
                    float sw = shape.Bounds.Width * minimapScale, sh = shape.Bounds.Height * minimapScale;
                    Color renderColor = (shape.FillColor != Color.Transparent) ? shape.FillColor : shape.ShapeColor;
                    using (Brush b = new SolidBrush(renderColor)) g.FillRectangle(b, sx, sy, sw, sh);
                }
            }

            float vx = _minimapRect.X + (-_cameraOffset.X / ZoomFactor) * minimapScale, vy = _minimapRect.Y + (-_cameraOffset.Y / ZoomFactor) * minimapScale;
            float vw = (this.Width / ZoomFactor) * minimapScale, vh = (this.Height / ZoomFactor) * minimapScale;
            vx = Math.Max(_minimapRect.Left, Math.Min(vx, _minimapRect.Right - vw));
            vy = Math.Max(_minimapRect.Top, Math.Min(vy, _minimapRect.Bottom - vh));
            using (Pen vp = new Pen(Color.Red, 2f)) g.DrawRectangle(vp, vx, vy, vw, vh);
        }

        private void DrawGrid(Graphics g, RectangleF viewRect) {
            int startX = (int)(Math.Floor(viewRect.Left / GridSize) * GridSize), startY = (int)(Math.Floor(viewRect.Top / GridSize) * GridSize);
            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235))) {
                for (float x = startX; x < viewRect.Right; x += GridSize) g.DrawLine(gridPen, x, viewRect.Top, x, viewRect.Bottom);
                for (float y = startY; y < viewRect.Bottom; y += GridSize) g.DrawLine(gridPen, viewRect.Left, y, viewRect.Right, y);
            }
        }

        private void DrawRulers(Graphics g) {
            g.SmoothingMode = SmoothingMode.None; 
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Brush rulerBg = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (Pen rulerPen = new Pen(Color.FromArgb(180, 180, 180)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
            using (Font rulerFont = new Font("Arial", 7)) {
                g.FillRectangle(rulerBg, 0, 0, this.Width, RULER_SIZE); g.DrawLine(rulerPen, 0, RULER_SIZE, this.Width, RULER_SIZE);
                g.FillRectangle(rulerBg, 0, 0, RULER_SIZE, this.Height); g.DrawLine(rulerPen, RULER_SIZE, 0, RULER_SIZE, this.Height);
                g.FillRectangle(Brushes.White, 0, 0, RULER_SIZE, RULER_SIZE); g.DrawRectangle(rulerPen, 0, 0, RULER_SIZE, RULER_SIZE);

                float step = 100 * ZoomFactor, subStep = step / 10;
                float startX = _cameraOffset.X % step, worldStartX = -((int)(_cameraOffset.X / step)) * 100;
                if (startX > 0) { startX -= step; worldStartX -= 100; }

                for (float x = startX; x < this.Width; x += step) {
                    if (x > RULER_SIZE) { g.DrawLine(rulerPen, x, 0, x, RULER_SIZE); g.DrawString(worldStartX.ToString(), rulerFont, textBrush, x + 2, 2); }
                    for (int i = 1; i < 10; i++) {
                        float subX = x + i * subStep;
                        if (subX > RULER_SIZE) { int lineLen = (i == 5) ? 10 : 5; g.DrawLine(rulerPen, subX, RULER_SIZE - lineLen, subX, RULER_SIZE); }
                    }
                    worldStartX += 100;
                }

                float startY = _cameraOffset.Y % step, worldStartY = -((int)(_cameraOffset.Y / step)) * 100;
                if (startY > 0) { startY -= step; worldStartY -= 100; }
                StringFormat sfVert = new StringFormat() { FormatFlags = StringFormatFlags.DirectionVertical };

                for (float y = startY; y < this.Height; y += step) {
                    if (y > RULER_SIZE) { g.DrawLine(rulerPen, 0, y, RULER_SIZE, y); g.DrawString(worldStartY.ToString(), rulerFont, textBrush, 2, y + 2, sfVert); }
                    for (int i = 1; i < 10; i++) {
                        float subY = y + i * subStep;
                        if (subY > RULER_SIZE) { int lineLen = (i == 5) ? 10 : 5; g.DrawLine(rulerPen, RULER_SIZE - lineLen, subY, RULER_SIZE, subY); }
                    }
                    worldStartY += 100;
                }

                if (_currentMouseScreenPos != Point.Empty) {
                    using (Pen cursorPen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash }) {
                        g.DrawLine(cursorPen, _currentMouseScreenPos.X, 0, _currentMouseScreenPos.X, RULER_SIZE);
                        g.DrawLine(cursorPen, 0, _currentMouseScreenPos.Y, RULER_SIZE, _currentMouseScreenPos.Y);
                    }
                }
            }
        }

        public Bitmap GetTransparentCanvasRender() {
            ClearSelection();
            float maxX = ActualPageSize.Width, maxY = ActualPageSize.Height;
            Bitmap bmp = new Bitmap(Math.Max((int)maxX + 50, (int)PageSize.Width), Math.Max((int)maxY + 50, (int)PageSize.Height)); 
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < Shapes.Count; i++) Shapes[i].DrawWithTransform(g);
            }
            return bmp;
        }
    }
}
