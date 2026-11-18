using System;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Tạo kết nối tới Firebird 2.5 embedded (fbembed.dll nằm cạnh app).
    /// Chỉ đọc/ghi dữ liệu, không đổi schema.
    /// </summary>
    public static class KazukoConnection
    {
        private static string _userId = "SYSDBA";
        private static string _password = "masterkey";

        /// <summary>
        /// Tài khoản Firebird dùng cho mọi kết nối (đặt từ tab "Cơ sở dữ liệu").
        /// Lưu ý: Firebird embedded bỏ qua tài khoản/mật khẩu; chúng có tác dụng khi dùng máy chủ.
        /// </summary>
        public static void Configure(string userId, string password)
        {
            _userId = string.IsNullOrWhiteSpace(userId) ? "SYSDBA" : userId.Trim();
            _password = password ?? "";
        }

        public static string BuildConnectionString(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Thiếu đường dẫn DATA.FDB", nameof(databasePath));

            var csb = new FbConnectionStringBuilder
            {
                ServerType = FbServerType.Embedded,
                UserID = _userId,
                Password = _password,
                Database = databasePath,
                Charset = "UTF8",
                Pooling = false
            };
            return csb.ToString();
        }

        public static FbConnection Open(string databasePath)
        {
            var conn = new FbConnection(BuildConnectionString(databasePath));
            conn.Open();
            return conn;
        }
    }
}
