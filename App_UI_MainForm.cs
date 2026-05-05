/*
 * 檔案功能：主視覺介面設計與控制邏輯 (動態向量圖示 UI)
 * 對應選單：MainWorkspace
 * 對應資料庫：MainWorkspace
 * 資料表名稱：App_UI_MainForm
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DrawingApp
{
    public class App_UI_MainForm : Form
    {
        private Panel _toolbarPanel;
        private App_CanvasControl _canvas;
        private Button _btnPointer, _btnLine, _btnRect, _btnCircle, _btnNode;
        private Button _activeToolBtn; // 記錄目前被選取的工具按鈕

        public App_UI_MainForm()
        {
            InitializeUI();
            InitializeDatabaseAsync();
        }

        private async void InitializeDatabaseAsync()
        {
            try { await App_Database.InitializeDatabaseAsync("MainWorkspace", "App_CanvasControl"); }
            catch (Exception ex) { MessageBox.Show($"DB Init Error: {ex.Message}"); }
        }

        private void InitializeUI()
        {
            this.Text = "整合通知中心 - Draw.io 風格架構繪圖";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);

            _toolbarPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White, Padding = new Padding(10) };

            // 建立工具列按鈕 (使用動態繪圖)
            _btnPointer = CreateIconButton(10, App_Shapes.ShapeType.Pointer);
            _btnLine = CreateIconButton(60, App_Shapes.ShapeType.Line);
            _btnRect = CreateIconButton(110, App_Shapes.ShapeType.Rectangle);
            _btnCircle = CreateIconButton(160, App_Shapes.ShapeType.Circle);
            _btnNode = CreateIconButton(210, App_Shapes.ShapeType.TextNode);
            
            // 預設選擇游標
            SetActiveButton(_btnPointer);

            // 操作按鈕 (保留文字)
            Button btnColor = CreateTextButton("Color", 280, btnColor_Click);
            Button btnPNG = CreateTextButton("Export PNG", 370, btnSavePNG_Click);
            Button btnPDF = CreateTextButton("Export PDF", 470, btnSavePDF_Click);
            Button btnDB = CreateTextButton("Save DB", 570, btnSaveDB_Click);

            _toolbarPanel.Controls.AddRange(new Control[] { _btnPointer, _btnLine, _btnRect, _btnCircle, _btnNode, btnColor, btnPNG, btnPDF, btnDB });

            _canvas = new App_CanvasControl { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };

            Panel canvasContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 15, 15, 15) };
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        // 建立帶有動態向量圖示的按鈕
        private Button CreateIconButton(int x, App_Shapes.ShapeType toolType)
        {
            Button btn = new Button
            {
                Location = new Point(x, 10), Size = new Size(40, 40),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                Tag = toolType // 將工具類型存入 Tag
            };
            btn.FlatAppearance.BorderSize = 0;
            
            btn.Click += (s, e) => {
                _canvas.CurrentTool = toolType;
                SetActiveButton(btn);
            };

            // 利用 Paint 事件繪製專業的向量小圖示
            btn.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(80,80,80), 2))
                {
                    int w = btn.Width, h = btn.Height;
                    switch (toolType)
                    {
                        case App_Shapes.ShapeType.Pointer:
                            // 畫一個滑鼠游標箭頭
                            Point[] pts = { new Point(14, 10), new Point(14, 30), new Point(19, 23), new Point(26, 23) };
                            g.DrawPolygon(p, pts);
                            break;
                        case App_Shapes.ShapeType.Line:
                            // 畫一條斜線加箭頭
                            p.CustomEndCap = new CustomLineCap(null, new GraphicsPath(new[] { new PointF(-2,-2), new PointF(0,0), new PointF(2,-2) }, new[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line }));
                            g.DrawLine(p, 10, 30, 30, 10);
                            break;
                        case App_Shapes.ShapeType.Rectangle:
                            g.DrawRectangle(p, 10, 12, 20, 16);
                            break;
                        case App_Shapes.ShapeType.Circle:
                            g.DrawEllipse(p, 10, 10, 20, 20);
                            break;
                        case App_Shapes.ShapeType.TextNode:
                            g.DrawRectangle(p, 8, 12, 24, 16);
                            using (Font f = new Font("Arial", 8, FontStyle.Bold))
                                g.DrawString("A", f, Brushes.DimGray, 14, 13);
                            break;
                    }
                }
            };
            return btn;
        }

        private Button CreateTextButton(string text, int xOffset, EventHandler onClick)
        {
            Button btn = new Button { Text = text, Location = new Point(xOffset, 10), Size = new Size(85, 40), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            return btn;
        }

        // 設定工具按鈕的選取高亮狀態
        private void SetActiveButton(Button btn)
        {
            if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.White;
            _activeToolBtn = btn;
            _activeToolBtn.BackColor = Color.LightSkyBlue;
        }

        private void btnColor_Click(object sender, EventArgs e)
        {
            using (ColorDialog cd = new ColorDialog())
                if (cd.ShowDialog() == DialogResult.OK) _canvas.CurrentColor = cd.Color;
        }

        private async void btnSavePNG_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "PNG Image|*.png" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ((Button)sender).Enabled = false;
                    Bitmap bmp = _canvas.GetTransparentCanvasRender();
                    await App_Export.ExportToPngAsync(bmp, sfd.FileName);
                    this.Invoke((MethodInvoker)delegate {
                        MessageBox.Show("PNG Exported!", "Success");
                        ((Button)sender).Enabled = true;
                    });
                }
            }
        }

        private async void btnSavePDF_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "PDF Document|*.pdf" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ((Button)sender).Enabled = false;
                    Bitmap bmp = _canvas.GetTransparentCanvasRender();
                    await App_Export.ExportToPdfAsync(bmp, sfd.FileName, true);
                    this.Invoke((MethodInvoker)delegate {
                        MessageBox.Show("PDF Exported!", "Success");
                        ((Button)sender).Enabled = true;
                    });
                }
            }
        }

        private async void btnSaveDB_Click(object sender, EventArgs e)
        {
            ((Button)sender).Enabled = false;
            string dummyJson = $"[{{\"Count\": {_canvas.Shapes.Count}}}]";
            await App_Database.SaveDrawingDataAsync("MainWorkspace", "App_CanvasControl", dummyJson);
            this.Invoke((MethodInvoker)delegate {
                MessageBox.Show("Saved to SQLite!", "Database");
                ((Button)sender).Enabled = true;
            });
        }
    }
}
