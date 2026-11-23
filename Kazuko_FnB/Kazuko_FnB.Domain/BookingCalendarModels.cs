using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Dữ liệu màn "Theo dõi đặt phòng" (lưới lịch Gantt): loại phòng, phòng, thanh đặt phòng.
    /// Nguồn: <c>docs/specs/theo-doi-dat-phong.md</c> §3.
    /// </summary>

    /// <summary>Một loại phòng (<c>DLOAIPHONG</c>) dùng làm hàng nhóm trên lưới.</summary>
    internal sealed class CalendarRoomType
    {
        public string Id;
        public string Name;
        /// <summary>Màu chữ (<c>DLOAIPHONG.MAUCHU</c>, ARGB; 0 = không đặt).</summary>
        public int ForeColorArgb;
        /// <summary>Màu nền (<c>DLOAIPHONG.MAUNEN</c>, ARGB; 0 = không đặt).</summary>
        public int BackColorArgb;
        public int SortOrder;
    }

    /// <summary>Một phòng/bàn (<c>DBAN</c>) có loại phòng, hiển thị thành một hàng trên lưới.</summary>
    internal sealed class CalendarRoom
    {
        public string Id;
        public string Name;
        public string TypeId;
        public string TypeName;
    }

    /// <summary>
    /// Một phiếu đặt phòng (<c>TDATHANG</c>) vẽ thành một thanh trên lưới.
    /// TUNGAY/DENNGAY là ngày (phần giờ nếu có được chuẩn hóa về 00:00 khi vẽ).
    /// </summary>
    internal sealed class CalendarBookingBar
    {
        public string Id;
        public string Guid;
        public string RoomId;
        public string RoomName;
        public string TenKhach;
        public DateTime TuNgay;
        public DateTime DenNgay;
        /// <summary>Màu thanh (<c>TDATHANG.MAUSAC</c>, ARGB; 0 = màu cam mặc định của app gốc).</summary>
        public int ColorArgb;
        /// <summary>Tên mục đích (<c>DMUCDICHDAT.NAME</c>), dùng cho tooltip.</summary>
        public string PurposeName;
        /// <summary>Yêu cầu khác (<c>TDATHANG.NOTE</c>), dùng cho tooltip.</summary>
        public string Note;
    }

    /// <summary>Kết quả tải lưới lịch: các loại, các phòng, các thanh, và tổng số phòng (đếm tất cả, kể cả phòng không có loại).</summary>
    internal sealed class CalendarData
    {
        public List<CalendarRoomType> Types = new List<CalendarRoomType>();
        public List<CalendarRoom> Rooms = new List<CalendarRoom>();
        public List<CalendarBookingBar> Bars = new List<CalendarBookingBar>();
        /// <summary>Tổng số phòng STATUS = 30 (kể cả phòng không có DLOAIPHONGID — dùng cho 4 hàng tổng).</summary>
        public int TotalRooms;
    }

    /// <summary>
    /// Dữ liệu mồi cho dialog đặt phòng khi mở từ một vùng ô đã chọn trên lưới
    /// (spec §1.4): các phòng trong vùng + ngày vào/ra = ngày min/max của vùng (00:00).
    /// </summary>
    internal sealed class BookingCalendarPreset
    {
        public List<string> RoomIds = new List<string>();
        public List<string> RoomNames = new List<string>();
        public DateTime TuGio;
        public DateTime DenGio;
    }

    /// <summary>Kết quả áp dụng một lần kéo thả trên lưới (theo quy ước result của repo: Ok + ErrorMessage).</summary>
    internal sealed class BookingCalendarMoveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static BookingCalendarMoveResult OkResult()
        {
            return new BookingCalendarMoveResult { Ok = true };
        }

        public static BookingCalendarMoveResult Fail(string error)
        {
            return new BookingCalendarMoveResult { Ok = false, ErrorMessage = error };
        }
    }

    /// <summary>
    /// Chuỗi hiển thị riêng của màn lịch. App gốc dùng chữ "đặt phòng" trong các hộp
    /// của màn này (không phải "đặt hàng" như màn Khách đặt hàng) — giữ y hệt (§5.6).
    /// </summary>
    internal static class CalendarText
    {
        public const string SelectAreaFirst = "Mời bạn chọn vùng có chứa phòng và ngày trước";
        public const string OutOfWindow =
            "Đặt phòng đang chọn có khoảng thời gian vượt khỏi vùng hiển thị không thể thay đổi bằng cách kéo thả.\n" +
            "Bạn có muốn mở cửa sổ để thay đổi khoảng ngày không?";
        public const string DeleteConfirm = "Bạn có muốn thực hiện xóa đặt phòng đang chọn không?";
        public const string DeleteBlocked = "Bạn không thể xóa đặt phòng này vì đã đặt tiền hoặc đã tạo hóa đơn";
        public const string Unsupported = "Chức năng này chưa hỗ trợ";
    }
}
