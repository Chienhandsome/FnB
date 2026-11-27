using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục cửa hàng (DCUAHANG, spec docs/specs/danh-muc-cua-hang/).
    /// Kiểm trùng Mã (UTF8: phân biệt hoa/thường, không cắt khoảng trắng) và Tên (UNICODE_CI_AI:
    /// không phân biệt hoa/thường và dấu, không cắt khoảng trắng); đếm tham chiếu 4 bảng
    /// (DKHOHANG, TDONHANG, TTHUCHI, TNGUOIDUNGTHEOCUAHANG); xóa cứng; không DDL.
    /// Khi UPDATE không đụng SIMAGEID (giữ nguyên giá trị cũ) và các cột khung.
    /// </summary>
    public sealed class StoreRepository
    {
        public const int ActiveStatus = StoreText.ActiveStatus;

        /// <summary>4 bảng tham chiếu tới DCUAHANG.ID theo nghiep-vu.md §5.</summary>
        public static readonly StoreReferenceColumn[] ReferenceColumns =
        {
            new StoreReferenceColumn("DKHOHANG", "DCUAHANGID", "kho"),
            new StoreReferenceColumn("TDONHANG", "DCUAHANGID", "hóa đơn"),
            new StoreReferenceColumn("TTHUCHI", "DCUAHANGID", "phiếu thu chi"),
            new StoreReferenceColumn("TNGUOIDUNGTHEOCUAHANG", "DCUAHANGID", "người dùng")
        };

        private const string SelectSql =
            "SELECT C.ID, C.CODE, C.NAME, C.SIMAGEID, C.DIACHI, C.DIENTHOAI, " +
            "C.DKHOHANGID, C.STEMPLATEID, C.NOTE, COALESCE(C.STATUS, 0) AS STATUS, " +
            "C.USERCREATEDID, C.TIMECREATED, C.USERMODIFIEDID, C.TIMEMODIFIED, " +
            "COALESCE(K.NAME, '') AS WAREHOUSENAME, COALESCE(T.NAME, '') AS TEMPLATENAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DCUAHANG C " +
            "LEFT JOIN DKHOHANG K ON K.ID = C.DKHOHANGID " +
            "LEFT JOIN STEMPLATE T ON T.ID = C.STEMPLATEID " +
            "LEFT JOIN SUSER UC ON UC.ID = C.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = C.USERMODIFIEDID ";

        private readonly string _databasePath;

        public StoreRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi cửa hàng đang dùng (STATUS = 30), xếp theo tên.</summary>
        public List<StoreRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE C.STATUS = @S ORDER BY C.NAME",
                    "@S", ActiveStatus);
                var list = new List<StoreRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một cửa hàng theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public StoreRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static StoreRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE C.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Danh sách kho đang dùng (STATUS = 30) cho ô chọn Kho mặc định.</summary>
        public List<LookupItem> LoadActiveWarehouses()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DKHOHANG WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        /// <summary>
        /// Danh sách mẫu hóa đơn cho ô chọn Mẫu hóa đơn (nghiep-vu.md §1):
        /// chỉ lấy mẫu đang dùng (STATUS = 30) có SFORMID = 5f029a5b-8ec2-4229-bd1d-03fed9569db2.
        /// </summary>
        public List<LookupItem> LoadInvoiceTemplates()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM STEMPLATE WHERE STATUS = @S AND SFORMID = @F ORDER BY NAME",
                    "@S", ActiveStatus, "@F", StoreText.InvoiceTemplateFormId);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        /// <summary>
        /// Nạp danh sách cửa hàng (kèm DKHOHANGID) của phiên theo nghiep-vu.md §3:
        /// - Admin: mọi cửa hàng STATUS = 30 theo SORTORDER, NAME (phần tử đầu là cửa hàng mặc định).
        /// - Người khác: cửa hàng STATUS = 30 trong TNGUOIDUNGTHEOCUAHANG (STATUS = 30) của tài khoản.
        /// </summary>
        public List<StoreRecord> LoadSessionStores(string userId, bool isAdmin)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt;
                if (isAdmin)
                {
                    dt = DbHelper.Table(conn, null,
                        "SELECT ID, NAME, DKHOHANGID FROM DCUAHANG WHERE STATUS = @S " +
                        "ORDER BY CASE WHEN SORTORDER IS NULL OR TRIM(SORTORDER) = '' THEN 1 ELSE 0 END, SORTORDER, NAME",
                        "@S", ActiveStatus);
                }
                else
                {
                    if (string.IsNullOrEmpty(userId))
                        return new List<StoreRecord>();
                    dt = DbHelper.Table(conn, null,
                        "SELECT C.ID, C.NAME, C.DKHOHANGID " +
                        "FROM TNGUOIDUNGTHEOCUAHANG T " +
                        "INNER JOIN DCUAHANG C ON C.ID = T.DCUAHANGID " +
                        "WHERE T.SUSERID = @U AND COALESCE(T.STATUS, 0) = @S AND C.STATUS = @S " +
                        "ORDER BY CASE WHEN T.TIMECREATED IS NULL THEN 1 ELSE 0 END, T.TIMECREATED, " +
                        "CASE WHEN C.SORTORDER IS NULL OR TRIM(C.SORTORDER) = '' THEN 1 ELSE 0 END, C.SORTORDER, C.NAME",
                        "@U", userId, "@S", ActiveStatus);
                }

                var list = new List<StoreRecord>(dt.Rows.Count);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]);
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                        continue;
                    list.Add(new StoreRecord
                    {
                        Id = id,
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        DefaultWarehouseId = DbHelper.Str(r["DKHOHANGID"])
                    });
                }
                return list;
            });
        }

        /// <summary>Mã cửa hàng đã tồn tại chưa (collation UTF8: phân biệt hoa/thường, không cắt khoảng trắng)?</summary>
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
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, CODE FROM DCUAHANG WHERE CODE IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(DbHelper.Str(r["CODE"]), code, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Tên cửa hàng đã tồn tại chưa (collation UNICODE_CI_AI: không phân biệt hoa/thường và dấu, không cắt khoảng trắng)?</summary>
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
            string target = StoreText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DCUAHANG WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(StoreText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool WarehouseExistsIn(FbConnection conn, FbTransaction tx, string warehouseId)
        {
            if (string.IsNullOrWhiteSpace(warehouseId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DKHOHANG WHERE ID = @I AND STATUS = @S",
                "@I", warehouseId, "@S", ActiveStatus));
            return n > 0;
        }

        public static bool InvoiceTemplateValidIn(FbConnection conn, FbTransaction tx, string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM STEMPLATE WHERE ID = @I AND STATUS = @S AND SFORMID = @F",
                "@I", templateId, "@S", ActiveStatus, "@F", StoreText.InvoiceTemplateFormId));
            return n > 0;
        }

        /// <summary>Số cửa hàng đang dùng (STATUS = 30) để chặn xóa cửa hàng cuối cùng (Q1).</summary>
        public static int CountActiveIn(FbConnection conn, FbTransaction tx)
        {
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DCUAHANG WHERE STATUS = @S", "@S", ActiveStatus));
        }

        /// <summary>Đếm tham chiếu cửa hàng trên đủ 4 bảng tham chiếu (kể cả bảng có 0 dòng).</summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new List<ReferenceBlock>();
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        public static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceColumns.Length);
            foreach (StoreReferenceColumn rc in ReferenceColumns)
            {
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

        public static void InsertIn(FbConnection conn, FbTransaction tx, StoreRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DCUAHANG (ID, CODE, NAME, DIACHI, DIENTHOAI, DKHOHANGID, STEMPLATEID, NOTE, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @CODE, @NAME, @ADDR, @PHONE, @WH, @TPL, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@CODE", row.Code ?? "",
                "@NAME", row.Name ?? "",
                "@ADDR", NullIfEmpty(row.Address),
                "@PHONE", NullIfEmpty(row.Phone),
                "@WH", NullIfEmpty(row.WarehouseId),
                "@TPL", NullIfEmpty(row.TemplateId),
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        /// <summary>
        /// Cập nhật các cột trên hộp thoại + dấu vết sửa. Không đụng SIMAGEID (giữ nguyên giá trị cũ)
        /// và không đụng các cột khung (SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID).
        /// </summary>
        public static int UpdateIn(FbConnection conn, FbTransaction tx, StoreRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DCUAHANG SET CODE = @CODE, NAME = @NAME, DIACHI = @ADDR, DIENTHOAI = @PHONE, " +
                "DKHOHANGID = @WH, STEMPLATEID = @TPL, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@CODE", row.Code ?? "",
                "@NAME", row.Name ?? "",
                "@ADDR", NullIfEmpty(row.Address),
                "@PHONE", NullIfEmpty(row.Phone),
                "@WH", NullIfEmpty(row.WarehouseId),
                "@TPL", NullIfEmpty(row.TemplateId),
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DCUAHANG WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static StoreRow RowFrom(DataRow r)
        {
            return new StoreRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                TemplateId = DbHelper.Str(r["STEMPLATEID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                WarehouseName = DbHelper.Str(r["WAREHOUSENAME"]) ?? "",
                TemplateName = DbHelper.Str(r["TEMPLATENAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
