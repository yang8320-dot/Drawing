// ============================================================
// FILE: UI/App_UI_MainForm.Layers.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    // --- 負責圖層管理與支援拖曳排序 ---
    public partial class App_UI_MainForm
    {
        private void BuildLayerPanel(Control parent)
        {
            // 【修正4】: 移除 Label，直接讓 TreeView 填滿 SplitContainer Panel2 空間
            Panel layerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            _tvLayers = new TreeView 
            { 
                Dock = DockStyle.Fill, 
                HideSelection = false,
                FullRowSelect = true,
                ItemHeight = 22,
                Font = new Font("微軟正黑體", 9),
                AllowDrop = true
            };
            
            _tvLayers.AfterSelect += TvLayers_AfterSelect;
            _tvLayers.ItemDrag += TvLayers_ItemDrag;
            _tvLayers.DragEnter += TvLayers_DragEnter;
            _tvLayers.DragDrop += TvLayers_DragDrop;

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
            parent.Controls.Add(layerPanel);
        }

        private void TvLayers_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Item is TreeNode node && node.Tag is App_Shapes.ShapeBase shape && !shape.IsLocked)
            {
                if (node.Parent != null) return; 
                DoDragDrop(e.Item, DragDropEffects.Move);
            }
        }

        private void TvLayers_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void TvLayers_DragDrop(object sender, DragEventArgs e)
        {
            if (CurrentCanvas == null) return;
            
            Point targetPoint = _tvLayers.PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = _tvLayers.GetNodeAt(targetPoint);
            TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));

            if (draggedNode != null && targetNode != null && !draggedNode.Equals(targetNode))
            {
                if (draggedNode.Tag is App_Shapes.ShapeBase draggedShape && targetNode.Tag is App_Shapes.ShapeBase targetShape)
                {
                    CurrentCanvas.Shapes.Remove(draggedShape);
                    int targetIndex = CurrentCanvas.Shapes.IndexOf(targetShape);
                    
                    if (targetPoint.Y > draggedNode.Bounds.Y)
                        CurrentCanvas.Shapes.Insert(Math.Max(0, targetIndex), draggedShape);
                    else
                        CurrentCanvas.Shapes.Insert(Math.Min(CurrentCanvas.Shapes.Count, targetIndex + 1), draggedShape);

                    CurrentCanvas.Invalidate();
                    RefreshLayerTree();
                    _isDirty = true;
                    UpdateWindowTitle();
                }
            }
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
            TreeNode node = new TreeNode(GetShapeName(shape)) { Tag = shape };

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
            else if (shape is App_Shapes.ParallelogramShape) name = "平行四邊形";
            else if (shape is App_Shapes.CylinderShape) name = "資料庫";
            else if (shape is App_Shapes.DocumentShape) name = "文件";
            else if (shape is App_Shapes.BlockArrowShape) name = "粗箭頭";
            else if (shape is App_Shapes.DoubleArrowShape) name = "雙向箭頭";
            else if (shape is App_Shapes.BraceLeftShape) name = "左大括號";
            else if (shape is App_Shapes.BraceRightShape) name = "右大括號";
            else if (shape is App_Shapes.Branch1To2Shape) name = "一對二分支";
            else if (shape is App_Shapes.Branch1To3Shape) name = "一對三分支";
            else if (shape is App_Shapes.Branch1To4Shape) name = "一對四分支";
            else if (shape is App_Shapes.ConnectorShape) name = "連線";
            else if (shape is App_Shapes.TextNodeShape tns) name = tns.IsTransparent ? "純文字" : "文字框";
            else if (shape is App_Shapes.ImageShape) name = "圖片";
            else if (shape is App_Shapes.FreehandShape) name = "手繪線條";
            else if (shape is App_Shapes.BezierShape) name = "貝茲曲線";
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
                TreeNode foundNode = FindNodeByTag(_tvLayers.Nodes, CurrentCanvas.SelectedShapes[0]);
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
                CurrentCanvas.ClearSelection();
                shape.IsSelected = true;
                CurrentCanvas.SelectedShapes.Add(shape);
                
                CurrentCanvas.Invalidate();
                RefreshPropertyPanel();
            }
        }
    }
}
