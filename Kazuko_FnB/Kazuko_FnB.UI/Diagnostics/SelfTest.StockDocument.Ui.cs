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
        private static void StockDocumentUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA1 = DbHelper.NewId();
            string whA2 = DbHelper.NewId();
            string whB1 = DbHelper.NewId();
            string whB2 = DbHelper.NewId();
            string supp1 = DbHelper.NewId();
            string unitOdd = DbHelper.NewId();
            string unitEven = DbHelper.NewId();
            string prodId = DbHelper.NewId();
            var createdOrderIds = new List<string>();
            string xlsExpPath = Path.Combine(Path.GetTempPath(), "kazuko-stock-exp-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsTrPath = Path.Combine(Path.GetTempPath(), "kazuko-stock-tr-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')", "@I", storeA, "@N", "CH XCK A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')", "@I", storeB, "@N", "CH XCK B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)", "@I", whA1, "@N", "Kho A1 " + tag, "@S", storeA, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)", "@I", whA2, "@N", "Kho A2 " + tag, "@S", storeA, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)", "@I", whB1, "@N", "Kho B1 " + tag, "@S", storeB, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)", "@I", whB2, "@N", "Kho B2 " + tag, "@S", storeB, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')", "@I", supp1, "@N", "NCC Ánh Sáng " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')", "@I", unitOdd, "@N", "Gói " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')", "@I", unitEven, "@N", "Thùng " + tag);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U1, @U2, 12, 120000, 150000, 130000, 30, 'selftest_xck', @T)",
                    "@I", prodId, "@C", "XCK-" + tag, "@N", "Trà Nhài " + tag, "@U1", unitOdd, "@U2", unitEven, "@T", DbHelper.Now());

                var adminSess = new UserSession
                {
                    UserId = "selftest_xck",
                    UserName = "selftest_xck",
                    DisplayName = "Selftest XCK",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH XCK A " + tag }
                };

                // Seed 2 phiếu Xuất khác (whA1 & whB1) và 2 phiếu Chuyển kho (whA1->whA2 & whB1->whB2).
                var expSvc = new StockDocumentService(adminSess, StockDocumentKind.Export);
                var trSvc = new StockDocumentService(adminSess, StockDocumentKind.Transfer);

                var expA = new StockDocumentRow
                {
                    Name = "PX-UIA-" + tag, Date = DateTime.Today, SourceWarehouseId = whA1, SupplierId = supp1,
                    Description = "Xuất mẫu A " + tag, Note = "Ghi chú xuất A " + tag,
                    GiamTheoTien = true, TienGiamGia = 20000m,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodId, ProductName = "Trà Nhài " + tag, UnitId = unitEven, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 2m, UnitPrice = 120000m }
                    }
                };
                var expB = new StockDocumentRow
                {
                    Name = "PX-UIB-" + tag, Date = DateTime.Today.AddDays(-8), SourceWarehouseId = whB1,
                    Description = "Xuất mẫu B " + tag, Note = "Ghi chú xuất B " + tag,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodId, ProductName = "Trà Nhài " + tag, UnitId = unitOdd, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 3m, UnitPrice = 10000m }
                    }
                };
                var trA = new StockDocumentRow
                {
                    Name = "CK-UIA-" + tag, Date = DateTime.Today, SourceWarehouseId = whA1, DestWarehouseId = whA2,
                    Description = "Chuyển mẫu A " + tag, Note = "Ghi chú chuyển A " + tag,
                    TileThue = 10m, PhiVanChuyen = 10000m,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodId, ProductName = "Trà Nhài " + tag, UnitId = unitEven, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 2m, UnitPrice = 100000m }
                    }
                };
                var trB = new StockDocumentRow
                {
                    Name = "CK-UIB-" + tag, Date = DateTime.Today.AddDays(-8), SourceWarehouseId = whB1, DestWarehouseId = whB2,
                    Description = "Chuyển mẫu B " + tag, Note = "Ghi chú chuyển B " + tag,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodId, ProductName = "Trà Nhài " + tag, UnitId = unitOdd, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 12m, RawQuantity = 4m, UnitPrice = 15000m }
                    }
                };

                if (expSvc.Save(expA).Ok) createdOrderIds.Add(expA.Id);
                if (expSvc.Save(expB).Ok) createdOrderIds.Add(expB.Id);
                if (trSvc.Save(trA).Ok) createdOrderIds.Add(trA.Id);
                if (trSvc.Save(trB).Ok) createdOrderIds.Add(trB.Id);

                // 1. Lưới Quản lý xuất kho (8 cột, màu dòng tổng, lọc, xuất Excel) & Quản lý chuyển kho (9 cột, lọc, chi tiết chỉ SLNHAP>0, xuất Excel).
                TestStockListViewsUi(adminSess, whA1, whA2, supp1, expA.Id, trA.Id, tag, xlsExpPath, xlsTrPath);

                // 2. Phạm vi cửa hàng non-admin (cả Xuất khác & Chuyển kho, T4) + hộp thoại chỉ đọc khi không có quyền Sửa.
                TestStockNonAdminAndReadOnlyUi(db, storeA, expA.Id, expB.Id, trA.Id, trB.Id, tag);

                // 3. Hộp thoại Xuất khác (không thuế/phí) & Chuyển kho (có thuế/phí): thêm hàng, 2 ĐVT, suy ngược đơn giá, đổi màu theme, lưu thật 0 DataError, khóa ngày.
                List<string> dlgIds = TestStockEditDialogsUi(db, adminSess, storeA, whA1, whA2, tag);
                createdOrderIds.AddRange(dlgIds);

                // 4. Menu KHO HÀNG > Xuất khác & Chuyển kho mở màn thật và ẩn khi không có quyền.
                TestStockMenuWiringAndPermissions(db, adminSess);
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
                SafeDeleteById(db, "DKHOHANG", whA1);
                SafeDeleteById(db, "DKHOHANG", whA2);
                SafeDeleteById(db, "DKHOHANG", whB1);
                SafeDeleteById(db, "DKHOHANG", whB2);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                SafeDeleteFile(xlsExpPath);
                SafeDeleteFile(xlsTrPath);
            }
        }

        private static void TestStockListViewsUi(
            UserSession adminSess, string whA1, string whA2, string supp1,
            string expAId, string trAId, string tag, string xlsExpPath, string xlsTrPath)
        {
            // Quản lý xuất kho (8 cột).
            using (var host = MakeOffscreenHost())
            {
                var view = new StockDocumentListView(adminSess, StockDocumentKind.Export) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expCols = { "Ghi chú", "Số phiếu", "Ngày", "Tổng cộng", "Tiền giảm giá", "Tiền hàng", "Kho xuất", "Nhân viên xuất" };
                    bool colsOk = CheckHeadersNoClip(grid, expCols);

                    view.SearchBoxForTest.Text = "anh sang " + tag;
                    Application.DoEvents();
                    int rowsFound = grid.Rows.Count;
                    decimal sumVal = rowsFound >= 2 ? Convert.ToDecimal(grid.Rows[rowsFound - 1].Cells["colTongCong"].Value) : -1m;
                    Color expectedSumBg = Draw.Mix(ThemeManager.Current.Surface, ThemeManager.Current.Primary, 0.10f);
                    bool sumColorOk = rowsFound >= 2 && grid.Rows[rowsFound - 1].DefaultCellStyle.BackColor == expectedSumBg;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsExpPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsExpPath) ? ReadFirstXlsSheet(xlsExpPath) : null;

                    view.SearchBoxForTest.Text = "";
                    view.FromDateBoxForTest.Value = DateTime.Today;
                    view.ToDateBoxForTest.Value = DateTime.Today;
                    SelectWarehouseCombo(view.SourceWarehouseComboForTest, whA1);
                    view.SupplierComboForTest.SelectId(supp1);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filteredOk = grid.Rows.Count == 2 && view.SelectRowByIdForTest(expAId);

                    Check("Xuất khác §7 #5 (lưới 8 cột không cắt chữ, màu dòng tổng, lọc ngày/kho/NCC/tìm không dấu, xuất Excel): đúng 8 cột & .xls khớp",
                        colsOk && rowsFound == 2 && sumVal == 220000m && sumColorOk && filteredOk
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 8
                        && HeadersMatch(snap, VisibleGridHeaders(grid)) && XlsSheetContainsSubstring(snap, "PX-UIA-" + tag),
                        SingleLine("cols=" + colsOk + ", sum=" + sumVal + ", bgOk=" + sumColorOk + ", xlsCols=" + (snap == null ? -1 : snap.ColCount)));
                }
                finally { host.Close(); }
            }

            // Quản lý chuyển kho (9 cột, chi tiết dưới chỉ hiện SLNHAP > 0).
            using (var host = MakeOffscreenHost())
            {
                var view = new StockDocumentListView(adminSess, StockDocumentKind.Transfer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] trCols = { "Ghi chú", "Số phiếu", "Ngày", "Tổng cộng", "Kho xuất", "Kho nhập", "Nhân viên xuất", "Nhân viên nhập", "Diễn giải" };
                    bool colsOk = CheckHeadersNoClip(grid, trCols);

                    view.SearchBoxForTest.Text = "chuyen mau a " + tag;
                    Application.DoEvents();
                    int rowsFound = grid.Rows.Count;
                    // 2 * 100.000 = 200.000; thuế 10% = 20.000; phí = 10.000 => TONGCONG = 230.000.
                    decimal sumVal = rowsFound >= 2 ? Convert.ToDecimal(grid.Rows[rowsFound - 1].Cells["colTongCong"].Value) : -1m;
                    Color expectedSumBg = Draw.Mix(ThemeManager.Current.Surface, ThemeManager.Current.Primary, 0.10f);
                    bool sumColorOk = rowsFound >= 2 && grid.Rows[rowsFound - 1].DefaultCellStyle.BackColor == expectedSumBg;

                    // Chọn trA -> lưới chi tiết dưới chỉ có đúng 1 dòng nhập (không hiện dòng xuất đối ứng).
                    bool selTrA = view.SelectRowByIdForTest(trAId);
                    int detailCount = view.DetailGridForTest.Rows.Count;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsTrPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsTrPath) ? ReadFirstXlsSheet(xlsTrPath) : null;

                    view.SearchBoxForTest.Text = "";
                    SelectWarehouseCombo(view.SourceWarehouseComboForTest, whA1);
                    SelectWarehouseCombo(view.DestWarehouseComboForTest, whA2);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filteredOk = grid.Rows.Count == 2 && view.SelectRowByIdForTest(trAId);

                    Check("Chuyển kho §7 #5 (lưới 9 cột không cắt chữ, màu dòng tổng, lọc kho xuất/nhập, chi tiết chỉ SLNHAP>0, xuất Excel): đúng 9 cột & .xls khớp",
                        colsOk && rowsFound == 2 && sumVal == 230000m && sumColorOk && selTrA && detailCount == 1 && filteredOk
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 9
                        && HeadersMatch(snap, VisibleGridHeaders(grid)) && XlsSheetContainsSubstring(snap, "CK-UIA-" + tag),
                        SingleLine("cols=" + colsOk + ", sum=" + sumVal + ", det=" + detailCount + ", xlsCols=" + (snap == null ? -1 : snap.ColCount)));
                }
                finally { host.Close(); }
            }
        }

        private static void TestStockNonAdminAndReadOnlyUi(
            string db, string storeA, string expAId, string expBId, string trAId, string trBId, string tag)
        {
            var perms = new FunctionPermissions(false);
            perms.Add("fn_xk", StockDocumentConstants.ExportFunctionName, RoleModes.View);
            perms.Add("fn_ck", StockDocumentConstants.TransferFunctionName, RoleModes.View);
            var nonAdminSess = new UserSession
            {
                UserId = "u_xck_a_" + tag,
                UserName = "u_xck_a",
                DisplayName = "User Store A",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH XCK A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH XCK A " + tag }
            };

            bool expScopeOk = false, expRoOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new StockDocumentListView(nonAdminSess, StockDocumentKind.Export) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool hasA = view.SelectRowByIdForTest(expAId);
                    bool hasB = view.SelectRowByIdForTest(expBId);
                    expScopeOk = hasA && !hasB;

                    view.DialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            expRoOk = dlg.IsReadOnlyForTest && dlg.NumberBoxForTest.ReadOnly
                                && !dlg.SourceWarehouseComboForTest.Enabled && dlg.DetailGridForTest.ReadOnly
                                && !dlg.SaveButtonForTest.Visible && !dlg.SaveCloseButtonForTest.Visible
                                && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (hasA && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 1, view.GridForTest.CurrentRow.Index);
                }
                finally { host.Close(); }
            }

            bool trScopeOk = false, trRoOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new StockDocumentListView(nonAdminSess, StockDocumentKind.Transfer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool hasA = view.SelectRowByIdForTest(trAId);
                    bool hasB = view.SelectRowByIdForTest(trBId);
                    trScopeOk = hasA && !hasB;

                    view.DialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            trRoOk = dlg.IsReadOnlyForTest && dlg.NumberBoxForTest.ReadOnly
                                && !dlg.SourceWarehouseComboForTest.Enabled && !dlg.DestWarehouseComboForTest.Enabled
                                && dlg.DetailGridForTest.ReadOnly && !dlg.SaveCloseButtonForTest.Visible
                                && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (hasA && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 1, view.GridForTest.CurrentRow.Index);
                }
                finally { host.Close(); }
            }

            Check("Xuất/Chuyển §7 #5 (quyền theo cửa hàng non-admin T4 & hộp thoại chỉ đọc khi thiếu quyền Sửa): chỉ thấy kho cửa hàng mình và nhấp đôi mở chỉ đọc",
                expScopeOk && expRoOk && trScopeOk && trRoOk,
                SingleLine("exp=" + expScopeOk + "/" + expRoOk + ", tr=" + trScopeOk + "/" + trRoOk));
        }

        private static List<string> TestStockEditDialogsUi(
            string db, UserSession adminSess, string storeA, string whA1, string whA2, string tag)
        {
            var savedIds = new List<string>();
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add("fn_xk", StockDocumentConstants.ExportFunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
            var sessNoDate = new UserSession
            {
                UserId = "u_xck_nodt", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH XCK A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH XCK A " + tag }
            };
            bool dateLocked;
            using (var d = new StockDocumentEditDialog(sessNoDate, StockDocumentKind.Export, null, false))
                dateLocked = !d.DateBoxForTest.Enabled;

            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tTarget = themes.Count > 1 ? themes[1] : themes[0];
            ThemeManager.Apply(themes[0]);

            // 1. Export dialog: không có ô thuế/phí trong MoneyBox, không có cột % giảm trên lưới chi tiết, thêm hàng, đổi 2 ĐVT, suy ngược đơn giá, đổi theme, lưu thật 0 DataError.
            bool expNoTaxShip = false, expNoDiscCol = false, expUnitSwitchOk = false, expRevOk = false, expThemeOk = false;
            int expDataErr = -1;
            string expSavedId = null;
            using (var dlg = new StockDocumentEditDialog(adminSess, StockDocumentKind.Export, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    expNoTaxShip = dlg.TileThueBoxForTest.Parent == null && dlg.PhiVanChuyenBoxForTest.Parent == null;
                    expNoDiscCol = dlg.DetailGridForTest.Columns["d_disc"] == null;

                    dlg.SearchBoxForTest.Text = "XCK-" + tag;
                    Application.DoEvents();
                    dlg.QtyBoxForTest.Value = 2m;
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();

                    dlg.CommitDetailCellEditForTest(0, "d_unit", "Gói " + tag);
                    Application.DoEvents();
                    decimal slOdd = dlg.LinesForTest.Count == 1 ? dlg.LinesForTest[0].ConvertedQuantity : -1m;
                    dlg.CommitDetailCellEditForTest(0, "d_unit", "Thùng " + tag);
                    Application.DoEvents();
                    decimal slEven = dlg.LinesForTest.Count == 1 ? dlg.LinesForTest[0].ConvertedQuantity : -1m;
                    expUnitSwitchOk = slOdd == 2m && slEven == 24m;

                    dlg.CommitDetailCellEditForTest(0, "d_amount", 360000m);
                    Application.DoEvents();
                    expRevOk = dlg.LinesForTest.Count == 1 && dlg.LinesForTest[0].UnitPrice == 180000m && dlg.TongCongBoxForTest.Value == 360000m;

                    ThemeManager.Apply(tTarget);
                    Application.DoEvents();
                    expThemeOk = dlg.BackColor == tTarget.Background && dlg.DetailGridForTest.BackgroundColor == tTarget.Surface;

                    dlg.SourceWarehouseComboForTest.SelectId(whA1);
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    expSavedId = dlg.SavedId;
                    expDataErr = dlg.DetailDataErrorCountForTest;
                }
                finally { dlg.Close(); }
            }
            if (!string.IsNullOrEmpty(expSavedId)) savedIds.Add(expSavedId);

            // 2. Transfer dialog: có ô thuế & phí vận chuyển, tính đúng TONGCONG và lưu thật 0 DataError.
            bool trHasTaxShip = false, trCalcOk = false;
            int trDataErr = -1;
            string trSavedId = null;
            using (var dlg = new StockDocumentEditDialog(adminSess, StockDocumentKind.Transfer, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    trHasTaxShip = dlg.TileThueBoxForTest.Parent != null && dlg.PhiVanChuyenBoxForTest.Parent != null;
                    dlg.SearchBoxForTest.Text = "XCK-" + tag;
                    Application.DoEvents();
                    dlg.QtyBoxForTest.Value = 2m;
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();

                    dlg.CommitDetailCellEditForTest(0, "d_price", 100000m);
                    dlg.TileThueBoxForTest.Value = 10m;
                    dlg.PhiVanChuyenBoxForTest.Value = 25000m;
                    Application.DoEvents();
                    // 2 * 100.000 = 200.000; thuế 10% = 20.000; phí = 25.000 => TONGCONG = 245.000.
                    trCalcOk = dlg.TongCongBoxForTest.Value == 245000m;

                    dlg.SourceWarehouseComboForTest.SelectId(whA1);
                    dlg.DestWarehouseComboForTest.SelectId(whA2);
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    trSavedId = dlg.SavedId;
                    trDataErr = dlg.DetailDataErrorCountForTest;
                }
                finally { dlg.Close(); }
            }
            if (!string.IsNullOrEmpty(trSavedId)) savedIds.Add(trSavedId);

            StockDocumentRow savedTr = trSavedId == null ? null : new StockDocumentService(adminSess, StockDocumentKind.Transfer).LoadById(trSavedId);
            Check("Xuất/Chuyển UI (hộp phiếu Xuất không thuế/phí & Chuyển có thuế/phí, 2 ĐVT, suy ngược giá, đổi theme, khóa ngày, lưu thật 0 DataError): đạt toàn bộ",
                dateLocked && expNoTaxShip && expNoDiscCol && expUnitSwitchOk && expRevOk && expThemeOk && expDataErr == 0 && !string.IsNullOrEmpty(expSavedId)
                && trHasTaxShip && trCalcOk && trDataErr == 0 && savedTr != null && savedTr.TongCong == 245000m,
                SingleLine("dt=" + dateLocked + ", expTax=" + expNoTaxShip + ", unit=" + expUnitSwitchOk + ", theme=" + expThemeOk + ", trCalc=" + trCalcOk + ", err=" + expDataErr + "/" + trDataErr));

            return savedIds;
        }

        private static void TestStockMenuWiringAndPermissions(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode expNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsStockExportListMenu(n, adminSess));
            MenuNode trNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsStockTransferListMenu(n, adminSess));

            bool opensExp = false, opensTr = false;
            if (expNode != null)
            {
                using (Control v = ScreenFactory.CreateView(expNode, adminSess))
                    opensExp = v is StockDocumentListView && ((StockDocumentListView)v).Kind == StockDocumentKind.Export;
            }
            if (trNode != null)
            {
                using (Control v = ScreenFactory.CreateView(trNode, adminSess))
                    opensTr = v is StockDocumentListView && ((StockDocumentListView)v).Kind == StockDocumentKind.Transfer;
            }

            var deniedSess = new UserSession
            {
                UserId = "u_no_xck", UserName = "u_no_xck", DisplayName = "Không quyền kho",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            MenuModel deniedMenu = MainMenuBuilder.Build(deniedSess);
            MenuNode deniedExp = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsStockExportListMenu(n, deniedSess));
            MenuNode deniedTr = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsStockTransferListMenu(n, deniedSess));

            Check("Xuất/Chuyển §7 #5 (nối menu KHO HÀNG > Xuất khác & Chuyển kho + phân quyền): admin mở 2 màn thật, user không quyền không thấy menu",
                expNode != null && opensExp && trNode != null && opensTr && deniedExp == null && deniedTr == null,
                SingleLine("exp=" + (expNode != null) + "/" + opensExp + ", tr=" + (trNode != null) + "/" + opensTr + ", denied=" + (deniedExp == null) + "/" + (deniedTr == null)));
        }
    }
}
