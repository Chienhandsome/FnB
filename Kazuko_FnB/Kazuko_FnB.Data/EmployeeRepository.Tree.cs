using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Phần mở rộng dạng cây và Thùng rác của <see cref="EmployeeRepository"/>
    /// (DNHANVIEN: thư mục ITEMTYPE = 1, phân cách ITEMTYPE = 2, xóa mềm STATUS = 0; spec docs/specs/nhan-su-cay.md).
    /// </summary>
    public sealed partial class EmployeeRepository
    {
        /// <summary>Mọi thư mục đang dùng (ITEMTYPE = 1, STATUS = 30), sắp theo SORTORDER rồi NAME.</summary>
        public List<EmployeeRow> LoadActiveFolders()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql +
                    "WHERE E.STATUS = @S AND E.ITEMTYPE = @T " +
                    "ORDER BY COALESCE(E.SORTORDER, ''), E.NAME",
                    "@S", ActiveStatus, "@T", EmployeeItemType.Folder);
                var list = new List<EmployeeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Mọi dòng hiển thị trên lưới (nhân viên và phân cách, STATUS = 30), sắp theo SORTORDER rồi NAME.</summary>
        public List<EmployeeRow> LoadActiveGridItems()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql +
                    "WHERE E.STATUS = @S AND (E.ITEMTYPE IS NULL OR E.ITEMTYPE = 0 OR E.ITEMTYPE = @SEP) " +
                    "ORDER BY COALESCE(E.SORTORDER, ''), E.NAME",
                    "@S", ActiveStatus, "@SEP", EmployeeItemType.Separator);
                var list = new List<EmployeeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Mọi mục trong Thùng rác (STATUS = 0: nhân viên, thư mục, phân cách).</summary>
        public List<EmployeeRow> LoadTrashItems()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql +
                    "WHERE COALESCE(E.STATUS, 0) = @S " +
                    "ORDER BY COALESCE(E.ITEMTYPE, 0), COALESCE(E.SORTORDER, ''), E.NAME",
                    "@S", TrashStatus);
                var list = new List<EmployeeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Tên thư mục đã tồn tại trong cùng thư mục cha (ITEMTYPE = 1, STATUS = 30, UNICODE_CI_AI)?</summary>
        public static bool IsFolderNameTakenIn(
            FbConnection conn, FbTransaction tx, string name, string parentId, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string target = EmployeeText.FoldNoTrim(name);
            string targetParent = parentId ?? "";
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, PARENTID FROM DNHANVIEN " +
                "WHERE NAME IS NOT NULL AND STATUS = @S AND ITEMTYPE = @T",
                "@S", ActiveStatus, "@T", EmployeeItemType.Folder);
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string rowParent = DbHelper.Str(r["PARENTID"]) ?? "";
                if (!string.Equals(rowParent, targetParent, StringComparison.Ordinal))
                    continue;
                if (string.Equals(EmployeeText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Đếm tổng số mục đang hoạt động (STATUS = 30) bên trong một thư mục,
        /// bao gồm mọi nhân viên, phân cách và thư mục con ở tất cả các cấp.
        /// </summary>
        public static int CountActiveDescendantsIn(FbConnection conn, FbTransaction tx, string folderId)
        {
            if (string.IsNullOrEmpty(folderId))
                return 0;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, PARENTID FROM DNHANVIEN WHERE STATUS = @S",
                "@S", ActiveStatus);
            var byParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["ID"]) ?? "";
                string pid = DbHelper.Str(r["PARENTID"]) ?? "";
                if (id.Length == 0 || pid.Length == 0)
                    continue;
                List<string> kids;
                if (!byParent.TryGetValue(pid, out kids))
                {
                    kids = new List<string>();
                    byParent[pid] = kids;
                }
                kids.Add(id);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            visited.Add(folderId);
            queue.Enqueue(folderId);
            int count = 0;
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                List<string> kids;
                if (!byParent.TryGetValue(cur, out kids))
                    continue;
                foreach (string kid in kids)
                {
                    if (visited.Add(kid))
                    {
                        count++;
                        queue.Enqueue(kid);
                    }
                }
            }
            return count;
        }

        /// <summary>AUTOID tiếp theo = MAX(AUTOID) + 1 toàn bảng DNHANVIEN.</summary>
        public static int NextAutoIdIn(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DNHANVIEN");
            return v == null ? 1 : Convert.ToInt32(v, CultureInfo.InvariantCulture) + 1;
        }

        /// <summary>SORTORDER tiếp theo xếp sau anh em cùng cha (ZZZZ, ZZZa, ZZZb, ...).</summary>
        public static string NextSortOrderIn(FbConnection conn, FbTransaction tx, string parentId)
        {
            object raw = string.IsNullOrEmpty(parentId)
                ? DbHelper.Scalar(conn, tx,
                    "SELECT MAX(SORTORDER) FROM DNHANVIEN WHERE PARENTID IS NULL OR PARENTID = ''")
                : DbHelper.Scalar(conn, tx,
                    "SELECT MAX(SORTORDER) FROM DNHANVIEN WHERE PARENTID = @PID",
                    "@PID", parentId);
            return NextSortOrderFrom(DbHelper.Str(raw));
        }

        /// <summary>Tính mã SORTORDER kế tiếp theo quy ước BookingMethodRepository (ZZZZ -> ZZZa -> ZZZb ...).</summary>
        public static string NextSortOrderFrom(string max)
        {
            if (string.IsNullOrEmpty(max))
                return "ZZZZ";
            char last = max[max.Length - 1];
            string head = max.Substring(0, max.Length - 1);
            if (last == 'Z')
                return head + "a";
            if (last < 'z')
                return head + (char)(last + 1);
            return max + "a";
        }

        /// <summary>Xây PARENTDIR của mục con: &lt;PARENTDIR của cha&gt;&lt;AUTOID của cha&gt;:, rỗng nếu ở gốc.</summary>
        public static string BuildParentDir(EmployeeRow parent)
        {
            if (parent == null)
                return "";
            return (parent.ParentDir ?? "") + parent.AutoId.ToString(CultureInfo.InvariantCulture) + ":";
        }

        /// <summary>Thêm thư mục (ITEMTYPE = 1) hoặc phân cách (ITEMTYPE = 2).</summary>
        public static void InsertTreeItemIn(FbConnection conn, FbTransaction tx, EmployeeRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHANVIEN (ID, NAME, ITEMTYPE, PARENTID, PARENTDIR, SORTORDER, AUTOID, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @ITYPE, @PID, @PDIR, @SORT, @AUTO, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@ITYPE", row.ItemType.HasValue ? (object)row.ItemType.Value : null,
                "@PID", row.ParentId ?? "",
                "@PDIR", row.ParentDir ?? "",
                "@SORT", string.IsNullOrEmpty(row.SortOrder) ? "ZZZZ" : row.SortOrder,
                "@AUTO", row.AutoId,
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        /// <summary>Đổi tên thư mục (chỉ cập nhật NAME, USERMODIFIEDID, TIMEMODIFIED).</summary>
        public static int UpdateFolderNameIn(FbConnection conn, FbTransaction tx, EmployeeRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHANVIEN SET NAME = @NAME, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM " +
                "WHERE ID = @ID AND ITEMTYPE = @T",
                "@NAME", row.Name ?? "",
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id,
                "@T", EmployeeItemType.Folder);
        }

        /// <summary>Xóa mềm vào Thùng rác: chỉ đổi STATUS = 0, giữ nguyên mọi cột khác (spec §1, §3).</summary>
        public static int SoftDeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHANVIEN SET STATUS = @ST WHERE ID = @ID",
                "@ST", TrashStatus, "@ID", id);
        }

        /// <summary>Khôi phục từ Thùng rác về STATUS = 30 tại vị trí targetParentId/targetParentDir.</summary>
        public static int RestoreIn(
            FbConnection conn, FbTransaction tx, string id, string targetParentId, string targetParentDir)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHANVIEN SET STATUS = @ST, PARENTID = @PID, PARENTDIR = @PDIR WHERE ID = @ID",
                "@ST", ActiveStatus,
                "@PID", targetParentId ?? "",
                "@PDIR", targetParentDir ?? "",
                "@ID", id);
        }
    }
}
