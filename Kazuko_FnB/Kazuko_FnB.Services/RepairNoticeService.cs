using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2):
    /// phân quyền theo tên chức năng, khóa ngày khi thiếu quyền đổi ngày, tự sinh số SCyy/#####,
    /// kiểm trùng số phiếu theo UNICODE_CI_AI, giữ người/giờ tạo khi sửa,
    /// xóa cứng và ghi TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class RepairNoticeService
    {
        private readonly UserSession _session;
        private readonly RepairNoticeRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public RepairNoticeService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new RepairNoticeRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return RepairNoticeRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return RepairNoticeRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return RepairNoticeRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return RepairNoticeRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return RepairNoticeRules.CanChangeDate(_session); } }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(forDate);
        }

        public List<RepairNoticeRow> LoadList(RepairNoticeFilter filter = null)
        {
            if (!CanView)
                return new List<RepairNoticeRow>();
            List<RepairNoticeRow> rows = _repo.LoadList(filter);
            if (filter != null && !string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => RepairNoticeRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        public RepairNoticeRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<RepairRoomLookup> LoadActiveRooms()
        {
            return _repo.LoadActiveRooms();
        }

        public List<LookupItem> LoadActiveRoomTypes()
        {
            return _repo.LoadActiveRoomTypes();
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return _repo.LoadActiveEmployees();
        }

        public RepairNoticeSaveResult Save(RepairNoticeRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.NotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.NoPermission);
            if (!isNew && !CanEdit)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.NoPermission);

            row.Name = (row.Name ?? "").Trim();
            row.Date = row.Date == DateTime.MinValue ? DateTime.Today : row.Date.Date;

            if (isNew && !CanChangeDate)
                row.Date = DateTime.Today;

            RepairNoticeSaveResult pre = RepairNoticeRules.Validate(row);
            if (!pre.Ok)
                return pre;

            RepairNoticeSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    RepairNoticeRow existing = null;
                    if (!isNew)
                    {
                        existing = RepairNoticeRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = RepairNoticeSaveResult.Fail(RepairNoticeText.NotFound);
                            return;
                        }
                        if (!CanChangeDate)
                            row.Date = existing.Date.Date;
                    }

                    bool keepOldRoom = existing != null
                        && string.Equals(existing.RoomId, row.RoomId, StringComparison.Ordinal);
                    if (!keepOldRoom && !RepairNoticeRepository.RoomExistsIn(conn, tx, row.RoomId))
                    {
                        outcome = RepairNoticeSaveResult.Fail(RepairNoticeText.RoomInvalid, hasRoomError: true);
                        return;
                    }

                    if (RepairNoticeRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = RepairNoticeSaveResult.Fail(
                            RepairNoticeText.NumberDuplicate(row.Name),
                            hasNumberError: true);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = RepairNoticeRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        RepairNoticeRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (RepairNoticeRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = RepairNoticeSaveResult.Fail(RepairNoticeText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới thông báo sửa chữa: " : "Cập nhật thông báo sửa chữa: ")
                        + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = RepairNoticeText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = RepairNoticeRules.Clip(row.Name),
                            Note = RepairNoticeRules.Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new RepairNoticeSaveResult
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

            return outcome ?? RepairNoticeSaveResult.Fail("Không thể lưu phiếu thông báo sửa chữa.");
        }

        public RepairNoticeDeleteResult Delete(string id, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(id))
                return RepairNoticeDeleteResult.Fail(RepairNoticeText.NotFound);
            if (!CanRemove)
                return RepairNoticeDeleteResult.Fail(RepairNoticeText.NoPermission);

            RepairNoticeDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                RepairNoticeRow existing = RepairNoticeRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = RepairNoticeDeleteResult.Fail(RepairNoticeText.NotFound);
                    return;
                }

                if (RepairNoticeRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = RepairNoticeDeleteResult.Fail(RepairNoticeText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string note = "Xóa thông báo sửa chữa: " + (existing.Name ?? "");
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = RepairNoticeText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = RepairNoticeRules.Clip(existing.Name),
                        Note = RepairNoticeRules.Clip(note),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new RepairNoticeDeleteResult { Ok = true };
            });

            return outcome ?? RepairNoticeDeleteResult.Fail("Không thể xóa phiếu thông báo sửa chữa.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(RepairNoticeRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
