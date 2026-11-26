using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        /// <summary>
        /// Thực hiện thanh toán một bill đơn hoặc một nhóm bill (NHOMGUID) trong MỘT transaction duy nhất (§3.7, §6.5, §6.6):
        /// kiểm quyền, nhân viên, số order, ngày giao dịch, khách/nợ, voucher, thẻ trả trước, điểm, tồn kho + xuất vật tư,
        /// ghi TDONHANG, xóa TINCHEBIEN, cấp SOTT, tính DIEM, nâng hạng khách, nhả khóa bàn và ghi TLUUVET.
        /// Bất kỳ lỗi nào giữa chừng đều rollback sạch toàn bộ transaction.
        /// </summary>
        public PaymentExecutionResult ExecutePayment(PaymentRequest request, UserSession overrideSession = null)
        {
            if (request == null || string.IsNullOrEmpty(request.PrimaryBillId))
                return PaymentExecutionResult.Fail(ServiceOrderConstants.SelectTableFirst);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncThanhToan))
                return PaymentExecutionResult.ReLogin(ServiceOrderConstants.FuncThanhToan);

            if (SystemConfig.BatBuocNhapNhanVienBanHang && string.IsNullOrEmpty(request.EmployeeId))
                return PaymentExecutionResult.Fail(ServicePaymentConstants.EmployeeRequired);

            if (SystemConfig.BatChucNangKiemSoatOrder)
            {
                string orderErr = PaymentAllocationRules.ValidateOrderNumbers(request.OrderNumbers);
                if (orderErr != null)
                    return PaymentExecutionResult.Fail(orderErr);
            }

            if (SystemConfig.LuaChonVoucherTuDanhSach && request.Voucher > 0m && string.IsNullOrEmpty(request.VoucherId))
                return PaymentExecutionResult.Fail(ServicePaymentConstants.VoucherRequiredFromList);

            decimal roundStep = SystemConfig.LamTronTien;
            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            bool allowDebt = SystemConfig.ChoPhepKhachNo;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;
            int businessDateMode = SystemConfig.CachChonNgayGiaoDich;
            int pointCalcMode = SystemConfig.CachTinhDiem;
            int revenuePerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;
            bool autoUpgrade = SystemConfig.TuDongNangCapThanhVienKhiDatHanMuc;

            PaymentExecutionResult outcome = null;
            ServicePaymentRepository payRepo = PayRepo();

            try
            {
                payRepo.RunInTransaction((conn, tx) =>
                {
                    List<ServiceOrderHeader> bills;
                    string loadErr = LoadAndValidateTargetBillsIn(
                        conn, tx, request.PrimaryBillId, request.PrimaryTableId, request.GroupGuid, roundStep, out bills);
                    if (loadErr != null)
                    {
                        outcome = PaymentExecutionResult.Fail(loadErr);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    DateTime serverDate = serverNow.Date;

                    // Kiểm tra ngày giao dịch (§6.5: Mode 0 theo ngày mở, Mode 1 theo ngày thanh toán + cảnh báo nếu khác ngày).
                    var paymentDates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
                    bool anyDateTransfer = false;
                    foreach (ServiceOrderHeader b in bills)
                    {
                        bool warn;
                        DateTime payDate = OrderCalculator.ResolveBusinessDateForPayment(businessDateMode, b.Ngay, serverDate, out warn);
                        paymentDates[b.Id] = payDate;
                        if (warn)
                            anyDateTransfer = true;
                    }
                    if (anyDateTransfer && !request.ConfirmDateTransfer)
                    {
                        outcome = new PaymentExecutionResult
                        {
                            Ok = false,
                            NeedsDateTransferConfirmation = true,
                            PaymentDate = serverDate,
                            ErrorMessage = ServicePaymentConstants.DateTransferWarning(serverDate)
                        };
                        return;
                    }

                    // Kiểm tra khách hàng (hồ sơ DKHACHHANG, §6.6).
                    string custName = "";
                    string custAddr = "";
                    string linkedCardId = null;
                    string custGroupId = null;
                    bool hasCustomer = !string.IsNullOrEmpty(request.CustomerId);
                    if (hasCustomer)
                    {
                        if (!ServicePaymentRepository.CustomerExistsIn(conn, tx, request.CustomerId, out custName, out custAddr, out linkedCardId, out custGroupId))
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.CustomerNotFound);
                            return;
                        }
                    }

                    var allBillIds = new List<string>(bills.Count);
                    var slices = new List<PaymentBillSlice>(bills.Count);
                    foreach (ServiceOrderHeader b in bills)
                    {
                        allBillIds.Add(b.Id);
                        decimal dep = ServicePaymentRepository.GetDepositIn(conn, tx, b.Id, b.BookingId, includeAdvance);
                        slices.Add(new PaymentBillSlice
                        {
                            BillId = b.Id,
                            BillName = b.Name,
                            TableId = b.TableId,
                            TableName = b.TableName,
                            BatDau = b.BatDau,
                            OpenDate = b.Ngay,
                            OriginalCustomerId = b.CustomerId,
                            TongCong = b.TongCong,
                            Deposit = dep
                        });
                    }

                    decimal oldDebt = hasCustomer
                        ? ServicePaymentRepository.GetOldDebtIn(conn, tx, request.CustomerId, allBillIds)
                        : 0m;

                    // Kiểm tra lại voucher, thẻ trả trước, điểm tích lũy ngay trong transaction.
                    if (request.Voucher > 0m && !string.IsNullOrEmpty(request.VoucherId))
                    {
                        VoucherInfo vi = ServicePaymentRepository.ValidateVoucherByIdIn(conn, tx, request.VoucherId, serverDate, allBillIds);
                        if (!vi.IsValid)
                        {
                            outcome = PaymentExecutionResult.Fail(vi.Error);
                            return;
                        }
                    }

                    if (request.PrepaidCard > 0m)
                    {
                        if (string.IsNullOrEmpty(request.PrepaidCardId))
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PrepaidCodeEmpty);
                            return;
                        }
                        PrepaidCardInfo ci = ServicePaymentRepository.ValidatePrepaidCardByIdIn(conn, tx, request.PrepaidCardId, serverDate, allBillIds);
                        if (!ci.IsValid)
                        {
                            outcome = PaymentExecutionResult.Fail(ci.Error);
                            return;
                        }
                        if (request.PrepaidCard > ci.Balance)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PrepaidOverBalance);
                            return;
                        }
                    }

                    if (request.PointsMoney > 0m)
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
                            conn, tx, request.CustomerId, pointCalcMode, revenuePerPoint, allBillIds);
                        decimal maxPointMoney = PaymentAllocationRules.CalcMoneyFromPoints(availPoints, pointRate);
                        if (request.PointsMoney > maxPointMoney)
                        {
                            outcome = PaymentExecutionResult.Fail(ServicePaymentConstants.PointsOverAvailable);
                            return;
                        }
                    }

                    PaymentAllocationResult alloc = PaymentAllocationRules.Allocate(new PaymentAllocationInput
                    {
                        Bills = slices,
                        CustomerId = hasCustomer ? request.CustomerId : null,
                        OldDebt = oldDebt,
                        CashTendered = request.CashTendered,
                        Transfer = request.Transfer,
                        Card = request.Card,
                        AccountId = request.AccountId,
                        Voucher = request.Voucher,
                        VoucherId = request.VoucherId,
                        PrepaidCard = request.PrepaidCard,
                        PrepaidCardId = request.PrepaidCardId,
                        PointsMoney = request.PointsMoney,
                        PointRate = pointRate,
                        RevenuePerPoint = revenuePerPoint,
                        AllowCustomerDebt = allowDebt
                    });
                    if (!alloc.IsValid)
                    {
                        outcome = PaymentExecutionResult.Fail(alloc.Error);
                        return;
                    }

                    if (alloc.Shortfall > 0m && !request.ConfirmDebt)
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

                    // Tính xuất vật tư (XuatVatTu) và kiểm tra âm kho toàn đợt thanh toán (§3.7, §6.6).
                    var exportsByBill = new Dictionary<string, List<MaterialExportItem>>(StringComparer.Ordinal);
                    foreach (ServiceOrderHeader b in bills)
                        exportsByBill[b.Id] = ServicePaymentRepository.ComputeMaterialExportsIn(conn, tx, b);

                    if (!allowNegativeStock)
                    {
                        string stockErr = ServicePaymentRepository.CheckPaymentStockIn(conn, tx, bills, exportsByBill);
                        if (stockErr != null)
                        {
                            outcome = PaymentExecutionResult.Fail(stockErr);
                            return;
                        }
                    }

                    // Ghi toàn bộ thay đổi trong cùng một transaction.
                    var billById = new Dictionary<string, ServiceOrderHeader>(StringComparer.Ordinal);
                    foreach (ServiceOrderHeader b in bills)
                        billById[b.Id] = b;

                    var paidIds = new List<string>(alloc.Bills.Count);
                    var paidNames = new List<string>(alloc.Bills.Count);
                    var logs = new List<ActivityLogRequest>(alloc.Bills.Count);
                    var upgradeCustomerIds = new HashSet<string>(StringComparer.Ordinal);
                    string primaryStoreId = null;

                    foreach (PaymentBillAllocation a in alloc.Bills)
                    {
                        ServiceOrderHeader b = billById[a.BillId];
                        if (string.IsNullOrEmpty(primaryStoreId) && !string.IsNullOrEmpty(b.StoreId))
                            primaryStoreId = b.StoreId;

                        DateTime billPayDate = paymentDates[b.Id];

                        ServicePaymentRepository.DeleteOldMaterialExportsIn(conn, tx, b.Id);
                        List<MaterialExportItem> exports = exportsByBill[b.Id];
                        foreach (MaterialExportItem exp in exports)
                            ServicePaymentRepository.InsertMaterialExportLineIn(conn, tx, b.Id, exp, actor.UserId, serverNow);

                        ServicePaymentRepository.DeleteKitchenPrintIn(conn, tx, b.Id);

                        int sott = ServicePaymentRepository.GetNextSequenceInDayIn(conn, tx, billPayDate);
                        int updated = ServicePaymentRepository.UpdateBillPaidIn(
                            conn, tx, a, billPayDate, serverNow, sott, actor.UserId, request.EmployeeId, request.OrderNumbers);
                        if (updated != 1)
                            throw new PaymentRollbackException(ServiceOrderConstants.BillAlreadyPaid);

                        ServicePaymentRepository.ReleaseTableLockIn(conn, tx, b.TableId);

                        if (request.TestHookAfterBillUpdate != null)
                            request.TestHookAfterBillUpdate(b.Id);

                        paidIds.Add(b.Id);
                        paidNames.Add(b.Name);
                        if (!string.IsNullOrEmpty(a.FinalCustomerId))
                            upgradeCustomerIds.Add(a.FinalCustomerId);

                        logs.Add(new ActivityLogRequest
                        {
                            FunctionName = ActivityConstants.FuncSuDungDichVu,
                            Kind = ActivityKind.DongBan,
                            BillId = b.Id,
                            TableName = b.TableName,
                            Note = "Thanh toán hóa đơn " + b.Name + " bàn " + b.TableName,
                            Amount = b.TongCong,
                            Account = actor.UserName,
                            UserId = actor.UserId,
                            Time = serverNow
                        });
                    }

                    _activityLogger.WriteBatch(conn, tx, logs);

                    string upgradedGroup = null;
                    if (autoUpgrade)
                    {
                        foreach (string cid in upgradeCustomerIds)
                        {
                            string upgraded = ServicePaymentRepository.TryUpgradeCustomerGroupIn(
                                conn, tx, cid, pointCalcMode, revenuePerPoint, actor.UserId, serverNow);
                            if (!string.IsNullOrEmpty(upgraded))
                                upgradedGroup = upgraded;
                        }
                    }

                    DateTime finalDate = paymentDates.ContainsKey(request.PrimaryBillId)
                        ? paymentDates[request.PrimaryBillId]
                        : serverDate;

                    outcome = new PaymentExecutionResult
                    {
                        Ok = true,
                        PrimaryBillId = request.PrimaryBillId,
                        PaidBillIds = paidIds,
                        PaidBillNames = string.Join(", ", paidNames.ToArray()),
                        PaymentDate = finalDate,
                        ExcessDeposit = alloc.ExcessDeposit,
                        CustomerId = hasCustomer ? request.CustomerId : null,
                        CustomerName = custName,
                        CustomerAddress = custAddr,
                        StoreId = primaryStoreId ?? (_session.CurrentStore != null ? _session.CurrentStore.Id : null),
                        UpgradedGroupName = upgradedGroup,
                        PrintAfterClose = request.PrintAfterClose,
                        Allocation = alloc
                    };
                });
            }
            catch (PaymentRollbackException ex)
            {
                return PaymentExecutionResult.Fail(ex.Message);
            }

            return outcome ?? PaymentExecutionResult.Fail("Không thể thanh toán hóa đơn.");
        }
    }
}
