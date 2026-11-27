using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void StockDocumentTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_xck",
                    UserName = "selftest_xck",
                    DisplayName = "Selftest XuatChuyenKho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                StockDocumentRulesPureTests();
                StockExportDbTests(db);
                StockTransferDbTests(db);
                StockDocumentUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void StockDocumentRulesPureTests()
        {
            var prod = new StockProductItem
            {
                Id = "P1", Code = "NL01", Name = "Sữa tươi",
                UnitId = "U_LON", UnitName = "Lon",
                BigUnitId = "U_THUNG", BigUnitName = "Thùng", QuyDoi = 24m,
                SellPrice = 300000m, BuyPrice = 240000m, CostPrice = 250000m
            };

            // Cấu hình GiaSuDungKhiChuyenXuatKho = 0 (GIABAN), 1 (GIANHAP), 2 (GIAVON).
            bool priceModeOk = StockDocumentRules.ResolveDefaultPrice(prod, 0) == 300000m
                && StockDocumentRules.ResolveDefaultPrice(prod, 1) == 240000m
                && StockDocumentRules.ResolveDefaultPrice(prod, 2) == 250000m;

            // Xuất khác ép thuế/phí = 0; Chuyển kho giữ thuế/phí như phiếu nhập.
            var lines = new List<StockDocumentLine>();
            string e1 = StockDocumentRules.AddOrMergeLine(lines, prod, "U_THUNG", "Thùng", 2m, 240000m, false, StockDocumentKind.Export);
            string e2 = StockDocumentRules.AddOrMergeLine(lines, prod, "U_THUNG", "Thùng", 1m, 240000m, false, StockDocumentKind.Export);
            string eZero = StockDocumentRules.AddOrMergeLine(lines, prod, "U_LON", "Lon", 0m, 10000m, false, StockDocumentKind.Export);

            // 3 thùng * 240.000 = 720.000, SL quy đổi = 72.
            PurchaseTotals expTot = StockDocumentRules.CalcTotals(StockDocumentKind.Export, lines, 10m, 0m, false, 8m, 15000m);
            // Export: TIENGIAMGIA = 72.000, TIENTHUE = 0, PHIVANCHUYEN = 0 => TONGCONG = 648.000.
            PurchaseTotals trTot = StockDocumentRules.CalcTotals(StockDocumentKind.Transfer, lines, 10m, 0m, false, 8m, 15000m);
            // Transfer: sau giảm = 648.000, thuế 8% = 51.840, phí = 15.000 => TONGCONG = 714.840.

            decimal revPrice = StockDocumentRules.CalcUnitPriceFromAmount(3m, 750000m);

            // Validate: thiếu số phiếu, thiếu kho xuất, thiếu kho nhập, kho nhập trùng kho xuất, dòng trống, SL=0.
            var okLine = new StockDocumentLine { ProductId = "P1", RawQuantity = 1m, UnitPrice = 10000m };
            string vNoNum = StockDocumentRules.Validate(new StockDocumentRow { Name = " ", SourceWarehouseId = "W1", Lines = new List<StockDocumentLine> { okLine } }, StockDocumentKind.Export);
            string vNoSrc = StockDocumentRules.Validate(new StockDocumentRow { Name = "PX1", SourceWarehouseId = "", Lines = new List<StockDocumentLine> { okLine } }, StockDocumentKind.Export);
            string vNoDst = StockDocumentRules.Validate(new StockDocumentRow { Name = "CK1", SourceWarehouseId = "W1", DestWarehouseId = "", Lines = new List<StockDocumentLine> { okLine } }, StockDocumentKind.Transfer);
            string vSameWh = StockDocumentRules.Validate(new StockDocumentRow { Name = "CK1", SourceWarehouseId = "W1", DestWarehouseId = "W1", Lines = new List<StockDocumentLine> { okLine } }, StockDocumentKind.Transfer);
            string vNoLine = StockDocumentRules.Validate(new StockDocumentRow { Name = "PX1", SourceWarehouseId = "W1", Lines = new List<StockDocumentLine>() }, StockDocumentKind.Export);

            Check("Xuất/Chuyển §7 #1-#3 (luật thuần): đơn giá 0/1/2, gộp dòng 2 ĐVT (SL=72), xuất khác ép thuế/phí=0, chuyển kho giữ thuế/phí, chặn trùng kho",
                priceModeOk && e1 == null && e2 == null && eZero == StockDocumentConstants.ExportQtyMustBePositive
                && lines.Count == 1 && lines[0].RawQuantity == 3m && lines[0].ConvertedQuantity == 72m
                && expTot.TienHang == 720000m && expTot.TienGiamGia == 72000m && expTot.TienThue == 0m && expTot.TongCong == 648000m
                && trTot.TienThue == 51840m && trTot.TongCong == 714840m && revPrice == 250000m
                && vNoNum == PurchaseConstants.NumberRequired
                && vNoSrc == StockDocumentConstants.SourceWarehouseRequired
                && vNoDst == StockDocumentConstants.DestWarehouseRequired
                && vSameWh == StockDocumentConstants.SameWarehouseBlocked
                && vNoLine == StockDocumentConstants.ExportLinesRequired,
                SingleLine("exp=" + expTot.TongCong + ", tr=" + trTot.TongCong + ", same=" + (vSameWh ?? "null")));
        }

        private static void StockExportDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whNoNegId = DbHelper.NewId();
            string whAllowNegId = DbHelper.NewId();
            string suppId = DbHelper.NewId();
            string empId = DbHelper.NewId();
            string unitOddId = DbHelper.NewId();
            string unitEvenId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();
            string prod2UnitId = DbHelper.NewId();
            var createdOrderIds = new List<string>();

            var cfgRepo = new ConfigRepository(db);
            int origLogFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            int origAllowNegSys = SystemConfig.GetInt("ChoPhepAmKho");
            int origPriceMode = SystemConfig.GetInt(StockDocumentConstants.ConfigPriceMode);
            bool hasPriceModeRow = BookingCount(db, "SELECT COUNT(*) FROM SCONFIG WHERE NAME = @N", "@N", StockDocumentConstants.ConfigPriceMode) > 0;

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 }
                }, "selftest_xck");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", storeId, "@N", "CH XuatKhac " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_xck', @T)",
                    "@I", whNoNegId, "@N", "Kho X KhongAm " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)",
                    "@I", whAllowNegId, "@N", "Kho X ChoAm " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", suppId, "@N", "NCC XuatTra " + tag);
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", empId, "@N", "NV Xuat " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", unitOddId, "@N", "Lon " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", unitEvenId, "@N", "Thùng " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 50000, 80000, 55000, 30, 'selftest_xck', @T)",
                    "@I", prod1Id, "@C", "XK1-" + tag, "@N", "Trà Xanh " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 20000, 35000, 22000, 30, 'selftest_xck', @T)",
                    "@I", prod2Id, "@C", "XK2-" + tag, "@N", "Bột Kem " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 24, 240000, 300000, 250000, 30, 'selftest_xck', @T)",
                    "@I", prod2UnitId, "@C", "XK3-" + tag, "@N", "Sữa Đặc " + tag, "@U1", unitOddId, "@U2", unitEvenId, "@T", DbHelper.Now());

                // Seed tồn ban đầu cho whNoNegId qua PurchaseService: prod1 = 20, prod2 = 15, prod2Unit = 30 lon.
                var purSvc = new PurchaseService(AppSession.Current);
                PurchaseSaveResult seedRes = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-SEED-XK-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prod1Id, ProductName = "Trà Xanh " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 20m, UnitPrice = 50000m },
                        new PurchaseLine { ProductId = prod2Id, ProductName = "Bột Kem " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 15m, UnitPrice = 20000m },
                        new PurchaseLine { ProductId = prod2UnitId, ProductName = "Sữa Đặc " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 30m, UnitPrice = 10000m }
                    }
                });
                if (seedRes.Ok) createdOrderIds.Add(seedRes.SavedId);

                var expSvc = new StockDocumentService(AppSession.Current, StockDocumentKind.Export);

                // §7 #1: Lưu phiếu xuất 2 mặt hàng; ép thuế=0, phí=0; tồn giảm đúng.
                string expNum1 = expSvc.GenerateNumber(DateTime.Today);
                var exp1 = new StockDocumentRow
                {
                    Name = expNum1,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    SupplierId = suppId,
                    ExportEmployeeId = empId,
                    Description = "Xuất trả NCC " + tag,
                    Note = "Phiếu xuất test 1",
                    GiamTheoTien = true,
                    TienGiamGia = 25000m,
                    TileThue = 10m,         // cố tình truyền khác 0 -> phải bị ép về 0
                    PhiVanChuyen = 50000m,  // cố tình truyền khác 0 -> phải bị ép về 0
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Xanh " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 6m, UnitPrice = 50000m },
                        new StockDocumentLine { ProductId = prod2Id, ProductName = "Bột Kem " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 4m, UnitPrice = 20000m }
                    }
                };
                // Line1=300.000, Line2=80.000 => TIENHANG=380.000; GG=25.000; TONGCONG=355.000; thuế/phí=0.
                StockDocumentSaveResult r1 = expSvc.Save(exp1);
                if (r1.Ok) createdOrderIds.Add(r1.SavedId);
                StockDocumentRow back1 = r1.Ok ? expSvc.LoadById(r1.SavedId) : null;
                decimal st1After = expSvc.GetStockBalance(prod1Id, whNoNegId);
                decimal st2After = expSvc.GetStockBalance(prod2Id, whNoNegId);

                Check("Xuất khác §7 #1 (lưu 2 mặt hàng, công thức & ép thuế/phí=0, tồn giảm đúng): LOAI=2 STATUS=30 DATHANHTOAN=30, tồn 20->14 và 15->11",
                    r1.Ok && back1 != null && back1.Loai == 2 && back1.Status == 30 && back1.PaidFlag == 30
                    && back1.Lines.Count == 2
                    && back1.Lines[0].ConvertedQuantity == 6m && back1.Lines[0].Amount == 300000m && back1.Lines[0].WarehouseId == whNoNegId
                    && back1.Lines[1].ConvertedQuantity == 4m && back1.Lines[1].Amount == 80000m && back1.Lines[1].WarehouseId == whNoNegId
                    && back1.TienHang == 380000m && back1.TienGiamGia == 25000m
                    && back1.TileThue == 0m && back1.TienThue == 0m && back1.PhiVanChuyen == 0m && back1.TongCong == 355000m
                    && st1After == 14m && st2After == 11m,
                    SingleLine(r1.ErrorMessage ?? ("tong=" + (back1 == null ? 0m : back1.TongCong) + ", st=" + st1After + "/" + st2After)));

                // §7 #1: Vượt tồn bị chặn khi kho không cho âm (nêu tên hàng, tồn 14, số xuất 15); cho phép khi kho cho âm.
                StockDocumentSaveResult rOverBlocked = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-OVER-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Xanh " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 15m, UnitPrice = 50000m }
                    }
                });

                var expAllow = new StockDocumentRow
                {
                    Name = "PX-ALLOW-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whAllowNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Xanh " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 5m, UnitPrice = 50000m }
                    }
                };
                StockDocumentSaveResult rOverAllowed = expSvc.Save(expAllow);
                if (rOverAllowed.Ok) createdOrderIds.Add(rOverAllowed.SavedId);
                decimal allowStock = expSvc.GetStockBalance(prod1Id, whAllowNegId);

                // Xuất theo ĐVT chẵn: tồn 30 lon -> xuất 2 thùng (48 lon) bị chặn; xuất 1 thùng (24 lon) đạt, tồn còn 6.
                StockDocumentSaveResult r2uBlocked = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-2U-BLK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod2UnitId, ProductName = "Sữa Đặc " + tag, UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 2m, UnitPrice = 240000m }
                    }
                });
                var exp2uOk = new StockDocumentRow
                {
                    Name = "PX-2U-OK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod2UnitId, ProductName = "Sữa Đặc " + tag, UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 1m, UnitPrice = 240000m }
                    }
                };
                StockDocumentSaveResult r2uOk = expSvc.Save(exp2uOk);
                if (r2uOk.Ok) createdOrderIds.Add(r2uOk.SavedId);
                decimal st2uAfter = expSvc.GetStockBalance(prod2UnitId, whNoNegId);

                Check("Xuất khác §7 #1 (chặn vượt tồn kho không cho âm, cho khi kho cho âm, ĐVT chẵn): chặn nêu tên+tồn+xuất; kho cho âm tồn -5; 2 thùng=48 bị chặn, 1 thùng=24 đạt",
                    !rOverBlocked.Ok
                    && rOverBlocked.ErrorMessage.IndexOf("Trà Xanh " + tag, StringComparison.Ordinal) >= 0
                    && rOverBlocked.ErrorMessage.IndexOf("14", StringComparison.Ordinal) >= 0
                    && rOverBlocked.ErrorMessage.IndexOf("15", StringComparison.Ordinal) >= 0
                    && rOverAllowed.Ok && allowStock == -5m
                    && !r2uBlocked.Ok && r2uBlocked.ErrorMessage.IndexOf("48", StringComparison.Ordinal) >= 0
                    && r2uOk.Ok && st2uAfter == 6m,
                    SingleLine(rOverBlocked.ErrorMessage ?? "ok"));

                // §7 #5 & #4: Số phiếu 255 ký tự không tràn TLUUVET; xóa cứng & rollback hook.
                string num255 = "PX255-" + tag + "-" + new string('X', 255 - 7 - tag.Length);
                var exp255 = new StockDocumentRow
                {
                    Name = num255,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Xanh " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 1m, UnitPrice = 50000m }
                    }
                };
                StockDocumentSaveResult r255 = expSvc.Save(exp255);
                if (r255.Ok) createdOrderIds.Add(r255.SavedId);
                int log255 = r255.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", r255.SavedId) : 0;

                bool saveRbOk = false;
                var rbExp = new StockDocumentRow
                {
                    Name = "PX-RB-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 1m, UnitPrice = 50000m }
                    }
                };
                try
                {
                    expSvc.Save(rbExp, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Save xuất khác"); });
                }
                catch (InvalidOperationException)
                {
                    saveRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE NAME = @N", "@N", "PX-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbExp.Id);
                }

                bool delRbOk = false;
                try
                {
                    expSvc.Delete(exp255.Id, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Delete xuất khác"); });
                }
                catch (InvalidOperationException)
                {
                    delRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", exp255.Id) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", exp255.Id) == 1;
                }

                StockDocumentDeleteResult rDel = expSvc.Delete(exp255.Id);
                int leftHdr = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", exp255.Id);
                int leftLn = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", exp255.Id);
                decimal st1AfterDel = expSvc.GetStockBalance(prod1Id, whNoNegId);

                // Cấu hình GiaSuDungKhiChuyenXuatKho thay đổi giữa 0/1/2 trả đúng giá sản phẩm.
                bool cfgPriceOk = true;
                if (hasPriceModeRow)
                {
                    cfgRepo.Save(new[] { new ConfigChange { Name = StockDocumentConstants.ConfigPriceMode, DataType = 3, IntValue = 2 } }, "selftest_xck");
                    SystemConfig.Reload(db);
                    StockProductItem loadedP1 = expSvc.LoadProducts().Find(p => p.Id == prod1Id);
                    cfgPriceOk = loadedP1 != null && StockDocumentRules.ResolveDefaultPrice(loadedP1, expSvc.PriceMode) == 55000m;
                    cfgRepo.Save(new[] { new ConfigChange { Name = StockDocumentConstants.ConfigPriceMode, DataType = 3, IntValue = origPriceMode } }, "selftest_xck");
                    SystemConfig.Reload(db);
                }

                Check("Xuất khác §7 #4 & #5 (số phiếu 255 ký tự, xóa cứng hoàn tồn, rollback hook, cấu hình giá xuất): tất cả đạt",
                    r255.Ok && num255.Length == 255 && log255 >= 1
                    && saveRbOk && delRbOk && rDel.Ok && leftHdr == 0 && leftLn == 0 && st1AfterDel == 14m && cfgPriceOk,
                    SingleLine("log255=" + log255 + ", rb=" + saveRbOk + "/" + delRbOk + ", st=" + st1AfterDel));
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origLogFlag },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = origAllowNegSys }
                }, "selftest_xck");
                if (hasPriceModeRow)
                    cfgRepo.Save(new[] { new ConfigChange { Name = StockDocumentConstants.ConfigPriceMode, DataType = 3, IntValue = origPriceMode } }, "selftest_xck");
                SystemConfig.Reload(db);

                foreach (string oid in createdOrderIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prod1Id);
                SafeDeleteById(db, "DMATHANG", prod2Id);
                SafeDeleteById(db, "DMATHANG", prod2UnitId);
                SafeDeleteById(db, "DDONVITINH", unitOddId);
                SafeDeleteById(db, "DDONVITINH", unitEvenId);
                SafeDeleteById(db, "DNHANVIEN", empId);
                SafeDeleteById(db, "DNHACUNGCAP", suppId);
                SafeDeleteById(db, "DKHOHANG", whNoNegId);
                SafeDeleteById(db, "DKHOHANG", whAllowNegId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static bool CheckHeadersNoClip(DataGridView grid, string[] expectedCols)
        {
            if (grid.Columns.Count != expectedCols.Length)
                return false;
            for (int i = 0; i < expectedCols.Length; i++)
            {
                if (grid.Columns[i].HeaderText != expectedCols[i])
                    return false;
                int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                if (need > grid.Columns[i].Width)
                    return false;
            }
            return true;
        }

        private static void SelectWarehouseCombo(ComboBox combo, string warehouseId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var li = combo.Items[i] as LookupItem;
                if (li != null && li.Id == warehouseId)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static MenuNode FindMenuLeaf(List<MenuNode> nodes, Predicate<MenuNode> match)
        {
            if (nodes == null) return null;
            foreach (MenuNode n in nodes)
            {
                if (n.IsLeaf && match(n))
                    return n;
                MenuNode child = FindMenuLeaf(n.Children, match);
                if (child != null)
                    return child;
            }
            return null;
        }
    }
}
