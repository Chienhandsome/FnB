using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho nhóm nhà cung cấp (DNHOMNHACUNGCAP, docs/specs/danh-muc-nha-cung-cap/):
    /// dùng chung quyền SFUNCTION với Danh mục nhà cung cấp, kiểm bắt buộc &amp; không trùng tên nhóm
    /// (UNICODE_CI_AI), xóa cứng chỉ khi không còn nhà cung cấp thuộc nhóm và không còn nhóm con,
    /// ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class SupplierGroupService
    {
        private readonly UserSession _session;
        private readonly SupplierGroupRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public SupplierGroupService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new SupplierGroupRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return SupplierRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return SupplierRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return SupplierRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return SupplierRules.Can(_session, RoleModes.Remove); } }

        public List<SupplierGroupRow> LoadAll()
        {
            if (!CanView)
                return new List<SupplierGroupRow>();
            return _repo.LoadAll();
        }

        public SupplierGroupRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public int CountSuppliers(string groupId)
        {
            return _repo.CountSuppliers(groupId);
        }

        public int CountChildren(string groupId)
        {
            return _repo.CountChildren(groupId);
        }

        public SupplierGroupSaveResult Save(SupplierGroupRow row)
        {
            if (row == null)
                return SupplierGroupSaveResult.Fail("Không có dữ liệu nhóm nhà cung cấp.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return SupplierGroupSaveResult.Fail(SupplierText.NoPermission);
            if (!isNew && !CanEdit)
                return SupplierGroupSaveResult.Fail(SupplierText.NoPermission);

            SupplierGroupSaveResult precheck = SupplierRules.ValidateGroup(row, false);
            if (!precheck.Ok)
                return precheck;

            SupplierGroupSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    if (!isNew)
                    {
                        SupplierGroupRow existing = SupplierGroupRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = SupplierGroupSaveResult.Fail(SupplierText.GroupNotFound);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (SupplierGroupRepository.IsNameTakenIn(conn, tx, row.Name, excludeId))
                    {
                        outcome = SupplierGroupSaveResult.Fail(SupplierText.GroupNameDuplicate(row.Name));
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = SupplierGroupRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        row.ItemType = 0;
                        row.AutoId = SupplierGroupRepository.NextAutoIdIn(conn, tx);
                        string parentId = string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId;
                        row.ParentId = parentId;
                        if (parentId.Length == 0)
                        {
                            row.ParentDir = "";
                            row.SortOrder = "ZZZZ";
                        }
                        else
                        {
                            SupplierGroupRow parent = SupplierGroupRepository.LoadByIdIn(conn, tx, parentId);
                            row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                            row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                        }
                        SupplierGroupRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (SupplierGroupRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = SupplierGroupSaveResult.Fail(SupplierText.GroupNotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhóm nhà cung cấp: " : "Cập nhật nhóm nhà cung cấp: ")
                        + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = SupplierText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = SupplierRules.ClipLogText(row.Name),
                            Note = SupplierRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new SupplierGroupSaveResult
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
            return outcome ?? SupplierGroupSaveResult.Fail("Không thể lưu nhóm nhà cung cấp.");
        }

        public SupplierGroupDeleteResult Delete(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return SupplierGroupDeleteResult.Fail(SupplierText.GroupNotFound);
            if (!CanRemove)
                return SupplierGroupDeleteResult.Fail(SupplierText.NoPermission);

            SupplierGroupDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                SupplierGroupRow existing = SupplierGroupRepository.LoadByIdIn(conn, tx, groupId);
                if (existing == null)
                {
                    outcome = SupplierGroupDeleteResult.Fail(SupplierText.GroupNotFound);
                    return;
                }

                int suppliers = SupplierGroupRepository.CountSuppliersIn(conn, tx, groupId);
                if (suppliers > 0)
                {
                    outcome = new SupplierGroupDeleteResult
                    {
                        Ok = false,
                        SupplierCount = suppliers,
                        ErrorMessage = ReferenceChecker.BlockedMessage("nhóm nhà cung cấp", existing.Name, suppliers)
                    };
                    return;
                }

                int children = SupplierGroupRepository.CountChildrenIn(conn, tx, groupId);
                if (children > 0)
                {
                    outcome = new SupplierGroupDeleteResult
                    {
                        Ok = false,
                        ChildGroupCount = children,
                        ErrorMessage = ReferenceChecker.BlockedMessage("nhóm nhà cung cấp", existing.Name, children)
                    };
                    return;
                }

                if (SupplierGroupRepository.DeleteIn(conn, tx, groupId) == 0)
                {
                    outcome = SupplierGroupDeleteResult.Fail(SupplierText.GroupNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = SupplierText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = SupplierRules.ClipLogText(existing.Name),
                        Note = SupplierRules.ClipLogText("Xóa nhóm nhà cung cấp: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new SupplierGroupDeleteResult { Ok = true };
            });

            return outcome ?? SupplierGroupDeleteResult.Fail("Không thể xóa nhóm nhà cung cấp.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(SupplierGroupRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
