using System;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không phụ thuộc DB/UI) cho Chuyển bàn, Gộp bàn, Chuyển mặt hàng và Hủy bill
    /// (docs/specs/su-dung-dich-vu.md §3.8, docs/pos-tap-nghiep-vu-ban-khu-vuc.md §2–§3).
    /// </summary>
    public static class TableTransferRules
    {
        public static MoveTablePriceMode NormalizePriceMode(int raw)
        {
            if (raw >= (int)MoveTablePriceMode.RaiseOnly && raw <= (int)MoveTablePriceMode.KeepCurrent)
                return (MoveTablePriceMode)raw;
            return MoveTablePriceMode.RaiseOnly;
        }

        /// <summary>
        /// Tính đơn giá mới của một dòng món khi chuyển/gộp sang khu vực khác theo GiaMatHangKhiChuyenBan:
        /// - 0 (RaiseOnly): chỉ nâng nếu đơn giá hiện tại &lt; giá khu vực mới.
        /// - 1 (AlwaysUpdate): luôn cập nhật theo giá khu vực mới.
        /// - 2 (KeepCurrent): giữ nguyên đơn giá cũ.
        /// Mặt hàng mở (TypeOpen) hoặc giá theo thời giá (PriceByTime) luôn giữ nguyên đơn giá đã nhập.
        /// </summary>
        public static decimal ResolveTransferredUnitPrice(
            decimal currentPrice,
            decimal targetAreaPrice,
            MoveTablePriceMode mode,
            bool isOpenItem,
            bool priceByTime)
        {
            if (isOpenItem || priceByTime || mode == MoveTablePriceMode.KeepCurrent)
                return currentPrice;
            if (mode == MoveTablePriceMode.RaiseOnly)
                return targetAreaPrice > currentPrice ? targetAreaPrice : currentPrice;
            return targetAreaPrice;
        }

        /// <summary>
        /// Chênh lệch số lượng in chế biến cần chuyển từ bill nguồn sang bill đích khi chuyển mặt hàng
        /// (GetChenhLechInCheBienChuyenDo, ban-khu-vuc.md §2):
        /// chỉ áp khi bật in xuống bếp và loại đồ tương ứng bật in; nếu đã in &gt; số còn lại sau chuyển
        /// thì chênh lệch = min(số chuyển, đã in − số còn lại).
        /// </summary>
        public static decimal CalcKitchenTransferDiff(
            decimal printedQty,
            decimal remainingSourceQty,
            decimal transferredQty,
            bool kitchenPrintEnabled,
            bool drinkTypePrintEnabled)
        {
            if (!kitchenPrintEnabled || !drinkTypePrintEnabled)
                return 0m;
            if (printedQty <= 0m || transferredQty <= 0m)
                return 0m;
            decimal remaining = remainingSourceQty < 0m ? 0m : remainingSourceQty;
            if (printedQty <= remaining)
                return 0m;
            decimal diff = printedQty - remaining;
            return diff > transferredQty ? transferredQty : diff;
        }

        /// <summary>Kiểm tra loại đồ (0 ăn, 1 uống, 2 dịch vụ, 3 khác) có bật in xuống bếp không.</summary>
        public static bool IsDrinkTypeKitchenEnabled(
            string drinkTypeId,
            bool inDoAn,
            bool inDoUong,
            bool inDichVu,
            bool inDoKhac)
        {
            switch (drinkTypeId ?? "1")
            {
                case "0": return inDoAn;
                case "1": return inDoUong;
                case "2": return inDichVu;
                case "3": return inDoKhac;
                default: return inDoUong;
            }
        }

        /// <summary>
        /// Kiểm tra hóa đơn còn nằm trong thời hạn cho phép hủy trên bàn (§3.8, ban-khu-vuc.md §3):
        /// |serverNow − billStart| &lt;= maxMinutes.
        /// </summary>
        public static bool IsWithinCancelWindow(DateTime billStart, DateTime serverNow, int maxMinutes)
        {
            if (maxMinutes < 0)
                return true;
            double elapsedMinutes = Math.Abs((serverNow - billStart).TotalMinutes);
            return elapsedMinutes <= maxMinutes;
        }
    }
}
