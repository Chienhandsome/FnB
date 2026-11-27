using System;
using System.Collections.Generic;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        private sealed class AcceptedGiftItem
        {
            public PromotionGiftRequest Request;
            public ServiceMenuItem Product;
            public string WarehouseId;
            public string Note;
        }

        /// <summary>
        /// Đồng bộ các dòng tặng 0 đồng (Mua x tặng y / Tặng theo trị giá) vào TDONHANGCHITIET (KHUYENMAI = 30, DONGIA = 0):
        /// dòng tặng vẫn trừ kho và chịu chặn âm kho như dòng bán thường; không đủ tồn thì bỏ qua dòng tặng và trả cảnh báo rõ.
        /// </summary>
        private static string SyncGiftLinesIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            List<ServiceOrderLine> existingGifts,
            List<PromotionGiftRequest> desiredGifts,
            bool allowNegativeStock,
            string actorId,
            DateTime now)
        {
            var warnings = new List<string>();
            var accepted = new List<AcceptedGiftItem>();

            Dictionary<string, decimal> committedDemand = null;
            if (!allowNegativeStock && desiredGifts != null && desiredGifts.Count > 0)
                committedDemand = BuildNonGiftTrackedDemandIn(conn, tx, header);

            if (desiredGifts != null)
            {
                for (int i = 0; i < desiredGifts.Count; i++)
                {
                    PromotionGiftRequest gift = desiredGifts[i];
                    if (gift == null || string.IsNullOrEmpty(gift.ProductId) || gift.Quantity <= 0m)
                        continue;

                    ServiceMenuItem giftProd = ServiceOrderRepository.LoadProductIn(conn, tx, gift.ProductId);
                    if (giftProd == null || giftProd.Status != 30)
                    {
                        warnings.Add("Không thể thêm hàng tặng khuyến mại do mặt hàng tặng không còn tồn tại.");
                        continue;
                    }
                    if (giftProd.TempLocked)
                    {
                        warnings.Add("Không thể thêm hàng tặng khuyến mại '" + giftProd.Name + "' do mặt hàng đang tạm khóa.");
                        continue;
                    }

                    string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, header.WarehouseId);
                    if (!allowNegativeStock && !string.IsNullOrEmpty(warehouseId))
                    {
                        string stockWarn = TryReserveGiftStockIn(conn, tx, header.Id, giftProd, gift.Quantity, warehouseId, committedDemand);
                        if (stockWarn != null)
                        {
                            warnings.Add(stockWarn);
                            continue;
                        }
                    }

                    accepted.Add(new AcceptedGiftItem
                    {
                        Request = gift,
                        Product = giftProd,
                        WarehouseId = warehouseId,
                        Note = PromotionRules.ClipLogText(FormatPromotionTag(gift.PromotionName))
                    });
                }
            }

            var usedExisting = new HashSet<string>(StringComparer.Ordinal);
            foreach (AcceptedGiftItem item in accepted)
            {
                ServiceOrderLine match = FindReusableGiftLine(existingGifts, usedExisting, item.Product.Id, item.Note);
                if (match != null)
                {
                    usedExisting.Add(match.Id);
                    bool changed = match.Quantity != item.Request.Quantity
                        || match.UnitPrice != 0m
                        || match.DiscountRate != 0m
                        || match.DiscountAmount != 0m
                        || match.DiscountByMoney
                        || match.Amount != 0m
                        || match.KhuyenMai != 30
                        || !string.Equals(match.Note ?? "", item.Note ?? "", StringComparison.Ordinal);

                    match.Quantity = item.Request.Quantity;
                    match.UnitPrice = 0m;
                    match.DiscountRate = 0m;
                    match.DiscountAmount = 0m;
                    match.DiscountByMoney = false;
                    match.Amount = 0m;
                    match.KhuyenMai = 30;
                    match.Note = item.Note;
                    match.PromotionLabel = item.Note;

                    if (changed)
                    {
                        ServiceOrderRepository.UpdateLineIn(conn, tx, match, actorId, now);
                        if (item.Product.ProductTypeId == ProductRules.TypeCombo && !string.IsNullOrEmpty(match.ComboId))
                            ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, header.Id, match.ComboId, match.Quantity, actorId, now);
                    }
                }
                else
                {
                    InsertGiftLineIn(conn, tx, header, item, actorId, now);
                }
            }

            foreach (ServiceOrderLine oldGift in existingGifts)
            {
                if (oldGift == null || usedExisting.Contains(oldGift.Id))
                    continue;
                ServiceOrderRepository.DeleteLineAndComboChildrenIn(conn, tx, header.Id, oldGift.Id, oldGift.ComboId);
                header.Lines.RemoveAll(x =>
                    x.Id == oldGift.Id || (!string.IsNullOrEmpty(oldGift.ComboId) && x.ComboParentId == oldGift.ComboId));
            }

            return warnings.Count > 0 ? string.Join(" ", warnings.ToArray()) : null;
        }

        private static Dictionary<string, decimal> BuildNonGiftTrackedDemandIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header)
        {
            var demand = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (header == null || header.Lines == null)
                return demand;

            foreach (ServiceOrderLine line in header.Lines)
            {
                if (line == null || line.IsMaterialExport || line.IsPromoGift || string.IsNullOrEmpty(line.ProductId) || line.Quantity <= 0m)
                    continue;
                if (line.ProductTypeId == ProductRules.TypeCombo || line.ProductTypeId == ProductRules.TypeComboOpen)
                    continue;

                string whId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? header.WarehouseId);
                if (string.IsNullOrEmpty(whId))
                    continue;

                ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                if (prod == null)
                    continue;

                var lineNeed = new Dictionary<string, decimal>(StringComparer.Ordinal);
                var lineNames = new Dictionary<string, string>(StringComparer.Ordinal);
                CollectTrackedDemandIn(conn, tx, prod, line.Quantity, null, lineNeed, lineNames, 0);
                foreach (KeyValuePair<string, decimal> kv in lineNeed)
                {
                    string key = whId + "|" + kv.Key;
                    decimal cur;
                    demand.TryGetValue(key, out cur);
                    demand[key] = cur + kv.Value;
                }
            }
            return demand;
        }

        private static string TryReserveGiftStockIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            ServiceMenuItem giftProd,
            decimal giftQty,
            string warehouseId,
            Dictionary<string, decimal> committedDemand)
        {
            var giftNeed = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var giftNames = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectTrackedDemandIn(conn, tx, giftProd, giftQty, null, giftNeed, giftNames, 0);
            if (giftNeed.Count == 0)
                return null;

            foreach (KeyValuePair<string, decimal> kv in giftNeed)
            {
                string key = warehouseId + "|" + kv.Key;
                decimal existingNeed;
                committedDemand.TryGetValue(key, out existingNeed);
                decimal totalNeed = existingNeed + kv.Value;
                decimal available = ServiceOrderRepository.GetStockBalanceIn(conn, tx, kv.Key, warehouseId, billId);
                if (available < totalNeed)
                {
                    return string.Format(
                        "Không thể thêm hàng tặng khuyến mại '{0}' (SL: {1}) do '{2}' không đủ tồn kho (tồn hiện có: {3}, cần xuất: {4}).",
                        giftProd.Name ?? "",
                        FormatPromoQty(giftQty),
                        giftNames[kv.Key] ?? "",
                        FormatPromoQty(available),
                        FormatPromoQty(totalNeed));
                }
            }

            foreach (KeyValuePair<string, decimal> kv in giftNeed)
            {
                string key = warehouseId + "|" + kv.Key;
                decimal existingNeed;
                committedDemand.TryGetValue(key, out existingNeed);
                committedDemand[key] = existingNeed + kv.Value;
            }
            return null;
        }

        private static ServiceOrderLine FindReusableGiftLine(
            List<ServiceOrderLine> existingGifts,
            HashSet<string> usedIds,
            string productId,
            string note)
        {
            ServiceOrderLine fallback = null;
            foreach (ServiceOrderLine g in existingGifts)
            {
                if (g == null || usedIds.Contains(g.Id))
                    continue;
                if (!string.Equals(g.ProductId ?? "", productId ?? "", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(g.Note ?? "", note ?? "", StringComparison.Ordinal))
                    return g;
                if (fallback == null)
                    fallback = g;
            }
            return fallback;
        }

        private static void InsertGiftLineIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            AcceptedGiftItem item,
            string actorId,
            DateTime now)
        {
            string comboId = DbHelper.NewId();
            var giftLine = new ServiceOrderLine
            {
                Id = DbHelper.NewId(),
                OrderId = header.Id,
                ProductId = item.Product.Id,
                ProductGroupId = item.Product.GroupId,
                ProductTypeId = item.Product.ProductTypeId,
                DrinkTypeId = item.Product.DrinkTypeId,
                ItemName = item.Product.Name,
                UnitId = item.Product.UnitId,
                UnitName = item.Product.UnitName,
                WarehouseId = item.WarehouseId,
                Quantity = item.Request.Quantity,
                UnitPrice = 0m,
                CostPrice = item.Product.CostPrice,
                DiscountRate = 0m,
                DiscountAmount = 0m,
                DiscountByMoney = false,
                Amount = 0m,
                KhuyenMai = 30,
                Note = item.Note,
                PromotionLabel = item.Note,
                ComboId = comboId,
                ComboParentId = "",
                ComboQty = 0m
            };
            ServiceOrderRepository.InsertLineIn(conn, tx, giftLine, actorId, now);
            header.Lines.Add(giftLine);

            if (item.Product.ProductTypeId == ProductRules.TypeCombo)
            {
                List<ServiceRecipeComponent> comps = ServiceOrderRepository.LoadRecipeIn(conn, tx, item.Product.Id);
                foreach (ServiceRecipeComponent c in comps)
                {
                    if (string.IsNullOrEmpty(c.MaterialId) || c.Quantity <= 0m)
                        continue;
                    var child = new ServiceOrderLine
                    {
                        Id = DbHelper.NewId(),
                        OrderId = header.Id,
                        ProductId = c.MaterialId,
                        ProductTypeId = c.MaterialTypeId,
                        ItemName = c.MaterialName,
                        UnitId = c.UnitId,
                        WarehouseId = item.WarehouseId,
                        Quantity = item.Request.Quantity * c.Quantity,
                        UnitPrice = 0m,
                        CostPrice = 0m,
                        DiscountRate = 0m,
                        DiscountAmount = 0m,
                        DiscountByMoney = false,
                        Amount = 0m,
                        KhuyenMai = 30,
                        Note = "",
                        ComboId = DbHelper.NewId(),
                        ComboParentId = comboId,
                        ComboQty = c.Quantity
                    };
                    ServiceOrderRepository.InsertLineIn(conn, tx, child, actorId, now);
                    header.Lines.Add(child);
                }
            }
        }

        private static string FormatPromoQty(decimal value)
        {
            return value == Math.Truncate(value)
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
