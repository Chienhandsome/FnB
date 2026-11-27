using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi nhóm nhà cung cấp (DNHOMNHACUNGCAP — bảng cây, spec docs/specs/danh-muc-nha-cung-cap/).
    /// Tên nhóm bắt buộc, không trùng (UNICODE_CI_AI, không cắt khoảng trắng);
    /// xóa cứng chỉ khi không còn nhà cung cấp thuộc nhóm và không còn nhóm con; không DDL.
    /// </summary>
    public sealed class SupplierGroupRepository
    {
        public const int ActiveStatus = SupplierText.ActiveStatus;

        private const string SelectColumns =
            "ID, NAME, NOTE, COALESCE(STATUS, 0) AS STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, " +
            "SORTORDER, USERCREATEDID, PARENTID, PARENTDIR, COALESCE(ITEMTYPE, 0) AS ITEMTYPE, " +
            "COALESCE(AUTOID, 0) AS AUTOID, SIMAGEID";

        private readonly string _databasePath;

        public SupplierGroupRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi nhóm nhà cung cấp đang dùng (STATUS = 30), xếp theo SORTORDER rồi NAME.</summary>
        public List<SupplierGroupRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " FROM DNHOMNHACUNGCAP WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);
                var list = new List<SupplierGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhóm nhà cung cấp theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public SupplierGroupRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static SupplierGroupRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " FROM DNHOMNHACUNGCAP WHERE ID = @I",
                "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Tên nhóm đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
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
            string target = SupplierText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DNHOMNHACUNGCAP WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(SupplierText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Số nhà cung cấp thuộc nhóm (để chặn xóa nhóm còn nhà cung cấp).</summary>
        public int CountSuppliers(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn => CountSuppliersIn(conn, null, groupId));
        }

        public static int CountSuppliersIn(FbConnection conn, FbTransaction tx, string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DNHACUNGCAP WHERE DNHOMNHACUNGCAPID = @G",
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
                "SELECT COUNT(*) FROM DNHOMNHACUNGCAP WHERE PARENTID = @G",
                "@G", groupId));
        }

        public static int NextAutoIdIn(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DNHOMNHACUNGCAP");
            return v == null ? 1 : Convert.ToInt32(v) + 1;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, SupplierGroupRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHOMNHACUNGCAP (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, " +
                "USERMODIFIEDID, TIMEMODIFIED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID) " +
                "VALUES (@ID, @NAME, @NOTE, @ST, @UC, @TC, NULL, NULL, @SORT, @PID, @PDIR, @ITYPE, @AUTO, @IMG)",
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
                "@AUTO", row.AutoId,
                "@IMG", string.IsNullOrEmpty(row.ImageId) ? null : row.ImageId);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, SupplierGroupRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHOMNHACUNGCAP SET NAME = @NAME, NOTE = @NOTE, SIMAGEID = @IMG, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@NOTE", string.IsNullOrEmpty(row.Note) ? null : row.Note,
                "@IMG", string.IsNullOrEmpty(row.ImageId) ? null : row.ImageId,
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DNHOMNHACUNGCAP WHERE ID = @I", "@I", id);
        }

        private static SupplierGroupRow RowFrom(DataRow r)
        {
            return new SupplierGroupRow
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
                AutoId = DbHelper.Int(r["AUTOID"]),
                ImageId = DbHelper.Str(r["SIMAGEID"])
            };
        }
    }
}
