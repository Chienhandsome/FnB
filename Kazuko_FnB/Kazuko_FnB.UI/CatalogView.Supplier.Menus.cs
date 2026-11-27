using System.Windows.Forms;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh chuột phải trên lưới nhà cung cấp và cây nhóm nhà cung cấp của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void BuildSupplierGridMenu()
        {
            _supplierGridMenu.AddItem("Thêm nhà cung cấp", Keys.Insert, delegate { AddSupplier(); });
            _supplierGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditSupplier(); });
            _supplierGridMenu.AddItem("Xem chi tiết", delegate { OpenSupplierFromGrid(); });
            _supplierGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteSupplier(); });
            _supplierGridMenu.AddSeparator();
            _supplierGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _supplierGridMenu.Font };
            AddSupplierSortItem(sort, "Tên nhà cung cấp (A → Z)", "s_name", true);
            AddSupplierSortItem(sort, "Tên nhà cung cấp (Z → A)", "s_name", false);
            AddSupplierSortItem(sort, "Mã nhà cung cấp (A → Z)", "s_code", true);
            AddSupplierSortItem(sort, "Mã nhà cung cấp (Z → A)", "s_code", false);
            _supplierGridMenu.Items.Add(sort);
            _supplierGridMenu.AddItem("Refresh", delegate { Reload(); });
            _supplierGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _supplierGridMenu.AddSeparator();
            _supplierGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _supplierGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _supplierGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _supplierColSubmenu = new ToolStripMenuItem("Cột hiển thị") { Font = _supplierGridMenu.Font };
            _supplierGridMenu.Items.Add(_supplierColSubmenu);
            _supplierGridMenu.Opening += delegate { UpdateSupplierGridMenuState(); };
        }

        private void AddSupplierSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _supplierGridMenu.Font };
            item.Click += delegate { SortSuppliers(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void SortSuppliers(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        private void UpdateSupplierGridMenuState()
        {
            bool hasRow = CurrentSupplierId() != null;
            foreach (ToolStripItem item in _supplierGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm nhà cung cấp":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewSupplier;
                        mi.Enabled = hasRow;
                        break;
                    case "Xóa":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && hasRow;
                        break;
                    case "Lọc theo giá trị":
                    case "Sao chép ô":
                    case "Sao chép vùng chọn":
                        mi.Enabled = hasRow;
                        break;
                }
            }

            if (_supplierColSubmenu != null)
            {
                _supplierColSubmenu.DropDownItems.Clear();
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    string key = col.Name;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = _supplierGridMenu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate
                    {
                        DataGridViewColumn c = _grid.Columns[key];
                        if (c != null)
                            c.Visible = !c.Visible;
                    };
                    _supplierColSubmenu.DropDownItems.Add(item);
                }
                _supplierColSubmenu.Enabled = _supplierColSubmenu.DropDownItems.Count > 0;
            }
        }

        private void BuildSupplierTreeMenu()
        {
            _supplierTreeMenu.AddItem("Thêm mới", delegate { AddSupplierGroup(null); });
            _supplierTreeMenu.AddItem("Thêm con", delegate { AddSupplierGroup(SelectedGroupId()); });
            _supplierTreeMenu.AddItem("Chỉnh sửa", delegate { EditSupplierGroup(); });
            _supplierTreeMenu.AddItem("Xem chi tiết", delegate { OpenSupplierGroupFromTree(); });
            _supplierTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedSupplierGroup(); });
            _supplierTreeMenu.AddItem("Xóa", delegate { DeleteSupplierGroup(); });
            _supplierTreeMenu.AddSeparator();
            _supplierTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _supplierTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _supplierTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _supplierTreeMenu.Opening += delegate { UpdateSupplierTreeMenuState(); };
        }

        private void UpdateSupplierTreeMenuState()
        {
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _supplierTreeMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Thêm con":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd && realGroup;
                        break;
                    case "Chỉnh sửa":
                    case "Đổi tên":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && realGroup;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewSupplier;
                        mi.Enabled = realGroup;
                        break;
                    case "Xóa":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && realGroup;
                        break;
                    case "Mở rộng":
                    case "Thu gọn":
                        mi.Enabled = hasKids;
                        break;
                }
            }
        }
    }
}
