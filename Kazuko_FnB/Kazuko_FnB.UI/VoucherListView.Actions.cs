using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
    /// <summary>Phần tải dữ liệu, CRUD, xuất Excel, menu ngữ cảnh, phím tắt và theme cho <see cref="VoucherListView"/>.</summary>
    internal sealed partial class VoucherListView
    {
        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddVoucher(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditVoucher(); });
            ToolStripMenuItem viewItem = _gridMenu.AddItem("Xem chi tiết", delegate { ViewVoucherReadOnly(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteVoucher(); });
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
                bool hasRow = CurrentVoucherRow() != null;
                add.Visible = _service.CanAdd;
                add.Enabled = _service.CanAdd;
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
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
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
            if (!_grid.RowHeadersVisible)
                return;
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private VoucherFilter CurrentFilter()
        {
            var selStore = _store.SelectedItem as LookupItem;
            string storeId = selStore == null ? "" : (selStore.Id ?? "");
            return new VoucherFilter
            {
                Kind = _kind,
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                StoreId = storeId,
                AllowedStoreIds = _session.IsAdmin ? null : new List<string>(_allowedStoreIds),
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
                string label = _kind == VoucherKind.Expense ? "phiếu chi" : "phiếu thu";
                _empty.Text = _service.CanView
                    ? "Không có " + label + " nào trong khoảng lọc."
                    : VoucherConstants.NoPermission;
            }
            catch (DataAccessException ex)
            {
                _rows = new List<VoucherRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }

            FillGrid();
            decimal total = 0m;
            foreach (VoucherRow r in _rows)
                total += r.Amount;
            _summary.Text = _rows.Count + " phiếu — Tổng: " + VoucherConstants.FormatMoneyVi(total);

            _loading = false;
            UpdateButtons();
            UpdateInfo();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (VoucherRow r in _rows)
            {
                string method = r.Transfer ? "Chuyển khoản" : "Tiền mặt";
                int idx = _grid.Rows.Add(
                    r.Date,
                    r.Name,
                    r.ObjectName,
                    r.Address,
                    r.ReasonName,
                    r.Amount,
                    method,
                    r.Transfer ? r.BankAccountName : "",
                    r.StoreName,
                    r.Description,
                    r.OriginalDoc,
                    r.Note);
                _grid.Rows[idx].Tag = r;
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }

        private VoucherRow CurrentVoucherRow()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as VoucherRow;
        }

        private void UpdateInfo()
        {
            VoucherRow row = CurrentVoucherRow();
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        private void UpdateButtons()
        {
            bool hasRow = CurrentVoucherRow() != null;
            _btnAdd.Visible = _service.CanAdd;
            _btnAdd.Enabled = _service.CanAdd;
            _btnEdit.Visible = _service.CanEdit;
            _btnEdit.Enabled = _service.CanEdit && hasRow;
            _btnRemove.Visible = _service.CanRemove;
            _btnRemove.Enabled = _service.CanRemove && hasRow;
        }

        // ------------------------------------------------------------------ CRUD & Xuất Excel

        private void AddVoucher()
        {
            if (!_service.CanAdd)
            {
                ShowWarning(VoucherConstants.NoPermission);
                return;
            }
            using (var dlg = new PhieuThuDialog(_session, _kind, null, false))
            {
                OpenVoucherDialog(dlg);
                if (dlg.AnySaved)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void OpenCurrentVoucher()
        {
            if (_service.CanEdit)
                EditVoucher();
            else if (_service.CanView)
                ViewVoucherReadOnly();
        }

        private void EditVoucher()
        {
            if (!_service.CanEdit)
            {
                ShowWarning(VoucherConstants.NoPermission);
                return;
            }
            VoucherRow row = CurrentVoucherRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;

            string linkedErr = VoucherRules.GetLinkedBlockMessage(row);
            if (linkedErr != null)
            {
                ShowWarning(linkedErr);
                return;
            }

            using (var dlg = new PhieuThuDialog(_session, _kind, row.Id, false))
            {
                OpenVoucherDialog(dlg);
                if (dlg.AnySaved)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void ViewVoucherReadOnly()
        {
            if (!_service.CanView)
                return;
            VoucherRow row = CurrentVoucherRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;
            using (var dlg = new PhieuThuDialog(_session, _kind, row.Id, true))
                OpenVoucherDialog(dlg);
        }

        private DialogResult OpenVoucherDialog(PhieuThuDialog dlg)
        {
            if (DialogOpenerForTest != null)
                return DialogOpenerForTest(dlg);
            return dlg.ShowDialog(this);
        }

        private void DeleteVoucher()
        {
            if (!_service.CanRemove)
            {
                ShowWarning(VoucherConstants.NoPermission);
                return;
            }
            VoucherRow row = CurrentVoucherRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;

            string linkedErr = VoucherRules.GetLinkedBlockMessage(row);
            if (linkedErr != null)
            {
                ShowWarning(linkedErr);
                return;
            }

            if (!SuppressPopup && !Msg.Confirm(this, VoucherConstants.DeleteConfirm))
                return;

            VoucherDeleteResult res;
            try
            {
                res = _service.Delete(row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }
            LastWarningMessageForTest = null;
            ReloadAndSelect(null);
        }

        internal bool ExportGridToExcel()
        {
            return GridExcelExporter.Export(
                this,
                _grid,
                "",
                GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal bool SelectRowByIdForTest(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var row = r.Tag as VoucherRow;
                if (row != null && string.Equals(row.Id, id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    r.Selected = true;
                    _grid.CurrentCell = r.Cells[0];
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
            if (!string.IsNullOrEmpty(id))
                SelectRowByIdForTest(id);
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
            if (inEditor)
                return;
            if (e.KeyCode == Keys.Insert && _service.CanAdd)
            {
                AddVoucher();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                OpenCurrentVoucher();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _service.CanRemove)
            {
                DeleteVoucher();
                e.Handled = true;
            }
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

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
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _rightSplit.BackColor = Draw.StrongBorder(t);
            _rightSplit.Panel1.BackColor = t.Background;
            _rightSplit.Panel2.BackColor = t.Background;
            ThemeApplier.ApplyInput(_txtSearch, t, t.Background, Draw.StrongBorder(t));
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            _infoTabs.BackColor = t.Background;
            _infoTabs.ForeColor = t.Text;
            foreach (TabPage p in _infoTabs.TabPages)
                p.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_grid, t);
            UpdateButtons();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null)
                    _gridMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
