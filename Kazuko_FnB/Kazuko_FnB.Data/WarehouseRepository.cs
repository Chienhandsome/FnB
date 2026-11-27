using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục kho hàng (DKHOHANG, spec docs/specs/danh-muc-kho-hang/).
    /// Kiểm trùng Tên (UNICODE_CI_AI, không cắt khoảng trắng); đếm tham chiếu đủ 6 cột kho
    /// (DCUAHANG.DKHOHANGID, DKHUVUC.DKHOHANGID, TDONHANG.DKHONHAPID, TDONHANG.DKHOXUATID,
    /// TDONHANGCHITIET.DKHOHANGID, TLUUTAM.DKHOXUATID); xóa cứng; không DDL.
    /// Khi UPDATE giữ nguyên CHOPHEPAMKHO theo quyết định Chiến chốt 26/11 (Q2).
    /// </summary>
    public sealed class WarehouseRepository
    {
        public const int ActiveStatus = WarehouseText.ActiveStatus;

        /// <summary>6 cột tham chiếu tới DKHOHANG.ID theo nghiep-vu.md §4.</summary>
        public static readonly WarehouseReferenceColumn[] ReferenceColumns =
        {
            new WarehouseReferenceColumn("DCUAHANG", "DKHOHANGID"),
            new WarehouseReferenceColumn("DKHUVUC", "DKHOHANGID"),
            new WarehouseReferenceColumn("TDONHANG", "DKHONHAPID"),
            new WarehouseReferenceColumn("TDONHANG", "DKHOXUATID"),
            new WarehouseReferenceColumn("TDONHANGCHITIET", "DKHOHANGID"),
            new WarehouseReferenceColumn("TLUUTAM", "DKHOXUATID")
        };

        private const string SelectSql =
            "SELECT K.ID, K.NAME, K.DCUAHANGID, COALESCE(K.CHOPHEPAMKHO, 0) AS CHOPHEPAMKHO, " +
            "K.SIMAGEID, K.NOTE, COALESCE(K.STATUS, 0) AS STATUS, " +
            "K.USERCREATEDID, K.TIMECREATED, K.USERMODIFIEDID, K.TIMEMODIFIED, " +
            "COALESCE(CH.NAME, '') AS STORENAME, COALESCE(IMG.NAME, '') AS IMAGENAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DKHOHANG K " +
            "LEFT JOIN DCUAHANG CH ON CH.ID = K.DCUAHANGID " +
            "LEFT JOIN SIMAGE IMG ON IMG.ID = K.SIMAGEID " +
            "LEFT JOIN SUSER UC ON UC.ID = K.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = K.USERMODIFIEDID ";

        private readonly string _databasePath;

        public WarehouseRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mọi kho hàng đang dùng (STATUS = 30), xếp theo tên.</summary>
        public List<WarehouseRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE K.STATUS = @S ORDER BY K.NAME",
                    "@S", ActiveStatus);
                var list = new List<WarehouseRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một kho hàng theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public WarehouseRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static WarehouseRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE K.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Tên kho hàng đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
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
            string target = WarehouseText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DKHOHANG WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(WarehouseText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Kiểm tra cửa hàng có tồn tại trong DCUAHANG hay không.</summary>
        public static bool StoreExistsIn(FbConnection conn, FbTransaction tx, string storeId)
        {
            if (string.IsNullOrWhiteSpace(storeId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DCUAHANG WHERE ID = @I", "@I", storeId));
            return n > 0;
        }

        /// <summary>Số kho hàng đang dùng (STATUS = 30) để chặn xóa kho cuối cùng (Q1).</summary>
        public static int CountActiveIn(FbConnection conn, FbTransaction tx)
        {
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DKHOHANG WHERE STATUS = @S", "@S", ActiveStatus));
        }

        /// <summary>Đếm tham chiếu kho trên đủ 6 cột tham chiếu (kể cả cột có 0 dòng).</summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            if (string.IsNullOrEmpty(id))
                return new List<ReferenceBlock>();
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        public static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceColumns.Length);
            foreach (WarehouseReferenceColumn rc in ReferenceColumns)
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

        public static void InsertIn(FbConnection conn, FbTransaction tx, WarehouseRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, SIMAGEID, NOTE, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @STORE, @ALLOWNEG, @IMG, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@STORE", NullIfEmpty(row.StoreId),
                "@ALLOWNEG", row.AllowNegativeStock,
                "@IMG", NullIfEmpty(row.ImageId),
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        /// <summary>
        /// Cập nhật các cột trên hộp thoại + dấu vết sửa. Không đụng CHOPHEPAMKHO (giữ nguyên giá trị hiện có theo Q2)
        /// và không đụng các cột khung (SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID).
        /// </summary>
        public static int UpdateIn(FbConnection conn, FbTransaction tx, WarehouseRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DKHOHANG SET NAME = @NAME, DCUAHANGID = @STORE, SIMAGEID = @IMG, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@STORE", NullIfEmpty(row.StoreId),
                "@IMG", NullIfEmpty(row.ImageId),
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static WarehouseRow RowFrom(DataRow r)
        {
            return new WarehouseRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                AllowNegativeStock = DbHelper.Int(r["CHOPHEPAMKHO"]),
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                StoreName = DbHelper.Str(r["STORENAME"]) ?? "",
                ImageName = DbHelper.Str(r["IMAGENAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
