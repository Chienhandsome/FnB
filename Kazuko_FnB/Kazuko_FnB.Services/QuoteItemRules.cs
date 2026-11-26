using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>Kết quả thao tác thêm mặt hàng vào danh sách chi tiết báo giá.</summary>
    public sealed class QuoteAddItemOutcome
    {
        public bool Changed { get; set; }
        public string WarningMessage { get; set; }
    }

    /// <summary>
    /// Luật thuần quyết định số lượng, tạo/gộp dòng khi thêm mặt hàng và kiểm tra khi sửa số lượng
    /// trên lưới chi tiết báo giá (docs/specs/bao-gia-ban-hang.md §5.0.2, §5.0 P2/P3, §2.1).
    /// </summary>
    public static class QuoteItemRules
    {
        /// <summary>
        /// Thêm mặt hàng 1 đơn vị tính (hoặc khi SuDung2DonViTinh != 30).
        /// - SL &lt;= 0: báo "Mời bạn nhập số lượng", không thêm.
        /// - Dòng đơn vị lẻ LUÔN gộp theo mặt hàng + ĐVT (không phụ thuộc NhapMotMatHangNhieuLanTrongPhieu),
        ///   cộng số lượng, giữ nguyên đơn giá dòng cũ.
        /// </summary>
        public static QuoteAddItemOutcome AddSingleUnitItem(List<QuoteLine> lines, QuoteSaleItem item, decimal outerQty)
        {
            var outcome = new QuoteAddItemOutcome();
            if (lines == null || item == null)
                return outcome;
            if (outerQty <= 0m)
            {
                outcome.WarningMessage = QuoteText.EnterQuantity;
                return outcome;
            }
            AddOrMergeOddLine(lines, item, outerQty);
            outcome.Changed = true;
            return outcome;
        }

        /// <summary>
        /// Thêm mặt hàng 2 đơn vị tính (khi SuDung2DonViTinh == 30 và mặt hàng có DDONVITINHCHANID),
        /// theo luật §5.0.2 đã chốt:
        /// - Hộp bấm Thoát (dialogConfirmed = false): không thêm gì, không cảnh báo.
        /// - Lẻ và chẵn trong hộp đều = 0 không phải trường hợp riêng, vẫn áp đúng luật dòng lẻ/chẵn:
        ///   * Đã có dòng lẻ cùng mặt hàng + ĐVT lẻ: nếu ô SL ngoài hộp &gt; 0 thì cộng ô SL vào dòng đó
        ///     (không xét số lẻ trong hộp, kể cả khi lẻ và chẵn trong hộp đều = 0), giữ đơn giá dòng cũ.
        ///     Nếu ô SL &lt;= 0 thì không cộng; nếu lẻ trong hộp &gt; 0 thì báo "Mời bạn nhập số lượng".
        ///   * Chưa có dòng lẻ: lẻ trong hộp &lt;= 0 thì không tạo dòng; lẻ trong hộp &gt; 0 thì:
        ///     nếu ô SL &gt; 0 tạo dòng mới với số lượng = ô SL (KHÔNG phải số lẻ trong hộp);
        ///     nếu ô SL &lt;= 0 thì không tạo và báo "Mời bạn nhập số lượng".
        /// - Dòng chẵn: lấy số chẵn trong hộp (&gt; 0), DDONVITINHID = DDONVITINHCHANID, đơn giá = GIABANCHAN, giảm = 0;
        ///   chẵn trong hộp &lt;= 0 thì không thêm, không gộp dòng chẵn;
        ///   gộp khi allowMultipleRowsPerItem = false (NhapMotMatHangNhieuLanTrongPhieu != 30),
        ///   thêm dòng mới khi allowMultipleRowsPerItem = true (= 30).
        /// - Nếu kết cục không có gì thay đổi thì cảnh báo "Mời bạn nhập số lượng".
        /// </summary>
        public static QuoteAddItemOutcome AddTwoUnitItem(
            List<QuoteLine> lines,
            QuoteSaleItem item,
            decimal outerQty,
            decimal dialogOddQty,
            decimal dialogEvenQty,
            bool dialogConfirmed,
            bool allowMultipleRowsPerItem)
        {
            var outcome = new QuoteAddItemOutcome();
            if (lines == null || item == null || !dialogConfirmed)
                return outcome;

            QuoteLine existingOdd = FindLine(lines, item.Id, item.UnitId);
            if (existingOdd != null)
            {
                if (outerQty > 0m)
                {
                    existingOdd.Quantity += outerQty;
                    existingOdd.Amount = QuoteCalculator.CalcLineAmount(existingOdd.Quantity, existingOdd.Price, existingOdd.DiscountPct);
                    outcome.Changed = true;
                }
                else if (dialogOddQty > 0m)
                {
                    outcome.WarningMessage = QuoteText.EnterQuantity;
                }
            }
            else if (dialogOddQty > 0m)
            {
                if (outerQty > 0m)
                {
                    lines.Add(CreateLine(item, item.UnitId, item.UnitName, outerQty, item.SellPrice));
                    outcome.Changed = true;
                }
                else
                {
                    outcome.WarningMessage = QuoteText.EnterQuantity;
                }
            }

            if (dialogEvenQty > 0m && item.HasBigUnit)
            {
                QuoteLine existingEven = allowMultipleRowsPerItem ? null : FindLine(lines, item.Id, item.BigUnitId);
                if (existingEven != null)
                {
                    existingEven.Quantity += dialogEvenQty;
                    existingEven.Amount = QuoteCalculator.CalcLineAmount(existingEven.Quantity, existingEven.Price, existingEven.DiscountPct);
                }
                else
                {
                    lines.Add(CreateLine(item, item.BigUnitId, item.BigUnitName, dialogEvenQty, item.BigSellPrice));
                }
                outcome.Changed = true;
            }

            if (!outcome.Changed && string.IsNullOrEmpty(outcome.WarningMessage))
                outcome.WarningMessage = QuoteText.EnterQuantity;

            return outcome;
        }

        /// <summary>
        /// Kiểm tra giá trị số lượng sau khi người dùng sửa trực tiếp trên lưới chi tiết (P3).
        /// Trả về true nếu hợp lệ (&gt; 0); ngược lại trả về false, khôi phục giá trị cũ và thông báo.
        /// </summary>
        public static bool ValidateEditedQuantity(decimal newQty, decimal oldQty, out decimal effectiveQty, out string warning)
        {
            if (newQty <= 0m)
            {
                effectiveQty = oldQty;
                warning = QuoteText.EnterQuantity;
                return false;
            }
            effectiveQty = newQty;
            warning = null;
            return true;
        }

        private static void AddOrMergeOddLine(List<QuoteLine> lines, QuoteSaleItem item, decimal qty)
        {
            QuoteLine existing = FindLine(lines, item.Id, item.UnitId);
            if (existing != null)
            {
                existing.Quantity += qty;
                existing.Amount = QuoteCalculator.CalcLineAmount(existing.Quantity, existing.Price, existing.DiscountPct);
                return;
            }
            lines.Add(CreateLine(item, item.UnitId, item.UnitName, qty, item.SellPrice));
        }

        private static QuoteLine FindLine(List<QuoteLine> lines, string productId, string unitId)
        {
            foreach (QuoteLine line in lines)
            {
                if (string.Equals(line.ProductId, productId, StringComparison.Ordinal)
                    && string.Equals(line.UnitId, unitId, StringComparison.Ordinal))
                    return line;
            }
            return null;
        }

        private static QuoteLine CreateLine(QuoteSaleItem item, string unitId, string unitName, decimal qty, decimal price)
        {
            return new QuoteLine
            {
                ProductId = item.Id,
                ProductCode = item.Code ?? "",
                ProductName = item.Name ?? "",
                UnitId = unitId,
                UnitName = unitName ?? "",
                Quantity = qty,
                Price = price,
                DiscountPct = 0m,
                Amount = QuoteCalculator.CalcLineAmount(qty, price, 0m),
                Note = ""
            };
        }
    }
}
