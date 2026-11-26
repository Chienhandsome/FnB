using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Bộ tính tiền thuần decimal cho hóa đơn nhà hàng (docs/specs/su-dung-dich-vu.md §3.6, §6.5, §3.1).
    /// Một bản duy nhất (P2), bỏ nhánh tiền giờ (TIENGIO = 0) và bỏ nhánh USD 2 số lẻ (Q1).
    /// Làm tròn bằng MoneyRounding.RoundToStep (AwayFromZero theo bội LamTronTien, bỏ qua khi roundStep &lt;= 0).
    /// </summary>
    public static class OrderCalculator
    {
        private const decimal LineAndComponentTolerance = 10m;
        private const decimal TotalTolerance = 1000m;

        /// <summary>Làm tròn theo bội roundStep (LamTronTien), kiểu AwayFromZero. roundStep &lt;= 0 thì giữ nguyên.</summary>
        public static decimal LamTron(decimal amount, decimal roundStep)
        {
            return roundStep > 0m ? MoneyRounding.RoundToStep(amount, roundStep) : amount;
        }

        /// <summary>
        /// Tính thành tiền một dòng (spec §3.6):
        /// - GIAMTHEOTIEN = true: THANHTIEN = DONGIA * SL - TIENGIAMGIA; TILE = Round(TIENGIAMGIA * 100 / (DONGIA * SL), 2, AwayFromZero).
        /// - GIAMTHEOTIEN = false: THANHTIEN = DONGIA * SL * (1 - TILE / 100); TIENGIAMGIA = DONGIA * SL - THANHTIEN.
        /// </summary>
        public static decimal CalcLineAmount(
            decimal quantity,
            decimal unitPrice,
            decimal discountRate,
            decimal discountAmount,
            bool discountByMoney,
            out decimal outDiscountRate,
            out decimal outDiscountAmount)
        {
            decimal gross = unitPrice * quantity;
            if (discountByMoney)
            {
                outDiscountAmount = discountAmount;
                outDiscountRate = gross == 0m
                    ? 0m
                    : Math.Round(discountAmount * 100m / gross, 2, MidpointRounding.AwayFromZero);
                return gross - discountAmount;
            }

            outDiscountRate = discountRate;
            decimal amount = gross * (1m - discountRate / 100m);
            outDiscountAmount = gross - amount;
            return amount;
        }

        /// <summary>Cập nhật DiscountRate, DiscountAmount và Amount trực tiếp trên một dòng chi tiết.</summary>
        public static decimal RecalcLine(ServiceOrderLine line)
        {
            if (line == null)
                return 0m;
            if (line.IsComboChild || line.IsMaterialExport)
            {
                line.UnitPrice = 0m;
                line.DiscountRate = 0m;
                line.DiscountAmount = 0m;
                line.Amount = 0m;
                return 0m;
            }
            decimal outRate;
            decimal outAmt;
            line.Amount = CalcLineAmount(
                line.Quantity,
                line.UnitPrice,
                line.DiscountRate,
                line.DiscountAmount,
                line.DiscountByMoney,
                out outRate,
                out outAmt);
            line.DiscountRate = outRate;
            line.DiscountAmount = outAmt;
            return line.Amount;
        }

        /// <summary>
        /// Tính tổng hóa đơn nhà hàng từ danh sách dòng (spec §3.6, v1 bỏ nhánh tiền giờ nên TIENGIO = 0):
        /// TIENHANG = LamTron(Σ THANHTIEN); TIENHANGCHUAGIAM = LamTron(Σ SL * DONGIA); GIAMGIAMATHANG = TIENHANGCHUAGIAM - TIENHANG.
        /// Giảm giá tổng: theo tiền -> TILE = Round(tien * 100 / TIENHANG, 2, AwayFromZero); theo % -> TIEN = LamTron(% * TIENHANG / 100).
        /// sauGiam = TIENHANG - TIENGIAMGIA.
        /// Phí dịch vụ: theo tiền -> TILE = Round(phi * 100 / sauGiam, 2, AwayFromZero); theo % -> PHI = LamTron(% * sauGiam / 100).
        /// TIENTHUE = LamTron((sauGiam + PHIDICHVU) * TILETHUE / 100).
        /// TONGCONG = LamTron(sauGiam + PHIDICHVU + TIENTHUE); TIENPHAITRA = TONGCONG + BILLKHAC.
        /// </summary>
        public static ServiceOrderTotals CalcTotals(
            IEnumerable<ServiceOrderLine> lines,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tilePhiDichVu,
            decimal phiDichVu,
            bool phiDichVuTheoTien,
            decimal tileThue,
            decimal billKhac,
            decimal roundStep)
        {
            decimal sumAmount = 0m;
            decimal sumGross = 0m;
            if (lines != null)
            {
                foreach (ServiceOrderLine line in lines)
                {
                    if (line == null || line.IsComboChild || line.IsMaterialExport)
                        continue;
                    decimal outRate;
                    decimal outDisc;
                    decimal amt = CalcLineAmount(
                        line.Quantity,
                        line.UnitPrice,
                        line.DiscountRate,
                        line.DiscountAmount,
                        line.DiscountByMoney,
                        out outRate,
                        out outDisc);
                    sumAmount += amt;
                    sumGross += line.Quantity * line.UnitPrice;
                }
            }

            decimal tienHang = LamTron(sumAmount, roundStep);
            decimal tienHangChuaGiam = LamTron(sumGross, roundStep);
            return CalcTotalsFromSubtotals(
                tienHang,
                tienHangChuaGiam,
                tileGiamGia,
                tienGiamGia,
                giamTheoTien,
                tilePhiDichVu,
                phiDichVu,
                phiDichVuTheoTien,
                tileThue,
                billKhac,
                roundStep);
        }

        /// <summary>Tính tổng hóa đơn từ TIENHANG và TIENHANGCHUAGIAM đã làm tròn.</summary>
        public static ServiceOrderTotals CalcTotalsFromSubtotals(
            decimal tienHang,
            decimal tienHangChuaGiam,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tilePhiDichVu,
            decimal phiDichVu,
            bool phiDichVuTheoTien,
            decimal tileThue,
            decimal billKhac,
            decimal roundStep)
        {
            decimal giamGiaMatHang = tienHangChuaGiam - tienHang;

            decimal outTileGiam;
            decimal outTienGiam;
            if (giamTheoTien)
            {
                outTienGiam = tienGiamGia;
                outTileGiam = tienHang == 0m
                    ? 0m
                    : Math.Round(outTienGiam * 100m / tienHang, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                outTileGiam = tileGiamGia;
                outTienGiam = LamTron(tileGiamGia * tienHang / 100m, roundStep);
            }

            decimal sauGiam = tienHang - outTienGiam;

            decimal outTilePhi;
            decimal outPhi;
            if (phiDichVuTheoTien)
            {
                outPhi = phiDichVu;
                outTilePhi = sauGiam == 0m
                    ? 0m
                    : Math.Round(outPhi * 100m / sauGiam, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                outTilePhi = tilePhiDichVu;
                outPhi = LamTron(tilePhiDichVu * sauGiam / 100m, roundStep);
            }

            decimal tienThue = LamTron((sauGiam + outPhi) * tileThue / 100m, roundStep);
            decimal tongCong = LamTron(sauGiam + outPhi + tienThue, roundStep);

            return new ServiceOrderTotals
            {
                TienHang = tienHang,
                TienHangChuaGiam = tienHangChuaGiam,
                GiamGiaMatHang = giamGiaMatHang,
                TileGiamGia = outTileGiam,
                TienGiamGia = outTienGiam,
                TilePhiDichVu = outTilePhi,
                PhiDichVu = outPhi,
                TienThue = tienThue,
                TongCong = tongCong,
                TienPhaiTra = tongCong + billKhac
            };
        }

        /// <summary>
        /// Kiểm tra tính nhất quán hóa đơn (DuLieuHoaDonOk, spec §6.5, bỏ tiền giờ = 0):
        /// - Mỗi dòng: tính lại từ SL, DONGIA và giảm giá (theo tiền hoặc %), làm tròn tới đồng AwayFromZero,
        ///   lệch tối đa 10đ so với THANHTIEN.
        /// - Tiền hàng = LamTron(tổng các dòng tính lại). Tính lại giảm giá, phí dịch vụ, thuế theo LamTron;
        ///   tiền hàng, giảm giá, phí, thuế lệch tối đa 10đ -> thông báo thêm " (1)".
        /// - Tổng cộng sau LamTron lệch tối đa 1.000đ -> thông báo thêm " (2)".
        /// </summary>
        public static ServiceOrderValidationResult ValidateInvoice(
            ServiceOrderHeader header,
            IEnumerable<ServiceOrderLine> lines,
            decimal roundStep)
        {
            if (header == null)
                return ServiceOrderValidationResult.Invalid(ServiceOrderConstants.InvoiceDataErrorBase);

            decimal recalcLineSum = 0m;
            if (lines != null)
            {
                foreach (ServiceOrderLine line in lines)
                {
                    if (line == null || line.IsComboChild || line.IsMaterialExport)
                        continue;
                    decimal outRate;
                    decimal outDisc;
                    decimal raw = CalcLineAmount(
                        line.Quantity,
                        line.UnitPrice,
                        line.DiscountRate,
                        line.DiscountAmount,
                        line.DiscountByMoney,
                        out outRate,
                        out outDisc);
                    decimal roundedLine = Math.Round(raw, 0, MidpointRounding.AwayFromZero);
                    if (Math.Abs(roundedLine - line.Amount) > LineAndComponentTolerance)
                        return ServiceOrderValidationResult.Invalid(ServiceOrderConstants.InvoiceDataErrorBase);
                    recalcLineSum += roundedLine;
                }
            }

            decimal expectedTienHang = LamTron(recalcLineSum, roundStep);
            decimal expectedGiamGia = header.GiamTheoTien
                ? header.TienGiamGia
                : LamTron(header.TileGiamGia * expectedTienHang / 100m, roundStep);
            decimal sauGiam = expectedTienHang - expectedGiamGia;
            decimal expectedPhi = header.PhiDichVuTheoTien
                ? header.PhiDichVu
                : LamTron(header.TilePhiDichVu * sauGiam / 100m, roundStep);
            decimal expectedThue = LamTron((sauGiam + expectedPhi) * header.TileThue / 100m, roundStep);

            if (Math.Abs(expectedTienHang - header.TienHang) > LineAndComponentTolerance
                || Math.Abs(expectedGiamGia - header.TienGiamGia) > LineAndComponentTolerance
                || Math.Abs(expectedPhi - header.PhiDichVu) > LineAndComponentTolerance
                || Math.Abs(expectedThue - header.TienThue) > LineAndComponentTolerance)
            {
                return ServiceOrderValidationResult.Invalid(ServiceOrderConstants.InvoiceDataErrorComponent);
            }

            decimal expectedTongCong = LamTron(sauGiam + expectedPhi + expectedThue, roundStep);
            if (Math.Abs(expectedTongCong - header.TongCong) > TotalTolerance)
                return ServiceOrderValidationResult.Invalid(ServiceOrderConstants.InvoiceDataErrorTotal);

            return ServiceOrderValidationResult.Valid();
        }

        /// <summary>Chọn giá bán theo DKHUVUC.DGIAMATHANGID ('2'/'3'/'4' -> GIABAN2/3/4; '0'/'1'/khác -> GIABAN).</summary>
        public static decimal ResolveAreaPrice(ServiceMenuItem item, string areaPriceLevelId)
        {
            if (item == null)
                return 0m;
            switch (areaPriceLevelId ?? "1")
            {
                case "2": return item.SellPrice2;
                case "3": return item.SellPrice3;
                case "4": return item.SellPrice4;
                default: return item.SellPrice;
            }
        }

        /// <summary>
        /// Xác định trạng thái bàn theo đúng thứ tự ưu tiên §3.1:
        /// (1) có bill chưa thanh toán -> TempPrinted (nếu SOLANINTAMTINH > 0) hoặc InUse;
        /// (2) DBAN.TRANGTHAI = 1 -> NeedCleaning, = 2 -> Cleaning;
        /// (3) có TSUACHUA chưa xong -> UnderRepair;
        /// (4) có TDATHANG hôm nay ±30 phút -> Reserved;
        /// (5) Empty.
        /// </summary>
        public static TableStatus ResolveTableStatus(
            bool hasUnpaidBill,
            int tempPrintCount,
            int tableFlag,
            int underRepairCount,
            bool hasReservation)
        {
            if (hasUnpaidBill)
                return tempPrintCount > 0 ? TableStatus.TempPrinted : TableStatus.InUse;
            if (tableFlag == 1)
                return TableStatus.NeedCleaning;
            if (tableFlag == 2)
                return TableStatus.Cleaning;
            if (underRepairCount > 0)
                return TableStatus.UnderRepair;
            if (hasReservation)
                return TableStatus.Reserved;
            return TableStatus.Empty;
        }

        /// <summary>
        /// Ngày giao dịch khi đóng bill theo CachChonNgayGiaoDich (§6.5):
        /// - Mode 0 (Theo ngày mở bàn): giữ nguyên openDate, không cảnh báo chuyển ngày.
        /// - Mode 1 (Theo ngày thanh toán): dùng serverDate; cảnh báo chuyển ngày nếu openDate != serverDate.
        /// </summary>
        public static DateTime ResolveBusinessDateForPayment(
            int businessDateMode,
            DateTime openDate,
            DateTime serverDate,
            out bool needsTransferWarning)
        {
            if (businessDateMode == (int)BusinessDateMode.ByOpenDate)
            {
                needsTransferWarning = false;
                return openDate.Date;
            }
            needsTransferWarning = openDate.Date != serverDate.Date;
            return serverDate.Date;
        }

        /// <summary>Làm tròn giờ vào (bỏ phần giây, trừ TuDongDoiGioVaoVeTruoc phút — spec §3.3).</summary>
        public static DateTime ComputeStartTime(DateTime serverNow, int shiftBackMinutes)
        {
            DateTime shifted = shiftBackMinutes != 0 ? serverNow.AddMinutes(-shiftBackMinutes) : serverNow;
            return new DateTime(shifted.Year, shifted.Month, shifted.Day, shifted.Hour, shifted.Minute, 0);
        }
    }
}
