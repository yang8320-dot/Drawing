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

        public App_UI_MainForm() { InitializeUI(); }

        private void InitializeUI()
        {
            this.Text = "無邊際智慧畫布繪圖系統 (支援 A4-A1)";
            this.Size = new Size(1300, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; // 讓表單優先攔截按鍵 (支援 ESC)

            _toolbarPanel = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = Color.White, Padding = new Padding(10) };

            int x = 10;
            var btnPointer = CreateIconButton(ref x, App_Shapes.ShapeType.Pointer, "游標 (Esc)");
            CreateIconButton(ref x, App_Shapes.ShapeType.ArrowLine, "箭頭線 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.StraightLine, "直線 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.Rectangle, "矩形 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.Circle, "圓形 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.Arc, "圓弧 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.TextNode, "文字框 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.Text, "純文字 (右鍵改色)");
            CreateIconButton(ref x, App_Shapes.ShapeType.Image, "插入圖片");
            
            SetActiveButton(btnPointer);

            x += 10;
            // 需求8: 畫布尺寸選單
            ComboBox cbPageSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(x, 20), Width = 120 };
            cbPageSize.Items.AddRange(new string[] { "A4 直式", "A4 橫式", "A3 直式", "A3 橫式", "A2 直式", "A2 橫式", "A1 直式", "A1 橫式" });
            cbPageSize.SelectedIndex = 0;
            cbPageSize.SelectedIndexChanged += (s, e) => UpdatePageSize(cbPageSize.Text);
            _toolbarPanel.Controls.Add(cbPageSize); x += 130;

            Button btnSave = CreateTextButton("存檔", ref x, (s,e) => App_SaveLoad.SaveAs(_canvas.Shapes));
            Button btnLoad = CreateTextButton("讀取", ref x, (s,e) => { var ld = App_SaveLoad.Load(); if (ld != null) { _canvas.Shapes = ld; _canvas.Invalidate(); }});
            Button btnPNG = CreateTextButton("匯出 PNG", ref x, async (s,e) => { using (var sfd = new SaveFileDialog { Filter = "PNG|*.png" }) if (sfd.ShowDialog() == DialogResult.OK) { await App_Export.ExportToPngAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName); MessageBox.Show("成功"); }});

            _canvas = new App_CanvasControl { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
            _canvas.OnToolResetRequested += () => { _canvas.CurrentTool = App_Shapes.ShapeType.Pointer; SetActiveButton(btnPointer); };
            _canvas.OnShapeDoubleClicked += ShowPropertyEditor; // 需求5,6: 萬用編輯器

            Panel canvasContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        private void UpdatePageSize(string type)
        {
            // 單位換算：放大10倍以便於繪圖
            switch (type) {
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

        private Button CreateIconButton(ref int x, App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button { Location = new Point(x, 10), Size = new Size(45, 45), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            Color iconColor = Color.FromArgb(80,80,80); // 預設顏色
            new ToolTip().SetToolTip(btn, tooltip);
            
            btn.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right && type != App_Shapes.ShapeType.Pointer && type != App_Shapes.ShapeType.Image) {
                    using (ColorDialog cd = new ColorDialog { Color = iconColor }) {
                        if (cd.ShowDialog() == DialogResult.OK) { iconColor = cd.Color; _canvas.CurrentColor = cd.Color; btn.Invalidate(); }
                    }
                } else if (e.Button == MouseButtons.Left) {
                    _canvas.CurrentTool = type; _canvas.CurrentColor = iconColor; SetActiveButton(btn);
                }
            };

            btn.Paint += (s, e) => {
                Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(iconColor, 2)) {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(15,10), new Point(15,35), new Point(22,25), new Point(30,25) });
                    else if (type == App_Shapes.ShapeType.ArrowLine) { p.CustomEndCap = new CustomLineCap(null, new GraphicsPath(new[] { new PointF(-2,-2), new PointF(0,0), new PointF(2,-2) }, new[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line })); g.DrawLine(p, 10, 35, 35, 10); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 10, 35, 35, 10);
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 10, 12, 25, 20);
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 10, 10, 25, 25);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 10, 10, 25, 25, 180, 180);
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 8, 12, 29, 20); g.DrawString("A", new Font("Arial", 10, FontStyle.Bold), new SolidBrush(iconColor), 15, 13); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 16, FontStyle.Bold), new SolidBrush(iconColor), 12, 10);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 10, 10, 25, 25); g.DrawEllipse(p, 15,15,5,5); g.DrawLine(p, 10,35, 25,20); }
                }
            };
            x += 50; _toolbarPanel.Controls.Add(btn); return btn;
        }

        private Button CreateTextButton(string text, ref int x, EventHandler onClick) {
            Button btn = new Button { Text = text, Location = new Point(x, 10), Size = new Size(70, 45), FlatStyle = FlatStyle.Flat };
            btn.FlatAppearance.BorderColor = Color.LightGray; btn.Click += onClick;
            x += 75; _toolbarPanel.Controls.Add(btn); return btn;
        }

        private void SetActiveButton(Button btn) { if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.White; _activeToolBtn = btn; _activeToolBtn.BackColor = Color.LightSkyBlue; }

        // 需求5, 6: 萬用屬性編輯器 (修改顏色與文字)
        private void ShowPropertyEditor(App_Shapes.ShapeBase shape)
        {
            using (Form form = new Form { Text = "編輯圖形屬性", Size = new Size(320, 350), StartPosition = FormStartPosition.CenterParent })
            {
                TextBox txtBox = new TextBox { Text = shape.Text, Multiline = true, Location = new Point(20, 20), Size = new Size(260, 80) };
                ComboBox cbFont = new ComboBox { Items = { "Arial", "標楷體", "微軟正黑體", "Times New Roman" }, Text = shape.FontName, Location = new Point(20, 120), Width = 150 };
                NumericUpDown nudSize = new NumericUpDown { Value = (decimal)shape.FontSize, Minimum = 8, Maximum = 72, Location = new Point(180, 120), Width = 100 };
                
                Button btnShapeColor = new Button { Text = "變更圖形外框顏色", Location = new Point(20, 170), Width = 260, BackColor = shape.ShapeColor, ForeColor = Color.White };
                Button btnFontColor = new Button { Text = "變更文字顏色", Location = new Point(20, 210), Width = 260, BackColor = shape.FontColor, ForeColor = Color.White };
                
                btnShapeColor.Click += (s, e) => { using (var cd = new ColorDialog { Color = shape.ShapeColor }) if (cd.ShowDialog() == DialogResult.OK) { shape.ShapeColor = cd.Color; btnShapeColor.BackColor = cd.Color; } };
                btnFontColor.Click += (s, e) => { using (var cd = new ColorDialog { Color = shape.FontColor }) if (cd.ShowDialog() == DialogResult.OK) { shape.FontColor = cd.Color; btnFontColor.BackColor = cd.Color; } };

                Button btnOk = new Button { Text = "確定", Location = new Point(110, 270), Width = 100 };
                btnOk.Click += (s, e) => {
                    shape.Text = txtBox.Text; shape.FontName = cbFont.Text; shape.FontSize = (float)nudSize.Value;
                    _canvas.Invalidate(); form.Close();
                };
                form.Controls.AddRange(new Control[] { new Label{Text="圖形內文字:", Location=new Point(20,0)}, txtBox, cbFont, nudSize, btnShapeColor, btnFontColor, btnOk });
                form.ShowDialog();
            }
        }
    }
}
