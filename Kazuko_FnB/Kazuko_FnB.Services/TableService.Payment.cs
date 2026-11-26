using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        private sealed class PaymentRollbackException : Exception
        {
            public PaymentRollbackException(string message) : base(message) { }
        }

        private ServicePaymentRepository PayRepo()
        {
            return new ServicePaymentRepository(_databasePath);
        }

        // ------------------------------------------------------------------ Quyền & tra cứu phục vụ thanh toán

        public bool CanPay(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncThanhToan);
        }

        public bool CanTempPrint(UserSession overrideSession = null)
        {
            return SystemConfig.ChoPhepInTamTinh && HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncInTamTinh);
        }

        public List<BusyTableForGroupItem> LoadBusyTablesForGroup()
        {
            return PayRepo().LoadBusyTablesForGroup(_session.UserName, SystemConfig.HeThongChayNhieuMayTram);
        }

        public GroupAssignResult AssignPaymentGroup(IList<string> billIds)
        {
            return PayRepo().AssignPaymentGroup(billIds, _session.UserId);
        }

        public void RestorePaymentGroup(IDictionary<string, string> previousGroupGuids)
        {
            PayRepo().RestorePaymentGroup(previousGroupGuids, _session.UserId);
        }

        public List<LookupItem> LoadExpenseReasons()
        {
            return new FormRepository(_databasePath).LoadExpenseReasons();
        }

        public string GenerateRefundVoucherNumber()
        {
            return new PhieuThuRepository(_databasePath).GenerateExpenseNumber();
        }

        public VoucherInfo ValidateVoucherCode(string code, IList<string> excludeBillIds)
        {
            return PayRepo().Read(conn =>
            {
                DateTime serverDate = InvoiceNumberRepository.GetServerTimestamp(conn, null).Date;
                return ServicePaymentRepository.ValidateVoucherByCodeIn(conn, null, code, serverDate, excludeBillIds);
            });
        }

        public List<VoucherInfo> LoadAvailableVouchers(IList<string> excludeBillIds = null)
        {
            return PayRepo().LoadAvailableVouchers(excludeBillIds);
        }

        public PrepaidCardInfo ValidatePrepaidCardCode(string code, IList<string> excludeBillIds)
        {
            return PayRepo().Read(conn =>
            {
                DateTime serverDate = InvoiceNumberRepository.GetServerTimestamp(conn, null).Date;
                return ServicePaymentRepository.ValidatePrepaidCardByCodeIn(conn, null, code, serverDate, excludeBillIds);
            });
        }

        public CustomerPaymentState LookupCustomerPaymentState(string customerId, IList<string> excludeBillIds)
        {
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;
            int pointMode = SystemConfig.CachTinhDiem;
            int revPerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;

            return PayRepo().Read(conn =>
                LoadCustomerPaymentStateIn(conn, null, customerId, excludeBillIds, pointMode, revPerPoint, pointRate));
        }

        /// <summary>
        /// Chuẩn bị dữ liệu mở hộp Xác nhận thanh toán cho bill đơn hoặc nhóm bill cùng NHOMGUID (§3.7).
        /// Kiểm tra bill chưa thanh toán, có dòng hàng và thỏa DuLieuHoaDonOk (§6.5).
        /// </summary>
        public PaymentDialogContext PreparePayment(string primaryBillId, string primaryTableId)
        {
            var ctx = new PaymentDialogContext
            {
                PrimaryBillId = primaryBillId,
                PrimaryTableId = primaryTableId
            };
            if (string.IsNullOrEmpty(primaryBillId))
            {
                ctx.ErrorMessage = ServiceOrderConstants.SelectTableFirst;
                return ctx;
            }

            decimal roundStep = SystemConfig.LamTronTien;
            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            decimal pointRate = SystemConfig.QuyDoi1DiemSangTien;
            int pointMode = SystemConfig.CachTinhDiem;
            int revPerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;

            ServicePaymentRepository payRepo = PayRepo();
            payRepo.Read(conn =>
            {
                List<ServiceOrderHeader> bills;
                string err = LoadAndValidateTargetBillsIn(conn, null, primaryBillId, primaryTableId, null, roundStep, out bills);
                if (err != null)
                {
                    ctx.ErrorMessage = err;
                    return 0;
                }

                var excludeIds = new List<string>(bills.Count);
                decimal totalBills = 0m;
                decimal totalDeposit = 0m;
                string initialCustomerId = null;

                foreach (ServiceOrderHeader b in bills)
                {
                    excludeIds.Add(b.Id);
                    decimal dep = ServicePaymentRepository.GetDepositIn(conn, null, b.Id, b.BookingId, includeAdvance);
                    totalBills += b.TongCong;
                    totalDeposit += dep;
                    if (string.Equals(b.Id, primaryBillId, StringComparison.Ordinal) && !string.IsNullOrEmpty(b.CustomerId))
                        initialCustomerId = b.CustomerId;

                    ctx.Bills.Add(new PaymentBillSlice
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

                if (string.IsNullOrEmpty(initialCustomerId))
                {
                    for (int i = bills.Count - 1; i >= 0; i--)
                    {
                        if (!string.IsNullOrEmpty(bills[i].CustomerId))
                        {
                            initialCustomerId = bills[i].CustomerId;
                            break;
                        }
                    }
                }

                ServiceOrderHeader primary = bills.Find(x => string.Equals(x.Id, primaryBillId, StringComparison.Ordinal)) ?? bills[0];
                ctx.GroupGuid = primary.GroupGuid ?? "";
                ctx.TotalBills = totalBills;
                ctx.TotalDeposit = totalDeposit;
                ctx.InitialCustomerId = initialCustomerId ?? "";
                ctx.InitialCustomerState = LoadCustomerPaymentStateIn(
                    conn, null, ctx.InitialCustomerId, excludeIds, pointMode, revPerPoint, pointRate);
                ctx.Ok = true;
                return 0;
            });

            if (!ctx.Ok)
                return ctx;

            ctx.Customers = _customerRepo.LoadLookup();
            ctx.BankAccounts = payRepo.LoadBankAccounts();
            if (SystemConfig.BatBuocNhapNhanVienBanHang)
                ctx.Employees = payRepo.LoadEmployees();
            return ctx;
        }

        // ------------------------------------------------------------------ Lưu phiếu chi trả lại tiền đặt trước (§3.7.2)

        public RefundVoucherResult SaveRefundDepositVoucher(RefundVoucherInput input)
        {
            var res = new RefundVoucherResult();
            if (input == null)
            {
                res.ErrorMessage = "Thiếu thông tin phiếu chi.";
                return res;
            }
            if (input.Amount <= 0m)
            {
                res.ErrorMessage = "Số tiền chi phải lớn hơn 0.";
                return res;
            }
            if (input.Transfer && string.IsNullOrEmpty(input.BankAccountId))
            {
                res.ErrorMessage = FundText.AccountRequired;
                return res;
            }

            string savedNumber;
            string savedId = new PhieuThuRepository(_databasePath).SaveExpenseVoucher(input, _session.UserId, out savedNumber);
            if (string.IsNullOrEmpty(savedId))
            {
                res.ErrorMessage = "Số phiếu \"" + savedNumber + "\" đã tồn tại. Mời bạn nhập số phiếu khác.";
                return res;
            }

            res.Ok = true;
            res.SavedId = savedId;
            res.SavedNumber = savedNumber;
            return res;
        }

        // ------------------------------------------------------------------ Nội bộ dùng chung

        private static string LoadAndValidateTargetBillsIn(
            FbConnection conn,
            FbTransaction tx,
            string primaryBillId,
            string primaryTableId,
            string overrideGroupGuid,
            decimal roundStep,
            out List<ServiceOrderHeader> bills)
        {
            bills = new List<ServiceOrderHeader>();
            ServiceOrderHeader primary = ServiceOrderRepository.LoadBillIn(conn, tx, primaryBillId);
            if (primary == null || primary.PaidFlag != 0)
                return ServiceOrderConstants.BillAlreadyPaid;

            string groupGuid = !string.IsNullOrEmpty(overrideGroupGuid) ? overrideGroupGuid : (primary.GroupGuid ?? "");
            if (!string.IsNullOrEmpty(groupGuid))
            {
                bills = ServicePaymentRepository.LoadGroupBillsIn(conn, tx, groupGuid);
                if (!bills.Exists(b => string.Equals(b.Id, primary.Id, StringComparison.Ordinal)))
                    bills.Add(primary);
            }
            else
            {
                bills.Add(primary);
            }

            foreach (ServiceOrderHeader b in bills)
            {
                int tmpPrint;
                string tblId = string.Equals(b.Id, primaryBillId, StringComparison.Ordinal) && !string.IsNullOrEmpty(primaryTableId)
                    ? primaryTableId
                    : b.TableId;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, b.Id, tblId, out tmpPrint))
                    return ServiceOrderConstants.BillAlreadyPaid;

                bool hasItem = b.Lines != null && b.Lines.Exists(l => l != null && !l.IsMaterialExport && !l.IsComboChild);
                if (!hasItem)
                    return ServicePaymentConstants.EmptyBillError;

                ServiceOrderValidationResult v = OrderCalculator.ValidateInvoice(b, b.Lines, roundStep);
                if (!v.IsValid)
                    return v.ErrorMessage;
            }

            return null;
        }

        private static CustomerPaymentState LoadCustomerPaymentStateIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            IList<string> excludeBillIds,
            int pointCalcMode,
            int revenuePerPoint,
            decimal pointRate)
        {
            var state = new CustomerPaymentState { PointRate = pointRate };
            if (string.IsNullOrEmpty(customerId))
                return state;

            string name, addr, cardId, groupId;
            if (!ServicePaymentRepository.CustomerExistsIn(conn, tx, customerId, out name, out addr, out cardId, out groupId))
                return state;

            DateTime serverDate = InvoiceNumberRepository.GetServerTimestamp(conn, tx).Date;
            state.CustomerId = customerId;
            state.CustomerName = name;
            state.OldDebt = ServicePaymentRepository.GetOldDebtIn(conn, tx, customerId, excludeBillIds);
            state.AvailablePoints = ServicePaymentRepository.GetCustomerPointsIn(
                conn, tx, customerId, pointCalcMode, revenuePerPoint, excludeBillIds);

            if (!string.IsNullOrEmpty(cardId))
            {
                PrepaidCardInfo card = ServicePaymentRepository.ValidatePrepaidCardByIdIn(
                    conn, tx, cardId, serverDate, excludeBillIds);
                if (card.IsValid && card.Balance > 0m)
                    state.LinkedPrepaidCard = card;
            }
            return state;
        }
    }
}
