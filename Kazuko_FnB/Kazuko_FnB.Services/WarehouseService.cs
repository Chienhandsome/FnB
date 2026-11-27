using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục kho hàng (DKHOHANG, docs/specs/danh-muc-kho-hang/):
    /// kiểm quyền theo SFUNCTION, kiểm bắt buộc Tên &amp; Cửa hàng, kiểm trùng Tên (UNICODE_CI_AI),
    /// lưu CHOPHEPAMKHO = 0 khi tạo mới và giữ nguyên khi sửa (Q2), xóa cứng chỉ khi 6 cột tham chiếu
    /// không còn dòng nào và không phải kho cuối cùng (Q1), ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class WarehouseService
    {
        private readonly UserSession _session;
        private readonly WarehouseRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public WarehouseService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new WarehouseRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return WarehouseRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return WarehouseRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return WarehouseRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return WarehouseRules.Can(_session, RoleModes.Remove); } }

        public List<WarehouseRow> LoadAll()
        {
            if (!CanView)
                return new List<WarehouseRow>();
            return _repo.LoadAll();
        }

        public WarehouseRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<ReferenceBlock> CheckReferences(string id)
        {
            return _repo.CheckReferences(id);
        }

        public WarehouseSaveResult Save(WarehouseRow row)
        {
            if (row == null)
                return WarehouseSaveResult.Fail("Không có dữ liệu kho hàng.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return WarehouseSaveResult.Fail(WarehouseText.NoPermission);
            if (!isNew && !CanEdit)
                return WarehouseSaveResult.Fail(WarehouseText.NoPermission);

            WarehouseSaveResult precheck = WarehouseRules.Validate(row, false);
            if (!precheck.Ok)
                return precheck;

            WarehouseSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    WarehouseRow existing = null;
                    if (!isNew)
                    {
                        existing = WarehouseRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = WarehouseSaveResult.Fail(WarehouseText.NotFound);
                            return;
                        }
                    }

                    if (!WarehouseRepository.StoreExistsIn(conn, tx, row.StoreId))
                    {
                        outcome = WarehouseSaveResult.Fail(WarehouseText.StoreEmpty, false, true);
                        return;
                    }

                    if (WarehouseRepository.IsNameTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = WarehouseSaveResult.Fail(WarehouseText.NameDuplicate(row.Name), true, false);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = WarehouseRepository.ActiveStatus;
                        row.AllowNegativeStock = WarehouseRules.AllowNegativeOnCreate();
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        WarehouseRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.AllowNegativeStock = WarehouseRules.AllowNegativeOnEdit(existing.AllowNegativeStock);
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (WarehouseRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = WarehouseSaveResult.Fail(WarehouseText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới kho hàng: " : "Cập nhật kho hàng: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = WarehouseText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = Clip(row.Name),
                            Note = Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new WarehouseSaveResult
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
            return outcome ?? WarehouseSaveResult.Fail("Không thể lưu kho hàng.");
        }

        public WarehouseDeleteResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return WarehouseDeleteResult.Fail(WarehouseText.NotFound);
            if (!CanRemove)
                return WarehouseDeleteResult.Fail(WarehouseText.NoPermission);

            WarehouseDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                WarehouseRow existing = WarehouseRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = WarehouseDeleteResult.Fail(WarehouseText.NotFound);
                    return;
                }

                List<ReferenceBlock> blocks = WarehouseRepository.CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    outcome = new WarehouseDeleteResult
                    {
                        Ok = false,
                        ReferenceCount = totalRefs,
                        Blocks = blocks,
                        ErrorMessage = ReferenceChecker.BlockedMessage("kho hàng", existing.Name, totalRefs)
                    };
                    return;
                }

                // Q1 (Chiến chốt 26/11): chặn xóa kho hàng cuối cùng.
                if (existing.Status == WarehouseRepository.ActiveStatus
                    && WarehouseRepository.CountActiveIn(conn, tx) <= 1)
                {
                    outcome = new WarehouseDeleteResult
                    {
                        Ok = false,
                        IsLastWarehouse = true,
                        Blocks = blocks,
                        ErrorMessage = WarehouseText.LastWarehouseBlocked
                    };
                    return;
                }

                if (WarehouseRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = WarehouseDeleteResult.Fail(WarehouseText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = WarehouseText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Xóa kho hàng: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new WarehouseDeleteResult
                {
                    Ok = true,
                    Blocks = blocks
                };
            });

            return outcome ?? WarehouseDeleteResult.Fail("Không thể xóa kho hàng.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(WarehouseRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }

        /// <summary>Cắt chuỗi ghi lưu vết cho vừa cột VARCHAR(255) (tên kho dài tối đa 255 ký tự).</summary>
        private static string Clip(string text)
        {
            text = text ?? "";
            return text.Length > 255 ? text.Substring(0, 255) : text;
        }
    }
}
