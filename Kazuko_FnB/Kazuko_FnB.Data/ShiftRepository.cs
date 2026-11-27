using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục ca làm việc (DCALAMVIEC, spec docs/specs/nhan-su-danh-muc.md §3).
    /// Kiểm trùng Tên (UNICODE_CI_AI, không cắt khoảng trắng); đếm tham chiếu 2 bảng
    /// (DNHANVIEN.DCALAMVIECID, TBANGLUONGCHITIET.DCALAMVIECID, kiểm tồn tại qua RDB$RELATION_FIELDS);
    /// xóa cứng; không DDL; để trống ITEMTYPE khi thêm mới và giữ nguyên khi sửa.
    /// </summary>
    public sealed class ShiftRepository
    {
        public const int ActiveStatus = ShiftText.ActiveStatus;

        /// <summary>2 bảng tham chiếu tới DCALAMVIEC.ID theo spec §3.</summary>
        public static readonly ShiftReferenceColumn[] ReferenceColumns =
        {
            new ShiftReferenceColumn("DNHANVIEN", "DCALAMVIECID", "nhân viên"),
            new ShiftReferenceColumn("TBANGLUONGCHITIET", "DCALAMVIECID", "dòng chấm công")
        };

        private const string SelectSql =
            "SELECT C.ID, C.NAME, C.TUGIO, C.DENGIO, COALESCE(C.TILELUONG, 0) AS TILELUONG, " +
            "C.NOTE, COALESCE(C.STATUS, 0) AS STATUS, " +
            "C.USERCREATEDID, C.TIMECREATED, C.USERMODIFIEDID, C.TIMEMODIFIED, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DCALAMVIEC C " +
            "LEFT JOIN SUSER UC ON UC.ID = C.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = C.USERMODIFIEDID ";

        private readonly string _databasePath;

        public ShiftRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi ca làm việc đang dùng (STATUS = 30), sắp tăng theo tên.</summary>
        public List<ShiftRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE C.STATUS = @S ORDER BY C.NAME",
                    "@S", ActiveStatus);
                var list = new List<ShiftRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một ca làm việc theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public ShiftRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static ShiftRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE C.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Tên ca đã tồn tại chưa (UNICODE_CI_AI: bỏ hoa/thường và dấu, không cắt khoảng trắng)?</summary>
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
            string target = ShiftText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DCALAMVIEC WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(ShiftText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Đếm tham chiếu ca làm việc trên các bảng hiện có trong CSDL.</summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new List<ReferenceBlock>();
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        public static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceColumns.Length);
            foreach (ShiftReferenceColumn rc in ReferenceColumns)
            {
                if (!EmployeeRepository.ColumnExistsIn(conn, tx, rc.TableName, rc.ColumnName))
                    continue;
                int count = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM " + rc.TableName + " WHERE " + rc.ColumnName + " = @I",
                    "@I", id));
                blocks.Add(new ReferenceBlock
                {
                    TableName = rc.TableName,
                    ColumnName = rc.ColumnName,
                    Count = count
                });
            }
            return blocks;
        }

        /// <summary>Thêm mới ca làm việc (để trống ITEMTYPE, PARENTID, PARENTDIR → NULL).</summary>
        public static void InsertIn(FbConnection conn, FbTransaction tx, ShiftRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DCALAMVIEC (ID, NAME, TUGIO, DENGIO, TILELUONG, NOTE, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @FROM, @TO, @RATE, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@FROM", (object)row.FromTime,
                "@TO", (object)row.ToTime,
                "@RATE", row.SalaryRate,
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        /// <summary>Cập nhật ca làm việc (không chạm ITEMTYPE, USERCREATEDID, TIMECREATED).</summary>
        public static int UpdateIn(FbConnection conn, FbTransaction tx, ShiftRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DCALAMVIEC SET NAME = @NAME, TUGIO = @FROM, DENGIO = @TO, TILELUONG = @RATE, " +
                "NOTE = @NOTE, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@FROM", (object)row.FromTime,
                "@TO", (object)row.ToTime,
                "@RATE", row.SalaryRate,
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DCALAMVIEC WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static ShiftRow RowFrom(DataRow r)
        {
            return new ShiftRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                FromTime = r["TUGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TUGIO"]),
                ToTime = r["DENGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["DENGIO"]),
                SalaryRate = DbHelper.Dec(r["TILELUONG"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
