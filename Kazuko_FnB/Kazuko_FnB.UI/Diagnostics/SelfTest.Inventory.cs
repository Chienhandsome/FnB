using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void InventoryTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tk",
                    UserName = "selftest_tk",
                    DisplayName = "Selftest TonKho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                InventoryRulesPureTests();
                InventoryStockFormulaAndScopeDbTests(db);
                InventoryUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void InventoryRulesPureTests()
        {
            // Spec §4 #2: 2 ĐVT — tồn 50, quy đổi 24 -> "2 thùng 2 chai"; biên 48, 50.5, 10, 1 ĐVT.
            string f50 = InventoryRules.FormatTwoUnits(50m, true, 24m, "thùng", "chai");
            string f48 = InventoryRules.FormatTwoUnits(48m, true, 24m, "thùng", "chai");
            string f505 = InventoryRules.FormatTwoUnits(50.5m, true, 24m, "thùng", "chai");
            string f10 = InventoryRules.FormatTwoUnits(10m, true, 24m, "thùng", "chai");
            string fOne = InventoryRules.FormatTwoUnits(50m, false, 24m, "thùng", "chai");

            Check("Tồn kho §4 #2 (luật 2 ĐVT): tồn 50 quy đổi 24 ra \"2 thùng 2 chai\" và các ca biên đúng",
                f50 == "2 thùng 2 chai" && f48 == "2 thùng 0 chai" && f505 == "2 thùng 2,5 chai"
                && f10 == "0 thùng 10 chai" && fOne == "",
                SingleLine("f50=" + f50 + ", f48=" + f48 + ", f505=" + f505 + ", f10=" + f10));

            // Tông màu dòng & giá trị vốn/bán: tồn âm -> đỏ (ưu tiên hơn vàng), tồn <= tối thiểu (>0) -> vàng, TONTOITHIEU=0 -> bình thường.
            var rNeg = new InventoryItemRow { StockQuantity = -3m, MinStock = 5m, CostPrice = 20000m, SellPrice = 30000m };
            var rLow = new InventoryItemRow { StockQuantity = 5m, MinStock = 5m, CostPrice = 10000m, SellPrice = 15000m };
            var rZeroNoMin = new InventoryItemRow { StockQuantity = 0m, MinStock = 0m, CostPrice = 10000m, SellPrice = 15000m };
            var rNormal = new InventoryItemRow { StockQuantity = 20m, MinStock = 5m, CostPrice = 10000m, SellPrice = 15000m };
            InventoryRules.ApplyMetrics(new[] { rNeg, rLow, rZeroNoMin, rNormal });

            InventoryTotals totals = InventoryRules.ComputeTotals(new[] { rNeg, rLow, rZeroNoMin, rNormal });

            // Lọc dưới tồn tối thiểu & ẩn tồn 0 & tìm không dấu.
            bool belowMinOk = InventoryRules.MatchesFilter(rNeg, new InventoryFilter { OnlyBelowMinStock = true })
                && InventoryRules.MatchesFilter(rLow, new InventoryFilter { OnlyBelowMinStock = true })
                && !InventoryRules.MatchesFilter(rZeroNoMin, new InventoryFilter { OnlyBelowMinStock = true })
                && !InventoryRules.MatchesFilter(rNormal, new InventoryFilter { OnlyBelowMinStock = true });

            bool hideZeroOk = InventoryRules.MatchesFilter(rNeg, new InventoryFilter { HideZeroStock = true })
                && !InventoryRules.MatchesFilter(rZeroNoMin, new InventoryFilter { HideZeroStock = true })
                && InventoryRules.MatchesFilter(rNormal, new InventoryFilter { HideZeroStock = true });

            var rSearch = new InventoryItemRow { Code = "NL-SUA-01", Name = "Sữa Đặc Có Đường" };
            bool searchOk = InventoryRules.MatchesFilter(rSearch, new InventoryFilter { SearchText = "sua dac" })
                && InventoryRules.MatchesFilter(rSearch, new InventoryFilter { SearchText = "nl-sua" })
                && !InventoryRules.MatchesFilter(rSearch, new InventoryFilter { SearchText = "ca phe" });

            // Chọn kho mặc định theo cửa hàng đăng nhập.
            var whs = new List<WarehouseRow>
            {
                new WarehouseRow { Id = "W1", StoreId = "S1", Name = "Kho 1" },
                new WarehouseRow { Id = "W2", StoreId = "S2", Name = "Kho 2" },
                new WarehouseRow { Id = "W3", StoreId = "S2", Name = "Kho 3" }
            };
            bool defWhOk = InventoryRules.ResolveDefaultWarehouseId(whs, "S2", "W3") == "W3"
                && InventoryRules.ResolveDefaultWarehouseId(whs, "S2", null) == "W2"
                && InventoryRules.ResolveDefaultWarehouseId(whs, "S9", null) == "W1";

            Check("Tồn kho §4 #1 & #3 (luật thuần): tông đỏ/vàng/bình thường, giá trị vốn/bán, lọc dưới tối thiểu/ẩn 0/tìm không dấu, kho mặc định",
                rNeg.Tone == InventoryRowTone.NegativeStock && rLow.Tone == InventoryRowTone.LowStock
                && rZeroNoMin.Tone == InventoryRowTone.Normal && rNormal.Tone == InventoryRowTone.Normal
                && rNeg.CostValue == -60000m && rNeg.SellValue == -90000m
                && rNormal.CostValue == 200000m && rNormal.SellValue == 300000m
                && totals.TotalCostValue == 190000m && totals.TotalSellValue == 285000m
                && belowMinOk && hideZeroOk && searchOk && defWhOk,
                SingleLine("cost=" + totals.TotalCostValue + ", sell=" + totals.TotalSellValue + ", filt=" + belowMinOk + "/" + hideZeroOk + "/" + searchOk));
        }

        private static void InventoryStockFormulaAndScopeDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string whA = DbHelper.NewId();
            string whB = DbHelper.NewId();
            string unitChai = DbHelper.NewId();
            string unitThung = DbHelper.NewId();
            string prodCalc = DbHelper.NewId();
            string prodTwoUnit = DbHelper.NewId();
            string prodNeg = DbHelper.NewId();
            string prodZero = DbHelper.NewId();
            string prodRecipe = DbHelper.NewId();
            string prodInactive = DbHelper.NewId();
            var orderIds = new List<string>();

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", storeA, "@N", "CH TK A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", storeB, "@N", "CH TK B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tk', @T)", "@I", whA, "@N", "Kho TK A " + tag, "@S", storeA, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tk', @T)", "@I", whB, "@N", "Kho TK B " + tag, "@S", storeB, "@T", DbHelper.Now());
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whA, "@I", storeA);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whB, "@I", storeB);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", unitChai, "@N", "chai");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tk')", "@I", unitThung, "@N", "thùng");

                DateTime now = DbHelper.Now();
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 5, 10000, 12000, 20000, 30, 'selftest_tk', @T)",
                    "@I", prodCalc, "@C", "TK-CALC-" + tag, "@N", "Nước Ngọt " + tag, "@U", unitChai, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U1, @U2, 24, 10, 240000, 10000, 15000, 30, 'selftest_tk', @T)",
                    "@I", prodTwoUnit, "@C", "TK-2U-" + tag, "@N", "Bia Lon " + tag, "@U1", unitChai, "@U2", unitThung, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 5, 18000, 20000, 30000, 30, 'selftest_tk', @T)",
                    "@I", prodNeg, "@C", "TK-NEG-" + tag, "@N", "Đường Cát " + tag, "@U", unitChai, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 0, 5000, 6000, 10000, 30, 'selftest_tk', @T)",
                    "@I", prodZero, "@C", "TK-ZERO-" + tag, "@N", "Ly Nhựa " + tag, "@U", unitChai, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '1', @U, 0, 0, 0, 35000, 30, 'selftest_tk', @T)",
                    "@I", prodRecipe, "@C", "TK-REC-" + tag, "@N", "Trà Pha Chế " + tag, "@U", unitChai, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 0, 1000, 1000, 2000, 0, 'selftest_tk', @T)",
                    "@I", prodInactive, "@C", "TK-OFF-" + tag, "@N", "Hàng Ngừng Dùng " + tag, "@U", unitChai, "@T", now);

                // Seed chứng từ cho prodCalc theo đúng spec §4 #1:
                // nhập 10, bán 3 (đã thanh toán), xuất khác 2, chuyển đi 1 (whA -> whB) => tồn whA = 4, tồn whB = 1.
                // Kèm bill chưa thanh toán (SLXUAT = 7) và phiếu STATUS khác 30 (-30 nhập 50, 0 xuất 9) -> KHÔNG tính.
                orderIds.Add(InsertStockOrderLine(db, "PN-TK-" + tag, 1, 30, 30, whA, null, storeA, prodCalc, unitChai, 10m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "HD-PAID-" + tag, 0, 30, 30, null, whA, storeA, prodCalc, unitChai, 0m, 3m));
                orderIds.Add(InsertStockOrderLine(db, "PX-TK-" + tag, 2, 30, 30, null, whA, storeA, prodCalc, unitChai, 0m, 2m));

                string trId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DKHOXUATID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, 30, 30, @D, @DN, @DX, @S, 'selftest_tk', @T)",
                    "@I", trId, "@N", "CK-TK-" + tag, "@D", DateTime.Today, "@DN", whB, "@DX", whA, "@S", storeA, "@T", now);
                BookingExec(db,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @O, @P, 'CK-OUT', @U, @W, 0, 0, 1, 1, 30, 'selftest_tk', @T)",
                    "@I", DbHelper.NewId(), "@O", trId, "@P", prodCalc, "@U", unitChai, "@W", whA, "@T", now);
                BookingExec(db,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @O, @P, 'CK-IN', @U, @W, 1, 1, 0, 0, 30, 'selftest_tk', @T)",
                    "@I", DbHelper.NewId(), "@O", trId, "@P", prodCalc, "@U", unitChai, "@W", whB, "@T", now);
                orderIds.Add(trId);

                orderIds.Add(InsertStockOrderLine(db, "HD-UNPAID-" + tag, 0, 30, 0, null, whA, storeA, prodCalc, unitChai, 0m, 7m));
                orderIds.Add(InsertStockOrderLine(db, "PN-DEL-" + tag, 1, -30, 30, whA, null, storeA, prodCalc, unitChai, 50m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PX-OFF-" + tag, 2, 0, 30, null, whA, storeA, prodCalc, unitChai, 0m, 9m));

                // Seed prodTwoUnit = 50 chai tại whA; prodNeg = +2 - 5 = -3 tại whA.
                orderIds.Add(InsertStockOrderLine(db, "PN-2U-" + tag, 1, 30, 30, whA, null, storeA, prodTwoUnit, unitChai, 50m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-NEG-" + tag, 1, 30, 30, whA, null, storeA, prodNeg, unitChai, 2m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PX-NEG-" + tag, 2, 30, 30, null, whA, storeA, prodNeg, unitChai, 0m, 5m));

                var repo = new InventoryRepository(db);
                Dictionary<string, decimal> batchWhA = repo.LoadStockBalancesByWarehouse(whA);
                Dictionary<string, decimal> batchWhB = repo.LoadStockBalancesByWarehouse(whB);

                // Chứng minh hàm SQL gộp theo kho khớp 100% với ServiceOrderRepository.GetStockBalanceIn trên nhiều mặt hàng ở cả 2 kho.
                string[] checkIds = { prodCalc, prodTwoUnit, prodNeg, prodZero };
                bool parityOk = true;
                foreach (string pid in checkIds)
                {
                    decimal bA;
                    batchWhA.TryGetValue(pid, out bA);
                    decimal sA = repo.GetStockBalance(pid, whA);
                    decimal bB;
                    batchWhB.TryGetValue(pid, out bB);
                    decimal sB = repo.GetStockBalance(pid, whB);
                    if (bA != sA || bB != sB)
                        parityOk = false;
                }

                var adminSess = new UserSession
                {
                    UserId = "selftest_tk", UserName = "selftest_tk", DisplayName = "Admin TK",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH TK A " + tag }
                };
                var svc = new InventoryService(adminSess);
                List<InventoryItemRow> itemsA = svc.LoadWarehouseItems(whA);
                List<InventoryItemRow> itemsB = svc.LoadWarehouseItems(whB);

                InventoryItemRow rCalcA = itemsA.Find(x => x.ProductId == prodCalc);
                InventoryItemRow rCalcB = itemsB.Find(x => x.ProductId == prodCalc);
                InventoryItemRow rTwoA = itemsA.Find(x => x.ProductId == prodTwoUnit);
                InventoryItemRow rNegA = itemsA.Find(x => x.ProductId == prodNeg);
                InventoryItemRow rZeroA = itemsA.Find(x => x.ProductId == prodZero);
                bool excludesNonStock = !itemsA.Exists(x => x.ProductId == prodRecipe || x.ProductId == prodInactive);

                Check("Tồn kho §4 #1 (công thức tồn & khớp ServiceOrderRepository.GetStockBalanceIn): nhập 10 - bán 3 - xuất 2 - chuyển 1 = 4, bỏ bill chưa trả & STATUS<>30, khớp GetStockBalanceIn",
                    parityOk && excludesNonStock
                    && rCalcA != null && rCalcA.StockQuantity == 4m && rCalcA.CostValue == 48000m && rCalcA.SellValue == 80000m && rCalcA.Tone == InventoryRowTone.LowStock
                    && rCalcB != null && rCalcB.StockQuantity == 1m
                    && rTwoA != null && rTwoA.StockQuantity == 50m && rTwoA.TwoUnitDisplay == "2 thùng 2 chai"
                    && rNegA != null && rNegA.StockQuantity == -3m && rNegA.Tone == InventoryRowTone.NegativeStock
                    && rZeroA != null && rZeroA.StockQuantity == 0m && rZeroA.Tone == InventoryRowTone.Normal,
                    SingleLine("parity=" + parityOk + ", calcA=" + (rCalcA == null ? -999m : rCalcA.StockQuantity)
                        + ", calcB=" + (rCalcB == null ? -999m : rCalcB.StockQuantity)
                        + ", twoU=" + (rTwoA == null ? "null" : rTwoA.TwoUnitDisplay)
                        + ", neg=" + (rNegA == null ? -999m : rNegA.StockQuantity)));

                // Kiểm tra phạm vi kho non-admin ở lớp Service: user chỉ gán storeA không thấy whB và gọi thẳng whB trả rỗng.
                var permsView = new FunctionPermissions(false);
                permsView.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
                var nonAdminSess = new UserSession
                {
                    UserId = "u_tk_a_" + tag, UserName = "u_tk_a", DisplayName = "User TK A",
                    IsAdmin = false, DatabasePath = db, Permissions = permsView,
                    Stores = new List<StoreRecord> { new StoreRecord { Id = storeA, Name = "CH TK A " + tag } },
                    CurrentStore = new StoreRecord { Id = storeA, Name = "CH TK A " + tag }
                };
                var nonAdminSvc = new InventoryService(nonAdminSess);
                List<WarehouseRow> allowedWh = nonAdminSvc.LoadAllowedWarehouses();
                List<InventoryItemRow> blockedWhB = nonAdminSvc.LoadWarehouseItems(whB);
                List<InventoryItemRow> allowedWhA = nonAdminSvc.LoadWarehouseItems(whA);
                InventoryItemRow maskedCalc = allowedWhA.Find(x => x.ProductId == prodCalc);

                Check("Tồn kho §4 #3 & #4 (Service non-admin & không quyền Xem giá nhập): chặn kho cửa hàng khác, ẩn giá về 0 ở tầng Service",
                    allowedWh.Exists(w => w.Id == whA) && !allowedWh.Exists(w => w.Id == whB)
                    && blockedWhB.Count == 0
                    && maskedCalc != null && maskedCalc.StockQuantity == 4m
                    && maskedCalc.BuyPrice == 0m && maskedCalc.CostPrice == 0m && maskedCalc.CostValue == 0m && maskedCalc.SellValue == 0m,
                    SingleLine("whCnt=" + allowedWh.Count + ", blockedB=" + blockedWhB.Count + ", masked=" + (maskedCalc != null)));
            }
            finally
            {
                foreach (string oid in orderIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodCalc);
                SafeDeleteById(db, "DMATHANG", prodTwoUnit);
                SafeDeleteById(db, "DMATHANG", prodNeg);
                SafeDeleteById(db, "DMATHANG", prodZero);
                SafeDeleteById(db, "DMATHANG", prodRecipe);
                SafeDeleteById(db, "DMATHANG", prodInactive);
                SafeDeleteById(db, "DDONVITINH", unitChai);
                SafeDeleteById(db, "DDONVITINH", unitThung);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID IN (@A, @B)", "@A", storeA, "@B", storeB);
                SafeDeleteById(db, "DKHOHANG", whA);
                SafeDeleteById(db, "DKHOHANG", whB);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
            }
        }

        private static string InsertStockOrderLine(
            string db, string name, int loai, int status, int paidFlag,
            string whIn, string whOut, string storeId, string prodId, string unitId,
            decimal slNhap, decimal slXuat)
        {
            string orderId = DbHelper.NewId();
            string lineWh = !string.IsNullOrEmpty(whIn) ? whIn : whOut;
            DateTime now = DbHelper.Now();
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DKHOXUATID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @L, @S, @P, @D, @WN, @WX, @ST, 'selftest_tk', @T)",
                "@I", orderId, "@N", name, "@L", loai, "@S", status, "@P", paidFlag,
                "@D", DateTime.Today, "@WN", whIn, "@WX", whOut, "@ST", storeId, "@T", now);
            BookingExec(db,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @O, @M, @N, @U, @W, @SN, @SN, @SX, @SX, 30, 'selftest_tk', @T)",
                "@I", DbHelper.NewId(), "@O", orderId, "@M", prodId, "@N", name,
                "@U", unitId, "@W", lineWh, "@SN", slNhap, "@SX", slXuat, "@T", now);
            return orderId;
        }
    }
}
