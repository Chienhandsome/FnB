using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void VoucherUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string bookingId = DbHelper.NewId();
            var createdVoucherIds = new List<string>();
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-voucher-export-" + Guid.NewGuid().ToString("N") + ".xls");

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tc')",
                    "@I", storeId, "@N", "CH UI " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tc')",
                    "@I", bankId, "@N", "TK UI " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_tc')",
                    "@I", incReasonId, "@N", "LD Thu UI " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 2, 30, 'selftest_tc')",
                    "@I", expReasonId, "@N", "LD Chi UI " + tag);

                var adminSess = new UserSession
                {
                    UserId = "selftest_tc",
                    UserName = "selftest_tc",
                    DisplayName = "Selftest ThuChi",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI " + tag }
                };

                // 1. Dialog Tạo phiếu thu: Ctrl+S (Lưu & Mới) -> tự sinh số tiếp, Ctrl+L (Lưu & Đóng), ghi thật 2 phiếu vào TTHUCHI.
                string id1 = null, id2 = null, num1 = null, num2 = null;
                using (var dlg = new PhieuThuDialog(adminSess, VoucherKind.Income))
                {
                    dlg.SuppressPopup = true;
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        num1 = dlg.NumberBoxForTest.Text;
                        dlg.StoreComboForTest.SelectId(storeId);
                        dlg.ReasonComboForTest.SelectId(incReasonId);
                        dlg.PartyNameBoxForTest.Text = "Nguyễn Văn Thu " + tag;
                        dlg.AmountBoxForTest.Value = 80000m;
                        Application.DoEvents();

                        dlg.DispatchKeyForTest(Keys.Control | Keys.S);
                        Application.DoEvents();
                        id1 = dlg.SavedId;
                        if (id1 != null) createdVoucherIds.Add(id1);
                        num2 = dlg.NumberBoxForTest.Text;

                        dlg.StoreComboForTest.SelectId(storeId);
                        dlg.ReasonComboForTest.SelectId(incReasonId);
                        dlg.PartyNameBoxForTest.Text = "Trần Thị Hai " + tag;
                        dlg.AmountBoxForTest.Value = 120000m;
                        Application.DoEvents();

                        dlg.DispatchKeyForTest(Keys.Control | Keys.L);
                        Application.DoEvents();
                        id2 = dlg.SavedId;
                        if (id2 != null) createdVoucherIds.Add(id2);
                    }
                    finally { dlg.Close(); }
                }
                Check("Phiếu thu/chi UI (PhieuThuDialog thu): Ctrl+S Lưu & Mới sinh số kế tiếp, Ctrl+L Lưu & Đóng ghi thật 2 phiếu",
                    !string.IsNullOrEmpty(id1) && !string.IsNullOrEmpty(id2) && id1 != id2 && num1 != num2,
                    "n1=" + num1 + ", n2=" + num2);

                // 2. Dialog Tạo phiếu chi: ghi thật phiếu chi chuyển khoản (LOAI=1, CHI=30000, CHUYENKHOAN=30).
                string expId = null;
                using (var dlgExp = new PhieuThuDialog(adminSess, VoucherKind.Expense))
                {
                    dlgExp.SuppressPopup = true;
                    ShowDialogOffscreen(dlgExp);
                    try
                    {
                        dlgExp.StoreComboForTest.SelectId(storeId);
                        dlgExp.ReasonComboForTest.SelectId(expReasonId);
                        dlgExp.PartyNameBoxForTest.Text = "Chi điện UI " + tag;
                        dlgExp.AmountBoxForTest.Value = 30000m;
                        dlgExp.TransferCheckForTest.Checked = true;
                        Application.DoEvents();
                        bool bankEnabled = dlgExp.BankComboForTest.Enabled;
                        dlgExp.BankComboForTest.SelectId(bankId);
                        Application.DoEvents();

                        dlgExp.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        expId = dlgExp.SavedId;
                        if (expId != null) createdVoucherIds.Add(expId);

                        VoucherRow expBack = expId == null ? null : new VoucherService(adminSess).LoadById(expId);
                        Check("Phiếu thu/chi UI (PhieuThuDialog chi): bật TK khi tích chuyển khoản, bấm Lưu ghi LOAI=1, CHI=30000",
                            bankEnabled && expBack != null && expBack.Kind == VoucherKind.Expense
                            && expBack.Amount == 30000m && expBack.Transfer && expBack.BankAccountId == bankId,
                            SingleLine(dlgExp.LastWarningForTest ?? "ok"));
                    }
                    finally { dlgExp.Close(); }
                }

                // 3. Khóa ô Ngày khi thiếu quyền "Thay đổi ngày trong phiếu thu chi" và mở khi có quyền.
                var permsNoDate = new FunctionPermissions(false);
                permsNoDate.Add("fn_tc", VoucherConstants.FunctionListVoucher, RoleModes.View | RoleModes.Add);
                var sessNoDate = new UserSession
                {
                    UserId = "u_nodate", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate,
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI " + tag }
                };
                var permsWithDate = new FunctionPermissions(false);
                permsWithDate.Add("fn_tc", VoucherConstants.FunctionListVoucher, RoleModes.View | RoleModes.Add);
                permsWithDate.Add("fn_date", VoucherConstants.FunctionDateChange, RoleModes.View);
                var sessWithDate = new UserSession
                {
                    UserId = "u_date", IsAdmin = false, DatabasePath = db, Permissions = permsWithDate,
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI " + tag }
                };
                bool dateLockedOff, dateUnlockedOn;
                using (var d1 = new PhieuThuDialog(sessNoDate, VoucherKind.Income))
                    dateLockedOff = !d1.DatePickerForTest.Enabled;
                using (var d2 = new PhieuThuDialog(sessWithDate, VoucherKind.Income))
                    dateUnlockedOn = d2.DatePickerForTest.Enabled;
                Check("Phiếu thu/chi UI (quyền đổi ngày): khóa ô Ngày khi thiếu quyền và mở khi có quyền",
                    dateLockedOff && dateUnlockedOn);

                // 4. Seed thêm 1 phiếu cọc gắn TDATHANGID để kiểm Danh mục phiếu thu: lọc, tìm không dấu, xuất Excel, chặn sửa/xóa phiếu cọc.
                string depId = DbHelper.NewId();
                createdVoucherIds.Add(depId);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, TENDOITUONG, THU, CHI, DCUAHANGID, TDATHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, @O, 50000, 0, @S, @B, 30, 'selftest_tc', @T)",
                    "@I", depId, "@N", "PT-COC-UI-" + tag, "@D", DateTime.Today,
                    "@O", "Khách cọc " + tag, "@S", storeId, "@B", bookingId, "@T", DbHelper.Now());

                using (var host = MakeOffscreenHost())
                {
                    var view = new VoucherListView(adminSess, VoucherKind.Income) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        view.SearchBoxForTest.Text = "nguyen van thu " + tag;
                        Application.DoEvents();
                        int searchHits = view.GridForTest.Rows.Count;

                        var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                        view.GridExportOutputForTest = rec;
                        view.ExcelButtonForTest.PerformClick();
                        Application.DoEvents();
                        XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                        view.SearchBoxForTest.Text = "PT-COC-UI-" + tag;
                        Application.DoEvents();
                        bool selDep = view.SelectRowByIdForTest(depId);
                        view.EditButtonForTest.PerformClick();
                        string editBlockMsg = view.LastWarningMessageForTest;
                        view.RemoveButtonForTest.PerformClick();
                        string delBlockMsg = view.LastWarningMessageForTest;
                        bool depIntact = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE ID = @I", "@I", depId) == 1;

                        Check("Phiếu thu/chi §4 #9 (VoucherListView lọc tìm không dấu & xuất Excel): đúng dòng lọc và xuất .xls qua GridExcelExporter",
                            searchHits == 1 && snap != null && rec.PromptSaveCount == 1
                            && snap.RowCount == 2 && XlsSheetContainsSubstring(snap, "Nguyễn Văn Thu " + tag),
                            "hits=" + searchHits + ", xlsRows=" + (snap == null ? -1 : snap.RowCount));

                        Check("Phiếu thu/chi §4 #7 UI (VoucherListView chặn sửa/xóa phiếu cọc): hiện cảnh báo, bản ghi cọc giữ nguyên",
                            selDep && !string.IsNullOrEmpty(editBlockMsg) && !string.IsNullOrEmpty(delBlockMsg) && depIntact,
                            SingleLine(editBlockMsg));
                    }
                    finally { host.Close(); }
                }

                // 5. CatalogView chế độ Lý do thu chi (DLYDOTHUCHI) & Tài khoản ngân hàng (DTAIKHOANNGANHANG) + phân quyền ẩn nút.
                TestFundCatalogViewsUi(db);

                // 6. Menu & phân quyền §4 #11: cả 6 mục menu Quỹ mở màn thật và ẩn khi không có quyền Xem.
                TestFundMenuWiringAndPermissions(db, adminSess);
            }
            finally
            {
                foreach (string vid in createdVoucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestFundCatalogViewsUi(string db)
        {
            var catRepo = new CatalogRepository(db);
            string reasonTableId = catRepo.FindIdByTable("DLYDOTHUCHI");
            string bankTableId = catRepo.FindIdByTable("DTAIKHOANNGANHANG");
            bool reasonAdminOk = false, bankAdminOk = false, reasonViewOnlyOk = false;

            using (var host = MakeOffscreenHost())
            {
                var vReason = new CatalogView(db, reasonTableId, "Danh mục lý do thu chi") { Dock = DockStyle.Fill };
                host.Controls.Add(vReason);
                host.Show();
                Application.DoEvents();
                reasonAdminOk = vReason.IsReasonModeForTest && vReason.AddButtonForTest.Visible
                    && vReason.EditButtonForTest.Visible && vReason.RemoveButtonForTest.Visible;
                host.Close();
            }
            using (var host = MakeOffscreenHost())
            {
                var vBank = new CatalogView(db, bankTableId, "Danh mục tài khoản ngân hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(vBank);
                host.Show();
                Application.DoEvents();
                bankAdminOk = vBank.IsBankAccountModeForTest && vBank.AddButtonForTest.Visible
                    && vBank.EditButtonForTest.Visible && vBank.RemoveButtonForTest.Visible;
                host.Close();
            }

            UserSession prev = AppSession.Current;
            try
            {
                var viewOnlyPerms = new FunctionPermissions(false);
                viewOnlyPerms.Add("fn_ld", VoucherConstants.FunctionReasonCatalog, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_ld_view", IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms };
                using (var host = MakeOffscreenHost())
                {
                    var vReason = new CatalogView(db, reasonTableId, "Danh mục lý do thu chi") { Dock = DockStyle.Fill };
                    host.Controls.Add(vReason);
                    host.Show();
                    Application.DoEvents();
                    reasonViewOnlyOk = vReason.IsReasonModeForTest && !vReason.AddButtonForTest.Visible
                        && !vReason.EditButtonForTest.Visible && !vReason.RemoveButtonForTest.Visible;
                    host.Close();
                }
            }
            finally
            {
                AppSession.Current = prev;
            }

            Check("Phiếu thu/chi §4 #10 UI (CatalogView Lý do thu chi & TK ngân hàng): bật CRUD cho admin và ẩn nút sửa/xóa khi chỉ có quyền Xem",
                reasonAdminOk && bankAdminOk && reasonViewOnlyOk,
                "reason=" + reasonAdminOk + ", bank=" + bankAdminOk + ", viewOnly=" + reasonViewOnlyOk);
        }

        private static void TestFundMenuWiringAndPermissions(string db, UserSession adminSess)
        {
            var menuRepo = new MenuRepository(db);
            List<MenuNode> roots = menuRepo.LoadTree();
            menuRepo.FillFunctionIds(roots);

            MenuNode nTaoThu = null, nTaoChi = null, nDmThu = null, nDmChi = null, nDmLyDo = null, nDmTk = null;
            foreach (MenuNode n in Flatten(roots))
            {
                string title = (n.Title ?? "").Trim();
                if (string.Equals(title, "Tạo phiếu thu", StringComparison.OrdinalIgnoreCase)) nTaoThu = n;
                else if (string.Equals(title, "Tạo phiếu chi", StringComparison.OrdinalIgnoreCase)) nTaoChi = n;
                else if (string.Equals(title, "Danh mục phiếu thu", StringComparison.OrdinalIgnoreCase)) nDmThu = n;
                else if (string.Equals(title, "Danh mục phiếu chi", StringComparison.OrdinalIgnoreCase)) nDmChi = n;
                else if (string.Equals(title, "Danh mục lý do thu chi", StringComparison.OrdinalIgnoreCase)) nDmLyDo = n;
                else if (string.Equals(title, "Danh mục tài khoản ngân hàng", StringComparison.OrdinalIgnoreCase)) nDmTk = n;
            }

            bool taoThuOk = nTaoThu != null && ScreenFactory.IsNewIncomeVoucher(nTaoThu, adminSess);
            bool taoChiOk = nTaoChi != null && ScreenFactory.IsNewExpenseVoucher(nTaoChi, adminSess);

            bool dmThuOk = false, dmChiOk = false, dmLyDoOk = false, dmTkOk = false;
            if (nDmThu != null)
            {
                using (Control c = ScreenFactory.CreateView(nDmThu, adminSess))
                    dmThuOk = c is VoucherListView && ((VoucherListView)c).KindForTest == VoucherKind.Income;
            }
            if (nDmChi != null)
            {
                using (Control c = ScreenFactory.CreateView(nDmChi, adminSess))
                    dmChiOk = c is VoucherListView && ((VoucherListView)c).KindForTest == VoucherKind.Expense;
            }
            if (nDmLyDo != null)
            {
                using (Control c = ScreenFactory.CreateView(nDmLyDo, adminSess))
                    dmLyDoOk = c is CatalogView && ((CatalogView)c).IsReasonModeForTest;
            }
            if (nDmTk != null)
            {
                using (Control c = ScreenFactory.CreateView(nDmTk, adminSess))
                    dmTkOk = c is CatalogView && ((CatalogView)c).IsBankAccountModeForTest;
            }

            Check("Phiếu thu/chi §4 #11 (nối 6 mục menu Quỹ): Tạo thu/chi + Danh mục thu/chi + Lý do thu chi + TK ngân hàng đều mở màn thật",
                taoThuOk && taoChiOk && dmThuOk && dmChiOk && dmLyDoOk && dmTkOk,
                "tThu=" + taoThuOk + ", tChi=" + taoChiOk + ", dmThu=" + dmThuOk + ", dmChi=" + dmChiOk + ", ld=" + dmLyDoOk + ", tk=" + dmTkOk);

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_fund",
                UserName = "u_no_fund",
                DisplayName = "Không quyền quỹ",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });

            bool hiddenWhenNoPerm = true;
            foreach (MenuNode item in new[] { nTaoThu, nTaoChi, nDmThu, nDmChi, nDmLyDo, nDmTk })
            {
                if (item != null && !string.IsNullOrEmpty(item.FunctionId) && MenuTreeContainsId(deniedMenu.Roots, item.Id))
                    hiddenWhenNoPerm = false;
            }
            Check("Phiếu thu/chi §4 #11 (phân quyền menu): user không có quyền Xem không thấy các mục menu Quỹ",
                hiddenWhenNoPerm);
        }
    }
}
