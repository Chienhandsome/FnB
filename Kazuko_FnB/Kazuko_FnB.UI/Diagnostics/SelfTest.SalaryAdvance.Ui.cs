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
        private static void SalaryAdvanceUiTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string seedIncomeId = DbHelper.NewId();
            string createdAdv1Id = null;
            string createdAdv2Id = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-tu-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tu')",
                    "@I", storeId, "@N", "CH UI TU " + tag);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 150000, 0, 0, 30, 30, 'selftest_tu', @T)",
                    "@I", empAId, "@N", "Lê Thị Tạm Ứng " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 150000, 0, 0, 30, 30, 'selftest_tu', @T)",
                    "@I", empBId, "@N", "Phạm Văn Khác " + tag, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 2, 30, 'selftest_tu')",
                    "@I", expReasonId, "@N", "Chi tạm ứng UI " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_tu')",
                    "@I", incReasonId, "@N", "Lý do thu UI " + tag);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DCUAHANGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 0, 0, @S, 2000000, 0, 0, 30, 'selftest_tu', @T)",
                    "@I", seedIncomeId, "@N", "PT-UI-TU-" + tag, "@D", DateTime.Today, "@S", storeId, "@T", DbHelper.Now());

                var adminSess = new UserSession
                {
                    UserId = "selftest_tu",
                    UserName = "selftest_tu",
                    DisplayName = "Selftest TamUng",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TU " + tag }
                };

                TestSalaryAdvanceMenuRouting(db, adminSess);

                // #7 UI: Hộp PhieuThuDialog chế độ tạm ứng không có ô chuyển khoản/TK, khóa Loại đối tượng=NV,
                // lý do thu không có trong danh sách, lưu 1 phiếu xong mở mới tự chọn lý do tạm ứng gần nhất.
                using (PhieuThuDialog dlg = PhieuThuDialog.CreateForSalaryAdvance(adminSess, null, false))
                {
                    dlg.SuppressPopup = true;
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        bool noTransfer = !dlg.TransferCheckForTest.Visible && !dlg.BankComboForTest.Visible;
                        bool lockedEmpType = !dlg.ObjectTypeComboForTest.Enabled && dlg.ObjectTypeComboForTest.SelectedIndex == PhieuThuRules.Employee;
                        bool defaultDescOk = dlg.DescriptionBoxForTest != null && dlg.DescriptionBoxForTest.Text == SalaryAdvanceText.DefaultDescription;

                        bool hasIncomeReason = false;
                        bool hasExpenseReason = false;
                        foreach (object item in dlg.ReasonComboForTest.Items)
                        {
                            var li = item as LookupItem;
                            if (li == null) continue;
                            if (li.Id == incReasonId) hasIncomeReason = true;
                            if (li.Id == expReasonId) hasExpenseReason = true;
                        }

                        dlg.StoreComboForTest.SelectId(storeId);
                        dlg.ReasonComboForTest.SelectId(expReasonId);
                        dlg.EmployeeComboForTest.SelectId(empAId);
                        dlg.AmountBoxForTest.Value = 400000m;
                        Application.DoEvents();

                        dlg.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        createdAdv1Id = dlg.SavedId;

                        Check("Tạm ứng lương §4 #7 UI (PhieuThuDialog chế độ tạm ứng): ẩn chuyển khoản/TK, khóa đối tượng NV, loại lý do thu, diễn giải mặc định, lưu thành công",
                            dlg.IsSalaryAdvanceModeForTest && noTransfer && lockedEmpType && defaultDescOk
                            && !hasIncomeReason && hasExpenseReason && !string.IsNullOrEmpty(createdAdv1Id),
                            "noCk=" + noTransfer + ", lockObj=" + lockedEmpType + ", desc=" + defaultDescOk
                            + ", hasInc=" + hasIncomeReason + ", hasExp=" + hasExpenseReason + ", id=" + (createdAdv1Id != null));
                    }
                    finally { dlg.Close(); }
                }

                // Mở phiếu mới lần 2 -> kiểm lý do mặc định là lý do của phiếu LATAMUNG=30 gần nhất, lưu phiếu 2 cho NV B.
                using (PhieuThuDialog dlg2 = PhieuThuDialog.CreateForSalaryAdvance(adminSess, null, false))
                {
                    dlg2.SuppressPopup = true;
                    ShowDialogOffscreen(dlg2);
                    try
                    {
                        bool latestReasonSelected = dlg2.ReasonComboForTest.SelectedId == expReasonId;
                        dlg2.StoreComboForTest.SelectId(storeId);
                        dlg2.EmployeeComboForTest.SelectId(empBId);
                        dlg2.AmountBoxForTest.Value = 250000m;
                        Application.DoEvents();
                        dlg2.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        createdAdv2Id = dlg2.SavedId;

                        Check("Tạm ứng lương §3 UI (lý do mặc định gần nhất): mở phiếu mới tự chọn lý do của phiếu LATAMUNG=30 gần nhất",
                            latestReasonSelected && !string.IsNullOrEmpty(createdAdv2Id),
                            "sel=" + dlg2.ReasonComboForTest.SelectedId + ", exp=" + expReasonId);
                    }
                    finally { dlg2.Close(); }
                }

                // #10 UI: SalaryAdvanceListView lọc (kể cả Từ/Đến ngày), hàng tổng, tìm không dấu, xuất Excel, đổi màu theme, quyền đổi ngày và chỉ Xem.
                TestSalaryAdvanceListViewAndPermissions(db, adminSess, storeId, empAId, createdAdv1Id, tag, xlsPath);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (createdAdv1Id != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", createdAdv1Id);
                    SafeDeleteById(db, "TTHUCHI", createdAdv1Id);
                }
                if (createdAdv2Id != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", createdAdv2Id);
                    SafeDeleteById(db, "TTHUCHI", createdAdv2Id);
                }
                SafeDeleteById(db, "TTHUCHI", seedIncomeId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empBId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestSalaryAdvanceMenuRouting(string db, UserSession adminSess)
        {
            var deniedSess = new UserSession { UserId = "u_tu_den", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
            MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, SalaryAdvanceText.FunctionName);
            MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, SalaryAdvanceText.FunctionName);

            bool openedView = false;
            if (adminNode != null)
            {
                using (Control c = ScreenFactory.CreateView(adminNode, adminSess))
                    openedView = c is SalaryAdvanceListView;
            }
            bool routeNoAccent = ScreenFactory.IsSalaryAdvanceMenu(new MenuNode { Title = "tam ung luong" })
                && ScreenFactory.IsSalaryAdvanceMenu(new MenuNode { Title = "TẠM ỨNG LƯƠNG" })
                && ScreenFactory.IsSalaryAdvanceMenu(new MenuNode { FunctionId = SalaryAdvanceText.FunctionId })
                && !ScreenFactory.IsSalaryAdvanceMenu(new MenuNode { Title = "Thưởng phạt" });

            Check("Tạm ứng lương §4 #10 (menu NHÂN SỰ > Tạm ứng lương & ScreenFactory): Admin thấy và mở màn thật, không quyền ẩn, nhận diện không dấu",
                adminNode != null && deniedNode == null && openedView && routeNoAccent,
                "adm=" + (adminNode != null) + ", den=" + (deniedNode == null) + ", view=" + openedView + ", route=" + routeNoAccent);
        }

        private static void TestSalaryAdvanceListViewAndPermissions(
            string db, UserSession adminSess, string storeId, string empAId, string adv1Id, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new SalaryAdvanceListView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    int bothRows = view.GridForTest.Rows.Count;
                    decimal bothTotal = view.TotalAmountForTest;

                    // Lọc theo nhân viên A.
                    int empIdx = -1;
                    for (int i = 0; i < view.EmployeeComboForTest.Items.Count; i++)
                    {
                        var li = view.EmployeeComboForTest.Items[i] as LookupItem;
                        if (li != null && li.Id == empAId) { empIdx = i; break; }
                    }
                    if (empIdx >= 0)
                        view.EmployeeComboForTest.SelectedIndex = empIdx;
                    Application.DoEvents();
                    int empARows = view.GridForTest.Rows.Count;
                    decimal empATotal = view.TotalAmountForTest;

                    // Tìm không dấu & Xuất Excel.
                    view.EmployeeComboForTest.SelectedIndex = 0;
                    view.SearchBoxForTest.Text = "le thi tam ung " + tag;
                    Application.DoEvents();
                    int searchRows = view.GridForTest.Rows.Count;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    // Lọc Từ/Đến ngày về 02/2020 -> 0 dòng, tổng 0; trả lại tháng hiện tại -> lại thấy dòng tìm.
                    DateTime origFrom = view.FromDateBoxForTest.Value;
                    DateTime origTo = view.ToDateBoxForTest.Value;
                    view.FromDateBoxForTest.Value = new DateTime(2020, 2, 1);
                    view.ToDateBoxForTest.Value = new DateTime(2020, 2, 28);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool emptyDateFilterOk = view.GridForTest.Rows.Count == 0 && view.TotalAmountForTest == 0m;
                    view.FromDateBoxForTest.Value = origFrom;
                    view.ToDateBoxForTest.Value = origTo;
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();

                    // Đổi bảng màu trực tiếp -> kiểm màu ô lưới thật.
                    var themes = new List<ThemeTokens>(BuiltInThemes.All());
                    ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];
                    ThemeManager.Apply(tB);
                    view.ApplyTheme(tB);
                    Application.DoEvents();
                    DataGridView g = view.GridForTest;
                    bool cellColorOk = g.Rows.Count == 0
                        || (g.Rows[0].Cells[0].InheritedStyle.BackColor == tB.Surface
                            && g.Rows[0].Cells[0].InheritedStyle.SelectionBackColor == tB.Primary);

                    Check("Tạm ứng lương §4 #10 (lưới lọc Từ/Đến ngày & NV, tổng 650k/400k, tìm không dấu, xuất Excel, màu theme thật): đạt",
                        bothRows == 2 && bothTotal == 650000m
                        && empARows == 1 && empATotal == 400000m
                        && searchRows == 1 && emptyDateFilterOk
                        && snap != null && rec.PromptSaveCount == 1 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Lê Thị Tạm Ứng " + tag)
                        && g.BackgroundColor == tB.Surface && cellColorOk,
                        "both=" + bothRows + "(" + bothTotal + "), empA=" + empARows + "(" + empATotal + "), hit=" + searchRows
                        + ", dateEmpty=" + emptyDateFilterOk + ", xls=" + (snap == null ? -1 : snap.RowCount) + ", cellClr=" + cellColorOk);
                }
                finally { host.Close(); }
            }

            // Quyền đổi ngày trên hộp Tạm ứng lương: khóa ô Ngày khi thiếu "Thay đổi ngày trong phiếu thu chi", mở khi có.
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add(SalaryAdvanceText.FunctionId, SalaryAdvanceText.FunctionName, RoleModes.View | RoleModes.Add);
            var sessNoDate = new UserSession
            {
                UserId = "u_tu_nodate",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = permsNoDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH UI TU " + tag } },
                CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TU " + tag }
            };
            var permsWithDate = new FunctionPermissions(false);
            permsWithDate.Add(SalaryAdvanceText.FunctionId, SalaryAdvanceText.FunctionName, RoleModes.View | RoleModes.Add);
            permsWithDate.Add("", VoucherConstants.FunctionDateChange, RoleModes.View);
            var sessWithDate = new UserSession
            {
                UserId = "u_tu_date",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = permsWithDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH UI TU " + tag } },
                CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TU " + tag }
            };
            bool advDateLocked, advDateUnlocked;
            using (PhieuThuDialog d1 = PhieuThuDialog.CreateForSalaryAdvance(sessNoDate, null, false))
                advDateLocked = !d1.DatePickerForTest.Enabled;
            using (PhieuThuDialog d2 = PhieuThuDialog.CreateForSalaryAdvance(sessWithDate, null, false))
                advDateUnlocked = d2.DatePickerForTest.Enabled;
            Check("Tạm ứng lương §3 UI (quyền đổi ngày): khóa ô Ngày khi thiếu quyền đổi ngày phiếu thu chi và mở khi có quyền",
                advDateLocked && advDateUnlocked,
                "locked=" + advDateLocked + ", unlocked=" + advDateUnlocked);

            // Quyền chỉ Xem: ẩn nút Thêm/Sửa/Xóa, nhấp đôi mở hộp chỉ đọc.
            var permsView = new FunctionPermissions(false);
            permsView.Add(SalaryAdvanceText.FunctionId, SalaryAdvanceText.FunctionName, RoleModes.View);
            var sessView = new UserSession
            {
                UserId = "u_tu_ro",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = permsView,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH UI TU " + tag } },
                CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TU " + tag }
            };
            using (var hostRo = MakeOffscreenHost())
            {
                var viewRo = new SalaryAdvanceListView(sessView) { Dock = DockStyle.Fill, SuppressPopup = true };
                hostRo.Controls.Add(viewRo);
                hostRo.Show();
                Application.DoEvents();
                try
                {
                    bool hiddenCrud = !viewRo.AddButtonForTest.Visible && !viewRo.EditButtonForTest.Visible && !viewRo.RemoveButtonForTest.Visible;
                    bool roOpened = false, roNoSave = false;
                    viewRo.DialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest && dlg.IsSalaryAdvanceModeForTest;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (viewRo.SelectRowByIdForTest(adv1Id) && viewRo.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(viewRo.GridForTest, 0, viewRo.GridForTest.CurrentRow.Index);

                    Check("Tạm ứng lương §4 #10 (UI chỉ có quyền Xem): ẩn Thêm/Sửa/Xóa, nhấp đôi mở hộp chỉ đọc",
                        hiddenCrud && roOpened && roNoSave,
                        "hid=" + hiddenCrud + ", ro=" + roOpened + ", noSave=" + roNoSave);
                }
                finally { hostRo.Close(); }
            }
        }
    }
}
