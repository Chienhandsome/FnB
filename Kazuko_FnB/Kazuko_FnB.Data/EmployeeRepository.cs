using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục nhân viên (DNHANVIEN, spec docs/specs/nhan-su-danh-muc.md §2).
    /// Kiểm trùng Tên (UNICODE_CI_AI, không cắt khoảng trắng) và Mã (không phân biệt hoa thường);
    /// đếm tham chiếu 8 bảng (kiểm tra sự tồn tại bảng/cột qua RDB$RELATION_FIELDS trước khi đếm);
    /// xóa cứng; không DDL; không chạm cột LUONGTHEOCA, SIMAGEID, ITEMTYPE khi UPDATE.
    /// </summary>
    public sealed class EmployeeRepository
    {
        public const int ActiveStatus = EmployeeText.ActiveStatus;

        /// <summary>8 bảng tham chiếu tới DNHANVIEN.ID theo spec §2.</summary>
        public static readonly EmployeeReferenceColumn[] ReferenceColumns =
        {
            new EmployeeReferenceColumn("DKHACHHANG", "DNHANVIENID", "khách hàng"),
            new EmployeeReferenceColumn("TTHUCHI", "DNHANVIENID", "phiếu thu chi"),
            new EmployeeReferenceColumn("TTHUONGPHAT", "DNHANVIENID", "phiếu thưởng phạt"),
            new EmployeeReferenceColumn("TBANGLUONGCHITIET", "DNHANVIENID", "dòng chấm công"),
            new EmployeeReferenceColumn("TBANGLUONGTONGHOP", "DNHANVIENID", "bảng lương"),
            new EmployeeReferenceColumn("SUSER", "DNHANVIENID", "tài khoản"),
            new EmployeeReferenceColumn("TSUACHUA", "DNHANVIENID", "phiếu sửa chữa"),
            new EmployeeReferenceColumn("TVAORA", "DNHANVIENID", "dòng vào/ra chấm công")
        };

        private const string SelectSql =
            "SELECT E.ID, E.CODE, E.NAME, E.DIENTHOAI, E.DIACHI, E.DCALAMVIECID, " +
            "COALESCE(E.CACHTINHLUONG, 30) AS CACHTINHLUONG, " +
            "COALESCE(E.LUONGCA, 0) AS LUONGCA, COALESCE(E.LUONGTHANG, 0) AS LUONGTHANG, " +
            "COALESCE(E.NGHICHUNHAT, 0) AS NGHICHUNHAT, COALESCE(E.NGHITHU7, 0) AS NGHITHU7, " +
            "E.LUONGTHEOCA, E.SIMAGEID, E.NOTE, COALESCE(E.STATUS, 0) AS STATUS, " +
            "E.USERCREATEDID, E.TIMECREATED, E.USERMODIFIEDID, E.TIMEMODIFIED, " +
            "COALESCE(C.NAME, '') AS SHIFTNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DNHANVIEN E " +
            "LEFT JOIN DCALAMVIEC C ON C.ID = E.DCALAMVIECID " +
            "LEFT JOIN SUSER UC ON UC.ID = E.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = E.USERMODIFIEDID ";

        private readonly string _databasePath;

        public EmployeeRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi nhân viên đang dùng (STATUS = 30), sắp tăng theo tên.</summary>
        public List<EmployeeRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE E.STATUS = @S ORDER BY E.NAME",
                    "@S", ActiveStatus);
                var list = new List<EmployeeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhân viên theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public EmployeeRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static EmployeeRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE E.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Danh sách ca làm việc đang dùng (DCALAMVIEC STATUS = 30) cho ô chọn Ca làm việc.</summary>
        public List<LookupItem> LoadActiveShifts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DCALAMVIEC WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public static bool ShiftExistsIn(FbConnection conn, FbTransaction tx, string shiftId)
        {
            if (string.IsNullOrWhiteSpace(shiftId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DCALAMVIEC WHERE ID = @I AND STATUS = @S",
                "@I", shiftId, "@S", ActiveStatus));
            return n > 0;
        }

        /// <summary>Tên nhân viên đã tồn tại chưa (UNICODE_CI_AI: bỏ hoa/thường và dấu, không cắt khoảng trắng)?</summary>
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
            string target = EmployeeText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DNHANVIEN WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(EmployeeText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Mã nhân viên đã tồn tại chưa (không phân biệt hoa/thường, T2)?</summary>
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
            string target = code.Trim();
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, CODE FROM DNHANVIEN WHERE CODE IS NOT NULL AND CODE <> ''");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string existingCode = (DbHelper.Str(r["CODE"]) ?? "").Trim();
                if (existingCode.Length > 0 && string.Equals(existingCode, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Kiểm tra bảng và cột có tồn tại thật trong schema Firebird hay không qua RDB$RELATION_FIELDS.</summary>
        public static bool ColumnExistsIn(FbConnection conn, FbTransaction tx, string tableName, string columnName)
        {
            if (!CatalogRepository.IsIdentifier(tableName) || !CatalogRepository.IsIdentifier(columnName))
                return false;
            object n = DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM RDB$RELATION_FIELDS F " +
                "JOIN RDB$RELATIONS R ON R.RDB$RELATION_NAME = F.RDB$RELATION_NAME " +
                "WHERE (R.RDB$SYSTEM_FLAG = 0 OR R.RDB$SYSTEM_FLAG IS NULL) " +
                "AND R.RDB$VIEW_BLR IS NULL " +
                "AND F.RDB$RELATION_NAME = @T " +
                "AND TRIM(F.RDB$FIELD_NAME) = @C",
                "@T", tableName, "@C", columnName);
            return DbHelper.Int(n) > 0;
        }

        /// <summary>Đếm tham chiếu nhân viên trên các bảng hiện có trong CSDL.</summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new List<ReferenceBlock>();
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        public static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceColumns.Length);
            foreach (EmployeeReferenceColumn rc in ReferenceColumns)
            {
                if (!ColumnExistsIn(conn, tx, rc.TableName, rc.ColumnName))
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

        /// <summary>Thêm mới nhân viên (bỏ qua LUONGTHEOCA, SIMAGEID, ITEMTYPE, PARENTID, PARENTDIR → NULL).</summary>
        public static void InsertIn(FbConnection conn, FbTransaction tx, EmployeeRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHANVIEN (ID, CODE, NAME, DIENTHOAI, DIACHI, DCALAMVIECID, " +
                "CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, NOTE, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @CODE, @NAME, @PHONE, @ADDR, @SHIFT, @MODE, @LCA, @LTH, @CN, @T7, @NOTE, " +
                "@ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@CODE", NullIfEmpty(row.Code),
                "@NAME", row.Name ?? "",
                "@PHONE", NullIfEmpty(row.Phone),
                "@ADDR", NullIfEmpty(row.Address),
                "@SHIFT", NullIfEmpty(row.ShiftId),
                "@MODE", row.SalaryMode,
                "@LCA", row.ShiftSalary,
                "@LTH", row.MonthlySalary,
                "@CN", row.OffSunday,
                "@T7", row.OffSaturday,
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        /// <summary>
        /// Cập nhật nhân viên. Không chạm LUONGTHEOCA, SIMAGEID (giữ nguyên giá trị cũ)
        /// và không chạm cột tạo/khung (USERCREATEDID, TIMECREATED, ITEMTYPE, PARENTID, PARENTDIR).
        /// </summary>
        public static int UpdateIn(FbConnection conn, FbTransaction tx, EmployeeRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHANVIEN SET CODE = @CODE, NAME = @NAME, DIENTHOAI = @PHONE, DIACHI = @ADDR, " +
                "DCALAMVIECID = @SHIFT, CACHTINHLUONG = @MODE, LUONGCA = @LCA, LUONGTHANG = @LTH, " +
                "NGHICHUNHAT = @CN, NGHITHU7 = @T7, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@CODE", NullIfEmpty(row.Code),
                "@NAME", row.Name ?? "",
                "@PHONE", NullIfEmpty(row.Phone),
                "@ADDR", NullIfEmpty(row.Address),
                "@SHIFT", NullIfEmpty(row.ShiftId),
                "@MODE", row.SalaryMode,
                "@LCA", row.ShiftSalary,
                "@LTH", row.MonthlySalary,
                "@CN", row.OffSunday,
                "@T7", row.OffSaturday,
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DNHANVIEN WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static EmployeeRow RowFrom(DataRow r)
        {
            return new EmployeeRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                ShiftId = DbHelper.Str(r["DCALAMVIECID"]),
                SalaryMode = DbHelper.Int(r["CACHTINHLUONG"]),
                ShiftSalary = DbHelper.Dec(r["LUONGCA"]),
                MonthlySalary = DbHelper.Dec(r["LUONGTHANG"]),
                OffSunday = DbHelper.Int(r["NGHICHUNHAT"]),
                OffSaturday = DbHelper.Int(r["NGHITHU7"]),
                LegacyShiftSalary = r["LUONGTHEOCA"] is DBNull ? (decimal?)null : DbHelper.Dec(r["LUONGTHEOCA"]),
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                ShiftName = DbHelper.Str(r["SHIFTNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
