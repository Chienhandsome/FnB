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
        public static string BuildConnectionString(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Thiếu đường dẫn DATA.FDB", nameof(databasePath));

            var csb = new FbConnectionStringBuilder
            {
                ServerType = FbServerType.Embedded,
                UserID = "SYSDBA",
                Password = "masterkey",
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
