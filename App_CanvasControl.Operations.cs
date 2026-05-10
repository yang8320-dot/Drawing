using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    // 負責處理畫布右鍵選單、剪貼簿操作、群組操作與文字編輯器
    public partial class App_CanvasControl
    {
        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("複製 (Ctrl+C)", null, (s, e) => Copy());
            menu.Items.Add("貼上 (Ctrl+V)", null, (s, e) => Paste());
            menu.Items.Add("原地複製 (Ctrl+D)", null, (s, e) => DuplicateSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("群組 (Ctrl+G)", null, (s, e) => GroupSelected());
            menu.Items.Add("解除群組 (Ctrl+U)", null, (s, e) => UngroupSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("移到最上層", null, (s, e) => ChangeZIndex(0));
            menu.Items.Add("移到最下層", null, (s, e) => ChangeZIndex(-99));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("鎖定/解鎖圖形", null, (s, e) => ToggleLock());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("匯出選取物件 (PNG)", null, async (s, e) => {
                if (SelectedShapes.Count == 0) return;
                using (var sfd = new SaveFileDialog() { Filter = "PNG 圖片|*.png" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        await App_Export.ExportSelectionToPngAsync(SelectedShapes, sfd.FileName);
                        MessageBox.Show("局部匯出成功！", "匯出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            });
            return menu;
        }

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var menu = (ContextMenuStrip)sender;
            bool hasSelection = SelectedShapes.Count > 0;
            bool hasGroupSelection = SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape;
            bool hasClipboard = _clipboard.Count > 0;

            menu.Items[0].Enabled = hasSelection; 
            menu.Items[1].Enabled = hasClipboard; 
            menu.Items[2].Enabled = hasSelection; 
            menu.Items[4].Enabled = SelectedShapes.Count > 1; 
            menu.Items[5].Enabled = hasGroupSelection; 
            menu.Items[7].Enabled = hasSelection; 
            menu.Items[8].Enabled = hasSelection; 
            menu.Items[10].Enabled = hasSelection; 
            menu.Items[12].Enabled = hasSelection; 
            
            if (hasSelection)
            {
                bool isAllLocked = SelectedShapes.All(s => s.IsLocked);
                menu.Items[10].Text = isAllLocked ? "解鎖圖形" : "鎖定圖形";
            }
        }
        
        // --- 基本剪貼與群組邏輯 ---
        private void ToggleLock() { if (SelectedShapes.Count > 0) { bool isAllLocked = SelectedShapes.All(s => s.IsLocked); foreach (var s in SelectedShapes) s.IsLocked = !isAllLocked; this.Invalidate(); } }
        public void ChangeZIndex(int direction) { if (SelectedShapes.Count > 0) CmdManager.ExecuteCommand(new ChangeZIndexCommand(Shapes, SelectedShapes, direction)); }
        private void GroupSelected() { if (SelectedShapes.Count < 2) return; var group = new App_Shapes.GroupShape(SelectedShapes.ToList()); CmdManager.ExecuteCommand(new GroupCommand(Shapes, SelectedShapes, group)); ClearSelection(); SelectedShapes.Add(group); group.IsSelected = true; TriggerSelectionChanged(); }
        private void UngroupSelected() { if (SelectedShapes.Count == 1 && SelectedShapes[0] is App_Shapes.GroupShape group && !group.IsLocked) { CmdManager.ExecuteCommand(new UngroupCommand(Shapes, group)); ClearSelection(); foreach (var child in group.Children) { child.IsSelected = true; SelectedShapes.Add(child); } TriggerSelectionChanged(); } }

        private void Copy() { if (SelectedShapes.Count > 0) { _clipboard = App_SaveLoad.CloneShapes(SelectedShapes); } }
        private void Paste() {
            if (_clipboard.Count > 0) {
                ClearSelection();
                var newClones = App_SaveLoad.CloneShapes(_clipboard);
                foreach (var s in newClones) { s.Id = Guid.NewGuid(); s.IsLocked = false; s.Move(20, 20); s.IsSelected = true; SelectedShapes.Add(s); }
                CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, newClones));
                TriggerSelectionChanged();
            }
        }
        private void DuplicateSelected() {
            if (SelectedShapes.Count == 0) return;
            var clones = App_SaveLoad.CloneShapes(SelectedShapes);
            foreach (var c in clones) { c.Id = Guid.NewGuid(); c.IsLocked = false; c.Move(10, 10); }
            CmdManager.ExecuteCommand(new AddShapesCommand(Shapes, clones));
            ClearSelection();
            foreach (var c in clones) { c.IsSelected = true; SelectedShapes.Add(c); }
            TriggerSelectionChanged();
        }

        // --- 文字編輯器邏輯 ---
        private void InitializeInlineEditor()
        {
            _inlineTextBox = new TextBox();
            _inlineTextBox.Multiline = true;
            _inlineTextBox.BorderStyle = BorderStyle.FixedSingle;
            _inlineTextBox.Visible = false;
            
            _inlineTextBox.Leave += (s, e) => CommitInlineText();
            _inlineTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    CommitInlineText();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    CancelInlineText();
                }
            };
            this.Controls.Add(_inlineTextBox);
        }

        public void StartInlineEditing(App_Shapes.ShapeBase shape)
        {
            _editingShape = shape;
            _inlineTextBox.Text = shape.Text;
            
            FontStyle style = FontStyle.Regular;
            if (shape.FontBold) style |= FontStyle.Bold;
            if (shape.FontItalic) style |= FontStyle.Italic;
            if (shape.FontUnderline) style |= FontStyle.Underline;
            
            _inlineTextBox.Font = new Font(shape.FontName, shape.FontSize * ZoomFactor, style);
            _inlineTextBox.ForeColor = shape.FontColor;

            if (shape.TextAlignment == App_Shapes.TextAlign.TopLeft || shape.TextAlignment == App_Shapes.TextAlign.MiddleLeft || shape.TextAlignment == App_Shapes.TextAlign.BottomLeft)
                _inlineTextBox.TextAlign = HorizontalAlignment.Left;
            else if (shape.TextAlignment == App_Shapes.TextAlign.TopRight || shape.TextAlignment == App_Shapes.TextAlign.MiddleRight || shape.TextAlignment == App_Shapes.TextAlign.BottomRight)
                _inlineTextBox.TextAlign = HorizontalAlignment.Right;
            else
                _inlineTextBox.TextAlign = HorizontalAlignment.Center;

            PointF center = shape.GetCenter();
            PointF screenCenter = new PointF(center.X * ZoomFactor + _cameraOffset.X, center.Y * ZoomFactor + _cameraOffset.Y);
            int screenW = (int)(shape.Bounds.Width * ZoomFactor);
            int screenH = (int)(shape.Bounds.Height * ZoomFactor);

            _inlineTextBox.Bounds = new Rectangle((int)screenCenter.X - screenW/2 + 5, (int)screenCenter.Y - screenH/2 + 5, screenW - 10, screenH - 10);
            _inlineTextBox.Visible = true;
            _inlineTextBox.Focus();
            _inlineTextBox.SelectAll();
        }

        private void CommitInlineText()
        {
            if (_editingShape != null)
            {
                _editingShape.Text = _inlineTextBox.Text;
                _editingShape = null;
            }
            _inlineTextBox.Visible = false;
            this.Focus();
            this.Invalidate();
        }

        private void CancelInlineText()
        {
            _editingShape = null;
            _inlineTextBox.Visible = false;
            this.Focus();
        }
    }
}
