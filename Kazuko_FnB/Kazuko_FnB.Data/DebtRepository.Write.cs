using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Phần ghi phiếu thu công nợ khách hàng (TTHUCHI LOAI = 0, LAPHIEUTHUCONGNO = 30) kèm các dòng phân bổ
    /// vào TCHITIETTHANHTOAN trong một transaction duy nhất (docs/specs/cong-no.md §7 T1).
    /// KHÔNG sửa TDONHANG.TIENTHANHTOAN của hóa đơn.
    /// </summary>
    public sealed partial class DebtRepository
    {
        /// <summary>Kiểm tra khách hàng còn tồn tại (STATUS = 30) và đọc Tên, Địa chỉ trong transaction.</summary>
        public static bool CustomerActiveExistsIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            out string name,
            out string address)
        {
            name = "";
            address = "";
            if (string.IsNullOrEmpty(customerId))
                return false;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME, DIACHI FROM DKHACHHANG WHERE ID = @I AND STATUS = @S",
                "@I", customerId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return false;
            name = DbHelper.Str(dt.Rows[0]["NAME"]) ?? "";
            address = DbHelper.Str(dt.Rows[0]["DIACHI"]) ?? "";
            return true;
        }

        /// <summary>
        /// Đọc lại một hóa đơn của khách hàng kèm tổng SOTIEN đã phân bổ trên TCHITIETTHANHTOAN trong transaction
        /// để kiểm tra không thu vượt "Còn nợ" của đơn đó.
        /// </summary>
        public static DebtDocumentRow LoadCustomerBillIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string customerId)
        {
            if (string.IsNullOrEmpty(billId) || string.IsNullOrEmpty(customerId))
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, H.NGAY, COALESCE(H.LOAI, 0) AS LOAI, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, " +
                "COALESCE((SELECT SUM(COALESCE(CT.SOTIEN, 0)) " +
                "FROM TCHITIETTHANHTOAN CT INNER JOIN TTHUCHI T ON T.ID = CT.TTHUCHIID " +
                "WHERE CT.TDONHANGID = H.ID AND COALESCE(T.STATUS, 0) <> @Del), 0) AS ALLOCPAID, " +
                "COALESCE(H.DIENGIAI, '') AS DIENGIAI, COALESCE(H.NOTE, '') AS NOTE, H.TIMECREATED " +
                "FROM TDONHANG H " +
                "WHERE H.ID = @B AND H.DKHACHHANGID = @C " +
                "AND COALESCE(H.LOAI, 0) IN (0, 99) " +
                "AND COALESCE(H.DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(H.STATUS, 0) = 30",
                "@B", billId, "@C", customerId, "@Del", DeletedStatus);

            return dt.Rows.Count == 0 ? null : CustomerBillFrom(dt.Rows[0]);
        }

        /// <summary>
        /// Ghi MỘT phiếu thu công nợ vào TTHUCHI: LOAI = 0, LAPHIEUTHUCONGNO = 30, LOAIDOITUONG = 2 (Khách hàng),
        /// KHONGTHAYDOICONGNO = 0, TDATHANGID = NULL, TDONHANGID = NULL, THU = tổng thu.
        /// </summary>
        public static void InsertDebtReceiptIn(
            FbConnection conn,
            FbTransaction tx,
            string voucherId,
            DebtCollectRequest req,
            decimal totalAmount,
            string actorId,
            DateTime createdAt)
        {
            string number = DebtConstants.ClipText((req.VoucherNumber ?? "").Trim());
            string custName = DebtConstants.ClipText((req.CustomerName ?? "").Trim());
            string custAddr = DebtConstants.ClipText((req.CustomerAddress ?? "").Trim());
            string desc = DebtConstants.ClipText((req.Description ?? "").Trim());
            string note = DebtConstants.ClipText((req.Note ?? "").Trim());

            DbHelper.Exec(conn, tx,
                "INSERT INTO TTHUCHI (" +
                "ID, NAME, NGAY, LOAI, LOAIDOITUONG, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, LATAMUNG, " +
                "TENDOITUONG, DIACHI, DNHANVIENID, DKHACHHANGID, DNHACUNGCAPID, DCUAHANGID, DLYDOTHUCHIID, " +
                "DIENGIAI, CHUNGTUGOC, NOTE, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NGAY, 0, 2, 30, 0, 0, " +
                "@TEN, @DC, NULL, @KH, NULL, @CH, @LD, " +
                "@DG, NULL, @NOTE, @THU, 0, @CK, @TK, " +
                "@ST, @UC, @TC, NULL, NULL)",
                "@ID", voucherId,
                "@NAME", number,
                "@NGAY", req.Date.Date,
                "@TEN", NullIfEmpty(custName),
                "@DC", NullIfEmpty(custAddr),
                "@KH", req.CustomerId,
                "@CH", NullIfEmpty(req.StoreId),
                "@LD", NullIfEmpty(req.ReasonId),
                "@DG", NullIfEmpty(desc),
                "@NOTE", NullIfEmpty(note),
                "@THU", totalAmount,
                "@CK", req.Transfer ? ActiveStatus : 0,
                "@TK", req.Transfer ? NullIfEmpty(req.BankAccountId) : null,
                "@ST", ActiveStatus,
                "@UC", DebtConstants.ClipText(actorId ?? "", 36),
                "@TC", createdAt);
        }

        /// <summary>
        /// Ghi các dòng phân bổ vào TCHITIETTHANHTOAN trong cùng transaction với phiếu thu.
        /// Đọc danh sách cột thật từ RDB$RELATION_FIELDS và chỉ ghép các tên cột nằm trong whitelist cố định
        /// để an toàn tuyệt đối với schema thật (luôn có ID, TTHUCHIID, TDONHANGID, SOTIEN; các cột audit nếu có).
        /// </summary>
        public static void InsertAllocationLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string voucherId,
            string voucherNumber,
            IList<DebtAllocationLine> lines,
            string actorId,
            DateTime createdAt)
        {
            if (lines == null || lines.Count == 0)
                return;

            HashSet<string> cols = ReadTableColumnsIn(conn, tx, "TCHITIETTHANHTOAN");
            bool hasNote = cols.Contains("NOTE");
            bool hasName = cols.Contains("NAME");
            bool hasStatus = cols.Contains("STATUS");
            bool hasUserCreated = cols.Contains("USERCREATEDID");
            bool hasTimeCreated = cols.Contains("TIMECREATED");

            var colNames = new List<string> { "ID", "TTHUCHIID", "TDONHANGID", "SOTIEN" };
            var paramNames = new List<string> { "@ID", "@TCID", "@DHID", "@ST" };
            if (hasNote) { colNames.Add("NOTE"); paramNames.Add("@NOTE"); }
            if (hasName) { colNames.Add("NAME"); paramNames.Add("@NAME"); }
            if (hasStatus) { colNames.Add("STATUS"); paramNames.Add("@STATUS"); }
            if (hasUserCreated) { colNames.Add("USERCREATEDID"); paramNames.Add("@UC"); }
            if (hasTimeCreated) { colNames.Add("TIMECREATED"); paramNames.Add("@TCRE"); }

            string sql = "INSERT INTO TCHITIETTHANHTOAN (" + string.Join(", ", colNames.ToArray()) + ") " +
                "VALUES (" + string.Join(", ", paramNames.ToArray()) + ")";

            string clippedActor = DebtConstants.ClipText(actorId ?? "", 36);
            string clippedNumber = DebtConstants.ClipText((voucherNumber ?? "").Trim());

            for (int i = 0; i < lines.Count; i++)
            {
                DebtAllocationLine line = lines[i];
                if (line == null || line.PayAmount <= 0m)
                    continue;

                var args = new List<object>
                {
                    "@ID", DbHelper.NewId(),
                    "@TCID", voucherId,
                    "@DHID", line.BillId,
                    "@ST", line.PayAmount
                };
                if (hasNote)
                {
                    args.Add("@NOTE");
                    args.Add(NullIfEmpty(DebtConstants.ClipText((line.Note ?? "").Trim())));
                }
                if (hasName)
                {
                    args.Add("@NAME");
                    args.Add(clippedNumber);
                }
                if (hasStatus)
                {
                    args.Add("@STATUS");
                    args.Add(ActiveStatus);
                }
                if (hasUserCreated)
                {
                    args.Add("@UC");
                    args.Add(clippedActor);
                }
                if (hasTimeCreated)
                {
                    args.Add("@TCRE");
                    args.Add(createdAt.AddSeconds(i));
                }

                DbHelper.Exec(conn, tx, sql, args.ToArray());
            }
        }

        /// <summary>Đọc các dòng phân bổ TCHITIETTHANHTOAN của một phiếu thu công nợ.</summary>
        public List<DebtAllocationLine> LoadAllocationsByVoucher(string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId))
                return new List<DebtAllocationLine>();

            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT CT.TDONHANGID, COALESCE(H.NAME, '') AS BILLNUM, H.NGAY, " +
                    "COALESCE(H.TONGCONG, 0) AS TONGCONG, COALESCE(CT.SOTIEN, 0) AS SOTIEN, " +
                    "COALESCE(CT.NOTE, '') AS NOTE " +
                    "FROM TCHITIETTHANHTOAN CT " +
                    "LEFT JOIN TDONHANG H ON H.ID = CT.TDONHANGID " +
                    "WHERE CT.TTHUCHIID = @V " +
                    "ORDER BY H.NGAY ASC, H.NAME ASC",
                    "@V", voucherId);

                var list = new List<DebtAllocationLine>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new DebtAllocationLine
                    {
                        BillId = DbHelper.Str(r["TDONHANGID"]) ?? "",
                        BillNumber = DbHelper.Str(r["BILLNUM"]) ?? "",
                        BillDate = ReadDate(r["NGAY"]),
                        BillTotal = DbHelper.Dec(r["TONGCONG"]),
                        PayAmount = DbHelper.Dec(r["SOTIEN"]),
                        Note = DbHelper.Str(r["NOTE"]) ?? ""
                    });
                }
                return list;
            });
        }

        private static HashSet<string> ReadTableColumnsIn(FbConnection conn, FbTransaction tx, string tableName)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @T",
                "@T", tableName);
            foreach (DataRow r in dt.Rows)
            {
                string col = (DbHelper.Str(r[0]) ?? "").Trim();
                if (col.Length > 0)
                    set.Add(col);
            }
            return set;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
