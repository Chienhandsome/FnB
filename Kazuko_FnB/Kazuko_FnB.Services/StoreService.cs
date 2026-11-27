using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục cửa hàng (DCUAHANG, docs/specs/danh-muc-cua-hang/):
    /// kiểm quyền SFUNCTION, kiểm bắt buộc Mã/Tên, độ dài (Mã 36, chữ 255), kiểm trùng Mã (UTF8) và Tên (UNICODE_CI_AI),
    /// giữ nguyên SIMAGEID khi sửa, xóa cứng chặn khi còn tham chiếu ở 4 bảng hoặc là cửa hàng cuối cùng (Q1),
    /// ghi lưu vết TLUUVET trong cùng transaction, và nạp lại cửa hàng + kho mặc định của phiên sau khi lưu/xóa (§2, §3).
    /// </summary>
    public sealed class StoreService
    {
        private readonly UserSession _session;
        private readonly StoreRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public StoreService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new StoreRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return StoreRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return StoreRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return StoreRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return StoreRules.Can(_session, RoleModes.Remove); } }

        public List<StoreRow> LoadAll()
        {
            if (!CanView)
                return new List<StoreRow>();
            return _repo.LoadAll();
        }

        public StoreRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<LookupItem> LoadActiveWarehouses()
        {
            return _repo.LoadActiveWarehouses();
        }

        public List<LookupItem> LoadInvoiceTemplates()
        {
            return _repo.LoadInvoiceTemplates();
        }

        public List<ReferenceBlock> CheckReferences(string id)
        {
            return _repo.CheckReferences(id);
        }

        public StoreSaveResult Save(StoreRow row)
        {
            if (row == null)
                return StoreSaveResult.Fail("Không có dữ liệu cửa hàng.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return StoreSaveResult.Fail(StoreText.NoPermission);
            if (!isNew && !CanEdit)
                return StoreSaveResult.Fail(StoreText.NoPermission);

            StoreSaveResult precheck = StoreRules.Validate(row, false, false);
            if (!precheck.Ok)
                return precheck;

            StoreSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    StoreRow existing = null;
                    if (!isNew)
                    {
                        existing = StoreRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = StoreSaveResult.Fail(StoreText.NotFound);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(row.WarehouseId))
                    {
                        bool keepOldWarehouse = existing != null
                            && string.Equals(existing.WarehouseId, row.WarehouseId, StringComparison.Ordinal);
                        if (!keepOldWarehouse && !StoreRepository.WarehouseExistsIn(conn, tx, row.WarehouseId))
                        {
                            outcome = StoreSaveResult.Fail(StoreText.WarehouseInvalid);
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(row.TemplateId))
                    {
                        bool keepOldTemplate = existing != null
                            && string.Equals(existing.TemplateId, row.TemplateId, StringComparison.Ordinal);
                        if (!keepOldTemplate && !StoreRepository.InvoiceTemplateValidIn(conn, tx, row.TemplateId))
                        {
                            outcome = StoreSaveResult.Fail(StoreText.TemplateInvalid);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (StoreRepository.IsCodeTakenIn(conn, tx, row.Code, excludeId))
                    {
                        outcome = StoreSaveResult.Fail(StoreText.CodeDuplicate(row.Code), true, false);
                        return;
                    }
                    if (StoreRepository.IsNameTakenIn(conn, tx, row.Name, excludeId))
                    {
                        outcome = StoreSaveResult.Fail(StoreText.NameDuplicate(row.Name), false, true);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = StoreRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        StoreRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ImageId = existing.ImageId;
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (StoreRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = StoreSaveResult.Fail(StoreText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới cửa hàng: " : "Cập nhật cửa hàng: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = StoreText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = Clip(row.Name),
                            Note = Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new StoreSaveResult
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

            if (outcome != null && outcome.Ok)
                RefreshSessionStores();

            return outcome ?? StoreSaveResult.Fail("Không thể lưu cửa hàng.");
        }

        public StoreDeleteResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return StoreDeleteResult.Fail(StoreText.NotFound);
            if (!CanRemove)
                return StoreDeleteResult.Fail(StoreText.NoPermission);

            StoreDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                StoreRow existing = StoreRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = StoreDeleteResult.Fail(StoreText.NotFound);
                    return;
                }

                List<ReferenceBlock> blocks = StoreRepository.CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    outcome = new StoreDeleteResult
                    {
                        Ok = false,
                        ReferenceCount = totalRefs,
                        Blocks = blocks,
                        ErrorMessage = StoreRules.BuildReferenceBlockedMessage(existing.Name, blocks)
                    };
                    return;
                }

                // Q1 (Chiến chốt 26/11): chặn xóa cửa hàng cuối cùng.
                if (existing.Status == StoreRepository.ActiveStatus
                    && StoreRepository.CountActiveIn(conn, tx) <= 1)
                {
                    outcome = new StoreDeleteResult
                    {
                        Ok = false,
                        IsLastStore = true,
                        Blocks = blocks,
                        ErrorMessage = StoreText.LastStoreBlocked
                    };
                    return;
                }

                if (StoreRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = StoreDeleteResult.Fail(StoreText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = StoreText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Xóa cửa hàng: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new StoreDeleteResult
                {
                    Ok = true,
                    Blocks = blocks
                };
            });

            if (outcome != null && outcome.Ok)
                RefreshSessionStores();

            return outcome ?? StoreDeleteResult.Fail("Không thể xóa cửa hàng.");
        }

        /// <summary>
        /// Nạp lại danh sách cửa hàng, cửa hàng mặc định và kho mặc định của phiên theo nghiep-vu.md §2–§3.
        /// Không đổi luật chọn cửa hàng mặc định (Admin: đầu tiên theo SORTORDER; người khác: dòng đầu TNGUOIDUNGTHEOCUAHANG).
        /// </summary>
        public void RefreshSessionStores()
        {
            RefreshSession(_session, _repo);
            UserSession current = AppSession.Current;
            if (current != null
                && !ReferenceEquals(current, _session)
                && string.Equals(current.DatabasePath, _session.DatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                RefreshSession(current, _repo);
            }
        }

        private static void RefreshSession(UserSession session, StoreRepository repo)
        {
            if (session == null || repo == null)
                return;
            List<StoreRecord> stores = repo.LoadSessionStores(session.UserId, session.IsAdmin);
            session.Stores = stores;
            session.CurrentStore = stores.Count > 0 ? stores[0] : null;
            session.CurrentWarehouseId = session.CurrentStore != null ? session.CurrentStore.DefaultWarehouseId : null;
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(StoreRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }

        /// <summary>Cắt chuỗi ghi lưu vết cho vừa cột VARCHAR(255) (tên cửa hàng dài tối đa 255 ký tự).</summary>
        private static string Clip(string text)
        {
            text = text ?? "";
            return text.Length > 255 ? text.Substring(0, 255) : text;
        }
    }
}
