using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho màn Tồn kho (docs/specs/ton-kho.md).
    /// Công thức tồn thống nhất 100% với <see cref="ServiceOrderRepository.GetStockBalanceIn"/>
    /// (STATUS = 30 và LOAI &lt;&gt; 0 hoặc LOAI = 0 với DATHANHTOAN = 30).
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed class InventoryRepository
    {
        public const int ActiveStatus = InventoryConstants.ActiveStatus;

        private readonly string _databasePath;

        public InventoryRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>
        /// Đọc tồn kho một mặt hàng tại một kho bằng đúng hàm dùng chung
        /// <see cref="ServiceOrderRepository.GetStockBalanceIn"/>.
        /// </summary>
        public decimal GetStockBalance(string productId, string warehouseId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(warehouseId))
                return 0m;
            return DbHelper.Read(_databasePath, conn =>
                ServiceOrderRepository.GetStockBalanceIn(conn, null, productId, warehouseId, ""));
        }

        /// <summary>
        /// Đọc tồn kho gộp theo mặt hàng cho toàn bộ một kho trong một truy vấn.
        /// Giữ nguyên điều kiện lọc (STATUS = 30, LOAI &lt;&gt; 0 hoặc LOAI = 0 và DATHANHTOAN = 30)
        /// và công thức SUM(SLNHAP - SLXUAT) của <see cref="ServiceOrderRepository.GetStockBalanceIn"/>.
        /// </summary>
        public Dictionary<string, decimal> LoadStockBalancesByWarehouse(string warehouseId)
        {
            if (string.IsNullOrEmpty(warehouseId))
                return new Dictionary<string, decimal>(StringComparer.Ordinal);
            return DbHelper.Read(_databasePath, conn => LoadStockBalancesByWarehouseIn(conn, null, warehouseId));
        }

        public static Dictionary<string, decimal> LoadStockBalancesByWarehouseIn(
            FbConnection conn,
            FbTransaction tx,
            string warehouseId)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(warehouseId))
                return map;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT C.DMATHANGID, COALESCE(SUM(COALESCE(C.SLNHAP, 0) - COALESCE(C.SLXUAT, 0)), 0) AS TONKHO " +
                "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " +
                "WHERE C.DKHOHANGID = @W " +
                "AND C.DMATHANGID IS NOT NULL " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "AND (COALESCE(H.LOAI, 0) <> 0 OR (COALESCE(H.LOAI, 0) = 0 AND COALESCE(H.DATHANHTOAN, 0) = 30)) " +
                "GROUP BY C.DMATHANGID",
                "@W", warehouseId);

            foreach (DataRow r in dt.Rows)
            {
                string pid = DbHelper.Str(r["DMATHANGID"]);
                if (!string.IsNullOrEmpty(pid))
                    map[pid] = DbHelper.Dec(r["TONKHO"]);
            }
            return map;
        }

        /// <summary>
        /// Danh sách kho đang dùng trong phạm vi người dùng (admin thấy hết; non-admin chỉ thấy kho của cửa hàng mình).
        /// Dùng lại trực tiếp <see cref="PurchaseRepository.LoadAllowedWarehousesIn"/>.
        /// </summary>
        public List<WarehouseRow> LoadAllowedWarehouses(string userId, bool isAdmin, IList<string> sessionStoreIds)
        {
            return DbHelper.Read(_databasePath, conn =>
                PurchaseRepository.LoadAllowedWarehousesIn(conn, null, userId, isAdmin, sessionStoreIds));
        }

        /// <summary>Kho mặc định được gắn trên cửa hàng đăng nhập (DCUAHANG.DKHOHANGID), hoặc null nếu chưa gán.</summary>
        public string GetStoreDefaultWarehouseId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT DKHOHANGID FROM DCUAHANG WHERE ID = @I AND STATUS = @S",
                    "@I", storeId, "@S", ActiveStatus)));
        }

        /// <summary>
        /// Toàn bộ nhóm mặt hàng đang dùng (STATUS = 30, gồm cả nhóm nguyên liệu DLOAIDOID = '4'),
        /// sắp theo SORTORDER, NAME để dựng cây lọc nhóm bên trái.
        /// </summary>
        public List<ProductGroupRow> LoadProductGroups()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER " +
                    "FROM DNHOMMATHANG WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);

                var list = new List<ProductGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ProductGroupRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        DrinkTypeId = DbHelper.Str(r["DLOAIDOID"]) ?? "",
                        ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Loại mặt hàng theo dõi tồn kho đang dùng (DLOAIMATHANG ID '0' kiêm vật tư và '2' nguyên liệu).</summary>
        public List<LookupItem> LoadStockProductTypes()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLOAIMATHANG WHERE STATUS = @S AND ID IN ('0', '2') ORDER BY ID",
                    "@S", ActiveStatus);

                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    string name = DbHelper.Str(r["NAME"]) ?? "";
                    if (id.Length > 0 && name.Length > 0)
                        list.Add(new LookupItem { Id = id, Name = name });
                }
                return list;
            });
        }

        /// <summary>
        /// Tải danh sách mặt hàng theo dõi tồn kho (loại rỗng, '0' kiêm vật tư, '2' nguyên liệu; STATUS = 30; bỏ 'KM')
        /// kèm số tồn tại <paramref name="warehouseId"/> trong cùng một kết nối đọc.
        /// </summary>
        public List<InventoryItemRow> LoadStockItems(string warehouseId, bool sortByCode)
        {
            if (string.IsNullOrEmpty(warehouseId))
                return new List<InventoryItemRow>();

            return DbHelper.Read(_databasePath, conn =>
            {
                Dictionary<string, decimal> balances = LoadStockBalancesByWarehouseIn(conn, null, warehouseId);
                return LoadStockItemsWithBalancesIn(conn, balances, sortByCode);
            });
        }

        /// <summary>
        /// Tải danh sách mặt hàng theo dõi tồn kho cho màn Tồn nhiều kho trong một kết nối đọc,
        /// dùng lại đúng <see cref="LoadStockBalancesByWarehouseIn"/> (một công thức tồn duy nhất) cho từng kho được phép.
        /// Nếu <paramref name="selectedWarehouseId"/> rỗng/null thì tổng hợp tồn trên mọi kho trong <paramref name="allowedWarehouseIds"/>;
        /// ngược lại lưới chính lấy tồn của riêng kho <paramref name="selectedWarehouseId"/>, còn <paramref name="balancesByWarehouse"/>
        /// vẫn chứa tồn chi tiết của mọi kho được phép.
        /// </summary>
        public List<InventoryItemRow> LoadStockItemsForWarehouses(
            IList<string> allowedWarehouseIds,
            bool sortByCode,
            out Dictionary<string, Dictionary<string, decimal>> balancesByWarehouse,
            string selectedWarehouseId = null)
        {
            var perWh = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);
            balancesByWarehouse = perWh;
            if (allowedWarehouseIds == null || allowedWarehouseIds.Count == 0)
                return new List<InventoryItemRow>();

            List<InventoryItemRow> result = DbHelper.Read(_databasePath, conn =>
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string whId in allowedWarehouseIds)
                {
                    if (string.IsNullOrEmpty(whId) || !seen.Add(whId))
                        continue;
                    perWh[whId] = LoadStockBalancesByWarehouseIn(conn, null, whId);
                }

                Dictionary<string, decimal> mainBalances;
                if (!string.IsNullOrEmpty(selectedWarehouseId))
                {
                    if (!perWh.TryGetValue(selectedWarehouseId, out mainBalances))
                        mainBalances = new Dictionary<string, decimal>(StringComparer.Ordinal);
                }
                else
                {
                    mainBalances = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    foreach (Dictionary<string, decimal> whMap in perWh.Values)
                    {
                        foreach (KeyValuePair<string, decimal> kv in whMap)
                        {
                            decimal acc;
                            mainBalances.TryGetValue(kv.Key, out acc);
                            mainBalances[kv.Key] = acc + kv.Value;
                        }
                    }
                }

                return LoadStockItemsWithBalancesIn(conn, mainBalances, sortByCode);
            });

            balancesByWarehouse = perWh;
            return result;
        }

        /// <summary>
        /// Đọc tồn kho một mặt hàng trên từng kho được phép bằng đúng hàm dùng chung
        /// <see cref="ServiceOrderRepository.GetStockBalanceIn"/> trong một kết nối đọc.
        /// </summary>
        public Dictionary<string, decimal> LoadProductStockByWarehouses(string productId, IList<string> allowedWarehouseIds)
        {
            var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(productId) || allowedWarehouseIds == null || allowedWarehouseIds.Count == 0)
                return result;

            return DbHelper.Read(_databasePath, conn =>
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string whId in allowedWarehouseIds)
                {
                    if (string.IsNullOrEmpty(whId) || !seen.Add(whId))
                        continue;
                    result[whId] = ServiceOrderRepository.GetStockBalanceIn(conn, null, productId, whId, "");
                }
                return result;
            });
        }

        /// <summary>Đọc thông tin cơ bản (đơn vị, quy đổi, giá vốn) của một mặt hàng theo dõi tồn kho đang dùng.</summary>
        public InventoryItemRow LoadStockItemById(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return null;

            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.CODE, M.NAME, M.DLOAIMATHANGID, COALESCE(T.NAME, '') AS TYPENAME, " +
                    "M.DNHOMMATHANGID, COALESCE(G.NAME, '') AS GROUPNAME, " +
                    "M.DDONVITINHID, COALESCE(U1.NAME, '') AS UNITNAME, " +
                    "M.DDONVITINHCHANID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                    "COALESCE(M.QUYDOI, 0) AS QUYDOI, " +
                    "COALESCE(M.TONTOITHIEU, 0) AS TONTOITHIEU, " +
                    "COALESCE(M.GIANHAP, 0) AS GIANHAP, " +
                    "COALESCE(M.GIAVON, 0) AS GIAVON, " +
                    "COALESCE(M.GIABAN, 0) AS GIABAN " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DLOAIMATHANG T ON T.ID = M.DLOAIMATHANGID " +
                    "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                    "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                    "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                    "WHERE M.ID = @I AND M.STATUS = @S AND M.ID <> @Sys " +
                    "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2'))",
                    "@I", productId, "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

                if (dt.Rows.Count == 0)
                    return null;
                return MapStockItemRow(dt.Rows[0], 0m);
            });
        }

        private static List<InventoryItemRow> LoadStockItemsWithBalancesIn(
            FbConnection conn,
            Dictionary<string, decimal> balances,
            bool sortByCode)
        {
            string orderBy = sortByCode ? "M.CODE, M.NAME" : "M.NAME, M.CODE";

            DataTable dt = DbHelper.Table(conn, null,
                "SELECT M.ID, M.CODE, M.NAME, M.DLOAIMATHANGID, COALESCE(T.NAME, '') AS TYPENAME, " +
                "M.DNHOMMATHANGID, COALESCE(G.NAME, '') AS GROUPNAME, " +
                "M.DDONVITINHID, COALESCE(U1.NAME, '') AS UNITNAME, " +
                "M.DDONVITINHCHANID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                "COALESCE(M.QUYDOI, 0) AS QUYDOI, " +
                "COALESCE(M.TONTOITHIEU, 0) AS TONTOITHIEU, " +
                "COALESCE(M.GIANHAP, 0) AS GIANHAP, " +
                "COALESCE(M.GIAVON, 0) AS GIAVON, " +
                "COALESCE(M.GIABAN, 0) AS GIABAN " +
                "FROM DMATHANG M " +
                "LEFT JOIN DLOAIMATHANG T ON T.ID = M.DLOAIMATHANGID " +
                "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                "WHERE M.STATUS = @S AND M.ID <> @Sys " +
                "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2')) " +
                "ORDER BY " + orderBy,
                "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

            var list = new List<InventoryItemRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string pid = DbHelper.Str(r["ID"]) ?? "";
                decimal stock;
                balances.TryGetValue(pid, out stock);
                list.Add(MapStockItemRow(r, stock));
            }
            return list;
        }

        private static InventoryItemRow MapStockItemRow(DataRow r, decimal stock)
        {
            string rawTypeId = DbHelper.Str(r["DLOAIMATHANGID"]) ?? "";
            return new InventoryItemRow
            {
                ProductId = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                TypeId = string.IsNullOrEmpty(rawTypeId) ? InventoryConstants.TypeProductWithStock : rawTypeId,
                TypeName = DbHelper.Str(r["TYPENAME"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMMATHANGID"]) ?? "",
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                UnitId = DbHelper.Str(r["DDONVITINHID"]),
                UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                StockQuantity = stock,
                MinStock = DbHelper.Dec(r["TONTOITHIEU"]),
                BuyPrice = DbHelper.Dec(r["GIANHAP"]),
                CostPrice = DbHelper.Dec(r["GIAVON"]),
                SellPrice = DbHelper.Dec(r["GIABAN"])
            };
        }
    }
}
