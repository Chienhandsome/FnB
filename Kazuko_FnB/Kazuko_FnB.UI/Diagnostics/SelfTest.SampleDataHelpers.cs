using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void CheckThuNganDeleteBlocked(string db, GroupRepository groups, UserAdminRepository users, GroupRow thuNgan)
        {
            int existingUsers = DbHelper.Read(db, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM SUSER WHERE SGROUPUSERID = @G", "@G", thuNgan.Id)));
            string tag = DbHelper.NewId().Substring(0, 8);
            string probeUserId = null;
            try
            {
                probeUserId = users.Save(new UserRow
                {
                    Name = "THU NGAN THU " + tag,
                    UserName = "thungan.thu." + tag,
                    Password = "p1",
                    Email = "",
                    GroupId = thuNgan.Id
                }, new string[0], "selftest");
                int expectedRefs = existingUsers + 1;
                int actualRefs = groups.CountReferences(thuNgan.Id);
                Check("Không xóa được nhóm còn tài khoản: đếm đúng số tài khoản như app gốc",
                    actualRefs == expectedRefs,
                    "actual=" + actualRefs + ", expected=" + expectedRefs);
                string expectedMsg = "Bạn không thể xóa nhóm người dùng 'Thu ngân' vì còn (" + expectedRefs + ") dữ liệu khác liên kết tới 'Thu ngân'";
                string actualMsg = ReferenceChecker.BlockedMessage("nhóm người dùng", "Thu ngân", actualRefs);
                Check("Thông báo xóa nhóm đúng chữ gốc", actualMsg == expectedMsg, actualMsg);
            }
            finally
            {
                if (probeUserId != null)
                {
                    try { users.Delete(probeUserId); } catch { }
                }
            }
        }

        private static void CheckBookingMethodLoadAll(string db, BookingMethodRepository methods)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string activeId = null;
            string inactiveId = null;
            try
            {
                string err;
                activeId = methods.Save(new BookingMethodRow { Name = "PTD DANG DUNG " + tag }, "selftest", out err);
                inactiveId = InsertInactiveBookingMethod(db, "PTD KHONG DUNG " + tag);

                List<string> expectedIds = DbHelper.Read(db, conn =>
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT ID FROM DPHUONGTHUCDAT WHERE STATUS = 30 ORDER BY SORTORDER, NAME");
                    var ids = new List<string>(dt.Rows.Count);
                    foreach (DataRow r in dt.Rows)
                        ids.Add(DbHelper.Str(r[0]));
                    return ids;
                });

                List<BookingMethodRow> loaded = methods.LoadAll();
                bool exactMatch = loaded.Count == expectedIds.Count;
                if (exactMatch)
                {
                    for (int i = 0; i < loaded.Count; i++)
                    {
                        if (!string.Equals(loaded[i].Id, expectedIds[i], StringComparison.Ordinal))
                        {
                            exactMatch = false;
                            break;
                        }
                    }
                }
                bool hasActive = activeId != null && loaded.Exists(r => r.Id == activeId);
                bool excludesInactive = inactiveId != null && !loaded.Exists(r => r.Id == inactiveId);
                Check("Phương thức đặt: LoadAll trả đúng tập thư mục STATUS = 30 (gồm dòng đang dùng, loại dòng STATUS = 0)",
                    exactMatch && hasActive && excludesInactive,
                    "loaded=" + loaded.Count + ", expected=" + expectedIds.Count +
                    ", hasActive=" + hasActive + ", excludesInactive=" + excludesInactive);
            }
            finally
            {
                if (activeId != null)
                {
                    try { BookingExec(db, "DELETE FROM DPHUONGTHUCDAT WHERE ID = @I", "@I", activeId); } catch { }
                }
                if (inactiveId != null)
                {
                    try { BookingExec(db, "DELETE FROM DPHUONGTHUCDAT WHERE ID = @I", "@I", inactiveId); } catch { }
                }
            }
        }

        private static string InsertInactiveBookingMethod(string db, string name)
        {
            string id = DbHelper.NewId();
            DbHelper.InTransaction(db, (conn, tx) =>
            {
                object maxAuto = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DPHUONGTHUCDAT");
                int autoId = maxAuto == null || maxAuto is DBNull ? 1 : Convert.ToInt32(maxAuto) + 1;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DPHUONGTHUCDAT (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID) " +
                    "VALUES (@ID, @NAME, @NOTE, 0, 'selftest', @TC, 'ZZZ0', '', '', 0, @AUTO, @IMG)",
                    "@ID", id, "@NAME", name, "@NOTE", null, "@TC", DbHelper.Now(), "@AUTO", autoId, "@IMG", null);
            });
            return id;
        }

        private static int CountActiveRoomsSql(string db)
        {
            return DbHelper.Read(db, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DBAN WHERE STATUS = 30")));
        }

        private static void CheckCalendarTotalsOnDb(BookingCalendarRules.CalendarTotals lt, int existingRooms)
        {
            int expectedTotalRooms = existingRooms + 4;
            int[] baseRemaining = { 3, 2, 1, 3, 4, 4, 4 };
            bool remainingOk = lt.RemainingPerDay != null && lt.RemainingPerDay.Length == 7;
            if (remainingOk)
            {
                for (int d = 0; d < 7; d++)
                {
                    if (lt.RemainingPerDay[d] != baseRemaining[d] + existingRooms)
                    {
                        remainingOk = false;
                        break;
                    }
                }
            }
            string actualRem = lt.RemainingPerDay == null
                ? "null"
                : string.Join(",", Array.ConvertAll(lt.RemainingPerDay, i => i.ToString()));
            string expectedRem = string.Join(",", Array.ConvertAll(baseRemaining, i => (i + existingRooms).ToString()));
            Check("Lịch tổng trên DB: Còn lại [3,2,1,3,4,4,4] + P đủ 7 ngày",
                remainingOk,
                "actual=[" + actualRem + "], expected=[" + expectedRem + "] (P=" + existingRooms + ")");

            double expectedCapDay2 = Math.Round(3 * 100.0 / expectedTotalRooms, 1, MidpointRounding.AwayFromZero);
            Check("Lịch tổng trên DB: Công suất ngày 2 = 3 / (P + 4)",
                lt.CapacityPerDay[2] == expectedCapDay2,
                "actual=" + lt.CapacityPerDay[2] + ", expected=" + expectedCapDay2 + " (P=" + existingRooms + ")");
        }
    }
}
