using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void BusinessResultUiTests(string db, BusinessResultFixture fx)
        {
            var repo = new BusinessResultRepository(db);
            DateTime dbToday = repo.GetServerDate();
            DateTime firstOfMonth = new DateTime(dbToday.Year, dbToday.Month, 1);

            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // 1. Kiểm màn Admin thật (Show ngoài màn hình): mặc định ngày, thứ tự dòng LOAI 0..9 trên trang FastReport, thiếu ngày cảnh báo.
            using (var host = MakeOffscreenHost())
            {
                var view = new BusinessResultView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool openOk = view.FromDateBoxForTest.NullableValue == firstOfMonth
                        && view.ToDateBoxForTest.NullableValue == dbToday
                        && view.StoreComboForTest.SelectedIndex == 0
                        && view.LoadCountForTest == 1
                        && view.CurrentReportForTest != null
                        && view.CurrentReportForTest.PreparedPages != null
                        && view.CurrentReportForTest.PreparedPages.Count > 0;
                    Check("KQKD UI (§7 mở màn): Từ=01 tháng hiện tại, Đến=hôm nay theo DB, tự tải báo cáo, nhiều CH để trống", openOk);

                    // Đổi sang khoảng 01/04/2005–30/04/2005 và bấm nút Tải dữ liệu thật (PerformClick).
                    view.FromDateBoxForTest.Inner.Text = "01/04/2005";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "30/04/2005";
                    view.ToDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    Application.DoEvents();

                    // Kiểm số mục I./1..7/II./A./B./C./III., thứ tự dòng LOAI 0..9, không có Tiền giờ trực tiếp trên trang FastReport đã dựng.
                    List<KqkdPreparedRow> prepRows = ExtractKqkdPreparedRows(view.CurrentReportForTest);
                    List<KeyValuePair<string, string>> allTexts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                    int idxDt = FindKqkdRow(prepRows, "DOANH THU", 0);
                    int idxRevNg = FindKqkdRow(prepRows, "(Chưa phân nhóm)", 0);
                    int idxRevCf = FindKqkdRow(prepRows, "KQKD-01-Cà phê", 0);
                    int idxGg = FindKqkdRow(prepRows, "Giảm giá", 0);
                    int idxPdv = FindKqkdRow(prepRows, "Phí dịch vụ", 0);
                    int idxThue = FindKqkdRow(prepRows, "Thuế", 0);
                    int idxCp = FindKqkdRow(prepRows, "CHI PHÍ", 0);
                    int idxDp = FindKqkdRow(prepRows, "Định phí", 0);
                    int idxDp1 = FindKqkdRow(prepRows, fx.FixedReason1Name, idxDp + 1);
                    int idxDp2 = FindKqkdRow(prepRows, fx.FixedReason2Name, idxDp + 1);
                    int idxDp3 = FindKqkdRow(prepRows, fx.FixedReason3Name, idxDp + 1);
                    int idxBp = FindKqkdRow(prepRows, "Biến phí", 0);
                    int idxVarCf = FindKqkdRow(prepRows, "KQKD-01-Cà phê", idxBp + 1);
                    int idxCk = FindKqkdRow(prepRows, "Chi khác", 0);
                    int idxCk1 = FindKqkdRow(prepRows, fx.OtherReason1Name, idxCk + 1);
                    int idxCk2 = FindKqkdRow(prepRows, fx.OtherReason2Name, idxCk + 1);
                    int idxPl = FindKqkdRow(prepRows, "LÃI/LỖ", 0);

                    bool orderAndNumbersOk = prepRows.Count == view.LoadedRowsForTest.Count
                        && idxDt == 0 && prepRows[idxDt].ItemNumber == "I."
                        && idxRevNg == 1 && prepRows[idxRevNg].ItemNumber == "1"
                        && idxRevCf == 2 && prepRows[idxRevCf].ItemNumber == "2"
                        && idxGg == 5 && prepRows[idxGg].ItemNumber == "5"
                        && idxPdv == 6 && prepRows[idxPdv].ItemNumber == "6"
                        && idxThue == 7 && prepRows[idxThue].ItemNumber == "7"
                        && idxCp == 8 && prepRows[idxCp].ItemNumber == "II."
                        && idxDp == 9 && prepRows[idxDp].ItemNumber == "A."
                        && idxDp1 > idxDp && idxDp1 < idxBp
                        && idxDp2 > idxDp && idxDp2 < idxBp
                        && idxDp3 > idxDp && idxDp3 < idxBp
                        && idxBp > idxDp && prepRows[idxBp].ItemNumber == "B."
                        && idxVarCf > idxBp && idxVarCf < idxCk
                        && idxCk > idxBp && prepRows[idxCk].ItemNumber == "C."
                        && idxCk1 > idxCk && idxCk1 < idxPl
                        && idxCk2 > idxCk && idxCk2 < idxPl
                        && idxPl == prepRows.Count - 1 && prepRows[idxPl].ItemNumber == "III."
                        && !PageTextsContain(allTexts, "Tiền giờ");

                    Check("KQKD UI (§7/C8/Q14 số mục I./1..7/II./A./B./C./III., thứ tự dòng LOAI 0..9, không có Tiền giờ)",
                        orderAndNumbersOk,
                        "rows=" + prepRows.Count + "/" + view.LoadedRowsForTest.Count + ", plIdx=" + idxPl);

                    // Kiểm thiếu ngày → cảnh báo, KHÔNG chạy truy vấn (LoadCount không tăng, báo cáo đang hiện không đổi).
                    int countBeforeMissing = view.LoadCountForTest;
                    Report reportBeforeMissing = view.CurrentReportForTest;
                    view.FromDateBoxForTest.Inner.Text = "";
                    view.FromDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    bool missingFromOk = view.LastWarningMessageForTest == BusinessResultConstants.MissingDateMessage
                        && view.LoadCountForTest == countBeforeMissing
                        && ReferenceEquals(view.CurrentReportForTest, reportBeforeMissing);

                    view.FromDateBoxForTest.Inner.Text = "01/04/2005";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "";
                    view.ToDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    bool missingToOk = view.LastWarningMessageForTest == BusinessResultConstants.MissingDateMessage
                        && view.LoadCountForTest == countBeforeMissing
                        && ReferenceEquals(view.CurrentReportForTest, reportBeforeMissing);

                    Check("KQKD UI (§7 thiếu Từ ngày hoặc Đến ngày): cảnh báo và không chạy truy vấn",
                        missingFromOk && missingToOk);
                }
                finally { host.Close(); }
            }

            // 2. C9 chọn sẵn cửa hàng trên UI thật: 0 cửa hàng (trống), 1 cửa hàng (chọn sẵn StoreA), 2 cửa hàng (trống).
            CheckStorePreselect(db, fx.User0StoreId, "kqkd_u0", 1, 0, "");
            CheckStorePreselect(db, fx.User1StoreId, "kqkd_u1", 2, 1, fx.StoreAId);
            CheckStorePreselect(db, fx.User2StoresId, "kqkd_u2", 3, 0, "");
        }

        private static void CheckStorePreselect(
            string db,
            string userId,
            string userName,
            int expectedItemCount,
            int expectedSelectedIndex,
            string expectedStoreId)
        {
            var session = new UserSession
            {
                UserId = userId, UserName = userName, DisplayName = userName,
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host = MakeOffscreenHost())
            {
                var view = new BusinessResultView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    var selected = view.StoreComboForTest.SelectedItem as StoreRecord;
                    string actualId = selected == null ? "" : (selected.Id ?? "");
                    Check("KQKD UI (§7/C9 chọn sẵn cửa hàng user " + userName + ": items=" + expectedItemCount + ", idx=" + expectedSelectedIndex + ")",
                        view.StoreComboForTest.Items.Count == expectedItemCount
                        && view.StoreComboForTest.SelectedIndex == expectedSelectedIndex
                        && actualId == expectedStoreId,
                        "items=" + view.StoreComboForTest.Items.Count + ", idx=" + view.StoreComboForTest.SelectedIndex + ", id=" + actualId);
                }
                finally { host.Close(); }
            }
        }
    }
}
