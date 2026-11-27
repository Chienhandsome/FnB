using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void OrderControlRepoAndUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string userA = DbHelper.NewId();
            string userNoStore = DbHelper.NewId();
            string linkA = DbHelper.NewId();
            var billIds = new List<string>();
            string xlsUsedPath = Path.Combine(Path.GetTempPath(), "kazuko-kso-used-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsIssuePath = Path.Combine(Path.GetTempPath(), "kazuko-kso-iss-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;

            DateTime from = new DateTime(2005, 6, 10);
            DateTime to = new DateTime(2005, 6, 11);
            DateTime hugeDay = new DateTime(2005, 7, 15);

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kso')", "@I", storeA, "@N", "KSO-CH-A-" + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kso')", "@I", storeB, "@N", "KSO-CH-B-" + tag);
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest_kso')",
                    "@I", userA, "@N", "KSO User A", "@U", "kso_ua_" + tag);
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest_kso')",
                    "@I", userNoStore, "@N", "KSO User None", "@U", "kso_un_" + tag);
                BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest_kso')",
                    "@I", linkA, "@U", userA, "@C", storeA);

                Action<string, int, int, int, DateTime, string, string> seedBill =
                    delegate (string name, int loai, int status, int paid, DateTime ngay, string storeId, string soOrder)
                    {
                        string id = DbHelper.NewId();
                        billIds.Add(id);
                        BookingExec(db,
                            "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DCUAHANGID, SOORDER, USERCREATEDID) " +
                            "VALUES (@I, @N, @L, @S, @P, @D, @C, @O, 'selftest_kso')",
                            "@I", id, "@N", name, "@L", loai, "@S", status, "@P", paid, "@D", ngay, "@C", storeId, "@O", soOrder);
                    };

                // Trong khoảng [10/06/2005, 11/06/2005]:
                // Store A: bill ngày đầu (đã TT, "1,2") + bill ngày cuối 23:30 (chưa TT, "2,4,bad") -> dùng 1,2,4; trùng 2; thiếu 3; viết sai 'bad'.
                // Store B: bill ngày 10/06 ("6") -> Admin thấy thêm 6 (thiếu 5); User A KHÔNG thấy 6.
                seedBill("KSO-A1", 0, 30, 30, new DateTime(2005, 6, 10, 8, 0, 0), storeA, "1,2");
                seedBill("KSO-A2", 0, 30, 0, new DateTime(2005, 6, 11, 23, 30, 0), storeA, "2, 4 ,bad");
                seedBill("KSO-B1", 0, 30, 30, new DateTime(2005, 6, 10, 12, 0, 0), storeB, "6");

                // Các bill BỊ LOẠI (ngoài khoảng, STATUS<>30, LOAI<>0, SOORDER rỗng/NULL).
                seedBill("KSO-BEFORE", 0, 30, 30, new DateTime(2005, 6, 9, 23, 59, 0), storeA, "90");
                seedBill("KSO-AFTER", 0, 30, 30, new DateTime(2005, 6, 12, 0, 0, 0), storeA, "91");
                seedBill("KSO-CANCEL", 0, -30, 30, new DateTime(2005, 6, 10, 10, 0, 0), storeA, "92");
                seedBill("KSO-ST0", 0, 0, 30, new DateTime(2005, 6, 10, 10, 10, 0), storeA, "93");
                seedBill("KSO-IMP", 1, 30, 30, new DateTime(2005, 6, 10, 10, 20, 0), storeA, "94");
                seedBill("KSO-EMPTY", 0, 30, 30, new DateTime(2005, 6, 10, 10, 30, 0), storeA, "   ");
                seedBill("KSO-NULL", 0, 30, 30, new DateTime(2005, 6, 10, 10, 40, 0), storeA, null);

                // Ngày riêng 15/07/2005 để kiểm định dạng số >= 1.000 không phụ thuộc culture en-US.
                seedBill("KSO-H1", 0, 30, 30, new DateTime(2005, 7, 15, 9, 0, 0), storeA, "1");
                seedBill("KSO-H2", 0, 30, 30, new DateTime(2005, 7, 15, 10, 0, 0), storeA, "1502");

                // ---------------------------------------------------------- Kiểm Repo & Service (spec §3 #5, #6)
                var adminSess = new UserSession
                {
                    UserId = "selftest_kso", UserName = "selftest_kso", DisplayName = "Admin KSO",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };
                var permsView = new FunctionPermissions(false);
                permsView.Add(OrderControlConstants.FunctionId, OrderControlConstants.FunctionName, RoleModes.View);
                var userASess = new UserSession
                {
                    UserId = userA, UserName = "kso_ua_" + tag, DisplayName = "User A",
                    IsAdmin = false, DatabasePath = db, Permissions = permsView
                };
                var userNoneSess = new UserSession
                {
                    UserId = userNoStore, UserName = "kso_un_" + tag, DisplayName = "User None",
                    IsAdmin = false, DatabasePath = db, Permissions = permsView
                };

                string errAdm, errUa, errUn, errBadDate, errEmpty;
                OrderControlAnalysisResult resAdm = new OrderControlService(adminSess).LoadAnalysis(from, to, out errAdm);
                OrderControlAnalysisResult resUa = new OrderControlService(userASess).LoadAnalysis(from, to, out errUa);
                OrderControlAnalysisResult resUn = new OrderControlService(userNoneSess).LoadAnalysis(from, to, out errUn);
                OrderControlAnalysisResult resBadDate = new OrderControlService(adminSess).LoadAnalysis(to, from, out errBadDate);
                OrderControlAnalysisResult resEmpty = new OrderControlService(adminSess).LoadAnalysis(new DateTime(2005, 1, 1), new DateTime(2005, 1, 2), out errEmpty);

                bool admOk = errAdm == null
                    && resAdm.UsedRows.Count == 4
                    && resAdm.UsedRows[0].OrderNumber == 1 && resAdm.UsedRows[1].OrderNumber == 2
                    && resAdm.UsedRows[2].OrderNumber == 4 && resAdm.UsedRows[3].OrderNumber == 6
                    && resAdm.Summary.MinOrder == 1 && resAdm.Summary.MaxOrder == 6
                    && resAdm.Summary.UsedCount == 4 && resAdm.Summary.DuplicateCount == 1
                    && resAdm.Summary.InvalidCount == 1 && resAdm.Summary.MissingCount == 2L
                    && !resAdm.UsedRows.Exists(u => u.OrderNumber >= 90);

                bool userAOk = errUa == null
                    && resUa.UsedRows.Count == 3
                    && resUa.UsedRows[0].OrderNumber == 1 && resUa.UsedRows[1].OrderNumber == 2 && resUa.UsedRows[2].OrderNumber == 4
                    && !resUa.UsedRows.Exists(u => u.OrderNumber == 6)
                    && resUa.Summary.MinOrder == 1 && resUa.Summary.MaxOrder == 4
                    && resUa.Summary.MissingCount == 1L && resUa.Summary.DuplicateCount == 1 && resUa.Summary.InvalidCount == 1;

                bool userNoneOk = errUn == null && !resUn.HasAnyData && resUn.UsedRows.Count == 0 && resUn.IssueRows.Count == 0;
                bool dateRangeBlockOk = errBadDate == OrderControlConstants.InvalidDateRangeMessage && !resBadDate.HasAnyData;
                bool emptyRangeOk = errEmpty == null && !resEmpty.HasAnyData;

                Check("Kiểm soát order §3 #5 & #6 (Repo/Service lọc biên đóng, gồm chưa TT, loại hủy/LOAI!=0/rỗng, giới hạn cửa hàng non-admin, chặn Từ>Đến): đạt",
                    admOk && userAOk && userNoneOk && dateRangeBlockOk && emptyRangeOk,
                    OcSingleLine("adm=" + admOk + ", ua=" + userAOk + ", un=" + userNoneOk
                        + ", badDate=" + dateRangeBlockOk + ", empty=" + emptyRangeOk));

                // ---------------------------------------------------------- Kiểm UI thật mở qua menu & ScreenFactory (spec §3 #6, #8)
                int billCountBefore = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG");
                int logCountBefore = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET");

                MenuNode realMenuNode = OcFindMenuLeaf(MainMenuBuilder.Build(adminSess).Roots, n => ScreenFactory.IsOrderControlMenu(n));
                Control createdView = realMenuNode != null ? ScreenFactory.CreateView(realMenuNode, adminSess) : null;
                var view = createdView as OrderControlView;

                if (view == null)
                {
                    if (createdView != null) createdView.Dispose();
                    Check("Kiểm soát order §3 #6 & #8 (UI mở qua menu thật): dựng được OrderControlView", false, "node=" + (realMenuNode != null));
                    return;
                }

                ThemeTokens t0 = BuiltInThemes.Orange;
                ThemeTokens t1 = BuiltInThemes.Ocean;
                ThemeManager.Apply(t0);

                using (var host = new Form
                {
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    Location = new Point(-20000, -20000),
                    ClientSize = new Size(1366, 768)
                })
                {
                    view.Dock = DockStyle.Fill;
                    view.SuppressPopup = true;
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        DateTime dbToday = new OrderControlRepository(db).GetServerDate();
                        bool defaultTodayOk = view.FromDateBoxForTest.Value == dbToday && view.ToDateBoxForTest.Value == dbToday;

                        // Chặn khi Từ ngày > Đến ngày.
                        int loadsBeforeInvalid = view.LoadCountForTest;
                        view.FromDateBoxForTest.Value = to;
                        view.ToDateBoxForTest.Value = from;
                        view.ReloadData();
                        bool uiBlockBadDateOk = view.LastWarningMessageForTest == OrderControlConstants.InvalidDateRangeMessage
                            && view.LoadCountForTest == loadsBeforeInvalid;

                        // Khoảng trống -> hiện thông báo "Không có order nào trong khoảng ngày".
                        view.FromDateBoxForTest.Value = new DateTime(2005, 1, 1);
                        view.ToDateBoxForTest.Value = new DateTime(2005, 1, 2);
                        view.ReloadData();
                        bool uiEmptyOk = view.UsedGridForTest.Rows.Count == 0
                            && view.IssueGridForTest.Rows.Count == 0
                            && view.EmptyBannerForTest.Visible
                            && view.EmptyBannerLabelForTest.Text == OrderControlConstants.EmptyDataMessage
                            && view.SummaryValueTextForTest("max") == "—"
                            && view.SummaryValueTextForTest("min") == "—"
                            && view.SummaryValueTextForTest("used") == "0";

                        // Khoảng có đủ dữ liệu [10/06/2005, 11/06/2005] -> gọi thẳng ReloadData + kiểm nút Xem & F5.
                        view.FromDateBoxForTest.Value = from;
                        view.ToDateBoxForTest.Value = to;
                        view.ReloadData();
                        int loadsAfterReload = view.LoadCountForTest;
                        view.ViewButtonForTest.PerformClick();
                        bool btnClickReloaded = view.LoadCountForTest == loadsAfterReload + 1;
                        bool f5Handled = view.DispatchCmdKeyForTest(Keys.F5) && view.LoadCountForTest == loadsAfterReload + 2;

                        bool gridsDataOk = !view.EmptyBannerForTest.Visible
                            && view.UsedGridForTest.Rows.Count == 4
                            && Convert.ToInt32(view.UsedGridForTest.Rows[0].Cells["colOrderNumber"].Value) == 1
                            && Convert.ToString(view.UsedGridForTest.Rows[1].Cells["colBillNumber"].Value) == "KSO-A1 (trùng)"
                            && view.IssueGridForTest.Rows.Count == 4
                            && Convert.ToString(view.IssueGridForTest.Rows[0].Cells["colIssueType"].Value) == OrderControlConstants.IssueTypeInvalid
                            && Convert.ToString(view.IssueGridForTest.Rows[0].Cells["colIssueMessage"].Value) == "Lỗi số order 'bad' ở bill 'KSO-A2'"
                            && Convert.ToString(view.IssueGridForTest.Rows[1].Cells["colIssueType"].Value) == OrderControlConstants.IssueTypeDuplicate
                            && Convert.ToString(view.IssueGridForTest.Rows[1].Cells["colIssueMessage"].Value) == "Trùng số order 2 (bill 'KSO-A1', 'KSO-A2')"
                            && Convert.ToString(view.IssueGridForTest.Rows[2].Cells["colIssueMessage"].Value) == "Thiếu order 3"
                            && Convert.ToString(view.IssueGridForTest.Rows[3].Cells["colIssueMessage"].Value) == "Thiếu order 5";

                        bool summaryOk = view.SummaryValueTextForTest("max") == "6"
                            && view.SummaryValueTextForTest("min") == "1"
                            && view.SummaryValueTextForTest("used") == "4"
                            && view.SummaryValueTextForTest("dup") == "1"
                            && view.SummaryValueTextForTest("invalid") == "1"
                            && view.SummaryValueTextForTest("missing") == "2";

                        // Kiểm màu THẬT của ô trên lưới theo ThemeTokens + đổi bảng màu trực tiếp.
                        DataGridView ug = view.UsedGridForTest;
                        DataGridView ig = view.IssueGridForTest;
                        bool colorsT0Ok = ug.Rows[1].Cells[0].InheritedStyle.BackColor == OrderControlView.UsedDuplicateBackColor(t0)
                            && ug.Rows[1].Cells[0].InheritedStyle.ForeColor == OrderControlView.UsedDuplicateForeColor(t0)
                            && ig.Rows[0].Cells[0].InheritedStyle.BackColor == OrderControlView.IssueRowBackColor(OrderControlIssueKind.Invalid, t0)
                            && ig.Rows[0].Cells[0].InheritedStyle.ForeColor == OrderControlView.IssueRowForeColor(OrderControlIssueKind.Invalid, t0)
                            && ig.Rows[1].Cells[0].InheritedStyle.ForeColor == OrderControlView.IssueRowForeColor(OrderControlIssueKind.Duplicate, t0)
                            && ig.Rows[2].Cells[0].InheritedStyle.ForeColor == OrderControlView.IssueRowForeColor(OrderControlIssueKind.Missing, t0);

                        ThemeManager.Apply(t1);
                        Application.DoEvents();
                        bool colorsT1Ok = ug.BackgroundColor == t1.Surface && ig.BackgroundColor == t1.Surface
                            && ug.Rows[1].Cells[0].InheritedStyle.BackColor == OrderControlView.UsedDuplicateBackColor(t1)
                            && ig.Rows[0].Cells[0].InheritedStyle.BackColor == OrderControlView.IssueRowBackColor(OrderControlIssueKind.Invalid, t1)
                            && ig.Rows[1].Cells[0].InheritedStyle.ForeColor == OrderControlView.IssueRowForeColor(OrderControlIssueKind.Duplicate, t1)
                            && ig.Rows[2].Cells[0].InheritedStyle.ForeColor == OrderControlView.IssueRowForeColor(OrderControlIssueKind.Missing, t1);

                        // Kiểm bố cục 1366x768: không cuộn dọc thanh nút/bộ lọc, mọi tiêu đề cột đủ rộng không cắt chữ.
                        bool topBarSingleRowOk = !view.VerticalScroll.Visible && !view.AutoScroll
                            && !view.TopBarForTest.VerticalScroll.Visible && !view.TopBarForTest.AutoScroll
                            && view.TopBarForTest.Height <= (int)Math.Ceiling(56 * (view.DeviceDpi / 96f));
                        bool usedColsNoClip = OcCheckHeadersNoClip(ug, view.HeadingFontForTest, new[] { "Số order", "Số bill", "Ngày" });
                        bool issueColsNoClip = OcCheckHeadersNoClip(ig, view.HeadingFontForTest, new[] { "Loại", "Nội dung", "Ngày/Bill" });

                        // Xuất Excel cả hai lưới qua nút thật (đúng cột đang hiện).
                        var recUsed = new GridExportOutputRecorder { NextSavePath = xlsUsedPath, ConfirmOpenResult = false };
                        view.GridExportOutputForTest = recUsed;
                        view.ExportUsedButtonForTest.PerformClick();
                        Application.DoEvents();
                        XlsSheetSnapshot snapUsed = File.Exists(xlsUsedPath) ? ReadFirstXlsSheet(xlsUsedPath) : null;
                        bool xlsUsedOk = snapUsed != null && recUsed.PromptSaveCount == 1
                            && snapUsed.ColCount == 3 && snapUsed.RowCount == 5
                            && HeadersMatch(snapUsed, VisibleGridHeaders(ug))
                            && XlsSheetContainsSubstring(snapUsed, "KSO-A1 (trùng)");

                        var recIss = new GridExportOutputRecorder { NextSavePath = xlsIssuePath, ConfirmOpenResult = false };
                        view.GridExportOutputForTest = recIss;
                        view.ExportIssueButtonForTest.PerformClick();
                        Application.DoEvents();
                        XlsSheetSnapshot snapIss = File.Exists(xlsIssuePath) ? ReadFirstXlsSheet(xlsIssuePath) : null;
                        bool xlsIssOk = snapIss != null && recIss.PromptSaveCount == 1
                            && snapIss.ColCount == 3 && snapIss.RowCount == 5
                            && HeadersMatch(snapIss, VisibleGridHeaders(ig))
                            && XlsSheetContainsSubstring(snapIss, "Thiếu order 3")
                            && XlsSheetContainsSubstring(snapIss, "Lỗi số order 'bad' ở bill 'KSO-A2'");

                        // Kiểm không phụ thuộc culture máy (ép en-US tạm thời): số thiếu 1.500 hiện "1.500", số order 1502 hiện "1502", ngày "15/07/2005".
                        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                        view.FromDateBoxForTest.Value = hugeDay;
                        view.ToDateBoxForTest.Value = hugeDay;
                        view.ReloadData();
                        string enUsMissingSummary = view.SummaryValueTextForTest("missing");
                        string enUsOrderCell = ug.Rows.Count >= 2 ? Convert.ToString(ug.Rows[1].Cells["colOrderNumber"].FormattedValue) : "";
                        string enUsDateCell = ug.Rows.Count >= 2 ? Convert.ToString(ug.Rows[1].Cells["colBillDate"].FormattedValue) : "";
                        Thread.CurrentThread.CurrentCulture = savedCulture;
                        bool cultureOk = enUsMissingSummary == "1.500" && enUsOrderCell == "1502" && enUsDateCell == "15/07/2005";

                        // Kiểm chỉ đọc: không có nút Lưu/Thêm/Sửa/Xóa; số dòng TDONHANG & TLUUVET không đổi.
                        var allBtns = new List<Control>();
                        CollectAllButtons(view, allBtns);
                        bool noWriteButtons = true;
                        for (int i = 0; i < allBtns.Count; i++)
                        {
                            string txt = (allBtns[i].Text ?? "").Trim();
                            if (txt.IndexOf("Lưu", StringComparison.OrdinalIgnoreCase) >= 0
                                || txt.IndexOf("Thêm", StringComparison.OrdinalIgnoreCase) >= 0
                                || txt.IndexOf("Sửa", StringComparison.OrdinalIgnoreCase) >= 0
                                || txt.IndexOf("Xóa", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                noWriteButtons = false;
                            }
                        }
                        bool noDbWrite = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG") == billCountBefore
                            && BookingCount(db, "SELECT COUNT(*) FROM TLUUVET") == logCountBefore;

                        Check("Kiểm soát order §3 #6 & #8 (UI mở từ menu thật 1366x768, lọc/rỗng/chặn ngày, 2 lưới + tóm tắt, màu ô thật + đổi theme, xuất Excel 2 lưới, culture độc lập, chỉ đọc): đạt",
                            defaultTodayOk && uiBlockBadDateOk && uiEmptyOk
                            && btnClickReloaded && f5Handled && gridsDataOk && summaryOk
                            && colorsT0Ok && colorsT1Ok && topBarSingleRowOk && usedColsNoClip && issueColsNoClip
                            && xlsUsedOk && xlsIssOk && cultureOk && noWriteButtons && noDbWrite,
                            OcSingleLine("def=" + defaultTodayOk + ", blk=" + uiBlockBadDateOk + ", emp=" + uiEmptyOk
                                + ", btn=" + btnClickReloaded + ", f5=" + f5Handled + ", data=" + gridsDataOk
                                + ", sum=" + summaryOk + ", clr=" + colorsT0Ok + "/" + colorsT1Ok
                                + ", bar=" + topBarSingleRowOk + ", clip=" + usedColsNoClip + "/" + issueColsNoClip
                                + ", xls=" + xlsUsedOk + "/" + xlsIssOk + ", cult=" + cultureOk
                                + "(" + enUsMissingSummary + "/" + enUsOrderCell + "/" + enUsDateCell + ")"
                                + ", ro=" + noWriteButtons + "/" + noDbWrite));
                    }
                    finally
                    {
                        Thread.CurrentThread.CurrentCulture = savedCulture;
                        host.Close();
                    }
                }

                // Kiểm preview form --render-screen preview.ordercontrol.
                bool previewOk = false;
                using (Form pvForm = ScreenFactory.Create("preview.ordercontrol", adminSess))
                {
                    var pv = pvForm as OrderControlPreviewForm;
                    if (pv != null)
                    {
                        pv.StartPosition = FormStartPosition.Manual;
                        pv.ShowInTaskbar = false;
                        pv.Location = new Point(-20000, -20000);
                        pv.Show();
                        Application.DoEvents();
                        try
                        {
                            pv.Preview("from=10/06/2005");
                            pv.Preview("to=11/06/2005");
                            previewOk = pv.ViewForTest != null
                                && pv.ViewForTest.UsedGridForTest.Rows.Count == 4
                                && pv.ViewForTest.IssueGridForTest.Rows.Count == 4;
                        }
                        finally { pv.Close(); }
                    }
                }
                Check("Kiểm soát order §3 #8 (preview.ordercontrol): Show form chụp màn hình và nạp khoảng ngày hoạt động",
                    previewOk, "previewOk=" + previewOk);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
                ThemeManager.Apply(savedTheme);
                foreach (string id in billIds)
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", id);
                BookingExec(db, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE ID = @I", "@I", linkA);
                BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B)", "@A", userA, "@B", userNoStore);
                BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", storeA, "@B", storeB);
                SafeDeleteFile(xlsUsedPath);
                SafeDeleteFile(xlsIssuePath);
            }
        }

        private static bool OcCheckHeadersNoClip(DataGridView grid, Font headingFont, string[] expectedHeaders)
        {
            if (grid == null || expectedHeaders == null || grid.Columns.Count != expectedHeaders.Length)
                return false;
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                DataGridViewColumn col = grid.Columns[i];
                if (!col.Visible || !string.Equals(col.HeaderText, expectedHeaders[i], StringComparison.Ordinal))
                    return false;
                int need = TextRenderer.MeasureText(col.HeaderText, headingFont).Width;
                if (col.Width < need)
                    return false;
            }
            return true;
        }
    }
}
