using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu Xuất khác (TDONHANG LOAI = 2) và Chuyển kho (TDONHANG LOAI = 3) theo docs/specs/phieu-xuat-chuyen-kho.md.
    /// Phần sinh số phiếu và ghi/sửa/xóa (kể cả dòng xuất đối ứng của chuyển kho) đặt ở <c>StockDocumentRepository.Write.cs</c>.
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed partial class StockDocumentRepository
    {
        public const int ActiveStatus = StockDocumentConstants.ActiveStatus;
        public const int DeletedStatus = StockDocumentConstants.DeletedStatus;

        private const string SelectColumns =
            "H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, COALESCE(H.STATUS, 0) AS STATUS, " +
            "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, H.DKHOXUATID, H.DKHONHAPID, H.DCUAHANGID, " +
            "H.DNHACUNGCAPID, H.DNHANVIENXUATID, H.DNHANVIENNHAPID, H.DIENGIAI, H.NOTE, " +
            "COALESCE(H.TIENHANG, 0) AS TIENHANG, COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
            "COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, COALESCE(H.GIAMTHEOTIEN, 0) AS GIAMTHEOTIEN, " +
            "COALESCE(H.TILETHUE, 0) AS TILETHUE, COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
            "COALESCE(H.PHIVANCHUYEN, 0) AS PHIVANCHUYEN, COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
            "H.USERCREATEDID, H.TIMECREATED, H.USERMODIFIEDID, H.TIMEMODIFIED, " +
            "COALESCE(KX.NAME, '') AS SRCWHNAME, COALESCE(KN.NAME, '') AS DSTWHNAME, " +
            "COALESCE(NCC.NAME, '') AS SUPPLIERNAME, " +
            "COALESCE(NVX.NAME, '') AS EXPEMPNAME, COALESCE(NVN.NAME, '') AS IMPEMPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM TDONHANG H " +
            "LEFT JOIN DKHOHANG KX ON KX.ID = H.DKHOXUATID " +
            "LEFT JOIN DKHOHANG KN ON KN.ID = H.DKHONHAPID " +
            "LEFT JOIN DNHACUNGCAP NCC ON NCC.ID = H.DNHACUNGCAPID " +
            "LEFT JOIN DNHANVIEN NVX ON NVX.ID = H.DNHANVIENXUATID " +
            "LEFT JOIN DNHANVIEN NVN ON NVN.ID = H.DNHANVIENNHAPID " +
            "LEFT JOIN SUSER UC ON UC.ID = H.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = H.USERMODIFIEDID ";

        private readonly string _databasePath;

        public StockDocumentRepository(string databasePath)
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

        public List<StockDocumentRow> LoadList(StockDocumentFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime from = filter.FromDate.Date;
                DateTime toExcl = filter.ToDate.Date.AddDays(1);
                if (toExcl <= from)
                    toExcl = from.AddDays(1);

                int loai = StockDocumentConstants.OrderTypeOf(filter.Kind);
                var args = new List<object>
                {
                    "@Loai", loai,
                    "@St", ActiveStatus,
                    "@From", from,
                    "@ToExcl", toExcl
                };

                string where =
                    "WHERE COALESCE(H.LOAI, 0) = @Loai " +
                    "AND COALESCE(H.STATUS, 0) = @St " +
                    "AND H.NGAY >= @From AND H.NGAY < @ToExcl ";

                if (!string.IsNullOrEmpty(filter.SourceWarehouseId))
                {
                    where += "AND H.DKHOXUATID = @SWh ";
                    args.Add("@SWh");
                    args.Add(filter.SourceWarehouseId);
                }

                if (filter.Kind == StockDocumentKind.Transfer && !string.IsNullOrEmpty(filter.DestWarehouseId))
                {
                    where += "AND H.DKHONHAPID = @DWh ";
                    args.Add("@DWh");
                    args.Add(filter.DestWarehouseId);
                }

                if (filter.Kind == StockDocumentKind.Export && !string.IsNullOrEmpty(filter.SupplierId))
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

                var list = new List<StockDocumentRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    StockDocumentRow row = RowFrom(r);
                    if (allowedWh != null)
                    {
                        bool inScope = filter.Kind == StockDocumentKind.Transfer
                            ? allowedWh.Contains(row.SourceWarehouseId ?? "") || allowedWh.Contains(row.DestWarehouseId ?? "")
                            : allowedWh.Contains(row.SourceWarehouseId ?? "");
                        if (!inScope)
                            continue;
                    }
                    list.Add(row);
                }
                return list;
            });
        }

        public StockDocumentRow LoadById(string id, StockDocumentKind kind)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id, kind));
        }

        public static StockDocumentRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id, StockDocumentKind kind)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " " + SelectFrom + "WHERE H.ID = @I",
                "@I", id);
            if (dt.Rows.Count == 0)
                return null;
            StockDocumentRow row = RowFrom(dt.Rows[0]);
            if (row.Loai != StockDocumentConstants.OrderTypeOf(kind))
                return null;
            row.Lines = LoadLinesIn(conn, tx, id, kind);
            return row;
        }

        public List<StockDocumentLine> LoadLines(string orderId, StockDocumentKind kind)
        {
            if (string.IsNullOrEmpty(orderId))
                return new List<StockDocumentLine>();
            return DbHelper.Read(_databasePath, conn => LoadLinesIn(conn, null, orderId, kind));
        }

        /// <summary>
        /// Đọc dòng chi tiết hiển thị:
        /// - Xuất khác: dòng có SLXUAT &gt; 0.
        /// - Chuyển kho (spec §3): chỉ lấy dòng nhập có SLNHAP &gt; 0 (ẩn dòng xuất đối ứng).
        /// </summary>
        public static List<StockDocumentLine> LoadLinesIn(FbConnection conn, FbTransaction tx, string orderId, StockDocumentKind kind)
        {
            string qtyFilter = kind == StockDocumentKind.Transfer
                ? "AND COALESCE(L.SLNHAP, 0) > 0 "
                : "AND COALESCE(L.SLXUAT, 0) > 0 ";

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT L.ID, L.TDONHANGID, L.DMATHANGID, L.TENHANG, L.DDONVITINHID, L.DKHOHANGID, " +
                "COALESCE(L.SLNHAP, 0) AS SLNHAP, COALESCE(L.SLNHAPCHUAQUYDOI, 0) AS SLNHAPCHUAQUYDOI, " +
                "COALESCE(L.SLXUAT, 0) AS SLXUAT, COALESCE(L.SLXUATCHUAQUYDOI, 0) AS SLXUATCHUAQUYDOI, " +
                "COALESCE(L.DONGIA, 0) AS DONGIA, COALESCE(L.THANHTIEN, 0) AS THANHTIEN, L.NOTE, " +
                "COALESCE(M.CODE, '') AS PCODE, COALESCE(M.NAME, '') AS PNAME, " +
                "M.DDONVITINHID AS BASEUNITID, COALESCE(U1.NAME, '') AS BASEUNITNAME, " +
                "M.DDONVITINHCHANID AS BIGUNITID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                "COALESCE(M.QUYDOI, 0) AS QUYDOI, COALESCE(UL.NAME, '') AS LINEUNITNAME " +
                "FROM TDONHANGCHITIET L " +
                "LEFT JOIN DMATHANG M ON M.ID = L.DMATHANGID " +
                "LEFT JOIN DDONVITINH UL ON UL.ID = L.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                "WHERE L.TDONHANGID = @O AND COALESCE(L.STATUS, 0) = @S " + qtyFilter +
                "ORDER BY L.TIMECREATED, L.ID",
                "@O", orderId, "@S", ActiveStatus);

            var list = new List<StockDocumentLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string lineName = DbHelper.Str(r["TENHANG"]);
                string prodName = DbHelper.Str(r["PNAME"]) ?? "";
                decimal converted = kind == StockDocumentKind.Transfer
                    ? DbHelper.Dec(r["SLNHAP"])
                    : DbHelper.Dec(r["SLXUAT"]);
                decimal raw = kind == StockDocumentKind.Transfer
                    ? DbHelper.Dec(r["SLNHAPCHUAQUYDOI"])
                    : DbHelper.Dec(r["SLXUATCHUAQUYDOI"]);
                if (raw == 0m && converted != 0m)
                    raw = converted;

                string unitId = DbHelper.Str(r["DDONVITINHID"]);
                string baseUnitId = DbHelper.Str(r["BASEUNITID"]);
                if (string.IsNullOrEmpty(unitId))
                    unitId = baseUnitId;

                string lineUnitName = DbHelper.Str(r["LINEUNITNAME"]) ?? "";
                if (lineUnitName.Length == 0)
                    lineUnitName = DbHelper.Str(r["BASEUNITNAME"]) ?? "";

                list.Add(new StockDocumentLine
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
                    RawQuantity = raw,
                    ConvertedQuantity = converted,
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Note = DbHelper.Str(r["NOTE"]) ?? ""
                });
            }
            return list;
        }

        /// <summary>Danh sách mặt hàng đủ điều kiện xuất / chuyển kho (loại rỗng, '0' kiêm vật tư, '2' nguyên liệu; đang dùng).</summary>
        public List<StockProductItem> LoadProducts(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string orderBy = sortByCode ? "M.CODE, M.NAME" : "M.NAME, M.CODE";
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.CODE, M.NAME, M.DLOAIMATHANGID, " +
                    "M.DDONVITINHID, COALESCE(U1.NAME, '') AS UNITNAME, " +
                    "M.DDONVITINHCHANID, COALESCE(U2.NAME, '') AS BIGUNITNAME, " +
                    "COALESCE(M.QUYDOI, 0) AS QUYDOI, " +
                    "COALESCE(M.GIABAN, 0) AS GIABAN, COALESCE(M.GIANHAP, 0) AS GIANHAP, COALESCE(M.GIAVON, 0) AS GIAVON " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DDONVITINH U1 ON U1.ID = M.DDONVITINHID " +
                    "LEFT JOIN DDONVITINH U2 ON U2.ID = M.DDONVITINHCHANID " +
                    "WHERE M.STATUS = @S AND M.ID <> @Sys " +
                    "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0', '2')) " +
                    "ORDER BY " + orderBy,
                    "@S", ActiveStatus, "@Sys", ProductRepository.SystemProductId);

                var list = new List<StockProductItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new StockProductItem
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
                        SellPrice = DbHelper.Dec(r["GIABAN"]),
                        BuyPrice = DbHelper.Dec(r["GIANHAP"]),
                        CostPrice = DbHelper.Dec(r["GIAVON"])
                    });
                }
                return list;
            });
        }

        private static StockDocumentRow RowFrom(DataRow r)
        {
            return new StockDocumentRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                Loai = DbHelper.Int(r["LOAI"]),
                Status = DbHelper.Int(r["STATUS"]),
                PaidFlag = DbHelper.Int(r["DATHANHTOAN"]),
                SourceWarehouseId = DbHelper.Str(r["DKHOXUATID"]),
                DestWarehouseId = DbHelper.Str(r["DKHONHAPID"]),
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                SupplierId = DbHelper.Str(r["DNHACUNGCAPID"]),
                ExportEmployeeId = DbHelper.Str(r["DNHANVIENXUATID"]),
                ImportEmployeeId = DbHelper.Str(r["DNHANVIENNHAPID"]),
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
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                SourceWarehouseName = DbHelper.Str(r["SRCWHNAME"]) ?? "",
                DestWarehouseName = DbHelper.Str(r["DSTWHNAME"]) ?? "",
                SupplierName = DbHelper.Str(r["SUPPLIERNAME"]) ?? "",
                ExportEmployeeName = DbHelper.Str(r["EXPEMPNAME"]) ?? "",
                ImportEmployeeName = DbHelper.Str(r["IMPEMPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? "",
                Lines = new List<StockDocumentLine>()
            };
        }
    }
}
