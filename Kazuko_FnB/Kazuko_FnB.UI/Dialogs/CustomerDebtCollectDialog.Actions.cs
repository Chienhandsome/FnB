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
    /// <summary>Phần nạp danh mục, phân bổ tự động, sự kiện lưới, lưu phiếu thu công nợ và theme cho <see cref="CustomerDebtCollectDialog"/>.</summary>
    internal sealed partial class CustomerDebtCollectDialog
    {
        private void LoadLookups()
        {
            _syncing = true;
            try
            {
                try { _txtNumber.Text = _service.GenerateDebtReceiptNumber(); }
                catch (DataAccessException) { _txtNumber.Text = ""; }

                _dtDate.Value = DateTime.Today;

                IList<LookupItem> stores;
                try
                {
                    stores = _session.IsAdmin
                        ? _formRepo.LoadAll("DCUAHANG")
                        : _formRepo.LoadStoresOfUser(_session.UserId);
                    if (!_session.IsAdmin && stores.Count == 0)
                    {
                        IList<LookupItem> all = _formRepo.LoadAll("DCUAHANG");
                        if (all.Count == 1)
                            stores = all;
                    }
                }
                catch (DataAccessException)
                {
                    stores = new List<LookupItem>();
                }
                _store.SetItems(stores);
                if (_session.CurrentStore != null)
                    _store.SelectId(_session.CurrentStore.Id);
                if (_store.SelectedIndex < 0 && _store.Items.Count > 0)
                    _store.SelectedIndex = 0;

                IList<LookupItem> reasons;
                try { reasons = _formRepo.LoadIncomeReasons(); }
                catch (DataAccessException) { reasons = new List<LookupItem>(); }
                _reason.SetItems(reasons);
                try
                {
                    string defReason = _service.FindDefaultDebtReceiptReasonId();
                    if (!string.IsNullOrEmpty(defReason))
                        _reason.SelectId(defReason);
                }
                catch (DataAccessException) { }

                IList<LookupItem> banks;
                try { banks = _formRepo.LoadAll("DTAIKHOANNGANHANG"); }
                catch (DataAccessException) { banks = new List<LookupItem>(); }
                _bank.SetItems(banks);

                _chkTransfer.Checked = false;
                _bank.SelectId(null);
                _txtDescription.Text = DebtConstants.ClipText("Thu công nợ khách hàng " + (_customer.Name ?? "").Trim());
                _txtNote.Text = "";
            }
            finally
            {
                _syncing = false;
            }
        }

        private void LoadOpenBills()
        {
            _syncing = true;
            try
            {
                List<DebtDocumentRow> openBills;
                try { openBills = _service.LoadOpenCustomerBills(_customer.Id); }
                catch (DataAccessException) { openBills = new List<DebtDocumentRow>(); }

                _allocations = new List<DebtAllocationLine>(openBills.Count);
                decimal sumOpen = 0m;
                foreach (DebtDocumentRow b in openBills)
                {
                    _allocations.Add(new DebtAllocationLine
                    {
                        BillId = b.Id,
                        BillNumber = b.Number,
                        BillDate = b.Date,
                        BillTotal = b.TotalAmount,
                        BillRemaining = b.RemainingDebt,
                        PayAmount = 0m,
                        Note = ""
                    });
                    sumOpen += b.RemainingDebt;
                }

                decimal defaultAmount = _customer.RemainingDebt > 0m
                    ? Math.Min(_customer.RemainingDebt, sumOpen)
                    : sumOpen;
                if (defaultAmount < 0m)
                    defaultAmount = 0m;

                decimal allocated = DebtRules.AllocateOldestFirst(_allocations, defaultAmount);
                SetAmountValue(allocated);

                _billGrid.Rows.Clear();
                foreach (DebtAllocationLine line in _allocations)
                {
                    int idx = _billGrid.Rows.Add(
                        line.BillNumber, line.BillDate, line.BillTotal, line.BillRemaining, line.PayAmount);
                    _billGrid.Rows[idx].Tag = line;
                }

                _emptyBills.Visible = _allocations.Count == 0;
                UpdateAllocSummary();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void ApplyModeState()
        {
            bool editable = !_readOnly && _allocations.Count > 0;
            _txtNumber.ReadOnly = _readOnly;
            _dtDate.Enabled = !_readOnly && _service.CanChangeVoucherDate;
            _store.Enabled = !_readOnly;
            _reason.Enabled = !_readOnly;
            _numAmount.ReadOnly = !editable;
            _numAmount.Enabled = editable;
            _btnAutoAlloc.Enabled = editable;
            _chkTransfer.Enabled = editable;
            _bank.Enabled = editable && _chkTransfer.Checked;
            _txtDescription.ReadOnly = _readOnly;
            _txtNote.ReadOnly = _readOnly;
            _billGrid.ReadOnly = !editable;
            if (_billGrid.Columns["b_pay"] != null)
                _billGrid.Columns["b_pay"].ReadOnly = !editable;

            _btnSave.Visible = !_readOnly;
            _btnSave.Enabled = editable;
        }

        private void ApplyAutoAllocate(decimal targetAmount)
        {
            if (_allocations.Count == 0)
                return;
            _syncing = true;
            try
            {
                DebtRules.AllocateOldestFirst(_allocations, targetAmount);
                for (int i = 0; i < _allocations.Count && i < _billGrid.Rows.Count; i++)
                    _billGrid.Rows[i].Cells["b_pay"].Value = _allocations[i].PayAmount;
                UpdateAllocSummary();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void OnBillCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _allocations.Count || e.ColumnIndex < 0)
                return;
            if (!NumericEditGridCell.IsNumericCell(_billGrid, e.RowIndex, e.ColumnIndex))
                return;
            _cellParseFailed = !NumericEditGridCell.ParseCell(_billGrid, e, 0m);
        }

        private void OnBillCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            LastWarningForTest = null;
            bool parseFailed = _cellParseFailed;
            _cellParseFailed = false;
            if (_syncing || _readOnly || e.RowIndex < 0 || e.RowIndex >= _allocations.Count || e.ColumnIndex < 0)
                return;
            if (_billGrid.Columns[e.ColumnIndex].Name != "b_pay")
                return;

            DebtAllocationLine line = _allocations[e.RowIndex];
            decimal val;
            if (parseFailed || !NumberInput.TryParse(_billGrid.Rows[e.RowIndex].Cells["b_pay"].Value, out val))
            {
                SyncBillRow(e.RowIndex);
                return;
            }
            if (val < 0m)
            {
                SyncBillRow(e.RowIndex);
                ShowWarning(DebtConstants.AllocationNegative);
                return;
            }
            if (val > line.BillRemaining)
            {
                SyncBillRow(e.RowIndex);
                ShowWarning(DebtConstants.AllocationExceedsDebt(line.BillNumber, line.BillRemaining, val));
                return;
            }

            line.PayAmount = val;
            SyncBillRow(e.RowIndex);
            _syncing = true;
            try
            {
                SetAmountValue(DebtRules.SumAllocations(_allocations));
                UpdateAllocSummary();
            }
            finally
            {
                _syncing = false;
            }
        }

        internal void CommitPayCellForTest(int rowIndex, object newValue)
        {
            if (rowIndex < 0 || rowIndex >= _billGrid.Rows.Count)
                return;
            DataGridViewColumn col = _billGrid.Columns["b_pay"];
            if (col == null)
                return;
            _billGrid.Rows[rowIndex].Cells["b_pay"].Value = newValue;
            OnBillCellEndEdit(_billGrid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        private void SyncBillRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _allocations.Count || rowIndex >= _billGrid.Rows.Count)
                return;
            _syncing = true;
            try
            {
                _billGrid.Rows[rowIndex].Cells["b_pay"].Value = _allocations[rowIndex].PayAmount;
            }
            finally
            {
                _syncing = false;
            }
        }

        private void SetAmountValue(decimal val)
        {
            if (val < _numAmount.Minimum) val = _numAmount.Minimum;
            if (val > _numAmount.Maximum) val = _numAmount.Maximum;
            _numAmount.Value = val;
        }

        private void UpdateAllocSummary()
        {
            decimal totalPay = DebtRules.SumAllocations(_allocations);
            decimal totalRem = 0m;
            int selectedBills = 0;
            foreach (DebtAllocationLine l in _allocations)
            {
                totalRem += l.BillRemaining;
                if (l.PayAmount > 0m)
                    selectedBills++;
            }
            _lblAllocSummary.Text = string.Format(
                "Phân bổ: {0} đơn — Tổng thu lần này: {1} / Tổng còn nợ các đơn: {2}",
                selectedBills,
                DebtConstants.FormatMoneyVi(totalPay),
                DebtConstants.FormatMoneyVi(totalRem));
        }

        private void Save()
        {
            if (_readOnly)
                return;
            if (_billGrid.IsCurrentCellInEditMode)
                _billGrid.EndEdit();

            var req = new DebtCollectRequest
            {
                CustomerId = _customer.Id,
                CustomerName = _customer.Name,
                CustomerAddress = _customer.Address,
                VoucherNumber = (_txtNumber.Text ?? "").Trim(),
                Date = _dtDate.Value.Date,
                StoreId = _store.SelectedId,
                ReasonId = _reason.SelectedId,
                Transfer = _chkTransfer.Checked,
                BankAccountId = _bank.SelectedId,
                Description = (_txtDescription.Text ?? "").Trim(),
                Note = (_txtNote.Text ?? "").Trim(),
                Allocations = new List<DebtAllocationLine>(_allocations)
            };

            DebtCollectResult res;
            try
            {
                res = _service.CollectCustomerDebt(req);
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được phiếu thu công nợ: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                if (res.ErrorMessage == VoucherConstants.BankAccountMissing && _bank.Enabled)
                    _bank.Focus();
                else if (res.ErrorMessage != null && res.ErrorMessage.IndexOf("Số phiếu", StringComparison.OrdinalIgnoreCase) >= 0)
                    _txtNumber.Focus();
                return;
            }

            LastWarningForTest = null;
            SavedOk = true;
            SavedVoucherId = res.SavedVoucherId;
            SavedVoucherNumber = res.SavedVoucherNumber;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly && keyData == (Keys.Control | Keys.L) && _btnSave.Visible && _btnSave.Enabled)
            {
                Save();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DrawBillRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_billGrid.RowHeadersVisible || e.RowIndex < 0)
                return;
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _billGrid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture),
                _billGrid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
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

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyGrid(_billGrid, t);
            _lblCurrentDebt.ForeColor = t.Primary;
            _lblAllocSummary.ForeColor = t.Text;
            _emptyBills.BackColor = _billGrid.BackgroundColor;
            _emptyBills.ForeColor = t.TextMuted;
            if (_billGrid.Columns["b_pay"] != null && !_readOnly)
                _billGrid.Columns["b_pay"].DefaultCellStyle.BackColor = Draw.Mix(t.Surface, t.Primary, 0.08f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
