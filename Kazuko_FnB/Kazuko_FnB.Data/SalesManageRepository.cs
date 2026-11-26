using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc dữ liệu cho "Quản lý bán hàng" và "Điều chỉnh hóa đơn"
    /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §1, §2.2, §3.1, §6.3, §6.4).
    /// Phần ghi trong một transaction nằm ở SalesManageRepository.Write.cs.
    /// Mọi câu SQL tham số hóa; chỉ lọc LOAI = 0, DATHANHTOAN = 30, STATUS &lt;&gt; -30.
    /// </summary>
    public sealed partial class SalesManageRepository
    {
        private const int ActiveStatus = 30;
        private const int DeletedStatus = -30;
        private readonly string _databasePath;

        public SalesManageRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public T Read<T>(Func<FbConnection, T> work)
        {
            return DbHelper.Read(_databasePath, work);
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Ngày hiện tại của máy chủ DB (dùng làm ngày giao dịch cho chế độ 0, 1 theo §6.4).</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null).Date);
        }

        /// <summary>
        /// Danh sách bill đã thanh toán (LOAI = 0, DATHANHTOAN = 30, STATUS &lt;&gt; -30)
        /// trong khoảng [fromDate, toDate] (trọn ngày toDate), sắp theo NGAY, GIOTHANHTOAN, NAME (§1, §3.1).
        /// </summary>
        public List<PaidBillListItem> LoadPaidBills(DateTime fromDate, DateTime toDate)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime start = fromDate.Date;
                DateTime endExcl = toDate.Date.AddDays(1);
                if (endExcl <= start)
                    endExcl = start.AddDays(1);

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT H.ID, H.NAME, H.NGAY, " +
                    "H.DKHACHHANGID, COALESCE(K.MAKHACH, '') AS CUSTCODE, COALESCE(K.NAME, '') AS CUSTNAME, " +
                    "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                    "COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                    "COALESCE(H.TIENTHUE, 0) AS TIENTHUE, COALESCE(H.TILETHUE, 0) AS TILETHUE, " +
                    "COALESCE(H.TIENHANG, 0) AS TIENHANG, H.GIOTHANHTOAN, " +
                    "COALESCE(H.KHACHDUA, 0) AS KHACHDUA, COALESCE(H.TRALAI, 0) AS TRALAI, " +
                    "COALESCE(H.VOUCHER, 0) AS VOUCHER, COALESCE(H.THETRATRUOC, 0) AS THETRATRUOC, " +
                    "COALESCE(H.TRUTICHLUY, 0) AS TRUTICHLUY, COALESCE(H.TIENMAT, 0) AS TIENMAT, " +
                    "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, COALESCE(H.THE, 0) AS THE, " +
                    "H.DBANID, COALESCE(B.NAME, '') AS TABLENAME, " +
                    "H.BATDAU, H.KETTHUC, COALESCE(H.TIENGIO, 0) AS TIENGIO, " +
                    "COALESCE(H.TILEGIAMGIAGIO, 0) AS TILEGIAMGIAGIO, COALESCE(H.TIENGIAMGIAGIO, 0) AS TIENGIAMGIAGIO, " +
                    "COALESCE(H.SOKHACH, 1) AS SOKHACH, " +
                    "COALESCE(H.PHIDICHVU, 0) AS PHIDICHVU, COALESCE(H.TILEPHIDICHVU, 0) AS TILEPHIDICHVU, " +
                    "COALESCE(H.SOORDER, '') AS SOORDER, COALESCE(H.NHOMGUID, '') AS NHOMGUID, " +
                    "COALESCE(H.NOTE, '') AS NOTE, COALESCE(H.TDATHANGID, '') AS TDATHANGID, " +
                    "COALESCE(H.USERTHANHTOANID, '') AS USERTHANHTOANID, COALESCE(U.NAME, '') AS CASHIERNAME " +
                    "FROM TDONHANG H " +
                    "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                    "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                    "LEFT JOIN SUSER U ON U.ID = H.USERTHANHTOANID " +
                    "WHERE COALESCE(H.LOAI, 0) = 0 AND COALESCE(H.DATHANHTOAN, 0) = 30 " +
                    "AND COALESCE(H.STATUS, 0) <> @Del " +
                    "AND H.NGAY >= @From AND H.NGAY < @ToExcl " +
                    "ORDER BY H.NGAY, H.GIOTHANHTOAN, H.NAME",
                    "@Del", DeletedStatus,
                    "@From", start,
                    "@ToExcl", endExcl);

                var list = new List<PaidBillListItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new PaidBillListItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Ngay = ReadDateTime(r["NGAY"]) ?? start,
                        CustomerId = DbHelper.Str(r["DKHACHHANGID"]) ?? "",
                        CustomerCode = DbHelper.Str(r["CUSTCODE"]) ?? "",
                        CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                        TongCong = DbHelper.Dec(r["TONGCONG"]),
                        TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                        TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                        TienThue = DbHelper.Dec(r["TIENTHUE"]),
                        TileThue = DbHelper.Dec(r["TILETHUE"]),
                        TienHang = DbHelper.Dec(r["TIENHANG"]),
                        GioThanhToan = ReadDateTime(r["GIOTHANHTOAN"]),
                        KhachDua = DbHelper.Dec(r["KHACHDUA"]),
                        TraLai = DbHelper.Dec(r["TRALAI"]),
                        Voucher = DbHelper.Dec(r["VOUCHER"]),
                        TheTraTruoc = DbHelper.Dec(r["THETRATRUOC"]),
                        TruTichLuy = DbHelper.Dec(r["TRUTICHLUY"]),
                        TienMat = DbHelper.Dec(r["TIENMAT"]),
                        ChuyenKhoan = DbHelper.Dec(r["CHUYENKHOAN"]),
                        The = DbHelper.Dec(r["THE"]),
                        TableId = DbHelper.Str(r["DBANID"]) ?? "",
                        TableName = DbHelper.Str(r["TABLENAME"]) ?? "",
                        BatDau = ReadDateTime(r["BATDAU"]),
                        KetThuc = ReadDateTime(r["KETTHUC"]),
                        TienGio = DbHelper.Dec(r["TIENGIO"]),
                        TileGiamGiaGio = DbHelper.Dec(r["TILEGIAMGIAGIO"]),
                        TienGiamGiaGio = DbHelper.Dec(r["TIENGIAMGIAGIO"]),
                        GuestCount = Math.Max(1, DbHelper.Int(r["SOKHACH"])),
                        PhiDichVu = DbHelper.Dec(r["PHIDICHVU"]),
                        TilePhiDichVu = DbHelper.Dec(r["TILEPHIDICHVU"]),
                        OrderNumbers = DbHelper.Str(r["SOORDER"]) ?? "",
                        GroupGuid = (DbHelper.Str(r["NHOMGUID"]) ?? "").Trim(),
                        Note = DbHelper.Str(r["NOTE"]) ?? "",
                        BookingId = DbHelper.Str(r["TDATHANGID"]) ?? "",
                        UserThanhToanId = DbHelper.Str(r["USERTHANHTOANID"]) ?? "",
                        UserThanhToanName = DbHelper.Str(r["CASHIERNAME"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc đầy đủ chi tiết bill đã thanh toán kèm các cột thanh toán đã lưu (§2.2).</summary>
        public PaidBillDetail LoadPaidBillDetail(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadPaidBillDetailIn(conn, null, billId));
        }

        public static PaidBillDetail LoadPaidBillDetailIn(FbConnection conn, FbTransaction tx, string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return null;

            ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
            if (header == null)
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT COALESCE(TIENMAT, 0) AS TIENMAT, COALESCE(KHACHDUA, 0) AS KHACHDUA, " +
                "COALESCE(TRALAI, 0) AS TRALAI, COALESCE(CHUYENKHOAN, 0) AS CHUYENKHOAN, " +
                "COALESCE(THE, 0) AS THE, DTAIKHOANNGANHANGID, " +
                "COALESCE(VOUCHER, 0) AS VOUCHER, DVOUCHERID, " +
                "COALESCE(THETRATRUOC, 0) AS THETRATRUOC, DTHETRATRUOCID, " +
                "COALESCE(TRUTICHLUY, 0) AS TRUTICHLUY, COALESCE(DIEMGIAM, 0) AS DIEMGIAM, " +
                "COALESCE(DATTRUOC, 0) AS DATTRUOC, COALESCE(NOCU, 0) AS NOCU, " +
                "COALESCE(TIENTHANHTOAN, 0) AS TIENTHANHTOAN, COALESCE(CONGNO, 0) AS CONGNO, " +
                "DNHANVIENXUATID, COALESCE(SOORDER, '') AS SOORDER, " +
                "USERTHANHTOANID, GIOTHANHTOAN, GIOTRADO " +
                "FROM TDONHANG WHERE ID = @I AND COALESCE(STATUS, 0) <> @Del",
                "@I", billId, "@Del", DeletedStatus);

            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            return new PaidBillDetail
            {
                Header = header,
                GioTraDo = ReadDateTime(r["GIOTRADO"]),
                Payment = new ExistingPaymentSnapshot
                {
                    Cash = DbHelper.Dec(r["TIENMAT"]),
                    KhachDua = DbHelper.Dec(r["KHACHDUA"]),
                    TraLai = DbHelper.Dec(r["TRALAI"]),
                    Transfer = DbHelper.Dec(r["CHUYENKHOAN"]),
                    Card = DbHelper.Dec(r["THE"]),
                    AccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"]) ?? "",
                    Voucher = DbHelper.Dec(r["VOUCHER"]),
                    VoucherId = DbHelper.Str(r["DVOUCHERID"]) ?? "",
                    PrepaidCard = DbHelper.Dec(r["THETRATRUOC"]),
                    PrepaidCardId = DbHelper.Str(r["DTHETRATRUOCID"]) ?? "",
                    PointsMoney = DbHelper.Dec(r["TRUTICHLUY"]),
                    PointsUsed = DbHelper.Dec(r["DIEMGIAM"]),
                    DatTruoc = DbHelper.Dec(r["DATTRUOC"]),
                    NoCu = DbHelper.Dec(r["NOCU"]),
                    TienThanhToan = DbHelper.Dec(r["TIENTHANHTOAN"]),
                    CongNo = DbHelper.Dec(r["CONGNO"]),
                    EmployeeId = DbHelper.Str(r["DNHANVIENXUATID"]) ?? "",
                    OrderNumbers = DbHelper.Str(r["SOORDER"]) ?? "",
                    UserThanhToanId = DbHelper.Str(r["USERTHANHTOANID"]) ?? "",
                    GioThanhToan = ReadDateTime(r["GIOTHANHTOAN"])
                }
            };
        }

        /// <summary>
        /// Tổng số tiền đã chi trả lại tiền đặt trước cho hóa đơn này (TTHUCHI LOAI = 1, STATUS &lt;&gt; -30, §6.4).
        /// Chỉ tính các phiếu chi có gắn TDONHANGID (hoặc TDATHANGID); phiếu do app gốc tạo không gắn mã bill nên có thể thiếu.
        /// </summary>
        public static decimal GetRefundedDepositIn(FbConnection conn, FbTransaction tx, string billId, string bookingId)
        {
            bool hasBill = !string.IsNullOrEmpty(billId);
            bool hasBooking = !string.IsNullOrEmpty(bookingId);
            if (!hasBill && !hasBooking)
                return 0m;

            if (hasBill && hasBooking)
            {
                return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(CHI, 0)), 0) FROM TTHUCHI " +
                    "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 1 " +
                    "AND (TDONHANGID = @B OR TDATHANGID = @D)",
                    "@Del", DeletedStatus, "@B", billId, "@D", bookingId));
            }
            if (hasBill)
            {
                return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(CHI, 0)), 0) FROM TTHUCHI " +
                    "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 1 AND TDONHANGID = @B",
                    "@Del", DeletedStatus, "@B", billId));
            }
            return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(CHI, 0)), 0) FROM TTHUCHI " +
                "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 1 AND TDATHANGID = @D",
                "@Del", DeletedStatus, "@D", bookingId));
        }

        /// <summary>
        /// Đếm và cộng tổng THU/CHI các phiếu thu/chi còn hiệu lực (STATUS &lt;&gt; -30) liên quan đến hóa đơn (§6.3).
        /// </summary>
        public static BillRelatedVouchersSummary GetRelatedVouchersSummaryIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string bookingId)
        {
            var summary = new BillRelatedVouchersSummary();
            bool hasBill = !string.IsNullOrEmpty(billId);
            bool hasBooking = !string.IsNullOrEmpty(bookingId);
            if (!hasBill && !hasBooking)
                return summary;

            DataTable dt;
            if (hasBill && hasBooking)
            {
                dt = DbHelper.Table(conn, tx,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(COALESCE(THU, 0)), 0) AS SUMTHU, " +
                    "COALESCE(SUM(COALESCE(CHI, 0)), 0) AS SUMCHI " +
                    "FROM TTHUCHI WHERE COALESCE(STATUS, 0) <> @Del " +
                    "AND (TDONHANGID = @B OR TDATHANGID = @D)",
                    "@Del", DeletedStatus, "@B", billId, "@D", bookingId);
            }
            else if (hasBill)
            {
                dt = DbHelper.Table(conn, tx,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(COALESCE(THU, 0)), 0) AS SUMTHU, " +
                    "COALESCE(SUM(COALESCE(CHI, 0)), 0) AS SUMCHI " +
                    "FROM TTHUCHI WHERE COALESCE(STATUS, 0) <> @Del AND TDONHANGID = @B",
                    "@Del", DeletedStatus, "@B", billId);
            }
            else
            {
                dt = DbHelper.Table(conn, tx,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(COALESCE(THU, 0)), 0) AS SUMTHU, " +
                    "COALESCE(SUM(COALESCE(CHI, 0)), 0) AS SUMCHI " +
                    "FROM TTHUCHI WHERE COALESCE(STATUS, 0) <> @Del AND TDATHANGID = @D",
                    "@Del", DeletedStatus, "@D", bookingId);
            }

            if (dt.Rows.Count > 0)
            {
                summary.Count = DbHelper.Int(dt.Rows[0]["CNT"]);
                summary.TotalIncome = DbHelper.Dec(dt.Rows[0]["SUMTHU"]);
                summary.TotalExpense = DbHelper.Dec(dt.Rows[0]["SUMCHI"]);
            }
            return summary;
        }

        /// <summary>Đọc nhóm khách hàng của một khách hàng để áp lại giảm giá theo nhóm (Q4, §6.3).</summary>
        public CustomerGroupRow LoadCustomerGroupByCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadCustomerGroupByCustomerIn(conn, null, customerId));
        }

        public static CustomerGroupRow LoadCustomerGroupByCustomerIn(FbConnection conn, FbTransaction tx, string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT G.ID, G.NAME, COALESCE(G.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                "COALESCE(G.TILEGIAMGIATIENHANG, 0) AS TILEGIAMGIATIENHANG, " +
                "COALESCE(G.TILEGIAMGIATIENGIO, 0) AS TILEGIAMGIATIENGIO, " +
                "COALESCE(G.TILEGIAMDOAN, 0) AS TILEGIAMDOAN, " +
                "COALESCE(G.TILEGIAMDOUONG, 0) AS TILEGIAMDOUONG, " +
                "COALESCE(G.TILEGIAMDICHVU, 0) AS TILEGIAMDICHVU, " +
                "COALESCE(G.TILEGIAMDOKHAC, 0) AS TILEGIAMDOKHAC " +
                "FROM DKHACHHANG K INNER JOIN DNHOMKHACHHANG G ON G.ID = K.DNHOMKHACHHANGID " +
                "WHERE K.ID = @C AND K.STATUS = @S AND G.STATUS = @S",
                "@C", customerId, "@S", ActiveStatus);

            if (dt.Rows.Count == 0)
                return null;
            DataRow r = dt.Rows[0];
            return new CustomerGroupRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                DiscountTotal = DbHelper.Dec(r["TILEGIAMGIA"]),
                DiscountGoods = DbHelper.Dec(r["TILEGIAMGIATIENHANG"]),
                DiscountHour = DbHelper.Dec(r["TILEGIAMGIATIENGIO"]),
                DiscountFood = DbHelper.Dec(r["TILEGIAMDOAN"]),
                DiscountDrink = DbHelper.Dec(r["TILEGIAMDOUONG"]),
                DiscountService = DbHelper.Dec(r["TILEGIAMDICHVU"]),
                DiscountOther = DbHelper.Dec(r["TILEGIAMDOKHAC"])
            };
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            return Convert.ToDateTime(v);
        }
    }
}
