using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Phần truy vấn chi tiết phát sinh công nợ (hóa đơn/phiếu nhập-xuất trả &amp; phiếu thu/chi theo ngày)
    /// cho <see cref="DebtRepository"/>.
    /// </summary>
    public sealed partial class DebtRepository
    {
        /// <summary>Chi tiết bill và phiếu thu/chi theo ngày của một khách hàng đang chọn.</summary>
        public DebtPartnerDetail LoadCustomerDetail(string customerId)
        {
            var detail = new DebtPartnerDetail();
            if (string.IsNullOrEmpty(customerId))
                return detail;

            return DbHelper.Read(_databasePath, conn =>
            {
                detail.Documents = LoadCustomerBillsIn(conn, null, customerId);
                detail.Vouchers = LoadCustomerVouchersIn(conn, null, customerId);
                return detail;
            });
        }

        /// <summary>Danh sách hóa đơn còn nợ (&gt; 0) của khách, sắp từ đơn cũ nhất để thu nợ theo đơn.</summary>
        public List<DebtDocumentRow> LoadOpenCustomerBills(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return new List<DebtDocumentRow>();
            return DbHelper.Read(_databasePath, conn =>
            {
                List<DebtDocumentRow> all = LoadCustomerBillsIn(conn, null, customerId);
                return all.FindAll(b => b.RemainingDebt > 0m);
            });
        }

        /// <summary>Chi tiết phiếu nhập/xuất trả và phiếu thu/chi theo ngày của một nhà cung cấp đang chọn.</summary>
        public DebtPartnerDetail LoadSupplierDetail(string supplierId)
        {
            var detail = new DebtPartnerDetail();
            if (string.IsNullOrEmpty(supplierId))
                return detail;

            return DbHelper.Read(_databasePath, conn =>
            {
                detail.Documents = LoadSupplierDocsIn(conn, null, supplierId);
                detail.Vouchers = LoadSupplierVouchersIn(conn, null, supplierId);
                return detail;
            });
        }

        public static List<DebtDocumentRow> LoadCustomerBillsIn(FbConnection conn, FbTransaction tx, string customerId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, " +
                "COALESCE((SELECT SUM(COALESCE(CT.SOTIEN, 0)) " +
                "FROM TCHITIETTHANHTOAN CT INNER JOIN TTHUCHI T ON T.ID = CT.TTHUCHIID " +
                "WHERE CT.TDONHANGID = H.ID AND COALESCE(T.STATUS, 0) <> @Del), 0) AS ALLOCPAID, " +
                "COALESCE(H.DIENGIAI, '') AS DIENGIAI, COALESCE(H.NOTE, '') AS NOTE, H.TIMECREATED " +
                "FROM TDONHANG H " +
                "WHERE H.DKHACHHANGID = @C " +
                "AND COALESCE(H.LOAI, 0) IN (0, 99) " +
                "AND COALESCE(H.DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "ORDER BY H.NGAY ASC, H.TIMECREATED ASC, H.NAME ASC",
                "@C", customerId, "@Del", DeletedStatus);

            var list = new List<DebtDocumentRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(CustomerBillFrom(r));
            return list;
        }

        private static List<DebtVoucherRow> LoadCustomerVouchersIn(FbConnection conn, FbTransaction tx, string customerId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.ID, T.NAME, T.NGAY, COALESCE(T.LOAI, 0) AS LOAI, " +
                "COALESCE(T.LAPHIEUTHUCONGNO, 0) AS LAPHIEUTHUCONGNO, " +
                "COALESCE(T.THU, 0) AS THU, COALESCE(T.CHI, 0) AS CHI, " +
                "COALESCE(T.DIENGIAI, '') AS DIENGIAI, COALESCE(T.NOTE, '') AS NOTE, T.TIMECREATED " +
                "FROM TTHUCHI T " +
                "WHERE T.DKHACHHANGID = @C " +
                "AND COALESCE(T.KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (T.TDATHANGID IS NULL OR T.TDATHANGID = '') " +
                "AND COALESCE(T.STATUS, 0) <> @Del " +
                "ORDER BY T.NGAY ASC, T.TIMECREATED ASC, T.NAME ASC",
                "@C", customerId, "@Del", DeletedStatus);

            return ReadVouchers(dt);
        }

        private static List<DebtDocumentRow> LoadSupplierDocsIn(FbConnection conn, FbTransaction tx, string supplierId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, " +
                "COALESCE(H.DIENGIAI, '') AS DIENGIAI, COALESCE(H.NOTE, '') AS NOTE, H.TIMECREATED " +
                "FROM TDONHANG H " +
                "WHERE H.DNHACUNGCAPID = @S " +
                "AND COALESCE(H.LOAI, 0) IN (1, 2, 98) " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "ORDER BY H.NGAY ASC, H.TIMECREATED ASC, H.NAME ASC",
                "@S", supplierId);

            var list = new List<DebtDocumentRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                int loai = DbHelper.Int(r["LOAI"]);
                decimal sign = loai == 2 ? -1m : 1m;
                decimal rawTotal = DbHelper.Dec(r["TONGCONG"]);
                decimal rawPaid = DbHelper.Dec(r["TIENTHANHTOAN"]);
                list.Add(new DebtDocumentRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Number = DbHelper.Str(r["NAME"]) ?? "",
                    Date = ReadDate(r["NGAY"]),
                    Loai = loai,
                    DocTypeLabel = loai == 1 ? "Nhập kho" : loai == 2 ? "Xuất trả" : "Nợ đầu kỳ",
                    TotalAmount = sign * rawTotal,
                    PaidOnBill = rawPaid,
                    AllocatedPaid = 0m,
                    PaidTotal = sign * rawPaid,
                    RemainingDebt = sign * (rawTotal - rawPaid),
                    Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    TimeCreated = ReadNullableDate(r["TIMECREATED"])
                });
            }
            return list;
        }

        private static List<DebtVoucherRow> LoadSupplierVouchersIn(FbConnection conn, FbTransaction tx, string supplierId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.ID, T.NAME, T.NGAY, COALESCE(T.LOAI, 0) AS LOAI, " +
                "COALESCE(T.LAPHIEUTHUCONGNO, 0) AS LAPHIEUTHUCONGNO, " +
                "COALESCE(T.THU, 0) AS THU, COALESCE(T.CHI, 0) AS CHI, " +
                "COALESCE(T.DIENGIAI, '') AS DIENGIAI, COALESCE(T.NOTE, '') AS NOTE, T.TIMECREATED " +
                "FROM TTHUCHI T " +
                "WHERE T.DNHACUNGCAPID = @S " +
                "AND COALESCE(T.KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (T.TDATHANGID IS NULL OR T.TDATHANGID = '') " +
                "AND COALESCE(T.STATUS, 0) <> @Del " +
                "ORDER BY T.NGAY ASC, T.TIMECREATED ASC, T.NAME ASC",
                "@S", supplierId, "@Del", DeletedStatus);

            return ReadVouchers(dt);
        }

        private static DebtDocumentRow CustomerBillFrom(DataRow r)
        {
            int loai = DbHelper.Int(r["LOAI"]);
            decimal total = DbHelper.Dec(r["TONGCONG"]);
            decimal paidOnBill = DbHelper.Dec(r["TIENTHANHTOAN"]);
            decimal allocPaid = DbHelper.Dec(r["ALLOCPAID"]);
            return new DebtDocumentRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Number = DbHelper.Str(r["NAME"]) ?? "",
                Date = ReadDate(r["NGAY"]),
                Loai = loai,
                DocTypeLabel = loai == 99 ? "Nợ đầu kỳ" : "Hóa đơn bán",
                TotalAmount = total,
                PaidOnBill = paidOnBill,
                AllocatedPaid = allocPaid,
                PaidTotal = paidOnBill + allocPaid,
                RemainingDebt = total - paidOnBill - allocPaid,
                Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                TimeCreated = ReadNullableDate(r["TIMECREATED"])
            };
        }

        private static List<DebtVoucherRow> ReadVouchers(DataTable dt)
        {
            var list = new List<DebtVoucherRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                int loai = DbHelper.Int(r["LOAI"]);
                list.Add(new DebtVoucherRow
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Number = DbHelper.Str(r["NAME"]) ?? "",
                    Date = ReadDate(r["NGAY"]),
                    Loai = loai,
                    IsDebtReceipt = DbHelper.Int(r["LAPHIEUTHUCONGNO"]) != 0 || loai == 2,
                    Income = DbHelper.Dec(r["THU"]),
                    Expense = DbHelper.Dec(r["CHI"]),
                    Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    TimeCreated = ReadNullableDate(r["TIMECREATED"])
                });
            }
            return list;
        }

        private static DateTime ReadDate(object v)
        {
            return v == null || v is DBNull ? DateTime.Today : Convert.ToDateTime(v);
        }

        private static DateTime? ReadNullableDate(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
