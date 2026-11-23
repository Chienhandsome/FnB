using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Thư mục "Phương thức đặt" (DPHUONGTHUCDAT) của màn Khách đặt hàng: đọc cây, thêm / sửa / xóa thư mục.
    /// Thư mục mới: STATUS = 30, SORTORDER xếp sau thư mục cuối, PARENTID/PARENTDIR như nhóm mặt hàng
    /// (con của nút cha: PARENTDIR = "&lt;PARENTDIR cha&gt;&lt;id cha&gt;:"), AUTOID = MAX + 1.
    /// Xóa cứng (không thùng rác), bị chặn khi còn thư mục con hoặc còn phiếu đặt (TDATHANG) thuộc thư mục.
    /// </summary>
    public sealed class BookingMethodRepository
    {
        private const int ActiveStatus = 30;
        private const string Columns =
            "ID, NAME, NOTE, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS, SIMAGEID, " +
            "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED";

        public const string NameEmpty = "Trường \"Tên thư mục\" không được trống!";
        public const string Gone = "Thư mục không còn tồn tại.";

        private readonly string _databasePath;

        public BookingMethodRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên thư mục\" đã tồn tại!", name);
        }

        /// <summary>Mọi thư mục đang dùng, xếp theo SORTORDER rồi tên.</summary>
        public List<BookingMethodRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + Columns + " FROM DPHUONGTHUCDAT WHERE STATUS = @S ORDER BY SORTORDER, NAME", "@S", ActiveStatus);
                var list = new List<BookingMethodRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        public BookingMethodRow Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + Columns + " FROM DPHUONGTHUCDAT WHERE ID = @I", "@I", id);
                return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
            });
        }

        /// <summary>
        /// Lưu thư mục (thêm khi Id rỗng, sửa khi có Id) trong một transaction. Trả Id đã lưu; lỗi thì trả null
        /// và đặt <paramref name="error"/>. Khi thêm lỗi, row.Id được trả về null để lưu lại được.
        /// </summary>
        public string Save(BookingMethodRow row, string actorId, out string error)
        {
            error = null;
            if (row == null)
            {
                error = Gone;
                return null;
            }
            string err = null;
            string savedId = null;
            bool idAssigned = false;
            try
            {
                DbHelper.InTransaction(_databasePath, (conn, tx) =>
                {
                    string name = ProductText.NormalizeName(row.Name);
                    if (name.Length == 0)
                    {
                        err = NameEmpty;
                        return;
                    }
                    if (IsNameTakenIn(conn, tx, name, row.Id))
                    {
                        err = NameDuplicate(name);
                        return;
                    }
                    row.Name = name;
                    string note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim();

                    if (string.IsNullOrEmpty(row.Id))
                    {
                        string parentId = row.ParentId ?? "";
                        string parentDir = "";
                        if (parentId.Length > 0)
                        {
                            DataTable p = DbHelper.Table(conn, tx,
                                "SELECT PARENTDIR FROM DPHUONGTHUCDAT WHERE ID = @I AND STATUS = @S", "@I", parentId, "@S", ActiveStatus);
                            if (p.Rows.Count == 0)
                            {
                                err = "Thư mục cha không còn tồn tại.";
                                return;
                            }
                            parentDir = (DbHelper.Str(p.Rows[0]["PARENTDIR"]) ?? "") + parentId + ":";
                        }
                        row.Id = DbHelper.NewId();
                        idAssigned = true;
                        row.Status = ActiveStatus;
                        row.CreatedById = actorId;
                        row.CreatedAt = DbHelper.Now();
                        row.ParentId = parentId;
                        row.ParentDir = parentDir;
                        row.SortOrder = NextSortOrder(conn, tx);
                        object maxAuto = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DPHUONGTHUCDAT");
                        row.AutoId = maxAuto == null || maxAuto is DBNull ? 1 : Convert.ToInt32(maxAuto) + 1;
                        // Ảnh thư mục: dùng chung ảnh của các thư mục tạo sẵn (cùng một biểu tượng thư mục).
                        object img = DbHelper.Scalar(conn, tx,
                            "SELECT FIRST 1 SIMAGEID FROM DPHUONGTHUCDAT WHERE SIMAGEID IS NOT NULL AND SIMAGEID <> ''");
                        row.ImageId = img == null || img is DBNull ? null : Convert.ToString(img);
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DPHUONGTHUCDAT (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, SORTORDER, PARENTID, PARENTDIR, " +
                            "ITEMTYPE, AUTOID, SIMAGEID) VALUES (@ID, @NAME, @NOTE, @STATUS, @UC, @TC, @SORT, @PID, @PDIR, 0, @AUTO, @IMG)",
                            "@ID", row.Id, "@NAME", row.Name, "@NOTE", note, "@STATUS", row.Status, "@UC", row.CreatedById,
                            "@TC", row.CreatedAt, "@SORT", row.SortOrder, "@PID", row.ParentId, "@PDIR", row.ParentDir,
                            "@AUTO", row.AutoId, "@IMG", row.ImageId);
                    }
                    else
                    {
                        row.ModifiedById = actorId;
                        row.ModifiedAt = DbHelper.Now();
                        int updated = DbHelper.Exec(conn, tx,
                            "UPDATE DPHUONGTHUCDAT SET NAME = @NAME, NOTE = @NOTE, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                            "@NAME", row.Name, "@NOTE", note, "@UM", row.ModifiedById, "@TM", row.ModifiedAt, "@ID", row.Id);
                        if (updated == 0)
                        {
                            err = Gone;
                            return;
                        }
                    }
                    savedId = row.Id;
                });
            }
            catch
            {
                if (idAssigned)
                    row.Id = null;
                throw;
            }
            if (err != null && idAssigned)
                row.Id = null;
            error = err;
            return err == null ? savedId : null;
        }

        /// <summary>Xóa cứng một thư mục. Trả null khi xóa được, ngược lại trả thông báo chặn/lỗi.</summary>
        public string Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "Không có thư mục để xóa.";
            string error = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                string name = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT NAME FROM DPHUONGTHUCDAT WHERE ID = @I", "@I", id));
                if (name == null)
                {
                    error = Gone;
                    return;
                }
                int children = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM DPHUONGTHUCDAT WHERE PARENTID = @I AND STATUS = @S", "@I", id, "@S", ActiveStatus));
                int booked = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM TDATHANG WHERE DPHUONGTHUCDATID = @I AND STATUS = @S", "@I", id, "@S", ActiveStatus));
                if (children + booked > 0)
                {
                    error = ReferenceChecker.BlockedMessage("thư mục", name, children + booked);
                    return;
                }
                if (DbHelper.Exec(conn, tx, "DELETE FROM DPHUONGTHUCDAT WHERE ID = @I", "@I", id) == 0)
                    error = Gone;
            });
            return error;
        }

        private static string NextSortOrder(FbConnection conn, FbTransaction tx)
        {
            string max = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT MAX(SORTORDER) FROM DPHUONGTHUCDAT"));
            if (string.IsNullOrEmpty(max))
                return "ZZZZ";
            // Các thư mục tạo sẵn dùng ZZZZ, ZZZa, ZZZb, …: tăng ký tự cuối (Z → a), hết z thì nối thêm "a".
            char last = max[max.Length - 1];
            string head = max.Substring(0, max.Length - 1);
            if (last == 'Z')
                return head + "a";
            if (last < 'z')
                return head + (char)(last + 1);
            return max + "a";
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DPHUONGTHUCDAT WHERE STATUS = @S", "@S", ActiveStatus);
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(ProductText.NormalizeName(DbHelper.Str(r["NAME"])), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static BookingMethodRow RowFrom(DataRow r)
        {
            return new BookingMethodRow
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]),
                ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                ParentDir = DbHelper.Str(r["PARENTDIR"]) ?? "",
                SortOrder = DbHelper.Str(r["SORTORDER"]) ?? "",
                AutoId = DbHelper.Int(r["AUTOID"]),
                Status = DbHelper.Int(r["STATUS"]),
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"])
            };
        }
    }
}
