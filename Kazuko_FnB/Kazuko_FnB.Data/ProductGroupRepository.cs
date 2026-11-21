using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi nhóm mặt hàng (DNHOMMATHANG, spec 1.1/1.3/3.1b/3.3).
    /// Nhóm mới: STATUS = 30, SORTORDER = 'ZZZZ' (gốc) hoặc SORTORDER của nhóm cha, PARENTID/PARENTDIR '' ở gốc,
    /// AUTOID = MAX + 1, DLOAIDOID mặc định '1' (Đồ uống). Xóa nhóm bị chặn khi còn mặt hàng hoặc còn nhóm con.
    /// Mọi câu SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class ProductGroupRepository
    {
        private const int ActiveStatus = 30;

        private readonly string _databasePath;

        public ProductGroupRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mọi nhóm đang dùng (STATUS = 30), xếp theo SORTORDER rồi tên — dữ liệu cho cây nhóm.</summary>
        public List<ProductGroupRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, CODE, DLOAIDOID, SIMAGEID, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS, " +
                    "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED " +
                    "FROM DNHOMMATHANG WHERE STATUS = @S ORDER BY SORTORDER, NAME", "@S", ActiveStatus);
                var list = new List<ProductGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhóm (mọi STATUS); null nếu không có.</summary>
        public ProductGroupRow Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, CODE, DLOAIDOID, SIMAGEID, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS, " +
                    "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED " +
                    "FROM DNHOMMATHANG WHERE ID = @I", "@I", id);
                return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
            });
        }

        /// <summary>Tên nhóm đã tồn tại chưa (không phân biệt hoa/thường, cắt khoảng trắng, trùng toàn bảng)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            string norm = ProductText.NormalizeName(name);
            if (norm.Length == 0)
                return false;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null, "SELECT ID, NAME FROM DNHOMMATHANG");
                foreach (DataRow r in dt.Rows)
                {
                    if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                        continue;
                    if (string.Equals(ProductText.NormalizeName(DbHelper.Str(r["NAME"])), norm,
                        StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            });
        }

        /// <summary>Số mặt hàng đang dùng thuộc nhóm (để chặn xóa nhóm còn hàng).</summary>
        public int CountProducts(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DMATHANG WHERE DNHOMMATHANGID = @G AND STATUS = @S",
                    "@G", groupId, "@S", ActiveStatus)));
        }

        /// <summary>Nhóm có nhóm con đang dùng không (để chặn xóa nhóm còn con)?</summary>
        public bool HasChildren(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DNHOMMATHANG WHERE PARENTID = @G AND STATUS = @S",
                    "@G", groupId, "@S", ActiveStatus)) > 0);
        }

        /// <summary>
        /// Lưu nhóm (thêm/sửa) trong một transaction; kiểm trùng tên trong cùng transaction.
        /// Trả ID đã lưu; lỗi thì trả ErrorMessage (null khi lưu được).
        /// </summary>
        public string Save(ProductGroupRow row, string actorId, out string errorMessage)
        {
            errorMessage = null;
            if (row == null)
            {
                errorMessage = "Không có dữ liệu nhóm.";
                return null;
            }
            string savedId = null;
            string error = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                bool isNew = string.IsNullOrEmpty(row.Id);
                string name = ProductText.NormalizeName(row.Name);
                if (name.Length == 0)
                {
                    error = "Trường \"Tên nhóm\" không được trống!";
                    return;
                }
                row.Name = name;
                if (IsNameTakenIn(conn, tx, name, row.Id))
                {
                    error = string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm\" đã tồn tại!", name);
                    return;
                }

                if (isNew)
                {
                    row.Id = DbHelper.NewId();
                    row.Status = ActiveStatus;
                    row.CreatedById = actorId;
                    row.CreatedAt = DbHelper.Now();
                    row.ModifiedById = null;
                    row.ModifiedAt = null;
                    row.AutoId = NextAutoId(conn, tx);
                    string parentId = string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId;
                    row.ParentId = parentId;
                    if (parentId.Length == 0)
                    {
                        row.ParentDir = "";
                        row.SortOrder = "ZZZZ";
                    }
                    else
                    {
                        ProductGroupRow parent = LoadIn(conn, tx, parentId);
                        // PARENTDIR chỉ để tương thích app gốc (clone không đọc): dạng chuỗi cha "id:" —
                        // quan sát trong DEMO (SMENU): con của nút gốc có PARENTDIR = "<id cha>:".
                        row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                        row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                    }
                    if (string.IsNullOrEmpty(row.DrinkTypeId))
                        row.DrinkTypeId = "1";
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, SIMAGEID, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS, " +
                        "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                        "VALUES (@ID, @NAME, @CODE, @DLOAIDOID, @SIMAGEID, @PARENTID, @PARENTDIR, @SORTORDER, @AUTOID, @STATUS, " +
                        "@USERCREATEDID, @TIMECREATED, NULL, NULL)",
                        GroupParams(row));
                }
                else
                {
                    row.ModifiedById = actorId;
                    row.ModifiedAt = DbHelper.Now();
                    DbHelper.Exec(conn, tx,
                        "UPDATE DNHOMMATHANG SET NAME = @NAME, CODE = @CODE, DLOAIDOID = @DLOAIDOID, SIMAGEID = @SIMAGEID, " +
                        "USERMODIFIEDID = @USERMODIFIEDID, TIMEMODIFIED = @TIMEMODIFIED WHERE ID = @ID",
                        GroupParams(row));
                }
                savedId = row.Id;
            });
            errorMessage = error;
            return error == null ? savedId : null;
        }

        /// <summary>
        /// Xóa hẳn nhóm. Bị chặn khi còn mặt hàng (STATUS = 30) hoặc còn nhóm con.
        /// Trả null khi xóa được, ngược lại trả thông báo chặn (mẫu framework).
        /// </summary>
        public string Delete(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return "Không có nhóm để xóa.";
            ProductGroupRow row = Load(groupId);
            if (row == null)
                return "Nhóm không còn tồn tại.";
            int products = CountProducts(groupId);
            if (products > 0)
                return ReferenceChecker.BlockedMessage("nhóm mặt hàng", row.Name, products);
            if (HasChildren(groupId))
            {
                int children = DbHelper.Read(_databasePath, conn =>
                    DbHelper.Int(DbHelper.Scalar(conn, null,
                        "SELECT COUNT(*) FROM DNHOMMATHANG WHERE PARENTID = @G AND STATUS = @S",
                        "@G", groupId, "@S", ActiveStatus)));
                return ReferenceChecker.BlockedMessage("nhóm mặt hàng", row.Name, children);
            }
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
                DbHelper.Exec(conn, tx, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", groupId));
            return null;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DNHOMMATHANG");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(ProductText.NormalizeName(DbHelper.Str(r["NAME"])), name,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int NextAutoId(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DNHOMMATHANG");
            return v == null ? 1 : Convert.ToInt32(v) + 1;
        }

        private static ProductGroupRow LoadIn(FbConnection conn, FbTransaction tx, string id)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, CODE, DLOAIDOID, SIMAGEID, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED FROM DNHOMMATHANG WHERE ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        private static object[] GroupParams(ProductGroupRow row)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@NAME", row.Name,
                "@CODE", string.IsNullOrEmpty(row.Code) ? "" : row.Code,
                "@DLOAIDOID", string.IsNullOrEmpty(row.DrinkTypeId) ? "1" : row.DrinkTypeId,
                "@SIMAGEID", string.IsNullOrEmpty(row.ImageId) ? null : row.ImageId,
                "@PARENTID", string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId,
                "@PARENTDIR", string.IsNullOrEmpty(row.ParentDir) ? "" : row.ParentDir,
                "@SORTORDER", string.IsNullOrEmpty(row.SortOrder) ? "ZZZZ" : row.SortOrder,
                "@AUTOID", row.AutoId,
                "@STATUS", row.Status,
                "@USERCREATEDID", row.CreatedById,
                "@TIMECREATED", row.CreatedAt,
                "@USERMODIFIEDID", row.ModifiedById,
                "@TIMEMODIFIED", row.ModifiedAt
            };
        }

        private static ProductGroupRow RowFrom(DataRow r)
        {
            return new ProductGroupRow
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]),
                Code = DbHelper.Str(r["CODE"]) ?? "",
                DrinkTypeId = DbHelper.Str(r["DLOAIDOID"]) ?? "",
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                ParentDir = DbHelper.Str(r["PARENTDIR"]) ?? "",
                SortOrder = DbHelper.Str(r["SORTORDER"]) ?? "",
                AutoId = DbHelper.Int(r["AUTOID"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"])
            };
        }
    }
}
