using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho màn "Thống kê doanh thu" (docs/specs/thong-ke-doanh-thu.md).
    /// Không có câu SQL ghi; mọi giá trị truyền bằng tham số SQL; phạm vi bill và phạm vi ngày + cửa hàng
    /// dùng chung <see cref="BillReportScopeSql"/>.
    /// </summary>
    public sealed class RevenueReportRepository
    {
        private readonly string _databasePath;

        public RevenueReportRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Ngày hiện tại theo máy chủ CSDL Firebird (CURRENT_TIMESTAMP), không dùng DateTime.Today.</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
                return v == null || v is DBNull ? DbHelper.Now().Date : Convert.ToDateTime(v).Date;
            });
        }

        /// <summary>
        /// Danh sách cửa hàng cho combo lọc: admin thấy tất cả cửa hàng đang dùng;
        /// user thường chỉ thấy cửa hàng gắn với user trong TNGUOIDUNGTHEOCUAHANG.
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

        /// <summary>
        /// Danh sách bill trong phạm vi <see cref="BillReportScopeSql.AppendWhere"/>,
        /// sắp tăng dần theo NGAY, GIOTHANHTOAN (spec §1, §2.1). Khóa ngoại rỗng trả chuỗi rỗng (LEFT JOIN);
        /// các cột tiền NULL quy về 0.
        /// </summary>
        public List<RevenueBillRow> LoadBills(BillReportScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException("scope");

            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(8);
                var args = new List<object>(16);
                BillReportScopeSql.AppendWhere("H", scope, conditions, args);

                string sql =
                    "SELECT " +
                    "H.ID, H.NAME, H.NGAY, " +
                    "B.NAME AS TENBAN, " +
                    "H.BATDAU, H.KETTHUC, H.GIOTHANHTOAN, " +
                    "K.NAME AS TENKHACHHANG, K.DIACHI, K.MAKHACH, " +
                    "U.NAME AS THANHTOANBOI, " +
                    "H.NOTE, " +
                    "COALESCE(H.TIENHANGCHUAGIAM, 0) AS TIENHANGCHUAGIAM, " +
                    "COALESCE(H.TIENGIO, 0) AS TIENGIO, " +
                    "COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                    "COALESCE(H.GIAMGIAMATHANG, 0) AS GIAMGIAMATHANG, " +
                    "COALESCE(H.TIENGIAMGIAGIO, 0) AS TIENGIAMGIAGIO, " +
                    "COALESCE(H.PHIDICHVU, 0) AS PHIDICHVU, " +
                    "COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
                    "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                    "COALESCE(H.TIENMAT, 0) AS TIENMAT, " +
                    "COALESCE(H.VOUCHER, 0) AS VOUCHER, " +
                    "COALESCE(H.THE, 0) AS THE, " +
                    "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, " +
                    "COALESCE(H.TRUTICHLUY, 0) AS TRUTICHLUY, " +
                    "COALESCE(H.THETRATRUOC, 0) AS THETRATRUOC, " +
                    "COALESCE(H.CONGNO, 0) AS CONGNO " +
                    "FROM TDONHANG H " +
                    "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                    "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                    "LEFT JOIN SUSER U ON U.ID = H.USERTHANHTOANID " +
                    "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                    "ORDER BY H.NGAY, H.GIOTHANHTOAN, H.NAME";

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<RevenueBillRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new RevenueBillRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Ngay = ReadDateTime(r["NGAY"]),
                        TableName = DbHelper.Str(r["TENBAN"]) ?? "",
                        StartTime = ReadDateTime(r["BATDAU"]),
                        EndTime = ReadDateTime(r["KETTHUC"]),
                        PaidTime = ReadDateTime(r["GIOTHANHTOAN"]),
                        CustomerName = DbHelper.Str(r["TENKHACHHANG"]) ?? "",
                        PaidBy = DbHelper.Str(r["THANHTOANBOI"]) ?? "",
                        Note = DbHelper.Str(r["NOTE"]) ?? "",
                        CustomerAddress = DbHelper.Str(r["DIACHI"]) ?? "",
                        CustomerCode = DbHelper.Str(r["MAKHACH"]) ?? "",
                        TienHangChuaGiam = DbHelper.Dec(r["TIENHANGCHUAGIAM"]),
                        TienGio = DbHelper.Dec(r["TIENGIO"]),
                        TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                        GiamGiaMatHang = DbHelper.Dec(r["GIAMGIAMATHANG"]),
                        TienGiamGiaGio = DbHelper.Dec(r["TIENGIAMGIAGIO"]),
                        PhiDichVu = DbHelper.Dec(r["PHIDICHVU"]),
                        TienThue = DbHelper.Dec(r["TIENTHUE"]),
                        TongCong = DbHelper.Dec(r["TONGCONG"]),
                        TienMat = DbHelper.Dec(r["TIENMAT"]),
                        Voucher = DbHelper.Dec(r["VOUCHER"]),
                        The = DbHelper.Dec(r["THE"]),
                        ChuyenKhoan = DbHelper.Dec(r["CHUYENKHOAN"]),
                        TruTichLuy = DbHelper.Dec(r["TRUTICHLUY"]),
                        TheTraTruoc = DbHelper.Dec(r["THETRATRUOC"]),
                        CongNo = DbHelper.Dec(r["CONGNO"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Chi tiết bill đang chọn (TDONHANGCHITIET, spec §2.2):
        /// loại XUATVATTU khác 0, mặt hàng nguyên liệu (DMATHANG.DLOAIMATHANGID = '2', LEFT JOIN để giữ dòng không có mặt hàng),
        /// và dòng con combo (COMBOPARENTID không rỗng). Truyền billId rỗng trả danh sách trống.
        /// </summary>
        public List<RevenueBillLine> LoadBillLines(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return new List<RevenueBillLine>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT " +
                    "D.ID, D.DMATHANGID, " +
                    "D.TENHANG, " +
                    "U.NAME AS DVT, " +
                    "D.SLXUATCHUAQUYDOI, " +
                    "D.DONGIA, " +
                    "D.TILEGIAMGIA, " +
                    "D.THANHTIEN, " +
                    "D.NOTE " +
                    "FROM TDONHANGCHITIET D " +
                    "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                    "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                    "WHERE D.TDONHANGID = @BillId " +
                    "AND COALESCE(D.XUATVATTU, 0) = 0 " +
                    "AND COALESCE(M.DLOAIMATHANGID, '') <> @MatType " +
                    "AND COALESCE(D.COMBOPARENTID, '') = '' " +
                    "ORDER BY D.TIMECREATED, D.ID";

                DataTable dt = DbHelper.Table(conn, null, sql,
                    "@BillId", billId,
                    "@MatType", RevenueReportConstants.MaterialProductTypeId);

                var list = new List<RevenueBillLine>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new RevenueBillLine
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                        ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                        UnitName = DbHelper.Str(r["DVT"]) ?? "",
                        Quantity = ReadDecimal(r["SLXUATCHUAQUYDOI"]),
                        UnitPrice = ReadDecimal(r["DONGIA"]),
                        DiscountRate = ReadDecimal(r["TILEGIAMGIA"]),
                        Amount = ReadDecimal(r["THANHTIEN"]),
                        Note = DbHelper.Str(r["NOTE"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// THU KHÁC, CHI KHÁC từ TTHUCHI: dùng chung <see cref="BillReportScopeSql.AppendDateAndStoreWhere"/>
        /// (NGAY trong [FromDate, ToDate + 1 ngày) và lọc cửa hàng theo user/admin).
        /// KHÔNG lọc STATUS, LOAI, LATAMUNG, CHUYENKHOAN (spec §2.3, §3 P1/Q13a).
        /// </summary>
        public RevenueCashFlowTotals LoadOtherCashFlow(BillReportScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException("scope");

            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(4);
                var args = new List<object>(8);
                BillReportScopeSql.AppendDateAndStoreWhere("T", scope, conditions, args, "Tc");

                string sql =
                    "SELECT " +
                    "COALESCE(SUM(COALESCE(T.THU, 0)), 0) AS THUKHAC, " +
                    "COALESCE(SUM(COALESCE(T.CHI, 0)), 0) AS CHIKHAC " +
                    "FROM TTHUCHI T " +
                    "WHERE " + string.Join(" AND ", conditions.ToArray());

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                if (dt.Rows.Count == 0)
                    return new RevenueCashFlowTotals();

                DataRow r = dt.Rows[0];
                return new RevenueCashFlowTotals
                {
                    OtherIncome = DbHelper.Dec(r["THUKHAC"]),
                    OtherExpense = DbHelper.Dec(r["CHIKHAC"])
                };
            });
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

        private static decimal? ReadDecimal(object v)
        {
            return v == null || v is DBNull ? (decimal?)null : Convert.ToDecimal(v);
        }
    }
}
