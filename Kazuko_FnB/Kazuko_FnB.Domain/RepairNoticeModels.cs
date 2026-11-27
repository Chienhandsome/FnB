using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng phiếu thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2).</summary>
    public sealed class RepairNoticeRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string RoomId { get; set; }
        public string RoomTypeId { get; set; }
        public string EmployeeId { get; set; }
        public string Content { get; set; }
        public bool IsFixed { get; set; }
        public bool IsUsable { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi trực tiếp vào TSUACHUA).
        public string RoomName { get; set; }
        public string RoomTypeName { get; set; }
        public string EmployeeName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static RepairNoticeRow NewDefault()
        {
            return new RepairNoticeRow
            {
                Name = "",
                Date = DateTime.Today,
                RoomId = null,
                RoomTypeId = null,
                EmployeeId = null,
                Content = "",
                IsFixed = false,
                IsUsable = true,
                Note = "",
                Status = RepairNoticeText.ActiveStatus
            };
        }
    }

    /// <summary>Mục chọn Phòng/Bàn kèm Loại phòng mặc định (DBAN.DLOAIPHONGID).</summary>
    public sealed class RepairRoomLookup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RoomTypeId { get; set; }
    }

    /// <summary>Bộ lọc lưới Thông báo sửa chữa (Từ/Đến ngày để trống = tất cả, Chỉ chưa sửa xong, ô tìm không dấu).</summary>
    public sealed class RepairNoticeFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool UnfixedOnly { get; set; }
        public string SearchText { get; set; }
    }

    /// <summary>Kết quả lưu phiếu thông báo sửa chữa.</summary>
    public sealed class RepairNoticeSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public bool HasNumberError { get; set; }
        public bool HasRoomError { get; set; }
        public bool HasContentError { get; set; }
        public bool HasNoteError { get; set; }

        public static RepairNoticeSaveResult Fail(
            string message,
            bool hasNumberError = false,
            bool hasRoomError = false,
            bool hasContentError = false,
            bool hasNoteError = false)
        {
            return new RepairNoticeSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNumberError = hasNumberError,
                HasRoomError = hasRoomError,
                HasContentError = hasContentError,
                HasNoteError = hasNoteError
            };
        }
    }

    /// <summary>Kết quả xóa cứng phiếu thông báo sửa chữa.</summary>
    public sealed class RepairNoticeDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static RepairNoticeDeleteResult Fail(string message)
        {
            return new RepairNoticeDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng cho Thông báo sửa chữa (TSUACHUA).</summary>
    public static class RepairNoticeText
    {
        public const string FunctionName = "Thông báo sửa chữa";
        public const string FunctionDateChange = "Thay đổi ngày trong các phiếu";
        public const string TableName = "TSUACHUA";
        public const string DefaultNumberTemplate = "SC(yy)/(*****)";

        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int FlagOn = 30;
        public const int FlagOff = 0;
        public const int MaxTextLength = 255;

        public const string NumberRequired = "Mời bạn nhập: Số phiếu";
        public const string RoomRequired = "Mời bạn chọn Phòng/Bàn.";
        public const string RoomInvalid = "Phòng/Bàn không tồn tại hoặc đã ngừng sử dụng.";
        public const string ContentRequired = "Mời bạn nhập: Nội dung";
        public const string NotFound = "Phiếu thông báo sửa chữa không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NumberDuplicate(string number)
        {
            return string.Format("Số phiếu \"{0}\" đã tồn tại. Mời bạn nhập số phiếu khác.", number ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string BoolText(bool value)
        {
            return value ? "Có" : "Không";
        }

        /// <summary>So trùng theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, kể cả đ/Đ).</summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
