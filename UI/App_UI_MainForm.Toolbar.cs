using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DrawingApp
{
    // --- 負責主視窗核心、建置上方與左側的工具列、按鈕工廠與匯出 ---
    public partial class App_UI_MainForm : Form
    {
        // ===== 核心 UI 容器 =====
        private FlowLayoutPanel _topBar;
        private FlowLayoutPanel _leftPanel;
        private Panel _rightPanel;
        private TabControl _tabControl;

        // ===== 狀態變數 =====
        private int _tabCounter = 1;
        private bool _isDirty = false;
        
        // ===== 工具列元件 =====
        private Button _activeToolBtn;
        private Button _btnPointer;
        private Button _btnFormatPainter;

        // ===== 屬性面板元件 (供 Properties.cs 使用) =====
        private CheckBox _chkAlignToPage;
        private FlowLayoutPanel _alignmentPanel;
        private FlowLayoutPanel _zIndexPanel;
        private Panel _customPropertiesPanel;
        private GroupBox _gbAppearance;
        private Button _btnShapeColor;
        private ComboBox _cbBrushType;
        private Button _btnFillColor;
        private Button _btnGradientColor;
        private TrackBar _tbStrokeWidth;
        private Label _lblStrokeWidthValue;
        private ComboBox _cbDashStyle;
        private CheckBox _chkShadow;
        private GroupBox _gbText;
        private Button _btnFontColor;
        private ComboBox _cbFontName;
        private NumericUpDown _nudFontSize;
        private CheckBox _chkBold;
        private CheckBox _chkItalic;
        private CheckBox _chkUnderline;
        private ComboBox _cbTextAlign;
        private bool _isUpdatingUI = false;

        // ===== 圖層面板元件 (供 Layers.cs 使用) =====
        private TreeView _tvLayers;
        private bool _isSyncingTree = false;

        // ===== 動態取得當前畫布 =====
        public App_CanvasControl CurrentCanvas
        {
            get
            {
                if (_tabControl != null && _tabControl.SelectedTab != null && _tabControl.SelectedTab.Controls.Count > 0)
                {
                    return _tabControl.SelectedTab.Controls[0] as App_CanvasControl;
                }
                return null;
            }
        }

        // ===== 主視窗建構子 =====
        public App_UI_MainForm()
        {
            // 表單基本設定
            this.Text = "簡易畫線軟體";
            this.Size = new Size(1280, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            // 建立 TabControl (畫布區)
            _tabControl = new TabControl { Dock = DockStyle.Fill };
            _tabControl.SelectedIndexChanged += (s, e) => {
                RefreshPropertyPanel();
                RefreshLayerTree();
                UpdateWindowTitle();
            };

            // 呼叫 Partial Class 中定義的方法來建立面板
            BuildTopBar();
            BuildLeftPanel();
            BuildRightPanel();

            // 加入至主視窗 (注意加入順序影響 Dock 佈局)
            this.Controls.Add(_tabControl);
            this.Controls.Add(_leftPanel);
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_topBar);

            // 預設開啟一張新畫布
            AddNewTab($"畫布 {_tabCounter++}");
        }

        // ===== 核心功能方法 =====
        private void AddNewTab(string title)
        {
            TabPage page = new TabPage(title);
            App_CanvasControl canvas = new App_CanvasControl { Dock = DockStyle.Fill };
            
            // [修正 1]：綁定插入圖片的事件，讓畫布呼叫主視窗的圖片選擇器
            canvas.OnImageInsertRequested += HandleImageInsert;

            // [修正 2]：當畫布有任何指令異動(新增、刪除、復原、重做)時，強制更新圖層面板與標題
            canvas.CmdManager.OnStateChanged += () => {
                RefreshLayerTree();
                _isDirty = true;
                UpdateWindowTitle();
            };

            // 綁定畫布選取變更事件
            canvas.OnSelectionChanged += () => {
                RefreshPropertyPanel();
                SyncLayerTreeSelection();
            };
            
            canvas.OnToolChangedRequested += (toolType) => {
                foreach (Control ctrl in _leftPanel.Controls)
                {
                    if (ctrl is Button btn && btn.Tag is App_Shapes.ShapeType type && type == toolType)
                    {
                        SetActiveButton(btn);
                        break;
                    }
                }
                canvas.CurrentTool = toolType;
            };

            page.Controls.Add(canvas);
            _tabControl.TabPages.Add(page);
            _tabControl.SelectedTab = page;
            
            _isDirty = true;
            UpdateWindowTitle();
        }

        private void SaveAllTabs()
        {
            var project = new DrawProject();
            foreach (TabPage page in _tabControl.TabPages)
            {
                if (page.Controls.Count > 0 && page.Controls[0] is App_CanvasControl canvas)
                {
                    project.Pages.Add(new DrawPage { Title = page.Text, Shapes = canvas.Shapes });
                }
            }
            if (App_SaveLoad.SaveProject(project))
            {
                _isDirty = false;
                UpdateWindowTitle();
            }
        }

        private void LoadTabs()
        {
            var project = App_SaveLoad.LoadProject();
            if (project != null)
            {
                _tabControl.TabPages.Clear();
                foreach (var page in project.Pages)
                {
                    AddNewTab(page.Title);
                    if (CurrentCanvas != null)
                    {
                        CurrentCanvas.Shapes = page.Shapes;
                        CurrentCanvas.Invalidate();
                    }
                }
                _isDirty = false;
                UpdateWindowTitle();
                RefreshLayerTree();
            }
        }

        private void UpdateWindowTitle()
        {
            string dirtyMark = _isDirty ? "*" : "";
            string tabName = _tabControl.SelectedTab != null ? _tabControl.SelectedTab.Text : "無畫布";
            this.Text = $"簡易畫線軟體 - {tabName}{dirtyMark}";
        }

        // ===== 以下為原本 Toolbar 的建置邏輯 =====

        private void BuildTopBar()
        {
            _topBar = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Top, 
                Height = 55, 
                BackColor = Color.FromArgb(245, 246, 248), 
                Padding = new Padding(10, 10, 10, 10),
                WrapContents = false 
            };

            _topBar.Controls.Add(CreateTextButton("➕ 新增畫布", 100, (s, e) => AddNewTab($"畫布 {_tabCounter++}")));
            
            ComboBox cbPageSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Margin = new Padding(0, 7, 8, 0) };
            cbPageSize.Items.AddRange(new string[] { "A4 直式", "A4 橫式", "A3 直式", "A3 橫式", "A2 直式", "A2 橫式", "A1 直式", "A1 橫式" });
            cbPageSize.SelectedIndex = 0;
            cbPageSize.SelectedIndexChanged += (s, e) => { 
                if (CurrentCanvas != null) { 
                    UpdatePageSize(cbPageSize.Text); 
                    _isDirty = true; 
                    UpdateWindowTitle();
                } 
            };
            _topBar.Controls.Add(cbPageSize);

            _topBar.Controls.Add(CreateDivider());
            _topBar.Controls.Add(CreateTextButton("復原", 60, (s, e) => CurrentCanvas?.CmdManager.Undo()));
            _topBar.Controls.Add(CreateTextButton("重做", 60, (s, e) => CurrentCanvas?.CmdManager.Redo()));
            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("放大 +", 65, (s, e) => { if (CurrentCanvas != null) CurrentCanvas.SetZoom(CurrentCanvas.ZoomFactor + 0.2f); }));
            _topBar.Controls.Add(CreateTextButton("縮小 -", 65, (s, e) => { if (CurrentCanvas != null) CurrentCanvas.SetZoom(CurrentCanvas.ZoomFactor - 0.2f); }));
            _topBar.Controls.Add(CreateTextButton("100%", 60, (s, e) => CurrentCanvas?.SetZoom(1.0f)));

            CheckBox chkSnap = new CheckBox { Text = "網格對齊", Checked = true, AutoSize = true, Margin = new Padding(5, 9, 10, 0) };
            chkSnap.CheckedChanged += (s, e) => { if (CurrentCanvas != null) { CurrentCanvas.SnapToGrid = chkSnap.Checked; CurrentCanvas.Invalidate(); } };
            _topBar.Controls.Add(chkSnap);

            CheckBox chkRuler = new CheckBox { Text = "顯示尺規", Checked = true, AutoSize = true, Margin = new Padding(0, 9, 15, 0) };
            chkRuler.CheckedChanged += (s, e) => { if (CurrentCanvas != null) { CurrentCanvas.ShowRulers = chkRuler.Checked; CurrentCanvas.Invalidate(); } };
            _topBar.Controls.Add(chkRuler);

            _topBar.Controls.Add(CreateDivider());
            _topBar.Controls.Add(CreateTextButton("存檔", 60, (s, e) => SaveAllTabs()));
            _topBar.Controls.Add(CreateTextButton("讀取", 60, (s, e) => LoadTabs()));
            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("匯出 PNG", 90, async (s, e) => {
                if (CurrentCanvas == null) return;
                using (var sfd = new SaveFileDialog { Filter = "PNG 圖片|*.png" })
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToPngAsync(CurrentCanvas.GetTransparentCanvasRender(), sfd.FileName);
                        MessageBox.Show("當前畫布 PNG 匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
            }));
            
            _topBar.Controls.Add(CreateTextButton("匯出 PDF", 90, (s, e) => { if (CurrentCanvas != null) ShowPdfExportDialog(); }));
            _topBar.Controls.Add(CreateTextButton("匯出 SVG", 90, async (s, e) => {
                if (CurrentCanvas == null) return;
                using (var sfd = new SaveFileDialog { Filter = "SVG 向量圖|*.svg" })
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToSvgAsync(CurrentCanvas.Shapes, CurrentCanvas.PageSize, sfd.FileName);
                        MessageBox.Show("當前畫布 SVG 匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
            }));
            
            Label lblZenMode = new Label { Text = "💡 按 F11 進入全螢幕", ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(20, 10, 0, 0) };
            _topBar.Controls.Add(lblZenMode);
        }

        private void BuildLeftPanel()
        {
            _leftPanel = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 65, BackColor = Color.FromArgb(230, 233, 237), Padding = new Padding(5), AutoScroll = true };
            
            _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標\n快捷鍵: V\n(可框選、旋轉、縮放)");
            SetActiveButton(_btnPointer);
            
            CreateToolButton(App_Shapes.ShapeType.HandPan, "拖曳畫布 (Hand Tool)\n快捷鍵: H 或按住 Space\n(可用滑鼠左鍵直接平移畫面)");
            
            _btnFormatPainter = CreateToolButton(App_Shapes.ShapeType.FormatPainter, "格式刷\n(先選取圖形，點擊此按鈕後，再點擊其他圖形以套用格式)");
            _btnFormatPainter.Click += (s, e) => {
                if (CurrentCanvas != null && CurrentCanvas.SelectedShapes.Count > 0)
                {
                    CurrentCanvas.FormatSourceShape = CurrentCanvas.SelectedShapes[0];
                    CurrentCanvas.CurrentTool = App_Shapes.ShapeType.FormatPainter;
                    SetActiveButton(_btnFormatPainter);
                }
                else
                {
                    MessageBox.Show("請先選取一個要複製格式的圖形！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    if (CurrentCanvas != null) CurrentCanvas.CurrentTool = App_Shapes.ShapeType.Pointer;
                    SetActiveButton(_btnPointer);
                }
            };

            CreateToolButton(App_Shapes.ShapeType.ArrowLine, "智慧箭頭線");
            CreateToolButton(App_Shapes.ShapeType.StraightLine, "智慧直線");
            CreateToolButton(App_Shapes.ShapeType.OrthogonalLine, "90度折線 (智慧避障)\n快捷鍵: L");

            CreateToolButton(App_Shapes.ShapeType.Rectangle, "矩形\n快捷鍵: R");
            CreateToolButton(App_Shapes.ShapeType.RoundedRectangle, "圓角矩形"); 
            CreateToolButton(App_Shapes.ShapeType.Circle, "圓形");
            CreateToolButton(App_Shapes.ShapeType.Arc, "圓弧");
            CreateToolButton(App_Shapes.ShapeType.Diamond, "菱形");
            CreateToolButton(App_Shapes.ShapeType.Triangle, "三角形");
            CreateToolButton(App_Shapes.ShapeType.Pentagon, "五邊形"); 
            CreateToolButton(App_Shapes.ShapeType.Hexagon, "六邊形"); 
            CreateToolButton(App_Shapes.ShapeType.Star, "星形"); 
            CreateToolButton(App_Shapes.ShapeType.Cloud, "雲朵"); 

            CreateToolButton(App_Shapes.ShapeType.TextNode, "文字框\n快捷鍵: T");
            CreateToolButton(App_Shapes.ShapeType.Text, "純文字");
            CreateToolButton(App_Shapes.ShapeType.Image, "插入圖片");
            CreateToolButton(App_Shapes.ShapeType.Freehand, "自由畫筆\n快捷鍵: P");
            CreateToolButton(App_Shapes.ShapeType.BezierPen, "鋼筆工具 (貝茲曲線)\n快捷鍵: B\n(點擊或拖曳拉出控制桿)");
        }

        private Button CreateTextButton(string text, int width, EventHandler onClick)
        {
            Button btn = new Button 
            { 
                Text = text, 
                Size = new Size(width, 35), 
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 238, 255); 
            btn.Click += onClick;
            return btn;
        }

        private Panel CreateDivider() => new Panel { Width = 1, Height = 35, BackColor = Color.FromArgb(200, 200, 200), Margin = new Padding(4, 0, 12, 0) };

        private void SetActiveButton(Button btn)
        {
            if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.Transparent;
            _activeToolBtn = btn;
            _activeToolBtn.BackColor = Color.LightSkyBlue;
        }

        private Button CreateToolButton(App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button { Size = new Size(45, 45), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(2, 2, 2, 8) };
            btn.FlatAppearance.BorderSize = 0;
            btn.Tag = type; 
            Color iconColor = Color.FromArgb(80, 80, 80);
            
            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            
            Point mouseDownLocation = Point.Empty;

            btn.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) mouseDownLocation = e.Location; };

            btn.MouseMove += (s, e) => {
                if (e.Button == MouseButtons.Left && mouseDownLocation != Point.Empty)
                {
                    if (Math.Abs(e.X - mouseDownLocation.X) > 5 || Math.Abs(e.Y - mouseDownLocation.Y) > 5)
                    {
                        if (type != App_Shapes.ShapeType.FormatPainter) 
                            btn.DoDragDrop(type, DragDropEffects.Copy);
                        mouseDownLocation = Point.Empty;
                    }
                }
            };

            btn.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Left && type != App_Shapes.ShapeType.FormatPainter)
                {
                    if (CurrentCanvas != null) CurrentCanvas.CurrentTool = type;
                    SetActiveButton(btn);
                }
            };

            btn.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(iconColor, 2))
                {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(14, 12), new Point(14, 32), new Point(20, 24), new Point(27, 24) });
                    else if (type == App_Shapes.ShapeType.HandPan) 
                    { 
                        g.DrawLine(p, 16, 26, 16, 14); g.DrawArc(p, 14, 12, 4, 4, 180, 180); 
                        g.DrawLine(p, 20, 26, 20, 10); g.DrawArc(p, 18, 8, 4, 4, 180, 180); 
                        g.DrawLine(p, 24, 26, 24, 12); g.DrawArc(p, 22, 10, 4, 4, 180, 180); 
                        g.DrawLine(p, 28, 26, 28, 16); g.DrawArc(p, 26, 14, 4, 4, 180, 180); 
                        g.DrawArc(p, 14, 26, 18, 12, 0, 180);
                    }
                    else if (type == App_Shapes.ShapeType.FormatPainter) 
                    {
                        g.FillRectangle(new SolidBrush(iconColor), 14, 10, 16, 8);
                        g.DrawRectangle(p, 16, 18, 12, 4);
                        g.DrawLine(p, 22, 22, 22, 34);
                    }
                    else if (type == App_Shapes.ShapeType.ArrowLine) { g.DrawLine(p, 10, 32, 32, 10); g.DrawLine(p, 22, 10, 32, 10); g.DrawLine(p, 32, 10, 32, 20); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 10, 32, 32, 10);
                    else if (type == App_Shapes.ShapeType.OrthogonalLine) g.DrawLines(p, new PointF[] { new PointF(10, 32), new PointF(22, 32), new PointF(22, 12), new PointF(32, 12) });
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 10, 12, 24, 20);
                    else if (type == App_Shapes.ShapeType.RoundedRectangle) 
                    {
                        using(GraphicsPath gp = new GraphicsPath()) {
                            gp.AddArc(10, 12, 6, 6, 180, 90); gp.AddArc(28, 12, 6, 6, 270, 90);
                            gp.AddArc(28, 26, 6, 6, 0, 90); gp.AddArc(10, 26, 6, 6, 90, 90);
                            gp.CloseFigure(); g.DrawPath(p, gp);
                        }
                    }
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 10, 10, 24, 24);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 10, 10, 24, 24, 180, 180);
                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(22, 8), new PointF(36, 22), new PointF(22, 36), new PointF(8, 22) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(22, 10), new PointF(34, 32), new PointF(10, 32) });
                    else if (type == App_Shapes.ShapeType.Pentagon) 
                    {
                        PointF[] pts = new PointF[5];
                        for (int i = 0; i < 5; i++) {
                            double a = Math.PI / 2 + (i * 2 * Math.PI / 5);
                            pts[i] = new PointF(22 - (float)(12 * Math.Cos(a)), 22 - (float)(12 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Hexagon) 
                    {
                        PointF[] pts = new PointF[6];
                        for (int i = 0; i < 6; i++) {
                            double a = i * Math.PI / 3;
                            pts[i] = new PointF(22 + (float)(12 * Math.Cos(a)), 22 + (float)(12 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Star) 
                    {
                        PointF[] pts = new PointF[10];
                        for (int i = 0; i < 10; i++) {
                            double a = Math.PI / 2 + (i * Math.PI / 5);
                            float r = (i % 2 == 0) ? 14 : 6;
                            pts[i] = new PointF(22 - (float)(r * Math.Cos(a)), 22 - (float)(r * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Cloud) 
                    {
                        g.DrawArc(p, 10, 18, 10, 10, 90, 180); g.DrawArc(p, 14, 12, 12, 12, 180, 180);
                        g.DrawArc(p, 22, 14, 12, 12, 270, 180); g.DrawArc(p, 24, 20, 10, 10, 0, 180);
                        g.DrawLine(p, 15, 28, 29, 28);
                    }
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 8, 12, 28, 20); g.DrawString("A", new Font("Arial", 10), new SolidBrush(iconColor), 14, 14); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), new SolidBrush(iconColor), 12, 10);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 10, 10, 24, 24); g.DrawEllipse(p, 14, 14, 4, 4); g.DrawLine(p, 10, 34, 24, 20); }
                    else if (type == App_Shapes.ShapeType.Freehand) { g.DrawBezier(p, new Point(10, 22), new Point(20, 10), new Point(25, 34), new Point(35, 22)); }
                    else if (type == App_Shapes.ShapeType.BezierPen) 
                    { 
                        g.DrawLine(p, 22, 10, 16, 24); g.DrawLine(p, 22, 10, 28, 24); 
                        g.DrawLine(p, 16, 24, 22, 34); g.DrawLine(p, 28, 24, 22, 34);
                        g.FillEllipse(Brushes.White, 20, 8, 4, 4); g.DrawEllipse(p, 20, 8, 4, 4); 
                    }
                }
            };
            _leftPanel.Controls.Add(btn);
            return btn;
        }

        private void HandleImageInsert(PointF pt)
        {
            if (CurrentCanvas == null) return;
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "圖片檔案|*.jpg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    using (Bitmap originalImg = new Bitmap(ofd.FileName))
                    {
                        Bitmap finalImg = originalImg;
                        int maxW = 1920, maxH = 1080;
                        
                        if (originalImg.Width > maxW || originalImg.Height > maxH)
                        {
                            float ratioX = (float)maxW / originalImg.Width;
                            float ratioY = (float)maxH / originalImg.Height;
                            float ratio = Math.Min(ratioX, ratioY);
                            
                            int newW = (int)(originalImg.Width * ratio);
                            int newH = (int)(originalImg.Height * ratio);
                            finalImg = new Bitmap(originalImg, newW, newH);
                        }
                        
                        var imgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, finalImg);
                        CurrentCanvas.CmdManager.ExecuteCommand(new AddShapeCommand(CurrentCanvas.Shapes, imgShape));
                        if (finalImg != originalImg) finalImg.Dispose();
                    }
                }
            }
        }

        private void ShowPdfExportDialog()
        {
            if (CurrentCanvas == null) return;
            using (Form pdfForm = new Form { Text = "選擇 PDF 尺寸", Size = new Size(300, 200), StartPosition = FormStartPosition.CenterParent })
            {
                ComboBox cbSize = new ComboBox { Location = new Point(20, 30) };
                cbSize.Items.AddRange(new string[] { "A4", "A3", "A2", "A1" });
                cbSize.SelectedIndex = 0;

                ComboBox cbOri = new ComboBox { Location = new Point(150, 30) };
                cbOri.Items.AddRange(new string[] { "直式", "橫式" });
                cbOri.SelectedIndex = 0;

                Button btnOk = new Button { Text = "匯出", Location = new Point(100, 100) };
                btnOk.Click += async (sender, ev) => {
                    using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF 文件|*.pdf" })
                    {
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            await App_Export.ExportToPdfAsync(CurrentCanvas.GetTransparentCanvasRender(), sfd.FileName, cbOri.SelectedIndex == 1);
                            MessageBox.Show("當前畫布 PDF 匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (CurrentCanvas == null) return;
            switch (type)
            {
                case "A4 直式": CurrentCanvas.PageSize = new SizeF(2100, 2970); break;
                case "A4 橫式": CurrentCanvas.PageSize = new SizeF(2970, 2100); break;
                case "A3 直式": CurrentCanvas.PageSize = new SizeF(2970, 4200); break;
                case "A3 橫式": CurrentCanvas.PageSize = new SizeF(4200, 2970); break;
                case "A2 直式": CurrentCanvas.PageSize = new SizeF(4200, 5940); break;
                case "A2 橫式": CurrentCanvas.PageSize = new SizeF(5940, 4200); break;
                case "A1 直式": CurrentCanvas.PageSize = new SizeF(5940, 8410); break;
                case "A1 橫式": CurrentCanvas.PageSize = new SizeF(8410, 5940); break;
            }
        }
    }
}
