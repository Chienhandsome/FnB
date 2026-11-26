using System;
using System.Collections.Generic;
using System.IO;
using Aspose.Cells;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void QuoteTests(string db)
        {
            QuoteCalculatorTests();
            QuoteItemRuleTests();
            QuoteExcelTests();
            QuoteRepoAndUiTests(db);
        }

        // ------------------------------------------------------------------ A. QuoteCalculator (>= 12 ca theo §7 & §5.0)

        private static void QuoteCalculatorTests()
        {
            // Ca 1: Thành tiền dòng = SL * DONGIA * (1 - TILE/100), không làm tròn.
            decimal lineAmt = QuoteCalculator.CalcLineAmount(2.5m, 13333m, 7m);
            Check("Báo giá §7 QuoteCalculator [1]: thành tiền dòng không làm tròn (2.5 * 13333 * 0.93 = 30999.225)",
                lineAmt == 30999.225m, "ra=" + lineAmt);

            // Ca 2 (§5.0 bắt buộc): 235000, giảm 7%, thuế 8%, phí 0 -> giảm 16000, thuế 18000, tổng 237000.
            var lines235 = new List<QuoteLine>
            {
                new QuoteLine { Quantity = 1m, Price = 235000m, DiscountPct = 0m }
            };
            QuoteTotals t235 = QuoteCalculator.CalcTotals(lines235, 7m, 0m, false, 8m, 0m, 1000m);
            Check("Báo giá §5.0 QuoteCalculator [2]: 235000 giảm 7% thuế 8% phí 0 -> giảm 16000, thuế 18000, tổng 237000",
                t235.TienHang == 235000m && t235.TienGiamGia == 16000m && t235.TienThue == 18000m && t235.TongCong == 237000m,
                "gg=" + t235.TienGiamGia + ", thue=" + t235.TienThue + ", tong=" + t235.TongCong);

            // Ca 3: Biên làm tròn AwayFromZero (14500 -> 15000, khác banker's rounding vốn ra 14000).
            QuoteTotals tHalf = QuoteCalculator.CalcTotalsFromSubtotal(145000m, 10m, 0m, false, 0m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [3]: biên làm tròn AwayFromZero 14500 -> 15000",
                tHalf.TienGiamGia == 15000m && tHalf.TongCong == 130000m, "gg=" + tHalf.TienGiamGia);

            // Ca 4: Làm tròn xuống dưới biên (14400 -> 14000).
            QuoteTotals tDown = QuoteCalculator.CalcTotalsFromSubtotal(144000m, 10m, 0m, false, 0m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [4]: làm tròn xuống 14400 -> 14000",
                tDown.TienGiamGia == 14000m && tDown.TongCong == 130000m, "gg=" + tDown.TienGiamGia);

            // Ca 5: Giảm theo tiền (GIAMTHEOTIEN = true) làm tròn số nhập trước rồi mới tính TILEGIAMGIA.
            QuoteTotals tMoney = QuoteCalculator.CalcTotalsFromSubtotal(150000m, 0m, 14500m, true, 0m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [5]: giảm theo tiền 14500 -> làm tròn 15000, TILE = 10% (dùng số đã tròn)",
                tMoney.TienGiamGia == 15000m && tMoney.TileGiamGia == 10m && tMoney.TongCong == 135000m,
                "gg=" + tMoney.TienGiamGia + ", tl=" + tMoney.TileGiamGia);

            // Ca 6: GIAMTHEOTIEN = true khi TIENHANG = 0 -> TILEGIAMGIA = 0, không chia cho 0.
            QuoteTotals tZero = QuoteCalculator.CalcTotalsFromSubtotal(0m, 15m, 10000m, true, 10m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [6]: TIENHANG = 0 ở chế độ giảm theo tiền -> TILEGIAMGIA = 0",
                tZero.TienHang == 0m && tZero.TileGiamGia == 0m && tZero.TienGiamGia == 10000m,
                "tl=" + tZero.TileGiamGia);

            // Ca 7: Chuyển qua lại giữa giảm theo tiền và giảm theo %.
            QuoteTotals tSwitch1 = QuoteCalculator.CalcTotalsFromSubtotal(200000m, 0m, 25000m, true, 0m, 0m, 1000m);
            QuoteTotals tSwitch2 = QuoteCalculator.CalcTotalsFromSubtotal(200000m, 5m, tSwitch1.TienGiamGia, false, 0m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [7]: chuyển cờ GIAMTHEOTIEN từ tiền (25000/12.5%) sang % (5% -> 10000)",
                tSwitch1.TileGiamGia == 12.5m && tSwitch2.TienGiamGia == 10000m && tSwitch2.TongCong == 190000m);

            // Ca 8: Thuế tính trên (TIENHANG - TIENGIAMGIA đã tròn) và làm tròn AwayFromZero (2500 -> 3000).
            QuoteTotals tTax = QuoteCalculator.CalcTotalsFromSubtotal(100000m, 0m, 50000m, true, 5m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [8]: thuế 5% trên 50000 = 2500 -> làm tròn 3000, tổng 53000",
                tTax.TienThue == 3000m && tTax.TongCong == 53000m, "thue=" + tTax.TienThue);

            // Ca 9: Phí vận chuyển cộng vào trước bước làm tròn TONGCONG.
            QuoteTotals tShip = QuoteCalculator.CalcTotalsFromSubtotal(235000m, 7m, 0m, false, 8m, 12500m, 1000m);
            Check("Báo giá §7 QuoteCalculator [9]: có phí vận chuyển 12500 -> tổng 237000 + 12500 = 249500 -> 250000",
                tShip.TongCong == 250000m, "tong=" + tShip.TongCong);

            // Ca 10: TIENHANG tổng nhiều dòng có số lẻ không làm tròn TIENHANG.
            var multiLines = new List<QuoteLine>
            {
                new QuoteLine { Quantity = 1m, Price = 10000m, DiscountPct = 3m }, // 9700
                new QuoteLine { Quantity = 3m, Price = 3333.5m, DiscountPct = 0m } // 10000.5
            };
            QuoteTotals tMulti = QuoteCalculator.CalcTotals(multiLines, 0m, 0m, false, 0m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [10]: TIENHANG nhiều dòng giữ nguyên phần lẻ (19700.5), TONGCONG tròn 20000",
                tMulti.TienHang == 19700.5m && tMulti.TongCong == 20000m, "th=" + tMulti.TienHang + ", tc=" + tMulti.TongCong);

            // Ca 11: roundStep = 0 -> không làm tròn.
            QuoteTotals tNoRound = QuoteCalculator.CalcTotalsFromSubtotal(235000m, 7m, 0m, false, 8m, 0m, 0m);
            Check("Báo giá §7 QuoteCalculator [11]: roundStep = 0 giữ nguyên số chưa làm tròn",
                tNoRound.TienGiamGia == 16450m && tNoRound.TienThue == 17484m && tNoRound.TongCong == 236034m);

            // Ca 12: Bước làm tròn tùy chỉnh 500.
            QuoteTotals tStep500 = QuoteCalculator.CalcTotalsFromSubtotal(235000m, 7m, 0m, false, 0m, 0m, 500m);
            Check("Báo giá §7 QuoteCalculator [12]: bước làm tròn 500 (16450 -> 16500, tổng 218500)",
                tStep500.TienGiamGia == 16500m && tStep500.TongCong == 218500m);

            // Ca 13: Danh sách dòng rỗng nhưng có phí vận chuyển.
            QuoteTotals tEmpty = QuoteCalculator.CalcTotals(new List<QuoteLine>(), 10m, 0m, false, 8m, 15400m, 1000m);
            Check("Báo giá §7 QuoteCalculator [13]: không có dòng hàng, phí vận chuyển 15400 -> tổng 15000",
                tEmpty.TienHang == 0m && tEmpty.TienGiamGia == 0m && tEmpty.TienThue == 0m && tEmpty.TongCong == 15000m);

            // Ca 14: Chiết khấu dòng 100% -> TIENHANG = 0.
            var freeLines = new List<QuoteLine> { new QuoteLine { Quantity = 5m, Price = 50000m, DiscountPct = 100m } };
            QuoteTotals tFree = QuoteCalculator.CalcTotals(freeLines, 10m, 0m, false, 10m, 0m, 1000m);
            Check("Báo giá §7 QuoteCalculator [14]: giảm dòng 100% -> TIENHANG = 0, TONGCONG = 0",
                tFree.TienHang == 0m && tFree.TongCong == 0m);
        }

        // ------------------------------------------------------------------ B. QuoteItemRules (§5.0.2 & P2/P3)

        private static void QuoteItemRuleTests()
        {
            var item1 = new QuoteSaleItem
            {
                Id = "P1", Code = "MH01", Name = "Nước suối",
                UnitId = "U1", UnitName = "Chai", SellPrice = 15000m
            };
            var item2 = new QuoteSaleItem
            {
                Id = "P2", Code = "MH02", Name = "Bia lon",
                UnitId = "U1", UnitName = "Lon", SellPrice = 15000m,
                BigUnitId = "U2", BigUnitName = "Thùng", BigSellPrice = 340000m, QuyDoi = 24m
            };

            // 1 ĐVT: SL <= 0 bị chặn, báo "Mời bạn nhập số lượng".
            var lines = new List<QuoteLine>();
            QuoteAddItemOutcome r0 = QuoteItemRules.AddSingleUnitItem(lines, item1, 0m);
            QuoteAddItemOutcome rNeg = QuoteItemRules.AddSingleUnitItem(lines, item1, -3m);
            Check("Báo giá §7/P3 thêm 1 ĐVT SL <= 0: báo 'Mời bạn nhập số lượng', không thêm dòng",
                !r0.Changed && r0.WarningMessage == QuoteText.EnterQuantity
                && !rNeg.Changed && rNeg.WarningMessage == QuoteText.EnterQuantity
                && lines.Count == 0);

            // 1 ĐVT: dòng lẻ LUÔN gộp theo mặt hàng + ĐVT, cộng SL, giữ nguyên đơn giá dòng cũ.
            QuoteItemRules.AddSingleUnitItem(lines, item1, 2m);
            lines[0].Price = 18000m; // người dùng sửa đơn giá trên lưới
            QuoteAddItemOutcome rMerge = QuoteItemRules.AddSingleUnitItem(lines, item1, 3m);
            Check("Báo giá §5.0 P2 dòng lẻ luôn gộp + giữ đơn giá dòng cũ (2+3=5, giá vẫn 18000)",
                rMerge.Changed && lines.Count == 1 && lines[0].Quantity == 5m && lines[0].Price == 18000m && lines[0].Amount == 90000m);

            // 2 ĐVT — Bảng §5.0.2 Ca 1: chưa có dòng lẻ | Ô SL=2 | Lẻ trong hộp=5 -> tạo dòng 2 (không phải 5).
            var two1 = new List<QuoteLine>();
            QuoteAddItemOutcome c1 = QuoteItemRules.AddTwoUnitItem(two1, item2, 2m, 5m, 0m, true, false);
            Check("Báo giá §5.0.2 [ca 1/4]: chưa có dòng lẻ, ô SL=2, lẻ trong hộp=5 -> tạo dòng SL=2",
                c1.Changed && c1.WarningMessage == null && two1.Count == 1 && two1[0].UnitId == "U1" && two1[0].Quantity == 2m && two1[0].Price == 15000m);

            // 2 ĐVT — Bảng §5.0.2 Ca 2: đã có dòng lẻ (2) | Ô SL=0 | Lẻ trong hộp=3 -> không đổi + báo cảnh báo P3.
            QuoteAddItemOutcome c2 = QuoteItemRules.AddTwoUnitItem(two1, item2, 0m, 3m, 0m, true, false);
            Check("Báo giá §5.0.2 [ca 2/4]: đã có dòng lẻ (2), ô SL=0, lẻ trong hộp=3 -> dòng lẻ không đổi, cảnh báo SL",
                !c2.Changed && c2.WarningMessage == QuoteText.EnterQuantity && two1.Count == 1 && two1[0].Quantity == 2m);

            // 2 ĐVT — Bảng §5.0.2 Ca 3: đã có dòng lẻ (2) | Ô SL=2 | Lẻ trong hộp=0 | Chẵn=1 -> cộng thêm 2 (thành 4) + dòng chẵn 1.
            QuoteAddItemOutcome c3 = QuoteItemRules.AddTwoUnitItem(two1, item2, 2m, 0m, 1m, true, false);
            Check("Báo giá §5.0.2 [ca 3/4]: đã có dòng lẻ (2), ô SL=2, lẻ trong hộp=0 -> cộng thêm 2 thành 4 + dòng chẵn 1 giá 340000",
                c3.Changed && c3.WarningMessage == null && two1.Count == 2
                && two1[0].UnitId == "U1" && two1[0].Quantity == 4m
                && two1[1].UnitId == "U2" && two1[1].Quantity == 1m && two1[1].Price == 340000m && two1[1].DiscountPct == 0m);

            // 2 ĐVT — Bảng §5.0.2 Ca 4: chưa có dòng lẻ | Ô SL=0 | Lẻ trong hộp=3 | Chẵn=2 -> KHÔNG tạo dòng lẻ (P3), cảnh báo, VẪN tạo dòng chẵn 2.
            var two4 = new List<QuoteLine>();
            QuoteAddItemOutcome c4 = QuoteItemRules.AddTwoUnitItem(two4, item2, 0m, 3m, 2m, true, false);
            Check("Báo giá §5.0.2 [ca 4/4]: chưa có dòng lẻ, ô SL=0, lẻ hộp=3, chẵn hộp=2 -> không tạo dòng lẻ (P3), vẫn tạo dòng chẵn 2",
                c4.Changed && c4.WarningMessage == QuoteText.EnterQuantity && two4.Count == 1
                && two4[0].UnitId == "U2" && two4[0].Quantity == 2m && two4[0].Price == 340000m);

            // 2 ĐVT — Ca lẻ và chẵn trong hộp đều = 0 (r4, 4 tình huống a/b/c/d):
            // (a) có dòng lẻ 2 (giữ đơn giá đã sửa 18000), ô SL 2 -> thành 4, thành tiền 72000, số dòng không đổi, không cảnh báo.
            var zA = new List<QuoteLine>
            {
                new QuoteLine { ProductId = item2.Id, UnitId = item2.UnitId, Quantity = 2m, Price = 18000m, DiscountPct = 0m, Amount = 36000m }
            };
            QuoteAddItemOutcome rZA = QuoteItemRules.AddTwoUnitItem(zA, item2, 2m, 0m, 0m, true, false);
            bool zAOk = rZA.Changed && rZA.WarningMessage == null && zA.Count == 1
                && zA[0].Quantity == 4m && zA[0].Price == 18000m && zA[0].Amount == 72000m;
            // (b) có dòng lẻ 2, ô SL 0 -> không đổi, có cảnh báo.
            var zB = new List<QuoteLine>
            {
                new QuoteLine { ProductId = item2.Id, UnitId = item2.UnitId, Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m }
            };
            QuoteAddItemOutcome rZB = QuoteItemRules.AddTwoUnitItem(zB, item2, 0m, 0m, 0m, true, false);
            bool zBOk = !rZB.Changed && rZB.WarningMessage == QuoteText.EnterQuantity
                && zB.Count == 1 && zB[0].Quantity == 2m && zB[0].Amount == 30000m;
            // (c) chưa có dòng nào, ô SL 2 -> không tạo dòng, có cảnh báo.
            var zC = new List<QuoteLine>();
            QuoteAddItemOutcome rZC = QuoteItemRules.AddTwoUnitItem(zC, item2, 2m, 0m, 0m, true, false);
            bool zCOk = !rZC.Changed && rZC.WarningMessage == QuoteText.EnterQuantity && zC.Count == 0;
            // (d) chỉ có dòng chẵn, ô SL 2 -> không đổi dòng chẵn, không tạo dòng lẻ, có cảnh báo.
            var zD = new List<QuoteLine>
            {
                new QuoteLine { ProductId = item2.Id, UnitId = item2.BigUnitId, Quantity = 1m, Price = 340000m, DiscountPct = 0m, Amount = 340000m }
            };
            QuoteAddItemOutcome rZD = QuoteItemRules.AddTwoUnitItem(zD, item2, 2m, 0m, 0m, true, false);
            bool zDOk = !rZD.Changed && rZD.WarningMessage == QuoteText.EnterQuantity
                && zD.Count == 1 && zD[0].UnitId == item2.BigUnitId && zD[0].Quantity == 1m;
            Check("Báo giá §5.0.2 [lẻ và chẵn đều 0]: (a) có lẻ+SL=2 thành 4 giữ giá; (b) có lẻ+SL=0 cảnh báo; (c) rỗng cảnh báo; (d) chỉ chẵn cảnh báo",
                zAOk && zBOk && zCOk && zDOk,
                "a=" + zAOk + ", b=" + zBOk + ", c=" + zCOk + ", d=" + zDOk);

            // 2 ĐVT — Ca bấm Thoát (dialogConfirmed = false) -> không thêm gì, không cảnh báo.
            QuoteAddItemOutcome cCancel = QuoteItemRules.AddTwoUnitItem(two1, item2, 2m, 5m, 3m, false, false);
            Check("Báo giá §5.0.2 [bấm Thoát]: không thêm/đổi gì, không cảnh báo",
                !cCancel.Changed && cCancel.WarningMessage == null
                && two1.Count == 2 && two1[0].Quantity == 4m && two1[1].Quantity == 1m);

            // 2 ĐVT — Dòng chẵn gộp theo NhapMotMatHangNhieuLanTrongPhieu (false = gộp, true = thêm dòng mới).
            QuoteAddItemOutcome cEvenMerge = QuoteItemRules.AddTwoUnitItem(two1, item2, 0m, 0m, 2m, true, false);
            QuoteAddItemOutcome cEvenSplit = QuoteItemRules.AddTwoUnitItem(two1, item2, 0m, 0m, 1m, true, true);
            Check("Báo giá §5.0 P2 dòng chẵn: cấu hình != 30 thì gộp (1+2=3), cấu hình = 30 thì thêm dòng mới",
                cEvenMerge.Changed && cEvenSplit.Changed && two1.Count == 3
                && two1[1].UnitId == "U2" && two1[1].Quantity == 3m
                && two1[2].UnitId == "U2" && two1[2].Quantity == 1m);

            // Kiểm tra sửa số lượng <= 0 trên lưới chi tiết (trả về giá trị cũ).
            decimal eff0, effNeg, effOk;
            string w0, wNeg, wOk;
            bool ok0 = QuoteItemRules.ValidateEditedQuantity(0m, 4m, out eff0, out w0);
            bool okNeg = QuoteItemRules.ValidateEditedQuantity(-2m, 4m, out effNeg, out wNeg);
            bool okPos = QuoteItemRules.ValidateEditedQuantity(6m, 4m, out effOk, out wOk);
            Check("Báo giá §5.0 P3 sửa trực tiếp lưới chi tiết: SL <= 0 bị chặn và trả về giá trị cũ",
                !ok0 && eff0 == 4m && w0 == QuoteText.EnterQuantity
                && !okNeg && effNeg == 4m && wNeg == QuoteText.EnterQuantity
                && okPos && effOk == 6m && wOk == null);
        }

        // ------------------------------------------------------------------ F. QuoteExcelImporter (thuần + file .xlsx thật)

        private static void QuoteExcelTests()
        {
            var items = new List<QuoteSaleItem>
            {
                new QuoteSaleItem
                {
                    Id = "I1", Code = "CF01", Name = "Cà phê sữa",
                    UnitId = "U_LY", UnitName = "Ly", SellPrice = 25000m,
                    BigUnitId = "U_THUNG", BigUnitName = "Thùng", BigSellPrice = 240000m
                },
                // Trùng khóa lấy dòng đầu (Id = I1, không phải I1_DUP).
                new QuoteSaleItem
                {
                    Id = "I1_DUP", Code = "cf01", Name = "cà phê sữa",
                    UnitId = "U_LY", UnitName = "Ly", SellPrice = 99000m
                },
                new QuoteSaleItem
                {
                    Id = "I2", Code = "TR01", Name = "Trà đào",
                    UnitId = "U_LY", UnitName = "Ly", SellPrice = 30000m
                }
            };
            var units = new List<LookupItem>
            {
                new LookupItem { Id = "U_LY", Name = "Ly" },
                new LookupItem { Id = "U_THUNG", Name = "Thùng" },
                new LookupItem { Id = "U_HOP", Name = "Hộp" }
            };

            // Thiếu cột bắt buộc.
            Check("Báo giá §2.3 Excel thiếu cột Mã hàng (SuDungMaHang=30)",
                QuoteExcelImporter.ValidateMapping(new QuoteExcelColumnMap { ItemCol = -1, QuantityCol = 1 }, true) == QuoteText.SelectColumnCode);
            Check("Báo giá §2.3 Excel thiếu cột Tên hàng (SuDungMaHang=0)",
                QuoteExcelImporter.ValidateMapping(new QuoteExcelColumnMap { ItemCol = -1, QuantityCol = 1 }, false) == QuoteText.SelectColumnName);
            Check("Báo giá §2.3 Excel thiếu cột Số lượng",
                QuoteExcelImporter.ValidateMapping(new QuoteExcelColumnMap { ItemCol = 0, QuantityCol = -1 }, false) == QuoteText.SelectColumnQuantity);

            // Đủ 3 lỗi gốc gom lại một lần, không nhập dòng nào (cả chế độ khớp theo tên vẫn dùng "Mã hàng 'x'...").
            var badRows = new List<string[]>
            {
                new[] { "Cà phê sữa", "2", "Ly", "20000", "0" },       // dòng hợp lệ nhưng cả đợt phải bị hủy
                new[] { "Không Tồn Tại", "1", "", "10000", "0" },      // lỗi 1: không thấy mặt hàng
                new[] { "Cà phê sữa", "1", "ChaiLa", "10000", "0" },   // lỗi 2: ĐVT không tồn tại
                new[] { "Trà đào", "1", "Hộp", "10000", "0" }          // lỗi 3: ĐVT tồn tại nhưng không thuộc mặt hàng
            };
            var mapAll = new QuoteExcelColumnMap { ItemCol = 0, QuantityCol = 1, UnitCol = 2, PriceCol = 3, DiscountPctCol = 4 };
            QuoteExcelImportResult badRes = QuoteExcelImporter.Import(badRows, mapAll, false, true, items, units);
            Check("Báo giá §7 Excel gom đủ 3 câu lỗi gốc và không nhập dòng nào",
                !badRes.Ok && badRes.Lines.Count == 0 && badRes.Errors.Count == 3
                && badRes.Errors[0] == "Mã hàng 'Không Tồn Tại' không tồn tại trong hệ thống"
                && badRes.Errors[1] == "Đơn vị tính 'ChaiLa' ở mặt hàng 'Cà phê sữa' không tồn tại trong hệ thống"
                && badRes.Errors[2] == "Mặt hàng 'Trà đào' không có đơn vị tính 'Hộp'",
                string.Join(" | ", badRes.Errors.ToArray()));

            // Tự gán cột Excel: không gán nhầm "Giảm giá" làm "Đơn giá" (r2 #6).
            QuoteExcelColumnMap det1 = QuoteExcelImporter.AutoDetectColumns(
                new[] { "Tên hàng", "Giảm giá", "Số lượng", "Đơn giá" }, false, false);
            QuoteExcelColumnMap det2 = QuoteExcelImporter.AutoDetectColumns(
                new[] { "Tên hàng", "Giảm giá", "Số lượng" }, false, false);
            Check("Báo giá §2.3 Excel tự gán cột: Tên hàng, Giảm giá, Số lượng, Đơn giá không gán nhầm Giảm giá làm Đơn giá",
                det1.ItemCol == 0 && det1.DiscountPctCol == 1 && det1.QuantityCol == 2 && det1.PriceCol == 3
                && det2.ItemCol == 0 && det2.DiscountPctCol == 1 && det2.QuantityCol == 2 && det2.PriceCol == -1,
                "det1=" + det1.ItemCol + "/" + det1.DiscountPctCol + "/" + det1.QuantityCol + "/" + det1.PriceCol + ", det2P=" + det2.PriceCol);

            // Luật phân tích số Excel (r2 #2): ô chữ nhóm nghìn cho Đơn giá, mơ hồ cho SL/% giảm, ô số invariant không mơ hồ.
            decimal p1, p2, p3, p4, p5, p6, p7, pBad;
            bool parsePriceOk =
                QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell(" 340.000 "), true, out p1) && p1 == 340000m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("340,000"), true, out p2) && p2 == 340000m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.500.000"), true, out p3) && p3 == 1500000m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("340,000.50"), true, out p4) && p4 == 340000.50m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("340.000,50"), true, out p5) && p5 == 340000.50m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("2,5"), false, out p6) && p6 == 2.5m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("2.5"), false, out p7) && p7 == 2.5m
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("34,00.50"), true, out pBad)
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("abc"), true, out pBad);
            decimal qNum;
            bool ambigAndNumericOk =
                !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.250"), false, out pBad)
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1,250"), false, out pBad)
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.NumberCell(1.125m), false, out qNum) && qNum == 1.125m;
            Check("Báo giá §2.3 Excel ParseNumber: nhóm nghìn Đơn giá, mơ hồ SL/% giảm, ô số 1.125 không mơ hồ",
                parsePriceOk && ambigAndNumericOk);

            // Lỗi chuỗi không đọc được / mơ hồ / giá âm / % giảm ngoài 0..100 hủy toàn bộ nhập (r2 #2 & #3).
            var invalidNumRows = new List<string[]>
            {
                new[] { "CF01", "1.250", "", "10000", "0" },      // SL mơ hồ
                new[] { "CF01", "2", "", "abc", "1.250" },        // Đơn giá lỗi chữ + % giảm mơ hồ
                new[] { "CF01", "2", "", "-5000", "105" },        // Đơn giá âm + % giảm > 100
                new[] { "TR01", "1", "", "10000", "-5" }          // % giảm < 0
            };
            QuoteExcelImportResult invRes = QuoteExcelImporter.Import(invalidNumRows, mapAll, true, true, items, units);
            Check("Báo giá §2.3 Excel không ép giá trị âm thầm: báo lỗi số mơ hồ/chữ/giá âm/% giảm ngoài 0..100 và hủy nhập",
                !invRes.Ok && invRes.Lines.Count == 0 && invRes.Errors.Count == 6
                && invRes.Errors[0] == "Giá trị '1.250' ở cột 'Số lượng' của mặt hàng 'CF01' không đọc được thành số"
                && invRes.Errors[1] == "Giá trị 'abc' ở cột 'Đơn giá' của mặt hàng 'CF01' không đọc được thành số"
                && invRes.Errors[2] == "Giá trị '1.250' ở cột '% giảm' của mặt hàng 'CF01' không đọc được thành số"
                && invRes.Errors[3] == "Giá trị '-5000' ở cột 'Đơn giá' của mặt hàng 'CF01' không hợp lệ"
                && invRes.Errors[4] == "Giá trị '105' ở cột '% giảm' của mặt hàng 'CF01' không hợp lệ"
                && invRes.Errors[5] == "Giá trị '-5' ở cột '% giảm' của mặt hàng 'TR01' không hợp lệ",
                string.Join(" | ", invRes.Errors.ToArray()));

            // Đọc file .xlsx thật tạo bằng Aspose.Cells (FileFormatType.Excel2007Xlsx) trong thư mục tạm:
            // - ô chữ "340.000" ở Đơn giá -> 340000; ô số 1.125 ở Số lượng -> 1.125
            // - khớp mã không phân biệt hoa thường + trùng khóa lấy dòng đầu (I1)
            // - ĐVT trống -> đơn vị lẻ; ĐVT chẵn -> đơn vị chẵn; đơn giá trống -> 0; bỏ SL=0 và SL âm
            string tempFile = Path.Combine(Path.GetTempPath(), "kazuko-quote-selftest-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                var wb = new Workbook();
                Cells cells = wb.Worksheets[0].Cells;
                cells[0, 0].PutValue("Mã hàng");
                cells[0, 1].PutValue("Số lượng");
                cells[0, 2].PutValue("ĐVT");
                cells[0, 3].PutValue("Đơn giá");
                cells[0, 4].PutValue("% giảm");

                // Dòng 1: ô số 1.125 ở SL, ĐVT trống -> lẻ, đơn giá trống -> 0
                cells[1, 0].PutValue("cf01");
                cells[1, 1].PutValue(1.125);
                cells[1, 2].PutValue("");
                cells[1, 3].PutValue("");
                cells[1, 4].PutValue(5);
                // Dòng 2: cùng mã CF01, ĐVT chẵn (Thùng), ô CHỮ "340.000" ở Đơn giá -> 340000
                cells[2, 0].PutValue("CF01");
                cells[2, 1].PutValue(1);
                cells[2, 2].PutValue("thùng");
                cells[2, 3].PutValue("340.000");
                cells[2, 4].PutValue(0);
                // Dòng 3: thiếu mã -> bỏ qua
                cells[3, 0].PutValue("");
                cells[3, 1].PutValue(5);
                // Dòng 4: SL = 0 -> bỏ qua
                cells[4, 0].PutValue("TR01");
                cells[4, 1].PutValue(0);
                // Dòng 5: SL âm -> bỏ qua và báo số dòng bị bỏ
                cells[5, 0].PutValue("TR01");
                cells[5, 1].PutValue(-4);
                cells[5, 3].PutValue(30000);

                wb.Save(tempFile, FileFormatType.Excel2007Xlsx);

                QuoteExcelSheetData sheet = QuoteExcelReader.ReadFirstSheet(tempFile);
                QuoteExcelImportResult realRes = QuoteExcelImporter.Import(sheet.Rows, mapAll, true, true, items, units);
                Check("Báo giá §7 Excel đọc file .xlsx thật (Aspose.Cells): ô chữ '340.000'->340000, ô số 1.125->1.125, giá trống=0, bỏ SL=0 và SL âm",
                    realRes.Ok && realRes.Lines.Count == 2
                    && realRes.SkippedNegativeCount == 1 && realRes.SkippedEmptyOrZeroCount == 2
                    && realRes.Lines[0].ProductId == "I1" && realRes.Lines[0].UnitId == "U_LY"
                    && realRes.Lines[0].Quantity == 1.125m && realRes.Lines[0].Price == 0m && realRes.Lines[0].DiscountPct == 5m
                    && realRes.Lines[1].ProductId == "I1" && realRes.Lines[1].UnitId == "U_THUNG"
                    && realRes.Lines[1].Quantity == 1m && realRes.Lines[1].Price == 340000m,
                    "ok=" + realRes.Ok + ", lines=" + realRes.Lines.Count + ", err=" + string.Join(";", realRes.Errors.ToArray()));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
