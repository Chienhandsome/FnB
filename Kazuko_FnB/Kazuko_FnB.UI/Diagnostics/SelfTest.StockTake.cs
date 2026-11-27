using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void StockTakeTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_kk",
                    UserName = "selftest_kk",
                    DisplayName = "Selftest KiemKe",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                StockTakeRulesPureTests();
                StockTakeDbTests(db);
                StockTakeUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void StockTakeRulesPureTests()
        {
            var prod2u = new StockTakeProductItem
            {
                Id = "P2U", Code = "NL2U", Name = "Bia Lon",
                UnitId = "U_LON", UnitName = "Lon",
                BigUnitId = "U_THUNG", BigUnitName = "Thùng", QuyDoi = 24m
            };

            // §6 #2 & #3: ĐVT chẵn quy đổi 24, gộp dòng cùng mặt hàng (cùng ĐVT và khác ĐVT), chặn SL âm, chặn thiếu số phiếu/kho/dòng.
            var lines = new List<StockTakeLine>();
            string e1 = StockTakeRules.AddOrMergeLine(lines, prod2u, "U_THUNG", "Thùng", 1m, 50m);
            string e2 = StockTakeRules.AddOrMergeLine(lines, prod2u, "U_THUNG", "Thùng", 1m, 50m);
            // 2 thùng * 24 = 48, tồn hệ thống 50 => chênh lệch -2, SLXUAT = 2, SLNHAP = 0.
            bool mergeSameOk = e1 == null && e2 == null && lines.Count == 1
                && lines[0].RawActualQuantity == 2m && lines[0].ActualQuantityBase == 48m
                && lines[0].Difference == -2m && lines[0].ExportQty == 2m && lines[0].ImportQty == 0m
                && lines[0].Tone == StockTakeDiffTone.Shortage;

            // Thêm thêm 4 lon lẻ vào cùng mặt hàng => gộp về 52 lon cơ sở, chênh lệch +2, SLNHAP = 2.
            string e3 = StockTakeRules.AddOrMergeLine(lines, prod2u, "U_LON", "Lon", 4m, 50m);
            bool mergeDiffUnitOk = e3 == null && lines.Count == 1
                && lines[0].UnitId == "U_LON" && lines[0].RawActualQuantity == 52m && lines[0].ActualQuantityBase == 52m
                && lines[0].Difference == 2m && lines[0].ImportQty == 2m && lines[0].ExportQty == 0m
                && lines[0].Tone == StockTakeDiffTone.Surplus;

            string eNeg = StockTakeRules.AddOrMergeLine(lines, prod2u, "U_LON", "Lon", -1m, 50m);

            var okLine = new StockTakeLine { ProductId = "P1", ProductName = "Hàng 1", RawActualQuantity = 0m };
            string vNoNum = StockTakeRules.Validate(new StockTakeRow { Name = " ", WarehouseId = "W1", Lines = new List<StockTakeLine> { okLine } });
            string vNoWh = StockTakeRules.Validate(new StockTakeRow { Name = "KK1", WarehouseId = "", Lines = new List<StockTakeLine> { okLine } });
            string vNoLines = StockTakeRules.Validate(new StockTakeRow { Name = "KK1", WarehouseId = "W1", Lines = new List<StockTakeLine>() });
            string vDupProd = StockTakeRules.Validate(new StockTakeRow
            {
                Name = "KK1", WarehouseId = "W1",
                Lines = new List<StockTakeLine>
                {
                    new StockTakeLine { ProductId = "P1", ProductName = "Hàng 1", RawActualQuantity = 1m },
                    new StockTakeLine { ProductId = "P1", ProductName = "Hàng 1", RawActualQuantity = 2m }
                }
            });

            Check("Kiểm kê §6 #2 & #3 (luật thuần): quy đổi 2 ĐVT (2 thùng=48), gộp dòng cùng & khác ĐVT, cho phép SL=0, chặn SL âm/thiếu số phiếu/thiếu kho/trùng dòng",
                mergeSameOk && mergeDiffUnitOk && eNeg == StockTakeConstants.ActualQtyNegative
                && vNoNum == PurchaseConstants.NumberRequired
                && vNoWh == StockTakeConstants.WarehouseRequired
                && vNoLines == StockTakeConstants.LinesRequired
                && vDupProd != null && vDupProd.IndexOf("Hàng 1", StringComparison.Ordinal) >= 0,
                SingleLine("same=" + mergeSameOk + ", diffU=" + mergeDiffUnitOk + ", neg=" + (eNeg ?? "null")));
        }

        private static void StockTakeDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whNoNegId = DbHelper.NewId();
            string whAllowNegId = DbHelper.NewId();
            string unitOddId = DbHelper.NewId();
            string unitEvenId = DbHelper.NewId();
            string prodShortId = DbHelper.NewId();
            string prodSurplusId = DbHelper.NewId();
            string prodExactId = DbHelper.NewId();
            string prod2UnitId = DbHelper.NewId();
            string prodDriftId = DbHelper.NewId();
            string prodCostId = DbHelper.NewId();
            var createdOrderIds = new List<string>();

            var cfgRepo = new ConfigRepository(db);
            int origLogFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            int origAllowNegSys = SystemConfig.GetInt("ChoPhepAmKho");
            int origTwoUnitFlag = SystemConfig.GetInt("SuDung2DonViTinh");
            bool hasTwoUnitRow = BookingCount(db, "SELECT COUNT(*) FROM SCONFIG WHERE NAME = 'SuDung2DonViTinh'") > 0;

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 }
                }, "selftest_kk");
                if (hasTwoUnitRow)
                    cfgRepo.Save(new[] { new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 30 } }, "selftest_kk");
                SystemConfig.Reload(db);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')",
                    "@I", storeId, "@N", "CH KiemKe " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_kk', @T)",
                    "@I", whNoNegId, "@N", "Kho KK KhongAm " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_kk', @T)",
                    "@I", whAllowNegId, "@N", "Kho KK ChoAm " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')",
                    "@I", unitOddId, "@N", "Lon " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kk')",
                    "@I", unitEvenId, "@N", "Thùng " + tag);

                Action<string, string, string, string, decimal> insertProd = (id, code, name, bigU, qd) =>
                {
                    BookingExec(db,
                        "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @C, @N, '2', @U1, @U2, @QD, 10000, 15000, 10000, 30, 'selftest_kk', @T)",
                        "@I", id, "@C", code, "@N", name, "@U1", unitOddId, "@U2", bigU, "@QD", qd, "@T", now);
                };
                insertProd(prodShortId, "KK-SH-" + tag, "Trà Đen " + tag, null, 0m);
                insertProd(prodSurplusId, "KK-SU-" + tag, "Sữa Tươi " + tag, null, 0m);
                insertProd(prodExactId, "KK-EX-" + tag, "Đường Nâu " + tag, null, 0m);
                insertProd(prod2UnitId, "KK-2U-" + tag, "Bia Hộp 2DVT " + tag, unitEvenId, 24m);
                insertProd(prodDriftId, "KK-DR-" + tag, "Bột Matcha " + tag, null, 0m);
                insertProd(prodCostId, "KK-GV-" + tag, "Hạt Cà Phê " + tag, null, 0m);

                // Seed tồn ban đầu tại whNoNegId: prodShort=10, prodSurplus=10, prodExact=10, prod2Unit=50, prodDrift=10.
                var purSvc = new PurchaseService(AppSession.Current);
                PurchaseSaveResult seedRes = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-SEED-KK-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prodShortId, ProductName = "Trà Đen " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 10000m },
                        new PurchaseLine { ProductId = prodSurplusId, ProductName = "Sữa Tươi " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 10000m },
                        new PurchaseLine { ProductId = prodExactId, ProductName = "Đường Nâu " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 10000m },
                        new PurchaseLine { ProductId = prod2UnitId, ProductName = "Bia Hộp 2DVT " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 50m, UnitPrice = 10000m },
                        new PurchaseLine { ProductId = prodDriftId, ProductName = "Bột Matcha " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 10000m }
                    }
                });
                if (seedRes.Ok) createdOrderIds.Add(seedRes.SavedId);

                var svc = new StockTakeService(AppSession.Current);

                // §6 #1: tồn 10 -> thực tế 7 (SLXUAT=3, tồn 7), tồn 10 -> thực tế 12 (SLNHAP=2, tồn 12), tồn 10 -> thực tế 10 (cả hai = 0, vẫn ghi dòng).
                string kkNum1 = svc.GenerateNumber(DateTime.Today);
                var kk1 = new StockTakeRow
                {
                    Name = kkNum1,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Description = "Kiểm kê định kỳ " + tag,
                    Note = "Phiếu kiểm kê #1",
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodShortId, ProductName = "Trà Đen " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 7m },
                        new StockTakeLine { ProductId = prodSurplusId, ProductName = "Sữa Tươi " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 12m },
                        new StockTakeLine { ProductId = prodExactId, ProductName = "Đường Nâu " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 10m }
                    }
                };
                StockTakeSaveResult r1 = svc.Save(kk1);
                if (r1.Ok) createdOrderIds.Add(r1.SavedId);

                DataTable dtLines1 = r1.Ok ? ReadStockTakeDetailRows(db, r1.SavedId) : new DataTable();
                decimal balShort = svc.GetStockBalance(prodShortId, whNoNegId);
                decimal balSurplus = svc.GetStockBalance(prodSurplusId, whNoNegId);
                decimal balExact = svc.GetStockBalance(prodExactId, whNoNegId);
                bool hdr1Ok = r1.Ok
                    && kkNum1.StartsWith("KK" + DateTime.Today.ToString("yy") + "/", StringComparison.Ordinal)
                    && BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I AND LOAI = 4 AND STATUS = 30 AND DATHANHTOAN = 30 AND COALESCE(TIENHANG, 0) = 0 AND COALESCE(TONGCONG, 0) = 0", "@I", r1.SavedId) == 1;
                bool lShortOk = MatchDetailRow(dtLines1, prodShortId, 10m, 7m, 0m, 0m, 3m, 3m);
                bool lSurplusOk = MatchDetailRow(dtLines1, prodSurplusId, 10m, 12m, 2m, 2m, 0m, 0m);
                bool lExactOk = MatchDetailRow(dtLines1, prodExactId, 10m, 10m, 0m, 0m, 0m, 0m);

                Check("Kiểm kê §6 #1 (tồn 10 thực tế 7/12/10): ghi đúng LOAI=4 không tiền, SLXUAT=SLXUATCHUAQUYDOI=3 (tồn 7), SLNHAP=SLNHAPCHUAQUYDOI=2 (tồn 12), dòng khớp=0 vẫn lưu",
                    hdr1Ok && dtLines1.Rows.Count == 3 && lShortOk && lSurplusOk && lExactOk
                    && balShort == 7m && balSurplus == 12m && balExact == 10m,
                    SingleLine(r1.ErrorMessage ?? ("bal=" + balShort + "/" + balSurplus + "/" + balExact + ", rows=" + dtLines1.Rows.Count)));

                // §6 #2: ĐVT chẵn (quy đổi 24): tồn 50 lẻ, thực tế 2 thùng -> quy 48, SLXUAT=SLXUATCHUAQUYDOI=2, tồn=48 (cả khi SuDung2DonViTinh=30).
                var kk2u = new StockTakeRow
                {
                    Name = "KK-2U-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Description = "Kiểm kê 2 ĐVT",
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine
                        {
                            ProductId = prod2UnitId, ProductName = "Bia Hộp 2DVT " + tag,
                            UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m,
                            SystemQuantity = 50m, RawActualQuantity = 2m
                        }
                    }
                };
                StockTakeSaveResult r2u = svc.Save(kk2u);
                if (r2u.Ok) createdOrderIds.Add(r2u.SavedId);
                DataTable dt2u = r2u.Ok ? ReadStockTakeDetailRows(db, r2u.SavedId) : new DataTable();
                decimal bal2u = svc.GetStockBalance(prod2UnitId, whNoNegId);

                Check("Kiểm kê §6 #2 (ĐVT chẵn quy đổi 24, SuDung2DonViTinh=30): tồn 50 nhập 2 thùng -> SLTHUCTE=48, SLXUAT=SLXUATCHUAQUYDOI=2, tồn còn 48",
                    r2u.Ok && MatchDetailRow(dt2u, prod2UnitId, 50m, 48m, 0m, 0m, 2m, 2m) && bal2u == 48m,
                    SingleLine(r2u.ErrorMessage ?? ("bal2u=" + bal2u)));

                // §6 #3: Trùng số phiếu bị chặn; số phiếu dài 255 ký tự lưu được không làm lưu vết tràn.
                StockTakeSaveResult rDupNum = svc.Save(new StockTakeRow
                {
                    Name = kkNum1,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodExactId, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 10m }
                    }
                });

                string num255 = "KK255-" + tag + "-" + new string('K', 255 - 7 - tag.Length);
                var kk255 = new StockTakeRow
                {
                    Name = num255,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodExactId, ProductName = "Đường Nâu " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 10m }
                    }
                };
                StockTakeSaveResult r255 = svc.Save(kk255);
                if (r255.Ok) createdOrderIds.Add(r255.SavedId);
                int log255 = r255.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", r255.SavedId) : 0;

                Check("Kiểm kê §6 #3 (trùng số phiếu báo lỗi, số phiếu 255 ký tự không làm tràn lưu vết): đạt",
                    !rDupNum.Ok && rDupNum.ErrorMessage == PurchaseConstants.NumberDuplicate(kkNum1)
                    && r255.Ok && num255.Length == 255 && log255 >= 1,
                    SingleLine("dup=" + (rDupNum.ErrorMessage ?? "ok") + ", log255=" + log255));

                // §6 #4 (T2): Tồn hệ thống đổi giữa lúc thêm dòng (10) và lúc lưu (thêm phiếu nhập +5 => 15) -> báo và không lưu; lưu lại ghi đúng số mới.
                decimal seenSys = svc.GetStockBalance(prodDriftId, whNoNegId);
                var kkDrift = new StockTakeRow
                {
                    Name = "KK-DRIFT-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodDriftId, ProductName = "Bột Matcha " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = seenSys, RawActualQuantity = 7m }
                    }
                };
                // Phát sinh nhập thêm 5 trước khi bấm Lưu.
                PurchaseSaveResult driftPur = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-DRIFT-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prodDriftId, ProductName = "Bột Matcha " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 5m, UnitPrice = 10000m }
                    }
                });
                if (driftPur.Ok) createdOrderIds.Add(driftPur.SavedId);

                StockTakeSaveResult rDriftBlocked = svc.Save(kkDrift);
                int driftSavedBeforeConfirm = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE NAME = @N", "@N", "KK-DRIFT-" + tag);
                bool driftUpdatedInMemory = kkDrift.Lines[0].SystemQuantity == 15m && kkDrift.Lines[0].Difference == -8m;

                // Người dùng xem lại số mới (15) và xác nhận lưu lại.
                StockTakeSaveResult rDriftConfirmed = svc.Save(kkDrift);
                if (rDriftConfirmed.Ok) createdOrderIds.Add(rDriftConfirmed.SavedId);
                DataTable dtDrift = rDriftConfirmed.Ok ? ReadStockTakeDetailRows(db, rDriftConfirmed.SavedId) : new DataTable();
                decimal balDriftAfter = svc.GetStockBalance(prodDriftId, whNoNegId);

                Check("Kiểm kê §6 #4 (T2 tồn hệ thống đổi giữa lúc thêm và lúc lưu): báo \"Tồn hệ thống đã thay đổi, hãy xem lại\" & không lưu; lưu lại ghi SLHETHONG=15 SLXUAT=8 tồn=7",
                    seenSys == 10m
                    && !rDriftBlocked.Ok && rDriftBlocked.SystemStockChanged
                    && rDriftBlocked.ErrorMessage.IndexOf("Tồn hệ thống đã thay đổi, hãy xem lại", StringComparison.Ordinal) >= 0
                    && driftSavedBeforeConfirm == 0 && driftUpdatedInMemory
                    && rDriftConfirmed.Ok && MatchDetailRow(dtDrift, prodDriftId, 15m, 7m, 0m, 0m, 8m, 8m) && balDriftAfter == 7m,
                    SingleLine("blk=" + (!rDriftBlocked.Ok) + ", memSys=" + kkDrift.Lines[0].SystemQuantity + ", after=" + balDriftAfter));

                // §6 #5: Sửa phiếu (loại trừ chính phiếu), đổi kho bị khóa, sửa/xóa gây âm kho bị chặn khi CHOPHEPAMKHO=0 & cho phép khi CHOPHEPAMKHO=30.
                // 1) Đổi kho trên phiếu kk1 đã có dòng -> chặn.
                StockTakeRow kk1ForWhChange = svc.LoadById(kk1.Id);
                kk1ForWhChange.WarehouseId = whAllowNegId;
                StockTakeSaveResult rWhLocked = svc.Save(kk1ForWhChange);

                // 2) Sửa kk1: đổi prodSurplus từ 12 thành 15 (tồn không tính kk1 vẫn là 10 => SLNHAP=5, tồn=15).
                StockTakeRow kk1Edit = svc.LoadById(kk1.Id);
                StockTakeLine lineSurp = kk1Edit.Lines.Find(l => l.ProductId == prodSurplusId);
                lineSurp.RawActualQuantity = 15m;
                StockTakeSaveResult rEditOk = svc.Save(kk1Edit);
                decimal balSurpAfterEdit = svc.GetStockBalance(prodSurplusId, whNoNegId);

                // 3) Xuất 12 prodSurplus từ whNoNegId (tồn còn 3) -> xóa phiếu kk1 (mất +5 => tồn còn -2) và sửa bỏ dòng prodSurplus PHẢI BỊ CHẶN!
                var expSvc = new StockDocumentService(AppSession.Current, StockDocumentKind.Export);
                StockDocumentSaveResult expOut = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-NEGKK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whNoNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodSurplusId, ProductName = "Sữa Tươi " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 12m, UnitPrice = 10000m }
                    }
                });
                if (expOut.Ok) createdOrderIds.Add(expOut.SavedId);

                StockTakeDeleteResult rDelNegBlocked = svc.Delete(kk1.Id);
                StockTakeRow kk1RemoveLine = svc.LoadById(kk1.Id);
                kk1RemoveLine.Lines.RemoveAll(l => l.ProductId == prodSurplusId);
                StockTakeSaveResult rEditNegBlocked = svc.Save(kk1RemoveLine);

                // 4) Kho cho âm (CHOPHEPAMKHO=30): kiểm kê tăng +5 rồi xuất 4 rồi xóa phiếu kiểm kê -> ĐƯỢC PHÉP (tồn = -4).
                var kkAllow = new StockTakeRow
                {
                    Name = "KK-ALLOW-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whAllowNegId,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodSurplusId, ProductName = "Sữa Tươi " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 0m, RawActualQuantity = 5m }
                    }
                };
                StockTakeSaveResult rAllowSave = svc.Save(kkAllow);
                if (rAllowSave.Ok) createdOrderIds.Add(rAllowSave.SavedId);
                StockDocumentSaveResult expAllow = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-ALLOWKK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whAllowNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prodSurplusId, ProductName = "Sữa Tươi " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 4m, UnitPrice = 10000m }
                    }
                });
                if (expAllow.Ok) createdOrderIds.Add(expAllow.SavedId);
                StockTakeDeleteResult rDelAllowOk = svc.Delete(kkAllow.Id);
                decimal balAllowAfterDel = svc.GetStockBalance(prodSurplusId, whAllowNegId);

                Check("Kiểm kê §6 #5 (sửa tính lại tồn loại trừ chính phiếu, đổi kho bị khóa, sửa/xóa gây âm kho bị chặn khi không cho âm & cho khi cho âm): đạt toàn bộ",
                    !rWhLocked.Ok && rWhLocked.ErrorMessage == StockTakeConstants.WarehouseLockedOnEdit
                    && rEditOk.Ok && balSurpAfterEdit == 15m
                    && !rDelNegBlocked.Ok && rDelNegBlocked.ErrorMessage.IndexOf("Sữa Tươi " + tag, StringComparison.Ordinal) >= 0
                    && rDelNegBlocked.ErrorMessage.IndexOf("3", StringComparison.Ordinal) >= 0 && rDelNegBlocked.ErrorMessage.IndexOf("-2", StringComparison.Ordinal) >= 0
                    && !rEditNegBlocked.Ok && rEditNegBlocked.ErrorMessage.IndexOf("Sữa Tươi " + tag, StringComparison.Ordinal) >= 0
                    && rDelAllowOk.Ok && balAllowAfterDel == -4m,
                    SingleLine("whLock=" + (!rWhLocked.Ok) + ", editBal=" + balSurpAfterEdit + ", delBlk=" + (!rDelNegBlocked.Ok) + ", editBlk=" + (!rEditNegBlocked.Ok) + ", allow=" + balAllowAfterDel));

                // §6 #6: Rollback hook trên Save & Delete + xóa cứng hoàn tồn & lưu vết.
                bool saveRbOk = false;
                var rbRow = new StockTakeRow
                {
                    Name = "KK-RB-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    Lines = new List<StockTakeLine>
                    {
                        new StockTakeLine { ProductId = prodExactId, UnitId = unitOddId, BaseUnitId = unitOddId, SystemQuantity = 10m, RawActualQuantity = 8m }
                    }
                };
                try
                {
                    svc.Save(rbRow, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Save kiểm kê"); });
                }
                catch (InvalidOperationException)
                {
                    saveRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE NAME = @N", "@N", "KK-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbRow.Id);
                }

                bool delRbOk = false;
                try
                {
                    svc.Delete(kk2u.Id, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Delete kiểm kê"); });
                }
                catch (InvalidOperationException)
                {
                    delRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", kk2u.Id) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", kk2u.Id) == 1;
                }

                StockTakeDeleteResult rDel2u = svc.Delete(kk2u.Id);
                int leftHdr2u = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", kk2u.Id);
                int leftLn2u = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", kk2u.Id);
                int delLog2u = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", kk2u.Id);
                decimal bal2uRestored = svc.GetStockBalance(prod2UnitId, whNoNegId);

                Check("Kiểm kê §6 #6 (xóa cứng mất phiếu+chi tiết, tồn trở lại 50, có lưu vết; rollback hook Save/Delete): đạt toàn bộ",
                    saveRbOk && delRbOk && rDel2u.Ok && leftHdr2u == 0 && leftLn2u == 0 && delLog2u >= 2 && bal2uRestored == 50m,
                    SingleLine("rb=" + saveRbOk + "/" + delRbOk + ", left=" + leftHdr2u + "/" + leftLn2u + ", logs=" + delLog2u + ", bal=" + bal2uRestored));

                // §6 #8: Giá vốn — nhập 10 giá trị 100.000 (giaVon=10.000); kiểm kê thiếu 3 (tồn 7, giá trị 70.000); kiểm kê thừa 2 (tồn 9, giá trị 90.000).
                bool costOk = VerifyStockTakeCostFormulaParticipation(db, purSvc, svc, whNoNegId, prodCostId, unitOddId, tag, createdOrderIds);
                Check("Kiểm kê §6 #8 (tham gia tính giá vốn TON/GIATRI theo công thức bình quân cuối kỳ): phiếu LOAI=4 STATUS=30 trừ/cộng đúng TON=7->9 & GIATRI=70.000->90.000",
                    costOk);
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origLogFlag },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = origAllowNegSys }
                }, "selftest_kk");
                if (hasTwoUnitRow)
                    cfgRepo.Save(new[] { new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = origTwoUnitFlag } }, "selftest_kk");
                SystemConfig.Reload(db);

                foreach (string oid in createdOrderIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prodShortId);
                SafeDeleteById(db, "DMATHANG", prodSurplusId);
                SafeDeleteById(db, "DMATHANG", prodExactId);
                SafeDeleteById(db, "DMATHANG", prod2UnitId);
                SafeDeleteById(db, "DMATHANG", prodDriftId);
                SafeDeleteById(db, "DMATHANG", prodCostId);
                SafeDeleteById(db, "DDONVITINH", unitOddId);
                SafeDeleteById(db, "DDONVITINH", unitEvenId);
                SafeDeleteById(db, "DKHOHANG", whNoNegId);
                SafeDeleteById(db, "DKHOHANG", whAllowNegId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }
    }
}
