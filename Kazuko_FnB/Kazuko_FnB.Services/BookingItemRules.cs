using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>Kết quả thao tác thêm mặt hàng vào danh sách chi tiết phiếu đặt hàng.</summary>
    public sealed class BookingAddItemOutcome
    {
        public bool Changed { get; set; }
        public string WarningMessage { get; set; }
    }

    /// <summary>Kết quả người dùng chọn từ hộp Nhập số lượng 2 đơn vị tính của Khách đặt hàng.</summary>
    public sealed class BookingQuantityPromptResult
    {
        public bool Confirmed { get; set; }
        public decimal OddQuantity { get; set; }
        public decimal EvenQuantity { get; set; }
    }

    /// <summary>Kết quả kiểm tra số lượng vừa sửa trực tiếp trên ô Số lượng lưới chi tiết phiếu đặt hàng.</summary>
    public sealed class BookingGridQtyEditResult
    {
        public bool Accepted { get; set; }
        public bool Changed { get; set; }
        public decimal Quantity { get; set; }
        public string WarningMessage { get; set; }
    }

    /// <summary>
    /// Luật thuần quyết định số lượng, tạo/gộp dòng khi thêm mặt hàng và kiểm tra sửa ô Số lượng
    /// trên lưới chi tiết phiếu Khách đặt hàng (docs/specs/khach-dat-hang.md mục "Thay đổi chốt sau khi đã code" đợt 1 &amp; 2).
    /// </summary>
    public static class BookingItemRules
    {
        /// <summary>
        /// Thêm mặt hàng nhánh 1 đơn vị tính (hoặc khi SuDung2DonViTinh != 30).
        /// Luật đợt 2: SL &lt;= 0 cảnh báo "Mời bạn nhập số lượng";
        /// LUÔN gộp theo mặt hàng + ĐVT (không phụ thuộc NhapMotMatHangNhieuLanTrongPhieu),
        /// cộng số lượng, giữ nguyên đơn giá và % giảm của dòng cũ, tính lại thành tiền.
        /// </summary>
        public static BookingAddItemOutcome AddSingleUnitItem(
            List<BookingLine> lines,
            BookingSaleItem item,
            int customerPriceType,
            decimal outerQty)
        {
            var outcome = new BookingAddItemOutcome();
            if (lines == null || item == null)
                return outcome;
            if (outerQty <= 0m)
            {
                outcome.WarningMessage = BookingText.EnterQuantity;
                return outcome;
            }
            BookingLine existing = FindLine(lines, item.Id, item.UnitId);
            if (existing != null)
            {
                existing.Quantity += outerQty;
                existing.Amount = BookingRules.CalcLineAmount(existing.Quantity, existing.Price, existing.DiscountPct);
            }
            else
            {
                lines.Add(CreateLine(item, item.UnitId, item.UnitName, outerQty, item.PriceForCustomer(customerPriceType)));
            }
            outcome.Changed = true;
            return outcome;
        }

        /// <summary>
        /// Phân tích giá trị số từ ô lưới theo quy tắc dùng chung NumberInput.TryParse:
        /// dấu '.' hay ',' đều là dấu thập phân (tối đa một dấu); từ hai dấu trở lên, chữ, trống, null, DBNull trả về false.
        /// </summary>
        public static bool TryParseCellDecimal(object rawValue, out decimal value)
        {
            return NumberInput.TryParse(rawValue, out value);
        }

        /// <summary>
        /// Kiểm tra giá trị mới của ô Số lượng trên lưới chi tiết (spec đợt 2):
        /// - Không đọc được (chữ, trống, null) hoặc &lt;= 0 (khác giá trị cũ): trả về số lượng cũ, báo EnterQuantity.
        /// - Giá trị không đổi (kể cả dòng cũ đang có SL 0): chấp nhận, không báo, không đánh dấu đã sửa.
        /// - Giá trị dương mới: chấp nhận và đánh dấu đã đổi.
        /// </summary>
        public static BookingGridQtyEditResult EvaluateEditedQuantity(object rawValue, decimal oldQty)
        {
            decimal parsed;
            if (!TryParseCellDecimal(rawValue, out parsed))
            {
                return new BookingGridQtyEditResult
                {
                    Accepted = false,
                    Changed = false,
                    Quantity = oldQty,
                    WarningMessage = BookingText.EnterQuantity
                };
            }
            if (parsed == oldQty)
            {
                return new BookingGridQtyEditResult
                {
                    Accepted = true,
                    Changed = false,
                    Quantity = oldQty
                };
            }
            if (parsed <= 0m)
            {
                return new BookingGridQtyEditResult
                {
                    Accepted = false,
                    Changed = false,
                    Quantity = oldQty,
                    WarningMessage = BookingText.EnterQuantity
                };
            }
            return new BookingGridQtyEditResult
            {
                Accepted = true,
                Changed = true,
                Quantity = parsed
            };
        }

        /// <summary>
        /// Thêm mặt hàng 2 đơn vị tính (khi SuDung2DonViTinh == 30 và mặt hàng có đơn vị chẵn):
        /// 1. Hộp bấm Hủy bỏ (dialogConfirmed = false): không thêm gì, không cảnh báo.
        /// 2. Đã có dòng lẻ (cùng mặt hàng + ĐVT lẻ): ô SL &gt; 0 thì cộng ô SL vào dòng đó
        ///    (không xét số lẻ trong hộp, giữ đơn giá và % giảm của dòng cũ, luôn gộp không phụ thuộc mergeAllowed).
        ///    Ô SL &lt;= 0 thì không cộng; nếu lẻ trong hộp &gt; 0 thì báo "Mời bạn nhập số lượng".
        /// 3. Chưa có dòng lẻ: lẻ trong hộp &lt;= 0 thì không tạo dòng lẻ; lẻ trong hộp &gt; 0 và ô SL &gt; 0
        ///    thì tạo dòng mới với số lượng = ô SL (KHÔNG phải số trong hộp), đơn giá theo khách, giảm 0;
        ///    lẻ trong hộp &gt; 0 mà ô SL &lt;= 0 thì không tạo và báo "Mời bạn nhập số lượng".
        /// 4. Dòng chẵn: lấy số chẵn trong hộp (&gt; 0), kể cả khi dòng lẻ không được tạo;
        ///    ĐVT = đơn vị chẵn, đơn giá = BigSellPrice, giảm 0; gộp theo mergeAllowed.
        /// 5. Không có gì thay đổi (kể cả lẻ và chẵn đều 0) thì báo "Mời bạn nhập số lượng".
        ///    Lẻ và chẵn đều 0 mà đã có dòng lẻ và ô SL &gt; 0 thì cộng ô SL, không cảnh báo.
        /// </summary>
        public static BookingAddItemOutcome AddTwoUnitItem(
            List<BookingLine> lines,
            BookingSaleItem item,
            int customerPriceType,
            decimal outerQty,
            decimal dialogOddQty,
            decimal dialogEvenQty,
            bool dialogConfirmed,
            bool mergeAllowed)
        {
            var outcome = new BookingAddItemOutcome();
            if (lines == null || item == null || !dialogConfirmed)
                return outcome;

            BookingLine existingOdd = FindLine(lines, item.Id, item.UnitId);
            if (existingOdd != null)
            {
                if (outerQty > 0m)
                {
                    existingOdd.Quantity += outerQty;
                    existingOdd.Amount = BookingRules.CalcLineAmount(existingOdd.Quantity, existingOdd.Price, existingOdd.DiscountPct);
                    outcome.Changed = true;
                }
                else if (dialogOddQty > 0m)
                {
                    outcome.WarningMessage = BookingText.EnterQuantity;
                }
            }
            else if (dialogOddQty > 0m)
            {
                if (outerQty > 0m)
                {
                    lines.Add(CreateLine(item, item.UnitId, item.UnitName, outerQty, item.PriceForCustomer(customerPriceType)));
                    outcome.Changed = true;
                }
                else
                {
                    outcome.WarningMessage = BookingText.EnterQuantity;
                }
            }

            if (dialogEvenQty > 0m && item.HasBigUnit)
            {
                BookingLine existingEven = mergeAllowed ? FindLine(lines, item.Id, item.BigUnitId) : null;
                if (existingEven != null)
                {
                    existingEven.Quantity += dialogEvenQty;
                    existingEven.Amount = BookingRules.CalcLineAmount(existingEven.Quantity, existingEven.Price, existingEven.DiscountPct);
                }
                else
                {
                    lines.Add(CreateLine(item, item.BigUnitId, item.BigUnitName, dialogEvenQty, item.BigSellPrice));
                }
                outcome.Changed = true;
            }

            if (!outcome.Changed && string.IsNullOrEmpty(outcome.WarningMessage))
                outcome.WarningMessage = BookingText.EnterQuantity;

            return outcome;
        }

        private static BookingLine FindLine(List<BookingLine> lines, string productId, string unitId)
        {
            foreach (BookingLine line in lines)
            {
                if (string.Equals(line.ProductId, productId, StringComparison.Ordinal)
                    && string.Equals(line.UnitId, unitId, StringComparison.Ordinal))
                    return line;
            }
            return null;
        }

        private static BookingLine CreateLine(BookingSaleItem item, string unitId, string unitName, decimal qty, decimal price)
        {
            return new BookingLine
            {
                ProductId = item.Id,
                ProductName = item.Name ?? "",
                UnitId = unitId,
                UnitName = unitName ?? "",
                Quantity = qty,
                Price = price,
                DiscountPct = 0m,
                Amount = BookingRules.CalcLineAmount(qty, price, 0m)
            };
        }
    }
}
