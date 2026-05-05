// ==========================================
// 檔案功能：主視窗動態排版介面、選單路由
// 對應選單：DrawingApp
// 對應資料庫：無
// 對應資料表：無
// ==========================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PortableDrawingApp
{
    public class MainForm : Form
    {
        private Panel menuPanel;
        private Panel canvasContainer;
        private App_CanvasEngine canvas;
        private DbManager dbManager;

        public MainForm()
        {
            dbManager = new DbManager();
            InitializeComponent();
            LoadDataAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Pro Portable Drawing App";
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);

            // 主選單 (Padding 10)
            menuPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };
            this.Controls.Add(menuPanel);

            FlowLayoutPanel flowMenu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            menuPanel.Controls.Add(flowMenu);

            // 動態生成按鈕
            flowMenu.Controls.Add(CreateMenuButton("Rect", (s, e) => canvas.CurrentMode = ShapeType.Rectangle));
            flowMenu.Controls.Add(CreateMenuButton("Circle", (s, e) => canvas.CurrentMode = ShapeType.Circle));
            flowMenu.Controls.Add(CreateMenuButton("Mind Node", (s, e) => canvas.CurrentMode = ShapeType.MindMapNode));
            flowMenu.Controls.Add(CreateMenuButton("Add Text", (s, e) => AddTextToSelected()));
            flowMenu.Controls.Add(CreateMenuButton("Save DB", async (s, e) => await SaveDataAsync()));
            flowMenu.Controls.Add(CreateMenuButton("Export PNG", (s, e) => ExportPngAction()));
            flowMenu.Controls.Add(CreateMenuButton("Export PDF", (s, e) => canvas.ExportToPdf(true)));

            // 畫布容器 (主選單與頁面間隔 15)
            canvasContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 15, 15, 15)
            };
            this.Controls.Add(canvasContainer);
            canvasContainer.BringToFront();

            canvas = new App_CanvasEngine { Dock = DockStyle.Fill };
            canvasContainer.Controls.Add(canvas);
        }

        private Button CreateMenuButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(62, 62, 66),
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0), // 控制項間距適中
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void AddTextToSelected()
        {
            var shape = canvas.Shapes.Find(s => s.IsSelected);
            if (shape != null)
            {
                string input = Microsoft.VisualBasic.Interaction.InputBox("Enter text:", "Text Input", shape.Content);
                shape.Content = input;
                canvas.Invalidate();
            }
        }

        private async Task SaveDataAsync()
        {
            this.Text = "Pro Portable Drawing App - Saving...";
            await dbManager.SaveShapesAsync(canvas.Shapes);
            this.Text = "Pro Portable Drawing App";
            MessageBox.Show("Data saved to SQLite Database!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void LoadDataAsync()
        {
            canvas.Shapes = await dbManager.LoadShapesAsync();
            canvas.Invalidate();
        }

        private void ExportPngAction()
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image|*.png" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    canvas.ExportToPng(sfd.FileName);
                    MessageBox.Show("Exported transparent PNG!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
