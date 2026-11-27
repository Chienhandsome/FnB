using System;
using System.Collections.Generic;
using System.IO;
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
        private static void RewardPenaltyUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string reasonId = DbHelper.NewId();
            string rp1Id = DbHelper.NewId();
            string rp2Id = DbHelper.NewId();
            string rpOldId = DbHelper.NewId();
            string dlgMonthSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-tp-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 30, 30, 'selftest_tp', @T)",
                    "@I", empAId, "@N", "Đỗ Hoàng An " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 30, 30, 'selftest_tp', @T)",
                    "@I", empBId, "@N", "Bùi Minh Bảo " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DLYDOTHUONGPHAT (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_tp', @T)",
                    "@I", reasonId, "@N", "Đạt chỉ tiêu " + tag, "@T", DbHelper.Now());

                DateTime curDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, Math.Min(15, DateTime.Today.Day));
                DateTime oldDate = new DateTime(2020, 2, 10);

                BookingExec(db,
                    "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, DLYDOTHUONGPHATID, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @E, 200000, 10000, @R, @NT, 30, 'selftest_tp', @T)",
                    "@I", rp1Id, "@N", "TP-UI1-" + tag, "@D", curDate, "@E", empAId, "@R", reasonId, "@NT", "Thưởng đặc biệt " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, DLYDOTHUONGPHATID, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @E, 0, 50000, NULL, @NT, 30, 'selftest_tp', @T)",
                    "@I", rp2Id, "@N", "TP-UI2-" + tag, "@D", curDate, "@E", empBId, "@NT", "Đi muộn " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, DLYDOTHUONGPHATID, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @E, 999000, 0, NULL, @NT, 30, 'selftest_tp', @T)",
                    "@I", rpOldId, "@N", "TP-OLD-" + tag, "@D", oldDate, "@E", empAId, "@NT", "Phiếu năm 2020", "@T", DbHelper.Now());

                string tpTableId = new CatalogRepository(db).FindIdByTable(RewardPenaltyText.TableName);

                TestRewardPenaltyMenuRouting(db);
                TestRewardPenaltyGridFiltersTotalsAndExport(db, tpTableId, empAId, tag, xlsPath);
                dlgMonthSavedId = TestRewardPenaltyDialogMonthLimitAndDatePerm(db, empAId, tag);
                TestRewardPenaltyReadOnlyAndTheme(db, tpTableId, rp1Id);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (dlgMonthSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgMonthSavedId);
                    SafeDeleteById(db, "TTHUONGPHAT", dlgMonthSavedId);
                }
                SafeDeleteById(db, "TTHUONGPHAT", rp1Id);
                SafeDeleteById(db, "TTHUONGPHAT", rp2Id);
                SafeDeleteById(db, "TTHUONGPHAT", rpOldId);
                SafeDeleteById(db, "DLYDOTHUONGPHAT", reasonId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empBId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestRewardPenaltyMenuRouting(string db)
        {
            var adminSess = new UserSession { UserId = "u_tp_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            var deniedSess = new UserSession { UserId = "u_tp_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };

            MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, RewardPenaltyText.FunctionName)
                ?? FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, RewardPenaltyText.AltFunctionName);
            MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, RewardPenaltyText.FunctionName)
                ?? FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, RewardPenaltyText.AltFunctionName);

            bool openedMode = false;
            if (adminNode != null)
            {
                using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                {
                    var cv = created as CatalogView;
                    openedMode = cv != null && cv.IsRewardPenaltyModeForTest;
                }
            }
            bool routeNoAccent = ScreenFactory.IsRewardPenaltyMenu(new MenuNode { Title = "thuong phat" }, adminSess)
                && ScreenFactory.IsRewardPenaltyMenu(new MenuNode { Title = "THƯỞNG PHẠT" }, adminSess)
                && !ScreenFactory.IsRewardPenaltyMenu(new MenuNode { Title = "Tạm ứng lương" }, adminSess);

            Check("Thưởng phạt §4 #5 (menu NHÂN SỰ > Thưởng phạt & ScreenFactory): Admin thấy và mở màn thật, không quyền ẩn, nhận diện không dấu",
                adminNode != null && deniedNode == null && openedMode && routeNoAccent,
                "adm=" + (adminNode != null) + ", den=" + (deniedNode == null) + ", mode=" + openedMode + ", route=" + routeNoAccent);
        }

        private static void TestRewardPenaltyGridFiltersTotalsAndExport(
            string db, string tpTableId, string empAId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, tpTableId, RewardPenaltyText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressRpPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Số phiếu", "Ngày", "Nhân viên", "Thưởng", "Phạt", "Lý do", "Ghi chú" };
                    bool colsOk = view.IsRewardPenaltyModeForTest && grid.Columns.Count == expected.Length;
                    bool noClip = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClip = false;
                    }

                    // Mặc định tháng hiện tại: thấy rp1 (200k/10k) và rp2 (0/50k), KHÔNG thấy rpOld (2020).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    int curMonthRows = view.VisibleRows;
                    decimal sumCurBonus = view.RpTotalBonusForTest;
                    decimal sumCurPenalty = view.RpTotalPenaltyForTest;

                    // Lọc theo nhân viên A -> chỉ còn rp1 (200k/10k).
                    int empAIdx = -1;
                    for (int i = 0; i < view.RpEmployeeFilterForTest.Items.Count; i++)
                    {
                        var ch = view.RpEmployeeFilterForTest.Items[i] as Choice;
                        if (ch != null && ch.Key == empAId) { empAIdx = i; break; }
                    }
                    if (empAIdx >= 0)
                        view.RpEmployeeFilterForTest.SelectedIndex = empAIdx;
                    Application.DoEvents();
                    int empARows = view.VisibleRows;
                    decimal empABonus = view.RpTotalBonusForTest;
                    decimal empAPenalty = view.RpTotalPenaltyForTest;

                    // Tìm không dấu trên lý do/ghi chú & Xuất Excel 7 cột.
                    view.SearchBoxForTest.Text = "dac biet " + tag;
                    Application.DoEvents();
                    int searchHits = view.VisibleRows;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    // Đổi Từ/Đến ngày về 01/02/2020 - 28/02/2020 -> thấy rpOld (999.000).
                    view.SearchBoxForTest.Text = "";
                    view.RpEmployeeFilterForTest.SelectedIndex = 0;
                    view.RpFromDateBoxForTest.Value = new DateTime(2020, 2, 1);
                    view.RpToDateBoxForTest.Value = new DateTime(2020, 2, 28);
                    view.RpFilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool oldFound = view.VisibleRows >= 1 && view.RpTotalBonusForTest >= 999000m;

                    Check("Thưởng phạt §4 #4 (lưới 7 cột, lọc Từ/Đến ngày & Nhân viên, hàng tổng, tìm không dấu, xuất Excel): đạt",
                        colsOk && noClip
                        && curMonthRows == 2 && sumCurBonus == 200000m && sumCurPenalty == 60000m
                        && empARows == 1 && empABonus == 200000m && empAPenalty == 10000m
                        && searchHits == 1
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 7 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Đỗ Hoàng An " + tag)
                        && oldFound,
                        "cols=" + colsOk + ", clip=" + noClip + ", cur=" + curMonthRows + "(" + sumCurBonus + "/" + sumCurPenalty + ")"
                        + ", empA=" + empARows + "(" + empABonus + "/" + empAPenalty + "), hit=" + searchHits
                        + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount) + ", old=" + oldFound);
                }
                finally { host.Close(); }
            }
        }

        private static string TestRewardPenaltyDialogMonthLimitAndDatePerm(string db, string empAId, string tag)
        {
            string savedId = null;

            // §4 #3 trên hộp thoại trực tiếp: giới hạn 03/2004, ngày 05/04/2004 bị chặn, 15/03/2004 lưu được.
            using (var dlg = new RewardPenaltyEditDialog(AppSession.Current, null, false, 3, 2004))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    bool labelsOk = AllLabelsFit(dlg.FieldLabelsForTest);
                    dlg.NumberBoxForTest.Text = "TP-M0304-" + tag;
                    dlg.EmployeeComboForTest.SelectId(empAId);
                    dlg.BonusBoxForTest.Value = 120000m;
                    dlg.DatePickerForTest.Value = new DateTime(2004, 4, 5);
                    bool outSaved = dlg.DoSave();
                    string outErr = dlg.LastErrorForTest;

                    dlg.DatePickerForTest.Value = new DateTime(2004, 3, 15);
                    bool inSaved = dlg.DoSave();
                    savedId = dlg.SavedId;

                    Check("Thưởng phạt §4 #3/#5 (RewardPenaltyEditDialog giới hạn tháng 03/2004 & bố cục gọn): 05/04/2004 chặn, 15/03/2004 lưu, không cuộn dọc",
                        noScroll && labelsOk && !outSaved && outErr == "Ngày phải nằm trong tháng 3 năm 2004"
                        && inSaved && !string.IsNullOrEmpty(savedId),
                        "noScr=" + noScroll + ", lbl=" + labelsOk + ", outErr=" + SingleLine(outErr) + ", inOk=" + inSaved);
                }
                finally { dlg.Close(); }
            }

            // §4 #4 quyền đổi ngày: khóa ô Ngày khi thiếu "Thay đổi ngày trong các phiếu", mở khi có quyền.
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add("", RewardPenaltyText.FunctionName, RoleModes.View | RoleModes.Add);
            var sessNoDate = new UserSession { UserId = "u_tp_nodate", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate };

            var permsWithDate = new FunctionPermissions(false);
            permsWithDate.Add("", RewardPenaltyText.FunctionName, RoleModes.View | RoleModes.Add);
            permsWithDate.Add("", RewardPenaltyText.FunctionDateChange, RoleModes.View);
            var sessWithDate = new UserSession { UserId = "u_tp_date", IsAdmin = false, DatabasePath = db, Permissions = permsWithDate };

            bool dateLocked, dateUnlocked;
            using (var d1 = new RewardPenaltyEditDialog(sessNoDate, null, false))
                dateLocked = !d1.DatePickerForTest.Enabled;
            using (var d2 = new RewardPenaltyEditDialog(sessWithDate, null, false))
                dateUnlocked = d2.DatePickerForTest.Enabled;
            Check("Thưởng phạt §4 #4 (khóa ô Ngày theo quyền 'Thay đổi ngày trong các phiếu'): khóa khi thiếu quyền và mở khi có quyền",
                dateLocked && dateUnlocked,
                "locked=" + dateLocked + ", unlocked=" + dateUnlocked);

            return savedId;
        }

        private static void TestRewardPenaltyReadOnlyAndTheme(string db, string tpTableId, string sampleId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", RewardPenaltyText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_tp_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, tpTableId, RewardPenaltyText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false, saveBlocked = false;

                    view.RpDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NumberBoxForTest.ReadOnly && !dlg.DatePickerForTest.Enabled
                                && !dlg.EmployeeComboForTest.Enabled && !dlg.BonusBoxForTest.Enabled && !dlg.PenaltyBoxForTest.Enabled;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            saveBlocked = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectRewardPenaltyRowByIdForTest(sampleId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    Check("Thưởng phạt §4 #5 (UI chỉ có quyền Xem): ẩn Thêm/Sửa/Xóa, nhấp đôi mở hộp chỉ đọc và chặn lưu",
                        hiddenCrud && roOpened && roLocked && roNoSave && saveBlocked,
                        "hid=" + hiddenCrud + ", ro=" + roOpened + ", lock=" + roLocked + ", noSave=" + roNoSave + ", blk=" + saveBlocked);

                    // Đổi bảng màu trực tiếp -> kiểm màu ô lưới thật.
                    var themes = new List<ThemeTokens>(BuiltInThemes.All());
                    ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];
                    ThemeManager.Apply(tB);
                    view.ApplyTheme(tB, tB.Background);
                    Application.DoEvents();

                    DataGridView g = view.GridForTest;
                    bool cellColorOk = g.Rows.Count == 0
                        || (g.Rows[0].Cells[0].InheritedStyle.BackColor == tB.Surface
                            && g.Rows[0].Cells[0].InheritedStyle.SelectionBackColor == tB.Primary);
                    Check("Thưởng phạt §5 (đổi bảng màu trực tiếp): màu ô lưới thực tế khớp ThemeTokens",
                        g.BackgroundColor == tB.Surface && cellColorOk,
                        "grid=" + (g.BackgroundColor == tB.Surface) + ", cell=" + cellColorOk);
                    host.Close();
                }
            }
            finally { AppSession.Current = prev; }
        }
    }
}
