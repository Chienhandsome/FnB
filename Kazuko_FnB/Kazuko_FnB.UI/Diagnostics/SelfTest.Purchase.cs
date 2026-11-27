using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PurchaseTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_nk",
                    UserName = "selftest_nk",
                    DisplayName = "Selftest NhapKho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                PurchaseRulesPureTests();
                PurchaseServiceDbTests(db);
                PurchaseUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void PurchaseRulesPureTests()
        {
            // §6 #2: Công thức dòng và tổng (giảm %, giảm tiền, thuế, phí vận chuyển, không làm tròn thêm TONGCONG).
            decimal lineAmt1 = PurchaseRules.CalcLineAmount(3m, 12500m, 10m); // 3 * 12500 * 0.9 = 33750
            decimal lineAmt2 = PurchaseRules.CalcLineAmount(2m, 33333m, 5m);  // 2 * 33333 * 0.95 = 63332.7 -> 63333
            decimal revPrice = PurchaseRules.CalcUnitPriceFromAmount(4m, 20m, 320000m); // 320000 / (4 * 0.8) = 100000
            decimal revZeroQty = PurchaseRules.CalcUnitPriceFromAmount(0m, 10m, 50000m);
            decimal revFullDisc = PurchaseRules.CalcUnitPriceFromAmount(5m, 100m, 50000m);

            // Giảm %: TIENHANG = 123456, TL = 10% -> TIENGIAMGIA = 100 * Round(123456 * 10 / 10000) = 100 * 123 = 12300.
            // Sau giảm = 111156; thuế 8% = 8892.48; phí VC = 15000 -> TONGCONG = 135048.48 (không làm tròn thêm).
            PurchaseTotals tPct = PurchaseRules.CalcTotalsFromSubtotal(123456m, 10m, 0m, false, 8m, 15000m);
            // Giảm tiền: TIENHANG = 500000, giảm tiền = 37500 -> giữ 37500, thuế 10% = 46250, phí 12345 -> TONGCONG = 521095.
            PurchaseTotals tMoney = PurchaseRules.CalcTotalsFromSubtotal(500000m, 0m, 37500m, true, 10m, 12345m);

            Check("Nhập kho §6 #2 (công thức dòng, suy ngược đơn giá và tổng phiếu): giảm %, giảm tiền, thuế, phí VC đúng chính xác không làm tròn thừa",
                lineAmt1 == 33750m && lineAmt2 == 63333m
                && revPrice == 100000m && revZeroQty == 0m && revFullDisc == 0m
                && tPct.TienGiamGia == 12300m && tPct.TienThue == 8892.48m && tPct.TongCong == 135048.48m
                && tMoney.TienGiamGia == 37500m && tMoney.TileGiamGia == 7.5m && tMoney.TienThue == 46250m && tMoney.TongCong == 521095m,
                "tPct=" + tPct.TongCong + ", tMoney=" + tMoney.TongCong);

            // §6 #3: Quy đổi 2 ĐVT (chẵn vs lẻ).
            decimal convEven = PurchaseRules.CalcConvertedQuantity(3m, "U_THUNG", "U_LON", "U_THUNG", 24m);
            decimal convOdd = PurchaseRules.CalcConvertedQuantity(3m, "U_LON", "U_LON", "U_THUNG", 24m);
            Check("Nhập kho §6 #3 (quy đổi 2 ĐVT thuần): 3 thùng x 24 = 72, 3 lon lẻ = 3",
                convEven == 72m && convOdd == 3m,
                "even=" + convEven + ", odd=" + convOdd);

            // §6 #4: Gộp dòng trùng (cùng MH + cùng đơn giá + cùng ĐVT), khác giá hoặc khác ĐVT tách dòng, SL <= 0 chặn.
            var lines = new List<PurchaseLine>();
            var prod = new PurchaseProductItem
            {
                Id = "P1", Code = "NL01", Name = "Sữa tươi", UnitId = "U_LON", UnitName = "Lon",
                BigUnitId = "U_THUNG", BigUnitName = "Thùng", QuyDoi = 24m, BuyPrice = 240000m
            };
            string e1 = PurchaseRules.AddOrMergeLine(lines, prod, "U_THUNG", "Thùng", 2m, 240000m, false);
            string e2 = PurchaseRules.AddOrMergeLine(lines, prod, "U_THUNG", "Thùng", 1m, 240000m, false); // gộp -> 3 thùng (SLNHAP=72)
            string e3 = PurchaseRules.AddOrMergeLine(lines, prod, "U_THUNG", "Thùng", 1m, 250000m, false); // khác giá -> dòng mới
            string e4 = PurchaseRules.AddOrMergeLine(lines, prod, "U_LON", "Lon", 5m, 10000m, false);       // khác ĐVT -> dòng mới
            string eZero = PurchaseRules.AddOrMergeLine(lines, prod, "U_LON", "Lon", 0m, 10000m, false);    // SL=0 -> chặn

            Check("Nhập kho §6 #4 (gộp dòng trùng & chặn SL<=0 thuần): cùng MH+giá+ĐVT gộp thành 3 thùng (SLNHAP=72), khác giá/ĐVT tách dòng, SL=0 chặn",
                e1 == null && e2 == null && e3 == null && e4 == null
                && lines.Count == 3 && lines[0].RawQuantity == 3m && lines[0].ConvertedQuantity == 72m
                && eZero == PurchaseConstants.QtyMustBePositive,
                "rows=" + lines.Count + ", sl0=" + (lines.Count > 0 ? lines[0].ConvertedQuantity.ToString() : "0"));

            // §6 #4: Kiểm tra bắt buộc Số phiếu, Kho, NCC (khi cấu hình bật), dòng trống, SL=0.
            var validLine = new PurchaseLine { ProductId = "P1", RawQuantity = 1m, UnitPrice = 10000m };
            string errNoNum = PurchaseRules.Validate(new PurchaseRow { Name = "  ", WarehouseId = "W1", Lines = new List<PurchaseLine> { validLine } }, false);
            string errNoWh = PurchaseRules.Validate(new PurchaseRow { Name = "PN01", WarehouseId = "", Lines = new List<PurchaseLine> { validLine } }, false);
            string errNoSupp = PurchaseRules.Validate(new PurchaseRow { Name = "PN01", WarehouseId = "W1", SupplierId = "", Lines = new List<PurchaseLine> { validLine } }, true);
            string errNoLine = PurchaseRules.Validate(new PurchaseRow { Name = "PN01", WarehouseId = "W1", Lines = new List<PurchaseLine>() }, false);
            string errZeroLine = PurchaseRules.Validate(new PurchaseRow
            {
                Name = "PN01", WarehouseId = "W1",
                Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = "P1", RawQuantity = 0m, UnitPrice = 10000m } }
            }, false);

            Check("Nhập kho §6 #4 (Validate thuần): chặn thiếu Số phiếu, Kho, NCC khi bật cờ, không có dòng và SL=0",
                errNoNum == PurchaseConstants.NumberRequired
                && errNoWh == PurchaseConstants.WarehouseRequired
                && errNoSupp == PurchaseConstants.SupplierRequired
                && errNoLine == PurchaseConstants.LinesRequired
                && errZeroLine == PurchaseConstants.QtyMustBePositive,
                SingleLine(errZeroLine ?? "ok"));
        }

        private static void PurchaseServiceDbTests(string db)
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
            string exportOrderId = DbHelper.NewId();
            var createdOrderIds = new List<string>();

            var cfgRepo = new ConfigRepository(db);
            int origLogFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            int origAllowNegSys = SystemConfig.GetInt("ChoPhepAmKho");
            int origReqSupp = SystemConfig.GetInt(PurchaseConstants.ConfigRequireSupplier);
            bool hasReqSuppRow = BookingCount(db, "SELECT COUNT(*) FROM SCONFIG WHERE NAME = @N", "@N", PurchaseConstants.ConfigRequireSupplier) > 0;

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 }
                }, "selftest_nk");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", storeId, "@N", "CH NhapKho " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_nk', @T)",
                    "@I", whNoNegId, "@N", "Kho KhongAm " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_nk', @T)",
                    "@I", whAllowNegId, "@N", "Kho ChoAm " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", suppId, "@N", "NCC NhapKho " + tag);
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", empId, "@N", "NV NhapKho " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", unitOddId, "@N", "Lon " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nk')",
                    "@I", unitEvenId, "@N", "Thùng " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 50000, 80000, 30, 'selftest_nk', @T)",
                    "@I", prod1Id, "@C", "NL1-" + tag, "@N", "Trà Đen " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 20000, 35000, 30, 'selftest_nk', @T)",
                    "@I", prod2Id, "@C", "NL2-" + tag, "@N", "Đường Cát " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 24, 240000, 15000, 30, 'selftest_nk', @T)",
                    "@I", prod2UnitId, "@C", "NL3-" + tag, "@N", "Sữa Lon 2DVT " + tag, "@U1", unitOddId, "@U2", unitEvenId, "@T", DbHelper.Now());

                var svc = new PurchaseService(AppSession.Current);

                // §6 #1 + #2: Lưu phiếu nhập 2 mặt hàng kèm giảm %, thuế, phí vận chuyển -> kiểm TDONHANG, TDONHANGCHITIET và tồn kho.
                string num1 = svc.GenerateNumber(DateTime.Today);
                var order1 = new PurchaseRow
                {
                    Name = num1,
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    SupplierId = suppId,
                    EmployeeId = empId,
                    Description = "Nhập mua hàng " + tag,
                    Note = "Phiếu test 1",
                    TileGiamGia = 10m,
                    GiamTheoTien = false,
                    TileThue = 8m,
                    PhiVanChuyen = 15000m,
                    PaidAmount = 100000m,
                    PaymentMethod = PayMethod.Cash,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prod1Id, ProductName = "Trà Đen " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 50000m, DiscountPct = 0m },
                        new PurchaseLine { ProductId = prod2Id, ProductName = "Đường Cát " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 5m, UnitPrice = 20000m, DiscountPct = 10m }
                    }
                };
                // Line1 = 500.000, Line2 = 90.000 => TIENHANG = 590.000; GG 10% = 59.000; sau GG = 531.000;
                // Thuế 8% = 42.480; Phí VC = 15.000 => TONGCONG = 588.480.
                decimal st1Before = svc.GetStockBalance(prod1Id, whNoNegId);
                decimal st2Before = svc.GetStockBalance(prod2Id, whNoNegId);
                PurchaseSaveResult r1 = svc.Save(order1);
                if (r1.Ok) createdOrderIds.Add(r1.SavedId);
                PurchaseRow back1 = r1.Ok ? svc.LoadById(r1.SavedId) : null;
                decimal st1After = svc.GetStockBalance(prod1Id, whNoNegId);
                decimal st2After = svc.GetStockBalance(prod2Id, whNoNegId);

                Check("Nhập kho §6 #1 (lưu phiếu nhập 2 mặt hàng): TDONHANG LOAI=1 STATUS=30 DATHANHTOAN=30, 2 dòng đúng SLNHAP/THANHTIEN/DKHOHANGID và tồn tăng đúng",
                    r1.Ok && back1 != null && back1.Loai == 1 && back1.Status == 30 && back1.PaidFlag == 30
                    && back1.Lines.Count == 2
                    && back1.Lines[0].ConvertedQuantity == 10m && back1.Lines[0].Amount == 500000m && back1.Lines[0].WarehouseId == whNoNegId
                    && back1.Lines[1].ConvertedQuantity == 5m && back1.Lines[1].Amount == 90000m && back1.Lines[1].WarehouseId == whNoNegId
                    && st1After - st1Before == 10m && st2After - st2Before == 5m,
                    SingleLine(r1.ErrorMessage ?? ("st1=" + (st1After - st1Before) + ", st2=" + (st2After - st2Before))));

                Check("Nhập kho §6 #2 (DB công thức tổng giảm %, thuế, phí VC & thanh toán): TIENHANG/TIENGIAMGIA/TIENTHUE/TONGCONG/TIENMAT khớp",
                    back1 != null && back1.TienHang == 590000m && back1.TienGiamGia == 59000m
                    && back1.TienThue == 42480m && back1.PhiVanChuyen == 15000m && back1.TongCong == 588480m
                    && back1.PaidAmount == 100000m && back1.CashAmount == 100000m && back1.TransferAmount == 0m,
                    back1 == null ? "null" : ("tong=" + back1.TongCong + ", thue=" + back1.TienThue));

                // §6 #3: ĐVT chẵn: nhập 3 thùng, quy đổi 24 => SLNHAP = 72, tồn tăng 72.
                var order2Unit = new PurchaseRow
                {
                    Name = svc.GenerateNumber(DateTime.Today),
                    Date = DateTime.Today,
                    WarehouseId = whNoNegId,
                    SupplierId = suppId,
                    Description = "Nhập 2 ĐVT " + tag,
                    GiamTheoTien = true,
                    TienGiamGia = 20000m,
                    TileThue = 10m,
                    PhiVanChuyen = 5000m,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine
                        {
                            ProductId = prod2UnitId, ProductName = "Sữa Lon 2DVT " + tag,
                            UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m,
                            RawQuantity = 3m, UnitPrice = 240000m, DiscountPct = 0m
                        }
                    }
                };
                decimal st2uBefore = svc.GetStockBalance(prod2UnitId, whNoNegId);
                PurchaseSaveResult r2u = svc.Save(order2Unit);
                if (r2u.Ok) createdOrderIds.Add(r2u.SavedId);
                PurchaseRow back2u = r2u.Ok ? svc.LoadById(r2u.SavedId) : null;
                decimal st2uAfter = svc.GetStockBalance(prod2UnitId, whNoNegId);
                // TIENHANG=720000, GG tiền=20000, thuế 10% = 70000, phí 5000 => TONGCONG=775000.
                Check("Nhập kho §6 #3 & #2 (ĐVT chẵn 3 thùng x 24 = 72 & giảm theo tiền): SLNHAP=72, SLNHAPCHUAQUYDOI=3, tồn +72, TONGCONG=775000",
                    r2u.Ok && back2u != null && back2u.Lines.Count == 1
                    && back2u.Lines[0].RawQuantity == 3m && back2u.Lines[0].ConvertedQuantity == 72m
                    && st2uAfter - st2uBefore == 72m && back2u.TongCong == 775000m,
                    SingleLine(r2u.ErrorMessage ?? ("sl=" + (st2uAfter - st2uBefore))));

                // §6 #4: Trùng Số phiếu báo "đã tồn tại", Số phiếu dài đúng 255 ký tự không làm lưu vết tràn; cấu hình bắt buộc NCC.
                PurchaseSaveResult rDup = svc.Save(new PurchaseRow
                {
                    Name = num1, Date = DateTime.Today, WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prod1Id, UnitId = unitOddId, RawQuantity = 1m, UnitPrice = 1000m } }
                });
                string num255 = "PN255-" + tag + "-" + new string('A', 255 - 7 - tag.Length);
                var order255 = new PurchaseRow
                {
                    Name = num255, Date = DateTime.Today, WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prod1Id, UnitId = unitOddId, RawQuantity = 1m, UnitPrice = 1000m } }
                };
                PurchaseSaveResult r255 = svc.Save(order255);
                if (r255.Ok) createdOrderIds.Add(r255.SavedId);
                int log255Count = r255.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", r255.SavedId) : 0;

                bool reqSuppBlocked = true;
                if (hasReqSuppRow)
                {
                    cfgRepo.Save(new[] { new ConfigChange { Name = PurchaseConstants.ConfigRequireSupplier, DataType = 3, IntValue = 30 } }, "selftest_nk");
                    SystemConfig.Reload(db);
                    PurchaseSaveResult rNoSupp = svc.Save(new PurchaseRow
                    {
                        Name = "PN-NOSUPP-" + tag, Date = DateTime.Today, WarehouseId = whNoNegId, SupplierId = null,
                        Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prod1Id, UnitId = unitOddId, RawQuantity = 1m, UnitPrice = 1000m } }
                    });
                    reqSuppBlocked = !rNoSupp.Ok && rNoSupp.ErrorMessage == PurchaseConstants.SupplierRequired;
                    cfgRepo.Save(new[] { new ConfigChange { Name = PurchaseConstants.ConfigRequireSupplier, DataType = 3, IntValue = origReqSupp } }, "selftest_nk");
                    SystemConfig.Reload(db);
                }

                Check("Nhập kho §6 #4 (trùng số phiếu, số phiếu 255 ký tự không tràn lưu vết, bắt buộc NCC): trùng báo đã tồn tại, 255 ký tự lưu + TLUUVET OK",
                    !rDup.Ok && rDup.ErrorMessage != null && rDup.ErrorMessage.IndexOf("đã tồn tại", StringComparison.OrdinalIgnoreCase) >= 0
                    && r255.Ok && num255.Length == 255 && log255Count >= 1 && reqSuppBlocked,
                    SingleLine(r255.ErrorMessage ?? ("dup=" + rDup.ErrorMessage + ", log255=" + log255Count)));

                // §6 #5: Sửa phiếu đổi số lượng -> tồn cập nhật đúng; sửa/xóa gây âm kho bị chặn ở kho CHOPHEPAMKHO=0, cho phép ở kho CHOPHEPAMKHO=30.
                // Hiện tại prod1 ở whNoNegId có tồn = 10 (order1) + 1 (order255) = 11.
                // Sửa order1: prod1 10 -> 15 => tồn thành 16.
                back1.Lines[0].RawQuantity = 15m;
                PurchaseSaveResult rEditUp = svc.Save(back1);
                decimal st1AfterUp = svc.GetStockBalance(prod1Id, whNoNegId);

                // Seed 1 phiếu xuất khác (LOAI=2, STATUS=30) xuất 12 prod1 khỏi whNoNegId => tồn còn 4.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHOXUATID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 2, 30, 30, @W, 'selftest_nk', @T)",
                    "@I", exportOrderId, "@N", "PX-ST-" + tag, "@D", DateTime.Today, "@W", whNoNegId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, THANHTIEN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @O, @P, @NM, @U, @W, 0, 0, 12, 12, 50000, 600000, 30, 'selftest_nk', @T)",
                    "@I", DbHelper.NewId(), "@O", exportOrderId, "@P", prod1Id, "@NM", "Trà Đen " + tag, "@U", unitOddId, "@W", whNoNegId, "@T", DbHelper.Now());

                // Tồn hiện có = 4. Hạ order1 prod1 từ 15 xuống 10 => tồn sau sửa = 4 - 5 = -1 -> PHẢI BỊ CHẶN (nêu tên hàng, tồn 4, sau sửa -1).
                PurchaseRow toEditNeg = svc.LoadById(back1.Id);
                toEditNeg.Lines[0].RawQuantity = 10m;
                PurchaseSaveResult rEditNegBlocked = svc.Save(toEditNeg);
                PurchaseDeleteResult rDelNegBlocked = svc.Delete(back1.Id);

                // Tạo 1 phiếu trên kho CHOPHEPAMKHO=30, xuất vượt, rồi giảm/xóa => PHẢI CHO PHÉP.
                var orderAllow = new PurchaseRow
                {
                    Name = "PN-ALLOW-" + tag, Date = DateTime.Today, WarehouseId = whAllowNegId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prod1Id, ProductName = "Trà Đen " + tag, UnitId = unitOddId, RawQuantity = 5m, UnitPrice = 50000m } }
                };
                PurchaseSaveResult rAllowSave = svc.Save(orderAllow);
                if (rAllowSave.Ok) createdOrderIds.Add(rAllowSave.SavedId);
                BookingExec(db,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, THANHTIEN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @O, @P, @NM, @U, @W, 0, 0, 4, 4, 50000, 200000, 30, 'selftest_nk', @T)",
                    "@I", DbHelper.NewId(), "@O", exportOrderId, "@P", prod1Id, "@NM", "Trà Đen " + tag, "@U", unitOddId, "@W", whAllowNegId, "@T", DbHelper.Now());
                PurchaseRow allowLoaded = svc.LoadById(orderAllow.Id);
                allowLoaded.Lines[0].RawQuantity = 2m; // tồn 1 -> -2 trên kho cho phép âm
                PurchaseSaveResult rAllowEdit = svc.Save(allowLoaded);
                decimal allowStockAfterEdit = svc.GetStockBalance(prod1Id, whAllowNegId);
                PurchaseDeleteResult rAllowDel = svc.Delete(orderAllow.Id);

                Check("Nhập kho §6 #5 (sửa số lượng & chặn/cho âm kho T3): sửa cập nhật tồn; kho không cho âm chặn sửa/xóa kèm tên hàng & số tồn; kho cho âm được phép",
                    rEditUp.Ok && st1AfterUp == 16m
                    && !rEditNegBlocked.Ok && rEditNegBlocked.ErrorMessage.IndexOf("Trà Đen " + tag, StringComparison.Ordinal) >= 0
                    && rEditNegBlocked.ErrorMessage.IndexOf("4", StringComparison.Ordinal) >= 0
                    && rEditNegBlocked.ErrorMessage.IndexOf("-1", StringComparison.Ordinal) >= 0
                    && !rDelNegBlocked.Ok
                    && rAllowEdit.Ok && allowStockAfterEdit == -2m && rAllowDel.Ok,
                    SingleLine(rEditNegBlocked.ErrorMessage ?? "ok"));

                // §6 #6: Xóa cứng phiếu + chi tiết + lưu vết; rollback bằng hook khi lỗi giữa chừng (cả Save lẫn Delete).
                int delLogBefore = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", order2Unit.Id);
                PurchaseDeleteResult rDel2u = svc.Delete(order2Unit.Id);
                int delLogAfter = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", order2Unit.Id);
                int headerLeft = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", order2Unit.Id);
                int linesLeft = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", order2Unit.Id);

                bool saveRolledBack = false;
                var rbOrder = new PurchaseRow
                {
                    Name = "PN-RB-" + tag, Date = DateTime.Today, WarehouseId = whNoNegId,
                    Lines = new List<PurchaseLine> { new PurchaseLine { ProductId = prod2Id, UnitId = unitOddId, RawQuantity = 2m, UnitPrice = 10000m } }
                };
                try
                {
                    svc.Save(rbOrder, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Save"); });
                }
                catch (InvalidOperationException)
                {
                    saveRolledBack = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE NAME = @N", "@N", "PN-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbOrder.Id);
                }

                bool deleteRolledBack = false;
                try
                {
                    svc.Delete(order255.Id, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Delete"); });
                }
                catch (InvalidOperationException)
                {
                    deleteRolledBack = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", order255.Id) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", order255.Id) == 1;
                }

                Check("Nhập kho §6 #6 (xóa cứng + TLUUVET + rollback hook): xóa mất cả đầu phiếu và chi tiết, ghi lưu vết, lỗi giữa chừng rollback trọn vẹn",
                    rDel2u.Ok && headerLeft == 0 && linesLeft == 0 && delLogAfter == delLogBefore + 1
                    && saveRolledBack && deleteRolledBack,
                    "hdr=" + headerLeft + ", ln=" + linesLeft + ", rbSave=" + saveRolledBack + ", rbDel=" + deleteRolledBack);
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origLogFlag },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = origAllowNegSys }
                }, "selftest_nk");
                if (hasReqSuppRow)
                    cfgRepo.Save(new[] { new ConfigChange { Name = PurchaseConstants.ConfigRequireSupplier, DataType = 3, IntValue = origReqSupp } }, "selftest_nk");
                SystemConfig.Reload(db);

                BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", exportOrderId);
                SafeDeleteById(db, "TDONHANG", exportOrderId);
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
    }
}
