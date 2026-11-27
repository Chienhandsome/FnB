using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục nhà cung cấp (DNHACUNGCAP, spec docs/specs/danh-muc-nha-cung-cap/).
    /// Kiểm trùng Mã/Tên theo collation UNICODE_CI_AI (không cắt khoảng trắng); sinh mã tự động NCC + 5 chữ số;
    /// đếm tham chiếu ở 2 bảng TDONHANG.DNHACUNGCAPID và TTHUCHI.DNHACUNGCAPID; xóa cứng; không DDL.
    /// </summary>
    public sealed class SupplierRepository
    {
        public const int ActiveStatus = SupplierText.ActiveStatus;

        /// <summary>2 bảng tham chiếu tới DNHACUNGCAP.ID theo nghiep-vu.md §4.</summary>
        public static readonly string[] ReferenceTables =
        {
            "TDONHANG",
            "TTHUCHI"
        };

        private const string SelectSql =
            "SELECT N.ID, N.MANHACUNGCAP, N.NAME, N.DNHOMNHACUNGCAPID, N.DIACHI, N.DIENTHOAI, " +
            "N.EMAIL, N.WEBSITE, N.NOTE, COALESCE(N.STATUS, 0) AS STATUS, " +
            "N.USERCREATEDID, N.TIMECREATED, N.USERMODIFIEDID, N.TIMEMODIFIED, " +
            "COALESCE(G.NAME, '') AS GROUPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DNHACUNGCAP N " +
            "LEFT JOIN DNHOMNHACUNGCAP G ON G.ID = N.DNHOMNHACUNGCAPID " +
            "LEFT JOIN SUSER UC ON UC.ID = N.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = N.USERMODIFIEDID ";

        private readonly string _databasePath;

        public SupplierRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi nhà cung cấp đang dùng (STATUS = 30), xếp theo tên.</summary>
        public List<SupplierRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE N.STATUS = @S ORDER BY N.NAME",
                    "@S", ActiveStatus);
                var list = new List<SupplierRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhà cung cấp theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public SupplierRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static SupplierRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE N.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Danh sách tra cứu nhà cung cấp dùng chung (Tên, Địa chỉ, Điện thoại).</summary>
        public List<SupplierLookupItem> LoadLookup()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, DNHOMNHACUNGCAPID " +
                    "FROM DNHACUNGCAP WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<SupplierLookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new SupplierLookupItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Code = DbHelper.Str(r["MANHACUNGCAP"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMNHACUNGCAPID"])
                    });
                }
                return list;
            });
        }

        /// <summary>Mã nhà cung cấp tự sinh kế tiếp (NCC + 5 chữ số; bảng trống → NCC00001).</summary>
        public string NextCode()
        {
            return DbHelper.Read(_databasePath, conn => NextCodeIn(conn, null));
        }

        public static string NextCodeIn(FbConnection conn, FbTransaction tx)
        {
            long max = 0;
            DataTable dt = DbHelper.Table(conn, tx, "SELECT MANHACUNGCAP FROM DNHACUNGCAP WHERE MANHACUNGCAP IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                long n;
                if (TryParseNccNumber(DbHelper.Str(r["MANHACUNGCAP"]), out n) && n > max)
                    max = n;
            }
            return SupplierText.CodePrefix + (max + 1).ToString("D5", CultureInfo.InvariantCulture);
        }

        /// <summary>Mã nhà cung cấp đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
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
            string target = SupplierText.FoldNoTrim(code);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, MANHACUNGCAP FROM DNHACUNGCAP WHERE MANHACUNGCAP IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(SupplierText.FoldNoTrim(DbHelper.Str(r["MANHACUNGCAP"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Tên nhà cung cấp đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
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
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DNHACUNGCAP WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(SupplierText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Đếm tham chiếu nhà cung cấp trên 2 bảng TDONHANG và TTHUCHI (kể cả bảng có 0 dòng).</summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new List<ReferenceBlock>();
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        public static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceTables.Length);
            foreach (string table in ReferenceTables)
            {
                int count = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM " + table + " WHERE DNHACUNGCAPID = @I",
                    "@I", id));
                blocks.Add(new ReferenceBlock
                {
                    TableName = table,
                    ColumnName = "DNHACUNGCAPID",
                    Count = count
                });
            }
            return blocks;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, SupplierRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DNHOMNHACUNGCAPID, DIACHI, DIENTHOAI, " +
                "EMAIL, WEBSITE, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @CODE, @NAME, @GRP, @ADDR, @PHONE, @EMAIL, @WEB, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@CODE", row.Code ?? "",
                "@NAME", row.Name ?? "",
                "@GRP", NullIfEmpty(row.GroupId),
                "@ADDR", NullIfEmpty(row.Address),
                "@PHONE", NullIfEmpty(row.Phone),
                "@EMAIL", NullIfEmpty(row.Email),
                "@WEB", NullIfEmpty(row.Website),
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, SupplierRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHACUNGCAP SET MANHACUNGCAP = @CODE, NAME = @NAME, DNHOMNHACUNGCAPID = @GRP, " +
                "DIACHI = @ADDR, DIENTHOAI = @PHONE, EMAIL = @EMAIL, WEBSITE = @WEB, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@CODE", row.Code ?? "",
                "@NAME", row.Name ?? "",
                "@GRP", NullIfEmpty(row.GroupId),
                "@ADDR", NullIfEmpty(row.Address),
                "@PHONE", NullIfEmpty(row.Phone),
                "@EMAIL", NullIfEmpty(row.Email),
                "@WEB", NullIfEmpty(row.Website),
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DNHACUNGCAP WHERE ID = @I", "@I", id);
        }

        private static bool TryParseNccNumber(string code, out long number)
        {
            number = 0;
            if (string.IsNullOrEmpty(code) || code.Length < 4)
                return false;
            if ((code[0] != 'N' && code[0] != 'n')
                || (code[1] != 'C' && code[1] != 'c')
                || (code[2] != 'C' && code[2] != 'c'))
                return false;
            for (int i = 3; i < code.Length; i++)
            {
                if (code[i] < '0' || code[i] > '9')
                    return false;
            }
            return long.TryParse(code.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static SupplierRow RowFrom(DataRow r)
        {
            return new SupplierRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["MANHACUNGCAP"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMNHACUNGCAPID"]),
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                Email = DbHelper.Str(r["EMAIL"]) ?? "",
                Website = DbHelper.Str(r["WEBSITE"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
