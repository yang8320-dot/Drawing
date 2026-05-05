/*
 * 檔案功能：實作無邊界畫布、圖形繪製、選取/移動/縮放/連線狀態機與按鍵監聽
 * 對應選單：MainWorkspace
 * 對應資料庫：MainWorkspace.sqlite
 * 資料表名稱：App_CanvasControl
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DrawingApp
{
    public class App_CanvasControl : Panel
    {
        public List<App_Shapes.ShapeBase> Shapes { get; private set; } = new List<App_Shapes.ShapeBase>();
        
        // 畫布狀態與互動參數
        private PointF _canvasOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;

        private enum InteractionState { Idle, Drawing, Moving, Resizing, Connecting }
        private InteractionState _currentState = InteractionState.Idle;

        public App_Shapes.ShapeType CurrentTool { get; set; } = App_Shapes.ShapeType.Pointer;
        public Color CurrentColor { get; set; } = Color.Black;
        
        private App_Shapes.ShapeBase _tempShape = null;
        private App_Shapes.ShapeBase _selectedShape = null;
        private App_Shapes.HandlePosition _resizingHandle = App_Shapes.HandlePosition.None;
        private App_Shapes.ShapeBase _hoveredShapeForConnection = null;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Default;
            
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
        }

        // 攔截鍵盤事件 (支援 Delete 鍵刪除)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete && _selectedShape != null)
            {
                // 若刪除圖形，同步移除與之相連的線條
                Shapes.RemoveAll(s => s is App_Shapes.ConnectorShape c && (c.SourceShape == _selectedShape || c.TargetShape == _selectedShape));
                Shapes.Remove(_selectedShape);
                _selectedShape = null;
                this.Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private PointF GetRealPoint(Point pt) => new PointF(pt.X - _canvasOffset.X, pt.Y - _canvasOffset.Y);

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus(); // 確保能接收鍵盤事件
            if (e.Button == MouseButtons.Middle)
            {
                _isPanning = true;
                _lastMousePos = e.Location;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                PointF realPt = GetRealPoint(e.Location);
                _lastMousePos = e.Location;

                if (CurrentTool == App_Shapes.ShapeType.Pointer)
                {
                    // 1. 檢查是否點中選取框的縮放控制點
                    if (_selectedShape != null)
                    {
                        var handle = _selectedShape.HitTestHandle(realPt);
                        if (handle != App_Shapes.HandlePosition.None)
                        {
                            _currentState = InteractionState.Resizing;
                            _resizingHandle = handle;
                            return;
                        }
                    }

                    // 2. 檢查是否點中圖形 (由上而下尋找)
                    App_Shapes.ShapeBase hitShape = null;
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) { hitShape = Shapes[i]; break; }
                    }

                    if (_selectedShape != null) _selectedShape.IsSelected = false;

                    if (hitShape != null)
                    {
                        _selectedShape = hitShape;
                        _selectedShape.IsSelected = true;
                        _currentState = InteractionState.Moving;
                    }
                    else
                    {
                        _selectedShape = null;
                        _currentState = InteractionState.Idle;
                    }
                }
                else if (CurrentTool == App_Shapes.ShapeType.Line)
                {
                    // 智慧連線模式：從圖形錨點出發
                    _currentState = InteractionState.Connecting;
                    var connector = new App_Shapes.ConnectorShape(realPt, CurrentColor);
                    
                    // 尋找起始圖形
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i].HitTest(realPt)) { connector.SourceShape = Shapes[i]; break; }
                    }
                    _tempShape = connector;
                }
                else
                {
                    // 繪製一般圖形模式
                    _currentState = InteractionState.Drawing;
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, realPt, CurrentColor);
                }
                this.Invalidate();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            PointF realPt = GetRealPoint(e.Location);

            // 平動畫布
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

                if (_currentState == InteractionState.Moving && _selectedShape != null)
                {
                    _selectedShape.Move(dx, dy);
                }
                else if (_currentState == InteractionState.Resizing && _selectedShape != null)
                {
                    // 根據抓取的角落動態改變 Bounds
                    RectangleF b = _selectedShape.Bounds;
                    switch (_resizingHandle)
                    {
                        case App_Shapes.HandlePosition.NW: b = new RectangleF(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.NE: b = new RectangleF(b.X, b.Y + dy, b.Width + dx, b.Height - dy); break;
                        case App_Shapes.HandlePosition.SW: b = new RectangleF(b.X + dx, b.Y, b.Width - dx, b.Height + dy); break;
                        case App_Shapes.HandlePosition.SE: b = new RectangleF(b.X, b.Y, b.Width + dx, b.Height + dy); break;
                    }
                    // 防止縮小到負數
                    if (b.Width > 5 && b.Height > 5) _selectedShape.Bounds = b;
                }
                else if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.UpdateEndPoint(realPt);
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape conn)
                {
                    conn.UpdateEndPoint(realPt);
                    // 即時尋找可附著的目標
                    _hoveredShapeForConnection = null;
                    for (int i = Shapes.Count - 1; i >= 0; i--)
                    {
                        if (Shapes[i] != conn.SourceShape && Shapes[i].HitTest(realPt))
                        {
                            _hoveredShapeForConnection = Shapes[i]; break;
                        }
                    }
                }
                
                _lastMousePos = e.Location;
                this.Invalidate();
            }
            else // Hover 狀態變更游標
            {
                if (CurrentTool == App_Shapes.ShapeType.Pointer && _selectedShape != null)
                {
                    var handle = _selectedShape.HitTestHandle(realPt);
                    if (handle == App_Shapes.HandlePosition.NW || handle == App_Shapes.HandlePosition.SE) this.Cursor = Cursors.SizeNWSE;
                    else if (handle == App_Shapes.HandlePosition.NE || handle == App_Shapes.HandlePosition.SW) this.Cursor = Cursors.SizeNESW;
                    else if (_selectedShape.HitTest(realPt)) this.Cursor = Cursors.SizeAll;
                    else this.Cursor = Cursors.Default;
                }
                else
                {
                    this.Cursor = CurrentTool == App_Shapes.ShapeType.Pointer ? Cursors.Default : Cursors.Cross;
                }
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
                if (_currentState == InteractionState.Drawing && _tempShape != null)
                {
                    _tempShape.NormalizeBounds();
                    if (_tempShape.Bounds.Width > 5 && _tempShape.Bounds.Height > 5) Shapes.Add(_tempShape);
                }
                else if (_currentState == InteractionState.Connecting && _tempShape is App_Shapes.ConnectorShape conn)
                {
                    conn.TargetShape = _hoveredShapeForConnection; // 確認附著目標
                    Shapes.Add(conn);
                }
                else if (_currentState == InteractionState.Resizing && _selectedShape != null)
                {
                    _selectedShape.NormalizeBounds();
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

            // 繪製背景網格 (draw.io 風格)
            DrawGrid(g);

            g.TranslateTransform(_canvasOffset.X, _canvasOffset.Y);

            // 繪製所有圖形
            foreach (var shape in Shapes) shape.Draw(g);

            // 繪製繪製中/連線中的暫存圖形
            _tempShape?.Draw(g);

            // 若有 Hover 到連線目標，畫出提示錨點
            if (_hoveredShapeForConnection != null)
            {
                foreach (var anchor in _hoveredShapeForConnection.GetAnchors())
                {
                    g.FillEllipse(Brushes.Red, anchor.X - 4, anchor.Y - 4, 8, 8);
                }
            }

            // 繪製選取框與控制點 (放置於最上層)
            _selectedShape?.DrawSelection(g);
            
            g.ResetTransform();
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
            if (_selectedShape != null) _selectedShape.IsSelected = false; // 匯出前取消選取
            Bitmap bmp = new Bitmap(2000, 2000); 
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                foreach (var shape in Shapes) shape.Draw(g);
            }
            return bmp;
        }
    }
}
