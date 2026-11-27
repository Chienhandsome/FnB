using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc dữ liệu màn Khách hàng thân thiết (docs/specs/khach-hang-than-thiet.md §2, §6):
    /// - Danh sách khách hàng (DKHACHHANG STATUS = 30, xếp theo NAME) kèm Doanh số, Số hóa đơn, Điểm tích lũy
    ///   tính theo công thức riêng của màn này (tách biệt với ServicePaymentRepository.GetCustomerPointsIn).
    /// - Cây nhóm khách hàng (DNHOMKHACHHANG).
    /// - Tab Hóa đơn (TDONHANG LOAI = 0, DATHANHTOAN = 30) và tab Tặng điểm - tặng quà (TTANGGIAMDIEM).
    /// Phần ghi phiếu TTANGGIAMDIEM nằm ở <c>LoyaltyRepository.Write.cs</c>.
    /// </summary>
    public sealed partial class LoyaltyRepository
    {
        public const int ActiveStatus = LoyaltyConstants.ActiveStatus;
        public const int DeletedStatus = LoyaltyConstants.DeletedStatus;

        private readonly string _databasePath;

        public LoyaltyRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Công thức điểm riêng của màn Khách hàng thân thiết (spec §2).</summary>
        public static decimal ComputeScreenPoints(
            decimal initialPoints,
            decimal billRevenue,
            decimal billEarnedPoints,
            decimal billUsedPoints,
            decimal manualNetPoints,
            int cachTinhDiem,
            int doanhSo1Diem)
        {
            decimal fromBills;
            if (cachTinhDiem == 1)
            {
                fromBills = billEarnedPoints - billUsedPoints;
            }
            else
            {
                decimal revPoints = doanhSo1Diem > 0
                    ? Math.Round(billRevenue / (decimal)doanhSo1Diem, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                fromBills = revPoints - billUsedPoints;
            }
            return initialPoints + fromBills + manualNetPoints;
        }

        /// <summary>Đọc toàn bộ khách hàng đang dùng (STATUS = 30) kèm Doanh số, Số hóa đơn, Điểm tích lũy.</summary>
        public List<LoyaltyCustomerRow> LoadCustomers(int cachTinhDiem, int doanhSo1Diem)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                Dictionary<string, BillAggregate> billsByCust = LoadBillAggregatesIn(conn, null);
                Dictionary<string, decimal> manualByCust = LoadManualPointMapIn(conn, null);

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT K.ID, K.MAKHACH, K.NAME, K.DNHOMKHACHHANGID, COALESCE(G.NAME, '') AS GROUPNAME, " +
                    "K.DIACHI, K.DIENTHOAI, K.EMAIL, COALESCE(K.DIEMTICHLUYBANDAU, 0) AS DIEMTICHLUYBANDAU, K.NOTE " +
                    "FROM DKHACHHANG K " +
                    "LEFT JOIN DNHOMKHACHHANG G ON G.ID = K.DNHOMKHACHHANGID " +
                    "WHERE K.STATUS = @S " +
                    "ORDER BY K.NAME",
                    "@S", ActiveStatus);

                var list = new List<LoyaltyCustomerRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    decimal initial = DbHelper.Dec(r["DIEMTICHLUYBANDAU"]);
                    BillAggregate bill;
                    billsByCust.TryGetValue(id, out bill);
                    decimal manual;
                    manualByCust.TryGetValue(id, out manual);

                    decimal revenue = bill != null ? bill.Revenue : 0m;
                    int billCount = bill != null ? bill.Count : 0;
                    decimal earned = bill != null ? bill.Earned : 0m;
                    decimal used = bill != null ? bill.Used : 0m;

                    list.Add(new LoyaltyCustomerRow
                    {
                        Id = id,
                        Code = DbHelper.Str(r["MAKHACH"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMKHACHHANGID"]) ?? "",
                        GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        Email = DbHelper.Str(r["EMAIL"]) ?? "",
                        InitialPoints = initial,
                        Revenue = revenue,
                        BillCount = billCount,
                        Points = ComputeScreenPoints(initial, revenue, earned, used, manual, cachTinhDiem, doanhSo1Diem),
                        Note = DbHelper.Str(r["NOTE"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc điểm hiện có của một khách hàng theo đúng công thức màn Khách hàng thân thiết (spec §2, §3).</summary>
        public decimal GetCustomerPointsForScreen(
            string customerId,
            int cachTinhDiem,
            int doanhSo1Diem,
            string excludeVoucherId = null)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;
            return DbHelper.Read(_databasePath, conn =>
                GetCustomerPointsForScreenIn(conn, null, customerId, cachTinhDiem, doanhSo1Diem, excludeVoucherId));
        }

        /// <summary>
        /// Tính điểm hiện có của một khách trong transaction theo công thức riêng màn này,
        /// tùy chọn bỏ qua một phiếu TTANGGIAMDIEM (khi kiểm tra sửa hoặc xóa phiếu đó).
        /// </summary>
        public static decimal GetCustomerPointsForScreenIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            int cachTinhDiem,
            int doanhSo1Diem,
            string excludeVoucherId)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;

            decimal initial = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(DIEMTICHLUYBANDAU, 0) FROM DKHACHHANG WHERE ID = @C",
                "@C", customerId));

            DataTable dtBill = DbHelper.Table(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(TONGCONG, 0)), 0) AS REV, " +
                "COALESCE(SUM(COALESCE(DIEM, 0)), 0) AS EARNED, " +
                "COALESCE(SUM(COALESCE(DIEMGIAM, 0)), 0) AS USED " +
                "FROM TDONHANG " +
                "WHERE DKHACHHANGID = @C AND COALESCE(DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(LOAI, 0) = 0 AND COALESCE(STATUS, 0) <> @Del",
                "@C", customerId, "@Del", DeletedStatus);

            decimal rev = dtBill.Rows.Count > 0 ? DbHelper.Dec(dtBill.Rows[0]["REV"]) : 0m;
            decimal earned = dtBill.Rows.Count > 0 ? DbHelper.Dec(dtBill.Rows[0]["EARNED"]) : 0m;
            decimal used = dtBill.Rows.Count > 0 ? DbHelper.Dec(dtBill.Rows[0]["USED"]) : 0m;

            decimal manual;
            if (string.IsNullOrEmpty(excludeVoucherId))
            {
                manual = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(DIEMTANG, 0) - COALESCE(DIEMGIAM, 0)), 0) " +
                    "FROM TTANGGIAMDIEM WHERE DKHACHHANGID = @C AND COALESCE(STATUS, 0) <> @Del",
                    "@C", customerId, "@Del", DeletedStatus));
            }
            else
            {
                manual = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(DIEMTANG, 0) - COALESCE(DIEMGIAM, 0)), 0) " +
                    "FROM TTANGGIAMDIEM WHERE DKHACHHANGID = @C AND COALESCE(STATUS, 0) <> @Del AND ID <> @Ex",
                    "@C", customerId, "@Del", DeletedStatus, "@Ex", excludeVoucherId));
            }

            return ComputeScreenPoints(initial, rev, earned, used, manual, cachTinhDiem, doanhSo1Diem);
        }

        /// <summary>Đọc cây nhóm khách hàng đang dùng (STATUS = 30).</summary>
        public List<LoyaltyGroupNode> LoadGroups()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, PARENTID, SORTORDER, COALESCE(DIEMTICHLUY, 0) AS DIEMTICHLUY " +
                    "FROM DNHOMKHACHHANG WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);

                var list = new List<LoyaltyGroupNode>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new LoyaltyGroupNode
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? "",
                        ThresholdPoints = DbHelper.Dec(r["DIEMTICHLUY"])
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc chi tiết 2 tab (Hóa đơn &amp; Tặng điểm - tặng quà) của khách đang chọn.</summary>
        public LoyaltyCustomerDetail LoadCustomerDetail(string customerId)
        {
            var detail = new LoyaltyCustomerDetail();
            if (string.IsNullOrEmpty(customerId))
                return detail;

            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dtBills = DbHelper.Table(conn, null,
                    "SELECT ID, COALESCE(DIEM, 0) AS DIEM, NAME, NGAY, " +
                    "COALESCE(TONGCONG, 0) AS TONGCONG, COALESCE(DIEMGIAM, 0) AS DIEMGIAM " +
                    "FROM TDONHANG " +
                    "WHERE DKHACHHANGID = @C AND COALESCE(DATHANHTOAN, 0) = 30 " +
                    "AND COALESCE(LOAI, 0) = 0 AND COALESCE(STATUS, 0) <> @Del " +
                    "ORDER BY NGAY DESC, TIMECREATED DESC, NAME DESC",
                    "@C", customerId, "@Del", DeletedStatus);

                foreach (DataRow r in dtBills.Rows)
                {
                    detail.Bills.Add(new LoyaltyBillRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Points = DbHelper.Dec(r["DIEM"]),
                        Number = DbHelper.Str(r["NAME"]) ?? "",
                        Date = ReadDate(r["NGAY"]),
                        TotalAmount = DbHelper.Dec(r["TONGCONG"]),
                        PointsUsed = DbHelper.Dec(r["DIEMGIAM"])
                    });
                }

                DataTable dtVouchers = DbHelper.Table(conn, null,
                    "SELECT T.ID, T.NAME, T.NGAY, T.DKHACHHANGID, COALESCE(K.NAME, '') AS CUSTNAME, " +
                    "COALESCE(T.DIEMTANG, 0) AS DIEMTANG, COALESCE(T.DIEMGIAM, 0) AS DIEMGIAM, " +
                    "COALESCE(T.LYDO, '') AS LYDO, COALESCE(T.NOTE, '') AS NOTE, " +
                    "COALESCE(T.STATUS, 0) AS STATUS, T.USERCREATEDID, T.TIMECREATED, " +
                    "T.USERMODIFIEDID, T.TIMEMODIFIED " +
                    "FROM TTANGGIAMDIEM T " +
                    "LEFT JOIN DKHACHHANG K ON K.ID = T.DKHACHHANGID " +
                    "WHERE T.DKHACHHANGID = @C AND COALESCE(T.STATUS, 0) <> @Del " +
                    "ORDER BY T.NGAY DESC, T.TIMECREATED DESC, T.NAME DESC",
                    "@C", customerId, "@Del", DeletedStatus);

                foreach (DataRow r in dtVouchers.Rows)
                    detail.Vouchers.Add(VoucherFrom(r));

                return detail;
            });
        }

        public LoyaltyPointVoucherRow LoadVoucherById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadVoucherByIdIn(conn, null, id));
        }

        public static LoyaltyPointVoucherRow LoadVoucherByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.ID, T.NAME, T.NGAY, T.DKHACHHANGID, COALESCE(K.NAME, '') AS CUSTNAME, " +
                "COALESCE(T.DIEMTANG, 0) AS DIEMTANG, COALESCE(T.DIEMGIAM, 0) AS DIEMGIAM, " +
                "COALESCE(T.LYDO, '') AS LYDO, COALESCE(T.NOTE, '') AS NOTE, " +
                "COALESCE(T.STATUS, 0) AS STATUS, T.USERCREATEDID, T.TIMECREATED, " +
                "T.USERMODIFIEDID, T.TIMEMODIFIED " +
                "FROM TTANGGIAMDIEM T " +
                "LEFT JOIN DKHACHHANG K ON K.ID = T.DKHACHHANGID " +
                "WHERE T.ID = @I AND COALESCE(T.STATUS, 0) <> @Del",
                "@I", id, "@Del", DeletedStatus);
            return dt.Rows.Count == 0 ? null : VoucherFrom(dt.Rows[0]);
        }

        // ------------------------------------------------------------------ Nội bộ truy vấn gộp

        private sealed class BillAggregate
        {
            public decimal Revenue;
            public int Count;
            public decimal Earned;
            public decimal Used;
        }

        private static Dictionary<string, BillAggregate> LoadBillAggregatesIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, BillAggregate>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.DKHACHHANGID, " +
                "COALESCE(SUM(COALESCE(H.TONGCONG, 0)), 0) AS REV, " +
                "COUNT(*) AS CNT, " +
                "COALESCE(SUM(COALESCE(H.DIEM, 0)), 0) AS EARNED, " +
                "COALESCE(SUM(COALESCE(H.DIEMGIAM, 0)), 0) AS USED " +
                "FROM TDONHANG H " +
                "WHERE H.DKHACHHANGID IS NOT NULL AND H.DKHACHHANGID <> '' " +
                "AND COALESCE(H.DATHANHTOAN, 0) = 30 AND COALESCE(H.LOAI, 0) = 0 " +
                "AND COALESCE(H.STATUS, 0) <> @Del " +
                "GROUP BY H.DKHACHHANGID",
                "@Del", DeletedStatus);

            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DKHACHHANGID"]);
                if (string.IsNullOrEmpty(id))
                    continue;
                map[id] = new BillAggregate
                {
                    Revenue = DbHelper.Dec(r["REV"]),
                    Count = DbHelper.Int(r["CNT"]),
                    Earned = DbHelper.Dec(r["EARNED"]),
                    Used = DbHelper.Dec(r["USED"])
                };
            }
            return map;
        }

        private static Dictionary<string, decimal> LoadManualPointMapIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.DKHACHHANGID, " +
                "COALESCE(SUM(COALESCE(T.DIEMTANG, 0) - COALESCE(T.DIEMGIAM, 0)), 0) AS NETPTS " +
                "FROM TTANGGIAMDIEM T " +
                "WHERE T.DKHACHHANGID IS NOT NULL AND T.DKHACHHANGID <> '' " +
                "AND COALESCE(T.STATUS, 0) <> @Del " +
                "GROUP BY T.DKHACHHANGID",
                "@Del", DeletedStatus);

            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DKHACHHANGID"]);
                if (!string.IsNullOrEmpty(id))
                    map[id] = DbHelper.Dec(r["NETPTS"]);
            }
            return map;
        }

        private static LoyaltyPointVoucherRow VoucherFrom(DataRow r)
        {
            return new LoyaltyPointVoucherRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Number = DbHelper.Str(r["NAME"]) ?? "",
                Date = ReadDate(r["NGAY"]),
                CustomerId = DbHelper.Str(r["DKHACHHANGID"]) ?? "",
                CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                GiftPoints = DbHelper.Dec(r["DIEMTANG"]),
                ReducePoints = DbHelper.Dec(r["DIEMGIAM"]),
                Reason = DbHelper.Str(r["LYDO"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = ReadNullableDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = ReadNullableDateTime(r["TIMEMODIFIED"])
            };
        }

        private static DateTime ReadDate(object v)
        {
            return v == null || v is DBNull ? DateTime.Today : Convert.ToDateTime(v).Date;
        }

        private static DateTime? ReadNullableDateTime(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
