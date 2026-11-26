using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class BillAdjustService
    {
        /// <summary>
        /// Cập nhật hóa đơn đã thanh toán trong MỘT transaction duy nhất (P1, §2.2, §6.3, §6.4, §6.6):
        /// kiểm quyền, chặn bill nhóm, kiểm khách/nợ/voucher/thẻ/điểm/tồn kho, đồng bộ dòng + xuất vật tư lại,
        /// cập nhật TDONHANG (giữ nguyên USERTHANHTOANID, GIOTHANHTOAN, NGAY), ghi lưu vết gom (TLUUVET).
        /// Không nâng/hạ nhóm khách (§6.3); nếu còn cọc dư trả kèm AlreadyRefundedDeposit và cảnh báo §6.4.
        /// </summary>
        public PaymentExecutionResult UpdatePaidBill(BillUpdateRequest req, UserSession overrideSession = null)
        {
            if (req == null || req.WorkingBill == null || string.IsNullOrEmpty(req.WorkingBill.Id))
                return PaymentExecutionResult.Fail(SalesManageConstants.SelectBillFirst);

            UserSession actor = overrideSession ?? _session;
            if (req.Mode == BillManageMode.Manage && !CanEditManage(actor))
                return PaymentExecutionResult.Fail(ServiceOrderConstants.NoPermission);
            if (!TableService.HasFunction(actor, ServiceOrderConstants.FuncThanhToan))
                return PaymentExecutionResult.ReLogin(ServiceOrderConstants.FuncThanhToan);
            if (!string.IsNullOrEmpty(req.WorkingBill.GroupGuid))
                return PaymentExecutionResult.Fail(SalesManageConstants.GroupBillEditBlocked);

            if (SystemConfig.BatChucNangKiemSoatOrder)
            {
                string orderErr = PaymentAllocationRules.ValidateOrderNumbers(req.OrderNumbers);
                if (orderErr != null)
                    return PaymentExecutionResult.Fail(orderErr);
            }
            if (SystemConfig.LuaChonVoucherTuDanhSach && req.Voucher > 0m && string.IsNullOrEmpty(req.VoucherId))
                return PaymentExecutionResult.Fail(ServicePaymentConstants.VoucherRequiredFromList);

            ServiceOrderHeader working = CloneWorkingBill(req.WorkingBill);
            bool hasItem = working.Lines != null
                && working.Lines.Exists(l => l != null && !l.IsMaterialExport && !l.IsComboChild);
            if (!hasItem)
                return PaymentExecutionResult.Fail(ServicePaymentConstants.EmptyBillError);

            decimal roundStep = SystemConfig.LamTronTien;
            OrderModePolicy.RecalcBillTotalsInPlace(working, roundStep);
            ServiceOrderValidationResult val = OrderCalculator.ValidateInvoice(working, working.Lines, roundStep);
            if (!val.IsValid)
                return PaymentExecutionResult.Fail(val.ErrorMessage);

            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            bool allowDebt = SystemConfig.ChoPhepKhachNo;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;
            int pointCalcMode = SystemConfig.CachTinhDiem;
            int revenuePerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;

            PaymentExecutionResult outcome = null;

            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    PaidBillDetail dbDetail = SalesManageRepository.LoadPaidBillDetailIn(conn, tx, working.Id);
                    if (dbDetail == null || dbDetail.Header == null || dbDetail.Header.PaidFlag != 30)
                    {
                        outcome = PaymentExecutionResult.Fail(SalesManageConstants.BillNotPaidOrMissing);
                        return;
                    }
                    if (dbDetail.IsGroupBill)
                    {
                        outcome = PaymentExecutionResult.Fail(SalesManageConstants.GroupBillEditBlocked);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    DateTime serverDate = serverNow.Date;

                    string custId = req.CustomerId != null ? req.CustomerId : working.CustomerId;
                    if (req.Mode == BillManageMode.Adjust
                        && !string.Equals(custId ?? "", dbDetail.Header.CustomerId ?? "", StringComparison.Ordinal)
                        && !CanChangeCustomerInAdjust(actor))
                    {
                        outcome = PaymentExecutionResult.ReLogin(SalesManageConstants.FuncThayDoiKhachHangDieuChinh);
                        return;
                    }
                    string custName = "";
                    string custAddr = "";
                    string linkedCardId = null;
                    string custGroupId = null;
                    bool hasCustomer = !string.IsNullOrEmpty(custId);
                    if (hasCustomer)
                    {
                        if (!ServicePaymentRepository.CustomerExistsIn(conn, tx, custId, out custName, out custAddr, out linkedCardId, out custGroupId))
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.CustomerNotFound);
                            return;
                        }
                    }

                    var excludeIds = new List<string> { working.Id };
                    decimal deposit = ServicePaymentRepository.GetDepositIn(conn, tx, working.Id, dbDetail.Header.BookingId, includeAdvance);
                    decimal alreadyRefunded = SalesManageRepository.GetRefundedDepositIn(conn, tx, working.Id, dbDetail.Header.BookingId);
                    decimal oldDebt = hasCustomer
                        ? ServicePaymentRepository.GetOldDebtIn(conn, tx, custId, excludeIds)
                        : 0m;

                    if (req.Voucher > 0m && !string.IsNullOrEmpty(req.VoucherId))
                    {
                        VoucherInfo vi = ServicePaymentRepository.ValidateVoucherByIdIn(conn, tx, req.VoucherId, serverDate, excludeIds);
                        if (!vi.IsValid)
                        {
                            outcome = PaymentExecutionResult.Fail(vi.Error);
                            return;
                        }
                    }

                    if (req.PrepaidCard > 0m)
                    {
                        if (string.IsNullOrEmpty(req.PrepaidCardId))
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PrepaidCodeEmpty);
                            return;
                        }
                        PrepaidCardInfo ci = ServicePaymentRepository.ValidatePrepaidCardByIdIn(conn, tx, req.PrepaidCardId, serverDate, excludeIds);
                        if (!ci.IsValid)
                        {
                            outcome = PaymentExecutionResult.Fail(ci.Error);
                            return;
                        }
                        if (req.PrepaidCard > ci.Balance)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PrepaidOverBalance);
                            return;
                        }
                    }

                    if (req.PointsMoney > 0m)
                    {
                        if (!hasCustomer)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PointsRequireCustomer);
                            return;
                        }
                        if (pointRate <= 0m)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PointRateNotConfigured);
                            return;
                        }
                        decimal availPoints = ServicePaymentRepository.GetCustomerPointsIn(
                            conn, tx, custId, pointCalcMode, revenuePerPoint, excludeIds);
                        decimal maxPointMoney = PaymentAllocationRules.CalcMoneyFromPoints(availPoints, pointRate);
                        if (req.PointsMoney > maxPointMoney)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PointsOverAvailable);
                            return;
                        }
                    }

                    var slice = new PaymentBillSlice
                    {
                        BillId = working.Id,
                        BillName = working.Name,
                        TableId = working.TableId,
                        TableName = working.TableName,
                        BatDau = working.BatDau,
                        OpenDate = dbDetail.Header.Ngay,
                        OriginalCustomerId = hasCustomer ? custId : null,
                        TongCong = working.TongCong,
                        Deposit = deposit
                    };

                    PaymentAllocationResult alloc = PaymentAllocationRules.Allocate(new PaymentAllocationInput
                    {
                        Bills = new List<PaymentBillSlice> { slice },
                        CustomerId = hasCustomer ? custId : null,
                        OldDebt = oldDebt,
                        CashTendered = req.CashTendered,
                        Transfer = req.Transfer,
                        Card = req.Card,
                        AccountId = req.AccountId,
                        Voucher = req.Voucher,
                        VoucherId = req.VoucherId,
                        PrepaidCard = req.PrepaidCard,
                        PrepaidCardId = req.PrepaidCardId,
                        PointsMoney = req.PointsMoney,
                        PointRate = pointRate,
                        RevenuePerPoint = revenuePerPoint,
                        AllowCustomerDebt = allowDebt
                    });
                    if (!alloc.IsValid)
                    {
                        outcome = PaymentExecutionResult.Fail(alloc.Error);
                        return;
                    }

                    if (alloc.Shortfall > 0m && !req.ConfirmDebt)
                    {
                        outcome = new PaymentExecutionResult
                        {
                            Ok = false,
                            NeedsDebtConfirmation = true,
                            DebtAmount = alloc.Shortfall,
                            DebtCustomerName = custName,
                            ErrorMessage = ServicePaymentConstants.DebtConfirmPrompt(
                                custName, NumberInput.FormatForDisplay(alloc.Shortfall, 2, true))
                        };
                        return;
                    }

                    List<MaterialExportItem> exports = ServicePaymentRepository.ComputeMaterialExportsIn(conn, tx, working);
                    if (!allowNegativeStock)
                    {
                        string stockErr = SalesManageRepository.CheckPaidBillUpdateStockIn(conn, tx, working, exports);
                        if (stockErr != null)
                        {
                            outcome = PaymentExecutionResult.Fail(stockErr);
                            return;
                        }
                    }

                    SalesManageRepository.SyncBillLinesAndMaterialExportsIn(conn, tx, working, exports, actor.UserId, serverNow);

                    bool updateDate = req.Mode == BillManageMode.Manage
                        && req.AllowDateChange
                        && working.Ngay.Date != dbDetail.Header.Ngay.Date;

                    PaymentBillAllocation a = alloc.Bills[0];
                    int updated = SalesManageRepository.UpdatePaidBillHeaderAndPaymentIn(
                        conn, tx, working, a, updateDate, working.Ngay.Date,
                        req.EmployeeId, req.OrderNumbers, actor.UserId, serverNow);
                    if (updated != 1)
                        throw new BillUpdateRollbackException(SalesManageConstants.BillNotPaidOrMissing);

                    if (req.TestHookAfterUpdate != null)
                        req.TestHookAfterUpdate();

                    string funcName = OrderModePolicy.FunctionNameOf(req.Mode);
                    var logs = new List<ActivityLogRequest>();
                    if (req.PendingLogs != null)
                    {
                        foreach (ActivityLogRequest item in req.PendingLogs)
                        {
                            if (item == null)
                                continue;
                            logs.Add(new ActivityLogRequest
                            {
                                FunctionName = string.IsNullOrEmpty(item.FunctionName) ? funcName : item.FunctionName,
                                Kind = item.Kind,
                                BillId = working.Id,
                                TableName = working.TableName,
                                Note = item.Note,
                                ItemName = item.ItemName,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice,
                                Amount = item.Amount,
                                Account = actor.UserName,
                                UserId = actor.UserId,
                                Time = item.Time ?? serverNow
                            });
                        }
                    }
                    logs.Add(new ActivityLogRequest
                    {
                        FunctionName = funcName,
                        Kind = ActivityKind.DongBan,
                        BillId = working.Id,
                        TableName = working.TableName,
                        Note = req.PrintAfterClose
                            ? "Đóng hóa đơn (Có in) - " + working.Name
                            : "Đóng hóa đơn (Không in) - " + working.Name,
                        Amount = working.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = serverNow
                    });
                    _activityLogger.WriteBatch(conn, tx, logs);

                    string refundWarning = alloc.ExcessDeposit > 0m
                        ? SalesManageConstants.AlreadyRefundedDepositWarning(alreadyRefunded, alloc.ExcessDeposit)
                        : null;

                    outcome = new PaymentExecutionResult
                    {
                        Ok = true,
                        PrimaryBillId = working.Id,
                        PaidBillIds = new List<string> { working.Id },
                        PaidBillNames = working.Name,
                        PaymentDate = updateDate ? working.Ngay.Date : dbDetail.Header.Ngay.Date,
                        ExcessDeposit = alloc.ExcessDeposit,
                        AlreadyRefundedDeposit = alreadyRefunded,
                        RefundDepositWarning = refundWarning,
                        CustomerId = hasCustomer ? custId : null,
                        CustomerName = custName,
                        CustomerAddress = custAddr,
                        StoreId = dbDetail.Header.StoreId ?? (_session.CurrentStore != null ? _session.CurrentStore.Id : null),
                        PrintAfterClose = req.PrintAfterClose,
                        Allocation = alloc
                    };
                });
            }
            catch (BillUpdateRollbackException ex)
            {
                return PaymentExecutionResult.Fail(ex.Message);
            }

            return outcome ?? PaymentExecutionResult.Fail("Không thể cập nhật hóa đơn.");
        }
    }
}
