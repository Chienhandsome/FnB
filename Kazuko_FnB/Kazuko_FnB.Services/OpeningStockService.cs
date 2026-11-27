using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Tồn kho ban đầu (TDONHANG LOAI = 97, NAME = 'TONDAU', docs/specs/so-du-dau-ky.md P1):
    /// - Mỗi kho tối đa một phiếu LOAI = 97; lưu trong MỘT transaction kèm lưu vết TLUUVET (lỗi rollback).
    /// - Chặn tồn đầu âm / giá vốn âm nêu rõ tên mặt hàng (T1).
    /// - Chặn giảm tồn đầu làm tồn hiện tại thành âm khi kho không cho âm kho (T2), dùng lại
    ///   <see cref="ServiceOrderRepository.GetStockBalanceIn"/> loại trừ chính phiếu đang sửa.
    /// - Sau khi commit: gọi <see cref="CostRecalcService.RunAutoAfterPurchase"/> khi bật TuDongTinhGiaVon
    ///   (lỗi chỉ trả cảnh báo, không làm hỏng phiếu đã lưu).
    /// </summary>
    public sealed class OpeningStockService
    {
        private readonly UserSession _session;
        private readonly OpeningStockRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public OpeningStockService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new OpeningStockRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return OpeningStockRules.CanView(_session); } }
        public bool CanEdit { get { return OpeningStockRules.CanEdit(_session); } }
        public bool CanChangeDate { get { return OpeningStockRules.CanChangeDate(_session); } }

        public List<WarehouseRow> LoadAllowedWarehouses()
        {
            if (!CanView)
                return new List<WarehouseRow>();
            return _repo.LoadAllowedWarehouses(_session.UserId, _session.IsAdmin, SessionStoreIds());
        }

        public string ResolveDefaultWarehouseId(IList<WarehouseRow> allowedWarehouses)
        {
            string storeId = _session.CurrentStore == null ? null : _session.CurrentStore.Id;
            string storeDefaultWh = string.IsNullOrEmpty(storeId)
                ? null
                : _repo.GetStoreDefaultWarehouseId(storeId);
            return InventoryRules.ResolveDefaultWarehouseId(allowedWarehouses, storeId, storeDefaultWh);
        }

        /// <summary>Nạp dữ liệu Tồn kho ban đầu cho một kho (kiểm quyền Xem và phạm vi kho của non-admin).</summary>
        public OpeningStockDocument LoadDocument(string warehouseId)
        {
            DateTime defaultDate = OpeningStockRules.DefaultOpeningDate(DateTime.Today);
            if (!CanView || string.IsNullOrWhiteSpace(warehouseId))
                return new OpeningStockDocument { Date = defaultDate };

            if (!_session.IsAdmin)
            {
                List<WarehouseRow> allowed = LoadAllowedWarehouses();
                if (!allowed.Exists(w => string.Equals(w.Id, warehouseId, StringComparison.Ordinal)))
                    return new OpeningStockDocument { Date = defaultDate };
            }

            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            return _repo.LoadWarehouseDocument(warehouseId, defaultDate, sortByCode);
        }

        /// <summary>
        /// Lưu Tồn kho ban đầu cho một kho trong một transaction duy nhất (TDONHANG + TDONHANGCHITIET + TLUUVET).
        /// <paramref name="testHookAfterWrite"/> và <paramref name="autoCostRunner"/> chỉ dùng cho selftest.
        /// </summary>
        public OpeningStockSaveResult Save(
            OpeningStockSaveRequest request,
            Action testHookAfterWrite = null,
            Func<UserSession, string> autoCostRunner = null)
        {
            if (!CanEdit)
                return OpeningStockSaveResult.Fail(OpeningStockConstants.NoPermission);

            string validationError = OpeningStockRules.Validate(request);
            if (validationError != null)
                return OpeningStockSaveResult.Fail(validationError);

            OpeningStockSaveResult outcome = null;
            bool persistedChange = false;

            _repo.RunInTransaction((conn, tx) =>
            {
                WarehouseRow targetWh = WarehouseRepository.LoadByIdIn(conn, tx, request.WarehouseId);
                if (targetWh == null || targetWh.Status != OpeningStockConstants.ActiveStatus)
                {
                    outcome = OpeningStockSaveResult.Fail(OpeningStockConstants.WarehouseRequired);
                    return;
                }

                if (!_session.IsAdmin)
                {
                    List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                        conn, tx, _session.UserId, false, SessionStoreIds());
                    if (!allowed.Exists(w => string.Equals(w.Id, targetWh.Id, StringComparison.Ordinal)))
                    {
                        outcome = OpeningStockSaveResult.Fail(OpeningStockConstants.WarehouseNotAllowed);
                        return;
                    }
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                List<OpeningStockHeader> existingOrders = OpeningStockRepository.FindOpeningOrdersIn(conn, tx, targetWh.Id);
                OpeningStockHeader existingHeader = existingOrders.Count > 0 ? existingOrders[0] : null;
                string dupWarning = existingOrders.Count > 1
                    ? OpeningStockConstants.DuplicateOrdersWarning(existingOrders.Count)
                    : null;

                List<OpeningStockLineRecord> existingLines = existingHeader != null
                    ? OpeningStockRepository.LoadOrderLinesIn(conn, tx, existingHeader.Id)
                    : new List<OpeningStockLineRecord>();

                DateTime saveDate;
                if (!CanChangeDate)
                {
                    saveDate = existingHeader != null && existingHeader.Date != DateTime.MinValue
                        ? existingHeader.Date.Date
                        : OpeningStockRules.DefaultOpeningDate(serverNow);
                }
                else
                {
                    saveDate = request.Date == DateTime.MinValue
                        ? OpeningStockRules.DefaultOpeningDate(serverNow)
                        : request.Date.Date;
                }

                // Spec §2 (T2): kiểm tra giảm tồn đầu gây âm kho khi kho không cho phép âm kho.
                if (existingHeader != null && existingLines.Count > 0)
                {
                    string negErr = CheckNegativeStockOnChangeIn(
                        conn, tx, targetWh, existingHeader.Id, existingLines, request.Items);
                    if (negErr != null)
                    {
                        outcome = OpeningStockSaveResult.Fail(negErr);
                        return;
                    }
                }

                var existingByProduct = GroupLinesByProduct(existingLines);
                List<OpeningStockItemRow> items = request.Items ?? new List<OpeningStockItemRow>();

                bool hasAnyPositive = false;
                foreach (OpeningStockItemRow item in items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.ProductId) && item.Quantity > 0m)
                    {
                        hasAnyPositive = true;
                        break;
                    }
                }

                string orderId = existingHeader != null ? existingHeader.Id : null;
                bool createdNewHeader = false;
                if (orderId == null && hasAnyPositive)
                {
                    orderId = DbHelper.NewId();
                    OpeningStockRepository.InsertHeaderIn(
                        conn, tx, orderId, saveDate, targetWh.Id, targetWh.StoreId, 0m, _session.UserId ?? "", serverNow);
                    createdNewHeader = true;
                }

                if (orderId == null)
                {
                    // Kho chưa có phiếu 97 và mọi mặt hàng đều có tồn đầu = 0 -> không tạo phiếu rỗng.
                    if (testHookAfterWrite != null)
                        testHookAfterWrite();
                    outcome = new OpeningStockSaveResult
                    {
                        Ok = true,
                        SavedOrderId = null,
                        OrderDeleted = false,
                        LineCount = 0,
                        TotalAmount = 0m,
                        Warning = dupWarning
                    };
                    return;
                }

                string actorId = _session.UserId ?? "";
                int seq = 0;
                foreach (OpeningStockItemRow item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ProductId))
                        continue;

                    List<OpeningStockLineRecord> curLines;
                    existingByProduct.TryGetValue(item.ProductId, out curLines);

                    if (item.Quantity == 0m)
                    {
                        if (curLines != null)
                        {
                            foreach (OpeningStockLineRecord oldLine in curLines)
                                OpeningStockRepository.DeleteLineIn(conn, tx, oldLine.Id);
                        }
                        item.LineId = null;
                        item.Amount = 0m;
                        continue;
                    }

                    decimal amt = OpeningStockRules.CalcLineAmount(item.Quantity, item.CostPrice);
                    string clippedName = PurchaseRules.Clip(item.Name);
                    if (curLines != null && curLines.Count > 0)
                    {
                        OpeningStockLineRecord primary = curLines[0];
                        OpeningStockRepository.UpdateLineIn(
                            conn, tx, primary.Id, clippedName, item.UnitId, targetWh.Id,
                            item.Quantity, item.CostPrice, amt, actorId, serverNow);
                        for (int k = 1; k < curLines.Count; k++)
                            OpeningStockRepository.DeleteLineIn(conn, tx, curLines[k].Id);
                        item.LineId = primary.Id;
                        item.Amount = amt;
                    }
                    else
                    {
                        string newLineId = OpeningStockRepository.InsertLineIn(
                            conn, tx, orderId, item.ProductId, clippedName, item.UnitId, targetWh.Id,
                            item.Quantity, item.CostPrice, amt, actorId, serverNow.AddSeconds(seq++));
                        item.LineId = newLineId;
                        item.Amount = amt;
                    }
                }

                int remainingCount;
                decimal remainingTotal;
                OpeningStockRepository.CountAndSumActiveLinesIn(conn, tx, orderId, out remainingCount, out remainingTotal);

                bool orderDeleted = false;
                string savedOrderId = orderId;
                string logNote;
                if (remainingCount == 0)
                {
                    OpeningStockRepository.DeleteOrderIn(conn, tx, orderId);
                    orderDeleted = true;
                    savedOrderId = null;
                    logNote = "Xóa tồn kho ban đầu kho: " + (targetWh.Name ?? "");
                }
                else
                {
                    OpeningStockRepository.UpdateHeaderIn(
                        conn, tx, orderId, saveDate, targetWh.Id, targetWh.StoreId, remainingTotal, actorId, serverNow);
                    string verb = createdNewHeader ? "Tạo mới" : "Cập nhật";
                    logNote = string.Format(
                        "{0} tồn kho ban đầu kho: {1} ({2} mặt hàng, tổng {3})",
                        verb,
                        targetWh.Name ?? "",
                        remainingCount,
                        PurchaseConstants.FormatMoneyVi(remainingTotal));
                }

                WriteActivityLogIn(conn, tx, orderId, targetWh.Name ?? OpeningStockConstants.OrderName, logNote, remainingTotal, serverNow);
                persistedChange = true;

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new OpeningStockSaveResult
                {
                    Ok = true,
                    SavedOrderId = savedOrderId,
                    OrderDeleted = orderDeleted,
                    LineCount = remainingCount,
                    TotalAmount = remainingTotal,
                    Warning = dupWarning
                };
            });

            if (outcome != null && outcome.Ok && persistedChange)
            {
                string costWarn;
                try
                {
                    costWarn = autoCostRunner != null
                        ? autoCostRunner(_session)
                        : CostRecalcService.RunAutoAfterPurchase(_session);
                }
                catch (Exception)
                {
                    costWarn = CostRecalcConstants.AutoFailedWarning;
                }
                if (!string.IsNullOrEmpty(costWarn))
                {
                    outcome.Warning = string.IsNullOrEmpty(outcome.Warning)
                        ? costWarn
                        : outcome.Warning + " " + costWarn;
                }
            }

            return outcome ?? OpeningStockSaveResult.Fail("Không thể lưu tồn kho ban đầu.");
        }

        private static Dictionary<string, List<OpeningStockLineRecord>> GroupLinesByProduct(List<OpeningStockLineRecord> lines)
        {
            var map = new Dictionary<string, List<OpeningStockLineRecord>>(StringComparer.Ordinal);
            if (lines == null)
                return map;
            foreach (OpeningStockLineRecord l in lines)
            {
                if (l == null || string.IsNullOrEmpty(l.ProductId))
                    continue;
                List<OpeningStockLineRecord> list;
                if (!map.TryGetValue(l.ProductId, out list))
                    map[l.ProductId] = list = new List<OpeningStockLineRecord>();
                list.Add(l);
            }
            return map;
        }

        /// <summary>
        /// Kiểm tra giảm tồn đầu gây âm kho (spec §2 T2) bằng đúng hàm tồn chung
        /// <see cref="ServiceOrderRepository.GetStockBalanceIn"/> (loại trừ chính phiếu 97 đang sửa).
        /// </summary>
        private static string CheckNegativeStockOnChangeIn(
            FbConnection conn,
            FbTransaction tx,
            WarehouseRow targetWh,
            string existingOrderId,
            List<OpeningStockLineRecord> existingLines,
            List<OpeningStockItemRow> newItems)
        {
            if (targetWh == null || targetWh.AllowNegativeStock != 0)
                return null;
            if (SystemConfig.ChoPhepAmKho)
                return null;

            var oldQtyByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var oldNameByProduct = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (OpeningStockLineRecord l in existingLines)
            {
                if (l == null || string.IsNullOrEmpty(l.ProductId))
                    continue;
                decimal prev;
                oldQtyByProduct.TryGetValue(l.ProductId, out prev);
                oldQtyByProduct[l.ProductId] = prev + l.Quantity;
                if (!oldNameByProduct.ContainsKey(l.ProductId) && !string.IsNullOrEmpty(l.ProductName))
                    oldNameByProduct[l.ProductId] = l.ProductName;
            }

            var newItemByProduct = new Dictionary<string, OpeningStockItemRow>(StringComparer.Ordinal);
            if (newItems != null)
            {
                foreach (OpeningStockItemRow item in newItems)
                {
                    if (item != null && !string.IsNullOrEmpty(item.ProductId))
                        newItemByProduct[item.ProductId] = item;
                }
            }

            foreach (KeyValuePair<string, decimal> kv in oldQtyByProduct)
            {
                string productId = kv.Key;
                decimal oldQty = kv.Value;

                OpeningStockItemRow submitted;
                if (!newItemByProduct.TryGetValue(productId, out submitted))
                    continue; // Mặt hàng không nằm trong danh sách gửi lên (vd. đã ngừng dùng) -> giữ nguyên dòng cũ.

                decimal newQty = submitted.Quantity;
                if (newQty >= oldQty)
                    continue;

                decimal currentStock = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, targetWh.Id, "");
                decimal stockWithoutOrder = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, targetWh.Id, existingOrderId);
                decimal afterStock = stockWithoutOrder + newQty;

                if (afterStock < 0m)
                {
                    string itemName = !string.IsNullOrEmpty(submitted.Name)
                        ? submitted.Name
                        : (oldNameByProduct.ContainsKey(productId) && !string.IsNullOrEmpty(oldNameByProduct[productId])
                            ? oldNameByProduct[productId]
                            : PurchaseRepository.LoadProductNameIn(conn, tx, productId));
                    return PurchaseConstants.NegativeStockBlocked(itemName, currentStock, afterStock);
                }
            }

            return null;
        }

        private void WriteActivityLogIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string tableName,
            string note,
            decimal amount,
            DateTime serverNow)
        {
            _activityLogger.WriteBatch(conn, tx, new[]
            {
                new ActivityLogRequest
                {
                    FunctionName = OpeningStockConstants.FunctionName,
                    Kind = ActivityKind.Khac,
                    BillId = orderId ?? "",
                    TableName = PurchaseRules.Clip(tableName, OpeningStockConstants.MaxTextLength),
                    Note = PurchaseRules.Clip(note, OpeningStockConstants.MaxTextLength),
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
    }
}
