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
        private static void PayrollPayoutUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string shiftId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string seedCashId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string empZeroId = DbHelper.NewId();
            string periodId = DbHelper.NewId();
            var createdVoucherIds = new List<string>();
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cl')",
                    "@I", storeId, "@N", "CH UI CL " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cl')",
                    "@I", bankId, "@N", "TK UI CL " + tag);
                BookingExec(db, "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 100, 30, 'selftest_cl', @T)",
                    "@I", shiftId, "@N", "Ca UI CL " + tag, "@T", now);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 1, 30, 'selftest_cl')",
                    "@I", expReasonId, "@N", "Chi lương UI " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_cl')",
                    "@I", incReasonId, "@N", "Lý do thu UI CL " + tag);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, LATAMUNG, " +
                    "DCUAHANGID, DLYDOTHUCHIID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 0, 0, 0, 0, @S, @R, 5000000, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", seedCashId, "@N", "PT-UI-CL-" + tag, "@D", new DateTime(2004, 3, 1), "@S", storeId, "@R", incReasonId, "@T", now);
                createdVoucherIds.Add(seedCashId);

                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empAId, "@N", "An UI CL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empBId, "@N", "Bình UI CL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empZeroId, "@N", "Cường 0đ CL " + tag, "@T", now);

                BookingExec(db,
                    "INSERT INTO TBANGLUONG (ID, NAME, THANG, NAM, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, 2004, 30, 'selftest_cl', @T)",
                    "@I", periodId, "@N", "Tháng 3/2004 UI " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 1000000, 0, 0, 0, 1000000, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empAId, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 1000000, 0, 0, 0, 1000000, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empBId, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 0, 0, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empZeroId, "@T", now);

                var adminSess = new UserSession
                {
                    UserId = "selftest_cl",
                    UserName = "selftest_cl",
                    DisplayName = "Admin CL UI",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI CL " + tag }
                };

                // 1. Mở hộp Chi lương bằng nút thật trên PayrollView + kiểm nút ở chế độ Chấm công và quyền chỉ Xem.
                TestPayrollViewPayOutButtonAndDialogUi(
                    db, adminSess, periodId, storeId, bankId, expReasonId, incReasonId,
                    empAId, empBId, empZeroId, createdVoucherIds);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string vid in createdVoucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                SafeDeleteById(db, "TBANGLUONG", periodId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empBId);
                SafeDeleteById(db, "DNHANVIEN", empZeroId);
                SafeDeleteById(db, "DCALAMVIEC", shiftId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static void TestPayrollViewPayOutButtonAndDialogUi(
            string db, UserSession adminSess, string periodId, string storeId, string bankId,
            string expReasonId, string incReasonId, string empAId, string empBId, string empZeroId,
            List<string> createdVoucherIds)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            bool openedByRealButton = false;
            bool viewToolbarOk = false;
            bool payoutUiFlowOk = false;
            string uiDetail = "";

            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1366, 768);
                var view = new PayrollView(adminSess, PayrollMode.Payroll) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SelectPeriodByIdForTest(periodId);
                    view.ApplyPayOutButtonState();
                    Application.DoEvents();

                    viewToolbarOk = view.PayOutButtonForTest.Visible
                        && view.PayOutButtonForTest.Enabled
                        && !view.ToolbarFlowForTest.VerticalScroll.Visible
                        && view.PayOutButtonForTest.Bottom <= view.ToolbarPanelForTest.Height;

                    view.PayoutDialogOpenerForTest = dlg =>
                    {
                        openedByRealButton = true;
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            payoutUiFlowOk = RunPayoutDialogInteractions(
                                db, dlg, storeId, bankId, expReasonId, incReasonId,
                                empAId, empBId, empZeroId, t0, t1, createdVoucherIds, out uiDetail);
                        }
                        finally { if (!dlg.IsDisposed) dlg.Close(); }
                        return DialogResult.OK;
                    };

                    // Bấm nút thật "Chi lương" trên PayrollView.
                    view.PayOutButtonForTest.PerformClick();
                    Application.DoEvents();
                }
                finally { host.Close(); }
            }

            // Kiểm chế độ Chấm công ẩn nút Chi lương và quyền chỉ Xem khóa nút Chi lương.
            bool attHidesPayOut = false;
            using (var hostAtt = MakeOffscreenHost())
            {
                var attView = new PayrollView(adminSess, PayrollMode.Attendance) { Dock = DockStyle.Fill, SuppressPopup = true };
                hostAtt.Controls.Add(attView);
                hostAtt.Show();
                Application.DoEvents();
                try
                {
                    attView.SelectPeriodByIdForTest(periodId);
                    attView.ApplyPayOutButtonState();
                    attHidesPayOut = !attView.PayOutButtonForTest.Visible && !attView.PayOutButtonForTest.Enabled;
                }
                finally { hostAtt.Close(); }
            }

            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add("fn_tl_ro_cl", PayrollText.PayrollFunctionName, RoleModes.View);
            var roSess = new UserSession
            {
                UserId = "u_cl_ui_ro", UserName = "u_cl_ui_ro", DisplayName = "Chỉ xem CL UI",
                IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms
            };
            bool roDisablesPayOut = false;
            using (var hostRo = MakeOffscreenHost())
            {
                var roView = new PayrollView(roSess, PayrollMode.Payroll) { Dock = DockStyle.Fill, SuppressPopup = true };
                hostRo.Controls.Add(roView);
                hostRo.Show();
                Application.DoEvents();
                try
                {
                    roView.SelectPeriodByIdForTest(periodId);
                    roView.ApplyPayOutButtonState();
                    roDisablesPayOut = roView.PayOutButtonForTest.Visible && !roView.PayOutButtonForTest.Enabled;
                }
                finally { hostRo.Close(); }
            }

            Check("Chi lương P3b §6 #11-#13 & §7 (UI mở bằng nút thật trên PayrollView, lưới/hàng tổng/màu cảnh báo vượt thực nhận & đổi theme, cảnh báo 0đ/đã có phiếu, tạo/sửa/xóa qua PhieuThuDialog, quyền & Chấm công): đạt",
                openedByRealButton && viewToolbarOk && payoutUiFlowOk && attHidesPayOut && roDisablesPayOut,
                SingleLine("open=" + openedByRealButton + ", tb=" + viewToolbarOk
                    + ", attHide=" + attHidesPayOut + ", roLock=" + roDisablesPayOut + ", " + uiDetail));
        }

        private static bool RunPayoutDialogInteractions(
            string db, PayrollPayoutDialog dlg, string storeId, string bankId,
            string expReasonId, string incReasonId, string empAId, string empBId, string empZeroId,
            ThemeTokens t0, ThemeTokens t1, List<string> createdVoucherIds, out string detail)
        {
            decimal cash0 = ReadStoreCashBalance(db, storeId);
            decimal bank0 = ReadStoreBankNet(db, storeId, bankId);

            // 1. Kiểm cột lưới, hàng tổng ban đầu (2.000.000 / 0 / 2.000.000), thanh nút không cuộn dọc.
            DataGridView g = dlg.GridForTest;
            bool colsOk = g.Columns["colEmployee"] != null && g.Columns["colNetSalary"] != null
                && g.Columns["colPaidAmount"] != null && g.Columns["colRemaining"] != null
                && g.Columns["colVouchers"] != null;
            bool dlgToolbarNoVScroll = !dlg.ToolbarFlowForTest.VerticalScroll.Visible
                && dlg.CreateButtonForTest.Bottom <= dlg.ToolbarPanelForTest.Height;

            DataGridViewRow sum0 = dlg.FindSummaryRowForTest();
            bool sum0Ok = sum0 != null
                && Convert.ToString(sum0.Cells["colNetSalary"].Value) == "2.000.000"
                && Convert.ToString(sum0.Cells["colPaidAmount"].Value) == "0"
                && Convert.ToString(sum0.Cells["colRemaining"].Value) == "2.000.000";

            // 2. Chọn A (Thực nhận 1.000.000, chưa chi): nút Xem/Sửa & Xóa đang tắt; bấm nút Tạo phiếu chi lương thật.
            dlg.SelectEmployeeByIdForTest(empAId);
            bool beforeCreateBtns = dlg.CreateButtonForTest.Enabled
                && !dlg.ViewEditButtonForTest.Enabled
                && !dlg.DeleteButtonForTest.Enabled;

            bool dlgAFieldsOk = false;
            string savedAId = null;
            dlg.VoucherDialogOpenerForTest = vDlg =>
            {
                vDlg.SuppressPopup = true;
                ShowDialogOffscreen(vDlg);
                try
                {
                    bool hasIncReason = false, hasExpReason = false;
                    foreach (object item in vDlg.ReasonComboForTest.Items)
                    {
                        var li = item as LookupItem;
                        if (li == null) continue;
                        if (li.Id == incReasonId) hasIncReason = true;
                        if (li.Id == expReasonId) hasExpReason = true;
                    }

                    // Thử chặn ngày ngoài tháng & chuyển khoản thiếu TK ngay trên PhieuThuDialog thật.
                    vDlg.DatePickerForTest.Value = new DateTime(2004, 4, 2);
                    vDlg.SaveButtonForTest.PerformClick();
                    bool outMonthBlockedUi = vDlg.LastWarningForTest == PayrollPayoutText.MonthLimitBlocked(3, 2004);

                    vDlg.DatePickerForTest.Value = new DateTime(2004, 3, 31);
                    vDlg.TransferCheckForTest.Checked = true;
                    vDlg.BankComboForTest.SelectId(null);
                    vDlg.SaveButtonForTest.PerformClick();
                    bool noBankBlockedUi = vDlg.LastWarningForTest == VoucherConstants.BankAccountMissing;

                    vDlg.TransferCheckForTest.Checked = false;
                    vDlg.StoreComboForTest.SelectId(storeId);
                    vDlg.ReasonComboForTest.SelectId(expReasonId);
                    bool defaultsOk = vDlg.IsSalaryPayoutModeForTest
                        && !vDlg.ObjectTypeComboForTest.Enabled
                        && vDlg.ObjectTypeComboForTest.SelectedIndex == PhieuThuRules.Employee
                        && !vDlg.EmployeeComboForTest.Enabled
                        && !vDlg.PartyNameBoxForTest.Enabled
                        && vDlg.TransferCheckForTest.Visible
                        && vDlg.AmountBoxForTest.Value == 1000000m
                        && vDlg.DescriptionBoxForTest.Text == "Chi tiền lương tháng 3 năm 2004"
                        && !hasIncReason && hasExpReason;

                    vDlg.SaveButtonForTest.PerformClick();
                    savedAId = vDlg.SavedId;
                    if (!string.IsNullOrEmpty(savedAId))
                        createdVoucherIds.Add(savedAId);
                    dlgAFieldsOk = defaultsOk && outMonthBlockedUi && noBankBlockedUi && !string.IsNullOrEmpty(savedAId);
                }
                finally { if (!vDlg.IsDisposed) vDlg.Close(); }
                return DialogResult.OK;
            };

            dlg.CreateButtonForTest.PerformClick();
            DataGridViewRow rowAAfter = dlg.FindRowForEmployeeForTest(empAId);
            bool afterAOk = rowAAfter != null
                && Convert.ToString(rowAAfter.Cells["colPaidAmount"].Value) == "1.000.000"
                && Convert.ToString(rowAAfter.Cells["colRemaining"].Value) == "0"
                && dlg.ViewEditButtonForTest.Enabled && dlg.DeleteButtonForTest.Enabled
                && ReadStoreCashBalance(db, storeId) - cash0 == -1000000m;

            // 3. Chọn B (Thực nhận 1.000.000): tạo phiếu 1 = 600.000 (tiền mặt), kiểm tự chọn lý do chi lương gần nhất (expReasonId).
            dlg.SelectEmployeeByIdForTest(empBId);
            bool latestReasonAutoSelected = false;
            string savedB1Id = null;
            dlg.VoucherDialogOpenerForTest = vDlg =>
            {
                vDlg.SuppressPopup = true;
                ShowDialogOffscreen(vDlg);
                try
                {
                    latestReasonAutoSelected = vDlg.ReasonComboForTest.SelectedId == expReasonId;
                    vDlg.StoreComboForTest.SelectId(storeId);
                    vDlg.AmountBoxForTest.Value = 600000m;
                    vDlg.SaveButtonForTest.PerformClick();
                    savedB1Id = vDlg.SavedId;
                    if (!string.IsNullOrEmpty(savedB1Id))
                        createdVoucherIds.Add(savedB1Id);
                }
                finally { if (!vDlg.IsDisposed) vDlg.Close(); }
                return DialogResult.OK;
            };
            dlg.CreateButtonForTest.PerformClick();

            // Tạo phiếu 2 cho B: hỏi xác nhận "Nhân viên đã có phiếu chi lương 600.000 đ, tạo thêm phiếu?",
            // mặc định 400.000 (Còn lại), sửa thành 700.000 chuyển khoản qua bankId -> vượt thực nhận (1.300.000 > 1.000.000).
            bool askedAdditional = false;
            bool secondDefault400k = false;
            string savedB2Id = null;
            dlg.ConfirmAdditionalPayoutOverrideForTest = msg =>
            {
                askedAdditional = msg == PayrollPayoutText.AlreadyPaidConfirm(600000m);
                return true;
            };
            dlg.VoucherDialogOpenerForTest = vDlg =>
            {
                vDlg.SuppressPopup = true;
                ShowDialogOffscreen(vDlg);
                try
                {
                    secondDefault400k = vDlg.AmountBoxForTest.Value == 400000m;
                    vDlg.StoreComboForTest.SelectId(storeId);
                    vDlg.AmountBoxForTest.Value = 700000m;
                    vDlg.TransferCheckForTest.Checked = true;
                    vDlg.BankComboForTest.SelectId(bankId);
                    vDlg.SaveButtonForTest.PerformClick();
                    savedB2Id = vDlg.SavedId;
                    if (!string.IsNullOrEmpty(savedB2Id))
                        createdVoucherIds.Add(savedB2Id);
                }
                finally { if (!vDlg.IsDisposed) vDlg.Close(); }
                return DialogResult.OK;
            };
            dlg.CreateButtonForTest.PerformClick();

            // Kiểm màu cảnh báo thật trên lưới cho dòng B (Đã chi 1.300.000 > Thực nhận 1.000.000) + đổi theme sống.
            DataGridViewRow rowBOver = dlg.FindRowForEmployeeForTest(empBId);
            bool overColorT0Ok = rowBOver != null
                && Convert.ToString(rowBOver.Cells["colPaidAmount"].Value) == "1.300.000"
                && Convert.ToString(rowBOver.Cells["colRemaining"].Value) == "-300.000"
                && Convert.ToString(rowBOver.Cells["colVouchers"].Value).IndexOf(", ", StringComparison.Ordinal) > 0
                && rowBOver.Cells["colEmployee"].Style.BackColor == PayrollPayoutDialog.OverpaidRowBackColor(t0)
                && rowBOver.Cells["colPaidAmount"].Style.ForeColor == PayrollPayoutDialog.OverpaidForeColor(t0)
                && rowBOver.Cells["colRemaining"].Style.ForeColor == PayrollPayoutDialog.OverpaidForeColor(t0)
                && ReadStoreBankNet(db, storeId, bankId) - bank0 == -700000m;

            ThemeManager.Apply(t1);
            Application.DoEvents();
            bool overColorT1Ok = rowBOver != null
                && PayrollPayoutDialog.OverpaidRowBackColor(t1) != PayrollPayoutDialog.OverpaidRowBackColor(t0)
                && rowBOver.Cells["colEmployee"].Style.BackColor == PayrollPayoutDialog.OverpaidRowBackColor(t1)
                && rowBOver.Cells["colRemaining"].Style.ForeColor == PayrollPayoutDialog.OverpaidForeColor(t1);

            // 4. Cảnh báo khi Thực nhận = 0 và Còn lại = 0 (NV C): từ chối -> không mở hộp; đồng ý -> vẫn mở được hộp chi lương.
            dlg.SelectEmployeeByIdForTest(empZeroId);
            bool askedZeroWarn = false;
            bool zeroVoucherOpenedOnReject = false;
            dlg.ConfirmZeroSalaryOverrideForTest = msg =>
            {
                askedZeroWarn = msg == PayrollPayoutText.ZeroSalaryWarning;
                return false;
            };
            dlg.VoucherDialogOpenerForTest = vDlg => { zeroVoucherOpenedOnReject = true; return DialogResult.Cancel; };
            dlg.CreateButtonForTest.PerformClick();

            bool zeroVoucherOpenedOnAccept = false;
            dlg.ConfirmZeroSalaryOverrideForTest = msg => msg == PayrollPayoutText.ZeroSalaryWarning;
            dlg.VoucherDialogOpenerForTest = vDlg =>
            {
                zeroVoucherOpenedOnAccept = vDlg.IsSalaryPayoutModeForTest && vDlg.AmountBoxForTest.Value == 0m;
                return DialogResult.Cancel;
            };
            dlg.CreateButtonForTest.PerformClick();
            bool zeroWarnOk = askedZeroWarn && !zeroVoucherOpenedOnReject && zeroVoucherOpenedOnAccept;

            // 5. Xem/Sửa phiếu B1 (600.000 -> 500.000) & Xóa phiếu B2 (chuyển khoản 700.000) qua nút thật.
            dlg.SelectEmployeeByIdForTest(empBId);
            dlg.VoucherPickerOverrideForTest = (list, title) => list.Find(v => v.Id == savedB1Id);
            dlg.VoucherDialogOpenerForTest = vDlg =>
            {
                vDlg.SuppressPopup = true;
                ShowDialogOffscreen(vDlg);
                try
                {
                    vDlg.AmountBoxForTest.Value = 500000m;
                    vDlg.SaveButtonForTest.PerformClick();
                }
                finally { if (!vDlg.IsDisposed) vDlg.Close(); }
                return DialogResult.OK;
            };
            dlg.ViewEditButtonForTest.PerformClick();

            dlg.VoucherPickerOverrideForTest = (list, title) => list.Find(v => v.Id == savedB2Id);
            dlg.ConfirmDeleteOverrideForTest = msg => true;
            dlg.DeleteButtonForTest.PerformClick();

            DataGridViewRow rowBFinal = dlg.FindRowForEmployeeForTest(empBId);
            DataGridViewRow sumFinal = dlg.FindSummaryRowForTest();
            bool editAndDelUiOk = rowBFinal != null && sumFinal != null
                && Convert.ToString(rowBFinal.Cells["colPaidAmount"].Value) == "500.000"
                && Convert.ToString(rowBFinal.Cells["colRemaining"].Value) == "500.000"
                && rowBFinal.Cells["colEmployee"].Style.BackColor == t1.Surface
                && Convert.ToString(sumFinal.Cells["colPaidAmount"].Value) == "1.500.000"
                && Convert.ToString(sumFinal.Cells["colRemaining"].Value) == "500.000"
                && ReadStoreCashBalance(db, storeId) - cash0 == -1500000m
                && ReadStoreBankNet(db, storeId, bankId) == bank0;

            detail = "cols=" + colsOk + ", tb=" + dlgToolbarNoVScroll + ", sum0=" + sum0Ok
                + ", preBtn=" + beforeCreateBtns + ", dlgA=" + dlgAFieldsOk + ", afterA=" + afterAOk
                + ", latestR=" + latestReasonAutoSelected + ", addCfm=" + askedAdditional + ", def400=" + secondDefault400k
                + ", overT0=" + overColorT0Ok + ", overT1=" + overColorT1Ok + ", zero=" + zeroWarnOk + ", edDel=" + editAndDelUiOk;

            return colsOk && dlgToolbarNoVScroll && sum0Ok && beforeCreateBtns && dlgAFieldsOk && afterAOk
                && latestReasonAutoSelected && askedAdditional && secondDefault400k
                && overColorT0Ok && overColorT1Ok && zeroWarnOk && editAndDelUiOk;
        }
    }
}
