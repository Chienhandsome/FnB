using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PromotionApplyTests(string db)
        {
            PromotionApplierPureLineTests();
            PromotionApplierPureBillAndGiftTests();
            PromotionApplierPureMaxAndStatusTests();
            PromotionApplyDbIntegrationTests(db);
            PromotionApplyUiTests(db);
        }

        private static ActivePromotionBundle MakeBundle(
            string id, string name, string typeName,
            DateTime? from, DateTime? to, int stoppedFlag, decimal headerPct,
            params PromotionDetailRow[] details)
        {
            var b = new ActivePromotionBundle
            {
                Header = new PromotionRow
                {
                    Id = id,
                    Name = name,
                    TypeName = typeName,
                    FromDate = from,
                    ToDate = to,
                    StoppedFlag = stoppedFlag,
                    DiscountPercent = headerPct,
                    Status = 30
                }
            };
            if (details != null)
                b.Details.AddRange(details);
            return b;
        }

        private static void PromotionApplierPureLineTests()
        {
            DateTime d = new DateTime(2026, 6, 15);
            var groupParents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "G_ROOT", "" },
                { "G_CHILD", "G_ROOT" },
                { "G_GRAND", "G_CHILD" },
                { "G_OTHER", "" }
            };

            // §6.1: Giảm % theo sản phẩm 10% cho món A (50.000 x 2 = 90.000), món khác không đổi; theo nhóm gồm nhóm con/cháu.
            var pProdPct = MakeBundle("P1", "Giảm 10% món A", "Giảm giá % theo sản phẩm", d.AddDays(-1), d.AddDays(1), 0, 0m,
                new PromotionDetailRow { ProductId = "A", DiscountPercent = 10m });
            var pGrpPct = MakeBundle("P2", "Giảm 15% nhóm gốc", "Giảm giá theo nhóm hàng", d.AddDays(-1), d.AddDays(1), 0, 0m,
                new PromotionDetailRow { ProductGroupId = "G_ROOT", DiscountPercent = 15m });

            PromotionApplyResult r1 = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d,
                RoundStep = 1000m,
                GroupParents = groupParents,
                Promotions = new List<ActivePromotionBundle> { pProdPct, pGrpPct },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "L1", ProductId = "A", ProductGroupId = "G_OTHER", Quantity = 2m, UnitPrice = 50000m },
                    new PromotionBillLineInput { LineId = "L2", ProductId = "B", ProductGroupId = "G_GRAND", Quantity = 2m, UnitPrice = 40000m },
                    new PromotionBillLineInput { LineId = "L3", ProductId = "C", ProductGroupId = "G_OTHER", Quantity = 1m, UnitPrice = 30000m }
                }
            });
            Check("KM P2 §6.1 (thuần): % SP 10% món A (50k x 2 -> 90k), % nhóm 15% áp cả nhóm con/cháu (40k x 2 -> 68k), món khác không đổi",
                r1.Lines.Count == 3
                && r1.Lines[0].EffectiveDiscountRate == 10m && r1.Lines[0].EffectiveAmount == 90000m && r1.Lines[0].IsPromotionDiscount
                && r1.Lines[1].EffectiveDiscountRate == 15m && r1.Lines[1].EffectiveAmount == 68000m && r1.Lines[1].IsPromotionDiscount
                && r1.Lines[2].EffectiveDiscountRate == 0m && r1.Lines[2].EffectiveAmount == 30000m && !r1.Lines[2].IsPromotionDiscount,
                "A=" + r1.Lines[0].EffectiveAmount + ", B=" + r1.Lines[1].EffectiveAmount + ", C=" + r1.Lines[2].EffectiveAmount);

            // §6.2: Giảm tiền 5.000/đơn vị (SL 2 giảm 10.000, không xuống dưới 0); đồng giá 30.000 cho món 50.000 -> 60.000; đồng giá cao hơn bỏ qua.
            var pAmt = MakeBundle("P3", "Giảm tiền SP", "Giảm giá tiền theo sản phẩm", d, d, 0, 0m,
                new PromotionDetailRow { ProductId = "A", SalePrice = 5000m },
                new PromotionDetailRow { ProductId = "CHEAP", SalePrice = 60000m });
            var pFixed = MakeBundle("P4", "Đồng giá", "Đồng giá", d, d, 0, 0m,
                new PromotionDetailRow { ProductId = "D", SalePrice = 30000m },
                new PromotionDetailRow { ProductId = "E", SalePrice = 60000m });

            PromotionApplyResult r2 = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d,
                RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pAmt, pFixed },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "LA", ProductId = "A", Quantity = 2m, UnitPrice = 50000m },
                    new PromotionBillLineInput { LineId = "LC", ProductId = "CHEAP", Quantity = 2m, UnitPrice = 20000m },
                    new PromotionBillLineInput { LineId = "LD", ProductId = "D", Quantity = 2m, UnitPrice = 50000m },
                    new PromotionBillLineInput { LineId = "LE", ProductId = "E", Quantity = 2m, UnitPrice = 50000m }
                }
            });
            Check("KM P2 §6.2 (thuần): giảm tiền 5k/đv x 2 = giảm 10k (90k) và chặn 0đ; đồng giá 30k cho món 50k x 2 = 60k; đồng giá 60k > 50k không tác dụng",
                r2.Lines[0].EffectiveDiscountAmount == 10000m && r2.Lines[0].EffectiveAmount == 90000m && r2.Lines[0].IsPromotionDiscount
                && r2.Lines[1].EffectiveAmount == 0m && r2.Lines[1].EffectiveDiscountRate == 100m && r2.Lines[1].IsPromotionDiscount
                && r2.Lines[2].EffectiveAmount == 60000m && r2.Lines[2].EffectiveDiscountRate == 40m && r2.Lines[2].IsPromotionDiscount
                && r2.Lines[3].EffectiveAmount == 100000m && r2.Lines[3].EffectiveDiscountRate == 0m && !r2.Lines[3].IsPromotionDiscount,
                "A=" + r2.Lines[0].EffectiveAmount + ", cheap=" + r2.Lines[1].EffectiveAmount + ", D=" + r2.Lines[2].EffectiveAmount + ", E=" + r2.Lines[3].EffectiveAmount);
        }

        private static void PromotionApplierPureBillAndGiftTests()
        {
            DateTime d = new DateTime(2026, 6, 15);

            // §6.3: Tổng bill 10%; theo giá trị 200k (5%) / 500k (8%): 520k -> 8%, 250k -> 5%, 150k -> 0%; theo SL; giữ giảm tay lớn hơn.
            var pTotal = MakeBundle("PB1", "Tổng bill 10%", "Giảm giá tổng bill", null, null, 0, 10m);
            PromotionApplyResult rTot = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pTotal },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = 2m, UnitPrice = 100000m }
                }
            });
            PromotionApplyResult rTotManualWins = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                ManualBillDiscountRate = 15m,
                Promotions = new List<ActivePromotionBundle> { pTotal },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = 2m, UnitPrice = 100000m }
                }
            });

            var pVal = MakeBundle("PB2", "Theo giá trị", "Giảm theo giá trị đơn hàng", d, d, 0, 0m,
                new PromotionDetailRow { OrderValue = 200000m, DiscountPercent = 5m },
                new PromotionDetailRow { OrderValue = 500000m, DiscountPercent = 8m });
            Func<decimal, PromotionApplyResult> evalVal = amount => PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pVal },
                Lines = new List<PromotionBillLineInput> { new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = 1m, UnitPrice = amount } }
            });
            PromotionApplyResult v520 = evalVal(520000m);
            PromotionApplyResult v250 = evalVal(250000m);
            PromotionApplyResult v150 = evalVal(150000m);

            var pQty = MakeBundle("PB3", "Theo SL", "Giảm theo số lượng mặt hàng", d, d, 0, 0m,
                new PromotionDetailRow { ItemQuantity = 3m, DiscountPercent = 5m },
                new PromotionDetailRow { ItemQuantity = 6m, DiscountPercent = 12m });
            Func<decimal, PromotionApplyResult> evalQty = qty => PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pQty },
                Lines = new List<PromotionBillLineInput> { new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = qty, UnitPrice = 20000m } }
            });
            PromotionApplyResult q7 = evalQty(7m);
            PromotionApplyResult q4 = evalQty(4m);
            PromotionApplyResult q2 = evalQty(2m);

            Check("KM P2 §6.3 (thuần): tổng bill 10%, giá trị 520k->8%/250k->5%/150k->0%, SL 7->12%/4->5%/2->0%, giảm tay 15% > KM 10% giữ tay",
                rTot.UsePromotionBillDiscount && rTot.EffectiveBillDiscountRate == 10m && rTot.EffectiveBillDiscountAmount == 20000m
                && !rTotManualWins.UsePromotionBillDiscount && rTotManualWins.EffectiveBillDiscountRate == 15m
                && v520.EffectiveBillDiscountRate == 8m && v250.EffectiveBillDiscountRate == 5m && v150.EffectiveBillDiscountRate == 0m
                && q7.EffectiveBillDiscountRate == 12m && q4.EffectiveBillDiscountRate == 5m && q2.EffectiveBillDiscountRate == 0m,
                "v520=" + v520.EffectiveBillDiscountRate + ", v250=" + v250.EffectiveBillDiscountRate + ", v150=" + v150.EffectiveBillDiscountRate);

            // §6.4: Mua 2 tặng 1 (bán lẻ): mua 5 -> tặng 2, mua 3 -> tặng 1, mua 1 -> không tặng; trống DMATHANGTANGID -> cùng món.
            var pBuyGet = MakeBundle("PG1", "Mua 2 tặng 1", "Mua x sản phẩm tặng y sản phẩm", d, d, 0, 0m,
                new PromotionDetailRow { ProductId = "A", BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = "GIFT" },
                new PromotionDetailRow { ProductId = "SAME", BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = "" });
            Func<string, decimal, PromotionApplyResult> evalBuy = (pid, qty) => PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pBuyGet },
                Lines = new List<PromotionBillLineInput> { new PromotionBillLineInput { LineId = "L1", ProductId = pid, Quantity = qty, UnitPrice = 30000m } }
            });
            PromotionApplyResult b5 = evalBuy("A", 5m);
            PromotionApplyResult b3 = evalBuy("A", 3m);
            PromotionApplyResult b1 = evalBuy("A", 1m);
            PromotionApplyResult bSame = evalBuy("SAME", 4m);

            // §6.5: Tặng theo trị giá: 300k >= 200k -> tặng 1; 150k -> không tặng; 550k vượt 2 mức (200k & 500k) -> chọn mức 500k.
            var pGiftVal = MakeBundle("PG2", "Tặng trị giá", "Tặng theo trị giá", d, d, 0, 0m,
                new PromotionDetailRow { OrderValue = 200000m, GiftQuantity = 1m, GiftProductId = "G1" },
                new PromotionDetailRow { OrderValue = 500000m, GiftQuantity = 2m, GiftProductId = "G2" });
            Func<decimal, PromotionApplyResult> evalGiftVal = amt => PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pGiftVal },
                Lines = new List<PromotionBillLineInput> { new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = 1m, UnitPrice = amt } }
            });
            PromotionApplyResult gv300 = evalGiftVal(300000m);
            PromotionApplyResult gv150 = evalGiftVal(150000m);
            PromotionApplyResult gv550 = evalGiftVal(550000m);

            Check("KM P2 §6.4 & §6.5 (thuần): Mua 2 tặng 1 (5->2, 3->1, 1->0, trống=cùng món) và Tặng theo trị giá (300k->G1x1, 150k->0, 550k->G2x2)",
                b5.Gifts.Count == 1 && b5.Gifts[0].ProductId == "GIFT" && b5.Gifts[0].Quantity == 2m
                && b3.Gifts.Count == 1 && b3.Gifts[0].Quantity == 1m
                && b1.Gifts.Count == 0
                && bSame.Gifts.Count == 1 && bSame.Gifts[0].ProductId == "SAME" && bSame.Gifts[0].Quantity == 2m
                && gv300.Gifts.Count == 1 && gv300.Gifts[0].ProductId == "G1" && gv300.Gifts[0].Quantity == 1m
                && gv150.Gifts.Count == 0
                && gv550.Gifts.Count == 1 && gv550.Gifts[0].ProductId == "G2" && gv550.Gifts[0].Quantity == 2m,
                "b5=" + (b5.Gifts.Count == 1 ? b5.Gifts[0].Quantity.ToString() : "0") + ", gv550=" + (gv550.Gifts.Count == 1 ? gv550.Gifts[0].ProductId : "none"));
        }

        private static void PromotionApplierPureMaxAndStatusTests()
        {
            DateTime d = new DateTime(2026, 6, 15);
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "GC", "GP" }, { "GP", "" } };

            // §6.6: Nhiều loại giảm % cùng áp 1 dòng (10% SP, 15% nhóm, 20% đồng giá 40k/50k) -> chọn Max 20%, không cộng dồn;
            // giảm tay 25% > 20% -> giữ tay; tổng bill 5% vs giá trị 8% vs SL 12% -> chọn Max 12%, không cộng dồn.
            var pLine10 = MakeBundle("M1", "SP 10%", "Giảm giá % theo sản phẩm", d, d, 0, 0m,
                new PromotionDetailRow { ProductId = "A", DiscountPercent = 10m });
            var pGrp15 = MakeBundle("M2", "Nhóm 15%", "Giảm giá theo nhóm hàng", d, d, 0, 0m,
                new PromotionDetailRow { ProductGroupId = "GP", DiscountPercent = 15m });
            var pFix20 = MakeBundle("M3", "Đồng giá 40k", "Đồng giá", d, d, 0, 0m,
                new PromotionDetailRow { ProductId = "A", SalePrice = 40000m });
            var pBill5 = MakeBundle("M4", "Bill 5%", "Giảm giá tổng bill", d, d, 0, 5m);
            var pVal8 = MakeBundle("M5", "GT 8%", "Giảm theo giá trị đơn hàng", d, d, 0, 0m,
                new PromotionDetailRow { OrderValue = 100000m, DiscountPercent = 8m });
            var pQty12 = MakeBundle("M6", "SL 12%", "Giảm theo số lượng mặt hàng", d, d, 0, 0m,
                new PromotionDetailRow { ItemQuantity = 3m, DiscountPercent = 12m });

            PromotionApplyResult rMax = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m, GroupParents = parents,
                Promotions = new List<ActivePromotionBundle> { pLine10, pGrp15, pFix20, pBill5, pVal8, pQty12 },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "L1", ProductId = "A", ProductGroupId = "GC", Quantity = 2m, UnitPrice = 50000m },
                    new PromotionBillLineInput { LineId = "L2", ProductId = "A", ProductGroupId = "GC", Quantity = 2m, UnitPrice = 50000m, ManualDiscountRate = 25m }
                }
            });

            // §6.6: Đợt ngừng (30), hết hạn, chưa tới -> không áp dụng; thiếu mốc ngày -> vẫn áp dụng.
            var pStopped = MakeBundle("S1", "Ngừng", "Giảm giá tổng bill", d.AddDays(-5), d.AddDays(5), 30, 50m);
            var pExpired = MakeBundle("S2", "Hết hạn", "Giảm giá tổng bill", d.AddDays(-10), d.AddDays(-1), 0, 40m);
            var pUpcoming = MakeBundle("S3", "Chưa tới", "Giảm giá tổng bill", d.AddDays(1), d.AddDays(10), 0, 30m);
            PromotionApplyResult rNone = PromotionApplier.Apply(new PromotionApplyInput
            {
                SaleDate = d, RoundStep = 1000m,
                Promotions = new List<ActivePromotionBundle> { pStopped, pExpired, pUpcoming },
                Lines = new List<PromotionBillLineInput>
                {
                    new PromotionBillLineInput { LineId = "L1", ProductId = "A", Quantity = 2m, UnitPrice = 50000m }
                }
            });

            // §6.7: Kết quả PromotionApplier đưa qua OrderCalculator cho thuế + phí dịch vụ đúng.
            ServiceOrderTotals totals = OrderCalculator.CalcTotalsFromSubtotals(
                rMax.TienHang, rMax.TienHangChuaGiam,
                rMax.EffectiveBillDiscountRate, rMax.EffectiveBillDiscountAmount, rMax.EffectiveBillDiscountByMoney,
                5m, 0m, false, 10m, 0m, 1000m);
            // L1: 80k + L2: 75k = 155k; giảm tổng 12% = 18.6k -> 19k; sauGiam = 136k; phí 5% = 6.8k -> 7k; thuế 10%*(143k)=14.3k->14k; tổng=157k.
            Check("KM P2 §6.6 & §6.7 (thuần): chọn Max không cộng dồn (dòng 20%, bill 12%), giữ tay 25%, bỏ đợt ngừng/hết hạn/chưa tới, qua OrderCalculator đúng",
                rMax.Lines[0].EffectiveDiscountRate == 20m && rMax.Lines[0].EffectiveAmount == 80000m && rMax.Lines[0].IsPromotionDiscount
                && rMax.Lines[1].EffectiveDiscountRate == 25m && rMax.Lines[1].EffectiveAmount == 75000m && !rMax.Lines[1].IsPromotionDiscount
                && rMax.EffectiveBillDiscountRate == 12m && rMax.UsePromotionBillDiscount
                && rNone.EffectiveBillDiscountRate == 0m && !rNone.UsePromotionBillDiscount
                && totals.TienHang == 155000m && totals.TienGiamGia == 19000m && totals.PhiDichVu == 7000m
                && totals.TienThue == 14000m && totals.TongCong == 157000m,
                "l0=" + rMax.Lines[0].EffectiveDiscountRate + ", bill=" + rMax.EffectiveBillDiscountRate + ", tc=" + totals.TongCong);
        }
    }
}
