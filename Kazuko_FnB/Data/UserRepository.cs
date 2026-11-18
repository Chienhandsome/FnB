using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    public sealed class UserRecord
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public string GroupId { get; set; }
    }

    public sealed class StoreRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Đọc người dùng / quyền / cửa hàng. Chỉ đọc, không ghi DB.
    /// SQL giữ đúng logic app gốc (No1Lib.Sys: đăng nhập theo USERNAME).
    /// </summary>
    public sealed class UserRepository
    {
        private readonly string _databasePath;

        public UserRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public List<UserRecord> FindByUserName(string userName)
        {
            var list = new List<UserRecord>();
            using (var conn = KazukoConnection.Open(_databasePath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT ID, \"PASSWORD\", ISADMIN, NAME, USERNAME, SGROUPUSERID " +
                    "FROM SUSER WHERE USERNAME = @Name";
                cmd.Parameters.Add("@Name", FbDbType.VarChar).Value = userName;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new UserRecord
                        {
                            Id = AsString(r[0]),
                            Password = AsString(r[1]),
                            IsAdmin = !(r[2] is DBNull) && Convert.ToInt32(r[2]) == 1,
                            Name = AsString(r[3]),
                            UserName = AsString(r[4]),
                            GroupId = AsString(r[5])
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Cờ quyền (SGROUPROLE.MODE) của người dùng với một chức năng (SFUNCTION.ID).
        /// Không có dòng nào = 0 (Locked), giống app gốc.
        /// </summary>
        public int GetFunctionMode(string userId, string functionId)
        {
            using (var conn = KazukoConnection.Open(_databasePath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT MAX(COALESCE(R.MODE, 0)) FROM SUSER U " +
                    "INNER JOIN SGROUPROLE R ON U.SGROUPUSERID = R.SGROUPUSERID " +
                    "WHERE U.ID = @UserId AND R.SFUNCTIONID = @FunctionId";
                cmd.Parameters.Add("@UserId", FbDbType.VarChar).Value = userId;
                cmd.Parameters.Add("@FunctionId", FbDbType.VarChar).Value = functionId;
                object v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
            }
        }

        /// <summary>Tên nhóm người dùng (SGROUPUSER.NAME), null nếu không có.</summary>
        public string GetGroupName(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;
            using (var conn = KazukoConnection.Open(_databasePath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT NAME FROM SGROUPUSER WHERE ID = @Id";
                cmd.Parameters.Add("@Id", FbDbType.VarChar).Value = groupId;
                object v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? null : v.ToString();
            }
        }

        public List<StoreRecord> GetStores(string userId)
        {
            var list = new List<StoreRecord>();
            using (var conn = KazukoConnection.Open(_databasePath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT C.ID, C.NAME FROM TNGUOIDUNGTHEOCUAHANG T " +
                    "INNER JOIN DCUAHANG C ON C.ID = T.DCUAHANGID " +
                    "WHERE T.SUSERID = @UserId ORDER BY C.NAME";
                cmd.Parameters.Add("@UserId", FbDbType.VarChar).Value = userId;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new StoreRecord { Id = AsString(r[0]), Name = AsString(r[1]) });
                }
            }
            return list;
        }

        private static string AsString(object v)
        {
            return v == null || v is DBNull ? null : v.ToString();
        }
    }
}
