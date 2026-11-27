using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần hiển thị lưới, tô màu dòng từ ThemeTokens (không hard-code màu), hàng tổng cộng giá trị,
    /// bảo vệ cột giá nhập, menu ngữ cảnh, vẽ cây nhóm, đổi theme và giải phóng tài nguyên cho <see cref="InventoryView"/>.
    /// </summary>
    internal sealed partial class InventoryView
    {
        // ------------------------------------------------------------------ Màu dòng suy từ ThemeTokens (không hard-code màu)

        internal static Color NegativeRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Danger, 0.18f);
        }

        internal static Color NegativeRowForeColor(ThemeTokens t)
        {
            return t.Danger;
        }

        internal static Color LowStockRowBackColor(ThemeTokens t)
        {
            Color warnBase = Draw.ShiftHsl(t.Danger, 44f, 0.16f);
            return Draw.Mix(t.Surface, warnBase, 0.28f);
        }

        internal static Color LowStockRowForeColor(ThemeTokens t)
        {
            return t.Text;
        }

        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.10f);
        }

        // ------------------------------------------------------------------ Quyền xem giá nhập & menu lưới

        private void ApplyPricePermissionVisibility()
        {
            foreach (string colName in PriceProtectedColumns)
            {
                DataGridViewColumn col = _grid.Columns[colName];
                if (col != null)
                    col.Visible = _canViewBuyPrice;
            }
            // Spec §1: không có quyền xem giá nhập -> ẩn 4 cột giá và tắt xuất Excel.
            _btnExcel.Visible = _canViewBuyPrice;
            _btnExcel.Enabled = _canViewBuyPrice;
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            _gridMenu.AddItem("Làm mới", Keys.F5, delegate { ReloadAll(); });
            ToolStripMenuItem exportItem = _gridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem copy = _gridMenu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = _gridMenu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(_grid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = _gridMenu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(_grid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            ToolStripMenuItem columns = _gridMenu.AddItem("Cột hiển thị", null);
            _gridMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_grid); });

            _gridMenu.Opening += delegate
            {
                exportItem.Visible = _canViewBuyPrice;
                exportItem.Enabled = _canViewBuyPrice;
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
                if (!_canViewBuyPrice)
                    StripProtectedColumnsFromMenu(columns);
            };
        }

        private void StripProtectedColumnsFromMenu(ToolStripMenuItem columnsMenu)
        {
            for (int i = columnsMenu.DropDownItems.Count - 1; i >= 0; i--)
            {
                string caption = columnsMenu.DropDownItems[i].Text;
                foreach (string name in PriceProtectedColumns)
                {
                    DataGridViewColumn col = _grid.Columns[name];
                    if (col != null && string.Equals(col.HeaderText, caption, StringComparison.Ordinal))
                    {
                        columnsMenu.DropDownItems.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : FirstVisibleColumnIndex(_grid);
                if (col >= 0)
                    _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[col];
                _grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _grid.ClearSelection();
            }
            _gridMenu.Show(_grid, e.Location);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            bool isSummary = ReferenceEquals(_grid.Rows[e.RowIndex].Tag, SummaryRowTag);
            string text = isSummary ? "Σ" : (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, _theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ------------------------------------------------------------------ Đổ lưới & tô màu dòng

        private void FillGrid()
        {
            _grid.Rows.Clear();
            ThemeTokens t = ThemeManager.Current;

            foreach (InventoryItemRow r in _visibleItems)
            {
                // Khi thiếu quyền Xem giá nhập, không đưa dữ liệu giá vào ô (kinh-nghiem.md: chống lộ qua sao chép).
                int idx = _grid.Rows.Add(
                    r.Code ?? "",
                    r.Name ?? "",
                    r.UnitName ?? "",
                    r.StockQuantity,
                    r.TwoUnitDisplay ?? "",
                    r.MinStock,
                    _canViewBuyPrice ? (object)r.BuyPrice : null,
                    _canViewBuyPrice ? (object)r.CostPrice : null,
                    _canViewBuyPrice ? (object)r.CostValue : null,
                    _canViewBuyPrice ? (object)r.SellValue : null);
                _grid.Rows[idx].Tag = r;
                StyleDataRow(_grid.Rows[idx], r, t);
            }

            InventoryTotals totals = InventoryRules.ComputeTotals(_visibleItems);
            if (_visibleItems.Count > 0 && _canViewBuyPrice)
            {
                int sumIdx = _grid.Rows.Add(
                    "", "Tổng cộng", "", null, "", null, null, null,
                    totals.TotalCostValue,
                    totals.TotalSellValue);
                _grid.Rows[sumIdx].Tag = SummaryRowTag;
                StyleSummaryRow(_grid.Rows[sumIdx], t);
            }

            if (_grid.Rows.Count > 0)
            {
                int firstCol = FirstVisibleColumnIndex(_grid);
                _grid.Rows[0].Selected = true;
                if (firstCol >= 0)
                    _grid.CurrentCell = _grid.Rows[0].Cells[firstCol];
            }

            _empty.Visible = _visibleItems.Count == 0;
            _empty.Text = !_service.CanView
                ? InventoryConstants.NoPermission
                : string.IsNullOrEmpty(SelectedWarehouseId())
                    ? InventoryConstants.EmptyNoWarehouse
                    : InventoryConstants.EmptyNoItems;

            _summary.Text = _canViewBuyPrice
                ? totals.ItemCount + " mặt hàng — Giá trị vốn: " + PurchaseConstants.FormatMoneyVi(totals.TotalCostValue)
                : totals.ItemCount + " mặt hàng";
        }

        private void StyleDataRow(DataGridViewRow row, InventoryItemRow item, ThemeTokens t)
        {
            if (row == null || item == null)
                return;
            if (item.Tone == InventoryRowTone.NegativeStock)
            {
                row.DefaultCellStyle.BackColor = NegativeRowBackColor(t);
                row.DefaultCellStyle.ForeColor = NegativeRowForeColor(t);
                row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Primary, t.Danger, 0.50f);
                row.DefaultCellStyle.SelectionForeColor = t.OnPrimary;
            }
            else if (item.Tone == InventoryRowTone.LowStock)
            {
                row.DefaultCellStyle.BackColor = LowStockRowBackColor(t);
                row.DefaultCellStyle.ForeColor = LowStockRowForeColor(t);
                Color warnSel = Draw.ShiftHsl(t.Danger, 44f, 0.10f);
                row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, warnSel, 0.48f);
                row.DefaultCellStyle.SelectionForeColor = t.Text;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.Empty;
                row.DefaultCellStyle.ForeColor = Color.Empty;
                row.DefaultCellStyle.SelectionBackColor = Color.Empty;
                row.DefaultCellStyle.SelectionForeColor = Color.Empty;
            }
        }

        private void StyleSummaryRow(DataGridViewRow row, ThemeTokens t)
        {
            if (row == null)
                return;
            row.DefaultCellStyle.Font = _summaryFont;
            row.DefaultCellStyle.BackColor = SummaryRowBackColor(t);
            row.DefaultCellStyle.ForeColor = t.Text;
            row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, t.Primary, 0.22f);
            row.DefaultCellStyle.SelectionForeColor = t.Text;
        }

        private static int FirstVisibleColumnIndex(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
                if (c.Visible)
                    return c.Index;
            return -1;
        }

        private void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
        {
            bool selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
            Color bg = selected ? _theme.Primary : _theme.Surface;
            Color fg = selected ? _theme.OnPrimary : _theme.Text;
            Rectangle fill = new Rectangle(e.Bounds.X, e.Bounds.Y, Math.Max(e.Bounds.Width, _tree.ClientSize.Width - e.Bounds.X), e.Bounds.Height);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, fill);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, _bodyFont, e.Bounds, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = _split.Panel2.BackColor = t.Background;
            ThemeApplier.ApplyInput(_txtSearch, t, t.Background, Draw.StrongBorder(t));
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyGrid(_grid, t);

            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (ReferenceEquals(r.Tag, SummaryRowTag))
                {
                    StyleSummaryRow(r, t);
                    continue;
                }
                var item = r.Tag as InventoryItemRow;
                if (item != null)
                    StyleDataRow(r, item, t);
            }
            _tree.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null) _gridMenu.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
                if (_summaryFont != null) _summaryFont.Dispose();
                if (_smallFont != null) _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
