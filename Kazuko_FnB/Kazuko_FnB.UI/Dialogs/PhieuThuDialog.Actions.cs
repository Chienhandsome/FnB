using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phần khởi tạo phiếu mới, nạp phiếu cũ, lưu phiếu thu / chi thật vào TTHUCHI (hoặc cọc đặt hàng),
    /// phím tắt Ctrl+L / Ctrl+S và hook kiểm thử cho <see cref="PhieuThuDialog"/>.
    /// </summary>
    internal sealed partial class PhieuThuDialog
    {
        private void InitNewDocument()
        {
            _loading = true;
            try
            {
                _editingId = null;
                _isNew = true;
                _linkedWarning = null;
                SetTitle(BuildTitle(_kind, true, _readOnly));

                if (!_isDepositMode)
                {
                    try
                    {
                        _number.Text = _voucherService.GenerateNumber(_kind);
                    }
                    catch (DataAccessException)
                    {
                        _number.Text = "";
                    }
                    _number.ReadOnly = _readOnly;
                }

                _objectType.SelectedIndex = PhieuThuRules.OtherObject;
                _employee.SelectId(null);
                _customer.SelectId(null);
                _supplier.SelectId(null);
                _partyName.Text = "";
                _partyAddress.Text = "";
                _reason.SelectId(null);
                SetAmount(0m);
                _transfer.Checked = false;
                _bank.SelectId(null);
                SetOptionalText(_description, "");
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

        private void LoadExistingVoucher(string voucherId)
        {
            VoucherRow row = null;
            try
            {
                row = _voucherService.LoadById(voucherId);
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

            string linkedErr = VoucherRules.GetLinkedBlockMessage(row);
            if (linkedErr != null)
            {
                _readOnly = true;
                _linkedWarning = linkedErr;
            }

            _loading = true;
            try
            {
                SetTitle(BuildTitle(_kind, false, _readOnly));
                _number.Text = row.Name ?? "";
                _date.Value = row.Date;
                int objIdx = row.ObjectType >= 0 && row.ObjectType < _objectType.Items.Count
                    ? row.ObjectType : PhieuThuRules.OtherObject;
                _objectType.SelectedIndex = objIdx;
                _employee.SelectId(row.EmployeeId);
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
                    _noDebtChange.Checked = row.NoDebtChange;
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
        }

        private void InitDepositMode(PhieuThuDepositPreset preset)
        {
            BookingDeposit existing = null;
            if (!string.IsNullOrEmpty(preset.ExistingReceiptId))
            {
                try { existing = _phieuThuRepo.LoadReceipt(preset.ExistingReceiptId); }
                catch (DataAccessException) { existing = null; }
            }

            _loading = true;
            try
            {
                if (existing != null)
                {
                    _number.Text = existing.Number ?? "";
                    _number.ReadOnly = true;
                    _objectType.SelectedIndex = string.IsNullOrEmpty(existing.CustomerId)
                        ? PhieuThuRules.OtherObject : PhieuThuRules.Customer;
                    _customer.SelectId(existing.CustomerId);
                    _partyName.Text = existing.CustomerName ?? "";
                    _partyAddress.Text = existing.Address ?? "";
                    _employee.SelectId(existing.EmployeeId);
                    _store.SelectId(existing.StoreId);
                    _reason.SelectId(existing.ReasonId);
                    SetAmount(existing.Amount);
                    _date.Value = existing.Date;
                    _transfer.Checked = existing.Transfer;
                    _bank.SelectId(existing.BankAccountId);
                    SetOptionalText(_description, existing.Note);
                }
                else
                {
                    string number;
                    try { number = _phieuThuRepo.GenerateNumber(); }
                    catch (DataAccessException) { number = ""; }
                    _number.Text = number;
                    _number.ReadOnly = true;
                    _objectType.SelectedIndex = string.IsNullOrEmpty(preset.CustomerId)
                        ? PhieuThuRules.OtherObject : PhieuThuRules.Customer;
                    _customer.SelectId(preset.CustomerId);
                    _partyName.Text = preset.CustomerName ?? "";
                    _partyAddress.Text = preset.Address ?? "";
                    string reasonId = null;
                    try { reasonId = _phieuThuRepo.LoadLatestDepositReasonId(); }
                    catch (DataAccessException) { reasonId = null; }
                    _reason.SelectId(reasonId);
                    SetAmount(preset.Amount);
                    SetOptionalText(_description, BookingText.DepositNote(preset.BookingNumber));
                }
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
        }

        private void ApplyModeState()
        {
            if (_readOnly)
            {
                foreach (string name in _form.EditorNames)
                {
                    Control c = _form.Editor(name);
                    if (c != null)
                        c.Enabled = false;
                }
            }
            else
            {
                bool canChangeDate = _voucherService.CanChangeDate;
                _date.Enabled = canChangeDate;
                _bank.Enabled = PhieuThuRules.BankAccountEnabled(_transfer.Checked);
            }

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && !_isDepositMode && !_isSalaryPayoutMode && !_isPrepaidCardMode;
            if (_isPrepaidCardMode)
                ApplyPrepaidCardModeState();
            else if (_isSalaryPayoutMode)
                ApplySalaryPayoutModeState();
            else if (_isSalaryAdvanceMode)
                ApplySalaryAdvanceModeState();
            else if (!string.IsNullOrEmpty(_linkedWarning))
                SubtitleText = _linkedWarning;
            else if (!_isDepositMode)
                SubtitleText = BuildSubtitle(_kind, _readOnly);

            _form.ApplyTheme(ThemeManager.Current);
        }

        // ------------------------------------------------------------------ Phím tắt & Lưu

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L) && _btnSave.Visible && _btnSave.Enabled)
                {
                    Save(false);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S) && _btnSaveNew.Visible && _btnSaveNew.Enabled)
                {
                    Save(true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        internal bool DispatchKeyForTest(Keys keyData)
        {
            var m = new Message();
            return ProcessCmdKey(ref m, keyData);
        }

        private void Save(bool saveAndNew)
        {
            if (_readOnly)
                return;

            if (_isDepositMode)
            {
                SaveDepositMode();
                return;
            }
            if (_isPrepaidCardMode)
            {
                SavePrepaidCardMode();
                return;
            }
            if (_isSalaryPayoutMode)
            {
                SaveSalaryPayoutMode();
                return;
            }
            if (_isSalaryAdvanceMode)
            {
                SaveSalaryAdvanceMode(saveAndNew);
                return;
            }

            VoucherRow row = ReadFormToRow();
            VoucherSaveResult res;
            try
            {
                res = _voucherService.Save(row);
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

            if (saveAndNew)
            {
                InitNewDocument();
                _number.Focus();
                _number.SelectAll();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private VoucherRow ReadFormToRow()
        {
            return new VoucherRow
            {
                Id = _editingId,
                Kind = _kind,
                Name = (_number.Text ?? "").Trim(),
                Date = _date.Value.Date,
                ObjectType = _objectType.SelectedIndex < 0 ? 0 : _objectType.SelectedIndex,
                EmployeeId = _employee.SelectedId,
                CustomerId = _customer.SelectedId,
                SupplierId = _supplier.SelectedId,
                ObjectName = (_partyName.Text ?? "").Trim(),
                Address = (_partyAddress.Text ?? "").Trim(),
                StoreId = _store.SelectedId,
                ReasonId = _reason.SelectedId,
                Amount = GetAmount(),
                Transfer = _transfer.Checked,
                BankAccountId = _bank.SelectedId,
                Description = GetOptionalText(_description),
                OriginalDoc = GetOptionalText(_originalDoc),
                Note = GetOptionalText(_note),
                NoDebtChange = _noDebtChange != null && _noDebtChange.Checked
            };
        }

        /// <summary>Chế độ cọc (§3.5 Khách đặt hàng): ghi phiếu thu cọc gắn TDATHANGID qua PhieuThuRepository.</summary>
        private void SaveDepositMode()
        {
            var missing = new List<string>();
            foreach (string name in _form.EditorNames)
            {
                FieldMap map = _form.MapOf(name);
                Control c = _form.Editor(name);
                if (map == null || !map.Required || !c.Enabled)
                    continue;
                if (IsEmpty(c))
                {
                    LayoutControl lbl = map.LabelControl == null ? null : _layout.Find(map.LabelControl);
                    missing.Add(lbl != null ? lbl.Text.Trim() : map.DataField);
                }
            }

            string error = PhieuThuRules.Validate(missing, _transfer.Checked, _bank.SelectedId != null);
            if (error != null)
            {
                ShowWarning(error);
                if (error == PhieuThuRules.BankAccountMissing && !SuppressPopup)
                    _bank.DroppedDown = true;
                return;
            }

            BookingDeposit existing = null;
            if (!string.IsNullOrEmpty(_depositPreset.ExistingReceiptId))
            {
                try { existing = _phieuThuRepo.LoadReceipt(_depositPreset.ExistingReceiptId); }
                catch (DataAccessException) { existing = null; }
            }

            var dep = new BookingDeposit
            {
                Id = existing == null ? null : existing.Id,
                BookingId = _depositPreset.BookingId,
                Number = (_number.Text ?? "").Trim(),
                Date = _date.Value.Date,
                ObjectType = _objectType.SelectedIndex < 0 ? 0 : _objectType.SelectedIndex,
                CustomerId = _objectType.SelectedIndex == PhieuThuRules.Customer ? _customer.SelectedId : null,
                CustomerName = (_partyName.Text ?? "").Trim(),
                Address = (_partyAddress.Text ?? "").Trim(),
                EmployeeId = _employee.SelectedId,
                StoreId = _store.SelectedId,
                ReasonId = _reason.SelectedId,
                Note = GetOptionalText(_description),
                Amount = GetAmount(),
                Transfer = _transfer.Checked,
                BankAccountId = _bank.SelectedId
            };

            string savedId;
            try
            {
                savedId = _phieuThuRepo.SaveDeposit(dep, _session.UserId);
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            if (savedId == null)
            {
                ShowWarning(VoucherConstants.NumberDuplicate(dep.Number));
                _number.Focus();
                _number.SelectAll();
                return;
            }

            SavedId = savedId;
            SavedNumber = dep.Number;
            AnySaved = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ------------------------------------------------------------------ Helper ô nhập & Preview

        private void SetAmount(decimal amount)
        {
            if (_amount == null)
                return;
            if (amount < _amount.Minimum)
                amount = _amount.Minimum;
            if (amount > _amount.Maximum)
                amount = _amount.Maximum;
            _amount.Value = amount;
        }

        private decimal GetAmount()
        {
            return _amount == null ? 0m : _amount.Value;
        }

        private static void SetOptionalText(TextBox tb, string value)
        {
            if (tb != null)
                tb.Text = value ?? "";
        }

        private static string GetOptionalText(TextBox tb)
        {
            return tb == null ? "" : (tb.Text ?? "").Trim();
        }

        private static bool IsEmpty(Control c)
        {
            var lc = c as LookupCombo;
            if (lc != null)
                return lc.SelectedId == null;
            var cb = c as ComboBox;
            if (cb != null)
                return cb.SelectedIndex < 0;
            var tb = c as TextBox;
            if (tb != null)
                return tb.Text.Trim().Length == 0;
            return false;
        }

        private void ShowWarning(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ShowError(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Error(this, message);
        }

        /// <summary>--render-screen: "type=N" chọn loại đối tượng; "transfer" tích chuyển khoản; "customer" chọn khách đầu tiên.</summary>
        public void Preview(string option)
        {
            if (option == null)
                return;
            if (option.StartsWith("type=", StringComparison.Ordinal))
            {
                int n;
                if (int.TryParse(option.Substring(5), out n))
                    _objectType.SelectedIndex = n;
            }
            else if (option == "transfer")
                _transfer.Checked = true;
            else if (option == "customer" && _customer.Items.Count > 0)
                _customer.SelectedIndex = 0;
        }
    }
}
