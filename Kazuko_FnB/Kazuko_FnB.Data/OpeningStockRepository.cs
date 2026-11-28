using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu Tồn kho ban đầu (TDONHANG LOAI = 97, NAME = 'TONDAU' + TDONHANGCHITIET)
    /// theo docs/specs/so-du-dau-ky.md §2 (P1).
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed class OpeningStockRepository
    {
        public const int OrderType = OpeningStockConstants.OrderType;
        public const int ActiveStatus = OpeningStockConstants.ActiveStatus;
        public const int PaidFlag = OpeningStockConstants.PaidFlag;

        private readonly string _databasePath;

        public OpeningStockRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public T Read<T>(Func<FbConnection, T> work)
        {
            return DbHelper.Read(_databasePath, work);
        }

        /// <summary>Danh sách kho đang dùng trong phạm vi người dùng (admin thấy hết; non-admin chỉ thấy kho của cửa hàng mình).</summary>
        public List<WarehouseRow> LoadAllowedWarehouses(string userId, bool isAdmin, IList<string> sessionStoreIds)
        {
            return DbHelper.Read(_databasePath, conn =>
                PurchaseRepository.LoadAllowedWarehousesIn(conn, null, userId, isAdmin, sessionStoreIds));
        }

        /// <summary>Kho mặc định gắn trên cửa hàng đăng nhập (DCUAHANG.DKHOHANGID), hoặc null nếu chưa gán.</summary>
        public string GetStoreDefaultWarehouseId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT DKHOHANGID FROM DCUAHANG WHERE ID = @I AND STATUS = @S",
                    "@I", storeId, "@S", ActiveStatus)));
        }

        /// <summary>Tìm các phiếu tồn kho ban đầu (LOAI = 97, STATUS = 30) của một kho, sắp theo NGAY, TIMECREATED, ID tăng dần.</summary>
        public static List<OpeningStockHeader> FindOpeningOrdersIn(FbConnection conn, FbTransaction tx, string warehouseId)
        {
            var list = new List<OpeningStockHeader>();
            if (string.IsNullOrEmpty(warehouseId))
                return list;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, NGAY, DKHONHAPID, DCUAHANGID, COALESCE(TONGCONG, 0) AS TONGCONG " +
                "FROM TDONHANG " +
                "WHERE DKHONHAPID = @W AND COALESCE(LOAI, 0) = @L AND COALESCE(STATUS, 0) = @S " +
                "ORDER BY NGAY ASC, TIMECREATED ASC, ID ASC",
                "@W", warehouseId, "@L", OrderType, "@S", ActiveStatus);

            foreach (DataRow r in dt.Rows)
            {
                list.Add(new OpeningStockHeader
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Date = r["NGAY"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(r["NGAY"]).Date,
                    WarehouseId = DbHelper.Str(r["DKHONHAPID"]) ?? "",
                    StoreId = DbHelper.Str(r["DCUAHANGID"]),
                    TotalAmount = DbHelper.Dec(r["TONGCONG"])
                });
            }
            return list;
        }

        /// <summary>Đọc toàn bộ dòng chi tiết đang dùng (STATUS = 30) của một phiếu tồn kho ban đầu.</summary>
        public static List<OpeningStockLineRecord> LoadOrderLinesIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            var list = new List<OpeningStockLineRecord>();
            if (string.IsNullOrEmpty(orderId))
                return list;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                "COALESCE(SLNHAP, 0) AS SLNHAP, COALESCE(DONGIA, 0) AS DONGIA, COALESCE(THANHTIEN, 0) AS THANHTIEN " +
                "FROM TDONHANGCHITIET " +
                "WHERE TDONHANGID = @O AND COALESCE(STATUS, 0) = @S " +
                "ORDER BY TIMECREATED ASC, ID ASC",
                "@O", orderId, "@S", ActiveStatus);

            foreach (DataRow r in dt.Rows)
            {
                list.Add(new OpeningStockLineRecord
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    OrderId = DbHelper.Str(r["TDONHANGID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductName = DbHelper.Str(r["TENHANG"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
                    WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                    Quantity = DbHelper.Dec(r["SLNHAP"]),
                    CostPrice = DbHelper.Dec(r["DONGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"])
                });
            }
            return list;
        }

        /// <summary>
        /// Nạp toàn bộ màn Tồn kho ban đầu cho một kho trong cùng một kết nối đọc:
        /// phiếu LOAI = 97 đầu tiên theo ngày (nếu có) + mọi mặt hàng theo dõi tồn đang dùng
        /// (loại rỗng, '0' kiêm vật tư, '2' nguyên liệu; bỏ 'KM').
        /// </summary>
        public OpeningStockDocument LoadWarehouseDocument(string warehouseId, DateTime defaultDate, bool sortByCode)
        {
            if (string.IsNullOrEmpty(warehouseId))
                return new OpeningStockDocument { Date = defaultDate.Date };

            return DbHelper.Read(_databasePath, conn =>
            {
                WarehouseRow wh = WarehouseRepository.LoadByIdIn(conn, null, warehouseId);
                List<OpeningStockHeader> headers = FindOpeningOrdersIn(conn, null, warehouseId);
                OpeningStockHeader firstHeader = headers.Count > 0 ? headers[0] : null;

                var lineByProduct = new Dictionary<string, OpeningStockLineRecord>(StringComparer.Ordinal);
                if (firstHeader != null)
                {
                    List<OpeningStockLineRecord> lines = LoadOrderLinesIn(conn, null, firstHeader.Id);
                    foreach (OpeningStockLineRecord l in lines)
                    {
                        if (string.IsNullOrEmpty(l.ProductId))
                            continue;
                        OpeningStockLineRecord existing;
                        if (lineByProduct.TryGetValue(l.ProductId, out existing))
                        {
                            existing.Quantity += l.Quantity;
                            existing.Amount += l.Amount;
                        }
                        else
                        {
                            lineByProduct[l.ProductId] = l;
                        }
                    }
                }

                string orderBy = sortByCode ? "M.CODE, M.NAME" : "M.NAME, M.CODE";
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.CODE, M.MASANCO, M.NAME, M.DDONVITINHID, COALESCE(U.NAME, '') AS UNITNAME, " +
                    "COALESCE(M.GIAVON, 0) AS GIAVON " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "WHERE M.STATUS = @S AND M.ID <> @Sys " +
                    "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2')) " +
                    "ORDER BY " + orderBy,
                    "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

                var doc = new OpeningStockDocument
                {
                    OrderId = firstHeader != null ? firstHeader.Id : null,
                    WarehouseId = warehouseId,
                    WarehouseName = wh != null ? (wh.Name ?? "") : "",
                    Date = firstHeader != null && firstHeader.Date != DateTime.MinValue ? firstHeader.Date : defaultDate.Date,
                    DuplicateOrderCount = headers.Count,
                    Warning = headers.Count > 1 ? OpeningStockConstants.DuplicateOrdersWarning(headers.Count) : null
                };

                foreach (DataRow r in dt.Rows)
                {
                    string pid = DbHelper.Str(r["ID"]) ?? "";
                    decimal defaultCost = DbHelper.Dec(r["GIAVON"]);
                    OpeningStockLineRecord line;
                    bool hasLine = lineByProduct.TryGetValue(pid, out line);

                    decimal qty = hasLine ? line.Quantity : 0m;
                    decimal cost = hasLine ? line.CostPrice : defaultCost;
                    decimal amt = Math.Round(qty * cost, 0, MidpointRounding.AwayFromZero);

                    doc.Items.Add(new OpeningStockItemRow
                    {
                        ProductId = pid,
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        Barcode = DbHelper.Str(r["MASANCO"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        UnitId = DbHelper.Str(r["DDONVITINHID"]),
                        UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                        LineId = hasLine ? line.Id : null,
                        DefaultCostPrice = defaultCost,
                        Quantity = qty,
                        CostPrice = cost,
                        Amount = amt,
                        OriginalQuantity = qty,
                        OriginalCostPrice = cost
                    });
                }

                return doc;
            });
        }

        public static void InsertHeaderIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            DateTime date,
            string warehouseId,
            string storeId,
            decimal totalAmount,
            string actorId,
            DateTime createdAt)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHONHAPID, DCUAHANGID, " +
                "DIENGIAI, NOTE, " +
                "TIENHANG, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, " +
                "TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG, " +
                "TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @NGAY, @LOAI, @ST, @PAID, @WH, @STORE, " +
                "@DESC, NULL, " +
                "@TH, 0, 0, 0, " +
                "0, 0, 0, @TC, " +
                "0, 0, 0, " +
                "@UC, @TCRE, NULL, NULL)",
                "@ID", orderId,
                "@NAME", OpeningStockConstants.OrderName,
                "@NGAY", date.Date,
                "@LOAI", OrderType,
                "@ST", ActiveStatus,
                "@PAID", PaidFlag,
                "@WH", NullIfEmpty(warehouseId),
                "@STORE", NullIfEmpty(storeId),
                "@DESC", OpeningStockConstants.DefaultDescription,
                "@TH", totalAmount,
                "@TC", totalAmount,
                "@UC", actorId ?? "",
                "@TCRE", createdAt);
        }

        public static int UpdateHeaderIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            DateTime date,
            string warehouseId,
            string storeId,
            decimal totalAmount,
            string actorId,
            DateTime modifiedAt)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "NAME = @NAME, NGAY = @NGAY, DKHONHAPID = @WH, DCUAHANGID = @STORE, " +
                "STATUS = @ST, DATHANHTOAN = @PAID, " +
                "TIENHANG = @TH, TILEGIAMGIA = 0, TIENGIAMGIA = 0, GIAMTHEOTIEN = 0, " +
                "TILETHUE = 0, TIENTHUE = 0, PHIVANCHUYEN = 0, TONGCONG = @TC, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD " +
                "WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@NAME", OpeningStockConstants.OrderName,
                "@NGAY", date.Date,
                "@WH", NullIfEmpty(warehouseId),
                "@STORE", NullIfEmpty(storeId),
                "@ST", ActiveStatus,
                "@PAID", PaidFlag,
                "@TH", totalAmount,
                "@TC", totalAmount,
                "@UM", actorId ?? "",
                "@TMOD", modifiedAt,
                "@ID", orderId,
                "@LOAI", OrderType);
        }

        public static string InsertLineIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string productId,
            string productName,
            string unitId,
            string warehouseId,
            decimal quantity,
            decimal costPrice,
            decimal amount,
            string actorId,
            DateTime createdAt)
        {
            string lineId = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (" +
                "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                "SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, " +
                "DONGIA, TILEGIAMGIA, THANHTIEN, NOTE, XUATVATTU, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                "@SLN, @SLN, 0, 0, " +
                "@PRICE, 0, @AMT, NULL, 0, @ST, " +
                "@UC, @TC, NULL, NULL)",
                "@ID", lineId,
                "@OID", orderId,
                "@PID", NullIfEmpty(productId),
                "@NAME", productName ?? "",
                "@UID", NullIfEmpty(unitId),
                "@WID", NullIfEmpty(warehouseId),
                "@SLN", quantity,
                "@PRICE", costPrice,
                "@AMT", amount,
                "@ST", ActiveStatus,
                "@UC", actorId ?? "",
                "@TC", createdAt);
            return lineId;
        }

        public static int UpdateLineIn(
            FbConnection conn,
            FbTransaction tx,
            string lineId,
            string productName,
            string unitId,
            string warehouseId,
            decimal quantity,
            decimal costPrice,
            decimal amount,
            string actorId,
            DateTime modifiedAt)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANGCHITIET SET " +
                "TENHANG = @NAME, DDONVITINHID = @UID, DKHOHANGID = @WID, " +
                "SLNHAP = @SLN, SLNHAPCHUAQUYDOI = @SLN, SLXUAT = 0, SLXUATCHUAQUYDOI = 0, " +
                "DONGIA = @PRICE, TILEGIAMGIA = 0, THANHTIEN = @AMT, " +
                "STATUS = @ST, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM " +
                "WHERE ID = @ID",
                "@NAME", productName ?? "",
                "@UID", NullIfEmpty(unitId),
                "@WID", NullIfEmpty(warehouseId),
                "@SLN", quantity,
                "@PRICE", costPrice,
                "@AMT", amount,
                "@ST", ActiveStatus,
                "@UM", actorId ?? "",
                "@TM", modifiedAt,
                "@ID", lineId);
        }

        public static int DeleteLineIn(FbConnection conn, FbTransaction tx, string lineId)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TDONHANGCHITIET WHERE ID = @ID", "@ID", lineId);
        }

        public static int DeleteOrderIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            PurchaseRepository.DeleteLinesIn(conn, tx, orderId);
            return DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANG WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@ID", orderId, "@LOAI", OrderType);
        }

        public static void CountAndSumActiveLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            out int lineCount,
            out decimal totalAmount)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT COUNT(*) AS CNT, COALESCE(SUM(COALESCE(THANHTIEN, 0)), 0) AS TOTAL " +
                "FROM TDONHANGCHITIET WHERE TDONHANGID = @O AND COALESCE(STATUS, 0) = @S",
                "@O", orderId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
            {
                lineCount = 0;
                totalAmount = 0m;
                return;
            }
            lineCount = DbHelper.Int(dt.Rows[0]["CNT"]);
            totalAmount = DbHelper.Dec(dt.Rows[0]["TOTAL"]);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
