using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    public class App_UI_MainForm : Form
    {
        private FlowLayoutPanel _topBar;
        private FlowLayoutPanel _leftPanel;
        private Panel _rightPanel;
        
        private TabControl _tabControl;
        private App_CanvasControl CurrentCanvas => _tabControl.SelectedTab?.Controls.OfType<App_CanvasControl>().FirstOrDefault();

        private Button _activeToolBtn;
        private Button _btnPointer; 
        private Button _btnFormatPainter;

        private FlowLayoutPanel _alignmentPanel;
        private FlowLayoutPanel _zIndexPanel;
        private Panel _customPropertiesPanel;
        
        private TreeView _tvLayers;
        private bool _isSyncingTree = false;

        private Button _btnShapeColor;
        private Button _btnFillColor;
        // --- 擴充：漸層次色按鈕與筆刷類型下拉選單 ---
        private Button _btnGradientColor;
        private ComboBox _cbBrushType;
        private CheckBox _chkShadow;
        // ------------------------------------

        private Button _btnFontColor;
        private TrackBar _tbStrokeWidth;
        private Label _lblStrokeWidthValue;
        private ComboBox _cbFontName;
        private NumericUpDown _nudFontSize;
        private CheckBox _chkBold, _chkItalic, _chkUnderline;
        private ComboBox _cbTextAlign;
        private ComboBox _cbDashStyle;
        private bool _isUpdatingUI = false;

        private TextBox _tabEditBox;

        private int _tabCounter = 1;
        private bool _isDirty = false;
        private Timer _autoSaveTimer;

        private bool _isZenMode = false;
        private FormWindowState _previousWindowState;

        public App_UI_MainForm()
        {
            InitializeUI();
            
            var recoveredProject = App_SaveLoad.CheckAndLoadAutoSave();
            if (recoveredProject != null && recoveredProject.Pages.Count > 0)
            {
                foreach (var page in recoveredProject.Pages) AddNewTab(page.Title, page.Shapes);
            }
            else
            {
                AddNewTab($"畫布 {_tabCounter++}");
            }

            this.FormClosing += App_UI_MainForm_FormClosing;
            
            _autoSaveTimer = new Timer();
            _autoSaveTimer.Interval = 300000; 
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleZenMode();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleZenMode()
        {
            _isZenMode = !_isZenMode;
            if (_isZenMode)
            {
                _previousWindowState = this.WindowState;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                _topBar.Visible = false;
                _leftPanel.Visible = false;
                _rightPanel.Visible = false;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = _previousWindowState;
                _topBar.Visible = true;
                _leftPanel.Visible = true;
                _rightPanel.Visible = true;
            }
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (_isDirty) 
            {
                var project = new DrawProject();
                foreach (TabPage tab in _tabControl.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is App_CanvasControl canvas)
                        project.Pages.Add(new DrawPage { Title = tab.Text, Shapes = canvas.Shapes });
                }
                App_SaveLoad.PerformAutoSave(project);
            }
        }

        private void InitializeUI()
        {
            this.Text = "商業級繪圖系統 (支援多分頁、防多開、圖層管理、等比縮放)";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.SelectedIndexChanged += (s, e) => {
                RefreshPropertyPanel();
                RefreshLayerTree();
            };
            _tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            _tabControl.MouseClick += TabControl_MouseClick;

            _tabEditBox = new TextBox();
            _tabEditBox.Visible = false;
            _tabEditBox.BorderStyle = BorderStyle.FixedSingle;
            _tabEditBox.Leave += TabEditBox_Leave;
            _tabEditBox.KeyDown += TabEditBox_KeyDown;

            _topBar = new FlowLayoutPanel() 
            { 
                Dock = DockStyle.Top, 
                Height = 55, 
                BackColor = Color.FromArgb(245, 246, 248), 
                Padding = new Padding(10, 10, 10, 10),
                WrapContents = false 
            };

            _topBar.Controls.Add(CreateTextButton("➕ 新增畫布", 100, (s, e) => AddNewTab($"畫布 {_tabCounter++}")));
            
            ComboBox cbPageSize = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Margin = new Padding(0, 7, 8, 0) };
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

            CheckBox chkSnap = new CheckBox() { Text = "網格對齊", Checked = true, AutoSize = true, Margin = new Padding(5, 9, 15, 0) };
            chkSnap.CheckedChanged += (s, e) => { 
                if (CurrentCanvas != null) {
                    CurrentCanvas.SnapToGrid = chkSnap.Checked; 
                    CurrentCanvas.Invalidate(); 
                }
            };
            _topBar.Controls.Add(chkSnap);

            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("存檔", 60, (s, e) => SaveAllTabs()));
            _topBar.Controls.Add(CreateTextButton("讀取", 60, (s, e) => LoadTabs()));

            _topBar.Controls.Add(CreateDivider());

            _topBar.Controls.Add(CreateTextButton("匯出 PNG", 90, async (s, e) => {
                if (CurrentCanvas == null) return;
                using (var sfd = new SaveFileDialog() { Filter = "PNG 圖片|*.png" })
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToPngAsync(CurrentCanvas.GetTransparentCanvasRender(), sfd.FileName);
                        MessageBox.Show("當前畫布 PNG 匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
            }));
            
            _topBar.Controls.Add(CreateTextButton("匯出 PDF", 90, (s, e) => {
                if (CurrentCanvas != null) ShowPdfExportDialog();
            }));

            _topBar.Controls.Add(CreateTextButton("匯出 SVG", 90, async (s, e) => {
                if (CurrentCanvas == null) return;
                using (var sfd = new SaveFileDialog() { Filter = "SVG 向量圖|*.svg" })
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportToSvgAsync(CurrentCanvas.Shapes, CurrentCanvas.PageSize, sfd.FileName);
                        MessageBox.Show("當前畫布 SVG 匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
            }));
            
            Label lblZenMode = new Label() { Text = "💡 按 F11 進入全螢幕", ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(20, 10, 0, 0) };
            _topBar.Controls.Add(lblZenMode);

            _leftPanel = new FlowLayoutPanel() { Dock = DockStyle.Left, Width = 65, BackColor = Color.FromArgb(230, 233, 237), Padding = new Padding(5), AutoScroll = true };
            
            _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標\n(可框選、旋轉、縮放)");
            SetActiveButton(_btnPointer);
            
            CreateToolButton(App_Shapes.ShapeType.HandPan, "拖曳畫布 (Hand Tool)\n(可用滑鼠左鍵直接平移畫面)");
            
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
                    CurrentCanvas.CurrentTool = App_Shapes.ShapeType.Pointer;
                    SetActiveButton(_btnPointer);
                }
            };

            CreateToolButton(App_Shapes.ShapeType.ArrowLine, "智慧箭頭線");
            CreateToolButton(App_Shapes.ShapeType.StraightLine, "智慧直線");
            CreateToolButton(App_Shapes.ShapeType.OrthogonalLine, "90度折線 (智慧避障)");

            CreateToolButton(App_Shapes.ShapeType.Rectangle, "矩形");
            CreateToolButton(App_Shapes.ShapeType.RoundedRectangle, "圓角矩形"); 
            CreateToolButton(App_Shapes.ShapeType.Circle, "圓形");
            CreateToolButton(App_Shapes.ShapeType.Arc, "圓弧");
            CreateToolButton(App_Shapes.ShapeType.Diamond, "菱形");
            CreateToolButton(App_Shapes.ShapeType.Triangle, "三角形");
            CreateToolButton(App_Shapes.ShapeType.Pentagon, "五邊形"); 
            CreateToolButton(App_Shapes.ShapeType.Hexagon, "六邊形"); 
            CreateToolButton(App_Shapes.ShapeType.Star, "星形"); 
            CreateToolButton(App_Shapes.ShapeType.Cloud, "雲朵"); 

            CreateToolButton(App_Shapes.ShapeType.TextNode, "文字框");
            CreateToolButton(App_Shapes.ShapeType.Text, "純文字");
            CreateToolButton(App_Shapes.ShapeType.Image, "插入圖片");
            CreateToolButton(App_Shapes.ShapeType.Freehand, "自由畫筆");

            _rightPanel = new Panel() { Dock = DockStyle.Right, Width = 300, BackColor = Color.FromArgb(245, 245, 245) };
            BuildPropertyPanel();

            Panel centerContainer = new Panel() { Dock = DockStyle.Fill };
            centerContainer.Controls.Add(_tabControl);
            centerContainer.Controls.Add(_tabEditBox); 
            _tabEditBox.BringToFront(); 

            this.Controls.Add(centerContainer);
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_leftPanel);
            this.Controls.Add(_topBar);
        }

        private void App_UI_MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("您有未儲存的變更，是否要先存檔再離開？\n\n按「是」進行存檔，\n按「否」不存檔直接離開，\n按「取消」回到程式。", "尚未存檔", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    SaveAllTabs();
                    if (_isDirty) e.Cancel = true;
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                
                if (result == DialogResult.No) App_SaveLoad.DeleteAutoSave();
            }
            else
            {
                App_SaveLoad.DeleteAutoSave();
            }

            if (!e.Cancel)
            {
                foreach (TabPage tab in _tabControl.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is App_CanvasControl canvas)
                    {
                        foreach (var shape in canvas.Shapes) shape.Dispose();
                    }
                }
            }
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = "商業級繪圖系統 (支援多分頁、防多開、圖層管理、等比縮放)";
            this.Text = _isDirty ? baseTitle + " [未存檔 *]" : baseTitle;
        }

        private Button CreateTextButton(string text, int width, EventHandler onClick)
        {
            Button btn = new Button() 
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

        private Panel CreateDivider()
        {
            return new Panel() { Width = 1, Height = 35, BackColor = Color.FromArgb(200, 200, 200), Margin = new Padding(4, 0, 12, 0) };
        }

        private void TabControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                for (int i = 0; i < _tabControl.TabCount; i++)
                {
                    if (_tabControl.GetTabRect(i).Contains(e.Location))
                    {
                        var tabToClose = _tabControl.TabPages[i];
                        ContextMenuStrip closeMenu = new ContextMenuStrip();
                        closeMenu.Items.Add("關閉此畫布", null, (s, ev) => 
                        {
                            if (_tabControl.TabCount > 1)
                            {
                                if (tabToClose.Controls.Count > 0 && tabToClose.Controls[0] is App_CanvasControl canvas)
                                {
                                    foreach (var shape in canvas.Shapes) shape.Dispose();
                                }
                                
                                _tabControl.TabPages.Remove(tabToClose);
                                _isDirty = true;
                                UpdateWindowTitle();
                            }
                            else
                            {
                                MessageBox.Show("至少需要保留一張畫布。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        });
                        closeMenu.Show(_tabControl, e.Location);
                        break;
                    }
                }
            }
        }

        private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _tabControl.TabCount; i++)
            {
                Rectangle rect = _tabControl.GetTabRect(i);
                if (rect.Contains(e.Location))
                {
                    _tabEditBox.Text = _tabControl.TabPages[i].Text;
                    _tabEditBox.Bounds = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
                    _tabEditBox.Tag = _tabControl.TabPages[i]; 
                    _tabEditBox.Visible = true;
                    _tabEditBox.BringToFront();
                    _tabEditBox.Focus();
                    _tabEditBox.SelectAll();
                    break;
                }
            }
        }

        private void TabEditBox_Leave(object sender, EventArgs e) { CommitTabRename(); }
        
        private void TabEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitTabRename(); }
            else if (e.KeyCode == Keys.Escape) _tabEditBox.Visible = false;
        }

        private void CommitTabRename()
        {
            if (_tabEditBox.Visible && _tabEditBox.Tag is TabPage page)
            {
                if (!string.IsNullOrWhiteSpace(_tabEditBox.Text)) 
                {
                    page.Text = _tabEditBox.Text.Trim();
                    _isDirty = true;
                    UpdateWindowTitle();
                }
                _tabEditBox.Visible = false;
            }
        }

        private void AddNewTab(string title, List<App_Shapes.ShapeBase> shapes = null)
        {
            TabPage page = new TabPage(title);
            page.ToolTipText = "雙擊標籤可修改名稱，右鍵點擊可關閉畫布";
            _tabControl.ShowToolTips = true;

            var canvas = new App_CanvasControl();
            canvas.Dock = DockStyle.Fill;
            if (shapes != null) canvas.Shapes = shapes;

            canvas.MouseUp += (s, e) => RefreshPropertyPanel();
            
            canvas.CmdManager.OnStateChanged += () => {
                RefreshPropertyPanel();
                RefreshLayerTree();
                _isDirty = true;
                UpdateWindowTitle();
            };

            canvas.OnSelectionChanged += () => { 
                RefreshPropertyPanel();
                SyncLayerTreeSelection();
            };
            
            canvas.OnToolResetRequested += () => { 
                if (CurrentCanvas != null) CurrentCanvas.CurrentTool = App_Shapes.ShapeType.Pointer; 
                SetActiveButton(_btnPointer); 
            };
            
            canvas.OnImageInsertRequested += HandleImageInsert;

            page.Controls.Add(canvas);
            _tabControl.TabPages.Add(page);
            _tabControl.SelectedTab = page; 

            if (shapes == null)
            {
                _isDirty = true;
                UpdateWindowTitle();
            }
        }

        private void SaveAllTabs()
        {
            var project = new DrawProject();
            foreach (TabPage tab in _tabControl.TabPages)
            {
                if (tab.Controls.Count > 0 && tab.Controls[0] is App_CanvasControl canvas)
                    project.Pages.Add(new DrawPage { Title = tab.Text, Shapes = canvas.Shapes });
            }
            
            bool success = App_SaveLoad.SaveProject(project);
            if (success)
            {
                _isDirty = false;
                UpdateWindowTitle();
            }
        }

        private void LoadTabs()
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("您有未儲存的變更，如果讀取新檔案將會遺失當前進度。\n確定要繼續讀取嗎？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No) return;
            }

            var project = App_SaveLoad.LoadProject();
            if (project != null && project.Pages.Count > 0)
            {
                foreach (TabPage tab in _tabControl.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is App_CanvasControl oldCanvas)
                    {
                        foreach (var shape in oldCanvas.Shapes) shape.Dispose();
                    }
                }
                
                _tabControl.TabPages.Clear();
                foreach (var page in project.Pages) AddNewTab(page.Title, page.Shapes);
                
                _isDirty = false;
                UpdateWindowTitle();
                RefreshLayerTree();
            }
        }

        private void BuildPropertyPanel()
        {
            _rightPanel.Controls.Clear();

            SplitContainer scRight = new SplitContainer() 
            { 
                Orientation = Orientation.Horizontal, 
                Dock = DockStyle.Fill,
                SplitterDistance = 480, 
                FixedPanel = FixedPanel.Panel1
            };

            Panel topPropPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Panel actionsPanel = new Panel() { Dock = DockStyle.Top, Height = 170 };

            Label alignTitle = new Label() { Text = "快速對齊", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, 10), AutoSize = true };
            actionsPanel.Controls.Add(alignTitle);

            _alignmentPanel = new FlowLayoutPanel() { Location = new Point(0, 35), Width = 280, Height = 70, WrapContents = true };
            _alignmentPanel.Controls.Add(CreateAlignButton("靠左", (s, e) => AlignShapes("Left")));
            _alignmentPanel.Controls.Add(CreateAlignButton("置中", (s, e) => AlignShapes("Center")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠右", (s, e) => AlignShapes("Right")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠上", (s, e) => AlignShapes("Top")));
            _alignmentPanel.Controls.Add(CreateAlignButton("垂直置中", (s, e) => AlignShapes("Middle")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠下", (s, e) => AlignShapes("Bottom")));
            _alignmentPanel.Controls.Add(CreateAlignButton("水平均分", (s, e) => DistributeShapes("Horizontal")));
            _alignmentPanel.Controls.Add(CreateAlignButton("垂直均分", (s, e) => DistributeShapes("Vertical")));
            actionsPanel.Controls.Add(_alignmentPanel);

            Label zIndexTitle = new Label() { Text = "圖層順序", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, 115), AutoSize = true };
            actionsPanel.Controls.Add(zIndexTitle);

            _zIndexPanel = new FlowLayoutPanel() { Location = new Point(0, 140), Width = 280, Height = 35, WrapContents = true };
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最上層", (s, e) => { CurrentCanvas?.ChangeZIndex(0); RefreshLayerTree(); }));
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最下層", (s, e) => { CurrentCanvas?.ChangeZIndex(-99); RefreshLayerTree(); }));
            actionsPanel.Controls.Add(_zIndexPanel);

            _customPropertiesPanel = new Panel() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 10, 0, 0) };

            int yOffset = 0;
            
            Label lblAppearance = new Label() { Text = "外觀設定", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, yOffset), AutoSize = true };
            _customPropertiesPanel.Controls.Add(lblAppearance);
            yOffset += 30;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "邊框顏色", Location = new Point(0, yOffset + 5), AutoSize = true });
            _btnShapeColor = new Button() { Location = new Point(80, yOffset), Size = new Size(160, 25), FlatStyle = FlatStyle.Flat };
            _btnShapeColor.Click += (s, e) => PickColor(_btnShapeColor, c => ApplyPropertyChange(cmd => cmd.ShapeColor = c));
            _customPropertiesPanel.Controls.Add(_btnShapeColor);
            yOffset += 35;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "填色類型", Location = new Point(0, yOffset + 5), AutoSize = true });
            _cbBrushType = new ComboBox() { Location = new Point(80, yOffset), Size = new Size(160, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbBrushType.Items.AddRange(new string[] { "純色填充", "線性漸層" });
            _cbBrushType.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FillBrushType = (App_Shapes.BrushType)_cbBrushType.SelectedIndex);
            _customPropertiesPanel.Controls.Add(_cbBrushType);
            yOffset += 35;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "主填充色", Location = new Point(0, yOffset + 5), AutoSize = true });
            _btnFillColor = new Button() { Location = new Point(80, yOffset), Size = new Size(75, 25), FlatStyle = FlatStyle.Flat };
            _btnFillColor.Click += (s, e) => PickColor(_btnFillColor, c => ApplyPropertyChange(cmd => cmd.FillColor = c), true);
            _customPropertiesPanel.Controls.Add(_btnFillColor);

            _btnGradientColor = new Button() { Location = new Point(165, yOffset), Size = new Size(75, 25), FlatStyle = FlatStyle.Flat };
            _btnGradientColor.Click += (s, e) => PickColor(_btnGradientColor, c => ApplyPropertyChange(cmd => cmd.GradientColor2 = c));
            _customPropertiesPanel.Controls.Add(_btnGradientColor);
            yOffset += 40;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "線條粗細", Location = new Point(0, yOffset + 5), AutoSize = true });
            _tbStrokeWidth = new TrackBar() { Location = new Point(80, yOffset), Size = new Size(130, 30), Minimum = 1, Maximum = 20, TickStyle = TickStyle.None };
            _lblStrokeWidthValue = new Label() { Location = new Point(220, yOffset + 5), AutoSize = true };
            _tbStrokeWidth.ValueChanged += (s, e) => {
                _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString();
                ApplyPropertyChange(cmd => cmd.StrokeWidth = _tbStrokeWidth.Value);
            };
            _customPropertiesPanel.Controls.Add(_tbStrokeWidth);
            _customPropertiesPanel.Controls.Add(_lblStrokeWidthValue);
            yOffset += 40;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "線條樣式", Location = new Point(0, yOffset + 5), AutoSize = true });
            _cbDashStyle = new ComboBox() { Location = new Point(80, yOffset), Size = new Size(160, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbDashStyle.Items.AddRange(new string[] { "實線 (Solid)", "虛線 (Dash)", "點線 (Dot)", "點虛線 (DashDot)" });
            _cbDashStyle.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.StrokeDashStyle = (DashStyle)_cbDashStyle.SelectedIndex);
            _customPropertiesPanel.Controls.Add(_cbDashStyle);
            yOffset += 35;

            _chkShadow = new CheckBox() { Text = "啟用立體陰影", Location = new Point(80, yOffset), AutoSize = true };
            _chkShadow.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.EnableShadow = _chkShadow.Checked);
            _customPropertiesPanel.Controls.Add(_chkShadow);
            yOffset += 35;

            Panel div1 = new Panel() { BackColor = Color.LightGray, Height = 1, Width = 260, Location = new Point(0, yOffset) };
            _customPropertiesPanel.Controls.Add(div1);
            yOffset += 15;

            Label lblText = new Label() { Text = "文字設定", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, yOffset), AutoSize = true };
            _customPropertiesPanel.Controls.Add(lblText);
            yOffset += 30;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "字體顏色", Location = new Point(0, yOffset + 5), AutoSize = true });
            _btnFontColor = new Button() { Location = new Point(80, yOffset), Size = new Size(160, 25), FlatStyle = FlatStyle.Flat };
            _btnFontColor.Click += (s, e) => PickColor(_btnFontColor, c => ApplyPropertyChange(cmd => cmd.FontColor = c));
            _customPropertiesPanel.Controls.Add(_btnFontColor);
            yOffset += 35;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "字體/大小", Location = new Point(0, yOffset + 5), AutoSize = true });
            _cbFontName = new ComboBox() { Location = new Point(80, yOffset), Size = new Size(100, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FontFamily font in FontFamily.Families) _cbFontName.Items.Add(font.Name);
            _cbFontName.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontName = _cbFontName.Text);
            _customPropertiesPanel.Controls.Add(_cbFontName);

            _nudFontSize = new NumericUpDown() { Location = new Point(190, yOffset), Size = new Size(50, 25), Minimum = 6, Maximum = 144 };
            _nudFontSize.ValueChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontSize = (float)_nudFontSize.Value);
            _customPropertiesPanel.Controls.Add(_nudFontSize);
            yOffset += 35;

            _chkBold = new CheckBox() { Text = "粗體", Location = new Point(80, yOffset), AutoSize = true };
            _chkBold.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontBold = _chkBold.Checked);
            _chkItalic = new CheckBox() { Text = "斜體", Location = new Point(140, yOffset), AutoSize = true };
            _chkItalic.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontItalic = _chkItalic.Checked);
            _chkUnderline = new CheckBox() { Text = "底線", Location = new Point(200, yOffset), AutoSize = true };
            _chkUnderline.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontUnderline = _chkUnderline.Checked);
            _customPropertiesPanel.Controls.Add(_chkBold);
            _customPropertiesPanel.Controls.Add(_chkItalic);
            _customPropertiesPanel.Controls.Add(_chkUnderline);
            yOffset += 35;

            _customPropertiesPanel.Controls.Add(new Label() { Text = "對齊方式", Location = new Point(0, yOffset + 5), AutoSize = true });
            _cbTextAlign = new ComboBox() { Location = new Point(80, yOffset), Size = new Size(160, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _cbTextAlign.Items.AddRange(Enum.GetNames(typeof(App_Shapes.TextAlign)));
            _cbTextAlign.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.TextAlignment = (App_Shapes.TextAlign)_cbTextAlign.SelectedIndex);
            _customPropertiesPanel.Controls.Add(_cbTextAlign);

            topPropPanel.Controls.Add(_customPropertiesPanel);
            topPropPanel.Controls.Add(actionsPanel);
            scRight.Panel1.Controls.Add(topPropPanel);

            Panel layerPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Label lblLayers = new Label() { Text = "圖層管理 (層級由上而下)", Font = new Font("Arial", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 25 };
            
            _tvLayers = new TreeView() 
            { 
                Dock = DockStyle.Fill, 
                HideSelection = false,
                FullRowSelect = true,
                ItemHeight = 22,
                Font = new Font("微軟正黑體", 9)
            };
            
            _tvLayers.AfterSelect += TvLayers_AfterSelect;
            
            ContextMenuStrip layerMenu = new ContextMenuStrip();
            layerMenu.Items.Add("鎖定 / 解鎖", null, (s, e) => {
                if (_tvLayers.SelectedNode?.Tag is App_Shapes.ShapeBase shape)
                {
                    shape.IsLocked = !shape.IsLocked;
                    CurrentCanvas?.Invalidate();
                    RefreshLayerTree();
                }
            });
            layerMenu.Items.Add(new ToolStripSeparator());
            layerMenu.Items.Add("刪除圖層", null, (s, e) => {
                if (_tvLayers.SelectedNode?.Tag is App_Shapes.ShapeBase shape && CurrentCanvas != null)
                {
                    CurrentCanvas.CmdManager.ExecuteCommand(new RemoveShapesCommand(CurrentCanvas.Shapes, new List<App_Shapes.ShapeBase> { shape }));
                }
            });
            
            _tvLayers.NodeMouseClick += (s, e) => {
                if (e.Button == MouseButtons.Right)
                {
                    _tvLayers.SelectedNode = e.Node;
                    layerMenu.Show(_tvLayers, e.Location);
                }
            };

            layerPanel.Controls.Add(_tvLayers);
            layerPanel.Controls.Add(lblLayers);
            scRight.Panel2.Controls.Add(layerPanel);

            _rightPanel.Controls.Add(scRight);

            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
            _customPropertiesPanel.Enabled = false;
        }

        private void RefreshLayerTree()
        {
            if (CurrentCanvas == null) return;
            
            _isSyncingTree = true;
            _tvLayers.Nodes.Clear();

            for (int i = CurrentCanvas.Shapes.Count - 1; i >= 0; i--)
            {
                _tvLayers.Nodes.Add(CreateTreeNode(CurrentCanvas.Shapes[i]));
            }
            
            _tvLayers.ExpandAll();
            _isSyncingTree = false;
            
            SyncLayerTreeSelection();
        }

        private TreeNode CreateTreeNode(App_Shapes.ShapeBase shape)
        {
            TreeNode node = new TreeNode(GetShapeName(shape));
            node.Tag = shape;

            if (shape is App_Shapes.GroupShape group)
            {
                for (int i = group.Children.Count - 1; i >= 0; i--)
                {
                    node.Nodes.Add(CreateTreeNode(group.Children[i]));
                }
            }

            return node;
        }

        private string GetShapeName(App_Shapes.ShapeBase shape)
        {
            string name = "圖形";
            if (shape is App_Shapes.RectShape) name = "矩形";
            else if (shape is App_Shapes.RoundedRectShape) name = "圓角矩形";
            else if (shape is App_Shapes.CircleShape) name = "圓形";
            else if (shape is App_Shapes.ArcShape) name = "圓弧";
            else if (shape is App_Shapes.DiamondShape) name = "菱形";
            else if (shape is App_Shapes.TriangleShape) name = "三角形";
            else if (shape is App_Shapes.PentagonShape) name = "五邊形";
            else if (shape is App_Shapes.HexagonShape) name = "六邊形";
            else if (shape is App_Shapes.StarShape) name = "星形";
            else if (shape is App_Shapes.CloudShape) name = "雲朵";
            else if (shape is App_Shapes.ConnectorShape) name = "連線";
            else if (shape is App_Shapes.TextNodeShape tns) name = tns.IsTransparent ? "純文字" : "文字框";
            else if (shape is App_Shapes.ImageShape) name = "圖片";
            else if (shape is App_Shapes.FreehandShape) name = "手繪線條";
            else if (shape is App_Shapes.GroupShape) name = "📂 群組";

            if (!string.IsNullOrEmpty(shape.Text))
            {
                string snippet = shape.Text.Replace("\n", " ").Replace("\r", "");
                if (snippet.Length > 8) snippet = snippet.Substring(0, 8) + "...";
                name += $" - {snippet}";
            }

            if (shape.IsLocked) name = "🔒 " + name;

            return name;
        }

        private void SyncLayerTreeSelection()
        {
            if (_isSyncingTree || CurrentCanvas == null) return;
            
            _isSyncingTree = true;
            _tvLayers.SelectedNode = null;

            if (CurrentCanvas.SelectedShapes.Count > 0)
            {
                var targetShape = CurrentCanvas.SelectedShapes[0];
                TreeNode foundNode = FindNodeByTag(_tvLayers.Nodes, targetShape);
                if (foundNode != null)
                {
                    _tvLayers.SelectedNode = foundNode;
                    foundNode.EnsureVisible();
                }
            }
            
            _isSyncingTree = false;
        }

        private TreeNode FindNodeByTag(TreeNodeCollection nodes, App_Shapes.ShapeBase target)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == target) return node;
                if (node.Nodes.Count > 0)
                {
                    TreeNode found = FindNodeByTag(node.Nodes, target);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void TvLayers_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_isSyncingTree || CurrentCanvas == null) return;

            if (e.Node.Tag is App_Shapes.ShapeBase shape)
            {
                for (int i = 0; i < CurrentCanvas.SelectedShapes.Count; i++) 
                    CurrentCanvas.SelectedShapes[i].IsSelected = false;
                
                CurrentCanvas.SelectedShapes.Clear();
                
                shape.IsSelected = true;
                CurrentCanvas.SelectedShapes.Add(shape);
                
                CurrentCanvas.Invalidate();
                RefreshPropertyPanel();
            }
        }

        private void PickColor(Button btn, Action<Color> applyAction, bool allowTransparent = false)
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = btn.BackColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    btn.BackColor = cd.Color;
                    btn.Text = "";
                    applyAction(cd.Color);
                }
            }
            btn.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Right && allowTransparent)
                {
                    btn.BackColor = Color.Transparent;
                    btn.Text = "透明";
                    applyAction(Color.Transparent);
                }
            };
        }

        private void ApplyPropertyChange(Action<App_Shapes.ShapeBase> propertySetter)
        {
            if (_isUpdatingUI || CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count == 0) return;

            var shapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            if (shapes.Count == 0) return;

            var cmd = new ChangeFormatCommand(shapes);
            foreach (var s in shapes) propertySetter(s);
            cmd.CaptureNewState();

            CurrentCanvas.CmdManager.ExecuteCommand(cmd);
            CurrentCanvas.Invalidate();
            
            _isDirty = true;
            UpdateWindowTitle();
            
            RefreshLayerTree(); 
        }

        private Button CreateAlignButton(string text, EventHandler onClick)
        {
            Button btn = new Button()
            {
                Text = text,
                Size = new Size(85, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("微軟正黑體", 8)
            };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            return btn;
        }

        private void AlignShapes(string type)
        {
            if (CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count < 2) return;
            
            var shapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            if (shapes.Count == 0) return;

            var oldBounds = shapes.Select(s => s.Bounds).ToList();
            var newBounds = new List<RectangleF>();

            float referenceValue = 0;

            switch (type)
            {
                case "Left":
                    referenceValue = shapes.Min(s => s.Bounds.Left);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(referenceValue, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height));
                    break;
                case "Right":
                    referenceValue = shapes.Max(s => s.Bounds.Right);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(referenceValue - s.Bounds.Width, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height));
                    break;
                case "Center":
                    referenceValue = shapes.Average(s => s.Bounds.X + s.Bounds.Width / 2);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(referenceValue - s.Bounds.Width / 2, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height));
                    break;
                case "Top":
                    referenceValue = shapes.Min(s => s.Bounds.Top);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, referenceValue, s.Bounds.Width, s.Bounds.Height));
                    break;
                case "Bottom":
                    referenceValue = shapes.Max(s => s.Bounds.Bottom);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, referenceValue - s.Bounds.Height, s.Bounds.Width, s.Bounds.Height));
                    break;
                case "Middle":
                    referenceValue = shapes.Average(s => s.Bounds.Y + s.Bounds.Height / 2);
                    foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, referenceValue - s.Bounds.Height / 2, s.Bounds.Width, s.Bounds.Height));
                    break;
            }

            CurrentCanvas.CmdManager.ExecuteCommand(new TransformShapesCommand(shapes, oldBounds, newBounds));
            CurrentCanvas.Invalidate();
        }

        private void DistributeShapes(string type)
        {
            if (CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count < 3) return;
            
            var shapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            if (shapes.Count < 3) return;

            var oldBounds = shapes.Select(s => s.Bounds).ToList();
            var newBounds = new List<RectangleF>();

            if (type == "Horizontal")
            {
                shapes = shapes.OrderBy(s => s.Bounds.X).ToList();
                float totalSpace = shapes.Last().Bounds.Right - shapes.First().Bounds.Left;
                float totalShapeWidth = shapes.Sum(s => s.Bounds.Width);
                float gap = (totalSpace - totalShapeWidth) / (shapes.Count - 1);
                
                float currentX = shapes.First().Bounds.Left;
                foreach (var s in shapes)
                {
                    newBounds.Add(new RectangleF(currentX, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height));
                    currentX += s.Bounds.Width + gap;
                }
            }
            else if (type == "Vertical")
            {
                shapes = shapes.OrderBy(s => s.Bounds.Y).ToList();
                float totalSpace = shapes.Last().Bounds.Bottom - shapes.First().Bounds.Top;
                float totalShapeHeight = shapes.Sum(s => s.Bounds.Height);
                float gap = (totalSpace - totalShapeHeight) / (shapes.Count - 1);
                
                float currentY = shapes.First().Bounds.Top;
                foreach (var s in shapes)
                {
                    newBounds.Add(new RectangleF(s.Bounds.X, currentY, s.Bounds.Width, s.Bounds.Height));
                    currentY += s.Bounds.Height + gap;
                }
            }

            var orderedNewBounds = new List<RectangleF>();
            var originalShapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            for (int i = 0; i < originalShapes.Count; i++)
            {
                int sortedIndex = shapes.IndexOf(originalShapes[i]);
                orderedNewBounds.Add(newBounds[sortedIndex]);
            }

            CurrentCanvas.CmdManager.ExecuteCommand(new TransformShapesCommand(originalShapes, oldBounds, orderedNewBounds));
            CurrentCanvas.Invalidate();
        }

        private void RefreshPropertyPanel()
        {
            if (CurrentCanvas != null)
            {
                int selCount = CurrentCanvas.SelectedShapes.Count;
                
                _alignmentPanel.Enabled = selCount > 1;
                _zIndexPanel.Enabled = selCount > 0;
                
                if (selCount > 0)
                {
                    _customPropertiesPanel.Enabled = true;
                    
                    var shape = CurrentCanvas.SelectedShapes[0];
                    
                    _isUpdatingUI = true;
                    
                    _btnShapeColor.BackColor = shape.ShapeColor;
                    
                    _btnFillColor.BackColor = shape.FillColor;
                    _btnFillColor.Text = shape.FillColor == Color.Transparent ? "透明" : "";
                    
                    _btnGradientColor.BackColor = shape.GradientColor2;
                    _cbBrushType.SelectedIndex = (int)shape.FillBrushType;
                    _chkShadow.Checked = shape.EnableShadow;
                    
                    _tbStrokeWidth.Value = Math.Max(1, Math.Min(20, (int)shape.StrokeWidth));
                    _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString();
                    
                    _cbDashStyle.SelectedIndex = (int)shape.StrokeDashStyle;

                    _btnFontColor.BackColor = shape.FontColor;
                    if (_cbFontName.Items.Contains(shape.FontName)) _cbFontName.SelectedItem = shape.FontName;
                    _nudFontSize.Value = (decimal)shape.FontSize;
                    
                    _chkBold.Checked = shape.FontBold;
                    _chkItalic.Checked = shape.FontItalic;
                    _chkUnderline.Checked = shape.FontUnderline;

                    _cbTextAlign.SelectedIndex = (int)shape.TextAlignment;

                    _isUpdatingUI = false;
                }
                else
                {
                    _customPropertiesPanel.Enabled = false;
                }
            }
            else
            {
                _alignmentPanel.Enabled = false;
                _zIndexPanel.Enabled = false;
                _customPropertiesPanel.Enabled = false;
            }
        }

        private void HandleImageInsert(PointF pt)
        {
            if (CurrentCanvas == null) return;
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "圖片檔案|*.jpg;*.png;*.bmp" })
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
                        CurrentCanvas.Invalidate();
                        
                        if (finalImg != originalImg) finalImg.Dispose();
                    }
                }
            }
        }

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
            CurrentCanvas.Invalidate();
        }

        private Button CreateToolButton(App_Shapes.ShapeType type, string tooltip)
        {
            Button btn = new Button() { Size = new Size(45, 45), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(2, 2, 2, 8) };
            btn.FlatAppearance.BorderSize = 0;
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
                            gp.AddArc(10, 12, 6, 6, 180, 90);
                            gp.AddArc(28, 12, 6, 6, 270, 90);
                            gp.AddArc(28, 26, 6, 6, 0, 90);
                            gp.AddArc(10, 26, 6, 6, 90, 90);
                            gp.CloseFigure();
                            g.DrawPath(p, gp);
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
                        g.DrawArc(p, 10, 18, 10, 10, 90, 180);
                        g.DrawArc(p, 14, 12, 12, 12, 180, 180);
                        g.DrawArc(p, 22, 14, 12, 12, 270, 180);
                        g.DrawArc(p, 24, 20, 10, 10, 0, 180);
                        g.DrawLine(p, 15, 28, 29, 28);
                    }
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 8, 12, 28, 20); g.DrawString("A", new Font("Arial", 10), new SolidBrush(iconColor), 14, 14); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), new SolidBrush(iconColor), 12, 10);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 10, 10, 24, 24); g.DrawEllipse(p, 14, 14, 4, 4); g.DrawLine(p, 10, 34, 24, 20); }
                    else if (type == App_Shapes.ShapeType.Freehand) { g.DrawBezier(p, new Point(10, 22), new Point(20, 10), new Point(25, 34), new Point(35, 22)); }
                }
            };
            _leftPanel.Controls.Add(btn);
            return btn;
        }

        private void SetActiveButton(Button btn)
        {
            if (_activeToolBtn != null) _activeToolBtn.BackColor = Color.Transparent;
            _activeToolBtn = btn;
            _activeToolBtn.BackColor = Color.LightSkyBlue;
        }
    }
}
