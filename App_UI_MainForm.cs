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
        private Button _activeToolBtn;

        public App_UI_MainForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "專業版繪圖系統 (支援 Command Undo/Redo & Zoom)";
            this.Size = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _toolbarPanel = new Panel();
            _toolbarPanel.Dock = DockStyle.Top;
            _toolbarPanel.Height = 70;
            _toolbarPanel.BackColor = Color.White;

            int currentX = 10;
            
            // 繪圖工具區
            var btnPointer = CreateIconButton(currentX, App_Shapes.ShapeType.Pointer, "游標 (Esc)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.ArrowLine, "箭頭線"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.OrthogonalLine, "90度折線"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Rectangle, "矩形"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Circle, "圓形"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Diamond, "菱形 (判斷)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Triangle, "三角形"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.TextNode, "文字框"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Text, "純文字"); currentX += 45;
            
            SetActiveButton(btnPointer);

            // 分隔線
            currentX += 10;

            // 系統與操作區
            _canvas = new App_CanvasControl();
            _canvas.Dock = DockStyle.Fill;
            
            Button btnUndo = CreateTextButton("復原", currentX, (s, e) => _canvas.CmdManager.Undo()); currentX += 60;
            Button btnRedo = CreateTextButton("重做", currentX, (s, e) => _canvas.CmdManager.Redo()); currentX += 60;
            
            // 視角縮放區
            Button btnZoomIn = CreateTextButton("放大+", currentX, (s, e) => _canvas.SetZoom(_canvas.ZoomFactor + 0.2f)); currentX += 60;
            Button btnZoomOut = CreateTextButton("縮小-", currentX, (s, e) => _canvas.SetZoom(_canvas.ZoomFactor - 0.2f)); currentX += 60;
            Button btnZoomReset = CreateTextButton("100%", currentX, (s, e) => _canvas.SetZoom(1.0f)); currentX += 60;

            // 存取檔
            CreateTextButton("存檔", currentX, (s, e) => App_SaveLoad.SaveAs(_canvas.Shapes)); currentX += 60;
            CreateTextButton("讀取", currentX, (s, e) => {
                var data = App_SaveLoad.Load();
                if (data != null) { _canvas.Shapes = data; _canvas.Invalidate(); }
            }); currentX += 60;

            // 事件綁定
            _canvas.OnToolResetRequested += () => { _canvas.CurrentTool = App_Shapes.ShapeType.Pointer; SetActiveButton(btnPointer); };
            _canvas.OnShapeDoubleClicked += ShowPropertyEditor;

            Panel canvasContainer = new Panel();
            canvasContainer.Dock = DockStyle.Fill;
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        private Button CreateIconButton(int startX, App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button();
            btn.Location = new Point(startX, 10);
            btn.Size = new Size(40, 40);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            
            new ToolTip().SetToolTip(btn, tooltip);
            
            btn.Click += (s, e) => {
                _canvas.CurrentTool = type;
                SetActiveButton(btn);
            };

            btn.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(80, 80, 80), 2))
                {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(12, 10), new Point(12, 30), new Point(18, 22), new Point(25, 22) });
                    else if (type == App_Shapes.ShapeType.ArrowLine) { g.DrawLine(p, 8, 30, 30, 8); g.DrawLine(p, 20, 8, 30, 8); g.DrawLine(p, 30, 8, 30, 18); }
                    else if (type == App_Shapes.ShapeType.OrthogonalLine) { g.DrawLines(p, new PointF[] { new PointF(8, 30), new PointF(20, 30), new PointF(20, 10), new PointF(30, 10) }); }
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 8, 10, 24, 20);
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 8, 8, 24, 24);
                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(20, 6), new PointF(34, 20), new PointF(20, 34), new PointF(6, 20) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(20, 8), new PointF(32, 30), new PointF(8, 30) });
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 6, 10, 28, 20); g.DrawString("A", new Font("Arial", 10), Brushes.Black, 12, 12); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), Brushes.Black, 10, 8);
                }
            };
            _toolbarPanel.Controls.Add(btn);
            return btn;
        }

        private Button CreateTextButton(string text, int startX, EventHandler onClick)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(startX, 10);
            btn.Size = new Size(55, 40);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            _toolbarPanel.Controls.Add(btn);
            return btn;
        }

        private void SetActiveButton(Button btn)
        {
            if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.White;
            _activeToolBtn = btn;
            _activeToolBtn.BackColor = Color.LightSkyBlue;
        }

        // 進階屬性面板 (支援線條粗細與樣式)
        private void ShowPropertyEditor(App_Shapes.ShapeBase shape)
        {
            using (Form form = new Form())
            {
                form.Text = "進階圖形屬性";
                form.Size = new Size(340, 420);
                form.StartPosition = FormStartPosition.CenterParent;

                TextBox txtBox = new TextBox() { Text = shape.Text, Multiline = true, Location = new Point(20, 20), Size = new Size(280, 60) };
                
                ComboBox cbFont = new ComboBox() { Location = new Point(20, 90), Width = 150 };
                cbFont.Items.AddRange(new string[] { "Arial", "標楷體", "微軟正黑體" });
                cbFont.Text = shape.FontName;
                NumericUpDown nudFontSize = new NumericUpDown() { Location = new Point(180, 90), Width = 120, Minimum = 8, Maximum = 72, Value = (decimal)shape.FontSize };
                
                // 新增：線條粗細與樣式
                Label lblStroke = new Label() { Text = "外框粗細:", Location = new Point(20, 130), Width = 80 };
                NumericUpDown nudStroke = new NumericUpDown() { Location = new Point(100, 130), Width = 70, Minimum = 1, Maximum = 20, Value = (decimal)shape.StrokeWidth };
                
                Label lblDash = new Label() { Text = "外框樣式:", Location = new Point(20, 160), Width = 80 };
                ComboBox cbDash = new ComboBox() { Location = new Point(100, 160), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
                cbDash.Items.AddRange(new object[] { DashStyle.Solid, DashStyle.Dash, DashStyle.Dot });
                cbDash.SelectedItem = shape.StrokeDashStyle;

                Button btnShapeColor = new Button() { Text = "外框顏色", Location = new Point(20, 210), Width = 280, BackColor = shape.ShapeColor, ForeColor = Color.White };
                btnShapeColor.Click += (s, e) => { using (var cd = new ColorDialog() { Color = shape.ShapeColor }) if (cd.ShowDialog() == DialogResult.OK) { shape.ShapeColor = cd.Color; btnShapeColor.BackColor = cd.Color; } };

                Button btnFontColor = new Button() { Text = "文字顏色", Location = new Point(20, 250), Width = 280, BackColor = shape.FontColor, ForeColor = Color.White };
                btnFontColor.Click += (s, e) => { using (var cd = new ColorDialog() { Color = shape.FontColor }) if (cd.ShowDialog() == DialogResult.OK) { shape.FontColor = cd.Color; btnFontColor.BackColor = cd.Color; } };

                Button btnOk = new Button() { Text = "確定", Location = new Point(120, 320), Width = 100 };
                btnOk.Click += (s, e) =>
                {
                    shape.Text = txtBox.Text;
                    shape.FontName = cbFont.Text;
                    shape.FontSize = (float)nudFontSize.Value;
                    shape.StrokeWidth = (float)nudStroke.Value;
                    shape.StrokeDashStyle = (DashStyle)cbDash.SelectedItem;
                    
                    // 屬性變更不寫入 Undo 以免過於複雜，這裡僅強制重繪
                    _canvas.Invalidate();
                    form.Close();
                };

                form.Controls.AddRange(new Control[] { txtBox, cbFont, nudFontSize, lblStroke, nudStroke, lblDash, cbDash, btnShapeColor, btnFontColor, btnOk });
                form.ShowDialog();
            }
        }
    }
}
