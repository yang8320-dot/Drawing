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
            this.Text = "無邊際智慧畫布繪圖系統 (支援 A4-A1)";
            this.Size = new Size(1300, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; // 優先捕捉按鍵

            _toolbarPanel = new Panel();
            _toolbarPanel.Dock = DockStyle.Top;
            _toolbarPanel.Height = 65;
            _toolbarPanel.BackColor = Color.White;
            _toolbarPanel.Padding = new Padding(10);

            int currentX = 10;
            
            // 工具按鈕列
            var btnPointer = CreateIconButton(currentX, App_Shapes.ShapeType.Pointer, "游標 (Esc)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.ArrowLine, "箭頭線 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.StraightLine, "直線 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.Rectangle, "矩形 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.Circle, "圓形 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.Arc, "圓弧 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.TextNode, "文字框 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.Text, "純文字 (右鍵改色)"); currentX += 50;
            CreateIconButton(currentX, App_Shapes.ShapeType.Image, "插入圖片"); currentX += 60;
            
            SetActiveButton(btnPointer);

            // 畫布尺寸選單
            ComboBox cbPageSize = new ComboBox();
            cbPageSize.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPageSize.Location = new Point(currentX, 20);
            cbPageSize.Width = 120;
            cbPageSize.Items.AddRange(new string[] { "A4 直式", "A4 橫式", "A3 直式", "A3 橫式", "A2 直式", "A2 橫式", "A1 直式", "A1 橫式" });
            cbPageSize.SelectedIndex = 0;
            cbPageSize.SelectedIndexChanged += (s, e) => UpdatePageSize(cbPageSize.Text);
            _toolbarPanel.Controls.Add(cbPageSize);
            currentX += 130;

            // 文字操作按鈕
            CreateTextButton("存檔", currentX, (s, e) => App_SaveLoad.SaveAs(_canvas.Shapes)); currentX += 75;
            CreateTextButton("讀取", currentX, (s, e) => {
                var data = App_SaveLoad.Load();
                if (data != null)
                {
                    _canvas.Shapes = data;
                    _canvas.Invalidate();
                }
            }); currentX += 75;
            
            CreateTextButton("匯出 PNG", currentX, async (s, e) => {
                using (var sfd = new SaveFileDialog() { Filter = "PNG|*.png" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToPngAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName);
                        MessageBox.Show("匯出成功！");
                    }
                }
            });

            _canvas = new App_CanvasControl();
            _canvas.Dock = DockStyle.Fill;
            _canvas.Margin = new Padding(0, 15, 0, 0);
            
            // 綁定畫布事件
            _canvas.OnToolResetRequested += () => {
                _canvas.CurrentTool = App_Shapes.ShapeType.Pointer;
                SetActiveButton(btnPointer);
            };
            
            _canvas.OnShapeDoubleClicked += ShowPropertyEditor; // 雙擊開啟屬性編輯器

            Panel canvasContainer = new Panel();
            canvasContainer.Dock = DockStyle.Fill;
            canvasContainer.Padding = new Padding(15);
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        private void UpdatePageSize(string type)
        {
            switch (type)
            {
                case "A4 直式": _canvas.PageSize = new SizeF(2100, 2970); break;
                case "A4 橫式": _canvas.PageSize = new SizeF(2970, 2100); break;
                case "A3 直式": _canvas.PageSize = new SizeF(2970, 4200); break;
                case "A3 橫式": _canvas.PageSize = new SizeF(4200, 2970); break;
                case "A2 直式": _canvas.PageSize = new SizeF(4200, 5940); break;
                case "A2 橫式": _canvas.PageSize = new SizeF(5940, 4200); break;
                case "A1 直式": _canvas.PageSize = new SizeF(5940, 8410); break;
                case "A1 橫式": _canvas.PageSize = new SizeF(8410, 5940); break;
            }
            _canvas.Invalidate();
        }

        private Button CreateIconButton(int startX, App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button();
            btn.Location = new Point(startX, 10);
            btn.Size = new Size(45, 45);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            
            Color iconColor = Color.FromArgb(80, 80, 80);
            
            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            
            btn.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && type != App_Shapes.ShapeType.Pointer && type != App_Shapes.ShapeType.Image)
                {
                    using (ColorDialog cd = new ColorDialog() { Color = iconColor })
                    {
                        if (cd.ShowDialog() == DialogResult.OK)
                        {
                            iconColor = cd.Color;
                            _canvas.CurrentColor = cd.Color;
                            btn.Invalidate(); // 重繪圖示顏色
                        }
                    }
                }
                else if (e.Button == MouseButtons.Left)
                {
                    _canvas.CurrentTool = type;
                    _canvas.CurrentColor = iconColor;
                    SetActiveButton(btn);
                }
            };

            btn.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                
                using (Pen p = new Pen(iconColor, 2))
                {
                    if (type == App_Shapes.ShapeType.Pointer)
                    {
                        g.DrawPolygon(p, new Point[] { new Point(15, 10), new Point(15, 35), new Point(22, 25), new Point(30, 25) });
                    }
                    else if (type == App_Shapes.ShapeType.ArrowLine)
                    {
                        GraphicsPath arrow = new GraphicsPath();
                        arrow.AddLine(new PointF(-2, -2), new PointF(0, 0));
                        arrow.AddLine(new PointF(0, 0), new PointF(2, -2));
                        p.CustomEndCap = new CustomLineCap(null, arrow);
                        g.DrawLine(p, 10, 35, 35, 10);
                    }
                    else if (type == App_Shapes.ShapeType.StraightLine)
                    {
                        g.DrawLine(p, 10, 35, 35, 10);
                    }
                    else if (type == App_Shapes.ShapeType.Rectangle)
                    {
                        g.DrawRectangle(p, 10, 12, 25, 20);
                    }
                    else if (type == App_Shapes.ShapeType.Circle)
                    {
                        g.DrawEllipse(p, 10, 10, 25, 25);
                    }
                    else if (type == App_Shapes.ShapeType.Arc)
                    {
                        g.DrawArc(p, 10, 10, 25, 25, 180, 180);
                    }
                    else if (type == App_Shapes.ShapeType.TextNode)
                    {
                        g.DrawRectangle(p, 8, 12, 29, 20);
                        using(Brush b = new SolidBrush(iconColor))
                            g.DrawString("A", new Font("Arial", 10, FontStyle.Bold), b, 15, 13);
                    }
                    else if (type == App_Shapes.ShapeType.Text)
                    {
                        using(Brush b = new SolidBrush(iconColor))
                            g.DrawString("T", new Font("Arial", 16, FontStyle.Bold), b, 12, 10);
                    }
                    else if (type == App_Shapes.ShapeType.Image)
                    {
                        g.DrawRectangle(p, 10, 10, 25, 25);
                        g.DrawEllipse(p, 15, 15, 5, 5);
                        g.DrawLine(p, 10, 35, 25, 20);
                    }
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
            btn.Size = new Size(70, 45);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            
            _toolbarPanel.Controls.Add(btn);
            return btn;
        }

        private void SetActiveButton(Button btn)
        {
            if (_activeToolBtn != null)
            {
                _activeToolBtn.BackColor = Color.White;
            }
            _activeToolBtn = btn;
            _activeToolBtn.BackColor = Color.LightSkyBlue;
        }

        // 萬用屬性編輯器 (包含文字內容、字型、外框與文字顏色)
        private void ShowPropertyEditor(App_Shapes.ShapeBase shape)
        {
            using (Form form = new Form())
            {
                form.Text = "編輯圖形與文字屬性";
                form.Size = new Size(320, 360);
                form.StartPosition = FormStartPosition.CenterParent;

                Label lblText = new Label() { Text = "文字內容:", Location = new Point(20, 10) };
                TextBox txtBox = new TextBox() { Text = shape.Text, Multiline = true, Location = new Point(20, 30), Size = new Size(260, 60) };
                
                ComboBox cbFont = new ComboBox() { Location = new Point(20, 100), Width = 150 };
                cbFont.Items.AddRange(new string[] { "Arial", "標楷體", "微軟正黑體", "Times New Roman" });
                cbFont.Text = shape.FontName;
                
                NumericUpDown nudSize = new NumericUpDown() { Location = new Point(180, 100), Width = 100, Minimum = 8, Maximum = 72 };
                nudSize.Value = (decimal)shape.FontSize;
                
                Button btnShapeColor = new Button() { Text = "變更圖形顏色", Location = new Point(20, 150), Width = 260, BackColor = shape.ShapeColor, ForeColor = Color.White };
                btnShapeColor.Click += (s, e) =>
                {
                    using (var cd = new ColorDialog() { Color = shape.ShapeColor })
                    {
                        if (cd.ShowDialog() == DialogResult.OK)
                        {
                            shape.ShapeColor = cd.Color;
                            btnShapeColor.BackColor = cd.Color;
                        }
                    }
                };

                Button btnFontColor = new Button() { Text = "變更文字顏色", Location = new Point(20, 190), Width = 260, BackColor = shape.FontColor, ForeColor = Color.White };
                btnFontColor.Click += (s, e) =>
                {
                    using (var cd = new ColorDialog() { Color = shape.FontColor })
                    {
                        if (cd.ShowDialog() == DialogResult.OK)
                        {
                            shape.FontColor = cd.Color;
                            btnFontColor.BackColor = cd.Color;
                        }
                    }
                };

                Button btnOk = new Button() { Text = "確定", Location = new Point(110, 260), Width = 100 };
                btnOk.Click += (s, e) =>
                {
                    shape.Text = txtBox.Text;
                    shape.FontName = cbFont.Text;
                    shape.FontSize = (float)nudSize.Value;
                    _canvas.Invalidate();
                    form.Close();
                };

                form.Controls.AddRange(new Control[] { lblText, txtBox, cbFont, nudSize, btnShapeColor, btnFontColor, btnOk });
                form.ShowDialog();
            }
        }
    }
}
