using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PurchaseUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA = DbHelper.NewId();
            string whB = DbHelper.NewId();
            string supp1 = DbHelper.NewId();
            string supp2 = DbHelper.NewId();
            string unitOdd = DbHelper.NewId();
            string unitEven = DbHelper.NewId();
            string prodId = DbHelper.NewId();
            var createdOrderIds = new List<string>();
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-purchase-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", storeA, "@N", "CH NK A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", storeB, "@N", "CH NK B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_nk', @T)",
                    "@I", whA, "@N", "Kho A " + tag, "@S", storeA, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_nk', @T)",
                    "@I", whB, "@N", "Kho B " + tag, "@S", storeB, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", supp1, "@N", "NCC Ánh Dương " + tag);
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", supp2, "@N", "NCC Bình Minh " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", unitOdd, "@N", "Gói " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", unitEven, "@N", "Thùng " + tag);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U1, @U2, 12, 120000, 15000, 30, 'selftest_nk', @T)",
                    "@I", prodId, "@C", "UI-" + tag, "@N", "Cà Phê Hạt " + tag, "@U1", unitOdd, "@U2", unitEven, "@T", DbHelper.Now());

                var adminSess = new UserSession
                {
                    UserId = "selftest_nk",
                    UserName = "selftest_nk",
                    DisplayName = "Selftest NhapKho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH NK A " + tag }
                };

                // 1. Seed 2 phiếu trên 2 kho khác cửa hàng (1 phiếu hôm nay ở whA + supp1, 1 phiếu 10 ngày trước ở whB + supp2).
                var adminSvc = new PurchaseService(adminSess);
                var pA = new PurchaseRow
                {
                    Name = "PN-UIA-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whA,
                    SupplierId = supp1,
                    Description = "Nhập đợt 1 " + tag,
                    Note = "Ghi chú A " + tag,
                    PhiVanChuyen = 20000m,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prodId, ProductName = "Cà Phê Hạt " + tag, UnitId = unitEven, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 2m, UnitPrice = 120000m }
                    }
                };
                var pB = new PurchaseRow
                {
                    Name = "PN-UIB-" + tag,
                    Date = DateTime.Today.AddDays(-10),
                    WarehouseId = whB,
                    SupplierId = supp2,
                    Description = "Nhập đợt 2 " + tag,
                    Note = "Ghi chú B " + tag,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prodId, ProductName = "Cà Phê Hạt " + tag, UnitId = unitOdd, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 5m, UnitPrice = 10000m }
                    }
                };
                PurchaseSaveResult rA = adminSvc.Save(pA);
                if (rA.Ok) createdOrderIds.Add(rA.SavedId);
                PurchaseSaveResult rB = adminSvc.Save(pB);
                if (rB.Ok) createdOrderIds.Add(rB.SavedId);

                // 2. §6 #7: PurchaseListView — đúng 12 cột, tiêu đề không cắt chữ, lọc ngày/kho/NCC/tìm không dấu, hàng tổng cộng, xuất Excel.
                TestPurchaseListViewUi(adminSess, whA, supp1, pA.Id, tag, xlsPath);

                // 3. §6 #7: Non-admin chỉ thấy phiếu của kho thuộc cửa hàng mình + ẩn cột tiền khi thiếu quyền Xem giá nhập (chống lộ khi sao chép).
                TestPurchaseNonAdminAndHiddenBuyPrice(db, storeA, pA.Id, pB.Id, tag);

                // 4. PurchaseEditDialog UI: thêm mặt hàng, đổi ĐVT trên lưới, sửa Thành tiền suy ngược Đơn giá, đổi theme khi mở, lưu qua nút thật (0 DataError), quyền đổi ngày.
                string dlgSavedId = TestPurchaseEditDialogUi(db, adminSess, storeA, whA, tag);
                if (!string.IsNullOrEmpty(dlgSavedId))
                    createdOrderIds.Add(dlgSavedId);

                // 5. §6 #8: Menu KHO HÀNG > Nhập hàng vào kho mở PurchaseListView thật và ẩn khi thiếu quyền Xem.
                TestPurchaseMenuWiringAndPermissions(db, adminSess);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string oid in createdOrderIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodId);
                SafeDeleteById(db, "DDONVITINH", unitOdd);
                SafeDeleteById(db, "DDONVITINH", unitEven);
                SafeDeleteById(db, "DNHACUNGCAP", supp1);
                SafeDeleteById(db, "DNHACUNGCAP", supp2);
                SafeDeleteById(db, "DKHOHANG", whA);
                SafeDeleteById(db, "DKHOHANG", whB);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestPurchaseListViewUi(
            UserSession adminSess, string whA, string supp1, string orderAId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new PurchaseListView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedCols =
                    {
                        "Ghi chú", "Số phiếu", "Ngày", "Tổng cộng", "Phí vận chuyển",
                        "Tiền giảm giá", "Tiền thuế", "Tiền hàng", "Nhà cung cấp",
                        "Kho nhập", "Nhân viên nhập", "Diễn giải"
                    };
                    bool colsOk = grid.Columns.Count == expectedCols.Length;
                    bool noHeaderClip = true;
                    for (int i = 0; colsOk && i < expectedCols.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expectedCols[i])
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noHeaderClip = false;
                    }

                    // Tìm không dấu "anh duong <tag>" -> lọc còn 1 phiếu + 1 dòng Tổng cộng = 2 dòng, tổng = 260.000.
                    view.SearchBoxForTest.Text = "anh duong " + tag;
                    Application.DoEvents();
                    int searchRows = grid.Rows.Count;
                    decimal sumTotal = searchRows >= 2 ? Convert.ToDecimal(grid.Rows[searchRows - 1].Cells["colTongCong"].Value) : -1m;

                    // Xuất Excel qua nút thật -> đọc lại file .xls kiểm đủ 12 cột và có số phiếu PN-UIA.
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    // Xóa ô tìm, lọc theo Kho A + NCC 1 + ngày hôm nay -> đúng 1 phiếu pA (+ 1 hàng Tổng cộng).
                    view.SearchBoxForTest.Text = "";
                    view.FromDateBoxForTest.Value = DateTime.Today;
                    view.ToDateBoxForTest.Value = DateTime.Today;
                    for (int i = 0; i < view.WarehouseComboForTest.Items.Count; i++)
                    {
                        var li = view.WarehouseComboForTest.Items[i] as LookupItem;
                        if (li != null && li.Id == whA)
                        {
                            view.WarehouseComboForTest.SelectedIndex = i;
                            break;
                        }
                    }
                    view.SupplierComboForTest.SelectId(supp1);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filteredOk = grid.Rows.Count == 2 && view.SelectRowByIdForTest(orderAId);

                    bool xlsHeadersOk = HeadersMatch(snap, VisibleGridHeaders(grid));
                    Check("Nhập kho §6 #7 (lưới 12 cột, hàng tổng cộng, lọc ngày/kho/NCC/tìm không dấu, xuất Excel): đủ cột không cắt chữ, lọc và xuất .xls đúng",
                        colsOk && noHeaderClip && searchRows == 2 && sumTotal == 260000m && filteredOk
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 12 && xlsHeadersOk
                        && XlsSheetContainsSubstring(snap, "PN-UIA-" + tag),
                        "cols=" + colsOk + ", clip=" + noHeaderClip + ", sum=" + sumTotal + ", xlsCols=" + (snap == null ? -1 : snap.ColCount) + ", xlsHdr=" + xlsHeadersOk);
                }
                finally { host.Close(); }
            }
        }

        private static void TestPurchaseNonAdminAndHiddenBuyPrice(
            string db, string storeA, string orderAId, string orderBId, string tag)
        {
            // User không phải admin, chỉ gán storeA, có quyền Nhập kho (chỉ Xem), KHÔNG có quyền Xem giá nhập.
            var perms = new FunctionPermissions(false);
            perms.Add("fn_nk", PurchaseConstants.FunctionName, RoleModes.View);
            var nonAdminSess = new UserSession
            {
                UserId = "u_store_a_" + tag,
                UserName = "u_store_a",
                DisplayName = "User Store A",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH NK A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH NK A " + tag }
            };

            using (var host = MakeOffscreenHost())
            {
                var view = new PurchaseListView(nonAdminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool hasA = view.SelectRowByIdForTest(orderAId);
                    bool hasB = view.SelectRowByIdForTest(orderBId);

                    bool moneyColsHidden = !grid.Columns["colTongCong"].Visible
                        && !grid.Columns["colPhiVanChuyen"].Visible
                        && !grid.Columns["colTienGiamGia"].Visible
                        && !grid.Columns["colTienThue"].Visible
                        && !grid.Columns["colTienHang"].Visible;

                    // Kiểm chống lộ qua sao chép vùng chọn & ô ẩn phải null (kinh-nghiem.md).
                    bool noLeakInHiddenCell = hasA && grid.CurrentRow != null
                        && grid.CurrentRow.Cells["colTongCong"].Value == null;
                    string copyText = DetailGridMenu.BuildSelectionCopyText(grid) ?? "";
                    bool noLeakInCopy = copyText.IndexOf("260000", StringComparison.Ordinal) < 0
                        && copyText.IndexOf("260.000", StringComparison.Ordinal) < 0;

                    // Nhấp đôi khi chỉ có quyền Xem -> mở hộp thoại chỉ đọc (ẩn các nút Lưu, khóa mọi ô, ẩn khối tiền).
                    bool roOpened = false, roLocked = false, roNoSave = false, roMoneyHidden = false;
                    view.DialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NumberBoxForTest.ReadOnly && !dlg.WarehouseComboForTest.Enabled
                                && dlg.DetailGridForTest.ReadOnly && !dlg.AddProductButtonForTest.Enabled;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveCloseButtonForTest.Visible
                                && !dlg.SaveNewButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            roMoneyHidden = !dlg.MoneyBoxForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (hasA && grid.CurrentRow != null)
                        InvokeGridDoubleClick(grid, 1, grid.CurrentRow.Index);

                    Check("Nhập kho §6 #7 (phạm vi cửa hàng non-admin, ẩn cột tiền & chống lộ sao chép, hộp thoại chỉ đọc): chỉ thấy kho mình, không lộ tiền, mở chỉ đọc",
                        hasA && !hasB && moneyColsHidden && noLeakInHiddenCell && noLeakInCopy
                        && roOpened && roLocked && roNoSave && roMoneyHidden,
                        "A=" + hasA + ", B=" + hasB + ", hide=" + moneyColsHidden + ", ro=" + roOpened + ", copyOk=" + noLeakInCopy);
                }
                finally { host.Close(); }
            }
        }

        private static string TestPurchaseEditDialogUi(
            string db, UserSession adminSess, string storeA, string whA, string tag)
        {
            string savedId = null;

            // 1. Quyền đổi ngày: khóa ô Ngày khi không có quyền "Thay đổi ngày trong các phiếu", mở khi có quyền.
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add("fn_nk", PurchaseConstants.FunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
            var sessNoDate = new UserSession
            {
                UserId = "u_nk_nodate", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH NK A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH NK A " + tag }
            };
            var permsWithDate = new FunctionPermissions(false);
            permsWithDate.Add("fn_nk", PurchaseConstants.FunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
            permsWithDate.Add("fn_dt", PurchaseConstants.FunctionDateChange, RoleModes.View);
            var sessWithDate = new UserSession
            {
                UserId = "u_nk_date", IsAdmin = false, DatabasePath = db, Permissions = permsWithDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH NK A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH NK A " + tag }
            };
            bool dateLocked, dateUnlocked;
            using (var d1 = new PurchaseEditDialog(sessNoDate, null, false))
                dateLocked = !d1.DateBoxForTest.Enabled;
            using (var d2 = new PurchaseEditDialog(sessWithDate, null, false))
                dateUnlocked = d2.DateBoxForTest.Enabled;

            // 2. Mở dialog tạo phiếu thật: chọn mặt hàng, thêm 2 thùng, đổi ĐVT qua lại, sửa Thành tiền -> suy ngược Đơn giá, đổi theme khi mở, bấm Lưu thật.
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tTarget = themes.Count > 1 ? themes[1] : themes[0];
            ThemeManager.Apply(themes[0]);

            bool revPriceOk = false, unitSwitchOk = false, headerClipOk = true, themeColorOk = false;
            int dataErrorCount = -1;
            using (var dlg = new PurchaseEditDialog(adminSess, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    foreach (DataGridViewColumn col in dlg.DetailGridForTest.Columns)
                    {
                        if (!col.Visible) continue;
                        int need = TextRenderer.MeasureText(col.HeaderText, dlg.DetailGridForTest.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > col.Width) headerClipOk = false;
                    }

                    dlg.SearchBoxForTest.Text = "UI-" + tag;
                    Application.DoEvents();
                    dlg.QtyBoxForTest.Value = 2m;
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();

                    // Đổi ĐVT trên lưới chi tiết sang ĐVT lẻ (SLNHAP = 2) rồi đổi lại ĐVT chẵn (SLNHAP = 24).
                    dlg.CommitDetailCellEditForTest(0, "d_unit", "Gói " + tag);
                    Application.DoEvents();
                    decimal slOdd = dlg.LinesForTest.Count == 1 ? dlg.LinesForTest[0].ConvertedQuantity : -1m;
                    dlg.CommitDetailCellEditForTest(0, "d_unit", "Thùng " + tag);
                    Application.DoEvents();
                    decimal slEven = dlg.LinesForTest.Count == 1 ? dlg.LinesForTest[0].ConvertedQuantity : -1m;
                    unitSwitchOk = slOdd == 2m && slEven == 24m;

                    // Sửa Thành tiền từ 240.000 thành 300.000 cho 2 thùng -> Đơn giá phải suy ngược thành 150.000.
                    dlg.CommitDetailCellEditForTest(0, "d_amount", 300000m);
                    Application.DoEvents();
                    revPriceOk = dlg.LinesForTest.Count == 1
                        && dlg.LinesForTest[0].UnitPrice == 150000m
                        && dlg.LinesForTest[0].ConvertedQuantity == 24m
                        && dlg.TongCongBoxForTest.Value == 300000m;

                    // Đổi theme ngay khi hộp thoại đang mở và kiểm tra màu cập nhật theo theme mới.
                    ThemeManager.Apply(tTarget);
                    Application.DoEvents();
                    themeColorOk = dlg.BackColor == tTarget.Background
                        && dlg.DetailGridForTest.BackgroundColor == tTarget.Surface;

                    dlg.WarehouseComboForTest.SelectId(whA);
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;
                    dataErrorCount = dlg.DetailDataErrorCountForTest;
                }
                finally { dlg.Close(); }
            }

            PurchaseRow savedRow = savedId == null ? null : new PurchaseService(adminSess).LoadById(savedId);
            Check("Nhập kho UI (PurchaseEditDialog: đổi ngày, thêm hàng, đổi 2 ĐVT, suy ngược đơn giá, đổi màu theme, lưu thật, 0 DataError): mọi thao tác UI hoạt động chuẩn",
                dateLocked && dateUnlocked && unitSwitchOk && revPriceOk && headerClipOk && themeColorOk
                && dataErrorCount == 0 && savedRow != null && savedRow.TongCong == 300000m,
                "dtLock=" + dateLocked + "/" + dateUnlocked + ", unit=" + unitSwitchOk + ", rev=" + revPriceOk
                + ", theme=" + themeColorOk + ", err=" + dataErrorCount + ", saved=" + (savedRow != null));

            return savedId;
        }

        private static void TestPurchaseMenuWiringAndPermissions(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode adminNode = FindMenuLeafByTitle(adminMenu.Roots, "Nhập hàng vào kho");

            bool opensRealView = false;
            if (adminNode != null)
            {
                using (Control view = ScreenFactory.CreateView(adminNode, adminSess))
                    opensRealView = view is PurchaseListView;
            }

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_nk",
                UserName = "u_no_nk",
                DisplayName = "Không quyền nhập kho",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });
            MenuNode deniedNode = FindMenuLeafByTitle(deniedMenu.Roots, "Nhập hàng vào kho");

            Check("Nhập kho §6 #8 (nối menu KHO HÀNG > Nhập hàng vào kho & phân quyền menu): admin mở PurchaseListView thật, user không quyền không thấy menu",
                adminNode != null && opensRealView && deniedNode == null,
                "adminNode=" + (adminNode != null) + ", realView=" + opensRealView + ", deniedNull=" + (deniedNode == null));
        }
    }
}
