// ============================================================
// FILE: App_CanvasControl.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrawingApp.Tools;

namespace DrawingApp
{
    public partial class App_CanvasControl : Panel
    {
        // ===== 核心資料與狀態 =====
        public List<App_Shapes.ShapeBase> Shapes { get; set; } = new List<App_Shapes.ShapeBase>();
        public CommandManager CmdManager { get; } = new CommandManager();

        private SizeF _basePageSize = new SizeF(2100, 2970);
        public SizeF PageSize 
        { 
            get => _basePageSize; 
            set { _basePageSize = value; _isQuadTreeDirty = true; this.Invalidate(); } 
        }

        public SizeF ActualPageSize 
        {
            get 
            {
                if (Shapes == null || Shapes.Count == 0) return _basePageSize;
                float maxX = _basePageSize.Width, maxY = _basePageSize.Height;
                for (int i = 0; i < Shapes.Count; i++)
                {
                    var s = Shapes[i];
                    if (s is App_Shapes.ConnectorShape cs) {
                        maxX = Math.Max(maxX, Math.Max(cs.StartPt.X, cs.EndPt.X) + 100);
                        maxY = Math.Max(maxY, Math.Max(cs.StartPt.Y, cs.EndPt.Y) + 100);
                    } else {
                        maxX = Math.Max(maxX, s.Bounds.Right + 100);
                        maxY = Math.Max(maxY, s.Bounds.Bottom + 100);
                    }
                }
                return new SizeF(maxX, maxY);
            }
        }
        
        // ===== 視角與渲染設定 =====
        public float ZoomFactor { get; private set; } = 1.0f;
        private PointF _cameraOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;
        private Point _currentMouseScreenPos;

        public bool SnapToGrid { get; set; } = true;
        public float GridSize { get; set; } = 20f;
        public bool ShowRulers { get; set; } = true;

        public bool ShowPageBounds { get; set; } = false;
        public bool ShowPageNumbers { get; set; } = false;
        public string CanvasTitle { get; set; } = "未命名";

        // 【Req 1: 新增 物件鎖點 與 正交模式】
        public bool EnableObjectSnap { get; set; } = true;
        public bool EnableOrthoMode { get; set; } = false;

        private const int RULER_SIZE = 25;

        // ===== 工具與選取狀態 =====
        private ITool _currentToolInstance;
        private App_Shapes.ShapeType _currentToolType = App_Shapes.ShapeType.Pointer;
        private App_Shapes.ShapeType _previousToolType = App_Shapes.ShapeType.Pointer;
        
        public App_Shapes.ShapeType CurrentShapeType => _currentToolType;
        public Color CurrentColor { get; set; } = Color.Black;

        public List<App_Shapes.ShapeBase> SelectedShapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        private App_Shapes.ShapeBase _tempShape = null;
        public App_Shapes.ShapeBase FormatSourceShape { get; set; }

        // 【Req 9: 儲存預設樣式供後續新物件套用】
        public App_Shapes.ShapeBase DefaultFormatTemplate { get; } = new App_Shapes.RectShape(new PointF(0, 0), Color.Black);

        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;
        private App_Shapes.AnchorPosition _hoveredAnchor = App_Shapes.AnchorPosition.Auto;
        private List<Tuple<PointF, PointF>> _smartGuides = new List<Tuple<PointF, PointF>>();

        // ===== 附屬元件狀態 (由其他 Partial 控制) =====
        private Rectangle _minimapRect;
        private bool _isDraggingMinimap = false;
        private const int MINIMAP_WIDTH = 200;

        private TextBox _inlineTextBox;
        private App_Shapes.ShapeBase _editingShape = null;

        private App_Shapes.QuadTree _quadTree;
        private bool _isQuadTreeDirty = true;
        
        private List<App_Shapes.ShapeBase> _clipboard = new List<App_Shapes.ShapeBase>();

        // ===== 事件委派 =====
        public event Action<PointF> OnImageInsertRequested;
        public event Action<App_Shapes.ShapeType> OnToolChangedRequested;
        public event Action OnSelectionChanged;

        public App_Shapes.ShapeType CurrentTool 
        { 
            get => _currentToolType; 
            set 
            { 
                if (_currentToolType != App_Shapes.ShapeType.HandPan) _previousToolType = _currentToolType;
                _currentToolInstance?.OnToolDeactivated(this);
                _currentToolType = value;
                
                switch (_currentToolType)
                {
                    case App_Shapes.ShapeType.Pointer: _currentToolInstance = new PointerTool(); this.Cursor = Cursors.Default; break;
                    case App_Shapes.ShapeType.HandPan: _currentToolInstance = null; this.Cursor = Cursors.Hand; break;
                    case App_Shapes.ShapeType.FormatPainter: _currentToolInstance = null; this.Cursor = Cursors.UpArrow; break;
                    case App_Shapes.ShapeType.ArrowLine:
                    case App_Shapes.ShapeType.StraightLine:
                    case App_Shapes.ShapeType.OrthogonalLine: _currentToolInstance = new ConnectionTool(); this.Cursor = Cursors.Cross; break;
                    case App_Shapes.ShapeType.BezierPen: _currentToolInstance = new BezierTool(); this.Cursor = Cursors.Cross; break;
                    case App_Shapes.ShapeType.Image: _currentToolInstance = null; this.Cursor = Cursors.Cross; break;
                    default: _currentToolInstance = new DrawingTool(); this.Cursor = Cursors.Cross; break;
                }
            } 
        }

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

            CmdManager.OnStateChanged += () => { _isQuadTreeDirty = true; this.Invalidate(); };

