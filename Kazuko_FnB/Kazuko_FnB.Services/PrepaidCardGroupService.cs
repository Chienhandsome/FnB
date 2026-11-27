using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho nhóm thẻ trả trước (DNHOMTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2):
    /// dùng chung quyền SFUNCTION với Danh mục thẻ trả trước, kiểm bắt buộc &amp; không trùng tên nhóm
    /// trong cùng nhóm cha (UNICODE_CI_AI), xóa cứng chỉ khi không còn thẻ và không còn nhóm con,
    /// ghi lưu vết TLUUVET trong cùng transaction (cắt 255 ký tự).
    /// </summary>
    public sealed class PrepaidCardGroupService
    {
        private readonly UserSession _session;
        private readonly PrepaidCardGroupRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public PrepaidCardGroupService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new PrepaidCardGroupRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return PrepaidCardRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return PrepaidCardRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return PrepaidCardRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return PrepaidCardRules.Can(_session, RoleModes.Remove); } }

        public List<PrepaidCardGroupRow> LoadAll()
        {
            if (!CanView)
                return new List<PrepaidCardGroupRow>();
            return _repo.LoadAll();
        }

        public PrepaidCardGroupRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public PrepaidCardGroupSaveResult Save(PrepaidCardGroupRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return PrepaidCardGroupSaveResult.Fail("Không có dữ liệu nhóm thẻ trả trước.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return PrepaidCardGroupSaveResult.Fail(PrepaidCardText.NoPermission);
            if (!isNew && !CanEdit)
                return PrepaidCardGroupSaveResult.Fail(PrepaidCardText.NoPermission);

            PrepaidCardGroupSaveResult precheck = PrepaidCardRules.ValidateGroup(row, false);
            if (!precheck.Ok)
                return precheck;

            PrepaidCardGroupSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    string effectiveParentId = row.ParentId ?? "";
                    if (!isNew)
                    {
                        PrepaidCardGroupRow existing = PrepaidCardGroupRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = PrepaidCardGroupSaveResult.Fail(PrepaidCardText.GroupNotFound);
                            return;
                        }
                        effectiveParentId = existing.ParentId ?? "";
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (PrepaidCardGroupRepository.IsNameTakenIn(conn, tx, row.Name, effectiveParentId, excludeId))
                    {
                        outcome = PrepaidCardGroupSaveResult.Fail(PrepaidCardText.GroupNameDuplicate(row.Name));
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = PrepaidCardGroupRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        row.ItemType = 0;
                        row.AutoId = PrepaidCardGroupRepository.NextAutoIdIn(conn, tx);
                        row.ParentId = effectiveParentId;
                        if (effectiveParentId.Length == 0)
                        {
                            row.ParentDir = "";
                            row.SortOrder = "ZZZZ";
                        }
                        else
                        {
                            PrepaidCardGroupRow parent = PrepaidCardGroupRepository.LoadByIdIn(conn, tx, effectiveParentId);
                            row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                            row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                        }
                        PrepaidCardGroupRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (PrepaidCardGroupRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = PrepaidCardGroupSaveResult.Fail(PrepaidCardText.GroupNotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhóm thẻ trả trước: " : "Cập nhật nhóm thẻ trả trước: ")
                        + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = PrepaidCardText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = PrepaidCardRules.ClipLogText(row.Name),
                            Note = PrepaidCardRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new PrepaidCardGroupSaveResult
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
            return outcome ?? PrepaidCardGroupSaveResult.Fail("Không thể lưu nhóm thẻ trả trước.");
        }

        public PrepaidCardGroupDeleteResult Delete(string groupId, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return PrepaidCardGroupDeleteResult.Fail(PrepaidCardText.GroupNotFound);
            if (!CanRemove)
                return PrepaidCardGroupDeleteResult.Fail(PrepaidCardText.NoPermission);

            PrepaidCardGroupDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PrepaidCardGroupRow existing = PrepaidCardGroupRepository.LoadByIdIn(conn, tx, groupId);
                if (existing == null)
                {
                    outcome = PrepaidCardGroupDeleteResult.Fail(PrepaidCardText.GroupNotFound);
                    return;
                }

                int cards = PrepaidCardGroupRepository.CountCardsIn(conn, tx, groupId);
                if (cards > 0)
                {
                    outcome = new PrepaidCardGroupDeleteResult
                    {
                        Ok = false,
                        CardCount = cards,
                        ErrorMessage = PrepaidCardText.GroupHasCards(existing.Name, cards)
                    };
                    return;
                }

                int children = PrepaidCardGroupRepository.CountChildrenIn(conn, tx, groupId);
                if (children > 0)
                {
                    outcome = new PrepaidCardGroupDeleteResult
                    {
                        Ok = false,
                        ChildGroupCount = children,
                        ErrorMessage = PrepaidCardText.GroupHasChildren(existing.Name, children)
                    };
                    return;
                }

                if (PrepaidCardGroupRepository.DeleteIn(conn, tx, groupId) == 0)
                {
                    outcome = PrepaidCardGroupDeleteResult.Fail(PrepaidCardText.GroupNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = PrepaidCardText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = PrepaidCardRules.ClipLogText(existing.Name),
                        Note = PrepaidCardRules.ClipLogText("Xóa nhóm thẻ trả trước: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new PrepaidCardGroupDeleteResult { Ok = true };
            });

            return outcome ?? PrepaidCardGroupDeleteResult.Fail("Không thể xóa nhóm thẻ trả trước.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(PrepaidCardGroupRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
