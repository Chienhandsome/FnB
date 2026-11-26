using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    internal sealed partial class PaymentDialog
    {
        private void WireEvents()
        {
            _cboCustomer.ValueChanged += delegate
            {
                if (_updating) return;
                OnCustomerChanged();
            };

            _numVoucher.ValueChanged += delegate { if (!_updating) OnNonCashOrElectronicChanged(); };
            _numPrepaid.ValueChanged += delegate { if (!_updating) OnNonCashOrElectronicChanged(); };
            _numPoints.ValueChanged += delegate { if (!_updating) OnNonCashOrElectronicChanged(); };
            _numCard.ValueChanged += delegate { if (!_updating) OnNonCashOrElectronicChanged(); };
            _numTransfer.ValueChanged += delegate { if (!_updating) OnNonCashOrElectronicChanged(); };
            _cboBank.ValueChanged += delegate { if (!_updating) UpdateChangeAndStatus(); };
            _numCash.ValueChanged += delegate { if (!_updating) UpdateChangeAndStatus(); };

            _chkDebt.CheckedChanged += delegate
            {
                if (_updating) return;
                _updating = true;
                try
                {
                    if (_chkDebt.Checked)
                    {
                        _numCash.Value = 0m;
                        _numCash.Enabled = false;
                    }
                    else
                    {
                        _numCash.Enabled = CurrentNetDue() > 0m;
                        ResetDefaultCashCore();
                    }
                }
                finally { _updating = false; }
                UpdateChangeAndStatus();
            };

            _btnVoucher.Click += delegate { PickVoucher(); };
            _btnPrepaid.Click += delegate { PickPrepaidCard(); };
            _btnPoints.Click += delegate { PickRedeemPoints(); };
        }

        private void OnCustomerChanged()
        {
            string custId = _cboCustomer.SelectedId ?? "";
            _customerState = _service.LookupCustomerPaymentState(custId, _billIds);
            _prepaidCardId = null;
            _prepaidCardBalance = 0m;
            _updating = true;
            try
            {
                _numOldDebt.Value = _customerState.OldDebt;
                _numNetDue.Value = CurrentNetDue();
                _numPoints.Value = 0m;
                _numPrepaid.Value = 0m;
            }
            finally { _updating = false; }
            ApplyCustomerLinkedCardIfAny();
            ResetDefaultCash();
            UpdateChangeAndStatus();
        }

        private void ApplyCustomerLinkedCardIfAny()
        {
            if (!_showPrepaidRow || _customerState == null || _customerState.LinkedPrepaidCard == null)
                return;
            PrepaidCardInfo card = _customerState.LinkedPrepaidCard;
            decimal due = Math.Max(0m, CurrentNetDue());
            _prepaidCardId = card.Id;
            _prepaidCardBalance = card.Balance;
            _updating = true;
            try
            {
                _numPrepaid.Value = Math.Min(card.Balance, due);
            }
            finally { _updating = false; }
        }

        private void OnNonCashOrElectronicChanged()
        {
            if (!_chkDebt.Checked)
                ResetDefaultCash();
            UpdateChangeAndStatus();
        }

        private void ResetDefaultCash()
        {
            _updating = true;
            try
            {
                ResetDefaultCashCore();
            }
            finally { _updating = false; }
        }

        private void ResetDefaultCashCore()
        {
            decimal due = CurrentNetDue();
            if (due <= 0m)
            {
                _numCash.Value = 0m;
                _numCash.Enabled = false;
                return;
            }
            _numCash.Enabled = !_chkDebt.Checked;
            decimal rem = Math.Max(0m, due - _numVoucher.Value - _numPrepaid.Value - _numPoints.Value - _numCard.Value - _numTransfer.Value);
            _numCash.Value = _chkDebt.Checked ? 0m : rem;
        }

        private decimal CurrentNetDue()
        {
            return _ctx.TotalBills + (_customerState != null ? _customerState.OldDebt : 0m) - _ctx.TotalDeposit;
        }

        private void UpdateChangeAndStatus()
        {
            decimal due = CurrentNetDue();
            decimal nonCash = _numVoucher.Value + _numPrepaid.Value + _numPoints.Value;
            FundPaymentResult res = FundRules.SettlePayment(new FundPaymentInput
            {
                Due = due,
                CashTendered = _numCash.Value,
                Transfer = _numTransfer.Value,
                Card = _numCard.Value,
                NonCash = nonCash,
                AccountId = _cboBank.SelectedId
            });

            _updating = true;
            try
            {
                _numNetDue.Value = due;
                _numChange.Value = string.IsNullOrEmpty(res.Error) ? res.Change : 0m;
            }
            finally { _updating = false; }

            _lblStatus.Text = res.Error ?? "";
            _lblStatus.ForeColor = ThemeManager.Current.Danger;
        }

        private void PickVoucher()
        {
            bool fromList = SystemConfig.LuaChonVoucherTuDanhSach;
            List<VoucherInfo> avail = fromList ? _service.LoadAvailableVouchers(_billIds) : null;
            using (var dlg = new VoucherPickDialog(fromList, avail, code => _service.ValidateVoucherCode(code, _billIds)))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedVoucher == null)
                    return;
                _voucherId = dlg.SelectedVoucher.Id;
                _updating = true;
                try { _numVoucher.Value = dlg.SelectedVoucher.Value; }
                finally { _updating = false; }
                OnNonCashOrElectronicChanged();
            }
        }

        private void PickPrepaidCard()
        {
            using (var dlg = new PrepaidCardInputDialog(code => _service.ValidatePrepaidCardCode(code, _billIds)))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedCard == null)
                    return;
                _prepaidCardId = dlg.SelectedCard.Id;
                _prepaidCardBalance = dlg.SelectedCard.Balance;
                decimal due = Math.Max(0m, CurrentNetDue());
                _updating = true;
                try { _numPrepaid.Value = Math.Min(_prepaidCardBalance, due); }
                finally { _updating = false; }
                OnNonCashOrElectronicChanged();
            }
        }

        private void PickRedeemPoints()
        {
            string custId = _cboCustomer.SelectedId ?? "";
            if (custId.Length == 0)
            {
                Warn(ServicePaymentConstants.PointsRequireCustomer);
                return;
            }
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;
            using (var dlg = new RedeemPointsDialog(_customerState.CustomerName, _customerState.AvailablePoints, CurrentNetDue(), pointRate))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _updating = true;
                try { _numPoints.Value = dlg.UsedMoney; }
                finally { _updating = false; }
                OnNonCashOrElectronicChanged();
            }
        }

        private void DoTempPrint()
        {
            LastInfoForTest = ServiceOrderConstants.NotSupportedYet;
            if (!SuppressPopup)
                Msg.Info(this, "In tạm tính: " + ServiceOrderConstants.NotSupportedYet);
        }

        private void SubmitPayment(bool printAfterClose)
        {
            var req = new PaymentRequest
            {
                PrimaryBillId = _ctx.PrimaryBillId,
                PrimaryTableId = _ctx.PrimaryTableId,
                GroupGuid = _ctx.GroupGuid,
                CustomerId = _cboCustomer.SelectedId,
                EmployeeId = _showEmployeeRow ? _cboEmployee.SelectedId : null,
                OrderNumbers = _showOrderRow ? _txtOrderNumbers.Text : null,
                CashTendered = _numCash.Value,
                Transfer = _showTransferRow ? _numTransfer.Value : 0m,
                Card = _showCardRow ? _numCard.Value : 0m,
                AccountId = _showBankRow ? _cboBank.SelectedId : null,
                Voucher = _showVoucherRow ? _numVoucher.Value : 0m,
                VoucherId = _voucherId,
                PrepaidCard = _showPrepaidRow ? _numPrepaid.Value : 0m,
                PrepaidCardId = _prepaidCardId,
                PointsMoney = _showPointsRow ? _numPoints.Value : 0m,
                PrintAfterClose = printAfterClose
            };

            UserSession overrideUser = null;
            for (int step = 0; step < 5; step++)
            {
                PaymentExecutionResult res = _service.ExecutePayment(req, overrideUser);
                if (res.Ok)
                {
                    ExecutionResult = res;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                if (res.NeedsReLogin)
                {
                    if (SuppressPopup)
                    {
                        Warn(res.ErrorMessage);
                        return;
                    }
                    using (var login = new ServiceReLoginDialog(_session.DatabasePath, res.RequiredFunctionId, "Thanh toán"))
                    {
                        if (login.ShowDialog(this) != DialogResult.OK || login.VerifiedSession == null)
                            return;
                        overrideUser = login.VerifiedSession;
                        continue;
                    }
                }
                if (res.NeedsDateTransferConfirmation)
                {
                    bool ok = AutoConfirmPromptsForTest || (!SuppressPopup && Msg.Confirm(this, res.ErrorMessage));
                    if (!ok)
                    {
                        Warn(res.ErrorMessage);
                        return;
                    }
                    req.ConfirmDateTransfer = true;
                    continue;
                }
                if (res.NeedsDebtConfirmation)
                {
                    bool ok = AutoConfirmPromptsForTest || (!SuppressPopup && Msg.Confirm(this, res.ErrorMessage));
                    if (!ok)
                    {
                        Warn(res.ErrorMessage);
                        return;
                    }
                    req.ConfirmDebt = true;
                    continue;
                }

                Warn(res.ErrorMessage);
                return;
            }
        }

        private void Warn(string msg)
        {
            LastWarningForTest = msg;
            _lblStatus.Text = msg ?? "";
            if (!SuppressPopup)
                Msg.Warning(this, msg);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F9 && _btnNoPrint != null && _btnNoPrint.Enabled)
            {
                SubmitPayment(false);
                return true;
            }
            if (keyData == Keys.F8 && _btnTempPrint != null && _btnTempPrint.Visible)
            {
                DoTempPrint();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
