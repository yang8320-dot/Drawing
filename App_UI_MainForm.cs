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
            this.Text = "終極專業繪圖系統 (完整保留版)";
            this.Size = new Size(1500, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _toolbarPanel = new Panel();
            _toolbarPanel.Dock = DockStyle.Top;
            _toolbarPanel.Height = 70;
            _toolbarPanel.BackColor = Color.White;

            int currentX = 10;
            
            // 恢復所有繪圖按鈕與右鍵改色
            var btnPointer = CreateIconButton(currentX, App_Shapes.ShapeType.Pointer, "游標 (Esc)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.ArrowLine, "箭頭線 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.StraightLine, "直線 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.OrthogonalLine, "90度折線 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Rectangle, "矩形 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Circle, "圓形 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Arc, "圓弧 (右鍵改色)"); currentX += 45; // 恢復圓弧
            CreateIconButton(currentX, App_Shapes.ShapeType.Diamond, "菱形 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Triangle, "三角形 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.TextNode, "文字框 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Text, "純文字 (右鍵改色)"); currentX += 45;
            CreateIconButton(currentX, App_Shapes.ShapeType.Image, "插入圖片"); currentX += 50;
            
            SetActiveButton(btnPointer);

            // 恢復 A4-A1 畫布尺寸選擇
            ComboBox cbPageSize = new ComboBox();
            cbPageSize.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPageSize.Location = new Point(currentX, 20);
            cbPageSize.Width = 100;
            cbPageSize.Items.AddRange(new string[] { "A4 直式", "A4 橫式", "A3 直式", "A3 橫式", "A2 直式", "A2 橫式", "A1 直式", "A1 橫式" });
            cbPageSize.SelectedIndex = 0;
            cbPageSize.SelectedIndexChanged += (s, e) => UpdatePageSize(cbPageSize.Text);
            _toolbarPanel.Controls.Add(cbPageSize);
            currentX += 110;

            _canvas = new App_CanvasControl();
            _canvas.Dock = DockStyle.Fill;
            
            Button btnUndo = CreateTextButton("復原", currentX, (s, e) => _canvas.CmdManager.Undo()); currentX += 50;
            Button btnRedo = CreateTextButton("重做", currentX, (s, e) => _canvas.CmdManager.Redo()); currentX += 50;
            
            Button btnZoomIn = CreateTextButton("放大+", currentX, (s, e) => _canvas.SetZoom(_canvas.ZoomFactor + 0.2f)); currentX += 55;
            Button btnZoomOut = CreateTextButton("縮小-", currentX, (s, e) => _canvas.SetZoom(_canvas.ZoomFactor - 0.2f)); currentX += 55;
            Button btnZoomReset = CreateTextButton("100%", currentX, (s, e) => _canvas.SetZoom(1.0f)); currentX += 55;

            CreateTextButton("存檔", currentX, (s, e) => App_SaveLoad.SaveAs(_canvas.Shapes)); currentX += 50;
            CreateTextButton("讀取", currentX, (s, e) => {
                var data = App_SaveLoad.Load();
                if (data != null) { _canvas.Shapes = data; _canvas.Invalidate(); }
            }); currentX += 50;

            // 恢復 PNG / PDF 匯出對話框功能
            CreateTextButton("匯出 PNG", currentX, async (s, e) => {
                using (var sfd = new SaveFileDialog() { Filter = "PNG|*.png" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToPngAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName);
                        MessageBox.Show("PNG 匯出成功！");
                    }
                }
            }); currentX += 80;

            CreateTextButton("匯出 PDF", currentX, (s, e) => ShowPdfExportDialog()); currentX += 80;

            // 綁定事件
            _canvas.OnToolResetRequested += () => { _canvas.CurrentTool = App_Shapes.ShapeType.Pointer; SetActiveButton(btnPointer); };
            _canvas.OnShapeDoubleClicked += ShowPropertyEditor;
            _canvas.OnImageInsertRequested += HandleImageInsert; // 恢復插入圖片對話框

            Panel canvasContainer = new Panel();
            canvasContainer.Dock = DockStyle.Fill;
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        // 恢復插入圖片事件處理邏輯
        private void HandleImageInsert(PointF pt)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "圖片檔案|*.jpg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Bitmap img = new Bitmap(ofd.FileName);
                    var imgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, img);
                    _canvas.CmdManager.ExecuteCommand(new AddShapeCommand(_canvas.Shapes, imgShape));
                    _canvas.Invalidate();
                }
            }
        }

        // 恢復 PDF 匯出視窗
        private void ShowPdfExportDialog()
        {
            using (Form pdfForm = new Form() { Text = "選擇 PDF 尺寸", Size = new Size(300, 200), StartPosition = FormStartPosition.CenterParent })
            {
                ComboBox cbSize = new ComboBox() { Location = new Point(20, 30) };
                cbSize.Items.AddRange(new string[] { "A4", "A3", "A2", "A1" });
                cbSize.SelectedIndex = 0;

                ComboBox cbOri = new ComboBox() { Location = new Point(150, 30) };
                cbOri.Items.AddRange(new string[] { "直式", "橫式" });
                cbOri.SelectedIndex = 0;

                Button btnOk = new Button() { Text = "匯出", Location = new Point(100, 100) };
                btnOk.Click += async (sender, ev) => {
                    using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "PDF 文件|*.pdf" })
                    {
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            await App_Export.ExportToPdfAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName, cbOri.SelectedIndex == 1);
                            MessageBox.Show("PDF 匯出成功！");
                        }
                    }
                    pdfForm.Close();
                };
                pdfForm.Controls.AddRange(new Control[] { cbSize, cbOri, btnOk });
                pdfForm.ShowDialog();
            }
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
            btn.Size = new Size(40, 40);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            
            Color iconColor = Color.FromArgb(80, 80, 80);
            new ToolTip().SetToolTip(btn, tooltip);
            
            // 恢復右鍵選色功能
            btn.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right && type != App_Shapes.ShapeType.Pointer && type != App_Shapes.ShapeType.Image)
                {
                    using (ColorDialog cd = new ColorDialog() { Color = iconColor })
                    {
                        if (cd.ShowDialog() == DialogResult.OK)
                        {
                            iconColor = cd.Color;
                            _canvas.CurrentColor = cd.Color;
                            btn.Invalidate(); 
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

            btn.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(iconColor, 2))
                {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(12, 10), new Point(12, 30), new Point(18, 22), new Point(25, 22) });
                    else if (type == App_Shapes.ShapeType.ArrowLine) { g.DrawLine(p, 8, 30, 30, 8); g.DrawLine(p, 20, 8, 30, 8); g.DrawLine(p, 30, 8, 30, 18); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 8, 30, 30, 8);
                    else if (type == App_Shapes.ShapeType.OrthogonalLine) g.DrawLines(p, new PointF[] { new PointF(8, 30), new PointF(20, 30), new PointF(20, 10), new PointF(30, 10) });
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 8, 10, 24, 20);
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 8, 8, 24, 24);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 8, 8, 24, 24, 180, 180);
                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(20, 6), new PointF(34, 20), new PointF(20, 34), new PointF(6, 20) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(20, 8), new PointF(32, 30), new PointF(8, 30) });
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 6, 10, 28, 20); g.DrawString("A", new Font("Arial", 10), new SolidBrush(iconColor), 12, 12); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), new SolidBrush(iconColor), 10, 8);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 8, 8, 24, 24); g.DrawEllipse(p, 12, 12, 4, 4); g.DrawLine(p, 8, 32, 22, 18); }
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
            btn.Size = new Size(50, 40);
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

        private void ShowPropertyEditor(App_Shapes.ShapeBase shape)
        {
            using (Form form = new Form())
            {
                form.Text = "進階圖形與文字屬性";
                form.Size = new Size(340, 420);
                form.StartPosition = FormStartPosition.CenterParent;

                TextBox txtBox = new TextBox() { Text = shape.Text, Multiline = true, Location = new Point(20, 20), Size = new Size(280, 60) };
                
                ComboBox cbFont = new ComboBox() { Location = new Point(20, 90), Width = 150 };
                cbFont.Items.AddRange(new string[] { "Arial", "標楷體", "微軟正黑體" });
                cbFont.Text = shape.FontName;
                NumericUpDown nudFontSize = new NumericUpDown() { Location = new Point(180, 90), Width = 120, Minimum = 8, Maximum = 72, Value = (decimal)shape.FontSize };
                
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
                    
                    _canvas.Invalidate();
                    form.Close();
                };

                form.Controls.AddRange(new Control[] { txtBox, cbFont, nudFontSize, lblStroke, nudStroke, lblDash, cbDash, btnShapeColor, btnFontColor, btnOk });
                form.ShowDialog();
            }
        }
    }
}
