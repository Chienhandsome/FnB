using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ Chi lương (patch P3b, spec docs/specs/nhan-su-tinh-luong.md §5):
    /// - Đọc danh sách Thực nhận / Đã chi / Còn lại / Số phiếu cho bảng lương đã lưu.
    /// - Tạo / Sửa / Xóa phiếu chi lương (LOAI = 1, TBANGLUONGID = bảng lương, LOAIDOITUONG = 1, LATAMUNG = 0)
    ///   bằng đúng đường ghi của <see cref="VoucherRepository"/> và kiểm vượt quỹ của <see cref="VoucherService"/>.
    /// - Mọi thao tác ghi/xóa chạy trong một transaction duy nhất kèm lưu vết (cắt chuỗi tối đa 255 ký tự).
    /// </summary>
    public sealed class PayrollPayoutService
    {
        private readonly UserSession _session;
        private readonly PayrollPayoutRepository _repo;
        private readonly VoucherRepository _voucherRepo;
        private readonly ActivityLogger _activityLogger;

        public PayrollPayoutService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new PayrollPayoutRepository(session.DatabasePath);
            _voucherRepo = new VoucherRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return PayrollPayoutRules.CanView(_session); } }
        public bool CanPayOut { get { return PayrollPayoutRules.CanPayOut(_session); } }
        public bool CanChangeDate { get { return PayrollPayoutRules.CanChangeDate(_session); } }

        public string GenerateNumber()
        {
            return _voucherRepo.GenerateNumber(VoucherKind.Expense);
        }

        public string LoadLatestReasonId()
        {
            return _repo.LoadLatestPayoutReasonId();
        }

        public PayrollPayoutSheet LoadPayoutSheet(string payrollId)
        {
            if (!CanView || string.IsNullOrEmpty(payrollId))
                return null;
            return _repo.LoadPayoutSheet(payrollId);
        }

        public VoucherRow LoadById(string voucherId, string expectedPayrollId = null)
        {
            if (!CanView || string.IsNullOrEmpty(voucherId))
                return null;
            VoucherRow row = _voucherRepo.LoadById(voucherId);
            return PayrollPayoutRules.IsEditablePayout(row, expectedPayrollId) ? row : null;
        }

        /// <summary>
        /// Tạo mới hoặc sửa phiếu chi lương trong một transaction duy nhất.
        /// Tham số <paramref name="testHookAfterWrite"/> dùng cho selftest kiểm rollback.
        /// </summary>
        public VoucherSaveResult Save(VoucherRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return VoucherSaveResult.Fail(PayrollPayoutText.NotFound);
            if (!CanPayOut)
                return VoucherSaveResult.Fail(PayrollPayoutText.NoPermission);

            bool isNew = string.IsNullOrEmpty(row.Id);
            row.Kind = VoucherKind.Expense;
            row.IsAdvance = false; // Ghi LATAMUNG = 0 (không phải tạm ứng).
            row.ObjectType = PhieuThuRules.Employee;
            row.CustomerId = null;
            row.SupplierId = null;
            row.Address = "";
            row.NoDebtChange = false;
            if (!row.Transfer)
                row.BankAccountId = null;

            VoucherRules.NormalizeLinks(row);

            VoucherSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    VoucherRow existing = null;
                    if (!isNew)
                    {
                        existing = VoucherRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = VoucherSaveResult.Fail(PayrollPayoutText.NotFound);
                            return;
                        }
                        if (!PayrollPayoutRules.IsEditablePayout(existing, row.PayrollId))
                        {
                            outcome = VoucherSaveResult.Fail(PayrollPayoutText.NotPayoutVoucher);
                            return;
                        }
                        row.PayrollId = existing.PayrollId;
                        row.EmployeeId = existing.EmployeeId;
                        row.Date = CanChangeDate
                            ? (row.Date == DateTime.MinValue ? existing.Date.Date : row.Date.Date)
                            : existing.Date.Date;
                    }

                    PayrollPeriodRow period = PayrollRepository.LoadPeriodByIdIn(conn, tx, row.PayrollId);
                    if (period == null)
                    {
                        outcome = VoucherSaveResult.Fail(PayrollPayoutText.PayrollRequired);
                        return;
                    }

                    if (isNew)
                    {
                        if (row.Date == DateTime.MinValue || !CanChangeDate)
                            row.Date = PayrollPayoutRules.DefaultPayoutDate(period.Year, period.Month, DateTime.Today);
                        else
                            row.Date = row.Date.Date;
                    }

                    string validationErr = PayrollPayoutRules.Validate(row, period.Month, period.Year);
                    if (validationErr != null)
                    {
                        outcome = VoucherSaveResult.Fail(validationErr);
                        return;
                    }

                    string empName = PayrollPayoutRepository.LoadEmployeeNameInPayrollIn(conn, tx, period.Id, row.EmployeeId);
                    if (empName == null)
                    {
                        bool keepOldEmp = existing != null
                            && string.Equals(existing.EmployeeId, row.EmployeeId, StringComparison.Ordinal);
                        if (!keepOldEmp)
                        {
                            outcome = VoucherSaveResult.Fail(PayrollPayoutText.EmployeeNotInPayroll);
                            return;
                        }
                        empName = !string.IsNullOrWhiteSpace(existing.EmployeeName)
                            ? existing.EmployeeName
                            : (existing.ObjectName ?? "");
                    }
                    row.ObjectName = PayrollText.Clip(empName);

                    if (!string.IsNullOrWhiteSpace(row.ReasonId)
                        && !SalaryAdvanceRepository.IsExpenseReasonIn(conn, tx, row.ReasonId))
                    {
                        outcome = VoucherSaveResult.Fail(PayrollPayoutText.IncomeReasonNotAllowed);
                        return;
                    }

                    if (VoucherRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = VoucherSaveResult.Fail(VoucherConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Dùng lại đúng hàm kiểm chi vượt quỹ của Phiếu chi thường (FundRepository + FundLedgerRules).
                    string overdraftErr = VoucherService.CheckCashOverdraftIn(conn, tx, row, existing);
                    if (overdraftErr != null)
                    {
                        outcome = VoucherSaveResult.Fail(overdraftErr);
                        return;
                    }

                    row.Amount = Math.Round(row.Amount, 2, MidpointRounding.AwayFromZero);
                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = VoucherRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        VoucherRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (VoucherRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = VoucherSaveResult.Fail(PayrollPayoutText.NotFound);
                            return;
                        }
                    }

                    string note = string.Format("{0} phiếu chi lương {1} ({2}) cho {3} (Số tiền: {4})",
                        isNew ? "Tạo" : "Sửa",
                        row.Name ?? "",
                        period.Name ?? "",
                        empName,
                        VoucherConstants.FormatMoneyVi(row.Amount));
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = PayrollPayoutText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = PayrollText.Clip(row.Name),
                            Note = PayrollText.Clip(note),
                            Amount = row.Amount,
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new VoucherSaveResult
                    {
                        Ok = true,
                        SavedId = row.Id,
                        SavedNumber = row.Name
                    };
                });
            }
            catch
            {
                if (assignedNewId)
                    row.Id = null;
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                row.Id = null;
            return outcome ?? VoucherSaveResult.Fail("Không thể lưu phiếu chi lương.");
        }

        /// <summary>
        /// Xóa cứng phiếu chi lương, hoàn lại quỹ và ghi lưu vết trong một transaction duy nhất.
        /// </summary>
        public VoucherDeleteResult Delete(string voucherId, string expectedPayrollId = null, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(voucherId))
                return VoucherDeleteResult.Fail(PayrollPayoutText.NotFound);
            if (!CanPayOut)
                return VoucherDeleteResult.Fail(PayrollPayoutText.NoPermission);

            VoucherDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                VoucherRow existing = VoucherRepository.LoadByIdIn(conn, tx, voucherId);
                if (existing == null)
                {
                    outcome = VoucherDeleteResult.Fail(PayrollPayoutText.NotFound);
                    return;
                }
                if (!PayrollPayoutRules.IsEditablePayout(existing, expectedPayrollId))
                {
                    outcome = VoucherDeleteResult.Fail(PayrollPayoutText.NotPayoutVoucher);
                    return;
                }

                if (VoucherRepository.DeleteIn(conn, tx, voucherId) == 0)
                {
                    outcome = VoucherDeleteResult.Fail(PayrollPayoutText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string empLabel = !string.IsNullOrWhiteSpace(existing.EmployeeName)
                    ? existing.EmployeeName
                    : (existing.ObjectName ?? "");
                string note = string.Format("Xóa phiếu chi lương {0} của {1} (Số tiền: {2})",
                    existing.Name ?? "",
                    empLabel,
                    VoucherConstants.FormatMoneyVi(existing.Amount));
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = PayrollPayoutText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = PayrollText.Clip(existing.Name),
                        Note = PayrollText.Clip(note),
                        Amount = existing.Amount,
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new VoucherDeleteResult { Ok = true };
            });

            return outcome ?? VoucherDeleteResult.Fail("Không thể xóa phiếu chi lương.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }
    }
}
