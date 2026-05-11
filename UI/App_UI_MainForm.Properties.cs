// ============================================================
// FILE: UI/App_UI_MainForm.Properties.cs
// ============================================================

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
            _rightPanel = new Panel { Dock = DockStyle.Right, Width = 320, BackColor = Color.FromArgb(245, 245, 245) };

            SplitContainer scRight = new SplitContainer 
            { 
                Orientation = Orientation.Horizontal, 
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2
            };

            this.Load += (s, e) => 
            {
                try
                {
                    scRight.Panel2MinSize = 150; 
                    if (scRight.Height > 400) scRight.SplitterDistance = scRight.Height - 250;
                } catch { }
            };

            FlowLayoutPanel topPropPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false, 
                AutoScroll = true, 
                Padding = new Padding(5) 
            };

            _customPropertiesPanel = new FlowLayoutPanel 
            { 
                Width = 285, 
                AutoSize = true, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false,
                Margin = new Padding(0)
            };

            // ==========================================
            // 【區塊 1】外觀與線條設定
            // ==========================================
            _gbAppearance = new GroupBox { Text = "外觀與線條設定", Width = 285, Height = 250, Font = new Font("Arial", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
            TableLayoutPanel tlpApp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(5, 8, 5, 5), Font = new Font("Arial", 9, FontStyle.Regular) };
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            tlpApp.Controls.Add(new Label { Text = "邊框顏色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 0);
            _btnShapeColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnShapeColor.Click += (s, e) => PickColor(_btnShapeColor, c => ApplyPropertyChange(cmd => cmd.ShapeColor = c));
            tlpApp.Controls.Add(_btnShapeColor, 1, 0);

            tlpApp.Controls.Add(new Label { Text = "填色類型", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 1);
            _cbBrushType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbBrushType.Items.AddRange(new string[] { "純色填充", "線性漸層" });
            _cbBrushType.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FillBrushType = (App_Shapes.BrushType)_cbBrushType.SelectedIndex);
            tlpApp.Controls.Add(_cbBrushType, 1, 1);

            tlpApp.Controls.Add(new Label { Text = "主副填色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 2);
            TableLayoutPanel tlpColor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            tlpColor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tlpColor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _btnFillColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnFillColor.Click += (s, e) => PickColor(_btnFillColor, c => ApplyPropertyChange(cmd => cmd.FillColor = c), true);
            _btnGradientColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnGradientColor.Click += (s, e) => PickColor(_btnGradientColor, c => ApplyPropertyChange(cmd => cmd.GradientColor2 = c));
            tlpColor.Controls.Add(_btnFillColor, 0, 0); tlpColor.Controls.Add(_btnGradientColor, 1, 0);
            tlpApp.Controls.Add(tlpColor, 1, 2);

            tlpApp.Controls.Add(new Label { Text = "線條粗細", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 10, 0, 0) }, 0, 3);
            FlowLayoutPanel flpStroke = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            _tbStrokeWidth = new TrackBar { Width = 140, Minimum = 1, Maximum = 20, TickStyle = TickStyle.None };
            _lblStrokeWidthValue = new Label { Text = "2", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            _tbStrokeWidth.ValueChanged += (s, e) => {
                _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString();
                ApplyPropertyChange(cmd => cmd.StrokeWidth = _tbStrokeWidth.Value);
            };
            flpStroke.Controls.Add(_tbStrokeWidth); flpStroke.Controls.Add(_lblStrokeWidthValue);
            tlpApp.Controls.Add(flpStroke, 1, 3);

            tlpApp.Controls.Add(new Label { Text = "線條樣式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 4);
            _cbDashStyle = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbDashStyle.Items.AddRange(new string[] { "實線 (Solid)", "虛線 (Dash)", "點線 (Dot)", "點虛線 (DashDot)" });
            _cbDashStyle.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.StrokeDashStyle = (DashStyle)_cbDashStyle.SelectedIndex);
            tlpApp.Controls.Add(_cbDashStyle, 1, 4);

            _chkShadow = new CheckBox { Text = "啟用立體陰影", AutoSize = true, Margin = new Padding(0, 7, 0, 0) };
            _chkShadow.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.EnableShadow = _chkShadow.Checked);
            tlpApp.Controls.Add(_chkShadow, 1, 5);

            _gbAppearance.Controls.Add(tlpApp);
            _customPropertiesPanel.Controls.Add(_gbAppearance); 

            // ==========================================
            // 【區塊 2】文字與排版設定
            // ==========================================
            _gbText = new GroupBox { Text = "文字與排版設定", Width = 285, Height = 200, Font = new Font("Arial", 9, FontStyle.Bold), Margin = new Padding(0, 0, 0, 10) };
            TableLayoutPanel tlpText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(5, 8, 5, 5), Font = new Font("Arial", 9, FontStyle.Regular) };
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            tlpText.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            tlpText.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            tlpText.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            tlpText.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));

            tlpText.Controls.Add(new Label { Text = "字體顏色", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 0);
            _btnFontColor = new Button { Dock = DockStyle.Fill, Height = 25, FlatStyle = FlatStyle.Flat };
            _btnFontColor.Click += (s, e) => PickColor(_btnFontColor, c => ApplyPropertyChange(cmd => cmd.FontColor = c));
            tlpText.Controls.Add(_btnFontColor, 1, 0);

            tlpText.Controls.Add(new Label { Text = "字型/大小", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 1);
            TableLayoutPanel tlpFont = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            _cbFontName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (FontFamily font in FontFamily.Families) _cbFontName.Items.Add(font.Name);
            _cbFontName.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontName = _cbFontName.Text);
            _nudFontSize = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 6, Maximum = 144 };
            _nudFontSize.ValueChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontSize = (float)_nudFontSize.Value);
            tlpFont.Controls.Add(_cbFontName, 0, 0); tlpFont.Controls.Add(_nudFontSize, 1, 0);
            tlpText.Controls.Add(tlpFont, 1, 1);

            tlpText.Controls.Add(new Label { Text = "樣式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 2);
            FlowLayoutPanel flpStyle = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            _chkBold = new CheckBox { Text = "粗", AutoSize = true, Width = 40 }; _chkBold.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontBold = _chkBold.Checked);
            _chkItalic = new CheckBox { Text = "斜", AutoSize = true, Width = 40 }; _chkItalic.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontItalic = _chkItalic.Checked);
            _chkUnderline = new CheckBox { Text = "底線", AutoSize = true, Width = 55 }; _chkUnderline.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontUnderline = _chkUnderline.Checked);
            flpStyle.Controls.Add(_chkBold); flpStyle.Controls.Add(_chkItalic); flpStyle.Controls.Add(_chkUnderline);
            tlpText.Controls.Add(flpStyle, 1, 2);

            tlpText.Controls.Add(new Label { Text = "對齊方式", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 3);
            _cbTextAlign = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cbTextAlign.Items.AddRange(Enum.GetNames(typeof(App_Shapes.TextAlign)));
            _cbTextAlign.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.TextAlignment = (App_Shapes.TextAlign)_cbTextAlign.SelectedIndex);
            tlpText.Controls.Add(_cbTextAlign, 1, 3);

            _gbText.Controls.Add(tlpText);
            _customPropertiesPanel.Controls.Add(_gbText); 

            topPropPanel.Controls.Add(_customPropertiesPanel); 

            // ==========================================
            // 【Req 10: 快速對齊區塊 (預設隱藏並加摺疊按鈕)】
            // ==========================================
            _gbAlign = new GroupBox { Text = "▶ 快速對齊", Width = 285, Height = 25, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(5), Margin = new Padding(0, 0, 0, 10), Cursor = Cursors.Hand };
            _gbAlign.Click += (s, e) => ToggleGroupBox(_gbAlign, 125, "快速對齊");
            
            _chkAlignToPage = new CheckBox { Text = "對齊畫布邊緣", Dock = DockStyle.Top, Font = new Font("Arial", 9), ForeColor = Color.DimGray, Height = 25, Padding = new Padding(5, 0, 0, 0), Visible = false };
            _gbAlign.Controls.Add(_chkAlignToPage);

            _alignmentPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Visible = false };
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
            _gbAlign.Controls.Add(_alignmentPanel);
            topPropPanel.Controls.Add(_gbAlign); 

            // ==========================================
            // 【Req 10: 圖層順序區塊 (預設隱藏並加摺疊按鈕)】
            // ==========================================
            _gbZIndex = new GroupBox { Text = "▶ 圖層順序", Width = 285, Height = 25, Font = new Font("Arial", 9, FontStyle.Bold), Padding = new Padding(5), Margin = new Padding(0, 0, 0, 10), Cursor = Cursors.Hand };
            _gbZIndex.Click += (s, e) => ToggleGroupBox(_gbZIndex, 65, "圖層順序");

            _zIndexPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Visible = false };
            TableLayoutPanel tlpZIndex = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpZIndex.Controls.Add(CreateGridButton("移到最上層", (s, e) => { CurrentCanvas?.ChangeZIndex(0); RefreshLayerTree(); }), 0, 0);
            tlpZIndex.Controls.Add(CreateGridButton("移到最下層", (s, e) => { CurrentCanvas?.ChangeZIndex(-99); RefreshLayerTree(); }), 1, 0);
            _zIndexPanel.Controls.Add(tlpZIndex);
            _gbZIndex.Controls.Add(_zIndexPanel);
            topPropPanel.Controls.Add(_gbZIndex); 

            scRight.Panel1.Controls.Add(topPropPanel);

            // 【區塊 5】建立圖層面板
            BuildLayerPanel(scRight.Panel2);

            _rightPanel.Controls.Add(scRight);

            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
            
            // 【Req 9: 一開始即使沒有選取物件，也開放使用者修改預設格式】
            _customPropertiesPanel.Enabled = true;
        }

        // 動態摺疊 GroupBox
        private void ToggleGroupBox(GroupBox gb, int expandHeight, string title)
        {
            if (gb.Height == 25)
            {
                gb.Height = expandHeight;
                gb.Text = "▼ " + title;
                foreach (Control c in gb.Controls) c.Visible = true;
            }
            else
            {
                gb.Height = 25;
                gb.Text = "▶ " + title;
                foreach (Control c in gb.Controls) c.Visible = false;
            }
        }

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
                
                // 【Req 9: 取選中物件或預設格式來更新 UI】
                App_Shapes.ShapeBase shapeToRead = selCount > 0 ? CurrentCanvas.SelectedShapes[0] : CurrentCanvas.DefaultFormatTemplate;
                
                _isUpdatingUI = true; 
                
                _btnShapeColor.BackColor = shapeToRead.ShapeColor;
                _btnFillColor.BackColor = shapeToRead.FillColor;
                _btnFillColor.Text = shapeToRead.FillColor == Color.Transparent ? "透明" : "";
                
                _btnGradientColor.BackColor = shapeToRead.GradientColor2;
                _cbBrushType.SelectedIndex = (int)shapeToRead.FillBrushType;
                _chkShadow.Checked = shapeToRead.EnableShadow;
                
                _tbStrokeWidth.Value = Math.Max(1, Math.Min(20, (int)shapeToRead.StrokeWidth));
                _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString();
                _cbDashStyle.SelectedIndex = (int)shapeToRead.StrokeDashStyle;

                _btnFontColor.BackColor = shapeToRead.FontColor;
                if (_cbFontName.Items.Contains(shapeToRead.FontName)) _cbFontName.SelectedItem = shapeToRead.FontName;
                _nudFontSize.Value = (decimal)shapeToRead.FontSize;
                
                _chkBold.Checked = shapeToRead.FontBold;
                _chkItalic.Checked = shapeToRead.FontItalic;
                _chkUnderline.Checked = shapeToRead.FontUnderline;

                _cbTextAlign.SelectedIndex = (int)shapeToRead.TextAlignment;

                _isUpdatingUI = false; 
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
            if (_isUpdatingUI || CurrentCanvas == null) return;

            // 【Req 9: 無論有無選取物件，皆將變更寫入 DefaultFormatTemplate 記憶格式】
            propertySetter(CurrentCanvas.DefaultFormatTemplate);

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

            if (type == "Horizontal")
            {
                shapes = shapes.OrderBy(s => s.Bounds.X).ToList();
            }
            else if (type == "Vertical")
            {
                shapes = shapes.OrderBy(s => s.Bounds.Y).ToList();
            }

            var oldBounds = shapes.Select(s => s.Bounds).ToList();
            var newBounds = new List<RectangleF>();

            if (type == "Horizontal")
            {
                float firstX = shapes.First().Bounds.X;
                float lastX = shapes.Last().Bounds.X;
                float step = (lastX - firstX) / (shapes.Count - 1);

                for (int i = 0; i < shapes.Count; i++)
                {
                    var s = shapes[i];
                    newBounds.Add(new RectangleF(firstX + i * step, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height));
                }
            }
            else if (type == "Vertical")
            {
                float firstY = shapes.First().Bounds.Y;
                float lastY = shapes.Last().Bounds.Y;
                float step = (lastY - firstY) / (shapes.Count - 1);

                for (int i = 0; i < shapes.Count; i++)
                {
                    var s = shapes[i];
                    newBounds.Add(new RectangleF(s.Bounds.X, firstY + i * step, s.Bounds.Width, s.Bounds.Height));
                }
            }

            CurrentCanvas.CmdManager.ExecuteCommand(new TransformShapesCommand(shapes, oldBounds, newBounds));
        }
    }
}
