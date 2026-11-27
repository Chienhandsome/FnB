using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Chế độ Chi lương (LOAI = 1, TBANGLUONGID = bảng lương, LOAIDOITUONG = 1, LATAMUNG = 0,
    /// cho phép chuyển khoản) của <see cref="PhieuThuDialog"/> (patch P3b, spec docs/specs/nhan-su-tinh-luong.md §5).
    /// Tách partial riêng để không đổi hành vi Phiếu chi thường và Tạm ứng lương.
    /// </summary>
    internal sealed partial class PhieuThuDialog
    {
        private bool _isSalaryPayoutMode;
        private PayrollPayoutService _salaryPayoutService;
        private PayrollPayoutPreset _salaryPayoutPreset;

        internal bool IsSalaryPayoutModeForTest { get { return _isSalaryPayoutMode; } }

        /// <summary>
        /// Mở hộp thoại Tạo / Sửa / Xem phiếu chi lương dùng chung layout Phiếu chi:
        /// khóa đối tượng = Nhân viên được chọn, cho phép chuyển khoản (bắt buộc TK ngân hàng),
        /// mặc định diễn giải "Chi tiền lương tháng m năm y", số tiền = Còn lại nếu &gt; 0 ngược lại Thực nhận,
        /// lý do = lý do phiếu chi lương gần nhất, ngày giới hạn trong tháng bảng lương.
        /// </summary>
        public static PhieuThuDialog CreateForSalaryPayout(UserSession session, PayrollPayoutPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException("preset");
            return new PhieuThuDialog(
                session,
                VoucherKind.Expense,
                preset.VoucherId,
                preset.ReadOnly,
                false,
                false,
                null,
                preset);
        }

        private static string BuildSalaryPayoutTitle(bool isNew, bool readOnly)
        {
            if (readOnly)
                return "PHIẾU CHI LƯƠNG — XEM CHI TIẾT";
            return isNew ? "PHIẾU CHI LƯƠNG" : "PHIẾU CHI LƯƠNG — SỬA";
        }

        private static string BuildSalaryPayoutSubtitle(PayrollPayoutPreset preset, bool readOnly)
        {
            if (readOnly)
                return "Chế độ chỉ xem phiếu chi lương";
            string period = preset != null && !string.IsNullOrEmpty(preset.PeriodName)
                ? preset.PeriodName
                : (preset != null ? PayrollText.FormatPeriodName(preset.Month, preset.Year) : "");
            return string.Format("Chi lương {0} — Ctrl+L: Lưu, ESC: Đóng.", period);
        }

        private void ConfigureSalaryPayoutLayout()
        {
            if (_noDebtChange != null)
            {
                _noDebtChange.Checked = false;
                _noDebtChange.Visible = false;
                _noDebtChange.Enabled = false;
                Label ndcLbl = _form.LabelOf(_noDebtChange.Name);
                if (ndcLbl != null)
                    ndcLbl.Visible = false;
            }

            if (_amount != null)
            {
                _amount.Minimum = 0m;
                _amount.Maximum = PayrollPayoutText.MaxAmount;
                _amount.DecimalPlaces = 2;
            }
        }

        private void InitNewSalaryPayoutDocument()
        {
            _loading = true;
            try
            {
                _editingId = null;
                _isNew = true;
                _linkedWarning = null;
                SetTitle(BuildSalaryPayoutTitle(true, _readOnly));

                try
                {
                    _number.Text = _salaryPayoutService.GenerateNumber();
                }
                catch (DataAccessException)
                {
                    _number.Text = "";
                }
                _number.ReadOnly = _readOnly;

                if (PhieuThuRules.Employee < _objectType.Items.Count)
                    _objectType.SelectedIndex = PhieuThuRules.Employee;

                EnsureEmployeeInLookup(_salaryPayoutPreset.EmployeeId, _salaryPayoutPreset.EmployeeName);
                _employee.SelectId(_salaryPayoutPreset.EmployeeId);
                _customer.SelectId(null);
                _supplier.SelectId(null);
                _partyName.Text = _salaryPayoutPreset.EmployeeName ?? "";
                _partyAddress.Text = "";

                string latestReasonId = null;
                try { latestReasonId = _salaryPayoutService.LoadLatestReasonId(); }
                catch (DataAccessException) { latestReasonId = null; }
                _reason.SelectId(latestReasonId);

                decimal defaultAmount = PayrollPayoutRules.DefaultPayoutAmount(
                    _salaryPayoutPreset.NetSalary,
                    _salaryPayoutPreset.RemainingAmount);
                SetAmount(defaultAmount);

                _transfer.Checked = false;
                _bank.SelectId(null);
                SetOptionalText(_description, PayrollPayoutText.DefaultDescription(_salaryPayoutPreset.Month, _salaryPayoutPreset.Year));
                SetOptionalText(_originalDoc, "");
                SetOptionalText(_note, "");
                if (_noDebtChange != null)
                    _noDebtChange.Checked = false;

                if (_session.CurrentStore != null)
                    _store.SelectId(_session.CurrentStore.Id);
                else if (_store.Items.Count > 0)
                    _store.SelectedIndex = 0;

                _date.Value = PayrollPayoutRules.DefaultPayoutDate(
                    _salaryPayoutPreset.Year,
                    _salaryPayoutPreset.Month,
                    DateTime.Today);
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
            ApplyModeState();
        }

        private void LoadExistingSalaryPayout(string voucherId)
        {
            VoucherRow row = null;
            try
            {
                row = _salaryPayoutService.LoadById(voucherId, _salaryPayoutPreset.PayrollId);
            }
            catch (DataAccessException)
            {
                row = null;
            }

            if (row == null)
            {
                _loadFailed = true;
                return;
            }

            _loading = true;
            try
            {
                SetTitle(BuildSalaryPayoutTitle(false, _readOnly));
                _number.Text = row.Name ?? "";
                _date.Value = row.Date;
                if (PhieuThuRules.Employee < _objectType.Items.Count)
                    _objectType.SelectedIndex = PhieuThuRules.Employee;

                string empName = !string.IsNullOrWhiteSpace(row.EmployeeName)
                    ? row.EmployeeName
                    : (!string.IsNullOrWhiteSpace(row.ObjectName) ? row.ObjectName : (_salaryPayoutPreset.EmployeeName ?? ""));
                EnsureEmployeeInLookup(row.EmployeeId, empName);
                _employee.SelectId(row.EmployeeId);
                _customer.SelectId(null);
                _supplier.SelectId(null);
                _partyName.Text = empName;
                _partyAddress.Text = "";
                _store.SelectId(row.StoreId);
                _reason.SelectId(row.ReasonId);
                SetAmount(row.Amount);
                _transfer.Checked = row.Transfer;
                _bank.SelectId(row.BankAccountId);
                SetOptionalText(_description, row.Description);
                SetOptionalText(_originalDoc, row.OriginalDoc);
                SetOptionalText(_note, row.Note);
                if (_noDebtChange != null)
                    _noDebtChange.Checked = false;
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
        }

        private void EnsureEmployeeInLookup(string employeeId, string employeeName)
        {
            if (string.IsNullOrEmpty(employeeId))
                return;
            for (int i = 0; i < _employee.Items.Count; i++)
            {
                var item = _employee.Items[i] as LookupItem;
                if (item != null && string.Equals(item.Id, employeeId, StringComparison.Ordinal))
                    return;
            }
            _employee.Items.Add(new LookupItem { Id = employeeId, Name = employeeName ?? "" });
        }

        private void ApplySalaryPayoutModeState()
        {
            _objectType.Enabled = false;
            _employee.Enabled = false;
            _customer.Enabled = false;
            _supplier.Enabled = false;
            _partyName.Enabled = false;
            _partyAddress.Enabled = false;
            _transfer.Visible = true;
            _transfer.Enabled = !_readOnly;
            _bank.Visible = true;
            _bank.Enabled = !_readOnly && PhieuThuRules.BankAccountEnabled(_transfer.Checked);
            _btnSaveNew.Visible = false;
            if (!_readOnly)
                _date.Enabled = _salaryPayoutService.CanChangeDate;
            SubtitleText = BuildSalaryPayoutSubtitle(_salaryPayoutPreset, _readOnly);
        }

        private void SaveSalaryPayoutMode()
        {
            VoucherRow row = ReadFormToRow();
            row.Kind = VoucherKind.Expense;
            row.IsAdvance = false;
            row.PayrollId = _salaryPayoutPreset.PayrollId;
            row.ObjectType = PhieuThuRules.Employee;
            row.EmployeeId = _salaryPayoutPreset.EmployeeId;
            row.ObjectName = !string.IsNullOrWhiteSpace(_salaryPayoutPreset.EmployeeName)
                ? _salaryPayoutPreset.EmployeeName
                : (_partyName.Text ?? "").Trim();
            row.CustomerId = null;
            row.SupplierId = null;
            row.NoDebtChange = false;

            VoucherSaveResult res;
            try
            {
                res = _salaryPayoutService.Save(row);
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                if (res.ErrorMessage == VoucherConstants.BankAccountMissing && _bank.Enabled)
                {
                    _bank.Focus();
                    if (!SuppressPopup)
                        _bank.DroppedDown = true;
                }
                else if (res.ErrorMessage != null && res.ErrorMessage.IndexOf("Ngày phải nằm trong tháng", StringComparison.OrdinalIgnoreCase) >= 0 && _date.Enabled)
                {
                    _date.Focus();
                }
                else if (res.ErrorMessage != null && res.ErrorMessage.IndexOf("Số phiếu", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _number.Focus();
                    _number.SelectAll();
                }
                else if (res.ErrorMessage == VoucherConstants.AmountMustBePositive && _amount != null)
                {
                    _amount.Focus();
                }
                return;
            }

            LastWarningForTest = null;
            SavedId = res.SavedId;
            SavedNumber = res.SavedNumber;
            AnySaved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
