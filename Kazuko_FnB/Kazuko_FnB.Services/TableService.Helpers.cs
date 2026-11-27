using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ Nội bộ dùng chung

        private void AddLineCoreIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            ServiceMenuItem product,
            decimal quantity,
            decimal unitPrice,
            string itemName,
            string unitId,
            string note,
            List<ServiceRecipeComponent> openComboComponents,
            DateTime now)
        {
            string cleanNote = (note ?? "").Trim();
            bool isOpenItem = product.ProductTypeId == ProductRules.TypeOpen;
            bool isCombo = product.ProductTypeId == ProductRules.TypeCombo;
            bool isComboOpen = product.ProductTypeId == ProductRules.TypeComboOpen;
            bool allowMerge = !SystemConfig.NhapMotMatHangNhieuLanTrongPhieu && !isOpenItem && !isComboOpen && quantity > 0m;

            if (allowMerge)
            {
                ServiceOrderLine match = header.Lines.Find(l =>
                    !l.IsComboChild && !l.IsMaterialExport && !l.IsPromoGift
                    && string.Equals(l.ProductId, product.Id, StringComparison.Ordinal)
                    && (l.IsPromoDiscounted || (l.DiscountRate == 0m && !l.DiscountByMoney))
                    && string.Equals(l.Note ?? "", cleanNote, StringComparison.Ordinal)
                    && (product.PriceByTime || l.UnitPrice == unitPrice));
                if (match != null)
                {
                    match.Quantity += quantity;
                    if (product.PriceByTime)
                        match.UnitPrice = unitPrice;
                    OrderCalculator.RecalcLine(match);
                    ServiceOrderRepository.UpdateLineIn(conn, tx, match, _session.UserId, now);
                    if (isCombo && !string.IsNullOrEmpty(match.ComboId))
                        ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, header.Id, match.ComboId, match.Quantity, _session.UserId, now);
                    return;
                }
            }

            string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, header.WarehouseId);
            string comboId = DbHelper.NewId();
            var newLine = new ServiceOrderLine
            {
                Id = DbHelper.NewId(),
                OrderId = header.Id,
                ProductId = product.Id,
                ProductGroupId = product.GroupId,
                ProductTypeId = product.ProductTypeId,
                DrinkTypeId = product.DrinkTypeId,
                ItemName = itemName,
                UnitId = unitId,
                WarehouseId = warehouseId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                CostPrice = product.CostPrice,
                DiscountRate = 0m,
                DiscountAmount = 0m,
                DiscountByMoney = false,
                Note = cleanNote,
                ComboId = comboId,
                ComboParentId = "",
                ComboQty = 0m
            };
            OrderCalculator.RecalcLine(newLine);
            ServiceOrderRepository.InsertLineIn(conn, tx, newLine, _session.UserId, now);
            header.Lines.Add(newLine);

            if (isCombo || isComboOpen)
            {
                List<ServiceRecipeComponent> comps = isComboOpen && openComboComponents != null && openComboComponents.Count > 0
                    ? openComboComponents
                    : ServiceOrderRepository.LoadRecipeIn(conn, tx, product.Id);
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
                        WarehouseId = warehouseId,
                        Quantity = quantity * c.Quantity,
                        UnitPrice = 0m,
                        CostPrice = 0m,
                        DiscountRate = 0m,
                        DiscountAmount = 0m,
                        DiscountByMoney = false,
                        Amount = 0m,
                        Note = "",
                        ComboId = DbHelper.NewId(),
                        ComboParentId = comboId,
                        ComboQty = c.Quantity
                    };
                    ServiceOrderRepository.InsertLineIn(conn, tx, child, _session.UserId, now);
                    header.Lines.Add(child);
                }
            }
        }

        private void RecalcAndSaveHeaderIn(FbConnection conn, FbTransaction tx, ServiceOrderHeader header, decimal roundStep, DateTime now)
        {
            ServiceOrderTotals totals = OrderCalculator.CalcTotals(
                header.Lines,
                header.TileGiamGia,
                header.TienGiamGia,
                header.GiamTheoTien,
                header.TilePhiDichVu,
                header.PhiDichVu,
                header.PhiDichVuTheoTien,
                header.TileThue,
                header.BillKhac,
                roundStep);

            header.TienHang = totals.TienHang;
            header.TienHangChuaGiam = totals.TienHangChuaGiam;
            header.GiamGiaMatHang = totals.GiamGiaMatHang;
            header.TileGiamGia = totals.TileGiamGia;
            header.TienGiamGia = totals.TienGiamGia;
            header.TilePhiDichVu = totals.TilePhiDichVu;
            header.PhiDichVu = totals.PhiDichVu;
            header.TienThue = totals.TienThue;
            header.TongCong = totals.TongCong;
            header.TienPhaiTra = totals.TienPhaiTra;

            ServiceOrderRepository.UpdateBillTotalsAndMetaIn(conn, tx, header, _session.UserId, now);
        }

        private static string CheckStockForAdditionIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            ServiceMenuItem product,
            decimal addQty,
            string warehouseId,
            List<ServiceRecipeComponent> openComboComponents)
        {
            if (product == null || string.IsNullOrEmpty(warehouseId) || addQty <= 0m)
                return null;

            var needed = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectTrackedDemandIn(conn, tx, product, addQty, openComboComponents, needed, names, 0);
            if (needed.Count == 0)
                return null;

            foreach (ServiceOrderLine existing in header.Lines)
            {
                if (existing == null || existing.IsMaterialExport || existing.IsPromoGift || string.IsNullOrEmpty(existing.ProductId))
                    continue;
                if (needed.ContainsKey(existing.ProductId))
                    needed[existing.ProductId] += existing.Quantity;
            }

            foreach (KeyValuePair<string, decimal> kv in needed)
            {
                decimal available = ServiceOrderRepository.GetStockBalanceIn(conn, tx, kv.Key, warehouseId, header.Id);
                if (available < kv.Value)
                    return ServiceOrderConstants.InsufficientStock(names[kv.Key], available, kv.Value);
            }
            return null;
        }

        private static void CollectTrackedDemandIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceMenuItem product,
            decimal qty,
            List<ServiceRecipeComponent> overrideComps,
            Dictionary<string, decimal> needed,
            Dictionary<string, string> names,
            int depth)
        {
            if (product == null || qty <= 0m || depth > 2)
                return;
            if (product.HasStock)
            {
                decimal cur;
                needed.TryGetValue(product.Id, out cur);
                needed[product.Id] = cur + qty;
                names[product.Id] = product.Name;
                return;
            }
            if (product.ProductTypeId == ProductRules.TypeBrew
                || product.ProductTypeId == ProductRules.TypeCombo
                || product.ProductTypeId == ProductRules.TypeComboOpen)
            {
                List<ServiceRecipeComponent> comps = overrideComps != null && overrideComps.Count > 0
                    ? overrideComps
                    : ServiceOrderRepository.LoadRecipeIn(conn, tx, product.Id);
                foreach (ServiceRecipeComponent c in comps)
                {
                    if (string.IsNullOrEmpty(c.MaterialId) || c.Quantity <= 0m)
                        continue;
                    if (c.HasStock)
                    {
                        decimal cur;
                        needed.TryGetValue(c.MaterialId, out cur);
                        needed[c.MaterialId] = cur + qty * c.Quantity;
                        names[c.MaterialId] = c.MaterialName;
                    }
                    else if (c.MaterialTypeId == ProductRules.TypeBrew && depth < 2)
                    {
                        ServiceMenuItem sub = ServiceOrderRepository.LoadProductIn(conn, tx, c.MaterialId);
                        CollectTrackedDemandIn(conn, tx, sub, qty * c.Quantity, null, needed, names, depth + 1);
                    }
                }
            }
        }

        private static bool CanModifyAfterTempPrint(UserSession actor)
        {
            return SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh
                || HasFunction(actor, ServiceOrderConstants.FuncDuocGiamSauInTamTinh);
        }

        private static string CheckKitchenPrintBeforeReduceIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            ServiceOrderLine line,
            decimal targetQty,
            UserSession actor,
            bool isDelete)
        {
            bool restrict = SystemConfig.KhongDuocGiamDoSauKhiInPhaChe
                || !HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMonSauInBep);
            if (!restrict)
                return null;

            decimal printed = ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, tx, header.Id, line.ItemName);
            if (printed > 0m)
            {
                if (isDelete)
                    return ServiceOrderConstants.CannotDeleteAfterKitchenPrint;
                if (targetQty < printed)
                    return ServiceOrderConstants.CannotReduceBelowKitchenPrint(printed);
            }

            if (!string.IsNullOrEmpty(line.ComboId))
            {
                foreach (ServiceOrderLine child in header.Lines)
                {
                    if (!string.Equals(child.ComboParentId, line.ComboId, StringComparison.Ordinal))
                        continue;
                    decimal childPrinted = ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, tx, header.Id, child.ItemName);
                    if (childPrinted > 0m)
                    {
                        if (isDelete)
                            return ServiceOrderConstants.CannotDeleteAfterKitchenPrint;
                        if (targetQty * child.ComboQty < childPrinted)
                            return ServiceOrderConstants.CannotReduceBelowKitchenPrint(childPrinted);
                    }
                }
            }
            return null;
        }

        private static ServiceOrderLine FindEditableParentLine(ServiceOrderHeader header, string lineId)
        {
            if (header == null || header.Lines == null)
                return null;
            return header.Lines.Find(l =>
                string.Equals(l.Id, lineId, StringComparison.Ordinal)
                && !l.IsComboChild && !l.IsMaterialExport && !l.IsPromoGift);
        }

        private static decimal RateForDrinkType(string drinkTypeId, GroupDiscountInput input)
        {
            switch (drinkTypeId ?? "1")
            {
                case "0": return input.FoodRate;
                case "1": return input.DrinkRate;
                case "2": return input.ServiceRate;
                case "3": return input.OtherRate;
                default: return 0m;
            }
        }
    }
}
