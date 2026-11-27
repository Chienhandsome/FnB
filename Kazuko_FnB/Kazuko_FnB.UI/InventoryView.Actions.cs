using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần nạp danh mục lọc, tải dữ liệu tồn kho theo kho, lọc cục bộ, xuất Excel,
    /// phím tắt và hỗ trợ kiểm thử UI cho <see cref="InventoryView"/>.
    /// Phần tô màu, dựng lưới, menu ngữ cảnh và theme đặt ở <c>InventoryView.Render.cs</c>.
    /// </summary>
    internal sealed partial class InventoryView
    {
        public void ReloadAll()
        {
            _loading = true;
            try
            {
                LoadWarehouses();
                LoadProductTypes();
                LoadGroupTree();
            }
            catch (DataAccessException ex)
            {
                _allowedWarehouses = new List<WarehouseRow>();
                _allGroups = new List<ProductGroupRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được danh mục lọc: " + ex.Message;
            }
            finally
            {
                _loading = false;
            }
            ReloadStock();
        }

        public void ReloadStock()
        {
            _loading = true;
            try
            {
                if (!_service.CanView)
                {
                    _warehouseItems = new List<InventoryItemRow>();
                    _visibleItems = new List<InventoryItemRow>();
                    _grid.Rows.Clear();
                    _empty.Text = InventoryConstants.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = "0 mặt hàng";
                    return;
                }

                string whId = SelectedWarehouseId();
                if (string.IsNullOrEmpty(whId))
                {
                    _warehouseItems = new List<InventoryItemRow>();
                    _visibleItems = new List<InventoryItemRow>();
                    _grid.Rows.Clear();
                    _empty.Text = InventoryConstants.EmptyNoWarehouse;
                    _empty.Visible = true;
                    _summary.Text = "0 mặt hàng";
                    return;
                }

                _warehouseItems = _service.LoadWarehouseItems(whId);
            }
            catch (DataAccessException ex)
            {
                _warehouseItems = new List<InventoryItemRow>();
                _visibleItems = new List<InventoryItemRow>();
                _grid.Rows.Clear();
                _empty.Text = "Không đọc được dữ liệu tồn kho: " + ex.Message;
                _empty.Visible = true;
                _summary.Text = "0 mặt hàng";
                return;
            }
            finally
            {
                _loading = false;
            }

            ApplyLocalFilter();
        }

        private void LoadWarehouses()
        {
            string keepId = SelectedWarehouseId();
            _allowedWarehouses = _service.LoadAllowedWarehouses();

            _warehouse.Items.Clear();
            foreach (WarehouseRow w in _allowedWarehouses)
                _warehouse.Items.Add(new LookupItem { Id = w.Id, Name = w.Name });
            _warehouse.DisplayMember = "Name";

            if (_warehouse.Items.Count == 0)
                return;

            string targetId = keepId;
            if (string.IsNullOrEmpty(targetId)
                || !_allowedWarehouses.Exists(w => string.Equals(w.Id, targetId, StringComparison.Ordinal)))
            {
                targetId = _service.ResolveDefaultWarehouseId(_allowedWarehouses);
            }

            int selIdx = 0;
            for (int i = 0; i < _warehouse.Items.Count; i++)
            {
                var li = _warehouse.Items[i] as LookupItem;
                if (li != null && string.Equals(li.Id, targetId, StringComparison.Ordinal))
                {
                    selIdx = i;
                    break;
                }
            }
            _warehouse.SelectedIndex = selIdx;
        }

        private void LoadProductTypes()
        {
            var prev = _productType.SelectedItem as LookupItem;
            string keepId = prev == null ? "" : (prev.Id ?? "");

            List<LookupItem> dbTypes = _service.LoadStockProductTypes();
            string label0 = "Mặt hàng kiêm vật tư";
            string label2 = "Nguyên liệu";
            foreach (LookupItem t in dbTypes)
            {
                if (string.Equals(t.Id, InventoryConstants.TypeProductWithStock, StringComparison.Ordinal))
                    label0 = t.Name;
                else if (string.Equals(t.Id, InventoryConstants.TypeMaterial, StringComparison.Ordinal))
                    label2 = t.Name;
            }

            _productType.Items.Clear();
            _productType.Items.Add(new LookupItem { Id = InventoryConstants.TypeAll, Name = "Tất cả loại" });
            _productType.Items.Add(new LookupItem { Id = InventoryConstants.TypeProductWithStock, Name = label0 });
            _productType.Items.Add(new LookupItem { Id = InventoryConstants.TypeMaterial, Name = label2 });
            _productType.DisplayMember = "Name";

            int selIdx = 0;
            for (int i = 0; i < _productType.Items.Count; i++)
            {
                var li = _productType.Items[i] as LookupItem;
                if (li != null && string.Equals(li.Id, keepId, StringComparison.Ordinal))
                {
                    selIdx = i;
                    break;
                }
            }
            _productType.SelectedIndex = selIdx;
        }

        private void LoadGroupTree()
        {
            string keepGroupId = _selectedGroupId;
            _allGroups = _service.LoadProductGroups();

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byId = new Dictionary<string, ProductGroupRow>(StringComparer.Ordinal);
            foreach (ProductGroupRow g in _allGroups)
                if (!string.IsNullOrEmpty(g.Id))
                    byId[g.Id] = g;

            var byParent = new Dictionary<string, List<ProductGroupRow>>(StringComparer.Ordinal);
            var tops = new List<ProductGroupRow>();
            foreach (ProductGroupRow g in _allGroups)
            {
                if (string.IsNullOrEmpty(g.ParentId) || !byId.ContainsKey(g.ParentId)
                    || string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                {
                    tops.Add(g);
                    continue;
                }
                List<ProductGroupRow> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<ProductGroupRow>();
                kids.Add(g);
            }

            foreach (ProductGroupRow top in tops)
                AddGroupTreeNode(root.Nodes, top, byParent, 0);

            root.ExpandAll();
            TreeNode selected = string.IsNullOrEmpty(keepGroupId) ? root : (FindGroupNode(root, keepGroupId) ?? root);
            _selectedGroupId = selected.Tag as string;
            _tree.SelectedNode = selected;
            _tree.EndUpdate();
        }

        private static void AddGroupTreeNode(
            TreeNodeCollection into,
            ProductGroupRow node,
            Dictionary<string, List<ProductGroupRow>> byParent,
            int depth)
        {
            var tn = new TreeNode(node.Name ?? "") { Tag = node.Id };
            into.Add(tn);
            List<ProductGroupRow> kids;
            if (depth < 64 && byParent.TryGetValue(node.Id, out kids))
            {
                foreach (ProductGroupRow child in kids)
                    AddGroupTreeNode(tn.Nodes, child, byParent, depth + 1);
            }
        }

        private static TreeNode FindGroupNode(TreeNode from, string groupId)
        {
            foreach (TreeNode child in from.Nodes)
            {
                if (string.Equals(child.Tag as string, groupId, StringComparison.Ordinal))
                    return child;
                TreeNode found = FindGroupNode(child, groupId);
                if (found != null)
                    return found;
            }
            return null;
        }

        private string SelectedWarehouseId()
        {
            var li = _warehouse.SelectedItem as LookupItem;
            return li == null ? "" : (li.Id ?? "");
        }

        private InventoryFilter CurrentFilter()
        {
            var selType = _productType.SelectedItem as LookupItem;
            HashSet<string> groupIds = string.IsNullOrEmpty(_selectedGroupId)
                ? null
                : InventoryRules.CollectGroupSubtree(_allGroups, _selectedGroupId);
            return new InventoryFilter
            {
                WarehouseId = SelectedWarehouseId(),
                ProductTypeId = selType == null ? "" : (selType.Id ?? ""),
                GroupId = _selectedGroupId,
                GroupIds = groupIds,
                SearchText = _txtSearch.Text,
                OnlyBelowMinStock = _chkBelowMin.Checked,
                HideZeroStock = _chkHideZero.Checked
            };
        }

        private void ApplyLocalFilter()
        {
            InventoryFilter filter = CurrentFilter();
            _visibleItems = _warehouseItems.FindAll(r => InventoryRules.MatchesFilter(r, filter));
            FillGrid();
        }

        internal bool ExportGridToExcel()
        {
            if (!_canViewBuyPrice)
                return false;
            return GridExcelExporter.Export(
                this, _grid, "TonKho", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal DataGridViewRow FindRowByProductIdForTest(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return null;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var item = r.Tag as InventoryItemRow;
                if (item != null && string.Equals(item.ProductId, productId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        internal bool SelectGroupNodeForTest(string groupId)
        {
            if (_tree.Nodes.Count == 0)
                return false;
            TreeNode root = _tree.Nodes[0];
            TreeNode target = string.IsNullOrEmpty(groupId) ? root : FindGroupNode(root, groupId);
            if (target == null)
                return false;
            _tree.SelectedNode = target;
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadAll();
                return true;
            }
            if (keyData == Keys.F3)
            {
                ReloadStock();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                ReloadStock();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll();
                e.Handled = true;
            }
        }
    }
}
