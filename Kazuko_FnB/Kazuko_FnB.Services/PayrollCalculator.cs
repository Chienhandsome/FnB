using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Hàm tính công thức lương thuần (không phụ thuộc DB hay UI), bám đúng UpdateThucNhan của app gốc
    /// và spec docs/specs/nhan-su-tinh-luong.md §3.
    /// </summary>
    public static class PayrollCalculator
    {
        /// <summary>
        /// Tỉ lệ lương của ca: TILELUONG/100; TILELUONG = 0 coi là 100 (1,0); TILELUONG âm coi là 0 (T5).
        /// </summary>
        public static decimal EffectiveShiftRate(decimal salaryRatePercent)
        {
            if (salaryRatePercent < 0m)
                return 0m;
            if (salaryRatePercent == 0m)
                return 1m;
            return salaryRatePercent / 100m;
        }

        /// <summary>Số ngày của tháng (an toàn khi năm = 0).</summary>
        public static int DaysInMonth(int year, int month)
        {
            if (month < 1 || month > 12)
                return 0;
            int safeYear = year < 1 ? 1 : (year > 9999 ? 9999 : year);
            return DateTime.DaysInMonth(safeYear, month);
        }

        /// <summary>Đếm số ngày thuộc một thứ cụ thể (VD: Chủ nhật, Thứ 7) theo đúng lịch tháng/năm.</summary>
        public static int CountDayOfWeek(int year, int month, DayOfWeek target)
        {
            int total = DaysInMonth(year, month);
            if (total <= 0)
                return 0;
            int safeYear = year < 1 ? 1 : (year > 9999 ? 9999 : year);
            int count = 0;
            for (int d = 1; d <= total; d++)
            {
                if (new DateTime(safeYear, month, d).DayOfWeek == target)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Mẫu số số ngày làm việc trong tháng cho cách tính Theo tháng (0):
        /// trừ Chủ nhật nếu offSunday, trừ Thứ 7 nếu offSaturday, trừ cả hai nếu cả hai bật.
        /// </summary>
        public static int WorkingDaysInMonth(int year, int month, bool offSunday, bool offSaturday)
        {
            int total = DaysInMonth(year, month);
            if (total <= 0)
                return 0;
            int result = total;
            if (offSunday)
                result -= CountDayOfWeek(year, month, DayOfWeek.Sunday);
            if (offSaturday)
                result -= CountDayOfWeek(year, month, DayOfWeek.Saturday);
            return result > 0 ? result : 0;
        }

        /// <summary>
        /// Tính lương cho một nhân viên từ lưới chấm công trong bộ nhớ.
        /// Không làm tròn trung gian (giữ nguyên decimal theo spec §3 T7).
        /// </summary>
        public static PayrollCalculationResult CalculateEmployee(
            int year,
            int month,
            PayrollEmployeeEntry employee,
            IList<PayrollShiftInfo> shifts)
        {
            var res = new PayrollCalculationResult();
            if (employee == null)
                return res;

            int totalDays = DaysInMonth(year, month);
            int safeYear = year < 1 ? 1 : (year > 9999 ? 9999 : year);

            decimal soCaDiLam = 0m;
            int soNgayDiLam = 0;

            if (totalDays > 0 && shifts != null && shifts.Count > 0)
            {
                for (int d = 1; d <= totalDays; d++)
                {
                    var date = new DateTime(safeYear, month, d);
                    bool hasDiLam = false;
                    for (int s = 0; s < shifts.Count; s++)
                    {
                        PayrollShiftInfo sh = shifts[s];
                        if (sh == null || string.IsNullOrEmpty(sh.Id))
                            continue;
                        AttendanceStatus st = employee.GetStatus(sh.Id, date);
                        if (st == AttendanceStatus.Present || st == AttendanceStatus.PaidLeave)
                        {
                            soCaDiLam += EffectiveShiftRate(sh.SalaryRate);
                            hasDiLam = true;
                        }
                    }
                    if (hasDiLam)
                        soNgayDiLam++;
                }
            }

            int workingDays = WorkingDaysInMonth(year, month, employee.OffSunday, employee.OffSaturday);
            decimal baseSalary = employee.SalaryMode == EmployeeSalaryMode.ByShift
                ? employee.ShiftSalary
                : employee.MonthlySalary;

            decimal tongLuong;
            if (employee.SalaryMode == EmployeeSalaryMode.ByShift)
            {
                tongLuong = employee.ShiftSalary * soCaDiLam;
            }
            else if (employee.SalaryMode == EmployeeSalaryMode.MonthByShift)
            {
                tongLuong = totalDays > 0 ? soCaDiLam * employee.MonthlySalary / totalDays : 0m;
            }
            else
            {
                tongLuong = workingDays > 0 ? soNgayDiLam * employee.MonthlySalary / workingDays : 0m;
            }

            decimal thucNhan = tongLuong + employee.Bonus - employee.Penalty - employee.Advance;

            res.TotalShifts = soCaDiLam;
            res.TotalDays = soNgayDiLam;
            res.WorkingDaysInMonth = workingDays;
            res.BaseSalary = baseSalary;
            res.TotalSalary = tongLuong;
            res.NetSalary = thucNhan;
            return res;
        }

        /// <summary>Tính lại và ghi thẳng kết quả vào <paramref name="employee"/> trong bộ nhớ.</summary>
        public static PayrollCalculationResult RecalculateEmployee(
            int year,
            int month,
            PayrollEmployeeEntry employee,
            IList<PayrollShiftInfo> shifts)
        {
            PayrollCalculationResult res = CalculateEmployee(year, month, employee, shifts);
            if (employee != null)
            {
                employee.TotalShifts = res.TotalShifts;
                employee.TotalDays = res.TotalDays;
                employee.BaseSalary = res.BaseSalary;
                employee.TotalSalary = res.TotalSalary;
                employee.NetSalary = res.NetSalary;
            }
            return res;
        }

        /// <summary>Chỉ cập nhật số ca và số ngày đi làm (dùng cho chế độ Chấm công hoặc sau khi nạp snapshot).</summary>
        public static void RecalculateAttendanceCountsOnly(
            int year,
            int month,
            PayrollEmployeeEntry employee,
            IList<PayrollShiftInfo> shifts)
        {
            if (employee == null)
                return;
            int totalDays = DaysInMonth(year, month);
            int safeYear = year < 1 ? 1 : (year > 9999 ? 9999 : year);
            decimal soCa = 0m;
            int soNgay = 0;

            if (totalDays > 0 && shifts != null && shifts.Count > 0)
            {
                for (int d = 1; d <= totalDays; d++)
                {
                    var date = new DateTime(safeYear, month, d);
                    bool hasDiLam = false;
                    for (int s = 0; s < shifts.Count; s++)
                    {
                        PayrollShiftInfo sh = shifts[s];
                        if (sh == null || string.IsNullOrEmpty(sh.Id))
                            continue;
                        AttendanceStatus st = employee.GetStatus(sh.Id, date);
                        if (st == AttendanceStatus.Present || st == AttendanceStatus.PaidLeave)
                        {
                            soCa += EffectiveShiftRate(sh.SalaryRate);
                            hasDiLam = true;
                        }
                    }
                    if (hasDiLam)
                        soNgay++;
                }
            }

            employee.TotalShifts = soCa;
            employee.TotalDays = soNgay;
        }

        /// <summary>Làm tròn hiển thị không số lẻ theo spec §3 T7 (MidpointRounding.AwayFromZero).</summary>
        public static decimal RoundForDisplay(decimal amount)
        {
            return Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Kiểm tra nhân viên có ít nhất một ô chấm công (1/2/3) hoặc có phát sinh tiền hay không (spec §4).</summary>
        public static bool HasAttendanceOrMoney(PayrollEmployeeEntry employee)
        {
            if (employee == null)
                return false;
            foreach (KeyValuePair<string, AttendanceStatus> kv in employee.Attendance)
            {
                if (kv.Value != AttendanceStatus.Empty)
                    return true;
            }
            return employee.TotalSalary != 0m
                || employee.Bonus != 0m
                || employee.Penalty != 0m
                || employee.Advance != 0m
                || employee.NetSalary != 0m;
        }
    }
}
