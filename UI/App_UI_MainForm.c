using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp
{
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

        // ===== 屬性面板元件 =====
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

        // ===== 圖層面板元件 =====
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

            // 呼叫其他 Partial Class 中定義的方法來建立面板
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
            
            // 綁定畫布事件
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
    }
}
