using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho chức năng "Trả đồ" ở Điều chỉnh hóa đơn
    /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §2.3, §7):
    /// - Loại trừ KM tự động ('KM'), dòng xuất vật tư (XUATVATTU = 30), dịch vụ theo giờ ('7'), dòng con combo.
    /// - Gộp theo (mặt hàng, đơn giá, % giảm, đơn vị tính) với SLORDER = Σ SLXUATCHUAQUYDOI &gt; 0.
    /// - Bảo toàn bất biến SLTRALAI + SLSUDUNG = SLORDER; nhập vượt khoảng [0, SLORDER] thì đặt SLTRALAI = 0.
    /// - Tạo dòng SL âm (-SLTRALAI) giữ nguyên đơn giá và % giảm gốc.
    /// </summary>
    public static class ReturnItemRules
    {
        public static List<ReturnableItemRow> BuildReturnableGroups(IEnumerable<ServiceOrderLine> lines)
        {
            var result = new List<ReturnableItemRow>();
            if (lines == null)
                return result;

            var byKey = new Dictionary<string, ReturnableItemRow>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (ServiceOrderLine line in lines)
            {
                if (line == null)
                    continue;
                if (line.IsMaterialExport || line.IsComboChild)
                    continue;
                if (string.Equals(line.ProductId, "KM", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(line.ProductTypeId, ProductRules.TypeHourlyService, StringComparison.Ordinal))
                    continue;

                bool isOpen = string.IsNullOrEmpty(line.ProductId)
                    || string.Equals(line.ProductTypeId, ProductRules.TypeOpen, StringComparison.Ordinal);
                string key = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}\u001F{1}\u001F{2}\u001F{3}\u001F{4}",
                    line.ProductId ?? "",
                    isOpen ? (line.ItemName ?? "") : "",
                    line.UnitPrice,
                    line.DiscountRate,
                    line.UnitId ?? "");

                ReturnableItemRow group;
                if (!byKey.TryGetValue(key, out group))
                {
                    group = new ReturnableItemRow
                    {
                        GroupKey = key,
                        ProductId = line.ProductId ?? "",
                        ProductTypeId = line.ProductTypeId ?? "0",
                        DrinkTypeId = line.DrinkTypeId ?? "1",
                        ItemName = line.ItemName ?? "",
                        UnitId = line.UnitId ?? "",
                        UnitName = line.UnitName ?? "",
                        WarehouseId = line.WarehouseId ?? "",
                        UnitPrice = line.UnitPrice,
                        CostPrice = line.CostPrice,
                        DiscountRate = line.DiscountRate,
                        OrderQty = 0m
                    };
                    byKey[key] = group;
                    order.Add(key);
                }
                group.OrderQty += line.Quantity;
            }

            foreach (string key in order)
            {
                ReturnableItemRow g = byKey[key];
                if (g.OrderQty > 0m)
                {
                    g.UsedQty = g.OrderQty;
                    g.ReturnQty = 0m;
                    result.Add(g);
                }
            }
            return result;
        }

        /// <summary>
        /// Cập nhật khi người dùng nhập cột SL trả lại (SLTRALAI):
        /// nhập &lt; 0 hoặc &gt; SLORDER thì đưa về 0; luôn giữ SLTRALAI + SLSUDUNG = SLORDER (§2.3).
        /// </summary>
        public static void ApplyReturnQty(ReturnableItemRow row, decimal enteredReturnQty)
        {
            if (row == null)
                return;
            if (enteredReturnQty < 0m || enteredReturnQty > row.OrderQty)
            {
                row.ReturnQty = 0m;
                row.UsedQty = row.OrderQty;
                return;
            }
            row.ReturnQty = enteredReturnQty;
            row.UsedQty = row.OrderQty - enteredReturnQty;
        }

        /// <summary>
        /// Cập nhật khi người dùng nhập cột SL sử dụng (SLSUDUNG):
        /// nhập &lt; 0 hoặc &gt; SLORDER thì đặt SLSUDUNG = SLORDER, SLTRALAI = 0; luôn giữ SLTRALAI + SLSUDUNG = SLORDER (§2.3).
        /// </summary>
        public static void ApplyUsedQty(ReturnableItemRow row, decimal enteredUsedQty)
        {
            if (row == null)
                return;
            if (enteredUsedQty < 0m || enteredUsedQty > row.OrderQty)
            {
                row.UsedQty = row.OrderQty;
                row.ReturnQty = 0m;
                return;
            }
            row.UsedQty = enteredUsedQty;
            row.ReturnQty = row.OrderQty - enteredUsedQty;
        }

        /// <summary>
        /// Tạo dòng SL âm (-SLTRALAI) giữ nguyên đơn giá và % giảm gốc để gắn vào hóa đơn trong bộ nhớ (§2.3, §7).
        /// </summary>
        public static ServiceOrderLine CreateReturnLine(string orderId, ReturnableItemRow group, decimal returnQty, string newLineId, string newComboId)
        {
            if (group == null)
                throw new ArgumentNullException("group");
            if (returnQty <= 0m)
                throw new ArgumentOutOfRangeException("returnQty", "Số lượng trả phải lớn hơn 0.");

            var line = new ServiceOrderLine
            {
                Id = newLineId,
                OrderId = orderId ?? "",
                ProductId = group.ProductId ?? "",
                ProductTypeId = group.ProductTypeId ?? "0",
                DrinkTypeId = group.DrinkTypeId ?? "1",
                ItemName = group.ItemName ?? "",
                UnitId = group.UnitId,
                UnitName = group.UnitName ?? "",
                WarehouseId = group.WarehouseId,
                Quantity = -returnQty,
                UnitPrice = group.UnitPrice,
                CostPrice = group.CostPrice,
                DiscountRate = group.DiscountRate,
                DiscountAmount = 0m,
                DiscountByMoney = false,
                Note = "Trả đồ",
                ComboId = newComboId ?? "",
                ComboParentId = "",
                ComboQty = 0m
            };
            OrderCalculator.RecalcLine(line);
            return line;
        }

        /// <summary>Tạo câu nội dung lưu vết Trả đồ theo §2.3: "Trả lại 'x', số lượng: n đơn giá: p".</summary>
        public static string FormatReturnLogNote(string itemName, decimal returnQty, decimal unitPrice)
        {
            string qtyText = returnQty == Math.Truncate(returnQty)
                ? returnQty.ToString("0", CultureInfo.InvariantCulture)
                : returnQty.ToString("0.##", CultureInfo.InvariantCulture);
            return string.Format("Trả lại '{0}', số lượng: {1} đơn giá: {2}",
                itemName ?? "", qtyText, SalesManageConstants.FormatMoneyVi(unitPrice));
        }
    }
}
