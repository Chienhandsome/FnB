using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn tổng hợp công nợ khách hàng &amp; nhà cung cấp (docs/specs/cong-no.md §2–§4).
    /// - KH: Σ(TONGCONG − TIENTHANHTOAN) trên TDONHANG (LOAI IN (0, 99), DATHANHTOAN = 30, STATUS = 30)
    ///   + Σ(CHI − THU) trên TTHUCHI (KHONGTHAYDOICONGNO = 0, TDATHANGID rỗng, STATUS &lt;&gt; -30).
    ///   Khớp hoàn toàn với <see cref="ServicePaymentRepository.GetOldDebtIn"/>.
    /// - NCC: Σ(sign × (TONGCONG − TIENTHANHTOAN)) trên TDONHANG (LOAI IN (1, 2, 98), STATUS = 30, sign = -1 khi LOAI = 2)
    ///   − Σ(CHI − THU) trên TTHUCHI (KHONGTHAYDOICONGNO = 0, TDATHANGID rỗng, STATUS &lt;&gt; -30).
    /// Phần chi tiết đặt ở <c>DebtRepository.Detail.cs</c>; phần ghi đặt ở <c>DebtRepository.Write.cs</c>.
    /// </summary>
    public sealed partial class DebtRepository
    {
        public const int ActiveStatus = DebtConstants.ActiveStatus;
        public const int DeletedStatus = DebtConstants.DeletedStatus;

        private readonly string _databasePath;

        public DebtRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Danh sách khách hàng đang dùng (STATUS = 30) kèm Còn nợ tính gộp (bỏ khách vãng lai không có hồ sơ).</summary>
        public List<DebtPartnerRow> LoadCustomerDebts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                Dictionary<string, decimal> billDebts = LoadCustomerBillDebtMapIn(conn, null);
                Dictionary<string, decimal> voucherAdj = LoadCustomerVoucherAdjMapIn(conn, null);

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT K.ID, K.MAKHACH, K.NAME, K.DNHOMKHACHHANGID, COALESCE(G.NAME, '') AS GROUPNAME, " +
                    "K.DIACHI, K.DIENTHOAI " +
                    "FROM DKHACHHANG K " +
                    "LEFT JOIN DNHOMKHACHHANG G ON G.ID = K.DNHOMKHACHHANGID " +
                    "WHERE K.STATUS = @S " +
                    "ORDER BY K.NAME, K.MAKHACH",
                    "@S", ActiveStatus);

                var list = new List<DebtPartnerRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    decimal bill;
                    decimal adj;
                    billDebts.TryGetValue(id, out bill);
                    voucherAdj.TryGetValue(id, out adj);
                    list.Add(new DebtPartnerRow
                    {
                        Id = id,
                        Code = DbHelper.Str(r["MAKHACH"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMKHACHHANGID"]) ?? "",
                        GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        RemainingDebt = bill + adj
                    });
                }
                return list;
            });
        }

        /// <summary>Danh sách nhà cung cấp đang dùng (STATUS = 30) kèm Còn nợ tính gộp.</summary>
        public List<DebtPartnerRow> LoadSupplierDebts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                Dictionary<string, decimal> docDebts = LoadSupplierDocDebtMapIn(conn, null);
                Dictionary<string, decimal> voucherPaid = LoadSupplierVoucherPaidMapIn(conn, null);

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT N.ID, N.MANHACUNGCAP, N.NAME, N.DNHOMNHACUNGCAPID, COALESCE(G.NAME, '') AS GROUPNAME, " +
                    "N.DIACHI, N.DIENTHOAI " +
                    "FROM DNHACUNGCAP N " +
                    "LEFT JOIN DNHOMNHACUNGCAP G ON G.ID = N.DNHOMNHACUNGCAPID " +
                    "WHERE N.STATUS = @S " +
                    "ORDER BY N.NAME, N.MANHACUNGCAP",
                    "@S", ActiveStatus);

                var list = new List<DebtPartnerRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    decimal doc;
                    decimal paid;
                    docDebts.TryGetValue(id, out doc);
                    voucherPaid.TryGetValue(id, out paid);
                    list.Add(new DebtPartnerRow
                    {
                        Id = id,
                        Code = DbHelper.Str(r["MANHACUNGCAP"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMNHACUNGCAPID"]) ?? "",
                        GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        RemainingDebt = doc - paid
                    });
                }
                return list;
            });
        }

        /// <summary>Còn nợ của một khách hàng theo công thức §2 (khớp ServicePaymentRepository.GetOldDebtIn).</summary>
        public decimal GetCustomerDebt(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;
            return DbHelper.Read(_databasePath, conn => GetCustomerDebtIn(conn, null, customerId));
        }

        public static decimal GetCustomerDebtIn(FbConnection conn, FbTransaction tx, string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;

            decimal billDebt = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(TONGCONG, 0) - COALESCE(TIENTHANHTOAN, 0)), 0) " +
                "FROM TDONHANG WHERE DKHACHHANGID = @C " +
                "AND COALESCE(LOAI, 0) IN (0, 99) " +
                "AND COALESCE(DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(STATUS, 0) = 30",
                "@C", customerId));

            decimal voucherAdj = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(CHI, 0) - COALESCE(THU, 0)), 0) " +
                "FROM TTHUCHI WHERE DKHACHHANGID = @C " +
                "AND COALESCE(KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (TDATHANGID IS NULL OR TDATHANGID = '') " +
                "AND COALESCE(STATUS, 0) <> @Del",
                "@C", customerId, "@Del", DeletedStatus));

            return billDebt + voucherAdj;
        }

        /// <summary>Còn nợ của một nhà cung cấp theo công thức §2.</summary>
        public decimal GetSupplierDebt(string supplierId)
        {
            if (string.IsNullOrEmpty(supplierId))
                return 0m;
            return DbHelper.Read(_databasePath, conn => GetSupplierDebtIn(conn, null, supplierId));
        }

        public static decimal GetSupplierDebtIn(FbConnection conn, FbTransaction tx, string supplierId)
        {
            if (string.IsNullOrEmpty(supplierId))
                return 0m;

            decimal docDebt = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(" +
                "CASE WHEN COALESCE(LOAI, 0) = 2 " +
                "THEN -(COALESCE(TONGCONG, 0) - COALESCE(TIENTHANHTOAN, 0)) " +
                "ELSE (COALESCE(TONGCONG, 0) - COALESCE(TIENTHANHTOAN, 0)) END), 0) " +
                "FROM TDONHANG WHERE DNHACUNGCAPID = @S " +
                "AND COALESCE(LOAI, 0) IN (1, 2, 98) " +
                "AND COALESCE(STATUS, 0) = 30",
                "@S", supplierId));

            decimal voucherPaid = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(CHI, 0) - COALESCE(THU, 0)), 0) " +
                "FROM TTHUCHI WHERE DNHACUNGCAPID = @S " +
                "AND COALESCE(KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (TDATHANGID IS NULL OR TDATHANGID = '') " +
                "AND COALESCE(STATUS, 0) <> @Del",
                "@S", supplierId, "@Del", DeletedStatus));

            return docDebt - voucherPaid;
        }

        public List<DebtGroupNode> LoadCustomerGroups()
        {
            return LoadGroups("DNHOMKHACHHANG");
        }

        public List<DebtGroupNode> LoadSupplierGroups()
        {
            return LoadGroups("DNHOMNHACUNGCAP");
        }

        /// <summary>Tìm ID lý do thu "Thu công nợ" (LALYDOTHU = 30) nếu có trong DLYDOTHUCHI.</summary>
        public string FindDefaultDebtReceiptReasonId()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLYDOTHUCHI WHERE STATUS = @S AND COALESCE(LALYDOTHU, 0) = 30",
                    "@S", ActiveStatus);
                foreach (DataRow r in dt.Rows)
                {
                    string folded = CustomerText.FoldNoTrim((DbHelper.Str(r["NAME"]) ?? "").Trim());
                    if (folded == "thu cong no" || folded == "thu no" || folded == "thu no khach hang")
                        return DbHelper.Str(r["ID"]);
                }
                return null;
            });
        }

        // ------------------------------------------------------------------ Nội bộ truy vấn gộp

        private List<DebtGroupNode> LoadGroups(string tableName)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, PARENTID, SORTORDER FROM " + tableName +
                    " WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);
                var list = new List<DebtGroupNode>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new DebtGroupNode
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        private static Dictionary<string, decimal> LoadCustomerBillDebtMapIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.DKHACHHANGID, " +
                "COALESCE(SUM(COALESCE(H.TONGCONG, 0) - COALESCE(H.TIENTHANHTOAN, 0)), 0) AS DEBT " +
                "FROM TDONHANG H " +
                "WHERE H.DKHACHHANGID IS NOT NULL AND H.DKHACHHANGID <> '' " +
                "AND COALESCE(H.LOAI, 0) IN (0, 99) " +
                "AND COALESCE(H.DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "GROUP BY H.DKHACHHANGID");
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DKHACHHANGID"]);
                if (!string.IsNullOrEmpty(id))
                    map[id] = DbHelper.Dec(r["DEBT"]);
            }
            return map;
        }

        private static Dictionary<string, decimal> LoadCustomerVoucherAdjMapIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.DKHACHHANGID, " +
                "COALESCE(SUM(COALESCE(T.CHI, 0) - COALESCE(T.THU, 0)), 0) AS ADJ " +
                "FROM TTHUCHI T " +
                "WHERE T.DKHACHHANGID IS NOT NULL AND T.DKHACHHANGID <> '' " +
                "AND COALESCE(T.KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (T.TDATHANGID IS NULL OR T.TDATHANGID = '') " +
                "AND COALESCE(T.STATUS, 0) <> @Del " +
                "GROUP BY T.DKHACHHANGID",
                "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DKHACHHANGID"]);
                if (!string.IsNullOrEmpty(id))
                    map[id] = DbHelper.Dec(r["ADJ"]);
            }
            return map;
        }

        private static Dictionary<string, decimal> LoadSupplierDocDebtMapIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.DNHACUNGCAPID, " +
                "COALESCE(SUM(CASE WHEN COALESCE(H.LOAI, 0) = 2 " +
                "THEN -(COALESCE(H.TONGCONG, 0) - COALESCE(H.TIENTHANHTOAN, 0)) " +
                "ELSE (COALESCE(H.TONGCONG, 0) - COALESCE(H.TIENTHANHTOAN, 0)) END), 0) AS DEBT " +
                "FROM TDONHANG H " +
                "WHERE H.DNHACUNGCAPID IS NOT NULL AND H.DNHACUNGCAPID <> '' " +
                "AND COALESCE(H.LOAI, 0) IN (1, 2, 98) " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "GROUP BY H.DNHACUNGCAPID");
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DNHACUNGCAPID"]);
                if (!string.IsNullOrEmpty(id))
                    map[id] = DbHelper.Dec(r["DEBT"]);
            }
            return map;
        }

        private static Dictionary<string, decimal> LoadSupplierVoucherPaidMapIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT T.DNHACUNGCAPID, " +
                "COALESCE(SUM(COALESCE(T.CHI, 0) - COALESCE(T.THU, 0)), 0) AS PAID " +
                "FROM TTHUCHI T " +
                "WHERE T.DNHACUNGCAPID IS NOT NULL AND T.DNHACUNGCAPID <> '' " +
                "AND COALESCE(T.KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (T.TDATHANGID IS NULL OR T.TDATHANGID = '') " +
                "AND COALESCE(T.STATUS, 0) <> @Del " +
                "GROUP BY T.DNHACUNGCAPID",
                "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DNHACUNGCAPID"]);
                if (!string.IsNullOrEmpty(id))
                    map[id] = DbHelper.Dec(r["PAID"]);
            }
            return map;
        }
    }
}
