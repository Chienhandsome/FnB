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

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phần tải dữ liệu, Tạo phiếu chi lương (kèm cảnh báo Thực nhận/Còn lại = 0 và xác nhận khi đã có phiếu),
    /// Xem/Sửa phiếu, Xóa phiếu (chọn phiếu khi nhân viên có nhiều phiếu chi lương), phím tắt và menu ngữ cảnh
    /// cho <see cref="PayrollPayoutDialog"/>.
    /// </summary>
    internal sealed partial class PayrollPayoutDialog
    {
        private void WireEvents()
        {
            _btnCreate.Click += delegate { CreatePayoutVoucher(); };
            _btnViewEdit.Click += delegate { ViewOrEditVoucher(); };
            _btnDelete.Click += delegate { DeleteVoucher(); };
            _btnRefresh.Click += delegate { ReloadData(SelectedEmployeeId()); };

            _grid.SelectionChanged += delegate { if (!_loading) UpdateButtons(); };
            _grid.CurrentCellChanged += delegate { if (!_loading) UpdateButtons(); };
            _grid.RowPostPaint += DrawRowNumber;
            _grid.MouseDown += OnGridMouseDown;
            _grid.CellDoubleClick += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;
                PayrollPayoutRow row = SelectedPayoutRow();
                if (row == null)
                    return;
                if (row.Vouchers.Count > 0)
                    ViewOrEditVoucher();
                else
                    CreatePayoutVoucher();
            };
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem mCreate = _gridMenu.AddItem("Tạo phiếu chi lương", delegate { CreatePayoutVoucher(); });
            ToolStripMenuItem mEdit = _gridMenu.AddItem("Xem/Sửa phiếu", delegate { ViewOrEditVoucher(); });
            ToolStripMenuItem mDelete = _gridMenu.AddItem("Xóa phiếu", delegate { DeleteVoucher(); });
            _gridMenu.AddSeparator();
            _gridMenu.AddItem("Làm mới", delegate { ReloadData(SelectedEmployeeId()); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem mCopyCell = _gridMenu.AddItem("Sao chép ô", delegate { DetailGridMenu.CopyCell(_grid); });
            ToolStripMenuItem mCopySel = _gridMenu.AddItem("Sao chép vùng chọn", delegate { DetailGridMenu.CopySelection(_grid); });

            _gridMenu.Opening += delegate
            {
                PayrollPayoutRow row = SelectedPayoutRow();
                bool canPay = _service.CanPayOut;
                bool hasRow = row != null;
                bool hasVoucher = hasRow && row.Vouchers.Count > 0;

                mCreate.Enabled = canPay && hasRow;
                mEdit.Enabled = _service.CanView && hasVoucher;
                mDelete.Enabled = canPay && hasVoucher;
                mCopyCell.Enabled = _grid.CurrentCell != null;
                mCopySel.Enabled = _grid.SelectedCells.Count > 0;
            };
        }

        private void OnGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0 && !ReferenceEquals(_grid.Rows[hit.RowIndex].Tag, SummaryRowTag))
            {
                _grid.ClearSelection();
                _grid.Rows[hit.RowIndex].Selected = true;
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[col];
                UpdateButtons();
            }
            _gridMenu.Show(_grid, e.Location);
        }

        internal void ReloadData(string selectEmployeeId)
        {
            _loading = true;
            try
            {
                _sheet = _service.LoadPayoutSheet(_period.Id);
                FillGrid(selectEmployeeId);
            }
            catch (DataAccessException ex)
            {
                _sheet = null;
                FillGrid(null);
                ShowWarning("Không đọc được dữ liệu chi lương: " + ex.Message);
            }
            finally
            {
                _loading = false;
                UpdateButtons();
            }
        }

        internal void UpdateButtons()
        {
            PayrollPayoutRow row = SelectedPayoutRow();
            bool canPay = _service.CanPayOut;
            bool hasRow = row != null;
            bool hasVoucher = hasRow && row.Vouchers.Count > 0;

            _btnCreate.Enabled = canPay && hasRow;
            _btnViewEdit.Enabled = _service.CanView && hasVoucher;
            _btnDelete.Enabled = canPay && hasVoucher;
        }

        internal bool SelectEmployeeByIdForTest(string employeeId)
        {
            if (string.IsNullOrEmpty(employeeId))
                return false;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i].Tag as PayrollPayoutRow;
                if (row != null && string.Equals(row.EmployeeId, employeeId, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.Rows[i].Selected = true;
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    UpdateButtons();
                    return true;
                }
            }
            return false;
        }

        internal DataGridViewRow FindRowForEmployeeForTest(string employeeId)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i].Tag as PayrollPayoutRow;
                if (row != null && string.Equals(row.EmployeeId, employeeId, StringComparison.Ordinal))
                    return _grid.Rows[i];
            }
            return null;
        }

        internal DataGridViewRow FindSummaryRowForTest()
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (ReferenceEquals(_grid.Rows[i].Tag, SummaryRowTag))
                    return _grid.Rows[i];
            }
            return null;
        }

        // ------------------------------------------------------------------ Tạo / Xem-Sửa / Xóa phiếu chi lương

        internal bool CreatePayoutVoucher()
        {
            if (!_service.CanPayOut)
            {
                ShowWarning(PayrollPayoutText.NoPermission);
                return false;
            }
            PayrollPayoutRow row = SelectedPayoutRow();
            if (row == null)
                return false;

            // Khi Còn lại và Thực nhận đều 0: cảnh báo nhưng vẫn cho tạo nếu người dùng đồng ý.
            if (row.RemainingAmount == 0m && row.NetSalary == 0m)
            {
                string zeroMsg = PayrollPayoutText.ZeroSalaryWarning;
                if (!ConfirmAction(zeroMsg, ConfirmZeroSalaryOverrideForTest))
                    return false;
            }

            // Khi nhân viên đã có phiếu chi lương: hỏi xác nhận tạo thêm phiếu.
            if (row.Vouchers.Count > 0)
            {
                string addMsg = PayrollPayoutText.AlreadyPaidConfirm(row.PaidAmount);
                if (!ConfirmAction(addMsg, ConfirmAdditionalPayoutOverrideForTest))
                    return false;
            }

            var preset = new PayrollPayoutPreset
            {
                PayrollId = _period.Id,
                Month = _period.Month,
                Year = _period.Year,
                PeriodName = _period.Name,
                EmployeeId = row.EmployeeId,
                EmployeeName = row.EmployeeName,
                NetSalary = row.NetSalary,
                PaidAmount = row.PaidAmount,
                RemainingAmount = row.RemainingAmount,
                VoucherId = null,
                ReadOnly = false
            };

            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryPayout(_session, preset))
            {
                OpenVoucherDialog(dlg);
                if (dlg.AnySaved)
                {
                    AnyChanged = true;
                    ReloadData(row.EmployeeId);
                    return true;
                }
            }
            return false;
        }

        internal bool ViewOrEditVoucher()
        {
            if (!_service.CanView)
                return false;
            PayrollPayoutRow row = SelectedPayoutRow();
            if (row == null || row.Vouchers.Count == 0)
                return false;

            PayrollPayoutVoucherInfo target = PickTargetVoucher(row.Vouchers, "Chọn phiếu chi lương để xem/sửa");
            if (target == null)
                return false;

            var preset = new PayrollPayoutPreset
            {
                PayrollId = _period.Id,
                Month = _period.Month,
                Year = _period.Year,
                PeriodName = _period.Name,
                EmployeeId = row.EmployeeId,
                EmployeeName = row.EmployeeName,
                NetSalary = row.NetSalary,
                PaidAmount = row.PaidAmount,
                RemainingAmount = row.RemainingAmount,
                VoucherId = target.Id,
                ReadOnly = !_service.CanPayOut
            };

            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryPayout(_session, preset))
            {
                OpenVoucherDialog(dlg);
                if (dlg.AnySaved)
                {
                    AnyChanged = true;
                    ReloadData(row.EmployeeId);
                    return true;
                }
            }
            return false;
        }

        internal bool DeleteVoucher()
        {
            LastDeleteResultForTest = null;
            if (!_service.CanPayOut)
            {
                ShowWarning(PayrollPayoutText.NoPermission);
                return false;
            }
            PayrollPayoutRow row = SelectedPayoutRow();
            if (row == null || row.Vouchers.Count == 0)
                return false;

            PayrollPayoutVoucherInfo target = PickTargetVoucher(row.Vouchers, "Chọn phiếu chi lương cần xóa");
            if (target == null)
                return false;

            string confirmMsg = PayrollPayoutText.DeleteConfirm(target.Number);
            if (!ConfirmAction(confirmMsg, ConfirmDeleteOverrideForTest))
                return false;

            VoucherDeleteResult res;
            try
            {
                res = _service.Delete(target.Id, _period.Id);
            }
            catch (DataAccessException ex)
            {
                res = VoucherDeleteResult.Fail("Không xóa được phiếu chi lương: " + ex.Message);
            }

            LastDeleteResultForTest = res;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage ?? "Không thể xóa phiếu chi lương.");
                return false;
            }

            AnyChanged = true;
            LastWarningForTest = null;
            ReloadData(row.EmployeeId);
            return true;
        }

        private PayrollPayoutVoucherInfo PickTargetVoucher(List<PayrollPayoutVoucherInfo> vouchers, string title)
        {
            if (vouchers == null || vouchers.Count == 0)
                return null;
            if (vouchers.Count == 1)
                return vouchers[0];
            if (VoucherPickerOverrideForTest != null)
                return VoucherPickerOverrideForTest(vouchers, title);
            if (SuppressPopup)
                return vouchers[vouchers.Count - 1];

            using (var picker = new VoucherPickDialog(title, vouchers))
            {
                return picker.ShowDialog(this) == DialogResult.OK ? picker.Picked : null;
            }
        }

        private DialogResult OpenVoucherDialog(PhieuThuDialog dlg)
        {
            if (VoucherDialogOpenerForTest != null)
                return VoucherDialogOpenerForTest(dlg);
            return dlg.ShowDialog(this);
        }

        private bool ConfirmAction(string message, Func<string, bool> testOverride)
        {
            LastConfirmMessageForTest = message;
            if (testOverride != null)
                return testOverride(message);
            if (SuppressPopup)
                return true;
            return Msg.Confirm(this, message);
        }

        private PayrollPayoutRow SelectedPayoutRow()
        {
            if (_grid.CurrentRow == null || ReferenceEquals(_grid.CurrentRow.Tag, SummaryRowTag))
                return null;
            return _grid.CurrentRow.Tag as PayrollPayoutRow;
        }

        private string SelectedEmployeeId()
        {
            PayrollPayoutRow row = SelectedPayoutRow();
            return row == null ? null : row.EmployeeId;
        }

        private void ShowWarning(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        /// <summary>Hộp chọn gọn một phiếu chi lương khi nhân viên có từ 2 phiếu trở lên.</summary>
        private sealed class VoucherPickDialog : ThemedDialog
        {
            private readonly List<PayrollPayoutVoucherInfo> _items;
            private readonly ListBox _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

            public PayrollPayoutVoucherInfo Picked { get; private set; }

            public VoucherPickDialog(string title, List<PayrollPayoutVoucherInfo> items)
                : base(title.ToUpperInvariant(), "Chọn một phiếu chi lương để tiếp tục", 460, 280)
            {
                _items = items;
                _list.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
                foreach (PayrollPayoutVoucherInfo v in items)
                {
                    string method = v.Transfer ? "Chuyển khoản" : "Tiền mặt";
                    _list.Items.Add(string.Format("{0}  —  {1}  —  {2} đ  ({3})",
                        v.Number,
                        v.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                        PayrollPayoutRules.FormatMoney(v.Amount),
                        method));
                }
                if (_list.Items.Count > 0)
                    _list.SelectedIndex = _list.Items.Count - 1;
                _list.DoubleClick += delegate { Accept(); };
                Body.Controls.Add(_list);

                FlatButton btnCancel = AddButton("Hủy", null, delegate { Close(); });
                AddButton("Chọn", "primary", delegate { Accept(); });
                CancelButton = btnCancel;
                ApplyDialogTheme();
            }

            private void Accept()
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _items.Count)
                {
                    Picked = _items[_list.SelectedIndex];
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }

            protected override void ApplyDialogTheme()
            {
                base.ApplyDialogTheme();
                ThemeTokens t = ThemeManager.Current;
                _list.BackColor = t.Surface;
                _list.ForeColor = t.Text;
            }
        }
    }
}
