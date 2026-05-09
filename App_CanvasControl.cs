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
        private bool _isPanning = false;
        private Point _lastMousePos;
        private Point _currentMouseScreenPos; // 記錄滑鼠當前位置，供尺規游標使用
        
        private float _dragTotalDx = 0;
        private float _dragTotalDy = 0;

        private RectangleF _initialBounds;
        private float _initialAngle;

        private Guid _oldSrcId, _oldTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA;
        private PointF _oldStart, _oldEnd;

        // --- 新增：BezierPenDrawing 狀態，用來處理鋼筆工具連續點擊 ---
        private enum InteractionState { Idle, Drawing, Moving, Resizing, Rotating, Connecting, BoxSelecting, BezierPenDrawing }
        private InteractionState _currentState = InteractionState.Idle;

        private App_Shapes.ShapeType _currentTool = App_Shapes.ShapeType.Pointer;
        private App_Shapes.ShapeType _previousTool = App_Shapes.ShapeType.Pointer;

        public App_Shapes.ShapeType CurrentTool 
        { 
            get => _currentTool; 
            set 
            { 
                if (_currentTool != App_Shapes.ShapeType.HandPan) _previousTool = _currentTool;
                _currentTool = value;
                if (_currentTool == App_Shapes.ShapeType.HandPan)
                    this.Cursor = Cursors.Hand;
                else if (_currentTool == App_Shapes.ShapeType.FormatPainter)
                    this.Cursor = Cursors.UpArrow; 
                else if (_currentTool == App_Shapes.ShapeType.BezierPen)
                    this.Cursor = Cursors.Cross;
                else
                    this.Cursor = Cursors.Default;

                // 如果切換工具時，鋼筆還沒畫完，就強制結束
                if (_currentState == InteractionState.BezierPenDrawing && _currentTool != App_Shapes.ShapeType.BezierPen)
                {
                    FinishBezierDrawing();
                }
            } 
        }

        public Color CurrentColor { get; set; } = Color.Black;
        
        public bool SnapToGrid { get; set; } = true;
        public float GridSize { get; set; } = 20f;
        
        // --- 新增：尺規顯示開關 ---
        public bool ShowRulers { get; set; } = true;
        private const int RULER_SIZE = 25;

        private App_Shapes.ShapeBase _tempShape = null;
        private RectangleF _boxSelectRect;
        
        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private List<App_Shapes.ShapeBase> _clipboard = new List<App_Shapes.ShapeBase>();
        
        public App_Shapes.ShapeBase FormatSourceShape { get; set; }

        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;
        private App_Shapes.AnchorPosition _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

        private TextBox _inlineTextBox;
        private App_Shapes.ShapeBase _editingShape = null;

        private List<Tuple<PointF, PointF>> _smartGuides = new List<Tuple<PointF, PointF>>();

        private Rectangle _minimapRect;
        private bool _isDraggingMinimap = false;
        private const int MINIMAP_WIDTH = 200;

        private App_Shapes.QuadTree _quadTree;
        private bool _isQuadTreeDirty = true;

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
                    
                    for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
                    SelectedShapes.Clear();
                    newShape.IsSelected = true;
                    SelectedShapes.Add(newShape);
                    OnSelectionChanged?.Invoke();
                    
                    _isQuadTreeDirty = true;
                    this.Invalidate();
                }
            }
        }

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

        private void ToggleLock()
        {
            if (SelectedShapes.Count == 0) return;
            bool isAllLocked = SelectedShapes.All(s => s.IsLocked);
            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsLocked = !isAllLocked;
            this.Invalidate();
        }

        public void ChangeZIndex(int direction)
        {
            if (SelectedShapes.Count == 0) return;
            CmdManager.ExecuteCommand(new ChangeZIndexCommand(Shapes, SelectedShapes, direction));
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
                else if (child.HitTest(pt))
                {
                    return child;
                }
            }
            return null;
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
                    {
                        targetShape = hitChild;
                    }
                }

                if (!(targetShape is App_Shapes.ConnectorShape) && !(targetShape is App_Shapes.GroupShape))
                {
                    StartInlineEditing(targetShape);
                }
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

        private float Snap(float angle, float step)
        {
            if (Control.ModifierKeys == Keys.Alt) return angle; 
            return (float)Math.Round(angle / step) * step;
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
                this.Cursor = Cursors.Default;
                CurrentTool = _previousTool; 
            }
            base.OnKeyUp(e);
        }

        private void FinishBezierDrawing()
        {
            if (_currentState == InteractionState.BezierPenDrawing && _tempShape is App_Shapes.BezierShape bezier)
            {
                bezier.NormalizeBounds();
                if (bezier.LocalNodes.Count > 1)
                {
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, bezier));
                }
                _tempShape = null;
                _currentState = InteractionState.Idle;
                this.Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_inlineTextBox.Focused) return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == Keys.Escape || keyData == Keys.Enter) 
            { 
                if (_currentState == InteractionState.BezierPenDrawing)
                {
                    FinishBezierDrawing();
                    return true;
                }

                _tempShape?.Dispose();
                _tempShape = null;
                _hoveredShapeForConnection = null;
                _hoveredAnchor = App_Shapes.AnchorPosition.Auto;
                _currentState = InteractionState.Idle;
                this.Invalidate();
                
                OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Pointer);
                return true; 
            }

            if (keyData == Keys.V) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Pointer); return true; }
            if (keyData == Keys.H) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.HandPan); return true; }
            if (keyData == Keys.T) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.TextNode); return true; }
            if (keyData == Keys.P) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Freehand); return true; }
            if (keyData == Keys.B) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.BezierPen); return true; }
            if (keyData == Keys.L) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.OrthogonalLine); return true; }
            if (keyData == Keys.R) { OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Rectangle); return true; }

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
            if (keyData == (Keys.Control | Keys.C)) { Copy(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { Paste(); return true; }
            if (keyData == (Keys.Control | Keys.D)) { DuplicateSelected(); return true; }

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
                            
                            for (int i = 0; i < SelectedShapes.Count; i++)
                            {
                                var s = SelectedShapes[i];
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
                for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
                SelectedShapes.Clear();
                
                var newClones = App_SaveLoad.CloneShapes(_clipboard);
                foreach (var s in newClones)
                {
                    s.Id = Guid.NewGuid();
                    s.IsLocked = false; 
                    s.Move(20, 20);
                    s.IsSelected = true;
                    SelectedShapes.Add(s);
                }
                CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, newClones));
                OnSelectionChanged?.Invoke();
                return;
            }

            if (Clipboard.ContainsImage())
            {
                Image img = Clipboard.GetImage();
                if (img != null)
                {
                    PointF pt = GetRealPoint(_lastMousePos == Point.Empty ? new Point(50, 50) : _lastMousePos);
                    var newImgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, new Bitmap(img));
                    
                    for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
                    SelectedShapes.Clear();
                    
                    newImgShape.IsSelected = true;
                    CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newImgShape));
                    SelectedShapes.Add(newImgShape);
                    
                    OnSelectionChanged?.Invoke();
                }
            }
        }

        private void DuplicateSelected()
        {
            if (SelectedShapes.Count == 0) return;
            
            var clones = App_SaveLoad.CloneShapes(SelectedShapes);
            foreach (var c in clones)
            {
                c.Id = Guid.NewGuid();
                c.IsLocked = false;
                c.Move(10, 10);
            }

            CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, clones));

            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
            SelectedShapes.Clear();

            foreach (var c in clones)
            {
                c.IsSelected = true;
                SelectedShapes.Add(c);
            }

            OnSelectionChanged?.Invoke();
        }

        private void InvalidateWorldRect(RectangleF worldRect)
        {
            if (worldRect == RectangleF.Empty) return;
            float x = worldRect.X * ZoomFactor + _cameraOffset.X;
            float y = worldRect.Y * ZoomFactor + _cameraOffset.Y;
            float w = worldRect.Width * ZoomFactor;
            float h = worldRect.Height * ZoomFactor;
            
            Rectangle screenRect = new Rectangle((int)x - 50, (int)y - 50, (int)w + 100, (int)h + 100);
            this.Invalidate(screenRect);
        }

        private RectangleF GetShapesAndConnectorsBounds(List<App_Shapes.ShapeBase> shapes)
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

        private void UpdateCursor(PointF realPt)
        {
            if (_currentState != InteractionState.Idle) return; 

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

            EnsureQuadTree();
            if (_quadTree != null)
            {
                List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                _quadTree.Retrieve(nearShapes, new RectangleF(realPt.X - 5, realPt.Y - 5, 10, 10));
                nearShapes = nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();

                foreach (var shape in nearShapes)
                {
                    if (shape.HitTest(realPt))
                    {
                        this.Cursor = shape.IsLocked ? Cursors.No : Cursors.SizeAll;
                        return;
                    }
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

            // 忽略點擊尺規區域
            if (ShowRulers && (e.X <= RULER_SIZE || e.Y <= RULER_SIZE)) return;

            if (e.Button == MouseButtons.Left && _minimapRect.Contains(e.Location))
            {
                _isDraggingMinimap = true;
                UpdateCameraFromMinimap(e.Location);
                return;
            }

            if (e.Button == MouseButtons.Middle || _isPanning ||
               (e.Button == MouseButtons.Left && CurrentTool == App_Shapes.ShapeType.HandPan))
            {
                _isPanning = true;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                EnsureQuadTree();
                List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                if (_quadTree != null)
                {
                    _quadTree.Retrieve(nearShapes, new RectangleF(realPt.X - 5, realPt.Y - 5, 10, 10));
                    nearShapes = nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();
                }

                if (CurrentTool == App_Shapes.ShapeType.FormatPainter)
                {
                    if (FormatSourceShape != null)
                    {
                        App_Shapes.ShapeBase hit = null;
                        foreach (var shape in nearShapes)
                        {
                            if (shape.HitTest(realPt)) { hit = shape; break; }
                        }

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
                    foreach (var shape in nearShapes)
                    {
                        if (shape.HitTest(realPt)) { hit = shape; break; }
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
                                for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
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
                        for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
                        SelectedShapes.Clear();
                        OnSelectionChanged?.Invoke();
                        
                        _currentState = InteractionState.BoxSelecting;
                        _boxSelectRect = new RectangleF(realPt.X, realPt.Y, 0, 0);
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.Image)
                {
                    OnImageInsertRequested?.Invoke(realPt);
                    OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Pointer);
                }
                else if (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.StraightLine || CurrentTool == App_Shapes.ShapeType.OrthogonalLine)
                {
                    _currentState = InteractionState.Connecting;
                    bool isArrow = (CurrentTool == App_Shapes.ShapeType.ArrowLine || CurrentTool == App_Shapes.ShapeType.OrthogonalLine);
                    bool isOrtho = (CurrentTool == App_Shapes.ShapeType.OrthogonalLine);
                    
                    _tempShape = new App_Shapes.ConnectorShape(realPt, CurrentColor, isArrow, isOrtho);
                    
                    foreach (var shape in nearShapes)
                    {
                        if (shape.HitTest(realPt)) 
                        { 
                            var conn = (App_Shapes.ConnectorShape)_tempShape;
                            conn.SourceId = shape.Id; 
                            conn.SourceAnchor = DetectAnchor(shape, realPt);
                            break; 
                        }
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.BezierPen)
                {
                    if (_currentState != InteractionState.BezierPenDrawing)
                    {
                        _currentState = InteractionState.BezierPenDrawing;
                        _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, realPt, CurrentColor);
                        ((App_Shapes.BezierShape)_tempShape).FillColor = Color.Transparent;
                    }
                    else
                    {
                        var bezier = (App_Shapes.BezierShape)_tempShape;
                        bezier.AddNode(realPt);
                    }
                }
                else if (CurrentTool != App_Shapes.ShapeType.HandPan && CurrentTool != App_Shapes.ShapeType.FormatPainter)
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
            float minimapScale = MINIMAP_WIDTH / ActualPageSize.Width;
            float targetX = (mouseLoc.X - _minimapRect.X) / minimapScale;
            float targetY = (mouseLoc.Y - _minimapRect.Y) / minimapScale;

            _cameraOffset.X = -(targetX * ZoomFactor - this.Width / 2f);
            _cameraOffset.Y = -(targetY * ZoomFactor - this.Height / 2f);
            
            this.Invalidate();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            _currentMouseScreenPos = e.Location;
            _smartGuides.Clear();

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
                    
                    RectangleF oldBounds = GetShapesAndConnectorsBounds(movableShapes);

                    if (movableShapes.Count == 1)
                    {
                        var me = movableShapes[0];
                        float snapThreshold = 5.0f / ZoomFactor;

                        float bestDx = dx, bestDy = dy;
                        PointF myCenter = me.GetCenter();
                        float futureCenterX = myCenter.X + dx;
                        float futureCenterY = myCenter.Y + dy;

                        EnsureQuadTree();
                        List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                        if (_quadTree != null)
                        {
                            _quadTree.Retrieve(nearShapes, new RectangleF(futureCenterX - 200, futureCenterY - 200, 400, 400));
                        }

                        foreach (var other in nearShapes)
                        {
                            if (other == me || other is App_Shapes.ConnectorShape) continue;

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
                    for (int i = 0; i < movableShapes.Count; i++) movableShapes[i].Move(dx, dy);

                    RectangleF newBounds = GetShapesAndConnectorsBounds(movableShapes);

                    if (_smartGuides.Count > 0)
                    {
                        this.Invalidate();
                    }
                    else
                    {
                        InvalidateWorldRect(oldBounds);
                        InvalidateWorldRect(newBounds);
                        if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect); 
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
                        for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
                        SelectedShapes.Clear();
                    }

                    EnsureQuadTree();
                    List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                    if (_quadTree != null) _quadTree.Retrieve(nearShapes, _boxSelectRect);

                    foreach (var s in nearShapes)
                    {
                        if (s.HitTest(new PointF(_boxSelectRect.X + _boxSelectRect.Width/2, _boxSelectRect.Y + _boxSelectRect.Height/2)) || _boxSelectRect.IntersectsWith(s.Bounds))
                        {
                            if (!SelectedShapes.Contains(s))
                            {
                                s.IsSelected = true;
                                SelectedShapes.Add(s);
                            }
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

                        EnsureQuadTree();
                        List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                        if (_quadTree != null)
                        {
                            _quadTree.Retrieve(nearShapes, new RectangleF(realPt.X - 5, realPt.Y - 5, 10, 10));
                            nearShapes = nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();
                        }

                        foreach (var other in nearShapes)
                        {
                            if (other != conn && other.HitTest(realPt))
                            {
                                _hoveredShapeForConnection = other;
                                _hoveredAnchor = DetectAnchor(other, realPt);

                                if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                                {
                                    conn.SourceId = other.Id;
                                    conn.SourceAnchor = _hoveredAnchor;
                                }
                                else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                                {
                                    conn.TargetId = other.Id;
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
                        
                        bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
                        float ratio = 1.0f;
                        if (keepRatio && _initialBounds.Height != 0) 
                            ratio = _initialBounds.Width / _initialBounds.Height;

                        switch (_resizingHandle)
                        {
                            case App_Shapes.HandlePosition.NW: 
                                float newW_NW = b.Width - ldx;
                                float newH_NW = b.Height - ldy;
                                if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NW = newW_NW / ratio; else newW_NW = newH_NW * ratio; }
                                b = new RectangleF(b.Right - newW_NW, b.Bottom - newH_NW, newW_NW, newH_NW);
                                break;
                            case App_Shapes.HandlePosition.N:  
                                b = new RectangleF(b.X, b.Y + ldy, b.Width, b.Height - ldy); 
                                if (keepRatio) { float oldC = b.X + b.Width/2; b.Width = b.Height * ratio; b.X = oldC - b.Width/2; }
                                break;
                            case App_Shapes.HandlePosition.NE: 
                                float newW_NE = b.Width + ldx;
                                float newH_NE = b.Height - ldy;
                                if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_NE = newW_NE / ratio; else newW_NE = newH_NE * ratio; }
                                b = new RectangleF(b.X, b.Bottom - newH_NE, newW_NE, newH_NE);
                                break;
                            case App_Shapes.HandlePosition.E:  
                                b = new RectangleF(b.X, b.Y, b.Width + ldx, b.Height); 
                                if (keepRatio) { float oldC = b.Y + b.Height/2; b.Height = b.Width / ratio; b.Y = oldC - b.Height/2; }
                                break;
                            case App_Shapes.HandlePosition.SE: 
                                float newW_SE = b.Width + ldx;
                                float newH_SE = b.Height + ldy;
                                if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SE = newW_SE / ratio; else newW_SE = newH_SE * ratio; }
                                b = new RectangleF(b.X, b.Y, newW_SE, newH_SE);
                                break;
                            case App_Shapes.HandlePosition.S:  
                                b = new RectangleF(b.X, b.Y, b.Width, b.Height + ldy); 
                                if (keepRatio) { float oldC = b.X + b.Width/2; b.Width = b.Height * ratio; b.X = oldC - b.Width/2; }
                                break;
                            case App_Shapes.HandlePosition.SW: 
                                float newW_SW = b.Width - ldx;
                                float newH_SW = b.Height + ldy;
                                if (keepRatio) { if (Math.Abs(ldx) > Math.Abs(ldy)) newH_SW = newW_SW / ratio; else newW_SW = newH_SW * ratio; }
                                b = new RectangleF(b.Right - newW_SW, b.Y, newW_SW, newH_SW);
                                break;
                            case App_Shapes.HandlePosition.W:  
                                b = new RectangleF(b.X + ldx, b.Y, b.Width - ldx, b.Height); 
                                if (keepRatio) { float oldC = b.Y + b.Height/2; b.Height = b.Width / ratio; b.Y = oldC - b.Height/2; }
                                break;
                        }

                        if (b.Width > 5 && b.Height > 5) shape.SetBounds(b);
                    }

                    RectangleF newBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { shape });
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(newBounds);
                    if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect);
                }
                else if (_currentState == InteractionState.BezierPenDrawing && _tempShape is App_Shapes.BezierShape bezier)
                {
                    // 滑鼠拖曳時，更新控制桿
                    bezier.UpdateLastControlPoint(realPt);
                    this.Invalidate();
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
                        bool keepRatio = Control.ModifierKeys.HasFlag(Keys.Shift);
                        float snapX = Snap(realPt.X);
                        float snapY = Snap(realPt.Y);
                        
                        if (keepRatio)
                        {
                            float diffX = snapX - _tempShape.Bounds.X;
                            float diffY = snapY - _tempShape.Bounds.Y;
                            float maxDim = (float)Math.Max(Math.Abs(diffX), Math.Abs(diffY));
                            snapX = _tempShape.Bounds.X + maxDim * Math.Sign(diffX);
                            snapY = _tempShape.Bounds.Y + maxDim * Math.Sign(diffY);
                        }
                        
                        _tempShape.UpdateEndPoint(new PointF(snapX, snapY));
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

                    EnsureQuadTree();
                    List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
                    if (_quadTree != null)
                    {
                        _quadTree.Retrieve(nearShapes, new RectangleF(realPt.X - 5, realPt.Y - 5, 10, 10));
                        nearShapes = nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();
                    }

                    foreach (var other in nearShapes)
                    {
                        if (other.Id != c.SourceId && other.HitTest(realPt))
                        {
                            _hoveredShapeForConnection = other; 
                            _hoveredAnchor = DetectAnchor(other, realPt);
                            break;
                        }
                    }
                    RectangleF newBounds = GetShapesAndConnectorsBounds(new List<App_Shapes.ShapeBase> { c });
                    InvalidateWorldRect(oldBounds);
                    InvalidateWorldRect(newBounds);
                }
                
                _lastMousePos = new Point((int)(_lastMousePos.X + dx * ZoomFactor), (int)(_lastMousePos.Y + dy * ZoomFactor));
            }
            else
            {
                // 如果是貝茲鋼筆，且滑鼠沒有按下 (懸停狀態)，要畫出一條虛擬線到滑鼠位置
                if (_currentState == InteractionState.BezierPenDrawing && _tempShape is App_Shapes.BezierShape)
                {
                    this.Invalidate();
                }

                // 滑鼠純移動，更新尺規游標
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
                if (CurrentTool == App_Shapes.ShapeType.HandPan)
                    this.Cursor = Cursors.Hand;
                else if (CurrentTool == App_Shapes.ShapeType.FormatPainter)
                    this.Cursor = Cursors.UpArrow;
                else if (CurrentTool == App_Shapes.ShapeType.BezierPen)
                    this.Cursor = Cursors.Cross;
                else
                    this.Cursor = Cursors.Default;
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
                    for (int i = 0; i < movableShapes.Count; i++) movableShapes[i].Move(-_dragTotalDx, -_dragTotalDy);
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
                else if (_currentState == InteractionState.BezierPenDrawing)
                {
                    // 鋼筆工具放開滑鼠時，不結束，讓使用者繼續點下一點
                    return; 
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
                        OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Pointer);
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
                    OnToolChangedRequested?.Invoke(App_Shapes.ShapeType.Pointer);
                }

                _tempShape = null;
                _hoveredShapeForConnection = null;
                _hoveredAnchor = App_Shapes.AnchorPosition.Auto;
                _currentState = InteractionState.Idle;
                this.Invalidate();
            }
        }

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

            if (_currentState != InteractionState.Idle || _currentState == InteractionState.BezierPenDrawing)
            {
                foreach (var s in SelectedShapes) 
                    if (!visibleShapes.Contains(s)) visibleShapes.Add(s);
                if (_tempShape != null && !visibleShapes.Contains(_tempShape)) 
                    visibleShapes.Add(_tempShape);
            }

            var sortedVisibleShapes = visibleShapes.Distinct().OrderBy(s => Shapes.IndexOf(s)).ToList();

            for (int i = 0; i < sortedVisibleShapes.Count; i++)
            {
                var shape = sortedVisibleShapes[i];
                if (!(shape is App_Shapes.ConnectorShape))
                {
                    shape.DrawWithTransform(g);
                }
            }

            bool isFastMode = (_currentState == InteractionState.Moving || _currentState == InteractionState.Resizing);

            for (int i = 0; i < Shapes.Count; i++)
            {
                if (Shapes[i] is App_Shapes.ConnectorShape shape)
                {
                    App_Shapes.ShapeBase src = null;
                    App_Shapes.ShapeBase tgt = null;

                    for (int j = 0; j < Shapes.Count; j++)
                    {
                        if (Shapes[j].Id == shape.SourceId) src = Shapes[j];
                        if (Shapes[j].Id == shape.TargetId) tgt = Shapes[j];
                        if (src != null && tgt != null) break;
                    }

                    PointF p1 = shape.StartPt;
                    PointF p2 = shape.EndPt;

                    if (src != null)
                        p1 = shape.SourceAnchor == App_Shapes.AnchorPosition.Auto ? src.GetIntersection(tgt != null ? tgt.GetCenter() : shape.EndPt) : src.GetAnchorPoint(shape.SourceAnchor);
                    
                    if (tgt != null)
                        p2 = shape.TargetAnchor == App_Shapes.AnchorPosition.Auto ? tgt.GetIntersection(p1) : tgt.GetAnchorPoint(shape.TargetAnchor);

                    shape.DrawDynamic(g, p1, p2, Shapes, isFastMode, _quadTree);
                }
            }

            _tempShape?.DrawWithTransform(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt, Shapes, true, _quadTree); 
            
            // 繪製鋼筆工具尚未決定的下一條線 (懸停導引線)
            if (_currentState == InteractionState.BezierPenDrawing && _tempShape is App_Shapes.BezierShape bezier && bezier.LocalNodes.Count > 0)
            {
                PointF lastPt = bezier.LocalNodes.Last().Anchor;
                PointF realPt = GetRealPoint(_currentMouseScreenPos);
                using (Pen guidePen = new Pen(Color.CornflowerBlue, 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(guidePen, bezier.Bounds.X + lastPt.X, bezier.Bounds.Y + lastPt.Y, realPt.X, realPt.Y);
                }
            }
            
            if ((_currentState == InteractionState.Connecting || _currentState == InteractionState.Resizing) && _hoveredShapeForConnection != null)
            {
                PointF anchorPt = _hoveredAnchor == App_Shapes.AnchorPosition.Auto 
                    ? _hoveredShapeForConnection.GetIntersection(GetRealPoint(_lastMousePos)) 
                    : _hoveredShapeForConnection.GetAnchorPoint(_hoveredAnchor);
                
                g.FillEllipse(Brushes.LightCoral, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
                g.DrawEllipse(Pens.Red, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
            }

            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].DrawSelection(g);
            
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
                for (int i = 0; i < _smartGuides.Count; i++)
                {
                    g.DrawLine(guidePen, _smartGuides[i].Item1, _smartGuides[i].Item2);
                }
            }

            g.Transform = oldTransform; 

            // --- 繪製小地圖 ---
            float minimapScale = MINIMAP_WIDTH / currentCanvasSize.Width;
            int minimapHeight = (int)(currentCanvasSize.Height * minimapScale);
            _minimapRect = new Rectangle(this.Width - MINIMAP_WIDTH - 20, this.Height - minimapHeight - 20, MINIMAP_WIDTH, minimapHeight);

            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 245, 245, 245)))
                g.FillRectangle(bgBrush, _minimapRect);
            g.DrawRectangle(Pens.Gray, _minimapRect);

            for (int i = 0; i < Shapes.Count; i++)
            {
                var shape = Shapes[i];
                if (!(shape is App_Shapes.ConnectorShape))
                {
                    float sx = _minimapRect.X + shape.Bounds.X * minimapScale;
                    float sy = _minimapRect.Y + shape.Bounds.Y * minimapScale;
                    float sw = shape.Bounds.Width * minimapScale;
                    float sh = shape.Bounds.Height * minimapScale;
                    
                    Color renderColor = (shape.FillColor != Color.Transparent) ? shape.FillColor : shape.ShapeColor;
                    using (Brush b = new SolidBrush(renderColor))
                        g.FillRectangle(b, sx, sy, sw, sh);
                }
            }

            float vx = _minimapRect.X + (-_cameraOffset.X / ZoomFactor) * minimapScale;
            float vy = _minimapRect.Y + (-_cameraOffset.Y / ZoomFactor) * minimapScale;
            float vw = (this.Width / ZoomFactor) * minimapScale;
            float vh = (this.Height / ZoomFactor) * minimapScale;
            
            vx = Math.Max(_minimapRect.Left, Math.Min(vx, _minimapRect.Right - vw));
            vy = Math.Max(_minimapRect.Top, Math.Min(vy, _minimapRect.Bottom - vh));
            
            using (Pen vp = new Pen(Color.Red, 2f))
                g.DrawRectangle(vp, vx, vy, vw, vh);

            // --- 新增：繪製尺規 ---
            if (ShowRulers)
            {
                DrawRulers(e.Graphics);
            }
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

        // --- 新增：繪製畫布尺規 ---
        private void DrawRulers(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None; // 尺規不需要反鋸齒
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Brush rulerBg = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (Pen rulerPen = new Pen(Color.FromArgb(180, 180, 180)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
            using (Font rulerFont = new Font("Arial", 7))
            {
                // 頂部尺規 (Top Ruler)
                g.FillRectangle(rulerBg, 0, 0, this.Width, RULER_SIZE);
                g.DrawLine(rulerPen, 0, RULER_SIZE, this.Width, RULER_SIZE);

                // 左側尺規 (Left Ruler)
                g.FillRectangle(rulerBg, 0, 0, RULER_SIZE, this.Height);
                g.DrawLine(rulerPen, RULER_SIZE, 0, RULER_SIZE, this.Height);

                // 左上角交界處方塊
                g.FillRectangle(Brushes.White, 0, 0, RULER_SIZE, RULER_SIZE);
                g.DrawRectangle(rulerPen, 0, 0, RULER_SIZE, RULER_SIZE);

                float step = 100 * ZoomFactor; // 每 100 像素一個大刻度
                float subStep = step / 10;     // 每 10 像素一個小刻度

                // 畫水平尺規刻度
                float startX = _cameraOffset.X % step;
                float worldStartX = -((int)(_cameraOffset.X / step)) * 100;
                
                if (startX > 0) 
                { 
                    startX -= step; 
                    worldStartX -= 100; 
                }

                for (float x = startX; x < this.Width; x += step)
                {
                    if (x > RULER_SIZE)
                    {
                        g.DrawLine(rulerPen, x, 0, x, RULER_SIZE);
                        g.DrawString(worldStartX.ToString(), rulerFont, textBrush, x + 2, 2);
                    }

                    for (int i = 1; i < 10; i++)
                    {
                        float subX = x + i * subStep;
                        if (subX > RULER_SIZE)
                        {
                            int lineLen = (i == 5) ? 10 : 5; // 中間 50 像素的刻度稍微長一點
                            g.DrawLine(rulerPen, subX, RULER_SIZE - lineLen, subX, RULER_SIZE);
                        }
                    }
                    worldStartX += 100;
                }

                // 畫垂直尺規刻度
                float startY = _cameraOffset.Y % step;
                float worldStartY = -((int)(_cameraOffset.Y / step)) * 100;

                if (startY > 0) 
                { 
                    startY -= step; 
                    worldStartY -= 100; 
                }

                StringFormat sfVert = new StringFormat() { FormatFlags = StringFormatFlags.DirectionVertical };

                for (float y = startY; y < this.Height; y += step)
                {
                    if (y > RULER_SIZE)
                    {
                        g.DrawLine(rulerPen, 0, y, RULER_SIZE, y);
                        g.DrawString(worldStartY.ToString(), rulerFont, textBrush, 2, y + 2, sfVert);
                    }

                    for (int i = 1; i < 10; i++)
                    {
                        float subY = y + i * subStep;
                        if (subY > RULER_SIZE)
                        {
                            int lineLen = (i == 5) ? 10 : 5;
                            g.DrawLine(rulerPen, RULER_SIZE - lineLen, subY, RULER_SIZE, subY);
                        }
                    }
                    worldStartY += 100;
                }

                // 畫滑鼠游標的紅色追蹤線
                if (_currentMouseScreenPos != Point.Empty)
                {
                    using (Pen cursorPen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
                    {
                        // 頂部追蹤
                        g.DrawLine(cursorPen, _currentMouseScreenPos.X, 0, _currentMouseScreenPos.X, RULER_SIZE);
                        // 左側追蹤
                        g.DrawLine(cursorPen, 0, _currentMouseScreenPos.Y, RULER_SIZE, _currentMouseScreenPos.Y);
                    }
                }
            }
        }

        public Bitmap GetTransparentCanvasRender()
        {
            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].IsSelected = false;
            float maxX = ActualPageSize.Width;
            float maxY = ActualPageSize.Height;
            Bitmap bmp = new Bitmap(Math.Max((int)maxX + 50, (int)PageSize.Width), Math.Max((int)maxY + 50, (int)PageSize.Height)); 
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < Shapes.Count; i++) Shapes[i].DrawWithTransform(g);
            }
            return bmp;
        }
    }
}
