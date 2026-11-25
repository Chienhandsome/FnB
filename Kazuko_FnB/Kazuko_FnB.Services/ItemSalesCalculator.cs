using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Công thức thuần decimal của màn "Thống kê mặt hàng bán" (docs/specs/thong-ke-mat-hang-ban.md §2.0, §2.2, §2.3).
    /// Không truy cập DB, không làm tròn thêm (chỉ cộng/trừ/chia theo spec §5).
    /// </summary>
    public static class ItemSalesCalculator
    {
        /// <summary>LAI = THANHTIEN − THANHTIENNHAP.</summary>
        public static decimal ComputeProfit(decimal salesAmount, decimal costAmount)
        {
            return salesAmount - costAmount;
        }

        /// <summary>TILELAI = THANHTIEN == 0 ? 0 : 100 × LAI / THANHTIEN.</summary>
        public static decimal ComputeProfitRate(decimal salesAmount, decimal profit)
        {
            if (salesAmount == 0m)
                return 0m;
            return 100m * profit / salesAmount;
        }

        /// <summary>Gán LAI và TILELAI cho từng dòng lưới.</summary>
        public static void ApplyRowMetrics(IEnumerable<ItemSalesRow> rows)
        {
            if (rows == null)
                return;
            foreach (ItemSalesRow row in rows)
            {
                if (row == null)
                    continue;
                row.Profit = ComputeProfit(row.SalesAmount, row.CostAmount);
                row.ProfitRate = ComputeProfitRate(row.SalesAmount, row.Profit);
            }
        }

        /// <summary>
        /// Tính toàn bộ khối tổng cộng (§2.3 và §2.0) trên toàn bộ dòng thuộc phạm vi ngày + cửa hàng
        /// (KHÔNG lọc theo cây nhóm hay ô Lọc):
        ///   Tổng tiền bán   = Σ (SLXUATCHUAQUYDOI_ròng × DONGIA) (trước chiết khấu dòng)
        ///   Tổng tiền nhập  = Σ THANHTIENNHAP
        ///   Tổng tiền lãi bán hàng = Tổng tiền bán − Tổng tiền nhập
        ///   Tổng tiền lãi   = Tổng tiền lãi bán hàng − Tổng tiền giảm giá + Thuế + Phí dịch vụ + Tiền giờ
        /// </summary>
        public static ItemSalesSummary ComputeSummary(IEnumerable<ItemSalesRow> allRows, ItemSalesBillTotals billTotals)
        {
            decimal totalSalesBeforeDiscount = 0m;
            decimal totalCost = 0m;

            if (allRows != null)
            {
                foreach (ItemSalesRow row in allRows)
                {
                    if (row == null)
                        continue;
                    totalSalesBeforeDiscount += row.Quantity * row.UnitPrice;
                    totalCost += row.CostAmount;
                }
            }

            decimal totalDiscount = billTotals == null ? 0m : billTotals.TotalDiscount;
            decimal timeCharge = billTotals == null ? 0m : billTotals.TimeCharge;
            decimal serviceFee = billTotals == null ? 0m : billTotals.ServiceFee;
            decimal tax = billTotals == null ? 0m : billTotals.Tax;

            decimal grossSalesProfit = totalSalesBeforeDiscount - totalCost;
            decimal netProfit = grossSalesProfit - totalDiscount + tax + serviceFee + timeCharge;

            return new ItemSalesSummary
            {
                TotalSalesBeforeDiscount = totalSalesBeforeDiscount,
                TotalCost = totalCost,
                GrossSalesProfit = grossSalesProfit,
                TimeCharge = timeCharge,
                TotalDiscount = totalDiscount,
                ServiceFee = serviceFee,
                Tax = tax,
                NetProfit = netProfit
            };
        }
    }
}
