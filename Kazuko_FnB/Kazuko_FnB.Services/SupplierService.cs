using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục nhà cung cấp (DNHACUNGCAP, docs/specs/danh-muc-nha-cung-cap/):
    /// kiểm quyền theo SFUNCTION, sinh mã tự động NCC + 5 chữ số, kiểm bắt buộc Tên &amp; Mã,
    /// kiểm trùng Tên &amp; Mã theo UNICODE_CI_AI trong cùng transaction, xóa cứng chỉ khi không còn
    /// tham chiếu ở TDONHANG và TTHUCHI (báo số tham chiếu), ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class SupplierService
    {
        private readonly UserSession _session;
        private readonly SupplierRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public SupplierService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new SupplierRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return SupplierRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return SupplierRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return SupplierRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return SupplierRules.Can(_session, RoleModes.Remove); } }

        public List<SupplierRow> LoadAll()
        {
            if (!CanView)
                return new List<SupplierRow>();
            return _repo.LoadAll();
        }

        public SupplierRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<SupplierLookupItem> LoadLookup()
        {
            return _repo.LoadLookup();
        }

        public string NextCode()
        {
            return _repo.NextCode();
        }

        public List<ReferenceBlock> CheckReferences(string id)
        {
            return _repo.CheckReferences(id);
        }

        public SupplierSaveResult Save(SupplierRow row)
        {
            if (row == null)
                return SupplierSaveResult.Fail("Không có dữ liệu nhà cung cấp.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return SupplierSaveResult.Fail(SupplierText.NoPermission);
            if (!isNew && !CanEdit)
                return SupplierSaveResult.Fail(SupplierText.NoPermission);

            SupplierSaveResult precheck = SupplierRules.ValidateSupplier(row, false, false);
            if (!precheck.Ok)
                return precheck;

            SupplierSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    if (!isNew)
                    {
                        SupplierRow existing = SupplierRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = SupplierSaveResult.Fail(SupplierText.NotFound);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    bool dupName = SupplierRepository.IsNameTakenIn(conn, tx, row.Name, excludeId);
                    bool dupCode = SupplierRepository.IsCodeTakenIn(conn, tx, row.Code, excludeId);
                    SupplierSaveResult dupCheck = SupplierRules.ValidateSupplier(row, dupName, dupCode);
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
                        row.Status = SupplierRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        SupplierRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (SupplierRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = SupplierSaveResult.Fail(SupplierText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhà cung cấp: " : "Cập nhật nhà cung cấp: ")
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

                    outcome = new SupplierSaveResult
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
            return outcome ?? SupplierSaveResult.Fail("Không thể lưu nhà cung cấp.");
        }

        public SupplierDeleteResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return SupplierDeleteResult.Fail(SupplierText.NotFound);
            if (!CanRemove)
                return SupplierDeleteResult.Fail(SupplierText.NoPermission);

            SupplierDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                SupplierRow existing = SupplierRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = SupplierDeleteResult.Fail(SupplierText.NotFound);
                    return;
                }

                List<ReferenceBlock> blocks = SupplierRepository.CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    outcome = new SupplierDeleteResult
                    {
                        Ok = false,
                        ReferenceCount = totalRefs,
                        Blocks = blocks,
                        ErrorMessage = ReferenceChecker.BlockedMessage("nhà cung cấp", existing.Name, totalRefs)
                    };
                    return;
                }

                if (SupplierRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = SupplierDeleteResult.Fail(SupplierText.NotFound);
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
                        Note = SupplierRules.ClipLogText("Xóa nhà cung cấp: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new SupplierDeleteResult
                {
                    Ok = true,
                    Blocks = blocks
                };
            });

            return outcome ?? SupplierDeleteResult.Fail("Không thể xóa nhà cung cấp.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(SupplierRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
