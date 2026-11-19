using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đếm bản ghi còn tham chiếu tới một dòng danh mục trước khi xóa (DB không có khóa ngoại nên chương trình tự kiểm).
    /// Tìm mọi bảng có cột <c>column</c> (ví dụ SIMAGEID, SGROUPUSERID, DNHOMKHACHHANGID) rồi đếm theo ID.
    /// Bảng nằm trong <c>ignoreTables</c> (ví dụ dòng quyền đi theo nhóm) không tính.
    /// </summary>
    public static class ReferenceChecker
    {
        private static readonly Regex Identifier = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

        public static int Count(FbConnection conn, FbTransaction tx, string column, string id, params string[] ignoreTables)
        {
            if (!Identifier.IsMatch(column))
                throw new ArgumentException("Tên cột không hợp lệ", "column");
            var ignore = new HashSet<string>(ignoreTables ?? new string[0], StringComparer.OrdinalIgnoreCase);
            DataTable tables = DbHelper.Table(conn, tx,
                "SELECT TRIM(F.RDB$RELATION_NAME) FROM RDB$RELATION_FIELDS F " +
                "JOIN RDB$RELATIONS R ON R.RDB$RELATION_NAME = F.RDB$RELATION_NAME " +
                "WHERE R.RDB$SYSTEM_FLAG = 0 AND R.RDB$VIEW_BLR IS NULL AND TRIM(F.RDB$FIELD_NAME) = @C", "@C", column);
            int total = 0;
            foreach (DataRow r in tables.Rows)
            {
                string table = DbHelper.Str(r[0]);
                if (string.IsNullOrEmpty(table) || ignore.Contains(table) || !Identifier.IsMatch(table))
                    continue;
                object n = DbHelper.Scalar(conn, tx, "SELECT COUNT(*) FROM " + table + " WHERE " + column + " = @I", "@I", id);
                total += DbHelper.Int(n);
            }
            return total;
        }

        /// <summary>Thông báo chặn xóa (đúng chữ app gốc, hộp "Cảnh báo").</summary>
        public static string BlockedMessage(string kind, string name, int count)
        {
            return "Bạn không thể xóa " + kind + " '" + name + "' vì còn (" + count + ") dữ liệu khác liên kết tới '" + name + "'";
        }
    }
}
