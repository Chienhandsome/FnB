using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Kiểm kê kho (TDONHANG LOAI = 4 + TDONHANGCHITIET, docs/specs/kiem-ke-kho.md):
    /// - Một transaction duy nhất cho lưu/sửa/xóa kèm lưu vết TLUUVET (lỗi tự rollback).
    /// - Dùng lại hàm tồn chung <see cref="ServiceOrderRepository.GetStockBalanceIn"/> (loại trừ chính phiếu đang sửa)
    ///   và hàm quy đổi ĐVT <see cref="PurchaseRules.CalcConvertedQuantity"/>.
    /// - T2: tính lại tồn hệ thống ĐÚNG LÚC LƯU trong transaction; khác số người dùng đã thấy thì cập nhật lại và báo, không lưu.
    /// - T3: chặn sửa/xóa gây âm kho khi kho không bật cho phép âm kho.
    /// </summary>
    public sealed class StockTakeService
    {
        private readonly UserSession _session;
        private readonly StockTakeRepository _repo;
        private readonly InventoryRepository _inventoryRepo;
        private readonly ActivityLogger _activityLogger;

        public StockTakeService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new StockTakeRepository(session.DatabasePath);
            _inventoryRepo = new InventoryRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return StockTakeRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return StockTakeRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return StockTakeRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return StockTakeRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return StockTakeRules.CanChangeDate(_session); } }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(forDate);
        }

        public List<WarehouseRow> LoadAllowedWarehouses()
        {
            return _repo.Read(conn => PurchaseRepository.LoadAllowedWarehousesIn(
                conn, null, _session.UserId, _session.IsAdmin, SessionStoreIds()));
        }

        public string ResolveDefaultWarehouseId(IList<WarehouseRow> allowedWarehouses)
        {
            string storeId = _session.CurrentStore == null ? null : _session.CurrentStore.Id;
            string storeDefaultWh = string.IsNullOrEmpty(storeId)
                ? null
                : _inventoryRepo.GetStoreDefaultWarehouseId(storeId);
            return InventoryRules.ResolveDefaultWarehouseId(allowedWarehouses, storeId, storeDefaultWh);
        }

        public List<StockTakeProductItem> LoadProducts()
        {
            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            return _repo.LoadProducts(sortByCode);
        }

        public StockTakeRow LoadById(string id)
        {
            if (!CanView || string.IsNullOrEmpty(id))
                return null;
            return _repo.LoadById(id);
        }

        public List<StockTakeLine> LoadLines(string orderId)
        {
            if (!CanView || string.IsNullOrEmpty(orderId))
                return new List<StockTakeLine>();
            return _repo.LoadLines(orderId);
        }

        public List<StockTakeRow> LoadList(StockTakeFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<StockTakeRow>();

            if (!_session.IsAdmin && filter.AllowedWarehouseIds == null)
                filter.AllowedWarehouseIds = LoadAllowedWarehouses().Select(w => w.Id).ToList();

            List<StockTakeRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => StockTakeRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        /// <summary>Đọc tồn kho hiện tại bằng đúng hàm tồn dùng chung ServiceOrderRepository.GetStockBalanceIn.</summary>
        public decimal GetStockBalance(string productId, string warehouseId, string excludeOrderId = "")
        {
            return _repo.Read(conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, productId, warehouseId, excludeOrderId));
        }

        /// <summary>Danh sách mặt hàng theo dõi tồn của kho có tồn khác 0 chưa xuất hiện trong phiếu kiểm kê đã lưu (spec §5).</summary>
        public List<StockTakeUncountedItem> LoadUncountedItems(string stockTakeId)
        {
            if (!CanView || string.IsNullOrEmpty(stockTakeId))
                return new List<StockTakeUncountedItem>();

            StockTakeRow doc = _repo.LoadById(stockTakeId);
            if (doc == null || string.IsNullOrEmpty(doc.WarehouseId))
                return new List<StockTakeUncountedItem>();

            if (!_session.IsAdmin)
            {
                List<WarehouseRow> allowed = LoadAllowedWarehouses();
                if (!allowed.Exists(w => string.Equals(w.Id, doc.WarehouseId, StringComparison.Ordinal)))
                    return new List<StockTakeUncountedItem>();
            }

            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            return _repo.LoadUncountedItems(stockTakeId, doc.WarehouseId, sortByCode);
        }

        /// <summary>
        /// Lưu phiếu kiểm kê mới hoặc cập nhật phiếu đã lưu trong một transaction duy nhất (TDONHANG + TDONHANGCHITIET + TLUUVET).
        /// <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public StockTakeSaveResult Save(StockTakeRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return StockTakeSaveResult.Fail(StockTakeConstants.NotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return StockTakeSaveResult.Fail(PurchaseConstants.NoPermission);
            if (!isNew && !CanEdit)
                return StockTakeSaveResult.Fail(PurchaseConstants.NoPermission);

            row.Name = (row.Name ?? "").Trim();
            row.Description = (row.Description ?? "").Trim();
            row.Note = (row.Note ?? "").Trim();

            string validationError = StockTakeRules.Validate(row);
            if (validationError != null)
                return StockTakeSaveResult.Fail(validationError);

            StockTakeSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    WarehouseRow targetWh = WarehouseRepository.LoadByIdIn(conn, tx, row.WarehouseId);
                    if (targetWh == null || targetWh.Status != StockTakeConstants.ActiveStatus)
                    {
                        outcome = StockTakeSaveResult.Fail(StockTakeConstants.WarehouseRequired);
                        return;
                    }

                    if (!_session.IsAdmin)
                    {
                        List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                            conn, tx, _session.UserId, false, SessionStoreIds());
                        if (!allowed.Exists(w => string.Equals(w.Id, targetWh.Id, StringComparison.Ordinal)))
                        {
                            outcome = StockTakeSaveResult.Fail(StockTakeConstants.WarehouseNotAllowed);
                            return;
                        }
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    StockTakeRow existing = null;
                    if (!isNew)
                    {
                        existing = StockTakeRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = StockTakeSaveResult.Fail(StockTakeConstants.NotFound);
                            return;
                        }
                        if (existing.Lines != null && existing.Lines.Count > 0
                            && !string.Equals(existing.WarehouseId ?? "", row.WarehouseId ?? "", StringComparison.Ordinal))
                        {
                            outcome = StockTakeSaveResult.Fail(StockTakeConstants.WarehouseLockedOnEdit);
                            return;
                        }
                        if (!CanChangeDate)
                            row.Date = existing.Date;
                    }
                    else if (!CanChangeDate)
                    {
                        row.Date = serverNow.Date;
                    }

                    if (PurchaseRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = StockTakeSaveResult.Fail(PurchaseConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Spec §2 (T2): tính lại tồn hệ thống ĐÚNG LÚC LƯU trong transaction (loại trừ chính phiếu đang sửa).
                    // Nếu có dòng khác số người dùng đã thấy -> cập nhật lại số mới trên dòng và báo, không lưu.
                    string excludeId = isNew ? "" : row.Id;
                    bool anySystemChanged = false;
                    foreach (StockTakeLine line in row.Lines)
                    {
                        decimal liveBalance = ServiceOrderRepository.GetStockBalanceIn(
                            conn, tx, line.ProductId, row.WarehouseId, excludeId);
                        if (liveBalance != line.SystemQuantity)
                        {
                            anySystemChanged = true;
                            line.SystemQuantity = liveBalance;
                        }
                        StockTakeRules.RecalcLine(line);
                    }
                    if (anySystemChanged)
                    {
                        outcome = StockTakeSaveResult.FailSystemStockChanged(StockTakeConstants.SystemStockChanged);
                        return;
                    }

                    foreach (StockTakeLine line in row.Lines)
                    {
                        line.WarehouseId = row.WarehouseId;
                        line.ProductName = PurchaseRules.Clip(line.ProductName);
                        line.Note = PurchaseRules.Clip(line.Note);
                        StockTakeRules.RecalcLine(line);
                    }

                    row.StoreId = targetWh.StoreId;
                    row.Loai = StockTakeConstants.OrderType;
                    row.Status = StockTakeConstants.ActiveStatus;
                    row.PaidFlag = StockTakeConstants.PaidFlag;

                    // Spec §4 (T3): chặn sửa gây âm kho khi kho không cho phép âm kho.
                    if (!isNew)
                    {
                        string negErr = CheckNegativeStockOnChangeIn(conn, tx, existing, row.WarehouseId, row.Lines);
                        if (negErr != null)
                        {
                            outcome = StockTakeSaveResult.Fail(negErr);
                            return;
                        }
                    }

                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        StockTakeRepository.InsertHeaderIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (StockTakeRepository.UpdateHeaderIn(conn, tx, row) == 0)
                        {
                            outcome = StockTakeSaveResult.Fail(StockTakeConstants.NotFound);
                            return;
                        }
                        PurchaseRepository.DeleteLinesIn(conn, tx, row.Id);
                    }

                    StockTakeRepository.InsertLinesIn(
                        conn, tx, row.Id, row.WarehouseId, row.Lines, _session.UserId ?? "", serverNow);

                    string actionPrefix = isNew ? "Thêm mới phiếu kiểm kê: " : "Cập nhật phiếu kiểm kê: ";
                    WriteActivityLogIn(conn, tx, row.Id, row.Name, actionPrefix + row.Name, serverNow);

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new StockTakeSaveResult
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
            return outcome ?? StockTakeSaveResult.Fail("Không thể lưu phiếu kiểm kê.");
        }

        /// <summary>
        /// Xóa cứng phiếu kiểm kê và chi tiết + ghi lưu vết trong một transaction duy nhất (spec §4).
        /// Chặn khi xóa làm tồn kho thành âm mà kho không cho phép âm kho (T3).
        /// </summary>
        public StockTakeDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return StockTakeDeleteResult.Fail(StockTakeConstants.NotFound);
            if (!CanRemove)
                return StockTakeDeleteResult.Fail(PurchaseConstants.NoPermission);

            StockTakeDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                StockTakeRow existing = StockTakeRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = StockTakeDeleteResult.Fail(StockTakeConstants.NotFound);
                    return;
                }

                if (!_session.IsAdmin)
                {
                    List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                        conn, tx, _session.UserId, false, SessionStoreIds());
                    if (!allowed.Exists(w => string.Equals(w.Id, existing.WarehouseId, StringComparison.Ordinal)))
                    {
                        outcome = StockTakeDeleteResult.Fail(StockTakeConstants.WarehouseNotAllowed);
                        return;
                    }
                }

                string negErr = CheckNegativeStockOnChangeIn(conn, tx, existing, null, null);
                if (negErr != null)
                {
                    outcome = StockTakeDeleteResult.Fail(negErr);
                    return;
                }

                if (StockTakeRepository.DeleteOrderIn(conn, tx, id) == 0)
                {
                    outcome = StockTakeDeleteResult.Fail(StockTakeConstants.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                WriteActivityLogIn(conn, tx, existing.Id, existing.Name, "Xóa phiếu kiểm kê: " + existing.Name, serverNow);

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new StockTakeDeleteResult { Ok = true };
            });

            return outcome ?? StockTakeDeleteResult.Fail("Không thể xóa phiếu kiểm kê.");
        }

        /// <summary>
        /// Kiểm tra âm kho khi sửa hoặc xóa phiếu kiểm kê (spec §4 T3).
        /// Xét chênh lệch ròng (SLNHAP - SLXUAT) của từng (kho, mặt hàng) trên phiếu cũ so với phiếu mới (hoặc 0 khi xóa).
        /// </summary>
        private static string CheckNegativeStockOnChangeIn(
            FbConnection conn,
            FbTransaction tx,
            StockTakeRow existing,
            string newWarehouseId,
            List<StockTakeLine> newLines)
        {
            if (existing == null || existing.Lines == null || existing.Lines.Count == 0)
                return null;
            if (SystemConfig.ChoPhepAmKho)
                return null;

            var oldNetByKey = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var keyMeta = new Dictionary<string, Tuple<string, string, string>>(StringComparer.Ordinal);
            foreach (StockTakeLine l in existing.Lines)
            {
                if (l == null || string.IsNullOrEmpty(l.ProductId))
                    continue;
                string whId = !string.IsNullOrEmpty(l.WarehouseId) ? l.WarehouseId : existing.WarehouseId;
                if (string.IsNullOrEmpty(whId))
                    continue;
                string key = whId + "|" + l.ProductId;
                decimal prev;
                oldNetByKey.TryGetValue(key, out prev);
                oldNetByKey[key] = prev + (l.ImportQty - l.ExportQty);
                if (!keyMeta.ContainsKey(key))
                    keyMeta[key] = Tuple.Create(whId, l.ProductId, l.ProductName ?? "");
            }

            var newNetByKey = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (newLines != null && !string.IsNullOrEmpty(newWarehouseId))
            {
                foreach (StockTakeLine l in newLines)
                {
                    if (l == null || string.IsNullOrEmpty(l.ProductId))
                        continue;
                    string key = newWarehouseId + "|" + l.ProductId;
                    decimal prev;
                    newNetByKey.TryGetValue(key, out prev);
                    newNetByKey[key] = prev + (l.ImportQty - l.ExportQty);
                }
            }

            var allowNegCache = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, decimal> kv in oldNetByKey)
            {
                Tuple<string, string, string> meta = keyMeta[kv.Key];
                string whId = meta.Item1;
                string productId = meta.Item2;

                bool allowNeg;
                if (!allowNegCache.TryGetValue(whId, out allowNeg))
                {
                    WarehouseRow wh = WarehouseRepository.LoadByIdIn(conn, tx, whId);
                    allowNeg = wh != null && wh.AllowNegativeStock != 0;
                    allowNegCache[whId] = allowNeg;
                }
                if (allowNeg)
                    continue;

                decimal newNet;
                newNetByKey.TryGetValue(kv.Key, out newNet);
                if (newNet >= kv.Value)
                    continue;

                decimal currentStock = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, whId, "");
                decimal stockWithoutOrder = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, whId, existing.Id);
                decimal afterStock = stockWithoutOrder + newNet;
                if (afterStock < 0m)
                {
                    string itemName = !string.IsNullOrEmpty(meta.Item3)
                        ? meta.Item3
                        : PurchaseRepository.LoadProductNameIn(conn, tx, productId);
                    return PurchaseConstants.NegativeStockBlocked(itemName, currentStock, afterStock);
                }
            }

            return null;
        }

        private void WriteActivityLogIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string orderNumber,
            string note,
            DateTime serverNow)
        {
            _activityLogger.WriteBatch(conn, tx, new[]
            {
                new ActivityLogRequest
                {
                    FunctionName = StockTakeConstants.FunctionName,
                    Kind = ActivityKind.Khac,
                    BillId = orderId,
                    TableName = PurchaseRules.Clip(orderNumber),
                    Note = PurchaseRules.Clip(note),
                    Amount = 0m,
                    Account = !string.IsNullOrEmpty(_session.UserName) ? _session.UserName : (_session.DisplayName ?? ""),
                    UserId = _session.UserId ?? "",
                    Time = serverNow
                }
            });
        }

        private List<string> SessionStoreIds()
        {
            if (_session.Stores == null || _session.Stores.Count == 0)
                return null;
            return _session.Stores.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        private static void ResetNew(StockTakeRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
