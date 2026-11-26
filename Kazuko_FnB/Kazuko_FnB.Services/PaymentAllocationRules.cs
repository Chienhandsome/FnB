using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật thanh toán thuần decimal cho bill đơn và nhóm bill (docs/specs/su-dung-dich-vu.md §3.7.1–§3.7.3,
    /// docs/specs/quy-tien.md §4.3 &amp; §4.8). Dùng lại <see cref="FundRules.SettlePayment"/>.
    /// Không đụng DB, không đụng UI.
    /// </summary>
    public static class PaymentAllocationRules
    {
        /// <summary>
        /// Kiểm tra hợp lệ và phân bổ tiền thanh toán cho 1 bill hoặc nhóm nhiều bill (NHOMGUID).
        /// Thứ tự sắp bill cố định và lặp lại được: BATDAU tăng dần, rồi số phiếu (BillName), rồi BillId.
        /// Thứ tự lấp: đặt trước (tính chung cả nhóm, Q15) -> voucher -> thẻ trả trước -> trừ tích lũy (Q18)
        /// -> thẻ ATM -> chuyển khoản -> tiền mặt; chuyển khoản dư &amp; tiền trả lại ghi ở bill cuối (§4.8).
        /// </summary>
        public static PaymentAllocationResult Allocate(PaymentAllocationInput input)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (input.Bills == null || input.Bills.Count == 0)
                return Fail(ServicePaymentConstants.EmptyBillError);

            if (input.Voucher < 0m || input.PrepaidCard < 0m || input.PointsMoney < 0m)
                return Fail(FundText.NegativeAmount);

            var sorted = new List<PaymentBillSlice>(input.Bills);
            sorted.Sort(CompareBills);

            decimal totalBills = 0m;
            decimal totalDeposit = 0m;
            foreach (PaymentBillSlice b in sorted)
            {
                if (b == null)
                    return Fail(ServicePaymentConstants.EmptyBillError);
                totalBills += b.TongCong;
                totalDeposit += b.Deposit > 0m ? b.Deposit : 0m;
            }

            decimal netDue = totalBills + input.OldDebt - totalDeposit;
            decimal nonCash = input.Voucher + input.PrepaidCard + input.PointsMoney;
            decimal totalPaidInput = input.CashTendered + input.Transfer + input.Card + nonCash;

            FundPaymentResult fundRes = FundRules.SettlePayment(new FundPaymentInput
            {
                Due = netDue,
                CashTendered = input.CashTendered,
                Transfer = input.Transfer,
                Card = input.Card,
                NonCash = nonCash,
                AccountId = input.AccountId
            });
            if (!string.IsNullOrEmpty(fundRes.Error))
                return Fail(fundRes.Error);

            decimal shortfall = netDue > 0m ? Math.Max(0m, netDue - totalPaidInput) : 0m;
            if (shortfall > 0m)
            {
                if (!input.AllowCustomerDebt)
                    return Fail(ServicePaymentConstants.DebtDisabled);
                if (string.IsNullOrEmpty(input.CustomerId))
                    return Fail(ServicePaymentConstants.CustomerRequiredForDebt);
            }

            // Q15 / Q16: cọc dư = min(tổng cọc, max(0, -TONGTIEN)).
            decimal excessDeposit = netDue < 0m ? Math.Min(totalDeposit, -netDue) : 0m;
            decimal totalDepositUsed = totalDeposit - excessDeposit;

            int n = sorted.Count;
            int last = n - 1;
            var depUsed = new decimal[n];
            decimal remDep = totalDepositUsed;
            for (int i = 0; i < n; i++)
            {
                if (i < last)
                {
                    decimal take = Math.Min(remDep, Math.Max(0m, sorted[i].TongCong));
                    depUsed[i] = take;
                    remDep -= take;
                }
                else
                {
                    depUsed[i] = remDep;
                    remDep = 0m;
                }
            }

            // Số còn cần lấp trên từng bill (Σ need == max(0, netDue)).
            var need = new decimal[n];
            for (int i = 0; i < n; i++)
            {
                need[i] = i < last
                    ? sorted[i].TongCong - depUsed[i]
                    : sorted[i].TongCong + input.OldDebt - depUsed[i];
            }
            if (need[last] < 0m)
            {
                decimal neg = -need[last];
                need[last] = 0m;
                for (int i = last - 1; i >= 0 && neg > 0m; i--)
                {
                    decimal reduce = Math.Min(Math.Max(0m, need[i]), neg);
                    need[i] -= reduce;
                    neg -= reduce;
                }
            }
            for (int i = 0; i < n; i++)
                if (need[i] < 0m) need[i] = 0m;

            decimal remVoucher = input.Voucher;
            decimal remPrepaid = input.PrepaidCard;
            decimal remPoints = input.PointsMoney;
            decimal remCard = input.Card;
            decimal remTransfer = input.Transfer;
            decimal remCash = Math.Max(0m, fundRes.CashKept);

            var allocations = new List<PaymentBillAllocation>(n);
            decimal sumCashAllocated = 0m;
            decimal totalPointsMoneyApplied = 0m;

            for (int i = 0; i < n; i++)
            {
                PaymentBillSlice b = sorted[i];
                decimal left = need[i];

                decimal v = Take(ref remVoucher, ref left);
                decimal p = Take(ref remPrepaid, ref left);
                decimal pt = Take(ref remPoints, ref left);
                decimal cd = Take(ref remCard, ref left);
                decimal tr = Take(ref remTransfer, ref left);
                decimal ca = Take(ref remCash, ref left);

                decimal applied = v + p + pt + cd + tr + ca;
                sumCashAllocated += ca;
                totalPointsMoneyApplied += pt;

                allocations.Add(new PaymentBillAllocation
                {
                    BillId = b.BillId,
                    BillName = b.BillName,
                    TableId = b.TableId,
                    TableName = b.TableName,
                    TongCong = b.TongCong,
                    DepositUsed = depUsed[i],
                    DatTruoc = n == 1 ? totalDeposit : depUsed[i] + (i == last ? excessDeposit : 0m),
                    NoCu = i == last ? input.OldDebt : 0m,
                    Voucher = v,
                    PrepaidCard = p,
                    PointsMoney = pt,
                    Card = cd,
                    Transfer = tr,
                    Cash = ca,
                    TienThanhToan = depUsed[i] + applied,
                    EarnedPoints = CalcEarnedPoints(b.TongCong, input.RevenuePerPoint)
                });
            }

            // §4.8: chuyển khoản còn dư ghi hết vào bill cuối; tiền trả lại trừ vào TIENMAT của bill cuối (âm được).
            if (remTransfer > 0m)
                allocations[last].Transfer += remTransfer;
            allocations[last].Change = fundRes.Change;

            decimal cashDelta = fundRes.CashKept - sumCashAllocated;
            if (cashDelta != 0m)
                allocations[last].Cash += cashDelta;

            // Hoàn thiện cột dẫn xuất, tài khoản, mã voucher/thẻ, điểm sử dụng và khách hàng đứng tên trên từng bill.
            decimal totalPointsExpected = CalcPointsFromMoney(totalPointsMoneyApplied, input.PointRate);
            decimal pointsAssigned = 0m;
            int lastPointsBillIdx = -1;

            for (int i = 0; i < n; i++)
            {
                PaymentBillAllocation a = allocations[i];
                PaymentBillSlice b = sorted[i];

                a.KhachDua = a.Cash + a.Change;
                a.CongNo = a.TongCong - a.TienThanhToan;
                a.ConLai = Math.Max(0m, a.CongNo);
                a.ConNoFlag = a.CongNo > 0m ? 30 : 0;
                a.AccountId = (a.Card != 0m || a.Transfer != 0m) ? (input.AccountId ?? "") : null;
                a.VoucherId = a.Voucher > 0m && !string.IsNullOrEmpty(input.VoucherId) ? input.VoucherId : null;
                a.PrepaidCardId = a.PrepaidCard > 0m && !string.IsNullOrEmpty(input.PrepaidCardId) ? input.PrepaidCardId : null;

                if (a.PointsMoney > 0m)
                {
                    a.PointsUsed = CalcPointsFromMoney(a.PointsMoney, input.PointRate);
                    pointsAssigned += a.PointsUsed;
                    lastPointsBillIdx = i;
                }

                if (n == 1)
                {
                    a.FinalCustomerId = !string.IsNullOrEmpty(input.CustomerId) ? input.CustomerId : b.OriginalCustomerId;
                }
                else
                {
                    bool carriesDebt = a.CongNo > 0m;
                    bool carriesOldDebt = i == last && input.OldDebt != 0m && !string.IsNullOrEmpty(input.CustomerId);
                    bool usesCustomerPoints = a.PointsMoney > 0m && !string.IsNullOrEmpty(input.CustomerId);
                    a.FinalCustomerId = (carriesDebt || carriesOldDebt || usesCustomerPoints)
                        ? input.CustomerId
                        : b.OriginalCustomerId;
                }
            }

            if (lastPointsBillIdx >= 0 && pointsAssigned != totalPointsExpected)
                allocations[lastPointsBillIdx].PointsUsed += totalPointsExpected - pointsAssigned;

            return new PaymentAllocationResult
            {
                TotalBills = totalBills,
                TotalDeposit = totalDeposit,
                TotalDepositUsed = totalDepositUsed,
                NetDue = netDue,
                TotalPaidInput = totalPaidInput,
                Change = fundRes.Change,
                CashKept = fundRes.CashKept,
                Shortfall = shortfall,
                ExcessDeposit = excessDeposit,
                Bills = allocations
            };
        }

        /// <summary>
        /// Kiểm soát số order khi BatChucNangKiemSoatOrder = 30 (§3.7):
        /// bắt buộc có nội dung, dạng danh sách số nguyên phân tách bởi dấu phẩy (vd: "12, 16, 17,19").
        /// Trả null nếu hợp lệ, ngược lại trả thông báo nguyên văn §3.7.
        /// </summary>
        public static string ValidateOrderNumbers(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ServicePaymentConstants.OrderNumberRequired;

            string[] parts = raw.Split(',');
            int validCount = 0;
            foreach (string p in parts)
            {
                string trimmed = (p ?? "").Trim();
                if (trimmed.Length == 0)
                    return ServicePaymentConstants.OrderNumberInvalid;
                int num;
                if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out num))
                    return ServicePaymentConstants.OrderNumberInvalid;
                validCount++;
            }
            return validCount == 0 ? ServicePaymentConstants.OrderNumberRequired : null;
        }

        /// <summary>Tính điểm tích lũy earned trên một hóa đơn: (int)(TONGCONG / DoanhSoTuongUngVoi1Diem).</summary>
        public static int CalcEarnedPoints(decimal tongCong, int revenuePerPoint)
        {
            if (tongCong <= 0m || revenuePerPoint <= 0)
                return 0;
            return (int)Math.Floor(tongCong / revenuePerPoint);
        }

        /// <summary>Quy đổi tiền trừ tích lũy sang số điểm (DIEMGIAM = Round(tiền / quyĐổi, 2, AwayFromZero)).</summary>
        public static decimal CalcPointsFromMoney(decimal money, decimal pointRate)
        {
            if (money <= 0m || pointRate <= 0m)
                return 0m;
            return Math.Round(money / pointRate, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Quy đổi số điểm sang tiền (điểm × QuyDoi1DiemSangTien).</summary>
        public static decimal CalcMoneyFromPoints(decimal points, decimal pointRate)
        {
            if (points <= 0m || pointRate <= 0m)
                return 0m;
            return Math.Round(points * pointRate, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Số điểm tối đa có thể dùng cho số tiền phải thanh toán (nút "Sử dụng hết").</summary>
        public static decimal CalcMaxRedeemablePoints(decimal availablePoints, decimal netDue, decimal pointRate)
        {
            if (availablePoints <= 0m || netDue <= 0m || pointRate <= 0m)
                return 0m;
            decimal maxByDue = Math.Round(netDue / pointRate, 2, MidpointRounding.AwayFromZero);
            return Math.Min(availablePoints, maxByDue);
        }

        private static decimal Take(ref decimal source, ref decimal need)
        {
            if (source <= 0m || need <= 0m)
                return 0m;
            decimal used = Math.Min(source, need);
            source -= used;
            need -= used;
            return used;
        }

        private static int CompareBills(PaymentBillSlice a, PaymentBillSlice b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int cmp = DateTime.Compare(a.BatDau, b.BatDau);
            if (cmp != 0) return cmp;
            cmp = string.CompareOrdinal(a.BillName ?? "", b.BillName ?? "");
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(a.BillId ?? "", b.BillId ?? "");
        }

        private static PaymentAllocationResult Fail(string error)
        {
            return new PaymentAllocationResult { Error = error };
        }
    }
}
