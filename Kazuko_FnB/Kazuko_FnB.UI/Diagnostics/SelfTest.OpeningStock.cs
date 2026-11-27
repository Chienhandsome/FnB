using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Tồn kho ban đầu (docs/specs/so-du-dau-ky.md P1, §4 #1–#5).</summary>
    public static partial class SelfTest
    {
        private static void OpeningStockTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_td",
                    UserName = "selftest_td",
                    DisplayName = "Selftest TonDau",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                OpeningStockPureRuleTests();
                OpeningStockCrudAndNegativeStockDbTests(db);
                OpeningStockCostRecalcRollbackAnd255DbTests(db);
                OpeningStockUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void OpeningStockPureRuleTests()
        {
            DateTime defDate = OpeningStockRules.DefaultOpeningDate(new DateTime(2026, 11, 27));
            decimal amt1 = OpeningStockRules.CalcLineAmount(10m, 10000m);
            decimal amt2 = OpeningStockRules.CalcLineAmount(5m, 20000m);
            decimal amtRound = OpeningStockRules.CalcLineAmount(3m, 333.5m); // 1000.5 -> 1001

            var items = new List<OpeningStockItemRow>
            {
                new OpeningStockItemRow { ProductId = "P1", Code = "MH01", Name = "Trà Xanh", Quantity = 10m, CostPrice = 10000m, Amount = amt1 },
                new OpeningStockItemRow { ProductId = "P2", Code = "MH02", Name = "Sữa Đặc", Quantity = 5m, CostPrice = 20000m, Amount = amt2 },
                new OpeningStockItemRow { ProductId = "P3", Code = "MH03", Name = "Đường Trắng", Quantity = 0m, CostPrice = 15000m, Amount = 0m }
            };
            OpeningStockTotals totals = OpeningStockRules.ComputeTotals(items);

            string errNoWh = OpeningStockRules.Validate(new OpeningStockSaveRequest { WarehouseId = "", Items = items });
            string errNegQty = OpeningStockRules.Validate(new OpeningStockSaveRequest
            {
                WarehouseId = "W1",
                Items = new List<OpeningStockItemRow> { new OpeningStockItemRow { ProductId = "P1", Name = "Trà Xanh", Quantity = -2m, CostPrice = 10000m } }
            });
            string errNegCost = OpeningStockRules.Validate(new OpeningStockSaveRequest
            {
                WarehouseId = "W1",
                Items = new List<OpeningStockItemRow> { new OpeningStockItemRow { ProductId = "P1", Name = "Trà Xanh", Quantity = 2m, CostPrice = -100m } }
            });
            string validOk = OpeningStockRules.Validate(new OpeningStockSaveRequest { WarehouseId = "W1", Items = items });

            bool onlyEnteredOk = OpeningStockRules.MatchesFilter(items[0], new OpeningStockFilter { OnlyEntered = true })
                && !OpeningStockRules.MatchesFilter(items[2], new OpeningStockFilter { OnlyEntered = true });
            bool searchOk = OpeningStockRules.MatchesFilter(items[0], new OpeningStockFilter { SearchText = "tra xanh" })
                && OpeningStockRules.MatchesFilter(items[1], new OpeningStockFilter { SearchText = "mh02" })
                && !OpeningStockRules.MatchesFilter(items[2], new OpeningStockFilter { SearchText = "tra xanh" });

            Check("Tồn đầu P1 (luật thuần): ngày mặc định 31/12 năm trước, thành tiền, tổng 200.000, chặn âm nêu tên hàng, lọc chỉ đã nhập & tìm không dấu",
                defDate == new DateTime(2025, 12, 31)
                && amt1 == 100000m && amt2 == 100000m && amtRound == 1001m
                && totals.ItemCount == 3 && totals.EnteredCount == 2 && totals.TotalAmount == 200000m
                && errNoWh == OpeningStockConstants.WarehouseRequired
                && errNegQty == OpeningStockConstants.NegativeQtyBlocked("Trà Xanh")
                && errNegCost == OpeningStockConstants.NegativeCostBlocked("Trà Xanh")
                && validOk == null && onlyEnteredOk && searchOk,
                SingleLine("def=" + defDate.ToString("yyyy-MM-dd") + ", tot=" + totals.TotalAmount + ", neg=" + errNegQty));
        }

        private static void OpeningStockCrudAndNegativeStockDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whBlockNeg = DbHelper.NewId();
            string whAllowNeg = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodA = DbHelper.NewId();
            string prodB = DbHelper.NewId();
            string prodNullType = DbHelper.NewId();
            string prodRecipe = DbHelper.NewId();
            string prodInactive = DbHelper.NewId();
            var extraOrderIds = new List<string>();
            var touchedWhIds = new[] { whBlockNeg, whAllowNeg };

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", storeId, "@N", "CH TD " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_td', @T)",
                    "@I", whBlockNeg, "@N", "Kho Chặn Âm " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_td', @T)",
                    "@I", whAllowNeg, "@N", "Kho Cho Âm " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", unitId, "@N", "Cái " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 10000, 20000, 30, 'selftest_td', @T)",
                    "@I", prodA, "@C", "TD-A-" + tag, "@N", "Trà Xanh TD " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 20000, 35000, 30, 'selftest_td', @T)",
                    "@I", prodB, "@C", "TD-B-" + tag, "@N", "Sữa Đặc TD " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, NULL, @U, 5000, 10000, 30, 'selftest_td', @T)",
                    "@I", prodNullType, "@C", "TD-N-" + tag, "@N", "Mặt Hàng Không Loại " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '1', @U, 12000, 30000, 30, 'selftest_td', @T)",
                    "@I", prodRecipe, "@C", "TD-R-" + tag, "@N", "Món Pha Chế TD " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 8000, 15000, 0, 'selftest_td', @T)",
                    "@I", prodInactive, "@C", "TD-OFF-" + tag, "@N", "Hàng Ngừng Dùng TD " + tag, "@U", unitId, "@T", now);

                var svc = new OpeningStockService(AppSession.Current);
                var invSvc = new InventoryService(AppSession.Current);

                // Nạp trước khi có phiếu: đủ hàng loại '0', '2', NULL; loại '1' và STATUS=0 bị loại; mặc định giá vốn từ DMATHANG.GIAVON.
                OpeningStockDocument initDoc = svc.LoadDocument(whBlockNeg);
                OpeningStockItemRow initA = initDoc.Items.Find(x => x.ProductId == prodA);
                OpeningStockItemRow initB = initDoc.Items.Find(x => x.ProductId == prodB);
                OpeningStockItemRow initNull = initDoc.Items.Find(x => x.ProductId == prodNullType);
                bool loadFilterOk = initA != null && initA.CostPrice == 10000m && initA.Quantity == 0m
                    && initB != null && initB.CostPrice == 20000m && initB.Quantity == 0m
                    && initNull != null && initNull.CostPrice == 5000m
                    && !initDoc.Items.Exists(x => x.ProductId == prodRecipe || x.ProductId == prodInactive)
                    && initDoc.Date == OpeningStockRules.DefaultOpeningDate(DateTime.Today);

                // Spec §4 #1: 2 mặt hàng (10 cái giá vốn 10.000; 5 cái giá vốn 20.000) -> 1 phiếu LOAI=97, NAME='TONDAU', TONGCONG=TIENHANG=200.000.
                DateTime openDate = new DateTime(2025, 12, 31);
                OpeningStockSaveResult r1 = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 10m, CostPrice = 10000m },
                        new OpeningStockItemRow { ProductId = prodB, Name = "Sữa Đặc TD " + tag, UnitId = unitId, Quantity = 5m, CostPrice = 20000m },
                        new OpeningStockItemRow { ProductId = prodNullType, Name = "Mặt Hàng Không Loại " + tag, UnitId = unitId, Quantity = 0m, CostPrice = 5000m }
                    }
                });

                DataTable hdrDt = ReadOpeningHeaders(db, whBlockNeg);
                DataTable lineDt = r1.Ok ? ReadOpeningLines(db, r1.SavedOrderId) : new DataTable();
                bool hdrOk = r1.Ok && hdrDt.Rows.Count == 1
                    && DbHelper.Int(hdrDt.Rows[0]["LOAI"]) == 97
                    && DbHelper.Str(hdrDt.Rows[0]["NAME"]) == "TONDAU"
                    && DbHelper.Int(hdrDt.Rows[0]["STATUS"]) == 30
                    && DbHelper.Int(hdrDt.Rows[0]["DATHANHTOAN"]) == 30
                    && DbHelper.Dec(hdrDt.Rows[0]["TIENHANG"]) == 200000m
                    && DbHelper.Dec(hdrDt.Rows[0]["TONGCONG"]) == 200000m
                    && DbHelper.Dec(hdrDt.Rows[0]["TILETHUE"]) == 0m
                    && DbHelper.Str(hdrDt.Rows[0]["DKHONHAPID"]) == whBlockNeg;

                bool linesOk = lineDt.Rows.Count == 2
                    && MatchOpeningLine(lineDt, prodA, whBlockNeg, 10m, 10000m, 100000m)
                    && MatchOpeningLine(lineDt, prodB, whBlockNeg, 5m, 20000m, 100000m);

                // Tồn kho của 2 mặt hàng đúng trên cả hàm tồn chung và màn Tồn kho.
                decimal balA = invSvc.GetStockBalance(prodA, whBlockNeg);
                decimal balB = invSvc.GetStockBalance(prodB, whBlockNeg);
                List<InventoryItemRow> invRows = invSvc.LoadWarehouseItems(whBlockNeg);
                InventoryItemRow invA = invRows.Find(x => x.ProductId == prodA);
                InventoryItemRow invB = invRows.Find(x => x.ProductId == prodB);
                bool stockOk = balA == 10m && balB == 5m
                    && invA != null && invA.StockQuantity == 10m
                    && invB != null && invB.StockQuantity == 5m;

                Check("Tồn đầu P1 §4 #1 (lưu 2 mặt hàng 10x10.000 & 5x20.000): 1 phiếu LOAI=97 NAME=TONDAU TONGCONG=TIENHANG=200.000, đúng 2 dòng & tồn kho chuẩn",
                    loadFilterOk && hdrOk && linesOk && stockOk,
                    SingleLine("load=" + loadFilterOk + ", hdr=" + hdrOk + ", lines=" + linesOk + ", bal=" + balA + "/" + balB));

                // Spec §4 #2: sửa số lượng, đưa prodB về 0 -> xóa dòng; lưu nhiều lần chỉ 1 phiếu/kho; đưa hết về 0 -> xóa phiếu.
                string firstOrderId = r1.SavedOrderId;
                OpeningStockSaveResult r2 = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 12m, CostPrice = 11000m },
                        new OpeningStockItemRow { ProductId = prodB, Name = "Sữa Đặc TD " + tag, UnitId = unitId, Quantity = 0m, CostPrice = 20000m }
                    }
                });
                DataTable lineAfterZeroB = ReadOpeningLines(db, firstOrderId);
                bool zeroBDeletedOk = r2.Ok && r2.SavedOrderId == firstOrderId
                    && ReadOpeningHeaders(db, whBlockNeg).Rows.Count == 1
                    && lineAfterZeroB.Rows.Count == 1
                    && MatchOpeningLine(lineAfterZeroB, prodA, whBlockNeg, 12m, 11000m, 132000m)
                    && r2.TotalAmount == 132000m;

                // Lưu lần 3 trên cùng kho vẫn đúng 1 phiếu.
                OpeningStockSaveResult r3 = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 8m, CostPrice = 10000m }
                    }
                });
                bool stillSingleOrder = r3.Ok && r3.SavedOrderId == firstOrderId
                    && ReadOpeningHeaders(db, whBlockNeg).Rows.Count == 1
                    && invSvc.GetStockBalance(prodA, whBlockNeg) == 8m;

                // Đưa hết về 0 -> xóa cả phiếu LOAI = 97.
                OpeningStockSaveResult rDel = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 0m, CostPrice = 10000m }
                    }
                });
                bool allZeroDeletedOrder = rDel.Ok && rDel.OrderDeleted && string.IsNullOrEmpty(rDel.SavedOrderId)
                    && ReadOpeningHeaders(db, whBlockNeg).Rows.Count == 0
                    && ReadOpeningLines(db, firstOrderId).Rows.Count == 0
                    && invSvc.GetStockBalance(prodA, whBlockNeg) == 0m;

                Check("Tồn đầu P1 §4 #2 (sửa về 0 xóa dòng, hết dòng xóa phiếu, chỉ 1 phiếu mỗi kho dù lưu nhiều lần): đạt toàn bộ",
                    zeroBDeletedOk && stillSingleOrder && allZeroDeletedOrder,
                    SingleLine("zeroB=" + zeroBDeletedOk + ", single=" + stillSingleOrder + ", delAll=" + allZeroDeletedOrder));

                // Spec §4 #3: tồn đầu âm bị chặn nêu tên hàng; giảm tồn đầu gây âm kho bị chặn khi kho không cho âm, cho phép khi kho cho âm.
                OpeningStockSaveResult rNeg = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = -3m, CostPrice = 10000m }
                    }
                });
                bool negBlockedWithName = !rNeg.Ok && rNeg.ErrorMessage != null
                    && rNeg.ErrorMessage.IndexOf("Trà Xanh TD " + tag, StringComparison.Ordinal) >= 0;

                // Seed tồn đầu = 10 trên whBlockNeg + xuất 7 -> tồn hiện có = 3.
                svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 10m, CostPrice = 10000m }
                    }
                });
                extraOrderIds.Add(InsertStockOrderLine(db, "PX-TD1-" + tag, 2, 30, 30, null, whBlockNeg, storeId, prodA, unitId, 0m, 7m));

                // Giảm tồn đầu 10 -> 5 (tồn sau = -2) trên kho KHÔNG cho âm -> CHẶN nêu tên hàng + tồn 3 + tồn sau -2.
                OpeningStockSaveResult rBlockNeg = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 5m, CostPrice = 10000m }
                    }
                });
                bool blockNegStockOk = !rBlockNeg.Ok && rBlockNeg.ErrorMessage != null
                    && rBlockNeg.ErrorMessage.IndexOf("Trà Xanh TD " + tag, StringComparison.Ordinal) >= 0
                    && rBlockNeg.ErrorMessage.IndexOf("3", StringComparison.Ordinal) >= 0
                    && rBlockNeg.ErrorMessage.IndexOf("-2", StringComparison.Ordinal) >= 0
                    && invSvc.GetStockBalance(prodA, whBlockNeg) == 3m;

                // Giảm xuống 7 (tồn sau = 0) trên kho KHÔNG cho âm -> ĐƯỢC phép.
                OpeningStockSaveResult rToZeroOk = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whBlockNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 7m, CostPrice = 10000m }
                    }
                });
                bool reduceToZeroOk = rToZeroOk.Ok && invSvc.GetStockBalance(prodA, whBlockNeg) == 0m;

                // Kho CHO PHÉP âm kho (CHOPHEPAMKHO = 30): tồn đầu 10, xuất 7, giảm tồn đầu xuống 5 (tồn sau = -2) -> ĐƯỢC phép.
                svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whAllowNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 10m, CostPrice = 10000m }
                    }
                });
                extraOrderIds.Add(InsertStockOrderLine(db, "PX-TD2-" + tag, 2, 30, 30, null, whAllowNeg, storeId, prodA, unitId, 0m, 7m));
                OpeningStockSaveResult rAllowNeg = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whAllowNeg,
                    Date = openDate,
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodA, Name = "Trà Xanh TD " + tag, UnitId = unitId, Quantity = 5m, CostPrice = 10000m }
                    }
                });
                bool allowNegStockOk = rAllowNeg.Ok && invSvc.GetStockBalance(prodA, whAllowNeg) == -2m;

                Check("Tồn đầu P1 §4 #3 (chặn tồn âm nêu tên hàng; giảm tồn gây âm kho bị chặn khi kho không cho âm, cho phép khi kho cho âm): đạt toàn bộ",
                    negBlockedWithName && blockNegStockOk && reduceToZeroOk && allowNegStockOk,
                    SingleLine("neg=" + negBlockedWithName + ", blk=" + blockNegStockOk + ", to0=" + reduceToZeroOk + ", allow=" + allowNegStockOk));
            }
            finally
            {
                CleanupOpeningWarehouseOrders(db, touchedWhIds);
                foreach (string oid in extraOrderIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodA);
                SafeDeleteById(db, "DMATHANG", prodB);
                SafeDeleteById(db, "DMATHANG", prodNullType);
                SafeDeleteById(db, "DMATHANG", prodRecipe);
                SafeDeleteById(db, "DMATHANG", prodInactive);
                SafeDeleteById(db, "DDONVITINH", unitId);
                SafeDeleteById(db, "DKHOHANG", whBlockNeg);
                SafeDeleteById(db, "DKHOHANG", whAllowNeg);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static DataTable ReadOpeningHeaders(string db, string warehouseId)
        {
            return DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHONHAPID, TIENHANG, TONGCONG, TILETHUE " +
                "FROM TDONHANG WHERE DKHONHAPID = @W AND COALESCE(LOAI, 0) = 97 AND COALESCE(STATUS, 0) = 30",
                "@W", warehouseId));
        }

        private static DataTable ReadOpeningLines(string db, string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return new DataTable();
            return DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT ID, DMATHANGID, DKHOHANGID, COALESCE(SLNHAP, 0) AS SLNHAP, COALESCE(SLNHAPCHUAQUYDOI, 0) AS SLRAW, " +
                "COALESCE(DONGIA, 0) AS DONGIA, COALESCE(THANHTIEN, 0) AS THANHTIEN, COALESCE(TILEGIAMGIA, 0) AS TILEGIAMGIA " +
                "FROM TDONHANGCHITIET WHERE TDONHANGID = @O AND COALESCE(STATUS, 0) = 30",
                "@O", orderId));
        }

        private static bool MatchOpeningLine(
            DataTable dt, string productId, string warehouseId, decimal qty, decimal costPrice, decimal amount)
        {
            foreach (DataRow r in dt.Rows)
            {
                if (DbHelper.Str(r["DMATHANGID"]) != productId)
                    continue;
                return DbHelper.Str(r["DKHOHANGID"]) == warehouseId
                    && DbHelper.Dec(r["SLNHAP"]) == qty
                    && DbHelper.Dec(r["SLRAW"]) == qty
                    && DbHelper.Dec(r["DONGIA"]) == costPrice
                    && DbHelper.Dec(r["THANHTIEN"]) == amount
                    && DbHelper.Dec(r["TILEGIAMGIA"]) == 0m;
            }
            return false;
        }

        private static void CleanupOpeningWarehouseOrders(string db, IEnumerable<string> warehouseIds)
        {
            foreach (string whId in warehouseIds)
            {
                DataTable dt = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                    "SELECT ID FROM TDONHANG WHERE DKHONHAPID = @W AND COALESCE(LOAI, 0) = 97", "@W", whId));
                foreach (DataRow r in dt.Rows)
                {
                    string oid = DbHelper.Str(r["ID"]);
                    if (string.IsNullOrEmpty(oid)) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
            }
        }
    }
}
