using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void ServiceOrderTests(string db)
        {
            ServiceOrderCalculatorTests();
            ServiceOrderValidationAndDateTests();
            ServiceOrderRepoAndUiTests(db);
        }

        // ------------------------------------------------------------------ A. OrderCalculator (§3.6, §7: >= 15 ca)

        private static void ServiceOrderCalculatorTests()
        {
            // Ca 1: Dòng giảm theo % (GIAMTHEOTIEN = false).
            decimal outR1, outD1;
            decimal amt1 = OrderCalculator.CalcLineAmount(3m, 25000m, 10m, 0m, false, out outR1, out outD1);
            Check("SDDV §7 OrderCalculator [1]: dòng giảm 10% (3 x 25.000 = 75.000 -> giảm 7.500, thành tiền 67.500)",
                amt1 == 67500m && outR1 == 10m && outD1 == 7500m,
                "amt=" + amt1 + ", disc=" + outD1);

            // Ca 2: Dòng giảm theo tiền (GIAMTHEOTIEN = true) suy ra % làm tròn 2 số lẻ AwayFromZero.
            decimal outR2, outD2;
            decimal amt2 = OrderCalculator.CalcLineAmount(2m, 30000m, 0m, 10000m, true, out outR2, out outD2);
            Check("SDDV §7 OrderCalculator [2]: dòng giảm theo tiền 10.000 trên 60.000 -> thành tiền 50.000, CK 16.67%",
                amt2 == 50000m && outD2 == 10000m && outR2 == 16.67m,
                "amt=" + amt2 + ", rate=" + outR2);

            // Ca 3: Biên làm tròn AwayFromZero 500 -> 1.000 (không phải banker's rounding).
            Check("SDDV §7 OrderCalculator [3]: LamTron biên 500 -> 1.000 và 2.500 -> 3.000 (AwayFromZero)",
                OrderCalculator.LamTron(500m, 1000m) == 1000m && OrderCalculator.LamTron(2500m, 1000m) == 3000m,
                "500->" + OrderCalculator.LamTron(500m, 1000m) + ", 2500->" + OrderCalculator.LamTron(2500m, 1000m));

            // Ca 4: Làm tròn xuống dưới biên 499 -> 0, 14.499 -> 14.000.
            Check("SDDV §7 OrderCalculator [4]: LamTron 499 -> 0 và 14.499 -> 14.000",
                OrderCalculator.LamTron(499m, 1000m) == 0m && OrderCalculator.LamTron(14499m, 1000m) == 14000m);

            // Ca 5: TIENHANG và TIENHANGCHUAGIAM đều làm tròn theo LamTron; GIAMGIAMATHANG = hiệu.
            var lines5 = new List<ServiceOrderLine>
            {
                new ServiceOrderLine { Quantity = 1m, UnitPrice = 25400m, DiscountRate = 0m },
                new ServiceOrderLine { Quantity = 1m, UnitPrice = 30100m, DiscountRate = 10m } // 27090
            };
            // Σ THANHTIEN = 52490 -> 52000; Σ SL*DG = 55500 -> 56000; GIAMGIAMATHANG = 4000.
            ServiceOrderTotals t5 = OrderCalculator.CalcTotals(lines5, 0m, 0m, false, 0m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [5]: TIENHANG=52.000, TIENHANGCHUAGIAM=56.000, GIAMGIAMATHANG=4.000",
                t5.TienHang == 52000m && t5.TienHangChuaGiam == 56000m && t5.GiamGiaMatHang == 4000m && t5.TongCong == 52000m,
                "th=" + t5.TienHang + ", cg=" + t5.TienHangChuaGiam);

            // Ca 6: Giảm tổng theo % (145.000 giảm 10% = 14.500 -> làm tròn 15.000, tổng 130.000).
            ServiceOrderTotals t6 = OrderCalculator.CalcTotalsFromSubtotals(145000m, 145000m, 10m, 0m, false, 0m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [6]: giảm tổng 10% trên 145.000 -> 15.000 (biên 500), tổng 130.000",
                t6.TienGiamGia == 15000m && t6.TileGiamGia == 10m && t6.TongCong == 130000m,
                "gg=" + t6.TienGiamGia + ", tc=" + t6.TongCong);

            // Ca 7: Giảm tổng theo tiền -> % làm tròn 2 số lẻ AwayFromZero (25.000 / 200.000 = 12.5%).
            ServiceOrderTotals t7 = OrderCalculator.CalcTotalsFromSubtotals(200000m, 200000m, 0m, 25000m, true, 0m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [7]: giảm tổng theo tiền 25.000 / 200.000 -> TILE=12.5%, tổng 175.000",
                t7.TienGiamGia == 25000m && t7.TileGiamGia == 12.5m && t7.TongCong == 175000m);

            // Ca 8: Giảm tổng theo tiền khi TIENHANG = 0 không chia cho 0 -> TILE = 0.
            ServiceOrderTotals t8 = OrderCalculator.CalcTotalsFromSubtotals(0m, 0m, 10m, 10000m, true, 0m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [8]: TIENHANG = 0 giảm theo tiền -> TILE = 0, không lỗi chia 0",
                t8.TileGiamGia == 0m && t8.TienGiamGia == 10000m);

            // Ca 9: Phí dịch vụ theo % tính trên sauGiam (200.000 - 20.000 = 180.000; phí 5% = 9.000; tổng 189.000).
            ServiceOrderTotals t9 = OrderCalculator.CalcTotalsFromSubtotals(200000m, 200000m, 10m, 0m, false, 5m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [9]: phí DV 5% trên sau giảm 180.000 = 9.000, tổng 189.000",
                t9.PhiDichVu == 9000m && t9.TilePhiDichVu == 5m && t9.TongCong == 189000m);

            // Ca 10: Phí dịch vụ theo tiền suy ngược % trên sauGiam (15.000 / 180.000 = 8.33%).
            ServiceOrderTotals t10 = OrderCalculator.CalcTotalsFromSubtotals(200000m, 200000m, 10m, 0m, false, 0m, 15000m, true, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [10]: phí DV theo tiền 15.000 / 180.000 -> TILE=8.33%, tổng 195.000",
                t10.PhiDichVu == 15000m && t10.TilePhiDichVu == 8.33m && t10.TongCong == 195000m);

            // Ca 11: Thuế tính trên (sauGiam + PHIDICHVU) và làm tròn LamTron.
            // 200.000 - 20.000 = 180.000; phí 5% = 9.000 -> 189.000; thuế 10% = 18.900 -> 19.000; tổng = 208.000.
            ServiceOrderTotals t11 = OrderCalculator.CalcTotalsFromSubtotals(200000m, 200000m, 10m, 0m, false, 5m, 0m, false, 10m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [11]: thuế 10% trên (sau giảm + phí DV = 189.000) -> 19.000, tổng 208.000",
                t11.TienThue == 19000m && t11.TongCong == 208000m,
                "thue=" + t11.TienThue + ", tc=" + t11.TongCong);

            // Ca 12: Đầy đủ giảm % + phí DV % + thuế % có làm tròn biên 500.
            // 235.000 giảm 7% = 16.450 -> 16.000 -> sauGiam 219.000; phí 5% = 10.950 -> 11.000 -> 230.000;
            // thuế 8% = 18.400 -> 18.000; tổng = 248.000.
            ServiceOrderTotals t12 = OrderCalculator.CalcTotalsFromSubtotals(235000m, 235000m, 7m, 0m, false, 5m, 0m, false, 8m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [12]: 235.000 giảm 7% (16k), phí 5% (11k), thuế 8% (18k) -> tổng 248.000",
                t12.TienGiamGia == 16000m && t12.PhiDichVu == 11000m && t12.TienThue == 18000m && t12.TongCong == 248000m);

            // Ca 13: BILLKHAC cộng vào TIENPHAITRA (không đổi TONGCONG).
            ServiceOrderTotals t13 = OrderCalculator.CalcTotalsFromSubtotals(200000m, 200000m, 0m, 0m, false, 0m, 0m, false, 0m, 150000m, 1000m);
            Check("SDDV §7 OrderCalculator [13]: TIENPHAITRA = TONGCONG + BILLKHAC (200k + 150k = 350k)",
                t13.TongCong == 200000m && t13.TienPhaiTra == 350000m);

            // Ca 14: roundStep = 0 -> giữ nguyên số lẻ, không làm tròn.
            ServiceOrderTotals t14 = OrderCalculator.CalcTotalsFromSubtotals(235000m, 235000m, 7m, 0m, false, 0m, 0m, false, 8m, 0m, 0m);
            Check("SDDV §7 OrderCalculator [14]: LamTronTien = 0 không làm tròn (giảm 16.450, thuế 17.484, tổng 236.034)",
                t14.TienGiamGia == 16450m && t14.TienThue == 17484m && t14.TongCong == 236034m);

            // Ca 15: Bước làm tròn tùy chỉnh 500 (16.450 -> 16.500).
            ServiceOrderTotals t15 = OrderCalculator.CalcTotalsFromSubtotals(235000m, 235000m, 7m, 0m, false, 0m, 0m, false, 0m, 0m, 500m);
            Check("SDDV §7 OrderCalculator [15]: LamTronTien = 500 (16.450 -> 16.500, tổng 218.500)",
                t15.TienGiamGia == 16500m && t15.TongCong == 218500m);

            // Ca 16: Dòng con combo và dòng xuất vật tư không cộng vào TIENHANG.
            var lines16 = new List<ServiceOrderLine>
            {
                new ServiceOrderLine { Quantity = 2m, UnitPrice = 50000m, ComboId = "C1" },
                new ServiceOrderLine { Quantity = 2m, UnitPrice = 99999m, ComboParentId = "C1" },
                new ServiceOrderLine { Quantity = 5m, UnitPrice = 88888m, XuatVatTu = 30 }
            };
            ServiceOrderTotals t16 = OrderCalculator.CalcTotals(lines16, 0m, 0m, false, 0m, 0m, false, 0m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [16]: bỏ qua dòng con combo và dòng xuất vật tư -> TIENHANG = 100.000",
                t16.TienHang == 100000m && t16.TongCong == 100000m);

            // Ca 17: Phí dịch vụ theo tiền khi sauGiam = 0 (giảm 100%) -> TILEPHIDICHVU = 0, không chia 0.
            ServiceOrderTotals t17 = OrderCalculator.CalcTotalsFromSubtotals(100000m, 100000m, 100m, 0m, false, 0m, 10000m, true, 10m, 0m, 1000m);
            Check("SDDV §7 OrderCalculator [17]: giảm 100% (sauGiam=0) + phí 10k + thuế 10% -> TILEPHI=0, thuế 1k, tổng 11k",
                t17.TilePhiDichVu == 0m && t17.TienThue == 1000m && t17.TongCong == 11000m);

            // Ca 18: Số thập phân lớn chính xác kiểu decimal (không mất chính xác như double).
            var lines18 = new List<ServiceOrderLine>
            {
                new ServiceOrderLine { Quantity = 1.5m, UnitPrice = 33333.34m, DiscountRate = 0m } // 50000.01
            };
            ServiceOrderTotals t18 = OrderCalculator.CalcTotals(lines18, 0m, 0m, false, 0m, 0m, false, 0m, 0m, 0m);
            Check("SDDV §7 OrderCalculator [18]: độ chính xác decimal 1.5 * 33333.34 = 50000.01",
                t18.TienHang == 50000.01m && t18.TongCong == 50000.01m);
        }

        // ------------------------------------------------------------------ B. §6.5 ValidateInvoice, Số hóa đơn & Ngày giao dịch

        private static void ServiceOrderValidationAndDateTests()
        {
            // Kiểm hóa đơn (§6.5, §7): lệch dòng 10đ qua, 11đ chặn.
            var baseLine = new ServiceOrderLine { Quantity = 2m, UnitPrice = 25000m, Amount = 50010m };
            var hdrOk = new ServiceOrderHeader { TienHang = 50000m, TongCong = 50000m };
            ServiceOrderValidationResult vLine10 = OrderCalculator.ValidateInvoice(hdrOk, new[] { baseLine }, 1000m);

            var badLine = new ServiceOrderLine { Quantity = 2m, UnitPrice = 25000m, Amount = 50011m };
            ServiceOrderValidationResult vLine11 = OrderCalculator.ValidateInvoice(hdrOk, new[] { badLine }, 1000m);
            Check("SDDV §6.5/§7 Kiểm hóa đơn: lệch dòng 10đ qua, 11đ chặn",
                vLine10.IsValid && !vLine11.IsValid && vLine11.ErrorMessage == ServiceOrderConstants.InvoiceDataErrorBase,
                "v10=" + vLine10.IsValid + ", v11=" + vLine11.ErrorMessage);

            // Lệch thành phần (tiền hàng/giảm/phí/thuế) > 10đ -> chặn với " (1)".
            var cleanLine = new ServiceOrderLine { Quantity = 2m, UnitPrice = 50000m, Amount = 100000m };
            var hdrCompBad = new ServiceOrderHeader { TienHang = 100011m, TongCong = 100000m };
            ServiceOrderValidationResult vComp = OrderCalculator.ValidateInvoice(hdrCompBad, new[] { cleanLine }, 1000m);
            Check("SDDV §6.5 Kiểm hóa đơn: lệch thành phần 11đ chặn với thông báo (1)",
                !vComp.IsValid && vComp.ErrorMessage == ServiceOrderConstants.InvoiceDataErrorComponent,
                vComp.ErrorMessage);

            // Lệch tổng cộng 1.000đ qua, 1.001đ chặn với " (2)".
            var hdrTot1000 = new ServiceOrderHeader { TienHang = 100000m, TongCong = 101000m };
            var hdrTot1001 = new ServiceOrderHeader { TienHang = 100000m, TongCong = 101001m };
            ServiceOrderValidationResult vTot1000 = OrderCalculator.ValidateInvoice(hdrTot1000, new[] { cleanLine }, 1000m);
            ServiceOrderValidationResult vTot1001 = OrderCalculator.ValidateInvoice(hdrTot1001, new[] { cleanLine }, 1000m);
            Check("SDDV §6.5/§7 Kiểm hóa đơn: tổng cộng lệch 1.000đ qua, 1.001đ chặn với thông báo (2)",
                vTot1000.IsValid && !vTot1001.IsValid && vTot1001.ErrorMessage == ServiceOrderConstants.InvoiceDataErrorTotal,
                "1000=" + vTot1000.IsValid + ", 1001=" + vTot1001.ErrorMessage);

            // §6.5/§7: Số hóa đơn đúng định dạng cả 4 kiểu quay vòng.
            DateTime d = new DateTime(2026, 11, 26);
            string fDay = InvoiceNumberService.Format(InvoiceCycleMode.ByDay, d, 7);
            string fMonth = InvoiceNumberService.Format(InvoiceCycleMode.ByMonth, d, 12);
            string fYear = InvoiceNumberService.Format(InvoiceCycleMode.ByYear, d, 345);
            string fNone = InvoiceNumberService.Format(InvoiceCycleMode.NoCycle, d, 89);
            Check("SDDV §6.5/§7 Định dạng số hóa đơn đủ 4 kiểu quay vòng (ngày/tháng/năm/không quay vòng)",
                fDay == "26110007" && fMonth == "112600012" && fYear == "26000345" && fNone == "0000089",
                fDay + " / " + fMonth + " / " + fYear + " / " + fNone);

            // §6.5/§7: CachChonNgayGiaoDich = 0 và 1.
            DateTime openD = new DateTime(2026, 11, 25);
            DateTime payD = new DateTime(2026, 11, 26);
            bool warn0, warn1;
            DateTime res0 = OrderCalculator.ResolveBusinessDateForPayment(0, openD, payD, out warn0);
            DateTime res1 = OrderCalculator.ResolveBusinessDateForPayment(1, openD, payD, out warn1);
            Check("SDDV §6.5/§7 CachChonNgayGiaoDich 0 và 1: mở D trả D+1 -> NGAY là D (không cảnh báo) và D+1 (có cảnh báo)",
                res0 == openD && !warn0 && res1 == payD && warn1,
                "m0=" + res0.ToString("dd/MM") + " w0=" + warn0 + ", m1=" + res1.ToString("dd/MM") + " w1=" + warn1);

            // §3.1: Thứ tự ưu tiên trạng thái bàn (đủ 7 trạng thái).
            bool statusOrderOk =
                OrderCalculator.ResolveTableStatus(true, 1, 1, 1, true) == TableStatus.TempPrinted
                && OrderCalculator.ResolveTableStatus(true, 0, 1, 1, true) == TableStatus.InUse
                && OrderCalculator.ResolveTableStatus(false, 0, 1, 1, true) == TableStatus.NeedCleaning
                && OrderCalculator.ResolveTableStatus(false, 0, 2, 1, true) == TableStatus.Cleaning
                && OrderCalculator.ResolveTableStatus(false, 0, 0, 1, true) == TableStatus.UnderRepair
                && OrderCalculator.ResolveTableStatus(false, 0, 0, 0, true) == TableStatus.Reserved
                && OrderCalculator.ResolveTableStatus(false, 0, 0, 0, false) == TableStatus.Empty;
            Check("SDDV §3.1/§7 Thứ tự ưu tiên trạng thái bàn: Đã in tạm tính > Đang sử dụng > Báo dọn > Dọn dẹp > Sửa chữa > Đặt > Trống",
                statusOrderOk);
        }
    }
}
