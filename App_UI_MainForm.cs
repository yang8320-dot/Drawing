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

        // 優化：使用 PropertyGrid 替換手刻的 UI，自動映射圖形屬性
        private PropertyGrid _propertyGrid;
        private FlowLayoutPanel _alignmentPanel;
        private FlowLayoutPanel _zIndexPanel;

        private TextBox _tabEditBox;

        private int _tabCounter = 1;
        private bool _isDirty = false;
        private Timer _autoSaveTimer;

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
            this.Text = "商業級繪圖系統 (支援多分頁、防多開、連線節點調整、自訂畫布名稱)";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.SelectedIndexChanged += (s, e) => RefreshPropertyPanel();
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

            _leftPanel = new FlowLayoutPanel() { Dock = DockStyle.Left, Width = 65, BackColor = Color.FromArgb(230, 233, 237), Padding = new Padding(5), AutoScroll = true };
            
            _btnPointer = CreateToolButton(App_Shapes.ShapeType.Pointer, "游標\n(可框選、旋轉、縮放)");
            SetActiveButton(_btnPointer);
            
            CreateToolButton(App_Shapes.ShapeType.HandPan, "拖曳畫布 (Hand Tool)\n(可用滑鼠左鍵直接平移畫面)");
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

            _rightPanel = new Panel() { Dock = DockStyle.Right, Width = 280, BackColor = Color.FromArgb(245, 245, 245), Padding = new Padding(10) };
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
                _isDirty = true;
                UpdateWindowTitle();
            };

            canvas.OnSelectionChanged += () => RefreshPropertyPanel();
            
            // 使用 PropertyGrid 後，不需要彈出獨立視窗了，直接引導看右側
            canvas.OnShapePropertyRequested += (shape) => {
                MessageBox.Show("請直接在右側「屬性控制面板」修改進階設定！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            }
        }

        // 優化：建立基於 PropertyGrid 的動態屬性面板
        private void BuildPropertyPanel()
        {
            _rightPanel.Controls.Clear();

            Panel actionsPanel = new Panel() { Dock = DockStyle.Top, Height = 170 };

            Label alignTitle = new Label() { Text = "快速對齊", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, 10), AutoSize = true };
            actionsPanel.Controls.Add(alignTitle);

            _alignmentPanel = new FlowLayoutPanel() { Location = new Point(0, 35), Width = 260, Height = 70, WrapContents = true };
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

            _zIndexPanel = new FlowLayoutPanel() { Location = new Point(0, 140), Width = 260, Height = 35, WrapContents = true };
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最上層", (s, e) => CurrentCanvas?.ChangeZIndex(0)));
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最下層", (s, e) => CurrentCanvas?.ChangeZIndex(-99)));
            actionsPanel.Controls.Add(_zIndexPanel);

            _propertyGrid = new PropertyGrid()
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                PropertySort = PropertySort.Categorized,
                HelpVisible = true
            };

            // 當使用者透過 PropertyGrid 更改數值時，更新畫布並標記未存檔
            _propertyGrid.PropertyValueChanged += (s, e) => {
                if (CurrentCanvas != null)
                {
                    CurrentCanvas.Invalidate();
                    _isDirty = true;
                    UpdateWindowTitle();
                }
            };

            _rightPanel.Controls.Add(_propertyGrid);
            _rightPanel.Controls.Add(actionsPanel);

            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
            _propertyGrid.Enabled = false;
        }

        private Button CreateAlignButton(string text, EventHandler onClick)
        {
            Button btn = new Button()
            {
                Text = text,
                Size = new Size(80, 28),
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

        // 優化：直接將選中的圖形丟給 PropertyGrid 顯示
        private void RefreshPropertyPanel()
        {
            if (CurrentCanvas != null)
            {
                int selCount = CurrentCanvas.SelectedShapes.Count;
                
                _alignmentPanel.Enabled = selCount > 1;
                _zIndexPanel.Enabled = selCount > 0;
                
                if (selCount > 0)
                {
                    _propertyGrid.Enabled = true;
                    // PropertyGrid 支援多選編輯，直接傳入陣列即可！
                    _propertyGrid.SelectedObjects = CurrentCanvas.SelectedShapes.ToArray();
                }
                else
                {
                    _propertyGrid.Enabled = false;
                    _propertyGrid.SelectedObject = null;
                }
            }
            else
            {
                _alignmentPanel.Enabled = false;
                _zIndexPanel.Enabled = false;
                _propertyGrid.Enabled = false;
                _propertyGrid.SelectedObject = null;
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
                    else if (type == App_Shapes.ShapeType.HandPan) 
                    { 
                        g.DrawLine(p, 16, 26, 16, 14); g.DrawArc(p, 14, 12, 4, 4, 180, 180); 
                        g.DrawLine(p, 20, 26, 20, 10); g.DrawArc(p, 18, 8, 4, 4, 180, 180); 
                        g.DrawLine(p, 24, 26, 24, 12); g.DrawArc(p, 22, 10, 4, 4, 180, 180); 
                        g.DrawLine(p, 28, 26, 28, 16); g.DrawArc(p, 26, 14, 4, 4, 180, 180); 
                        g.DrawArc(p, 14, 26, 18, 12, 0, 180);
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
