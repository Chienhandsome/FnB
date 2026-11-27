using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    public sealed partial class ServicePaymentRepository
    {
        /// <summary>
        /// Gán mật khẩu wifi cho hóa đơn vừa thanh toán (Chiến chốt 28/11, docs/specs/chien-chot-28-11.md):
        /// lấy xoay vòng theo thứ tự danh mục (DMATKHAUWIFI.STATUS = 30, sắp theo NAME rồi ID, đúng thứ tự lưới Danh mục mật khẩu wifi),
        /// mật khẩu kế tiếp sau mật khẩu của hóa đơn đã thanh toán gần nhất (hết danh sách thì quay lại đầu), KHÔNG xóa mật khẩu.
        /// Ghi vào TDONHANG.PASSWIFI để in lên hóa đơn. Danh mục trống thì không đổi gì và trả null.
        /// </summary>
        public static string AssignWifiPasswordIn(FbConnection conn, FbTransaction tx, string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM DMATKHAUWIFI " +
                "WHERE COALESCE(STATUS, 0) = 30 AND TRIM(COALESCE(NAME, '')) <> '' " +
                "ORDER BY NAME, ID");
            if (dt.Rows.Count == 0)
                return null;

            string last = DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 PASSWIFI FROM TDONHANG " +
                "WHERE COALESCE(DATHANHTOAN, 0) = 30 AND COALESCE(STATUS, 0) <> -30 AND ID <> @B " +
                "AND PASSWIFI IS NOT NULL AND TRIM(PASSWIFI) <> '' " +
                "ORDER BY GIOTHANHTOAN DESC, TIMEMODIFIED DESC",
                "@B", billId));

            int next = 0;
            if (!string.IsNullOrEmpty(last))
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (string.Equals(DbHelper.Str(dt.Rows[i]["NAME"]), last, StringComparison.Ordinal))
                    {
                        next = (i + 1) % dt.Rows.Count;
                        break;
                    }
                }
            }

            string password = DbHelper.Str(dt.Rows[next]["NAME"]) ?? "";
            if (password.Length > 255)
                password = password.Substring(0, 255);
            DbHelper.Exec(conn, tx, "UPDATE TDONHANG SET PASSWIFI = @P WHERE ID = @B", "@P", password, "@B", billId);
            return password;
        }
    }
}
