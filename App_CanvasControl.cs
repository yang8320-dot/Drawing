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

        // --- 紀錄連線拖曳前的狀態 ---
        private Guid _oldSrcId, _oldTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA;
        private PointF _oldStart, _oldEnd;

        private enum InteractionState { Idle, Drawing, Moving, Resizing, Rotating, Connecting, BoxSelecting }
        private InteractionState _currentState = InteractionState.Idle;

        public App_Shapes.ShapeType CurrentTool { get; set; } = App_Shapes.ShapeType.Pointer;
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

        public event Action<App_Shapes.ShapeBase> OnShapePropertyRequested;
        public event Action<PointF> OnImageInsertRequested;
        public event Action OnToolResetRequested;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            
            this.AllowDrop = true;
            this.DragEnter += Canvas_DragEnter;
            this.DragDrop += Canvas_DragDrop;
            
            this.ContextMenuStrip = CreateContextMenu();
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
            this.MouseWheel += Canvas_MouseWheel;
            this.DoubleClick += Canvas_DoubleClick;

            CmdManager.OnStateChanged += () => this.Invalidate();

            InitializeInlineEditor();
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
                newShape.UpdateEndPoint(new PointF(snapPt.X + 80, snapPt.Y + 80)); 
                
                CmdManager.ExecuteCommand(new AddShapeCommand(Shapes, newShape));
                
                SelectedShapes.ForEach(s => s.IsSelected = false);
                SelectedShapes.Clear();
                newShape.IsSelected = true;
                SelectedShapes.Add(newShape);
                
                this.Invalidate();
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

            // --- 新增：方向鍵微調 (Nudge) 功能 ---
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

                // 透過 Command 觸發移動，保證支援 Undo/Redo
                CmdManager.ExecuteCommand(new MoveShapesCommand(SelectedShapes, dx, dy));
                this.Invalidate();
                return true;
            }

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
                CommitInlineText();

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
                else
                {
                    _currentState = InteractionState.Drawing;
                    PointF snapPt = new PointF(Snap(realPt.X), Snap(realPt.Y));
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

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);
            _smartGuides.Clear();

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
                    if (SelectedShapes.Count == 1)
                    {
                        var me = SelectedShapes[0];
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
                    foreach (var s in SelectedShapes) s.Move(dx, dy);
                }
                else if (_currentState == InteractionState.Rotating && SelectedShapes.Count == 1)
                {
                    var me = SelectedShapes[0];
                    PointF center = me.GetCenter();
                    float angle = (float)(Math.Atan2(realPt.Y - center.Y, realPt.X - center.X) * 180 / Math.PI) + 90;
                    me.RotationAngle = Snap(angle, 15f); 
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
                    SelectedShapes = Shapes.Where(s => s.HitTest(new PointF(_boxSelectRect.X + _boxSelectRect.Width/2, _boxSelectRect.Y + _boxSelectRect.Height/2)) || _boxSelectRect.IntersectsWith(s.Bounds)).ToList();
                    SelectedShapes.ForEach(s => s.IsSelected = true);
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1)
                {
                    var shape = SelectedShapes[0];

                    if (shape is App_Shapes.ConnectorShape conn)
                    {
                        _hoveredShapeForConnection = null;
                        _hoveredAnchor = App_Shapes.AnchorPosition.Auto;

                        if (_resizingHandle == App_Shapes.HandlePosition.StartPoint)
                        {
                            conn.StartPt = realPt;
                            conn.SourceId = Guid.Empty; // 拖曳時先切斷綁定
                        }
                        else if (_resizingHandle == App_Shapes.HandlePosition.EndPoint)
                        {
                            conn.EndPt = realPt;
                            conn.TargetId = Guid.Empty; // 拖曳時先切斷綁定
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
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.UpdateEndPoint(new PointF(Snap(realPt.X), Snap(realPt.Y)));
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape c)
                {
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
                }
                
                _lastMousePos = new Point((int)(_lastMousePos.X + dx * ZoomFactor), (int)(_lastMousePos.Y + dy * ZoomFactor));
                this.Invalidate();
            }
        }

        private float Snap(float angle, float step)
        {
            if (Control.ModifierKeys == Keys.Alt) return angle; 
            return (float)Math.Round(angle / step) * step;
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.Cursor = Cursors.Default;
            }

            _smartGuides.Clear();

            if (e.Button == MouseButtons.Left)
            {
                if (_currentState == InteractionState.Moving && SelectedShapes.Count > 0 && (_dragTotalDx != 0 || _dragTotalDy != 0))
                {
                    foreach (var s in SelectedShapes) s.Move(-_dragTotalDx, -_dragTotalDy);
                    CmdManager.ExecuteCommand(new MoveShapesCommand(SelectedShapes, _dragTotalDx, _dragTotalDy));
                }
                else if (_currentState == InteractionState.Resizing && SelectedShapes.Count == 1)
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
                else if (_currentState == InteractionState.Rotating && SelectedShapes.Count == 1)
                {
                    CmdManager.ExecuteCommand(new RotateShapeCommand(SelectedShapes[0], _initialAngle, SelectedShapes[0].RotationAngle));
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
            
            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

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
                if (viewRect.IntersectsWith(shape.Bounds)) shape.DrawWithTransform(g);
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

            g.ResetTransform();
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
