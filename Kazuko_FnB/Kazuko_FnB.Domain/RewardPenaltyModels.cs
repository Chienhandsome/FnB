using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng phiếu thưởng phạt nhân viên (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2).</summary>
    public sealed class RewardPenaltyRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string EmployeeId { get; set; }
        public decimal Bonus { get; set; }
        public decimal Penalty { get; set; }
        public string ReasonId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string EmployeeName { get; set; }
        public string ReasonName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static RewardPenaltyRow NewDefault()
        {
            return new RewardPenaltyRow
            {
                Name = "",
                Date = DateTime.Today,
                EmployeeId = null,
                Bonus = 0m,
                Penalty = 0m,
                ReasonId = null,
                Note = "",
                Status = RewardPenaltyText.ActiveStatus
            };
        }
    }

    /// <summary>Bộ lọc lưới Thưởng phạt (Từ ngày, Đến ngày, Nhân viên, ô tìm không dấu).</summary>
    public sealed class RewardPenaltyFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>Rỗng = tất cả nhân viên.</summary>
        public string EmployeeId { get; set; }
        public string SearchText { get; set; }
    }

    /// <summary>Giới hạn tháng/năm cho ngày phiếu thưởng phạt (khi gọi từ chi tiết bảng lương, spec §2).</summary>
    public sealed class RewardPenaltyMonthLimit
    {
        public RewardPenaltyMonthLimit(int month, int year)
        {
            Month = month;
            Year = year;
        }

        public int Month { get; private set; }
        public int Year { get; private set; }
    }

    /// <summary>Kết quả lưu phiếu thưởng phạt.</summary>
    public sealed class RewardPenaltySaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public bool HasNumberError { get; set; }
        public bool HasDateError { get; set; }
        public bool HasEmployeeError { get; set; }
        public bool HasAmountError { get; set; }
        public bool HasNoteError { get; set; }

        public static RewardPenaltySaveResult Fail(
            string message,
            bool hasNumberError = false,
            bool hasDateError = false,
            bool hasEmployeeError = false,
            bool hasAmountError = false,
            bool hasNoteError = false)
        {
            return new RewardPenaltySaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNumberError = hasNumberError,
                HasDateError = hasDateError,
                HasEmployeeError = hasEmployeeError,
                HasAmountError = hasAmountError,
                HasNoteError = hasNoteError
            };
        }
    }

    /// <summary>Kết quả xóa cứng phiếu thưởng phạt.</summary>
    public sealed class RewardPenaltyDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static RewardPenaltyDeleteResult Fail(string message)
        {
            return new RewardPenaltyDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và định dạng của màn Thưởng phạt (TTHUONGPHAT).</summary>
    public static class RewardPenaltyText
    {
        public const string FunctionName = "Thưởng phạt";
        public const string AltFunctionName = "Quản lý thưởng phạt";
        public const string FunctionDateChange = "Thay đổi ngày trong các phiếu";
        public const string TableName = "TTHUONGPHAT";
        public const string ReasonTableName = "DLYDOTHUONGPHAT";
        public const string DefaultNumberTemplate = "TP(yy)/(*****)";

        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int MaxTextLength = 255;
        public const decimal MaxAmount = 999999999.99m;

        public const string NumberRequired = "Mời bạn nhập: Số phiếu";
        public const string EmployeeRequired = "Mời bạn chọn nhân viên.";
        public const string EmployeeInvalid = "Nhân viên không tồn tại hoặc đã ngừng sử dụng.";
        public const string AmountRequired = "Nhập số tiền thưởng hoặc phạt";
        public const string AmountInvalid = "Số tiền thưởng/phạt phải không âm và tối đa 9 chữ số phần nguyên.";
        public const string NotFound = "Phiếu thưởng phạt không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NumberDuplicate(string number)
        {
            return string.Format("Số phiếu \"{0}\" đã tồn tại. Mời bạn nhập số phiếu khác.", number ?? "");
        }

        public static string MonthLimitBlocked(int month, int year)
        {
            return string.Format("Ngày phải nằm trong tháng {0} năm {1}", month, year);
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string FormatMoneyVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value == Math.Truncate(value)
                ? value.ToString("#,##0", vi)
                : value.ToString("#,##0.##", vi);
        }
    }
}
