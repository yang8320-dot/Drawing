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

        private ComboBox _cbFont;
        private NumericUpDown _nudFontSize;
        private NumericUpDown _nudStroke;
        private ComboBox _cbDash;
        private Button _btnShapeColor;
        private Button _btnFontColor;
        
        private TextBox _tabEditBox;

        private bool _isUpdatingUI = false;
        private int _tabCounter = 1;

        // --- 優化：加入 Dirty Flag 變數 ---
        private bool _isDirty = false;

        public App_UI_MainForm()
        {
            InitializeUI();
            
            AddNewTab($"畫布 {_tabCounter++}");

            // --- 優化：攔截視窗關閉事件 ---
            this.FormClosing += App_UI_MainForm_FormClosing;
        }

        private void InitializeUI()
        {
            this.Text = "商業級繪圖系統 (支援多分頁、防多開、連線節點調整、自訂畫布名稱)";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.SelectedIndexChanged += (s, e) => RefreshPropertyPanel();
            _tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            // --- 優化：加入 Tab 的滑鼠右鍵點擊事件 (關閉分頁) ---
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
                    _isDirty = true; // 頁面大小改變視為未存檔
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

            _leftPanel = new FlowLayoutPanel() { Dock = DockStyle.Left, Width = 60, BackColor = Color.FromArgb(230, 233, 237), Padding = new Padding(5) };
            
            _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標\n(可框選、旋轉、縮放)");
            SetActiveButton(_btnPointer);
            
            CreateToolButton(App_Shapes.ShapeType.ArrowLine, "智慧箭頭線");
            CreateToolButton(App_Shapes.ShapeType.StraightLine, "智慧直線");
            CreateToolButton(App_Shapes.ShapeType.OrthogonalLine, "90度折線");
            CreateToolButton(App_Shapes.ShapeType.Rectangle, "矩形\n(可直接拖曳至畫布)");
            CreateToolButton(App_Shapes.ShapeType.Circle, "圓形\n(可直接拖曳至畫布)");
            CreateToolButton(App_Shapes.ShapeType.Arc, "圓弧\n(可直接拖曳至畫布)");
            CreateToolButton(App_Shapes.ShapeType.Diamond, "菱形\n(可直接拖曳至畫布)");
            CreateToolButton(App_Shapes.ShapeType.Triangle, "三角形\n(可直接拖曳至畫布)");
            CreateToolButton(App_Shapes.ShapeType.TextNode, "文字框");
            CreateToolButton(App_Shapes.ShapeType.Text, "純文字");
            CreateToolButton(App_Shapes.ShapeType.Image, "插入圖片");

            _rightPanel = new Panel() { Dock = DockStyle.Right, Width = 220, BackColor = Color.FromArgb(245, 245, 245), Padding = new Padding(10) };
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

        // --- 優化：關閉軟體時的防呆攔截 ---
        private void App_UI_MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("您有未儲存的變更，是否要先存檔再離開？\n\n按「是」進行存檔，\n按「否」不存檔直接離開，\n按「取消」回到程式。", "尚未存檔", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    SaveAllTabs();
                    if (_isDirty) // 如果存檔失敗或被取消，就阻止關閉
                    {
                        e.Cancel = true;
                    }
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = "商業級繪圖系統 (支援多分頁、防多開、連線節點調整、自訂畫布名稱)";
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

        // --- 優化：在 Tab 上點擊右鍵跳出關閉選單 ---
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
            
            // --- 優化：當 Canvas 的 CommandManager 狀態改變時，觸發 Dirty Flag ---
            canvas.CmdManager.OnStateChanged += () => {
                RefreshPropertyPanel();
                _isDirty = true;
                UpdateWindowTitle();
            };
            
            canvas.OnToolResetRequested += () => { 
                if (CurrentCanvas != null) CurrentCanvas.CurrentTool = App_Shapes.ShapeType.Pointer; 
                SetActiveButton(_btnPointer); 
            };
            
            canvas.OnImageInsertRequested += HandleImageInsert;

            page.Controls.Add(canvas);
            _tabControl.TabPages.Add(page);
            _tabControl.SelectedTab = page; 

            // 如果是使用者點擊新增按鈕，標記為未存檔
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
                _tabControl.TabPages.Clear();
                foreach (var page in project.Pages) AddNewTab(page.Title, page.Shapes);
                
                _isDirty = false;
                UpdateWindowTitle();
            }
        }

        private void BuildPropertyPanel()
        {
            int startY = 20;
            Label title = new Label() { Text = "圖形屬性", Font = new Font("Arial", 12, FontStyle.Bold), Location = new Point(10, startY), AutoSize = true };
            _rightPanel.Controls.Add(title); startY += 40;

            _rightPanel.Controls.Add(new Label() { Text = "字型:", Location = new Point(10, startY), AutoSize = true });
            _cbFont = new ComboBox() { Location = new Point(60, startY-3), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbFont.Items.AddRange(new string[] { "Arial", "標楷體", "微軟正黑體", "Times New Roman" });
            _cbFont.SelectedIndexChanged += PropertyChanged;
            _rightPanel.Controls.Add(_cbFont); startY += 40;

            _rightPanel.Controls.Add(new Label() { Text = "字號:", Location = new Point(10, startY), AutoSize = true });
            _nudFontSize = new NumericUpDown() { Location = new Point(60, startY-3), Width = 140, Minimum = 8, Maximum = 72 };
            _nudFontSize.ValueChanged += PropertyChanged;
            _rightPanel.Controls.Add(_nudFontSize); startY += 40;

            _rightPanel.Controls.Add(new Label() { Text = "線粗:", Location = new Point(10, startY), AutoSize = true });
            _nudStroke = new NumericUpDown() { Location = new Point(60, startY-3), Width = 140, Minimum = 1, Maximum = 20 };
            _nudStroke.ValueChanged += PropertyChanged;
            _rightPanel.Controls.Add(_nudStroke); startY += 40;

            _rightPanel.Controls.Add(new Label() { Text = "樣式:", Location = new Point(10, startY), AutoSize = true });
            _cbDash = new ComboBox() { Location = new Point(60, startY-3), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbDash.Items.AddRange(new object[] { DashStyle.Solid, DashStyle.Dash, DashStyle.Dot });
            _cbDash.SelectedIndexChanged += PropertyChanged;
            _rightPanel.Controls.Add(_cbDash); startY += 50;

            _btnShapeColor = new Button() { Text = "外框 / 圖形顏色", Location = new Point(10, startY), Width = 190, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
            _btnShapeColor.FlatAppearance.BorderColor = Color.Gray;
            _btnShapeColor.Click += (s, e) => ChangeColor(true);
            _rightPanel.Controls.Add(_btnShapeColor); startY += 45;

            _btnFontColor = new Button() { Text = "文字顏色", Location = new Point(10, startY), Width = 190, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
            _btnFontColor.FlatAppearance.BorderColor = Color.Gray;
            _btnFontColor.Click += (s, e) => ChangeColor(false);
            _rightPanel.Controls.Add(_btnFontColor);

            _rightPanel.Enabled = false; 
        }

        private void PropertyChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUI || CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count != 1) return;
            
            var shape = CurrentCanvas.SelectedShapes[0];
            shape.FontName = _cbFont.Text;
            shape.FontSize = (float)_nudFontSize.Value;
            shape.StrokeWidth = (float)_nudStroke.Value;
            shape.StrokeDashStyle = (DashStyle)_cbDash.SelectedItem;
            
            // 觸發重新繪製並標記為髒狀態
            CurrentCanvas.Invalidate();
            _isDirty = true;
            UpdateWindowTitle();
        }

        private void ChangeColor(bool isShapeColor)
        {
            if (CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count != 1) return;
            var shape = CurrentCanvas.SelectedShapes[0];

            using (ColorDialog cd = new ColorDialog() { Color = isShapeColor ? shape.ShapeColor : shape.FontColor })
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    if (isShapeColor)
                    {
                        shape.ShapeColor = cd.Color;
                        _btnShapeColor.BackColor = cd.Color;
                        _btnShapeColor.ForeColor = GetContrastColor(cd.Color);
                        CurrentCanvas.CurrentColor = cd.Color; 
                    }
                    else
                    {
                        shape.FontColor = cd.Color;
                        _btnFontColor.BackColor = cd.Color;
                        _btnFontColor.ForeColor = GetContrastColor(cd.Color);
                    }
                    CurrentCanvas.Invalidate();
                    _isDirty = true;
                    UpdateWindowTitle();
                }
            }
        }

        private Color GetContrastColor(Color bg) => (bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114) > 186 ? Color.Black : Color.White;

        private void RefreshPropertyPanel()
        {
            if (CurrentCanvas != null && CurrentCanvas.SelectedShapes.Count == 1)
            {
                _isUpdatingUI = true;
                var shape = CurrentCanvas.SelectedShapes[0];

                if (_cbFont.Items.Contains(shape.FontName)) _cbFont.SelectedItem = shape.FontName;
                _nudFontSize.Value = (decimal)Math.Max(_nudFontSize.Minimum, Math.Min(_nudFontSize.Maximum, (decimal)shape.FontSize));
                _nudStroke.Value = (decimal)Math.Max(_nudStroke.Minimum, Math.Min(_nudStroke.Maximum, (decimal)shape.StrokeWidth));
                _cbDash.SelectedItem = shape.StrokeDashStyle;

                _btnShapeColor.BackColor = shape.ShapeColor;
                _btnShapeColor.ForeColor = GetContrastColor(shape.ShapeColor);
                _btnFontColor.BackColor = shape.FontColor;
                _btnFontColor.ForeColor = GetContrastColor(shape.FontColor);

                _rightPanel.Enabled = true;
                _isUpdatingUI = false;
            }
            else
            {
                _rightPanel.Enabled = false;
            }
        }

        private void HandleImageInsert(PointF pt)
        {
            if (CurrentCanvas == null) return;
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "圖片檔案|*.jpg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Bitmap img = new Bitmap(ofd.FileName);
                    var imgShape = App_Shapes.ShapeFactory.CreateShape(App_Shapes.ShapeType.Image, pt, Color.Black, img);
                    CurrentCanvas.CmdManager.ExecuteCommand(new AddShapeCommand(CurrentCanvas.Shapes, imgShape));
                    CurrentCanvas.Invalidate();
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
                        btn.DoDragDrop(type, DragDropEffects.Copy);
                        mouseDownLocation = Point.Empty;
                    }
                }
            };

            btn.MouseUp += (s, e) => {
                if (e.Button == MouseButtons.Left)
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
                    else if (type == App_Shapes.ShapeType.ArrowLine) { g.DrawLine(p, 10, 32, 32, 10); g.DrawLine(p, 22, 10, 32, 10); g.DrawLine(p, 32, 10, 32, 20); }
                    else if (type == App_Shapes.ShapeType.StraightLine) g.DrawLine(p, 10, 32, 32, 10);
                    else if (type == App_Shapes.ShapeType.OrthogonalLine) g.DrawLines(p, new PointF[] { new PointF(10, 32), new PointF(22, 32), new PointF(22, 12), new PointF(32, 12) });
                    else if (type == App_Shapes.ShapeType.Rectangle) g.DrawRectangle(p, 10, 12, 24, 20);
                    else if (type == App_Shapes.ShapeType.Circle) g.DrawEllipse(p, 10, 10, 24, 24);
                    else if (type == App_Shapes.ShapeType.Arc) g.DrawArc(p, 10, 10, 24, 24, 180, 180);
                    else if (type == App_Shapes.ShapeType.Diamond) g.DrawPolygon(p, new PointF[] { new PointF(22, 8), new PointF(36, 22), new PointF(22, 36), new PointF(8, 22) });
                    else if (type == App_Shapes.ShapeType.Triangle) g.DrawPolygon(p, new PointF[] { new PointF(22, 10), new PointF(34, 32), new PointF(10, 32) });
                    else if (type == App_Shapes.ShapeType.TextNode) { g.DrawRectangle(p, 8, 12, 28, 20); g.DrawString("A", new Font("Arial", 10), new SolidBrush(iconColor), 14, 14); }
                    else if (type == App_Shapes.ShapeType.Text) g.DrawString("T", new Font("Arial", 14, FontStyle.Bold), new SolidBrush(iconColor), 12, 10);
                    else if (type == App_Shapes.ShapeType.Image) { g.DrawRectangle(p, 10, 10, 24, 24); g.DrawEllipse(p, 14, 14, 4, 4); g.DrawLine(p, 10, 34, 24, 20); }
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
