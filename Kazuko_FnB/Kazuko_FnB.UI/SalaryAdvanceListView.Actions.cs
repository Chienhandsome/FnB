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
    /// <summary>Phần tải dữ liệu, CRUD, tổng cộng, xuất Excel, menu ngữ cảnh, phím tắt và theme cho <see cref="SalaryAdvanceListView"/>.</summary>
    internal sealed partial class SalaryAdvanceListView
    {
        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddAdvance(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditAdvance(); });
            ToolStripMenuItem viewItem = _gridMenu.AddItem("Xem chi tiết", delegate { ViewAdvanceReadOnly(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteAdvance(); });
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
                bool hasRow = CurrentAdvanceRow() != null;
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

        private void LayoutTotalBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(12 * s);
            int w = _totalBar.ClientSize.Width;
            int h = _totalBar.ClientSize.Height;
            int valW = Math.Min((int)(320 * s), Math.Max((int)(140 * s), w / 2));
            _lblTotalAmount.SetBounds(Math.Max(pad, w - pad - valW), 0, valW, h);
            _lblTotalCaption.SetBounds(pad, 0, Math.Max(0, _lblTotalAmount.Left - pad - (int)(8 * s)), h);
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

        private SalaryAdvanceFilter CurrentFilter()
        {
            var selEmp = _employee.SelectedItem as LookupItem;
            string empId = selEmp == null ? "" : (selEmp.Id ?? "");
            return new SalaryAdvanceFilter
            {
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                EmployeeId = empId,
                AllowedStoreIds = _session.IsAdmin ? null : new List<string>(_allowedStoreIds),
                SearchText = _txtSearch.Text
            };
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                _multiStore = _service.CountActiveStores() > 1;
                if (_grid.Columns["colStore"] != null)
                    _grid.Columns["colStore"].Visible = _multiStore;

                _rows = _service.LoadList(CurrentFilter());
                _empty.Visible = _rows.Count == 0;
                _empty.Text = _service.CanView
                    ? "Không có phiếu tạm ứng lương nào trong khoảng lọc."
                    : SalaryAdvanceText.NoPermission;
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
            _totalAmount = total;
            string formatted = VoucherConstants.FormatMoneyVi(total);
            _summary.Text = _rows.Count + " phiếu — Tổng: " + formatted;
            _lblTotalCaption.Text = "Tổng cộng (" + _rows.Count + " phiếu):";
            _lblTotalAmount.Text = "Tổng tiền tạm ứng: " + formatted;
            LayoutTotalBar();

            _loading = false;
            UpdateButtons();
            UpdateInfo();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (VoucherRow r in _rows)
            {
                int idx = _grid.Rows.Add(
                    r.Name,
                    r.Date,
                    SalaryAdvanceRules.DisplayEmployee(r),
                    r.Amount,
                    r.ReasonName,
                    r.Description,
                    r.StoreName,
                    r.Note);
                _grid.Rows[idx].Tag = r;
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }

        private VoucherRow CurrentAdvanceRow()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as VoucherRow;
        }

        private void UpdateInfo()
        {
            VoucherRow row = CurrentAdvanceRow();
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
            bool hasRow = CurrentAdvanceRow() != null;
            _btnAdd.Visible = _service.CanAdd;
            _btnAdd.Enabled = _service.CanAdd;
            _btnEdit.Visible = _service.CanEdit;
            _btnEdit.Enabled = _service.CanEdit && hasRow;
            _btnRemove.Visible = _service.CanRemove;
            _btnRemove.Enabled = _service.CanRemove && hasRow;
        }

        // ------------------------------------------------------------------ CRUD & Xuất Excel

        private void AddAdvance()
        {
            if (!_service.CanAdd)
            {
                ShowWarning(SalaryAdvanceText.NoPermission);
                return;
            }
            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryAdvance(_session, null, false))
            {
                OpenAdvanceDialog(dlg);
                if (dlg.AnySaved)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void OpenCurrentAdvance()
        {
            if (_service.CanEdit)
                EditAdvance();
            else if (_service.CanView)
                ViewAdvanceReadOnly();
        }

        private void EditAdvance()
        {
            if (!_service.CanEdit)
            {
                ShowWarning(SalaryAdvanceText.NoPermission);
                return;
            }
            VoucherRow row = CurrentAdvanceRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;

            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryAdvance(_session, row.Id, false))
            {
                OpenAdvanceDialog(dlg);
                if (dlg.AnySaved)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void ViewAdvanceReadOnly()
        {
            if (!_service.CanView)
                return;
            VoucherRow row = CurrentAdvanceRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;
            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryAdvance(_session, row.Id, true))
                OpenAdvanceDialog(dlg);
        }

        private DialogResult OpenAdvanceDialog(PhieuThuDialog dlg)
        {
            if (DialogOpenerForTest != null)
                return DialogOpenerForTest(dlg);
            return dlg.ShowDialog(this);
        }

        private void DeleteAdvance()
        {
            if (!_service.CanRemove)
            {
                ShowWarning(SalaryAdvanceText.NoPermission);
                return;
            }
            VoucherRow row = CurrentAdvanceRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;

            if (!SuppressPopup && !Msg.Confirm(this, SalaryAdvanceText.DeleteConfirm))
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
                delegate (string msg) { LastExportMessageForTest = msg; });
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
                AddAdvance();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                OpenCurrentAdvance();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _service.CanRemove)
            {
                DeleteAdvance();
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
            _totalBar.BackColor = Draw.Mix(t.Surface, t.Primary, 0.08f);
            _lblTotalCaption.BackColor = Color.Transparent;
            _lblTotalCaption.ForeColor = t.Text;
            _lblTotalAmount.BackColor = Color.Transparent;
            _lblTotalAmount.ForeColor = t.Primary;
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
                if (_totalFont != null)
                    _totalFont.Dispose();
                if (_gridMenu != null)
                    _gridMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
