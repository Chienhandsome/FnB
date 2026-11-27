using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>
        /// Chiến chốt 28/11: mật khẩu wifi trên hóa đơn lấy xoay vòng theo thứ tự danh mục, không xóa mật khẩu.
        /// DB demo không có mật khẩu wifi và không có hóa đơn nào có PASSWIFI nên kết quả xác định.
        /// </summary>
        private static void WifiInvoiceTests(string db)
        {
            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string[] pwIds = { DbHelper.NewId(), DbHelper.NewId(), DbHelper.NewId() };
            string[] pwNames = { "ZW1-" + tag, "ZW2-" + tag, "ZW3-" + tag };
            string[] billIds = { DbHelper.NewId(), DbHelper.NewId(), DbHelper.NewId(), DbHelper.NewId(), DbHelper.NewId() };
            DateTime now = DbHelper.Now();
            try
            {
                const string insBill = "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, TONGCONG, DATHANHTOAN, STATUS, GIOTHANHTOAN, TIMEMODIFIED, USERCREATEDID, TIMECREATED) " +
                                       "VALUES (@I, @N, @D, 0, 1000, 30, @S, @G, @G, 'selftest_wfi', @G)";
                // Danh mục trống: không gán gì, không lỗi.
                BookingExec(db, insBill, "@I", billIds[0], "@N", "HD-WFI0-" + tag, "@D", now.Date, "@S", 30, "@G", now);
                string assignedEmpty = null;
                DbHelper.InTransaction(db, (conn, tx) => { assignedEmpty = ServicePaymentRepository.AssignWifiPasswordIn(conn, tx, billIds[0]); });
                string storedEmpty = ReadBillWifi(db, billIds[0]);

                for (int i = 0; i < 3; i++)
                {
                    BookingExec(db, "INSERT INTO DMATKHAUWIFI (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_wfi', @T)",
                        "@I", pwIds[i], "@N", pwNames[i], "@T", now);
                }

                // Bốn hóa đơn thanh toán lần lượt (thời gian tăng dần): ZW1, ZW2, ZW3, rồi quay lại ZW1.
                var got = new List<string>();
                for (int k = 1; k <= 4; k++)
                {
                    string bid = k <= 3 ? billIds[k] : billIds[4];
                    BookingExec(db, insBill, "@I", bid, "@N", "HD-WFI" + k + "-" + tag, "@D", now.Date, "@S", 30, "@G", now.AddMinutes(k));
                    string assigned = null;
                    DbHelper.InTransaction(db, (conn, tx) => { assigned = ServicePaymentRepository.AssignWifiPasswordIn(conn, tx, bid); });
                    got.Add(assigned + "|" + ReadBillWifi(db, bid));
                }

                int remaining = BookingCount(db, "SELECT COUNT(*) FROM DMATKHAUWIFI WHERE ID IN (@A, @B, @C)", "@A", pwIds[0], "@B", pwIds[1], "@C", pwIds[2]);
                Check("Wifi hóa đơn (Chiến chốt 28/11): danh mục trống không gán; lấy xoay vòng ZW1, ZW2, ZW3, ZW1 theo thứ tự danh mục, ghi PASSWIFI lên hóa đơn, không xóa mật khẩu",
                    assignedEmpty == null && string.IsNullOrEmpty(storedEmpty)
                    && got.Count == 4
                    && got[0] == pwNames[0] + "|" + pwNames[0]
                    && got[1] == pwNames[1] + "|" + pwNames[1]
                    && got[2] == pwNames[2] + "|" + pwNames[2]
                    && got[3] == pwNames[0] + "|" + pwNames[0]
                    && remaining == 3,
                    SingleLine("empty=" + assignedEmpty + ", got=" + string.Join(",", got.ToArray()) + ", kept=" + remaining));
            }
            finally
            {
                foreach (string id in billIds)
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", id);
                foreach (string id in pwIds)
                    BookingExec(db, "DELETE FROM DMATKHAUWIFI WHERE ID = @I", "@I", id);
            }
        }

        private static string ReadBillWifi(string db, string billId)
        {
            return DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT PASSWIFI FROM TDONHANG WHERE ID = @I", "@I", billId)));
        }
    }
}
