using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho màn Tồn kho (docs/specs/ton-kho.md):
    /// hiển thị tồn 2 ĐVT, tính giá trị vốn/bán, xác định tông màu dòng, lọc mặt hàng, chọn kho mặc định và kiểm quyền.
    /// </summary>
    public static class InventoryRules
    {
        /// <summary>
        /// Hiển thị tồn theo 2 ĐVT khi mặt hàng có đơn vị chẵn (spec §1 &amp; §4 #2):
        /// N = (int)(tồn / QUYDOI), M = tồn - N * QUYDOI -&gt; "N &lt;ĐVT chẵn&gt; M &lt;ĐVT lẻ&gt;".
        /// Mặt hàng không có 2 ĐVT trả về chuỗi rỗng "".
        /// </summary>
        public static string FormatTwoUnits(
            decimal stockQuantity,
            bool hasTwoUnits,
            decimal quyDoi,
            string bigUnitName,
            string baseUnitName)
        {
            if (!hasTwoUnits || quyDoi <= 0m)
                return "";

            string big = (bigUnitName ?? "").Trim();
            string odd = (baseUnitName ?? "").Trim();
            if (big.Length == 0)
                return "";

            long n = (long)Math.Truncate(stockQuantity / quyDoi);
            decimal m = stockQuantity - n * quyDoi;
            string nStr = n.ToString(CultureInfo.InvariantCulture);
            string mStr = m == Math.Truncate(m)
                ? ((long)m).ToString(CultureInfo.InvariantCulture)
                : PurchaseConstants.FormatQtyVi(m);

            if (odd.Length == 0)
                return string.Format("{0} {1} {2}", nStr, big, mStr);
            return string.Format("{0} {1} {2} {3}", nStr, big, mStr, odd);
        }

        /// <summary>
        /// Xác định màu dòng theo spec §1:
        /// - Tồn âm (&lt; 0) -&gt; NegativeStock (tô đỏ, ưu tiên cao nhất).
        /// - Tồn &lt;= TONTOITHIEU khi TONTOITHIEU &gt; 0 -&gt; LowStock (tô vàng).
        /// - Còn lại -&gt; Normal.
        /// </summary>
        public static InventoryRowTone ResolveTone(decimal stockQuantity, decimal minStock)
        {
            if (stockQuantity < 0m)
                return InventoryRowTone.NegativeStock;
            if (minStock > 0m && stockQuantity <= minStock)
                return InventoryRowTone.LowStock;
            return InventoryRowTone.Normal;
        }

        /// <summary>Áp công thức Giá trị vốn, Giá trị bán, chuỗi 2 ĐVT và tông màu cho một dòng tồn kho.</summary>
        public static void ApplyMetrics(InventoryItemRow row)
        {
            if (row == null)
                return;
            row.CostValue = row.StockQuantity * row.CostPrice;
            row.SellValue = row.StockQuantity * row.SellPrice;
            row.TwoUnitDisplay = FormatTwoUnits(
                row.StockQuantity, row.HasTwoUnits, row.QuyDoi, row.BigUnitName, row.UnitName);
            row.Tone = ResolveTone(row.StockQuantity, row.MinStock);
        }

        /// <summary>Áp công thức tính toán cho cả danh sách mặt hàng tồn kho.</summary>
        public static void ApplyMetrics(IEnumerable<InventoryItemRow> rows)
        {
            if (rows == null)
                return;
            foreach (InventoryItemRow r in rows)
                ApplyMetrics(r);
        }

        /// <summary>Kiểm tra một dòng tồn kho có khớp bộ lọc hiện tại hay không (spec §1).</summary>
        public static bool MatchesFilter(InventoryItemRow row, InventoryFilter filter)
        {
            if (row == null)
                return false;
            if (filter == null)
                return true;

            if (!string.IsNullOrEmpty(filter.ProductTypeId))
            {
                if (!string.Equals(row.EffectiveTypeId, filter.ProductTypeId, StringComparison.Ordinal))
                    return false;
            }

            if (filter.GroupIds != null && filter.GroupIds.Count > 0)
            {
                if (!filter.GroupIds.Contains(row.GroupId ?? ""))
                    return false;
            }
            else if (!string.IsNullOrEmpty(filter.GroupId))
            {
                if (!string.Equals(row.GroupId ?? "", filter.GroupId, StringComparison.Ordinal))
                    return false;
            }

            if (filter.OnlyBelowMinStock)
            {
                if (row.MinStock <= 0m || row.StockQuantity > row.MinStock)
                    return false;
            }

            if (filter.HideZeroStock && row.StockQuantity == 0m)
                return false;

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                string q = CatalogRepository.Fold(filter.SearchText.Trim());
                if (q.Length > 0)
                {
                    string codeFold = CatalogRepository.Fold(row.Code ?? "");
                    string nameFold = CatalogRepository.Fold(row.Name ?? "");
                    if (codeFold.IndexOf(q, StringComparison.Ordinal) < 0
                        && nameFold.IndexOf(q, StringComparison.Ordinal) < 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Tính tổng cộng giá trị trên danh sách dòng đang hiển thị (thuần decimal).</summary>
        public static InventoryTotals ComputeTotals(IEnumerable<InventoryItemRow> rows)
        {
            var totals = new InventoryTotals();
            if (rows == null)
                return totals;

            foreach (InventoryItemRow r in rows)
            {
                if (r == null)
                    continue;
                totals.ItemCount++;
                totals.TotalStockQuantity += r.StockQuantity;
                totals.TotalCostValue += r.CostValue;
                totals.TotalSellValue += r.SellValue;
            }
            return totals;
        }

        /// <summary>
        /// Chọn kho mặc định theo cửa hàng đăng nhập (spec §1):
        /// 1. Ưu tiên kho mặc định gắn trên DCUAHANG.DKHOHANGID nếu thuộc danh sách kho được phép.
        /// 2. Tiếp theo ưu tiên kho đầu tiên thuộc cửa hàng đang đăng nhập.
        /// 3. Cuối cùng lấy kho đầu tiên trong danh sách được phép (rỗng nếu không có kho nào).
        /// </summary>
        public static string ResolveDefaultWarehouseId(
            IList<WarehouseRow> allowedWarehouses,
            string currentStoreId,
            string storeDefaultWarehouseId)
        {
            if (allowedWarehouses == null || allowedWarehouses.Count == 0)
                return "";

            if (!string.IsNullOrEmpty(storeDefaultWarehouseId))
            {
                for (int i = 0; i < allowedWarehouses.Count; i++)
                {
                    if (string.Equals(allowedWarehouses[i].Id, storeDefaultWarehouseId, StringComparison.Ordinal))
                        return allowedWarehouses[i].Id;
                }
            }

            if (!string.IsNullOrEmpty(currentStoreId))
            {
                for (int i = 0; i < allowedWarehouses.Count; i++)
                {
                    if (string.Equals(allowedWarehouses[i].StoreId ?? "", currentStoreId, StringComparison.Ordinal))
                        return allowedWarehouses[i].Id;
                }
            }

            return allowedWarehouses[0].Id ?? "";
        }

        /// <summary>Mở rộng một nhóm được chọn thành tập gồm chính nó và mọi nhóm con cháu trong cây nhóm.</summary>
        public static HashSet<string> CollectGroupSubtree(IList<ProductGroupRow> allGroups, string rootGroupId)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(rootGroupId))
                return ids;

            ids.Add(rootGroupId);
            if (allGroups == null || allGroups.Count == 0)
                return ids;

            var byParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (ProductGroupRow g in allGroups)
            {
                if (g == null || string.IsNullOrEmpty(g.Id) || string.IsNullOrEmpty(g.ParentId))
                    continue;
                if (string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                    continue;
                List<string> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<string>();
                kids.Add(g.Id);
            }

            var queue = new Queue<string>();
            queue.Enqueue(rootGroupId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                List<string> kids;
                if (!byParent.TryGetValue(current, out kids))
                    continue;
                foreach (string childId in kids)
                {
                    if (ids.Add(childId))
                        queue.Enqueue(childId);
                }
            }
            return ids;
        }

        public static bool CanView(UserSession session)
        {
            return AppSession.Can(session, InventoryConstants.FunctionName, RoleModes.View);
        }

        public static bool CanViewBuyPrice(UserSession session)
        {
            return AppSession.Can(session, InventoryConstants.ViewBuyPriceFunctionName, RoleModes.View);
        }
    }
}
