using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Công nợ khách hàng &amp; Công nợ nhà cung cấp (docs/specs/cong-no.md):
    /// - Đọc danh sách công nợ gộp và chi tiết hóa đơn/chứng từ + phiếu thu/chi theo quyền SFUNCTION.
    /// - Thu nợ khách hàng theo từng đơn: ghi MỘT phiếu thu TTHUCHI (LOAI = 0, LAPHIEUTHUCONGNO = 30)
    ///   kèm các dòng TCHITIETTHANHTOAN và lưu vết TLUUVET trong MỘT transaction duy nhất;
    ///   KHÔNG sửa TDONHANG.TIENTHANHTOAN; kiểm tra live không thu quá Còn nợ của từng đơn; lỗi thì rollback.
    /// </summary>
    public sealed class DebtService
    {
        private readonly UserSession _session;
        private readonly DebtRepository _repo;
        private readonly VoucherRepository _voucherRepo;
        private readonly ActivityLogger _activityLogger;

        public DebtService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new DebtRepository(session.DatabasePath);
            _voucherRepo = new VoucherRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, session.UserName, session.UserId);
        }

        public bool CanViewCustomer { get { return DebtRules.CanViewCustomerDebt(_session); } }
        public bool CanViewSupplier { get { return DebtRules.CanViewSupplierDebt(_session); } }
        public bool CanCollectCustomer { get { return DebtRules.CanCollectCustomerDebt(_session); } }
        public bool CanPaySupplier { get { return DebtRules.CanPaySupplierDebt(_session); } }
        public bool CanChangeVoucherDate { get { return DebtRules.CanChangeVoucherDate(_session); } }

        public bool CanView(DebtPartyKind kind)
        {
            return kind == DebtPartyKind.Customer ? CanViewCustomer : CanViewSupplier;
        }

        public List<DebtPartnerRow> LoadPartnerDebts(DebtPartyKind kind)
        {
            if (!CanView(kind))
                return new List<DebtPartnerRow>();
            return kind == DebtPartyKind.Customer
                ? _repo.LoadCustomerDebts()
                : _repo.LoadSupplierDebts();
        }

        public List<DebtGroupNode> LoadGroups(DebtPartyKind kind)
        {
            if (!CanView(kind))
                return new List<DebtGroupNode>();
            return kind == DebtPartyKind.Customer
                ? _repo.LoadCustomerGroups()
                : _repo.LoadSupplierGroups();
        }

        public DebtPartnerDetail LoadPartnerDetail(DebtPartyKind kind, string partnerId)
        {
            if (!CanView(kind) || string.IsNullOrEmpty(partnerId))
                return new DebtPartnerDetail();
            return kind == DebtPartyKind.Customer
                ? _repo.LoadCustomerDetail(partnerId)
                : _repo.LoadSupplierDetail(partnerId);
        }

        public List<DebtDocumentRow> LoadOpenCustomerBills(string customerId)
        {
            if (!CanViewCustomer || string.IsNullOrEmpty(customerId))
                return new List<DebtDocumentRow>();
            return _repo.LoadOpenCustomerBills(customerId);
        }

        public decimal GetCustomerDebt(string customerId)
        {
            if (!CanViewCustomer || string.IsNullOrEmpty(customerId))
                return 0m;
            return _repo.GetCustomerDebt(customerId);
        }

        public decimal GetSupplierDebt(string supplierId)
        {
            if (!CanViewSupplier || string.IsNullOrEmpty(supplierId))
                return 0m;
            return _repo.GetSupplierDebt(supplierId);
        }

        public string GenerateDebtReceiptNumber()
        {
            return _voucherRepo.GenerateNumber(VoucherKind.Income);
        }

        public string FindDefaultDebtReceiptReasonId()
        {
            return _repo.FindDefaultDebtReceiptReasonId();
        }

        public List<DebtAllocationLine> LoadAllocationsByVoucher(string voucherId)
        {
            return _repo.LoadAllocationsByVoucher(voucherId);
        }

        /// <summary>
        /// Lưu phiếu thu nợ khách hàng theo từng đơn trong một transaction duy nhất (spec §6 #5 &amp; §7 T1).
        /// Tham số <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public DebtCollectResult CollectCustomerDebt(DebtCollectRequest request, Action testHookAfterWrite = null)
        {
            if (!CanCollectCustomer)
                return DebtCollectResult.Fail(DebtConstants.NoPermission);

            decimal totalAmount;
            string validationError = DebtRules.ValidateCollectRequest(request, out totalAmount);
            if (validationError != null)
                return DebtCollectResult.Fail(validationError);

            request.VoucherNumber = (request.VoucherNumber ?? "").Trim();
            request.Description = (request.Description ?? "").Trim();
            request.Note = (request.Note ?? "").Trim();
            if (!request.Transfer)
                request.BankAccountId = null;

            var positiveLines = new List<DebtAllocationLine>();
            foreach (DebtAllocationLine line in request.Allocations)
            {
                if (line != null && line.PayAmount > 0m)
                    positiveLines.Add(line);
            }

            DebtCollectResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                string custName, custAddr;
                if (!DebtRepository.CustomerActiveExistsIn(conn, tx, request.CustomerId, out custName, out custAddr))
                {
                    outcome = DebtCollectResult.Fail(DebtConstants.CustomerNotFound);
                    return;
                }

                if (string.IsNullOrWhiteSpace(request.CustomerName))
                    request.CustomerName = custName;
                if (string.IsNullOrWhiteSpace(request.CustomerAddress))
                    request.CustomerAddress = custAddr;
                if (string.IsNullOrWhiteSpace(request.Description))
                    request.Description = DebtConstants.ClipText("Thu công nợ khách hàng " + request.CustomerName);

                if (VoucherRepository.IsNumberTakenIn(conn, tx, request.VoucherNumber, ""))
                {
                    outcome = DebtCollectResult.Fail(VoucherConstants.NumberDuplicate(request.VoucherNumber));
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                if (!CanChangeVoucherDate)
                    request.Date = serverNow.Date;

                // Kiểm tra lại Còn nợ thực tế của từng hóa đơn ngay trong transaction (tránh thu quá nợ khi dữ liệu cũ).
                decimal verifiedTotal = 0m;
                foreach (DebtAllocationLine line in positiveLines)
                {
                    DebtDocumentRow liveBill = DebtRepository.LoadCustomerBillIn(conn, tx, line.BillId, request.CustomerId);
                    if (liveBill == null)
                    {
                        outcome = DebtCollectResult.Fail(DebtConstants.BillNotFound);
                        return;
                    }
                    if (line.PayAmount > liveBill.RemainingDebt)
                    {
                        outcome = DebtCollectResult.Fail(
                            DebtConstants.AllocationExceedsDebt(liveBill.Number, liveBill.RemainingDebt, line.PayAmount));
                        return;
                    }
                    verifiedTotal += line.PayAmount;
                }

                if (verifiedTotal <= 0m)
                {
                    outcome = DebtCollectResult.Fail(DebtConstants.CollectTotalMustBePositive);
                    return;
                }

                string voucherId = DbHelper.NewId();
                DebtRepository.InsertDebtReceiptIn(conn, tx, voucherId, request, verifiedTotal, _session.UserId, serverNow);
                DebtRepository.InsertAllocationLinesIn(conn, tx, voucherId, request.VoucherNumber, positiveLines, _session.UserId, serverNow);

                string tableName = DebtConstants.ClipText(request.VoucherNumber);
                string logNote = DebtConstants.ClipText(string.Format(
                    "Thu nợ theo đơn số {0} - Khách: {1} ({2} đơn, tổng: {3})",
                    request.VoucherNumber,
                    request.CustomerName,
                    positiveLines.Count,
                    DebtConstants.FormatMoneyVi(verifiedTotal)));

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = DebtConstants.CustomerDebtFunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = voucherId,
                        TableName = tableName,
                        Note = logNote,
                        Amount = verifiedTotal,
                        Account = _session.UserName,
                        UserId = _session.UserId,
                        Time = serverNow
                    }
                });

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new DebtCollectResult
                {
                    Ok = true,
                    SavedVoucherId = voucherId,
                    SavedVoucherNumber = request.VoucherNumber,
                    TotalCollected = verifiedTotal,
                    AllocationCount = positiveLines.Count
                };
            });

            return outcome ?? DebtCollectResult.Fail("Không thể lưu phiếu thu công nợ.");
        }
    }
}
