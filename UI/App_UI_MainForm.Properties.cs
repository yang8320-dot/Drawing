using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    // --- 負責右側的屬性面板 (外觀、文字) 與屬性連動 ---
    public partial class App_UI_MainForm
    {
        private void BuildRightPanel()
        {
            // 設定右側總寬度
            _rightPanel = new Panel { Dock = DockStyle.Right, Width = 320, BackColor = Color.FromArgb(245, 245, 245) };

            // 👑 關鍵修復 1：反轉分隔條邏輯，保護上半部屬性面板的空間
            SplitContainer scRight = new SplitContainer 
            { 
                Orientation = Orientation.Horizontal, 
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2, // 讓底部的圖層面板固定高度，上半部屬性面板自動填滿剩餘空間
                Panel2MinSize = 250,            // 圖層面板最少保留 250px
                SplitterDistance = 450          // 初始高度，視窗放大時會自動長大
            };

            // 👑 關鍵修復 2：採用「瀑布流 (FlowLayoutPanel)」，從上到下自動堆疊，保證絕對不重疊！
            FlowLayoutPanel topPropPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false, 
                AutoScroll = true, 
                Padding = new Padding(5) 
            };

            // ==========================================
            // 1. 快速對齊區塊
            // ==========================================
            GroupBox gbAlign = new GroupBox { Text = "快速對齊", Width = 285, Height = 150, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(5), Margin = new Padding(0, 0, 0, 10) };
            _chkAlignToPage = new CheckBox { Text = "對齊畫布邊緣", Dock = DockStyle.Top, Font = new Font("Arial", 9), ForeColor = Color.DimGray, Height = 25, Padding = new Padding(5, 0, 0, 0) };
            gbAlign.Controls.Add(_chkAlignToPage);

            _alignmentPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            TableLayoutPanel tlpAlign = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            for (int i = 0; i < 3; i++) tlpAlign.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            for (int i = 0; i < 3; i++) tlpAlign.RowStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            tlpAlign.Controls.Add(CreateGridButton("靠左", (s, e) => AlignShapes("Left")), 0, 0);
            tlpAlign.Controls.Add(CreateGridButton("置中", (s, e) => AlignShapes("Center")), 1, 0);
            tlpAlign.Controls.Add(CreateGridButton("靠右", (s, e) => AlignShapes("Right")), 2, 0);
            tlpAlign.Controls.Add(CreateGridButton("靠上", (s, e) => AlignShapes("Top")), 0, 1);
            tlpAlign.Controls.Add(CreateGridButton("垂直置中", (s, e) => AlignShapes("Middle")), 1, 1);
            tlpAlign.Controls.Add(CreateGridButton("靠下", (s, e) => AlignShapes("Bottom")), 2, 1);
            tlpAlign.Controls.Add(CreateGridButton("水平均分", (s, e) => DistributeShapes("Horizontal")), 0, 2);
            tlpAlign.Controls.Add(CreateGridButton("垂直均分", (s, e) => DistributeShapes("Vertical")), 1, 2);
            
            _alignmentPanel.Controls.Add(tlpAlign);
            gbAlign.Controls.Add(_alignmentPanel);
            topPropPanel.Controls.Add(gbAlign); // 加入瀑布流

            // ==========================================
            // 2. 圖層順序區塊
            // ==========================================
            GroupBox gbZIndex = new GroupBox { Text = "圖層順序", Width = 285, Height = 65, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(5), Margin = new Padding(0, 0, 0, 10) };
            _zIndexPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            TableLayoutPanel tlpZIndex = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpZIndex.Controls.Add(CreateGridButton("移到最上層", (s, e) => { CurrentCanvas?.ChangeZIndex(0); RefreshLayerTree(); }), 0, 0);
            tlpZIndex.Controls.Add(CreateGridButton("移到最下層", (s, e) => { CurrentCanvas?.ChangeZIndex(-99); RefreshLayerTree(); }), 1, 0);
            _zIndexPanel.Controls.Add(tlpZIndex);
            gbZIndex.Controls.Add(_zIndexPanel);
            topPropPanel.Controls.Add(gbZIndex); // 加入瀑布流

            // ==========================================
            // 3. 自訂屬性面板 (包含文字與外觀，用 FlowLayoutPanel 包裝以便一起隱藏)
            // ==========================================
            _customPropertiesPanel = new FlowLayoutPanel 
            { 
                Width = 285, 
                AutoSize = true, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false,
                Margin = new Padding(0)
            };

            // 3-1. 文字設定區塊
            _gbText = new GroupBox { Text = "文字與排版設定", Width = 280, Height = 160, Font = new Font("Arial", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
            TableLayoutPanel tlpText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(5), Font = new Font("Arial", 9, FontStyle.Regular) };
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            tlpText.Controls.Add(new Label { Text = "字體顏色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 0);
            _btnFontColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnFontColor.Click += (s, e) => PickColor(_btnFontColor, c => ApplyPropertyChange(cmd => cmd.FontColor = c));
            tlpText.Controls.Add(_btnFontColor, 1, 0);

            tlpText.Controls.Add(new Label { Text = "字型/大小", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 1);
            TableLayoutPanel tlpFont = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            _cbFontName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FontFamily font in FontFamily.Families) _cbFontName.Items.Add(font.Name);
            _cbFontName.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontName = _cbFontName.Text);
            _nudFontSize = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 6, Maximum = 144 };
            _nudFontSize.ValueChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontSize = (float)_nudFontSize.Value);
            tlpFont.Controls.Add(_cbFontName, 0, 0); tlpFont.Controls.Add(_nudFontSize, 1, 0);
            tlpText.Controls.Add(tlpFont, 1, 1);

            tlpText.Controls.Add(new Label { Text = "樣式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 2);
            FlowLayoutPanel flpStyle = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            _chkBold = new CheckBox { Text = "粗", AutoSize = true, Width = 40 }; _chkBold.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontBold = _chkBold.Checked);
            _chkItalic = new CheckBox { Text = "斜", AutoSize = true, Width = 40 }; _chkItalic.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontItalic = _chkItalic.Checked);
            _chkUnderline = new CheckBox { Text = "底線", AutoSize = true, Width = 55 }; _chkUnderline.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontUnderline = _chkUnderline.Checked);
            flpStyle.Controls.Add(_chkBold); flpStyle.Controls.Add(_chkItalic); flpStyle.Controls.Add(_chkUnderline);
            tlpText.Controls.Add(flpStyle, 1, 2);

            tlpText.Controls.Add(new Label { Text = "對齊方式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 3);
            _cbTextAlign = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbTextAlign.Items.AddRange(Enum.GetNames(typeof(App_Shapes.TextAlign)));
            _cbTextAlign.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.TextAlignment = (App_Shapes.TextAlign)_cbTextAlign.SelectedIndex);
            tlpText.Controls.Add(_cbTextAlign, 1, 3);

            _gbText.Controls.Add(tlpText);
            _customPropertiesPanel.Controls.Add(_gbText); // 加入內部瀑布流

            // 3-2. 外觀設定區塊
            _gbAppearance = new GroupBox { Text = "外觀與線條設定", Width = 280, Height = 210, Font = new Font("Arial", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
            TableLayoutPanel tlpApp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(5), Font = new Font("Arial", 9, FontStyle.Regular) };
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            tlpApp.Controls.Add(new Label { Text = "邊框顏色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 0);
            _btnShapeColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnShapeColor.Click += (s, e) => PickColor(_btnShapeColor, c => ApplyPropertyChange(cmd => cmd.ShapeColor = c));
            tlpApp.Controls.Add(_btnShapeColor, 1, 0);

            tlpApp.Controls.Add(new Label { Text = "填色類型", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 1);
            _cbBrushType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbBrushType.Items.AddRange(new string[] { "純色填充", "線性漸層" });
            _cbBrushType.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FillBrushType = (App_Shapes.BrushType)_cbBrushType.SelectedIndex);
            tlpApp.Controls.Add(_cbBrushType, 1, 1);

            tlpApp.Controls.Add(new Label { Text = "主副填色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 2);
            TableLayoutPanel tlpColor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            tlpColor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tlpColor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _btnFillColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnFillColor.Click += (s, e) => PickColor(_btnFillColor, c => ApplyPropertyChange(cmd => cmd.FillColor = c), true);
            _btnGradientColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnGradientColor.Click += (s, e) => PickColor(_btnGradientColor, c => ApplyPropertyChange(cmd => cmd.GradientColor2 = c));
            tlpColor.Controls.Add(_btnFillColor, 0, 0); tlpColor.Controls.Add(_btnGradientColor, 1, 0);
            tlpApp.Controls.Add(tlpColor, 1, 2);

            tlpApp.Controls.Add(new Label { Text = "線條粗細", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 3);
            FlowLayoutPanel flpStroke = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            _tbStrokeWidth = new TrackBar { Width = 140, Minimum = 1, Maximum = 20, TickStyle = TickStyle.None };
            _lblStrokeWidthValue = new Label { Text = "2", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _tbStrokeWidth.ValueChanged += (s, e) => {
                _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString();
                ApplyPropertyChange(cmd => cmd.StrokeWidth = _tbStrokeWidth.Value);
            };
            flpStroke.Controls.Add(_tbStrokeWidth); flpStroke.Controls.Add(_lblStrokeWidthValue);
            tlpApp.Controls.Add(flpStroke, 1, 3);

            tlpApp.Controls.Add(new Label { Text = "線條樣式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 5, 0, 0) }, 0, 4);
            _cbDashStyle = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbDashStyle.Items.AddRange(new string[] { "實線 (Solid)", "虛線 (Dash)", "點線 (Dot)", "點虛線 (DashDot)" });
            _cbDashStyle.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.StrokeDashStyle = (DashStyle)_cbDashStyle.SelectedIndex);
            tlpApp.Controls.Add(_cbDashStyle, 1, 4);

            _chkShadow = new CheckBox { Text = "啟用立體陰影", AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            _chkShadow.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.EnableShadow = _chkShadow.Checked);
            tlpApp.Controls.Add(_chkShadow, 1, 5);

            _gbAppearance.Controls.Add(tlpApp);
            _customPropertiesPanel.Controls.Add(_gbAppearance); // 加入內部瀑布流

            topPropPanel.Controls.Add(_customPropertiesPanel); // 加入外部主瀑布流

            // ==========================================
            // 完成排版，綁定至 SplitContainer
            // ==========================================
            scRight.Panel1.Controls.Add(topPropPanel);

            // 呼叫 Partial Class 的方法建立圖層面板 (在下半部)
            BuildLayerPanel(scRight.Panel2);

            _rightPanel.Controls.Add(scRight);

            // 初始狀態：禁用 (反灰)，但不隱藏
            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
            _customPropertiesPanel.Enabled = false;
        }

        // 專為 TableLayoutPanel 設計的彈性按鈕
        private Button CreateGridButton(string text, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, Cursor = Cursors.Hand, Font = new Font("微軟正黑體", 8),
                Margin = new Padding(2) 
            };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.Click += onClick;
            return btn;
        }

        private void RefreshPropertyPanel()
        {
            if (CurrentCanvas != null)
            {
                int selCount = CurrentCanvas.SelectedShapes.Count;
                
                _alignmentPanel.Enabled = _chkAlignToPage.Checked ? selCount > 0 : selCount > 1;
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

        private void PickColor(Button btn, Action<Color> applyAction, bool allowTransparent = false)
        {
            using (ColorDialog cd = new ColorDialog { Color = btn.BackColor })
            {
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

        private void AlignShapes(string type)
        {
            if (CurrentCanvas == null || CurrentCanvas.SelectedShapes.Count == 0) return;
            var shapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            if (shapes.Count == 0 || (!_chkAlignToPage.Checked && shapes.Count < 2)) return;

            var oldBounds = shapes.Select(s => s.Bounds).ToList();
            var newBounds = new List<RectangleF>();
            float refVal = 0;

            if (_chkAlignToPage.Checked)
            {
                SizeF ps = CurrentCanvas.PageSize;
                switch (type)
                {
                    case "Left": foreach (var s in shapes) newBounds.Add(new RectangleF(0, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Right": foreach (var s in shapes) newBounds.Add(new RectangleF(ps.Width - s.Bounds.Width, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Center": foreach (var s in shapes) newBounds.Add(new RectangleF(ps.Width / 2 - s.Bounds.Width / 2, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Top": foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, 0, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Bottom": foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, ps.Height - s.Bounds.Height, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Middle": foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, ps.Height / 2 - s.Bounds.Height / 2, s.Bounds.Width, s.Bounds.Height)); break;
                }
            }
            else
            {
                switch (type)
                {
                    case "Left": refVal = shapes.Min(s => s.Bounds.Left); foreach (var s in shapes) newBounds.Add(new RectangleF(refVal, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Right": refVal = shapes.Max(s => s.Bounds.Right); foreach (var s in shapes) newBounds.Add(new RectangleF(refVal - s.Bounds.Width, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Center": refVal = shapes.Average(s => s.Bounds.X + s.Bounds.Width / 2); foreach (var s in shapes) newBounds.Add(new RectangleF(refVal - s.Bounds.Width / 2, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Top": refVal = shapes.Min(s => s.Bounds.Top); foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, refVal, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Bottom": refVal = shapes.Max(s => s.Bounds.Bottom); foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, refVal - s.Bounds.Height, s.Bounds.Width, s.Bounds.Height)); break;
                    case "Middle": refVal = shapes.Average(s => s.Bounds.Y + s.Bounds.Height / 2); foreach (var s in shapes) newBounds.Add(new RectangleF(s.Bounds.X, refVal - s.Bounds.Height / 2, s.Bounds.Width, s.Bounds.Height)); break;
                }
            }
            CurrentCanvas.CmdManager.ExecuteCommand(new TransformShapesCommand(shapes, oldBounds, newBounds));
        }

        private void DistributeShapes(string type)
        {
            if (CurrentCanvas == null) return;
            var shapes = CurrentCanvas.SelectedShapes.Where(s => !s.IsLocked).ToList();
            if (shapes.Count < 3) return;

            var oldBounds = shapes.Select(s => s.Bounds).ToList();
            var newBounds = new List<RectangleF>();

            if (type == "Horizontal")
            {
                shapes = shapes.OrderBy(s => s.Bounds.X).ToList();
                float totalSpace = shapes.Last().Bounds.Right - shapes.First().Bounds.Left;
                float gap = (totalSpace - shapes.Sum(s => s.Bounds.Width)) / (shapes.Count - 1);
                
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
                float gap = (totalSpace - shapes.Sum(s => s.Bounds.Height)) / (shapes.Count - 1);
                
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
                orderedNewBounds.Add(newBounds[shapes.IndexOf(originalShapes[i])]);
            }

            CurrentCanvas.CmdManager.ExecuteCommand(new TransformShapesCommand(originalShapes, oldBounds, orderedNewBounds));
        }
    }
}
