using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void ServiceOrderPaymentTests(string db)
        {
            ServiceOrderPaymentPureTests();
            ServiceOrderPaymentDbTests(db);
        }

        private static PaymentBillSlice MakeSlice(string id, string name, DateTime start, decimal total, decimal deposit = 0m, string custId = null)
        {
            return new PaymentBillSlice
            {
                BillId = id,
                BillName = name,
                TableId = "T-" + id,
                TableName = "Ban " + name,
                BatDau = start,
                OpenDate = start.Date,
                OriginalCustomerId = custId,
                TongCong = total,
                Deposit = deposit
            };
        }

        // ------------------------------------------------------------------ A. Luật thuần PaymentAllocationRules (§3.7, quy-tien.md §4.3 & §4.8)

        private static void ServiceOrderPaymentPureTests()
        {
            DateTime t1 = new DateTime(2026, 11, 26, 10, 0, 0);
            DateTime t2 = new DateTime(2026, 11, 26, 10, 15, 0);
            DateTime t3 = new DateTime(2026, 11, 26, 10, 30, 0);

            // 1. Quẹt thẻ quá số phải trả bị chặn (gồm CK 300k + thẻ 200k cho bill 480k).
            PaymentAllocationResult cardOver = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m) },
                Transfer = 300000m,
                Card = 200000m,
                AccountId = "TK1"
            });
            PaymentAllocationResult cardNoAcc = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m) },
                Card = 480000m,
                AccountId = ""
            });
            Check("SDDV ThanhToan §7 Quẹt thẻ quá số phải trả (CK 300k + thẻ 200k / bill 480k) và thẻ thiếu tài khoản đều bị chặn",
                !cardOver.IsValid && cardOver.Error == FundText.CardOverDue
                && !cardNoAcc.IsValid && cardNoAcc.Error == FundText.AccountRequired,
                (cardOver.Error ?? "ok") + " | " + (cardNoAcc.Error ?? "ok"));

            // 2. Voucher lớn hơn bill không sinh tiền trả lại; thêm tiền mặt thì trả lại đúng tiền mặt.
            PaymentAllocationResult vouOver = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m) },
                Voucher = 500000m,
                VoucherId = "V1"
            });
            PaymentAllocationResult vouPlusCash = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m) },
                Voucher = 500000m,
                VoucherId = "V1",
                CashTendered = 50000m
            });
            Check("SDDV ThanhToan §7 Voucher lớn hơn bill không sinh tiền trả lại (500k/480k -> TRALAI=0; đưa thêm 50k TM -> TRALAI=50k, TIENMAT=0)",
                vouOver.IsValid && vouOver.Change == 0m && vouOver.CashKept == 0m && vouOver.Bills[0].TienThanhToan == 480000m && vouOver.Bills[0].CongNo == 0m
                && vouPlusCash.IsValid && vouPlusCash.Change == 50000m && vouPlusCash.CashKept == 0m && vouPlusCash.Bills[0].TienThanhToan == 480000m);

            // 3. Bill 480.000 chuyển khoản 500.000 -> CHUYENKHOAN=500k, TIENMAT=-20k, TRALAI=20k.
            PaymentAllocationResult ckOver = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m) },
                Transfer = 500000m,
                AccountId = "TK1"
            });
            List<FundEntry> ckFunds = ckOver.IsValid
                ? FundRules.ForBill(new FundBill
                {
                    PaidFlag = 30,
                    Cash = ckOver.Bills[0].Cash,
                    Transfer = ckOver.Bills[0].Transfer,
                    Card = ckOver.Bills[0].Card,
                    AccountId = ckOver.Bills[0].AccountId
                })
                : new List<FundEntry>();
            Check("SDDV ThanhToan §7 Bill 480k chuyển khoản 500k: CHUYENKHOAN=500k, TIENMAT=-20k, TRALAI=20k, quỹ TM -20k / TK +500k",
                ckOver.IsValid && ckOver.Bills[0].Transfer == 500000m && ckOver.Bills[0].Cash == -20000m
                && ckOver.Bills[0].Change == 20000m && ckOver.Bills[0].TienThanhToan == 480000m && ckOver.Bills[0].CongNo == 0m
                && Funds(ckFunds) == "TM -20000; TK[TK1] +500000",
                Funds(ckFunds));

            // 4. Bill đơn các ca đủ / thiếu / thu kèm nợ cũ / có đặt trước / nợ cũ âm (§3.7.1, §3.7.2).
            PaymentAllocationResult singleDebt = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m) },
                CustomerId = "KH1",
                CashTendered = 200000m,
                AllowCustomerDebt = true
            });
            PaymentAllocationResult singleOldDebt = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m) },
                CustomerId = "KH1",
                OldDebt = 100000m,
                CashTendered = 400000m
            });
            PaymentAllocationResult singleNegDebt = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m) },
                CustomerId = "KH1",
                OldDebt = -100000m,
                CashTendered = 250000m
            });
            PaymentAllocationResult singleWithDep = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 480000m, 200000m) },
                CashTendered = 280000m
            });
            Check("SDDV ThanhToan §3.7.2/§7 Bill đơn: thiếu (CONGNO=100k), thu nợ cũ (CONGNO=-100k), nợ cũ âm (TRALAI=50k), đặt trước (TIENTHANHTOAN=480k)",
                singleDebt.IsValid && singleDebt.Bills[0].TienThanhToan == 200000m && singleDebt.Bills[0].CongNo == 100000m && singleDebt.Bills[0].ConNoFlag == 30
                && singleOldDebt.IsValid && singleOldDebt.Bills[0].TienThanhToan == 400000m && singleOldDebt.Bills[0].CongNo == -100000m
                && singleNegDebt.IsValid && singleNegDebt.Change == 50000m && singleNegDebt.Bills[0].Cash == 200000m && singleNegDebt.Bills[0].CongNo == 100000m
                && singleWithDep.IsValid && singleWithDep.Bills[0].TienThanhToan == 480000m && singleWithDep.Bills[0].Cash == 280000m && singleWithDep.Bills[0].CongNo == 0m);

            // 5. Bill đơn cọc dư (Q16): bill 300k, cọc 400k -> TIENTHANHTOAN=300k, CONGNO=0, ExcessDeposit=100k; nhận thêm tiền bị chặn.
            PaymentAllocationResult q16 = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m, 400000m) }
            });
            PaymentAllocationResult q16ExtraMoney = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m, 400000m) },
                CashTendered = 10000m
            });
            Check("SDDV ThanhToan §7 Q16 Bill đơn cọc dư (300k cọc 400k): TIENTHANHTOAN=300k, CONGNO=0, cọc dư 100k, chặn nhận thêm tiền",
                q16.IsValid && q16.Bills[0].TienThanhToan == 300000m && q16.Bills[0].CongNo == 0m && q16.ExcessDeposit == 100000m
                && !q16ExtraMoney.IsValid && q16ExtraMoney.Error == FundText.NothingDue);

            // 6. Gộp bàn ví dụ quy-tien.md §4.8: 300k + 200k, trả 350k CK + 150k TM.
            PaymentAllocationResult gBasic = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B2", "HD2", t2, 200000m), MakeSlice("B1", "HD1", t1, 300000m) },
                Transfer = 350000m,
                CashTendered = 150000m,
                AccountId = "TK1"
            });
            Check("SDDV ThanhToan §7/Quỹ §4.8 Gộp bàn 300k+200k (CK 350k + TM 150k): sắp đúng BATDAU -> bill1 CK 300k; bill2 CK 50k + TM 150k",
                gBasic.IsValid && gBasic.Bills.Count == 2
                && gBasic.Bills[0].BillId == "B1" && gBasic.Bills[0].Transfer == 300000m && gBasic.Bills[0].Cash == 0m
                && gBasic.Bills[1].BillId == "B2" && gBasic.Bills[1].Transfer == 50000m && gBasic.Bills[1].Cash == 150000m);

            // 7. Nhóm cọc dư Q15: bill 1 = 300k (cọc 400k), bill 2 = 200k (cọc 0) -> thu 100k TM, không phiếu chi.
            PaymentAllocationResult gQ15 = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m, 400000m), MakeSlice("B2", "HD2", t2, 200000m, 0m) },
                CashTendered = 100000m
            });
            Check("SDDV ThanhToan §7 Q15 Nhóm cọc dư (300k cọc 400k + 200k): NetDue=100k, ExcessDeposit=0, bill1 TIENTHANHTOAN=300k, bill2 TIENTHANHTOAN=200k (TM 100k)",
                gQ15.IsValid && gQ15.NetDue == 100000m && gQ15.ExcessDeposit == 0m
                && gQ15.Bills[0].DepositUsed == 300000m && gQ15.Bills[0].TienThanhToan == 300000m && gQ15.Bills[0].Cash == 0m
                && gQ15.Bills[1].DepositUsed == 100000m && gQ15.Bills[1].TienThanhToan == 200000m && gQ15.Bills[1].Cash == 100000m);

            // 8. Gộp 3 bill đủ voucher + thẻ trả trước + tích lũy + thẻ ATM + CK + TM + cọc (Q18, Quỹ §7).
            var multiInput = new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice>
                {
                    MakeSlice("B1", "HD1", t1, 200000m, 50000m),  // need 150k -> Vou 100k + Prepaid 50k
                    MakeSlice("B2", "HD2", t2, 300000m, 0m),      // need 300k -> Prepaid 30k + Points 70k + Card 100k + CK 100k
                    MakeSlice("B3", "HD3", t3, 250000m, 0m)       // need 250k -> CK 50k + TM 200k
                },
                CustomerId = "KH1",
                Voucher = 100000m,
                VoucherId = "V1",
                PrepaidCard = 80000m,
                PrepaidCardId = "C1",
                PointsMoney = 70000m,
                PointRate = 1000m,
                Card = 100000m,
                Transfer = 150000m,
                CashTendered = 220000m,
                AccountId = "TK1",
                RevenuePerPoint = 20000
            };
            PaymentAllocationResult g3a = PaymentAllocationRules.Allocate(multiInput);
            PaymentAllocationResult g3b = PaymentAllocationRules.Allocate(multiInput);
            decimal sumCard = 0m, sumCk = 0m, sumCash = 0m, sumChg = 0m;
            if (g3a.IsValid)
            {
                foreach (PaymentBillAllocation a in g3a.Bills)
                {
                    sumCard += a.Card;
                    sumCk += a.Transfer;
                    sumCash += a.Cash;
                    sumChg += a.Change;
                }
            }
            bool g3OrderOk = g3a.IsValid && g3b.IsValid
                && g3a.Bills[0].Voucher == 100000m && g3a.Bills[0].PrepaidCard == 50000m && g3a.Bills[0].Card == 0m
                && g3a.Bills[1].PrepaidCard == 30000m && g3a.Bills[1].PointsMoney == 70000m && g3a.Bills[1].Card == 100000m && g3a.Bills[1].Transfer == 100000m
                && g3a.Bills[2].Transfer == 50000m && g3a.Bills[2].Cash == 200000m && g3a.Bills[2].Change == 20000m
                && g3a.Bills[0].Change == 0m && g3a.Bills[1].Change == 0m
                && sumCard == 100000m && sumCk == 150000m && sumCash == 200000m && sumChg == 20000m
                && g3a.Bills[2].Cash == g3b.Bills[2].Cash && g3a.Bills[1].Transfer == g3b.Bills[1].Transfer;
            Check("SDDV ThanhToan §7/Q18/Quỹ §7 Gộp 3 bill đủ mọi hình thức: đúng thứ tự lấp, mọi bất biến Σ khớp, lặp lại ra cùng kết quả",
                g3OrderOk);

            // 9. Gộp bàn có chuyển khoản dư: phần dư và TIENMAT âm nằm ở bill cuối.
            PaymentAllocationResult gCkOver = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m), MakeSlice("B2", "HD2", t2, 200000m) },
                Transfer = 530000m,
                AccountId = "TK1"
            });
            Check("SDDV ThanhToan §7/Quỹ §7 Gộp bàn CK dư (530k/500k): bill1 CK 300k; bill2 CK 230k, TIENMAT=-30k, TRALAI=30k",
                gCkOver.IsValid && gCkOver.Bills[0].Transfer == 300000m && gCkOver.Bills[0].Cash == 0m && gCkOver.Bills[0].Change == 0m
                && gCkOver.Bills[1].Transfer == 230000m && gCkOver.Bills[1].Cash == -30000m && gCkOver.Bills[1].Change == 30000m);

            // 10. Gộp bàn trả thiếu 100k & 250k + chọn khách đứng tên nợ + chặn khi thiếu khách hoặc ChoPhepKhachNo=0.
            PaymentAllocationResult gDebt100 = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice>
                {
                    MakeSlice("B1", "HD1", t1, 300000m, 0m, "KH-X"),
                    MakeSlice("B2", "HD2", t2, 200000m, 0m, "KH-Y")
                },
                CustomerId = "KH-A",
                CashTendered = 400000m,
                AllowCustomerDebt = true
            });
            PaymentAllocationResult gDebt250 = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice>
                {
                    MakeSlice("B1", "HD1", t1, 300000m, 0m, "KH-X"),
                    MakeSlice("B2", "HD2", t2, 200000m, 0m, "KH-Y")
                },
                CustomerId = "KH-A",
                CashTendered = 250000m,
                AllowCustomerDebt = true
            });
            PaymentAllocationResult gDebtBlockedOff = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m), MakeSlice("B2", "HD2", t2, 200000m) },
                CustomerId = "KH-A",
                CashTendered = 400000m,
                AllowCustomerDebt = false
            });
            PaymentAllocationResult gDebtNoCust = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice> { MakeSlice("B1", "HD1", t1, 300000m, 0m, "KH-X"), MakeSlice("B2", "HD2", t2, 200000m, 0m, "KH-Y") },
                CustomerId = "",
                CashTendered = 400000m,
                AllowCustomerDebt = true
            });
            Check("SDDV ThanhToan §7/Quỹ §7 Gộp bàn trả thiếu 100k (bill1 giữ KH-X, bill2 nợ 100k gắn KH-A) và 250k (bill2 nợ 200k, bill1 nợ 50k đều gắn KH-A); chặn khi tắt nợ hoặc chưa chọn khách",
                gDebt100.IsValid && gDebt100.Bills[0].CongNo == 0m && gDebt100.Bills[0].FinalCustomerId == "KH-X"
                && gDebt100.Bills[1].CongNo == 100000m && gDebt100.Bills[1].FinalCustomerId == "KH-A"
                && gDebt250.IsValid && gDebt250.Bills[1].CongNo == 200000m && gDebt250.Bills[0].CongNo == 50000m
                && gDebt250.Bills[0].FinalCustomerId == "KH-A" && gDebt250.Bills[1].FinalCustomerId == "KH-A"
                && !gDebtBlockedOff.IsValid && gDebtBlockedOff.Error == ServicePaymentConstants.DebtDisabled
                && !gDebtNoCust.IsValid && gDebtNoCust.Error == ServicePaymentConstants.CustomerRequiredForDebt);

            // 11. Gộp bàn thu kèm nợ cũ (Q17, Quỹ §4.8): 300k + 200k, nợ cũ 100k của KH-A, trả 600k TM.
            PaymentAllocationResult gOldDebt = PaymentAllocationRules.Allocate(new PaymentAllocationInput
            {
                Bills = new List<PaymentBillSlice>
                {
                    MakeSlice("B1", "HD1", t1, 300000m, 0m, "KH-X"),
                    MakeSlice("B2", "HD2", t2, 200000m, 0m, "KH-Y")
                },
                CustomerId = "KH-A",
                OldDebt = 100000m,
                CashTendered = 600000m
            });
            Check("SDDV ThanhToan §7/Q17/Quỹ §4.8 Gộp bàn thu kèm nợ cũ 100k (trả 600k TM): bill1 TM 300k giữ KH-X; bill2 TM 300k CONGNO=-100k gắn KH-A",
                gOldDebt.IsValid && gOldDebt.Bills[0].Cash == 300000m && gOldDebt.Bills[0].CongNo == 0m && gOldDebt.Bills[0].FinalCustomerId == "KH-X"
                && gOldDebt.Bills[1].Cash == 300000m && gOldDebt.Bills[1].CongNo == -100000m && gOldDebt.Bills[1].FinalCustomerId == "KH-A");

            // 12. Kiểm soát số order (§3.7).
            Check("SDDV ThanhToan §3.7 Kiểm soát số order: rỗng chặn, sai định dạng chặn, '12, 16, 17,19' hợp lệ",
                PaymentAllocationRules.ValidateOrderNumbers("") == ServicePaymentConstants.OrderNumberRequired
                && PaymentAllocationRules.ValidateOrderNumbers("12, abc, 17") == ServicePaymentConstants.OrderNumberInvalid
                && PaymentAllocationRules.ValidateOrderNumbers("12, 16, 17,19") == null);
        }
    }
}
