using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh, thanh lọc nhanh, hàng tổng cộng 3 cột tiền và bảng màu cho Danh mục thẻ trả trước của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void BuildPrepaidCardGridMenu()
        {
            _prepaidGridMenu.AddItem("Thêm thẻ trả trước", Keys.Insert, delegate { AddPrepaidCard(); });
            _prepaidGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditPrepaidCard(); });
            _prepaidGridMenu.AddItem("Xem chi tiết", delegate { OpenPrepaidCardFromGrid(); });
            _prepaidGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeletePrepaidCard(); });
            _prepaidGridMenu.AddSeparator();
            _prepaidGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _prepaidGridMenu.Font };
            AddPrepaidCardSortItem(sort, "Mã thẻ (A → Z)", "pc_code", true);
            AddPrepaidCardSortItem(sort, "Mã thẻ (Z → A)", "pc_code", false);
            AddPrepaidCardSortItem(sort, "Còn lại (giảm dần)", "pc_remaining", false);
            AddPrepaidCardSortItem(sort, "Còn lại (tăng dần)", "pc_remaining", true);
            AddPrepaidCardSortItem(sort, "Ngày hết hạn (tăng dần)", "pc_expiry", true);
            _prepaidGridMenu.Items.Add(sort);
            _prepaidGridMenu.AddItem("Refresh", delegate { Reload(); });
            _prepaidGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _prepaidGridMenu.AddSeparator();
            _prepaidGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _prepaidGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _prepaidGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _prepaidColSubmenu = new ToolStripMenuItem("Cột hiển thị") { Font = _prepaidGridMenu.Font };
            _prepaidGridMenu.Items.Add(_prepaidColSubmenu);
            _prepaidGridMenu.Opening += delegate { UpdatePrepaidCardGridMenuState(); };
        }

        private void AddPrepaidCardSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _prepaidGridMenu.Font };
            item.Click += delegate { SortPrepaidCards(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void SortPrepaidCards(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        private void UpdatePrepaidCardGridMenuState()
        {
            bool hasRow = CurrentPrepaidCardId() != null;
            foreach (ToolStripItem item in _prepaidGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm thẻ trả trước":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewPrepaidCard;
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

            if (_prepaidColSubmenu != null)
            {
                _prepaidColSubmenu.DropDownItems.Clear();
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    string key = col.Name;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = _prepaidGridMenu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate
                    {
                        DataGridViewColumn c = _grid.Columns[key];
                        if (c != null)
                            c.Visible = !c.Visible;
                    };
                    _prepaidColSubmenu.DropDownItems.Add(item);
                }
                _prepaidColSubmenu.Enabled = _prepaidColSubmenu.DropDownItems.Count > 0;
            }
        }

        private void BuildPrepaidCardTreeMenu()
        {
            _prepaidTreeMenu.AddItem("Thêm mới", delegate { AddPrepaidCardGroup(null); });
            _prepaidTreeMenu.AddItem("Thêm con", delegate { AddPrepaidCardGroup(SelectedGroupId()); });
            _prepaidTreeMenu.AddItem("Chỉnh sửa", delegate { EditPrepaidCardGroup(); });
            _prepaidTreeMenu.AddItem("Xem chi tiết", delegate { OpenPrepaidCardGroupFromTree(); });
            _prepaidTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedPrepaidCardGroup(); });
            _prepaidTreeMenu.AddItem("Xóa", delegate { DeletePrepaidCardGroup(); });
            _prepaidTreeMenu.AddSeparator();
            _prepaidTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _prepaidTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _prepaidTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _prepaidTreeMenu.Opening += delegate { UpdatePrepaidCardTreeMenuState(); };
        }

        private void UpdatePrepaidCardTreeMenuState()
        {
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _prepaidTreeMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewPrepaidCard;
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

        private void LayoutPrepaidFilterBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _prepaidFilterBar.ClientSize.Height;
            Size sz1 = _chkPrepaidHasBalance.GetPreferredSize(Size.Empty);
            Size sz2 = _chkPrepaidUnlockedOnly.GetPreferredSize(Size.Empty);
            int y1 = Math.Max(0, (h - sz1.Height) / 2);
            int y2 = Math.Max(0, (h - sz2.Height) / 2);
            _chkPrepaidHasBalance.SetBounds(pad, y1, sz1.Width, sz1.Height);
            _chkPrepaidUnlockedOnly.SetBounds(_chkPrepaidHasBalance.Right + (int)(18 * s), y2, sz2.Width, sz2.Height);
        }

        private void LayoutPrepaidTotalBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(12 * s);
            int w = _prepaidTotalBar.ClientSize.Width;
            int h = _prepaidTotalBar.ClientSize.Height;
            int valW = Math.Min((int)(620 * s), Math.Max((int)(240 * s), w * 3 / 4));
            _lblPrepaidTotalValues.SetBounds(Math.Max(pad, w - pad - valW), 0, valW, h);
            _lblPrepaidTotalCaption.SetBounds(pad, 0, Math.Max(0, _lblPrepaidTotalValues.Left - pad - (int)(8 * s)), h);
        }

        private void UpdatePrepaidCardTotalBar()
        {
            if (!_prepaidCardMode)
            {
                _prepaidTotalBar.Visible = false;
                return;
            }
            decimal totalLoaded = 0m;
            decimal totalUsed = 0m;
            decimal totalRemaining = 0m;
            int count = 0;
            if (_view != null)
            {
                count = _view.Count;
                for (int i = 0; i < _view.Count; i++)
                {
                    object ld = _view[i]["pc_loaded"];
                    object us = _view[i]["pc_used"];
                    object rm = _view[i]["pc_remaining"];
                    if (ld != null && !(ld is DBNull))
                        totalLoaded += Convert.ToDecimal(ld);
                    if (us != null && !(us is DBNull))
                        totalUsed += Convert.ToDecimal(us);
                    if (rm != null && !(rm is DBNull))
                        totalRemaining += Convert.ToDecimal(rm);
                }
            }
            _prepaidTotalLoaded = totalLoaded;
            _prepaidTotalUsed = totalUsed;
            _prepaidTotalRemaining = totalRemaining;
            _lblPrepaidTotalCaption.Text = "Tổng cộng (" + count + " thẻ):";
            _lblPrepaidTotalValues.Text = "Tổng nạp: " + PrepaidCardRules.FormatMoney(totalLoaded)
                + "   |   Tổng sử dụng: " + PrepaidCardRules.FormatMoney(totalUsed)
                + "   |   Còn lại: " + PrepaidCardRules.FormatMoney(totalRemaining);
            LayoutPrepaidTotalBar();
        }

        private void ApplyPrepaidCardTheme(ThemeTokens t, Color page)
        {
            _prepaidFilterBar.BackColor = page;
            _chkPrepaidHasBalance.BackColor = Color.Transparent;
            _chkPrepaidHasBalance.ForeColor = t.Text;
            _chkPrepaidUnlockedOnly.BackColor = Color.Transparent;
            _chkPrepaidUnlockedOnly.ForeColor = t.Text;

            _prepaidTotalBar.BackColor = Draw.Mix(t.Surface, t.Primary, 0.08f);
            _lblPrepaidTotalCaption.BackColor = Color.Transparent;
            _lblPrepaidTotalCaption.ForeColor = t.Text;
            _lblPrepaidTotalValues.BackColor = Color.Transparent;
            _lblPrepaidTotalValues.ForeColor = t.Primary;
            _prepaidFilterBar.Invalidate(true);
            _prepaidTotalBar.Invalidate(true);
        }

        private void DisposePrepaidCardUi()
        {
            if (_prepaidTotalFont != null)
                _prepaidTotalFont.Dispose();
            _prepaidGridMenu.Dispose();
            _prepaidTreeMenu.Dispose();
        }
    }
}
