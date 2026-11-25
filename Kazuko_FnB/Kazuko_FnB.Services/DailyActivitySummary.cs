using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Công thức thuần decimal cho báo cáo "Chi tiết hoạt động ngày" (docs/specs/chi-tiet-hoat-dong-ngay.md):
    /// dòng tổng 4 khối, 4 dòng tổng §2.5, "Còn nợ" phiếu nhập (P1/Q1), tách quỹ tiền nhập (Q12) và 8 dòng tổng hợp §2.7.
    /// Không truy cập DB, không làm tròn thêm.
    /// </summary>
    public static class DailyActivitySummary
    {
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Nhãn 8 dòng khối tổng hợp theo đúng thứ tự in của spec §2.7.</summary>
        public static readonly string[] OverallCaptions =
        {
            "TIỀN MẶT",
            "CHUYỂN KHOẢN",
            "TIỀN THẺ",
            "VOUCHER",
            "THẺ TRẢ TRƯỚC",
            "TRỪ TÍCH LŨY",
            "KHÁCH HÀNG NỢ",
            "NỢ NHÀ CUNG CẤP"
        };

        /// <summary>
        /// Còn nợ phiếu nhập theo P1 / Q1 (đã chốt): MAX(0, TONGCONG − TIENTHANHTOAN).
        /// Trả dư ra 0 (khác công thức gốc ra số âm).
        /// </summary>
        public static decimal CalculatePurchaseRemainingDebt(decimal totalAmount, decimal paidAmount)
        {
            decimal diff = totalAmount - paidAmount;
            return diff > 0m ? diff : 0m;
        }

        /// <summary>
        /// Công thức "Còn nợ" phiếu nhập của app gốc (§2.4, §3-1):
        /// CASE WHEN COALESCE(LOAITHANHTOAN, 0) &lt; TONGCONG THEN TONGCONG − TIENTHANHTOAN ELSE 0 END.
        /// Chỉ dùng để đối chiếu trong selftest (Q1).
        /// </summary>
        public static decimal CalculatePurchaseRemainingDebtLegacy(decimal totalAmount, decimal paidAmount, decimal paymentType)
        {
            return paymentType < totalAmount ? totalAmount - paidAmount : 0m;
        }

        /// <summary>
        /// Hình thức trả của phiếu nhập theo Q12 &amp; quy-tien.md §4.5:
        /// CHUYENKHOAN khác 0 → chuyển khoản; còn lại (0, NULL, phiếu cũ) → tiền mặt.
        /// </summary>
        public static bool IsPurchaseTransfer(DailyPurchaseRow purchase)
        {
            return purchase != null && purchase.ChuyenKhoan != 0m;
        }

        /// <summary>Tính toàn bộ dòng tổng 4 khối và khối tổng hợp 8 dòng từ dữ liệu báo cáo.</summary>
        public static DailyActivitySummaryResult Calculate(DailyActivityReportData data)
        {
            if (data == null)
                return new DailyActivitySummaryResult();
            return Calculate(data.SaleBills, data.SoldItems, data.BillTotalDiscount, data.Purchases, data.CashFlows);
        }

        public static DailyActivitySummaryResult Calculate(
            IEnumerable<DailySaleBillRow> saleBills,
            IEnumerable<DailySoldItemRow> soldItems,
            decimal billTotalDiscount,
            IEnumerable<DailyPurchaseRow> purchases,
            IEnumerable<DailyCashFlowRow> cashFlows)
        {
            DailySaleBillTotals saleTotals = CalculateSaleTotals(saleBills);
            DailySoldItemTotals itemTotals = CalculateItemTotals(soldItems, billTotalDiscount);
            DailyPurchaseTotals purchaseTotals = CalculatePurchaseTotals(purchases);
            DailyCashFlowTotals cashFlowTotals = CalculateCashFlowTotals(cashFlows);

            // §2.7 & Q12: DATTRUOC không tham gia dòng nào; phiếu nhập trừ đúng quỹ theo CHUYENKHOAN (lấy số tiền TIENTHANHTOAN).
            var overall = new DailyActivityOverallSummary
            {
                Cash = saleTotals.TienMat + cashFlowTotals.CashIn - cashFlowTotals.CashOut - purchaseTotals.PaidCash,
                BankTransfer = saleTotals.ChuyenKhoan + cashFlowTotals.TransferIn - cashFlowTotals.TransferOut - purchaseTotals.PaidTransfer,
                Card = saleTotals.The,
                Voucher = saleTotals.Voucher,
                PrepaidCard = saleTotals.TheTraTruoc,
                LoyaltyDeduction = saleTotals.TruTichLuy,
                CustomerDebt = saleTotals.CongNo,
                SupplierDebt = purchaseTotals.RemainingDebt
            };

            return new DailyActivitySummaryResult
            {
                SaleTotals = saleTotals,
                ItemTotals = itemTotals,
                PurchaseTotals = purchaseTotals,
                CashFlowTotals = cashFlowTotals,
                Overall = overall
            };
        }

        public static DailySaleBillTotals CalculateSaleTotals(IEnumerable<DailySaleBillRow> saleBills)
        {
            var totals = new DailySaleBillTotals();
            if (saleBills == null)
                return totals;

            foreach (DailySaleBillRow b in saleBills)
            {
                if (b == null)
                    continue;
                totals.TongCong += b.TongCong;
                totals.TienMat += b.TienMat;
                totals.ChuyenKhoan += b.ChuyenKhoan;
                totals.The += b.The;
                totals.Voucher += b.Voucher;
                totals.TruTichLuy += b.TruTichLuy;
                totals.TheTraTruoc += b.TheTraTruoc;
                totals.CongNo += b.CongNo;
            }
            return totals;
        }

        public static DailySoldItemTotals CalculateItemTotals(IEnumerable<DailySoldItemRow> soldItems, decimal billTotalDiscount)
        {
            decimal totalQty = 0m;
            decimal grossAmount = 0m;
            decimal itemDiscount = 0m;
            decimal amountSum = 0m;

            if (soldItems != null)
            {
                foreach (DailySoldItemRow item in soldItems)
                {
                    if (item == null)
                        continue;
                    decimal lineGross = item.Quantity * item.UnitPrice;
                    totalQty += item.Quantity;
                    grossAmount += lineGross;
                    itemDiscount += lineGross - item.Amount;
                    amountSum += item.Amount;
                }
            }

            return new DailySoldItemTotals
            {
                TotalQuantity = totalQty,
                GrossAmount = grossAmount,
                ItemDiscount = itemDiscount,
                BillDiscount = billTotalDiscount,
                NetTotal = amountSum - billTotalDiscount
            };
        }

        public static DailyPurchaseTotals CalculatePurchaseTotals(IEnumerable<DailyPurchaseRow> purchases)
        {
            var totals = new DailyPurchaseTotals();
            if (purchases == null)
                return totals;

            foreach (DailyPurchaseRow p in purchases)
            {
                if (p == null)
                    continue;
                totals.TongCong += p.TongCong;
                totals.TienThanhToan += p.TienThanhToan;
                totals.RemainingDebt += CalculatePurchaseRemainingDebt(p.TongCong, p.TienThanhToan);
                if (IsPurchaseTransfer(p))
                    totals.PaidTransfer += p.TienThanhToan;
                else
                    totals.PaidCash += p.TienThanhToan;
            }
            return totals;
        }

        public static DailyCashFlowTotals CalculateCashFlowTotals(IEnumerable<DailyCashFlowRow> cashFlows)
        {
            var totals = new DailyCashFlowTotals();
            if (cashFlows == null)
                return totals;

            foreach (DailyCashFlowRow cf in cashFlows)
            {
                if (cf == null)
                    continue;
                totals.CashIn += cf.CashIn;
                totals.CashOut += cf.CashOut;
                totals.TransferIn += cf.TransferIn;
                totals.TransferOut += cf.TransferOut;
            }
            return totals;
        }

        /// <summary>Đọc mảng 8 giá trị tổng hợp theo đúng thứ tự <see cref="OverallCaptions"/>.</summary>
        public static decimal[] OverallValues(DailyActivityOverallSummary overall)
        {
            if (overall == null)
                return new decimal[8];
            return new[]
            {
                overall.Cash,
                overall.BankTransfer,
                overall.Card,
                overall.Voucher,
                overall.PrepaidCard,
                overall.LoyaltyDeduction,
                overall.CustomerDebt,
                overall.SupplierDebt
            };
        }

        /// <summary>
        /// Định dạng giá trị cho 8 dòng tổng hợp (§2.7):
        /// 2 dòng đầu (TIỀN MẶT, CHUYỂN KHOẢN) luôn hiện số (kể cả 0); 6 dòng còn lại để trống khi bằng 0.
        /// </summary>
        public static string FormatOverallValue(int lineIndex, decimal value)
        {
            if (lineIndex >= 2 && value == 0m)
                return "";
            return FormatMoney(value);
        }

        /// <summary>Định dạng tiền VNĐ số nguyên có dấu phân nhóm nghìn (n0, vi-VN).</summary>
        public static string FormatMoney(decimal value)
        {
            return value.ToString("n0", ViCulture);
        }

        /// <summary>Định dạng số lượng (#,##0.##, vi-VN).</summary>
        public static string FormatQuantity(decimal value)
        {
            return value.ToString("#,##0.##", ViCulture);
        }

        /// <summary>Định dạng % giảm giá (0.##, vi-VN).</summary>
        public static string FormatPercent(decimal value)
        {
            return value.ToString("0.##", ViCulture);
        }
    }
}
