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
        private static void OpeningStockUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA = DbHelper.NewId();
            string whB = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodA = DbHelper.NewId();
            string prodB = DbHelper.NewId();
            string prodZero = DbHelper.NewId();
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-openingstock-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", storeA, "@N", "CH TD UI A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", storeB, "@N", "CH TD UI B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_td', @T)",
                    "@I", whA, "@N", "Kho TD UI A " + tag, "@S", storeA, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_td', @T)",
                    "@I", whB, "@N", "Kho TD UI B " + tag, "@S", storeB, "@T", now);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whA, "@I", storeA);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whB, "@I", storeB);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", unitId, "@N", "Cái " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 10000, 20000, 30, 'selftest_td', @T)",
                    "@I", prodA, "@C", "TDUIA-" + tag, "@N", "Trà Đào Cam Sả " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 20000, 35000, 30, 'selftest_td', @T)",
                    "@I", prodB, "@C", "TDUIB-" + tag, "@N", "Sữa Tươi Thanh Trùng " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 5000, 12000, 30, 'selftest_td', @T)",
                    "@I", prodZero, "@C", "TDUIZ-" + tag, "@N", "Ống Hút Giấy " + tag, "@U", unitId, "@T", now);

                var adminSess = new UserSession
                {
                    UserId = "selftest_td",
                    UserName = "selftest_td",
                    DisplayName = "Admin TD UI",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH TD UI A " + tag }
                };

                TestOpeningStockAdminUi(adminSess, whA, prodA, prodB, prodZero, tag, xlsPath);
                TestOpeningStockPermissionsAndDateLockUi(db, storeA, whA, whB, tag);
                TestOpeningStockMenuAndPreview(db, adminSess);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                CleanupOpeningWarehouseOrders(db, new[] { whA, whB });
                SafeDeleteById(db, "DMATHANG", prodA);
                SafeDeleteById(db, "DMATHANG", prodB);
                SafeDeleteById(db, "DMATHANG", prodZero);
                SafeDeleteById(db, "DDONVITINH", unitId);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID IN (@A, @B)", "@A", storeA, "@B", storeB);
                SafeDeleteById(db, "DKHOHANG", whA);
                SafeDeleteById(db, "DKHOHANG", whB);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestOpeningStockAdminUi(
            UserSession adminSess, string whA, string prodA, string prodB, string prodZero, string tag, string xlsPath)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningStockView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool colsOk = CheckHeadersNoClip(grid, new[] { "Mã hàng", "Mã sẵn có", "Tên mặt hàng", "ĐVT", "Tồn đầu", "Giá vốn", "Thành tiền" });
                    var selWh = view.WarehouseComboForTest.SelectedItem as LookupItem;
                    bool defaultWhAndDateOk = selWh != null && selWh.Id == whA
                        && view.DateBoxForTest.Value.Date == OpeningStockRules.DefaultOpeningDate(DateTime.Today);

                    // Lọc theo tag để cô lập đúng 3 mặt hàng seed (+ 1 hàng Tổng cộng = 4 dòng).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    DataGridViewRow rA = view.FindRowByProductIdForTest(prodA);
                    DataGridViewRow rB = view.FindRowByProductIdForTest(prodB);
                    bool defaultCostOk = rA != null && Convert.ToDecimal(rA.Cells["colCost"].Value) == 10000m
                        && rB != null && Convert.ToDecimal(rB.Cells["colCost"].Value) == 20000m;

                    // Thử nhập tồn đầu âm trên UI -> cảnh báo có tên hàng, không đổi giá trị, 0 DataError.
                    if (rA != null)
                        view.CommitCellEditForTest(rA.Index, "colQty", -5m);
                    bool negWarnOk = view.LastWarningMessageForTest != null
                        && view.LastWarningMessageForTest.IndexOf("Trà Đào Cam Sả " + tag, StringComparison.Ordinal) >= 0
                        && rA != null && Convert.ToDecimal(rA.Cells["colQty"].Value) == 0m;

                    // Nhập số thật qua NumericEditGridCell + nhấn phím Enter thật (PreProcessMessage WM_KEYDOWN VK_RETURN):
                    // Bắt đầu từ dòng dữ liệu đầu (row 0, luôn có dòng kế tiếp row 1 bất kể thứ tự sắp xếp theo tên hay mã).
                    bool enterMoveOk = false;
                    if (grid.Rows.Count >= 4)
                    {
                        FocusControlReal(host, grid);
                        TextBox edQty = BeginGridEditReal(grid, 0, "colQty");
                        if (edQty != null)
                        {
                            TypeCharsIntoGridEditor(edQty, "2.5", true);
                            bool enter1 = PressEnterOnGridReal(grid);
                            bool movedToCost = enter1
                                && !grid.IsCurrentCellInEditMode
                                && Convert.ToDecimal(grid.Rows[0].Cells["colQty"].Value) == 2.5m
                                && grid.CurrentCell != null
                                && grid.CurrentCell.RowIndex == 0
                                && grid.CurrentCell.OwningColumn.Name == "colCost";

                            TextBox edCost = BeginGridEditReal(grid, 0, "colCost");
                            bool enter2 = edCost != null && PressEnterOnGridReal(grid);
                            bool movedToNextQty = enter2
                                && !grid.IsCurrentCellInEditMode
                                && grid.CurrentCell != null
                                && grid.CurrentCell.RowIndex == 1
                                && grid.CurrentCell.OwningColumn.Name == "colQty";

                            bool enterNav = PressEnterOnGridReal(grid);
                            bool navToCost = enterNav
                                && grid.CurrentCell != null
                                && grid.CurrentCell.RowIndex == 1
                                && grid.CurrentCell.OwningColumn.Name == "colCost";

                            grid.CurrentCell = grid.Rows[2].Cells["colCost"];
                            bool enterLast = PressEnterOnGridReal(grid);
                            bool stayOffSummary = enterLast
                                && grid.CurrentCell != null
                                && grid.CurrentCell.RowIndex == 2;

                            enterMoveOk = movedToCost && movedToNextQty && navToCost && stayOffSummary;
                        }
                        view.CommitCellEditForTest(0, "colQty", 0m);
                    }

                    // Nhập tồn đầu hợp lệ: 10 cái @ 10.000 và 5 cái @ 20.000 -> thành tiền 100k + 100k = 200k.
                    if (rA != null && rB != null)
                    {
                        view.CommitCellEditForTest(rA.Index, "colQty", 10m);
                        view.CommitCellEditForTest(rB.Index, "colQty", 5m);
                        Application.DoEvents();
                    }

                    DataGridViewRow sumRow = grid.Rows.Count >= 4 ? grid.Rows[grid.Rows.Count - 1] : null;
                    bool totalsAndBoldOk = rA != null && Convert.ToDecimal(rA.Cells["colAmount"].Value) == 100000m
                        && rB != null && Convert.ToDecimal(rB.Cells["colAmount"].Value) == 100000m
                        && sumRow != null && Convert.ToDecimal(sumRow.Cells["colAmount"].Value) == 200000m
                        && sumRow.Cells["colName"].InheritedStyle.Font.Bold
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == OpeningStockView.SummaryRowBackColor(t0);

                    // Đổi theme sống -> màu thật của ô hàng tổng cộng cập nhật theo theme mới.
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool liveThemeOk = sumRow != null
                        && OpeningStockView.SummaryRowBackColor(t1) != OpeningStockView.SummaryRowBackColor(t0)
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == OpeningStockView.SummaryRowBackColor(t1)
                        && grid.BackgroundColor == t1.Surface;

                    // Cảnh báo khi đóng màn còn thay đổi chưa lưu.
                    bool dirtyBeforeSave = view.IsDirtyForTest;
                    view.ConfirmUnsavedOverrideForTest = delegate { return false; };
                    bool blockedCloseWhenDirty = !view.ConfirmClose() && view.UnsavedPromptTriggeredForTest;

                    // Bấm nút Lưu thật -> ghi DB, hết dirty.
                    view.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool savedUiOk = view.LastSaveResultForTest != null && view.LastSaveResultForTest.Ok
                        && !view.IsDirtyForTest
                        && view.LastSaveResultForTest.TotalAmount == 200000m
                        && view.DataErrorCountForTest == 0;

                    // Lọc tích "Chỉ hiện hàng đã nhập tồn" -> ẩn prodZero, giữ prodA & prodB; tìm không dấu "tra dao cam sa".
                    view.OnlyEnteredCheckForTest.Checked = true;
                    Application.DoEvents();
                    bool onlyEnteredUiOk = view.FindRowByProductIdForTest(prodA) != null
                        && view.FindRowByProductIdForTest(prodB) != null
                        && view.FindRowByProductIdForTest(prodZero) == null;

                    view.SearchBoxForTest.Text = "tra dao cam sa " + tag;
                    Application.DoEvents();
                    bool accentSearchOk = view.FindRowByProductIdForTest(prodA) != null
                        && view.FindRowByProductIdForTest(prodB) == null;

                    // Xuất Excel qua nút thật -> đọc lại file .xls đúng 7 cột đang hiện.
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 7
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "TDUIA-" + tag);

                    Check("Tồn đầu P1 §4 #5 & §5 (UI: 6 cột không cắt chữ, ngày 31/12, giá vốn mặc định, Enter chuyển ô, 0 DataError, màu hàng tổng + đổi theme, cảnh báo đóng chưa lưu, lưu thật, lọc đã nhập/tìm không dấu, xuất Excel)",
                        colsOk && defaultWhAndDateOk && defaultCostOk && negWarnOk && enterMoveOk
                        && totalsAndBoldOk && liveThemeOk && dirtyBeforeSave && blockedCloseWhenDirty
                        && savedUiOk && onlyEnteredUiOk && accentSearchOk && xlsOk,
                        SingleLine("cols=" + colsOk + ", def=" + defaultWhAndDateOk + "/" + defaultCostOk
                            + ", enter=" + enterMoveOk + ", theme=" + liveThemeOk + ", dirty=" + dirtyBeforeSave + "/" + blockedCloseWhenDirty
                            + ", save=" + savedUiOk + ", filt=" + onlyEnteredUiOk + "/" + accentSearchOk + ", xls=" + xlsOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestOpeningStockPermissionsAndDateLockUi(
            string db, string storeA, string whA, string whB, string tag)
        {
            // 1. User chỉ có quyền Xem -> mở chỉ-đọc: ẩn nút Lưu, lưới ReadOnly, khóa ô Ngày, chỉ thấy kho của cửa hàng mình.
            var viewPerms = new FunctionPermissions(false);
            viewPerms.Add("fn_td", OpeningStockConstants.FunctionName, RoleModes.View);
            var viewSess = new UserSession
            {
                UserId = "u_td_ro_" + tag, UserName = "u_td_ro", DisplayName = "Chỉ xem TD",
                IsAdmin = false, DatabasePath = db, Permissions = viewPerms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH A" } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH A" }
            };

            bool roOk = false, whScopeOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningStockView(viewSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    roOk = view.IsReadOnlyForTest && !view.SaveButtonForTest.Visible
                        && view.GridForTest.ReadOnly
                        && !view.DateBoxForTest.Enabled && view.DateBoxForTest.Inner.ReadOnly;

                    bool hasA = false, hasB = false;
                    foreach (object item in view.WarehouseComboForTest.Items)
                    {
                        var li = item as LookupItem;
                        if (li == null) continue;
                        if (li.Id == whA) hasA = true;
                        if (li.Id == whB) hasB = true;
                    }
                    whScopeOk = hasA && !hasB;
                }
                finally { host.Close(); }
            }

            // 2. User có quyền Sửa nhưng KHÔNG có quyền "Thay đổi ngày trong các phiếu" -> nút Lưu hiện, ô Ngày bị khóa.
            var editNoDatePerms = new FunctionPermissions(false);
            editNoDatePerms.Add("fn_td", OpeningStockConstants.FunctionName, RoleModes.View | RoleModes.Edit);
            var editNoDateSess = new UserSession
            {
                UserId = "u_td_nd_" + tag, UserName = "u_td_nd", DisplayName = "Sửa không đổi ngày",
                IsAdmin = false, DatabasePath = db, Permissions = editNoDatePerms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH A" } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH A" }
            };
            bool editDateLockedOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningStockView(editNoDateSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    editDateLockedOk = !view.IsReadOnlyForTest && view.SaveButtonForTest.Visible
                        && !view.GridForTest.ReadOnly
                        && !view.DateBoxForTest.Enabled && view.DateBoxForTest.Inner.ReadOnly;
                }
                finally { host.Close(); }
            }

            // 3. User có thêm quyền "Thay đổi ngày trong các phiếu" -> ô Ngày mở khóa.
            var editDatePerms = new FunctionPermissions(false);
            editDatePerms.Add("fn_td", OpeningStockConstants.FunctionName, RoleModes.View | RoleModes.Edit);
            editDatePerms.Add("fn_dt", OpeningStockConstants.FunctionDateChange, RoleModes.View);
            var editDateSess = new UserSession
            {
                UserId = "u_td_dt_" + tag, UserName = "u_td_dt", DisplayName = "Sửa có đổi ngày",
                IsAdmin = false, DatabasePath = db, Permissions = editDatePerms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH A" } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH A" }
            };
            bool editDateUnlockedOk = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningStockView(editDateSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    editDateUnlockedOk = view.DateBoxForTest.Enabled && !view.DateBoxForTest.Inner.ReadOnly;
                }
                finally { host.Close(); }
            }

            Check("Tồn đầu P1 §4 #5 (UI phân quyền: chỉ Xem mở chỉ-đọc, kho theo cửa hàng non-admin, khóa/mở ô Ngày theo quyền Thay đổi ngày)",
                roOk && whScopeOk && editDateLockedOk && editDateUnlockedOk,
                SingleLine("ro=" + roOk + ", wh=" + whScopeOk + ", dtLock=" + editDateLockedOk + ", dtOpen=" + editDateUnlockedOk));
        }

        private static void TestOpeningStockMenuAndPreview(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode openingNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsOpeningStockMenu(n));

            bool opensRealView = false;
            if (openingNode != null)
            {
                using (Control v = ScreenFactory.CreateView(openingNode, adminSess))
                    opensRealView = v is OpeningStockView;
            }

            // Nhận diện không dấu, không phân biệt hoa thường.
            bool accentInsensitiveOk = ScreenFactory.IsOpeningStockMenu(new MenuNode { Title = "ton kho ban dau" })
                && ScreenFactory.IsOpeningStockMenu(new MenuNode { Title = "TỒN KHO BAN ĐẦU" })
                && !ScreenFactory.IsOpeningStockMenu(new MenuNode { Title = "Tồn kho" });

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_td", UserName = "u_no_td", DisplayName = "Không quyền TD",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            bool deniedMenuConsistent = openingNode != null
                && (string.IsNullOrEmpty(openingNode.FunctionId)
                    || FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsOpeningStockMenu(n)) == null);

            bool previewOk = false;
            using (Form pv = ScreenFactory.Create("preview.openingstock", adminSess))
            {
                var f = pv as OpeningStockPreviewForm;
                if (f != null)
                {
                    ShowDialogOffscreen(f);
                    try
                    {
                        f.Preview("");
                        previewOk = f.ViewForTest != null && f.ViewForTest.GridForTest.Columns.Count == 7;
                    }
                    finally { f.Close(); }
                }
            }

            Check("Tồn đầu P1 §4 #5 (nối menu Tồn kho ban đầu theo tên không dấu, mở OpeningStockView thật & preview.openingstock)",
                openingNode != null && opensRealView && accentInsensitiveOk && deniedMenuConsistent && previewOk,
                SingleLine("node=" + (openingNode != null) + ", fnId=" + (openingNode == null ? "" : openingNode.FunctionId)
                    + ", view=" + opensRealView + ", fold=" + accentInsensitiveOk + ", denied=" + deniedMenuConsistent + ", pv=" + previewOk));
        }

        private static bool PressEnterOnGridReal(DataGridView grid)
        {
            Control target = grid.EditingControl ?? (Control)grid;
            Message msg = Message.Create(target.Handle, GRID_WM_KEYDOWN, (IntPtr)Keys.Enter, (IntPtr)0x001C0001);
            bool handled = target.PreProcessMessage(ref msg);
            Application.DoEvents();
            return handled;
        }
    }
}
