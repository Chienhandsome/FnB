using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Chế độ Tạm ứng lương (LATAMUNG = 30, LOAI = 1, CHUYENKHOAN = 0) của <see cref="PhieuThuDialog"/>
    /// (spec docs/specs/nhan-su-thuong-phat-tam-ung.md §3).
    /// Tách partial riêng để không làm thay đổi hành vi Danh mục phiếu chi và giữ gọn các file gốc.
    /// </summary>
    internal sealed partial class PhieuThuDialog
    {
        private bool _isSalaryAdvanceMode;
        private SalaryAdvanceService _salaryAdvanceService;
        private RewardPenaltyMonthLimit _advanceMonthLimit;

        internal bool IsSalaryAdvanceModeForTest { get { return _isSalaryAdvanceMode; } }

        /// <summary>
        /// Mở hộp thoại Tạo / Sửa / Xem Tạm ứng lương dùng chung layout Phiếu chi:
        /// khóa Loại đối tượng = Nhân viên, ẩn ô chuyển khoản &amp; tài khoản ngân hàng,
        /// mặc định lý do của phiếu LATAMUNG = 30 gần nhất và diễn giải "Tạm ứng lương".
        /// </summary>
        public static PhieuThuDialog CreateForSalaryAdvance(
            UserSession session,
            string voucherId = null,
            bool readOnly = false,
            int? limitMonth = null,
            int? limitYear = null)
        {
            var limit = (limitMonth.HasValue && limitYear.HasValue)
                ? new RewardPenaltyMonthLimit(limitMonth.Value, limitYear.Value)
                : null;
            return new PhieuThuDialog(session, VoucherKind.Expense, voucherId, readOnly, false, true, limit);
        }

        private static string BuildAdvanceTitle(bool isNew, bool readOnly)
        {
            if (readOnly)
                return "TẠM ỨNG LƯƠNG — XEM CHI TIẾT";
            return isNew ? "TẠM ỨNG LƯƠNG" : "TẠM ỨNG LƯƠNG — SỬA";
        }

        private static string BuildAdvanceSubtitle(bool readOnly)
        {
            if (readOnly)
                return "Chế độ chỉ xem";
            return "Nhập thông tin tạm ứng lương tiền mặt (Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Đóng).";
        }

        private void ConfigureSalaryAdvanceLayout()
        {
            _transfer.Checked = false;
            _transfer.Visible = false;
            _transfer.Enabled = false;
            Label transferLbl = _form.LabelOf("chkCHUYENKHOAN");
            if (transferLbl != null)
                transferLbl.Visible = false;

            _bank.SelectId(null);
            _bank.Visible = false;
            _bank.Enabled = false;
            Label bankLbl = _form.LabelOf("lueDTAIKHOANNGANHANGID");
            if (bankLbl != null)
                bankLbl.Visible = false;

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
                _amount.Maximum = SalaryAdvanceText.MaxAmount;
                _amount.DecimalPlaces = 2;
            }
        }

        private void InitNewSalaryAdvanceDocument()
        {
            _loading = true;
            try
            {
                _editingId = null;
                _isNew = true;
                _linkedWarning = null;
                SetTitle(BuildAdvanceTitle(true, _readOnly));

                try
                {
                    _number.Text = _salaryAdvanceService.GenerateNumber();
                }
                catch (DataAccessException)
                {
                    _number.Text = "";
                }
                _number.ReadOnly = _readOnly;

                if (PhieuThuRules.Employee < _objectType.Items.Count)
                    _objectType.SelectedIndex = PhieuThuRules.Employee;
                _employee.SelectId(null);
                _customer.SelectId(null);
                _supplier.SelectId(null);
                _partyName.Text = "";
                _partyAddress.Text = "";

                string latestReasonId = null;
                try { latestReasonId = _salaryAdvanceService.LoadLatestReasonId(); }
                catch (DataAccessException) { latestReasonId = null; }
                _reason.SelectId(latestReasonId);

                SetAmount(0m);
                _transfer.Checked = false;
                _bank.SelectId(null);
                SetOptionalText(_description, SalaryAdvanceText.DefaultDescription);
                SetOptionalText(_originalDoc, "");
                SetOptionalText(_note, "");
                if (_noDebtChange != null)
                    _noDebtChange.Checked = false;

                if (_session.CurrentStore != null)
                    _store.SelectId(_session.CurrentStore.Id);
                else if (_store.Items.Count > 0)
                    _store.SelectedIndex = 0;

                _date.Value = DateTime.Today;
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
            ApplyModeState();
        }

        private void LoadExistingSalaryAdvance(string voucherId)
        {
            VoucherRow row = null;
            try
            {
                row = _salaryAdvanceService.LoadById(voucherId);
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
                SetTitle(BuildAdvanceTitle(false, _readOnly));
                _number.Text = row.Name ?? "";
                _date.Value = row.Date;
                if (PhieuThuRules.Employee < _objectType.Items.Count)
                    _objectType.SelectedIndex = PhieuThuRules.Employee;
                _employee.SelectId(row.EmployeeId);
                _customer.SelectId(null);
                _supplier.SelectId(null);
                _partyName.Text = SalaryAdvanceRules.DisplayEmployee(row);
                _partyAddress.Text = "";
                _store.SelectId(row.StoreId);
                _reason.SelectId(row.ReasonId);
                SetAmount(row.Amount);
                _transfer.Checked = false;
                _bank.SelectId(null);
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

        private void ApplySalaryAdvanceModeState()
        {
            _objectType.Enabled = false;
            _customer.Enabled = false;
            _supplier.Enabled = false;
            _partyName.Enabled = false;
            _partyAddress.Enabled = false;
            _employee.Enabled = !_readOnly;
            _transfer.Checked = false;
            _transfer.Visible = false;
            _transfer.Enabled = false;
            _bank.Visible = false;
            _bank.Enabled = false;
            if (!_readOnly)
                _date.Enabled = _salaryAdvanceService.CanChangeDate;
            SubtitleText = BuildAdvanceSubtitle(_readOnly);
        }

        private void SaveSalaryAdvanceMode(bool saveAndNew)
        {
            VoucherRow row = ReadFormToRow();
            row.Kind = VoucherKind.Expense;
            row.IsAdvance = true;
            row.ObjectType = PhieuThuRules.Employee;
            row.CustomerId = null;
            row.SupplierId = null;
            row.Transfer = false;
            row.BankAccountId = null;
            row.NoDebtChange = false;

            VoucherSaveResult res;
            try
            {
                res = _salaryAdvanceService.Save(row, _advanceMonthLimit);
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                if (res.ErrorMessage == SalaryAdvanceText.EmployeeRequired && _employee.Enabled)
                    _employee.Focus();
                else if (res.ErrorMessage != null && res.ErrorMessage.IndexOf("Số phiếu", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _number.Focus();
                    _number.SelectAll();
                }
                else if (res.ErrorMessage == VoucherConstants.AmountMustBePositive && _amount != null)
                    _amount.Focus();
                return;
            }

            LastWarningForTest = null;
            SavedId = res.SavedId;
            SavedNumber = res.SavedNumber;
            AnySaved = true;

            if (saveAndNew)
            {
                InitNewSalaryAdvanceDocument();
                _number.Focus();
                _number.SelectAll();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
