using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu Kiểm kê kho (TDONHANG LOAI = 4 + TDONHANGCHITIET) theo docs/specs/kiem-ke-kho.md.
    /// Phần sinh số phiếu và ghi/sửa/xóa đặt ở <c>StockTakeRepository.Write.cs</c>.
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed partial class StockTakeRepository
    {
        public const int OrderType = StockTakeConstants.OrderType;
        public const int ActiveStatus = StockTakeConstants.ActiveStatus;
        public const int DeletedStatus = StockTakeConstants.DeletedStatus;

        private const string SelectColumns =
            "H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, COALESCE(H.STATUS, 0) AS STATUS, " +
            "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, H.DKHONHAPID, H.DCUAHANGID, " +
            "H.DIENGIAI, H.NOTE, " +
            "H.USERCREATEDID, H.TIMECREATED, H.USERMODIFIEDID, H.TIMEMODIFIED, " +
            "COALESCE(K.NAME, '') AS WAREHOUSENAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM TDONHANG H " +
            "LEFT JOIN DKHOHANG K ON K.ID = H.DKHONHAPID " +
            "LEFT JOIN SUSER UC ON UC.ID = H.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = H.USERMODIFIEDID ";

        private readonly string _databasePath;

        public StockTakeRepository(string databasePath)
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

        public List<StockTakeRow> LoadList(StockTakeFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime from = filter.FromDate.Date;
                DateTime toExcl = filter.ToDate.Date.AddDays(1);
                if (toExcl <= from)
                    toExcl = from.AddDays(1);

                var args = new List<object>
                {
                    "@Loai", OrderType,
                    "@St", ActiveStatus,
                    "@From", from,
                    "@ToExcl", toExcl
                };

                string where =
                    "WHERE COALESCE(H.LOAI, 0) = @Loai " +
                    "AND COALESCE(H.STATUS, 0) = @St " +
                    "AND H.NGAY >= @From AND H.NGAY < @ToExcl ";

                if (!string.IsNullOrEmpty(filter.WarehouseId))
                {
                    where += "AND H.DKHONHAPID = @Wh ";
                    args.Add("@Wh");
                    args.Add(filter.WarehouseId);
                }

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " " + SelectFrom + where +
                    "ORDER BY H.NGAY DESC, H.TIMECREATED DESC, H.NAME DESC",
                    args.ToArray());

                HashSet<string> allowedWh = null;
                if (filter.AllowedWarehouseIds != null)
                    allowedWh = new HashSet<string>(filter.AllowedWarehouseIds, StringComparer.Ordinal);

                var list = new List<StockTakeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    StockTakeRow row = RowFrom(r);
                    if (allowedWh != null && !allowedWh.Contains(row.WarehouseId ?? ""))
                        continue;
                    list.Add(row);
                }
                return list;
            });
        }

        public StockTakeRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static StockTakeRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " " + SelectFrom + "WHERE H.ID = @I",
                "@I", id);
            if (dt.Rows.Count == 0)
                return null;
            StockTakeRow row = RowFrom(dt.Rows[0]);
            if (row.Loai != OrderType)
                return null;
            row.Lines = LoadLinesIn(conn, tx, id);
            return row;
        }

        public List<StockTakeLine> LoadLines(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return new List<StockTakeLine>();
            return DbHelper.Read(_databasePath, conn => LoadLinesIn(conn, null, orderId));
        }

        /// <summary>Đọc mọi dòng chi tiết của phiếu kiểm kê (gồm cả dòng chênh lệch bằng 0).</summary>
        public static List<StockTakeLine> LoadLinesIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT L.ID, L.TDONHANGID, L.DMATHANGID, L.TENHANG, L.DDONVITINHID, L.DKHOHANGID, " +
                "COALESCE(L.SLHETHONG, 0) AS SLHETHONG, COALESCE(L.SLTHUCTE, 0) AS SLTHUCTE, " +
                "COALESCE(L.SLNHAP, 0) AS SLNHAP, COALESCE(L.SLXUAT, 0) AS SLXUAT, L.NOTE, " +
                "COALESCE(M.CODE, '') AS PCODE, COALESCE(M.NAME, '') AS PNAME, " +
                "M.DDONVITINHID AS BASEUNITID, COALESCE(U1.NAME, '') AS BASEUNITNAME, " +
                "M.DDONVITINHCHANID AS BIGUNITID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                "COALESCE(M.QUYDOI, 0) AS QUYDOI, COALESCE(UL.NAME, '') AS LINEUNITNAME " +
                "FROM TDONHANGCHITIET L " +
                "LEFT JOIN DMATHANG M ON M.ID = L.DMATHANGID " +
                "LEFT JOIN DDONVITINH UL ON UL.ID = L.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                "WHERE L.TDONHANGID = @O AND COALESCE(L.STATUS, 0) = @S " +
                "ORDER BY L.TIMECREATED, L.ID",
                "@O", orderId, "@S", ActiveStatus);

            var list = new List<StockTakeLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string lineName = DbHelper.Str(r["TENHANG"]);
                string prodName = DbHelper.Str(r["PNAME"]) ?? "";
                string baseUnitId = DbHelper.Str(r["BASEUNITID"]);
                string baseUnitName = DbHelper.Str(r["BASEUNITNAME"]) ?? "";
                string unitId = DbHelper.Str(r["DDONVITINHID"]);
                if (string.IsNullOrEmpty(unitId))
                    unitId = baseUnitId;
                string unitName = DbHelper.Str(r["LINEUNITNAME"]) ?? "";
                if (unitName.Length == 0)
                    unitName = baseUnitName;

                decimal sysQty = DbHelper.Dec(r["SLHETHONG"]);
                decimal actualBase = DbHelper.Dec(r["SLTHUCTE"]);
                decimal importQty = DbHelper.Dec(r["SLNHAP"]);
                decimal exportQty = DbHelper.Dec(r["SLXUAT"]);
                decimal diff = actualBase - sysQty;

                // Dòng lưu chuẩn bằng ĐVT cơ sở; đọc lại luôn giữ đúng SLTHUCTE đã quy đổi.
                if (!string.IsNullOrEmpty(baseUnitId))
                {
                    unitId = baseUnitId;
                    unitName = baseUnitName;
                }

                list.Add(new StockTakeLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    OrderId = DbHelper.Str(r["TDONHANGID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductCode = DbHelper.Str(r["PCODE"]) ?? "",
                    ProductName = string.IsNullOrEmpty(lineName) ? prodName : lineName,
                    UnitId = unitId,
                    UnitName = unitName,
                    BaseUnitId = baseUnitId,
                    BaseUnitName = baseUnitName,
                    BigUnitId = DbHelper.Str(r["BIGUNITID"]),
                    BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                    QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                    WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                    SystemQuantity = sysQty,
                    RawActualQuantity = actualBase,
                    ActualQuantityBase = actualBase,
                    Difference = diff,
                    ImportQty = importQty,
                    ExportQty = exportQty,
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    Tone = diff < 0m
                        ? StockTakeDiffTone.Shortage
                        : (diff > 0m ? StockTakeDiffTone.Surplus : StockTakeDiffTone.Match)
                });
            }
            return list;
        }

        /// <summary>Danh sách mặt hàng đủ điều kiện kiểm kê (loại rỗng, '0' kiêm vật tư, '2' nguyên liệu; đang dùng).</summary>
        public List<StockTakeProductItem> LoadProducts(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn => LoadProductsIn(conn, null, sortByCode));
        }

        public static List<StockTakeProductItem> LoadProductsIn(FbConnection conn, FbTransaction tx, bool sortByCode)
        {
            string orderBy = sortByCode ? "M.CODE, M.NAME" : "M.NAME, M.CODE";
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT M.ID, M.CODE, M.NAME, M.DLOAIMATHANGID, " +
                "M.DDONVITINHID, COALESCE(U1.NAME, '') AS UNITNAME, " +
                "M.DDONVITINHCHANID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                "COALESCE(M.QUYDOI, 0) AS QUYDOI " +
                "FROM DMATHANG M " +
                "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                "WHERE M.STATUS = @S AND M.ID <> @Sys " +
                "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2')) " +
                "ORDER BY " + orderBy,
                "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

            var list = new List<StockTakeProductItem>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new StockTakeProductItem
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Code = DbHelper.Str(r["CODE"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    TypeId = DbHelper.Str(r["DLOAIMATHANGID"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                    BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                    QuyDoi = DbHelper.Dec(r["QUYDOI"])
                });
            }
            return list;
        }

        /// <summary>
        /// Danh sách mặt hàng theo dõi tồn của kho có tồn khác 0 mà chưa có dòng trong phiếu kiểm kê (spec §5).
        /// Dùng công thức tồn thống nhất <see cref="InventoryRepository.LoadStockBalancesByWarehouseIn"/>.
        /// </summary>
        public List<StockTakeUncountedItem> LoadUncountedItems(string stockTakeId, string warehouseId, bool sortByCode)
        {
            if (string.IsNullOrEmpty(stockTakeId) || string.IsNullOrEmpty(warehouseId))
                return new List<StockTakeUncountedItem>();

            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable countedDt = DbHelper.Table(conn, null,
                    "SELECT DISTINCT DMATHANGID FROM TDONHANGCHITIET " +
                    "WHERE TDONHANGID = @O AND COALESCE(STATUS, 0) = @S AND DMATHANGID IS NOT NULL",
                    "@O", stockTakeId, "@S", ActiveStatus);

                var countedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DataRow r in countedDt.Rows)
                {
                    string pid = DbHelper.Str(r["DMATHANGID"]);
                    if (!string.IsNullOrEmpty(pid))
                        countedIds.Add(pid);
                }

                Dictionary<string, decimal> balances = InventoryRepository.LoadStockBalancesByWarehouseIn(conn, null, warehouseId);
                List<StockTakeProductItem> products = LoadProductsIn(conn, null, sortByCode);

                var result = new List<StockTakeUncountedItem>();
                foreach (StockTakeProductItem p in products)
                {
                    if (countedIds.Contains(p.Id))
                        continue;
                    decimal balance;
                    balances.TryGetValue(p.Id, out balance);
                    if (balance == 0m)
                        continue;
                    result.Add(new StockTakeUncountedItem
                    {
                        ProductId = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        UnitName = p.UnitName,
                        SystemQuantity = balance
                    });
                }
                return result;
            });
        }

        private static StockTakeRow RowFrom(DataRow r)
        {
            return new StockTakeRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                Loai = DbHelper.Int(r["LOAI"]),
                Status = DbHelper.Int(r["STATUS"]),
                PaidFlag = DbHelper.Int(r["DATHANHTOAN"]),
                WarehouseId = DbHelper.Str(r["DKHONHAPID"]),
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                WarehouseName = DbHelper.Str(r["WAREHOUSENAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? "",
                Lines = new List<StockTakeLine>()
            };
        }
    }
}
