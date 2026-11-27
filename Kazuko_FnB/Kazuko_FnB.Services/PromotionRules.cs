using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc thuần (không chạm DB) cho Danh mục đợt khuyến mại (spec docs/specs/danh-muc-khuyen-mai.md §2, §3, §5):
    /// phân quyền, trạng thái hiển thị, kiểm nhập liệu đầu đợt và từng loại chi tiết,
    /// kiểm tra giao khoảng ngày để chặn đợt trùng cùng loại, cắt chuỗi lưu vết.
    /// </summary>
    public static class PromotionRules
    {
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(PromotionText.FunctionName, bit);
        }

        /// <summary>Trạng thái hiển thị tại ngày máy chủ (spec §3): Ngừng áp dụng → Chưa tới → Hết hạn → Đang áp dụng.</summary>
        public static PromotionStatus ComputeStatus(PromotionRow row, DateTime serverDate)
        {
            if (row == null || row.IsStopped)
                return PromotionStatus.Stopped;
            DateTime today = serverDate.Date;
            if (row.FromDate.HasValue && today < row.FromDate.Value.Date)
                return PromotionStatus.Upcoming;
            if (row.ToDate.HasValue && today > row.ToDate.Value.Date)
                return PromotionStatus.Expired;
            return PromotionStatus.Active;
        }

        public static string StatusText(PromotionStatus status)
        {
            switch (status)
            {
                case PromotionStatus.Active: return PromotionText.StatusActive;
                case PromotionStatus.Upcoming: return PromotionText.StatusUpcoming;
                case PromotionStatus.Expired: return PromotionText.StatusExpired;
                case PromotionStatus.Stopped: return PromotionText.StatusStopped;
                default: return PromotionText.StatusStopped;
            }
        }

        public static bool UsesDetailGrid(PromotionKind kind)
        {
            return kind != PromotionKind.Unknown && kind != PromotionKind.TotalBillPercent;
        }

        /// <summary>
        /// Kiểm tra hai khoảng ngày [fromA, toA] và [fromB, toB] có giao nhau hay không (tính theo ngày, biên đóng).
        /// Thiếu Từ ngày = không giới hạn trái; thiếu Đến ngày = không giới hạn phải.
        /// </summary>
        public static bool IntervalsOverlap(DateTime? fromA, DateTime? toA, DateTime? fromB, DateTime? toB)
        {
            DateTime startA = fromA.HasValue ? fromA.Value.Date : DateTime.MinValue;
            DateTime endA = toA.HasValue ? toA.Value.Date : DateTime.MaxValue;
            DateTime startB = fromB.HasValue ? fromB.Value.Date : DateTime.MinValue;
            DateTime endB = toB.HasValue ? toB.Value.Date : DateTime.MaxValue;
            return startA <= endB && startB <= endA;
        }

        /// <summary>
        /// Tìm đợt đang không ngừng áp dụng cùng loại có khoảng ngày giao với <paramref name="candidate"/>.
        /// Đợt đang ngừng áp dụng bỏ qua kiểm trùng; chính nó (trùng ID) không tự báo trùng.
        /// </summary>
        public static PromotionRow FindOverlapping(PromotionRow candidate, IEnumerable<PromotionRow> existingSameTypeActive)
        {
            if (candidate == null || candidate.IsStopped || existingSameTypeActive == null)
                return null;
            string selfId = candidate.Id ?? "";
            foreach (PromotionRow other in existingSameTypeActive)
            {
                if (other == null || other.IsStopped)
                    continue;
                if (other.Status != 0 && other.Status != PromotionText.ActiveStatus)
                    continue;
                if (selfId.Length > 0 && string.Equals(other.Id ?? "", selfId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(other.TypeId ?? "", candidate.TypeId ?? "", StringComparison.Ordinal))
                    continue;
                if (IntervalsOverlap(candidate.FromDate, candidate.ToDate, other.FromDate, other.ToDate))
                    return other;
            }
            return null;
        }

        /// <summary>Kiểm tra đầy đủ đầu đợt và danh sách chi tiết theo loại khuyến mại (không chạm DB).</summary>
        public static PromotionSaveResult Validate(
            PromotionRow row,
            PromotionKind kind,
            IList<PromotionDetailRow> details,
            bool isDuplicateName,
            PromotionRow overlappingPromotion)
        {
            if (row == null)
                return PromotionSaveResult.Fail("Không có dữ liệu đợt khuyến mại.");

            if (string.IsNullOrWhiteSpace(row.Name))
                return PromotionSaveResult.Fail(PromotionText.NameEmpty, hasNameError: true);
            if (row.Name.Length > PromotionText.MaxTextLength)
                return PromotionSaveResult.Fail(
                    PromotionText.FieldTooLong("Tên đợt", PromotionText.MaxTextLength),
                    hasNameError: true);
            if (isDuplicateName)
                return PromotionSaveResult.Fail(PromotionText.NameDuplicate(row.Name), hasNameError: true);

            if (string.IsNullOrWhiteSpace(row.TypeId))
                return PromotionSaveResult.Fail(PromotionText.TypeRequired, hasTypeError: true);
            if (kind == PromotionKind.Unknown)
                return PromotionSaveResult.Fail(PromotionText.TypeUnknown, hasTypeError: true);

            if (row.FromDate.HasValue && row.ToDate.HasValue && row.ToDate.Value.Date < row.FromDate.Value.Date)
                return PromotionSaveResult.Fail(PromotionText.ToDateBeforeFromDate, hasDateError: true);

            if (row.Note != null && row.Note.Length > PromotionText.MaxTextLength)
                return PromotionSaveResult.Fail(PromotionText.FieldTooLong("Ghi chú", PromotionText.MaxTextLength));

            if (kind == PromotionKind.TotalBillPercent)
            {
                if (!IsValidPercent(row.DiscountPercent))
                    return PromotionSaveResult.Fail(PromotionText.DiscountOutOfRange, hasDiscountError: true);
            }
            else
            {
                PromotionSaveResult detailRes = ValidateDetails(kind, details);
                if (!detailRes.Ok)
                    return detailRes;
            }

            if (!row.IsStopped && overlappingPromotion != null)
            {
                return PromotionSaveResult.Fail(
                    PromotionText.OverlapBlocked(overlappingPromotion.Name),
                    hasOverlapError: true,
                    hasDateError: true);
            }

            return new PromotionSaveResult { Ok = true };
        }

        /// <summary>Kiểm tra danh sách chi tiết cho 8 loại dùng lưới chi tiết (spec §2, §3, §5.1).</summary>
        public static PromotionSaveResult ValidateDetails(PromotionKind kind, IList<PromotionDetailRow> details)
        {
            if (kind == PromotionKind.TotalBillPercent)
                return new PromotionSaveResult { Ok = true };
            if (kind == PromotionKind.Unknown)
                return PromotionSaveResult.Fail(PromotionText.TypeUnknown, hasTypeError: true);
            if (details == null || details.Count == 0)
                return PromotionSaveResult.Fail(PromotionText.DetailsRequired, hasDetailError: true);

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < details.Count; i++)
            {
                PromotionDetailRow d = details[i];
                int lineNo = i + 1;
                if (d == null)
                    return FailLine("Dòng " + lineNo + ": không có dữ liệu.", i);
                if (d.Note != null && d.Note.Length > PromotionText.MaxTextLength)
                    return FailLine("Dòng " + lineNo + ": " + PromotionText.FieldTooLong("Ghi chú", PromotionText.MaxTextLength), i);

                PromotionSaveResult err = ValidateSingleDetail(kind, d, lineNo, i, seenKeys);
                if (err != null)
                    return err;
            }
            return new PromotionSaveResult { Ok = true };
        }

        public static bool IsValidPercent(decimal percent)
        {
            return percent > 0m && percent <= 100m;
        }

        /// <summary>Cắt chuỗi ghi lưu vết TLUUVET vừa giới hạn VARCHAR(255).</summary>
        public static string ClipLogText(string value)
        {
            return ClipText(value, PromotionText.MaxTextLength);
        }

        public static string ClipText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (maxLength <= 0 || value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength);
        }

        private static PromotionSaveResult ValidateSingleDetail(
            PromotionKind kind,
            PromotionDetailRow d,
            int lineNo,
            int index,
            HashSet<string> seenKeys)
        {
            switch (kind)
            {
                case PromotionKind.ProductGroupPercent:
                    if (string.IsNullOrWhiteSpace(d.ProductGroupId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn nhóm mặt hàng.", index);
                    if (!seenKeys.Add(d.ProductGroupId.Trim()))
                        return FailLine("Dòng " + lineNo + ": nhóm mặt hàng bị lặp trong cùng đợt.", index);
                    if (!IsValidPercent(d.DiscountPercent))
                        return FailLine("Dòng " + lineNo + ": " + PromotionText.DiscountOutOfRange, index);
                    return null;

                case PromotionKind.OrderValuePercent:
                    if (d.OrderValue <= 0m)
                        return FailLine("Dòng " + lineNo + ": giá trị đơn hàng từ phải lớn hơn 0.", index);
                    if (!seenKeys.Add(DecimalKey(d.OrderValue)))
                        return FailLine("Dòng " + lineNo + ": mức giá trị đơn hàng bị lặp trong cùng đợt.", index);
                    if (!IsValidPercent(d.DiscountPercent))
                        return FailLine("Dòng " + lineNo + ": " + PromotionText.DiscountOutOfRange, index);
                    return null;

                case PromotionKind.QuantityPercent:
                    if (d.ItemQuantity <= 0m)
                        return FailLine("Dòng " + lineNo + ": tổng số lượng từ phải lớn hơn 0.", index);
                    if (!seenKeys.Add(DecimalKey(d.ItemQuantity)))
                        return FailLine("Dòng " + lineNo + ": mức tổng số lượng bị lặp trong cùng đợt.", index);
                    if (!IsValidPercent(d.DiscountPercent))
                        return FailLine("Dòng " + lineNo + ": " + PromotionText.DiscountOutOfRange, index);
                    return null;

                case PromotionKind.BuyXGetY:
                    if (string.IsNullOrWhiteSpace(d.ProductId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn mặt hàng mua.", index);
                    if (!seenKeys.Add(d.ProductId.Trim()))
                        return FailLine("Dòng " + lineNo + ": mặt hàng mua bị lặp trong cùng đợt.", index);
                    if (d.BuyQuantity <= 0m)
                        return FailLine("Dòng " + lineNo + ": số lượng mua phải lớn hơn 0.", index);
                    if (d.GiftQuantity <= 0m)
                        return FailLine("Dòng " + lineNo + ": số lượng tặng phải lớn hơn 0.", index);
                    return null;

                case PromotionKind.FixedPrice:
                    if (string.IsNullOrWhiteSpace(d.ProductId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn mặt hàng.", index);
                    if (!seenKeys.Add(d.ProductId.Trim()))
                        return FailLine("Dòng " + lineNo + ": mặt hàng bị lặp trong cùng đợt.", index);
                    if (d.SalePrice <= 0m)
                        return FailLine("Dòng " + lineNo + ": giá khuyến mại phải lớn hơn 0.", index);
                    return null;

                case PromotionKind.ProductPercent:
                    if (string.IsNullOrWhiteSpace(d.ProductId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn mặt hàng.", index);
                    if (!seenKeys.Add(d.ProductId.Trim()))
                        return FailLine("Dòng " + lineNo + ": mặt hàng bị lặp trong cùng đợt.", index);
                    if (!IsValidPercent(d.DiscountPercent))
                        return FailLine("Dòng " + lineNo + ": " + PromotionText.DiscountOutOfRange, index);
                    return null;

                case PromotionKind.ProductAmount:
                    if (string.IsNullOrWhiteSpace(d.ProductId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn mặt hàng.", index);
                    if (!seenKeys.Add(d.ProductId.Trim()))
                        return FailLine("Dòng " + lineNo + ": mặt hàng bị lặp trong cùng đợt.", index);
                    if (d.SalePrice <= 0m)
                        return FailLine("Dòng " + lineNo + ": số tiền giảm mỗi đơn vị phải lớn hơn 0.", index);
                    return null;

                case PromotionKind.GiftByOrderValue:
                    if (d.OrderValue <= 0m)
                        return FailLine("Dòng " + lineNo + ": giá trị đơn hàng từ phải lớn hơn 0.", index);
                    if (!seenKeys.Add(DecimalKey(d.OrderValue)))
                        return FailLine("Dòng " + lineNo + ": mức giá trị đơn hàng bị lặp trong cùng đợt.", index);
                    if (d.GiftQuantity <= 0m)
                        return FailLine("Dòng " + lineNo + ": số lượng tặng phải lớn hơn 0.", index);
                    if (string.IsNullOrWhiteSpace(d.GiftProductId))
                        return FailLine("Dòng " + lineNo + ": chưa chọn mặt hàng tặng.", index);
                    return null;

                default:
                    return PromotionSaveResult.Fail(PromotionText.TypeUnknown, hasTypeError: true);
            }
        }

        private static PromotionSaveResult FailLine(string message, int index)
        {
            return PromotionSaveResult.Fail(message, hasDetailError: true, errorLineIndex: index);
        }

        private static string DecimalKey(decimal value)
        {
            return (value / 1.000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);
        }
    }
}
