using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void DailyActivityLayoutAndScrollTests(string db, DailyActivityFixture fx)
        {
            var repo = new DailyActivityRepository(db);
            DateTime dbToday = repo.GetServerDate();
            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // LỖI 1: tạo 2 phiếu nhập ngày hôm nay (theo DB) để ngay khi dựng view (TRƯỚC Show) đã có khối NHẬP HÀNG.
            string todayPur1 = DbHelper.NewId();
            string todayPur2 = DbHelper.NewId();
            try
            {
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DKHONHAPID, DNHACUNGCAPID, USERCREATEDID, TONGCONG, TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, LOAITHANHTOAN) " +
                    "VALUES (@ID, 'CTHD-PN-TODAY1', 1, 0, 30, @NG, @KH, @NCC, 'selftest', 80000, 30000, 30000, 0, 0)",
                    "@ID", todayPur1, "@NG", dbToday.Date.AddHours(9), "@KH", fx.WhAId, "@NCC", fx.SuppAId);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DKHONHAPID, DNHACUNGCAPID, USERCREATEDID, TONGCONG, TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, LOAITHANHTOAN) " +
                    "VALUES (@ID, 'CTHD-PN-TODAY2', 1, 0, 30, @NG, @KH, @NCC, 'selftest', 50000, 50000, 0, 50000, 0)",
                    "@ID", todayPur2, "@NG", dbToday.Date.AddHours(10), "@KH", fx.WhAId, "@NCC", fx.SuppAId);

                using (var host = MakeOffscreenHost())
                {
                    var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                    host.Controls.Add(view);
                    DailyActivitySections body = view.BodyForTest;

                    // (a) Ngay sau khi dựng view và TRƯỚC khi host.Show(): không dựa vào getter Visible, kiểm 5 khối không đè nhau.
                    bool preShowNoOverlap = body.PurchasesGrid.Rows.Count >= 3
                        && AreSectionsNonOverlapping(body);
                    string preShowDetail = SectionBoundsSummary(body);

                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        // (b) Ẩn view, gọi LayoutContent(), hiện lại: khối NHẬP HÀNG và THU-CHI không đè nhau.
                        view.Visible = false;
                        body.LayoutContent();
                        view.Visible = true;
                        Application.DoEvents();

                        bool hideShowNoOverlap = body.PurchasesSection.Visible
                            && AreSectionsNonOverlapping(body);
                        string hideShowDetail = SectionBoundsSummary(body);

                        Check("Chi tiết HĐ ngày UI (bố cục không đè khối trước Show và sau ẩn→LayoutContent→hiện lại khi có phiếu nhập)",
                            preShowNoOverlap && hideShowNoOverlap,
                            "pre=" + preShowNoOverlap + " (" + preShowDetail + "); re=" + hideShowNoOverlap + " (" + hideShowDetail + ")");

                        // Khi không có phiếu nhập: ẩn khối NHẬP HÀNG và THU-CHI nằm ngay sau MẶT HÀNG BÁN.
                        view.FromDateBoxForTest.Inner.Text = "01/01/2001";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "02/01/2001";
                        view.ToDateBoxForTest.Commit();
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();

                        int gapWithoutPur = body.CashFlowSection.Top - body.ItemsSection.Bottom;
                        bool emptyCollapseOk = !body.PurchasesSection.Visible
                            && gapWithoutPur >= 0 && gapWithoutPur <= 40
                            && body.OverallSection.Top >= body.CashFlowSection.Bottom;
                        Check("Chi tiết HĐ ngày UI (khối NHẬP HÀNG rỗng bị ẩn và THU-CHI nằm ngay sau MẶT HÀNG BÁN)",
                            emptyCollapseOk, "gap=" + gapWithoutPur + ", purVis=" + body.PurchasesSection.Visible);

                        // LỖI 2: cuộn không tự nhảy ngược lên khi lưới giữ focus và gọi PerformLayout hoặc đổi Focus sang lưới khác.
                        view.FromDateBoxForTest.Inner.Text = "10/07/2004";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "11/07/2004";
                        view.ToDateBoxForTest.Commit();
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();

                        body.SalesGrid.Focus();
                        Application.DoEvents();
                        body.ScrollToBottom();
                        host.PerformLayout();
                        Application.DoEvents();

                        int expectedMaxY = Math.Max(0, body.PagePanel.Height - body.ScrollHost.ClientSize.Height);
                        int yAfterBottomLayout = -body.ScrollHost.AutoScrollPosition.Y;
                        bool bottomStableOk = expectedMaxY > 0 && yAfterBottomLayout == expectedMaxY;

                        int midTarget = expectedMaxY / 2;
                        body.ScrollHost.AutoScrollPosition = new Point(0, midTarget);
                        Application.DoEvents();
                        int yMidBeforeFocus = -body.ScrollHost.AutoScrollPosition.Y;
                        body.CashFlowGrid.Focus();
                        host.PerformLayout();
                        Application.DoEvents();
                        int yMidAfterFocus = -body.ScrollHost.AutoScrollPosition.Y;
                        bool midFocusStableOk = yMidBeforeFocus > 0 && yMidAfterFocus == yMidBeforeFocus;

                        Check("Chi tiết HĐ ngày UI (vùng cuộn không tự nhảy khi SalesGrid focus + PerformLayout và khi đổi Focus giữa trang)",
                            bottomStableOk && midFocusStableOk,
                            "bottom=" + yAfterBottomLayout + "/" + expectedMaxY + ", mid=" + yMidBeforeFocus + "->" + yMidAfterFocus);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B)", "@A", todayPur1, "@B", todayPur2);
            }

            // LỖI 3: preview.dailyactivity với option bottom, gọi PerformLayout + DoEvents như Program.RenderScreen.
            using (Form pvForm = Dialogs.ScreenFactory.Create("preview.dailyactivity", adminSession))
            {
                var pv = pvForm as Dialogs.DailyActivityPreviewForm;
                bool pvOk = false;
                string pvDetail = "null";
                if (pv != null)
                {
                    pv.StartPosition = FormStartPosition.Manual;
                    pv.ShowInTaskbar = false;
                    pv.Location = new Point(-20000, -20000);
                    pv.Show();
                    Application.DoEvents();
                    try
                    {
                        pv.Preview("from=10/07/2004");
                        pv.Preview("to=11/07/2004");
                        pv.Preview("bottom");
                        pv.PerformLayout();
                        Application.DoEvents();

                        DailyActivitySections pvBody = pv.ViewForTest.BodyForTest;
                        int expectedMaxY = Math.Max(0, pvBody.PagePanel.Height - pvBody.ScrollHost.ClientSize.Height);
                        int actualScrollY = -pvBody.ScrollHost.AutoScrollPosition.Y;
                        int overallBottomInView = pvBody.PagePanel.Top + pvBody.OverallSection.Bottom;
                        int viewH = pvBody.ScrollHost.ClientSize.Height;

                        pvOk = pvBody.SalesGrid.Rows.Count == 4
                            && expectedMaxY > 0
                            && actualScrollY == expectedMaxY
                            && overallBottomInView > 0
                            && overallBottomInView <= viewH;
                        pvDetail = "scroll=" + actualScrollY + "/" + expectedMaxY + ", overallBottom=" + overallBottomInView + "/" + viewH;
                    }
                    finally { pv.Close(); }
                }
                Check("Chi tiết HĐ ngày (preview.dailyactivity bottom + PerformLayout): scrollY chạm đáy và đáy khối TỔNG HỢP nằm trong khung nhìn",
                    pvOk, pvDetail);
            }
        }

        private static bool AreSectionsNonOverlapping(DailyActivitySections body)
        {
            return body.SalesSection.Bottom <= body.ItemsSection.Top
                && body.ItemsSection.Bottom <= body.PurchasesSection.Top
                && body.PurchasesSection.Bottom <= body.CashFlowSection.Top
                && body.CashFlowSection.Bottom <= body.OverallSection.Top;
        }

        private static string SectionBoundsSummary(DailyActivitySections body)
        {
            return "S:" + body.SalesSection.Top + "-" + body.SalesSection.Bottom
                + " I:" + body.ItemsSection.Top + "-" + body.ItemsSection.Bottom
                + " P:" + body.PurchasesSection.Top + "-" + body.PurchasesSection.Bottom
                + " C:" + body.CashFlowSection.Top + "-" + body.CashFlowSection.Bottom
                + " O:" + body.OverallSection.Top + "-" + body.OverallSection.Bottom;
        }
    }
}
