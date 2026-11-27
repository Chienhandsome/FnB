using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu Công nợ ban đầu (TDONHANG LOAI = 99 khách hàng / LOAI = 98 nhà cung cấp,
    /// NAME = 'CONGNODAU') theo docs/specs/so-du-dau-ky.md §3 (P2).
    /// Mọi câu SQL tham số hóa; không đặt @param trong NULLIF/COALESCE/UPPER/CASE; không DDL.
    /// </summary>
    public sealed class OpeningDebtRepository
    {
        public const int CustomerOrderType = OpeningDebtConstants.CustomerOrderType;
        public const int SupplierOrderType = OpeningDebtConstants.SupplierOrderType;
        public const int ActiveStatus = OpeningDebtConstants.ActiveStatus;
        public const int DeletedStatus = OpeningDebtConstants.DeletedStatus;
        public const int PaidFlag = OpeningDebtConstants.PaidFlag;

        private readonly string _databasePath;

        public OpeningDebtRepository(string databasePath)
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

        /// <summary>
        /// Nạp toàn bộ danh sách đối tác đang dùng (STATUS = 30) kèm phiếu công nợ ban đầu hiện có
        /// (LOAI = 99 cho khách hàng, LOAI = 98 cho nhà cung cấp) và số tiền đã thu theo đơn (khách hàng).
        /// </summary>
        public OpeningDebtDocument LoadDocument(DebtPartyKind kind, DateTime defaultDate)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                int dupCount;
                Dictionary<string, OpeningDebtOrderHeader> orderByPartner =
                    LoadAllOpeningOrdersMapIn(conn, null, kind, out dupCount);

                DataTable partners = kind == DebtPartyKind.Customer
                    ? DbHelper.Table(conn, null,
                        "SELECT ID, MAKHACH AS CODE, NAME, DIACHI, DIENTHOAI " +
                        "FROM DKHACHHANG WHERE STATUS = @S ORDER BY NAME, MAKHACH",
                        "@S", ActiveStatus)
                    : DbHelper.Table(conn, null,
                        "SELECT ID, MANHACUNGCAP AS CODE, NAME, DIACHI, DIENTHOAI " +
                        "FROM DNHACUNGCAP WHERE STATUS = @S ORDER BY NAME, MANHACUNGCAP",
                        "@S", ActiveStatus);

                var doc = new OpeningDebtDocument
                {
                    PartyKind = kind,
                    Date = defaultDate.Date,
                    DuplicatePartnerCount = dupCount,
                    Warning = dupCount > 0 ? OpeningDebtConstants.DuplicateOrdersWarning(dupCount) : null
                };

                foreach (DataRow r in partners.Rows)
                {
                    string partnerId = DbHelper.Str(r["ID"]) ?? "";
                    OpeningDebtOrderHeader hdr;
                    bool hasOrder = orderByPartner.TryGetValue(partnerId, out hdr);
                    decimal amount = hasOrder ? hdr.Amount : 0m;

                    doc.Items.Add(new OpeningDebtPartnerRow
                    {
                        PartnerId = partnerId,
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        OrderId = hasOrder ? hdr.Id : null,
                        OrderDate = hasOrder && hdr.Date != DateTime.MinValue ? (DateTime?)hdr.Date : null,
                        OpeningDebt = amount,
                        OriginalOpeningDebt = amount,
                        AllocatedPaid = hasOrder ? hdr.AllocatedPaid : 0m
                    });
                }

                return doc;
            });
        }

        /// <summary>
        /// Tìm tất cả phiếu công nợ ban đầu đang dùng (STATUS = 30) của một đối tác,
        /// sắp theo NGAY, TIMECREATED, ID tăng dần.
        /// </summary>
        public static List<OpeningDebtOrderHeader> FindPartnerOrdersIn(
            FbConnection conn,
            FbTransaction tx,
            DebtPartyKind kind,
            string partnerId)
        {
            var list = new List<OpeningDebtOrderHeader>();
            if (string.IsNullOrEmpty(partnerId))
                return list;

            int orderType = kind == DebtPartyKind.Customer ? CustomerOrderType : SupplierOrderType;
            string sql = kind == DebtPartyKind.Customer
                ? "SELECT ID, NAME, NGAY, DKHACHHANGID AS PARTNERID, DCUAHANGID, COALESCE(TONGCONG, 0) AS TONGCONG " +
                  "FROM TDONHANG " +
                  "WHERE DKHACHHANGID = @P AND COALESCE(LOAI, 0) = @L AND COALESCE(STATUS, 0) = @S " +
                  "ORDER BY NGAY ASC, TIMECREATED ASC, ID ASC"
                : "SELECT ID, NAME, NGAY, DNHACUNGCAPID AS PARTNERID, DCUAHANGID, COALESCE(TONGCONG, 0) AS TONGCONG " +
                  "FROM TDONHANG " +
                  "WHERE DNHACUNGCAPID = @P AND COALESCE(LOAI, 0) = @L AND COALESCE(STATUS, 0) = @S " +
                  "ORDER BY NGAY ASC, TIMECREATED ASC, ID ASC";

            DataTable dt = DbHelper.Table(conn, tx, sql, "@P", partnerId, "@L", orderType, "@S", ActiveStatus);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new OpeningDebtOrderHeader
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Date = r["NGAY"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(r["NGAY"]).Date,
                    PartnerId = DbHelper.Str(r["PARTNERID"]) ?? "",
                    StoreId = DbHelper.Str(r["DCUAHANGID"]),
                    Amount = DbHelper.Dec(r["TONGCONG"])
                });
            }
            return list;
        }

        /// <summary>
        /// Tổng số tiền đã thu theo đơn phân bổ vào một phiếu công nợ ban đầu
        /// (SUM(TCHITIETTHANHTOAN.SOTIEN) trên các phiếu thu TTHUCHI chưa xóa mềm).
        /// </summary>
        public static decimal GetAllocatedPaidIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return 0m;

            return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(CT.SOTIEN, 0)), 0) " +
                "FROM TCHITIETTHANHTOAN CT " +
                "INNER JOIN TTHUCHI T ON T.ID = CT.TTHUCHIID " +
                "WHERE CT.TDONHANGID = @O AND COALESCE(T.STATUS, 0) <> @Del",
                "@O", orderId, "@Del", DeletedStatus));
        }

        /// <summary>Kiểm tra đối tác (khách hàng / nhà cung cấp) đang dùng (STATUS = 30) và đọc tên thật.</summary>
        public static bool PartnerActiveExistsIn(
            FbConnection conn,
            FbTransaction tx,
            DebtPartyKind kind,
            string partnerId,
            out string partnerName)
        {
            partnerName = "";
            if (string.IsNullOrEmpty(partnerId))
                return false;

            string sql = kind == DebtPartyKind.Customer
                ? "SELECT NAME FROM DKHACHHANG WHERE ID = @I AND STATUS = @S"
                : "SELECT NAME FROM DNHACUNGCAP WHERE ID = @I AND STATUS = @S";

            DataTable dt = DbHelper.Table(conn, tx, sql, "@I", partnerId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return false;
            partnerName = DbHelper.Str(dt.Rows[0]["NAME"]) ?? "";
            return true;
        }

        public static void InsertOrderIn(
            FbConnection conn,
            FbTransaction tx,
            DebtPartyKind kind,
            string orderId,
            DateTime date,
            string partnerId,
            string storeId,
            decimal amount,
            string actorId,
            DateTime createdAt)
        {
            int orderType = kind == DebtPartyKind.Customer ? CustomerOrderType : SupplierOrderType;
            string customerId = kind == DebtPartyKind.Customer ? NullIfEmpty(partnerId) : null;
            string supplierId = kind == DebtPartyKind.Supplier ? NullIfEmpty(partnerId) : null;
            string desc = kind == DebtPartyKind.Customer
                ? OpeningDebtConstants.CustomerDefaultDescription
                : OpeningDebtConstants.SupplierDefaultDescription;

            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHACHHANGID, DNHACUNGCAPID, DCUAHANGID, " +
                "DIENGIAI, NOTE, " +
                "TIENHANG, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, " +
                "TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG, " +
                "TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @NGAY, @LOAI, @ST, @PAID, @KH, @NCC, @STORE, " +
                "@DESC, NULL, " +
                "@TH, 0, 0, 0, " +
                "0, 0, 0, @TC, " +
                "0, 0, 0, " +
                "@UC, @TCRE, NULL, NULL)",
                "@ID", orderId,
                "@NAME", OpeningDebtConstants.OrderName,
                "@NGAY", date.Date,
                "@LOAI", orderType,
                "@ST", ActiveStatus,
                "@PAID", PaidFlag,
                "@KH", customerId,
                "@NCC", supplierId,
                "@STORE", NullIfEmpty(storeId),
                "@DESC", DebtConstants.ClipText(desc, OpeningDebtConstants.MaxTextLength),
                "@TH", amount,
                "@TC", amount,
                "@UC", DebtConstants.ClipText(actorId ?? "", 36),
                "@TCRE", createdAt);
        }

        public static int UpdateOrderIn(
            FbConnection conn,
            FbTransaction tx,
            DebtPartyKind kind,
            string orderId,
            DateTime date,
            string partnerId,
            decimal amount,
            string actorId,
            DateTime modifiedAt)
        {
            int orderType = kind == DebtPartyKind.Customer ? CustomerOrderType : SupplierOrderType;
            string customerId = kind == DebtPartyKind.Customer ? NullIfEmpty(partnerId) : null;
            string supplierId = kind == DebtPartyKind.Supplier ? NullIfEmpty(partnerId) : null;

            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "NAME = @NAME, NGAY = @NGAY, DKHACHHANGID = @KH, DNHACUNGCAPID = @NCC, " +
                "STATUS = @ST, DATHANHTOAN = @PAID, " +
                "TIENHANG = @TH, TILEGIAMGIA = 0, TIENGIAMGIA = 0, GIAMTHEOTIEN = 0, " +
                "TILETHUE = 0, TIENTHUE = 0, PHIVANCHUYEN = 0, TONGCONG = @TC, " +
                "TIENTHANHTOAN = 0, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD " +
                "WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@NAME", OpeningDebtConstants.OrderName,
                "@NGAY", date.Date,
                "@KH", customerId,
                "@NCC", supplierId,
                "@ST", ActiveStatus,
                "@PAID", PaidFlag,
                "@TH", amount,
                "@TC", amount,
                "@UM", DebtConstants.ClipText(actorId ?? "", 36),
                "@TMOD", modifiedAt,
                "@ID", orderId,
                "@LOAI", orderType);
        }

        public static int DeleteOrderIn(FbConnection conn, FbTransaction tx, DebtPartyKind kind, string orderId)
        {
            int orderType = kind == DebtPartyKind.Customer ? CustomerOrderType : SupplierOrderType;
            PurchaseRepository.DeleteLinesIn(conn, tx, orderId);
            return DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANG WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@ID", orderId, "@LOAI", orderType);
        }

        private static Dictionary<string, OpeningDebtOrderHeader> LoadAllOpeningOrdersMapIn(
            FbConnection conn,
            FbTransaction tx,
            DebtPartyKind kind,
            out int duplicatePartnerCount)
        {
            var map = new Dictionary<string, OpeningDebtOrderHeader>(StringComparer.Ordinal);
            var dupPartners = new HashSet<string>(StringComparer.Ordinal);
            int orderType = kind == DebtPartyKind.Customer ? CustomerOrderType : SupplierOrderType;

            DataTable dt;
            if (kind == DebtPartyKind.Customer)
            {
                dt = DbHelper.Table(conn, tx,
                    "SELECT H.ID, H.NAME, H.NGAY, H.DKHACHHANGID AS PARTNERID, H.DCUAHANGID, " +
                    "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                    "COALESCE((SELECT SUM(COALESCE(CT.SOTIEN, 0)) " +
                    "FROM TCHITIETTHANHTOAN CT INNER JOIN TTHUCHI T ON T.ID = CT.TTHUCHIID " +
                    "WHERE CT.TDONHANGID = H.ID AND COALESCE(T.STATUS, 0) <> @Del), 0) AS ALLOCPAID " +
                    "FROM TDONHANG H " +
                    "WHERE H.DKHACHHANGID IS NOT NULL AND H.DKHACHHANGID <> '' " +
                    "AND COALESCE(H.LOAI, 0) = @L AND COALESCE(H.STATUS, 0) = @S " +
                    "ORDER BY H.NGAY ASC, H.TIMECREATED ASC, H.ID ASC",
                    "@Del", DeletedStatus, "@L", orderType, "@S", ActiveStatus);
            }
            else
            {
                dt = DbHelper.Table(conn, tx,
                    "SELECT H.ID, H.NAME, H.NGAY, H.DNHACUNGCAPID AS PARTNERID, H.DCUAHANGID, " +
                    "COALESCE(H.TONGCONG, 0) AS TONGCONG " +
                    "FROM TDONHANG H " +
                    "WHERE H.DNHACUNGCAPID IS NOT NULL AND H.DNHACUNGCAPID <> '' " +
                    "AND COALESCE(H.LOAI, 0) = @L AND COALESCE(H.STATUS, 0) = @S " +
                    "ORDER BY H.NGAY ASC, H.TIMECREATED ASC, H.ID ASC",
                    "@L", orderType, "@S", ActiveStatus);
            }

            foreach (DataRow r in dt.Rows)
            {
                string partnerId = DbHelper.Str(r["PARTNERID"]) ?? "";
                if (string.IsNullOrEmpty(partnerId))
                    continue;
                if (map.ContainsKey(partnerId))
                {
                    dupPartners.Add(partnerId);
                    continue;
                }
                map[partnerId] = new OpeningDebtOrderHeader
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Date = r["NGAY"] is DBNull ? DateTime.MinValue : Convert.ToDateTime(r["NGAY"]).Date,
                    PartnerId = partnerId,
                    StoreId = DbHelper.Str(r["DCUAHANGID"]),
                    Amount = DbHelper.Dec(r["TONGCONG"]),
                    AllocatedPaid = kind == DebtPartyKind.Customer ? DbHelper.Dec(r["ALLOCPAID"]) : 0m
                };
            }

            duplicatePartnerCount = dupPartners.Count;
            return map;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
