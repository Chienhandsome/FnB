using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>Menu ngữ cảnh, bộ lọc, tô màu ô trạng thái và bảng màu cho chế độ Đợt khuyến mại của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void PopulatePromotionTypeFilter()
        {
            string prevKey = SelectedPromotionTypeFilterId();
            _updatingPromoFilters = true;
            try
            {
                _cboPromoTypeFilter.Items.Clear();
                _cboPromoTypeFilter.Items.Add(new Choice("", "Tất cả loại hình"));
                foreach (PromotionTypeItem t in _promotionTypes)
                    _cboPromoTypeFilter.Items.Add(new Choice(t.Id, t.Name));
                int sel = 0;
                if (!string.IsNullOrEmpty(prevKey))
                {
                    for (int i = 1; i < _cboPromoTypeFilter.Items.Count; i++)
                    {
                        var ch = _cboPromoTypeFilter.Items[i] as Choice;
                        if (ch != null && string.Equals(ch.Key, prevKey, StringComparison.Ordinal))
                        {
                            sel = i;
                            break;
                        }
                    }
                }
                _cboPromoTypeFilter.SelectedIndex = sel;
            }
            finally
            {
                _updatingPromoFilters = false;
            }
        }

        private string SelectedPromotionTypeFilterId()
        {
            var ch = _cboPromoTypeFilter.SelectedItem as Choice;
            return ch == null ? "" : (ch.Key ?? "");
        }

        private string SelectedPromotionStatusFilterCode()
        {
            var ch = _cboPromoStatusFilter.SelectedItem as Choice;
            return ch == null ? "" : (ch.Key ?? "");
        }

        private void AppendPromotionFilterParts(List<string> parts)
        {
            if (!_promotionMode)
                return;
            string typeId = SelectedPromotionTypeFilterId();
            if (typeId.Length > 0)
                parts.Add("[" + ColPromoTypeId + "] = '" + typeId.Replace("'", "''") + "'");
            string statusCode = SelectedPromotionStatusFilterCode();
            if (statusCode.Length > 0)
                parts.Add("[" + ColPromoStatus + "] = " + statusCode);
        }

        private void LayoutPromotionFilterBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _promoFilterBar.ClientSize.Height;
            int comboH = Math.Min((int)(28 * s), Math.Max((int)(22 * s), h - (int)(10 * s)));
            int yCbo = Math.Max(0, (h - comboH) / 2);

            int typeLblW = TextRenderer.MeasureText(_lblPromoTypeFilter.Text, _lblPromoTypeFilter.Font).Width + (int)(8 * s);
            int statusLblW = TextRenderer.MeasureText(_lblPromoStatusFilter.Text, _lblPromoStatusFilter.Font).Width + (int)(8 * s);
            int typeCboW = (int)(260 * s);
            int statusCboW = (int)(180 * s);

            int x = pad;
            _lblPromoTypeFilter.SetBounds(x, 0, typeLblW, h);
            x = _lblPromoTypeFilter.Right + (int)(4 * s);
            _cboPromoTypeFilter.SetBounds(x, yCbo, typeCboW, comboH);
            x = _cboPromoTypeFilter.Right + (int)(18 * s);
            _lblPromoStatusFilter.SetBounds(x, 0, statusLblW, h);
            x = _lblPromoStatusFilter.Right + (int)(4 * s);
            _cboPromoStatusFilter.SetBounds(x, yCbo, statusCboW, comboH);
        }

        private void ApplyPromotionRowColors()
        {
            if (!_promotionMode || _grid.Columns["pm_status"] == null)
                return;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var drv = r.DataBoundItem as DataRowView;
                if (drv == null)
                    continue;
                int code = Convert.ToInt32(drv[ColPromoStatus]);
                Color fore = PromotionStatusColor((PromotionStatus)code, _theme);
                DataGridViewCell cell = r.Cells["pm_status"];
                cell.Style.ForeColor = fore;
                cell.Style.SelectionForeColor = _theme.OnPrimary;
            }
        }

        internal static Color PromotionStatusColor(PromotionStatus status, ThemeTokens t)
        {
            switch (status)
            {
                case PromotionStatus.Active: return t.Success;
                case PromotionStatus.Upcoming: return t.Info;
                case PromotionStatus.Expired: return t.TextMuted;
                case PromotionStatus.Stopped: return t.Danger;
                default: return t.Text;
            }
        }

        private void BuildPromotionGridMenu()
        {
            _promotionGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddPromotion(); });
            _promotionGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditPromotion(); });
            _promotionGridMenu.AddItem("Xem chi tiết", delegate { OpenPromotionFromGrid(); });
            _promotionGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeletePromotion(); });
            _promotionGridMenu.AddSeparator();
            _promotionGridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _promotionGridMenu.Font };
            AddPromotionSortItem(sort, "Tên đợt (A → Z)", "pm_name", true);
            AddPromotionSortItem(sort, "Tên đợt (Z → A)", "pm_name", false);
            AddPromotionSortItem(sort, "Từ ngày (mới nhất)", "pm_from", false);
            AddPromotionSortItem(sort, "Từ ngày (cũ nhất)", "pm_from", true);
            _promotionGridMenu.Items.Add(sort);
            _promotionGridMenu.AddItem("Refresh", delegate { Reload(); });
            _promotionGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _promotionGridMenu.AddSeparator();
            _promotionGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _promotionGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _promotionGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _promotionGridMenu.Opening += delegate { UpdatePromotionGridMenuState(); };
        }

        private void AddPromotionSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _promotionGridMenu.Font };
            item.Click += delegate
            {
                if (_view == null)
                    return;
                _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
                _grid.ClearSelection();
                ApplyPromotionRowColors();
            };
            parent.DropDownItems.Add(item);
        }

        private void UpdatePromotionGridMenuState()
        {
            bool hasRow = CurrentPromotionId() != null;
            foreach (ToolStripItem item in _promotionGridMenu.Items)
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
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewPromotion;
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

        private void ApplyPromotionTheme(ThemeTokens t, Color page)
        {
            _promoFilterBar.BackColor = page;
            _lblPromoTypeFilter.BackColor = Color.Transparent;
            _lblPromoTypeFilter.ForeColor = t.Text;
            _lblPromoStatusFilter.BackColor = Color.Transparent;
            _lblPromoStatusFilter.ForeColor = t.Text;
            _cboPromoTypeFilter.BackColor = t.Surface;
            _cboPromoTypeFilter.ForeColor = t.Text;
            _cboPromoStatusFilter.BackColor = t.Surface;
            _cboPromoStatusFilter.ForeColor = t.Text;
            ApplyPromotionRowColors();
            _promoFilterBar.Invalidate(true);
        }

        private void ShowPromotionWarning(string message)
        {
            LastPromotionMessageForTest = message;
            if (!SuppressPromotionPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowPromotionError(string message)
        {
            LastPromotionMessageForTest = message;
            if (!SuppressPromotionPopupForTest)
                Msg.Error(this, message);
        }

        private void DisposePromotionUi()
        {
            _promotionGridMenu.Dispose();
        }
    }
}
