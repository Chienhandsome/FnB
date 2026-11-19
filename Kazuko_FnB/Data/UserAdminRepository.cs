using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    /// <summary>Tài khoản trên form "Tài khoản người dùng" (chỉ các cột có trên form).</summary>
    public sealed class UserRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string GroupId { get; set; }
        public string EmployeeId { get; set; }
    }

    /// <summary>Một cửa hàng và việc người dùng có được truy cập hay không.</summary>
    public sealed class StoreAccess
    {
        public string StoreId { get; set; }
        public string StoreName { get; set; }
        public bool Access { get; set; }
    }

    /// <summary>
    /// Quản lý tài khoản (SUSER) và quyền vào cửa hàng (TNGUOIDUNGTHEOCUAHANG). Có ghi dữ liệu.
    /// Cột SUSER không nằm trên form (ISADMIN, CARDCODE, SMTP, DEFAULTFUNCID…) luôn được giữ nguyên khi sửa.
    /// Tài khoản quản trị (ISADMIN = 1) không hiện trong danh sách và không sửa/xóa được qua đây.
    /// </summary>
    public sealed class UserAdminRepository
    {
        private readonly string _databasePath;

        public UserAdminRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public List<UserRow> LoadUsers(string groupId)
        {
            var list = new List<UserRow>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                string sql = "SELECT ID, NAME, USERNAME, \"PASSWORD\", EMAIL, SGROUPUSERID, DNHANVIENID FROM SUSER " +
                             "WHERE COALESCE(ISADMIN, 0) <> 1 AND STATUS = 30";
                DataTable dt = string.IsNullOrEmpty(groupId)
                    ? DbHelper.Table(conn, null, sql + " ORDER BY USERNAME")
                    : DbHelper.Table(conn, null, sql + " AND SGROUPUSERID = @G ORDER BY USERNAME", "@G", groupId);
                foreach (DataRow r in dt.Rows)
                    list.Add(ToRow(r));
            }
            return list;
        }

        public UserRow Get(string id)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, USERNAME, \"PASSWORD\", EMAIL, SGROUPUSERID, DNHANVIENID FROM SUSER WHERE ID = @I", "@I", id);
                return dt.Rows.Count == 0 ? null : ToRow(dt.Rows[0]);
            }
        }

        private static UserRow ToRow(DataRow r)
        {
            return new UserRow
            {
                Id = DbHelper.Str(r[0]),
                Name = DbHelper.Str(r[1]) ?? "",
                UserName = DbHelper.Str(r[2]) ?? "",
                Password = DbHelper.Str(r[3]) ?? "",
                Email = DbHelper.Str(r[4]) ?? "",
                GroupId = DbHelper.Str(r[5]),
                EmployeeId = DbHelper.Str(r[6])
            };
        }

        /// <summary>Danh sách nhân viên (ID, tên) cho ô chọn nhân viên.</summary>
        public List<KeyValuePair<string, string>> LoadEmployees()
        {
            var list = new List<KeyValuePair<string, string>>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null, "SELECT ID, NAME FROM DNHANVIEN WHERE STATUS = 30 ORDER BY NAME");
                foreach (DataRow r in dt.Rows)
                    list.Add(new KeyValuePair<string, string>(DbHelper.Str(r[0]), DbHelper.Str(r[1]) ?? ""));
            }
            return list;
        }

        /// <summary>Mọi cửa hàng; người dùng mới (userId null) được tích sẵn tất cả.</summary>
        public List<StoreAccess> LoadStores(string userId)
        {
            var list = new List<StoreAccess>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT C.ID, C.NAME, (SELECT COUNT(*) FROM TNGUOIDUNGTHEOCUAHANG T WHERE T.DCUAHANGID = C.ID AND T.SUSERID = @U) " +
                    "FROM DCUAHANG C WHERE C.STATUS = 30 ORDER BY C.NAME", "@U", userId ?? "");
                foreach (DataRow r in dt.Rows)
                    list.Add(new StoreAccess
                    {
                        StoreId = DbHelper.Str(r[0]),
                        StoreName = DbHelper.Str(r[1]) ?? "",
                        Access = userId == null || DbHelper.Int(r[2]) > 0
                    });
            }
            return list;
        }

        /// <summary>Tên hoặc tài khoản đã có ở người dùng khác (không phân biệt hoa thường, bỏ khoảng trắng đầu/cuối).</summary>
        public bool IsTaken(string column, string value, string excludeId)
        {
            if (column != "NAME" && column != "USERNAME")
                throw new ArgumentException("Cột không hợp lệ", "column");
            string v = (value ?? "").Trim();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null, "SELECT ID, " + column + " FROM SUSER WHERE STATUS = 30");
                foreach (DataRow r in dt.Rows)
                {
                    if (string.Equals(DbHelper.Str(r[0]), excludeId, StringComparison.Ordinal))
                        continue;
                    if (string.Equals((DbHelper.Str(r[1]) ?? "").Trim(), v, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Thêm hoặc sửa tài khoản + ghi lại quyền cửa hàng trong MỘT transaction. Trả về ID.</summary>
        public string Save(UserRow u, IEnumerable<string> storeIds, string actorId)
        {
            bool isNew = string.IsNullOrEmpty(u.Id);
            string id = isNew ? DbHelper.NewId() : u.Id;
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DateTime now = DbHelper.Now();
                if (isNew)
                {
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO SUSER (ID, NAME, USERNAME, \"PASSWORD\", EMAIL, SGROUPUSERID, DNHANVIENID, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @UN, @P, @E, @G, @NV, 30, @A, @T)",
                        "@I", id, "@N", u.Name.Trim(), "@UN", u.UserName.Trim(), "@P", u.Password ?? "", "@E", u.Email ?? "",
                        "@G", u.GroupId, "@NV", EmptyToNull(u.EmployeeId), "@A", actorId, "@T", now);
                }
                else
                {
                    int n = DbHelper.Exec(conn, tx,
                        "UPDATE SUSER SET NAME = @N, USERNAME = @UN, \"PASSWORD\" = @P, EMAIL = @E, SGROUPUSERID = @G, DNHANVIENID = @NV, " +
                        "USERMODIFIEDID = @A, TIMEMODIFIED = @T WHERE ID = @I AND COALESCE(ISADMIN, 0) <> 1",
                        "@N", u.Name.Trim(), "@UN", u.UserName.Trim(), "@P", u.Password ?? "", "@E", u.Email ?? "",
                        "@G", u.GroupId, "@NV", EmptyToNull(u.EmployeeId), "@A", actorId, "@T", now, "@I", id);
                    if (n == 0)
                        throw new InvalidOperationException("Không tìm thấy tài khoản để sửa.");
                }

                // Quyền vào cửa hàng: xóa hết rồi ghi lại các cửa hàng được tích (đúng cách app gốc làm).
                DbHelper.Exec(conn, tx, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @U", "@U", id);
                foreach (string storeId in storeIds)
                {
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, STATUS, USERCREATEDID, TIMECREATED, SUSERID, DCUAHANGID) VALUES (@I, 30, @A, @T, @U, @C)",
                        "@I", DbHelper.NewId(), "@A", actorId, "@T", now, "@U", id, "@C", storeId);
                }
            });
            return id;
        }

        /// <summary>Số dữ liệu khác còn trỏ tới tài khoản này (hóa đơn đã thanh toán). Không tính quyền cửa hàng/khu vực.</summary>
        public int CountReferences(string userId)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
                return ReferenceChecker.Count(conn, null, "USERTHANHTOANID", userId);
        }

        /// <summary>Xóa tài khoản (kèm quyền cửa hàng/khu vực của nó). Người gọi kiểm tra CountReferences trước.</summary>
        public void Delete(string userId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @U", "@U", userId);
                DbHelper.Exec(conn, tx, "DELETE FROM DTKTHEOKHUVUC WHERE SUSERID = @U", "@U", userId);
                DbHelper.Exec(conn, tx, "DELETE FROM SUSER WHERE ID = @U AND COALESCE(ISADMIN, 0) <> 1", "@U", userId);
            });
        }

        /// <summary>Mật khẩu hiện tại (chữ thường) của tài khoản; null nếu không có.</summary>
        public string GetPassword(string userId)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
                return DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT \"PASSWORD\" FROM SUSER WHERE ID = @I", "@I", userId));
        }

        public void SetPassword(string userId, string newPassword, string actorId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE SUSER SET \"PASSWORD\" = @P, USERMODIFIEDID = @A, TIMEMODIFIED = @T WHERE ID = @I",
                    "@P", newPassword ?? "", "@A", actorId, "@T", DbHelper.Now(), "@I", userId);
            });
        }

        private static string EmptyToNull(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
