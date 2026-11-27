using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho nhóm wifi (DNHOMWIFI, spec docs/specs/quan-tri-nho.md §3):
    /// dùng chung quyền SFUNCTION với Danh mục mật khẩu wifi, kiểm bắt buộc &amp; không trùng tên nhóm
    /// trong cùng nhóm cha (UNICODE_CI_AI), xóa cứng chỉ khi không còn mật khẩu thuộc nhóm và không còn nhóm con,
    /// ghi lưu vết TLUUVET trong cùng transaction (cắt ngắn 255 ký tự tránh tràn cột).
    /// </summary>
    public sealed class WifiGroupService
    {
        private readonly UserSession _session;
        private readonly WifiGroupRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public WifiGroupService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new WifiGroupRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return WifiPasswordRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return WifiPasswordRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return WifiPasswordRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return WifiPasswordRules.Can(_session, RoleModes.Remove); } }

        public List<WifiGroupRow> LoadAll()
        {
            if (!CanView)
                return new List<WifiGroupRow>();
            return _repo.LoadAll();
        }

        public WifiGroupRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public int CountPasswords(string groupId)
        {
            return _repo.CountPasswords(groupId);
        }

        public int CountChildren(string groupId)
        {
            return _repo.CountChildren(groupId);
        }

        public WifiGroupSaveResult Save(WifiGroupRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return WifiGroupSaveResult.Fail("Không có dữ liệu nhóm wifi.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return WifiGroupSaveResult.Fail(WifiPasswordText.NoPermission);
            if (!isNew && !CanEdit)
                return WifiGroupSaveResult.Fail(WifiPasswordText.NoPermission);

            WifiGroupSaveResult precheck = WifiPasswordRules.ValidateGroup(row, false);
            if (!precheck.Ok)
                return precheck;

            WifiGroupSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    string effectiveParentId = row.ParentId ?? "";
                    if (!isNew)
                    {
                        WifiGroupRow existing = WifiGroupRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = WifiGroupSaveResult.Fail(WifiPasswordText.GroupNotFound);
                            return;
                        }
                        effectiveParentId = existing.ParentId ?? "";
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (WifiGroupRepository.IsNameTakenIn(conn, tx, row.Name, effectiveParentId, excludeId))
                    {
                        outcome = WifiGroupSaveResult.Fail(WifiPasswordText.GroupNameDuplicate(row.Name));
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = WifiGroupRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        row.ItemType = 0;
                        row.AutoId = WifiGroupRepository.NextAutoIdIn(conn, tx);
                        row.ParentId = effectiveParentId;
                        if (effectiveParentId.Length == 0)
                        {
                            row.ParentDir = "";
                            row.SortOrder = "ZZZZ";
                        }
                        else
                        {
                            WifiGroupRow parent = WifiGroupRepository.LoadByIdIn(conn, tx, effectiveParentId);
                            row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                            row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                        }
                        WifiGroupRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (WifiGroupRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = WifiGroupSaveResult.Fail(WifiPasswordText.GroupNotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhóm wifi: " : "Cập nhật nhóm wifi: ") + (row.Name ?? "");
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

                    outcome = new WifiGroupSaveResult
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
            return outcome ?? WifiGroupSaveResult.Fail("Không thể lưu nhóm wifi.");
        }

        public WifiGroupDeleteResult Delete(string groupId, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return WifiGroupDeleteResult.Fail(WifiPasswordText.GroupNotFound);
            if (!CanRemove)
                return WifiGroupDeleteResult.Fail(WifiPasswordText.NoPermission);

            WifiGroupDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                WifiGroupRow existing = WifiGroupRepository.LoadByIdIn(conn, tx, groupId);
                if (existing == null)
                {
                    outcome = WifiGroupDeleteResult.Fail(WifiPasswordText.GroupNotFound);
                    return;
                }

                int passwords = WifiGroupRepository.CountPasswordsIn(conn, tx, groupId);
                if (passwords > 0)
                {
                    outcome = new WifiGroupDeleteResult
                    {
                        Ok = false,
                        PasswordCount = passwords,
                        ErrorMessage = WifiPasswordText.GroupHasPasswords(existing.Name, passwords)
                    };
                    return;
                }

                int children = WifiGroupRepository.CountChildrenIn(conn, tx, groupId);
                if (children > 0)
                {
                    outcome = new WifiGroupDeleteResult
                    {
                        Ok = false,
                        ChildGroupCount = children,
                        ErrorMessage = WifiPasswordText.GroupHasChildren(existing.Name, children)
                    };
                    return;
                }

                if (WifiGroupRepository.DeleteIn(conn, tx, groupId) == 0)
                {
                    outcome = WifiGroupDeleteResult.Fail(WifiPasswordText.GroupNotFound);
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
                        Note = WifiPasswordRules.ClipLogText("Xóa nhóm wifi: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new WifiGroupDeleteResult { Ok = true };
            });

            return outcome ?? WifiGroupDeleteResult.Fail("Không thể xóa nhóm wifi.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(WifiGroupRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
