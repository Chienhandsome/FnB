using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Tiện ích SQL viết tay dùng chung cho các repository ghi dữ liệu (không ORM).
    /// Tham số truyền dạng cặp: "@Ten", giaTri, "@Ten2", giaTri2, ... (null → NULL).
    /// SQL luôn tham số hóa; chỉ nằm trong lớp Data.
    /// </summary>
    public static class DbHelper
    {
        /// <summary>ID mới (GUID chuỗi chữ thường, đúng dạng ID trong DATA.FDB).</summary>
        public static string NewId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>Giờ ghi vào cột TIMECREATED/TIMEMODIFIED (giờ máy, bỏ phần nghìn giây).</summary>
        public static DateTime Now()
        {
            DateTime n = DateTime.Now;
            return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, n.Second);
        }

        public static FbCommand Command(FbConnection conn, FbTransaction tx, string sql, params object[] args)
        {
            FbCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            if (args != null)
            {
                if (args.Length % 2 != 0)
                    throw new ArgumentException("Tham số SQL phải là cặp tên - giá trị.", "args");
                for (int i = 0; i < args.Length; i += 2)
                    cmd.Parameters.AddWithValue((string)args[i], args[i + 1] ?? DBNull.Value);
            }
            return cmd;
        }

        public static int Exec(FbConnection conn, FbTransaction tx, string sql, params object[] args)
        {
            try
            {
                using (FbCommand cmd = Command(conn, tx, sql, args))
                    return cmd.ExecuteNonQuery();
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        public static object Scalar(FbConnection conn, FbTransaction tx, string sql, params object[] args)
        {
            try
            {
                using (FbCommand cmd = Command(conn, tx, sql, args))
                {
                    object v = cmd.ExecuteScalar();
                    return v is DBNull ? null : v;
                }
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        public static DataTable Table(FbConnection conn, FbTransaction tx, string sql, params object[] args)
        {
            try
            {
                var dt = new DataTable();
                using (FbCommand cmd = Command(conn, tx, sql, args))
                using (var da = new FbDataAdapter(cmd))
                    da.Fill(dt);
                return dt;
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        /// <summary>Mở kết nối, chạy <paramref name="work"/> trong một transaction; lỗi thì hoàn tác.</summary>
        public static void InTransaction(string databasePath, Action<FbConnection, FbTransaction> work)
        {
            try
            {
                using (FbConnection conn = KazukoConnection.Open(databasePath))
                using (FbTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        work(conn, tx);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        /// <summary>Mở kết nối chỉ đọc một lần cho một nhóm truy vấn.</summary>
        public static T Read<T>(string databasePath, Func<FbConnection, T> work)
        {
            try
            {
                using (FbConnection conn = KazukoConnection.Open(databasePath))
                    return work(conn);
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        /// <summary>Chạy <paramref name="work"/>, đổi FbException thành DataAccessException (cho code dùng FbCommand trực tiếp).</summary>
        public static T Guard<T>(Func<T> work)
        {
            try
            {
                return work();
            }
            catch (FbException ex)
            {
                throw DataAccessException.From(ex);
            }
        }

        public static string Str(object v)
        {
            return v == null || v is DBNull ? null : Convert.ToString(v);
        }

        public static int Int(object v)
        {
            return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
        }

        public static decimal Dec(object v)
        {
            return v == null || v is DBNull ? 0m : Convert.ToDecimal(v);
        }
    }
}
