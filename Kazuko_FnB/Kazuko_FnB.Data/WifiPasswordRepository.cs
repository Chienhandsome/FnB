using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục mật khẩu wifi (DMATKHAUWIFI, spec docs/specs/quan-tri-nho.md §3).
    /// Kiểm trùng mật khẩu theo UNICODE_CI_AI trên toàn bảng; xóa cứng; không DDL.
    /// </summary>
    public sealed class WifiPasswordRepository
    {
        public const int ActiveStatus = WifiPasswordText.ActiveStatus;

        private const string SelectSql =
            "SELECT W.ID, W.NAME, W.NOTE, W.DNHOMWIFIID, COALESCE(W.STATUS, 0) AS STATUS, " +
            "W.USERCREATEDID, W.TIMECREATED, W.USERMODIFIEDID, W.TIMEMODIFIED, " +
            "COALESCE(G.NAME, '') AS GROUPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DMATKHAUWIFI W " +
            "LEFT JOIN DNHOMWIFI G ON G.ID = W.DNHOMWIFIID " +
            "LEFT JOIN SUSER UC ON UC.ID = W.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = W.USERMODIFIEDID ";

        private readonly string _databasePath;

        public WifiPasswordRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi mật khẩu wifi đang hoạt động (STATUS = 30), xếp theo tên.</summary>
        public List<WifiPasswordRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE W.STATUS = @S ORDER BY W.NAME",
                    "@S", ActiveStatus);
                var list = new List<WifiPasswordRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một mật khẩu wifi theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public WifiPasswordRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static WifiPasswordRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE W.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Mật khẩu wifi đã tồn tại chưa (so theo UNICODE_CI_AI toàn bảng, không cắt khoảng trắng)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, name, excludeId));
        }

        public static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string target = WifiPasswordText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DMATKHAUWIFI WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(WifiPasswordText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, WifiPasswordRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DMATKHAUWIFI (ID, NAME, NOTE, DNHOMWIFIID, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NOTE, @GRP, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, WifiPasswordRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DMATKHAUWIFI SET NAME = @NAME, NOTE = @NOTE, DNHOMWIFIID = @GRP, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DMATKHAUWIFI WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static DateTime? ReadDate(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }

        private static WifiPasswordRow RowFrom(DataRow r)
        {
            return new WifiPasswordRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMWIFIID"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = ReadDate(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = ReadDate(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
