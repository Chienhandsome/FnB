using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void NumberGridProductAndAreaTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var configRepo = new ConfigRepository(db);
            bool oldDefaultItemsCfg = false;
            bool configChanged = false;

            string groupId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string matId = DbHelper.NewId();
            string brewProdId = DbHelper.NewId();
            string recipeLineId = DbHelper.NewId();
            string areaId = DbHelper.NewId();
            string areaDefId = DbHelper.NewId();

            try
            {
                var adminSession = new UserSession
                {
                    UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };
                AppSession.Current = adminSession;
                SystemConfig.Reload(db);
                oldDefaultItemsCfg = SystemConfig.SuDungMatHangMacDinh;
                configChanged = true;
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = 30 }
                }, "selftest");
                SystemConfig.Reload(db);

                BookingExec(db,
                    "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                    "@id", groupId, "@n", "NHÓM NGP2 DM", "@c", "NGP2DM");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitId, "@n", "KgNG2");
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"DLOAIMATHANGID\", \"GIABAN\", \"GIANHAP\", \"GIAVON\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, '2', 0, 100000, 80000, 30, 'selftest')",
                    "@id", matId, "@c", "NGP2MAT", "@n", "Vật tư NGP2", "@g", groupId, "@u", unitId);
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"DLOAIMATHANGID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, '1', 35000, 30, 'selftest')",
                    "@id", brewProdId, "@c", "NGP2BREW", "@n", "Mặt hàng định lượng NGP2", "@g", groupId, "@u", unitId);
                BookingExec(db,
                    "INSERT INTO \"DDINHLUONG\" (\"ID\", \"STATUS\", \"USERCREATEDID\", \"DMATHANGID\", \"SOLUONG\", \"DVATTUID\") " +
                    "VALUES (@id, 30, 'selftest', @p, 1, @m)",
                    "@id", recipeLineId, "@p", brewProdId, "@m", matId);

                BookingExec(db,
                    "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", areaId, "@n", "KV MÓN MẶC ĐỊNH NGP2");
                BookingExec(db,
                    "INSERT INTO \"DMATHANGMACDINH\" (\"ID\", \"STATUS\", \"USERCREATEDID\", \"DKHUVUCID\", \"DMATHANGID\", \"SOLUONG\") " +
                    "VALUES (@id, 30, 'selftest', @a, @p, 1)",
                    "@id", areaDefId, "@a", areaId, "@p", brewProdId);

                foreach (string cultureName in new[] { "en-US", "vi-VN" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    TestProductRecipeGridNumberInput(db, adminSession, brewProdId, cultureName);
                    TestAreaDefaultsGridNumberInput(db, adminSession, areaId, cultureName);
                }
            }
            finally
            {
                if (configChanged)
                {
                    configRepo.Save(new[]
                    {
                        new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = oldDefaultItemsCfg ? 30 : 0 }
                    }, "selftest");
                }
                AppSession.Current = savedSession;
                SystemConfig.Reload(db);
                BookingExec(db, "DELETE FROM \"DMATHANGMACDINH\" WHERE \"DKHUVUCID\" = @id", "@id", areaId);
                BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", areaId);
                BookingExec(db, "DELETE FROM \"DDINHLUONG\" WHERE \"DMATHANGID\" = @id", "@id", brewProdId);
                BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" IN (@a, @b)", "@a", brewProdId, "@b", matId);
                BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", groupId);
                BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" = @id", "@id", unitId);
            }
        }

        private static void TestProductRecipeGridNumberInput(string db, UserSession session, string productId, string cultureTag)
        {
            var prodRepo = new ProductRepository(db);
            BookingExec(db, "UPDATE \"DDINHLUONG\" SET \"SOLUONG\" = 1 WHERE \"DMATHANGID\" = @p", "@p", productId);

            using (var dlg = new ProductEditDialog(session, productId, null, null))
            {
                dlg.SuppressPopup = true;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.TabsForTest.SelectedTab = dlg.RecipeTabForTest;
                Application.DoEvents();
                try
                {
                    DataGridView g = dlg.RecipeGridForTest;

                    // A7: gõ phím thật 0.125 -> đang sửa hiện "0,125", rời ô giá trị 0.125m, hiển thị "0,125" (#,##0.####), bấm Lưu đọc lại DB = 0.125m.
                    TextBox ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "0.125", true);
                    string typing0125 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal cell0125 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);
                    string fmt0125 = Convert.ToString(g.Rows[0].Cells["colQty"].FormattedValue);
                    decimal amtBuy0125 = Convert.ToDecimal(g.Rows[0].Cells["colAmtBuy"].Value);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<RecipeLine> dbDot = prodRepo.LoadRecipeLines(productId);
                    bool dotSaveOk = typing0125 == "0,125" && cell0125 == 0.125m && fmt0125 == "0,125"
                        && amtBuy0125 == 12500m
                        && dbDot.Count == 1 && dbDot[0].Quantity == 0.125m;

                    // Đổi sang 1 rồi gõ 0,125 -> lưu DB đọc lại đúng 0.125m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1", true);
                    LeaveGridCellReal(g, 0, "colUnit");
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();

                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "0,125", true);
                    LeaveGridCellReal(g, 0, "colUnit");
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<RecipeLine> dbComma = prodRepo.LoadRecipeLines(productId);
                    bool commaSaveOk = dbComma.Count == 1 && dbComma[0].Quantity == 0.125m;

                    // A10 (N4 - 4 chữ số lẻ): gõ phím 0.12345 chặn còn 0,1234; dán 0.56789 cắt còn 0,5678.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "0.12345", true);
                    string typed4 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal val4 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);
                    string fmt4 = Convert.ToString(g.Rows[0].Cells["colQty"].FormattedValue);

                    ed = BeginGridEditReal(g, 0, "colQty");
                    PasteTextIntoGridEditor(ed, "0.56789", true);
                    string pasted4 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal valPasted4 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    // A2b/A6: gõ phím từng ký tự 1.500.000 và dán 1.500.000 -> không commit dở dang, rời ô giữ số cũ 0.5678m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1.500.000", true);
                    string typedTwoDot = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal afterTypedBad = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    ed = BeginGridEditReal(g, 0, "colQty");
                    PasteTextIntoGridEditor(ed, "1.500.000", true);
                    string pastedTwoDot = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal afterPastedBad = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    // A3: 1600 hiện "1.600", vào lại sửa hiện "1600", rời ô không đổi giữ 1600m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1600", true);
                    LeaveGridCellReal(g, 0, "colUnit");
                    string fmt1600 = Convert.ToString(g.Rows[0].Cells["colQty"].FormattedValue);
                    ed = BeginGridEditReal(g, 0, "colQty");
                    string reEdit1600 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colUnit");
                    decimal val1600 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    // A9: đang sửa colQty = 0.25 (CHƯA rời ô), bấm thẳng nút Lưu -> lưu DB đúng 0.25m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "0.25", true);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<RecipeLine> dbImmediate = prodRepo.LoadRecipeLines(productId);
                    bool saveImmediateOk = dbImmediate.Count == 1 && dbImmediate[0].Quantity == 0.25m;

                    // A9: đang sửa colQty = 0.75 (CHƯA rời ô), bấm thẳng Thêm dòng -> dòng 0 giữ đúng 0.75m, có thêm dòng 1.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "0.75", true);
                    dlg.AddRecipeLineButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool addLineImmediateOk = g.Rows.Count == 2 && Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value) == 0.75m;

                    Check("Nhập số P2 [ProductEditDialog định lượng " + cultureTag + " A2b/A3/A6/A7/A9/A10]: 0.125 & 0,125 lưu DB, 0.12345->0,1234, 1.500.000 giữ cũ, 1600->1.600, A9 Lưu/Thêm, 0 DataError",
                        dotSaveOk && commaSaveOk
                        && typed4 == "0,1234" && val4 == 0.1234m && fmt4 == "0,1234"
                        && pasted4 == "0,5678" && valPasted4 == 0.5678m
                        && CountChar(typedTwoDot, ',') >= 2 && afterTypedBad == 0.5678m
                        && pastedTwoDot == "1,500,000" && afterPastedBad == 0.5678m
                        && fmt1600 == "1.600" && reEdit1600 == "1600" && val1600 == 1600m
                        && saveImmediateOk && addLineImmediateOk
                        && dlg.RecipeDataErrorCountForTest == 0,
                        "dot=" + dotSaveOk + ", comma=" + commaSaveOk + ", t4=" + typed4 + "/" + val4 + "/" + fmt4
                        + ", p4=" + pasted4 + ", b=" + typedTwoDot + "/" + afterTypedBad + ", f1600=" + fmt1600 + "/" + reEdit1600
                        + ", sv=" + saveImmediateOk + ", add=" + addLineImmediateOk + ", de=" + dlg.RecipeDataErrorCountForTest
                        + FormatDataErrorSuffix(dlg.RecipeDataErrorDetailForTest));
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestAreaDefaultsGridNumberInput(string db, UserSession session, string areaId, string cultureTag)
        {
            var areaRepo = new AreaRepository(db);
            BookingExec(db, "UPDATE \"DMATHANGMACDINH\" SET \"SOLUONG\" = 1 WHERE \"DKHUVUCID\" = @a", "@a", areaId);

            using (var dlg = new AreaEditDialog(session, areaId, null))
            {
                dlg.SuppressPopup = true;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.TabsForTest.SelectedTab = dlg.DefaultsTabForTest;
                Application.DoEvents();
                try
                {
                    DataGridView g = dlg.DefaultsGridForTest;

                    // A8: gõ 2,5 -> rời ô giá trị 2.5m, hiển thị "2,5", bấm Lưu đọc lại DB = 2.5m; thử thêm 2.5 -> "2,5" lưu DB = 2.5m.
                    TextBox ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "2,5", true);
                    string typingComma = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    decimal valComma = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);
                    string fmtComma = Convert.ToString(g.Rows[0].Cells["colQty"].FormattedValue);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<AreaDefaultItem> dbComma = areaRepo.LoadDefaultItems(areaId);
                    bool commaSaveOk = typingComma == "2,5" && valComma == 2.5m && fmtComma == "2,5"
                        && dbComma.Count == 1 && dbComma[0].Quantity == 2.5m;

                    // Đổi sang 1 rồi gõ 2.5 -> hiện "2,5" và lưu DB đúng 2.5m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1", true);
                    LeaveGridCellReal(g, 0, "colProduct");
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();

                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "2.5", true);
                    string typingDot = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<AreaDefaultItem> dbDot = areaRepo.LoadDefaultItems(areaId);
                    bool dotSaveOk = typingDot == "2,5" && dbDot.Count == 1 && dbDot[0].Quantity == 2.5m;

                    // A10 (N4 - 2 chữ số lẻ): gõ phím 2.128 còn 2,12; dán 3.456 còn 3,45.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "2.128", true);
                    string typed2128 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    decimal val212 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    ed = BeginGridEditReal(g, 0, "colQty");
                    PasteTextIntoGridEditor(ed, "3.456", true);
                    string pasted3456 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    decimal val345 = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    // A2b/A6: gõ phím từng ký tự 1.500.000 và dán 1.500.000 -> không commit dở dang, rời ô giữ số cũ 3.45m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1.500.000", true);
                    string typedTwoDot = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    decimal afterTypedBad = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    ed = BeginGridEditReal(g, 0, "colQty");
                    PasteTextIntoGridEditor(ed, "1.500.000", true);
                    string pastedTwoDot = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");
                    decimal afterPastedBad = Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value);

                    // A3: 1600 hiện "1.600" kể cả trên en-US, vào lại sửa hiện "1600".
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "1600", true);
                    LeaveGridCellReal(g, 0, "colProduct");
                    string fmt1600 = Convert.ToString(g.Rows[0].Cells["colQty"].FormattedValue);
                    ed = BeginGridEditReal(g, 0, "colQty");
                    string reEdit1600 = ed != null ? ed.Text : "";
                    LeaveGridCellReal(g, 0, "colProduct");

                    // A9: đang sửa colQty = 3.75 (CHƯA rời ô), bấm thẳng Lưu -> lưu DB đúng 3.75m.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "3.75", true);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    List<AreaDefaultItem> dbImmediate = areaRepo.LoadDefaultItems(areaId);
                    bool saveImmediateOk = dbImmediate.Count == 1 && dbImmediate[0].Quantity == 3.75m;

                    // A9: đang sửa colQty = 4.25 (CHƯA rời ô), bấm thẳng Thêm dòng -> dòng 0 giữ đúng 4.25m, có thêm dòng 1.
                    ed = BeginGridEditReal(g, 0, "colQty");
                    TypeCharsIntoGridEditor(ed, "4.25", true);
                    dlg.AddDefaultLineButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool addLineImmediateOk = g.Rows.Count == 2 && Convert.ToDecimal(g.Rows[0].Cells["colQty"].Value) == 4.25m;

                    Check("Nhập số P2 [AreaEditDialog món mặc định " + cultureTag + " A2b/A3/A6/A8/A9/A10]: 2,5 & 2.5 lưu DB, 2.128->2,12, 1.500.000 giữ cũ, 1600->1.600, A9 Lưu/Thêm, 0 DataError",
                        commaSaveOk && dotSaveOk
                        && typed2128 == "2,12" && val212 == 2.12m
                        && pasted3456 == "3,45" && val345 == 3.45m
                        && CountChar(typedTwoDot, ',') >= 2 && afterTypedBad == 3.45m
                        && pastedTwoDot == "1,500,000" && afterPastedBad == 3.45m
                        && fmt1600 == "1.600" && reEdit1600 == "1600"
                        && saveImmediateOk && addLineImmediateOk
                        && dlg.DefaultsDataErrorCountForTest == 0,
                        "c=" + commaSaveOk + ", d=" + dotSaveOk + ", t2128=" + typed2128 + "/" + val212
                        + ", p3456=" + pasted3456 + ", b=" + typedTwoDot + "/" + afterTypedBad
                        + ", f1600=" + fmt1600 + "/" + reEdit1600 + ", sv=" + saveImmediateOk + ", add=" + addLineImmediateOk
                        + ", de=" + dlg.DefaultsDataErrorCountForTest
                        + FormatDataErrorSuffix(dlg.DefaultsDataErrorDetailForTest));
                }
                finally { dlg.Close(); }
            }
        }
    }
}
