using System;
using System.Collections.Generic;
using System.Drawing;
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
        private static void InventoryUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA = DbHelper.NewId();
            string whB = DbHelper.NewId();
            string unitChai = DbHelper.NewId();
            string unitThung = DbHelper.NewId();
            string grpParent = null;
            string grpChild = null;
            string grpOther = null;
            string prodLow = DbHelper.NewId();
            string prodTwoUnit = DbHelper.NewId();
            string prodNeg = DbHelper.NewId();
            string prodZero = DbHelper.NewId();
            var orderIds = new List<string>();
            string xlsOkPath = Path.Combine(Path.GetTempPath(), "kazuko-inv-ok-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsBlockedPath = Path.Combine(Path.GetTempPath(), "kazuko-inv-no-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", storeA, "@N", "CH TK UI A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", storeB, "@N", "CH TK UI B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tk', @T)", "@I", whA, "@N", "Kho UI A " + tag, "@S", storeA, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tk', @T)", "@I", whB, "@N", "Kho UI B " + tag, "@S", storeB, "@T", DbHelper.Now());
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whA, "@I", storeA);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whB, "@I", storeB);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", unitChai, "@N", "chai");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", unitThung, "@N", "thùng");

                var grpRepo = new ProductGroupRepository(db);
                string err;
                grpParent = grpRepo.Save(new ProductGroupRow { Name = "Nhóm Cha TK " + tag, DrinkTypeId = "4" }, "selftest_tk", out err);
                grpChild = grpRepo.Save(new ProductGroupRow { Name = "Nhóm Con TK " + tag, ParentId = grpParent, DrinkTypeId = "4" }, "selftest_tk", out err);
                grpOther = grpRepo.Save(new ProductGroupRow { Name = "Nhóm Khác TK " + tag, DrinkTypeId = "1" }, "selftest_tk", out err);

                DateTime now = DbHelper.Now();
                // prodLow: loại '0', nhóm grpOther, tồn 4 <= 5 (tô vàng), GIANHAP 11100, GIAVON 12345, GIABAN 20000.
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DNHOMMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @G, @U, 5, 11100, 12345, 20000, 30, 'selftest_tk', @T)",
                    "@I", prodLow, "@C", "UI-LOW-" + tag, "@N", "Sữa Đặc " + tag, "@G", grpOther, "@U", unitChai, "@T", now);
                // prodTwoUnit: loại '2', nhóm grpChild, 2 ĐVT quy đổi 24, tồn 50 -> "2 thùng 2 chai" (bình thường), GIAVON 10000, GIABAN 15000.
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DNHOMMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @G, @U1, @U2, 24, 10, 240000, 10000, 15000, 30, 'selftest_tk', @T)",
                    "@I", prodTwoUnit, "@C", "UI-2U-" + tag, "@N", "Nước Cam " + tag, "@G", grpChild, "@U1", unitChai, "@U2", unitThung, "@T", now);
                // prodNeg: loại '2', nhóm grpParent, tồn -3 (tô đỏ, ưu tiên hơn vàng), GIAVON 20000, GIABAN 30000.
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DNHOMMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @G, @U, 5, 18000, 20000, 30000, 30, 'selftest_tk', @T)",
                    "@I", prodNeg, "@C", "UI-NEG-" + tag, "@N", "Bột Trà Xanh " + tag, "@G", grpParent, "@U", unitChai, "@T", now);
                // prodZero: loại '0', nhóm grpOther, tồn 0, TONTOITHIEU 0 (bình thường, ẩn khi tích Ẩn hàng tồn 0).
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DNHOMMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @G, @U, 0, 5000, 6000, 10000, 30, 'selftest_tk', @T)",
                    "@I", prodZero, "@C", "UI-ZERO-" + tag, "@N", "Ống Hút " + tag, "@G", grpOther, "@U", unitChai, "@T", now);

                // Seed giao dịch tại whA và whB.
                orderIds.Add(InsertStockOrderLine(db, "PN-UILOW-" + tag, 1, 30, 30, whA, null, storeA, prodLow, unitChai, 4m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-UILOWB-" + tag, 1, 30, 30, whB, null, storeB, prodLow, unitChai, 9m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-UI2U-" + tag, 1, 30, 30, whA, null, storeA, prodTwoUnit, unitChai, 50m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PX-UINEG-" + tag, 2, 30, 30, null, whA, storeA, prodNeg, unitChai, 0m, 3m));

                var adminSess = new UserSession
                {
                    UserId = "selftest_tk", UserName = "selftest_tk", DisplayName = "Admin TK UI",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH TK UI A " + tag }
                };

                // 1. Admin UI: 10 cột không cắt chữ, kho mặc định cửa hàng đăng nhập, 2 ĐVT, màu ô thật đỏ/vàng + đổi theme, tổng giá trị, bộ lọc & Excel.
                TestInventoryAdminUi(adminSess, whA, whB, grpParent, grpChild, prodLow, prodTwoUnit, prodNeg, prodZero, tag, xlsOkPath);

                // 2. Non-admin chỉ thấy kho cửa hàng mình + ẩn 4 cột giá + không lộ khi sao chép + tắt xuất Excel.
                TestInventoryRestrictedUi(db, storeA, whA, whB, prodLow, tag, xlsBlockedPath);

                // 3. Nối menu KHO HÀNG > Tồn kho + phân quyền menu + preview.inventory.
                TestInventoryMenuAndPreview(db, adminSess);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string oid in orderIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodLow);
                SafeDeleteById(db, "DMATHANG", prodTwoUnit);
                SafeDeleteById(db, "DMATHANG", prodNeg);
                SafeDeleteById(db, "DMATHANG", prodZero);
                if (!string.IsNullOrEmpty(grpChild)) SafeDeleteById(db, "DNHOMMATHANG", grpChild);
                if (!string.IsNullOrEmpty(grpParent)) SafeDeleteById(db, "DNHOMMATHANG", grpParent);
                if (!string.IsNullOrEmpty(grpOther)) SafeDeleteById(db, "DNHOMMATHANG", grpOther);
                SafeDeleteById(db, "DDONVITINH", unitChai);
                SafeDeleteById(db, "DDONVITINH", unitThung);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID IN (@A, @B)", "@A", storeA, "@B", storeB);
                SafeDeleteById(db, "DKHOHANG", whA);
                SafeDeleteById(db, "DKHOHANG", whB);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                SafeDeleteFile(xlsOkPath);
                SafeDeleteFile(xlsBlockedPath);
            }
        }

        private static void TestInventoryAdminUi(
            UserSession adminSess, string whA, string whB, string grpParent, string grpChild,
            string prodLow, string prodTwoUnit, string prodNeg, string prodZero, string tag, string xlsPath)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new InventoryView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedCols =
                    {
                        "Mã hàng", "Tên mặt hàng", "ĐVT", "Tồn kho", "Tồn 2 ĐVT",
                        "Tồn tối thiểu", "Giá nhập", "Giá vốn", "Giá trị vốn", "Giá trị bán"
                    };
                    bool colsOk = CheckHeadersNoClip(grid, expectedCols);

                    // Mặc định chọn đúng kho whA của cửa hàng đăng nhập.
                    var selWh = view.WarehouseComboForTest.SelectedItem as LookupItem;
                    bool defaultWhOk = selWh != null && selWh.Id == whA;

                    // Tìm theo tag để cô lập đúng 4 mặt hàng seed (+ 1 hàng Tổng cộng = 5 dòng).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();

                    DataGridViewRow rowLow = view.FindRowByProductIdForTest(prodLow);
                    DataGridViewRow rowTwo = view.FindRowByProductIdForTest(prodTwoUnit);
                    DataGridViewRow rowNeg = view.FindRowByProductIdForTest(prodNeg);
                    DataGridViewRow rowZero = view.FindRowByProductIdForTest(prodZero);

                    // Kiểm tra hiển thị 2 ĐVT thật trên ô lưới (§4 #2).
                    string twoUnitCellText = rowTwo == null ? "" : Convert.ToString(rowTwo.Cells["colTwoUnit"].Value);
                    bool twoUnitCellOk = twoUnitCellText == "2 thùng 2 chai";

                    // Kiểm tra màu THẬT của ô sau khi tô (không chỉ kiểm hàm chọn tông): đỏ cho tồn âm, vàng cho <= tồn tối thiểu.
                    Color negBg = rowNeg == null ? Color.Empty : rowNeg.Cells["colStock"].InheritedStyle.BackColor;
                    Color negFg = rowNeg == null ? Color.Empty : rowNeg.Cells["colStock"].InheritedStyle.ForeColor;
                    Color lowBg = rowLow == null ? Color.Empty : rowLow.Cells["colStock"].InheritedStyle.BackColor;
                    bool redCellOk = negBg == InventoryView.NegativeRowBackColor(t0)
                        && negFg == InventoryView.NegativeRowForeColor(t0)
                        && negBg.R > negBg.G && negBg.R > negBg.B;
                    bool yellowCellOk = lowBg == InventoryView.LowStockRowBackColor(t0)
                        && lowBg.R > lowBg.B && lowBg.G > lowBg.B;
                    bool normalCellOk = rowTwo != null && rowTwo.DefaultCellStyle.BackColor == Color.Empty;

                    // Tổng cộng giá trị trên 4 mặt hàng:
                    // CostValue: 4*12345 (49380) + 50*10000 (500000) + (-3)*20000 (-60000) + 0 = 489380.
                    // SellValue: 4*20000 (80000) + 50*15000 (750000) + (-3)*30000 (-90000) + 0 = 740000.
                    DataGridViewRow sumRow = grid.Rows.Count == 5 ? grid.Rows[4] : null;
                    decimal sumCost = sumRow == null ? -1m : Convert.ToDecimal(sumRow.Cells["colCostValue"].Value);
                    decimal sumSell = sumRow == null ? -1m : Convert.ToDecimal(sumRow.Cells["colSellValue"].Value);
                    bool sumColorOk = sumRow != null && sumRow.Cells["colName"].InheritedStyle.BackColor == InventoryView.SummaryRowBackColor(t0);

                    // Đổi theme lúc đang mở (Cam Nắng -> Xanh Biển có Surface khác) -> màu ô thật trên lưới cập nhật theo theme mới.
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool themeLiveOk = rowNeg != null && rowLow != null
                        && InventoryView.NegativeRowBackColor(t1) != InventoryView.NegativeRowBackColor(t0)
                        && rowNeg.Cells["colStock"].InheritedStyle.BackColor == InventoryView.NegativeRowBackColor(t1)
                        && rowLow.Cells["colStock"].InheritedStyle.BackColor == InventoryView.LowStockRowBackColor(t1)
                        && grid.BackgroundColor == t1.Surface;

                    Check("Tồn kho §4 #1 & #2 (lưới 10 cột không cắt chữ, kho mặc định, ô 2 ĐVT \"2 thùng 2 chai\", màu ô thật đỏ/vàng & đổi theme, tổng giá trị): đạt",
                        colsOk && defaultWhOk && twoUnitCellOk && redCellOk && yellowCellOk && normalCellOk
                        && sumCost == 489380m && sumSell == 740000m && sumColorOk && themeLiveOk,
                        SingleLine("cols=" + colsOk + ", defWh=" + defaultWhOk + ", 2u=" + twoUnitCellText
                            + ", red=" + redCellOk + ", yel=" + yellowCellOk + ", sum=" + sumCost + "/" + sumSell + ", theme=" + themeLiveOk));

                    // Xuất Excel qua nút thật khi có quyền Xem giá nhập (§4 #4).
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 10
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "2 thùng 2 chai")
                        && XlsSheetContainsSubstring(snap, "UI-LOW-" + tag);

                    // Lọc tích "Chỉ hiện hàng dưới mức tồn tối thiểu": chỉ còn prodLow (4<=5) & prodNeg (-3<=5), loại prodTwoUnit & prodZero.
                    view.BelowMinCheckForTest.Checked = true;
                    Application.DoEvents();
                    bool belowMinUiOk = view.FindRowByProductIdForTest(prodLow) != null
                        && view.FindRowByProductIdForTest(prodNeg) != null
                        && view.FindRowByProductIdForTest(prodTwoUnit) == null
                        && view.FindRowByProductIdForTest(prodZero) == null;
                    view.BelowMinCheckForTest.Checked = false;
                    Application.DoEvents();

                    // Lọc tích "Ẩn hàng tồn 0": ẩn prodZero, vẫn giữ prodLow, prodTwoUnit, prodNeg (tồn âm).
                    view.HideZeroCheckForTest.Checked = true;
                    Application.DoEvents();
                    bool hideZeroUiOk = view.FindRowByProductIdForTest(prodZero) == null
                        && view.FindRowByProductIdForTest(prodNeg) != null
                        && view.FindRowByProductIdForTest(prodLow) != null
                        && view.FindRowByProductIdForTest(prodTwoUnit) != null;
                    view.HideZeroCheckForTest.Checked = false;
                    Application.DoEvents();

                    // Lọc Loại hàng = Nguyên liệu ('2'): còn prodTwoUnit & prodNeg, loại prodLow & prodZero.
                    SelectWarehouseCombo(view.ProductTypeComboForTest, InventoryConstants.TypeMaterial);
                    Application.DoEvents();
                    bool typeUiOk = view.FindRowByProductIdForTest(prodTwoUnit) != null
                        && view.FindRowByProductIdForTest(prodNeg) != null
                        && view.FindRowByProductIdForTest(prodLow) == null;
                    SelectWarehouseCombo(view.ProductTypeComboForTest, InventoryConstants.TypeAll);
                    Application.DoEvents();

                    // Lọc cây Nhóm mặt hàng: chọn nhóm cha -> có cả prodNeg (cha) và prodTwoUnit (con); chọn nhóm con -> chỉ có prodTwoUnit.
                    bool selParent = view.SelectGroupNodeForTest(grpParent);
                    Application.DoEvents();
                    bool parentTreeOk = selParent && view.FindRowByProductIdForTest(prodNeg) != null
                        && view.FindRowByProductIdForTest(prodTwoUnit) != null
                        && view.FindRowByProductIdForTest(prodLow) == null;
                    bool selChild = view.SelectGroupNodeForTest(grpChild);
                    Application.DoEvents();
                    bool childTreeOk = selChild && view.FindRowByProductIdForTest(prodTwoUnit) != null
                        && view.FindRowByProductIdForTest(prodNeg) == null;
                    view.SelectGroupNodeForTest(null);
                    Application.DoEvents();

                    // Tìm không dấu & không phân biệt hoa thường theo tên ("sua dac") và theo mã ("ui-neg-").
                    view.SearchBoxForTest.Text = "sua dac " + tag;
                    Application.DoEvents();
                    bool searchNameOk = view.FindRowByProductIdForTest(prodLow) != null && view.FindRowByProductIdForTest(prodTwoUnit) == null;
                    view.SearchBoxForTest.Text = "ui-neg-" + tag.ToLowerInvariant();
                    Application.DoEvents();
                    bool searchCodeOk = view.FindRowByProductIdForTest(prodNeg) != null && view.FindRowByProductIdForTest(prodLow) == null;

                    // Đổi sang kho whB qua ComboBox thật -> tồn prodLow đổi từ 4 thành 9.
                    view.SearchBoxForTest.Text = tag;
                    SelectWarehouseCombo(view.WarehouseComboForTest, whB);
                    Application.DoEvents();
                    DataGridViewRow rowLowB = view.FindRowByProductIdForTest(prodLow);
                    decimal stockB = rowLowB == null ? -1m : Convert.ToDecimal(rowLowB.Cells["colStock"].Value);

                    Check("Tồn kho §4 #3 & #4 (lọc kho, loại, cây nhóm cha/con, tìm mã & tên không dấu, tích dưới tối thiểu, tích ẩn tồn 0, xuất Excel 10 cột): đạt toàn bộ",
                        xlsOk && belowMinUiOk && hideZeroUiOk && typeUiOk && parentTreeOk && childTreeOk
                        && searchNameOk && searchCodeOk && stockB == 9m,
                        SingleLine("xls=" + xlsOk + ", min=" + belowMinUiOk + ", zero=" + hideZeroUiOk
                            + ", type=" + typeUiOk + ", tree=" + parentTreeOk + "/" + childTreeOk
                            + ", search=" + searchNameOk + "/" + searchCodeOk + ", whB=" + stockB));
                }
                finally { host.Close(); }
            }
        }

        private static void TestInventoryRestrictedUi(
            string db, string storeA, string whA, string whB, string prodLow, string tag, string xlsBlockedPath)
        {
            // Non-admin: chỉ gán storeA, chỉ có quyền Xem "Tồn kho", KHÔNG có quyền "Xem giá nhập".
            var perms = new FunctionPermissions(false);
            perms.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
            var sess = new UserSession
            {
                UserId = "u_tk_ui_" + tag,
                UserName = "u_tk_ui",
                DisplayName = "User TK Store A",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = perms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH TK UI A " + tag } },
                CurrentStore = new StoreRecord { Id = storeA, Name = "CH TK UI A " + tag }
            };

            using (var host = MakeOffscreenHost())
            {
                var view = new InventoryView(sess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;

                    // Chỉ thấy kho cửa hàng A, không thấy kho cửa hàng B.
                    bool hasWhA = false, hasWhB = false;
                    foreach (object item in view.WarehouseComboForTest.Items)
                    {
                        var li = item as LookupItem;
                        if (li == null) continue;
                        if (li.Id == whA) hasWhA = true;
                        if (li.Id == whB) hasWhB = true;
                    }

                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    DataGridViewRow rowLow = view.FindRowByProductIdForTest(prodLow);

                    // Ẩn đúng 4 cột giá nhập/giá vốn/giá trị vốn/giá trị bán.
                    bool colsHidden = !grid.Columns["colBuyPrice"].Visible
                        && !grid.Columns["colCostPrice"].Visible
                        && !grid.Columns["colCostValue"].Visible
                        && !grid.Columns["colSellValue"].Visible;

                    // Giá trị trong các ô ẩn phải null & sao chép vùng chọn không được lộ giá (11100, 12345, 49380, 80000).
                    bool cellsNull = rowLow != null
                        && rowLow.Cells["colBuyPrice"].Value == null
                        && rowLow.Cells["colCostPrice"].Value == null
                        && rowLow.Cells["colCostValue"].Value == null
                        && rowLow.Cells["colSellValue"].Value == null;

                    if (rowLow != null)
                    {
                        grid.ClearSelection();
                        rowLow.Selected = true;
                        grid.CurrentCell = rowLow.Cells["colCode"];
                    }
                    string copyText = DetailGridMenu.BuildSelectionCopyText(grid) ?? "";
                    bool noCopyLeak = copyText.IndexOf("11100", StringComparison.Ordinal) < 0
                        && copyText.IndexOf("12345", StringComparison.Ordinal) < 0
                        && copyText.IndexOf("49380", StringComparison.Ordinal) < 0
                        && copyText.IndexOf("80000", StringComparison.Ordinal) < 0;

                    // Tắt xuất Excel khi không có quyền Xem giá nhập (spec §1).
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsBlockedPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    bool exportBtnHidden = !view.ExcelButtonForTest.Visible && !view.ExcelButtonForTest.Enabled;
                    bool exportBlocked = !view.ExportGridToExcel() && rec.PromptSaveCount == 0 && !File.Exists(xlsBlockedPath);

                    Check("Tồn kho §4 #3 & #4 (non-admin kho theo cửa hàng, không quyền Xem giá nhập ẩn 4 cột giá, ô null chống lộ sao chép & tắt xuất): đạt",
                        hasWhA && !hasWhB && colsHidden && cellsNull && noCopyLeak && exportBtnHidden && exportBlocked,
                        SingleLine("wh=" + hasWhA + "/" + !hasWhB + ", hide=" + colsHidden + ", null=" + cellsNull
                            + ", copyOk=" + noCopyLeak + ", expOff=" + exportBtnHidden + "/" + exportBlocked));
                }
                finally { host.Close(); }
            }
        }

        private static void TestInventoryMenuAndPreview(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode invNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsInventoryMenu(n));

            bool opensRealView = false;
            if (invNode != null)
            {
                using (Control v = ScreenFactory.CreateView(invNode, adminSess))
                    opensRealView = v is InventoryView;
            }

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_tk", UserName = "u_no_tk", DisplayName = "Không quyền tồn kho",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            MenuNode deniedNode = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsInventoryMenu(n));

            bool previewOk = false;
            using (Form pvForm = ScreenFactory.Create("preview.inventory", adminSess))
            {
                var pv = pvForm as InventoryPreviewForm;
                if (pv != null)
                {
                    pv.StartPosition = FormStartPosition.Manual;
                    pv.ShowInTaskbar = false;
                    pv.Location = new Point(-20000, -20000);
                    pv.Show();
                    Application.DoEvents();
                    try
                    {
                        pv.Preview("");
                        previewOk = pv.ViewForTest != null && pv.ViewForTest.GridForTest.Columns.Count == 10;
                    }
                    finally { pv.Close(); }
                }
            }

            Check("Tồn kho §4 #3 & #5 (nối menu KHO HÀNG > Tồn kho, phân quyền menu & preview.inventory): admin mở InventoryView thật, user không quyền không thấy menu",
                invNode != null && opensRealView && deniedNode == null && previewOk,
                SingleLine("node=" + (invNode != null) + ", view=" + opensRealView + ", denied=" + (deniedNode == null) + ", pv=" + previewOk));
        }
    }
}
