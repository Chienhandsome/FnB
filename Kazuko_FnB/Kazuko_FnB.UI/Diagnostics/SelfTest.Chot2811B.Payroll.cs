using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest mục C của spec docs/specs/chot-2811-b.md (hộp tạo mới bảng lương PayrollCreateDialog).</summary>
    public static partial class SelfTest
    {
        private static void Chot2811BPayrollCreateTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            var createdPeriodIds = new List<string>();
            var adminSess = new UserSession
            {
                UserId = "selftest_2811b",
                UserName = "selftest_2811b",
                DisplayName = "Selftest 2811B",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            var svc = new PayrollService(adminSess, PayrollMode.Payroll);

            try
            {
                // #1: Số phiếu sửa tay được, chặn trống/quá 255, không bị ghi đè khi đã sửa tay, DB lưu đúng tên đã sửa.
                string customName = "BL-CUSTOM-" + tag;
                string savedCustomId = null;
                bool editableOk = false, emptyBlocked = false, keptCustomOnMonthChange = false;

                PayrollCreateResult rOver255 = svc.CreatePeriod(5, 2006, new string('A', 256), "");
                bool over255Blocked = !rOver255.Ok && rOver255.ErrorMessage == PayrollText.NameTooLong;

                using (var dlg = new PayrollCreateDialog(svc, adminSess) { SuppressPopup = true })
                {
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        editableOk = !dlg.NameBoxForTest.ReadOnly;
                        dlg.MonthBoxForTest.Value = 5;
                        dlg.YearBoxForTest.Value = 2006;
                        Application.DoEvents();
                        bool autoFilledBeforeEdit = dlg.NameBoxForTest.Text == "Tháng 5/2006";

                        dlg.NameBoxForTest.Text = "   ";
                        Application.DoEvents();
                        dlg.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        emptyBlocked = dlg.CreatedPeriod == null && dlg.LastErrorForTest == PayrollText.NameRequired;

                        dlg.NameBoxForTest.Text = customName;
                        Application.DoEvents();
                        dlg.MonthBoxForTest.Value = 6;
                        Application.DoEvents();
                        keptCustomOnMonthChange = autoFilledBeforeEdit && dlg.NameBoxForTest.Text == customName;

                        dlg.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        if (dlg.CreatedPeriod != null)
                        {
                            savedCustomId = dlg.CreatedPeriod.Id;
                            createdPeriodIds.Add(savedCustomId);
                        }
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                string dbSavedName = savedCustomId == null ? "" : DbHelper.Read(db, conn =>
                    DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NAME FROM TBANGLUONG WHERE ID = @I", "@I", savedCustomId)) ?? "");

                Check("Chốt 28/11-B §C #1 (Bảng lương sửa Số phiếu tay): sửa được, chặn trống & quá 255, giữ tên tay khi đổi tháng, DB lưu đúng tên đã sửa",
                    editableOk && emptyBlocked && over255Blocked && keptCustomOnMonthChange && dbSavedName == customName,
                    SingleLine("ed=" + editableOk + ", emp=" + emptyBlocked + ", o255=" + over255Blocked + ", keep=" + keptCustomOnMonthChange + ", db=" + dbSavedName));

                // #2: Lưu & Mới tạo đúng 1 bảng, giữ hộp mở về mặc định trống, đóng hộp thấy bảng ở danh sách trái.
                string saveNewName = "BL-NEW-" + tag;
                string saveNewId = null;
                bool stayedOpenResetOk = false, shownInLeftList = false;
                int countBeforeSaveNew = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONG WHERE THANG = 7 AND NAM = 2006");

                using (var host = MakeOffscreenHost())
                {
                    var view = new PayrollView(adminSess, PayrollMode.Payroll) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        view.CreateDialogOpenerForTest = dlg =>
                        {
                            dlg.SuppressPopup = true;
                            ShowDialogOffscreen(dlg);
                            try
                            {
                                dlg.MonthBoxForTest.Value = 7;
                                dlg.YearBoxForTest.Value = 2006;
                                dlg.NameBoxForTest.Text = saveNewName;
                                dlg.NoteBoxForTest.Text = "Ghi chú tạm " + tag;
                                Application.DoEvents();

                                dlg.SaveNewButtonForTest.PerformClick();
                                Application.DoEvents();

                                DateTime today = DateTime.Today;
                                string expectedResetName = PayrollText.FormatPeriodName(today.Month, today.Year);
                                if (dlg.CreatedPeriod != null)
                                {
                                    saveNewId = dlg.CreatedPeriod.Id;
                                    createdPeriodIds.Add(saveNewId);
                                }

                                stayedOpenResetOk = !dlg.IsDisposed && dlg.Visible
                                    && dlg.AnySaved
                                    && (int)dlg.MonthBoxForTest.Value == today.Month
                                    && (int)dlg.YearBoxForTest.Value == today.Year
                                    && dlg.NameBoxForTest.Text == expectedResetName
                                    && dlg.NoteBoxForTest.Text == ""
                                    && !dlg.IsDirtyForTest;

                                dlg.CloseButtonForTest.PerformClick();
                                Application.DoEvents();
                            }
                            finally { if (!dlg.IsDisposed) dlg.Close(); }
                            return DialogResult.Cancel;
                        };

                        view.AddButtonForTest.PerformClick();
                        Application.DoEvents();

                        if (!string.IsNullOrEmpty(saveNewId))
                        {
                            foreach (DataGridViewRow r in view.PeriodGridForTest.Rows)
                            {
                                var p = r.Tag as PayrollPeriodRow;
                                if (p != null && p.Id == saveNewId && p.Name == saveNewName)
                                {
                                    shownInLeftList = true;
                                    break;
                                }
                            }
                        }
                    }
                    finally { host.Close(); }
                }

                int countAfterSaveNew = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONG WHERE THANG = 7 AND NAM = 2006");
                Check("Chốt 28/11-B §C #2 (Bảng lương Lưu & Mới): tạo đúng 1 bảng, hộp giữ mở về mặc định trống, đóng hộp hiện ở danh sách trái",
                    stayedOpenResetOk && countAfterSaveNew == countBeforeSaveNew + 1 && shownInLeftList,
                    SingleLine("reset=" + stayedOpenResetOk + ", dbCnt=" + countBeforeSaveNew + "->" + countAfterSaveNew + ", left=" + shownInLeftList));

                // #3: Lưu & In tạo bảng và mở PayrollPreviewForm đúng kỳ vừa tạo rồi đóng hộp.
                string printName = "BL-PRINT-" + tag;
                string printId = null;
                bool previewOpenedOk = false, dialogClosedOk = false;

                using (var dlg = new PayrollCreateDialog(svc, adminSess) { SuppressPopup = true })
                {
                    dlg.PrintPreviewOpenerForTest = (preview, created) =>
                    {
                        ShowDialogOffscreen(preview);
                        try
                        {
                            previewOpenedOk = preview.ViewForTest != null
                                && preview.ViewForTest.CurrentSheetForTest != null
                                && preview.ViewForTest.CurrentSheetForTest.Period != null
                                && preview.ViewForTest.CurrentSheetForTest.Period.Id == created.Id
                                && preview.ViewForTest.CurrentSheetForTest.Period.Name == printName;
                        }
                        finally { preview.Close(); }
                    };
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        dlg.MonthBoxForTest.Value = 8;
                        dlg.YearBoxForTest.Value = 2006;
                        dlg.NameBoxForTest.Text = printName;
                        Application.DoEvents();

                        dlg.SavePrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        if (dlg.CreatedPeriod != null)
                        {
                            printId = dlg.CreatedPeriod.Id;
                            createdPeriodIds.Add(printId);
                        }
                        dialogClosedOk = dlg.DialogResult == DialogResult.OK && (dlg.IsDisposed || !dlg.Visible);
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                int printDbCount = string.IsNullOrEmpty(printId) ? 0
                    : BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONG WHERE ID = @I AND NAME = @N", "@I", printId, "@N", printName);
                Check("Chốt 28/11-B §C #3 (Bảng lương Lưu & In): lưu DB, mở màn xem/in PayrollPreviewForm đúng bảng vừa tạo và đóng hộp",
                    printDbCount == 1 && previewOpenedOk && dialogClosedOk,
                    SingleLine("db=" + printDbCount + ", pv=" + previewOpenedOk + ", closed=" + dialogClosedOk));

                // #4: Thoát khi đã nhập dữ liệu nhưng chưa lưu -> hỏi đúng câu xác nhận (Không giữ mở, Có đóng).
                bool dirtyAsked = false, dirtyMsgOk = false, stayedOnNo = false, closedOnYes = false;
                using (var dlg = new PayrollCreateDialog(svc, adminSess) { SuppressPopup = true })
                {
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        dlg.NoteBoxForTest.Text = "Thay đổi chưa lưu";
                        Application.DoEvents();

                        dlg.ConfirmDiscardOverrideForTest = msg =>
                        {
                            dirtyAsked = true;
                            dirtyMsgOk = msg == PayrollText.DiscardChangesConfirm;
                            return false;
                        };
                        dlg.CloseButtonForTest.PerformClick();
                        Application.DoEvents();
                        stayedOnNo = !dlg.IsDisposed && dlg.Visible;

                        dlg.ConfirmDiscardOverrideForTest = msg => true;
                        dlg.CloseButtonForTest.PerformClick();
                        Application.DoEvents();
                        closedOnYes = dlg.IsDisposed || !dlg.Visible;
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                Check("Chốt 28/11-B §C #4 (Bảng lương thoát khi có thay đổi): hỏi đúng câu xác nhận, chọn Không giữ hộp mở, chọn Có đóng hộp",
                    dirtyAsked && dirtyMsgOk && stayedOnNo && closedOnYes,
                    SingleLine("asked=" + dirtyAsked + ", msg=" + dirtyMsgOk + ", stay=" + stayedOnNo + ", close=" + closedOnYes));

                // #5: Thoát khi chưa thay đổi -> đóng thẳng không hỏi.
                bool cleanAsked = false, cleanClosed = false;
                using (var dlg = new PayrollCreateDialog(svc, adminSess) { SuppressPopup = true })
                {
                    dlg.ConfirmDiscardOverrideForTest = msg => { cleanAsked = true; return true; };
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        dlg.CloseButtonForTest.PerformClick();
                        Application.DoEvents();
                        cleanClosed = dlg.IsDisposed || !dlg.Visible;
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                Check("Chốt 28/11-B §C #5 (Bảng lương thoát khi chưa đổi): đóng thẳng không hiện hộp hỏi xác nhận",
                    !cleanAsked && cleanClosed,
                    SingleLine("asked=" + cleanAsked + ", closed=" + cleanClosed));
            }
            finally
            {
                foreach (string pid in createdPeriodIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", pid);
                    SafeDeleteById(db, "TBANGLUONG", pid);
                }
            }
        }
    }
}
