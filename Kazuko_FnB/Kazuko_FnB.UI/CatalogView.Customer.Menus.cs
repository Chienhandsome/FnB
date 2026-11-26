using System.Windows.Forms;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh chuột phải trên lưới khách hàng và cây nhóm khách hàng của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void BuildCustomerGridMenu()
        {
            _customerGridMenu.AddItem("Thêm khách hàng", Keys.Insert, delegate { AddCustomer(); });
            _customerGridMenu.AddItem("Thêm nhanh (excel)", delegate { Msg.Info(this, UnsupportedText); });
            _customerGridMenu.AddItem("Cập nhật nhanh (excel)", delegate { Msg.Info(this, UnsupportedText); });
            _customerGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditCustomer(); });
            _customerGridMenu.AddItem("Xem chi tiết", delegate { OpenCustomerFromGrid(); });
            _customerGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteCustomer(); });
            _customerGridMenu.AddSeparator();
            _customerGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _customerGridMenu.Font };
            AddCustomerSortItem(sort, "Tên khách hàng (A → Z)", "c_name", true);
            AddCustomerSortItem(sort, "Tên khách hàng (Z → A)", "c_name", false);
            AddCustomerSortItem(sort, "Mã khách (A → Z)", "c_code", true);
            AddCustomerSortItem(sort, "Mã khách (Z → A)", "c_code", false);
            _customerGridMenu.Items.Add(sort);
            _customerGridMenu.AddItem("Refresh", delegate { Reload(); });
            _customerGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _customerGridMenu.AddItem("In danh sách", delegate { Msg.Info(this, UnsupportedText); });
            _customerGridMenu.AddSeparator();
            _customerGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _customerGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _customerGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _customerColSubmenu = new ToolStripMenuItem("Cột hiển thị") { Font = _customerGridMenu.Font };
            _customerGridMenu.Items.Add(_customerColSubmenu);
            _customerGridMenu.Opening += delegate { UpdateCustomerGridMenuState(); };
        }

        private void AddCustomerSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _customerGridMenu.Font };
            item.Click += delegate { SortCustomers(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void SortCustomers(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        private void UpdateCustomerGridMenuState()
        {
            bool hasRow = CurrentCustomerId() != null;
            foreach (ToolStripItem item in _customerGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm khách hàng":
                    case "Thêm nhanh (excel)":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Cập nhật nhanh (excel)":
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewCustomer;
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

            if (_customerColSubmenu != null)
            {
                _customerColSubmenu.DropDownItems.Clear();
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    string key = col.Name;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = _customerGridMenu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate
                    {
                        DataGridViewColumn c = _grid.Columns[key];
                        if (c != null)
                        {
                            c.Visible = !c.Visible;
                            ApplyFilter();
                        }
                    };
                    _customerColSubmenu.DropDownItems.Add(item);
                }
                _customerColSubmenu.Enabled = _customerColSubmenu.DropDownItems.Count > 0;
            }
        }

        private void BuildCustomerTreeMenu()
        {
            _customerTreeMenu.AddItem("Thêm mới", delegate { AddCustomerGroup(null); });
            _customerTreeMenu.AddItem("Thêm con", delegate { AddCustomerGroup(SelectedGroupId()); });
            _customerTreeMenu.AddItem("Chỉnh sửa", delegate { EditCustomerGroup(); });
            _customerTreeMenu.AddItem("Xem chi tiết", delegate { OpenCustomerGroupFromTree(); });
            _customerTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedCustomerGroup(); });
            _customerTreeMenu.AddItem("Xóa", delegate { DeleteCustomerGroup(); });
            _customerTreeMenu.AddSeparator();
            _customerTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _customerTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _customerTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _customerTreeMenu.Opening += delegate { UpdateCustomerTreeMenuState(); };
        }

        private void UpdateCustomerTreeMenuState()
        {
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _customerTreeMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewCustomer;
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
