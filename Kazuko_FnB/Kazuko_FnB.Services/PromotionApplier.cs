using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Bộ tính khuyến mại THUẦN (không chạm DB, decimal) cho 9 loại hình ở docs/specs/danh-muc-khuyen-mai.md §4:
    /// - Đợt hiệu lực: NGUNGAPDUNG &lt;&gt; 30, ngày bán nằm trong [TUNGAY, DENNGAY] (thiếu mốc = không giới hạn); v1 bỏ qua giờ.
    /// - Dòng: % sản phẩm, % nhóm (kể cả nhóm cha), giảm tiền mỗi đơn vị (GIABAN, không dưới 0), đồng giá (khi GIABAN &lt; đơn giá);
    ///   chọn mức lớn nhất, không cộng dồn; giữ giảm tay nếu lớn hơn hoặc bằng.
    /// - Tổng bill: tổng bill %, theo giá trị đơn hàng, theo số lượng; chọn mức lớn nhất; giữ giảm tay nếu lớn hơn hoặc bằng.
    /// - Tặng 0 đồng: Mua x tặng y (bán lẻ: Floor(SL/SOLUONGMUA)*SOLUONGTANG, trống = cùng món) và Tặng theo trị giá.
    /// - Không thêm công thức tiền thứ hai: mọi tính dòng và làm tròn dùng OrderCalculator.
    /// </summary>
    public static class PromotionApplier
    {
        public static bool IsPromotionActive(PromotionRow header, DateTime saleDate)
        {
            if (header == null || header.IsStopped || header.Kind == PromotionKind.Unknown)
                return false;
            if (header.Status != 0 && header.Status != PromotionText.ActiveStatus)
                return false;
            DateTime day = saleDate.Date;
            if (header.FromDate.HasValue && day < header.FromDate.Value.Date)
                return false;
            if (header.ToDate.HasValue && day > header.ToDate.Value.Date)
                return false;
            return true;
        }

        public static PromotionApplyResult Apply(PromotionApplyInput input)
        {
            var result = new PromotionApplyResult();
            if (input == null)
                return result;

            DateTime saleDate = input.SaleDate == DateTime.MinValue ? DateTime.Today : input.SaleDate.Date;
            List<ActivePromotionBundle> active = FilterActive(input.Promotions, saleDate);

            decimal sumAmount = 0m;
            decimal sumGross = 0m;
            decimal totalQty = 0m;

            if (input.Lines != null)
            {
                for (int i = 0; i < input.Lines.Count; i++)
                {
                    PromotionBillLineInput line = input.Lines[i];
                    if (line == null)
                        continue;

                    PromotionLineApplyResult lineRes = ApplyLinePromotions(line, active, input.GroupParents);
                    result.Lines.Add(lineRes);

                    sumAmount += lineRes.EffectiveAmount;
                    sumGross += line.Quantity * line.UnitPrice;
                    if (line.Quantity > 0m)
                        totalQty += line.Quantity;
                }
            }

            result.TienHang = OrderCalculator.LamTron(sumAmount, input.RoundStep);
            result.TienHangChuaGiam = OrderCalculator.LamTron(sumGross, input.RoundStep);
            result.TotalQuantity = totalQty;

            ApplyBillPromotions(input, active, result);
            CollectGiftRequests(input.Lines, active, result.TienHang, result.Gifts);
            return result;
        }

        private static List<ActivePromotionBundle> FilterActive(IList<ActivePromotionBundle> bundles, DateTime saleDate)
        {
            var list = new List<ActivePromotionBundle>();
            if (bundles == null)
                return list;
            for (int i = 0; i < bundles.Count; i++)
            {
                ActivePromotionBundle b = bundles[i];
                if (b != null && IsPromotionActive(b.Header, saleDate))
                    list.Add(b);
            }
            return list;
        }

        private static PromotionLineApplyResult ApplyLinePromotions(
            PromotionBillLineInput line,
            List<ActivePromotionBundle> activePromos,
            IDictionary<string, string> groupParents)
        {
            decimal gross = line.Quantity * line.UnitPrice;
            if (line.Quantity <= 0m || line.UnitPrice <= 0m)
                return BuildManualLineResult(line);

            decimal manualExactRate = ComputeManualLineExactRate(line, gross);
            decimal bestPromoExactRate = 0m;
            bool bestByMoney = false;
            decimal bestRate = 0m;
            decimal bestAmount = 0m;
            string bestPromoId = null;
            string bestPromoName = null;
            PromotionKind bestKind = PromotionKind.Unknown;
            HashSet<string> groupAncestors = null;

            for (int i = 0; i < activePromos.Count; i++)
            {
                ActivePromotionBundle bundle = activePromos[i];
                if (bundle.Details == null || bundle.Details.Count == 0)
                    continue;
                PromotionKind kind = bundle.Header.Kind;
                if (kind == PromotionKind.ProductGroupPercent && groupAncestors == null && !string.IsNullOrEmpty(line.ProductGroupId))
                    groupAncestors = BuildGroupAncestors(line.ProductGroupId, groupParents);

                decimal candExact;
                bool candByMoney;
                decimal candRate;
                decimal candAmt;
                if (!TryEvaluateLineBundle(kind, bundle.Details, line, groupAncestors, out candExact, out candByMoney, out candRate, out candAmt))
                    continue;

                if (candExact > bestPromoExactRate)
                {
                    bestPromoExactRate = candExact;
                    bestByMoney = candByMoney;
                    bestRate = candRate;
                    bestAmount = candAmt;
                    bestPromoId = bundle.Header.Id;
                    bestPromoName = bundle.Header.Name;
                    bestKind = kind;
                }
            }

            if (bestPromoExactRate > 0m && bestPromoExactRate > manualExactRate)
            {
                decimal outR, outD;
                decimal amt = OrderCalculator.CalcLineAmount(
                    line.Quantity, line.UnitPrice, bestRate, bestAmount, bestByMoney, out outR, out outD);
                return new PromotionLineApplyResult
                {
                    LineId = line.LineId,
                    ProductId = line.ProductId,
                    EffectiveDiscountRate = outR,
                    EffectiveDiscountAmount = outD,
                    EffectiveDiscountByMoney = bestByMoney,
                    EffectiveAmount = amt,
                    IsPromotionDiscount = true,
                    PromotionId = bestPromoId,
                    PromotionName = bestPromoName ?? "",
                    PromotionKind = bestKind
                };
            }

            return BuildManualLineResult(line);
        }

        private static bool TryEvaluateLineBundle(
            PromotionKind kind,
            List<PromotionDetailRow> details,
            PromotionBillLineInput line,
            HashSet<string> groupAncestors,
            out decimal exactRate,
            out bool byMoney,
            out decimal rate,
            out decimal amount)
        {
            exactRate = 0m;
            byMoney = false;
            rate = 0m;
            amount = 0m;

            for (int j = 0; j < details.Count; j++)
            {
                PromotionDetailRow d = details[j];
                if (d == null)
                    continue;

                decimal candExact = 0m;
                bool candByMoney = false;
                decimal candRate = 0m;
                decimal candAmt = 0m;

                if (kind == PromotionKind.ProductPercent)
                {
                    if (!SameId(d.ProductId, line.ProductId) || d.DiscountPercent <= 0m)
                        continue;
                    candExact = ClampPercent(d.DiscountPercent);
                    candRate = candExact;
                }
                else if (kind == PromotionKind.ProductGroupPercent)
                {
                    if (groupAncestors == null || string.IsNullOrEmpty(d.ProductGroupId) || d.DiscountPercent <= 0m)
                        continue;
                    if (!groupAncestors.Contains(d.ProductGroupId.Trim()))
                        continue;
                    candExact = ClampPercent(d.DiscountPercent);
                    candRate = candExact;
                }
                else if (kind == PromotionKind.ProductAmount)
                {
                    if (!SameId(d.ProductId, line.ProductId) || d.SalePrice <= 0m)
                        continue;
                    // Giảm tiền mỗi đơn vị (T1), không xuống dưới 0.
                    decimal reducePerUnit = Math.Min(d.SalePrice, line.UnitPrice);
                    candExact = reducePerUnit * 100m / line.UnitPrice;
                    candByMoney = true;
                    candRate = Math.Round(candExact, 2, MidpointRounding.AwayFromZero);
                    candAmt = reducePerUnit * line.Quantity;
                }
                else if (kind == PromotionKind.FixedPrice)
                {
                    if (!SameId(d.ProductId, line.ProductId) || d.SalePrice <= 0m || d.SalePrice >= line.UnitPrice)
                        continue;
                    decimal reducePerUnit = line.UnitPrice - d.SalePrice;
                    candExact = reducePerUnit * 100m / line.UnitPrice;
                    candByMoney = true;
                    candRate = Math.Round(candExact, 2, MidpointRounding.AwayFromZero);
                    candAmt = reducePerUnit * line.Quantity;
                }
                else
                {
                    return false;
                }

                if (candExact > exactRate)
                {
                    exactRate = candExact;
                    byMoney = candByMoney;
                    rate = candRate;
                    amount = candAmt;
                }
            }
            return exactRate > 0m;
        }

        private static PromotionLineApplyResult BuildManualLineResult(PromotionBillLineInput line)
        {
            decimal outR, outD;
            decimal amt = OrderCalculator.CalcLineAmount(
                line.Quantity, line.UnitPrice, line.ManualDiscountRate, line.ManualDiscountAmount,
                line.ManualDiscountByMoney, out outR, out outD);
            return new PromotionLineApplyResult
            {
                LineId = line.LineId,
                ProductId = line.ProductId,
                EffectiveDiscountRate = outR,
                EffectiveDiscountAmount = outD,
                EffectiveDiscountByMoney = line.ManualDiscountByMoney,
                EffectiveAmount = amt,
                IsPromotionDiscount = false
            };
        }

        private static void ApplyBillPromotions(
            PromotionApplyInput input,
            List<ActivePromotionBundle> activePromos,
            PromotionApplyResult result)
        {
            decimal tienHang = result.TienHang;
            decimal totalQty = result.TotalQuantity;

            decimal bestRate = 0m;
            string bestPromoId = null;
            string bestPromoName = null;
            PromotionKind bestKind = PromotionKind.Unknown;

            if (tienHang > 0m)
            {
                for (int i = 0; i < activePromos.Count; i++)
                {
                    ActivePromotionBundle bundle = activePromos[i];
                    PromotionKind kind = bundle.Header.Kind;
                    decimal candidate = 0m;

                    if (kind == PromotionKind.TotalBillPercent && bundle.Header.DiscountPercent > 0m)
                    {
                        candidate = ClampPercent(bundle.Header.DiscountPercent);
                    }
                    else if (kind == PromotionKind.OrderValuePercent)
                    {
                        PromotionDetailRow bestTier = FindBestOrderValueDetail(bundle.Details, tienHang, false);
                        if (bestTier != null && bestTier.DiscountPercent > 0m)
                            candidate = ClampPercent(bestTier.DiscountPercent);
                    }
                    else if (kind == PromotionKind.QuantityPercent && totalQty > 0m)
                    {
                        PromotionDetailRow bestTier = FindBestQuantityDetail(bundle.Details, totalQty);
                        if (bestTier != null && bestTier.DiscountPercent > 0m)
                            candidate = ClampPercent(bestTier.DiscountPercent);
                    }

                    if (candidate > bestRate)
                    {
                        bestRate = candidate;
                        bestPromoId = bundle.Header.Id;
                        bestPromoName = bundle.Header.Name;
                        bestKind = kind;
                    }
                }
            }

            decimal manualExactRate = ComputeManualBillExactRate(input, tienHang);
            if (bestRate > 0m && bestRate > manualExactRate)
            {
                result.UsePromotionBillDiscount = true;
                result.EffectiveBillDiscountByMoney = false;
                result.EffectiveBillDiscountRate = bestRate;
                result.EffectiveBillDiscountAmount = OrderCalculator.LamTron(bestRate * tienHang / 100m, input.RoundStep);
                result.BillPromotionId = bestPromoId;
                result.BillPromotionName = bestPromoName ?? "";
                result.BillPromotionKind = bestKind;
                return;
            }

            result.UsePromotionBillDiscount = false;
            result.EffectiveBillDiscountByMoney = input.ManualBillDiscountByMoney;
            if (input.ManualBillDiscountByMoney)
            {
                decimal amt = Math.Max(0m, input.ManualBillDiscountAmount);
                result.EffectiveBillDiscountAmount = amt;
                result.EffectiveBillDiscountRate = tienHang == 0m
                    ? 0m
                    : Math.Round(amt * 100m / tienHang, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                decimal rate = Math.Max(0m, input.ManualBillDiscountRate);
                result.EffectiveBillDiscountRate = rate;
                result.EffectiveBillDiscountAmount = OrderCalculator.LamTron(rate * tienHang / 100m, input.RoundStep);
            }
        }

        private static void CollectGiftRequests(
            IList<PromotionBillLineInput> lines,
            List<ActivePromotionBundle> activePromos,
            decimal tienHang,
            List<PromotionGiftRequest> gifts)
        {
            for (int i = 0; i < activePromos.Count; i++)
            {
                ActivePromotionBundle bundle = activePromos[i];
                if (bundle.Details == null || bundle.Details.Count == 0)
                    continue;
                PromotionKind kind = bundle.Header.Kind;

                if (kind == PromotionKind.BuyXGetY && lines != null)
                {
                    var qtyByProduct = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    for (int li = 0; li < lines.Count; li++)
                    {
                        PromotionBillLineInput line = lines[li];
                        if (line == null || line.Quantity <= 0m || string.IsNullOrEmpty(line.ProductId))
                            continue;
                        string key = line.ProductId.Trim();
                        decimal cur;
                        qtyByProduct.TryGetValue(key, out cur);
                        qtyByProduct[key] = cur + line.Quantity;
                    }
                    for (int j = 0; j < bundle.Details.Count; j++)
                    {
                        PromotionDetailRow d = bundle.Details[j];
                        if (d == null || string.IsNullOrEmpty(d.ProductId) || d.BuyQuantity <= 0m || d.GiftQuantity <= 0m)
                            continue;
                        decimal boughtQty;
                        if (!qtyByProduct.TryGetValue(d.ProductId.Trim(), out boughtQty) || boughtQty < d.BuyQuantity)
                            continue;
                        // Công thức bán lẻ (Chiến chốt): Floor(SL / SOLUONGMUA) * SOLUONGTANG.
                        decimal giftQty = Math.Floor(boughtQty / d.BuyQuantity) * d.GiftQuantity;
                        if (giftQty <= 0m)
                            continue;
                        string giftProdId = string.IsNullOrEmpty(d.GiftProductId) ? d.ProductId : d.GiftProductId;
                        MergeGiftRequest(gifts, giftProdId, giftQty, bundle.Header.Id, bundle.Header.Name, kind);
                    }
                }
                else if (kind == PromotionKind.GiftByOrderValue && tienHang > 0m)
                {
                    PromotionDetailRow bestTier = FindBestOrderValueDetail(bundle.Details, tienHang, true);
                    if (bestTier != null && bestTier.GiftQuantity > 0m && !string.IsNullOrEmpty(bestTier.GiftProductId))
                        MergeGiftRequest(gifts, bestTier.GiftProductId, bestTier.GiftQuantity, bundle.Header.Id, bundle.Header.Name, kind);
                }
            }
        }

        private static void MergeGiftRequest(
            List<PromotionGiftRequest> gifts, string productId, decimal qty, string promoId, string promoName, PromotionKind kind)
        {
            if (string.IsNullOrEmpty(productId) || qty <= 0m)
                return;
            for (int i = 0; i < gifts.Count; i++)
            {
                PromotionGiftRequest existing = gifts[i];
                if (SameId(existing.ProductId, productId) && SameId(existing.PromotionId, promoId))
                {
                    existing.Quantity += qty;
                    return;
                }
            }
            gifts.Add(new PromotionGiftRequest
            {
                ProductId = productId,
                Quantity = qty,
                PromotionId = promoId ?? "",
                PromotionName = promoName ?? "",
                PromotionKind = kind
            });
        }

        private static PromotionDetailRow FindBestOrderValueDetail(List<PromotionDetailRow> details, decimal tienHang, bool requireGift)
        {
            if (details == null || tienHang <= 0m)
                return null;
            PromotionDetailRow best = null;
            for (int i = 0; i < details.Count; i++)
            {
                PromotionDetailRow d = details[i];
                if (d == null || d.OrderValue <= 0m || d.OrderValue > tienHang)
                    continue;
                if (requireGift && (d.GiftQuantity <= 0m || string.IsNullOrEmpty(d.GiftProductId)))
                    continue;
                if (best == null || d.OrderValue > best.OrderValue)
                    best = d;
            }
            return best;
        }

        private static PromotionDetailRow FindBestQuantityDetail(List<PromotionDetailRow> details, decimal totalQty)
        {
            if (details == null || totalQty <= 0m)
                return null;
            PromotionDetailRow best = null;
            for (int i = 0; i < details.Count; i++)
            {
                PromotionDetailRow d = details[i];
                if (d == null || d.ItemQuantity <= 0m || d.ItemQuantity > totalQty)
                    continue;
                if (best == null || d.ItemQuantity > best.ItemQuantity)
                    best = d;
            }
            return best;
        }

        private static HashSet<string> BuildGroupAncestors(string groupId, IDictionary<string, string> groupParents)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string cur = (groupId ?? "").Trim();
            int guard = 0;
            while (cur.Length > 0 && guard < 64 && set.Add(cur))
            {
                guard++;
                string parent;
                if (groupParents == null || !groupParents.TryGetValue(cur, out parent) || string.IsNullOrWhiteSpace(parent))
                    break;
                cur = parent.Trim();
            }
            return set;
        }

        private static decimal ComputeManualLineExactRate(PromotionBillLineInput line, decimal gross)
        {
            if (gross <= 0m) return 0m;
            if (line.ManualDiscountByMoney)
                return Math.Max(0m, Math.Min(gross, line.ManualDiscountAmount)) * 100m / gross;
            return ClampPercent(line.ManualDiscountRate);
        }

        private static decimal ComputeManualBillExactRate(PromotionApplyInput input, decimal tienHang)
        {
            if (input.ManualBillDiscountByMoney)
                return tienHang <= 0m ? 0m : Math.Max(0m, input.ManualBillDiscountAmount) * 100m / tienHang;
            return ClampPercent(input.ManualBillDiscountRate);
        }

        private static decimal ClampPercent(decimal value)
        {
            if (value <= 0m) return 0m;
            return value > 100m ? 100m : value;
        }

        private static bool SameId(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
                && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
