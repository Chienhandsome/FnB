using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho báo cáo "Chi tiết hoạt động ngày" (docs/specs/chi-tiet-hoat-dong-ngay.md).
    /// Mọi câu SQL đều tham số hóa; dùng lại <see cref="BillReportScopeSql"/> cho bill và TTHUCHI;
    /// gom 5 truy vấn dữ liệu trong một phiên kết nối.
    /// </summary>
    public sealed class DailyActivityRepository
    {
        private readonly string _databasePath;

        public DailyActivityRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Ngày hiện tại theo máy chủ CSDL Firebird (CURRENT_TIMESTAMP).</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
                return v == null || v is DBNull ? DbHelper.Now().Date : Convert.ToDateTime(v).Date;
            });
        }

        /// <summary>
        /// Danh sách cửa hàng được phép cho combo lọc: admin thấy tất cả cửa hàng đang dùng;
        /// user thường chỉ thấy cửa hàng gắn trong TNGUOIDUNGTHEOCUAHANG.
        /// </summary>
        public List<StoreRecord> LoadAllowedStores(string userId, bool isAdmin)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt;
                if (isAdmin)
                {
                    dt = DbHelper.Table(conn, null,
                        "SELECT ID, NAME FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                        "@S", BillReportScope.ActiveStatus);
                }
                else
                {
                    if (string.IsNullOrEmpty(userId))
                        return new List<StoreRecord>();
                    dt = DbHelper.Table(conn, null,
                        "SELECT DISTINCT C.ID, C.NAME " +
                        "FROM TNGUOIDUNGTHEOCUAHANG T " +
                        "INNER JOIN DCUAHANG C ON C.ID = T.DCUAHANGID " +
                        "WHERE T.SUSERID = @UserId AND C.STATUS = @S " +
                        "ORDER BY C.NAME",
                        "@UserId", userId,
                        "@S", BillReportScope.ActiveStatus);
                }

                var list = new List<StoreRecord>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new StoreRecord
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Danh sách thu ngân (SUSER đang dùng, kể cả Admin) cho combo lọc.</summary>
        public List<CashierRecord> LoadCashiers()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, USERNAME FROM SUSER WHERE STATUS = @S ORDER BY NAME, USERNAME",
                    "@S", BillReportScope.ActiveStatus);

                var list = new List<CashierRecord>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new CashierRecord
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = (DbHelper.Str(r["NAME"]) ?? "").Trim(),
                        UserName = (DbHelper.Str(r["USERNAME"]) ?? "").Trim()
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc thông tin công ty (tên, địa chỉ, điện thoại, email) từ SCONFIG qua ConfigRepository.</summary>
        public DailyCompanyInfo LoadCompanyInfo()
        {
            Dictionary<string, ConfigValue> values = new ConfigRepository(_databasePath).LoadValues();
            return new DailyCompanyInfo
            {
                Name = ConfigText(values, "CompanyName"),
                Address = ConfigText(values, "CompanyAddress"),
                Phone = ConfigText(values, "CompanyPhone"),
                Email = ConfigText(values, "CompanyEmail")
            };
        }

        /// <summary>
        /// Tải toàn bộ 5 tập dữ liệu (bán hàng, mặt hàng bán, giảm giá tổng bill, nhập hàng, thu-chi)
        /// cùng thông tin công ty trong một kết nối chỉ đọc.
        /// </summary>
        public DailyActivityReportData LoadReport(DailyActivityFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            DailyCompanyInfo company = LoadCompanyInfo();
            BillReportScope scope = filter.ToBillScope();

            return DbHelper.Read(_databasePath, conn =>
            {
                return new DailyActivityReportData
                {
                    Company = company,
                    SaleBills = LoadSaleBills(conn, scope, filter.CashierId),
                    SoldItems = LoadSoldItems(conn, scope, filter.CashierId),
                    BillTotalDiscount = LoadBillTotalDiscount(conn, scope, filter.CashierId),
                    Purchases = LoadPurchases(conn, filter),
                    CashFlows = LoadCashFlows(conn, scope, filter.CashierId)
                };
            });
        }

        /// <summary>Khối 1: hóa đơn bán đã thanh toán (LOAI = 0, DATHANHTOAN = 30, STATUS = 30), lọc thu ngân theo USERTHANHTOANID, sắp NGAY, NAME.</summary>
        private static List<DailySaleBillRow> LoadSaleBills(FbConnection conn, BillReportScope scope, string cashierId)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);
            BillReportScopeSql.AppendWhere("H", scope, conditions, args, "Sale");

            if (!string.IsNullOrEmpty(cashierId))
            {
                conditions.Add("H.USERTHANHTOANID = @SaleCashierId");
                args.Add("@SaleCashierId");
                args.Add(cashierId);
            }

            string sql =
                "SELECT " +
                "H.ID, H.NAME, H.NGAY, H.GIOTHANHTOAN, " +
                "K.NAME AS KHACHHANG, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.TIENMAT, 0) AS TIENMAT, " +
                "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, " +
                "COALESCE(H.THE, 0) AS THE, " +
                "COALESCE(H.VOUCHER, 0) AS VOUCHER, " +
                "COALESCE(H.TRUTICHLUY, 0) AS TRUTICHLUY, " +
                "COALESCE(H.THETRATRUOC, 0) AS THETRATRUOC, " +
                "COALESCE(H.CONGNO, 0) AS CONGNO, " +
                "COALESCE(H.DATTRUOC, 0) AS DATTRUOC " +
                "FROM TDONHANG H " +
                "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "ORDER BY H.NGAY, H.NAME";

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var list = new List<DailySaleBillRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new DailySaleBillRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Ngay = ReadDateTime(r["NGAY"]),
                    PaidTime = ReadDateTime(r["GIOTHANHTOAN"]),
                    CustomerName = DbHelper.Str(r["KHACHHANG"]) ?? "",
                    TongCong = DbHelper.Dec(r["TONGCONG"]),
                    TienMat = DbHelper.Dec(r["TIENMAT"]),
                    ChuyenKhoan = DbHelper.Dec(r["CHUYENKHOAN"]),
                    The = DbHelper.Dec(r["THE"]),
                    Voucher = DbHelper.Dec(r["VOUCHER"]),
                    TruTichLuy = DbHelper.Dec(r["TRUTICHLUY"]),
                    TheTraTruoc = DbHelper.Dec(r["THETRATRUOC"]),
                    CongNo = DbHelper.Dec(r["CONGNO"]),
                    DatTruoc = DbHelper.Dec(r["DATTRUOC"])
                });
            }
            return list;
        }

        /// <summary>
        /// Khối 2: gộp theo (TENHANG, DMATHANG.CODE, DDONVITINHID, DONGIA, TILEGIAMGIA), INNER JOIN DMATHANG;
        /// loại combo con và xuất vật tư; KHÔNG loại mặt hàng nguyên liệu; sắp theo TENHANG.
        /// </summary>
        private static List<DailySoldItemRow> LoadSoldItems(FbConnection conn, BillReportScope scope, string cashierId)
        {
            var conditions = new List<string>(10);
            var args = new List<object>(20);
            BillReportScopeSql.AppendWhere("H", scope, conditions, args, "Item");

            if (!string.IsNullOrEmpty(cashierId))
            {
                conditions.Add("H.USERTHANHTOANID = @ItemCashierId");
                args.Add("@ItemCashierId");
                args.Add(cashierId);
            }

            conditions.Add("COALESCE(D.COMBOPARENTID, '') = ''");
            conditions.Add("COALESCE(D.XUATVATTU, 0) = 0");

            string sql =
                "SELECT " +
                "M.CODE AS MAHANG, " +
                "D.TENHANG, " +
                "D.DDONVITINHID, " +
                "U.NAME AS DVT, " +
                "COALESCE(D.DONGIA, 0) AS DONGIA, " +
                "COALESCE(D.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                "SUM(COALESCE(D.SLXUATCHUAQUYDOI, 0) - COALESCE(D.SLNHAPCHUAQUYDOI, 0)) AS SOLUONG, " +
                "SUM(COALESCE(D.THANHTIEN, 0)) AS THANHTIEN " +
                "FROM TDONHANGCHITIET D " +
                "INNER JOIN TDONHANG H ON H.ID = D.TDONHANGID " +
                "INNER JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "GROUP BY D.TENHANG, M.CODE, D.DDONVITINHID, U.NAME, D.DONGIA, D.TILEGIAMGIA " +
                "ORDER BY D.TENHANG, M.CODE, D.DONGIA, D.TILEGIAMGIA";

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var list = new List<DailySoldItemRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new DailySoldItemRow
                {
                    ProductCode = DbHelper.Str(r["MAHANG"]) ?? "",
                    ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "",
                    UnitName = DbHelper.Str(r["DVT"]) ?? "",
                    Quantity = DbHelper.Dec(r["SOLUONG"]),
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    DiscountRate = DbHelper.Dec(r["TILEGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"])
                });
            }
            return list;
        }

        /// <summary>Truy vấn riêng GiamGiaTongBill = Σ TIENGIAMGIA của bill cùng phạm vi (không nhân theo số dòng chi tiết).</summary>
        private static decimal LoadBillTotalDiscount(FbConnection conn, BillReportScope scope, string cashierId)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);
            BillReportScopeSql.AppendWhere("H", scope, conditions, args, "Disc");

            if (!string.IsNullOrEmpty(cashierId))
            {
                conditions.Add("H.USERTHANHTOANID = @DiscCashierId");
                args.Add("@DiscCashierId");
                args.Add(cashierId);
            }

            string sql =
                "SELECT COALESCE(SUM(COALESCE(H.TIENGIAMGIA, 0)), 0) " +
                "FROM TDONHANG H " +
                "WHERE " + string.Join(" AND ", conditions.ToArray());

            return DbHelper.Dec(DbHelper.Scalar(conn, null, sql, args.ToArray()));
        }

        /// <summary>
        /// Khối 3: phiếu nhập kho (TDONHANG.LOAI = 1, KHÔNG lọc STATUS hay DATHANHTOAN),
        /// lọc thu ngân theo USERCREATEDID, lọc cửa hàng gián tiếp qua DKHOHANG (DKHONHAPID),
        /// user thường ràng buộc qua TNGUOIDUNGTHEOCUAHANG cả khi để trống; DKHONHAPID NULL mất khi có lọc cửa hàng
        /// hoặc user thường, admin để trống vẫn thấy. Sắp NGAY, NAME.
        /// </summary>
        private static List<DailyPurchaseRow> LoadPurchases(FbConnection conn, DailyActivityFilter filter)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);

            conditions.Add("H.LOAI = @PurLoai");
            args.Add("@PurLoai");
            args.Add(DailyActivityConstants.PurchaseOrderLoai);

            conditions.Add("H.NGAY >= @PurFrom");
            args.Add("@PurFrom");
            args.Add(filter.FromDate.Date);

            conditions.Add("H.NGAY < @PurToExcl");
            args.Add("@PurToExcl");
            args.Add(filter.ToDate.Date.AddDays(1));

            if (!string.IsNullOrEmpty(filter.CashierId))
            {
                conditions.Add("H.USERCREATEDID = @PurCashierId");
                args.Add("@PurCashierId");
                args.Add(filter.CashierId);
            }

            if (!string.IsNullOrEmpty(filter.StoreId))
            {
                conditions.Add("H.DKHONHAPID IN (SELECT K.ID FROM DKHOHANG K WHERE K.DCUAHANGID = @PurStoreId)");
                args.Add("@PurStoreId");
                args.Add(filter.StoreId);
            }

            if (!filter.IsAdmin)
            {
                conditions.Add(
                    "EXISTS (SELECT 1 FROM DKHOHANG K " +
                    "INNER JOIN TNGUOIDUNGTHEOCUAHANG UCH ON UCH.DCUAHANGID = K.DCUAHANGID " +
                    "WHERE K.ID = H.DKHONHAPID AND UCH.SUSERID = @PurUserId)");
                args.Add("@PurUserId");
                args.Add(filter.UserId ?? "");
            }

            string sql =
                "SELECT " +
                "H.ID, H.NAME, H.NGAY, " +
                "N.NAME AS NHACUNGCAP, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, " +
                "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, " +
                "COALESCE(H.LOAITHANHTOAN, 0) AS LOAITHANHTOAN " +
                "FROM TDONHANG H " +
                "LEFT JOIN DNHACUNGCAP N ON N.ID = H.DNHACUNGCAPID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "ORDER BY H.NGAY, H.NAME";

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var list = new List<DailyPurchaseRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new DailyPurchaseRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Ngay = ReadDateTime(r["NGAY"]),
                    SupplierName = DbHelper.Str(r["NHACUNGCAP"]) ?? "",
                    TongCong = DbHelper.Dec(r["TONGCONG"]),
                    TienThanhToan = DbHelper.Dec(r["TIENTHANHTOAN"]),
                    ChuyenKhoan = DbHelper.Dec(r["CHUYENKHOAN"]),
                    LoaiThanhToan = DbHelper.Dec(r["LOAITHANHTOAN"])
                });
            }
            return list;
        }

        /// <summary>
        /// Khối 4: TTHUCHI dùng chung BillReportScopeSql.AppendDateAndStoreWhere (không lọc STATUS, LOAI, LATAMUNG),
        /// lọc thu ngân theo USERCREATEDID; tách tiền mặt (CHUYENKHOAN NULL/0) và chuyển khoản (khác 0). Sắp NGAY, NAME.
        /// </summary>
        private static List<DailyCashFlowRow> LoadCashFlows(FbConnection conn, BillReportScope scope, string cashierId)
        {
            var conditions = new List<string>(6);
            var args = new List<object>(12);
            BillReportScopeSql.AppendDateAndStoreWhere("T", scope, conditions, args, "Tc");

            if (!string.IsNullOrEmpty(cashierId))
            {
                conditions.Add("T.USERCREATEDID = @TcCashierId");
                args.Add("@TcCashierId");
                args.Add(cashierId);
            }

            string sql =
                "SELECT " +
                "T.ID, T.NAME, T.NGAY, T.DIENGIAI, T.TENDOITUONG, " +
                "CASE WHEN COALESCE(T.CHUYENKHOAN, 0) = 0 THEN COALESCE(T.THU, 0) ELSE 0 END AS THUTM, " +
                "CASE WHEN COALESCE(T.CHUYENKHOAN, 0) = 0 THEN COALESCE(T.CHI, 0) ELSE 0 END AS CHITM, " +
                "CASE WHEN COALESCE(T.CHUYENKHOAN, 0) <> 0 THEN COALESCE(T.THU, 0) ELSE 0 END AS THUCK, " +
                "CASE WHEN COALESCE(T.CHUYENKHOAN, 0) <> 0 THEN COALESCE(T.CHI, 0) ELSE 0 END AS CHICK " +
                "FROM TTHUCHI T " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "ORDER BY T.NGAY, T.NAME";

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var list = new List<DailyCashFlowRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new DailyCashFlowRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Ngay = ReadDateTime(r["NGAY"]),
                    Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                    ObjectName = DbHelper.Str(r["TENDOITUONG"]) ?? "",
                    CashIn = DbHelper.Dec(r["THUTM"]),
                    CashOut = DbHelper.Dec(r["CHITM"]),
                    TransferIn = DbHelper.Dec(r["THUCK"]),
                    TransferOut = DbHelper.Dec(r["CHICK"])
                });
            }
            return list;
        }

        private static string ConfigText(Dictionary<string, ConfigValue> map, string key)
        {
            ConfigValue val;
            if (map != null && map.TryGetValue(key, out val) && val != null && !string.IsNullOrWhiteSpace(val.TextValue))
                return val.TextValue.Trim();
            return "";
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            if (v is DateTime)
                return (DateTime)v;
            if (v is TimeSpan)
                return DateTime.Today.Add((TimeSpan)v);
            return Convert.ToDateTime(v);
        }
    }
}
