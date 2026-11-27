using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi nhóm wifi (DNHOMWIFI — bảng cây chuẩn, spec docs/specs/quan-tri-nho.md §3).
    /// Tên nhóm bắt buộc, không trùng trong cùng nhóm cha (UNICODE_CI_AI);
    /// xóa cứng chỉ khi không còn mật khẩu thuộc nhóm và không còn nhóm con; không DDL.
    /// </summary>
    public sealed class WifiGroupRepository
    {
        public const int ActiveStatus = WifiPasswordText.ActiveStatus;

        private const string SelectColumns =
            "ID, NAME, NOTE, COALESCE(STATUS, 0) AS STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, " +
            "SORTORDER, USERCREATEDID, PARENTID, PARENTDIR, COALESCE(ITEMTYPE, 0) AS ITEMTYPE, " +
            "COALESCE(AUTOID, 0) AS AUTOID";

        private readonly string _databasePath;

        public WifiGroupRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi nhóm wifi đang hoạt động (STATUS = 30), xếp theo SORTORDER rồi NAME.</summary>
        public List<WifiGroupRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " FROM DNHOMWIFI WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);
                var list = new List<WifiGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhóm wifi theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public WifiGroupRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static WifiGroupRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " FROM DNHOMWIFI WHERE ID = @I",
                "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Tên nhóm đã tồn tại trong cùng nhóm cha chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public bool IsNameTaken(string name, string parentId, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, name, parentId, excludeId));
        }

        public static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string parentId, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string target = WifiPasswordText.FoldNoTrim(name);
            string targetParent = parentId ?? "";
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME, PARENTID FROM DNHOMWIFI WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string rowParent = DbHelper.Str(r["PARENTID"]) ?? "";
                if (!string.Equals(rowParent, targetParent, StringComparison.Ordinal))
                    continue;
                if (string.Equals(WifiPasswordText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Số mật khẩu thuộc nhóm (để chặn xóa nhóm còn mật khẩu).</summary>
        public int CountPasswords(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn => CountPasswordsIn(conn, null, groupId));
        }

        public static int CountPasswordsIn(FbConnection conn, FbTransaction tx, string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DMATKHAUWIFI WHERE DNHOMWIFIID = @G",
                "@G", groupId));
        }

        /// <summary>Số nhóm con trực tiếp của nhóm (để chặn xóa nhóm còn nhóm con).</summary>
        public int CountChildren(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn => CountChildrenIn(conn, null, groupId));
        }

        public static int CountChildrenIn(FbConnection conn, FbTransaction tx, string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DNHOMWIFI WHERE PARENTID = @G",
                "@G", groupId));
        }

        public static int NextAutoIdIn(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DNHOMWIFI");
            return v == null ? 1 : Convert.ToInt32(v) + 1;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, WifiGroupRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHOMWIFI (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, " +
                "USERMODIFIEDID, TIMEMODIFIED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID) " +
                "VALUES (@ID, @NAME, @NOTE, @ST, @UC, @TC, NULL, NULL, @SORT, @PID, @PDIR, @ITYPE, @AUTO)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@NOTE", string.IsNullOrEmpty(row.Note) ? null : row.Note,
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt,
                "@SORT", string.IsNullOrEmpty(row.SortOrder) ? "ZZZZ" : row.SortOrder,
                "@PID", string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId,
                "@PDIR", string.IsNullOrEmpty(row.ParentDir) ? "" : row.ParentDir,
                "@ITYPE", row.ItemType,
                "@AUTO", row.AutoId);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, WifiGroupRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHOMWIFI SET NAME = @NAME, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@NOTE", string.IsNullOrEmpty(row.Note) ? null : row.Note,
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DNHOMWIFI WHERE ID = @I", "@I", id);
        }

        private static WifiGroupRow RowFrom(DataRow r)
        {
            return new WifiGroupRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                SortOrder = DbHelper.Str(r["SORTORDER"]) ?? "",
                ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                ParentDir = DbHelper.Str(r["PARENTDIR"]) ?? "",
                ItemType = DbHelper.Int(r["ITEMTYPE"]),
                AutoId = DbHelper.Int(r["AUTOID"])
            };
        }
    }
}
