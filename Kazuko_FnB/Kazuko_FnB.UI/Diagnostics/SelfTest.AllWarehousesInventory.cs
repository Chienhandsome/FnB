using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void AllWarehousesInventoryTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tnk",
                    UserName = "selftest_tnk",
                    DisplayName = "Selftest TonNhieuKho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                AllWarehousesInventoryPureRulesTests();
                AllWarehousesInventoryServiceAndDbTests(db);
                AllWarehousesInventoryUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void AllWarehousesInventoryPureRulesTests()
        {
            bool allSelOk = InventoryRules.IsAllWarehousesSelection(InventoryConstants.AllWarehousesId)
                && !InventoryRules.IsAllWarehousesSelection("")
                && !InventoryRules.IsAllWarehousesSelection(null)
                && !InventoryRules.IsAllWarehousesSelection("wh-1");

            var item = new InventoryItemRow
            {
                ProductId = "P1",
                UnitId = "U1",
                UnitName = "chai",
                BigUnitId = "U2",
                BigUnitName = "lốc",
                QuyDoi = 6m,
                CostPrice = 12000m
            };
            var warehouses = new List<WarehouseRow>
            {
                new WarehouseRow { Id = "K1", Name = "Kho 1", StoreName = "CH 1" },
                new WarehouseRow { Id = "K2", Name = "Kho 2", StoreName = "CH 1" },
                new WarehouseRow { Id = "K3", Name = "Kho 3", StoreName = "CH 1" },
                new WarehouseRow { Id = "K4", Name = "Kho 4", StoreName = "CH 1" }
            };
            var balances = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                { "K1", 10m },
                { "K2", 5m },
                { "K3", 0m },
                { "K4", -2m }
            };

            List<InventoryWarehouseDetailRow> showAll = InventoryRules.BuildWarehouseDetails(item, warehouses, balances, false, true);
            List<InventoryWarehouseDetailRow> hideZero = InventoryRules.BuildWarehouseDetails(item, warehouses, balances, true, true);
            List<InventoryWarehouseDetailRow> noPrice = InventoryRules.BuildWarehouseDetails(item, warehouses, balances, false, false);

            bool showAllOk = showAll.Count == 4
                && showAll[0].WarehouseId == "K1" && showAll[0].StockQuantity == 10m
                && showAll[0].TwoUnitDisplay == "1 lốc 4 chai" && showAll[0].CostValue == 120000m
                && showAll[1].WarehouseId == "K2" && showAll[1].StockQuantity == 5m
                && showAll[1].TwoUnitDisplay == "0 lốc 5 chai" && showAll[1].CostValue == 60000m
                && showAll[2].WarehouseId == "K3" && showAll[2].StockQuantity == 0m && showAll[2].CostValue == 0m
                && showAll[3].WarehouseId == "K4" && showAll[3].StockQuantity == -2m && showAll[3].CostValue == -24000m;

            bool hideZeroOk = hideZero.Count == 3
                && !hideZero.Exists(r => r.WarehouseId == "K3")
                && hideZero.Exists(r => r.WarehouseId == "K4" && r.StockQuantity == -2m);

            bool noPriceOk = noPrice.Count == 4
                && noPrice.TrueForAll(r => r.CostPrice == 0m && r.CostValue == 0m);

            Check("Tồn nhiều kho §5 #6 & #7 (luật thuần): nhận diện mục Tất cả các kho, dựng chi tiết theo kho, ẩn kho tồn 0, ẩn giá vốn",
                allSelOk && showAllOk && hideZeroOk && noPriceOk,
                SingleLine("allSel=" + allSelOk + ", showAll=" + showAllOk + ", hideZero=" + hideZeroOk + ", noPrice=" + noPriceOk));
        }

        private static void AllWarehousesInventoryServiceAndDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string store1 = DbHelper.NewId();
            string store2 = DbHelper.NewId();
            string whK1 = DbHelper.NewId();
            string whK2 = DbHelper.NewId();
            string whK3 = DbHelper.NewId();
            string whOther = DbHelper.NewId();
            string whInactive = DbHelper.NewId();
            string unitChai = DbHelper.NewId();
            string unitLoc = DbHelper.NewId();
            string prodX = DbHelper.NewId();
            string prodZero = DbHelper.NewId();
            var orderIds = new List<string>();

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", store1, "@N", "CH TNK 1 " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", store2, "@N", "CH TNK 2 " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK1, "@N", "Kho K1 " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK2, "@N", "Kho K2 " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whK3, "@N", "Kho K3 " + tag, "@S", store1, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_tnk', @T)", "@I", whOther, "@N", "Kho CH2 " + tag, "@S", store2, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 0, 'selftest_tnk', @T)", "@I", whInactive, "@N", "Kho Off " + tag, "@S", store1, "@T", now);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whK1, "@I", store1);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whOther, "@I", store2);

                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", unitChai, "@N", "chai");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tnk')", "@I", unitLoc, "@N", "lốc");

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 6, 5, 11000, 12000, 20000, 30, 'selftest_tnk', @T)",
                    "@I", prodX, "@C", "TNK-X-" + tag, "@N", "Trà Đào TNK " + tag, "@U1", unitChai, "@U2", unitLoc, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, TONTOITHIEU, GIANHAP, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 0, 5000, 6000, 10000, 30, 'selftest_tnk', @T)",
                    "@I", prodZero, "@C", "TNK-Z-" + tag, "@N", "Ống Hút TNK " + tag, "@U", unitChai, "@T", now);

                // Nhập 10 vào K1, 5 vào K2 (cùng CH1), 7 vào Kho CH2, 50 vào Kho ngừng dùng (STATUS = 0).
                // Thêm bill chưa thanh toán (-4 tại K1) và phiếu nhập đã xóa (-30, +99 tại K2) để chứng minh không bị tính.
                orderIds.Add(InsertStockOrderLine(db, "PN-TNK-K1-" + tag, 1, 30, 30, whK1, null, store1, prodX, unitChai, 10m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNK-K2-" + tag, 1, 30, 30, whK2, null, store1, prodX, unitChai, 5m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNK-CH2-" + tag, 1, 30, 30, whOther, null, store2, prodX, unitChai, 7m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNK-OFF-" + tag, 1, 30, 30, whInactive, null, store1, prodX, unitChai, 50m, 0m));
                orderIds.Add(InsertStockOrderLine(db, "HD-TNK-UNPAID-" + tag, 0, 30, 0, null, whK1, store1, prodX, unitChai, 0m, 4m));
                orderIds.Add(InsertStockOrderLine(db, "PN-TNK-DEL-" + tag, 1, -30, 30, whK2, null, store1, prodX, unitChai, 99m, 0m));

                // Đối chiếu trực tiếp với hàm tồn chung ServiceOrderRepository.GetStockBalanceIn.
                var repo = new InventoryRepository(db);
                Dictionary<string, decimal> directByWh = repo.LoadProductStockByWarehouses(prodX, new[] { whK1, whK2, whK3, whOther });
                bool parityOk = directByWh[whK1] == 10m && directByWh[whK1] == repo.GetStockBalance(prodX, whK1)
                    && directByWh[whK2] == 5m && directByWh[whK2] == repo.GetStockBalance(prodX, whK2)
                    && directByWh[whK3] == 0m && directByWh[whK3] == repo.GetStockBalance(prodX, whK3)
                    && directByWh[whOther] == 7m && directByWh[whOther] == repo.GetStockBalance(prodX, whOther);

                // Admin: cộng tất cả kho STATUS = 30 (K1=10 + K2=5 + CH2=7 = 22, không cộng kho STATUS=0).
                var adminSess = new UserSession
                {
                    UserId = "selftest_tnk", UserName = "selftest_tnk", DisplayName = "Admin TNK",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK 1 " + tag }
                };
                var adminSvc = new InventoryService(adminSess);
                Dictionary<string, Dictionary<string, decimal>> adminByWh;
                List<InventoryItemRow> adminAll = adminSvc.LoadItemsForMode(InventoryConstants.AllWarehousesId, true, out adminByWh);
                InventoryItemRow adminX = adminAll.Find(x => x.ProductId == prodX);
                bool adminSumOk = adminX != null && adminX.StockQuantity == 22m
                    && adminX.TwoUnitDisplay == "3 lốc 4 chai" && adminX.CostValue == 264000m;

                // Non-admin thuộc CH1 có quyền Xem giá nhập: chỉ cộng K1+K2+K3 = 15, không cộng CH2 (7) và không cộng kho ngừng dùng (50).
                var permsWithPrice = new FunctionPermissions(false);
                permsWithPrice.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
                permsWithPrice.Add("fn_gn", InventoryConstants.ViewBuyPriceFunctionName, RoleModes.View);
                var store1Sess = new UserSession
                {
                    UserId = "u_tnk_s1_" + tag, UserName = "u_tnk_s1", DisplayName = "User TNK CH1",
                    IsAdmin = false, DatabasePath = db, Permissions = permsWithPrice,
                    Stores = new List<StoreRecord> { new StoreRecord { Id = store1, Name = "CH TNK 1 " + tag } },
                    CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK 1 " + tag }
                };
                var store1Svc = new InventoryService(store1Sess);
                Dictionary<string, Dictionary<string, decimal>> s1ByWh;
                List<InventoryItemRow> s1All = store1Svc.LoadItemsForMode(InventoryConstants.AllWarehousesId, true, out s1ByWh);
                InventoryItemRow s1XAll = s1All.Find(x => x.ProductId == prodX);
                InventoryItemRow s1ZAll = s1All.Find(x => x.ProductId == prodZero);

                Dictionary<string, Dictionary<string, decimal>> s1ByWhK1;
                List<InventoryItemRow> s1K1 = store1Svc.LoadItemsForMode(whK1, true, out s1ByWhK1);
                InventoryItemRow s1XK1 = s1K1.Find(x => x.ProductId == prodX);

                Dictionary<string, Dictionary<string, decimal>> s1BlockedOut;
                List<InventoryItemRow> s1BlockedOther = store1Svc.LoadItemsForMode(whOther, true, out s1BlockedOut);

                List<InventoryItemRow> s1HideZero = store1Svc.LoadItems(new InventoryFilter
                {
                    WarehouseId = InventoryConstants.AllWarehousesId,
                    HideZeroStock = true,
                    SearchText = tag
                });
                bool hideZeroItemsOk = s1HideZero.Exists(x => x.ProductId == prodX && x.StockQuantity == 15m)
                    && !s1HideZero.Exists(x => x.ProductId == prodZero);

                List<InventoryWarehouseDetailRow> detAll = store1Svc.LoadProductWarehouseDetails(prodX, false);
                List<InventoryWarehouseDetailRow> detNonZero = store1Svc.LoadProductWarehouseDetails(prodX, true);
                InventoryWarehouseDetailRow dK1 = detAll.Find(d => d.WarehouseId == whK1);
                InventoryWarehouseDetailRow dK2 = detAll.Find(d => d.WarehouseId == whK2);
                InventoryWarehouseDetailRow dK3 = detAll.Find(d => d.WarehouseId == whK3);
                bool detOk = detAll.Count == 3
                    && dK1 != null && dK1.StockQuantity == 10m && dK1.CostValue == 120000m && dK1.TwoUnitDisplay == "1 lốc 4 chai"
                    && dK2 != null && dK2.StockQuantity == 5m && dK2.CostValue == 60000m && dK2.TwoUnitDisplay == "0 lốc 5 chai"
                    && dK3 != null && dK3.StockQuantity == 0m && dK3.CostValue == 0m
                    && !detAll.Exists(d => d.WarehouseId == whOther || d.WarehouseId == whInactive)
                    && detNonZero.Count == 2 && !detNonZero.Exists(d => d.WarehouseId == whK3);

                Check("Tồn nhiều kho §5 #6 (DB & Service): K1=10 + K2=5 = 15, chọn K1 = 10, admin cộng thêm CH2 = 22 (bỏ kho STATUS=0), chi tiết kho & ẩn tồn 0 khớp GetStockBalanceIn",
                    parityOk && adminSumOk
                    && s1XAll != null && s1XAll.StockQuantity == 15m && s1XAll.CostValue == 180000m && s1XAll.TwoUnitDisplay == "2 lốc 3 chai"
                    && s1ZAll != null && s1ZAll.StockQuantity == 0m
                    && s1XK1 != null && s1XK1.StockQuantity == 10m && s1XK1.CostValue == 120000m
                    && s1BlockedOther.Count == 0 && hideZeroItemsOk && detOk,
                    SingleLine("parity=" + parityOk + ", adm=" + (adminX == null ? -1m : adminX.StockQuantity)
                        + ", s1All=" + (s1XAll == null ? -1m : s1XAll.StockQuantity)
                        + ", s1K1=" + (s1XK1 == null ? -1m : s1XK1.StockQuantity)
                        + ", blk=" + s1BlockedOther.Count + ", hz=" + hideZeroItemsOk + ", det=" + detOk));

                // Non-admin thuộc CH1 KHÔNG có quyền Xem giá nhập: xóa trắng giá ở cả lưới chính lẫn chi tiết theo kho.
                var permsNoPrice = new FunctionPermissions(false);
                permsNoPrice.Add("fn_tk", InventoryConstants.FunctionName, RoleModes.View);
                var noPriceSess = new UserSession
                {
                    UserId = "u_tnk_np_" + tag, UserName = "u_tnk_np", DisplayName = "User TNK NoPrice",
                    IsAdmin = false, DatabasePath = db, Permissions = permsNoPrice,
                    Stores = new List<StoreRecord> { new StoreRecord { Id = store1, Name = "CH TNK 1 " + tag } },
                    CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK 1 " + tag }
                };
                var noPriceSvc = new InventoryService(noPriceSess);
                Dictionary<string, Dictionary<string, decimal>> npByWh;
                List<InventoryItemRow> npItems = noPriceSvc.LoadItemsForMode(InventoryConstants.AllWarehousesId, true, out npByWh);
                InventoryItemRow npX = npItems.Find(x => x.ProductId == prodX);
                List<InventoryWarehouseDetailRow> npDetails = noPriceSvc.LoadProductWarehouseDetails(prodX, false);

                bool npMainOk = npX != null && npX.StockQuantity == 15m
                    && npX.BuyPrice == 0m && npX.CostPrice == 0m && npX.CostValue == 0m && npX.SellValue == 0m;
                bool npDetOk = npDetails.Count == 3
                    && !npDetails.Exists(d => d.WarehouseId == whOther)
                    && npDetails.TrueForAll(d => d.CostPrice == 0m && d.CostValue == 0m);

                Check("Tồn nhiều kho §5 #7 (Service non-admin & không quyền Xem giá nhập): chỉ cộng kho cửa hàng mình (15), xóa giá vốn & giá trị vốn về 0 ở cả lưới chính và bảng chi tiết",
                    npMainOk && npDetOk,
                    SingleLine("main=" + npMainOk + ", det=" + npDetOk + ", detCnt=" + npDetails.Count));
            }
            finally
            {
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
                SafeDeleteById(db, "DKHOHANG", whInactive);
                SafeDeleteById(db, "DCUAHANG", store1);
                SafeDeleteById(db, "DCUAHANG", store2);
            }
        }

        private static void TestAllWarehousesMenuAndNormalMode(string db, string store1, string whK1, string tag)
        {
            var adminSess = new UserSession
            {
                UserId = "selftest_tnk",
                UserName = "selftest_tnk",
                DisplayName = "Admin TNK",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true),
                CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag }
            };
            var permsView = new FunctionPermissions(false);
            permsView.Add("", InventoryConstants.FunctionName, RoleModes.View);
            var viewSess = new UserSession
            {
                UserId = "u_tnk_vw",
                UserName = "u_tnk_vw",
                DisplayName = "View TNK",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = permsView,
                Stores = new List<StoreRecord> { new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag } },
                CurrentStore = new StoreRecord { Id = store1, Name = "CH TNK UI 1 " + tag }
            };
            var deniedSess = new UserSession
            {
                UserId = "u_tnk_no",
                UserName = "u_tnk_no",
                DisplayName = "No Perm TNK",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            };

            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode multiNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsAllWarehousesInventoryMenu(n));
            MenuNode normalNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsInventoryMenu(n));
            MenuNode viewMultiNode = FindMenuLeaf(MainMenuBuilder.Build(viewSess).Roots, n => ScreenFactory.IsAllWarehousesInventoryMenu(n));
            MenuNode deniedMultiNode = FindMenuLeaf(MainMenuBuilder.Build(deniedSess).Roots, n => ScreenFactory.IsAllWarehousesInventoryMenu(n));

            bool foldMatchOk = ScreenFactory.IsAllWarehousesInventoryMenu(new MenuNode { Title = "Tồn nhiều kho" })
                && ScreenFactory.IsAllWarehousesInventoryMenu(new MenuNode { Title = "ton nhieu kho" })
                && ScreenFactory.IsAllWarehousesInventoryMenu(new MenuNode { Title = "TỒN NHIỀU KHO" })
                && !ScreenFactory.IsAllWarehousesInventoryMenu(new MenuNode { Title = "Tồn kho" });

            bool opensMultiOk = false;
            if (multiNode != null)
            {
                using (Control created = ScreenFactory.CreateView(multiNode, adminSess))
                {
                    var iv = created as InventoryView;
                    var sel = iv == null ? null : iv.WarehouseComboForTest.SelectedItem as LookupItem;
                    opensMultiOk = iv != null && iv.IsAllWarehousesModeForTest && iv.DetailGridForTest != null
                        && sel != null && sel.Id == InventoryConstants.AllWarehousesId;
                }
            }

            // Tồn kho thường vẫn KHÔNG có mục "Tất cả các kho" và KHÔNG có bảng chi tiết.
            bool normalUnchangedOk = false;
            if (normalNode != null)
            {
                using (Control created = ScreenFactory.CreateView(normalNode, adminSess))
                {
                    var iv = created as InventoryView;
                    if (iv != null)
                    {
                        bool hasAllItem = false;
                        foreach (object obj in iv.WarehouseComboForTest.Items)
                        {
                            var li = obj as LookupItem;
                            if (li != null && (li.Id == InventoryConstants.AllWarehousesId
                                || string.Equals(li.Name, InventoryConstants.AllWarehousesDisplayName, StringComparison.OrdinalIgnoreCase)))
                            {
                                hasAllItem = true;
                            }
                        }
                        var sel = iv.WarehouseComboForTest.SelectedItem as LookupItem;
                        normalUnchangedOk = !iv.IsAllWarehousesModeForTest && iv.DetailGridForTest == null
                            && !hasAllItem && sel != null && sel.Id == whK1;
                    }
                }
            }

            bool previewAllOk = false;
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
                        pv.Preview("all");
                        previewAllOk = pv.ViewForTest != null && pv.ViewForTest.IsAllWarehousesModeForTest
                            && pv.ViewForTest.DetailGridForTest != null;
                    }
                    finally { pv.Close(); }
                }
            }

            Check("Tồn nhiều kho §5 #7 (menu, phân quyền & Tồn kho thường không đổi): menu mở đúng chế độ qua ScreenFactory, phân quyền theo Tồn kho, Tồn kho thường không có Tất cả các kho",
                multiNode != null && viewMultiNode != null && deniedMultiNode == null
                && foldMatchOk && opensMultiOk && normalUnchangedOk && previewAllOk,
                SingleLine("admNode=" + (multiNode != null) + ", vwNode=" + (viewMultiNode != null)
                    + ", denHidden=" + (deniedMultiNode == null) + ", fold=" + foldMatchOk
                    + ", opensMulti=" + opensMultiOk + ", normalOk=" + normalUnchangedOk + ", pvAll=" + previewAllOk));
        }
    }
}
