using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng ca làm việc (DCALAMVIEC, spec docs/specs/nhan-su-danh-muc.md §3).</summary>
    public sealed class ShiftRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Từ giờ (TUGIO, TIMESTAMP gắn ngày 01/01/2000, chỉ giờ:phút có nghĩa).</summary>
        public DateTime? FromTime { get; set; }
        /// <summary>Đến giờ (DENGIO, TIMESTAMP gắn ngày 01/01/2000, chỉ giờ:phút có nghĩa; cho phép nhỏ hơn Từ giờ).</summary>
        public DateTime? ToTime { get; set; }
        /// <summary>Tỉ lệ lương (TILELUONG, %, 0..1000, mặc định 100; 0 vẫn lưu được).</summary>
        public decimal SalaryRate { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static ShiftRow NewDefault()
        {
            return new ShiftRow
            {
                Name = "",
                FromTime = null,
                ToTime = null,
                SalaryRate = ShiftText.DefaultSalaryRate,
                Note = "",
                Status = ShiftText.ActiveStatus
            };
        }
    }

    /// <summary>Một cặp (Bảng, Cột, Nhãn tiếng Việt) tham chiếu tới DCALAMVIEC.ID.</summary>
    public sealed class ShiftReferenceColumn
    {
        public ShiftReferenceColumn(string tableName, string columnName, string displayLabel)
        {
            TableName = tableName;
            ColumnName = columnName;
            DisplayLabel = displayLabel;
        }

        public string TableName { get; private set; }
        public string ColumnName { get; private set; }
        public string DisplayLabel { get; private set; }
    }

    /// <summary>Kết quả lưu ca làm việc.</summary>
    public sealed class ShiftSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasFromTimeError { get; set; }
        public bool HasToTimeError { get; set; }
        public bool HasSalaryRateError { get; set; }
        public bool HasNoteError { get; set; }

        public static ShiftSaveResult Fail(string message, bool hasNameError = false)
        {
            return new ShiftSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError
            };
        }
    }

    /// <summary>Kết quả xóa cứng ca làm việc (kèm số tham chiếu từng bảng).</summary>
    public sealed class ShiftDeleteResult
    {
        public ShiftDeleteResult()
        {
            Blocks = new List<ReferenceBlock>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }

        public static ShiftDeleteResult Fail(string message)
        {
            return new ShiftDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng của Danh mục ca làm việc (DCALAMVIEC).</summary>
    public static class ShiftText
    {
        public const string FunctionName = "Danh mục ca làm việc";
        public const string TableName = "DCALAMVIEC";
        public const string ConfigUseTimekeeperKey = "TinhLuongSuDungMayChamCong";

        public const int ActiveStatus = 30;
        public const int MaxTextLength = 255;
        public const decimal MinSalaryRate = 0m;
        public const decimal MaxSalaryRate = 1000m;
        public const decimal DefaultSalaryRate = 100m;
        public const string ZeroRateHint = "0 được hiểu là 100%";

        public const string NameEmpty = "Trường \"Tên ca làm việc\" không được trống!";
        public const string FromTimeEmpty = "Trường \"Từ giờ\" không được trống!";
        public const string ToTimeEmpty = "Trường \"Đến giờ\" không được trống!";
        public const string ZeroDuration = "Ca làm việc không có thời lượng (Từ giờ và Đến giờ không được trùng nhau)!";
        public const string SalaryRateInvalid = "Trường \"Tỉ lệ lương (%)\" phải nằm trong khoảng 0 đến 1000!";
        public const string NotFound = "Ca làm việc không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên ca làm việc\" đã tồn tại!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột DCALAMVIEC.NAME:
        /// không phân biệt hoa/thường và dấu tiếng Việt (kể cả đ/Đ), KHÔNG cắt khoảng trắng đầu/cuối.
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
