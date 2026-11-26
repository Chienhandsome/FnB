using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    internal sealed partial class ServiceOrderView
    {
        internal PaymentDialogContext LastPreparedPaymentForTest { get; private set; }

        private void DoPay()
        {
            if (!_canViewPrice)
                return;
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }

            PaymentDialogContext ctx = _service.PreparePayment(_currentBill.Id, _currentTable.Id);
            if (!ctx.Ok)
            {
                ShowWarning(ctx.ErrorMessage);
                return;
            }
            LastPreparedPaymentForTest = ctx;
            if (SuppressPopup)
                return;

            using (var dlg = new PaymentDialog(_service, _session, ctx))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ExecutionResult != null && dlg.ExecutionResult.Ok)
                    HandlePaymentCompleted(dlg.ExecutionResult, ctx.BankAccounts);
            }
        }

        /// <summary>
        /// Thanh toán chung nhiều bàn (§3.7.3 Q14): chọn >= 2 bàn đang có khách -> gán NHOMGUID -> mở hộp thanh toán.
        /// Nếu đóng hộp thanh toán mà không trả -> gỡ NHOMGUID về như trước.
        /// </summary>
        private void DoGroupPay()
        {
            if (!_canViewPrice)
                return;

            List<BusyTableForGroupItem> busyTables = _service.LoadBusyTablesForGroup();
            if (busyTables.Count < 2)
            {
                ShowWarning(ServicePaymentConstants.GroupMinTables);
                return;
            }
            if (SuppressPopup)
                return;

            List<string> selectedBillIds;
            using (var picker = new GroupPaymentTablePickerDialog(busyTables, _currentTable != null ? _currentTable.Id : null))
            {
                if (picker.ShowDialog(this) != DialogResult.OK)
                    return;
                selectedBillIds = picker.SelectedBillIds;
            }

            GroupAssignResult assign = _service.AssignPaymentGroup(selectedBillIds);
            if (!assign.Ok)
            {
                ShowWarning(assign.ErrorMessage);
                return;
            }

            string primaryBillId = _currentBill != null && selectedBillIds.Contains(_currentBill.Id)
                ? _currentBill.Id
                : selectedBillIds[0];
            BusyTableForGroupItem primaryItem = busyTables.Find(x => string.Equals(x.BillId, primaryBillId, StringComparison.Ordinal));
            string primaryTableId = primaryItem != null ? primaryItem.TableId : (_currentTable != null ? _currentTable.Id : null);

            PaymentDialogContext ctx = _service.PreparePayment(primaryBillId, primaryTableId);
            if (!ctx.Ok)
            {
                _service.RestorePaymentGroup(assign.PreviousGroupGuids);
                ShowWarning(ctx.ErrorMessage);
                return;
            }

            using (var dlg = new PaymentDialog(_service, _session, ctx))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ExecutionResult != null && dlg.ExecutionResult.Ok)
                {
                    HandlePaymentCompleted(dlg.ExecutionResult, ctx.BankAccounts);
                }
                else
                {
                    // §3.7.3: đóng hộp thanh toán không trả -> gỡ NHOMGUID vừa gán, các bàn trở lại như trước.
                    _service.RestorePaymentGroup(assign.PreviousGroupGuids);
                    ReloadAll();
                    if (_currentTable != null)
                        OnTableSelected(_tableMap.FindTable(_currentTable.Id));
                }
            }
        }

        private void HandlePaymentCompleted(PaymentExecutionResult result, List<LookupItem> bankAccounts)
        {
            _lockRenewTimer.Stop();

            // §3.7.2: nếu đặt trước dư -> mở phiếu chi trả lại tiền đặt trước (ghi thật vào TTHUCHI).
            if (result.ExcessDeposit > 0m && !SuppressPopup)
            {
                using (var refundDlg = new RefundDepositDialog(_service, result, bankAccounts))
                    refundDlg.ShowDialog(this);
            }

            if (!string.IsNullOrEmpty(result.UpgradedGroupName) && !SuppressPopup)
            {
                Msg.Info(this, string.Format(
                    "Khách hàng '{0}' đã được tự động nâng hạng lên nhóm '{1}'.",
                    result.CustomerName ?? "", result.UpgradedGroupName));
            }

            _currentBill = null;
            _tableMap.ReloadAll();
            if (_currentTable != null)
            {
                _currentTable = _tableMap.FindTable(_currentTable.Id);
                if (_currentTable != null)
                    _tableMap.SelectTableVisual(_currentTable.Id);
            }
            RenderCurrentBill();
        }
    }
}
