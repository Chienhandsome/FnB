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
        private static void EmployeeUiTests(string db)
        {
            var repo = new EmployeeRepository(db);
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { EmployeeText.ConfigUseTimekeeperKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string shiftId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string refVoucherId = DbHelper.NewId();
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-nv-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);

                BookingExec(db,
                    "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 100, 30, 'selftest_nv', @T)",
                    "@I", shiftId, "@N", "Ca UI NV " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, CODE, NAME, DIENTHOAI, DIACHI, DCALAMVIECID, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @P, @A, @SH, 0, 120000, 4500000, 30, 30, @NT, 30, 'selftest_nv', @T)",
                    "@I", empAId, "@C", "NVUIA-" + tag, "@N", "Đặng Ánh Dương " + tag,
                    "@P", "0912000111", "@A", "88 Nguyễn Huệ", "@SH", shiftId, "@NT", "Ghi chú đặc biệt " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, CODE, NAME, DIENTHOAI, DIACHI, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @P, @A, 30, 200000, 0, 0, 30, @NT, 30, 'selftest_nv', @T)",
                    "@I", empBId, "@C", "NVUIB-" + tag, "@N", "Phạm Bảo " + tag,
                    "@P", "0988111222", "@A", "10 Hai Bà Trưng", "@NT", "Nhân viên ca", "@T", DbHelper.Now());
                InsertWarehouseRefRow(db, "TTHUCHI", "DNHANVIENID", refVoucherId, empBId, "TC-NV-UI-" + tag);

                string empTableId = new CatalogRepository(db).FindIdByTable(EmployeeText.TableName);

                TestEmployeeMenuRouting(db);
                TestEmployeeGridSearchExportAndDeleteBlock(db, empTableId, empAId, empBId, tag, xlsPath);
                dlgSavedId = TestEmployeeEditDialogUi(db, cfgRepo, cfgBackups, repo, shiftId, empAId, tag);
                TestEmployeeReadOnlyPermissionUi(db, repo, empTableId, empAId);
                TestEmployeeLiveThemeSwitch(db, empTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                SafeDeleteById(db, "TTHUCHI", refVoucherId);
                if (dlgSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgSavedId);
                    SafeDeleteById(db, "DNHANVIEN", dlgSavedId);
                }
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", empAId);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", empBId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empBId);
                SafeDeleteById(db, "DCALAMVIEC", shiftId);
                SafeDeleteFile(xlsPath);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void TestEmployeeMenuRouting(string db)
        {
            var adminSess = new UserSession { UserId = "u_nv_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            var deniedSess = new UserSession { UserId = "u_nv_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };

            MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, EmployeeText.FunctionName);
            MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, EmployeeText.FunctionName);

            bool openedMode = false;
            if (adminNode != null)
            {
                using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                {
                    var cv = created as CatalogView;
                    openedMode = cv != null && cv.IsEmployeeModeForTest;
                }
            }
            bool routeNoAccent = ScreenFactory.IsEmployeeCatalogMenu(new MenuNode { Title = "danh muc nhan vien" }, adminSess)
                && ScreenFactory.IsEmployeeCatalogMenu(new MenuNode { Title = "DANH MỤC NHÂN VIÊN" }, adminSess)
                && !ScreenFactory.IsEmployeeCatalogMenu(new MenuNode { Title = "Danh mục ca làm việc" }, adminSess);

            Check("Nhân sự §5 #5 (menu NHÂN SỰ > Danh mục nhân viên & ScreenFactory): Admin thấy và mở màn NV thật, không quyền bị ẩn, nhận diện không dấu",
                adminNode != null && deniedNode == null && openedMode && routeNoAccent,
                "adm=" + (adminNode != null) + ", den=" + (deniedNode == null) + ", mode=" + openedMode + ", route=" + routeNoAccent);
        }

        private static void TestEmployeeGridSearchExportAndDeleteBlock(
            string db, string empTableId, string empAId, string empBId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, empTableId, EmployeeText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressEmployeePopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Mã", "Tên nhân viên", "Điện thoại", "Địa chỉ", "Ca làm việc", "Cách tính lương", "Lương ca", "Lương tháng", "Ghi chú" };
                    bool colsOk = view.IsEmployeeModeForTest && grid.Columns.Count == expected.Length;
                    bool noClip = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClip = false;
                    }
                    Check("Nhân sự §5 #5 (lưới 9 cột nhân viên không cắt tiêu đề): đúng thứ tự và đủ rộng tiêu đề",
                        colsOk && noClip,
                        "cols=" + colsOk + ", clip=" + noClip + ", count=" + grid.Columns.Count);

                    int allCount = view.VisibleRows;
                    view.SearchBoxForTest.Text = "dang anh duong " + tag;
                    Application.DoEvents();
                    int hitName = view.VisibleRows;

                    view.SearchBoxForTest.Text = "nvuia-" + tag;
                    Application.DoEvents();
                    int hitCode = view.VisibleRows;

                    view.SearchBoxForTest.Text = "0912000111";
                    Application.DoEvents();
                    int hitPhone = view.VisibleRows;

                    view.SearchBoxForTest.Text = "nguyen hue";
                    Application.DoEvents();
                    int hitAddr = view.VisibleRows;

                    view.SearchBoxForTest.Text = "dac biet " + tag;
                    Application.DoEvents();
                    int hitNote = view.VisibleRows;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();
                    int restored = view.VisibleRows;

                    Check("Nhân sự §5 #5 (tìm không dấu Mã/Tên/SĐT/Địa chỉ/Ghi chú & Xuất Excel 9 cột): lọc đúng và xuất .xls đúng cột",
                        hitName == 1 && hitCode == 1 && hitPhone >= 1 && hitAddr >= 1 && hitNote == 1
                        && restored == allCount
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 9 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Đặng Ánh Dương " + tag),
                        "n=" + hitName + ", c=" + hitCode + ", p=" + hitPhone + ", a=" + hitAddr + ", nt=" + hitNote
                        + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount));

                    bool sel = view.SelectEmployeeRowByIdForTest(empBId);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warn = view.LastEmployeeMessageForTest;
                    bool stillThere = new EmployeeRepository(db).LoadById(empBId) != null;
                    Check("Nhân sự §5 #4 (UI nút Xóa chặn NV còn phiếu thu chi): báo rõ '1 phiếu thu chi' và giữ nguyên dòng",
                        sel && stillThere && !string.IsNullOrEmpty(warn) && warn.IndexOf("1 phiếu thu chi", StringComparison.Ordinal) >= 0,
                        SingleLine(warn));
                }
                finally { host.Close(); }
            }
        }

        private static string TestEmployeeEditDialogUi(
            string db, ConfigRepository cfgRepo, Dictionary<string, OpeningStockConfigBackup> cfgBackups,
            EmployeeRepository repo, string shiftId, string existingEmpAId, string tag)
        {
            string savedId = null;

            // Tắt máy chấm công: Mã & Ca ẩn; kiểm tra khóa ô lương theo radio và giữ nguyên Lương tháng cũ khi chuyển sang Theo ca.
            SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);
            using (var dlgOff = new EmployeeEditDialog(AppSession.Current, existingEmpAId, false))
            {
                dlgOff.SuppressPopup = true;
                ShowDialogOffscreen(dlgOff);
                try
                {
                    bool hiddenOff = !dlgOff.UseTimekeeperForTest && !dlgOff.CodeBoxForTest.Visible && !dlgOff.ShiftComboForTest.Visible;
                    bool monthModeLocksShift = dlgOff.ByMonthRadioForTest.Checked
                        && !dlgOff.ShiftSalaryBoxForTest.Enabled && dlgOff.MonthlySalaryBoxForTest.Enabled
                        && dlgOff.OffSaturdayCheckForTest.Enabled && dlgOff.OffSundayCheckForTest.Enabled;

                    // Chuyển sang Theo ca -> khóa Lương tháng & 2 checkbox, nhưng giá trị Lương tháng (4.500.000) vẫn giữ nguyên.
                    dlgOff.ByShiftRadioForTest.Checked = true;
                    dlgOff.ApplySalaryModeState();
                    bool shiftModeLocksMonth = !dlgOff.MonthlySalaryBoxForTest.Enabled && dlgOff.ShiftSalaryBoxForTest.Enabled
                        && !dlgOff.OffSaturdayCheckForTest.Enabled && !dlgOff.OffSundayCheckForTest.Enabled
                        && dlgOff.MonthlySalaryBoxForTest.Value == 4500000m;

                    // Chuyển sang Tháng theo ca -> khóa Lương ca, mở Lương tháng, khóa 2 checkbox (như app gốc).
                    dlgOff.MonthByShiftRadioForTest.Checked = true;
                    dlgOff.ApplySalaryModeState();
                    bool comboUnlocksBoth = !dlgOff.ShiftSalaryBoxForTest.Enabled && dlgOff.MonthlySalaryBoxForTest.Enabled
                        && !dlgOff.OffSaturdayCheckForTest.Enabled && !dlgOff.OffSundayCheckForTest.Enabled;

                    // Lưu ở chế độ Theo ca với Lương ca mới 180.000 -> DB phải giữ Lương tháng 4.500.000 và CODE/DCALAMVIECID cũ.
                    dlgOff.ByShiftRadioForTest.Checked = true;
                    dlgOff.ApplySalaryModeState();
                    dlgOff.ShiftSalaryBoxForTest.Value = 180000m;
                    dlgOff.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    EmployeeRow back = repo.LoadById(existingEmpAId);

                    Check("Nhân sự §5 #3 (EmployeeEditDialog tắt máy chấm công & khóa ô lương theo radio): ẩn Mã/Ca, khóa đúng 3 chế độ, lưu giữ Lương tháng 4.5tr và Mã/Ca cũ",
                        hiddenOff && monthModeLocksShift && shiftModeLocksMonth && comboUnlocksBoth
                        && back != null && back.SalaryMode == 30 && back.ShiftSalary == 180000m
                        && back.MonthlySalary == 4500000m && back.Code == "NVUIA-" + tag && back.ShiftId == shiftId,
                        "hid=" + hiddenOff + ", monLock=" + monthModeLocksShift + ", shLock=" + shiftModeLocksMonth
                        + ", cmb=" + comboUnlocksBoth + ", msal=" + (back == null ? -1m : back.MonthlySalary));
                }
                finally { dlgOff.Close(); }
            }

            Check("Nhân sự (nhãn và khóa lương như app gốc): 3 kiểu tên Lương theo ca / Lương tháng theo ngày / Lương tháng theo ca; hai kiểu tháng khóa Lương ca",
                EmployeeRules.SalaryModeDisplayText(EmployeeSalaryMode.ByShift) == "Lương theo ca"
                && EmployeeRules.SalaryModeDisplayText(EmployeeSalaryMode.ByMonth) == "Lương tháng theo ngày"
                && EmployeeRules.SalaryModeDisplayText(EmployeeSalaryMode.MonthByShift) == "Lương tháng theo ca"
                && !EmployeeRules.IsShiftSalaryLocked(EmployeeSalaryMode.ByShift)
                && EmployeeRules.IsShiftSalaryLocked(EmployeeSalaryMode.ByMonth)
                && EmployeeRules.IsShiftSalaryLocked(EmployeeSalaryMode.MonthByShift));

            // Bật máy chấm công: mở hộp mới -> Mã và Ca hiện và bắt buộc; nhập đủ -> lưu thành công; không cuộn dọc.
            SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 30);
            using (var dlgOn = new EmployeeEditDialog(AppSession.Current, null, false))
            {
                dlgOn.SuppressPopup = true;
                ShowDialogOffscreen(dlgOn);
                try
                {
                    bool visibleOn = dlgOn.UseTimekeeperForTest && dlgOn.CodeBoxForTest.Visible && dlgOn.ShiftComboForTest.Visible;
                    bool noScroll = !dlgOn.VerticalScroll.Visible && !dlgOn.AutoScroll && dlgOn.Height <= 728 && dlgOn.Width <= 1366;
                    bool labelsOk = AllLabelsFit(dlgOn.FieldLabelsForTest);

                    dlgOn.NameBoxForTest.Text = "NV Dialog Mới " + tag;
                    dlgOn.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blockedMissing = dlgOn.SavedId == null && !string.IsNullOrEmpty(dlgOn.LastErrorForTest);

                    dlgOn.CodeBoxForTest.Text = "NVDLG-" + tag;
                    dlgOn.ShiftComboForTest.SelectId(shiftId);
                    dlgOn.ShiftSalaryBoxForTest.Value = 250000m;
                    dlgOn.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlgOn.SavedId;
                    EmployeeRow back = savedId == null ? null : repo.LoadById(savedId);

                    Check("Nhân sự §5 #3/#6 (EmployeeEditDialog bật máy chấm công): hiện & bắt buộc Mã/Ca, không cuộn dọc, nhãn không cắt, lưu thành công",
                        visibleOn && noScroll && labelsOk && blockedMissing
                        && back != null && back.Code == "NVDLG-" + tag && back.ShiftId == shiftId && back.ShiftSalary == 250000m,
                        "vis=" + visibleOn + ", noScr=" + noScroll + ", lbl=" + labelsOk + ", blk=" + blockedMissing + ", saved=" + (back != null));
                }
                finally { dlgOn.Close(); }
            }

            return savedId;
        }

        private static void TestEmployeeReadOnlyPermissionUi(string db, EmployeeRepository repo, string empTableId, string sampleEmpId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", EmployeeText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_nv_ui_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, empTableId, EmployeeText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false, saveBlocked = false;
                    string origName = repo.LoadById(sampleEmpId).Name;

                    view.EmployeeDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NameBoxForTest.ReadOnly && dlg.PhoneBoxForTest.ReadOnly
                                && dlg.AddressBoxForTest.ReadOnly && !dlg.ShiftSalaryBoxForTest.Enabled
                                && !dlg.MonthlySalaryBoxForTest.Enabled && !dlg.ByShiftRadioForTest.Enabled;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            dlg.NameBoxForTest.Text = "Cố tình sửa khi chỉ xem";
                            saveBlocked = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectEmployeeRowByIdForTest(sampleEmpId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    string afterName = repo.LoadById(sampleEmpId).Name;
                    Check("Nhân sự §5 #5 (UI chỉ có quyền Xem NV): ẩn Thêm/Sửa/Xóa, nhấp đôi mở chỉ đọc, khóa ô và chặn lưu",
                        hiddenCrud && roOpened && roLocked && roNoSave && saveBlocked && afterName == origName,
                        "hid=" + hiddenCrud + ", ro=" + roOpened + ", lock=" + roLocked + ", noSave=" + roNoSave + ", blk=" + saveBlocked);
                    host.Close();
                }
            }
            finally { AppSession.Current = prev; }
        }

        private static void TestEmployeeLiveThemeSwitch(string db, string empTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new EmployeeEditDialog(AppSession.Current, null, false))
            {
                var view = new CatalogView(db, empTableId, EmployeeText.FunctionName) { Dock = DockStyle.Fill };
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
                        && dlg.ShiftSalaryBoxForTest.BackColor == tB.Surface;

                    Check("Nhân sự §5 #6 (đổi bảng màu trực tiếp NV): ô lưới thực tế và EmployeeEditDialog đổi đúng màu ThemeTokens",
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
