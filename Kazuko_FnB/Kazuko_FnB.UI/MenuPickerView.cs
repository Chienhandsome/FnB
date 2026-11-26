using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Cột chọn món bên phải màn "Sử dụng dịch vụ" (docs/specs/su-dung-dich-vu.md §2.1, §2.3, §3.2, §3.4):
    /// ô Tìm (F3), cây nhóm mặt hàng ("Tất cả" + nhóm, bỏ nguyên liệu), lưới mặt hàng (Tên hàng + Giá theo khu vực).
    /// Không chứa SQL; ẩn và không đổ giá khi thiếu quyền XemDonGiaTrongHoaDonBanHang (§4).
    /// </summary>
    internal sealed class MenuPickerView : Panel
    {
        private readonly Panel _searchPanel = new Panel { Dock = DockStyle.Top };
        private readonly Label _lblSearch = new Label { Text = "Tìm (F3):", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly InputBox _txtSearch = new InputBox();
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly TreeView _groupTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
        private readonly DataGridView _productGrid = new DataGridView { Dock = DockStyle.Fill };

        private readonly List<ProductGroupRow> _groups = new List<ProductGroupRow>();
        private readonly List<ServiceMenuItem> _allItems = new List<ServiceMenuItem>();
        private readonly List<ServiceMenuItem> _filtered = new List<ServiceMenuItem>();

        private string _selectedGroupId = "";
        private string _priceLevelId = "1";
        private bool _canViewPrice = true;

        public event EventHandler<ServiceMenuItem> ItemActivated;

        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal TreeView GroupTreeForTest { get { return _groupTree; } }
        internal DataGridView ProductGridForTest { get { return _productGrid; } }

        public ServiceMenuItem SelectedItem
        {
            get
            {
                if (_productGrid.CurrentRow == null || _productGrid.CurrentRow.Index < 0
                    || _productGrid.CurrentRow.Index >= _filtered.Count)
                    return null;
                return _filtered[_productGrid.CurrentRow.Index];
            }
        }

        public MenuPickerView()
        {
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            var bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            _searchPanel.Height = (int)(44 * s);
            _searchPanel.Padding = new Padding((int)(4 * s), (int)(6 * s), (int)(4 * s), (int)(4 * s));
            _lblSearch.Font = bodyFont;
            _lblSearch.Dock = DockStyle.Left;
            _lblSearch.Width = (int)(64 * s);

            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.Placeholder = "Nhập tên / mã món...";
            _txtSearch.Inner.Font = bodyFont;
            _txtSearch.TextChanged += delegate { ApplyFilter(); };
            _txtSearch.Inner.KeyDown += OnSearchKeyDown;

            _searchPanel.Controls.Add(_txtSearch);
            _searchPanel.Controls.Add(_lblSearch);

            _groupTree.Font = bodyFont;
            _groupTree.AfterSelect += delegate
            {
                _selectedGroupId = _groupTree.SelectedNode != null ? (_groupTree.SelectedNode.Tag as string ?? "") : "";
                ApplyFilter();
            };

            ConfigureGrid(bodyFont, s);
            _productGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < _filtered.Count && ItemActivated != null)
                    ItemActivated(this, _filtered[e.RowIndex]);
            };
            _productGrid.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && SelectedItem != null && ItemActivated != null)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ItemActivated(this, SelectedItem);
                }
            };

            _split.Panel1.Controls.Add(_groupTree);
            _split.Panel2.Controls.Add(_productGrid);
            _split.SizeChanged += delegate
            {
                if (_split.Height > 220 && _split.SplitterDistance < 90)
                    _split.SplitterDistance = (int)(145 * DeviceDpi / 96f);
            };

            Controls.Add(_split);
            Controls.Add(_searchPanel);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
        }

        private void ConfigureGrid(Font bodyFont, float s)
        {
            _productGrid.ReadOnly = true;
            _productGrid.AllowUserToAddRows = false;
            _productGrid.AllowUserToDeleteRows = false;
            _productGrid.AllowUserToResizeRows = false;
            _productGrid.MultiSelect = false;
            _productGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _productGrid.RowHeadersVisible = false;
            _productGrid.AutoGenerateColumns = false;
            _productGrid.Font = bodyFont;
            _productGrid.RowTemplate.Height = Math.Max((int)(34 * s), (int)(32 * s));
            _productGrid.ColumnHeadersHeight = (int)(32 * s);
            _productGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _productGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colProdName",
                HeaderText = "Tên hàng",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colPrice = new DataGridViewTextBoxColumn
            {
                Name = "colProdPrice",
                HeaderText = "Đơn giá",
                Width = (int)(86 * s),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colPrice.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            _productGrid.Columns.Add(colName);
            _productGrid.Columns.Add(colPrice);
        }

        public void SetData(List<ProductGroupRow> groups, List<ServiceMenuItem> items)
        {
            _groups.Clear();
            if (groups != null)
                _groups.AddRange(groups);
            _allItems.Clear();
            if (items != null)
                _allItems.AddRange(items);

            BuildGroupTree();
            ApplyFilter();
        }

        public void SetPriceLevel(string priceLevelId)
        {
            _priceLevelId = priceLevelId ?? "1";
            RenderGrid();
        }

        public void SetCanViewPrice(bool canView)
        {
            _canViewPrice = canView;
            DataGridViewColumn col = _productGrid.Columns["colProdPrice"];
            if (col != null)
                col.Visible = canView;
            RenderGrid();
        }

        public void FocusSearch()
        {
            _txtSearch.Inner.Focus();
            _txtSearch.Inner.SelectAll();
        }

        public void SelectProductById(string productId)
        {
            for (int i = 0; i < _filtered.Count; i++)
            {
                if (string.Equals(_filtered[i].Id, productId, StringComparison.Ordinal))
                {
                    _productGrid.ClearSelection();
                    _productGrid.Rows[i].Selected = true;
                    _productGrid.CurrentCell = _productGrid.Rows[i].Cells["colProdName"];
                    return;
                }
            }
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeTokens tokens = t ?? ThemeManager.Current;
            ThemeApplier.Apply(this, tokens);
            _split.BackColor = tokens.Border;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
                ApplyTheme(ThemeManager.Current);
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _txtSearch.Text = "";
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ServiceMenuItem item = SelectedItem ?? (_filtered.Count > 0 ? _filtered[0] : null);
                if (item != null && ItemActivated != null)
                    ItemActivated(this, item);
            }
        }

        private void BuildGroupTree()
        {
            _groupTree.BeginUpdate();
            try
            {
                _groupTree.Nodes.Clear();
                var root = new TreeNode("Tất cả") { Tag = "" };
                var byParent = new Dictionary<string, List<ProductGroupRow>>(StringComparer.Ordinal);
                foreach (ProductGroupRow g in _groups)
                {
                    string pid = g.ParentId ?? "";
                    List<ProductGroupRow> list;
                    if (!byParent.TryGetValue(pid, out list))
                    {
                        list = new List<ProductGroupRow>();
                        byParent[pid] = list;
                    }
                    list.Add(g);
                }
                AddGroupNodes(root.Nodes, "", byParent);
                _groupTree.Nodes.Add(root);
                root.Expand();
                _groupTree.SelectedNode = root;
                _selectedGroupId = "";
            }
            finally
            {
                _groupTree.EndUpdate();
            }
        }

        private static void AddGroupNodes(TreeNodeCollection target, string parentId, Dictionary<string, List<ProductGroupRow>> byParent)
        {
            List<ProductGroupRow> list;
            if (!byParent.TryGetValue(parentId, out list))
                return;
            foreach (ProductGroupRow g in list)
            {
                var node = new TreeNode(g.Name) { Tag = g.Id };
                AddGroupNodes(node.Nodes, g.Id, byParent);
                target.Add(node);
            }
        }

        private void ApplyFilter()
        {
            _filtered.Clear();
            string q = CatalogRepository.Fold(_txtSearch.Text ?? "").Trim();
            HashSet<string> allowedGroups = CollectDescendantGroups(_selectedGroupId);

            foreach (ServiceMenuItem item in _allItems)
            {
                if (allowedGroups != null && !allowedGroups.Contains(item.GroupId ?? ""))
                    continue;
                if (q.Length > 0)
                {
                    bool hit = CatalogRepository.Fold(item.Name ?? "").IndexOf(q, StringComparison.Ordinal) >= 0
                        || CatalogRepository.Fold(item.Code ?? "").IndexOf(q, StringComparison.Ordinal) >= 0;
                    if (!hit)
                        continue;
                }
                _filtered.Add(item);
            }
            RenderGrid();
        }

        private HashSet<string> CollectDescendantGroups(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;
            var set = new HashSet<string>(StringComparer.Ordinal) { groupId };
            bool added = true;
            while (added)
            {
                added = false;
                foreach (ProductGroupRow g in _groups)
                {
                    if (!string.IsNullOrEmpty(g.ParentId) && set.Contains(g.ParentId) && set.Add(g.Id))
                        added = true;
                }
            }
            return set;
        }

        private void RenderGrid()
        {
            _productGrid.Rows.Clear();
            foreach (ServiceMenuItem item in _filtered)
            {
                // Không đổ dữ liệu giá vào ô khi thiếu quyền XemDonGiaTrongHoaDonBanHang (kinh-nghiem.md).
                object priceCell = _canViewPrice
                    ? (object)NumberInput.FormatForDisplay(OrderCalculator.ResolveAreaPrice(item, _priceLevelId), 2, true)
                    : "";
                int idx = _productGrid.Rows.Add(item.Name, priceCell);
                _productGrid.Rows[idx].Tag = item;
            }
            if (_productGrid.Rows.Count > 0)
                _productGrid.CurrentCell = _productGrid.Rows[0].Cells["colProdName"];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
