using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục voucher (DVOUCHER, spec docs/specs/danh-muc-voucher.md §2).
    /// Kiểm trùng Mã voucher theo collation UNICODE_CI_AI; phân biệt hóa đơn đã thanh toán
    /// (DATHANHTOAN = 30, STATUS &lt;&gt; -30 cho cột Đã dùng) với bất kỳ hóa đơn nào tham chiếu
    /// DVOUCHERID (chặn xóa cứng và chặn sửa trường chính); không DDL.
    /// </summary>
    public sealed class GiftVoucherRepository
    {
        public const int ActiveStatus = GiftVoucherText.ActiveStatus;
        public const int DeletedStatus = GiftVoucherText.DeletedStatus;

        private const string SelectSql =
            "SELECT V.ID, V.NAME, V.NOTE, V.DNHOMVOUCHERID, COALESCE(V.GIATRI, 0) AS GIATRI, " +
            "V.NGAYPHATHANH, V.HANSUDUNG, V.DKHACHHANGID, COALESCE(V.STATUS, 0) AS STATUS, " +
            "V.USERCREATEDID, V.TIMECREATED, V.USERMODIFIEDID, V.TIMEMODIFIED, " +
            "COALESCE(G.NAME, '') AS GROUPNAME, " +
            "COALESCE(K.NAME, '') AS PARTNERNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DVOUCHER V " +
            "LEFT JOIN DNHOMVOUCHER G ON G.ID = V.DNHOMVOUCHERID " +
            "LEFT JOIN DKHACHHANG K ON K.ID = V.DKHACHHANGID " +
            "LEFT JOIN SUSER UC ON UC.ID = V.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = V.USERMODIFIEDID ";

        private readonly string _databasePath;

        public GiftVoucherRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null).Date);
        }

        /// <summary>Mọi voucher đang hoạt động (STATUS = 30), kèm tên nhóm, đối tác và trạng thái hóa đơn tham chiếu.</summary>
        public List<GiftVoucherRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE V.STATUS = @S ORDER BY V.NAME",
                    "@S", ActiveStatus);

                var paidBills = new Dictionary<string, string>(StringComparer.Ordinal);
                var anyBills = new Dictionary<string, string>(StringComparer.Ordinal);
                DataTable billDt = DbHelper.Table(conn, null,
                    "SELECT ID, DVOUCHERID, NAME, COALESCE(DATHANHTOAN, 0) AS DATHANHTOAN, COALESCE(STATUS, 0) AS STATUS " +
                    "FROM TDONHANG WHERE DVOUCHERID IS NOT NULL AND TRIM(DVOUCHERID) <> '' " +
                    "ORDER BY TIMECREATED DESC, ID");
                foreach (DataRow br in billDt.Rows)
                {
                    string vid = DbHelper.Str(br["DVOUCHERID"]) ?? "";
                    if (vid.Length == 0)
                        continue;
                    string billLabel = ResolveBillLabel(DbHelper.Str(br["NAME"]), DbHelper.Str(br["ID"]));
                    if (!anyBills.ContainsKey(vid))
                        anyBills[vid] = billLabel;
                    bool paidActive = DbHelper.Int(br["DATHANHTOAN"]) == ActiveStatus
                        && DbHelper.Int(br["STATUS"]) != DeletedStatus;
                    if (paidActive && !paidBills.ContainsKey(vid))
                        paidBills[vid] = billLabel;
                }

                var list = new List<GiftVoucherRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    GiftVoucherRow row = RowFrom(r);
                    string paid;
                    if (paidBills.TryGetValue(row.Id, out paid))
                        row.PaidBillName = paid;
                    string anyRef;
                    if (anyBills.TryGetValue(row.Id, out anyRef))
                        row.AnyRefBillName = anyRef;
                    list.Add(row);
                }
                return list;
            });
        }

        /// <summary>Một voucher theo ID (mọi STATUS), kèm thông tin hóa đơn tham chiếu; null nếu không tồn tại.</summary>
        public GiftVoucherRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static GiftVoucherRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE V.ID = @I", "@I", id);
            if (dt.Rows.Count == 0)
                return null;
            GiftVoucherRow row = RowFrom(dt.Rows[0]);
            row.PaidBillName = FindFirstPaidBillNameIn(conn, tx, id);
            row.AnyRefBillName = FindFirstAnyRefBillNameIn(conn, tx, id);
            return row;
        }

        /// <summary>Tìm số hóa đơn ĐÃ thanh toán (DATHANHTOAN = 30, STATUS &lt;&gt; -30) đầu tiên dùng voucher này.</summary>
        public static string FindFirstPaidBillNameIn(FbConnection conn, FbTransaction tx, string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME FROM TDONHANG WHERE DVOUCHERID = @I " +
                "AND COALESCE(DATHANHTOAN, 0) = @Paid AND COALESCE(STATUS, 0) <> @Del " +
                "ORDER BY TIMECREATED DESC, ID",
                "@I", voucherId, "@Paid", ActiveStatus, "@Del", DeletedStatus);
            if (dt.Rows.Count == 0)
                return null;
            return ResolveBillLabel(DbHelper.Str(dt.Rows[0]["NAME"]), DbHelper.Str(dt.Rows[0]["ID"]));
        }

        /// <summary>Tìm số hóa đơn BẤT KỲ (mọi STATUS, mọi DATHANHTOAN) đầu tiên tham chiếu DVOUCHERID.</summary>
        public static string FindFirstAnyRefBillNameIn(FbConnection conn, FbTransaction tx, string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME FROM TDONHANG WHERE DVOUCHERID = @I ORDER BY TIMECREATED DESC, ID",
                "@I", voucherId);
            if (dt.Rows.Count == 0)
                return null;
            return ResolveBillLabel(DbHelper.Str(dt.Rows[0]["NAME"]), DbHelper.Str(dt.Rows[0]["ID"]));
        }

        /// <summary>Mã voucher đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public bool IsCodeTaken(string code, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            return DbHelper.Read(_databasePath, conn => IsCodeTakenIn(conn, null, code, excludeId));
        }

        public static bool IsCodeTakenIn(FbConnection conn, FbTransaction tx, string code, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            string target = GiftVoucherText.FoldNoTrim(code);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DVOUCHER WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(GiftVoucherText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, GiftVoucherRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DVOUCHER (ID, NAME, NOTE, DNHOMVOUCHERID, GIATRI, NGAYPHATHANH, HANSUDUNG, " +
                "DKHACHHANGID, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NOTE, @GRP, @VAL, @ISSUE, @EXP, @PARTNER, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Code ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@VAL", row.Value,
                "@ISSUE", row.IssueDate.HasValue ? (object)row.IssueDate.Value.Date : null,
                "@EXP", row.ExpiryDate.HasValue ? (object)row.ExpiryDate.Value.Date : null,
                "@PARTNER", NullIfEmpty(row.PartnerId),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, GiftVoucherRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DVOUCHER SET NAME = @NAME, NOTE = @NOTE, DNHOMVOUCHERID = @GRP, GIATRI = @VAL, " +
                "NGAYPHATHANH = @ISSUE, HANSUDUNG = @EXP, DKHACHHANGID = @PARTNER, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Code ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@VAL", row.Value,
                "@ISSUE", row.IssueDate.HasValue ? (object)row.IssueDate.Value.Date : null,
                "@EXP", row.ExpiryDate.HasValue ? (object)row.ExpiryDate.Value.Date : null,
                "@PARTNER", NullIfEmpty(row.PartnerId),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DVOUCHER WHERE ID = @I", "@I", id);
        }

        private static string ResolveBillLabel(string billName, string billId)
        {
            string trimmed = (billName ?? "").Trim();
            if (trimmed.Length > 0)
                return trimmed;
            return string.IsNullOrEmpty(billId) ? "(không số)" : billId;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static DateTime? ReadDate(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }

        private static GiftVoucherRow RowFrom(DataRow r)
        {
            return new GiftVoucherRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMVOUCHERID"]),
                Value = DbHelper.Dec(r["GIATRI"]),
                IssueDate = ReadDate(r["NGAYPHATHANH"]),
                ExpiryDate = ReadDate(r["HANSUDUNG"]),
                PartnerId = DbHelper.Str(r["DKHACHHANGID"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = ReadDate(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = ReadDate(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                PartnerName = DbHelper.Str(r["PARTNERNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
