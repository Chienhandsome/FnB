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
        private static void StockTakeUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA = DbHelper.NewId();
            string whB = DbHelper.NewId();
            string unitOdd = DbHelper.NewId();
            string unitEven = DbHelper.NewId();
            string prodCountedId = DbHelper.NewId();
            string prodUncountedPosId = DbHelper.NewId();
            string prodUncountedZeroId = DbHelper.NewId();
            string prodRecipeId = DbHelper.NewId();
            var createdOrderIds = new List<string>();
            string xlsListPath = Path.Combine(Path.GetTempPath(), "kazuko-stocktake-list-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsUncountedPath = Path.Combine(Path.GetTempPath(), "kazuko-stocktake-uncounted-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')", "@I", storeA, "@N", "CH KK UI A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')", "@I", storeB, "@N", "CH KK UI B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_kk', @T)", "@I", whA, "@N", "Kho KK A " + tag, "@S", storeA, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_kk', @T)", "@I", whB, "@N", "Kho KK B " + tag, "@S", storeB, "@T", now);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whA, "@I", storeA);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whB, "@I", storeB);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')", "@I", unitOdd, "@N", "Chai " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')", "@I", unitEven, "@N", "Thùng " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 24, 240000, 300000, 30, 'selftest_kk', @T)",
                    "@I", prodCountedId, "@C", "KKUI-1-" + tag, "@N", "Nước Cam " + tag, "@U1", unitOdd, "@U2", unitEven, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U1, 10000, 15000, 30, 'selftest_kk', @T)",
                    "@I", prodUncountedPosId, "@C", "KKUI-2-" + tag, "@N", "Trà Đào Chưa Kiểm " + tag, "@U1", unitOdd, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, 5000, 10000, 30, 'selftest_kk', @T)",
                    "@I", prodUncountedZeroId, "@C", "KKUI-3-" + tag, "@N", "Ly Tồn Bằng 0 " + tag, "@U1", unitOdd, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '1', @U1, 0, 35000, 30, 'selftest_kk', @T)",
                    "@I", prodRecipeId, "@C", "KKUI-4-" + tag, "@N", "Món Pha Chế " + tag, "@U1", unitOdd, "@T", now);

                // Seed tồn kho whA: prodCounted=50, prodUncountedPos=15, prodUncountedZero=0.
                createdOrderIds.Add(InsertStockOrderLine(db, "PN-KKUI-1-" + tag, 1, 30, 30, whA, null, storeA, prodCountedId, unitOdd, 50m, 0m));
                createdOrderIds.Add(InsertStockOrderLine(db, "PN-KKUI-2-" + tag, 1, 30, 30, whA, null, storeA, prodUncountedPosId, unitOdd, 15m, 0m));

                var adminSess = new UserSession
                {
                    UserId = "selftest_kk", UserName = "selftest_kk", DisplayName = "Admin KK UI",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH KK UI A " + tag }
                };

                var svc = new StockTakeService(adminSess);
                var docA = new StockTakeRow
                {
                    Name = "KK-UIA-" + tag, Date = DateTime.Today, WarehouseId = whA,
                    Description = "Kiểm kê kho Ánh Dương " + tag, Note = "Ghi chú KK A " + tag,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodCountedId, ProductName = "Nước Cam " + tag, UnitId = unitOdd, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 24m, SystemQuantity = 50m, RawActualQuantity = 48m }
                    }
                };
                var docB = new StockTakeRow
                {
                    Name = "KK-UIB-" + tag, Date = DateTime.Today.AddDays(-8), WarehouseId = whB,
                    Description = "Kiểm kê kho Bình Minh " + tag, Note = "Ghi chú KK B " + tag,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodCountedId, ProductName = "Nước Cam " + tag, UnitId = unitOdd, BaseUnitId = unitOdd, BigUnitId = unitEven, QuyDoi = 24m, SystemQuantity = 0m, RawActualQuantity = 3m }
                    }
                };
                if (svc.Save(docA).Ok) createdOrderIds.Add(docA.Id);
                if (svc.Save(docB).Ok) createdOrderIds.Add(docB.Id);

                // 1. Lưới Quản lý kiểm kê (§6 #7): 5 cột không cắt chữ, lọc ngày/kho/tìm không dấu, xuất Excel, chi tiết màu chênh lệch.
                TestStockTakeListViewUi(adminSess, whA, docA.Id, docB.Id, tag, xlsListPath);

                // 2. Phạm vi non-admin (§6 #7) + hộp thoại chỉ đọc khi thiếu quyền Sửa.
                TestStockTakeNonAdminAndReadOnlyUi(db, storeA, docA.Id, docB.Id, tag);

                // 3. Hộp thoại Kiểm kê + nút Mặt hàng chưa kiểm kê + màu ô chênh lệch thật + đổi theme + khóa kho + T2 đổi tồn lúc lưu (§6 #4, #5, #7).
                string dlgSavedId = TestStockTakeEditAndUncountedUi(
                    db, adminSess, storeA, whA, prodCountedId, prodUncountedPosId, prodUncountedZeroId, prodRecipeId,
                    unitOdd, unitEven, tag, xlsUncountedPath, createdOrderIds);
                if (!string.IsNullOrEmpty(dlgSavedId))
                    createdOrderIds.Add(dlgSavedId);

                // 4. Nối menu KHO HÀNG > Kiểm kê kho + phân quyền + preview (§6 #9).
                TestStockTakeMenuAndPreview(db, adminSess);
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
                SafeDeleteById(db, "DMATHANG", prodCountedId);
                SafeDeleteById(db, "DMATHANG", prodUncountedPosId);
                SafeDeleteById(db, "DMATHANG", prodUncountedZeroId);
                SafeDeleteById(db, "DMATHANG", prodRecipeId);
                SafeDeleteById(db, "DDONVITINH", unitOdd);
                SafeDeleteById(db, "DDONVITINH", unitEven);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID IN (@A, @B)", "@A", storeA, "@B", storeB);
                SafeDeleteById(db, "DKHOHANG", whA);
                SafeDeleteById(db, "DKHOHANG", whB);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                SafeDeleteFile(xlsListPath);
                SafeDeleteFile(xlsUncountedPath);
            }
        }

        private static void TestStockTakeListViewUi(
            UserSession adminSess, string whA, string docAId, string docBId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new StockTakeListView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedCols = { "Ghi chú", "Số phiếu", "Ngày", "Kho kiểm kê", "Diễn giải" };
                    bool colsOk = CheckHeadersNoClip(grid, expectedCols);

                    view.SearchBoxForTest.Text = "anh duong " + tag;
                    Application.DoEvents();
                    bool searchOk = grid.Rows.Count == 1 && view.SelectRowByIdForTest(docAId);

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    view.FromDateBoxForTest.Value = DateTime.Today;
                    view.ToDateBoxForTest.Value = DateTime.Today;
                    SelectWarehouseCombo(view.WarehouseComboForTest, whA);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filterOk = view.SelectRowByIdForTest(docAId) && !view.SelectRowByIdForTest(docBId);
                    bool detailLoaded = view.DetailGridForTest.Rows.Count == 1;

                    Check("Kiểm kê §6 #7 (lưới 5 cột không cắt chữ gồm Kho kiểm kê T4, lọc ngày/kho/tìm không dấu, xuất Excel 5 cột): đạt toàn bộ",
                        colsOk && searchOk && filterOk && detailLoaded
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5
                        && HeadersMatch(snap, VisibleGridHeaders(grid)) && XlsSheetContainsSubstring(snap, "KK-UIA-" + tag),
                        SingleLine("cols=" + colsOk + ", search=" + searchOk + ", filter=" + filterOk + ", xlsCols=" + (snap == null ? -1 : snap.ColCount)));
                }
                finally { host.Close(); }
            }
        }

        private static void TestStockTakeNonAdminAndReadOnlyUi(
            string db, string storeA, string docAId, string docBId, string tag)
        {
            var perms = new FunctionPermissions(false);
            perms.Add("fn_kk", StockTakeConstants.FunctionName, RoleModes.View);
            var nonAdminSess = new UserSession
            {
                UserId = "u_kk_a_" + tag, UserName = "u_kk_a", DisplayName = "User KK A",
                IsAdmin = false, DatabasePath = db, Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH KK UI A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH KK UI A " + tag }
            };

            bool scopeOk = false, readOnlyOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new StockTakeListView(nonAdminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool hasA = view.SelectRowByIdForTest(docAId);
                    bool hasB = view.SelectRowByIdForTest(docBId);
                    scopeOk = hasA && !hasB;

                    view.DialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            readOnlyOk = dlg.IsReadOnlyForTest && dlg.NumberBoxForTest.ReadOnly
                                && !dlg.WarehouseComboForTest.Enabled && dlg.DetailGridForTest.ReadOnly
                                && !dlg.SaveButtonForTest.Visible && !dlg.SaveCloseButtonForTest.Visible
                                && dlg.CloseButtonForTest.Visible && dlg.UncountedButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (hasA && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 1, view.GridForTest.CurrentRow.Index);
                }
                finally { host.Close(); }
            }

            Check("Kiểm kê §6 #7 (non-admin chỉ thấy kho cửa hàng mình & mở chỉ đọc khi thiếu quyền Sửa): đạt",
                scopeOk && readOnlyOk,
                SingleLine("scope=" + scopeOk + ", ro=" + readOnlyOk));
        }

        private static string TestStockTakeEditAndUncountedUi(
            string db, UserSession adminSess, string storeA, string whA,
            string prodCountedId, string prodUncountedPosId, string prodUncountedZeroId, string prodRecipeId,
            string unitOdd, string unitEven, string tag, string xlsUncountedPath, List<string> createdOrderIds)
        {
            // Kiểm tra khóa ngày khi thiếu quyền Thay đổi ngày trong các phiếu.
            var permsNoDate = new FunctionPermissions(false);
            permsNoDate.Add("fn_kk", StockTakeConstants.FunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
            bool dateLocked;
            using (var d = new StockTakeEditDialog(new UserSession
            {
                UserId = "u_kk_nodt", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH KK UI A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH KK UI A " + tag }
            }, null, false))
            {
                dateLocked = !d.DateBoxForTest.Enabled && d.DescriptionBoxForTest.Text == StockTakeConstants.DefaultDescription;
            }

            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            string savedId = null;
            bool uncountedHiddenBeforeSave = false, uncountedVisibleAfterSave = false;
            bool whLockedAfterAdd = false, twoUnitEditOk = false;
            bool colorShortOk = false, colorSurpOk = false, colorMatchOk = false, themeLiveOk = false;
            bool uiDriftBlockedOk = false, uiDriftRefreshedOk = false;
            bool uncountedDialogOk = false, uncountedXlsOk = false;
            int dataErrCount = -1;

            using (var dlg = new StockTakeEditDialog(adminSess, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    uncountedHiddenBeforeSave = !dlg.UncountedButtonForTest.Visible;
                    dlg.WarehouseComboForTest.SelectId(whA);

                    // Thêm prodCountedId (tồn hiện tại = 48 do docA đã kiểm về 48): nhập 1 thùng (= 24) => thiếu (-24, đỏ).
                    dlg.SearchBoxForTest.Text = "KKUI-1-" + tag;
                    Application.DoEvents();
                    dlg.QtyBoxForTest.Value = 1m;
                    SelectComboById(dlg.AddUnitComboForTest, unitEven);
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();
                    whLockedAfterAdd = !dlg.WarehouseComboForTest.Enabled;

                    // Kiểm tra màu THẬT của ô thiếu (đỏ), rồi sửa thành 3 thùng (= 72 -> thừa +24, vàng), rồi 2 thùng (= 48 -> khớp 0, bình thường), đổi theme sống.
                    DataGridView dg = dlg.DetailGridForTest;
                    Color shortBg = dg.Rows[0].Cells["d_diff"].InheritedStyle.BackColor;
                    Color shortFg = dg.Rows[0].Cells["d_diff"].InheritedStyle.ForeColor;
                    colorShortOk = shortBg == StockTakeEditDialog.ShortageRowBackColor(t0)
                        && shortFg == StockTakeEditDialog.ShortageRowForeColor(t0)
                        && dlg.LinesForTest[0].ActualQuantityBase == 24m && dlg.LinesForTest[0].Difference == -24m;

                    dlg.CommitDetailCellEditForTest(0, "d_actual", 3m);
                    Application.DoEvents();
                    Color surpBg = dg.Rows[0].Cells["d_diff"].InheritedStyle.BackColor;
                    colorSurpOk = surpBg == StockTakeEditDialog.SurplusRowBackColor(t0)
                        && dlg.LinesForTest[0].ActualQuantityBase == 72m && dlg.LinesForTest[0].Difference == 24m;

                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    themeLiveOk = dlg.BackColor == t1.Background
                        && dg.Rows[0].Cells["d_diff"].InheritedStyle.BackColor == StockTakeEditDialog.SurplusRowBackColor(t1)
                        && StockTakeEditDialog.SurplusRowBackColor(t1) != StockTakeEditDialog.SurplusRowBackColor(t0);

                    dlg.CommitDetailCellEditForTest(0, "d_actual", 2m);
                    Application.DoEvents();
                    twoUnitEditOk = dlg.LinesForTest[0].ActualQuantityBase == 48m && dlg.LinesForTest[0].Difference == 0m;
                    colorMatchOk = dg.Rows[0].DefaultCellStyle.BackColor == Color.Empty;

                    // Mô phỏng tồn hệ thống đổi (+2) trước khi bấm Lưu trên UI (§6 #4).
                    createdOrderIds.Add(InsertStockOrderLine(db, "PN-UIDRIFT-" + tag, 1, 30, 30, whA, null, storeA, prodCountedId, unitOdd, 2m, 0m));
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    uiDriftBlockedOk = string.IsNullOrEmpty(dlg.SavedId)
                        && dlg.LastWarningMessageForTest != null
                        && dlg.LastWarningMessageForTest.IndexOf("Tồn hệ thống đã thay đổi, hãy xem lại", StringComparison.Ordinal) >= 0;
                    uiDriftRefreshedOk = Convert.ToDecimal(dg.Rows[0].Cells["d_sys"].Value) == 50m
                        && Convert.ToDecimal(dg.Rows[0].Cells["d_diff"].Value) == -2m;

                    // Xác nhận và bấm Lưu lần 2 -> lưu thành công, nút Mặt hàng chưa kiểm kê HIỆN RA!
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;
                    uncountedVisibleAfterSave = !string.IsNullOrEmpty(savedId) && dlg.UncountedButtonForTest.Visible;
                    dataErrCount = dlg.DetailDataErrorCountForTest;

                    // Bấm nút "Mặt hàng chưa kiểm kê": phải có prodUncountedPosId (tồn 15), KHÔNG có prodCountedId (đã kiểm), KHÔNG có prodUncountedZeroId (tồn 0), KHÔNG có prodRecipeId.
                    dlg.UncountedDialogOpenerForTest = uDlg =>
                    {
                        uDlg.SuppressPopup = true;
                        ShowDialogOffscreen(uDlg);
                        try
                        {
                            string[] uCols = { "Mã", "Tên", "ĐVT", "Tồn hệ thống" };
                            bool uHeadersOk = CheckHeadersNoClip(uDlg.GridForTest, uCols);
                            bool hasPos = uDlg.ItemsForTest.Exists(x => x.ProductId == prodUncountedPosId && x.SystemQuantity == 15m);
                            bool noCounted = !uDlg.ItemsForTest.Exists(x => x.ProductId == prodCountedId);
                            bool noZero = !uDlg.ItemsForTest.Exists(x => x.ProductId == prodUncountedZeroId);
                            bool noRecipe = !uDlg.ItemsForTest.Exists(x => x.ProductId == prodRecipeId);
                            uncountedDialogOk = uHeadersOk && hasPos && noCounted && noZero && noRecipe;

                            var rec = new GridExportOutputRecorder { NextSavePath = xlsUncountedPath, ConfirmOpenResult = false };
                            uDlg.GridExportOutputForTest = rec;
                            uDlg.ExcelButtonForTest.PerformClick();
                            Application.DoEvents();
                            XlsSheetSnapshot snap = File.Exists(xlsUncountedPath) ? ReadFirstXlsSheet(xlsUncountedPath) : null;
                            uncountedXlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 4
                                && HeadersMatch(snap, VisibleGridHeaders(uDlg.GridForTest))
                                && XlsSheetContainsSubstring(snap, "KKUI-2-" + tag);
                        }
                        finally { uDlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (uncountedVisibleAfterSave)
                        dlg.UncountedButtonForTest.PerformClick();
                }
                finally { dlg.Close(); }
            }

            Check("Kiểm kê §6 #4, #5 & #7 (hộp phiếu, khóa kho & ngày, màu ô chênh lệch thật đỏ/vàng/khớp + đổi theme, 2 ĐVT 0 DataError, T2 đổi tồn trên UI, nút Mặt hàng chưa kiểm kê + Excel): đạt toàn bộ",
                dateLocked && whLockedAfterAdd && twoUnitEditOk && dataErrCount == 0
                && colorShortOk && colorSurpOk && colorMatchOk && themeLiveOk
                && uiDriftBlockedOk && uiDriftRefreshedOk
                && uncountedHiddenBeforeSave && uncountedVisibleAfterSave && uncountedDialogOk && uncountedXlsOk,
                SingleLine("whLk=" + whLockedAfterAdd + ", clr=" + colorShortOk + "/" + colorSurpOk + "/" + themeLiveOk
                    + ", drift=" + uiDriftBlockedOk + "/" + uiDriftRefreshedOk
                    + ", unc=" + uncountedHiddenBeforeSave + "/" + uncountedVisibleAfterSave + "/" + uncountedDialogOk + "/" + uncountedXlsOk));

            return savedId;
        }

        private static void TestStockTakeMenuAndPreview(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode kkNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsStockTakeListMenu(n, adminSess));
            bool opensRealView = false;
            if (kkNode != null)
            {
                using (Control v = ScreenFactory.CreateView(kkNode, adminSess))
                    opensRealView = v is StockTakeListView;
            }

            var deniedSess = new UserSession
            {
                UserId = "u_no_kk", UserName = "u_no_kk", DisplayName = "Không quyền kiểm kê",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            MenuModel deniedMenu = MainMenuBuilder.Build(deniedSess);
            MenuNode deniedNode = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsStockTakeListMenu(n, deniedSess));

            bool previewListOk = false, previewEditOk = false;
            using (Form pv = ScreenFactory.Create("preview.stocktake", adminSess))
            {
                var f = pv as StockTakePreviewForm;
                if (f != null)
                {
                    ShowDialogOffscreen(f);
                    try { f.Preview(""); previewListOk = f.ViewForTest.GridForTest.Columns.Count == 5; }
                    finally { f.Close(); }
                }
            }
            using (Form pv = ScreenFactory.Create("preview.stocktakeedit", adminSess))
            {
                var f = pv as StockTakeEditDialog;
                if (f != null)
                {
                    ShowDialogOffscreen(f);
                    try { f.Preview(""); previewEditOk = f.DetailGridForTest.Columns.Count == 7; }
                    finally { f.Close(); }
                }
            }

            Check("Kiểm kê §6 #9 (nối menu KHO HÀNG > Kiểm kê kho, phân quyền menu & preview.stocktake/stocktakeedit): admin mở StockTakeListView thật, user không quyền ẩn menu",
                kkNode != null && opensRealView && deniedNode == null && previewListOk && previewEditOk,
                SingleLine("node=" + (kkNode != null) + ", view=" + opensRealView + ", denied=" + (deniedNode == null) + ", pv=" + previewListOk + "/" + previewEditOk));
        }

        private static void SelectComboById(ComboBox combo, string id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var li = combo.Items[i] as LookupItem;
                if (li != null && li.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
