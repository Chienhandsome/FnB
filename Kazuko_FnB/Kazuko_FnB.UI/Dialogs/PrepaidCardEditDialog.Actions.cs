using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần nạp dữ liệu, lưu thẻ, Nạp tiền, Chi trả tiền, Sửa/Xóa phiếu trong tab lịch sử cho <see cref="PrepaidCardEditDialog"/>.</summary>
    internal sealed partial class PrepaidCardEditDialog
    {
        private void LoadLookups()
        {
            try { _group.SetItems(_formRepo.LoadAll(PrepaidCardText.GroupTableName)); }
            catch (Exception) { _group.SetItems(new List<LookupItem>()); }
        }

        private void InitNewRecord(string presetGroupId)
        {
            _isNew = true;
            _readOnly = _explicitReadOnly || !_service.CanAdd;
            _row = PrepaidCardRow.NewDefault();
            _row.GroupId = string.IsNullOrEmpty(presetGroupId) ? null : presetGroupId;
            _vouchers = new List<PrepaidCardVoucherItem>();
            _usages = new List<PrepaidCardUsageItem>();
            SetTitle("THẺ TRẢ TRƯỚC - THÊM MỚI");
            BindRowToControls();
            BindHistoryGrids();
        }

        private void LoadExistingRecord(string cardId)
        {
            _isNew = false;
            _row = _service.LoadById(cardId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = PrepaidCardRow.NewDefault();
                _vouchers = new List<PrepaidCardVoucherItem>();
                _usages = new List<PrepaidCardUsageItem>();
                return;
            }
            _readOnly = _explicitReadOnly || !_service.CanEdit;
            SetTitle(_readOnly ? "THẺ TRẢ TRƯỚC - XEM CHI TIẾT" : "THẺ TRẢ TRƯỚC - SỬA");
            BindRowToControls();
            ReloadBalanceAndHistory();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _code.Text = _row.Code ?? "";
                _group.SelectId(_row.GroupId);
                _expiryDate.NullableValue = _row.ExpiryDate;
                _chkLocked.Checked = _row.Locked;
                _note.Text = _row.Note ?? "";
                UpdateSummaryBoxes();
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateSummaryBoxes()
        {
            _txtTotalLoaded.Text = PrepaidCardRules.FormatMoney(_row == null ? 0m : _row.TotalLoaded);
            _txtTotalUsed.Text = PrepaidCardRules.FormatMoney(_row == null ? 0m : _row.TotalUsed);
            _txtRemaining.Text = PrepaidCardRules.FormatMoney(_row == null ? 0m : _row.Remaining);
        }

        internal void ReloadBalanceAndHistory()
        {
            if (_row == null || string.IsNullOrEmpty(_row.Id))
            {
                _vouchers = new List<PrepaidCardVoucherItem>();
                _usages = new List<PrepaidCardUsageItem>();
                UpdateSummaryBoxes();
                BindHistoryGrids();
                UpdateTransactionButtons();
                return;
            }

            PrepaidCardRow fresh = _service.LoadById(_row.Id);
            if (fresh != null)
            {
                _row.TotalLoaded = fresh.TotalLoaded;
                _row.TotalUsed = fresh.TotalUsed;
                _row.Holders = fresh.Holders;
                _row.HolderNames = fresh.HolderNames;
            }
            _vouchers = _service.LoadVoucherHistory(_row.Id);
            _usages = _service.LoadUsageHistory(_row.Id);
            UpdateSummaryBoxes();
            BindHistoryGrids();
            UpdateTransactionButtons();
        }

        private void BindHistoryGrids()
        {
            _voucherGrid.Rows.Clear();
            foreach (PrepaidCardVoucherItem v in _vouchers)
            {
                int idx = _voucherGrid.Rows.Add(v.Number, v.Date, v.IncomeAmount, v.ExpenseAmount);
                _voucherGrid.Rows[idx].Tag = v;
            }
            if (_voucherGrid.Rows.Count > 0)
            {
                _voucherGrid.CurrentCell = _voucherGrid.Rows[0].Cells[0];
                _voucherGrid.Rows[0].Selected = true;
            }

            _usageGrid.Rows.Clear();
            foreach (PrepaidCardUsageItem u in _usages)
            {
                int idx = _usageGrid.Rows.Add(u.Number, u.Date, u.UsedAmount);
                _usageGrid.Rows[idx].Tag = u;
            }
        }

        private PrepaidCardVoucherItem SelectedVoucherItem()
        {
            return _voucherGrid.CurrentRow == null ? null : _voucherGrid.CurrentRow.Tag as PrepaidCardVoucherItem;
        }

        internal bool SelectVoucherByIdForTest(string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId))
                return false;
            foreach (DataGridViewRow r in _voucherGrid.Rows)
            {
                var item = r.Tag as PrepaidCardVoucherItem;
                if (item != null && string.Equals(item.Id, voucherId, StringComparison.Ordinal))
                {
                    _voucherGrid.ClearSelection();
                    _voucherGrid.CurrentCell = r.Cells[0];
                    r.Selected = true;
                    UpdateTransactionButtons();
                    return true;
                }
            }
            return false;
        }

        private void ApplyPermissions()
        {
            _code.ReadOnly = _readOnly;
            _group.Enabled = !_readOnly;
            _expiryDate.Enabled = !_readOnly;
            _expiryDate.Inner.ReadOnly = _readOnly;
            _chkLocked.Enabled = !_readOnly;
            _note.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        internal void UpdateTransactionButtons()
        {
            bool saved = !_isNew && _row != null && !string.IsNullOrEmpty(_row.Id);
            bool hasVoucher = SelectedVoucherItem() != null;
            _btnTopUp.Enabled = !_explicitReadOnly && saved && _service.CanTopUp;
            _btnWithdraw.Enabled = !_explicitReadOnly && saved && _service.CanWithdraw;
            _btnEditVoucher.Enabled = !_explicitReadOnly && saved && _service.CanEdit && hasVoucher;
            _btnDeleteVoucher.Enabled = !_explicitReadOnly && saved && _service.CanRemove && hasVoucher;
        }

        private void HookChangeEvents()
        {
            EventHandler onChanged = delegate
            {
                if (_loading)
                    return;
                ClearError();
                UpdateButtons();
            };
            _code.TextChanged += onChanged;
            _group.SelectedIndexChanged += onChanged;
            _expiryDate.ValueChanged += onChanged;
            _chkLocked.CheckedChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_code.Text).Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(_expiryDate.NullableValue.HasValue ? _expiryDate.NullableValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "").Append('|');
            sb.Append(_chkLocked.Checked ? "1" : "0").Append('|');
            sb.Append(_note.Text);
            return sb.ToString();
        }

        private bool Dirty { get { return !_readOnly && Snapshot() != _original; } }

        private void UpdateButtons()
        {
            if (_readOnly)
                return;
            bool canSave = Dirty || _isNew;
            _btnSave.Enabled = canSave;
            _btnSaveNew.Enabled = canSave && _service.CanAdd;
            _btnSaveClose.Enabled = canSave;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    if (_btnSave.Enabled) SaveStay();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    if (_btnSaveNew.Visible && _btnSaveNew.Enabled) SaveAndNew();
                    return true;
                }
                if (keyData == Keys.Enter && !_voucherGrid.Focused && !_usageGrid.Focused)
                {
                    SaveAndClose();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            InitNewRecord(_presetGroupId);
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            UpdateTransactionButtons();
            _code.Focus();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        internal bool DoSave()
        {
            if (_readOnly)
                return false;

            _expiryDate.Commit();
            _row.Code = _code.Text ?? "";
            _row.GroupId = _group.SelectedId;
            _row.ExpiryDate = _expiryDate.NullableValue;
            _row.Locked = _chkLocked.Checked;
            _row.Note = _note.Text ?? "";

            PrepaidCardSaveResult res;
            try { res = _service.Save(_row); }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                if (res.HasCodeError && !_code.ReadOnly) { _code.Focus(); _code.SelectAll(); }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("THẺ TRẢ TRƯỚC - SỬA");
            _original = Snapshot();
            UpdateButtons();
            ReloadBalanceAndHistory();
            return true;
        }

        // ------------------------------------------------------------------ Nạp tiền, Chi trả tiền, Sửa/Xóa phiếu

        private void OpenTopUpVoucher()
        {
            if (_explicitReadOnly || !_service.CanTopUp)
                return;
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
            {
                ShowValidationError(PrepaidCardText.SaveCardBeforeTransaction);
                return;
            }

            // Cảnh báo xác nhận khi nạp vào thẻ khóa hoặc hết hạn (spec §2, T4).
            _expiryDate.Commit();
            var checkCard = new PrepaidCardRow { Locked = _chkLocked.Checked, ExpiryDate = _expiryDate.NullableValue };
            string warn = PrepaidCardRules.BuildLockedOrExpiredWarning(checkCard, _service.GetServerDate());
            if (warn != null)
            {
                LastConfirmPromptForTest = warn;
                bool ok = ConfirmLockedExpiredOverrideForTest != null
                    ? ConfirmLockedExpiredOverrideForTest(warn)
                    : (!SuppressPopup && Msg.Confirm(this, warn));
                if (!ok)
                    return;
            }

            OpenCardVoucherDialog(VoucherKind.Income, null, false);
        }

        private void OpenWithdrawVoucher()
        {
            if (_explicitReadOnly || !_service.CanWithdraw)
                return;
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
            {
                ShowValidationError(PrepaidCardText.SaveCardBeforeTransaction);
                return;
            }
            OpenCardVoucherDialog(VoucherKind.Expense, null, false);
        }

        private void OpenEditSelectedVoucher()
        {
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
                return;
            PrepaidCardVoucherItem item = SelectedVoucherItem();
            if (item == null)
                return;
            bool viewOnly = _explicitReadOnly || !_service.CanEdit;
            OpenCardVoucherDialog(item.Kind, item.Id, viewOnly);
        }

        private void OpenCardVoucherDialog(VoucherKind kind, string voucherId, bool readOnly)
        {
            PrepaidCardHolderInfo singleHolder = (_row.Holders != null && _row.Holders.Count == 1)
                ? _row.Holders[0]
                : null;
            var preset = new PrepaidCardVoucherPreset
            {
                CardId = _row.Id,
                CardCode = string.IsNullOrWhiteSpace(_code.Text) ? (_row.Code ?? "") : _code.Text.Trim(),
                Kind = kind,
                VoucherId = voucherId,
                ReadOnly = readOnly,
                SingleHolder = singleHolder,
                CurrentRemaining = _row.Remaining
            };

            using (PhieuThuDialog dlg = PhieuThuDialog.CreateForPrepaidCard(_session, preset))
            {
                if (VoucherDialogOpenerForTest != null)
                    VoucherDialogOpenerForTest(dlg);
                else
                    dlg.ShowDialog(this);

                if (dlg.AnySaved)
                {
                    _anyVoucherChanged = true;
                    ReloadBalanceAndHistory();
                }
            }
        }

        private void DeleteSelectedVoucher()
        {
            if (_explicitReadOnly || !_service.CanRemove || _isNew || _row == null || string.IsNullOrEmpty(_row.Id))
                return;
            PrepaidCardVoucherItem item = SelectedVoucherItem();
            if (item == null)
                return;

            string confirmMsg = PrepaidCardText.DeleteVoucherConfirm(item.Number);
            LastConfirmPromptForTest = confirmMsg;
            bool ok = ConfirmDeleteVoucherOverrideForTest != null
                ? ConfirmDeleteVoucherOverrideForTest(confirmMsg)
                : (SuppressPopup || Msg.Confirm(this, confirmMsg));
            if (!ok)
                return;

            VoucherDeleteResult res;
            try { res = _voucherService.Delete(item.Id, _row.Id); }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không xóa được phiếu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                return;
            }

            ClearError();
            _anyVoucherChanged = true;
            ReloadBalanceAndHistory();
        }

        private void ShowValidationError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = (message ?? "").Replace(Environment.NewLine, "  |  ");
            _lblError.Visible = !string.IsNullOrEmpty(message);
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ClearError()
        {
            LastErrorForTest = null;
            _lblError.Text = "";
            _lblError.Visible = false;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_readOnly || DialogResult == DialogResult.OK || !Dirty || SuppressPopup)
                return;
            DialogResult answer = MessageBox.Show(this, "Dữ liệu đã thay đổi. Bạn có muốn lưu lại không?",
                "Xác nhận", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (answer == DialogResult.Yes && !DoSave())
                e.Cancel = true;
        }
    }
}
