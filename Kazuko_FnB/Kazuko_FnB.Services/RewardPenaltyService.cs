using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho màn Thưởng phạt (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2):
    /// phân quyền theo tên chức năng, kiểm ngày theo giới hạn tháng (nếu có), tự sinh số TPyy/#####,
    /// kiểm trùng số phiếu không phân biệt hoa/thường, giữ người/giờ tạo khi sửa, xóa cứng và ghi TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class RewardPenaltyService
    {
        private readonly UserSession _session;
        private readonly RewardPenaltyRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public RewardPenaltyService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new RewardPenaltyRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return RewardPenaltyRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return RewardPenaltyRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return RewardPenaltyRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return RewardPenaltyRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return RewardPenaltyRules.CanChangeDate(_session); } }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(forDate);
        }

        public List<RewardPenaltyRow> LoadList(RewardPenaltyFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<RewardPenaltyRow>();
            List<RewardPenaltyRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => RewardPenaltyRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        public RewardPenaltyRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return _repo.LoadActiveEmployees();
        }

        public List<LookupItem> LoadActiveReasons()
        {
            return _repo.LoadActiveReasons();
        }

        public RewardPenaltySaveResult Save(
            RewardPenaltyRow row,
            RewardPenaltyMonthLimit monthLimit = null,
            Action testHookAfterWrite = null)
        {
            if (row == null)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.NotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.NoPermission);
            if (!isNew && !CanEdit)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.NoPermission);

            row.Name = (row.Name ?? "").Trim();
            row.Date = row.Date == DateTime.MinValue ? DateTime.Today : row.Date.Date;

            if (isNew && !CanChangeDate && monthLimit == null)
                row.Date = DateTime.Today;

            RewardPenaltySaveResult pre = RewardPenaltyRules.Validate(row, monthLimit);
            if (!pre.Ok)
                return pre;

            RewardPenaltySaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    RewardPenaltyRow existing = null;
                    if (!isNew)
                    {
                        existing = RewardPenaltyRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = RewardPenaltySaveResult.Fail(RewardPenaltyText.NotFound);
                            return;
                        }
                        if (!CanChangeDate)
                            row.Date = existing.Date.Date;
                        RewardPenaltySaveResult recheck = RewardPenaltyRules.Validate(row, monthLimit);
                        if (!recheck.Ok)
                        {
                            outcome = recheck;
                            return;
                        }
                    }

                    bool keepOldEmp = existing != null
                        && string.Equals(existing.EmployeeId, row.EmployeeId, StringComparison.Ordinal);
                    if (!keepOldEmp && !RewardPenaltyRepository.EmployeeExistsIn(conn, tx, row.EmployeeId))
                    {
                        outcome = RewardPenaltySaveResult.Fail(RewardPenaltyText.EmployeeInvalid, hasEmployeeError: true);
                        return;
                    }

                    if (RewardPenaltyRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = RewardPenaltySaveResult.Fail(
                            RewardPenaltyText.NumberDuplicate(row.Name),
                            hasNumberError: true);
                        return;
                    }

                    row.Bonus = Math.Round(row.Bonus, 2, MidpointRounding.AwayFromZero);
                    row.Penalty = Math.Round(row.Penalty, 2, MidpointRounding.AwayFromZero);

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = RewardPenaltyRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        RewardPenaltyRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (RewardPenaltyRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = RewardPenaltySaveResult.Fail(RewardPenaltyText.NotFound);
                            return;
                        }
                    }

                    string note = string.Format("{0} phiếu thưởng phạt {1} (Thưởng: {2}, Phạt: {3})",
                        isNew ? "Thêm mới" : "Cập nhật",
                        row.Name ?? "",
                        RewardPenaltyText.FormatMoneyVi(row.Bonus),
                        RewardPenaltyText.FormatMoneyVi(row.Penalty));
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = RewardPenaltyText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = RewardPenaltyRules.Clip(row.Name),
                            Note = RewardPenaltyRules.Clip(note),
                            Amount = row.Bonus + row.Penalty,
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new RewardPenaltySaveResult
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
                    ResetNew(row);
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                ResetNew(row);

            return outcome ?? RewardPenaltySaveResult.Fail("Không thể lưu phiếu thưởng phạt.");
        }

        public RewardPenaltyDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return RewardPenaltyDeleteResult.Fail(RewardPenaltyText.NotFound);
            if (!CanRemove)
                return RewardPenaltyDeleteResult.Fail(RewardPenaltyText.NoPermission);

            RewardPenaltyDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                RewardPenaltyRow existing = RewardPenaltyRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = RewardPenaltyDeleteResult.Fail(RewardPenaltyText.NotFound);
                    return;
                }

                if (RewardPenaltyRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = RewardPenaltyDeleteResult.Fail(RewardPenaltyText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string note = string.Format("Xóa phiếu thưởng phạt {0} (Thưởng: {1}, Phạt: {2})",
                    existing.Name ?? "",
                    RewardPenaltyText.FormatMoneyVi(existing.Bonus),
                    RewardPenaltyText.FormatMoneyVi(existing.Penalty));
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = RewardPenaltyText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = RewardPenaltyRules.Clip(existing.Name),
                        Note = RewardPenaltyRules.Clip(note),
                        Amount = existing.Bonus + existing.Penalty,
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new RewardPenaltyDeleteResult { Ok = true };
            });

            return outcome ?? RewardPenaltyDeleteResult.Fail("Không thể xóa phiếu thưởng phạt.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(RewardPenaltyRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
