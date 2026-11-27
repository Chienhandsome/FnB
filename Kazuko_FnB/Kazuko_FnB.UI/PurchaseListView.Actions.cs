using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần tải dữ liệu, CRUD, hàng tổng cộng, quyền xem giá nhập, xuất Excel và theme cho <see cref="PurchaseListView"/>.</summary>
    internal sealed partial class PurchaseListView
    {
        private void ApplyBuyPriceVisibility()
        {
            foreach (string name in MoneyColumnNames)
                if (_grid.Columns[name] != null)
                    _grid.Columns[name].Visible = _canViewBuyPrice;
            foreach (string name in DetailMoneyColumnNames)
                if (_detailGrid.Columns[name] != null)
                    _detailGrid.Columns[name].Visible = _canViewBuyPrice;
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddPurchase(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditPurchase(); });
            ToolStripMenuItem viewItem = _gridMenu.AddItem("Xem chi tiết", delegate { ViewPurchaseReadOnly(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeletePurchase(); });
            _gridMenu.AddSeparator();
            _gridMenu.AddItem("Làm mới", delegate { Reload(); });
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
                bool hasRow = CurrentPurchaseRow() != null;
                add.Visible = add.Enabled = _service.CanAdd;
                edit.Visible = _service.CanEdit;
                edit.Enabled = _service.CanEdit && hasRow;
                viewItem.Visible = !_service.CanEdit && _service.CanView;
                viewItem.Enabled = hasRow;
                del.Visible = _service.CanRemove;
                del.Enabled = _service.CanRemove && hasRow;
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
                foreach (string name in MoneyColumnNames)
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
            if (e.Button != MouseButtons.Right) return;
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

        private void PositionEmpty() { _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30); }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            bool isSummary = ReferenceEquals(_grid.Rows[e.RowIndex].Tag, SummaryRowTag);
            string text = isSummary ? "Σ" : (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private PurchaseFilter CurrentFilter()
        {
            var selWh = _warehouse.SelectedItem as LookupItem;
            return new PurchaseFilter
            {
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                WarehouseId = selWh == null ? "" : (selWh.Id ?? ""),
                SupplierId = _supplier.SelectedId ?? "",
                AllowedWarehouseIds = _session.IsAdmin ? null : _allowedWarehouses.Select(w => w.Id).ToList(),
                SearchText = _txtSearch.Text
            };
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                _rows = _service.LoadList(CurrentFilter());
                _empty.Visible = _rows.Count == 0;
                _empty.Text = _service.CanView
                    ? "Không có phiếu nhập kho nào trong khoảng lọc."
                    : PurchaseConstants.NoPermission;
            }
            catch (DataAccessException ex)
            {
                _rows = new List<PurchaseRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }

            FillGrid();
            decimal sumTong = 0m;
            foreach (PurchaseRow r in _rows) sumTong += r.TongCong;
            _summary.Text = _canViewBuyPrice
                ? _rows.Count + " phiếu — Tổng: " + PurchaseConstants.FormatMoneyVi(sumTong)
                : _rows.Count + " phiếu";

            _loading = false;
            UpdateButtons();
            UpdateInfo();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            decimal sumTong = 0m, sumShip = 0m, sumDisc = 0m, sumTax = 0m, sumGoods = 0m;

            foreach (PurchaseRow r in _rows)
            {
                sumTong += r.TongCong;
                sumShip += r.PhiVanChuyen;
                sumDisc += r.TienGiamGia;
                sumTax += r.TienThue;
                sumGoods += r.TienHang;

                // Không đưa số tiền vào ô khi thiếu quyền Xem giá nhập (kinh-nghiem.md: chống lộ qua sao chép).
                int idx = _grid.Rows.Add(
                    r.Note, r.Name, r.Date,
                    _canViewBuyPrice ? (object)r.TongCong : null,
                    _canViewBuyPrice ? (object)r.PhiVanChuyen : null,
                    _canViewBuyPrice ? (object)r.TienGiamGia : null,
                    _canViewBuyPrice ? (object)r.TienThue : null,
                    _canViewBuyPrice ? (object)r.TienHang : null,
                    r.SupplierName, r.WarehouseName, r.EmployeeName, r.Description);
                _grid.Rows[idx].Tag = r;
            }

            if (_rows.Count > 0)
            {
                int sumIdx = _grid.Rows.Add(
                    "", "Tổng cộng", null,
                    _canViewBuyPrice ? (object)sumTong : null,
                    _canViewBuyPrice ? (object)sumShip : null,
                    _canViewBuyPrice ? (object)sumDisc : null,
                    _canViewBuyPrice ? (object)sumTax : null,
                    _canViewBuyPrice ? (object)sumGoods : null,
                    "", "", "", "");
                _grid.Rows[sumIdx].Tag = SummaryRowTag;
                StyleSummaryRow(_grid.Rows[sumIdx], ThemeManager.Current);

                int firstCol = FirstVisibleColumnIndex(_grid);
                _grid.Rows[0].Selected = true;
                if (firstCol >= 0) _grid.CurrentCell = _grid.Rows[0].Cells[firstCol];
            }
        }

        private void StyleSummaryRow(DataGridViewRow row, ThemeTokens t)
        {
            if (row == null) return;
            row.DefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            row.DefaultCellStyle.BackColor = Draw.Mix(t.Surface, t.Primary, 0.10f);
            row.DefaultCellStyle.ForeColor = t.Text;
            row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, t.Primary, 0.22f);
            row.DefaultCellStyle.SelectionForeColor = t.Text;
        }

        private static int FirstVisibleColumnIndex(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
                if (c.Visible) return c.Index;
            return -1;
        }

        private PurchaseRow CurrentPurchaseRow()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as PurchaseRow;
        }

        private void UpdateInfo()
        {
            PurchaseRow row = CurrentPurchaseRow();
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");

            _detailGrid.Rows.Clear();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            try
            {
                foreach (PurchaseLine l in _service.LoadLines(row.Id))
                {
                    _detailGrid.Rows.Add(
                        l.ProductCode, l.ProductName, l.UnitName, l.RawQuantity,
                        _canViewBuyPrice ? (object)l.UnitPrice : null,
                        _canViewBuyPrice ? (object)l.DiscountPct : null,
                        _canViewBuyPrice ? (object)l.Amount : null,
                        l.Note);
                }
            }
            catch (DataAccessException)
            {
                // Giữ lưới chi tiết trống nếu lỗi đọc.
            }
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        private void UpdateButtons()
        {
            bool hasRow = CurrentPurchaseRow() != null;
            _btnAdd.Visible = _btnAdd.Enabled = _service.CanAdd;
            _btnEdit.Visible = _service.CanEdit;
            _btnEdit.Enabled = _service.CanEdit && hasRow;
            _btnRemove.Visible = _service.CanRemove;
            _btnRemove.Enabled = _service.CanRemove && hasRow;
        }

        // ------------------------------------------------------------------ CRUD & Xuất Excel

        private void AddPurchase()
        {
            if (!_service.CanAdd) { ShowWarning(PurchaseConstants.NoPermission); return; }
            using (var dlg = new PurchaseEditDialog(_session, null, false))
            {
                OpenPurchaseDialog(dlg);
                if (dlg.AnySaved) ReloadAndSelect(dlg.SavedId);
            }
        }

        private void OpenCurrentPurchase()
        {
            if (_service.CanEdit) EditPurchase();
            else if (_service.CanView) ViewPurchaseReadOnly();
        }

        private void EditPurchase()
        {
            if (!_service.CanEdit) { ShowWarning(PurchaseConstants.NoPermission); return; }
            PurchaseRow row = CurrentPurchaseRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            using (var dlg = new PurchaseEditDialog(_session, row.Id, false))
            {
                OpenPurchaseDialog(dlg);
                if (dlg.AnySaved || dlg.Deleted) ReloadAndSelect(dlg.SavedId);
            }
        }

        private void ViewPurchaseReadOnly()
        {
            if (!_service.CanView) return;
            PurchaseRow row = CurrentPurchaseRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            using (var dlg = new PurchaseEditDialog(_session, row.Id, true))
                OpenPurchaseDialog(dlg);
        }

        private DialogResult OpenPurchaseDialog(PurchaseEditDialog dlg)
        {
            return DialogOpenerForTest != null ? DialogOpenerForTest(dlg) : dlg.ShowDialog(this);
        }

        private void DeletePurchase()
        {
            if (!_service.CanRemove) { ShowWarning(PurchaseConstants.NoPermission); return; }
            PurchaseRow row = CurrentPurchaseRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            if (!SuppressPopup && !Msg.Confirm(this, PurchaseConstants.DeleteConfirm)) return;

            PurchaseDeleteResult res;
            try
            {
                res = _service.Delete(row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok) { ShowWarning(res.ErrorMessage); return; }
            LastWarningMessageForTest = null;
            if (!string.IsNullOrEmpty(res.Warning))
                ShowWarning(res.Warning);
            ReloadAndSelect(null);
        }

        internal bool ExportGridToExcel()
        {
            return GridExcelExporter.Export(
                this, _grid, "", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal bool SelectRowByIdForTest(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            int col = FirstVisibleColumnIndex(_grid);
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var row = r.Tag as PurchaseRow;
                if (row != null && string.Equals(row.Id, id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    r.Selected = true;
                    if (col >= 0) _grid.CurrentCell = r.Cells[col];
                    UpdateButtons();
                    UpdateInfo();
                    return true;
                }
            }
            return false;
        }

        private void ReloadAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id)) SelectRowByIdForTest(id);
        }

        // ------------------------------------------------------------------ Phím tắt & Giao diện

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool inEditor = sender is TextBox || sender is ComboBox || sender is DateTimePicker;
            if (e.KeyCode == Keys.F3)
            {
                Reload();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
                return;
            }
            if (inEditor) return;
            if (e.KeyCode == Keys.Insert && _service.CanAdd) { AddPurchase(); e.Handled = true; }
            else if (e.KeyCode == Keys.F4) { OpenCurrentPurchase(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete && _service.CanRemove) { DeletePurchase(); e.Handled = true; }
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup) Msg.Warning(this, message);
        }

        private void OnThemeChanged(object sender, EventArgs e) { ApplyTheme(ThemeManager.Current); }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = _split.Panel2.BackColor = t.Background;
            _rightSplit.BackColor = Draw.StrongBorder(t);
            _rightSplit.Panel1.BackColor = _rightSplit.Panel2.BackColor = t.Background;
            ThemeApplier.ApplyInput(_txtSearch, t, t.Background, Draw.StrongBorder(t));
            foreach (FieldFrame f in _frames) f.ApplyTheme(t, t.Background);
            _infoTabs.BackColor = t.Background;
            _infoTabs.ForeColor = t.Text;
            foreach (TabPage p in _infoTabs.TabPages) p.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_grid, t);
            ThemeApplier.ApplyGrid(_detailGrid, t);
            foreach (DataGridViewRow r in _grid.Rows)
                if (ReferenceEquals(r.Tag, SummaryRowTag))
                    StyleSummaryRow(r, t);
            UpdateButtons();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null) _gridMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
