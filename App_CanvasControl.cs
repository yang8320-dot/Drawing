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

        public float ZoomFactor { get; private set; } = 1.0f;
        private PointF _cameraOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;
        
        private float _dragTotalDx = 0;
        private float _dragTotalDy = 0;

        private RectangleF _initialBounds;
        private float _initialAngle;

        private Guid _oldSrcId, _oldTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA;
        private PointF _oldStart, _oldEnd;

        private enum InteractionState { Idle, Drawing, Moving, Resizing, Rotating, Connecting, BoxSelecting }
        private InteractionState _currentState = InteractionState.Idle;

        private App_Shapes.ShapeType _currentTool = App_Shapes.ShapeType.Pointer;
        public App_Shapes.ShapeType CurrentTool 
        { 
            get => _currentTool; 
            set 
            { 
                _currentTool = value;
                this.Cursor = (_currentTool == App_Shapes.ShapeType.HandPan) ? Cursors.Hand : Cursors.Default;
            } 
        }

        public Color CurrentColor { get; set; } = Color.Black;
        
        public bool SnapToGrid { get; set; } = true;
        public float GridSize { get; set; } = 20f;

        private App_Shapes.ShapeBase _tempShape = null;
        private RectangleF _boxSelectRect;
        
        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private List<App_Shapes.ShapeBase> _clipboard = new List<App_Shapes.ShapeBase>();
        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;
        private App_Shapes.AnchorPosition _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

        private TextBox _inlineTextBox;
        private App_Shapes.ShapeBase _editingShape = null;

        private List<Tuple<PointF, PointF>> _smartGuides = new List<Tuple<PointF, PointF>>();

        private Rectangle _minimapRect;
        private bool _isDraggingMinimap = false;
        private const int MINIMAP_WIDTH = 200;

        public event Action<App_Shapes.ShapeBase> OnShapePropertyRequested;
        public event Action<PointF> OnImageInsertRequested;
        public event Action OnToolResetRequested;
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
            this.DoubleClick += Canvas_DoubleClick;

            CmdManager.OnStateChanged += () => this.Invalidate();

            InitializeInlineEditor();
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

        private void Canvas_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(App_Shapes.ShapeType)))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Canvas_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(App_Shapes.ShapeType)))
            {
                App_Shapes.ShapeType type = (App_Shapes.ShapeType)e.Data.GetData(typeof(App_Shapes.ShapeType));
                Point clientPt = this.PointToClient(new Point(e.X, e.Y));
                PointF realPt = GetRealPoint(clientPt);
                
                PointF snapPt = new PointF(Snap(realPt.X), Snap(realPt.Y));
                var newShape = App_Shapes.ShapeFactory.CreateShape(type, snapPt, CurrentColor);
                if (newShape != null)
                {
                    newShape.UpdateEndPoint(new PointF(snapPt.X + 80, snapPt.Y + 80)); 
                    
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newShape));
                    
                    SelectedShapes.ForEach(s => s.IsSelected = false);
                    SelectedShapes.Clear();
                    newShape.IsSelected = true;
                    SelectedShapes.Add(newShape);
                    OnSelectionChanged?.Invoke();
                    
                    this.Invalidate();
                }
            }
        }

        private void InitializeInlineEditor()
        {
            _inlineTextBox = new TextBox();
            _inlineTextBox.Multiline = true;
            _inlineTextBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineTextBox.TextAlign = HorizontalAlignment.Center;
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
            menu.Items.Add("鎖定/解鎖圖形", null, (s, e) => ToggleLock());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("進階屬性設定...", null, (s, e) => {
                if (SelectedShapes.Count == 1) OnShapePropertyRequested?.Invoke(SelectedShapes[0]);
            });
            return menu;
        }

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var menu = (ContextMenuStrip)sender;
            bool hasSelection = SelectedShapes.Count > 0;
            bool hasSingleSelection = SelectedShapes.Count == 1;
            bool hasGroupSelection = SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape;
            
            bool hasClipboard = _clipboard.Count > 0 || Clipboard.ContainsImage();

            menu.Items[0].Enabled = hasSelection; 
            menu.Items[1].Enabled = hasClipboard; 
            menu.Items[3].Enabled = SelectedShapes.Count > 1; 
            menu.Items[4].Enabled = hasGroupSelection; 
            menu.Items[6].Enabled = hasSelection; 
            menu.Items[7].Enabled = hasSelection; 
            menu.Items[9].Enabled = hasSelection; 
            
            if (hasSelection)
            {
                bool isAllLocked = SelectedShapes.All(s => s.IsLocked);
                menu.Items[9].Text = isAllLocked ? "解鎖圖形" : "鎖定圖形";
            }
        }

        private void ToggleLock()
        {
            if (SelectedShapes.Count == 0) return;
            bool isAllLocked = SelectedShapes.All(s => s.IsLocked);
            foreach (var s in SelectedShapes) s.IsLocked = !isAllLocked;
            this.Invalidate();
        }

        public void ChangeZIndex(int direction)
        {
            if (SelectedShapes.Count == 0) return;
            CmdManager.ExecuteCommand(new ChangeZIndexCommand(Shapes, SelectedShapes, direction));
            this.Invalidate();
        }

        private void GroupSelected()
        {
            if (SelectedShapes.Count < 2) return;
            var group = new App_Shapes.GroupShape(SelectedShapes.ToList());
            CmdManager.ExecuteCommand(new GroupCommand(Shapes, SelectedShapes, group));
            SelectedShapes.Clear();
            SelectedShapes.Add(group);
            group.IsSelected = true;
            OnSelectionChanged?.Invoke();
            this.Invalidate();
        }

        private void UngroupSelected()
        {
            if (SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape group)
            {
                if (group.IsLocked) return; 

                CmdManager.ExecuteCommand(new UngroupCommand(Shapes, group));
                SelectedShapes.Clear();
                foreach (var child in group.Children)
                {
                    child.IsSelected = true;
                    SelectedShapes.Add(child);
                }
                OnSelectionChanged?.Invoke();
                this.Invalidate();
            }
        }

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
            
            FontStyle style = FontStyle.Regular;
            if (shape.FontBold) style |= FontStyle.Bold;
            if (shape.FontItalic) style |= FontStyle.Italic;
            if (shape.FontUnderline) style |= FontStyle.Underline;
            
            _inlineTextBox.Font = new Font(shape.FontName, shape.FontSize * ZoomFactor, style);
            _inlineTextBox.ForeColor = shape.FontColor;

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

        private PointF GetRealPoint(Point screenPt)
        {
            return new PointF((screenPt.X - _cameraOffset.X) / ZoomFactor, (screenPt.Y - _cameraOffset.Y) / ZoomFactor);
        }

        private float Snap(float value)
        {
            if (!SnapToGrid) return value;
            return (float)Math.Round(value / GridSize) * GridSize;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_inlineTextBox.Focused) return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == Keys.Escape) 
            { 
                _tempShape = null;
                _hoveredShapeForConnection = null;
                _hoveredAnchor = App_Shapes.AnchorPosition.Auto;
                _currentState = InteractionState.Idle;
                this.Invalidate();
                
                OnToolResetRequested?.Invoke();
                return true; 
            }

            if (keyData == (Keys.Control | Keys.Z)) { CmdManager.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { CmdManager.Redo(); return true; }
            if (keyData == (Keys.Control | Keys.G)) { GroupSelected(); return true; }
            if (keyData == (Keys.Control | Keys.U)) { UngroupSelected(); return true; }

            Keys baseKey = keyData & ~Keys.Modifiers;
            if (SelectedShapes.Count > 0 && 
               (baseKey == Keys.Up || baseKey == Keys.Down || baseKey == Keys.Left || baseKey == Keys.Right))
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
                    this.Invalidate();
                }
                return true;
            }

            if (keyData == Keys.Delete && SelectedShapes.Count > 0)
            {
                var toRemove = new List<App_Shapes.ShapeBase>(SelectedShapes.Where(s => !s.IsLocked));
                if (toRemove.Count == 0) return true;

                foreach (var s in toRemove.ToList())
                {
                    toRemove.AddRange(Shapes.OfType<App_Shapes.ConnectorShape>().Where(c => c.SourceId == s.Id || c.TargetId == s.Id));
                }
                CmdManager.ExecuteCommand(new RemoveShapesCommand(Shapes, toRemove));
                SelectedShapes.RemoveAll(s => !s.IsLocked);
                OnSelectionChanged?.Invoke();
                this.Invalidate();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Copy() 
        { 
            if (SelectedShapes.Count > 0) 
            {
                _clipboard = App_SaveLoad.CloneShapes(SelectedShapes); 

                try
                {
                    float minX = SelectedShapes.Min(s => Math.Min(s.Bounds.Left, s.Bounds.Right));
                    float minY = SelectedShapes.Min(s => Math.Min(s.Bounds.Top, s.Bounds.Bottom));
                    float maxX = SelectedShapes.Max(s => Math.Max(s.Bounds.Left, s.Bounds.Right));
                    float maxY = SelectedShapes.Max(s => Math.Max(s.Bounds.Top, s.Bounds.Bottom));

                    int w = (int)(maxX - minX + 20);
                    int h = (int)(maxY - minY + 20);
                    
                    if (w > 0 && h > 0)
                    {
                        Bitmap bmp = new Bitmap(w, h);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.Transparent);
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.TranslateTransform(-minX + 10, -minY + 10);
                            
                            foreach(var s in SelectedShapes) 
                            {
                                bool wasSelected = s.IsSelected;
                                s.IsSelected = false; 
                                s.DrawWithTransform(g);
                                s.IsSelected = wasSelected;
                            }
                        }
                        Clipboard.SetImage(bmp);
                    }
                }
                catch { }
            }
        }

        private void Paste()
        {
            if (_clipboard.Count > 0)
            {
                SelectedShapes.ForEach(s => s.IsSelected = false);
                SelectedShapes.Clear();
                
                var newClones = App_SaveLoad.CloneShapes(_clipboard);
                foreach (var s in newClones)
                {
                    s.Id = Guid.NewGuid();
                    s.IsLocked = false; 
                    s.Move(20, 20);
                    s.IsSelected = true;
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, s));
                    SelectedShapes.Add(s);
                }
                OnSelectionChanged?.Invoke();
                this.Invalidate();
                return;
            }

            if (Clipboard.ContainsImage())
            {
                Image img = Clipboard.GetImage();
                if (img != null)
                {
                    PointF pt = GetRealPoint(_lastMousePos == Point.Empty ? new Point(50, 50) : _lastMousePos);
                    var newImgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, new Bitmap(img));
                    
                    SelectedShapes.ForEach(s => s.IsSelected = false);
                    SelectedShapes.Clear();
                    
                    newImgShape.IsSelected = true;
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newImgShape));
                    SelectedShapes.Add(newImgShape);
                    
                    OnSelectionChanged?.Invoke();
                    this.Invalidate();
                }
            }
        }

        // --- 優化：新增局部重繪計算器 ---
        private void InvalidateWorldRect(RectangleF worldRect)
        {
            if (worldRect == RectangleF.Empty) return;
            float x = worldRect.X * ZoomFactor + _cameraOffset.X;
            float y = worldRect.Y * ZoomFactor + _cameraOffset.Y;
            float w = worldRect.Width * ZoomFactor;
            float h = worldRect.Height * ZoomFactor;
            
            // 放大重繪範圍，涵蓋控制節點與邊框粗細
            Rectangle screenRect = new Rectangle((int)x - 50, (int)y - 50, (int)w + 100, (int)h + 100);
            this.Invalidate(screenRect);
        }

        // --- 優化：取得指定圖形及其連線的總邊界 ---
        private RectangleF GetShapesAndConnectorsBounds(List<App_Shapes.ShapeBase> shapes)
        {
            if (shapes == null || shapes.Count == 0) return RectangleF.Empty;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            
            var allAffected = new HashSet<App_Shapes.ShapeBase>(shapes);
            foreach (var c in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                if (shapes.Any(s => s.Id == c.SourceId || s.Id == c.TargetId))
                    allAffected.Add(c);
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

        // --- 優化：智慧游標變更邏輯 ---
        private void UpdateCursor(PointF realPt)
        {
            if (_currentState != InteractionState.Idle) return; 

            if (CurrentTool == App_Shapes.ShapeType.HandPan)
            {
                this.Cursor = Cursors.Hand;
                return;
            }

            if (CurrentTool != App_Shapes.ShapeType.Pointer)
            {
                this.Cursor = Cursors.Cross; 
                return;
            }

            if (SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
            {
                var handle = SelectedShapes[0].HitTestHandle(realPt);
                switch (handle)
                {
                    case App_Shapes.HandlePosition.NW:
                    case App_Shapes.HandlePosition.SE:
                        this.Cursor = Cursors.SizeNWSE; return;
                    case App_Shapes.HandlePosition.NE:
                    case App_Shapes.HandlePosition.SW:
                        this.Cursor = Cursors.SizeNESW; return;
                    case App_Shapes.HandlePosition.N:
                    case App_Shapes.HandlePosition.S:
                        this.Cursor = Cursors.SizeNS; return;
                    case App_Shapes.HandlePosition.E:
                    case App_Shapes.HandlePosition.W:
                        this.Cursor = Cursors.SizeWE; return;
                    case App_Shapes.HandlePosition.Rotate:
                        this.Cursor = Cursors.Hand; return; 
                    case App_Shapes.HandlePosition.StartPoint:
                    case App_Shapes.HandlePosition.EndPoint:
                        this.Cursor = Cursors.SizeAll; return;
                }
            }

            for (int i = Shapes.Count - 1; i >= 0; i--)
            {
                if (Shapes[i].HitTest(realPt))
                {
                    this.Cursor = Shapes[i].IsLocked ? Cursors.No : Cursors.SizeAll;
                    return;
                }
            }

            this.Cursor = Cursors.Default;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_inlineTextBox.Visible && !_inlineTextBox.Bounds.Contains(e.Location))
                CommitInlineText();

            this.Focus();
            PointF realPt = GetRealPoint(e.Location);
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Left && _minimapRect.Contains(e.Location))
            {
                _isDraggingMinimap = true;
                UpdateCameraFromMinimap(e.Location);
                return;
            }

            if (e.Button == MouseButtons.Middle || 
               (e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Space) ||
               (e.Button == MouseButtons.Left && CurrentTool == App_Shapes.ShapeType.HandPan))
            {
                _isPanning = true;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (CurrentTool == App_Shapes.ShapeType.Pointer)
                {
                    if (SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
                    {
                        var handle = SelectedShapes[0].HitTestHandle(realPt);
                        if (handle == App_Shapes.HandlePosition.Rotate)
                        {
                            _currentState = InteractionState.Rotating;
                            _initialAngle = SelectedShapes[0].RotationAngle;
                            return;
                        }
                        else if (handle != App_Shapes.HandlePosition.None)
                        {
                            _currentState = InteractionState.Resizing;
                            _resizingHandle = handle;
                            
                            if (SelectedShapes[0] is App_Shapes.ConnectorShape conn)
                            {
                                _oldSrcId = conn.SourceId; _oldTgtId = conn.TargetId;
                                _oldSA = conn.SourceAnchor; _oldTA = conn.TargetAnchor;
                                _oldStart = conn.StartPt; _oldEnd = conn.EndPt;
                            }
                            else
                            {
                                _initialBounds = SelectedShapes[0].Bounds;
                            }
                            return;
                        }
                    }

                    App_Shapes.ShapeBase hit = null;
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) { hit = Shapes[i]; break; }
                    }

                    if (hit != null)
                    {
                        bool isMultiSelectKey = (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift);
                        
                        if (isMultiSelectKey)
                        {
                            if (SelectedShapes.Contains(hit))
                            {
                                hit.IsSelected = false;
                                SelectedShapes.Remove(hit);
                            }
                            else
                            {
                                hit.IsSelected = true;
                                SelectedShapes.Add(hit);
                            }
                        }
                        else
                        {
                            if (!SelectedShapes.Contains(hit))
                            {
                                SelectedShapes.ForEach(s => s.IsSelected = false);
                                SelectedShapes.Clear();
                                hit.IsSelected = true;
                                SelectedShapes.Add(hit);
                            }
                        }
                        
                        OnSelectionChanged?.Invoke();

                        if (SelectedShapes.Any(s => !s.IsLocked))
                        {
                            _currentState = InteractionState.Moving;
                            _dragTotalDx = 0; _dragTotalDy = 0;
                        }
                    }
                    else
                    {
                        SelectedShapes.ForEach(s => s.IsSelected = false);
                        SelectedShapes.Clear();
                        OnSelectionChanged?.Invoke();
                        
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
                    
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) 
                        { 
                            var conn = (App_Shapes.ConnectorShape)_tempShape;
                            conn.SourceId = Shapes[i].Id; 
                            conn.SourceAnchor = DetectAnchor(Shapes[i], realPt);
                            break; 
                        }
                    }
                }
                else if (CurrentTool != App_Shapes.ShapeType.HandPan)
                {
                    _currentState = InteractionState.Drawing;
                    PointF snapPt = CurrentTool == App_Shapes.ShapeType.Freehand ? realPt : new PointF(Snap(realPt.X), Snap(realPt.Y));
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, snapPt, CurrentColor);
                }
                this.Invalidate();
            }
        }

        private App_Shapes.AnchorPosition DetectAnchor(App_Shapes.ShapeBase shape, PointF pt)
        {
            float threshold = 15f;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Top), pt) < threshold) return App_Shapes.AnchorPosition.Top;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Bottom), pt) < threshold) return App_Shapes.AnchorPosition.Bottom;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Left), pt) < threshold) return App_Shapes.AnchorPosition.Left;
            if (Distance(shape.GetAnchorPoint(App_Shapes.AnchorPosition.Right), pt) < threshold) return App_Shapes.AnchorPosition.Right;
            return App_Shapes.AnchorPosition.Auto;
        }

        private float Distance(PointF p1, PointF p2) => (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

        private void UpdateCameraFromMinimap(Point mouseLoc)
        {
            float minimapScale = MINIMAP_WIDTH / PageSize.Width;
            float targetX = (mouseLoc.X - _minimapRect.X) / minimapScale;
            float targetY = (mouseLoc.Y - _minimapRect.Y) / minimapScale;

            _cameraOffset.X = -(targetX * ZoomFactor - this.Width / 2f);
            _cameraOffset.Y = -(targetY * ZoomFactor - this.Height / 2f);
            
            this.Invalidate();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            _smartGuides.Clear();

            // 觸發游標優化判斷
            UpdateCursor(realPt);

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
            
            if (e.Button == MouseButtons.Left)
            {
                float dx = (e.X - _lastMousePos.X) / ZoomFactor;
                float dy = (e.Y - _lastMousePos.Y) / ZoomFactor;

                if (_currentState == InteractionState.Moving)
                {
                    var movableShapes = SelectedShapes.Where(s => !s.IsLocked).ToList();
                    
                    // 記錄移動前的髒點範圍
                    RectangleF oldBounds = GetShapesAndConnectorsBounds(movableShapes);

                    if (movableShapes.Count == 1)
                    {
                        var me = movableShapes[0];
                        float snapThreshold = 5.0f / ZoomFactor;

                        float bestDx = dx, bestDy = dy;
                        PointF myCenter = me.GetCenter();
                        float futureCenterX = myCenter.X + dx;
                        float futureCenterY = myCenter.Y + dy;

                        foreach (var other in Shapes.Where(s => s != me && !(s is App_Shapes.ConnectorShape)))
                        {
                            PointF otherCenter = other.GetCenter();
                            if (Math.Abs(futureCenterX - otherCenter.X) < snapThreshold)
                            {
                                bestDx = otherCenter.X - myCenter.X;
                                _smartGuides.Add(new Tuple<PointF, PointF>(new PointF(otherCenter.X, -10000), new PointF(otherCenter.X, 10000)));
                            }
                            if (Math.Abs(futureCenterY - otherCenter.Y) < snapThreshold)
                            {
                                bestDy = otherCenter.Y - myCenter.Y;
                                _smartGuides.Add(new Tuple<PointF, PointF>(new PointF(-10000, otherCenter.Y), new PointF(10000, otherCenter.Y)));
                            }
                        }
                        dx = bestDx;
                        dy = bestDy;
                    }

                    _dragTotalDx += dx;
                    _dragTotalDy += dy;
                    foreach (var s in movableShapes) s.Move(dx, dy);

                    // 記錄移動後的髒點範圍
                    RectangleF newBounds = GetShapesAndConnectorsBounds(movableShapes);

                    // 優化：有導引線時全畫面重繪以消除導引線殘留，否則只做局部重繪 (Partial Invalidation)
                    if (_smartGuides.Count > 0)
                    {
                        this.Invalidate();
                    }
                    else
                    {
                        InvalidateWorldRect(oldBounds);
                        InvalidateWorldRect(newBounds);
                        if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect); // 確保小地圖同步更新
                    }
                }
                else if (_currentState == InteractionState.Rotating && SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
                {
                    var me = SelectedShapes[0];
                    RectangleF oldBounds = me.Bounds;
                    PointF center = me.GetCenter();
                    float angle = (float)(Math.Atan2(realPt.Y - center.Y, realPt.X - center.X) * 180 / Math.PI) + 90;
                    me.RotationAngle = Snap(angle, 15f); 
                    
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(me.Bounds);
                }
                else if (_currentState == InteractionState.BoxSelecting)
                {
                    RectangleF oldBox = _boxSelectRect;
                    _boxSelectRect = new RectangleF(
                        Math.Min(_boxSelectRect.X, realPt.X),
                        Math.Min(_boxSelectRect.Y, realPt.Y),
                        Math.Abs(realPt.X - _boxSelectRect.X),
                        Math.Abs(realPt.Y - _boxSelectRect.Y)
                    );
                    
                    bool isMultiSelectKey = (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift);
                    if (!isMultiSelectKey) 
                    {
                        SelectedShapes.ForEach(s => s.IsSelected = false);
                        SelectedShapes.Clear();
                    }

                    var newlySelected = Shapes.Where(s => s.HitTest(new PointF(_boxSelectRect.X + _boxSelectRect.Width/2, _boxSelectRect.Y + _boxSelectRect.Height/2)) || _boxSelectRect.IntersectsWith(s.Bounds)).ToList();
                    
                    foreach(var ns in newlySelected)
                    {
                        if (!SelectedShapes.Contains(ns))
                        {
                            ns.IsSelected = true;
                            SelectedShapes.Add(ns);
                        }
                    }
                    
                    OnSelectionChanged?.Invoke();
                    
                    InvalidateWorldRect(oldBox);
                    InvalidateWorldRect(_boxSelectRect);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
                {
                    var shape = SelectedShapes[0];
                    RectangleF oldBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });

                    if (shape is App_Shapes.ConnectorShape conn)
                    {
                        _hoveredShapeForConnection = null;
                        _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

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

                        for (int i = Shapes.Count - 1; i >= 0; i--)
                        {
                            if (Shapes[i] != conn && Shapes[i].HitTest(realPt))
                            {
                                _hoveredShapeForConnection = Shapes[i];
                                _hoveredAnchor = DetectAnchor(Shapes[i], realPt);

                                if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                                {
                                    conn.SourceId = Shapes[i].Id;
                                    conn.SourceAnchor = _hoveredAnchor;
                                }
                                else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                                {
                                    conn.TargetId = Shapes[i].Id;
                                    conn.TargetAnchor = _hoveredAnchor;
                                }
                                break;
                            }
                        }
                    }
                    else 
                    {
                        PointF center = shape.GetCenter();
                        PointF lastLocal = App_Shapes.ShapeBase.RotatePoint(GetRealPoint(_lastMousePos), center, -shape.RotationAngle);
                        PointF currentLocal = App_Shapes.ShapeBase.RotatePoint(realPt, center, -shape.RotationAngle);
                        
                        float ldx = currentLocal.X - lastLocal.X;
                        float ldy = currentLocal.Y - lastLocal.Y;

                        RectangleF b = shape.Bounds;
                        switch (_resizingHandle)
                        {
                            case App_Shapes.HandlePosition.NW: b = new RectangleF(b.X + ldx, b.Y + ldy, b.Width - ldx, b.Height - ldy); break;
                            case App_Shapes.HandlePosition.N:  b = new RectangleF(b.X, b.Y + ldy, b.Width, b.Height - ldy); break;
                            case App_Shapes.HandlePosition.NE: b = new RectangleF(b.X, b.Y + ldy, b.Width + ldx, b.Height - ldy); break;
                            case App_Shapes.HandlePosition.E:  b = new RectangleF(b.X, b.Y, b.Width + ldx, b.Height); break;
                            case App_Shapes.HandlePosition.SE: b = new RectangleF(b.X, b.Y, b.Width + ldx, b.Height + ldy); break;
                            case App_Shapes.HandlePosition.S:  b = new RectangleF(b.X, b.Y, b.Width, b.Height + ldy); break;
                            case App_Shapes.HandlePosition.SW: b = new RectangleF(b.X + ldx, b.Y, b.Width - ldx, b.Height + ldy); break;
                            case App_Shapes.HandlePosition.W:  b = new RectangleF(b.X + ldx, b.Y, b.Width - ldx, b.Height); break;
                        }
                        if (b.Width > 5 && b.Height > 5) shape.SetBounds(b);
                    }

                    RectangleF newBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(newBounds);
                    if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect);
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    RectangleF oldBounds = _tempShape.Bounds;
                    if (_tempShape is App_Shapes.FreehandShape fh)
                    {
                        fh.AddPoint(realPt);
                    }
                    else
                    {
                        _tempShape.UpdateEndPoint(new PointF(Snap(realPt.X), Snap(realPt.Y)));
                    }
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(_tempShape.Bounds);
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
                    RectangleF oldBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { c });
                    c.UpdateEndPoint(realPt);
                    _hoveredShapeForConnection = null;
                    _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].Id != c.SourceId && Shapes[i].HitTest(realPt))
                        {
                            _hoveredShapeForConnection = Shapes[i]; 
                            _hoveredAnchor = DetectAnchor(Shapes[i], realPt);
                            break;
                        }
                    }
                    RectangleF newBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { c });
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(newBounds);
                }
                
                _lastMousePos = new Point((int)(_lastMousePos.X + dx * ZoomFactor), (int)(_lastMousePos.Y + dy * ZoomFactor));
                // 如果沒有進入任何特定狀態，我們仍然可能需要全面刷新，但上面的特定邏輯已經精準覆蓋
            }
        }

        private float Snap(float angle, float step)
        {
            if (Control.ModifierKeys == Keys.Alt) return angle; 
            return (float)Math.Round(angle / step) * step;
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDraggingMinimap)
            {
                _isDraggingMinimap = false;
                return;
            }

            if (_isPanning)
            {
                _isPanning = false;
                this.Cursor = (CurrentTool == App_Shapes.ShapeType.HandPan) ? Cursors.Hand : Cursors.Default;
            }

            if (_smartGuides.Count > 0)
            {
                _smartGuides.Clear();
                this.Invalidate(); 
            }

            if (e.Button == MouseButtons.Left)
            {
                var movableShapes = SelectedShapes.Where(s => !s.IsLocked).ToList();

                if (_currentState == InteractionState.Moving && movableShapes.Count > 0 && (_dragTotalDx != 0 || _dragTotalDy != 0))
                {
                    foreach (var s in movableShapes) s.Move(-_dragTotalDx, -_dragTotalDy);
                    CmdManager.ExecuteCommand(new MoveShapesCommand(movableShapes, _dragTotalDx, _dragTotalDy));
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
                {
                    if (SelectedShapes[0] is App_Shapes.ConnectorShape conn)
                    {
                        CmdManager.ExecuteCommand(new AdjustConnectorCommand(
                            conn, 
                            _oldSrcId, _oldTgtId, _oldSA, _oldTA, _oldStart, _oldEnd,
                            conn.SourceId, conn.TargetId, conn.SourceAnchor, conn.TargetAnchor, conn.StartPt, conn.EndPt));
                    }
                    else
                    {
                        CmdManager.ExecuteCommand(new ResizeShapeCommand(SelectedShapes[0], _initialBounds, SelectedShapes[0].Bounds));
                    }
                }
                else if (_currentState == InteractionState.Rotating && SelectedShapes.Count == 1 && !SelectedShapes[0].IsLocked)
                {
                    CmdManager.ExecuteCommand(new RotateShapeCommand(SelectedShapes[0], _initialAngle, SelectedShapes[0].RotationAngle));
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5 || _tempShape is App_Shapes.FreehandShape)
                    {
                        CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, _tempShape));
                    }
                    if (_tempShape is App_Shapes.TextNodeShape)
                    {
                        StartInlineEditing(_tempShape);
                        OnToolResetRequested?.Invoke();
                    }
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
                    if (_hoveredShapeForConnection != null)
                    {
                        c.TargetId = _hoveredShapeForConnection.Id;
                        c.TargetAnchor = _hoveredAnchor;
                    }
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, c));
                    OnToolResetRequested?.Invoke();
                }

                _tempShape = null;
                _hoveredShapeForConnection = null;
                _hoveredAnchor = App_Shapes.AnchorPosition.Auto;
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
            
            var oldTransform = g.Transform;

            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

            // 優化：取得目前實際需要重繪的裁切範圍，只畫範圍內的物件
            RectangleF clipWorldBounds = new RectangleF(
                (e.ClipRectangle.X - _cameraOffset.X) / ZoomFactor,
                (e.ClipRectangle.Y - _cameraOffset.Y) / ZoomFactor,
                e.ClipRectangle.Width / ZoomFactor,
                e.ClipRectangle.Height / ZoomFactor
            );

            RectangleF viewRect = new RectangleF(
                -_cameraOffset.X / ZoomFactor, 
                -_cameraOffset.Y / ZoomFactor, 
                this.Width / ZoomFactor, 
                this.Height / ZoomFactor);

            if (SnapToGrid) DrawGrid(g, viewRect);

            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, PageSize.Width, PageSize.Height);

            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape)))
            {
                // 優化：只重繪在裁切範圍內的物件
                if (clipWorldBounds.IntersectsWith(shape.Bounds)) shape.DrawWithTransform(g);
            }

            List<LineSegment> drawnLines = new List<LineSegment>();
            foreach (var shape in Shapes.OfType<App_Shapes.ConnectorShape>())
            {
                var src = Shapes.FirstOrDefault(x => x.Id == shape.SourceId);
                var tgt = Shapes.FirstOrDefault(x => x.Id == shape.TargetId);
                
                PointF p1 = shape.StartPt;
                PointF p2 = shape.EndPt;

                if (src != null)
                    p1 = shape.SourceAnchor == App_Shapes.AnchorPosition.Auto ? src.GetIntersection(tgt != null ? tgt.GetCenter() : shape.EndPt) : src.GetAnchorPoint(shape.SourceAnchor);
                
                if (tgt != null)
                    p2 = shape.TargetAnchor == App_Shapes.AnchorPosition.Auto ? tgt.GetIntersection(p1) : tgt.GetAnchorPoint(shape.TargetAnchor);

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

            _tempShape?.DrawWithTransform(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt);
            
            if ((_currentState == InteractionState.Connecting || _currentState == InteractionState.Resizing) && _hoveredShapeForConnection != null)
            {
                PointF anchorPt = _hoveredAnchor == App_Shapes.AnchorPosition.Auto 
                    ? _hoveredShapeForConnection.GetIntersection(GetRealPoint(_lastMousePos)) 
                    : _hoveredShapeForConnection.GetAnchorPoint(_hoveredAnchor);
                
                g.FillEllipse(Brushes.LightCoral, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
                g.DrawEllipse(Pens.Red, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
            }

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

            using (Pen guidePen = new Pen(Color.DeepPink, 1.5f) { DashStyle = DashStyle.Dash })
            {
                foreach (var line in _smartGuides)
                {
                    g.DrawLine(guidePen, line.Item1, line.Item2);
                }
            }

            g.Transform = oldTransform; 

            float minimapScale = MINIMAP_WIDTH / PageSize.Width;
            int minimapHeight = (int)(PageSize.Height * minimapScale);
            _minimapRect = new Rectangle(this.Width - MINIMAP_WIDTH - 20, this.Height - minimapHeight - 20, MINIMAP_WIDTH, minimapHeight);

            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 245, 245, 245)))
                g.FillRectangle(bgBrush, _minimapRect);
            g.DrawRectangle(Pens.Gray, _minimapRect);

            foreach (var shape in Shapes.Where(s => !(s is App_Shapes.ConnectorShape)))
            {
                float sx = _minimapRect.X + shape.Bounds.X * minimapScale;
                float sy = _minimapRect.Y + shape.Bounds.Y * minimapScale;
                float sw = shape.Bounds.Width * minimapScale;
                float sh = shape.Bounds.Height * minimapScale;
                
                Color renderColor = (shape.FillColor != Color.Transparent) ? shape.FillColor : shape.ShapeColor;
                using (Brush b = new SolidBrush(renderColor))
                    g.FillRectangle(b, sx, sy, sw, sh);
            }

            float vx = _minimapRect.X + (-_cameraOffset.X / ZoomFactor) * minimapScale;
            float vy = _minimapRect.Y + (-_cameraOffset.Y / ZoomFactor) * minimapScale;
            float vw = (this.Width / ZoomFactor) * minimapScale;
            float vh = (this.Height / ZoomFactor) * minimapScale;
            
            vx = Math.Max(_minimapRect.Left, Math.Min(vx, _minimapRect.Right - vw));
            vy = Math.Max(_minimapRect.Top, Math.Min(vy, _minimapRect.Bottom - vh));
            
            using (Pen vp = new Pen(Color.Red, 2f))
                g.DrawRectangle(vp, vx, vy, vw, vh);
        }

        private void DrawGrid(Graphics g, RectangleF viewRect)
        {
            int startX = (int)(Math.Floor(viewRect.Left / GridSize) * GridSize);
            int startY = (int)(Math.Floor(viewRect.Top / GridSize) * GridSize);

            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235)))
            {
                for (float x = startX; x < viewRect.Right; x += GridSize) g.DrawLine(gridPen, x, viewRect.Top, x, viewRect.Bottom);
                for (float y = startY; y < viewRect.Bottom; y += GridSize) g.DrawLine(gridPen, viewRect.Left, y, viewRect.Right, y);
            }
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
                foreach (var s in Shapes) s.DrawWithTransform(g);
            }
            return bmp;
        }
    }
}
