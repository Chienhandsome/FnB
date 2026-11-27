using System.Windows.Forms;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh chuột phải trên lưới mật khẩu wifi và cây nhóm wifi của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void BuildWifiGridMenu()
        {
            _wifiGridMenu.AddItem("Thêm mật khẩu", Keys.Insert, delegate { AddWifiPassword(); });
            _wifiGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditWifiPassword(); });
            _wifiGridMenu.AddItem("Xem chi tiết", delegate { OpenWifiPasswordFromGrid(); });
            _wifiGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteWifiPassword(); });
            _wifiGridMenu.AddSeparator();
            _wifiGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _wifiGridMenu.Font };
            AddWifiSortItem(sort, "Mật khẩu wifi (A → Z)", "wp_name", true);
            AddWifiSortItem(sort, "Mật khẩu wifi (Z → A)", "wp_name", false);
            AddWifiSortItem(sort, "Nhóm wifi (A → Z)", "wp_group", true);
            AddWifiSortItem(sort, "Nhóm wifi (Z → A)", "wp_group", false);
            _wifiGridMenu.Items.Add(sort);
            _wifiGridMenu.AddItem("Refresh", delegate { Reload(); });
            _wifiGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _wifiGridMenu.AddSeparator();
            _wifiGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _wifiGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _wifiGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _wifiGridMenu.Opening += delegate { UpdateWifiGridMenuState(); };
        }

        private void AddWifiSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _wifiGridMenu.Font };
            item.Click += delegate { SortWifiPasswords(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void SortWifiPasswords(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        private void UpdateWifiGridMenuState()
        {
            bool hasRow = CurrentWifiPasswordId() != null;
            foreach (ToolStripItem item in _wifiGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mật khẩu":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewWifiPassword;
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
        }

        private void BuildWifiTreeMenu()
        {
            _wifiTreeMenu.AddItem("Thêm mới", delegate { AddWifiGroup(null); });
            _wifiTreeMenu.AddItem("Thêm con", delegate { AddWifiGroup(SelectedGroupId()); });
            _wifiTreeMenu.AddItem("Chỉnh sửa", delegate { EditWifiGroup(); });
            _wifiTreeMenu.AddItem("Xem chi tiết", delegate { OpenWifiGroupFromTree(); });
            _wifiTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedWifiGroup(); });
            _wifiTreeMenu.AddItem("Xóa", delegate { DeleteWifiGroup(); });
            _wifiTreeMenu.AddSeparator();
            _wifiTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _wifiTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _wifiTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _wifiTreeMenu.Opening += delegate { UpdateWifiTreeMenuState(); };
        }

        private void UpdateWifiTreeMenuState()
        {
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _wifiTreeMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewWifiPassword;
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

        private void DisposeWifiPasswordUi()
        {
            _wifiGridMenu.Dispose();
            _wifiTreeMenu.Dispose();
        }
    }
}
