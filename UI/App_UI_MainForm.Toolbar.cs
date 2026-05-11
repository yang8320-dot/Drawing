// ============================================================
// FILE: UI/App_UI_MainForm.Toolbar.cs
// ============================================================

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

        private Dictionary<string, bool> _panelStates = new Dictionary<string, bool>();

        // 屬性面板變數宣告
        private GroupBox _gbAlign;
        private CheckBox _chkAlignToPage;
        private FlowLayoutPanel _alignmentPanel;
        
        private GroupBox _gbZIndex;
        private FlowLayoutPanel _zIndexPanel;
        
        private FlowLayoutPanel _customPropertiesPanel;
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
            // 【Req 4: 支援雙擊重新命名畫布】
            _tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
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

        // 【Req 5: 增加 Ctrl+S 儲存專案】
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveAllTabs();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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

        // 【Req 4: 雙擊觸發修改分頁名稱】
        private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _tabControl.TabPages.Count; i++)
            {
                if (_tabControl.GetTabRect(i).Contains(e.Location))
                {
                    RenameTab(i);
                    break;
                }
            }
        }

        private ContextMenuStrip CreateTabContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("重新命名", null, (s, e) => {
                if (_tabControl.SelectedIndex >= 0) RenameTab(_tabControl.SelectedIndex);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("關閉此畫布", null, (s, e) => {
                if (_tabControl.SelectedIndex >= 0) CloseTab(_tabControl.SelectedIndex);
            });
            return menu;
        }

        // 【Req 4: 跳出輸入框重新命名】
        private void RenameTab(int index)
        {
            TabPage page = _tabControl.TabPages[index];
            string currentName = page.Text;

            using (Form renameForm = new Form { Text = "重新命名畫布", Size = new Size(300, 150), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                TextBox txtName = new TextBox { Text = currentName, Location = new Point(20, 20), Width = 240 };
                Button btnOk = new Button { Text = "確定", DialogResult = DialogResult.OK, Location = new Point(80, 60), Width = 80 };
                Button btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(180, 60), Width = 80 };

                renameForm.Controls.Add(txtName);
                renameForm.Controls.Add(btnOk);
                renameForm.Controls.Add(btnCancel);
                renameForm.AcceptButton = btnOk;
                renameForm.CancelButton = btnCancel;

                if (renameForm.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txtName.Text))
                {
                    page.Text = txtName.Text;
                    if (page.Controls.Count > 0 && page.Controls[0] is App_CanvasControl canvas)
                    {
                        canvas.CanvasTitle = txtName.Text;
                        canvas.Invalidate();
                    }
                    _isDirty = true;
                    UpdateWindowTitle();
                    _tabControl.Invalidate(); 
                }
            }
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
            canvas.CanvasTitle = title;
            
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
            _topBar.Controls.Add(CreateTextButton("復原", 50, (s, e) => CurrentCanvas?.CmdManager.Undo()));
            _topBar.Controls.Add(CreateTextButton("重做", 50, (s, e) => CurrentCanvas?.CmdManager.Redo()));
            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("放大 +", 60, (s, e) => { if (CurrentCanvas != null) CurrentCanvas.SetZoom(CurrentCanvas.ZoomFactor + 0.2f); }));
            _topBar.Controls.Add(CreateTextButton("縮小 -", 60, (s, e) => { if (CurrentCanvas != null) CurrentCanvas.SetZoom(CurrentCanvas.ZoomFactor - 0.2f); }));
            _topBar.Controls.Add(CreateTextButton("100%", 50, (s, e) => CurrentCanvas?.SetZoom(1.0f)));

            CheckBox chkRuler = new CheckBox { Text = "尺規", Checked = true, AutoSize = true, Margin = new Padding(15, 9, 10, 0) };
            chkRuler.CheckedChanged += (s, e) => { if (CurrentCanvas != null) { CurrentCanvas.ShowRulers = chkRuler.Checked; CurrentCanvas.Invalidate(); } };
            _topBar.Controls.Add(chkRuler);

            CheckBox chkBounds = new CheckBox { Text = "紙張邊界", Checked = false, AutoSize = true, Margin = new Padding(0, 9, 10, 0) };
            chkBounds.CheckedChanged += (s, e) => { if (CurrentCanvas != null) { CurrentCanvas.ShowPageBounds = chkBounds.Checked; CurrentCanvas.Invalidate(); } };
            _topBar.Controls.Add(chkBounds);

            CheckBox chkNumbers = new CheckBox { Text = "頁碼", Checked = false, AutoSize = true, Margin = new Padding(0, 9, 10, 0) };
            chkNumbers.CheckedChanged += (s, e) => { if (CurrentCanvas != null) { CurrentCanvas.ShowPageNumbers = chkNumbers.Checked; CurrentCanvas.Invalidate(); } };
            _topBar.Controls.Add(chkNumbers);

            // 【Req 1: 加入鎖點與正交控制】
            _topBar.Controls.Add(CreateDivider());
            CheckBox chkSnapObject = new CheckBox { Text = "鎖點", Checked = true, AutoSize = true, Margin = new Padding(5, 9, 10, 0) };
            chkSnapObject.CheckedChanged += (s, e) => { if (CurrentCanvas != null) CurrentCanvas.EnableObjectSnap = chkSnapObject.Checked; };
            _topBar.Controls.Add(chkSnapObject);

            CheckBox chkOrtho = new CheckBox { Text = "正交模式", Checked = false, AutoSize = true, Margin = new Padding(0, 9, 10, 0) };
            chkOrtho.CheckedChanged += (s, e) => { if (CurrentCanvas != null) CurrentCanvas.EnableOrthoMode = chkOrtho.Checked; };
            _topBar.Controls.Add(chkOrtho);

            _topBar.Controls.Add(CreateDivider());
            _topBar.Controls.Add(CreateTextButton("存檔", 50, (s, e) => SaveAllTabs()));
            _topBar.Controls.Add(CreateTextButton("讀取", 50, (s, e) => LoadTabs()));
            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("📤 匯出圖檔", 100, (s, e) => ShowExportDialog()));
        }

        private void BuildLeftPanel()
        {
            // 【Req 6: 左側寬度 +20 (從 130 變成 150)】
            _leftPanelContainer = new Panel { Dock = DockStyle.Left, Width = 150, BackColor = Color.FromArgb(240, 240, 240) };
            
            Panel togglePanel = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.LightGray };
            Button btnToggle = new Button { Text = "◀ 隱藏工具", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Click += (s, e) => {
                if (_leftPanelContainer.Width > 30) {
                    _leftPanelContainer.Width = 30;
                    btnToggle.Text = "▶";
                    _leftPanel.Visible = false;
                } else {
                    _leftPanelContainer.Width = 150;
                    btnToggle.Text = "◀ 隱藏工具";
                    _leftPanel.Visible = true;
                }
            };
            togglePanel.Controls.Add(btnToggle);

            _leftPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Padding = new Padding(0) };

            var grpGeneral = CreateToolGroup("通用工具", "grpGeneral", new Control[] {
                _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標 (V)"),
                CreateToolButton(App_Shapes.ShapeType.HandPan, "拖曳畫布 (H)"),
                _btnFormatPainter = CreateToolButton(App_Shapes.ShapeType.FormatPainter, "格式刷"),
                CreateToolButton(App_Shapes.ShapeType.TextNode, "文字框 (T)"),
                CreateToolButton(App_Shapes.ShapeType.Text, "純文字")
            });

            var grpBasic = CreateToolGroup("基本圖形", "grpBasic", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Rectangle, "矩形 (R)"),
                CreateToolButton(App_Shapes.ShapeType.RoundedRectangle, "圓角矩形"),
                CreateToolButton(App_Shapes.ShapeType.Circle, "圓形"),
                CreateToolButton(App_Shapes.ShapeType.Arc, "圓弧"),
                CreateToolButton(App_Shapes.ShapeType.Triangle, "三角形"),
                CreateToolButton(App_Shapes.ShapeType.Diamond, "菱形"),
                CreateToolButton(App_Shapes.ShapeType.Parallelogram, "平行四邊形"),
                CreateToolButton(App_Shapes.ShapeType.Cylinder, "資料庫/圓柱體")
            });

            var grpConnectors = CreateToolGroup("箭頭與連線", "grpConnectors", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.ArrowLine, "智慧箭頭"),
                CreateToolButton(App_Shapes.ShapeType.StraightLine, "智慧直線"),
                CreateToolButton(App_Shapes.ShapeType.OrthogonalLine, "折線 (L)"),
                CreateToolButton(App_Shapes.ShapeType.BlockArrow, "粗箭頭"),
                CreateToolButton(App_Shapes.ShapeType.DoubleArrow, "雙向箭頭"),
                CreateToolButton(App_Shapes.ShapeType.BraceLeft, "左大括號"),
                CreateToolButton(App_Shapes.ShapeType.BraceRight, "右大括號"),
                CreateToolButton(App_Shapes.ShapeType.Branch1To2, "一對二分支"),
                CreateToolButton(App_Shapes.ShapeType.Branch1To3, "一對三分支"),
                CreateToolButton(App_Shapes.ShapeType.Branch1To4, "一對四分支")
            });

            var grpAdvanced = CreateToolGroup("流程圖/進階", "grpAdvanced", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Document, "文件"),
                CreateToolButton(App_Shapes.ShapeType.Pentagon, "五邊形"),
                CreateToolButton(App_Shapes.ShapeType.Hexagon, "六邊形"),
                CreateToolButton(App_Shapes.ShapeType.Star, "星形")
            });

            var grpDraw = CreateToolGroup("自由繪圖", "grpDraw", new Control[] {
                CreateToolButton(App_Shapes.ShapeType.Freehand, "自由畫筆 (P)"),
                CreateToolButton(App_Shapes.ShapeType.BezierPen, "鋼筆 (B)"),
                CreateToolButton(App_Shapes.ShapeType.Image, "插入圖片")
            });

            _leftPanel.Controls.Add(grpGeneral);
            _leftPanel.Controls.Add(grpBasic);
            _leftPanel.Controls.Add(grpConnectors);
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

        private FlowLayoutPanel CreateToolGroup(string title, string groupId, Control[] buttons)
        {
            FlowLayoutPanel groupContainer = new FlowLayoutPanel
            {
                Width = 130, // 搭配 150 寬度稍微加寬
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                Margin = new Padding(3, 5, 3, 0),
                WrapContents = false
            };

            Button btnHeader = new Button
            {
                Text = title,
                Width = 130,
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
                Width = 130,
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

        private Button CreateToolButton(App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button { Size = new Size(33, 33), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(1) };
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
                using (Pen p = new Pen(iconColor, 1.5f))
                {
                    if (type == App_Shapes.ShapeType.Pointer) g.DrawPolygon(p, new Point[] { new Point(10, 8), new Point(10, 24), new Point(15, 18), new Point(22, 18) });
                    else if (type == App_Shapes.ShapeType.HandPan) 
                    { 
                        g.DrawLine(p, 12, 20, 12, 10); g.DrawArc(p, 10, 8, 4, 4, 180, 180); 
                        g.DrawLine(p, 16, 20, 16, 6); g.DrawArc(p, 14, 4, 4, 4, 180, 180); 
                        g.DrawLine(p, 20, 20, 20, 8); g.DrawArc(p, 18, 6, 4, 4, 180, 180); 
                        g.DrawLine(p, 24, 20, 24, 12); g.DrawArc(p, 22, 10, 4, 4, 180, 180); 
                        g.DrawArc(p, 10, 20, 14, 10, 0, 180);
                    }
                    else if (type == App_Shapes.ShapeType.FormatPainter) 
                    {
                        g.FillRectangle(new SolidBrush(iconColor), 10, 6, 14, 6);
                        g.DrawRectangle(p, 12, 12, 10, 4);
                        g.DrawLine(p, 17, 16, 17, 26);
                    }
                    else if (type == App_Shapes.ShapeType.ArrowLine) { g.DrawLine(p, 6, 26, 26, 6); g.DrawLine(p, 18, 6, 26, 6); g.DrawLine(p, 26, 6, 26, 14); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 6, 26, 26, 6);
                    else if (type == App_Shapes.ShapeType.OrthogonalLine) g.DrawLines(p, new PointF[] { new PointF(6, 26), new PointF(16, 26), new PointF(16, 10), new PointF(26, 10) });
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 6, 10, 20, 14);
                    else if (type == App_Shapes.ShapeType.RoundedRectangle) 
                    {
                        using(GraphicsPath gp = new GraphicsPath()) {
                            gp.AddArc(6, 10, 4, 4, 180, 90); gp.AddArc(22, 10, 4, 4, 270, 90);
                            gp.AddArc(22, 20, 4, 4, 0, 90); gp.AddArc(6, 20, 4, 4, 90, 90);
                            gp.CloseFigure(); g.DrawPath(p, gp);
                        }
                    }
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 6, 6, 20, 20);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 6, 6, 20, 20, 180, 180);
                    else if (type == App_Shapes.ShapeType.Parallelogram) g.DrawPolygon(p, new PointF[] { new PointF(12,8), new PointF(26,8), new PointF(20,24), new PointF(6,24) });
                    else if (type == App_Shapes.ShapeType.Cylinder) {
                        g.DrawEllipse(p, 8, 6, 16, 6); g.DrawLine(p, 8, 9, 8, 24); g.DrawLine(p, 24, 9, 24, 24); g.DrawArc(p, 8, 21, 16, 6, 0, 180);
                    }
                    else if (type == App_Shapes.ShapeType.Document) {
                        g.DrawLine(p, 6, 8, 24, 8); g.DrawLine(p, 24, 8, 24, 22);
                        g.DrawBezier(p, new Point(24,22), new Point(18,26), new Point(12,18), new Point(6,22));
                        g.DrawLine(p, 6, 22, 6, 8);
                    }
                    else if (type == App_Shapes.ShapeType.BlockArrow) g.DrawPolygon(p, new PointF[] { new PointF(6,12), new PointF(16,12), new PointF(16,8), new PointF(26,16), new PointF(16,24), new PointF(16,20), new PointF(6,20) });
                    
                    // 繪製新圖示
                    else if (type == App_Shapes.ShapeType.DoubleArrow) { g.DrawLine(p, 10,16, 22,16); g.DrawLine(p, 10,16, 14,12); g.DrawLine(p, 10,16, 14,20); g.DrawLine(p, 22,16, 18,12); g.DrawLine(p, 22,16, 18,20); }
                    else if (type == App_Shapes.ShapeType.BraceLeft) { g.DrawBezier(p, new Point(20,8), new Point(16,8), new Point(16,16), new Point(10,16)); g.DrawBezier(p, new Point(10,16), new Point(16,16), new Point(16,24), new Point(20,24)); }
                    else if (type == App_Shapes.ShapeType.BraceRight) { g.DrawBezier(p, new Point(10,8), new Point(14,8), new Point(14,16), new Point(20,16)); g.DrawBezier(p, new Point(20,16), new Point(14,16), new Point(14,24), new Point(10,24)); }
                    else if (type == App_Shapes.ShapeType.Branch1To2) { g.DrawLine(p, 16,8, 16,16); g.DrawLine(p, 10,16, 22,16); g.DrawLine(p, 10,16, 10,24); g.DrawLine(p, 22,16, 22,24); }
                    else if (type == App_Shapes.ShapeType.Branch1To3) { g.DrawLine(p, 16,8, 16,24); g.DrawLine(p, 10,16, 22,16); g.DrawLine(p, 10,16, 10,24); g.DrawLine(p, 22,16, 22,24); }
                    else if (type == App_Shapes.ShapeType.Branch1To4) { g.DrawLine(p, 16,8, 16,16); g.DrawLine(p, 6,16, 26,16); g.DrawLine(p, 6,16, 6,24); g.DrawLine(p, 12,16, 12,24); g.DrawLine(p, 20,16, 20,24); g.DrawLine(p, 26,16, 26,24); }

                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(16, 6), new PointF(26, 16), new PointF(16, 26), new PointF(6, 16) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(16, 8), new PointF(26, 24), new PointF(6, 24) });
                    else if (type == App_Shapes.ShapeType.Pentagon) 
                    {
                        PointF[] pts = new PointF[5];
                        for (int i = 0; i < 5; i++) {
                            double a = Math.PI / 2 + (i * 2 * Math.PI / 5);
                            pts[i] = new PointF(16 - (float)(10 * Math.Cos(a)), 16 - (float)(10 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Hexagon) 
                    {
                        PointF[] pts = new PointF[6];
                        for (int i = 0; i < 6; i++) {
                            double a = i * Math.PI / 3;
                            pts[i] = new PointF(16 + (float)(10 * Math.Cos(a)), 16 + (float)(10 * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.Star) 
                    {
                        PointF[] pts = new PointF[10];
                        for (int i = 0; i < 10; i++) {
                            double a = Math.PI / 2 + (i * Math.PI / 5);
                            float r = (i % 2 == 0) ? 10 : 4;
                            pts[i] = new PointF(16 - (float)(r * Math.Cos(a)), 16 - (float)(r * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 4, 10, 24, 14); g.DrawString("A", new Font("Arial", 9), new SolidBrush(iconColor), 9, 9); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 12, FontStyle.Bold), new SolidBrush(iconColor), 8, 6);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 6, 6, 20, 20); g.DrawEllipse(p, 10, 10, 3, 3); g.DrawLine(p, 6, 26, 18, 14); }
                    else if (type == App_Shapes.ShapeType.Freehand) { g.DrawBezier(p, new Point(6, 18), new Point(14, 6), new Point(18, 28), new Point(26, 18)); }
                    else if (type == App_Shapes.ShapeType.BezierPen) 
                    { 
                        g.DrawLine(p, 16, 8, 10, 20); g.DrawLine(p, 16, 8, 22, 20); 
                        g.DrawLine(p, 10, 20, 16, 28); g.DrawLine(p, 22, 20, 16, 28);
                        g.FillEllipse(Brushes.White, 14, 6, 4, 4); g.DrawEllipse(p, 14, 6, 4, 4); 
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
                cbFormat.Items.AddRange(new string[] { "PNG (透明背景圖片)", "PDF (分頁向量文件)", "SVG (向量網頁圖形)" });
                cbFormat.SelectedIndex = 0;

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
                            else if (cbFormat.SelectedIndex == 1) await App_Export.ExportToPdfMultiPageAsync(CurrentCanvas, sfd.FileName);
                            else if (cbFormat.SelectedIndex == 2) await App_Export.ExportToSvgAsync(CurrentCanvas.Shapes, CurrentCanvas.ActualPageSize, sfd.FileName);

                            MessageBox.Show("匯出成功！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            exportForm.Close();
                        }
                    }
                };

                exportForm.Controls.AddRange(new Control[] { lblFormat, cbFormat, btnOk });
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
