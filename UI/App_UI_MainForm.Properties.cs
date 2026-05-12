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
    // --- 負責右側的屬性面板 (外觀、文字、對齊、順序、圖層) 與屬性連動 ---
    public partial class App_UI_MainForm
    {
        private FlowLayoutPanel _topPropPanel;

        private void BuildRightPanel()
        {
            _rightPanel = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Color.FromArgb(245, 245, 245) };

            // 最外層容器，確保出現卷軸且所有收合面板能順暢往下排
            _topPropPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                FlowDirection = FlowDirection.TopDown, 
                WrapContents = false, 
                AutoScroll = true, 
                Padding = new Padding(5, 5, 5, 10) 
            };

            Font contentFont = new Font("微軟正黑體", 9, FontStyle.Regular);

            // ==========================================
            // 【大框 1】外觀與線條設定
            // ==========================================
            TableLayoutPanel tlpApp = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Width = 265, Font = contentFont, Padding = new Padding(5, 10, 5, 5) };
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75)); // 標籤寬度
            tlpApp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 控制項寬度

            // 1. 邊框顏色
            tlpApp.Controls.Add(new Label { Text = "邊框顏色", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 0);
            _btnShapeColor = new Button { Height = 25, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _btnShapeColor.Click += (s, e) => PickColor(_btnShapeColor, c => ApplyPropertyChange(cmd => cmd.ShapeColor = c));
            tlpApp.Controls.Add(_btnShapeColor, 1, 0);

            // 2. 填色類型
            tlpApp.Controls.Add(new Label { Text = "填色類型", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 1);
            _cbBrushType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _cbBrushType.Items.AddRange(new string[] { "純色填充", "線性漸層" });
            _cbBrushType.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FillBrushType = (App_Shapes.BrushType)_cbBrushType.SelectedIndex);
            tlpApp.Controls.Add(_cbBrushType, 1, 1);

            // 3. 主副填色
            tlpApp.Controls.Add(new Label { Text = "主副填色", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 2);
            TableLayoutPanel tlpColors = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            tlpColors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tlpColors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _btnFillColor = new Button { Height = 25, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 2, 0) };
            _btnFillColor.Click += (s, e) => PickColor(_btnFillColor, c => ApplyPropertyChange(cmd => cmd.FillColor = c), true);
            _btnGradientColor = new Button { Height = 25, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Margin = new Padding(2, 0, 0, 0) };
            _btnGradientColor.Click += (s, e) => PickColor(_btnGradientColor, c => ApplyPropertyChange(cmd => cmd.GradientColor2 = c));
            tlpColors.Controls.Add(_btnFillColor, 0, 0); tlpColors.Controls.Add(_btnGradientColor, 1, 0);
            tlpApp.Controls.Add(tlpColors, 1, 2);

            // 4. 線條粗細 (修正高度撐開問題)
            tlpApp.Controls.Add(new Label { Text = "線條粗細", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 3);
            TableLayoutPanel tlpStroke = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            tlpStroke.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tlpStroke.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));
            _tbStrokeWidth = new TrackBar { Minimum = 1, Maximum = 20, TickStyle = TickStyle.None, Dock = DockStyle.Fill, AutoSize = false, Height = 25, Margin = new Padding(0) };
            _lblStrokeWidthValue = new Label { Text = "2", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, Margin = new Padding(0) };
            _tbStrokeWidth.ValueChanged += (s, e) => { _lblStrokeWidthValue.Text = _tbStrokeWidth.Value.ToString(); ApplyPropertyChange(cmd => cmd.StrokeWidth = _tbStrokeWidth.Value); };
            tlpStroke.Controls.Add(_tbStrokeWidth, 0, 0); tlpStroke.Controls.Add(_lblStrokeWidthValue, 1, 0);
            tlpApp.Controls.Add(tlpStroke, 1, 3);

            // 5. 線條樣式
            tlpApp.Controls.Add(new Label { Text = "線條樣式", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 4);
            _cbDashStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _cbDashStyle.Items.AddRange(new string[] { "實線 (Solid)", "虛線 (Dash)", "點線 (Dot)", "點虛線 (DashDot)" });
            _cbDashStyle.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.StrokeDashStyle = (DashStyle)_cbDashStyle.SelectedIndex);
            tlpApp.Controls.Add(_cbDashStyle, 1, 4);

            // 6. 啟用立體陰影
            _chkShadow = new CheckBox { Text = "啟用立體陰影", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            _chkShadow.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.EnableShadow = _chkShadow.Checked);
            tlpApp.Controls.Add(_chkShadow, 1, 5);

            _topPropPanel.Controls.Add(CreateCollapsiblePanel("外觀與線條設定", tlpApp));


            // ==========================================
            // 【大框 2】文字與排版設定
            // ==========================================
            TableLayoutPanel tlpText = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Width = 265, Font = contentFont, Padding = new Padding(5, 10, 5, 5) };
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            tlpText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 1. 字體顏色
            tlpText.Controls.Add(new Label { Text = "字體顏色", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 0);
            _btnFontColor = new Button { Height = 25, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _btnFontColor.Click += (s, e) => PickColor(_btnFontColor, c => ApplyPropertyChange(cmd => cmd.FontColor = c));
            tlpText.Controls.Add(_btnFontColor, 1, 0);

            // 2. 字型與大小
            tlpText.Controls.Add(new Label { Text = "字型/大小", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 1);
            TableLayoutPanel tlpFont = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tlpFont.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
            _cbFontName = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 2, 0) };
            foreach (FontFamily font in FontFamily.Families) _cbFontName.Items.Add(font.Name);
            _cbFontName.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontName = _cbFontName.Text);
            _nudFontSize = new NumericUpDown { Minimum = 6, Maximum = 144, Dock = DockStyle.Fill, Margin = new Padding(0) };
            _nudFontSize.ValueChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontSize = (float)_nudFontSize.Value);
            tlpFont.Controls.Add(_cbFontName, 0, 0); tlpFont.Controls.Add(_nudFontSize, 1, 0);
            tlpText.Controls.Add(tlpFont, 1, 1);

            // 3. 文字樣式
            tlpText.Controls.Add(new Label { Text = "文字樣式", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 2);
            FlowLayoutPanel flpStyle = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _chkBold = new CheckBox { Text = "粗", AutoSize = true, Width = 38, Margin = new Padding(0, 3, 3, 0) }; _chkBold.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontBold = _chkBold.Checked);
            _chkItalic = new CheckBox { Text = "斜", AutoSize = true, Width = 38, Margin = new Padding(0, 3, 3, 0) }; _chkItalic.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontItalic = _chkItalic.Checked);
            _chkUnderline = new CheckBox { Text = "底線", AutoSize = true, Width = 50, Margin = new Padding(0, 3, 0, 0) }; _chkUnderline.CheckedChanged += (s, e) => ApplyPropertyChange(cmd => cmd.FontUnderline = _chkUnderline.Checked);
            flpStyle.Controls.Add(_chkBold); flpStyle.Controls.Add(_chkItalic); flpStyle.Controls.Add(_chkUnderline);
            tlpText.Controls.Add(flpStyle, 1, 2);

            // 4. 對齊方式
            tlpText.Controls.Add(new Label { Text = "對齊方式", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) }, 0, 3);
            _cbTextAlign = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            _cbTextAlign.Items.AddRange(new string[] { "左上", "中上", "右上", "左中", "正中", "右中", "左下", "中下", "右下" });
            _cbTextAlign.SelectedIndexChanged += (s, e) => ApplyPropertyChange(cmd => cmd.TextAlignment = (App_Shapes.TextAlign)_cbTextAlign.SelectedIndex);
            tlpText.Controls.Add(_cbTextAlign, 1, 3);

            _topPropPanel.Controls.Add(CreateCollapsiblePanel("文字與排版設定", tlpText));


            // ==========================================
            // 【大框 3】快速對齊 (加寬按鈕設計)
            // ==========================================
            // 把 Padding 改小，讓裡面的元件有更多延展空間
            FlowLayoutPanel flpAlignMain = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, Width = 265, Font = contentFont, Padding = new Padding(0, 5, 0, 10) };
            
            _chkAlignToPage = new CheckBox { Text = "對齊畫布邊緣 (否則相對物件對齊)", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(5, 5, 0, 5) };
            
            // Width 撐到最滿 (265)，讓兩欄按鈕變得更寬
            TableLayoutPanel tlpAlignGrid = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, AutoSize = true, Width = 265, Margin = new Padding(0) };
            tlpAlignGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpAlignGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // 左欄：水平相關 / 右欄：垂直相關
            tlpAlignGrid.Controls.Add(CreateAlignButton("靠左對齊", () => AlignShapes("Left")), 0, 0);   
            tlpAlignGrid.Controls.Add(CreateAlignButton("靠上對齊", () => AlignShapes("Top")), 1, 0);
            
            tlpAlignGrid.Controls.Add(CreateAlignButton("水平置中", () => AlignShapes("Center")), 0, 1); 
            tlpAlignGrid.Controls.Add(CreateAlignButton("垂直置中", () => AlignShapes("Middle")), 1, 1);
            
            tlpAlignGrid.Controls.Add(CreateAlignButton("靠右對齊", () => AlignShapes("Right")), 0, 2);  
            tlpAlignGrid.Controls.Add(CreateAlignButton("靠下對齊", () => AlignShapes("Bottom")), 1, 2);
            
            tlpAlignGrid.Controls.Add(CreateAlignButton("水平均分", () => DistributeShapes("Horizontal")), 0, 3);  
            tlpAlignGrid.Controls.Add(CreateAlignButton("垂直均分", () => DistributeShapes("Vertical")), 1, 3);

            flpAlignMain.Controls.Add(_chkAlignToPage);
            flpAlignMain.Controls.Add(tlpAlignGrid);
            
            _alignmentPanel = flpAlignMain; 
            _topPropPanel.Controls.Add(CreateCollapsiblePanel("快速對齊", _alignmentPanel));


            // ==========================================
            // 【大框 4】圖層順序 (緊密貼合，無留白)
            // ==========================================
            TableLayoutPanel tlpZIndex = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Width = 265, Font = contentFont, Padding = new Padding(0, 10, 0, 10) };
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); 
            tlpZIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            
            Button btnBringTop = new Button { Text = "移到最上層", Height = 28, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
            btnBringTop.FlatAppearance.BorderColor = Color.LightGray;
            btnBringTop.Click += (s, e) => { CurrentCanvas?.ChangeZIndex(0); RefreshLayerTree(); };

            Button btnSendBottom = new Button { Text = "移到最下層", Height = 28, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
            btnSendBottom.FlatAppearance.BorderColor = Color.LightGray;
            btnSendBottom.Click += (s, e) => { CurrentCanvas?.ChangeZIndex(-99); RefreshLayerTree(); };

            tlpZIndex.Controls.Add(btnBringTop, 0, 0); 
            tlpZIndex.Controls.Add(btnSendBottom, 1, 0);
            
            _zIndexPanel = tlpZIndex;
            _topPropPanel.Controls.Add(CreateCollapsiblePanel("圖層順序", _zIndexPanel));


            // ==========================================
            // 【大框 5】圖層管理面板
            // ==========================================
            Control layerContent = BuildLayerPanelContent();
            _topPropPanel.Controls.Add(CreateCollapsiblePanel("圖層管理", layerContent));

            _rightPanel.Controls.Add(_topPropPanel);

            _alignmentPanel.Enabled = false;
            _zIndexPanel.Enabled = false;
        }

        // ==========================================
        // 輔助方法
        // ==========================================
        private Button CreateAlignButton(string text, Action onClick)
        {
            // 將 Dock 設為 Fill，按鈕就會自動撐滿 TableLayoutPanel 的欄位寬度
            Button btn = new Button { Text = text, Height = 28, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Dock = DockStyle.Fill, Margin = new Padding(2) };
            btn.FlatAppearance.BorderColor = Color.LightGray;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 245, 255);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Control CreateCollapsiblePanel(string title, Control contentPanel)
        {
            FlowLayoutPanel container = new FlowLayoutPanel 
            { 
                Width = 270, 
                AutoSize = true, 
                AutoSizeMode = AutoSizeMode.GrowAndShrink, 
                FlowDirection = FlowDirection.TopDown,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button btnToggle = new Button 
            { 
                Text = $"▼ {title}", 
                Width = 270, 
                Height = 32, 
                FlatStyle = FlatStyle.Flat, 
                TextAlign = ContentAlignment.MiddleLeft, 
                BackColor = Color.FromArgb(235, 235, 235),
                Font = new Font("微軟正黑體", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnToggle.FlatAppearance.BorderSize = 0;

            contentPanel.Width = 268; // 配合外框微調
            contentPanel.Margin = new Padding(0);

            btnToggle.Click += (s, e) => {
                contentPanel.Visible = !contentPanel.Visible;
                btnToggle.Text = contentPanel.Visible ? $"▼ {title}" : $"▶ {title}";
            };

            container.Controls.Add(btnToggle);
            container.Controls.Add(contentPanel);

            return container;
        }

        // ====== 屬性連動與邏輯保持不變 ======
        private void RefreshPropertyPanel()
        {
            if (CurrentCanvas != null)
            {
                int selCount = CurrentCanvas.SelectedShapes.Count;
                
                _alignmentPanel.Enabled = _chkAlignToPage.Checked ? selCount > 0 : selCount > 1;
                _zIndexPanel.Enabled = selCount > 0;
                
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

            if (type == "Horizontal") shapes = shapes.OrderBy(s => s.Bounds.X).ToList();
            else if (type == "Vertical") shapes = shapes.OrderBy(s => s.Bounds.Y).ToList();

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
