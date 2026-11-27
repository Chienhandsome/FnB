using System;
using System.Collections.Generic;
using System.Drawing;
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
        private static void ShiftUiTests(string db)
        {
            var repo = new ShiftRepository(db);
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ShiftText.ConfigUseTimekeeperKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string shiftAId = DbHelper.NewId();
            string shiftBId = DbHelper.NewId();
            string refEmpId = DbHelper.NewId();
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-ca-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 0);

                BookingExec(db,
                    "INSERT INTO DCALAMVIEC (ID, NAME, TUGIO, DENGIO, TILELUONG, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @F, @TO, 125, @NT, 30, 'selftest_ca', @T)",
                    "@I", shiftAId, "@N", "Ca Đêm Đặc Biệt " + tag,
                    "@F", new DateTime(2000, 1, 1, 22, 0, 0), "@TO", new DateTime(2000, 1, 1, 6, 0, 0),
                    "@NT", "Ca qua đêm " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 100, @NT, 30, 'selftest_ca', @T)",
                    "@I", shiftBId, "@N", "Ca Chiều " + tag, "@NT", "Đang có nhân viên", "@T", DbHelper.Now());
                InsertWarehouseRefRow(db, "DNHANVIEN", "DCALAMVIECID", refEmpId, shiftBId, "NV-UI-CA-" + tag);

                string shiftTableId = new CatalogRepository(db).FindIdByTable(ShiftText.TableName);

                TestShiftMenuRouting(db);
                TestShiftGridSearchExportAndDeleteBlock(db, shiftTableId, shiftAId, shiftBId, tag, xlsPath);
                dlgSavedId = TestShiftEditDialogUi(db, cfgRepo, cfgBackups, repo, tag);
                TestShiftReadOnlyPermissionUi(db, repo, shiftTableId, shiftAId);
                TestShiftLiveThemeSwitch(db, shiftTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                SafeDeleteById(db, "DNHANVIEN", refEmpId);
                if (dlgSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgSavedId);
                    SafeDeleteById(db, "DCALAMVIEC", dlgSavedId);
                }
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", shiftAId);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", shiftBId);
                SafeDeleteById(db, "DCALAMVIEC", shiftAId);
                SafeDeleteById(db, "DCALAMVIEC", shiftBId);
                SafeDeleteFile(xlsPath);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void TestShiftMenuRouting(string db)
        {
            var adminSess = new UserSession { UserId = "u_ca_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            var deniedSess = new UserSession { UserId = "u_ca_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };

            MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, ShiftText.FunctionName);
            MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, ShiftText.FunctionName);

            bool openedMode = false;
            if (adminNode != null)
            {
                using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                {
                    var cv = created as CatalogView;
                    openedMode = cv != null && cv.IsShiftModeForTest;
                }
            }
            bool routeNoAccent = ScreenFactory.IsShiftCatalogMenu(new MenuNode { Title = "danh muc ca lam viec" }, adminSess)
                && ScreenFactory.IsShiftCatalogMenu(new MenuNode { Title = "DANH MỤC CA LÀM VIỆC" }, adminSess)
                && !ScreenFactory.IsShiftCatalogMenu(new MenuNode { Title = "Danh mục nhân viên" }, adminSess);

            Check("Nhân sự §5 #7 (menu NHÂN SỰ > Danh mục ca làm việc & ScreenFactory): Admin thấy và mở màn ca thật, không quyền bị ẩn, nhận diện không dấu",
                adminNode != null && deniedNode == null && openedMode && routeNoAccent,
                "adm=" + (adminNode != null) + ", den=" + (deniedNode == null) + ", mode=" + openedMode + ", route=" + routeNoAccent);
        }

        private static void TestShiftGridSearchExportAndDeleteBlock(
            string db, string shiftTableId, string shiftAId, string shiftBId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, shiftTableId, ShiftText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressShiftPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Tên ca", "Từ giờ", "Đến giờ", "Tỉ lệ lương (%)", "Ghi chú" };
                    bool colsOk = view.IsShiftModeForTest && grid.Columns.Count == expected.Length;
                    bool noClip = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClip = false;
                    }
                    Check("Nhân sự §5 #7 (lưới 5 cột ca làm việc không cắt tiêu đề): đúng thứ tự và đủ rộng tiêu đề",
                        colsOk && noClip,
                        "cols=" + colsOk + ", clip=" + noClip + ", count=" + grid.Columns.Count);

                    int allCount = view.VisibleRows;
                    view.SearchBoxForTest.Text = "ca dem dac biet " + tag;
                    Application.DoEvents();
                    int hitName = view.VisibleRows;

                    bool timeCellsOk = grid.Rows.Count == 1
                        && Convert.ToString(grid.Rows[0].Cells["sh_from"].Value) == "22:00"
                        && Convert.ToString(grid.Rows[0].Cells["sh_to"].Value) == "06:00";

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();
                    int restored = view.VisibleRows;

                    Check("Nhân sự §5 #6/#7 (lưới hiện giờ HH:mm, tìm không dấu & Xuất Excel 5 cột ca): đạt",
                        hitName == 1 && timeCellsOk && restored == allCount
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Ca Đêm Đặc Biệt " + tag),
                        "hit=" + hitName + ", time=" + timeCellsOk
                        + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount));

                    bool sel = view.SelectShiftRowByIdForTest(shiftBId);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warn = view.LastShiftMessageForTest;
                    bool stillThere = new ShiftRepository(db).LoadById(shiftBId) != null;
                    Check("Nhân sự §5 #7 (UI nút Xóa chặn ca còn nhân viên): báo rõ '1 nhân viên' và giữ nguyên dòng",
                        sel && stillThere && !string.IsNullOrEmpty(warn) && warn.IndexOf("1 nhân viên", StringComparison.Ordinal) >= 0,
                        SingleLine(warn));
                }
                finally { host.Close(); }
            }
        }

        private static string TestShiftEditDialogUi(
            string db, ConfigRepository cfgRepo, Dictionary<string, OpeningStockConfigBackup> cfgBackups,
            ShiftRepository repo, string tag)
        {
            string savedId = null;

            // Tắt máy chấm công: Từ/Đến giờ khóa, mặc định tỉ lệ 100%, ghi chú "0 được hiểu là 100%".
            SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 0);
            using (var dlgOff = new ShiftEditDialog(AppSession.Current, null, false))
            {
                dlgOff.SuppressPopup = true;
                ShowDialogOffscreen(dlgOff);
                try
                {
                    bool lockedOff = !dlgOff.UseTimekeeperForTest
                        && !dlgOff.FromTimePickerForTest.Enabled && !dlgOff.ToTimePickerForTest.Enabled;
                    bool defaultRate = dlgOff.SalaryRateBoxForTest.Value == 100m;
                    bool hintOk = dlgOff.RateHintLabelForTest.Visible
                        && dlgOff.RateHintLabelForTest.Text == ShiftText.ZeroRateHint;
                    bool labelsOk = AllLabelsFit(dlgOff.FieldLabelsForTest);
                    bool noScroll = !dlgOff.VerticalScroll.Visible && !dlgOff.AutoScroll && dlgOff.Height <= 728 && dlgOff.Width <= 1366;

                    Check("Nhân sự §5 #6 (ShiftEditDialog tắt máy chấm công): khóa Từ/Đến giờ, mặc định 100%, có ghi chú '0 được hiểu là 100%', không cuộn dọc",
                        lockedOff && defaultRate && hintOk && labelsOk && noScroll,
                        "lock=" + lockedOff + ", def=" + defaultRate + ", hint=" + hintOk + ", lbl=" + labelsOk + ", noScr=" + noScroll);
                }
                finally { dlgOff.Close(); }
            }

            // Bật máy chấm công: Từ/Đến giờ mở, bắt buộc cả hai, trùng giờ chặn, ca qua đêm 22:00 - 06:00 lưu được.
            SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 30);
            using (var dlgOn = new ShiftEditDialog(AppSession.Current, null, false))
            {
                dlgOn.SuppressPopup = true;
                ShowDialogOffscreen(dlgOn);
                try
                {
                    bool enabledOn = dlgOn.UseTimekeeperForTest
                        && dlgOn.FromTimePickerForTest.Enabled && dlgOn.ToTimePickerForTest.Enabled
                        && dlgOn.FromTimePickerForTest.CustomFormat == "HH:mm"
                        && dlgOn.ToTimePickerForTest.CustomFormat == "HH:mm";

                    dlgOn.NameBoxForTest.Text = "Ca Đêm Dialog " + tag;
                    dlgOn.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blockedEmptyTime = dlgOn.SavedId == null && !string.IsNullOrEmpty(dlgOn.LastErrorForTest);

                    dlgOn.FromTimePickerForTest.Value = new DateTime(2000, 1, 1, 8, 0, 0);
                    dlgOn.FromTimePickerForTest.Checked = true;
                    dlgOn.ToTimePickerForTest.Value = new DateTime(2000, 1, 1, 8, 0, 0);
                    dlgOn.ToTimePickerForTest.Checked = true;
                    dlgOn.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blockedSameTime = dlgOn.SavedId == null && !string.IsNullOrEmpty(dlgOn.LastErrorForTest);

                    dlgOn.FromTimePickerForTest.Value = new DateTime(2000, 1, 1, 22, 0, 0);
                    dlgOn.FromTimePickerForTest.Checked = true;
                    dlgOn.ToTimePickerForTest.Value = new DateTime(2000, 1, 1, 6, 0, 0);
                    dlgOn.ToTimePickerForTest.Checked = true;
                    dlgOn.SalaryRateBoxForTest.Value = 150m;
                    dlgOn.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlgOn.SavedId;
                    ShiftRow back = savedId == null ? null : repo.LoadById(savedId);

                    Check("Nhân sự §5 #6 (ShiftEditDialog bật máy chấm công): mở HH:mm, chặn thiếu giờ & trùng giờ, lưu ca qua đêm 22:00-06:00 thành công",
                        enabledOn && blockedEmptyTime && blockedSameTime
                        && back != null
                        && ShiftRules.FormatShiftTime(back.FromTime) == "22:00"
                        && ShiftRules.FormatShiftTime(back.ToTime) == "06:00"
                        && back.SalaryRate == 150m,
                        "en=" + enabledOn + ", bEmp=" + blockedEmptyTime + ", bSame=" + blockedSameTime + ", saved=" + (back != null));
                }
                finally { dlgOn.Close(); }
            }

            return savedId;
        }

        private static void TestShiftReadOnlyPermissionUi(string db, ShiftRepository repo, string shiftTableId, string sampleShiftId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", ShiftText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_ca_ui_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, shiftTableId, ShiftText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false, saveBlocked = false;
                    string origName = repo.LoadById(sampleShiftId).Name;

                    view.ShiftDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NameBoxForTest.ReadOnly && !dlg.FromTimePickerForTest.Enabled
                                && !dlg.ToTimePickerForTest.Enabled && !dlg.SalaryRateBoxForTest.Enabled
                                && dlg.NoteBoxForTest.ReadOnly;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            dlg.NameBoxForTest.Text = "Cố tình sửa khi chỉ xem";
                            saveBlocked = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectShiftRowByIdForTest(sampleShiftId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    string afterName = repo.LoadById(sampleShiftId).Name;
                    Check("Nhân sự §5 #7 (UI chỉ có quyền Xem ca): ẩn Thêm/Sửa/Xóa, nhấp đôi mở chỉ đọc, khóa ô và chặn lưu",
                        hiddenCrud && roOpened && roLocked && roNoSave && saveBlocked && afterName == origName,
                        "hid=" + hiddenCrud + ", ro=" + roOpened + ", lock=" + roLocked + ", noSave=" + roNoSave + ", blk=" + saveBlocked);
                    host.Close();
                }
            }
            finally { AppSession.Current = prev; }
        }

        private static void TestShiftLiveThemeSwitch(string db, string shiftTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new ShiftEditDialog(AppSession.Current, null, false))
            {
                var view = new CatalogView(db, shiftTableId, ShiftText.FunctionName) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    ThemeManager.Apply(tB);
                    view.ApplyTheme(tB, tB.Background);
                    Application.DoEvents();

                    DataGridView g = view.GridForTest;
                    bool cellColorOk = g.Rows.Count == 0
                        || (g.Rows[0].Cells[0].InheritedStyle.BackColor == tB.Surface
                            && g.Rows[0].Cells[0].InheritedStyle.SelectionBackColor == tB.Primary);
                    bool dlgColorOk = dlg.BackColor == tB.Background
                        && dlg.ErrorLabelForTest.ForeColor == tB.Danger
                        && dlg.SalaryRateBoxForTest.BackColor == tB.Surface
                        && dlg.RateHintLabelForTest.ForeColor == tB.TextMuted;

                    Check("Nhân sự §5 #7 (đổi bảng màu trực tiếp ca): ô lưới thực tế và ShiftEditDialog đổi đúng màu ThemeTokens",
                        g.BackgroundColor == tB.Surface && cellColorOk && dlgColorOk,
                        "grid=" + (g.BackgroundColor == tB.Surface) + ", cell=" + cellColorOk + ", dlg=" + dlgColorOk);
                }
                finally
                {
                    dlg.Close();
                    host.Close();
                }
            }
        }
    }
}
