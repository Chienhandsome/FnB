using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Xuất khác (TDONHANG LOAI = 2) và Chuyển kho (TDONHANG LOAI = 3)
    /// theo docs/specs/phieu-xuat-chuyen-kho.md:
    /// - Một transaction duy nhất cho lưu/sửa/xóa kèm lưu vết TLUUVET (lỗi tự rollback).
    /// - Xuất khác ép thuế/phí = 0; Chuyển kho giữ thuế/phí như phiếu nhập và sinh lại dòng xuất đối ứng mỗi lần lưu.
    /// - Dùng lại công thức tồn duy nhất ServiceOrderRepository.GetStockBalanceIn (phần kiểm tồn ở StockDocumentService.StockCheck.cs).
    /// </summary>
    public sealed partial class StockDocumentService
    {
        private readonly UserSession _session;
        private readonly StockDocumentKind _kind;
        private readonly StockDocumentRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public StockDocumentService(UserSession session, StockDocumentKind kind)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _kind = kind;
            _repo = new StockDocumentRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public StockDocumentKind Kind { get { return _kind; } }
        public bool CanView { get { return StockDocumentRules.Can(_session, _kind, RoleModes.View); } }
        public bool CanAdd { get { return StockDocumentRules.Can(_session, _kind, RoleModes.Add); } }
        public bool CanEdit { get { return StockDocumentRules.Can(_session, _kind, RoleModes.Edit); } }
        public bool CanRemove { get { return StockDocumentRules.Can(_session, _kind, RoleModes.Remove); } }
        public bool CanChangeDate { get { return StockDocumentRules.CanChangeDate(_session); } }
        public int PriceMode { get { return SystemConfig.GetInt(StockDocumentConstants.ConfigPriceMode, 0); } }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(_kind, forDate);
        }

        public List<WarehouseRow> LoadAllowedWarehouses()
        {
            return _repo.Read(conn => PurchaseRepository.LoadAllowedWarehousesIn(
                conn, null, _session.UserId, _session.IsAdmin, SessionStoreIds()));
        }

        public List<StockProductItem> LoadProducts()
        {
            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            return _repo.LoadProducts(sortByCode);
        }

        public StockDocumentRow LoadById(string id)
        {
            if (!CanView || string.IsNullOrEmpty(id))
                return null;
            return _repo.LoadById(id, _kind);
        }

        public List<StockDocumentLine> LoadLines(string orderId)
        {
            if (!CanView || string.IsNullOrEmpty(orderId))
                return new List<StockDocumentLine>();
            return _repo.LoadLines(orderId, _kind);
        }

        public List<StockDocumentRow> LoadList(StockDocumentFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<StockDocumentRow>();

            filter.Kind = _kind;
            if (!_session.IsAdmin && filter.AllowedWarehouseIds == null)
                filter.AllowedWarehouseIds = LoadAllowedWarehouses().Select(w => w.Id).ToList();

            List<StockDocumentRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => StockDocumentRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        /// <summary>Đọc tồn kho hiện tại bằng đúng hàm tồn dùng chung ServiceOrderRepository.GetStockBalanceIn.</summary>
        public decimal GetStockBalance(string productId, string warehouseId, string excludeOrderId = "")
        {
            return _repo.Read(conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, productId, warehouseId, excludeOrderId));
        }

        /// <summary>
        /// Lưu phiếu mới hoặc cập nhật phiếu đã lưu trong một transaction duy nhất (TDONHANG + TDONHANGCHITIET + TLUUVET).
        /// <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public StockDocumentSaveResult Save(StockDocumentRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return StockDocumentSaveResult.Fail(StockDocumentConstants.NotFoundOf(_kind));

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return StockDocumentSaveResult.Fail(PurchaseConstants.NoPermission);
            if (!isNew && !CanEdit)
                return StockDocumentSaveResult.Fail(PurchaseConstants.NoPermission);

            row.Name = (row.Name ?? "").Trim();
            row.Description = (row.Description ?? "").Trim();
            row.Note = (row.Note ?? "").Trim();

            string validationError = StockDocumentRules.Validate(row, _kind);
            if (validationError != null)
                return StockDocumentSaveResult.Fail(validationError);

            StockDocumentSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    WarehouseRow sourceWh = WarehouseRepository.LoadByIdIn(conn, tx, row.SourceWarehouseId);
                    if (sourceWh == null || sourceWh.Status != StockDocumentConstants.ActiveStatus)
                    {
                        outcome = StockDocumentSaveResult.Fail(StockDocumentConstants.SourceWarehouseRequired);
                        return;
                    }

                    WarehouseRow destWh = null;
                    if (_kind == StockDocumentKind.Transfer)
                    {
                        destWh = WarehouseRepository.LoadByIdIn(conn, tx, row.DestWarehouseId);
                        if (destWh == null || destWh.Status != StockDocumentConstants.ActiveStatus)
                        {
                            outcome = StockDocumentSaveResult.Fail(StockDocumentConstants.DestWarehouseRequired);
                            return;
                        }
                    }

                    if (!_session.IsAdmin)
                    {
                        List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                            conn, tx, _session.UserId, false, SessionStoreIds());
                        bool srcOk = allowed.Exists(w => string.Equals(w.Id, sourceWh.Id, StringComparison.Ordinal));
                        bool dstOk = _kind != StockDocumentKind.Transfer
                            || allowed.Exists(w => string.Equals(w.Id, destWh.Id, StringComparison.Ordinal));
                        if (!srcOk || !dstOk)
                        {
                            outcome = StockDocumentSaveResult.Fail(StockDocumentConstants.WarehouseNotAllowed);
                            return;
                        }
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    StockDocumentRow existing = null;
                    if (!isNew)
                    {
                        existing = StockDocumentRepository.LoadByIdIn(conn, tx, row.Id, _kind);
                        if (existing == null)
                        {
                            outcome = StockDocumentSaveResult.Fail(StockDocumentConstants.NotFoundOf(_kind));
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
                        outcome = StockDocumentSaveResult.Fail(PurchaseConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Tính lại SL đã quy đổi và THANHTIEN từng dòng; cắt chuỗi vừa 255 ký tự.
                    string lineWarehouseId = _kind == StockDocumentKind.Transfer ? row.DestWarehouseId : row.SourceWarehouseId;
                    foreach (StockDocumentLine line in row.Lines)
                    {
                        line.WarehouseId = lineWarehouseId;
                        line.ProductName = PurchaseRules.Clip(line.ProductName);
                        line.Note = PurchaseRules.Clip(line.Note);
                        StockDocumentRules.RecalcLine(line);
                    }

                    PurchaseTotals totals = StockDocumentRules.CalcTotals(
                        _kind,
                        row.Lines,
                        row.TileGiamGia,
                        row.TienGiamGia,
                        row.GiamTheoTien,
                        row.TileThue,
                        row.PhiVanChuyen);

                    row.StoreId = sourceWh.StoreId;
                    if (_kind == StockDocumentKind.Export)
                    {
                        row.DestWarehouseId = null;
                        row.ImportEmployeeId = null;
                        row.TileThue = 0m;
                        row.TienThue = 0m;
                        row.PhiVanChuyen = 0m;
                    }
                    else
                    {
                        row.SupplierId = null;
                        row.TienThue = totals.TienThue;
                    }
                    row.TienHang = totals.TienHang;
                    row.TileGiamGia = totals.TileGiamGia;
                    row.TienGiamGia = totals.TienGiamGia;
                    row.TongCong = totals.TongCong;
                    row.Loai = StockDocumentConstants.OrderTypeOf(_kind);
                    row.Status = StockDocumentConstants.ActiveStatus;
                    row.PaidFlag = StockDocumentConstants.PaidFlag;

                    string stockErr = CheckStockOnSaveIn(conn, tx, existing, sourceWh, row);
                    if (stockErr != null)
                    {
                        outcome = StockDocumentSaveResult.Fail(stockErr);
                        return;
                    }

                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        StockDocumentRepository.InsertHeaderIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (StockDocumentRepository.UpdateHeaderIn(conn, tx, row) == 0)
                        {
                            outcome = StockDocumentSaveResult.Fail(StockDocumentConstants.NotFoundOf(_kind));
                            return;
                        }
                        // Xóa sạch chi tiết cũ (gồm cả dòng xuất đối ứng của chuyển kho) trước khi sinh lại.
                        PurchaseRepository.DeleteLinesIn(conn, tx, row.Id);
                    }

                    if (_kind == StockDocumentKind.Transfer)
                    {
                        StockDocumentRepository.InsertTransferLinesIn(
                            conn, tx, row.Id, row.DestWarehouseId, row.SourceWarehouseId,
                            row.Lines, _session.UserId ?? "", serverNow);
                    }
                    else
                    {
                        StockDocumentRepository.InsertExportLinesIn(
                            conn, tx, row.Id, row.SourceWarehouseId,
                            row.Lines, _session.UserId ?? "", serverNow);
                    }

                    string label = _kind == StockDocumentKind.Transfer ? "phiếu chuyển kho" : "phiếu xuất kho";
                    string actionPrefix = isNew ? "Thêm mới " + label + ": " : "Cập nhật " + label + ": ";
                    WriteActivityLogIn(conn, tx, row.Id, row.Name, actionPrefix + row.Name, row.TongCong, serverNow);

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new StockDocumentSaveResult
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
            return outcome ?? StockDocumentSaveResult.Fail("Không thể lưu phiếu.");
        }

        /// <summary>
        /// Xóa cứng phiếu và toàn bộ dòng chi tiết (gồm cả dòng xuất đối ứng của chuyển kho) + lưu vết trong 1 transaction.
        /// Chặn khi xóa làm tồn một kho thành âm mà kho không cho phép âm kho (spec §5).
        /// </summary>
        public StockDocumentDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return StockDocumentDeleteResult.Fail(StockDocumentConstants.NotFoundOf(_kind));
            if (!CanRemove)
                return StockDocumentDeleteResult.Fail(PurchaseConstants.NoPermission);

            StockDocumentDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                StockDocumentRow existing = StockDocumentRepository.LoadByIdIn(conn, tx, id, _kind);
                if (existing == null)
                {
                    outcome = StockDocumentDeleteResult.Fail(StockDocumentConstants.NotFoundOf(_kind));
                    return;
                }

                if (!_session.IsAdmin)
                {
                    List<WarehouseRow> allowed = PurchaseRepository.LoadAllowedWarehousesIn(
                        conn, tx, _session.UserId, false, SessionStoreIds());
                    bool inScope = _kind == StockDocumentKind.Transfer
                        ? allowed.Exists(w => string.Equals(w.Id, existing.SourceWarehouseId, StringComparison.Ordinal))
                          || allowed.Exists(w => string.Equals(w.Id, existing.DestWarehouseId, StringComparison.Ordinal))
                        : allowed.Exists(w => string.Equals(w.Id, existing.SourceWarehouseId, StringComparison.Ordinal));
                    if (!inScope)
                    {
                        outcome = StockDocumentDeleteResult.Fail(StockDocumentConstants.WarehouseNotAllowed);
                        return;
                    }
                }

                string negErr = CheckStockOnDeleteIn(conn, tx, existing);
                if (negErr != null)
                {
                    outcome = StockDocumentDeleteResult.Fail(negErr);
                    return;
                }

                if (StockDocumentRepository.DeleteOrderIn(conn, tx, id, _kind) == 0)
                {
                    outcome = StockDocumentDeleteResult.Fail(StockDocumentConstants.NotFoundOf(_kind));
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string label = _kind == StockDocumentKind.Transfer ? "phiếu chuyển kho" : "phiếu xuất kho";
                WriteActivityLogIn(conn, tx, existing.Id, existing.Name, "Xóa " + label + ": " + existing.Name, existing.TongCong, serverNow);

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new StockDocumentDeleteResult { Ok = true };
            });

            return outcome ?? StockDocumentDeleteResult.Fail("Không thể xóa phiếu.");
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
                    FunctionName = StockDocumentConstants.FunctionNameOf(_kind),
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

        private static void ResetNew(StockDocumentRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
