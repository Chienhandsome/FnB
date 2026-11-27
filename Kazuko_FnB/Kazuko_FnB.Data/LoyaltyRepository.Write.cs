using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Phần sinh số phiếu, kiểm trùng số phiếu và ghi/xóa cứng phiếu tặng/giảm điểm TTANGGIAMDIEM
    /// (docs/specs/khach-hang-than-thiet.md §3, §6).
    /// Cột thật TTANGGIAMDIEM: ID, NAME, NOTE, NGAY, DKHACHHANGID, DIEMTANG, DIEMGIAM, LYDO,
    /// STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED.
    /// </summary>
    public sealed partial class LoyaltyRepository
    {
        /// <summary>Sinh số phiếu tự động dạng TDyy/00001 tiếp nối số lớn nhất hiện có trong TTANGGIAMDIEM (T1).</summary>
        public string GenerateNumber(DateTime date)
        {
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, date));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, DateTime date)
        {
            string template = LoyaltyConstants.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TTANGGIAMDIEM WHERE NAME STARTING WITH @P",
                "@P", prefix);

            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);

            int nextSeq = DocumentNumber.NextSequence(names, prefix);
            return DocumentNumber.Format(template, date, nextSeq);
        }

        /// <summary>Kiểm tra số phiếu đã tồn tại chưa (không phân biệt hoa/thường/dấu, loại chính phiếu đang sửa).</summary>
        public static bool IsNumberTakenIn(FbConnection conn, FbTransaction tx, string number, string excludeId)
        {
            string trimmed = (number ?? "").Trim();
            if (trimmed.Length == 0)
                return false;

            string target = CustomerText.FoldNoTrim(trimmed);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM TTANGGIAMDIEM WHERE COALESCE(STATUS, 0) <> @Del AND NAME IS NOT NULL",
                "@Del", DeletedStatus);

            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string existing = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                if (string.Equals(CustomerText.FoldNoTrim(existing), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Thêm mới một phiếu TTANGGIAMDIEM trong transaction hiện tại.</summary>
        public static void InsertVoucherIn(FbConnection conn, FbTransaction tx, LoyaltyPointVoucherRow row)
        {
            string number = LoyaltyConstants.ClipText((row.Number ?? "").Trim());
            string reason = LoyaltyConstants.ClipText((row.Reason ?? "").Trim());
            string note = LoyaltyConstants.ClipText((row.Note ?? "").Trim());
            string createdBy = LoyaltyConstants.ClipText(row.CreatedById ?? "", 36);

            DbHelper.Exec(conn, tx,
                "INSERT INTO TTANGGIAMDIEM (" +
                "ID, NAME, NOTE, NGAY, DKHACHHANGID, DIEMTANG, DIEMGIAM, LYDO, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NOTE, @NGAY, @KH, @TANG, @GIAM, @LYDO, " +
                "@ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", number,
                "@NOTE", NullIfEmpty(note),
                "@NGAY", row.Date.Date,
                "@KH", row.CustomerId,
                "@TANG", row.GiftPoints,
                "@GIAM", row.ReducePoints,
                "@LYDO", reason,
                "@ST", ActiveStatus,
                "@UC", createdBy,
                "@TC", row.CreatedAt ?? DbHelper.Now());
        }

        /// <summary>Cập nhật phiếu TTANGGIAMDIEM (giữ nguyên USERCREATEDID/TIMECREATED, cập nhật USERMODIFIEDID/TIMEMODIFIED).</summary>
        public static int UpdateVoucherIn(FbConnection conn, FbTransaction tx, LoyaltyPointVoucherRow row)
        {
            string number = LoyaltyConstants.ClipText((row.Number ?? "").Trim());
            string reason = LoyaltyConstants.ClipText((row.Reason ?? "").Trim());
            string note = LoyaltyConstants.ClipText((row.Note ?? "").Trim());
            string modifiedBy = LoyaltyConstants.ClipText(row.ModifiedById ?? "", 36);

            return DbHelper.Exec(conn, tx,
                "UPDATE TTANGGIAMDIEM SET " +
                "NAME = @NAME, NOTE = @NOTE, NGAY = @NGAY, DKHACHHANGID = @KH, " +
                "DIEMTANG = @TANG, DIEMGIAM = @GIAM, LYDO = @LYDO, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM " +
                "WHERE ID = @ID AND COALESCE(STATUS, 0) <> @Del",
                "@NAME", number,
                "@NOTE", NullIfEmpty(note),
                "@NGAY", row.Date.Date,
                "@KH", row.CustomerId,
                "@TANG", row.GiftPoints,
                "@GIAM", row.ReducePoints,
                "@LYDO", reason,
                "@UM", modifiedBy,
                "@TM", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id,
                "@Del", DeletedStatus);
        }

        /// <summary>Xóa cứng một phiếu tặng/giảm điểm khỏi TTANGGIAMDIEM.</summary>
        public static int DeleteVoucherIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TTANGGIAMDIEM WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
