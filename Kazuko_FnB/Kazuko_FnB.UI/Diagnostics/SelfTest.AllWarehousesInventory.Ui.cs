using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
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
        private static void AllWarehousesInventoryUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string store1 = DbHelper.NewId();
            string store2 = DbHelper.NewId();
            string whK1 = DbHelper.NewId();
            string whK2 = DbHelper.NewId();
            string whK3 = DbHelper.NewId();
            string whOther = DbHelper.NewId();
            string unitChai = DbHelper.NewId();
            string unitLoc = DbHelper.NewId();
            string prodX = DbHelper.NewId();
            string prodZero = DbHelper.NewId();
            var orderIds = new List<string>();
            string xlsOkPath = Path.Combine(Path.GetTempPath(), "kazuko-tnk-ok-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsBlockedPath = Path.Combine(Path.GetTempPath(), "kazuko-tnk-no-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", store1, "@N", "CH TNK UI 1 " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", store2, "@N", "CH TNK UI 2 " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK1, "@N", "Kho K1 UI " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK2, "@N", "Kho K2 UI " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK3, "@N", "Kho K3 UI " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whOther, "@N", "Kho CH2 UI " + tag, "@S", store2, "@T", now);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whK1, "@I", store1);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whOther, "@I", store2);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", unitChai, "@N", "chai");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", unitLoc, "@N", "lốc");

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 6, 5, 11000, 12000, 20000, 30, 'selftest_tnk', @T)",
                    "@I", prodX, "@C", "TNK-UIX-" + tag, "@N", "Trà Vải TNK " + tag, "@U1", unitChai, "@U2", unitLoc, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 0, 5000, 6000, 10000, 30, 'selftest_tnk', @T)",
                    "@I", prodZero, "@C", "TNK-UIZ-" + tag, "@N", "Ly Giấy TNK " + tag, "@U", unitChai, "@T", now);

                // K1 = 10, K2 = 5 (cùng CH1), K3 = 0 (cùng CH1), Kho CH2 = 7.
                orderIds.Add(InsertStockOrderLine(db, "PN-TNKUI-K1-" + tag, 1, 30, 30, whK1, null, store1, prodX, unitChai, 10m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNKUI-K2-" + tag, 1, 30, 30, whK2, null, store1, prodX, unitChai, 5m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNKUI-CH2-" + tag, 1, 30, 30, whOther, null, store2, prodX, unitChai, 7m, 0m));

                TestAllWarehousesUiCa6(db, store1, whK1, whK2, whK3, whOther, prodX, prodZero, tag, xlsOkPath);
                TestAllWarehousesUiCa7Restricted(db, store1, whK1, whK2, whK3, whOther, prodX, tag, xlsBlockedPath);
                TestAllWarehousesMenuAndNormalMode(db, store1, whK1, tag);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string oid in orderIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodX);
                SafeDeleteById(db, "DMATHANG", prodZero);
                SafeDeleteById(db, "DDONVITINH", unitChai);
                SafeDeleteById(db, "DDONVITINH", unitLoc);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID IN (@A, @B)", "@A", store1, "@B", store2);
                SafeDeleteById(db, "DKHOHANG", whK1);
                SafeDeleteById(db, "DKHOHANG", whK2);
                SafeDeleteById(db, "DKHOHANG", whK3);
                SafeDeleteById(db, "DKHOHANG", whOther);
                SafeDeleteById(db, "DCUAHANG", store1);
                SafeDeleteById(db, "DCUAHANG", store2);
                SafeDeleteFile(xlsOkPath);
                SafeDeleteFile(xlsBlockedPath);
            }
        }

        private static void TestAllWarehousesUiCa6(
            string db, string store1, string whK1, string whK2, string whK3, string whOther,
            string prodX, string prodZero, string tag, string xlsPath)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            var perms = new FunctionPermissions(false);
            perms.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
            perms.Add("fn_gn", InventoryConstants.ViewBuyPriceFunctionName, RoleModes.View);
            var sess = new UserSession
            {
                UserId = "u_tnk_ui6_" + tag,
                UserName = "u_tnk_ui6",
                DisplayName = "User TNK CH1 Full",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag } },
                CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag }
            };

            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1366, 768);
                var view = new InventoryView(sess, true) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                view.ReloadAll();
                Application.DoEvents();
                try
                {
                    DataGridView mainGrid = view.GridForTest;
                    DataGridView detailGrid = view.DetailGridForTest;

                    bool modeOk = view.IsAllWarehousesModeForTest && detailGrid != null;
                    bool noTabsOk = !HasAnyTabControlInTree(view);
                    bool noVScrollOk = !view.VerticalScroll.Visible && !view.AutoScroll;

                    string[] expectedMainCols =
                    {
                        "Mã hàng", "Tên mặt hàng", "ĐVT", "Tồn kho", "Tồn 2 ĐVT",
                        "Tồn tối thiểu", "Giá nhập", "Giá vốn", "Giá trị vốn", "Giá trị bán"
                    };
                    string[] expectedDetailCols = { "Kho", "Tồn kho", "Tồn 2 ĐVT", "Giá trị vốn" };
                    bool mainColsOk = CheckHeadersNoClip(mainGrid, expectedMainCols);
                    bool detailColsOk = detailGrid != null && CheckHeadersNoClip(detailGrid, expectedDetailCols);

                    var defaultWh = view.WarehouseComboForTest.SelectedItem as LookupItem;
                    bool defaultAllOk = defaultWh != null
                        && defaultWh.Id == InventoryConstants.AllWarehousesId
                        && defaultWh.Name == InventoryConstants.AllWarehousesDisplayName;

                    // Lọc theo tag (gọi thẳng ApplyLocalFilter thay vì chỉ DoEvents).
                    view.SearchBoxForTest.Text = tag;
                    view.ApplyLocalFilter();

                    DataGridViewRow rowXAll = view.FindRowByProductIdForTest(prodX);
                    DataGridViewRow rowZAll = view.FindRowByProductIdForTest(prodZero);
                    decimal stockAll = rowXAll == null ? -1m : Convert.ToDecimal(rowXAll.Cells["colStock"].Value);
                    decimal costAll = rowXAll == null ? -1m : Convert.ToDecimal(rowXAll.Cells["colCostValue"].Value);
                    string twoUAll = rowXAll == null ? "" : Convert.ToString(rowXAll.Cells["colTwoUnit"].Value);
                    bool allSumOk = stockAll == 15m && costAll == 180000m && twoUAll == "2 lốc 3 chai" && rowZAll != null;

                    // Chọn mặt hàng X -> kiểm bảng chi tiết K1 = 10 (120.000), K2 = 5 (60.000), K3 = 0 (0), không có Kho CH2.
                    bool selX = view.SelectProductRowForTest(prodX);
                    view.RefreshWarehouseDetailForTest();
                    DataGridViewRow dK1 = view.FindDetailRowByWarehouseIdForTest(whK1);
                    DataGridViewRow dK2 = view.FindDetailRowByWarehouseIdForTest(whK2);
                    DataGridViewRow dK3 = view.FindDetailRowByWarehouseIdForTest(whK3);
                    DataGridViewRow dOther = view.FindDetailRowByWarehouseIdForTest(whOther);

                    decimal dK1Qty = dK1 == null ? -1m : Convert.ToDecimal(dK1.Cells["colDetailStock"].Value);
                    decimal dK1Val = dK1 == null ? -1m : Convert.ToDecimal(dK1.Cells["colDetailCostValue"].Value);
                    string dK1Two = dK1 == null ? "" : Convert.ToString(dK1.Cells["colDetailTwoUnit"].Value);
                    decimal dK2Qty = dK2 == null ? -1m : Convert.ToDecimal(dK2.Cells["colDetailStock"].Value);
                    decimal dK2Val = dK2 == null ? -1m : Convert.ToDecimal(dK2.Cells["colDetailCostValue"].Value);
                    decimal dK3Qty = dK3 == null ? -1m : Convert.ToDecimal(dK3.Cells["colDetailStock"].Value);

                    // Kiểm tra định dạng số không phụ thuộc culture máy (ép en-US tạm thời vẫn hiện vi-VN "120.000").
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    string formattedCostK1 = dK1 == null ? "" : Convert.ToString(dK1.Cells["colDetailCostValue"].FormattedValue);
                    bool cultureIndependentOk = formattedCostK1 == "120.000";
                    Thread.CurrentThread.CurrentCulture = savedCulture;

                    bool detailAllOk = selX && detailGrid.Rows.Count == 3
                        && dK1Qty == 10m && dK1Val == 120000m && dK1Two == "1 lốc 4 chai"
                        && dK2Qty == 5m && dK2Val == 60000m
                        && dK3Qty == 0m && dOther == null;

                    // Tích "Ẩn hàng tồn 0": ẩn prodZero trên lưới chính VÀ ẩn kho K3 (tồn 0) trên bảng chi tiết.
                    view.HideZeroCheckForTest.Checked = true;
                    view.ApplyLocalFilter();
                    view.SelectProductRowForTest(prodX);
                    view.RefreshWarehouseDetailForTest();
                    bool hideZeroMainOk = view.FindRowByProductIdForTest(prodZero) == null
                        && view.FindRowByProductIdForTest(prodX) != null;
                    bool hideZeroDetailOk = detailGrid.Rows.Count == 2
                        && view.FindDetailRowByWarehouseIdForTest(whK1) != null
                        && view.FindDetailRowByWarehouseIdForTest(whK2) != null
                        && view.FindDetailRowByWarehouseIdForTest(whK3) == null;

                    view.HideZeroCheckForTest.Checked = false;
                    view.ApplyLocalFilter();

                    // Chọn kho cụ thể K1 -> lưới chính hiện X = 10; bảng chi tiết vẫn hiện K1 = 10, K2 = 5.
                    SelectWarehouseCombo(view.WarehouseComboForTest, whK1);
                    view.ReloadStock();
                    view.ApplyLocalFilter();
                    DataGridViewRow rowXK1 = view.FindRowByProductIdForTest(prodX);
                    decimal stockK1 = rowXK1 == null ? -1m : Convert.ToDecimal(rowXK1.Cells["colStock"].Value);
                    view.SelectProductRowForTest(prodX);
                    view.RefreshWarehouseDetailForTest();
                    bool singleWhDetailOk = view.FindDetailRowByWarehouseIdForTest(whK1) != null
                        && view.FindDetailRowByWarehouseIdForTest(whK2) != null;

                    // Quay về "Tất cả các kho" và xuất Excel lưới chính (10 cột đang hiện).
                    SelectWarehouseCombo(view.WarehouseComboForTest, InventoryConstants.AllWarehousesId);
                    view.ReloadStock();
                    view.ApplyLocalFilter();
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 10
                        && HeadersMatch(snap, VisibleGridHeaders(mainGrid))
                        && XlsSheetContainsSubstring(snap, "TNK-UIX-" + tag)
                        && XlsSheetContainsSubstring(snap, "2 lốc 3 chai");

                    // Đổi bảng màu trực tiếp -> lưới chi tiết đổi màu theo ThemeTokens.
                    ThemeManager.Apply(t1);
                    view.ApplyTheme(t1);
                    bool themeOk = detailGrid.BackgroundColor == t1.Surface && mainGrid.BackgroundColor == t1.Surface;

                    Check("Tồn nhiều kho §5 #6 (UI 1366x768, Tất cả các kho = 15, chọn K1 = 10, chi tiết K1=10/K2=5 & giá trị vốn, ẩn tồn 0, xuất Excel, đổi theme, không tab size/HSD): đạt",
                        modeOk && noTabsOk && noVScrollOk && mainColsOk && detailColsOk && defaultAllOk
                        && allSumOk && detailAllOk && cultureIndependentOk
                        && hideZeroMainOk && hideZeroDetailOk
                        && stockK1 == 10m && singleWhDetailOk && xlsOk && themeOk,
                        SingleLine("mode=" + modeOk + ", noTabs=" + noTabsOk + ", noScr=" + noVScrollOk
                            + ", cols=" + mainColsOk + "/" + detailColsOk + ", defAll=" + defaultAllOk
                            + ", sum15=" + allSumOk + ", det=" + detailAllOk + ", fmt=" + formattedCostK1
                            + ", hz=" + hideZeroMainOk + "/" + hideZeroDetailOk
                            + ", k1=" + stockK1 + "/" + singleWhDetailOk + ", xls=" + xlsOk + ", thm=" + themeOk));
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = savedCulture;
                    host.Close();
                }
            }
        }

        private static void TestAllWarehousesUiCa7Restricted(
            string db, string store1, string whK1, string whK2, string whK3, string whOther,
            string prodX, string tag, string xlsBlockedPath)
        {
            var perms = new FunctionPermissions(false);
            perms.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
            var sess = new UserSession
            {
                UserId = "u_tnk_ui7_" + tag,
                UserName = "u_tnk_ui7",
                DisplayName = "User TNK Restricted",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag } },
                CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag }
            };

            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1366, 768);
                var view = new InventoryView(sess, true) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                view.ReloadAll();
                Application.DoEvents();
                try
                {
                    bool hasAll = false, hasK1 = false, hasK2 = false, hasOther = false;
                    foreach (object obj in view.WarehouseComboForTest.Items)
                    {
                        var li = obj as LookupItem;
                        if (li == null) continue;
                        if (li.Id == InventoryConstants.AllWarehousesId) hasAll = true;
                        if (li.Id == whK1) hasK1 = true;
                        if (li.Id == whK2) hasK2 = true;
                        if (li.Id == whOther) hasOther = true;
                    }

                    view.SearchBoxForTest.Text = tag;
                    view.ApplyLocalFilter();
                    view.SelectProductRowForTest(prodX);
                    view.RefreshWarehouseDetailForTest();

                    DataGridView mainGrid = view.GridForTest;
                    DataGridView detailGrid = view.DetailGridForTest;
                    DataGridViewRow rowX = view.FindRowByProductIdForTest(prodX);
                    decimal stockX = rowX == null ? -1m : Convert.ToDecimal(rowX.Cells["colStock"].Value);

                    bool mainPriceColsHidden = !mainGrid.Columns["colBuyPrice"].Visible
                        && !mainGrid.Columns["colCostPrice"].Visible
                        && !mainGrid.Columns["colCostValue"].Visible
                        && !mainGrid.Columns["colSellValue"].Visible;
                    bool mainPriceCellsNull = rowX != null
                        && rowX.Cells["colBuyPrice"].Value == null
                        && rowX.Cells["colCostPrice"].Value == null
                        && rowX.Cells["colCostValue"].Value == null
                        && rowX.Cells["colSellValue"].Value == null;

                    if (rowX != null)
                    {
                        mainGrid.ClearSelection();
                        rowX.Selected = true;
                        mainGrid.CurrentCell = rowX.Cells["colCode"];
                    }
                    string mainCopy = DetailGridMenu.BuildSelectionCopyText(mainGrid) ?? "";
                    bool mainNoLeak = mainCopy.IndexOf("11000", StringComparison.Ordinal) < 0
                        && mainCopy.IndexOf("12000", StringComparison.Ordinal) < 0
                        && mainCopy.IndexOf("180000", StringComparison.Ordinal) < 0
                        && mainCopy.IndexOf("300000", StringComparison.Ordinal) < 0;

                    DataGridViewRow dK1 = view.FindDetailRowByWarehouseIdForTest(whK1);
                    DataGridViewRow dK2 = view.FindDetailRowByWarehouseIdForTest(whK2);
                    DataGridViewRow dOther = view.FindDetailRowByWarehouseIdForTest(whOther);

                    bool detailCostHidden = !detailGrid.Columns["colDetailCostValue"].Visible;
                    bool detailCostNull = dK1 != null && dK1.Cells["colDetailCostValue"].Value == null
                        && dK2 != null && dK2.Cells["colDetailCostValue"].Value == null;

                    if (dK1 != null)
                    {
                        detailGrid.ClearSelection();
                        dK1.Selected = true;
                        detailGrid.CurrentCell = dK1.Cells["colDetailWarehouse"];
                    }
                    string detailCopy = DetailGridMenu.BuildSelectionCopyText(detailGrid) ?? "";
                    bool detailNoLeak = detailCopy.IndexOf("120000", StringComparison.Ordinal) < 0
                        && detailCopy.IndexOf("60000", StringComparison.Ordinal) < 0
                        && detailCopy.IndexOf("120.000", StringComparison.Ordinal) < 0;

                    // Kiểm tra menu ngữ cảnh "Cột hiển thị" của bảng chi tiết không cho bật lại cột Giá trị vốn.
                    bool foundColumnsMenu = false;
                    bool detailMenuStripped = true;
                    if (view.DetailMenuForTest != null)
                    {
                        view.DetailMenuForTest.Show(detailGrid, new Point(4, 4));
                        foreach (ToolStripItem ti in view.DetailMenuForTest.Items)
                        {
                            var mi = ti as ToolStripMenuItem;
                            if (mi != null && mi.Text == "Cột hiển thị")
                            {
                                foundColumnsMenu = true;
                                foreach (ToolStripItem sub in mi.DropDownItems)
                                {
                                    if (sub.Text == "Giá trị vốn")
                                        detailMenuStripped = false;
                                }
                            }
                        }
                        view.DetailMenuForTest.Close();
                    }

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsBlockedPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    bool exportOff = !view.ExcelButtonForTest.Visible && !view.ExcelButtonForTest.Enabled
                        && !view.ExportGridToExcel() && rec.PromptSaveCount == 0 && !File.Exists(xlsBlockedPath);

                    Check("Tồn nhiều kho §5 #7 (UI non-admin & không quyền Xem giá nhập): chỉ thấy kho cửa hàng mình (tổng 15), ẩn cột & giá trị vốn ở cả bảng chi tiết, không lộ qua sao chép/menu cột",
                        hasAll && hasK1 && hasK2 && !hasOther && stockX == 15m
                        && dK1 != null && dK2 != null && dOther == null && detailGrid.Rows.Count == 3
                        && mainPriceColsHidden && mainPriceCellsNull && mainNoLeak
                        && detailCostHidden && detailCostNull && detailNoLeak && foundColumnsMenu && detailMenuStripped && exportOff,
                        SingleLine("wh=" + hasAll + "/" + hasK1 + "/" + hasK2 + "/" + !hasOther
                            + ", stock=" + stockX + ", detCnt=" + detailGrid.Rows.Count + ", noOther=" + (dOther == null)
                            + ", mainHide=" + mainPriceColsHidden + "/" + mainPriceCellsNull + "/" + mainNoLeak
                            + ", detHide=" + detailCostHidden + "/" + detailCostNull + "/" + detailNoLeak
                            + ", menu=" + foundColumnsMenu + "/" + detailMenuStripped + ", expOff=" + exportOff));
                }
                finally { host.Close(); }
            }
        }

        private static bool HasAnyTabControlInTree(Control root)
        {
            if (root == null)
                return false;
            foreach (Control c in root.Controls)
            {
                if (c is TabControl || c is ThemedTabControl)
                    return true;
                if (HasAnyTabControlInTree(c))
                    return true;
            }
            return false;
        }
    }
}
