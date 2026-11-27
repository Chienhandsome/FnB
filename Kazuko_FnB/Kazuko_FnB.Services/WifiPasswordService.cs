using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục mật khẩu wifi (DMATKHAUWIFI, spec docs/specs/quan-tri-nho.md §3):
    /// kiểm quyền SFUNCTION, kiểm mật khẩu bắt buộc/không trùng (UNICODE_CI_AI toàn bảng),
    /// xóa cứng và ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class WifiPasswordService
    {
        private readonly UserSession _session;
        private readonly WifiPasswordRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public WifiPasswordService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new WifiPasswordRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return WifiPasswordRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return WifiPasswordRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return WifiPasswordRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return WifiPasswordRules.Can(_session, RoleModes.Remove); } }

        public List<WifiPasswordRow> LoadAll()
        {
            if (!CanView)
                return new List<WifiPasswordRow>();
            return _repo.LoadAll();
        }

        public WifiPasswordRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public WifiPasswordSaveResult Save(WifiPasswordRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return WifiPasswordSaveResult.Fail("Không có dữ liệu mật khẩu wifi.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return WifiPasswordSaveResult.Fail(WifiPasswordText.NoPermission);
            if (!isNew && !CanEdit)
                return WifiPasswordSaveResult.Fail(WifiPasswordText.NoPermission);

            WifiPasswordSaveResult precheck = WifiPasswordRules.ValidatePassword(row, false);
            if (!precheck.Ok)
                return precheck;

            WifiPasswordSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    if (!isNew)
                    {
                        WifiPasswordRow existing = WifiPasswordRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = WifiPasswordSaveResult.Fail(WifiPasswordText.NotFound);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    bool dupName = WifiPasswordRepository.IsNameTakenIn(conn, tx, row.Name, excludeId);
                    WifiPasswordSaveResult dupCheck = WifiPasswordRules.ValidatePassword(row, dupName);
                    if (!dupCheck.Ok)
                    {
                        outcome = dupCheck;
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = WifiPasswordRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        WifiPasswordRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (WifiPasswordRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = WifiPasswordSaveResult.Fail(WifiPasswordText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới mật khẩu wifi: " : "Cập nhật mật khẩu wifi: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = WifiPasswordText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = WifiPasswordRules.ClipLogText(row.Name),
                            Note = WifiPasswordRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new WifiPasswordSaveResult
                    {
                        Ok = true,
                        SavedId = row.Id
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
            return outcome ?? WifiPasswordSaveResult.Fail("Không thể lưu mật khẩu wifi.");
        }

        public WifiPasswordDeleteResult Delete(string id, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(id))
                return WifiPasswordDeleteResult.Fail(WifiPasswordText.NotFound);
            if (!CanRemove)
                return WifiPasswordDeleteResult.Fail(WifiPasswordText.NoPermission);

            WifiPasswordDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                WifiPasswordRow existing = WifiPasswordRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = WifiPasswordDeleteResult.Fail(WifiPasswordText.NotFound);
                    return;
                }

                if (WifiPasswordRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = WifiPasswordDeleteResult.Fail(WifiPasswordText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = WifiPasswordText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = WifiPasswordRules.ClipLogText(existing.Name),
                        Note = WifiPasswordRules.ClipLogText("Xóa mật khẩu wifi: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new WifiPasswordDeleteResult { Ok = true };
            });

            return outcome ?? WifiPasswordDeleteResult.Fail("Không thể xóa mật khẩu wifi.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(WifiPasswordRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
