using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Công thức khối tổng cộng màn "Thống kê doanh thu" (docs/specs/thong-ke-doanh-thu.md §2.3).
    /// Thuần decimal, không truy cập DB, không làm tròn thêm (chỉ cộng/trừ theo spec §5).
    /// </summary>
    public static class RevenueSummaryCalculator
    {
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static RevenueSummary Calculate(IEnumerable<RevenueBillRow> bills, RevenueCashFlowTotals cashFlow)
        {
            decimal otherIncome = cashFlow == null ? 0m : cashFlow.OtherIncome;
            decimal otherExpense = cashFlow == null ? 0m : cashFlow.OtherExpense;
            return Calculate(bills, otherIncome, otherExpense);
        }

        /// <summary>
        /// Tính toàn bộ 3 nhóm tổng cộng trên danh sách bill trong phạm vi kết hợp THU KHÁC / CHI KHÁC.
        /// TỔNG GIẢM GIÁ luôn cộng cả GIẢM GIÁ TIỀN GIỜ (kể cả khi UI ẩn dòng giờ theo Q7).
        /// TỔNG THỰC THU = TIỀN MẶT + TIỀN THẺ + CHUYỂN KHOẢN + THU KHÁC − CHI KHÁC
        /// (không cộng VOUCHER, TRỪ TÍCH LŨY, THẺ TRẢ TRƯỚC, CÔNG NỢ).
        /// </summary>
        public static RevenueSummary Calculate(IEnumerable<RevenueBillRow> bills, decimal otherIncome, decimal otherExpense)
        {
            decimal goodsAmount = 0m;
            decimal timeCharge = 0m;
            decimal goodsDiscount = 0m;
            decimal timeDiscount = 0m;
            decimal serviceFee = 0m;
            decimal tax = 0m;
            decimal totalRevenue = 0m;

            decimal cash = 0m;
            decimal card = 0m;
            decimal bankTransfer = 0m;
            decimal voucher = 0m;
            decimal loyaltyDeduction = 0m;
            decimal prepaidCard = 0m;
            decimal debt = 0m;

            if (bills != null)
            {
                foreach (RevenueBillRow bill in bills)
                {
                    if (bill == null)
                        continue;

                    goodsAmount += bill.TienHangChuaGiam;
                    timeCharge += bill.TienGio;
                    goodsDiscount += bill.TienGiamGia + bill.GiamGiaMatHang;
                    timeDiscount += bill.TienGiamGiaGio;
                    serviceFee += bill.PhiDichVu;
                    tax += bill.TienThue;
                    totalRevenue += bill.TongCong;

                    cash += bill.TienMat;
                    card += bill.The;
                    bankTransfer += bill.ChuyenKhoan;
                    voucher += bill.Voucher;
                    loyaltyDeduction += bill.TruTichLuy;
                    prepaidCard += bill.TheTraTruoc;
                    debt += bill.CongNo;
                }
            }

            decimal totalDiscount = goodsDiscount + timeDiscount;
            decimal netActualReceipt = cash + card + bankTransfer + otherIncome - otherExpense;

            return new RevenueSummary
            {
                GoodsAmount = goodsAmount,
                TimeCharge = timeCharge,
                GoodsDiscount = goodsDiscount,
                TimeDiscount = timeDiscount,
                TotalDiscount = totalDiscount,
                ServiceFee = serviceFee,
                Tax = tax,
                TotalRevenue = totalRevenue,

                Cash = cash,
                Card = card,
                BankTransfer = bankTransfer,
                Voucher = voucher,
                LoyaltyDeduction = loyaltyDeduction,
                PrepaidCard = prepaidCard,
                Debt = debt,

                OtherIncome = otherIncome,
                OtherExpense = otherExpense,
                NetActualReceipt = netActualReceipt
            };
        }

        /// <summary>Định dạng tiền VNĐ số nguyên có dấu phân nhóm nghìn (n0, vi-VN).</summary>
        public static string FormatMoney(decimal value)
        {
            return value.ToString("n0", ViCulture);
        }

        /// <summary>Định dạng 3 dòng giảm giá như app gốc: "-" + số n0.</summary>
        public static string FormatDiscount(decimal value)
        {
            return "-" + value.ToString("n0", ViCulture);
        }
    }
}
