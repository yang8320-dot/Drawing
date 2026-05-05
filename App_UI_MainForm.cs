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
            this.Text = "進階無邊際畫布繪圖系統";
            this.Size = new Size(1300, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);

            _toolbarPanel = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = Color.White, Padding = new Padding(10) };

            int x = 10;
            var btnPointer = CreateIconButton(ref x, App_Shapes.ShapeType.Pointer, "游標");
            var btnLineArr = CreateIconButton(ref x, App_Shapes.ShapeType.ArrowLine, "箭頭線");
            var btnLineStr = CreateIconButton(ref x, App_Shapes.ShapeType.StraightLine, "直線");
            var btnRect = CreateIconButton(ref x, App_Shapes.ShapeType.Rectangle, "矩形");
            var btnCircle = CreateIconButton(ref x, App_Shapes.ShapeType.Circle, "圓形");
            var btnArc = CreateIconButton(ref x, App_Shapes.ShapeType.Arc, "圓弧");
            var btnNode = CreateIconButton(ref x, App_Shapes.ShapeType.TextNode, "文字框");
            var btnText = CreateIconButton(ref x, App_Shapes.ShapeType.Text, "純文字");
            var btnImg = CreateIconButton(ref x, App_Shapes.ShapeType.Image, "插入圖");
            
            SetActiveButton(btnPointer);

            x += 20; // 間距
            Button btnColor = CreateTextButton("顏色", ref x, btnColor_Click);
            Button btnPNG = CreateTextButton("匯出 PNG", ref x, btnSavePNG_Click);
            Button btnPDF = CreateTextButton("匯出 PDF", ref x, btnSavePDF_Click);
            Button btnSave = CreateTextButton("另存新檔", ref x, (s,e) => App_SaveLoad.SaveAs(_canvas.Shapes));
            Button btnLoad = CreateTextButton("讀取檔案", ref x, (s,e) => {
                var loaded = App_SaveLoad.Load();
                if (loaded != null) { _canvas.Shapes = loaded; _canvas.Invalidate(); }
            });

            _canvas = new App_CanvasControl { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
            
            // 綁定事件
            _canvas.OnTextShapeDoubleClicked += ShowTextEditor;
            _canvas.OnImageInsertRequested += HandleImageInsert;

            Panel canvasContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            canvasContainer.Controls.Add(_canvas);

            this.Controls.Add(canvasContainer);
            this.Controls.Add(_toolbarPanel);
        }

        private Button CreateIconButton(ref int x, App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button { Location = new Point(x, 10), Size = new Size(45, 45), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            ToolTip tt = new ToolTip(); tt.SetToolTip(btn, tooltip);
            
            btn.Click += (s, e) => { _canvas.CurrentTool = type; SetActiveButton(btn); };
            btn.Paint += (s, e) => {
                Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(80,80,80), 2)) {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(15,10), new Point(15,35), new Point(22,25), new Point(30,25) });
                    else if (type == App_Shapes.ShapeType.ArrowLine) { p.CustomEndCap = new CustomLineCap(null, new GraphicsPath(new[] { new PointF(-2,-2), new PointF(0,0), new PointF(2,-2) }, new[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line })); g.DrawLine(p, 10, 35, 35, 10); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 10, 35, 35, 10);
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 10, 12, 25, 20);
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 10, 10, 25, 25);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 10, 10, 25, 25, 180, 180);
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 8, 12, 29, 20); g.DrawString("A", new Font("Arial", 10, FontStyle.Bold), Brushes.DimGray, 15, 13); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 16, FontStyle.Bold), Brushes.DimGray, 12, 10);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 10, 10, 25, 25); g.DrawEllipse(p, 15,15,5,5); g.DrawLine(p, 10,35, 25,20); }
                }
            };
            x += 50;
            _toolbarPanel.Controls.Add(btn);
            return btn;
        }

        private Button CreateTextButton(string text, ref int x, EventHandler onClick)
        {
            Button btn = new Button { Text = text, Location = new Point(x, 10), Size = new Size(80, 45), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            x += 85;
            _toolbarPanel.Controls.Add(btn);
            return btn;
        }

        private void SetActiveButton(Button btn) { if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.White; _activeToolBtn = btn; _activeToolBtn.BackColor = Color.LightSkyBlue; }

        private void btnColor_Click(object sender, EventArgs e) { using (ColorDialog cd = new ColorDialog()) if (cd.ShowDialog() == DialogResult.OK) _canvas.CurrentColor = cd.Color; }

        private async void btnSavePNG_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG 圖片|*.png" })
                if (sfd.ShowDialog() == DialogResult.OK) { await App_Export.ExportToPngAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName); MessageBox.Show("PNG 匯出成功！"); }
        }

        // PDF 尺寸選擇對話框 (需求 11)
        private void btnSavePDF_Click(object sender, EventArgs e)
        {
            using (Form pdfForm = new Form { Text = "選擇 PDF 尺寸", Size = new Size(300, 200), StartPosition = FormStartPosition.CenterParent })
            {
                ComboBox cbSize = new ComboBox { Items = { "A4", "A3", "A2", "A1" }, SelectedIndex = 0, Location = new Point(20, 30) };
                ComboBox cbOri = new ComboBox { Items = { "直式", "橫式" }, SelectedIndex = 0, Location = new Point(150, 30) };
                Button btnOk = new Button { Text = "匯出", Location = new Point(100, 100) };
                btnOk.Click += async (s, ev) => {
                    using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF 文件|*.pdf" })
                        if (sfd.ShowDialog() == DialogResult.OK) {
                            await App_Export.ExportToPdfAsync(_canvas.GetTransparentCanvasRender(), sfd.FileName, cbOri.SelectedIndex == 1);
                            MessageBox.Show("PDF 匯出成功！");
                        }
                    pdfForm.Close();
                };
                pdfForm.Controls.AddRange(new Control[] { cbSize, cbOri, btnOk });
                pdfForm.ShowDialog();
            }
        }

        // 文字編輯器 (需求 9, 10)
        private void ShowTextEditor(App_Shapes.TextNodeShape txtShape)
        {
            using (Form form = new Form { Text = "編輯文字", Size = new Size(300, 250), StartPosition = FormStartPosition.CenterParent })
            {
                TextBox txtBox = new TextBox { Text = txtShape.Text, Multiline = true, Location = new Point(20, 20), Size = new Size(240, 80) };
                ComboBox cbFont = new ComboBox { Items = { "Arial", "標楷體", "微軟正黑體", "Times New Roman" }, Text = txtShape.FontName, Location = new Point(20, 110) };
                NumericUpDown nudSize = new NumericUpDown { Value = (decimal)txtShape.FontSize, Minimum = 8, Maximum = 72, Location = new Point(150, 110) };
                Button btnOk = new Button { Text = "確定", Location = new Point(100, 160) };
                btnOk.Click += (s, e) => {
                    txtShape.Text = txtBox.Text;
                    txtShape.FontName = cbFont.Text;
                    txtShape.FontSize = (float)nudSize.Value;
                    form.Close();
                };
                form.Controls.AddRange(new Control[] { txtBox, cbFont, nudSize, btnOk });
                form.ShowDialog();
            }
        }

        // 插入圖片 (需求 14)
        private void HandleImageInsert(PointF pt)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "圖片檔案|*.jpg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Bitmap img = new Bitmap(ofd.FileName);
                    var imgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, img);
                    _canvas.Shapes.Add(imgShape);
                    _canvas.Invalidate();
                }
            }
        }
    }
}
