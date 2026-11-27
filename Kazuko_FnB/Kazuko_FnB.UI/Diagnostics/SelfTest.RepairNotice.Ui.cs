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
        private static void RepairNoticeUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string areaId = DbHelper.NewId();
            string rtVipId = DbHelper.NewId();
            string rtStdId = DbHelper.NewId();
            string roomAId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string rnUnfixedNewId = DbHelper.NewId();
            string rnUnfixedOldId = DbHelper.NewId();
            string rnFixedNewId = DbHelper.NewId();
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-sc-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db,
                    "INSERT INTO DKHUVUC (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_sc', @T)",
                    "@I", areaId, "@N", "Khu SC UI " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DLOAIPHONG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_sc', @T)",
                    "@I", rtVipId, "@N", "Phòng VIP UI " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DLOAIPHONG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_sc', @T)",
                    "@I", rtStdId, "@N", "Phòng Thường UI " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DBAN (ID, NAME, DKHUVUCID, DLOAIPHONGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @A, @LP, 30, 'selftest_sc', @T)",
                    "@I", roomAId, "@N", "Bàn Ánh Dương " + tag, "@A", areaId, "@LP", rtVipId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 30, 30, 'selftest_sc', @T)",
                    "@I", empAId, "@N", "Đỗ Hoàng An " + tag, "@T", DbHelper.Now());

                DateTime dNew = new DateTime(2026, 6, 15);
                DateTime dOld = new DateTime(2025, 3, 10);

                BookingExec(db,
                    "INSERT INTO TSUACHUA (ID, NAME, NGAY, DBANID, DLOAIPHONGID, DNHANVIENID, NOIDUNG, DASUAXONG, CONSUDUNGDUOC, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @B, @LP, @E, @ND, 0, 30, @NT, 30, 'selftest_sc', @T)",
                    "@I", rnUnfixedNewId, "@N", "SC-U1-" + tag, "@D", dNew, "@B", roomAId, "@LP", rtVipId, "@E", empAId,
                    "@ND", "Hỏng ổ cắm đặc biệt " + tag, "@NT", "Ưu tiên cao " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TSUACHUA (ID, NAME, NGAY, DBANID, DLOAIPHONGID, DNHANVIENID, NOIDUNG, DASUAXONG, CONSUDUNGDUOC, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @B, @LP, NULL, @ND, 0, 0, @NT, 30, 'selftest_sc', @T)",
                    "@I", rnUnfixedOldId, "@N", "SC-U2-" + tag, "@D", dOld, "@B", roomAId, "@LP", rtVipId,
                    "@ND", "Gãy chân bàn " + tag, "@NT", "Phiếu năm 2025 " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TSUACHUA (ID, NAME, NGAY, DBANID, DLOAIPHONGID, DNHANVIENID, NOIDUNG, DASUAXONG, CONSUDUNGDUOC, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @B, @LP, @E, @ND, 30, 30, @NT, 30, 'selftest_sc', @T)",
                    "@I", rnFixedNewId, "@N", "SC-F1-" + tag, "@D", dNew.AddDays(1), "@B", roomAId, "@LP", rtVipId, "@E", empAId,
                    "@ND", "Đã thay bóng đèn " + tag, "@NT", "Hoàn tất " + tag, "@T", DbHelper.Now());

                string scTableId = new CatalogRepository(db).FindIdByTable(RepairNoticeText.TableName);

                TestRepairNoticeMenuRouting(db);
                TestRepairNoticeGridFiltersSearchAndExport(db, scTableId, tag, xlsPath);
                dlgSavedId = TestRepairNoticeDialogAutoFillAndDatePerm(db, roomAId, rtVipId, rtStdId, tag);
                TestRepairNoticeReadOnlyAndTheme(db, scTableId, rnUnfixedNewId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (dlgSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgSavedId);
                    SafeDeleteById(db, "TSUACHUA", dlgSavedId);
                }
                SafeDeleteById(db, "TSUACHUA", rnUnfixedNewId);
                SafeDeleteById(db, "TSUACHUA", rnUnfixedOldId);
                SafeDeleteById(db, "TSUACHUA", rnFixedNewId);
                SafeDeleteById(db, "DBAN", roomAId);
                SafeDeleteById(db, "DLOAIPHONG", rtVipId);
                SafeDeleteById(db, "DLOAIPHONG", rtStdId);
                SafeDeleteById(db, "DKHUVUC", areaId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestRepairNoticeMenuRouting(string db)
        {
            var adminSess = new UserSession { UserId = "u_sc_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            var deniedSess = new UserSession { UserId = "u_sc_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
            var permsView = new FunctionPermissions(false);
            permsView.Add("", RepairNoticeText.FunctionName, RoleModes.View);
            var viewSess = new UserSession { UserId = "u_sc_vw", IsAdmin = false, DatabasePath = db, Permissions = permsView };

            MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, RepairNoticeText.FunctionName);
            MenuNode viewNode = FindMenuLeafByTitle(MainMenuBuilder.Build(viewSess).Roots, RepairNoticeText.FunctionName);
            MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, RepairNoticeText.FunctionName);

            bool openedMode = false;
            if (adminNode != null)
            {
                using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                {
                    var cv = created as CatalogView;
                    openedMode = cv != null && cv.IsRepairNoticeModeForTest;
                }
            }
            bool routeNoAccent = ScreenFactory.IsRepairNoticeMenu(new MenuNode { Title = "thong bao sua chua" }, adminSess)
                && ScreenFactory.IsRepairNoticeMenu(new MenuNode { Title = "THÔNG BÁO SỬA CHỮA" }, adminSess)
                && !ScreenFactory.IsRepairNoticeMenu(new MenuNode { Title = "Danh mục mật khẩu wifi" }, adminSess);

            Check("Thông báo sửa chữa §5 #3 (menu & ScreenFactory): Admin và user có quyền thấy menu và mở màn thật, không quyền ẩn, nhận diện không dấu",
                adminNode != null && viewNode != null && deniedNode == null && openedMode && routeNoAccent,
                "adm=" + (adminNode != null) + ", vw=" + (viewNode != null) + ", den=" + (deniedNode == null) + ", mode=" + openedMode + ", route=" + routeNoAccent);
        }

        private static void TestRepairNoticeGridFiltersSearchAndExport(
            string db, string scTableId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, scTableId, RepairNoticeText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressRnPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Số phiếu", "Ngày", "Phòng/Bàn", "Loại phòng", "Nhân viên", "Nội dung", "Đã sửa xong", "Còn sử dụng được", "Ghi chú" };
                    bool colsOk = view.IsRepairNoticeModeForTest && grid.Columns.Count == expected.Length;
                    bool noClip = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClip = false;
                    }

                    // Mặc định: Từ/Đến trống, Chỉ chưa sửa xong tắt -> thấy đủ 3 phiếu seed; sắp chưa sửa xong trước rồi ngày giảm dần.
                    bool defaultFiltersOk = !view.RnFromDateBoxForTest.NullableValue.HasValue
                        && !view.RnToDateBoxForTest.NullableValue.HasValue
                        && !view.RnUnfixedOnlyCheckForTest.Checked;
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    int allTagged = view.VisibleRows;
                    bool sortOk = allTagged == 3
                        && Convert.ToString(grid.Rows[0].Cells["rn_number"].Value) == "SC-U1-" + tag
                        && Convert.ToString(grid.Rows[1].Cells["rn_number"].Value) == "SC-U2-" + tag
                        && Convert.ToString(grid.Rows[2].Cells["rn_number"].Value) == "SC-F1-" + tag;

                    // Tích "Chỉ chưa sửa xong" -> còn 2 dòng (SC-U1, SC-U2).
                    view.RnUnfixedOnlyCheckForTest.Checked = true;
                    Application.DoEvents();
                    int unfixedRows = view.VisibleRows;

                    // Bỏ tích -> lại đủ 3 dòng; lọc Từ/Đến ngày 01/06/2026 - 30/06/2026 -> còn 2 dòng (SC-U1, SC-F1).
                    view.RnUnfixedOnlyCheckForTest.Checked = false;
                    view.RnFromDateBoxForTest.NullableValue = new DateTime(2026, 6, 1);
                    view.RnToDateBoxForTest.NullableValue = new DateTime(2026, 6, 30);
                    view.RnFilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    int juneRows = view.VisibleRows;

                    // Tìm không dấu trên nội dung & Xuất Excel 9 cột.
                    view.SearchBoxForTest.Text = "o cam dac biet " + tag;
                    Application.DoEvents();
                    int searchHits = view.VisibleRows;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    FocusControlReal(host, grid);
                    Message f3Msg = new Message();
                    bool f3Handled = InvokeProcessCmdKey(view, ref f3Msg, Keys.F3);
                    bool searchActive = view.SearchBoxForTest.Inner.Focused || view.SearchBoxForTest.ContainsFocus || host.ActiveControl == view.SearchBoxForTest;

                    Check("Thông báo sửa chữa §5 #3 (lưới 9 cột, thứ tự sắp, lọc Chưa sửa xong & Từ/Đến ngày, tìm không dấu, F3, xuất Excel): đạt",
                        colsOk && noClip && defaultFiltersOk && sortOk
                        && unfixedRows == 2 && juneRows == 2 && searchHits == 1
                        && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 9 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "SC-U1-" + tag),
                        "cols=" + colsOk + ", clip=" + noClip + ", def=" + defaultFiltersOk + ", sort=" + sortOk
                        + ", unfix=" + unfixedRows + ", june=" + juneRows + ", hit=" + searchHits
                        + ", f3=" + searchActive + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount));
                }
                finally { host.Close(); }
            }
        }

        private static string TestRepairNoticeDialogAutoFillAndDatePerm(
            string db, string roomAId, string rtVipId, string rtStdId, string tag)
        {
            string savedId = null;

            // §5 #1 & §6 trên RepairNoticeEditDialog: chọn bàn tự điền Loại phòng, sửa được Loại phòng, bố cục gọn không cuộn dọc.
            using (var dlg = new RepairNoticeEditDialog(AppSession.Current, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 7 && AllLabelsFit(dlg.FieldLabelsForTest);
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;
                    bool defaultsOk = !dlg.FixedCheckForTest.Checked && dlg.UsableCheckForTest.Checked;

                    dlg.RoomComboForTest.SelectId(roomAId);
                    Application.DoEvents();
                    bool autoFilledRoomType = dlg.RoomTypeComboForTest.SelectedId == rtVipId;

                    // Người dùng vẫn sửa được Loại phòng sang loại khác.
                    dlg.RoomTypeComboForTest.SelectId(rtStdId);
                    bool canOverrideRoomType = dlg.RoomTypeComboForTest.SelectedId == rtStdId;
                    dlg.RoomTypeComboForTest.SelectId(rtVipId);

                    dlg.NumberBoxForTest.Text = "SC-DLG-" + tag;
                    dlg.ContentBoxForTest.Text = "Hỏng công tắc điện " + tag;
                    dlg.NoteBoxForTest.Text = "Tạo qua dialog";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    RepairNoticeRow back = savedId == null ? null : new RepairNoticeRepository(db).LoadById(savedId);
                    Check("Thông báo sửa chữa §5 #1/#6 (RepairNoticeEditDialog): chọn bàn tự điền Loại phòng (sửa được), mặc định Còn sử dụng được, gọn không cuộn dọc 1366×768 @150% DPI",
                        noScroll && labelsOk && fits150 && defaultsOk && autoFilledRoomType && canOverrideRoomType
                        && back != null && back.RoomId == roomAId && back.RoomTypeId == rtVipId && !back.IsFixed && back.IsUsable,
                        "noScr=" + noScroll + ", lbl=" + labelsOk + ", fits150=" + fits150 + ", autoRt=" + autoFilledRoomType
                        + ", overRt=" + canOverrideRoomType + ", saved=" + (back != null));
                }
                finally { dlg.Close(); }
            }

            // §5 #3: Khóa ô Ngày khi thiếu quyền "Thay đổi ngày trong các phiếu", mở khi có quyền.
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add("", RepairNoticeText.FunctionName, RoleModes.View | RoleModes.Add);
            var sessNoDate = new UserSession { UserId = "u_sc_nodt", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate };

            var permsWithDate = new FunctionPermissions(false);
            permsWithDate.Add("", RepairNoticeText.FunctionName, RoleModes.View | RoleModes.Add);
            permsWithDate.Add("", RepairNoticeText.FunctionDateChange, RoleModes.View);
            var sessWithDate = new UserSession { UserId = "u_sc_dt", IsAdmin = false, DatabasePath = db, Permissions = permsWithDate };

            bool dateLocked, dateUnlocked;
            using (var d1 = new RepairNoticeEditDialog(sessNoDate, null, false))
                dateLocked = !d1.DatePickerForTest.Enabled;
            using (var d2 = new RepairNoticeEditDialog(sessWithDate, null, false))
                dateUnlocked = d2.DatePickerForTest.Enabled;
            Check("Thông báo sửa chữa §5 #3 (khóa ô Ngày theo quyền 'Thay đổi ngày trong các phiếu'): khóa khi thiếu quyền và mở khi có quyền",
                dateLocked && dateUnlocked,
                "locked=" + dateLocked + ", unlocked=" + dateUnlocked);

            return savedId;
        }

        private static void TestRepairNoticeReadOnlyAndTheme(string db, string scTableId, string sampleId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", RepairNoticeText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_sc_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, scTableId, RepairNoticeText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false, saveBlocked = false;

                    view.RnDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NumberBoxForTest.ReadOnly && !dlg.DatePickerForTest.Enabled
                                && !dlg.RoomComboForTest.Enabled && !dlg.RoomTypeComboForTest.Enabled
                                && !dlg.EmployeeComboForTest.Enabled && dlg.ContentBoxForTest.ReadOnly
                                && !dlg.FixedCheckForTest.Enabled && !dlg.UsableCheckForTest.Enabled
                                && dlg.NoteBoxForTest.ReadOnly;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            saveBlocked = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectRepairNoticeRowByIdForTest(sampleId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    Check("Thông báo sửa chữa §5 #3 (UI chỉ có quyền Xem): ẩn Thêm/Sửa/Xóa, nhấp đôi mở hộp chỉ đọc và chặn lưu",
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
                    Check("Thông báo sửa chữa §6 (đổi bảng màu trực tiếp): màu ô lưới thực tế khớp ThemeTokens",
                        g.BackgroundColor == tB.Surface && cellColorOk,
                        "grid=" + (g.BackgroundColor == tB.Surface) + ", cell=" + cellColorOk);
                    host.Close();
                }
            }
            finally { AppSession.Current = prev; }
        }
    }
}
