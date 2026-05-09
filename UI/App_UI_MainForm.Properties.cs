using System;
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
            _rightPanel = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Color.FromArgb(245, 245, 245) };

            SplitContainer scRight = new SplitContainer 
            { 
                Orientation = Orientation.Horizontal, 
                Dock = DockStyle.Fill,
                SplitterDistance = 550, 
                FixedPanel = FixedPanel.Panel1
            };

            Panel topPropPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            Panel actionsPanel = new Panel { Dock = DockStyle.Top, Height = 160 };

            // -- 快速對齊區塊 --
            Label alignTitle = new Label { Text = "快速對齊", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, 5), AutoSize = true };
            actionsPanel.Controls.Add(alignTitle);
            
            _chkAlignToPage = new CheckBox { Text = "對齊畫布邊緣", Location = new Point(160, 5), AutoSize = true, ForeColor = Color.DimGray };
            actionsPanel.Controls.Add(_chkAlignToPage);

            _alignmentPanel = new FlowLayoutPanel { Location = new Point(0, 30), Width = 280, Height = 70, WrapContents = true };
            _alignmentPanel.Controls.Add(CreateAlignButton("靠左", (s, e) => AlignShapes("Left")));
            _alignmentPanel.Controls.Add(CreateAlignButton("置中", (s, e) => AlignShapes("Center")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠右", (s, e) => AlignShapes("Right")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠上", (s, e) => AlignShapes("Top")));
            _alignmentPanel.Controls.Add(CreateAlignButton("垂直置中", (s, e) => AlignShapes("Middle")));
            _alignmentPanel.Controls.Add(CreateAlignButton("靠下", (s, e) => AlignShapes("Bottom")));
            _alignmentPanel.Controls.Add(CreateAlignButton("水平均分", (s, e) => DistributeShapes("Horizontal")));
            _alignmentPanel.Controls.Add(CreateAlignButton("垂直均分", (s, e) => DistributeShapes("Vertical")));
            actionsPanel.Controls.Add(_alignmentPanel);

            // -- 圖層順序區塊 --
            Label zIndexTitle = new Label { Text = "圖層順序", Font = new Font("Arial", 10, FontStyle.Bold), Location = new Point(0, 105), AutoSize = true };
            actionsPanel.Controls.Add(zIndexTitle);

            _zIndexPanel = new FlowLayoutPanel { Location = new Point(0, 130), Width = 280, Height = 30, WrapContents = true };
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最上層", (s, e) => { CurrentCanvas?.ChangeZIndex(0); RefreshLayerTree(); }));
            _zIndexPanel.Controls.Add(CreateAlignButton("移到最下層", (s, e) => { CurrentCanvas?.ChangeZIndex(-99); RefreshLayerTree(); }));
            actionsPanel.Controls.Add(_zIndexPanel);

            // -- 自訂屬性面板 --
            _customPropertiesPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 10, 0, 0) };

            // 1. 外觀設定區塊 (GroupBox & TableLayoutPanel)
            _gbAppearance = new GroupBox { Text = "外觀與線條設定", Dock = DockStyle.Top, Height = 190, Font = new Font("Arial", 9, FontStyle.Bold) };
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
            _customPropertiesPanel.Controls.Add(_gbAppearance);

            Panel spacer1 = new Panel { Dock = DockStyle.Top, Height = 10 };
            _customPropertiesPanel.Controls.Add(spacer1);

            // 2. 文字設定區塊
            _gbText = new GroupBox { Text = "文字與排版設定", Dock = DockStyle.Top, Height = 160, Font = new Font("Arial", 9, FontStyle.Bold) };
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
            _customPropertiesPanel.Controls.Add(_gbText);
            
            _gbText.BringToFront();
            spacer1.BringToFront();
            _gbAppearance.BringToFront();

            topPropPanel.Controls.Add(_customPropertiesPanel);
            topPropPanel.Controls.Add(actionsPanel);
            scRight.Panel1.Controls.Add(topPropPanel);

            // 呼叫 Partial Class 的方法建立圖層面板
            BuildLayerPanel(scRight.Panel2);

            _rightPanel.Controls.Add(scRight);

            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
            _customPropertiesPanel.Enabled = false;
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
                    
                    _isUpdatingUI = true; // 鎖定事件觸發
                    
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

                    _isUpdatingUI = false; // 解除鎖定
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
    }
}
