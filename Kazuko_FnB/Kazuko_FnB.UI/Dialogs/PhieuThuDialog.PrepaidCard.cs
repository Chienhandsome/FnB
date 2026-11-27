using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Chế độ Nạp tiền (LOAI = 0) và Chi trả tiền (LOAI = 1) gắn thẻ trả trước (DTHETRATRUOCID)
    /// của <see cref="PhieuThuDialog"/> (spec docs/specs/danh-muc-the-tra-truoc.md §3).
    /// Tách partial riêng để giữ nguyên hành vi Phiếu thu/chi thường, Tạm ứng lương và Chi lương.
    /// </summary>
    internal sealed partial class PhieuThuDialog
    {
        private bool _isPrepaidCardMode;
        private PrepaidCardVoucherService _prepaidCardVoucherService;
        private PrepaidCardVoucherPreset _prepaidCardPreset;

        internal bool IsPrepaidCardModeForTest { get { return _isPrepaidCardMode; } }

        /// <summary>Mở hộp thoại Nạp tiền / Chi trả tiền / Sửa / Xem phiếu gắn thẻ trả trước.</summary>
        public static PhieuThuDialog CreateForPrepaidCard(UserSession session, PrepaidCardVoucherPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException("preset");
            return new PhieuThuDialog(
                session,
                preset.Kind,
                preset.VoucherId,
                preset.ReadOnly,
                false,
                false,
                null,
                null,
                preset);
        }

        private static string ResolveDialogTitle(
            VoucherKind kind,
            string voucherId,
            bool readOnly,
            bool salaryAdvanceMode,
            PayrollPayoutPreset salaryPayoutPreset,
            PrepaidCardVoucherPreset prepaidCardPreset)
        {
            bool isNew = string.IsNullOrEmpty(voucherId);
            if (prepaidCardPreset != null)
                return BuildPrepaidCardTitle(prepaidCardPreset.Kind, isNew, readOnly || prepaidCardPreset.ReadOnly);
            if (salaryPayoutPreset != null)
                return BuildSalaryPayoutTitle(isNew, readOnly || salaryPayoutPreset.ReadOnly);
            if (salaryAdvanceMode)
                return BuildAdvanceTitle(isNew, readOnly);
            return BuildTitle(kind, isNew, readOnly);
        }

        private static string ResolveDialogSubtitle(
            VoucherKind kind,
            bool readOnly,
            bool salaryAdvanceMode,
            PayrollPayoutPreset salaryPayoutPreset,
            PrepaidCardVoucherPreset prepaidCardPreset)
        {
            if (prepaidCardPreset != null)
                return BuildPrepaidCardSubtitle(prepaidCardPreset, readOnly || prepaidCardPreset.ReadOnly);
            if (salaryPayoutPreset != null)
                return BuildSalaryPayoutSubtitle(salaryPayoutPreset, readOnly || salaryPayoutPreset.ReadOnly);
            if (salaryAdvanceMode)
                return BuildAdvanceSubtitle(readOnly);
            return BuildSubtitle(kind, readOnly);
        }

        private static string BuildPrepaidCardTitle(VoucherKind kind, bool isNew, bool readOnly)
        {
            string prefix = kind == VoucherKind.Expense ? "CHI TRẢ TIỀN THẺ TRẢ TRƯỚC" : "NẠP TIỀN THẺ TRẢ TRƯỚC";
            if (readOnly)
                return prefix + " — XEM CHI TIẾT";
            return isNew ? prefix : prefix + " — SỬA";
        }

        private static string BuildPrepaidCardSubtitle(PrepaidCardVoucherPreset preset, bool readOnly)
        {
            string code = preset == null ? "" : (preset.CardCode ?? "");
            if (readOnly)
                return "Chế độ chỉ xem phiếu thẻ trả trước " + code;
            return string.Format("Thẻ {0} (còn {1}) — Ctrl+L: Lưu, ESC: Đóng.",
                code, PrepaidCardRules.FormatMoney(preset == null ? 0m : preset.CurrentRemaining));
        }

        private bool ResolvePrepaidCardReadOnly(bool readOnly)
        {
            if (readOnly || (_prepaidCardPreset != null && _prepaidCardPreset.ReadOnly))
                return true;
            if (_isNew)
                return _kind == VoucherKind.Expense
                    ? !_prepaidCardVoucherService.CanWithdraw
                    : !_prepaidCardVoucherService.CanTopUp;
            return !_prepaidCardVoucherService.CanEdit;
        }

        private void ConfigurePrepaidCardLayout()
        {
            if (_noDebtChange != null)
            {
                _noDebtChange.Checked = true;
                _noDebtChange.Visible = false;
                _noDebtChange.Enabled = false;
                Label ndcLbl = _form.LabelOf(_noDebtChange.Name);
                if (ndcLbl != null)
                    ndcLbl.Visible = false;
            }

            if (_amount != null)
            {
                _amount.Minimum = 0m;
                _amount.Maximum = PrepaidCardText.MaxAmount;
                _amount.DecimalPlaces = 2;
            }
        }

        private void InitNewPrepaidCardDocument()
        {
            _loading = true;
            try
            {
                _editingId = null;
                _isNew = true;
                _linkedWarning = null;
                SetTitle(BuildPrepaidCardTitle(_kind, true, _readOnly));

                try { _number.Text = _prepaidCardVoucherService.GenerateNumber(_kind); }
                catch (DataAccessException) { _number.Text = ""; }
                _number.ReadOnly = _readOnly;

                PrepaidCardHolderInfo singleHolder = _prepaidCardPreset.SingleHolder;
                if (singleHolder != null && !string.IsNullOrEmpty(singleHolder.Id) && PhieuThuRules.Customer < _objectType.Items.Count)
                {
                    _objectType.SelectedIndex = PhieuThuRules.Customer;
                    EnsureCustomerInLookup(singleHolder);
                    _customer.SelectId(singleHolder.Id);
                    _employee.SelectId(null);
                    _supplier.SelectId(null);
                    _partyName.Text = singleHolder.Name ?? "";
                    _partyAddress.Text = singleHolder.Address ?? "";
                }
                else
                {
                    _objectType.SelectedIndex = PhieuThuRules.OtherObject;
                    _employee.SelectId(null);
                    _customer.SelectId(null);
                    _supplier.SelectId(null);
                    _partyName.Text = "";
                    _partyAddress.Text = "";
                }

                string latestReasonId = null;
                try { latestReasonId = _prepaidCardVoucherService.LoadLatestReasonId(_kind); }
                catch (DataAccessException) { latestReasonId = null; }
                _reason.SelectId(latestReasonId);

                SetAmount(0m);
                _transfer.Checked = false;
                _bank.SelectId(null);
                string defaultDesc = _kind == VoucherKind.Expense
                    ? PrepaidCardText.DefaultWithdrawDescription(_prepaidCardPreset.CardCode)
                    : PrepaidCardText.DefaultTopUpDescription(_prepaidCardPreset.CardCode);
                SetOptionalText(_description, PrepaidCardRules.ClipLogText(defaultDesc));
                SetOptionalText(_originalDoc, "");
                SetOptionalText(_note, "");
                if (_noDebtChange != null)
                    _noDebtChange.Checked = true;

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

        private void LoadExistingPrepaidCard(string voucherId)
        {
            VoucherRow row = null;
            try { row = _prepaidCardVoucherService.LoadById(voucherId, _prepaidCardPreset.CardId); }
            catch (DataAccessException) { row = null; }

            if (row == null)
            {
                _loadFailed = true;
                return;
            }

            _loading = true;
            try
            {
                SetTitle(BuildPrepaidCardTitle(row.Kind, false, _readOnly));
                _number.Text = row.Name ?? "";
                _date.Value = row.Date;
                int objIdx = row.ObjectType >= 0 && row.ObjectType < _objectType.Items.Count
                    ? row.ObjectType : PhieuThuRules.OtherObject;
                _objectType.SelectedIndex = objIdx;
                _employee.SelectId(row.EmployeeId);
                if (!string.IsNullOrEmpty(row.CustomerId))
                    EnsureCustomerInLookup(new PrepaidCardHolderInfo { Id = row.CustomerId, Name = row.CustomerName ?? row.ObjectName ?? "", Address = row.Address ?? "" });
                _customer.SelectId(row.CustomerId);
                _supplier.SelectId(row.SupplierId);
                _partyName.Text = row.ObjectName ?? "";
                _partyAddress.Text = row.Address ?? "";
                _store.SelectId(row.StoreId);
                _reason.SelectId(row.ReasonId);
                SetAmount(row.Amount);
                _transfer.Checked = row.Transfer;
                _bank.SelectId(row.BankAccountId);
                SetOptionalText(_description, row.Description);
                SetOptionalText(_originalDoc, row.OriginalDoc);
                SetOptionalText(_note, row.Note);
                if (_noDebtChange != null)
                    _noDebtChange.Checked = true;
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
        }

        private void EnsureCustomerInLookup(PrepaidCardHolderInfo holder)
        {
            if (holder == null || string.IsNullOrEmpty(holder.Id))
                return;
            for (int i = 0; i < _customer.Items.Count; i++)
            {
                var item = _customer.Items[i] as LookupItem;
                if (item != null && string.Equals(item.Id, holder.Id, StringComparison.Ordinal))
                    return;
            }
            _customer.Items.Add(new LookupItem { Id = holder.Id, Name = holder.Name ?? "", Address = holder.Address ?? "" });
        }

        private void ApplyPrepaidCardModeState()
        {
            _transfer.Visible = true;
            _transfer.Enabled = !_readOnly;
            _bank.Visible = true;
            _bank.Enabled = !_readOnly && PhieuThuRules.BankAccountEnabled(_transfer.Checked);
            _btnSaveNew.Visible = false;
            if (!_readOnly)
                _date.Enabled = _prepaidCardVoucherService.CanChangeDate;
            SubtitleText = BuildPrepaidCardSubtitle(_prepaidCardPreset, _readOnly);
        }

        private void SavePrepaidCardMode()
        {
            VoucherRow row = ReadFormToRow();
            row.Kind = _kind;
            row.IsAdvance = false;
            row.PayrollId = null;
            row.PrepaidCardId = _prepaidCardPreset.CardId;
            row.NoDebtChange = true;

            VoucherSaveResult res;
            try
            {
                res = _prepaidCardVoucherService.Save(row);
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
                else if (res.ErrorMessage != null && res.ErrorMessage.IndexOf("Số phiếu", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _number.Focus();
                    _number.SelectAll();
                }
                else if (_amount != null && (res.ErrorMessage == VoucherConstants.AmountMustBePositive
                    || res.ErrorMessage == PrepaidCardText.AmountTooLarge
                    || (res.ErrorMessage != null && res.ErrorMessage.IndexOf("số dư thẻ", StringComparison.OrdinalIgnoreCase) >= 0)))
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
