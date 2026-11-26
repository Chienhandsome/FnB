using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật tính tiền thuần decimal cho Báo giá bán hàng (docs/specs/bao-gia-ban-hang.md §5.0, §5.0.1, §2.2).
    /// KHÔNG dùng BookingRules.CalcTotals và không đổi luật Khách đặt hàng.
    /// </summary>
    public static class QuoteCalculator
    {
        /// <summary>Thành tiền một dòng = SOLUONG × DONGIA × (1 - TILEGIAMGIA/100), không làm tròn.</summary>
        public static decimal CalcLineAmount(decimal quantity, decimal price, decimal discountPct)
        {
            return quantity * price * (1m - discountPct / 100m);
        }

        /// <summary>Làm tròn theo bước roundStep (SystemConfig.LamTronTien), kiểu AwayFromZero. roundStep &lt;= 0 thì giữ nguyên.</summary>
        public static decimal RoundMoney(decimal amount, decimal roundStep)
        {
            return roundStep > 0m ? MoneyRounding.RoundToStep(amount, roundStep) : amount;
        }

        /// <summary>
        /// Tính tổng phiếu báo giá từ danh sách dòng chi tiết.
        /// - TIENHANG = tổng thành tiền các dòng, không làm tròn.
        /// - Giảm theo % (giamTheoTien = false): TIENGIAMGIA = LamTron(TIENHANG * TILE / 100).
        /// - Giảm theo tiền (giamTheoTien = true): TIENGIAMGIA = LamTron(số đã nhập),
        ///   TILEGIAMGIA = TIENHANG == 0 ? 0 : 100 * TIENGIAMGIA / TIENHANG (dùng số đã làm tròn).
        /// - TIENTHUE = LamTron(TILETHUE * (TIENHANG - TIENGIAMGIA đã tròn) / 100).
        /// - TONGCONG = LamTron(TIENHANG - TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN).
        /// </summary>
        public static QuoteTotals CalcTotals(
            IEnumerable<QuoteLine> lines,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tileThue,
            decimal phiVanChuyen,
            decimal roundStep)
        {
            decimal tienHang = 0m;
            if (lines != null)
            {
                foreach (QuoteLine line in lines)
                {
                    if (line == null)
                        continue;
                    tienHang += CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);
                }
            }
            return CalcTotalsFromSubtotal(tienHang, tileGiamGia, tienGiamGia, giamTheoTien, tileThue, phiVanChuyen, roundStep);
        }

        /// <summary>Tính tổng phiếu báo giá từ TIENHANG cho sẵn (không làm tròn TIENHANG).</summary>
        public static QuoteTotals CalcTotalsFromSubtotal(
            decimal tienHang,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tileThue,
            decimal phiVanChuyen,
            decimal roundStep)
        {
            decimal outTile;
            decimal outTienGiam;
            if (giamTheoTien)
            {
                outTienGiam = RoundMoney(tienGiamGia, roundStep);
                outTile = tienHang == 0m ? 0m : 100m * outTienGiam / tienHang;
            }
            else
            {
                outTile = tileGiamGia;
                outTienGiam = RoundMoney(tienHang * tileGiamGia / 100m, roundStep);
            }

            decimal tienThue = RoundMoney(tileThue * (tienHang - outTienGiam) / 100m, roundStep);
            decimal tongCong = RoundMoney(tienHang - outTienGiam + tienThue + phiVanChuyen, roundStep);

            return new QuoteTotals
            {
                TienHang = tienHang,
                TileGiamGia = outTile,
                TienGiamGia = outTienGiam,
                TienThue = tienThue,
                TongCong = tongCong
            };
        }
    }
}
