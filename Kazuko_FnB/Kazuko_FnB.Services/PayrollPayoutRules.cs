using System;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho phân hệ Chi lương (patch P3b, spec docs/specs/nhan-su-tinh-luong.md §5).
    /// Dùng lại <see cref="VoucherRules"/> và <see cref="PayrollRules"/>; không nhân bản luật quỹ.
    /// </summary>
    public static class PayrollPayoutRules
    {
        /// <summary>
        /// Số tiền mặc định khi tạo phiếu chi lương mới (spec §5 T10):
        /// bằng Còn lại nếu Còn lại &gt; 0, ngược lại bằng Thực nhận (nếu không dương thì 0 để người dùng tự nhập).
        /// </summary>
        public static decimal DefaultPayoutAmount(decimal netSalary, decimal remainingAmount)
        {
            if (remainingAmount > 0m)
                return remainingAmount;
            return netSalary > 0m ? netSalary : 0m;
        }

        /// <summary>
        /// Ngày mặc định khi tạo phiếu chi lương cho bảng lương tháng/năm:
        /// nếu hôm nay nằm trong tháng của bảng lương thì dùng hôm nay, ngược lại lấy ngày cuối tháng đó.
        /// </summary>
        public static DateTime DefaultPayoutDate(int year, int month, DateTime today)
        {
            if (month < PayrollText.MinMonth || month > PayrollText.MaxMonth)
                return today.Date;
            if (today.Year == year && today.Month == month)
                return today.Date;
            int safeYear = year < 1753 ? 1753 : (year > 9999 ? 9999 : year);
            int lastDay = DateTime.DaysInMonth(safeYear, month);
            return new DateTime(safeYear, month, lastDay);
        }

        /// <summary>
        /// Kiểm tra đầu vào phiếu chi lương trước khi ghi DB:
        /// dùng lại <see cref="VoucherRules.Validate"/> (số phiếu, cửa hàng, số tiền &gt; 0, chuyển khoản bắt buộc TK ngân hàng, độ dài 255)
        /// và kiểm tra thêm ràng buộc bảng lương, nhân viên và ngày nằm trong tháng của bảng lương.
        /// </summary>
        public static string Validate(VoucherRow row, int periodMonth, int periodYear)
        {
            if (row == null)
                return PayrollPayoutText.NotFound;
            if (string.IsNullOrWhiteSpace(row.PayrollId))
                return PayrollPayoutText.PayrollRequired;
            if (string.IsNullOrWhiteSpace(row.EmployeeId))
                return PayrollPayoutText.EmployeeRequired;

            string baseErr = VoucherRules.Validate(row);
            if (baseErr != null)
                return baseErr;

            if (row.Amount > PayrollPayoutText.MaxAmount)
                return "Số tiền chi lương vượt quá giới hạn cho phép.";

            if (periodMonth >= PayrollText.MinMonth && periodMonth <= PayrollText.MaxMonth)
            {
                if (row.Date.Month != periodMonth || row.Date.Year != periodYear)
                    return PayrollPayoutText.MonthLimitBlocked(periodMonth, periodYear);
            }

            return null;
        }

        /// <summary>
        /// Chỉ cho phép xem/sửa/xóa phiếu chi lương (LOAI = 1, LATAMUNG = 0, TBANGLUONGID có giá trị,
        /// không gắn chứng từ khác) thuộc đúng bảng lương đang mở.
        /// </summary>
        public static bool IsEditablePayout(VoucherRow row, string expectedPayrollId = null)
        {
            if (row == null)
                return false;
            if (row.Kind != VoucherKind.Expense || row.IsAdvance)
                return false;
            if (string.IsNullOrWhiteSpace(row.PayrollId))
                return false;
            if (!string.IsNullOrEmpty(expectedPayrollId)
                && !string.Equals(row.PayrollId, expectedPayrollId, StringComparison.Ordinal))
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(row.BookingId)
                && string.IsNullOrWhiteSpace(row.OrderId)
                && string.IsNullOrWhiteSpace(row.PrepaidCardId)
                && !row.IsDebtReceipt;
        }

        public static bool CanView(UserSession session)
        {
            return PayrollRules.CanPayroll(session, RoleModes.View);
        }

        /// <summary>Quyền mở và thao tác Chi lương: cần quyền "Tính lương" Sửa (Admin luôn được).</summary>
        public static bool CanPayOut(UserSession session)
        {
            return PayrollRules.CanPayroll(session, RoleModes.Edit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return VoucherRules.CanChangeDate(session);
        }

        public static string FormatMoney(decimal amount)
        {
            return VoucherConstants.FormatMoneyVi(amount);
        }
    }
}
