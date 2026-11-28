using System;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần, phân quyền và định dạng hiển thị cho màn Tính lương / Chấm công.
    /// </summary>
    public static class PayrollRules
    {
        public static string ValidatePeriod(int month, int year)
        {
            if (month < PayrollText.MinMonth || month > PayrollText.MaxMonth)
                return PayrollText.MonthInvalid;
            if (year < PayrollText.MinYear || year > PayrollText.MaxYear)
                return PayrollText.YearInvalid;
            return null;
        }

        public static string ValidatePeriodName(string name)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0)
                return PayrollText.NameRequired;
            if (trimmed.Length > PayrollText.MaxTextLength)
                return PayrollText.NameTooLong;
            return null;
        }

        /// <summary>Kiểm quyền theo chức năng "Tính lương"; Admin luôn được.</summary>
        public static bool CanPayroll(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            return session.Permissions.Can(PayrollText.PayrollFunctionName, bit);
        }

        /// <summary>Kiểm quyền theo chức năng "Chấm công" (tên hoặc SFUNCTIONID); Admin luôn được.</summary>
        public static bool CanAttendance(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if (session.Permissions.Can(PayrollText.AttendanceFunctionName, bit))
                return true;
            return (session.Permissions.ModeById(PayrollText.AttendanceFunctionId) & bit) == bit;
        }

        public static bool Can(UserSession session, PayrollMode mode, int bit)
        {
            return mode == PayrollMode.Payroll
                ? CanPayroll(session, bit)
                : CanAttendance(session, bit);
        }

        public static string FunctionNameOf(PayrollMode mode)
        {
            return mode == PayrollMode.Payroll
                ? PayrollText.PayrollFunctionName
                : PayrollText.AttendanceFunctionName;
        }

        /// <summary>Nhãn thứ viết gọn cho tiêu đề cột ngày: T2..T7, CN.</summary>
        public static string DayOfWeekShortVi(DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Monday: return "T2";
                case DayOfWeek.Tuesday: return "T3";
                case DayOfWeek.Wednesday: return "T4";
                case DayOfWeek.Thursday: return "T5";
                case DayOfWeek.Friday: return "T6";
                case DayOfWeek.Saturday: return "T7";
                default: return "CN";
            }
        }

        public static string DayHeaderText(DateTime date)
        {
            return date.Day.ToString() + Environment.NewLine + DayOfWeekShortVi(date);
        }

        public static string FormatMoneyDisplay(decimal amount)
        {
            decimal rounded = PayrollCalculator.RoundForDisplay(amount);
            if (rounded == 0m)
                return "0";
            return rounded.ToString("#,##0", NumberInput.ViFormat);
        }

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static string FormatShiftCount(decimal shifts)
        {
            return NumberInput.FormatForDisplay(shifts, 2, true);
        }

        /// <summary>Chuỗi thống kê nhân viên cố định theo định dạng số vi-VN (không phụ thuộc culture máy).</summary>
        public static string FormatEmployeeStats(string employeeName, decimal totalShifts, int totalDays)
        {
            return string.Format(ViCulture, "NV: {0} — Số ca: {1} · Số ngày: {2}",
                employeeName ?? "",
                FormatShiftCount(totalShifts),
                totalDays);
        }

        public static string StatusSymbol(AttendanceStatus status)
        {
            switch (status)
            {
                case AttendanceStatus.Present: return "✓";
                case AttendanceStatus.PaidLeave: return "P";
                case AttendanceStatus.UnpaidLeave: return "K";
                default: return "";
            }
        }

        public static string StatusLabel(AttendanceStatus status)
        {
            switch (status)
            {
                case AttendanceStatus.Present: return "Đi làm";
                case AttendanceStatus.PaidLeave: return "Nghỉ có phép";
                case AttendanceStatus.UnpaidLeave: return "Nghỉ không phép";
                default: return "Trống";
            }
        }
    }
}
