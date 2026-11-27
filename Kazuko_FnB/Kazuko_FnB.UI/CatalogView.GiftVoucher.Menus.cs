using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh chuột phải trên lưới voucher và cây nhóm voucher của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void BuildGiftVoucherGridMenu()
        {
            _giftVoucherGridMenu.AddItem("Thêm voucher", Keys.Insert, delegate { AddGiftVoucher(); });
            _giftVoucherGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditGiftVoucher(); });
            _giftVoucherGridMenu.AddItem("Xem chi tiết", delegate { OpenGiftVoucherFromGrid(); });
            _giftVoucherGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteGiftVoucher(); });
            _giftVoucherGridMenu.AddSeparator();
            _giftVoucherGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _giftVoucherGridMenu.Font };
            AddGiftVoucherSortItem(sort, "Mã voucher (A → Z)", "gv_code", true);
            AddGiftVoucherSortItem(sort, "Mã voucher (Z → A)", "gv_code", false);
            AddGiftVoucherSortItem(sort, "Giá trị (tăng dần)", "gv_value", true);
            AddGiftVoucherSortItem(sort, "Giá trị (giảm dần)", "gv_value", false);
            AddGiftVoucherSortItem(sort, "Hạn sử dụng (tăng dần)", "gv_expiry", true);
            AddGiftVoucherSortItem(sort, "Hạn sử dụng (giảm dần)", "gv_expiry", false);
            _giftVoucherGridMenu.Items.Add(sort);
            _giftVoucherGridMenu.AddItem("Refresh", delegate { Reload(); });
            _giftVoucherGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _giftVoucherGridMenu.AddSeparator();
            _giftVoucherGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _giftVoucherGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _giftVoucherGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _giftVoucherColSubmenu = new ToolStripMenuItem("Cột hiển thị") { Font = _giftVoucherGridMenu.Font };
            _giftVoucherGridMenu.Items.Add(_giftVoucherColSubmenu);
            _giftVoucherGridMenu.Opening += delegate { UpdateGiftVoucherGridMenuState(); };
        }

        private void AddGiftVoucherSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _giftVoucherGridMenu.Font };
            item.Click += delegate { SortGiftVouchers(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void SortGiftVouchers(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        private void UpdateGiftVoucherGridMenuState()
        {
            bool hasRow = CurrentGiftVoucherId() != null;
            foreach (ToolStripItem item in _giftVoucherGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm voucher":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewGiftVoucher;
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

            if (_giftVoucherColSubmenu != null)
            {
                _giftVoucherColSubmenu.DropDownItems.Clear();
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    string key = col.Name;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = _giftVoucherGridMenu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate
                    {
                        DataGridViewColumn c = _grid.Columns[key];
                        if (c != null)
                            c.Visible = !c.Visible;
                    };
                    _giftVoucherColSubmenu.DropDownItems.Add(item);
                }
                _giftVoucherColSubmenu.Enabled = _giftVoucherColSubmenu.DropDownItems.Count > 0;
            }
        }

        private void BuildGiftVoucherTreeMenu()
        {
            _giftVoucherTreeMenu.AddItem("Thêm mới", delegate { AddGiftVoucherGroup(null); });
            _giftVoucherTreeMenu.AddItem("Thêm con", delegate { AddGiftVoucherGroup(SelectedGroupId()); });
            _giftVoucherTreeMenu.AddItem("Chỉnh sửa", delegate { EditGiftVoucherGroup(); });
            _giftVoucherTreeMenu.AddItem("Xem chi tiết", delegate { OpenGiftVoucherGroupFromTree(); });
            _giftVoucherTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedGiftVoucherGroup(); });
            _giftVoucherTreeMenu.AddItem("Xóa", delegate { DeleteGiftVoucherGroup(); });
            _giftVoucherTreeMenu.AddSeparator();
            _giftVoucherTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _giftVoucherTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _giftVoucherTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _giftVoucherTreeMenu.Opening += delegate { UpdateGiftVoucherTreeMenuState(); };
        }

        private void UpdateGiftVoucherTreeMenuState()
        {
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _giftVoucherTreeMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewGiftVoucher;
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

        private void LayoutVoucherTotalBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(12 * s);
            int w = _voucherTotalBar.ClientSize.Width;
            int h = _voucherTotalBar.ClientSize.Height;
            int valW = Math.Min((int)(260 * s), Math.Max((int)(120 * s), w / 3));
            _lblVoucherTotalValue.SetBounds(Math.Max(pad, w - pad - valW), 0, valW, h);
            _lblVoucherTotalCaption.SetBounds(pad, 0, Math.Max(0, _lblVoucherTotalValue.Left - pad - (int)(8 * s)), h);
        }

        private void UpdateGiftVoucherTotalBar()
        {
            if (!_giftVoucherMode)
            {
                _voucherTotalBar.Visible = false;
                return;
            }
            decimal total = 0m;
            int count = 0;
            if (_view != null)
            {
                count = _view.Count;
                for (int i = 0; i < _view.Count; i++)
                {
                    object v = _view[i]["gv_value"];
                    if (v != null && !(v is DBNull))
                        total += Convert.ToDecimal(v);
                }
            }
            _voucherTotalValue = total;
            _lblVoucherTotalCaption.Text = "Tổng cộng (" + count + " voucher):";
            _lblVoucherTotalValue.Text = "Tổng giá trị: " + total.ToString("#,##0.##", new CultureInfo("vi-VN"));
            LayoutVoucherTotalBar();
        }

        private void ApplyGiftVoucherTheme(ThemeTokens t, Color page)
        {
            _chkVoucherUsableOnly.BackColor = Color.Transparent;
            _chkVoucherUsableOnly.ForeColor = t.Text;
            _voucherTotalBar.BackColor = Draw.Mix(t.Surface, t.Primary, 0.08f);
            _lblVoucherTotalCaption.BackColor = Color.Transparent;
            _lblVoucherTotalCaption.ForeColor = t.Text;
            _lblVoucherTotalValue.BackColor = Color.Transparent;
            _lblVoucherTotalValue.ForeColor = t.Primary;
            _voucherTotalBar.Invalidate(true);
        }

        private void DisposeGiftVoucherUi()
        {
            if (_voucherTotalFont != null)
                _voucherTotalFont.Dispose();
            _giftVoucherGridMenu.Dispose();
            _giftVoucherTreeMenu.Dispose();
        }
    }
}
