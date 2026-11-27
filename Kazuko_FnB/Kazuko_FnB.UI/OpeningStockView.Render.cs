using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần đổ lưới, sửa ô số trên lưới (Enter chuyển ô, 0 DataError), hàng tổng cộng đậm từ ThemeTokens,
    /// menu ngữ cảnh, đổi theme sống và giải phóng tài nguyên cho <see cref="OpeningStockView"/>.
    /// </summary>
    internal sealed partial class OpeningStockView
    {
        // ------------------------------------------------------------------ Sửa ô số trên lưới

        private void GridCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_readOnly || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.ColumnIndex < 0)
            {
                e.Cancel = true;
                return;
            }
            if (ReferenceEquals(_grid.Rows[e.RowIndex].Tag, SummaryRowTag))
            {
                e.Cancel = true;
                return;
            }
            string colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName != "colQty" && colName != "colCost")
                e.Cancel = true;
        }

        private void GridCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.ColumnIndex < 0)
                return;
            if (!NumericEditGridCell.IsNumericCell(_grid, e.RowIndex, e.ColumnIndex))
                return;
            _cellParseFailed = !NumericEditGridCell.ParseCell(_grid, e, 0m);
        }

        private void GridCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            bool parseFailed = _cellParseFailed;
            _cellParseFailed = false;
            _lastEditRejected = false;
            if (_updatingCell || _loading || _readOnly)
                return;
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.ColumnIndex < 0)
                return;

            var item = _grid.Rows[e.RowIndex].Tag as OpeningStockItemRow;
            if (item == null)
                return;

            string colName = _grid.Columns[e.ColumnIndex].Name;
            DataGridViewRow r = _grid.Rows[e.RowIndex];

            if (colName == "colQty")
            {
                decimal v;
                if (parseFailed || !NumberInput.TryParse(r.Cells["colQty"].Value, out v))
                {
                    _lastEditRejected = true;
                    SyncRowCells(e.RowIndex, item);
                    UpdateDirtyState();
                    return;
                }
                if (v < 0m)
                {
                    _lastEditRejected = true;
                    SyncRowCells(e.RowIndex, item);
                    ShowWarning(OpeningStockConstants.NegativeQtyBlocked(item.Name));
                    UpdateDirtyState();
                    return;
                }
                item.Quantity = v;
                OpeningStockRules.RecalcItem(item);
                SyncRowCells(e.RowIndex, item);
                UpdateSummaryRowAndBar();
                UpdateDirtyState();
            }
            else if (colName == "colCost")
            {
                decimal v;
                if (parseFailed || !NumberInput.TryParse(r.Cells["colCost"].Value, out v))
                {
                    _lastEditRejected = true;
                    SyncRowCells(e.RowIndex, item);
                    UpdateDirtyState();
                    return;
                }
                if (v < 0m)
                {
                    _lastEditRejected = true;
                    SyncRowCells(e.RowIndex, item);
                    ShowWarning(OpeningStockConstants.NegativeCostBlocked(item.Name));
                    UpdateDirtyState();
                    return;
                }
                item.CostPrice = v;
                OpeningStockRules.RecalcItem(item);
                SyncRowCells(e.RowIndex, item);
                UpdateSummaryRowAndBar();
                UpdateDirtyState();
            }
        }

        private void GridDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            DataErrorCountForTest++;
            DataErrorDetailForTest = NumericEditGridCell.AppendDataErrorDetail(DataErrorDetailForTest, _grid, e);
            e.ThrowException = false;
            if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count)
            {
                var item = _grid.Rows[e.RowIndex].Tag as OpeningStockItemRow;
                if (item != null)
                    SyncRowCells(e.RowIndex, item);
            }
        }

        /// <summary>Enter trên ô Tồn đầu chuyển sang Giá vốn cùng dòng; trên ô Giá vốn chuyển sang Tồn đầu dòng kế tiếp.</summary>
        private bool AdvanceCellOnEnter()
        {
            if (_readOnly || _grid.CurrentCell == null)
                return false;

            int rowIdx = _grid.CurrentCell.RowIndex;
            int colIdx = _grid.CurrentCell.ColumnIndex;
            if (rowIdx < 0 || rowIdx >= _grid.Rows.Count || ReferenceEquals(_grid.Rows[rowIdx].Tag, SummaryRowTag))
                return false;

            if (_grid.IsCurrentCellInEditMode)
            {
                _lastEditRejected = false;
                if (!_grid.EndEdit() || _lastEditRejected)
                    return true;
            }

            DataGridViewColumn qtyCol = _grid.Columns["colQty"];
            DataGridViewColumn costCol = _grid.Columns["colCost"];
            if (qtyCol == null || costCol == null)
                return false;

            if (colIdx < qtyCol.Index && qtyCol.Visible)
            {
                _grid.CurrentCell = _grid.Rows[rowIdx].Cells[qtyCol.Index];
                return true;
            }

            if (colIdx == qtyCol.Index && costCol.Visible)
            {
                _grid.CurrentCell = _grid.Rows[rowIdx].Cells[costCol.Index];
                return true;
            }

            int nextRow = rowIdx + 1;
            if (nextRow < _grid.Rows.Count && !ReferenceEquals(_grid.Rows[nextRow].Tag, SummaryRowTag))
            {
                int targetCol = qtyCol.Visible ? qtyCol.Index : (costCol.Visible ? costCol.Index : -1);
                if (targetCol >= 0)
                {
                    _grid.CurrentCell = _grid.Rows[nextRow].Cells[targetCol];
                    return true;
                }
            }

            // Đang ở dòng dữ liệu cuối: đã lưu xong ô sửa, giữ nguyên dòng và không nhảy xuống hàng Tổng cộng.
            return true;
        }

        internal void CommitCellEditForTest(int rowIndex, string columnName, object newValue)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;
            DataGridViewColumn col = _grid.Columns[columnName];
            if (col == null)
                return;
            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[col.Index];
            _grid.Rows[rowIndex].Cells[col.Index].Value = newValue;
            GridCellEndEdit(_grid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.10f);
        }

        internal static Color WarningBannerBackColor(ThemeTokens t)
        {
            Color warnBase = Draw.ShiftHsl(t.Danger, 44f, 0.16f);
            return Draw.Mix(t.Surface, warnBase, 0.26f);
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            _gridMenu.AddItem("Làm mới", Keys.F5, delegate { ReloadAll(false); });
            _gridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
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
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
            };
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
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

        private void FillGrid()
        {
            _updatingCell = true;
            try
            {
                _grid.Rows.Clear();
                ThemeTokens t = ThemeManager.Current;

                foreach (OpeningStockItemRow item in _visibleItems)
                {
                    int idx = _grid.Rows.Add(
                        item.Code ?? "",
                        item.Name ?? "",
                        item.UnitName ?? "",
                        item.Quantity,
                        item.CostPrice,
                        item.Amount);
                    _grid.Rows[idx].Tag = item;
                }

                OpeningStockTotals totals = OpeningStockRules.ComputeTotals(_visibleItems);
                if (_visibleItems.Count > 0)
                {
                    int sumIdx = _grid.Rows.Add("", "Tổng cộng", "", null, null, totals.TotalAmount);
                    DataGridViewRow sumRow = _grid.Rows[sumIdx];
                    sumRow.Tag = SummaryRowTag;
                    sumRow.ReadOnly = true;
                    StyleSummaryRow(sumRow, t);
                }

                _empty.Visible = _visibleItems.Count == 0;
                _empty.Text = !_service.CanView
                    ? OpeningStockConstants.NoPermission
                    : string.IsNullOrEmpty(SelectedWarehouseId())
                        ? OpeningStockConstants.EmptyNoWarehouse
                        : OpeningStockConstants.EmptyNoItems;

                UpdateSummaryLabel(totals);
            }
            finally
            {
                _updatingCell = false;
            }
        }

        private void SyncRowCells(int rowIndex, OpeningStockItemRow item)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count || item == null)
                return;
            _updatingCell = true;
            try
            {
                DataGridViewRow r = _grid.Rows[rowIndex];
                r.Cells["colQty"].Value = item.Quantity;
                r.Cells["colCost"].Value = item.CostPrice;
                r.Cells["colAmount"].Value = item.Amount;
            }
            finally
            {
                _updatingCell = false;
            }
        }

        private void UpdateSummaryRowAndBar()
        {
            OpeningStockTotals totals = OpeningStockRules.ComputeTotals(_visibleItems);
            if (_grid.Rows.Count > 0)
            {
                DataGridViewRow last = _grid.Rows[_grid.Rows.Count - 1];
                if (ReferenceEquals(last.Tag, SummaryRowTag))
                {
                    _updatingCell = true;
                    try { last.Cells["colAmount"].Value = totals.TotalAmount; }
                    finally { _updatingCell = false; }
                }
            }
            UpdateSummaryLabel(totals);
        }

        private void UpdateSummaryLabel(OpeningStockTotals totals)
        {
            _summary.Text = string.Format(
                "{0}/{1} mặt hàng có tồn — Tổng giá trị: {2}",
                totals.EnteredCount,
                totals.ItemCount,
                PurchaseConstants.FormatMoneyVi(totals.TotalAmount));
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
            _filterPanel.BackColor = t.Background;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _warnBanner.BackColor = WarningBannerBackColor(t);
            _lblWarn.ForeColor = t.Text;

            ThemeApplier.ApplyInput(_txtSearch, t, t.Background, Draw.StrongBorder(t));
            _dtDate.ApplyTheme(t, t.Background);
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyGrid(_grid, t);

            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (ReferenceEquals(r.Tag, SummaryRowTag))
                    StyleSummaryRow(r, t);
            }
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
            }
            base.Dispose(disposing);
        }
    }
}
