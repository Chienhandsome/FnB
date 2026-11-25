using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật quỹ tiền: giao dịch nào làm quỹ nào tăng giảm bao nhiêu.
    /// Xem docs/specs/quy-tien.md §4.2, §4.3, §4.5. Không đụng DB, không đụng giao diện.
    /// Chưa gồm: chia tiền gộp bàn (§4.8), mốc số dư đầu kỳ (§4.4), chuyển quỹ (§4.6).
    /// </summary>
    public static class FundRules
    {
        public const int SaleType = 0;
        public const int PurchaseType = 1;
        public const int SupplierReturnType = 2;
        public const int PaidFlag = 30;
        /// <summary>STATUS của bản ghi đã xóa mềm: không tính vào quỹ (Q4).</summary>
        public const int DeletedStatus = -30;

        /// <summary>Hóa đơn bán đã thanh toán. LOAI khác 0 (gồm 5, 6 chưa rõ, Q5) không sinh dòng quỹ.</summary>
        public static List<FundEntry> ForBill(FundBill bill)
        {
            if (bill == null) throw new ArgumentNullException("bill");
            var entries = new List<FundEntry>();
            if (bill.Status == DeletedStatus || bill.Type != SaleType || bill.PaidFlag != PaidFlag)
                return entries;

            Add(entries, FundKind.Cash, null, bill.Cash, false);
            Add(entries, FundKind.Account, bill.AccountId, bill.Transfer, false);
            Add(entries, FundKind.Account, bill.AccountId, bill.Card, true);
            return entries;
        }

        /// <summary>Phiếu thu/chi: cờ CHUYENKHOAN khác 0 là quỹ Tài khoản.</summary>
        public static List<FundEntry> ForVoucher(FundVoucher voucher)
        {
            if (voucher == null) throw new ArgumentNullException("voucher");
            var entries = new List<FundEntry>();
            if (voucher.Status == DeletedStatus)
                return entries;

            FundKind fund = voucher.TransferFlag != 0 ? FundKind.Account : FundKind.Cash;
            Add(entries, fund, voucher.AccountId, voucher.Income, false);
            Add(entries, fund, voucher.AccountId, -voucher.Expense, false);
            return entries;
        }

        /// <summary>
        /// Phiếu nhập (tiền ra) và xuất trả hàng nhà cung cấp (tiền vào, Q3). Số tiền lấy TIENTHANHTOAN;
        /// CHUYENKHOAN khác 0 là quỹ Tài khoản, còn lại (gồm phiếu cũ không ghi gì) là Tiền mặt.
        /// </summary>
        public static List<FundEntry> ForStockDocument(FundStockDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            var entries = new List<FundEntry>();
            if (document.Status == DeletedStatus)
                return entries;

            decimal amount;
            if (document.Type == PurchaseType)
                amount = -document.PaidAmount;
            else if (document.Type == SupplierReturnType && !string.IsNullOrEmpty(document.SupplierId))
                amount = document.PaidAmount;
            else
                return entries;

            FundKind fund = document.Transfer != 0m ? FundKind.Account : FundKind.Cash;
            Add(entries, fund, document.AccountId, amount, false);
            return entries;
        }

        /// <summary>
        /// §4.3 và Q2: tính tiền trả lại và số ghi TIENMAT. Tiền trả lại chỉ phát sinh từ tiền mặt khách đưa
        /// và chuyển khoản; thẻ không được quá số còn phải trả; phần dư voucher/thẻ trả trước/tích lũy bỏ.
        /// </summary>
        public static FundPaymentResult SettlePayment(FundPaymentInput input)
        {
            if (input == null) throw new ArgumentNullException("input");

            if (input.CashTendered < 0m || input.Transfer < 0m || input.Card < 0m || input.NonCash < 0m)
                return Fail(FundText.NegativeAmount);

            // Đặt trước lớn hơn tiền bill: phần trả lại đi qua phiếu chi, hóa đơn không nhận thêm tiền.
            if (input.Due <= 0m && (input.CashTendered != 0m || input.Transfer != 0m || input.Card != 0m))
                return Fail(FundText.NothingDue);

            if (input.Card > Math.Max(0m, input.Due - input.NonCash - input.Transfer))
                return Fail(FundText.CardOverDue);

            if ((input.Transfer != 0m || input.Card != 0m) && string.IsNullOrEmpty(input.AccountId))
                return Fail(FundText.AccountRequired);

            decimal dueForMoney = Math.Max(0m, input.Due - input.NonCash - input.Card);
            decimal change = Math.Max(0m, input.CashTendered + input.Transfer - dueForMoney);
            return new FundPaymentResult { Change = change, CashKept = input.CashTendered - change };
        }

        /// <summary>§4.5: giá trị cần ghi khi lưu phiếu nhập / xuất trả hàng.</summary>
        public static FundStockPaymentResult StockPaymentFields(PayMethod? method, decimal paidAmount, string accountId)
        {
            var result = new FundStockPaymentResult { AccountId = "" };
            if (paidAmount < 0m)
                result.Error = FundText.NegativeAmount;
            else if (paidAmount == 0m)
                return result;
            else if (method == null)
                result.Error = FundText.MethodRequired;
            else if (method == PayMethod.Cash)
                result.Cash = paidAmount;
            else if (string.IsNullOrEmpty(accountId))
                result.Error = FundText.AccountRequired;
            else
            {
                result.Transfer = paidAmount;
                result.AccountId = accountId;
            }
            return result;
        }

        private static FundPaymentResult Fail(string error)
        {
            return new FundPaymentResult { Error = error };
        }

        private static void Add(List<FundEntry> entries, FundKind fund, string accountId, decimal amount, bool isCard)
        {
            if (amount == 0m)
                return;
            entries.Add(new FundEntry
            {
                Fund = fund,
                AccountId = fund == FundKind.Account ? (accountId ?? "") : "",
                Amount = amount,
                IsCard = isCard
            });
        }
    }
}
