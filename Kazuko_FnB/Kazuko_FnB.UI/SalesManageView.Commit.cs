using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    internal sealed partial class SalesManageView
    {
        // ------------------------------------------------------------------ Trả đồ (§2.3)

        private void DoReturnItems()
        {
            if (_workingBill == null)
            {
                ShowWarning(SalesManageConstants.SelectBillFirst);
                return;
            }
            if (!string.IsNullOrEmpty(_workingBill.GroupGuid))
            {
                ShowWarning(SalesManageConstants.GroupBillReturnBlocked);
                return;
            }

            UserSession actor = _session;
            if (!TableService.HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMon))
            {
                if (SuppressPopup)
                {
                    ShowWarning(ServiceOrderConstants.NoPermission);
                    return;
                }
                using (var login = new ServiceReLoginDialog(_session.DatabasePath, ServiceOrderConstants.FuncXoaGiamMon, "Trả đồ"))
                {
                    if (login.ShowDialog(this) != DialogResult.OK || login.VerifiedSession == null)
                        return;
                    actor = login.VerifiedSession;
                }
            }

            List<ReturnableItemRow> groups = ReturnItemRules.BuildReturnableGroups(_workingBill.Lines);
            if (groups.Count == 0)
            {
                ShowWarning(SalesManageConstants.NoReturnableItems);
                return;
            }
            if (SuppressPopup)
                return;

            using (var dlg = new ReturnItemsDialog(_workingBill.Name, groups, _canViewPrice))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ConfirmedReturns.Count == 0)
                    return;
                ApplyConfirmedReturns(dlg.ConfirmedReturns, actor);
            }
        }

        internal void ApplyConfirmedReturns(List<ReturnableItemRow> confirmedReturns, UserSession actor)
        {
            if (_workingBill == null || confirmedReturns == null || confirmedReturns.Count == 0)
                return;

            // §2.3: GIOTRADO được ghi ngay vào DB khi bấm Trả đồ (ngoại lệ nguyên tắc chỉ nằm trong bộ nhớ).
            ServiceOpResult timeRes = _adjustService.RecordReturnTime(_workingBill.Id, actor);
            if (!timeRes.Ok)
            {
                ShowWarning(timeRes.ErrorMessage);
                return;
            }

            foreach (ReturnableItemRow r in confirmedReturns)
            {
                if (r.ReturnQty <= 0m)
                    continue;
                string comboId = DbHelper.NewId();
                ServiceOrderLine retLine = ReturnItemRules.CreateReturnLine(_workingBill.Id, r, r.ReturnQty, DbHelper.NewId(), comboId);
                _workingBill.Lines.Add(retLine);

                if (string.Equals(r.ProductTypeId, ProductRules.TypeCombo, StringComparison.Ordinal))
                {
                    foreach (ServiceRecipeComponent c in _adjustService.LoadRecipe(r.ProductId))
                    {
                        if (string.IsNullOrEmpty(c.MaterialId) || c.Quantity <= 0m)
                            continue;
                        _workingBill.Lines.Add(new ServiceOrderLine
                        {
                            Id = DbHelper.NewId(),
                            OrderId = _workingBill.Id,
                            ProductId = c.MaterialId,
                            ProductTypeId = c.MaterialTypeId,
                            ItemName = c.MaterialName,
                            UnitId = c.UnitId,
                            UnitName = c.UnitName ?? "",
                            WarehouseId = r.WarehouseId ?? _workingBill.WarehouseId,
                            Quantity = -r.ReturnQty * c.Quantity,
                            ComboId = DbHelper.NewId(),
                            ComboParentId = comboId,
                            ComboQty = c.Quantity
                        });
                    }
                }

                QueuePendingLog(
                    ActivityKind.TraDo,
                    ReturnItemRules.FormatReturnLogNote(r.ItemName, r.ReturnQty, r.UnitPrice),
                    r.ItemName,
                    r.ReturnQty,
                    r.UnitPrice,
                    retLine.Amount);
            }

            MarkDirtyAndRecalc(null);
        }

        // ------------------------------------------------------------------ Cập nhật / Hủy phiếu / In lại

        private void DoUpdateBill()
        {
            if (!_canViewPrice || _workingBill == null)
                return;

            PaymentDialogContext ctx = _adjustService.PrepareUpdatePayment(_workingBill, _mode);
            if (!ctx.Ok)
            {
                ShowWarning(ctx.ErrorMessage);
                return;
            }
            LastPreparedPaymentForTest = ctx;
            if (SuppressPopup)
                return;

            DateTime originalDate = _loadedDetail != null && _loadedDetail.Header != null
                ? _loadedDetail.Header.Ngay.Date
                : _workingBill.Ngay.Date;
            bool allowDateChange = SystemConfig.GetBool(SalesManageConstants.ConfigAllowChangeDate);

            using (var dlg = new PaymentDialog(_tableService, _session, ctx, (payReq, overrideUser) =>
            {
                var updateReq = new BillUpdateRequest
                {
                    Mode = _mode,
                    WorkingBill = _workingBill,
                    OriginalDate = originalDate,
                    AllowDateChange = allowDateChange,
                    CustomerId = payReq.CustomerId,
                    EmployeeId = payReq.EmployeeId,
                    OrderNumbers = payReq.OrderNumbers,
                    CashTendered = payReq.CashTendered,
                    Transfer = payReq.Transfer,
                    Card = payReq.Card,
                    AccountId = payReq.AccountId,
                    Voucher = payReq.Voucher,
                    VoucherId = payReq.VoucherId,
                    PrepaidCard = payReq.PrepaidCard,
                    PrepaidCardId = payReq.PrepaidCardId,
                    PointsMoney = payReq.PointsMoney,
                    PrintAfterClose = payReq.PrintAfterClose,
                    ConfirmDebt = payReq.ConfirmDebt,
                    PendingLogs = new List<ActivityLogRequest>(_pendingLogs)
                };
                return _adjustService.UpdatePaidBill(updateReq, overrideUser);
            }))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ExecutionResult != null && dlg.ExecutionResult.Ok)
                    HandleUpdateCompleted(dlg.ExecutionResult, ctx.BankAccounts);
            }
        }

        private void HandleUpdateCompleted(PaymentExecutionResult result, List<LookupItem> bankAccounts)
        {
            _isDirty = false;
            _pendingLogs.Clear();

            if (result.PrintAfterClose)
            {
                PrintBillById(result.PrimaryBillId, _btnUpdate);
            }
            else
            {
                LastInfoForTest = SalesManageConstants.UpdatedSuccessMessage;
                if (!SuppressPopup)
                    Msg.Info(this, SalesManageConstants.UpdatedSuccessMessage);
            }

            if (result.ExcessDeposit > 0m && !SuppressPopup)
            {
                using (var refundDlg = new RefundDepositDialog(_tableService, result, bankAccounts))
                    refundDlg.ShowDialog(this);
            }

            ReloadBillList(result.PrimaryBillId);
        }

        private void DoCancelBill()
        {
            if (_mode != BillManageMode.Manage)
                return;
            PaidBillListItem item = SelectedListBill();
            if (item == null)
            {
                ShowWarning(SalesManageConstants.SelectBillFirst);
                return;
            }
            if (item.IsGroupBill)
            {
                ShowWarning(SalesManageConstants.GroupBillCancelBlocked);
                return;
            }

            UserSession actor = _session;
            if (!TableService.HasFunction(actor, ServiceOrderConstants.FuncHuyHoaDon))
            {
                if (SuppressPopup)
                {
                    ShowWarning(ServiceOrderConstants.NoPermission);
                    return;
                }
                using (var login = new ServiceReLoginDialog(_session.DatabasePath, ServiceOrderConstants.FuncHuyHoaDon, "Hủy hóa đơn"))
                {
                    if (login.ShowDialog(this) != DialogResult.OK || login.VerifiedSession == null)
                        return;
                    actor = login.VerifiedSession;
                }
            }

            string reason = ServiceTransferConstants.CancelDefaultReason;
            if (!SuppressPopup)
            {
                if (SystemConfig.NhapLyDoKhiHuyHoaDon)
                {
                    reason = PromptDialog.Ask(this, "LÝ DO HỦY HÓA ĐƠN", "Nhập lý do hủy hóa đơn số '" + item.Name + "':", "");
                    if (string.IsNullOrWhiteSpace(reason))
                        return;
                }
                else if (!Msg.Confirm(this, SalesManageConstants.CancelBillConfirm(item.Name)))
                {
                    return;
                }
            }

            var req = new BillCancelPaidRequest { BillId = item.Id, Reason = reason };
            BillCancelPaidResult res = _adjustService.CancelPaidBill(req, actor);
            if (res.NeedsVoucherWarningConfirmation)
            {
                if (!SuppressPopup && !Msg.Confirm(this, res.ErrorMessage))
                    return;
                req.ConfirmRelatedVouchers = true;
                res = _adjustService.CancelPaidBill(req, actor);
            }
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            _isDirty = false;
            _pendingLogs.Clear();
            ReloadBillList(null);
        }

        private void DoReprintBill()
        {
            if (_mode != BillManageMode.Manage || _workingBill == null || _invoicePrintCoordinator.IsBusy)
                return;
            PrintBillById(_workingBill.Id, _btnReprint);
        }

        private void PrintBillById(string billId, Control trigger)
        {
            TempPrintPrepareResult prep = _tableService.PrepareReprintPaidBill(billId, null);
            if (prep.NeedsReLogin && !SuppressPopup)
            {
                using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, prep.RequiredFunctionId, "In lại bill"))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.VerifiedSession == null)
                        return;
                    prep = _tableService.PrepareReprintPaidBill(billId, dlg.VerifiedSession);
                }
            }
            if (!prep.Ok)
            {
                ShowWarning(prep.ErrorMessage);
                return;
            }

            IKitchenPrinterStore store = KitchenPrinterStoreForTest ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            _invoicePrintCoordinator.ReprintPaidBill(
                this,
                trigger,
                _session.DatabasePath,
                _session,
                prep.Document,
                store,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate(string msg) { LastPrintMessageForTest = msg; });
        }
    }
}
