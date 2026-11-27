using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho màn Tạm ứng lương (spec docs/specs/nhan-su-thuong-phat-tam-ung.md §3).
    /// Dùng lại luật chung của <see cref="VoucherRules"/>; không nhân bản luật quỹ.
    /// </summary>
    public static class SalaryAdvanceRules
    {
        /// <summary>Kiểm tra đầu vào phiếu tạm ứng lương (gọi tiếp <see cref="VoucherRules.Validate"/>).</summary>
        public static string Validate(VoucherRow row, RewardPenaltyMonthLimit monthLimit = null)
        {
            if (row == null)
                return SalaryAdvanceText.NotFound;
            if (row.Transfer)
                return SalaryAdvanceText.TransferNotAllowed;
            if (string.IsNullOrWhiteSpace(row.EmployeeId))
                return SalaryAdvanceText.EmployeeRequired;

            string baseErr = VoucherRules.Validate(row);
            if (baseErr != null)
                return baseErr;

            if (row.Amount > SalaryAdvanceText.MaxAmount)
                return SalaryAdvanceText.AmountTooLarge;

            if (monthLimit != null && monthLimit.Month >= 1 && monthLimit.Month <= 12 && monthLimit.Year > 0)
            {
                if (row.Date.Month != monthLimit.Month || row.Date.Year != monthLimit.Year)
                    return RewardPenaltyText.MonthLimitBlocked(monthLimit.Month, monthLimit.Year);
            }

            return null;
        }

        /// <summary>Chỉ cho phép sửa/xóa phiếu chi tạm ứng lương (LOAI = 1, LATAMUNG = 30) không gắn bảng lương/chứng từ khác.</summary>
        public static bool IsEditableAdvance(VoucherRow row)
        {
            if (row == null)
                return false;
            return row.Kind == VoucherKind.Expense
                && row.IsAdvance
                && string.IsNullOrWhiteSpace(row.PayrollId)
                && string.IsNullOrWhiteSpace(row.BookingId)
                && string.IsNullOrWhiteSpace(row.OrderId)
                && string.IsNullOrWhiteSpace(row.PrepaidCardId)
                && !row.IsDebtReceipt;
        }

        /// <summary>Tên nhân viên hiển thị trên lưới: ưu tiên DNHANVIEN.NAME, rỗng thì TENDOITUONG.</summary>
        public static string DisplayEmployee(VoucherRow row)
        {
            if (row == null)
                return "";
            if (!string.IsNullOrWhiteSpace(row.EmployeeName))
                return row.EmployeeName;
            return row.ObjectName ?? "";
        }

        /// <summary>Tìm nhanh không dấu tiếng Việt trên số phiếu, nhân viên, lý do, diễn giải, cửa hàng, ghi chú.</summary>
        public static bool MatchesSearch(VoucherRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.EmployeeName ?? "",
                row.ObjectName ?? "",
                row.ReasonName ?? "",
                row.Description ?? "",
                row.StoreName ?? "",
                row.Note ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if (session.Permissions.Can(SalaryAdvanceText.FunctionName, bit))
                return true;
            return (session.Permissions.ModeById(SalaryAdvanceText.FunctionId) & bit) == bit;
        }

        public static bool CanChangeDate(UserSession session)
        {
            return VoucherRules.CanChangeDate(session);
        }

        public static string Clip(string text, int maxLength = VoucherConstants.MaxTextLength)
        {
            string s = text ?? "";
            return maxLength > 0 && s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }
    }
}
