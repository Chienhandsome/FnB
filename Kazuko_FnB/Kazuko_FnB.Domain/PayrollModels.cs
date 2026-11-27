using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Chế độ mở màn PayrollView: Tính lương (đầy đủ) hoặc Chấm công (ẩn và không đọc/ghi tiền).</summary>
    public enum PayrollMode
    {
        Payroll = 0,
        Attendance = 1
    }

    /// <summary>Trạng thái chấm công một ô (nhân viên, ca, ngày) khớp enum DiLamMode của app gốc (0..3).</summary>
    public enum AttendanceStatus
    {
        Empty = 0,
        Present = 1,
        PaidLeave = 2,
        UnpaidLeave = 3
    }

    /// <summary>Một kỳ bảng lương trong bảng TBANGLUONG (mỗi cặp Tháng/Năm một dòng).</summary>
    public sealed class PayrollPeriodRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>Ca làm việc đang dùng (DCALAMVIEC STATUS = 30) dùng để dựng dòng lưới và tính tỉ lệ công.</summary>
    public sealed class PayrollShiftInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Tỉ lệ lương (%) trong DCALAMVIEC.TILELUONG (0 coi là 100, âm coi là 0).</summary>
        public decimal SalaryRate { get; set; }
    }

    /// <summary>Một bản ghi chi tiết chấm công trong TBANGLUONGCHITIET.</summary>
    public sealed class PayrollDetailRecord
    {
        public string Id { get; set; }
        public string EmployeeId { get; set; }
        public string ShiftId { get; set; }
        public DateTime Date { get; set; }
        public AttendanceStatus Status { get; set; }
    }

    /// <summary>Dữ liệu tổng hợp và chấm công trong bộ nhớ của một nhân viên trong một bảng lương.</summary>
    public sealed class PayrollEmployeeEntry
    {
        public PayrollEmployeeEntry()
        {
            Attendance = new Dictionary<string, AttendanceStatus>(StringComparer.Ordinal);
        }

        public string EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public bool IsActiveEmployee { get; set; }

        /// <summary>ID dòng TBANGLUONGTONGHOP đã lưu trong DB (rỗng nếu chưa có).</summary>
        public string SummaryId { get; set; }
        public bool HasSavedSummary { get { return !string.IsNullOrEmpty(SummaryId); } }

        // Thông số tính lương (snapshot từ TBANGLUONGTONGHOP nếu đã lưu, hoặc từ DNHANVIEN nếu chưa lưu / sau Tải lại).
        public int SalaryMode { get; set; }
        public decimal ShiftSalary { get; set; }
        public decimal MonthlySalary { get; set; }
        public bool OffSunday { get; set; }
        public bool OffSaturday { get; set; }

        // Các khoản tiền của nhân viên trong tháng.
        public decimal BaseSalary { get; set; }
        public decimal TotalSalary { get; set; }
        public decimal Bonus { get; set; }
        public decimal Penalty { get; set; }
        public decimal Advance { get; set; }
        public decimal NetSalary { get; set; }

        // Tổng số ca và số ngày đi làm tính từ lưới chấm công.
        public decimal TotalShifts { get; set; }
        public int TotalDays { get; set; }

        /// <summary>Chấm công theo khóa "shiftId|yyyy-MM-dd".</summary>
        public Dictionary<string, AttendanceStatus> Attendance { get; private set; }

        public static string CellKey(string shiftId, DateTime date)
        {
            return (shiftId ?? "") + "|" + date.ToString("yyyy-MM-dd");
        }

        public AttendanceStatus GetStatus(string shiftId, DateTime date)
        {
            AttendanceStatus st;
            return Attendance.TryGetValue(CellKey(shiftId, date), out st) ? st : AttendanceStatus.Empty;
        }

        public void SetStatus(string shiftId, DateTime date, AttendanceStatus status)
        {
            string key = CellKey(shiftId, date);
            if (status == AttendanceStatus.Empty)
                Attendance.Remove(key);
            else
                Attendance[key] = status;
        }
    }

    /// <summary>Toàn bộ dữ liệu nạp lên lưới của một kỳ bảng lương.</summary>
    public sealed class PayrollSheetData
    {
        public PayrollSheetData()
        {
            Shifts = new List<PayrollShiftInfo>();
            Employees = new List<PayrollEmployeeEntry>();
        }

        public PayrollMode Mode { get; set; }
        public PayrollPeriodRow Period { get; set; }
        public List<PayrollShiftInfo> Shifts { get; set; }
        public List<PayrollEmployeeEntry> Employees { get; set; }
        public int DuplicateDetailCount { get; set; }
        public string Warning { get; set; }
    }

    /// <summary>Dữ liệu tươi đọc từ DNHANVIEN + TTHUONGPHAT + TTHUCHI cho thao tác Tải lại.</summary>
    public sealed class PayrollLiveSnapshot
    {
        public string EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public bool IsActiveEmployee { get; set; }
        public int SalaryMode { get; set; }
        public decimal ShiftSalary { get; set; }
        public decimal MonthlySalary { get; set; }
        public bool OffSunday { get; set; }
        public bool OffSaturday { get; set; }
        public decimal Bonus { get; set; }
        public decimal Penalty { get; set; }
        public decimal Advance { get; set; }
    }

    /// <summary>Kết quả tính công thức lương thuần cho một nhân viên.</summary>
    public sealed class PayrollCalculationResult
    {
        public decimal TotalShifts { get; set; }
        public int TotalDays { get; set; }
        public int WorkingDaysInMonth { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal TotalSalary { get; set; }
        public decimal NetSalary { get; set; }
    }

    /// <summary>Kết quả thêm mới kỳ bảng lương.</summary>
    public sealed class PayrollCreateResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public PayrollPeriodRow Created { get; set; }

        public static PayrollCreateResult Fail(string message)
        {
            return new PayrollCreateResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Kết quả lưu (Cập nhật) bảng lương hoặc chấm công.</summary>
    public sealed class PayrollSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int SavedEmployeeCount { get; set; }
        public int InsertedDetailCount { get; set; }
        public int UpdatedDetailCount { get; set; }
        public int DeletedDetailCount { get; set; }
        public int RemovedDuplicateDetailCount { get; set; }

        public static PayrollSaveResult Fail(string message)
        {
            return new PayrollSaveResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Kết quả xóa kỳ bảng lương.</summary>
    public sealed class PayrollDeleteResult
    {
        public PayrollDeleteResult()
        {
            BlockingVoucherNumbers = new List<string>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> BlockingVoucherNumbers { get; set; }

        public static PayrollDeleteResult Fail(string message)
        {
            return new PayrollDeleteResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Hằng số định danh, thông báo và hàm chuẩn hóa chuỗi của phân hệ Tính lương / Chấm công.</summary>
    public static class PayrollText
    {
        public const string PayrollFunctionName = "Tính lương";
        public const string AttendanceFunctionName = "Chấm công";
        public const string AttendanceFunctionId = "8761354d-5b96-4a52-a3ea-7cc62d7693ea";

        public const string PeriodTableName = "TBANGLUONG";
        public const string SummaryTableName = "TBANGLUONGTONGHOP";
        public const string DetailTableName = "TBANGLUONGCHITIET";

        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int FlagOn = 30;
        public const int MinMonth = 1;
        public const int MaxMonth = 12;
        public const int MinYear = 0;
        public const int MaxYear = 2999;
        public const int MaxTextLength = 255;

        public const string MonthInvalid = "Tháng phải nằm trong khoảng từ 1 đến 12.";
        public const string YearInvalid = "Năm phải nằm trong khoảng từ 0 đến 2999.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string PeriodNotFound = "Bảng lương không còn tồn tại.";
        public const string NoShiftsWarning = "Chưa có ca làm việc nào.";
        public const string SavedSuccess = "Đã cập nhật";
        public const string UnsavedChangesConfirm = "Dữ liệu chấm công/lương có thay đổi chưa lưu. Bạn có muốn bỏ thay đổi không?";
        public const string ReloadConfirm = "Tải lại sẽ lấy lại lương cơ bản, cách tính lương, thưởng, phạt, tạm ứng hiện tại của nhân viên và tính lại; thay đổi chưa lưu sẽ mất. Bạn có muốn tiếp tục không?";

        public static string FormatPeriodName(int month, int year)
        {
            return string.Format("Tháng {0}/{1}", month, year);
        }

        public static string PeriodDuplicate(int month, int year)
        {
            return string.Format("Bảng lương tháng {0}/{1} đã tồn tại", month, year);
        }

        public static string DeleteConfirm(string periodName)
        {
            return string.Format("Bạn có muốn xóa bảng lương '{0}' cùng toàn bộ chấm công và tổng hợp lương của bảng này không?", periodName ?? "");
        }

        public static string DeleteBlockedByVouchers(string periodName, IList<string> voucherNumbers)
        {
            string list = voucherNumbers == null || voucherNumbers.Count == 0
                ? ""
                : string.Join(", ", new List<string>(voucherNumbers).ToArray());
            return string.Format("Không thể xóa bảng lương '{0}' vì đã có phiếu chi lương: {1}.", periodName ?? "", list);
        }

        public static string Clip(string text, int maxLength = MaxTextLength)
        {
            string s = text ?? "";
            return maxLength > 0 && s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }
    }
}
