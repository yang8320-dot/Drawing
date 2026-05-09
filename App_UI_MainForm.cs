using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    // --- 核心視窗、生命週期與分頁管理 ---
    public partial class App_UI_MainForm : Form
    {
        // UI 元件變數宣告
        private FlowLayoutPanel _topBar;
        private FlowLayoutPanel _leftPanel;
        private Panel _rightPanel;
        private TabControl _tabControl;
        private App_CanvasControl CurrentCanvas => _tabControl.SelectedTab?.Controls.OfType<App_CanvasControl>().FirstOrDefault();

        private Button _activeToolBtn;
        private Button _btnPointer; 
        private Button _btnFormatPainter;

        private FlowLayoutPanel _alignmentPanel;
        private CheckBox _chkAlignToPage;
        private FlowLayoutPanel _zIndexPanel;
        
        private Panel _customPropertiesPanel;
        private GroupBox _gbAppearance;
        private GroupBox _gbText;
        
        private TreeView _tvLayers;
        private bool _isSyncingTree = false;

        private Button _btnShapeColor;
        private Button _btnFillColor;
        private Button _btnGradientColor;
        private ComboBox _cbBrushType;
        private CheckBox _chkShadow;

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

        private void InitializeUI()
        {
            this.Text = "商業級繪圖系統 (支援多分頁、防多開、圖層管理、等比縮放)";
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true; 

            _tabControl = new TabControl { Dock = DockStyle.Fill };
            _tabControl.SelectedIndexChanged += (s, e) => { RefreshPropertyPanel(); RefreshLayerTree(); };
            _tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            _tabControl.MouseClick += TabControl_MouseClick;

            _tabEditBox = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle };
            _tabEditBox.Leave += TabEditBox_Leave;
            _tabEditBox.KeyDown += TabEditBox_KeyDown;

            // 委派給 Partial Class 的方法來建立區塊
            BuildTopBar();
            BuildLeftPanel();
            BuildRightPanel();

            Panel centerContainer = new Panel { Dock = DockStyle.Fill };
            centerContainer.Controls.Add(_tabControl);
            centerContainer.Controls.Add(_tabEditBox); 
            _tabEditBox.BringToFront(); 

            this.Controls.Add(centerContainer);
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_leftPanel);
            this.Controls.Add(_topBar);
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
                        foreach (var shape in canvas.Shapes) shape.Dispose();
                }
            }
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = "商業級繪圖系統 (支援多分頁、防多開、圖層管理、等比縮放)";
            this.Text = _isDirty ? baseTitle + " [未存檔 *]" : baseTitle;
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
                                    foreach (var shape in canvas.Shapes) shape.Dispose();
                                
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

            var canvas = new App_CanvasControl { Dock = DockStyle.Fill };
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
            
            canvas.OnToolChangedRequested += (type) => { 
                if (CurrentCanvas != null) CurrentCanvas.CurrentTool = type; 
                
                Button targetBtn = null;
                foreach (Control c in _leftPanel.Controls)
                {
                    if (c is Button btn && btn.Tag != null && (App_Shapes.ShapeType)btn.Tag == type)
                    {
                        targetBtn = btn;
                        break;
                    }
                }
                if (targetBtn != null) SetActiveButton(targetBtn);
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
                        foreach (var shape in oldCanvas.Shapes) shape.Dispose();
                }
                
                _tabControl.TabPages.Clear();
                foreach (var page in project.Pages) AddNewTab(page.Title, page.Shapes);
                
                _isDirty = false;
                UpdateWindowTitle();
                RefreshLayerTree();
            }
        }
    }
}
