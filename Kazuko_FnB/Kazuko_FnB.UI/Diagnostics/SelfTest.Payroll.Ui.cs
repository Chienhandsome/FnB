using System;
using System.Collections.Generic;
using System.Drawing;
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
        private static void PayrollUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string shiftSangId = DbHelper.NewId();
            string shiftToiId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empNegId = DbHelper.NewId();
            string advNegId = DbHelper.NewId();
            var createdPeriodIds = new List<string>();
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tl')",
                    "@I", storeId, "@N", "CH UI TL " + tag);
                BookingExec(db, "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 100, 30, 'selftest_tl', @T)",
                    "@I", shiftSangId, "@N", "Sáng UI " + tag, "@T", now);
                BookingExec(db, "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 50, 30, 'selftest_tl', @T)",
                    "@I", shiftToiId, "@N", "Tối UI " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_tl', @T)",
                    "@I", empAId, "@N", "An UI TL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_tl', @T)",
                    "@I", empNegId, "@N", "Bình Âm TL " + tag, "@T", now);
                // Tạm ứng 500.000 cho Bình Âm -> chưa chấm công thì Thực nhận = -500.000 (chữ màu Danger).
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, DCUAHANGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 1, 30, @E, @S, 0, 500000, 0, 30, 'selftest_tl', @T)",
                    "@I", advNegId, "@N", "PC-NEG-UI-" + tag, "@D", new DateTime(2004, 3, 10), "@E", empNegId, "@S", storeId, "@T", now);

                var adminSess = new UserSession
                {
                    UserId = "selftest_tl",
                    UserName = "selftest_tl",
                    DisplayName = "Admin TL UI",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TL " + tag }
                };

                // 1. Menu routing & quyền menu Tính lương / Chấm công + preview.
                TestPayrollMenuAndPermissions(db, adminSess);

                // 2. Tạo kỳ 3/2004 qua PayrollCreateDialog + kiểm chặn trùng trong hộp thoại.
                string periodId = TestPayrollCreateDialogUi(adminSess, createdPeriodIds);
                if (string.IsNullOrEmpty(periodId))
                    return;

                // 3. PayrollView chế độ Tính lương: lưới 31 ngày, màu ô thật theo ThemeManager, đổi theme sống,
                // tính lại ngay trong bộ nhớ trước khi lưu, phím tắt 1/2/3/0, cảnh báo đóng chưa lưu, Lưu, Tải lại, không cuộn dọc thanh nút, không nút Chi lương.
                TestPayrollViewPayrollModeUi(db, adminSess, periodId, empAId, empNegId, shiftSangId, shiftToiId, tag);

                // 4. PayrollView chế độ Chấm công: ẩn 6 cột tiền (không có giá trị tiền), ẩn nút Tải lại, Lưu chỉ ghi TBANGLUONGCHITIET không chạm TBANGLUONGTONGHOP.
                TestPayrollViewAttendanceModeUi(db, adminSess, periodId, empAId, shiftSangId);

                // 5. Quyền chỉ Xem: khóa mọi nút thao tác và không áp được trạng thái chấm công.
                TestPayrollViewReadOnlyUi(db, periodId, empAId, shiftSangId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string pid in createdPeriodIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", pid);
                    SafeDeleteById(db, "TBANGLUONG", pid);
                }
                SafeDeleteById(db, "TTHUCHI", advNegId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empNegId);
                SafeDeleteById(db, "DCALAMVIEC", shiftSangId);
                SafeDeleteById(db, "DCALAMVIEC", shiftToiId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static void TestPayrollMenuAndPermissions(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode payrollNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsPayrollMenu(n));
            MenuNode attNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsAttendanceMenu(n));

            bool openPayrollOk = false;
            bool openAttOk = false;
            if (payrollNode != null)
            {
                using (Control c = ScreenFactory.CreateView(payrollNode, adminSess))
                {
                    var pv = c as PayrollView;
                    openPayrollOk = pv != null && pv.ModeForTest == PayrollMode.Payroll;
                }
            }
            if (attNode != null)
            {
                using (Control c = ScreenFactory.CreateView(attNode, adminSess))
                {
                    var pv = c as PayrollView;
                    openAttOk = pv != null && pv.ModeForTest == PayrollMode.Attendance;
                }
            }

            var deniedSess = new UserSession { UserId = "u_tl_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
            MenuModel deniedMenu = MainMenuBuilder.Build(deniedSess);
            bool deniedHidesBoth = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsPayrollMenu(n)) == null
                && FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsAttendanceMenu(n)) == null;

            var attOnlyPerms = new FunctionPermissions(false);
            attOnlyPerms.Add(PayrollText.AttendanceFunctionId, PayrollText.AttendanceFunctionName, RoleModes.View);
            var attOnlySess = new UserSession { UserId = "u_cc_menu", IsAdmin = false, DatabasePath = db, Permissions = attOnlyPerms };
            MenuModel attOnlyMenu = MainMenuBuilder.Build(attOnlySess);
            bool attOnlyMenuOk = FindMenuLeaf(attOnlyMenu.Roots, n => ScreenFactory.IsAttendanceMenu(n)) != null
                && FindMenuLeaf(attOnlyMenu.Roots, n => ScreenFactory.IsPayrollMenu(n)) == null;

            bool foldOk = ScreenFactory.IsPayrollMenu(new MenuNode { Title = "tinh luong" })
                && ScreenFactory.IsPayrollMenu(new MenuNode { Title = "TÍNH LƯƠNG" })
                && ScreenFactory.IsAttendanceMenu(new MenuNode { Title = "cham cong" })
                && ScreenFactory.IsAttendanceMenu(new MenuNode { FunctionId = PayrollText.AttendanceFunctionId })
                && !ScreenFactory.IsPayrollMenu(new MenuNode { Title = "Chấm công" });

            bool previewOk = false;
            using (Form f1 = ScreenFactory.Create("preview.payroll", adminSess))
            using (Form f2 = ScreenFactory.Create("preview.attendance", adminSess))
            {
                var p1 = f1 as PayrollPreviewForm;
                var p2 = f2 as PayrollPreviewForm;
                previewOk = p1 != null && p1.ViewForTest.ModeForTest == PayrollMode.Payroll
                    && p2 != null && p2.ViewForTest.ModeForTest == PayrollMode.Attendance;
            }

            Check("Tính lương P3a §6 #10 (menu Tính lương & Chấm công, ScreenFactory, phân quyền menu, preview): mở đúng 2 chế độ PayrollView",
                payrollNode != null && attNode != null && openPayrollOk && openAttOk
                && deniedHidesBoth && attOnlyMenuOk && foldOk && previewOk,
                "pNode=" + (payrollNode != null) + ", aNode=" + (attNode != null)
                + ", openP=" + openPayrollOk + ", openA=" + openAttOk
                + ", den=" + deniedHidesBoth + ", attOnly=" + attOnlyMenuOk + ", fold=" + foldOk + ", pv=" + previewOk);
        }

        private static string TestPayrollCreateDialogUi(UserSession adminSess, List<string> createdPeriodIds)
        {
            var svc = new PayrollService(adminSess, PayrollMode.Payroll);
            string createdId = null;
            bool defaultsOk = false, autoNameOk = false, dupBlockedUi = false;

            using (var dlg = new PayrollCreateDialog(svc) { SuppressPopup = true })
            {
                ShowDialogOffscreen(dlg);
                try
                {
                    defaultsOk = (int)dlg.MonthBoxForTest.Value == DateTime.Today.Month
                        && (int)dlg.YearBoxForTest.Value == DateTime.Today.Year;
                    dlg.MonthBoxForTest.Value = 3;
                    dlg.YearBoxForTest.Value = 2004;
                    Application.DoEvents();
                    autoNameOk = dlg.NameBoxForTest.Text == "Tháng 3/2004";
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    if (dlg.CreatedPeriod != null)
                    {
                        createdId = dlg.CreatedPeriod.Id;
                        createdPeriodIds.Add(createdId);
                    }
                }
                finally { if (!dlg.IsDisposed) dlg.Close(); }
            }

            using (var dlgDup = new PayrollCreateDialog(svc) { SuppressPopup = true })
            {
                ShowDialogOffscreen(dlgDup);
                try
                {
                    dlgDup.MonthBoxForTest.Value = 3;
                    dlgDup.YearBoxForTest.Value = 2004;
                    Application.DoEvents();
                    dlgDup.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    dupBlockedUi = dlgDup.CreatedPeriod == null
                        && dlgDup.LastErrorForTest == PayrollText.PeriodDuplicate(3, 2004);
                }
                finally { if (!dlgDup.IsDisposed) dlgDup.Close(); }
            }

            Check("Tính lương P3a §6 #1 (UI PayrollCreateDialog): mặc định tháng/năm nay, tự sinh Tháng 3/2004, lưu thành công và chặn trùng trên hộp",
                defaultsOk && autoNameOk && !string.IsNullOrEmpty(createdId) && dupBlockedUi,
                "def=" + defaultsOk + ", auto=" + autoNameOk + ", id=" + (createdId != null) + ", dup=" + dupBlockedUi);
            return createdId;
        }

        private static void TestPayrollViewPayrollModeUi(
            string db, UserSession adminSess, string periodId, string empAId, string empNegId, string shiftSangId, string shiftToiId, string tag)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1366, 768);
                var view = new PayrollView(adminSess, PayrollMode.Payroll) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool selected = view.SelectPeriodByIdForTest(periodId);
                    Application.DoEvents();
                    DataGridView g = view.GridForTest;

                    // Kiểm không có nút Chi lương ở P3a và thanh nút không cuộn dọc ở 1366x768.
                    bool noPaySalaryBtn = !ContainsControlWithText(view, "Chi lương");
                    bool toolbarNoVScroll = !view.ToolbarFlowForTest.VerticalScroll.Visible
                        && view.SaveButtonForTest.Bottom <= view.ToolbarPanelForTest.Height;

                    // Kiểm cấu trúc lưới: 31 cột ngày (1..31), header "1\nT2" & "7\nCN", cố định Nhân viên/Ca, 6 cột tiền hiện.
                    bool colsOk = g.Columns["colEmployee"] != null && g.Columns["colEmployee"].Frozen
                        && g.Columns["colShift"] != null && g.Columns["colShift"].Frozen
                        && g.Columns["colDay_1"] != null && g.Columns["colDay_1"].HeaderText == "1" + Environment.NewLine + "T2"
                        && g.Columns["colDay_7"] != null && g.Columns["colDay_7"].HeaderText == "7" + Environment.NewLine + "CN"
                        && g.Columns["colDay_31"] != null && g.Columns["colDay_32"] == null;
                    bool sixMoneyVisible = true;
                    foreach (string colName in PayrollView.MoneyColumnNames)
                        if (g.Columns[colName] == null || !g.Columns[colName].Visible)
                            sixMoneyVisible = false;

                    DataGridViewRow rowAFirst = view.FindFirstRowForEmployeeForTest(empAId);
                    DataGridViewRow rowASang = view.FindRowForEmployeeShiftForTest(empAId, shiftSangId);
                    DataGridViewRow rowAToi = view.FindRowForEmployeeShiftForTest(empAId, shiftToiId);
                    DataGridViewRow rowNegFirst = view.FindFirstRowForEmployeeForTest(empNegId);

                    // Kiểm thực nhận âm tô màu t0.Danger ở dòng đầu nhân viên và dòng ca sau để trống cột tiền.
                    DataGridViewRow rowASecond = ReferenceEquals(rowAToi, rowAFirst) ? rowASang : rowAToi;
                    bool negRedOk = rowNegFirst != null
                        && Convert.ToString(rowNegFirst.Cells["colNetSalary"].Value) == "-500.000"
                        && rowNegFirst.Cells["colNetSalary"].Style.ForeColor == t0.Danger
                        && rowASecond != null && rowASecond.Cells["colTotalSalary"].Value == null;

                    // Áp trạng thái trực tiếp cho tập ô: ngày 1,2 Sáng = Đi làm (2 ca), ngày 3 Sáng = Nghỉ có phép (1 ca),
                    // ngày 4 Sáng = Nghỉ không phép (0 ca), ngày 1 Tối = Đi làm (0.5 ca) -> tổng 3.5 ca -> 350.000 (chỉ trong bộ nhớ, DB chưa đổi!).
                    int dbSumBefore = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E",
                        "@I", periodId, "@E", empAId);
                    int dbDetBefore = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I AND DNHANVIENID = @E",
                        "@I", periodId, "@E", empAId);

                    int applied1 = rowASang == null ? 0 : view.ApplyStatusToCellsForTest(
                        new[] { rowASang.Cells["colDay_1"], rowASang.Cells["colDay_2"] }, AttendanceStatus.Present);
                    int applied2 = rowASang == null ? 0 : view.ApplyStatusToCellsForTest(
                        new[] { rowASang.Cells["colDay_3"] }, AttendanceStatus.PaidLeave);
                    int applied3 = rowASang == null ? 0 : view.ApplyStatusToCellsForTest(
                        new[] { rowASang.Cells["colDay_4"] }, AttendanceStatus.UnpaidLeave);
                    int applied4 = rowAToi == null ? 0 : view.ApplyStatusToCellsForTest(
                        new[] { rowAToi.Cells["colDay_1"] }, AttendanceStatus.Present);
                    Application.DoEvents();

                    bool cellColorsT0Ok = rowASang != null && rowAToi != null
                        && rowASang.Cells["colDay_1"].Style.BackColor == PayrollView.AttendanceBackColor(t0, AttendanceStatus.Present, false)
                        && rowASang.Cells["colDay_3"].Style.BackColor == PayrollView.AttendanceBackColor(t0, AttendanceStatus.PaidLeave, false)
                        && rowASang.Cells["colDay_4"].Style.BackColor == PayrollView.AttendanceBackColor(t0, AttendanceStatus.UnpaidLeave, false)
                        && rowAToi.Cells["colDay_1"].Style.BackColor == PayrollView.AttendanceBackColor(t0, AttendanceStatus.Present, false);

                    bool inMemoryCalcOk = rowAFirst != null
                        && Convert.ToString(rowAFirst.Cells["colTotalSalary"].Value) == "350.000"
                        && Convert.ToString(rowAFirst.Cells["colNetSalary"].Value) == "350.000"
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId) == dbSumBefore
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId) == dbDetBefore;

                    // Chọn ô ngày 1 của NV A -> cập nhật trực tiếp nhãn thống kê và so khớp chính xác chuỗi định dạng vi-VN (3,5 ca, 3 ngày).
                    if (rowASang != null)
                    {
                        g.CurrentCell = rowASang.Cells["colDay_1"];
                        view.UpdateSelectedEmployeeStats();
                    }
                    string statsText = view.EmployeeStatsLabelForTest.Text ?? "";
                    string expectedStats = PayrollRules.FormatEmployeeStats("An UI TL " + tag, 3.5m, 3);
                    bool empStatsOk = string.Equals(statsText, expectedStats, StringComparison.Ordinal)
                        && statsText.IndexOf("Số ca: 3,5", StringComparison.Ordinal) >= 0
                        && statsText.IndexOf("Số ngày: 3", StringComparison.Ordinal) >= 0;

                    // Đổi bảng màu trực tiếp (live theme) -> kiểm màu thật trên ô lưới và chữ âm cập nhật sang t1.
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool liveThemeOk = rowASang != null && rowNegFirst != null
                        && PayrollView.AttendanceBackColor(t1, AttendanceStatus.Present, false) != PayrollView.AttendanceBackColor(t0, AttendanceStatus.Present, false)
                        && rowASang.Cells["colDay_1"].Style.BackColor == PayrollView.AttendanceBackColor(t1, AttendanceStatus.Present, false)
                        && rowASang.Cells["colDay_3"].Style.BackColor == PayrollView.AttendanceBackColor(t1, AttendanceStatus.PaidLeave, false)
                        && rowASang.Cells["colDay_4"].Style.BackColor == PayrollView.AttendanceBackColor(t1, AttendanceStatus.UnpaidLeave, false)
                        && rowNegFirst.Cells["colNetSalary"].Style.ForeColor == t1.Danger;

                    // Nút Xóa (0) và Đi làm (1) qua sự kiện nút thật trên ô đang chọn.
                    bool clearAndReapplyOk = false;
                    if (rowASang != null && rowAFirst != null)
                    {
                        g.ClearSelection();
                        rowASang.Cells["colDay_4"].Selected = true;
                        view.ClearCellButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool clearedToEmpty = rowASang.Cells["colDay_4"].Style.BackColor == PayrollView.AttendanceBackColor(t1, AttendanceStatus.Empty, false)
                            && string.IsNullOrEmpty(Convert.ToString(rowASang.Cells["colDay_4"].Value));

                        g.ClearSelection();
                        rowASang.Cells["colDay_4"].Selected = true;
                        view.PresentButtonForTest.PerformClick(); // thêm 1 ca -> 4.5 ca -> 450.000
                        Application.DoEvents();
                        clearAndReapplyOk = clearedToEmpty
                            && rowASang.Cells["colDay_4"].Style.BackColor == PayrollView.AttendanceBackColor(t1, AttendanceStatus.Present, false)
                            && Convert.ToString(rowAFirst.Cells["colTotalSalary"].Value) == "450.000";
                    }

                    // Cảnh báo khi đóng còn thay đổi chưa lưu.
                    bool dirtyBeforeSave = view.IsDirtyForTest;
                    view.ConfirmUnsavedOverrideForTest = delegate { return false; };
                    bool blockedCloseWhenDirty = !view.ConfirmClose() && view.UnsavedPromptTriggeredForTest;

                    // Bấm Cập nhật thật -> ghi DB, hết dirty.
                    view.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool savedOk = view.LastSaveResultForTest != null && view.LastSaveResultForTest.Ok && !view.IsDirtyForTest
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId) == 5;

                    // Đổi LUONGCA NV A -> 200.000 rồi bấm Tải lại (xác nhận true) -> lưới đổi thành 900.000, giữ chấm công.
                    BookingExec(db, "UPDATE DNHANVIEN SET LUONGCA = 200000 WHERE ID = @I", "@I", empAId);
                    bool reloadAsked = false;
                    view.ConfirmReloadOverrideForTest = delegate(string msg) { reloadAsked = msg.IndexOf("Tải lại", StringComparison.Ordinal) >= 0; return true; };
                    view.ReloadButtonForTest.PerformClick();
                    Application.DoEvents();
                    DataGridViewRow rowAFirstAfterReload = view.FindFirstRowForEmployeeForTest(empAId);
                    DataGridViewRow rowASangAfterReload = view.FindRowForEmployeeShiftForTest(empAId, shiftSangId);
                    bool reloadUiOk = reloadAsked && rowAFirstAfterReload != null && rowASangAfterReload != null
                        && Convert.ToString(rowAFirstAfterReload.Cells["colBaseSalary"].Value) == "200.000"
                        && Convert.ToString(rowAFirstAfterReload.Cells["colTotalSalary"].Value) == "900.000"
                        && Convert.ToString(rowASangAfterReload.Cells["colDay_1"].Value) == "✓";

                    Check("Tính lương P3a §6 #6/#8 & §7 (UI chế độ Tính lương): 31 cột ngày, cố định NV/Ca, màu ô & đổi theme thật, tính bộ nhớ trước khi lưu, chữ đỏ thực nhận âm, cảnh báo đóng, Lưu, Tải lại, không nút Chi lương, không cuộn dọc thanh nút",
                        selected && noPaySalaryBtn && toolbarNoVScroll && colsOk && sixMoneyVisible
                        && negRedOk && applied1 == 2 && applied2 == 1 && applied3 == 1 && applied4 == 1
                        && cellColorsT0Ok && inMemoryCalcOk && empStatsOk && liveThemeOk
                        && clearAndReapplyOk && dirtyBeforeSave && blockedCloseWhenDirty && savedOk && reloadUiOk,
                        SingleLine("sel=" + selected + ", noChi=" + noPaySalaryBtn + ", noVScr=" + toolbarNoVScroll
                            + ", cols=" + colsOk + ", m6=" + sixMoneyVisible + ", neg=" + negRedOk
                            + ", clr0=" + cellColorsT0Ok + ", mem=" + inMemoryCalcOk + ", stats=" + empStatsOk
                            + ", thm=" + liveThemeOk + ", btn=" + clearAndReapplyOk + ", dirty=" + dirtyBeforeSave + "/" + blockedCloseWhenDirty
                            + ", save=" + savedOk + ", rld=" + reloadUiOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestPayrollViewAttendanceModeUi(
            string db, UserSession adminSess, string periodId, string empAId, string shiftSangId)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new PayrollView(adminSess, PayrollMode.Attendance) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SelectPeriodByIdForTest(periodId);
                    Application.DoEvents();
                    DataGridView g = view.GridForTest;

                    bool moneyHiddenAndEmpty = true;
                    foreach (string colName in PayrollView.MoneyColumnNames)
                    {
                        if (g.Columns[colName] != null && g.Columns[colName].Visible)
                            moneyHiddenAndEmpty = false;
                        foreach (DataGridViewRow r in g.Rows)
                        {
                            if (g.Columns[colName] != null && r.Cells[colName].Value != null)
                                moneyHiddenAndEmpty = false;
                        }
                    }
                    bool reloadHidden = !view.ReloadButtonForTest.Visible;

                    int sumBefore = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                    decimal tongBefore = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                        "SELECT TONGLUONG FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));

                    DataGridViewRow rowASang = view.FindRowForEmployeeShiftForTest(empAId, shiftSangId);
                    int applied = rowASang == null ? 0 : view.ApplyStatusToCellsForTest(new[] { rowASang.Cells["colDay_5"] }, AttendanceStatus.Present);
                    view.SaveButtonForTest.PerformClick();
                    Application.DoEvents();

                    int sumAfter = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                    decimal tongAfter = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                        "SELECT TONGLUONG FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));
                    int detAfter = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId);

                    Check("Tính lương P3a §6 #7 (UI chế độ Chấm công): ẩn và không nạp 6 cột tiền, ẩn Tải lại, Cập nhật chỉ ghi TBANGLUONGCHITIET không đổi TBANGLUONGTONGHOP",
                        moneyHiddenAndEmpty && reloadHidden && applied == 1
                        && view.LastSaveResultForTest != null && view.LastSaveResultForTest.Ok
                        && sumAfter == sumBefore && tongAfter == tongBefore && detAfter == 6,
                        SingleLine("hidM=" + moneyHiddenAndEmpty + ", hidRld=" + reloadHidden
                            + ", app=" + applied + ", sum=" + sumBefore + "->" + sumAfter
                            + ", tong=" + tongBefore + "->" + tongAfter + ", det=" + detAfter));
                }
                finally { host.Close(); }
            }
        }

        private static void TestPayrollViewReadOnlyUi(string db, string periodId, string empAId, string shiftSangId)
        {
            var viewPerms = new FunctionPermissions(false);
            viewPerms.Add("fn_tl_ro", PayrollText.PayrollFunctionName, RoleModes.View);
            var viewSess = new UserSession
            {
                UserId = "u_tl_ro", UserName = "u_tl_ro", DisplayName = "Chỉ xem TL",
                IsAdmin = false, DatabasePath = db, Permissions = viewPerms
            };

            using (var host = MakeOffscreenHost())
            {
                var view = new PayrollView(viewSess, PayrollMode.Payroll) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SelectPeriodByIdForTest(periodId);
                    Application.DoEvents();

                    bool allLocked = !view.AddButtonForTest.Enabled
                        && !view.DeleteButtonForTest.Enabled
                        && !view.PresentButtonForTest.Enabled
                        && !view.PaidLeaveButtonForTest.Enabled
                        && !view.UnpaidLeaveButtonForTest.Enabled
                        && !view.ClearCellButtonForTest.Enabled
                        && !view.ReloadButtonForTest.Enabled
                        && !view.SaveButtonForTest.Enabled;

                    DataGridViewRow r = view.FindRowForEmployeeShiftForTest(empAId, shiftSangId);
                    int applied = r == null ? -1 : view.ApplyStatusToCellsForTest(new[] { r.Cells["colDay_10"] }, AttendanceStatus.Present);

                    Check("Tính lương P3a §6 #10 (UI quyền chỉ Xem): khóa toàn bộ nút Thêm/Xóa/chấm công/Tải lại/Cập nhật và không cho sửa ô",
                        allLocked && applied == 0 && !view.IsDirtyForTest,
                        SingleLine("locked=" + allLocked + ", applied=" + applied + ", dirty=" + view.IsDirtyForTest));
                }
                finally { host.Close(); }
            }
        }

        private static bool ContainsControlWithText(Control root, string text)
        {
            if (root == null)
                return false;
            if (!string.IsNullOrEmpty(root.Text) && root.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (Control c in root.Controls)
            {
                if (ContainsControlWithText(c, text))
                    return true;
            }
            return false;
        }
    }
}
