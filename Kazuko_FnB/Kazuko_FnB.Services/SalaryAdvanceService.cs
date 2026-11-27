using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho màn Tạm ứng lương (TTHUCHI LOAI = 1, LATAMUNG = 30, TBANGLUONGID IS NULL;
    /// spec docs/specs/nhan-su-thuong-phat-tam-ung.md §3).
    /// Dùng lại hàm sinh số, ghi/sửa/xóa của <see cref="VoucherRepository"/> và kiểm tồn quỹ của <see cref="VoucherService"/>.
    /// </summary>
    public sealed class SalaryAdvanceService
    {
        private readonly UserSession _session;
        private readonly SalaryAdvanceRepository _repo;
        private readonly VoucherRepository _voucherRepo;
        private readonly ActivityLogger _activityLogger;

        public SalaryAdvanceService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new SalaryAdvanceRepository(session.DatabasePath);
            _voucherRepo = new VoucherRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return SalaryAdvanceRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return SalaryAdvanceRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return SalaryAdvanceRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return SalaryAdvanceRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return SalaryAdvanceRules.CanChangeDate(_session); } }

        public string GenerateNumber()
        {
            return _voucherRepo.GenerateNumber(VoucherKind.Expense);
        }

        public string LoadLatestReasonId()
        {
            return _repo.LoadLatestAdvanceReasonId();
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return _repo.LoadActiveEmployees();
        }

        public int CountActiveStores()
        {
            return _repo.CountActiveStores();
        }

        public List<VoucherRow> LoadList(SalaryAdvanceFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<VoucherRow>();

            List<VoucherRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => SalaryAdvanceRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        public VoucherRow LoadById(string id)
        {
            if (!CanView)
                return null;
            VoucherRow row = _voucherRepo.LoadById(id);
            return SalaryAdvanceRules.IsEditableAdvance(row) ? row : null;
        }

        public VoucherSaveResult Save(
            VoucherRow row,
            RewardPenaltyMonthLimit monthLimit = null,
            Action testHookAfterWrite = null)
        {
            if (row == null)
                return VoucherSaveResult.Fail(SalaryAdvanceText.NotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return VoucherSaveResult.Fail(SalaryAdvanceText.NoPermission);
            if (!isNew && !CanEdit)
                return VoucherSaveResult.Fail(SalaryAdvanceText.NoPermission);

            row.Kind = VoucherKind.Expense;
            row.IsAdvance = true;
            row.ObjectType = PhieuThuRules.Employee;
            row.CustomerId = null;
            row.SupplierId = null;
            row.Address = "";
            row.NoDebtChange = false;
            if (!row.Transfer)
                row.BankAccountId = null;
            row.Date = row.Date == DateTime.MinValue ? DateTime.Today : row.Date.Date;

            if (isNew && !CanChangeDate && monthLimit == null)
                row.Date = DateTime.Today;

            VoucherRules.NormalizeLinks(row);
            string validationErr = SalaryAdvanceRules.Validate(row, monthLimit);
            if (validationErr != null)
                return VoucherSaveResult.Fail(validationErr);

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
                            outcome = VoucherSaveResult.Fail(SalaryAdvanceText.NotFound);
                            return;
                        }
                        if (!SalaryAdvanceRules.IsEditableAdvance(existing))
                        {
                            outcome = VoucherSaveResult.Fail(SalaryAdvanceText.NotAdvanceVoucher);
                            return;
                        }
                        if (!CanChangeDate)
                            row.Date = existing.Date.Date;
                        string recheckErr = SalaryAdvanceRules.Validate(row, monthLimit);
                        if (recheckErr != null)
                        {
                            outcome = VoucherSaveResult.Fail(recheckErr);
                            return;
                        }
                    }

                    string empName = SalaryAdvanceRepository.LoadActiveEmployeeNameIn(conn, tx, row.EmployeeId);
                    if (empName == null)
                    {
                        bool keepOldEmp = existing != null
                            && string.Equals(existing.EmployeeId, row.EmployeeId, StringComparison.Ordinal);
                        if (!keepOldEmp)
                        {
                            outcome = VoucherSaveResult.Fail(SalaryAdvanceText.EmployeeInvalid);
                            return;
                        }
                        empName = existing.EmployeeName ?? existing.ObjectName ?? "";
                    }
                    row.ObjectName = empName;

                    if (!string.IsNullOrWhiteSpace(row.ReasonId)
                        && !SalaryAdvanceRepository.IsExpenseReasonIn(conn, tx, row.ReasonId))
                    {
                        outcome = VoucherSaveResult.Fail(SalaryAdvanceText.IncomeReasonNotAllowed);
                        return;
                    }

                    if (VoucherRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = VoucherSaveResult.Fail(VoucherConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Dùng lại đúng hàm kiểm chi vượt quỹ của Phiếu thu chi (FundRepository + FundLedgerRules).
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
                            outcome = VoucherSaveResult.Fail(SalaryAdvanceText.NotFound);
                            return;
                        }
                    }

                    string note = string.Format("{0} tạm ứng lương {1} cho {2} (Số tiền: {3})",
                        isNew ? "Thêm mới" : "Cập nhật",
                        row.Name ?? "",
                        empName,
                        VoucherConstants.FormatMoneyVi(row.Amount));
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = SalaryAdvanceText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = SalaryAdvanceRules.Clip(row.Name),
                            Note = SalaryAdvanceRules.Clip(note),
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
            return outcome ?? VoucherSaveResult.Fail("Không thể lưu phiếu tạm ứng lương.");
        }

        public VoucherDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return VoucherDeleteResult.Fail(SalaryAdvanceText.NotFound);
            if (!CanRemove)
                return VoucherDeleteResult.Fail(SalaryAdvanceText.NoPermission);

            VoucherDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                VoucherRow existing = VoucherRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = VoucherDeleteResult.Fail(SalaryAdvanceText.NotFound);
                    return;
                }
                if (!SalaryAdvanceRules.IsEditableAdvance(existing))
                {
                    outcome = VoucherDeleteResult.Fail(SalaryAdvanceText.NotAdvanceVoucher);
                    return;
                }

                if (VoucherRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = VoucherDeleteResult.Fail(SalaryAdvanceText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string note = string.Format("Xóa tạm ứng lương {0} (Số tiền: {1})",
                    existing.Name ?? "",
                    VoucherConstants.FormatMoneyVi(existing.Amount));
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = SalaryAdvanceText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = SalaryAdvanceRules.Clip(existing.Name),
                        Note = SalaryAdvanceRules.Clip(note),
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

            return outcome ?? VoucherDeleteResult.Fail("Không thể xóa phiếu tạm ứng lương.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }
    }
}
