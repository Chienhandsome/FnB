using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc dữ liệu cho chức năng Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md).
    /// Việc tải bill, tính xuất vật tư, xóa và chèn dòng XUATVATTU = 30 dùng lại trực tiếp
    /// <see cref="ServiceOrderRepository.LoadBillIn"/>, <see cref="ServicePaymentRepository.ComputeMaterialExportsIn"/>,
    /// <see cref="ServicePaymentRepository.DeleteOldMaterialExportsIn"/> và <see cref="ServicePaymentRepository.InsertMaterialExportLineIn"/>.
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed class MaterialRegenRepository
    {
        private readonly string _databasePath;

        public MaterialRegenRepository(string databasePath)
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

        /// <summary>Ngày hiện tại theo máy chủ CSDL Firebird.</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null).Date);
        }

        /// <summary>
        /// Danh sách cửa hàng đang dùng trong phạm vi người dùng: admin thấy tất cả;
        /// người dùng thường chỉ thấy cửa hàng trong phiên hoặc bảng TNGUOIDUNGTHEOCUAHANG.
        /// </summary>
        public List<StoreRecord> LoadAllowedStores(string userId, bool isAdmin, IList<string> sessionStoreIds)
        {
            return DbHelper.Read(_databasePath, conn => LoadAllowedStoresIn(conn, null, userId, isAdmin, sessionStoreIds));
        }

        public static List<StoreRecord> LoadAllowedStoresIn(
            FbConnection conn,
            FbTransaction tx,
            string userId,
            bool isAdmin,
            IList<string> sessionStoreIds)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                "@S", MaterialRegenConstants.ActiveStatus);

            HashSet<string> allowed = isAdmin
                ? null
                : ResolveAllowedStoreIdsIn(conn, tx, userId, false, sessionStoreIds);

            var list = new List<StoreRecord>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["ID"]) ?? "";
                if (string.IsNullOrEmpty(id))
                    continue;
                if (allowed != null && !allowed.Contains(id))
                    continue;
                list.Add(new StoreRecord
                {
                    Id = id,
                    Name = DbHelper.Str(r["NAME"]) ?? ""
                });
            }
            return list;
        }

        /// <summary>
        /// Tập ID cửa hàng được phép của user thường (null với admin = mọi cửa hàng).
        /// Ưu tiên danh sách cửa hàng trong phiên nếu có; nếu rỗng thì đọc từ TNGUOIDUNGTHEOCUAHANG.
        /// </summary>
        public static HashSet<string> ResolveAllowedStoreIdsIn(
            FbConnection conn,
            FbTransaction tx,
            string userId,
            bool isAdmin,
            IList<string> sessionStoreIds)
        {
            if (isAdmin)
                return null;

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            if (sessionStoreIds != null && sessionStoreIds.Count > 0)
            {
                foreach (string sid in sessionStoreIds)
                {
                    if (!string.IsNullOrEmpty(sid))
                        allowed.Add(sid);
                }
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                DataTable st = DbHelper.Table(conn, tx,
                    "SELECT DCUAHANGID FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @U",
                    "@U", userId);
                foreach (DataRow r in st.Rows)
                {
                    string sid = DbHelper.Str(r["DCUAHANGID"]);
                    if (!string.IsNullOrEmpty(sid))
                        allowed.Add(sid);
                }
            }
            return allowed;
        }

        /// <summary>
        /// Danh sách ID hóa đơn đủ điều kiện xuất lại định lượng (spec §3):
        /// TDONHANG.LOAI = 0, STATUS = 30, DATHANHTOAN = 30, DBANID không rỗng,
        /// NGAY trong [fromDate, toDate + 1 ngày), thuộc phạm vi cửa hàng của người dùng.
        /// </summary>
        public static List<string> LoadEligibleBillIdsIn(
            FbConnection conn,
            FbTransaction tx,
            DateTime fromDate,
            DateTime toDate,
            string selectedStoreId,
            bool isAdmin,
            HashSet<string> allowedStoreIds)
        {
            var result = new List<string>();
            if (!isAdmin && (allowedStoreIds == null || allowedStoreIds.Count == 0))
                return result;

            bool hasSelectedStore = !string.IsNullOrEmpty(selectedStoreId);
            if (!isAdmin && hasSelectedStore && !allowedStoreIds.Contains(selectedStoreId))
                return result;

            var args = new List<object>
            {
                "@Loai", MaterialRegenConstants.SaleOrderLoai,
                "@St", MaterialRegenConstants.ActiveStatus,
                "@Paid", MaterialRegenConstants.PaidFlagValue,
                "@From", fromDate.Date,
                "@ToExcl", toDate.Date.AddDays(1)
            };

            string storeClause = "";
            if (hasSelectedStore)
            {
                storeClause = " AND H.DCUAHANGID = @StoreId";
                args.Add("@StoreId");
                args.Add(selectedStoreId);
            }
            else if (!isAdmin)
            {
                var paramNames = new List<string>(allowedStoreIds.Count);
                int idx = 0;
                foreach (string sid in allowedStoreIds)
                {
                    string p = "@AS" + (idx++);
                    paramNames.Add(p);
                    args.Add(p);
                    args.Add(sid);
                }
                storeClause = " AND H.DCUAHANGID IN (" + string.Join(", ", paramNames.ToArray()) + ")";
            }

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID FROM TDONHANG H " +
                "WHERE COALESCE(H.LOAI, 0) = @Loai " +
                "AND COALESCE(H.STATUS, 0) = @St " +
                "AND COALESCE(H.DATHANHTOAN, 0) = @Paid " +
                "AND H.DBANID IS NOT NULL AND TRIM(H.DBANID) <> '' " +
                "AND H.NGAY >= @From AND H.NGAY < @ToExcl" + storeClause + " " +
                "ORDER BY H.NGAY, H.TIMECREATED, H.ID",
                args.ToArray());

            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["ID"]);
                if (!string.IsNullOrEmpty(id))
                    result.Add(id);
            }
            return result;
        }

        /// <summary>
        /// Đọc danh sách cặp "DMATHANGID|DKHOHANGID" của các dòng xuất vật tư hiện có (XUATVATTU = 30) của một hóa đơn.
        /// Trả về cả số lượng dòng hiện có (để đếm số dòng sẽ xóa) và khóa vật tư/kho (để kiểm tồn âm sau khi xuất lại).
        /// </summary>
        public static List<string> LoadExistingExportKeysIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT DMATHANGID, DKHOHANGID FROM TDONHANGCHITIET " +
                "WHERE TDONHANGID = @B AND COALESCE(XUATVATTU, 0) = @Flag",
                "@B", billId, "@Flag", MaterialRegenConstants.MaterialExportFlag);

            var list = new List<string>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string pid = DbHelper.Str(r["DMATHANGID"]) ?? "";
                string wid = DbHelper.Str(r["DKHOHANGID"]) ?? "";
                list.Add(pid + "|" + wid);
            }
            return list;
        }

        /// <summary>
        /// Đếm số mặt hàng phân biệt có tồn kho âm (&lt; 0) trên các cặp (DMATHANGID|DKHOHANGID) bị ảnh hưởng,
        /// dùng đúng hàm tồn kho chung <see cref="ServiceOrderRepository.GetStockBalanceIn"/>.
        /// </summary>
        public static int CountNegativeStockProductsIn(
            FbConnection conn,
            FbTransaction tx,
            IEnumerable<string> materialWarehouseKeys,
            out List<string> sampleNames)
        {
            sampleNames = new List<string>();
            if (materialWarehouseKeys == null)
                return 0;

            var negativeProductIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in materialWarehouseKeys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                int sep = key.IndexOf('|');
                if (sep <= 0 || sep >= key.Length - 1)
                    continue;
                string productId = key.Substring(0, sep);
                string warehouseId = key.Substring(sep + 1);
                if (negativeProductIds.Contains(productId))
                    continue;

                decimal balance = ServiceOrderRepository.GetStockBalanceIn(conn, tx, productId, warehouseId, "");
                if (balance < 0m)
                    negativeProductIds.Add(productId);
            }

            if (negativeProductIds.Count == 0)
                return 0;

            foreach (string pid in negativeProductIds)
            {
                if (sampleNames.Count >= 5)
                    break;
                string name = DbHelper.Str(DbHelper.Scalar(conn, tx,
                    "SELECT NAME FROM DMATHANG WHERE ID = @I", "@I", pid));
                if (!string.IsNullOrEmpty(name))
                    sampleNames.Add(name);
            }
            return negativeProductIds.Count;
        }
    }
}
