/*
 * 檔案功能：主視覺介面設計與控制邏輯 (純 Code-First UI)
 * 對應選單：MainWorkspace
 * 對應資料庫：MainWorkspace
 * 資料表名稱：App_UI_MainForm
 */
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp
{
    public class App_UI_MainForm : Form
    {
        private Panel _toolbarPanel;
        private App_CanvasControl _canvas;
        private Button _btnLine, _btnRect, _btnCircle, _btnNode, _btnColor, _btnSavePNG, _btnSavePDF, _btnSaveDB;

        public App_UI_MainForm()
        {
            InitializeUI();
            InitializeDatabaseAsync();
        }

        private async void InitializeDatabaseAsync()
        {
            try
            {
                await App_Database.InitializeDatabaseAsync("MainWorkspace", "App_CanvasControl");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB Init Error: {ex.Message}");
            }
        }

        private void InitializeUI()
        {
            // 視窗基本設定
            this.Text = "整合通知中心 - 向量架構繪圖系統";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240); // 淺灰背景

            // 頂部工具列 (Toolbar)
            _toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(10) // 框內與文字間隔 10
            };

            // 動態生成按鈕 (修正處：將 Lambda 參數改為 (s, e) 以符合 EventHandler 簽章)
            _btnLine = CreateToolButton("Line", 10, (s, e) => _canvas.CurrentTool = App_Shapes.ShapeType.Line);
            _btnRect = CreateToolButton("Rectangle", 100, (s, e) => _canvas.CurrentTool = App_Shapes.ShapeType.Rectangle);
            _btnCircle = CreateToolButton("Circle", 190, (s, e) => _canvas.CurrentTool = App_Shapes.ShapeType.Circle);
            _btnNode = CreateToolButton("Text Node", 280, (s, e) => _canvas.CurrentTool = App_Shapes.ShapeType.TextNode);
            
            _btnColor = CreateToolButton("Change Color", 390, btnColor_Click);
            _btnSavePNG = CreateToolButton("Export PNG", 500, btnSavePNG_Click);
            _btnSavePDF = CreateToolButton("Export PDF", 610, btnSavePDF_Click);
            _btnSaveDB = CreateToolButton("Save to DB", 720, btnSaveDB_Click);

            _toolbarPanel.Controls.AddRange(new Control[] { _btnLine, _btnRect, _btnCircle, _btnNode, _btnColor, _btnSavePNG, _btnSavePDF, _btnSaveDB });

            // 繪圖畫布 (Canvas)
            _canvas = new App_CanvasControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 15, 0, 0) // 主選單與頁面間隔 15
            };

            // 為了實現間隔，我們使用一個外層 Panel 包裝 Canvas
            Panel canvasContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 15, 15, 15) // 與視窗邊緣保持舒適距離
            };
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        private Button CreateToolButton(string text, int xOffset, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(xOffset, 10),
                Size = new Size(85, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            return btn;
        }

        private void btnColor_Click(object sender, EventArgs e)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    _canvas.CurrentColor = cd.Color;
                }
            }
        }

        // 非同步匯出 PNG
        private async void btnSavePNG_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "PNG Image|*.png" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _btnSavePNG.Enabled = false;
                    Bitmap bmp = _canvas.GetTransparentCanvasRender();
                    
                    // 執行耗時作業 (非同步背景處理)
                    await App_Export.ExportToPngAsync(bmp, sfd.FileName);
                    
                    // 執行緒安全的 UI 更新 (確保不卡頓主執行緒)
                    this.Invoke((MethodInvoker)delegate {
                        MessageBox.Show("PNG Exported Successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _btnSavePNG.Enabled = true;
                    });
                }
            }
        }

        // 非同步匯出 PDF
        private async void btnSavePDF_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "PDF Document|*.pdf" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _btnSavePDF.Enabled = false;
                    Bitmap bmp = _canvas.GetTransparentCanvasRender();
                    
                    await App_Export.ExportToPdfAsync(bmp, sfd.FileName, isLandscape: true);
                    
                    this.Invoke((MethodInvoker)delegate {
                        MessageBox.Show("PDF Exported Successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _btnSavePDF.Enabled = true;
                    });
                }
            }
        }

        // 儲存至 SQLite
        private async void btnSaveDB_Click(object sender, EventArgs e)
        {
            _btnSaveDB.Enabled = false;
            
            // 將數量轉為字串作為基礎 Demo。實務上可串接 JSON 序列化模組
            string dummyJsonData = $"[{{\"ShapeCount\": {_canvas.Shapes.Count}}}]";

            await App_Database.SaveDrawingDataAsync("MainWorkspace", "App_CanvasControl", dummyJsonData);

            this.Invoke((MethodInvoker)delegate {
                MessageBox.Show("Data saved to SQLite successfully!", "Database", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _btnSaveDB.Enabled = true;
            });
        }
    }
}
