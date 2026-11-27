using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Tính lại giá vốn (docs/specs/tinh-lai-gia-von.md §5).</summary>
    public static partial class SelfTest
    {
        private static void CostRecalcTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_gv",
                    UserName = "selftest_gv",
                    DisplayName = "Selftest GiaVon",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };
                CostRecalcEnginePureTests();
                CostRecalcDbTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        /// <summary>Tắt tự động tính giá vốn trên bản sao DB selftest để các ca khác (lưu phiếu nhập làm dữ liệu mồi) không bị tính lại giá vốn toàn DB.</summary>
        private static void DisableAutoCostForTests(string db)
        {
            new ConfigRepository(db).Save(new[] { new ConfigChange { Name = "TuDongTinhGiaVon", DataType = 3, IntValue = 0 } }, "selftest");
            SystemConfig.Reload(db);
        }

        private static CostProduct CostTestProduct(string id, string type, decimal cost = 0m)
        {
            return new CostProduct { Id = id, TypeId = type, CurrentCost = cost };
        }

        private static CostLine CostTestLine(string id, string product, int loai, DateTime date, decimal qIn, decimal qOut, decimal amount, decimal cost = 0m)
        {
            return new CostLine { Id = id, ProductId = product, OrderType = loai, Date = date, QtyIn = qIn, QtyOut = qOut, Amount = amount, CurrentCost = cost };
        }

        private static decimal LineCostOf(CostRecalcPlan plan, string lineId, decimal whenUnchanged)
        {
            foreach (CostLineUpdate u in plan.LineUpdates)
                if (u.LineId == lineId) return u.NewCost;
            return whenUnchanged;
        }

        private static decimal ProductCostOf(CostRecalcPlan plan, string productId, decimal whenUnchanged)
        {
            foreach (CostProductUpdate u in plan.ProductUpdates)
                if (u.ProductId == productId) return u.NewCost;
            return whenUnchanged;
        }

        private static void CostRecalcEnginePureTests()
        {
            DateTime jan = new DateTime(2026, 1, 10), jan2 = new DateTime(2026, 1, 20);
            DateTime feb = new DateTime(2026, 2, 5), feb2 = new DateTime(2026, 2, 20);
            var products = new List<CostProduct>
            {
                CostTestProduct("A", "2"), CostTestProduct("B", "2"), CostTestProduct("X", "1"), CostTestProduct("Y", "4"), CostTestProduct("C", "0"), CostTestProduct("D", "3"), CostTestProduct("F", "2"), CostTestProduct("Z", "1")
            };
            var recipes = new List<CostRecipeLine>
            {
                new CostRecipeLine { ProductId = "X", IngredientId = "A", Quantity = 2m },
                new CostRecipeLine { ProductId = "X", IngredientId = "B", Quantity = 0.5m },
                new CostRecipeLine { ProductId = "Y", IngredientId = "X", Quantity = 1m },
                new CostRecipeLine { ProductId = "Y", IngredientId = "A", Quantity = 1m },
                new CostRecipeLine { ProductId = "Z", IngredientId = "F", Quantity = 3m }
            };
            var lines = new List<CostLine>
            {
                CostTestLine("a1", "A", 1, jan, 10m, 0m, 100000m), CostTestLine("a2", "A", 0, jan2, 0m, 4m, 0m),
                CostTestLine("a3", "A", 1, feb, 10m, 0m, 140000m), CostTestLine("a4", "A", 0, feb2, 0m, 1m, 0m),
                CostTestLine("b1", "B", 1, jan, 10m, 0m, 90000m),
                CostTestLine("x1", "X", 0, jan2, 0m, 1m, 0m), CostTestLine("x2", "X", 0, feb2, 0m, 1m, 0m),
                CostTestLine("y1", "Y", 0, jan2, 0m, 1m, 0m), CostTestLine("y2", "Y", 0, feb2, 0m, 1m, 0m),
                CostTestLine("c1", "C", 1, jan, 5m, 0m, 25000m), CostTestLine("c2", "C", 0, jan2, 0m, 8m, 0m), CostTestLine("c3", "C", 0, feb2, 0m, 1m, 0m),
                CostTestLine("d1", "D", 1, jan, 5m, 0m, 50000m), CostTestLine("d2", "D", 0, jan2, 0m, 1m, 0m),
                CostTestLine("z1", "Z", 0, jan2, 0m, 1m, 0m)
            };

            var progressSeen = new List<int>();
            CostRecalcPlan plan = CostRecalcEngine.Compute(products, recipes, lines, (d, t) => progressSeen.Add(d * 100 + t));

            // §5 #1, #2: tháng 1 giá vốn 10.000 (cả dòng nhập và dòng xuất); tồn 6, giá trị 60.000 mang sang; tháng 2: 200.000 / 16 = 12.500.
            bool avgOk = LineCostOf(plan, "a1", -1m) == 10000m && LineCostOf(plan, "a2", -1m) == 10000m
                && LineCostOf(plan, "a3", -1m) == 12500m && LineCostOf(plan, "a4", -1m) == 12500m
                && ProductCostOf(plan, "A", -1m) == 12500m;
            Check("Giá vốn §5 #1-#2: bình quân cuối kỳ theo tháng, tồn và giá trị mang sang tháng sau (10.000 rồi 12.500)", avgOk,
                "a1=" + LineCostOf(plan, "a1", -1m) + ", a2=" + LineCostOf(plan, "a2", -1m) + ", a3=" + LineCostOf(plan, "a3", -1m)
                + ", A=" + ProductCostOf(plan, "A", -1m));

            // §5 #5: THANHTIEN sau chiết khấu (90.000 cho 10) cho 9.000, không dùng đơn giá x SL.
            Check("Giá vốn §5 #5: dùng THANHTIEN sau chiết khấu (90.000 / 10 = 9.000)", ProductCostOf(plan, "B", -1m) == 9000m && LineCostOf(plan, "b1", -1m) == 9000m,
                "B=" + ProductCostOf(plan, "B", -1m));

            // §5 #3: món X = 2A + 0.5B: tháng 1 = 24.500, tháng 2 = 29.500; combo Y = X + A: tháng 1 = 34.500, tháng 2 = 42.000.
            Check("Giá vốn §5 #3: món định lượng và combo 2 cấp theo giá vốn vật tư của từng tháng",
                LineCostOf(plan, "x1", -1m) == 24500m && LineCostOf(plan, "x2", -1m) == 29500m
                && LineCostOf(plan, "y1", -1m) == 34500m && LineCostOf(plan, "y2", -1m) == 42000m
                && ProductCostOf(plan, "X", -1m) == 29500m && ProductCostOf(plan, "Y", -1m) == 42000m,
                "x1=" + LineCostOf(plan, "x1", -1m) + ", x2=" + LineCostOf(plan, "x2", -1m) + ", y1=" + LineCostOf(plan, "y1", -1m) + ", y2=" + LineCostOf(plan, "y2", -1m));

            // §5 #6 (T3): tồn âm giữ giá vốn tháng trước, không chia 0, không âm.
            Check("Giá vốn §5 #6 (T3): tồn âm giữ giá vốn tháng trước (5.000), không ra giá vốn âm",
                LineCostOf(plan, "c1", -1m) == 5000m && LineCostOf(plan, "c2", -1m) == 5000m && LineCostOf(plan, "c3", -1m) == 5000m
                && ProductCostOf(plan, "C", -1m) == 5000m, "c3=" + LineCostOf(plan, "c3", -1m));

            // T1: loại '3' không thuộc bước nào: không ghi dòng, không ghi đè DMATHANG.GIAVON; món có vật tư chưa có giá tính 0 = giá hiện có thì không cập nhật.
            Check("Giá vốn §5 #4 (T1): mặt hàng loại khác không bị ghi đè về 0; món có vật tư chưa có giá vốn không đổi",
                LineCostOf(plan, "d1", -9m) == -9m && LineCostOf(plan, "d2", -9m) == -9m && ProductCostOf(plan, "D", -9m) == -9m
                && ProductCostOf(plan, "Z", -9m) == -9m && LineCostOf(plan, "z1", -9m) == -9m, "");

            Check("Giá vốn: báo tiến độ theo tháng (2 tháng: 1/2 rồi 2/2)", plan.MonthCount == 2 && progressSeen.Count == 2 && progressSeen[0] == 102 && progressSeen[1] == 202,
                string.Join(",", progressSeen.ConvertAll(x => x.ToString()).ToArray()));

            // §5 #8: chạy hai lần cho cùng kết quả: áp kết quả vào dữ liệu rồi tính lại thì không còn thay đổi nào.
            foreach (CostLine l in lines)
                l.CurrentCost = LineCostOf(plan, l.Id, l.CurrentCost);
            foreach (CostProduct p in products)
                p.CurrentCost = ProductCostOf(plan, p.Id, p.CurrentCost);
            CostRecalcPlan again = CostRecalcEngine.Compute(products, recipes, lines, null);
            Check("Giá vốn §5 #8: chạy lần hai cho cùng kết quả (không còn dòng hay mặt hàng nào đổi)",
                again.LineUpdates.Count == 0 && again.ProductUpdates.Count == 0, "lines=" + again.LineUpdates.Count + ", products=" + again.ProductUpdates.Count);
        }

        private static void CostRecalcDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId(), whId = DbHelper.NewId(), unitId = DbHelper.NewId();
            string prodA = DbHelper.NewId(), prodX = DbHelper.NewId();
            var createdOrderIds = new List<string>();
            var createdConfigIds = new List<string>();
            var lineSnap = new Dictionary<string, decimal>();
            var prodSnap = new Dictionary<string, decimal>();
            var cfgRepo = new ConfigRepository(db);
            int origAuto = SystemConfig.GetInt("TuDongTinhGiaVon");
            bool hadAutoRow = BookingCount(db, "SELECT COUNT(*) FROM SCONFIG WHERE NAME = 'TuDongTinhGiaVon'") > 0;

            try
            {
                // Chụp giá vốn hiện có để khôi phục sau khi tính (selftest chạy trên bản sao DB, các ca sau không bị ảnh hưởng).
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM TDONHANGCHITIET")).Rows)
                    lineSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM DMATHANG")).Rows)
                    prodSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_gv')", "@I", storeId, "@N", "CH GiaVon " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_gv', @T)",
                    "@I", whId, "@N", "Kho GiaVon " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_gv')", "@I", unitId, "@N", "Kg " + tag);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 10000, 15000, 1, 30, 'selftest_gv', @T)", "@I", prodA, "@C", "GV-A-" + tag, "@N", "Nguyên liệu A " + tag, "@U", unitId, "@T", now);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '1', @U, 0, 50000, 1, 30, 'selftest_gv', @T)", "@I", prodX, "@C", "GV-X-" + tag, "@N", "Món X " + tag, "@U", unitId, "@T", now);
                BookingExec(db, "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest_gv', @T, @P, 2, @V)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", prodX, "@V", prodA);

                // Cấu hình tự động: tắt lúc đầu (chèn dòng SCONFIG nếu DB chưa có).
                if (!hadAutoRow)
                {
                    string cid = DbHelper.NewId();
                    BookingExec(db, "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, 'TuDongTinhGiaVon', 3, 0, 30, 'selftest_gv', @T)", "@I", cid, "@T", now);
                    createdConfigIds.Add(cid);
                }
                cfgRepo.Save(new[] { new ConfigChange { Name = "TuDongTinhGiaVon", DataType = 3, IntValue = 0 } }, "selftest_gv");
                SystemConfig.Reload(db);

                var purSvc = new PurchaseService(AppSession.Current);
                Func<string, DateTime, decimal, decimal, string> purchase = (num, date, qty, price) =>
                {
                    PurchaseSaveResult r = purSvc.Save(new PurchaseRow
                    {
                        Name = num + tag, Date = date, WarehouseId = whId,
                        Lines = new List<PurchaseLine>
                        {
                            new PurchaseLine { ProductId = prodA, ProductName = "Nguyên liệu A " + tag, UnitId = unitId, BaseUnitId = unitId, RawQuantity = qty, UnitPrice = price }
                        }
                    });
                    if (r.Ok) createdOrderIds.Add(r.SavedId);
                    return r.Ok ? r.SavedId : null;
                };
                string pn1 = purchase("PN-GV1-", new DateTime(2026, 1, 15), 10m, 10000m);
                string pn2 = purchase("PN-GV2-", new DateTime(2026, 2, 10), 10m, 14000m);

                Func<string, DateTime, int, string> addBill = (name, ngay, paid) =>
                {
                    string id = DbHelper.NewId();
                    createdOrderIds.Add(id);
                    BookingExec(db, "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DCUAHANGID, TONGCONG, USERCREATEDID) VALUES (@I, @N, 0, @P, 30, @G, @S, 0, 'selftest_gv')",
                        "@I", id, "@N", name + tag, "@P", paid, "@G", ngay, "@S", storeId);
                    return id;
                };
                var lineIds = new Dictionary<string, string>();
                Action<string, string, string, decimal> addSaleLine = (key, bill, prod, qty) =>
                {
                    string lid = DbHelper.NewId();
                    lineIds[key] = lid;
                    BookingExec(db, "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, THANHTIEN, SLXUAT, SLNHAP, GIAVON, STATUS, USERCREATEDID) " +
                        "VALUES (@I, @B, @P, 'GV-Line', @U, @W, 0, @Q, 0, 0, 30, 'selftest_gv')", "@I", lid, "@B", bill, "@P", prod, "@U", unitId, "@W", whId, "@Q", qty);
                };
                string paidBill = addBill("BH-GV1-", new DateTime(2026, 1, 20), 30);
                string unpaidBill = addBill("BH-GV2-", new DateTime(2026, 1, 21), 0);
                addSaleLine("saleA", paidBill, prodA, 4m);
                addSaleLine("saleX", paidBill, prodX, 1m);
                addSaleLine("unpaidA", unpaidBill, prodA, 3m);

                var svc = new CostRecalcService(AppSession.Current);
                CostRecalcResult res = svc.Run(null);
                decimal costA1 = ReadLineCost(db, lineIds["saleA"]);
                decimal costUnpaid = ReadLineCost(db, lineIds["unpaidA"]);
                decimal costX = ReadLineCost(db, lineIds["saleX"]);
                decimal prodACost = ReadProductCost(db, prodA);
                decimal prodXCost = ReadProductCost(db, prodX);
                decimal pnLine1 = ReadOrderLineCost(db, pn1), pnLine2 = ReadOrderLineCost(db, pn2);

                // Tồn 6 (60.000) cuối tháng 1; tháng 2: 16 cái, 200.000 => 12.500. Món X (2 x A) tháng 1: 20.000; giá hiện tại 25.000.
                Check("Giá vốn DB §5: tính thật trên dữ liệu: dòng nhập, dòng bán đã thanh toán, món định lượng, giá mặt hàng; bill chưa thanh toán không bị đụng",
                    res.Ok && pnLine1 == 10000m && pnLine2 == 12500m && costA1 == 10000m && costUnpaid == 0m
                    && costX == 20000m && prodACost == 12500m && prodXCost == 25000m,
                    "ok=" + res.Ok + ", pn1=" + pnLine1 + ", pn2=" + pnLine2 + ", saleA=" + costA1 + ", unpaid=" + costUnpaid + ", saleX=" + costX + ", A=" + prodACost + ", X=" + prodXCost);

                CostRecalcResult res2 = svc.Run(null);
                Check("Giá vốn DB §5 #8: chạy lần hai không đổi dòng hay mặt hàng nào", res2.Ok && res2.LinesChanged == 0 && res2.ProductsChanged == 0,
                    "lines=" + res2.LinesChanged + ", products=" + res2.ProductsChanged);

                // §5 #8 rollback: đặt lại giá sai rồi chạy với hook ném lỗi: không đổi gì.
                BookingExec(db, "UPDATE DMATHANG SET GIAVON = 7 WHERE ID = @I", "@I", prodA);
                bool threw = false;
                try { svc.Run(null, () => { throw new InvalidOperationException("hook"); }); }
                catch (Exception) { threw = true; }
                decimal afterFail = ReadProductCost(db, prodA);
                Check("Giá vốn DB §5 #8: lỗi giữa chừng rollback, không đổi dòng nào", threw && afterFail == 7m, "threw=" + threw + ", A=" + afterFail);
                svc.Run(null);

                // Quyền: không có quyền Sửa thì không chạy được.
                var limited = new CostRecalcService(new UserSession { UserId = "u", UserName = "u", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) });
                CostRecalcResult noPerm = limited.Run(null);
                Check("Giá vốn: không có quyền thì không chạy", !limited.CanRun && !noPerm.Ok && noPerm.Message == CostRecalcConstants.NoPermission, "msg=" + noPerm.Message);

                // §5 #9: tự động sau lưu phiếu nhập khi bật; tắt thì không đổi.
                cfgRepo.Save(new[] { new ConfigChange { Name = "TuDongTinhGiaVon", DataType = 3, IntValue = 30 } }, "selftest_gv");
                SystemConfig.Reload(db);
                PurchaseSaveResult auto = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-GV3-" + tag, Date = new DateTime(2026, 2, 20), WarehouseId = whId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prodA, ProductName = "Nguyên liệu A " + tag, UnitId = unitId, BaseUnitId = unitId, RawQuantity = 10m, UnitPrice = 20000m } }
                });
                if (auto.Ok) createdOrderIds.Add(auto.SavedId);
                // Tháng 2: tồn 6 + 10 + 10 = 26, giá trị 60.000 + 140.000 + 200.000 = 400.000 => 15.384,62.
                decimal autoCost = ReadProductCost(db, prodA);
                Check("Giá vốn §5 #9: bật tự động thì sau lưu phiếu nhập giá vốn đổi, không có cảnh báo", auto.Ok && string.IsNullOrEmpty(auto.Warning) && autoCost == 15384.62m, "A=" + autoCost + ", warn=" + auto.Warning);

                string failWarn = CostRecalcService.RunAutoAfterPurchase(new UserSession { UserId = "u", UserName = "u", IsAdmin = true, DatabasePath = @"Z:\khong-co\x.fdb", Permissions = new FunctionPermissions(true) });
                Check("Giá vốn §5 #9: lỗi tự động chỉ trả cảnh báo một câu, không ném lỗi", failWarn == CostRecalcConstants.AutoFailedWarning, "warn=" + failWarn);

                cfgRepo.Save(new[] { new ConfigChange { Name = "TuDongTinhGiaVon", DataType = 3, IntValue = 0 } }, "selftest_gv");
                SystemConfig.Reload(db);
                BookingExec(db, "UPDATE DMATHANG SET GIAVON = 7 WHERE ID = @I", "@I", prodA);
                PurchaseSaveResult off = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-GV4-" + tag, Date = new DateTime(2026, 2, 21), WarehouseId = whId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prodA, ProductName = "Nguyên liệu A " + tag, UnitId = unitId, BaseUnitId = unitId, RawQuantity = 1m, UnitPrice = 1000m } }
                });
                if (off.Ok) createdOrderIds.Add(off.SavedId);
                Check("Giá vốn §5 #9: tắt tự động thì sau lưu phiếu nhập giá vốn không đổi", off.Ok && string.IsNullOrEmpty(off.Warning) && ReadProductCost(db, prodA) == 7m, "A=" + ReadProductCost(db, prodA));
            }
            catch (Exception ex)
            {
                Check("Giá vốn DB: ngoại lệ không mong đợi", false, ex.ToString());
            }
            finally
            {
                try
                {
                    cfgRepo.Save(new[] { new ConfigChange { Name = "TuDongTinhGiaVon", DataType = 3, IntValue = origAuto } }, "selftest_gv");
                    foreach (string cid in createdConfigIds)
                        BookingExec(db, "DELETE FROM SCONFIG WHERE ID = @I", "@I", cid);
                    SystemConfig.Reload(db);
                    foreach (string oid in createdOrderIds)
                    {
                        BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                        BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                        BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", oid);
                    }
                    BookingExec(db, "DELETE FROM TLUUVET WHERE CHUCNANG = @F", "@F", CostRecalcConstants.FunctionName);
                    BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", prodX);
                    BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", prodX);
                    BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", prodA);
                    BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", whId);
                    BookingExec(db, "DELETE FROM DCUAHANG WHERE ID = @I", "@I", storeId);
                    BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId);
                    foreach (KeyValuePair<string, decimal> kv in lineSnap)
                        BookingExec(db, "UPDATE TDONHANGCHITIET SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                    foreach (KeyValuePair<string, decimal> kv in prodSnap)
                        BookingExec(db, "UPDATE DMATHANG SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                }
                catch (Exception ex)
                {
                    Check("Giá vốn DB: dọn dữ liệu", false, ex.Message);
                }
            }
        }

        private static decimal ReadLineCost(string db, string lineId)
        {
            object v = DbHelper.Read(db, conn => DbHelper.Scalar(conn, null, "SELECT COALESCE(GIAVON, 0) FROM TDONHANGCHITIET WHERE ID = @I", "@I", lineId));
            return DbHelper.Dec(v);
        }

        private static decimal ReadOrderLineCost(string db, string orderId)
        {
            object v = DbHelper.Read(db, conn => DbHelper.Scalar(conn, null, "SELECT FIRST 1 COALESCE(GIAVON, 0) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", orderId));
            return DbHelper.Dec(v);
        }

        private static decimal ReadProductCost(string db, string productId)
        {
            object v = DbHelper.Read(db, conn => DbHelper.Scalar(conn, null, "SELECT COALESCE(GIAVON, 0) FROM DMATHANG WHERE ID = @I", "@I", productId));
            return DbHelper.Dec(v);
        }
    }
}
