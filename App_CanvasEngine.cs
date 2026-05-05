// ==========================================
// 檔案功能：無邊界畫布、圖形繪製、縮放邏輯、PDF/PNG 匯出
// 對應選單：畫圖工作區
// 對應資料庫：DrawingApp.db
// 對應資料表：App_CanvasEngine
// ==========================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace PortableDrawingApp
{
    public class App_CanvasEngine : UserControl
    {
        public List<ShapeModel> Shapes = new List<ShapeModel>();
        public ShapeType CurrentMode { get; set; } = ShapeType.Rectangle;
        public Color CurrentColor { get; set; } = Color.Black;

        private ShapeModel activeShape = null;
        private bool isDragging = false;
        private bool isResizing = false;
        private int resizeHandleIndex = -1;
        private PointF lastMousePos;

        public App_CanvasEngine()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.None; // 無邊界畫布設計
            this.Cursor = Cursors.Cross;

            this.MouseDown += Canvas_MouseDown;
            this.MouseMove += Canvas_MouseMove;
            this.MouseUp += Canvas_MouseUp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 繪製心智圖/組織圖連線
            foreach (var shape in Shapes.Where(s => !string.IsNullOrEmpty(s.ParentId)))
            {
                var parent = Shapes.FirstOrDefault(p => p.Id == shape.ParentId);
                if (parent != null)
                {
                    PointF pCenter = new PointF(parent.X + parent.Width / 2, parent.Y + parent.Height / 2);
                    PointF sCenter = new PointF(shape.X + shape.Width / 2, shape.Y + shape.Height / 2);
                    using (Pen linkPen = new Pen(Color.Gray, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                    {
                        g.DrawLine(linkPen, pCenter, sCenter);
                    }
                }
            }

            // 繪製物件
            foreach (var shape in Shapes)
            {
                using (Pen pen = new Pen(shape.BorderColor, 2))
                {
                    if (shape.Type == ShapeType.Rectangle || shape.Type == ShapeType.MindMapNode)
                        g.DrawRectangle(pen, shape.X, shape.Y, shape.Width, shape.Height);
                    else if (shape.Type == ShapeType.Circle)
                        g.DrawEllipse(pen, shape.X, shape.Y, shape.Width, shape.Height);
                    else if (shape.Type == ShapeType.Line)
                        g.DrawLine(pen, shape.X, shape.Y, shape.X + shape.Width, shape.Y + shape.Height);

                    if (!string.IsNullOrEmpty(shape.Content))
                    {
                        using (Font font = new Font("Arial", 10))
                        {
                            g.DrawString(shape.Content, font, Brushes.Black, shape.X + 5, shape.Y + 5);
                        }
                    }
                }

                // 選取狀態顯示縮放點
                if (shape.IsSelected)
                {
                    foreach (var handle in shape.GetResizeHandles())
                    {
                        g.FillRectangle(Brushes.DodgerBlue, handle);
                        g.DrawRectangle(Pens.White, handle.X, handle.Y, handle.Width, handle.Height);
                    }
                }
            }
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            lastMousePos = e.Location;
            
            // 判斷是否點擊到縮放點
            var selectedShape = Shapes.FirstOrDefault(s => s.IsSelected);
            if (selectedShape != null)
            {
                var handles = selectedShape.GetResizeHandles();
                for (int i = 0; i < handles.Length; i++)
                {
                    if (handles[i].Contains(e.Location))
                    {
                        isResizing = true;
                        resizeHandleIndex = i;
                        activeShape = selectedShape;
                        return;
                    }
                }
            }

            // 判斷是否點擊到現有圖形
            activeShape = Shapes.LastOrDefault(s => s.GetBounds().Contains(e.Location));
            Shapes.ForEach(s => s.IsSelected = false);

            if (activeShape != null)
            {
                activeShape.IsSelected = true;
                isDragging = true;
            }
            else
            {
                // 建立新圖形
                activeShape = new ShapeModel
                {
                    Type = CurrentMode,
                    X = e.X, Y = e.Y,
                    Width = 0, Height = 0,
                    BorderColor = CurrentColor,
                    IsSelected = true
                };
                Shapes.Add(activeShape);
                isResizing = true; // 新建圖形視為右下角拖曳
                resizeHandleIndex = 3;
            }
            this.Invalidate();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (activeShape == null) return;

            float dx = e.X - lastMousePos.X;
            float dy = e.Y - lastMousePos.Y;

            if (isDragging)
            {
                activeShape.X += dx;
                activeShape.Y += dy;
            }
            else if (isResizing)
            {
                // 簡化的縮放邏輯 (以右下角為例)
                if (resizeHandleIndex == 3)
                {
                    activeShape.Width = Math.Max(5, e.X - activeShape.X);
                    activeShape.Height = Math.Max(5, e.Y - activeShape.Y);
                }
            }

            lastMousePos = e.Location;
            this.Invalidate();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            isResizing = false;
            resizeHandleIndex = -1;
            
            // 清理太小的圖形
            if (activeShape != null && activeShape.Width < 5 && activeShape.Height < 5 && activeShape.Type != ShapeType.Line)
            {
                Shapes.Remove(activeShape);
            }
            activeShape = null;
            this.Invalidate();
        }

        // ==========================================
        // 匯出功能
        // ==========================================
        public void ExportToPng(string filePath)
        {
            using (Bitmap bmp = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent); // 透明背景
                    this.OnPaint(new PaintEventArgs(g, new Rectangle(0, 0, bmp.Width, bmp.Height)));
                }
                bmp.Save(filePath, ImageFormat.Png);
            }
        }

        public void ExportToPdf(bool landscape)
        {
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            pd.DefaultPageSettings.Landscape = landscape;
            pd.PrintPage += (s, e) =>
            {
                using (Bitmap bmp = new Bitmap(this.Width, this.Height))
                {
                    this.DrawToBitmap(bmp, new Rectangle(0, 0, this.Width, this.Height));
                    e.Graphics.DrawImage(bmp, 0, 0);
                }
            };

            using (PrintDialog pdlg = new PrintDialog { Document = pd })
            {
                if (pdlg.ShowDialog() == DialogResult.OK) pd.Print();
            }
        }
    }
}
