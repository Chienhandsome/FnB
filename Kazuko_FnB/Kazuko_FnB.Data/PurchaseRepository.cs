using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu nhập kho (TDONHANG LOAI = 1 + TDONHANGCHITIET) theo docs/specs/phieu-nhap-kho.md.
    /// Phần sinh số phiếu, kiểm trùng và ghi/sửa/xóa nằm ở <c>PurchaseRepository.Write.cs</c>.
    /// Mọi câu SQL tham số hóa; chỉ dùng các cột đã xác minh trong schema thật; không đặt @param trong
    /// NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed partial class PurchaseRepository
    {
        public const int OrderType = PurchaseConstants.OrderType;
        public const int ActiveStatus = PurchaseConstants.ActiveStatus;
        public const int DeletedStatus = PurchaseConstants.DeletedStatus;

        private const string SelectColumns =
            "H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, COALESCE(H.STATUS, 0) AS STATUS, " +
            "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, H.DKHONHAPID, H.DCUAHANGID, H.DNHACUNGCAPID, " +
            "H.DNHANVIENNHAPID, H.DIENGIAI, H.NOTE, " +
            "COALESCE(H.TIENHANG, 0) AS TIENHANG, COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
            "COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, COALESCE(H.GIAMTHEOTIEN, 0) AS GIAMTHEOTIEN, " +
            "COALESCE(H.TILETHUE, 0) AS TILETHUE, COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
            "COALESCE(H.PHIVANCHUYEN, 0) AS PHIVANCHUYEN, COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
            "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, COALESCE(H.TIENMAT, 0) AS TIENMAT, " +
            "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, H.DTAIKHOANNGANHANGID, " +
            "H.USERCREATEDID, H.TIMECREATED, H.USERMODIFIEDID, H.TIMEMODIFIED, " +
            "COALESCE(K.NAME, '') AS WAREHOUSENAME, COALESCE(NCC.NAME, '') AS SUPPLIERNAME, " +
            "COALESCE(NV.NAME, '') AS EMPLOYEENAME, COALESCE(UC.NAME, '') AS CREATEDBY, " +
            "COALESCE(UM.NAME, '') AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM TDONHANG H " +
            "LEFT JOIN DKHOHANG K ON K.ID = H.DKHONHAPID " +
            "LEFT JOIN DNHACUNGCAP NCC ON NCC.ID = H.DNHACUNGCAPID " +
            "LEFT JOIN DNHANVIEN NV ON NV.ID = H.DNHANVIENNHAPID " +
            "LEFT JOIN SUSER UC ON UC.ID = H.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = H.USERMODIFIEDID ";

        private readonly string _databasePath;

        public PurchaseRepository(string databasePath)
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

        public List<PurchaseRow> LoadList(PurchaseFilter filter)
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

                if (!string.IsNullOrEmpty(filter.SupplierId))
                {
                    where += "AND H.DNHACUNGCAPID = @Supp ";
                    args.Add("@Supp");
                    args.Add(filter.SupplierId);
                }

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " " + SelectFrom + where +
                    "ORDER BY H.NGAY DESC, H.TIMECREATED DESC, H.NAME DESC",
                    args.ToArray());

                HashSet<string> allowedWh = null;
                if (filter.AllowedWarehouseIds != null)
                    allowedWh = new HashSet<string>(filter.AllowedWarehouseIds, StringComparer.Ordinal);

                var list = new List<PurchaseRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    PurchaseRow row = RowFrom(r);
                    if (allowedWh != null && !allowedWh.Contains(row.WarehouseId ?? ""))
                        continue;
                    list.Add(row);
                }
                return list;
            });
        }

        public PurchaseRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static PurchaseRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " " + SelectFrom + "WHERE H.ID = @I",
                "@I", id);
            if (dt.Rows.Count == 0)
                return null;
            PurchaseRow row = RowFrom(dt.Rows[0]);
            row.Lines = LoadLinesIn(conn, tx, id);
            return row;
        }

        public List<PurchaseLine> LoadLines(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return new List<PurchaseLine>();
            return DbHelper.Read(_databasePath, conn => LoadLinesIn(conn, null, orderId));
        }

        public static List<PurchaseLine> LoadLinesIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT L.ID, L.TDONHANGID, L.DMATHANGID, L.TENHANG, L.DDONVITINHID, L.DKHOHANGID, " +
                "COALESCE(L.SLNHAP, 0) AS SLNHAP, COALESCE(L.SLNHAPCHUAQUYDOI, 0) AS SLNHAPCHUAQUYDOI, " +
                "COALESCE(L.DONGIA, 0) AS DONGIA, COALESCE(L.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                "COALESCE(L.THANHTIEN, 0) AS THANHTIEN, L.NOTE, " +
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

            var list = new List<PurchaseLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string lineName = DbHelper.Str(r["TENHANG"]);
                string prodName = DbHelper.Str(r["PNAME"]) ?? "";
                decimal slNhap = DbHelper.Dec(r["SLNHAP"]);
                decimal slRaw = DbHelper.Dec(r["SLNHAPCHUAQUYDOI"]);
                if (slRaw == 0m && slNhap != 0m)
                    slRaw = slNhap;

                string unitId = DbHelper.Str(r["DDONVITINHID"]);
                string baseUnitId = DbHelper.Str(r["BASEUNITID"]);
                if (string.IsNullOrEmpty(unitId))
                    unitId = baseUnitId;

                string lineUnitName = DbHelper.Str(r["LINEUNITNAME"]) ?? "";
                if (lineUnitName.Length == 0)
                    lineUnitName = DbHelper.Str(r["BASEUNITNAME"]) ?? "";

                list.Add(new PurchaseLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    OrderId = DbHelper.Str(r["TDONHANGID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductCode = DbHelper.Str(r["PCODE"]) ?? "",
                    ProductName = string.IsNullOrEmpty(lineName) ? prodName : lineName,
                    UnitId = unitId,
                    UnitName = lineUnitName,
                    BaseUnitId = baseUnitId,
                    BaseUnitName = DbHelper.Str(r["BASEUNITNAME"]) ?? "",
                    BigUnitId = DbHelper.Str(r["BIGUNITID"]),
                    BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                    QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                    WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                    RawQuantity = slRaw,
                    ConvertedQuantity = slNhap,
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    DiscountPct = DbHelper.Dec(r["TILEGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Note = DbHelper.Str(r["NOTE"]) ?? ""
                });
            }
            return list;
        }

        /// <summary>
        /// Danh sách mặt hàng đủ điều kiện nhập kho (spec §2: loại nguyên liệu '2', kiêm vật tư '0' hoặc không có loại; đang dùng).
        /// </summary>
        public List<PurchaseProductItem> LoadProducts(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string orderBy = sortByCode ? "M.CODE, M.NAME" : "M.NAME, M.CODE";
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.CODE, M.NAME, M.DLOAIMATHANGID, " +
                    "M.DDONVITINHID, COALESCE(U1.NAME, '') AS UNITNAME, " +
                    "M.DDONVITINHCHANID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                    "COALESCE(M.QUYDOI, 0) AS QUYDOI, COALESCE(M.GIANHAP, 0) AS GIANHAP " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                    "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                    "WHERE M.STATUS = @S AND M.ID <> @Sys " +
                    "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2')) " +
                    "ORDER BY " + orderBy,
                    "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

                var list = new List<PurchaseProductItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new PurchaseProductItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        TypeId = DbHelper.Str(r["DLOAIMATHANGID"]) ?? "",
                        UnitId = DbHelper.Str(r["DDONVITINHID"]),
                        UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                        BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                        BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                        QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                        BuyPrice = DbHelper.Dec(r["GIANHAP"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Danh sách kho đang dùng trong phạm vi người dùng: admin thấy hết, user thường chỉ thấy kho thuộc cửa hàng được gán.
        /// </summary>
        public List<WarehouseRow> LoadAllowedWarehouses(string userId, bool isAdmin, IList<string> sessionStoreIds)
        {
            return DbHelper.Read(_databasePath, conn => LoadAllowedWarehousesIn(conn, null, userId, isAdmin, sessionStoreIds));
        }

        public static List<WarehouseRow> LoadAllowedWarehousesIn(
            FbConnection conn,
            FbTransaction tx,
            string userId,
            bool isAdmin,
            IList<string> sessionStoreIds)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT K.ID, K.NAME, K.DCUAHANGID, COALESCE(K.CHOPHEPAMKHO, 0) AS CHOPHEPAMKHO, " +
                "K.NOTE, COALESCE(K.STATUS, 0) AS STATUS, COALESCE(CH.NAME, '') AS STORENAME " +
                "FROM DKHOHANG K LEFT JOIN DCUAHANG CH ON CH.ID = K.DCUAHANGID " +
                "WHERE K.STATUS = @S ORDER BY K.NAME",
                "@S", ActiveStatus);

            HashSet<string> allowedStores = null;
            if (!isAdmin)
            {
                allowedStores = new HashSet<string>(StringComparer.Ordinal);
                if (sessionStoreIds != null && sessionStoreIds.Count > 0)
                {
                    foreach (string sid in sessionStoreIds)
                        if (!string.IsNullOrEmpty(sid))
                            allowedStores.Add(sid);
                }
                else if (!string.IsNullOrEmpty(userId))
                {
                    DataTable st = DbHelper.Table(conn, tx,
                        "SELECT DCUAHANGID FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @U",
                        "@U", userId);
                    foreach (DataRow sr in st.Rows)
                    {
                        string sid = DbHelper.Str(sr["DCUAHANGID"]);
                        if (!string.IsNullOrEmpty(sid))
                            allowedStores.Add(sid);
                    }
                }
            }

            var list = new List<WarehouseRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string storeId = DbHelper.Str(r["DCUAHANGID"]);
                if (allowedStores != null && !allowedStores.Contains(storeId ?? ""))
                    continue;
                list.Add(new WarehouseRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    StoreId = storeId,
                    StoreName = DbHelper.Str(r["STORENAME"]) ?? "",
                    AllowNegativeStock = DbHelper.Int(r["CHOPHEPAMKHO"]),
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    Status = DbHelper.Int(r["STATUS"])
                });
            }
            return list;
        }

        private static PurchaseRow RowFrom(DataRow r)
        {
            decimal transfer = DbHelper.Dec(r["CHUYENKHOAN"]);
            decimal paid = DbHelper.Dec(r["TIENTHANHTOAN"]);
            PayMethod? method = paid == 0m
                ? PayMethod.Cash
                : (transfer != 0m ? PayMethod.Transfer : PayMethod.Cash);

            return new PurchaseRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                Loai = DbHelper.Int(r["LOAI"]),
                Status = DbHelper.Int(r["STATUS"]),
                PaidFlag = DbHelper.Int(r["DATHANHTOAN"]),
                WarehouseId = DbHelper.Str(r["DKHONHAPID"]),
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                SupplierId = DbHelper.Str(r["DNHACUNGCAPID"]),
                EmployeeId = DbHelper.Str(r["DNHANVIENNHAPID"]),
                Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                TienHang = DbHelper.Dec(r["TIENHANG"]),
                TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                GiamTheoTien = DbHelper.Int(r["GIAMTHEOTIEN"]) == ActiveStatus,
                TileThue = DbHelper.Dec(r["TILETHUE"]),
                TienThue = DbHelper.Dec(r["TIENTHUE"]),
                PhiVanChuyen = DbHelper.Dec(r["PHIVANCHUYEN"]),
                TongCong = DbHelper.Dec(r["TONGCONG"]),
                PaidAmount = paid,
                PaymentMethod = method,
                CashAmount = DbHelper.Dec(r["TIENMAT"]),
                TransferAmount = transfer,
                BankAccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                WarehouseName = DbHelper.Str(r["WAREHOUSENAME"]) ?? "",
                SupplierName = DbHelper.Str(r["SUPPLIERNAME"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPLOYEENAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? "",
                Lines = new List<PurchaseLine>()
            };
        }
    }
}
