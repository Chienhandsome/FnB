using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho nhóm voucher (DNHOMVOUCHER, spec docs/specs/danh-muc-voucher.md §2):
    /// dùng chung quyền SFUNCTION với Danh mục voucher, kiểm bắt buộc &amp; không trùng tên nhóm
    /// trong cùng nhóm cha (UNICODE_CI_AI), xóa cứng chỉ khi không còn voucher thuộc nhóm và không còn nhóm con,
    /// ghi lưu vết TLUUVET trong cùng transaction (cắt ngắn 255 ký tự tránh tràn cột).
    /// </summary>
    public sealed class GiftVoucherGroupService
    {
        private readonly UserSession _session;
        private readonly GiftVoucherGroupRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public GiftVoucherGroupService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new GiftVoucherGroupRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return GiftVoucherRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return GiftVoucherRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return GiftVoucherRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return GiftVoucherRules.Can(_session, RoleModes.Remove); } }

        public List<GiftVoucherGroupRow> LoadAll()
        {
            if (!CanView)
                return new List<GiftVoucherGroupRow>();
            return _repo.LoadAll();
        }

        public GiftVoucherGroupRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public int CountVouchers(string groupId)
        {
            return _repo.CountVouchers(groupId);
        }

        public int CountChildren(string groupId)
        {
            return _repo.CountChildren(groupId);
        }

        public GiftVoucherGroupSaveResult Save(GiftVoucherGroupRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return GiftVoucherGroupSaveResult.Fail("Không có dữ liệu nhóm voucher.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return GiftVoucherGroupSaveResult.Fail(GiftVoucherText.NoPermission);
            if (!isNew && !CanEdit)
                return GiftVoucherGroupSaveResult.Fail(GiftVoucherText.NoPermission);

            GiftVoucherGroupSaveResult precheck = GiftVoucherRules.ValidateGroup(row, false);
            if (!precheck.Ok)
                return precheck;

            GiftVoucherGroupSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    string effectiveParentId = row.ParentId ?? "";
                    if (!isNew)
                    {
                        GiftVoucherGroupRow existing = GiftVoucherGroupRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = GiftVoucherGroupSaveResult.Fail(GiftVoucherText.GroupNotFound);
                            return;
                        }
                        effectiveParentId = existing.ParentId ?? "";
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (GiftVoucherGroupRepository.IsNameTakenIn(conn, tx, row.Name, effectiveParentId, excludeId))
                    {
                        outcome = GiftVoucherGroupSaveResult.Fail(GiftVoucherText.GroupNameDuplicate(row.Name));
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = GiftVoucherGroupRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        row.ItemType = 0;
                        row.AutoId = GiftVoucherGroupRepository.NextAutoIdIn(conn, tx);
                        row.ParentId = effectiveParentId;
                        if (effectiveParentId.Length == 0)
                        {
                            row.ParentDir = "";
                            row.SortOrder = "ZZZZ";
                        }
                        else
                        {
                            GiftVoucherGroupRow parent = GiftVoucherGroupRepository.LoadByIdIn(conn, tx, effectiveParentId);
                            row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                            row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                        }
                        GiftVoucherGroupRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (GiftVoucherGroupRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = GiftVoucherGroupSaveResult.Fail(GiftVoucherText.GroupNotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhóm voucher: " : "Cập nhật nhóm voucher: ")
                        + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = GiftVoucherText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = GiftVoucherRules.ClipLogText(row.Name),
                            Note = GiftVoucherRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new GiftVoucherGroupSaveResult
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
            return outcome ?? GiftVoucherGroupSaveResult.Fail("Không thể lưu nhóm voucher.");
        }

        public GiftVoucherGroupDeleteResult Delete(string groupId, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return GiftVoucherGroupDeleteResult.Fail(GiftVoucherText.GroupNotFound);
            if (!CanRemove)
                return GiftVoucherGroupDeleteResult.Fail(GiftVoucherText.NoPermission);

            GiftVoucherGroupDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                GiftVoucherGroupRow existing = GiftVoucherGroupRepository.LoadByIdIn(conn, tx, groupId);
                if (existing == null)
                {
                    outcome = GiftVoucherGroupDeleteResult.Fail(GiftVoucherText.GroupNotFound);
                    return;
                }

                int vouchers = GiftVoucherGroupRepository.CountVouchersIn(conn, tx, groupId);
                if (vouchers > 0)
                {
                    outcome = new GiftVoucherGroupDeleteResult
                    {
                        Ok = false,
                        VoucherCount = vouchers,
                        ErrorMessage = GiftVoucherText.GroupHasVouchers(existing.Name, vouchers)
                    };
                    return;
                }

                int children = GiftVoucherGroupRepository.CountChildrenIn(conn, tx, groupId);
                if (children > 0)
                {
                    outcome = new GiftVoucherGroupDeleteResult
                    {
                        Ok = false,
                        ChildGroupCount = children,
                        ErrorMessage = GiftVoucherText.GroupHasChildren(existing.Name, children)
                    };
                    return;
                }

                if (GiftVoucherGroupRepository.DeleteIn(conn, tx, groupId) == 0)
                {
                    outcome = GiftVoucherGroupDeleteResult.Fail(GiftVoucherText.GroupNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = GiftVoucherText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = GiftVoucherRules.ClipLogText(existing.Name),
                        Note = GiftVoucherRules.ClipLogText("Xóa nhóm voucher: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new GiftVoucherGroupDeleteResult { Ok = true };
            });

            return outcome ?? GiftVoucherGroupDeleteResult.Fail("Không thể xóa nhóm voucher.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(GiftVoucherGroupRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
