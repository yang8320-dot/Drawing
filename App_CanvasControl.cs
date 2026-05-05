/*
 * 檔案功能：實作無邊界畫布、圖形繪製、平移(Pan)與重繪邏輯
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
        // 儲存所有繪製的圖形
        public List<App_Shapes.IShape> Shapes { get; private set; } = new List<App_Shapes.IShape>();
        
        // 畫布平移偏移量 (實現無邊界畫布)
        private PointF _canvasOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePosition;

        // 目前繪圖狀態
        public App_Shapes.ShapeType CurrentTool { get; set; } = App_Shapes.ShapeType.Pointer;
        public Color CurrentColor { get; set; } = Color.Black;
        
        private App_Shapes.IShape _tempShape = null;

        public App_CanvasControl()
        {
            this.DoubleBuffered = true; // 防止閃爍
            this.BackColor = Color.White;
            this.Cursor = Cursors.Cross;
            
            // 滑鼠事件綁定
            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            // 中鍵平移畫布
            if (e.Button == MouseButtons.Middle)
            {
                _isPanning = true;
                _lastMousePosition = e.Location;
                this.Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 計算真實畫布座標 (扣除平移偏移量)
                PointF realPt = new PointF(e.X - _canvasOffset.X, e.Y - _canvasOffset.Y);

                if (CurrentTool != App_Shapes.ShapeType.Pointer)
                {
                    _tempShape = App_Shapes.ShapeFactory.CreateShape(CurrentTool, realPt, CurrentColor);
                }
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                int dx = e.X - _lastMousePosition.X;
                int dy = e.Y - _lastMousePosition.Y;
                _canvasOffset.X += dx;
                _canvasOffset.Y += dy;
                _lastMousePosition = e.Location;
                this.Invalidate(); // 觸發重繪
                return;
            }

            if (_tempShape != null && e.Button == MouseButtons.Left)
            {
                PointF realPt = new PointF(e.X - _canvasOffset.X, e.Y - _canvasOffset.Y);
                _tempShape.UpdateEndPoint(realPt);
                this.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.Cursor = Cursors.Cross;
            }

            if (_tempShape != null)
            {
                Shapes.Add(_tempShape);
                _tempShape = null;
                this.Invalidate();
            }
        }

        // 覆寫繪圖邏輯
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 應用畫布平移
            g.TranslateTransform(_canvasOffset.X, _canvasOffset.Y);

            // 繪製所有已儲存的圖形
            foreach (var shape in Shapes)
            {
                shape.Draw(g);
            }

            // 繪製正在繪製中的圖形
            _tempShape?.Draw(g);
            
            g.ResetTransform();
        }
        
        // 取得不含背景的乾淨 Bitmap (用於匯出 PNG/PDF)
        public Bitmap GetTransparentCanvasRender()
        {
            // 這裡需要計算所有圖形的邊界，簡化版直接給定一個大範圍
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
