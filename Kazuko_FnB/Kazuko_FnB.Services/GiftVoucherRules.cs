using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc thuần (không chạm DB) cho Danh mục voucher (DVOUCHER / DNHOMVOUCHER,
    /// spec docs/specs/danh-muc-voucher.md §2): kiểm quyền, kiểm nhập liệu, kiểm khóa sửa
    /// khi voucher đã có hóa đơn tham chiếu, lọc voucher còn dùng được, cắt chuỗi lưu vết.
    /// </summary>
    public static class GiftVoucherRules
    {
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(GiftVoucherText.FunctionName, bit);
        }

        /// <summary>Kiểm tra tính hợp lệ của một dòng voucher (không chạm DB).</summary>
        public static GiftVoucherSaveResult ValidateVoucher(GiftVoucherRow row, bool isDuplicateCode)
        {
            if (row == null)
                return GiftVoucherSaveResult.Fail("Không có dữ liệu voucher.");

            var errors = new List<string>();
            bool codeErr = false;
            bool valErr = false;
            bool dateErr = false;

            if (string.IsNullOrWhiteSpace(row.Code))
            {
                errors.Add(GiftVoucherText.CodeEmpty);
                codeErr = true;
            }
            else if (row.Code.Length > GiftVoucherText.MaxTextLength)
            {
                errors.Add(GiftVoucherText.FieldTooLong("Mã voucher", GiftVoucherText.MaxTextLength));
                codeErr = true;
            }
            else if (isDuplicateCode)
            {
                errors.Add(GiftVoucherText.CodeDuplicate(row.Code));
                codeErr = true;
            }

            if (row.Value <= 0m)
            {
                errors.Add(GiftVoucherText.ValueInvalid);
                valErr = true;
            }

            if (row.IssueDate.HasValue && row.ExpiryDate.HasValue
                && row.ExpiryDate.Value.Date < row.IssueDate.Value.Date)
            {
                errors.Add(GiftVoucherText.ExpiryBeforeIssue);
                dateErr = true;
            }

            if (row.Note != null && row.Note.Length > GiftVoucherText.MaxTextLength)
                errors.Add(GiftVoucherText.FieldTooLong("Ghi chú", GiftVoucherText.MaxTextLength));

            if (errors.Count > 0)
            {
                return GiftVoucherSaveResult.Fail(
                    string.Join(Environment.NewLine, errors.ToArray()),
                    hasCodeError: codeErr,
                    hasValueError: valErr,
                    hasDateError: dateErr);
            }
            return new GiftVoucherSaveResult { Ok = true };
        }

        /// <summary>
        /// Kiểm tra có thay đổi trường nào ngoài Ghi chú hay không (dùng để chặn sửa mã/giá trị/hạn/ngày phát hành/nhóm/đối tác
        /// khi voucher đã có hóa đơn tham chiếu trong TDONHANG).
        /// </summary>
        public static bool HasLockedFieldChanges(GiftVoucherRow existing, GiftVoucherRow updated)
        {
            if (existing == null || updated == null)
                return false;
            if (!string.Equals(existing.Code ?? "", updated.Code ?? "", StringComparison.Ordinal))
                return true;
            if (existing.Value != updated.Value)
                return true;
            if (!NullableDateEquals(existing.IssueDate, updated.IssueDate))
                return true;
            if (!NullableDateEquals(existing.ExpiryDate, updated.ExpiryDate))
                return true;
            if (!string.Equals(existing.GroupId ?? "", updated.GroupId ?? "", StringComparison.Ordinal))
                return true;
            if (!string.Equals(existing.PartnerId ?? "", updated.PartnerId ?? "", StringComparison.Ordinal))
                return true;
            return false;
        }

        /// <summary>Voucher còn dùng được tại ngày <paramref name="serverDate"/> (chưa dùng bởi hóa đơn đã thanh toán, trong khoảng phát hành–hạn dùng).</summary>
        public static bool IsUsableOn(GiftVoucherRow row, DateTime serverDate)
        {
            if (row == null || row.IsUsed)
                return false;
            DateTime today = serverDate.Date;
            if (row.IssueDate.HasValue && row.IssueDate.Value.Date > today)
                return false;
            if (row.ExpiryDate.HasValue && row.ExpiryDate.Value.Date < today)
                return false;
            return true;
        }

        /// <summary>Kiểm tra tính hợp lệ của một nhóm voucher (không chạm DB).</summary>
        public static GiftVoucherGroupSaveResult ValidateGroup(GiftVoucherGroupRow row, bool isDuplicateName)
        {
            if (row == null)
                return GiftVoucherGroupSaveResult.Fail("Không có dữ liệu nhóm voucher.");

            if (string.IsNullOrWhiteSpace(row.Name))
                return GiftVoucherGroupSaveResult.Fail(GiftVoucherText.GroupNameEmpty);
            if (row.Name.Length > GiftVoucherText.MaxTextLength)
                return GiftVoucherGroupSaveResult.Fail(
                    GiftVoucherText.FieldTooLong("Tên nhóm voucher", GiftVoucherText.MaxTextLength));
            if (row.Note != null && row.Note.Length > GiftVoucherText.MaxTextLength)
                return GiftVoucherGroupSaveResult.Fail(
                    GiftVoucherText.FieldTooLong("Ghi chú", GiftVoucherText.MaxTextLength));
            if (isDuplicateName)
                return GiftVoucherGroupSaveResult.Fail(GiftVoucherText.GroupNameDuplicate(row.Name));
            return new GiftVoucherGroupSaveResult { Ok = true };
        }

        /// <summary>Cắt chuỗi ghi lưu vết TLUUVET vừa giới hạn VARCHAR(255) để không tràn cột khi tên dài 255 ký tự.</summary>
        public static string ClipLogText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= GiftVoucherText.MaxTextLength
                ? value
                : value.Substring(0, GiftVoucherText.MaxTextLength);
        }

        private static bool NullableDateEquals(DateTime? a, DateTime? b)
        {
            if (!a.HasValue && !b.HasValue)
                return true;
            if (!a.HasValue || !b.HasValue)
                return false;
            return a.Value.Date == b.Value.Date;
        }
    }
}
