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
    /// <summary>Phần tải dữ liệu, CRUD, xuất Excel, menu ngữ cảnh, phím tắt và theme cho <see cref="StockTakeListView"/>.</summary>
    internal sealed partial class StockTakeListView
    {
        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddDocument(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditDocument(); });
            ToolStripMenuItem viewItem = _gridMenu.AddItem("Xem chi tiết", delegate { ViewDocumentReadOnly(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteDocument(); });
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
                bool hasRow = CurrentRow() != null;
                add.Visible = add.Enabled = _service.CanAdd;
                edit.Visible = _service.CanEdit;
                edit.Enabled = _service.CanEdit && hasRow;
                viewItem.Visible = !_service.CanEdit && _service.CanView;
                viewItem.Enabled = hasRow;
                del.Visible = _service.CanRemove;
                del.Enabled = _service.CanRemove && hasRow;
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
            };
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
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private StockTakeFilter CurrentFilter()
        {
            var selWh = _warehouse.SelectedItem as LookupItem;
            return new StockTakeFilter
            {
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                WarehouseId = selWh == null ? "" : (selWh.Id ?? ""),
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
                    ? "Không có phiếu kiểm kê nào trong khoảng lọc."
                    : PurchaseConstants.NoPermission;
            }
            catch (DataAccessException ex)
            {
                _rows = new List<StockTakeRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }

            FillGrid();
            _summary.Text = _rows.Count + " phiếu kiểm kê";

            _loading = false;
            UpdateButtons();
            UpdateInfo();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (StockTakeRow r in _rows)
            {
                int idx = _grid.Rows.Add(r.Note, r.Name, r.Date, r.WarehouseName, r.Description);
                _grid.Rows[idx].Tag = r;
            }

            if (_rows.Count > 0)
            {
                int firstCol = FirstVisibleColumnIndex(_grid);
                _grid.Rows[0].Selected = true;
                if (firstCol >= 0) _grid.CurrentCell = _grid.Rows[0].Cells[firstCol];
            }
        }

        private static int FirstVisibleColumnIndex(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
                if (c.Visible) return c.Index;
            return -1;
        }

        private StockTakeRow CurrentRow()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as StockTakeRow;
        }

        private void UpdateInfo()
        {
            StockTakeRow row = CurrentRow();
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");

            _detailGrid.Rows.Clear();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            ThemeTokens t = ThemeManager.Current;
            try
            {
                foreach (StockTakeLine l in _service.LoadLines(row.Id))
                {
                    int idx = _detailGrid.Rows.Add(
                        l.ProductCode, l.ProductName, l.UnitName,
                        l.SystemQuantity, l.ActualQuantityBase, l.Difference, l.Note);
                    _detailGrid.Rows[idx].Tag = l;
                    StockTakeEditDialog.StyleDiffRow(_detailGrid.Rows[idx], l.Tone, t);
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
            bool hasRow = CurrentRow() != null;
            _btnAdd.Visible = _btnAdd.Enabled = _service.CanAdd;
            _btnEdit.Visible = _service.CanEdit;
            _btnEdit.Enabled = _service.CanEdit && hasRow;
            _btnRemove.Visible = _service.CanRemove;
            _btnRemove.Enabled = _service.CanRemove && hasRow;
        }

        // ------------------------------------------------------------------ CRUD & Xuất Excel

        private void AddDocument()
        {
            if (!_service.CanAdd) { ShowWarning(PurchaseConstants.NoPermission); return; }
            using (var dlg = new StockTakeEditDialog(_session, null, false))
            {
                OpenDialog(dlg);
                if (dlg.AnySaved) ReloadAndSelect(dlg.SavedId);
            }
        }

        private void OpenCurrentDocument()
        {
            if (_service.CanEdit) EditDocument();
            else if (_service.CanView) ViewDocumentReadOnly();
        }

        private void EditDocument()
        {
            if (!_service.CanEdit) { ShowWarning(PurchaseConstants.NoPermission); return; }
            StockTakeRow row = CurrentRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            using (var dlg = new StockTakeEditDialog(_session, row.Id, false))
            {
                OpenDialog(dlg);
                if (dlg.AnySaved || dlg.Deleted) ReloadAndSelect(dlg.SavedId);
            }
        }

        private void ViewDocumentReadOnly()
        {
            if (!_service.CanView) return;
            StockTakeRow row = CurrentRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            using (var dlg = new StockTakeEditDialog(_session, row.Id, true))
                OpenDialog(dlg);
        }

        private DialogResult OpenDialog(StockTakeEditDialog dlg)
        {
            return DialogOpenerForTest != null ? DialogOpenerForTest(dlg) : dlg.ShowDialog(this);
        }

        private void DeleteDocument()
        {
            if (!_service.CanRemove) { ShowWarning(PurchaseConstants.NoPermission); return; }
            StockTakeRow row = CurrentRow();
            if (row == null || string.IsNullOrEmpty(row.Id)) return;
            if (!SuppressPopup && !Msg.Confirm(this, StockTakeConstants.DeleteConfirm)) return;

            StockTakeDeleteResult res;
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
                var row = r.Tag as StockTakeRow;
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
            if (e.KeyCode == Keys.Insert && _service.CanAdd) { AddDocument(); e.Handled = true; }
            else if (e.KeyCode == Keys.F4) { OpenCurrentDocument(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete && _service.CanRemove) { DeleteDocument(); e.Handled = true; }
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
            foreach (DataGridViewRow r in _detailGrid.Rows)
            {
                var line = r.Tag as StockTakeLine;
                if (line != null)
                    StockTakeEditDialog.StyleDiffRow(r, line.Tone, t);
            }
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
