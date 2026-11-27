using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc thuần (không chạm DB) cho Danh mục thẻ trả trước (DTHETRATRUOC / DNHOMTHETRATRUOC)
    /// và phiếu nạp/chi trả thẻ (spec docs/specs/danh-muc-the-tra-truoc.md §2–§3).
    /// </summary>
    public static class PrepaidCardRules
    {
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            return ((session.Permissions.ModeById(PrepaidCardText.FunctionId) & bit) == bit)
                || session.Permissions.Can(PrepaidCardText.FunctionName, bit);
        }

        /// <summary>Quyền Nạp tiền thẻ trả trước: gắn với quyền Thêm của Danh mục thẻ trả trước (T6).</summary>
        public static bool CanTopUp(UserSession session)
        {
            return Can(session, RoleModes.Add);
        }

        /// <summary>Quyền Chi trả tiền thẻ trả trước: gắn với quyền Sửa của Danh mục thẻ trả trước (T6).</summary>
        public static bool CanWithdraw(UserSession session)
        {
            return Can(session, RoleModes.Edit);
        }

        public static bool CanChangeVoucherDate(UserSession session)
        {
            return VoucherRules.CanChangeDate(session);
        }

        /// <summary>Kiểm tra tính hợp lệ của thẻ trả trước (không chạm DB).</summary>
        public static PrepaidCardSaveResult ValidateCard(PrepaidCardRow row, bool isDuplicateCode)
        {
            if (row == null)
                return PrepaidCardSaveResult.Fail("Không có dữ liệu thẻ trả trước.");

            var errors = new List<string>();
            bool codeErr = false;

            if (string.IsNullOrWhiteSpace(row.Code))
            {
                errors.Add(PrepaidCardText.CodeEmpty);
                codeErr = true;
            }
            else if (row.Code.Length > PrepaidCardText.MaxTextLength)
            {
                errors.Add(PrepaidCardText.FieldTooLong("Mã thẻ", PrepaidCardText.MaxTextLength));
                codeErr = true;
            }
            else if (isDuplicateCode)
            {
                errors.Add(PrepaidCardText.CodeDuplicate(row.Code));
                codeErr = true;
            }

            if (row.Note != null && row.Note.Length > PrepaidCardText.MaxTextLength)
                errors.Add(PrepaidCardText.FieldTooLong("Ghi chú", PrepaidCardText.MaxTextLength));

            if (errors.Count > 0)
            {
                return PrepaidCardSaveResult.Fail(
                    string.Join(Environment.NewLine, errors.ToArray()),
                    hasCodeError: codeErr);
            }
            return new PrepaidCardSaveResult { Ok = true };
        }

        /// <summary>Kiểm tra tính hợp lệ của nhóm thẻ trả trước (không chạm DB).</summary>
        public static PrepaidCardGroupSaveResult ValidateGroup(PrepaidCardGroupRow row, bool isDuplicateName)
        {
            if (row == null)
                return PrepaidCardGroupSaveResult.Fail("Không có dữ liệu nhóm thẻ trả trước.");

            if (string.IsNullOrWhiteSpace(row.Name))
                return PrepaidCardGroupSaveResult.Fail(PrepaidCardText.GroupNameEmpty);
            if (row.Name.Length > PrepaidCardText.MaxTextLength)
                return PrepaidCardGroupSaveResult.Fail(
                    PrepaidCardText.FieldTooLong("Tên nhóm thẻ", PrepaidCardText.MaxTextLength));
            if (row.Note != null && row.Note.Length > PrepaidCardText.MaxTextLength)
                return PrepaidCardGroupSaveResult.Fail(
                    PrepaidCardText.FieldTooLong("Ghi chú", PrepaidCardText.MaxTextLength));
            if (isDuplicateName)
                return PrepaidCardGroupSaveResult.Fail(PrepaidCardText.GroupNameDuplicate(row.Name));
            return new PrepaidCardGroupSaveResult { Ok = true };
        }

        /// <summary>
        /// Kiểm tra đầu vào phiếu nạp / chi trả thẻ trước khi ghi DB:
        /// dùng lại <see cref="VoucherRules.Validate"/> và giới hạn tối đa 9 chữ số nguyên.
        /// </summary>
        public static string ValidateVoucher(VoucherRow row)
        {
            if (row == null)
                return PrepaidCardText.VoucherNotFound;
            if (string.IsNullOrWhiteSpace(row.PrepaidCardId))
                return PrepaidCardText.NotFound;

            string baseErr = VoucherRules.Validate(row);
            if (baseErr != null)
                return baseErr;

            if (row.Amount > PrepaidCardText.MaxAmount)
                return PrepaidCardText.AmountTooLarge;

            return null;
        }

        /// <summary>Chỉ cho phép sửa/xóa phiếu thu/chi gắn đúng thẻ trả trước (không gắn chứng từ liên kết khác).</summary>
        public static bool IsEditableCardVoucher(VoucherRow row, string expectedCardId = null)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.PrepaidCardId))
                return false;
            if (row.RawLoai != 0 && row.RawLoai != 1)
                return false;
            if (!string.IsNullOrEmpty(expectedCardId)
                && !string.Equals(row.PrepaidCardId, expectedCardId, StringComparison.Ordinal))
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(row.BookingId)
                && string.IsNullOrWhiteSpace(row.OrderId)
                && string.IsNullOrWhiteSpace(row.PayrollId)
                && !row.IsAdvance
                && !row.IsDebtReceipt
                && !string.Equals(row.ReasonId, FundReasons.OpeningBalanceId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(row.ReasonId, FundReasons.TransferId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Thông báo chặn xóa thẻ nêu đúng số lượng từng loại tham chiếu (spec §2).</summary>
        public static string FormatDeleteBlockedMessage(PrepaidCardReferenceCounts counts)
        {
            if (counts == null || !counts.HasAny)
                return null;
            var parts = new List<string>(3);
            if (counts.VoucherCount > 0)
                parts.Add(counts.VoucherCount + " phiếu thu chi");
            if (counts.OrderCount > 0)
                parts.Add(counts.OrderCount + " hóa đơn");
            if (counts.CustomerCount > 0)
                parts.Add(counts.CustomerCount + " khách đang giữ");
            return "Thẻ đã có " + string.Join(", ", parts.ToArray()) + ", không xóa được";
        }

        /// <summary>Cảnh báo xác nhận khi nạp tiền vào thẻ đã khóa hoặc đã hết hạn (spec §2, T4); null nếu thẻ bình thường.</summary>
        public static string BuildLockedOrExpiredWarning(PrepaidCardRow card, DateTime serverDate)
        {
            if (card == null)
                return null;
            bool locked = card.Locked;
            bool expired = card.ExpiryDate.HasValue && card.ExpiryDate.Value.Date < serverDate.Date;
            if (locked && expired)
                return "Thẻ này đã bị khóa và đã hết hạn sử dụng. Bạn có chắc chắn muốn nạp tiền vào thẻ này không?";
            if (locked)
                return "Thẻ này đã bị khóa. Bạn có chắc chắn muốn nạp tiền vào thẻ này không?";
            if (expired)
                return "Thẻ này đã hết hạn sử dụng. Bạn có chắc chắn muốn nạp tiền vào thẻ này không?";
            return null;
        }

        /// <summary>Định dạng tiền độc lập culture hệ điều hành (ngăn nghìn '.', thập phân ',').</summary>
        public static string FormatMoney(decimal value)
        {
            return NumberInput.FormatForDisplay(value, 2, true);
        }

        /// <summary>Cắt chuỗi ghi lưu vết TLUUVET vừa giới hạn VARCHAR(255).</summary>
        public static string ClipLogText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= PrepaidCardText.MaxTextLength
                ? value
                : value.Substring(0, PrepaidCardText.MaxTextLength);
        }
    }
}