            InitializeInlineEditor();
            CurrentTool = App_Shapes.ShapeType.Pointer;
        }

        public void RequestToolChange(App_Shapes.ShapeType type) => OnToolChangedRequested?.Invoke(type);
        public void SetTempShape(App_Shapes.ShapeBase shape) { _tempShape = shape; }
        public void TriggerSelectionChanged() => OnSelectionChanged?.Invoke();
        public void ClearSelection() { foreach (var s in SelectedShapes) s.IsSelected = false; SelectedShapes.Clear(); }
        
        public void SetHoveredConnectionTarget(App_Shapes.ShapeBase shape, App_Shapes.AnchorPosition anchor) { _hoveredShapeForConnection = shape; _hoveredAnchor = anchor; }
        public App_Shapes.ShapeBase GetHoveredConnectionTarget() => _hoveredShapeForConnection;
        public App_Shapes.AnchorPosition GetHoveredAnchor() => _hoveredAnchor;

        public void AddSmartGuide(PointF p1, PointF p2) => _smartGuides.Add(new Tuple<PointF, PointF>(p1, p2));
        public void ClearSmartGuides() => _smartGuides.Clear();
        public bool HasSmartGuides() => _smartGuides.Count > 0;
        
        public PointF GetRealPointFromMouse() => GetRealPoint(_currentMouseScreenPos);
        public void InvalidateMinimap() { if (_minimapRect != Rectangle.Empty) this.Invalidate(_minimapRect); }

        public void SetZoom(float zoom)
        {
            ZoomFactor = Math.Max(0.2f, Math.Min(zoom, 5.0f));
            if (_inlineTextBox.Visible) CancelInlineText();
            this.Invalidate();
        }

        public float Snap(float value) => SnapToGrid ? (float)Math.Round(value / GridSize) * GridSize : value;
        public float SnapAngle(float angle, float step) => (Control.ModifierKeys == Keys.Alt) ? angle : (float)Math.Round(angle / step) * step;

        public void InvalidateWorldRect(RectangleF worldRect)
        {
            if (worldRect == RectangleF.Empty) return;
            float x = worldRect.X * ZoomFactor + _cameraOffset.X, y = worldRect.Y * ZoomFactor + _cameraOffset.Y;
            float w = worldRect.Width * ZoomFactor, h = worldRect.Height * ZoomFactor;
            this.Invalidate(new Rectangle((int)x - 50, (int)y - 50, (int)w + 100, (int)h + 100));
        }

        public RectangleF GetShapesAndConnectorsBounds(List<App_Shapes.ShapeBase> shapes)
        {
            if (shapes == null || shapes.Count == 0) return RectangleF.Empty;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var allAffected = new HashSet<App_Shapes.ShapeBase>(shapes);
            for (int i = 0; i < Shapes.Count; i++)
            {
                if (Shapes[i] is App_Shapes.ConnectorShape c && shapes.Any(s => s.Id == c.SourceId || s.Id == c.TargetId))
                    allAffected.Add(c);
            }

            bool hasValidPoints = false;
            foreach (var s in allAffected)
            {
                if (s is App_Shapes.ConnectorShape cs) {
                    minX = Math.Min(minX, Math.Min(cs.StartPt.X, cs.EndPt.X)); minY = Math.Min(minY, Math.Min(cs.StartPt.Y, cs.EndPt.Y));
                    maxX = Math.Max(maxX, Math.Max(cs.StartPt.X, cs.EndPt.X)); maxY = Math.Max(maxY, Math.Max(cs.StartPt.Y, cs.EndPt.Y));
                    hasValidPoints = true;
                } else {
                    minX = Math.Min(minX, s.Bounds.Left); minY = Math.Min(minY, s.Bounds.Top);
                    maxX = Math.Max(maxX, s.Bounds.Right); maxY = Math.Max(maxY, s.Bounds.Bottom);
                    hasValidPoints = true;
                }
            }
            return hasValidPoints ? new RectangleF(minX, minY, maxX - minX, maxY - minY) : RectangleF.Empty;
        }

        private void EnsureQuadTree()
        {
            if (_isQuadTreeDirty || _quadTree == null)
            {
                SizeF size = ActualPageSize;
                _quadTree = new App_Shapes.QuadTree(0, new RectangleF(0, 0, size.Width, size.Height));
                for (int i = 0; i < Shapes.Count; i++) _quadTree.Insert(Shapes[i]);
                _isQuadTreeDirty = false;
            }
        }

        public List<App_Shapes.ShapeBase> GetShapesInRect(RectangleF rect)
        {
            EnsureQuadTree();
            List<App_Shapes.ShapeBase> nearShapes = new List<App_Shapes.ShapeBase>();
            if (_quadTree != null) _quadTree.Retrieve(nearShapes, rect);
            return nearShapes.OrderByDescending(s => Shapes.IndexOf(s)).ToList();
        }

        public App_Shapes.ShapeBase GetShapeAtPoint(PointF pt)
        {
            var nearShapes = GetShapesInRect(new RectangleF(pt.X - 5, pt.Y - 5, 10, 10));
            foreach (var shape in nearShapes) if (shape.HitTest(pt)) return shape;
            return null;
        }

        private PointF GetRealPoint(Point screenPt) => new PointF((screenPt.X - _cameraOffset.X) / ZoomFactor, (screenPt.Y - _cameraOffset.Y) / ZoomFactor);
        
        private void TriggerImageInsert(PointF pt) => OnImageInsertRequested?.Invoke(pt);
    }
}
