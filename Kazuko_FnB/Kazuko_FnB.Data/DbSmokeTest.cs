using System;
using System.Collections.Generic;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Kiểm tra nhanh (G0): mở DB embedded, đếm bảng người dùng và số dòng vài bảng chính.
    /// Xác nhận truy cập được Firebird 2.5, KHÔNG chạy DDL.
    /// </summary>
    public static class DbSmokeTest
    {
        public static string Run(string databasePath)
        {
            return DbHelper.Guard(() => RunCore(databasePath));
        }

        private static string RunCore(string databasePath)
        {
            var sb = new StringBuilder();
            using (var conn = KazukoConnection.Open(databasePath))
            {
                var tables = new List<string>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "select rdb$relation_name from rdb$relations " +
                        "where coalesce(rdb$system_flag,0)=0 and rdb$view_blr is null " +
                        "order by 1";
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            tables.Add(((string)r[0]).Trim());
                }

                sb.AppendLine("OK ket noi Firebird 2.5 embedded.");
                sb.AppendLine("So bang nguoi dung: " + tables.Count);

                foreach (var t in new[] { "DMATHANG", "TDONHANG", "SFORM", "SREPORT" })
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "select count(*) from \"" + t + "\"";
                            long n = Convert.ToInt64(cmd.ExecuteScalar());
                            sb.AppendLine(t + ": " + n + " dong");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine(t + ": LOI " + ex.Message);
                    }
                }
            }
            return sb.ToString();
        }
    }
}
