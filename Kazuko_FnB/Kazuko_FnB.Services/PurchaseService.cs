using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Nhập hàng vào kho (TDONHANG LOAI = 1 + TDONHANGCHITIET, docs/specs/phieu-nhap-kho.md):
    /// - Một transaction duy nhất cho lưu/sửa/xóa kèm lưu vết TLUUVET (lỗi tự rollback).
    /// - Quy đổi 2 ĐVT, tính dòng và tổng, ghi TIENTHANHTOAN theo quy-tien.md §4.5 (FundRules.StockPaymentFields).
    /// - Dùng lại công thức tồn duy nhất ServiceOrderRepository.GetStockBalanceIn; chặn sửa/xóa gây âm kho (T3).
    /// </summary>
    public sealed class PurchaseService
    {
        private readonly UserSession _session;
        private readonly PurchaseRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public PurchaseService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new PurchaseRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return PurchaseRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return PurchaseRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return PurchaseRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return PurchaseRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return PurchaseRules.CanChangeDate(_session); } }
        public bool CanViewBuyPrice { get { return PurchaseRules.CanViewBuyPrice(_session); } }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(forDate);
        }

        public List<WarehouseRow> LoadAllowedWarehouses()
        {
            return _repo.LoadAllowedWarehouses(_session.UserId, _session.IsAdmin, SessionStoreIds());
        }

        public List<PurchaseProductItem> LoadProducts()
        {
            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            return _repo.LoadProducts(sortByCode);
        }

        public PurchaseRow LoadById(string id)
        {
            if (!CanView || string.IsNullOrEmpty(id))
                return null;
            return _repo.LoadById(id);
        }

        public List<PurchaseLine> LoadLines(string orderId)
        {
            if (!CanView || string.IsNullOrEmpty(orderId))
                return new List<PurchaseLine>();
            return _repo.LoadLines(orderId);
        }

        public List<PurchaseRow> LoadList(PurchaseFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<PurchaseRow>();

            if (!_session.IsAdmin && filter.AllowedWarehouseIds == null)
                filter.AllowedWarehouseIds = LoadAllowedWarehouses().Select(w => w.Id).ToList();

            List<PurchaseRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => PurchaseRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        /// <summary>Đọc tồn kho hiện tại bằng đúng hàm tồn dùng chung ServiceOrderRepository.GetStockBalanceIn.</summary>
        public decimal GetStockBalance(string productId, string warehouseId, string excludeOrderId = "")
        {
            return _repo.Read(conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, productId, warehouseId, excludeOrderId));
        }

        /// <summary>
        /// Lưu phiếu nhập mới hoặc cập nhật phiếu đã lưu trong một transaction duy nhất (TDONHANG + TDONHANGCHITIET + TLUUVET).
        /// <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public PurchaseSaveResult Save(PurchaseRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return PurchaseSaveResult.Fail(PurchaseConstants.NotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return PurchaseSaveResult.Fail(PurchaseConstants.NoPermission);
            if (!isNew && !CanEdit)
                return PurchaseSaveResult.Fail(PurchaseConstants.NoPermission);

            row.Name = (row.Name ?? "").Trim();
            row.Description = (row.Description ?? "").Trim();
            row.Note = (row.Note ?? "").Trim();

            bool requireSupplier = SystemConfig.GetBool(PurchaseConstants.ConfigRequireSupplier);
            string validationError = PurchaseRules.Validate(row, requireSupplier);
            if (validationError != null)
                return PurchaseSaveResult.Fail(validationError);

            PayMethod? payMethod = row.PaidAmount > 0m ? (row.PaymentMethod ?? PayMethod.Cash) : (PayMethod?)null;
            FundStockPaymentResult payFields = FundRules.StockPaymentFields(payMethod, row.PaidAmount, row.BankAccountId);
            if (!string.IsNullOrEmpty(payFields.Error))
                return PurchaseSaveResult.Fail(payFields.Error);

            PurchaseSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    WarehouseRow targetWarehouse = WarehouseRepository.LoadByIdIn(conn, tx, row.WarehouseId);
                    if (targetWarehouse == null || targetWarehouse.Status != PurchaseRepository.ActiveStatus)
                    {
                        outcome = PurchaseSaveResult.Fail(PurchaseConstants.WarehouseRequired);
                        return;
                    }

                    if (!_session.IsAdmin)
                    {
                        List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                            conn, tx, _session.UserId, false, SessionStoreIds());
                        if (!allowed.Exists(w => string.Equals(w.Id, targetWarehouse.Id, StringComparison.Ordinal)))
                        {
                            outcome = PurchaseSaveResult.Fail(PurchaseConstants.WarehouseNotAllowed);
                            return;
                        }
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    PurchaseRow existing = null;
                    if (!isNew)
                    {
                        existing = PurchaseRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null || existing.Loai != PurchaseConstants.OrderType)
                        {
                            outcome = PurchaseSaveResult.Fail(PurchaseConstants.NotFound);
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
                        outcome = PurchaseSaveResult.Fail(PurchaseConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Tính lại SLNHAP, THANHTIEN, DKHOHANGID từng dòng và tổng đầu phiếu ở lớp Services.
                    foreach (PurchaseLine line in row.Lines)
                    {
                        line.WarehouseId = row.WarehouseId;
                        line.ProductName = PurchaseRules.Clip(line.ProductName);
                        line.Note = PurchaseRules.Clip(line.Note);
                        PurchaseRules.RecalcLine(line);
                    }

                    PurchaseTotals totals = PurchaseRules.CalcTotals(
                        row.Lines,
                        row.TileGiamGia,
                        row.TienGiamGia,
                        row.GiamTheoTien,
                        row.TileThue,
                        row.PhiVanChuyen);

                    row.StoreId = targetWarehouse.StoreId;
                    row.TienHang = totals.TienHang;
                    row.TileGiamGia = totals.TileGiamGia;
                    row.TienGiamGia = totals.TienGiamGia;
                    row.TienThue = totals.TienThue;
                    row.TongCong = totals.TongCong;
                    row.CashAmount = payFields.Cash;
                    row.TransferAmount = payFields.Transfer;
                    row.BankAccountId = string.IsNullOrEmpty(payFields.AccountId) ? null : payFields.AccountId;
                    row.Loai = PurchaseConstants.OrderType;
                    row.Status = PurchaseConstants.ActiveStatus;
                    row.PaidFlag = PurchaseConstants.PaidFlag;

                    // Chặn sửa gây âm kho (spec §4 T3).
                    if (!isNew)
                    {
                        string negErr = CheckNegativeStockOnChangeIn(conn, tx, existing, row.WarehouseId, row.Lines);
                        if (negErr != null)
                        {
                            outcome = PurchaseSaveResult.Fail(negErr);
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
                        PurchaseRepository.InsertHeaderIn(conn, tx, row);
                        PurchaseRepository.InsertLinesIn(conn, tx, row.Id, row.Lines, _session.UserId ?? "", serverNow);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (PurchaseRepository.UpdateHeaderIn(conn, tx, row) == 0)
                        {
                            outcome = PurchaseSaveResult.Fail(PurchaseConstants.NotFound);
                            return;
                        }
                        PurchaseRepository.DeleteLinesIn(conn, tx, row.Id);
                        PurchaseRepository.InsertLinesIn(conn, tx, row.Id, row.Lines, _session.UserId ?? "", serverNow);
                    }

                    string actionPrefix = isNew ? "Thêm mới phiếu nhập kho: " : "Cập nhật phiếu nhập kho: ";
                    WriteActivityLogIn(conn, tx, row.Id, row.Name, actionPrefix + row.Name, row.TongCong, serverNow);

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new PurchaseSaveResult
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
            return outcome ?? PurchaseSaveResult.Fail("Không thể lưu phiếu nhập kho.");
        }

        /// <summary>
        /// Xóa cứng phiếu nhập kho và chi tiết + ghi lưu vết trong một transaction duy nhất (spec §4).
        /// Chặn khi xóa làm tồn âm mà kho không cho phép âm kho (T3).
        /// </summary>
        public PurchaseDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return PurchaseDeleteResult.Fail(PurchaseConstants.NotFound);
            if (!CanRemove)
                return PurchaseDeleteResult.Fail(PurchaseConstants.NoPermission);

            PurchaseDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PurchaseRow existing = PurchaseRepository.LoadByIdIn(conn, tx, id);
                if (existing == null || existing.Loai != PurchaseConstants.OrderType)
                {
                    outcome = PurchaseDeleteResult.Fail(PurchaseConstants.NotFound);
                    return;
                }

                if (!_session.IsAdmin)
                {
                    List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                        conn, tx, _session.UserId, false, SessionStoreIds());
                    if (!allowed.Exists(w => string.Equals(w.Id, existing.WarehouseId, StringComparison.Ordinal)))
                    {
                        outcome = PurchaseDeleteResult.Fail(PurchaseConstants.WarehouseNotAllowed);
                        return;
                    }
                }

                string negErr = CheckNegativeStockOnChangeIn(conn, tx, existing, null, null);
                if (negErr != null)
                {
                    outcome = PurchaseDeleteResult.Fail(negErr);
                    return;
                }

                if (PurchaseRepository.DeleteOrderIn(conn, tx, id) == 0)
                {
                    outcome = PurchaseDeleteResult.Fail(PurchaseConstants.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                WriteActivityLogIn(conn, tx, existing.Id, existing.Name, "Xóa phiếu nhập kho: " + existing.Name, existing.TongCong, serverNow);

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new PurchaseDeleteResult { Ok = true };
            });

            return outcome ?? PurchaseDeleteResult.Fail("Không thể xóa phiếu nhập kho.");
        }

        /// <summary>
        /// Kiểm tra âm kho khi sửa hoặc xóa phiếu nhập (spec §4 T3).
        /// Với mỗi (mặt hàng, kho) có mặt trên phiếu cũ: tính tồn hiện có và tồn sau khi thay đổi qua
        /// ServiceOrderRepository.GetStockBalanceIn. Chặn nếu tồn giảm xuống &lt; 0 khi kho không cho âm
        /// và cấu hình toàn hệ thống ChoPhepAmKho cũng không bật.
        /// </summary>
        private static string CheckNegativeStockOnChangeIn(
            FbConnection conn,
            FbTransaction tx,
            PurchaseRow existing,
            string newWarehouseId,
            List<PurchaseLine> newLines)
        {
            if (existing == null || existing.Lines == null || existing.Lines.Count == 0)
                return null;
            if (SystemConfig.ChoPhepAmKho)
                return null;

            // Gom tổng SLNHAP cũ theo (WarehouseId, ProductId).
            var oldByKey = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var keyMeta = new Dictionary<string, Tuple<string, string, string>>(StringComparer.Ordinal);
            foreach (PurchaseLine l in existing.Lines)
            {
                if (l == null || string.IsNullOrEmpty(l.ProductId))
                    continue;
                string whId = !string.IsNullOrEmpty(l.WarehouseId) ? l.WarehouseId : existing.WarehouseId;
                if (string.IsNullOrEmpty(whId))
                    continue;
                string key = whId + "|" + l.ProductId;
                decimal prev;
                oldByKey.TryGetValue(key, out prev);
                oldByKey[key] = prev + l.ConvertedQuantity;
                if (!keyMeta.ContainsKey(key))
                    keyMeta[key] = Tuple.Create(whId, l.ProductId, l.ProductName ?? "");
            }

            // Gom tổng SLNHAP mới theo (WarehouseId, ProductId).
            var newByKey = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (newLines != null && !string.IsNullOrEmpty(newWarehouseId))
            {
                foreach (PurchaseLine l in newLines)
                {
                    if (l == null || string.IsNullOrEmpty(l.ProductId))
                        continue;
                    string key = newWarehouseId + "|" + l.ProductId;
                    decimal prev;
                    newByKey.TryGetValue(key, out prev);
                    newByKey[key] = prev + l.ConvertedQuantity;
                }
            }

            var allowNegCache = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, decimal> kv in oldByKey)
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

                decimal newQty;
                newByKey.TryGetValue(kv.Key, out newQty);
                if (newQty >= kv.Value)
                    continue;

                decimal currentStock = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, whId, "");
                decimal stockWithoutOrder = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, whId, existing.Id);
                decimal afterStock = stockWithoutOrder + newQty;

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
            decimal amount,
            DateTime serverNow)
        {
            _activityLogger.WriteBatch(conn, tx, new[]
            {
                new ActivityLogRequest
                {
                    FunctionName = PurchaseConstants.FunctionName,
                    Kind = ActivityKind.Khac,
                    BillId = orderId,
                    TableName = PurchaseRules.Clip(orderNumber),
                    Note = PurchaseRules.Clip(note),
                    Amount = amount,
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

        private static void ResetNew(PurchaseRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
