using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Phần dịch vụ dạng cây, phân cách, xóa mềm và Thùng rác của <see cref="EmployeeService"/>
    /// (spec docs/specs/nhan-su-cay.md §1–§4).
    /// </summary>
    public sealed partial class EmployeeService
    {
        public List<EmployeeRow> LoadActiveFolders()
        {
            if (!CanView)
                return new List<EmployeeRow>();
            return _repo.LoadActiveFolders();
        }

        public List<EmployeeRow> LoadActiveGridItems()
        {
            if (!CanView)
                return new List<EmployeeRow>();
            return _repo.LoadActiveGridItems();
        }

        public List<EmployeeRow> LoadTrash()
        {
            if (!CanRemove)
                return new List<EmployeeRow>();
            return _repo.LoadTrashItems();
        }

        /// <summary>Thêm mới hoặc đổi tên thư mục nhân viên (ITEMTYPE = 1).</summary>
        public EmployeeSaveResult SaveFolder(EmployeeRow folder)
        {
            if (folder == null)
                return EmployeeSaveResult.Fail("Không có dữ liệu thư mục.");

            bool isNew = string.IsNullOrEmpty(folder.Id);
            if (isNew && !CanAdd)
                return EmployeeSaveResult.Fail(EmployeeText.NoPermission);
            if (!isNew && !CanEdit)
                return EmployeeSaveResult.Fail(EmployeeText.NoPermission);

            EmployeeSaveResult precheck = EmployeeRules.ValidateFolder(folder.Name, false);
            if (!precheck.Ok)
                return precheck;

            EmployeeSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    string effectiveParentId = string.IsNullOrEmpty(folder.ParentId) ? "" : folder.ParentId;
                    string parentDir = "";

                    if (!isNew)
                    {
                        EmployeeRow existing = EmployeeRepository.LoadByIdIn(conn, tx, folder.Id);
                        if (existing == null || !existing.IsFolder || existing.Status != EmployeeRepository.ActiveStatus)
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.FolderNotFound);
                            return;
                        }
                        effectiveParentId = existing.ParentId ?? "";
                    }
                    else if (effectiveParentId.Length > 0)
                    {
                        EmployeeRow parent = EmployeeRepository.LoadByIdIn(conn, tx, effectiveParentId);
                        if (parent == null || !parent.IsFolder || parent.Status != EmployeeRepository.ActiveStatus)
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.ParentFolderGone);
                            return;
                        }
                        parentDir = EmployeeRepository.BuildParentDir(parent);
                    }

                    string excludeId = isNew ? "" : folder.Id;
                    if (EmployeeRepository.IsFolderNameTakenIn(conn, tx, folder.Name, effectiveParentId, excludeId))
                    {
                        outcome = EmployeeRules.ValidateFolder(folder.Name, true);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        folder.Id = DbHelper.NewId();
                        assignedNewId = true;
                        folder.ItemType = EmployeeItemType.Folder;
                        folder.ParentId = effectiveParentId;
                        folder.ParentDir = parentDir;
                        folder.AutoId = EmployeeRepository.NextAutoIdIn(conn, tx);
                        folder.SortOrder = EmployeeRepository.NextSortOrderIn(conn, tx, effectiveParentId);
                        folder.Status = EmployeeRepository.ActiveStatus;
                        folder.CreatedById = _session.UserId ?? "";
                        folder.CreatedAt = serverNow;
                        folder.ModifiedById = null;
                        folder.ModifiedAt = null;
                        EmployeeRepository.InsertTreeItemIn(conn, tx, folder);
                    }
                    else
                    {
                        folder.ModifiedById = _session.UserId ?? "";
                        folder.ModifiedAt = serverNow;
                        if (EmployeeRepository.UpdateFolderNameIn(conn, tx, folder) == 0)
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.FolderNotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới thư mục nhân viên: " : "Cập nhật thư mục nhân viên: ") + (folder.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = EmployeeText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = folder.Id,
                            TableName = Clip(folder.Name),
                            Note = Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new EmployeeSaveResult { Ok = true, SavedId = folder.Id };
                });
            }
            catch
            {
                if (assignedNewId)
                    ResetNew(folder);
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                ResetNew(folder);
            return outcome ?? EmployeeSaveResult.Fail("Không thể lưu thư mục nhân viên.");
        }

        /// <summary>Thêm dòng phân cách (ITEMTYPE = 2, NAME = "___________") vào thư mục đang chọn (hoặc gốc).</summary>
        public EmployeeSaveResult AddSeparator(string parentId)
        {
            if (!CanAdd)
                return EmployeeSaveResult.Fail(EmployeeText.NoPermission);

            EmployeeSaveResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                string effectiveParentId = string.IsNullOrEmpty(parentId) ? "" : parentId;
                string parentDir = "";
                if (effectiveParentId.Length > 0)
                {
                    EmployeeRow parent = EmployeeRepository.LoadByIdIn(conn, tx, effectiveParentId);
                    if (parent == null || !parent.IsFolder || parent.Status != EmployeeRepository.ActiveStatus)
                    {
                        outcome = EmployeeSaveResult.Fail(EmployeeText.ParentFolderGone);
                        return;
                    }
                    parentDir = EmployeeRepository.BuildParentDir(parent);
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                var row = new EmployeeRow
                {
                    Id = DbHelper.NewId(),
                    Name = EmployeeText.SeparatorName,
                    ItemType = EmployeeItemType.Separator,
                    ParentId = effectiveParentId,
                    ParentDir = parentDir,
                    AutoId = EmployeeRepository.NextAutoIdIn(conn, tx),
                    SortOrder = EmployeeRepository.NextSortOrderIn(conn, tx, effectiveParentId),
                    Status = EmployeeRepository.ActiveStatus,
                    CreatedById = _session.UserId ?? "",
                    CreatedAt = serverNow
                };
                EmployeeRepository.InsertTreeItemIn(conn, tx, row);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = EmployeeText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = row.Id,
                        TableName = EmployeeText.SeparatorName,
                        Note = "Thêm phân cách danh mục nhân viên",
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new EmployeeSaveResult { Ok = true, SavedId = row.Id };
            });

            return outcome ?? EmployeeSaveResult.Fail("Không thể thêm phân cách.");
        }

        /// <summary>
        /// Xóa mềm (chuyển vào Thùng rác, STATUS = 0, không đổi cột khác — spec §3):
        /// nhân viên và phân cách luôn chuyển được kể cả khi đang được dùng ở bảng khác;
        /// thư mục chỉ chuyển được khi rỗng (không còn mục STATUS = 30 bên trong, kể cả thư mục con).
        /// </summary>
        public EmployeeDeleteResult SoftDelete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return EmployeeDeleteResult.Fail(EmployeeText.ItemNotFound);
            if (!CanRemove)
                return EmployeeDeleteResult.Fail(EmployeeText.NoPermission);

            EmployeeDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                EmployeeRow existing = EmployeeRepository.LoadByIdIn(conn, tx, id);
                if (existing == null || existing.Status != EmployeeRepository.ActiveStatus)
                {
                    outcome = EmployeeDeleteResult.Fail(EmployeeText.ItemNotFound);
                    return;
                }

                if (existing.IsFolder)
                {
                    int activeChildren = EmployeeRepository.CountActiveDescendantsIn(conn, tx, id);
                    if (activeChildren > 0)
                    {
                        outcome = new EmployeeDeleteResult
                        {
                            Ok = false,
                            ReferenceCount = activeChildren,
                            ErrorMessage = EmployeeText.FolderNotEmpty(existing.Name, activeChildren)
                        };
                        return;
                    }
                }

                if (EmployeeRepository.SoftDeleteIn(conn, tx, id) == 0)
                {
                    outcome = EmployeeDeleteResult.Fail(EmployeeText.ItemNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = EmployeeText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Chuyển vào thùng rác (" + EmployeeText.ItemTypeDisplayText(existing.ItemType) + "): " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new EmployeeDeleteResult { Ok = true };
            });

            return outcome ?? EmployeeDeleteResult.Fail("Không thể chuyển vào thùng rác.");
        }

        /// <summary>
        /// Khôi phục mục từ Thùng rác (STATUS = 0 -> 30, quyền Sửa — spec §3, §4):
        /// giữ vị trí cũ nếu thư mục cha còn hoạt động, ngược lại khôi phục lên gốc;
        /// chặn nếu trùng Tên/Mã với nhân viên đang dùng (hoặc trùng tên thư mục anh em cùng cha).
        /// </summary>
        public EmployeeRestoreResult Restore(string id)
        {
            if (string.IsNullOrEmpty(id))
                return EmployeeRestoreResult.Fail(EmployeeText.ItemNotFound);
            if (!CanEdit)
                return EmployeeRestoreResult.Fail(EmployeeText.NoPermission);

            EmployeeRestoreResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                EmployeeRow existing = EmployeeRepository.LoadByIdIn(conn, tx, id);
                if (existing == null || existing.Status != EmployeeRepository.TrashStatus)
                {
                    outcome = EmployeeRestoreResult.Fail(EmployeeText.ItemNotFound);
                    return;
                }

                string targetParentId = "";
                string targetParentDir = "";
                bool restoredToRoot = false;
                string origParentId = existing.ParentId ?? "";
                if (origParentId.Length > 0)
                {
                    EmployeeRow parent = EmployeeRepository.LoadByIdIn(conn, tx, origParentId);
                    if (parent != null && parent.IsFolder && parent.Status == EmployeeRepository.ActiveStatus)
                    {
                        targetParentId = parent.Id;
                        targetParentDir = EmployeeRepository.BuildParentDir(parent);
                    }
                    else
                    {
                        restoredToRoot = true;
                    }
                }

                if (existing.IsEmployee)
                {
                    if (EmployeeRepository.IsNameTakenIn(conn, tx, existing.Name, existing.Id))
                    {
                        outcome = EmployeeRestoreResult.Fail(EmployeeText.NameDuplicate(existing.Name));
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(existing.Code)
                        && EmployeeRepository.IsCodeTakenIn(conn, tx, existing.Code, existing.Id))
                    {
                        outcome = EmployeeRestoreResult.Fail(EmployeeText.CodeDuplicate(existing.Code));
                        return;
                    }
                }
                else if (existing.IsFolder)
                {
                    if (EmployeeRepository.IsFolderNameTakenIn(conn, tx, existing.Name, targetParentId, existing.Id))
                    {
                        outcome = EmployeeRestoreResult.Fail(EmployeeText.FolderNameDuplicate(existing.Name));
                        return;
                    }
                }

                if (EmployeeRepository.RestoreIn(conn, tx, id, targetParentId, targetParentDir) == 0)
                {
                    outcome = EmployeeRestoreResult.Fail(EmployeeText.ItemNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = EmployeeText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Khôi phục từ thùng rác: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new EmployeeRestoreResult
                {
                    Ok = true,
                    RestoredToRoot = restoredToRoot
                };
            });

            return outcome ?? EmployeeRestoreResult.Fail("Không thể khôi phục dữ liệu.");
        }
    }
}
