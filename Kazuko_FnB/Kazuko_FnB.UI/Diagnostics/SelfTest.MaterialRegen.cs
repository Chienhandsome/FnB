using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md §4 #1–#7).</summary>
    public static partial class SelfTest
    {
        private static void MaterialRegenTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_xl",
                    UserName = "selftest_xl",
                    DisplayName = "Selftest XuatLai",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                MaterialRegenPureRuleTests();
                MaterialRegenDbTests(db);
                MaterialRegenUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void MaterialRegenPureRuleTests()
        {
            DateTime defFrom, defTo;
            MaterialRegenRules.DefaultDateRange(new DateTime(2026, 2, 14), out defFrom, out defTo);
            bool defMonthOk = defFrom == new DateTime(2026, 2, 1) && defTo == new DateTime(2026, 2, 28);

            bool dateValOk = MaterialRegenRules.ValidateDateRange(null, new DateTime(2026, 1, 10)) == MaterialRegenConstants.DateRequired
                && MaterialRegenRules.ValidateDateRange(new DateTime(2026, 1, 10), null) == MaterialRegenConstants.DateRequired
                && MaterialRegenRules.ValidateDateRange(null, null) == MaterialRegenConstants.DateRequired
                && MaterialRegenRules.ValidateDateRange(new DateTime(2026, 1, 20), new DateTime(2026, 1, 10)) == MaterialRegenConstants.DateRangeInvalid
                && MaterialRegenRules.ValidateDateRange(new DateTime(2026, 1, 10), new DateTime(2026, 1, 10)) == null;

            bool confirmOk = MaterialRegenRules.IsConfirmWordValid("XACNHAN")
                && MaterialRegenRules.IsConfirmWordValid("xacnhan")
                && MaterialRegenRules.IsConfirmWordValid("  XacNhan ")
                && !MaterialRegenRules.IsConfirmWordValid("")
                && !MaterialRegenRules.IsConfirmWordValid("XAC")
                && !MaterialRegenRules.IsConfirmWordValid("XACNHAN1");

            string longStore = new string('C', 300);
            string note = MaterialRegenRules.BuildActivityNote(
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), longStore, 5, 10, 12, 2);
            bool clipOk = note.Length == 255 && MaterialRegenRules.Clip(longStore, 255).Length == 255;

            Check("Xuất lại §4 (luật thuần): tháng mặc định, bắt buộc ngày, Từ<=Đến, từ XACNHAN không phân biệt hoa thường, cắt chuỗi 255",
                defMonthOk && dateValOk && confirmOk && clipOk,
                MrOneLine("def=" + defMonthOk + ", date=" + dateValOk + ", confirm=" + confirmOk + ", clip=" + clipOk));
        }

        private static void MaterialRegenDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { "TuDongTinhGiaVon", "KichHoatLuuVetHoatDong" });
            var lineSnap = new Dictionary<string, decimal>();
            var prodSnap = new Dictionary<string, decimal>();

            string tag = DbHelper.NewId().Substring(0, 8);
            string store1 = DbHelper.NewId(), store2 = DbHelper.NewId();
            string wh1 = DbHelper.NewId(), wh2 = DbHelper.NewId(), whNeg = DbHelper.NewId();
            string area1 = DbHelper.NewId(), area2 = DbHelper.NewId(), areaNeg = DbHelper.NewId();
            string table1 = null, table2 = null, tableNeg = null;
            string unitId = DbHelper.NewId();
            string prodA = DbHelper.NewId(), prodB = DbHelper.NewId(), prodC = DbHelper.NewId();
            string prodX = DbHelper.NewId(), prodMid = DbHelper.NewId(), prodTop = DbHelper.NewId();
            string prodNegDrink = DbHelper.NewId();
            string userStore1 = DbHelper.NewId(), userStoreLink = DbHelper.NewId();
            var createdOrders = new List<string>();
            var createdRecipes = new List<string>();

            try
            {
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM TDONHANGCHITIET")).Rows)
                    lineSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM DMATHANG")).Rows)
                    prodSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);

                DateTime now = DbHelper.Now();
                DbHelper.InTransaction(db, (conn, tx) =>
                {
                    DbHelper.Exec(conn, tx, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xl')", "@I", store1, "@N", "CH XL1 " + tag);
                    DbHelper.Exec(conn, tx, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xl')", "@I", store2, "@N", "CH XL2 " + tag);
                    DbHelper.Exec(conn, tx, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xl', @T)",
                        "@I", wh1, "@N", "Kho XL1 " + tag, "@S", store1, "@T", now);
                    DbHelper.Exec(conn, tx, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xl', @T)",
                        "@I", wh2, "@N", "Kho XL2 " + tag, "@S", store2, "@T", now);
                    // Kho whNeg tắt CHOPHEPAMKHO (= 0) để chứng minh Xuất lại KHÔNG chặn âm kho (T4).
                    DbHelper.Exec(conn, tx, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_xl', @T)",
                        "@I", whNeg, "@N", "Kho XL Neg " + tag, "@S", store1, "@T", now);

                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZX', 'selftest_xl', @T)",
                        "@I", area1, "@N", "KV XL1 " + tag, "@W", wh1, "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZY', 'selftest_xl', @T)",
                        "@I", area2, "@N", "KV XL2 " + tag, "@W", wh2, "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZZ', 'selftest_xl', @T)",
                        "@I", areaNeg, "@N", "KV XL Neg " + tag, "@W", whNeg, "@T", now);

                    table1 = InsertTestTable(conn, tx, "XL-B1-" + tag, area1, 0, now);
                    table2 = InsertTestTable(conn, tx, "XL-B2-" + tag, area2, 0, now);
                    tableNeg = InsertTestTable(conn, tx, "XL-BN-" + tag, areaNeg, 0, now);

                    DbHelper.Exec(conn, tx, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xl')", "@I", unitId, "@N", "G " + tag);

                    Action<string, string, string, string, decimal> addProd = (id, code, name, type, gv) =>
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@I, @C, @N, @TP, @U, 10000, 30000, @GV, 30, 'selftest_xl', @T)",
                            "@I", id, "@C", code, "@N", name, "@TP", type, "@U", unitId, "@GV", gv, "@T", now);

                    addProd(prodA, "XLA-" + tag, "Nguyên liệu A " + tag, "2", 10000m);
                    addProd(prodB, "XLB-" + tag, "Nguyên liệu B " + tag, "2", 5000m);
                    addProd(prodC, "XLC-" + tag, "Nguyên liệu C " + tag, "2", 2000m);
                    addProd(prodX, "XLX-" + tag, "Món X " + tag, "1", 20000m);
                    addProd(prodMid, "XLM-" + tag, "Cốt Mid " + tag, "1", 0m);
                    addProd(prodTop, "XLT-" + tag, "Món 2 tầng " + tag, "1", 0m);
                    addProd(prodNegDrink, "XLN-" + tag, "Món gây âm " + tag, "1", 0m);

                    Func<string, string, decimal, string> addRecipe = (pId, vId, qty) =>
                    {
                        string rid = DbHelper.NewId();
                        createdRecipes.Add(rid);
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest_xl', @T, @P, @Q, @V)",
                            "@I", rid, "@T", now, "@P", pId, "@Q", qty, "@V", vId);
                        return rid;
                    };

                    // §4 #1: Món X ban đầu 2 x A.
                    addRecipe(prodX, prodA, 2m);
                    // §4 #2: 2 tầng: Top -> 1.5 Mid + 1 A; Mid -> 2 A + 0.5 B => 1 Top = 4 A + 0.75 B.
                    addRecipe(prodMid, prodA, 2m);
                    addRecipe(prodMid, prodB, 0.5m);
                    addRecipe(prodTop, prodMid, 1.5m);
                    addRecipe(prodTop, prodA, 1m);
                    // §4 #6: Món gây âm kho: 9 A + 2 B + 3 C.
                    addRecipe(prodNegDrink, prodA, 9m);
                    addRecipe(prodNegDrink, prodB, 2m);
                    addRecipe(prodNegDrink, prodC, 3m);

                    // User chỉ gắn store1.
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO SUSER (ID, USERNAME, NAME, \"PASSWORD\", ISADMIN, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @U, @N, '1', 0, 30, 'selftest_xl', @T)",
                        "@I", userStore1, "@U", "xl_u1_" + tag, "@N", "User XL1", "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @U, @S, 30, 'selftest_xl', @T)",
                        "@I", userStoreLink, "@U", userStore1, "@S", store1, "@T", now);
                });

                // Nhập kho 20 A @ 10.000 và 20 B @ 5.000 ở wh1 ngày 2004-01-05 (cô lập bằng năm 2004).
                MrInsertPurchase(db, createdOrders, "PN-XL1-" + tag, new DateTime(2004, 1, 5), wh1, store1, prodA, unitId, 20m, 200000m);
                MrInsertPurchase(db, createdOrders, "PN-XLB-" + tag, new DateTime(2004, 1, 5), wh1, store1, prodB, unitId, 20m, 100000m);

                // §4 #1: Bill B1 đã thanh toán ngày 2004-01-10: bán 3 Món X, dòng XUATVATTU cũ A = 6.
                string b1 = MrInsertBill(db, createdOrders, "HD-XL1-" + tag, new DateTime(2004, 1, 10), 0, 30, 30, table1, store1);
                string b1SaleLine = MrInsertDetail(db, b1, prodX, "Món X " + tag, unitId, null, 3m, 0, 20000m);
                MrInsertDetail(db, b1, prodA, "Nguyên liệu A " + tag, unitId, wh1, 6m, 30, 10000m);

                // §4 #2: Bill B2 đã thanh toán ngày 2004-01-12: bán 2 Món Top (2 tầng), chưa có dòng xuất vật tư.
                string b2 = MrInsertBill(db, createdOrders, "HD-XL2-" + tag, new DateTime(2004, 1, 12), 0, 30, 30, table1, store1);
                MrInsertDetail(db, b2, prodTop, "Món 2 tầng " + tag, unitId, null, 2m, 0, 0m);

                // §4 #3: Các bill loại trừ, mỗi bill có dòng xuất vật tư mốc C = 77 để kiểm không bị đụng.
                string bBefore = MrInsertBill(db, createdOrders, "HD-XLB-" + tag, new DateTime(2004, 1, 9), 0, 30, 30, table1, store1);
                MrInsertDetail(db, bBefore, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bBefore, prodC, "C", unitId, wh1, 77m, 30, 0m);

                string bAfter = MrInsertBill(db, createdOrders, "HD-XLA-" + tag, new DateTime(2004, 1, 21), 0, 30, 30, table1, store1);
                MrInsertDetail(db, bAfter, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bAfter, prodC, "C", unitId, wh1, 77m, 30, 0m);

                string bUnpaid = MrInsertBill(db, createdOrders, "HD-XLU-" + tag, new DateTime(2004, 1, 15), 0, 30, 0, table1, store1);
                MrInsertDetail(db, bUnpaid, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bUnpaid, prodC, "C", unitId, wh1, 77m, 30, 0m);

                string bCancelled = MrInsertBill(db, createdOrders, "HD-XLC-" + tag, new DateTime(2004, 1, 15), 0, -30, 30, table1, store1);
                MrInsertDetail(db, bCancelled, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bCancelled, prodC, "C", unitId, wh1, 77m, 30, 0m);

                string bNoTable = MrInsertBill(db, createdOrders, "HD-XLNT-" + tag, new DateTime(2004, 1, 15), 0, 30, 30, null, store1);
                MrInsertDetail(db, bNoTable, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bNoTable, prodC, "C", unitId, wh1, 77m, 30, 0m);

                string bOtherStore = MrInsertBill(db, createdOrders, "HD-XLS2-" + tag, new DateTime(2004, 1, 15), 0, 30, 30, table2, store2);
                MrInsertDetail(db, bOtherStore, prodX, "Món X", unitId, null, 1m, 0, 0m);
                MrInsertDetail(db, bOtherStore, prodC, "C", unitId, wh2, 77m, 30, 0m);

                // Tắt TuDongTinhGiaVon (= 0) để chứng minh Xuất lại vẫn tính lại giá vốn; bật lưu vết (= 30).
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "TuDongTinhGiaVon", 0);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);

                // Đổi công thức Món X: 2 x A -> 3 x A.
                BookingExec(db, "UPDATE DDINHLUONG SET SOLUONG = 3 WHERE DMATHANGID = @P AND DVATTUID = @V", "@P", prodX, "@V", prodA);

                var svc = new MaterialRegenService(AppSession.Current);
                var reqRangeS1 = new MaterialRegenRequest
                {
                    FromDate = new DateTime(2004, 1, 10),
                    ToDate = new DateTime(2004, 1, 20),
                    StoreId = store1,
                    StoreName = "CH XL1 " + tag,
                    ConfirmText = "xacnhan"
                };

                // §4 #4: Trống ngày hoặc Từ > Đến bị chặn và KHÔNG xóa dòng nào.
                int expLinesBeforeBad = MrCountAllExportLines(db, new[] { b1, b2, bBefore, bAfter, bUnpaid, bCancelled, bNoTable, bOtherStore });
                MaterialRegenResult rEmptyFrom = svc.Execute(new MaterialRegenRequest { FromDate = null, ToDate = new DateTime(2004, 1, 20), StoreId = store1, ConfirmText = "XACNHAN" });
                MaterialRegenResult rEmptyTo = svc.Execute(new MaterialRegenRequest { FromDate = new DateTime(2004, 1, 10), ToDate = null, StoreId = store1, ConfirmText = "XACNHAN" });
                MaterialRegenResult rReversed = svc.Execute(new MaterialRegenRequest { FromDate = new DateTime(2004, 1, 20), ToDate = new DateTime(2004, 1, 10), StoreId = store1, ConfirmText = "XACNHAN" });
                int expLinesAfterBad = MrCountAllExportLines(db, new[] { b1, b2, bBefore, bAfter, bUnpaid, bCancelled, bNoTable, bOtherStore });
                Check("Xuất lại §4 #4: trống Từ/Đến ngày hoặc Từ ngày sau Đến ngày bị chặn và không xóa dòng nào",
                    !rEmptyFrom.Ok && rEmptyFrom.Message == MaterialRegenConstants.DateRequired
                    && !rEmptyTo.Ok && rEmptyTo.Message == MaterialRegenConstants.DateRequired
                    && !rReversed.Ok && rReversed.Message == MaterialRegenConstants.DateRangeInvalid
                    && expLinesBeforeBad == expLinesAfterBad && MrReadExportQty(db, b1, prodA) == 6m,
                    MrOneLine("before=" + expLinesBeforeBad + ", after=" + expLinesAfterBad + ", b1A=" + MrReadExportQty(db, b1, prodA)));

                // §4 #5: Xem trước đếm đúng (2 HĐ b1 & b2, xóa 1 dòng của b1, sinh lại 1+2=3 dòng) và KHÔNG ghi gì; sai XACNHAN bị chặn.
                int logBeforePrev = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F", "@F", MaterialRegenConstants.FunctionName);
                MaterialRegenPreview prev = svc.Preview(reqRangeS1);
                MaterialRegenResult badConfirm = svc.Execute(new MaterialRegenRequest
                {
                    FromDate = reqRangeS1.FromDate, ToDate = reqRangeS1.ToDate, StoreId = store1, ConfirmText = "SAI"
                });
                int logAfterPrev = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F", "@F", MaterialRegenConstants.FunctionName);
                Check("Xuất lại §4 #5: Xem trước đếm đúng (2 HĐ, xóa 1, sinh 3) không ghi DB; xác nhận sai XACNHAN bị chặn",
                    prev.Ok && prev.EligibleBillCount == 2 && prev.ExistingExportLineCount == 1 && prev.NewExportLineCount == 3
                    && !badConfirm.Ok && badConfirm.Message == MaterialRegenConstants.ConfirmWordInvalid
                    && MrReadExportQty(db, b1, prodA) == 6m && MrReadExportQty(db, b2, prodA) == 0m
                    && logBeforePrev == logAfterPrev,
                    MrOneLine("bills=" + prev.EligibleBillCount + ", del=" + prev.ExistingExportLineCount + ", ins=" + prev.NewExportLineCount + ", b1A=" + MrReadExportQty(db, b1, prodA)));

                // §4 #6 (rollback): hook ném lỗi giữa chừng -> rollback sạch, dòng cũ b1 A=6 còn nguyên, b2 chưa có dòng, chưa có lưu vết.
                bool rbThrew = false;
                try
                {
                    svc.Execute(reqRangeS1, null, billId => { throw new InvalidOperationException("hook rollback"); });
                }
                catch (InvalidOperationException)
                {
                    rbThrew = true;
                }
                int logAfterRb = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F", "@F", MaterialRegenConstants.FunctionName);
                Check("Xuất lại §4 #6 (rollback): lỗi giữa chừng rollback toàn bộ, dòng cũ còn nguyên, không dòng mới, không lưu vết",
                    rbThrew && MrReadExportQty(db, b1, prodA) == 6m && MrReadExportQty(db, b2, prodA) == 0m && logAfterRb == logBeforePrev,
                    MrOneLine("threw=" + rbThrew + ", b1A=" + MrReadExportQty(db, b1, prodA) + ", b2A=" + MrReadExportQty(db, b2, prodA)));

                // Dùng userStore1 (có quyền Sửa, chỉ thuộc store1, StoreId rỗng) để kiểm lọc cửa hàng non-admin & chạy thật.
                var permsEdit = new FunctionPermissions(false);
                permsEdit.Add("fn_xl", MaterialRegenConstants.FunctionName, RoleModes.View | RoleModes.Edit);
                var svcUser1 = new MaterialRegenService(new UserSession
                {
                    UserId = userStore1, UserName = "xl_u1_" + tag, DisplayName = "User XL1",
                    IsAdmin = false, DatabasePath = db, Permissions = permsEdit
                });
                string longStoreName255 = "CH_" + tag + "_" + new string('S', 255 - ("CH_" + tag + "_").Length);
                MaterialRegenResult rRun1 = svcUser1.Execute(new MaterialRegenRequest
                {
                    FromDate = new DateTime(2004, 1, 10),
                    ToDate = new DateTime(2004, 1, 20),
                    StoreId = "",
                    StoreName = longStoreName255,
                    ConfirmText = "xacnhan"
                });

                // §4 #1: B1 xuất A = 9 tại wh1; tồn A tại wh1 = 20 - 9 (b1) - 8 (b2) = 3; giá vốn X = 30.000 (3 x 10.000); 0 mặt hàng tồn âm.
                decimal b1QtyA = MrReadExportQty(db, b1, prodA);
                string b1WhA = MrReadExportWarehouse(db, b1, prodA);
                decimal actualStockA = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, prodA, wh1, ""));
                decimal saleXCost = ReadLineCost(db, b1SaleLine);
                decimal prodXCost = ReadProductCost(db, prodX);
                Check("Xuất lại §4 #1: món X đổi 2->3 x A bán 3 phần => dòng xuất A=9 ở kho bàn, tồn A=3 đúng, giá vốn X=30.000 (dù TuDongTinhGiaVon=0)",
                    rRun1.Ok && string.IsNullOrEmpty(rRun1.CostWarning) && rRun1.NegativeStockProductCount == 0
                    && b1QtyA == 9m && b1WhA == wh1 && actualStockA == 3m
                    && saleXCost == 30000m && prodXCost == 30000m,
                    MrOneLine("b1A=" + b1QtyA + ", stockA=" + actualStockA + ", lineX=" + saleXCost + ", prodX=" + prodXCost + ", neg=" + rRun1.NegativeStockProductCount));

                // §4 #2: Món 2 tầng trên b2 khớp 100% ComputeMaterialExportsIn (A=8, B=1.5 tại wh1).
                List<MaterialExportItem> expectedB2 = DbHelper.Read(db, conn =>
                {
                    ServiceOrderHeader h = ServiceOrderRepository.LoadBillIn(conn, null, b2);
                    return ServicePaymentRepository.ComputeMaterialExportsIn(conn, null, h);
                });
                decimal b2QtyA = MrReadExportQty(db, b2, prodA);
                decimal b2QtyB = MrReadExportQty(db, b2, prodB);
                int b2ExpCount = MrCountAllExportLines(db, new[] { b2 });
                bool matchCompute = expectedB2.Count == 2 && b2ExpCount == 2
                    && expectedB2.Exists(x => x.MaterialId == prodA && x.WarehouseId == wh1 && x.Quantity == 8m)
                    && expectedB2.Exists(x => x.MaterialId == prodB && x.WarehouseId == wh1 && x.Quantity == 1.5m)
                    && b2QtyA == 8m && b2QtyB == 1.5m;
                Check("Xuất lại §4 #2: món định lượng 2 tầng sinh đúng A=8, B=1.5 giống hệt ComputeMaterialExportsIn",
                    matchCompute,
                    MrOneLine("cnt=" + b2ExpCount + ", A=" + b2QtyA + ", B=" + b2QtyB));

                // §4 #3: Hóa đơn ngoài khoảng, chưa thanh toán, đã hủy, không có bàn, cửa hàng khác (user thường) không đổi (giữ C=77).
                bool untouchedOk = MrReadExportQty(db, bBefore, prodC) == 77m
                    && MrReadExportQty(db, bAfter, prodC) == 77m
                    && MrReadExportQty(db, bUnpaid, prodC) == 77m
                    && MrReadExportQty(db, bCancelled, prodC) == 77m
                    && MrReadExportQty(db, bNoTable, prodC) == 77m
                    && MrReadExportQty(db, bOtherStore, prodC) == 77m;
                Check("Xuất lại §4 #3: ngoài khoảng, chưa thanh toán, đã hủy, không bàn, cửa hàng khác không bị đụng",
                    untouchedOk,
                    MrOneLine("bef=" + MrReadExportQty(db, bBefore, prodC) + ", aft=" + MrReadExportQty(db, bAfter, prodC)
                        + ", unp=" + MrReadExportQty(db, bUnpaid, prodC) + ", can=" + MrReadExportQty(db, bCancelled, prodC)
                        + ", nt=" + MrReadExportQty(db, bNoTable, prodC) + ", s2=" + MrReadExportQty(db, bOtherStore, prodC)));

                // §4 #7 (lưu vết một dòng + cắt 255): đúng 1 dòng mới trong TLUUVET.
                int logAfterRun1 = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F", "@F", MaterialRegenConstants.FunctionName);
                DataTable lastLog = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                    "SELECT FIRST 1 TAIKHOAN, BAN, NOTE FROM TLUUVET WHERE CHUCNANG = @F ORDER BY TIMECREATED DESC",
                    "@F", MaterialRegenConstants.FunctionName));
                bool logOk = logAfterRun1 == logBeforePrev + 1 && lastLog.Rows.Count == 1
                    && (DbHelper.Str(lastLog.Rows[0]["BAN"]) ?? "").Length <= 255
                    && (DbHelper.Str(lastLog.Rows[0]["NOTE"]) ?? "").Length <= 255
                    && (DbHelper.Str(lastLog.Rows[0]["NOTE"]) ?? "").IndexOf("10/01/2004", StringComparison.Ordinal) >= 0;
                Check("Xuất lại §4 #7 (lưu vết): ghi đúng một dòng vào TLUUVET, cắt chuỗi <= 255 không tràn",
                    logOk,
                    MrOneLine("diff=" + (logAfterRun1 - logBeforePrev) + ", noteLen=" + (lastLog.Rows.Count > 0 ? (DbHelper.Str(lastLog.Rows[0]["NOTE"]) ?? "").Length : -1)));

                // §4 #6 (chạy hai lần cùng kết quả): chạy lần 2 trên cùng khoảng -> cùng số dòng & tồn kho.
                MaterialRegenResult rRun2 = svcUser1.Execute(new MaterialRegenRequest
                {
                    FromDate = new DateTime(2004, 1, 10), ToDate = new DateTime(2004, 1, 20), StoreId = "", ConfirmText = "XACNHAN"
                });
                decimal stockAAfter2 = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, prodA, wh1, ""));
                Check("Xuất lại §4 #6 (lặp 2 lần): chạy lần hai cho cùng kết quả (2 HĐ, xóa 3, sinh 3, tồn A=3 không đổi)",
                    rRun2.Ok && rRun2.ProcessedBillCount == 2 && rRun2.DeletedLineCount == 3 && rRun2.InsertedLineCount == 3
                    && MrReadExportQty(db, b1, prodA) == 9m && MrReadExportQty(db, b2, prodA) == 8m && MrReadExportQty(db, b2, prodB) == 1.5m
                    && stockAAfter2 == 3m,
                    MrOneLine("del=" + rRun2.DeletedLineCount + ", ins=" + rRun2.InsertedLineCount + ", stockA=" + stockAAfter2));

                // §4 #6 (tồn âm): kho whNeg (CHOPHEPAMKHO = 0) nhập 5 A, 1 B, 100 C; bill ngày 2004-02-10 bán 1 Món gây âm (9 A, 2 B, 3 C)
                // -> A = -4 (<0), B = -1 (<0), C = +97 (>=0) => báo ĐÚNG 2 mặt hàng tồn âm, KHÔNG bị chặn âm kho!
                MrInsertPurchase(db, createdOrders, "PN-NEG-A-" + tag, new DateTime(2004, 2, 1), whNeg, store1, prodA, unitId, 5m, 50000m);
                MrInsertPurchase(db, createdOrders, "PN-NEG-B-" + tag, new DateTime(2004, 2, 1), whNeg, store1, prodB, unitId, 1m, 5000m);
                MrInsertPurchase(db, createdOrders, "PN-NEG-C-" + tag, new DateTime(2004, 2, 1), whNeg, store1, prodC, unitId, 100m, 200000m);
                string bNeg = MrInsertBill(db, createdOrders, "HD-XLNEG-" + tag, new DateTime(2004, 2, 10), 0, 30, 30, tableNeg, store1);
                MrInsertDetail(db, bNeg, prodNegDrink, "Món gây âm " + tag, unitId, null, 1m, 0, 0m);

                MaterialRegenResult rNeg = svc.Execute(new MaterialRegenRequest
                {
                    FromDate = new DateTime(2004, 2, 10), ToDate = new DateTime(2004, 2, 10), StoreId = store1, ConfirmText = "XACNHAN"
                });
                decimal negA = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, prodA, whNeg, ""));
                decimal negB = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, prodB, whNeg, ""));
                decimal posC = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, prodC, whNeg, ""));
                Check("Xuất lại §4 #6 (tồn âm): không chặn âm kho (dù CHOPHEPAMKHO=0) và báo đúng 2 mặt hàng có tồn âm (A=-4, B=-1, C=97)",
                    rNeg.Ok && rNeg.NegativeStockProductCount == 2 && negA == -4m && negB == -1m && posC == 97m,
                    MrOneLine("negCnt=" + rNeg.NegativeStockProductCount + ", A=" + negA + ", B=" + negB + ", C=" + posC));

                // §4 #7 (lỗi tính giá vốn chỉ cảnh báo, kết quả xuất lại còn nguyên).
                BookingExec(db, "UPDATE DDINHLUONG SET SOLUONG = 4 WHERE DMATHANGID = @P AND DVATTUID = @V", "@P", prodX, "@V", prodA);
                MaterialRegenResult rCostWarn = svc.Execute(
                    new MaterialRegenRequest { FromDate = new DateTime(2004, 1, 10), ToDate = new DateTime(2004, 1, 10), StoreId = store1, ConfirmText = "XACNHAN" },
                    null,
                    null,
                    s => CostRecalcService.RunAfterRegenerate(s, () => { throw new InvalidOperationException("gia lap loi gia von"); }));
                decimal b1QtyAfterCostFail = MrReadExportQty(db, b1, prodA);
                Check("Xuất lại §4 #7 (lỗi giá vốn): chỉ cảnh báo đúng một câu, kết quả xuất lại (A=12) vẫn được lưu nguyên",
                    rCostWarn.Ok && rCostWarn.CostWarning == MaterialRegenConstants.CostRecalcFailedWarning && b1QtyAfterCostFail == 12m,
                    MrOneLine("ok=" + rCostWarn.Ok + ", warn=" + rCostWarn.CostWarning + ", b1A=" + b1QtyAfterCostFail));

                // §4 #7 (quyền chỉ Xem): Xem trước được, Thực hiện bị chặn.
                var permsViewOnly = new FunctionPermissions(false);
                permsViewOnly.Add("fn_xl", MaterialRegenConstants.FunctionName, RoleModes.View);
                var svcViewOnly = new MaterialRegenService(new UserSession
                {
                    UserId = userStore1, UserName = "xl_ro", DisplayName = "Chỉ xem",
                    IsAdmin = false, DatabasePath = db, Permissions = permsViewOnly
                });
                MaterialRegenPreview prevRo = svcViewOnly.Preview(reqRangeS1);
                MaterialRegenResult execRo = svcViewOnly.Execute(reqRangeS1);
                Check("Xuất lại §4 #7 (quyền chỉ Xem): xem trước được nhưng Thực hiện bị chặn đúng thông báo",
                    svcViewOnly.CanView && !svcViewOnly.CanRun && prevRo.Ok
                    && !execRo.Ok && execRo.Message == MaterialRegenConstants.NoPermission,
                    MrOneLine("prevOk=" + prevRo.Ok + ", execOk=" + execRo.Ok + ", msg=" + execRo.Message));
            }
            catch (Exception ex)
            {
                Check("Xuất lại DB: ngoại lệ không mong đợi", false, MrOneLine(ex.ToString()));
            }
            finally
            {
                try
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE CHUCNANG = @F AND USERCREATEDID IN ('selftest_xl', @U1)",
                        "@F", MaterialRegenConstants.FunctionName, "@U1", userStore1);
                    foreach (string oid in createdOrders)
                    {
                        BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                        SafeDeleteById(db, "TDONHANG", oid);
                    }
                    foreach (string rid in createdRecipes)
                        SafeDeleteById(db, "DDINHLUONG", rid);
                    SafeDeleteById(db, "DMATHANG", prodX);
                    SafeDeleteById(db, "DMATHANG", prodMid);
                    SafeDeleteById(db, "DMATHANG", prodTop);
                    SafeDeleteById(db, "DMATHANG", prodNegDrink);
                    SafeDeleteById(db, "DMATHANG", prodA);
                    SafeDeleteById(db, "DMATHANG", prodB);
                    SafeDeleteById(db, "DMATHANG", prodC);
                    if (!string.IsNullOrEmpty(table1)) SafeDeleteById(db, "DBAN", table1);
                    if (!string.IsNullOrEmpty(table2)) SafeDeleteById(db, "DBAN", table2);
                    if (!string.IsNullOrEmpty(tableNeg)) SafeDeleteById(db, "DBAN", tableNeg);
                    SafeDeleteById(db, "DKHUVUC", area1);
                    SafeDeleteById(db, "DKHUVUC", area2);
                    SafeDeleteById(db, "DKHUVUC", areaNeg);
                    SafeDeleteById(db, "DKHOHANG", wh1);
                    SafeDeleteById(db, "DKHOHANG", wh2);
                    SafeDeleteById(db, "DKHOHANG", whNeg);
                    SafeDeleteById(db, "DDONVITINH", unitId);
                    SafeDeleteById(db, "TNGUOIDUNGTHEOCUAHANG", userStoreLink);
                    SafeDeleteById(db, "SUSER", userStore1);
                    SafeDeleteById(db, "DCUAHANG", store1);
                    SafeDeleteById(db, "DCUAHANG", store2);
                    RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
                    foreach (KeyValuePair<string, decimal> kv in lineSnap)
                        BookingExec(db, "UPDATE TDONHANGCHITIET SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                    foreach (KeyValuePair<string, decimal> kv in prodSnap)
                        BookingExec(db, "UPDATE DMATHANG SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                }
                catch (Exception ex)
                {
                    Check("Xuất lại DB: dọn dữ liệu", false, MrOneLine(ex.Message));
                }
            }
        }
    }
}
