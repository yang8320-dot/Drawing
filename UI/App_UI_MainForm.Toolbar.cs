using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace DrawingApp
{
    public partial class App_UI_MainForm : Form
    {
        private FlowLayoutPanel _topBar;
        private Panel _leftPanelContainer; 
        private FlowLayoutPanel _leftPanel; 
        private Panel _rightPanel;
        private TabControl _tabControl;

        private int _tabCounter = 1;
        private bool _isDirty = false;
        
        private Button _activeToolBtn;
        private Button _btnPointer;
        private Button _btnFormatPainter;

        // 面板展開狀態記憶
        private Dictionary<string, bool> _panelStates = new Dictionary<string, bool>();

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

        private TreeView _tvLayers;
        private bool _isSyncingTree = false;

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

        public App_UI_MainForm()
        {
            this.Text = "簡易畫線軟體";
            this.Size = new Size(1280, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            LoadPanelStates(); 

            _tabControl = new TabControl { Dock = DockStyle.Fill };
            _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabControl.Padding = new Point(15, 6);
            _tabControl.DrawItem += TabControl_DrawItem;
            _tabControl.MouseDown += TabControl_MouseDown;
            _tabControl.ContextMenuStrip = CreateTabContextMenu();

            _tabControl.SelectedIndexChanged += (s, e) => {
                RefreshPropertyPanel();
                RefreshLayerTree();
                UpdateWindowTitle();
            };

            BuildTopBar();
            BuildLeftPanel(); 
            BuildRightPanel();

            this.Controls.Add(_tabControl);
            this.Controls.Add(_leftPanelContainer); 
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_topBar);

            this.FormClosing += (s, e) => SavePanelStates(); 

            AddNewTab($"畫布 {_tabCounter++}");
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabPage = _tabControl.TabPages[e.Index];
            var tabRect = _tabControl.GetTabRect(e.Index);
            tabRect.Inflate(-2, -2);
            
            if (e.State == DrawItemState.Selected)
                e.Graphics.FillRectangle(Brushes.White, _tabControl.GetTabRect(e.Index));
            else
                e.Graphics.FillRectangle(SystemBrushes.Control, _tabControl.GetTabRect(e.Index));

            TextRenderer.DrawText(e.Graphics, tabPage.Text, tabPage.Font, tabRect, e.State == DrawItemState.Selected ? Color.Black : Color.DimGray, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var xRect = new Rectangle(tabRect.Right - 15, tabRect.Top + (tabRect.Height - 12) / 2, 12, 12);
            using (var p = new Pen(Color.Gray, 2))
            {
                e.Graphics.DrawLine(p, xRect.Left, xRect.Top, xRect.Right, xRect.Bottom);
                e.Graphics.DrawLine(p, xRect.Right, xRect.Top, xRect.Left, xRect.Bottom);
            }
        }

        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _tabControl.TabPages.Count; i++)
                {
                    var tabRect = _tabControl.GetTabRect(i);
                    tabRect.Inflate(-2, -2);
                    var xRect = new Rectangle(tabRect.Right - 15, tabRect.Top + (tabRect.Height - 12) / 2, 12, 12);
                    if (xRect.Contains(e.Location))
                    {
                        CloseTab(i);
                        return;
                    }
                }
            }
        }

        private ContextMenuStrip CreateTabContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("關閉此畫布分頁", null, (s, e) => {
                if (_tabControl.SelectedIndex >= 0) CloseTab(_tabControl.SelectedIndex);
            });
            return menu;
        }

        private void CloseTab(int index)
        {
            if (_tabControl.TabPages.Count > 1)
                _tabControl.TabPages.RemoveAt(index);
            else
                MessageBox.Show("這是最後一個畫布，無法關閉！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddNewTab(string title)
        {
            TabPage page = new TabPage(title);
            App_CanvasControl canvas = new App_CanvasControl { Dock = DockStyle.Fill };
            
            canvas.OnImageInsertRequested += HandleImageInsert;
            canvas.CmdManager.OnStateChanged += () => { RefreshLayerTree(); _isDirty = true; UpdateWindowTitle(); };
            canvas.OnSelectionChanged += () => { RefreshPropertyPanel(); SyncLayerTreeSelection(); };
            
            canvas.OnToolChangedRequested += (toolType) => {
                foreach (Control group in _leftPanel.Controls)
                {
                    if (group is FlowLayoutPanel groupContainer)
                    {
                        foreach(Control child in groupContainer.Controls)
                        {
                            if (child is FlowLayoutPanel contentPanel)
                            {
                                foreach(Control btnCtrl in contentPanel.Controls)
                                {
                                    if (btnCtrl is Button btn && btn.Tag is App_Shapes.ShapeType type && type == toolType)
                                    {
                                        SetActiveButton(btn);
                                        break;
                                    }
                                }
                            }
                        }
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
                    project.Pages.Add(new DrawPage { Title = page.Text, Shapes = canvas.Shapes });
            }
            if (App_SaveLoad.SaveProject(project))
            {
                _isDirty = false;
                UpdateWindowTitle();
            }
        }

        private void LoadTabs()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "所有支援格式 (*.draw;*.svg)|*.draw;*.svg|Draw Project (*.draw)|*.draw|SVG 向量圖 (*.svg)|*.svg", InitialDirectory = AppDomain.CurrentDomain.BaseDirectory })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string ext = Path.GetExtension(ofd.FileName).ToLower();
                    if (ext == ".svg")
                    {
                        var shapes = App_SvgParser.ParseSvg(ofd.FileName);
                        if (shapes != null && shapes.Count > 0)
                        {
                            AddNewTab(Path.GetFileNameWithoutExtension(ofd.FileName));
                            CurrentCanvas.Shapes = shapes;
                            CurrentCanvas.Invalidate();
                            _isDirty = true;
                            UpdateWindowTitle();
                            RefreshLayerTree();
                        }
                        else
                        {
                            MessageBox.Show("無法解析此 SVG 檔案或檔案內無支援的圖形格式！", "讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        var project = App_SaveLoad.LoadProject(ofd.FileName);
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
                }
            }
        }

        private void UpdateWindowTitle()
        {
            string dirtyMark = _isDirty ? "*" : "";
            string tabName = _tabControl.SelectedTab != null ? _tabControl.SelectedTab.Text : "無畫布";
            this.Text = $"簡易畫線軟體 - {tabName}{dirtyMark}";
        }

        private void BuildTopBar()
        {
            _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(245, 246, 248), Padding = new Padding(10, 10, 10, 10), WrapContents = false };

            _topBar.Controls.Add(CreateTextButton("➕ 新增畫布", 100, (s, e) => AddNewTab($"畫布 {_tabCounter++}")));
            
            ComboBox cbPageSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Margin = new Padding(0, 7, 8, 0) };
            cbPageSize.Items.AddRange(new string[] { "A4 直式", "A4 橫式", "A3 直式", "A3 橫式", "A2 直式", "A2 橫式", "A1 直式", "A1 橫式" });
            cbPageSize.SelectedIndex = 0;
            cbPageSize.SelectedIndexChanged += (s, e) => { if (CurrentCanvas != null) { UpdatePageSize(cbPageSize.Text); _isDirty = true; UpdateWindowTitle(); } };
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

            _topBar.Controls.Add(CreateTextButton("📤 匯出圖檔", 100, (s, e) => ShowExportDialog()));
        }

        // ==========================================
        // 【修正的左側面板】解決群組消失的問題
        // ==========================================
        private void BuildLeftPanel()
        {
            _leftPanelContainer = new Panel { Dock = DockStyle.Left, Width = 110, BackColor = Color.FromArgb(240, 240, 240) };
            
            Panel togglePanel = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.LightGray };
            Button btnToggle = new Button { Text = "◀ 隱藏工具", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) => {
                if (_leftPanelContainer.Width > 30) {
                    _leftPanelContainer.Width = 30;
                    btnToggle.Text = "▶";
                    _leftPanel.Visible = false;
                } else {
                    _leftPanelContainer.Width = 110;
                    btnToggle.Text = "◀ 隱藏工具";
                    _leftPanel.Visible = true;
                }
            };
            togglePanel.Controls.Add(btnToggle);

            _leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Padding = new Padding(0) };

            var grpGeneral = CreateToolGroup("通用工具", "grpGeneral", new Control[] {
                _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標 (V)"),
                CreateToolButton(App_Shapes.ShapeType.HandPan, "拖曳畫布 (H)"),
                _btnFormatPainter = CreateToolButton(App_Shapes.ShapeType.FormatPainter, "格式刷")
            });

            var grpConnectors = CreateToolGroup("連線工具", "grpConnectors", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.ArrowLine, "智慧箭頭"),
                CreateToolButton(App_Shapes.ShapeType.StraightLine, "智慧直線"),
                CreateToolButton(App_Shapes.ShapeType.OrthogonalLine, "折線 (L)")
            });

            var grpBasic = CreateToolGroup("基本圖形", "grpBasic", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Rectangle, "矩形 (R)"),
                CreateToolButton(App_Shapes.ShapeType.RoundedRectangle, "圓角矩形"),
                CreateToolButton(App_Shapes.ShapeType.Circle, "圓形"),
                CreateToolButton(App_Shapes.ShapeType.Triangle, "三角形"),
                CreateToolButton(App_Shapes.ShapeType.TextNode, "文字框 (T)"),
                CreateToolButton(App_Shapes.ShapeType.Text, "純文字")
            });

            var grpAdvanced = CreateToolGroup("進階圖形", "grpAdvanced", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Diamond, "菱形"),
                CreateToolButton(App_Shapes.ShapeType.Pentagon, "五邊形"),
                CreateToolButton(App_Shapes.ShapeType.Hexagon, "六邊形"),
                CreateToolButton(App_Shapes.ShapeType.Star, "星形"),
                CreateToolButton(App_Shapes.ShapeType.Cloud, "雲朵")
            });

            var grpDraw = CreateToolGroup("自由繪圖", "grpDraw", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Freehand, "自由畫筆 (P)"),
                CreateToolButton(App_Shapes.ShapeType.BezierPen, "鋼筆 (B)"),
                CreateToolButton(App_Shapes.ShapeType.Image, "插入圖片")
            });

            _leftPanel.Controls.Add(grpGeneral);
            _leftPanel.Controls.Add(grpConnectors);
            _leftPanel.Controls.Add(grpBasic);
            _leftPanel.Controls.Add(grpAdvanced);
            _leftPanel.Controls.Add(grpDraw);

            _leftPanelContainer.Controls.Add(_leftPanel);
            _leftPanelContainer.Controls.Add(togglePanel);

            SetActiveButton(_btnPointer);

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
        }

        // 【安全穩定的群組容器】
        private FlowLayoutPanel CreateToolGroup(string title, string groupId, Control[] buttons)
        {
            FlowLayoutPanel groupContainer = new FlowLayoutPanel
            {
                Width = 90,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                Margin = new Padding(0, 5, 0, 0),
                WrapContents = false
            };

            Button btnHeader = new Button
            {
                Text = title,
                Width = 90,
                Height = 25,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 220, 220),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Margin = new Padding(0)
            };
            btnHeader.FlatAppearance.BorderSize = 0;

            FlowLayoutPanel contentPanel = new FlowLayoutPanel
            {
                Width = 90,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(2),
                Margin = new Padding(0)
            };

            foreach (var btn in buttons) contentPanel.Controls.Add(btn);

            bool isExpanded = _panelStates.ContainsKey(groupId) ? _panelStates[groupId] : true;
            contentPanel.Visible = isExpanded;
            btnHeader.Text = isExpanded ? $"▼ {title}" : $"▶ {title}";

            btnHeader.Click += (s, e) => {
                contentPanel.Visible = !contentPanel.Visible;
                btnHeader.Text = contentPanel.Visible ? $"▼ {title}" : $"▶ {title}";
                _panelStates[groupId] = contentPanel.Visible;
            };

            groupContainer.Controls.Add(btnHeader);
            groupContainer.Controls.Add(contentPanel);

            return groupContainer;
        }

        private void LoadPanelStates()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "panel_states.ini");
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2) _panelStates[parts[0]] = bool.Parse(parts[1]);
                }
            }
        }

        private void SavePanelStates()
        {
            try {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "panel_states.ini");
                List<string> lines = new List<string>();
                foreach (var kvp in _panelStates) lines.Add($"{kvp.Key}={kvp.Value}");
                File.WriteAllLines(path, lines);
            } catch { }
        }

        private Button CreateTextButton(string text, int width, EventHandler onClick)
        {
            Button btn = new Button { Text = text, Size = new Size(width, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0) };
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

        // 【完整補回畫圖示與拖曳邏輯】
        private Button CreateToolButton(App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button { Size = new Size(40, 40), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(1, 1, 1, 1) };
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
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 8, 12, 24, 16);
                    else if (type == App_Shapes.ShapeType.RoundedRectangle) 
                    {
                        using(GraphicsPath gp = new GraphicsPath()) {
                            gp.AddArc(8, 12, 6, 6, 180, 90); gp.AddArc(26, 12, 6, 6, 270, 90);
                            gp.AddArc(26, 22, 6, 6, 0, 90); gp.AddArc(8, 22, 6, 6, 90, 90);
                            gp.CloseFigure(); g.DrawPath(p, gp);
                        }
                    }
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 10, 10, 20, 20);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 10, 10, 20, 20, 180, 180);
                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(20, 8), new PointF(32, 20), new PointF(20, 32), new PointF(8, 20) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(20, 10), new PointF(32, 30), new PointF(8, 30) });
                    else if (type == App_Shapes.ShapeType.Pentagon) 
                    {
                        PointF[] pts = new PointF[5];
                        for (int i = 0; i < 5; i++) {
                            double a = Math.PI / 2 + (i * 2 * Math.PI / 5);
                            pts[i] = new PointF(20 - (float)(11 * Math.Cos(a)), 20 - (float)(11 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Hexagon) 
                    {
                        PointF[] pts = new PointF[6];
                        for (int i = 0; i < 6; i++) {
                            double a = i * Math.PI / 3;
                            pts[i] = new PointF(20 + (float)(11 * Math.Cos(a)), 20 + (float)(11 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Star) 
                    {
                        PointF[] pts = new PointF[10];
                        for (int i = 0; i < 10; i++) {
                            double a = Math.PI / 2 + (i * Math.PI / 5);
                            float r = (i % 2 == 0) ? 12 : 5;
                            pts[i] = new PointF(20 - (float)(r * Math.Cos(a)), 20 - (float)(r * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Cloud) 
                    {
                        g.DrawArc(p, 8, 16, 10, 10, 90, 180); g.DrawArc(p, 12, 10, 12, 12, 180, 180);
                        g.DrawArc(p, 20, 12, 12, 12, 270, 180); g.DrawArc(p, 22, 18, 10, 10, 0, 180);
                        g.DrawLine(p, 13, 26, 27, 26);
                    }
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 6, 12, 28, 16); g.DrawString("A", new Font("Arial", 10), new SolidBrush(iconColor), 12, 12); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), new SolidBrush(iconColor), 11, 8);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 8, 8, 24, 24); g.DrawEllipse(p, 12, 12, 4, 4); g.DrawLine(p, 8, 32, 22, 18); }
                    else if (type == App_Shapes.ShapeType.Freehand) { g.DrawBezier(p, new Point(8, 20), new Point(18, 8), new Point(23, 32), new Point(33, 20)); }
                    else if (type == App_Shapes.ShapeType.BezierPen) 
                    { 
                        g.DrawLine(p, 20, 10, 14, 24); g.DrawLine(p, 20, 10, 26, 24); 
                        g.DrawLine(p, 14, 24, 20, 32); g.DrawLine(p, 26, 24, 20, 32);
                        g.FillEllipse(Brushes.White, 18, 8, 4, 4); g.DrawEllipse(p, 18, 8, 4, 4); 
                    }
                }
            };
            return btn;
        }

        private void ShowExportDialog()
        {
            if (CurrentCanvas == null) return;
            using (Form exportForm = new Form { Text = "匯出設定", Size = new Size(300, 250), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false })
            {
                Label lblFormat = new Label { Text = "請選擇匯出格式：", Location = new Point(20, 20), AutoSize = true };
                
                ComboBox cbFormat = new ComboBox { Location = new Point(20, 45), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
                cbFormat.Items.AddRange(new string[] { "PNG (透明背景圖片)", "PDF (向量文件)", "SVG (向量網頁圖形)" });
                cbFormat.SelectedIndex = 0;

                Label lblPdfSize = new Label { Text = "PDF 尺寸 (僅限 PDF):", Location = new Point(20, 85), AutoSize = true, Enabled = false };
                ComboBox cbPdfSize = new ComboBox { Location = new Point(20, 110), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
                cbPdfSize.Items.AddRange(new string[] { "A4", "A3", "A2", "A1" });
                cbPdfSize.SelectedIndex = 0;

                ComboBox cbPdfOri = new ComboBox { Location = new Point(150, 110), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
                cbPdfOri.Items.AddRange(new string[] { "直式", "橫式" });
                cbPdfOri.SelectedIndex = 0;

                cbFormat.SelectedIndexChanged += (s, e) => {
                    bool isPdf = cbFormat.SelectedIndex == 1;
                    lblPdfSize.Enabled = cbPdfSize.Enabled = cbPdfOri.Enabled = isPdf;
                };

                Button btnOk = new Button { Text = "選擇存檔位置...", Location = new Point(80, 160), Width = 130, Height = 35 };
                btnOk.Click += async (sender, ev) => {
                    string filter = cbFormat.SelectedIndex == 0 ? "PNG 圖片|*.png" : cbFormat.SelectedIndex == 1 ? "PDF 文件|*.pdf" : "SVG 向量圖|*.svg";
                    using (SaveFileDialog sfd = new SaveFileDialog { Filter = filter })
                    {
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            exportForm.Enabled = false;
                            btnOk.Text = "處理中...";

                            if (cbFormat.SelectedIndex == 0) await App_Export.ExportToPngAsync(CurrentCanvas.GetTransparentCanvasRender(), sfd.FileName);
                            else if (cbFormat.SelectedIndex == 1) await App_Export.ExportToPdfAsync(CurrentCanvas.GetTransparentCanvasRender(), sfd.FileName, cbPdfOri.SelectedIndex == 1);
                            else if (cbFormat.SelectedIndex == 2) await App_Export.ExportToSvgAsync(CurrentCanvas.Shapes, CurrentCanvas.PageSize, sfd.FileName);

                            MessageBox.Show("匯出成功！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            exportForm.Close();
                        }
                    }
                };

                exportForm.Controls.AddRange(new Control[] { lblFormat, cbFormat, lblPdfSize, cbPdfSize, cbPdfOri, btnOk });
                exportForm.ShowDialog();
            }
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
                        var imgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, originalImg);
                        CurrentCanvas.CmdManager.ExecuteCommand(new AddShapeCommand(CurrentCanvas.Shapes, imgShape));
                    }
                }
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
