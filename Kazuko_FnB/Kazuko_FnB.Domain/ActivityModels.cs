using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Phân loại thao tác trong TLUUVET.PHANLOAI — giữ ĐÚNG giá trị số của enum LoaiLuuVet app gốc (0–16)
    /// để dữ liệu cũ và mới đọc như nhau (docs/specs/luu-vet-hoat-dong.md §2.5).
    /// </summary>
    public enum ActivityKind
    {
        MoBan = 0,
        DongBan = 1,
        InBill = 2,
        InCheBien = 3,
        ThemMon = 4,
        XoaMon = 5,
        GiamMon = 6,
        ChuyenBan = 7,
        GopBan = 8,
        TangMon = 9,
        DatSoLuong = 10,
        DoiGiaBan = 11,
        DatGiamGiaMatHang = 12,
        HuyBill = 13,
        Khac = 14,
        ChuyenMatHang = 15,
        TraDo = 16
    }

    /// <summary>Trạng thái hóa đơn ở lưới trái màn Lưu vết hoạt động (spec §1, §2.1).</summary>
    public enum ActivityBillKind
    {
        /// <summary>Đã thanh toán (DATHANHTOAN = 30) — chữ mặc định.</summary>
        Completed = 0,
        /// <summary>Bill đã hủy (TDONHANGHUY) — chữ đỏ.</summary>
        Cancelled = 1,
        /// <summary>Bill đang sử dụng (DATHANHTOAN != 30) — chữ xanh dương.</summary>
        InUse = 2
    }

    /// <summary>Mức nhấn mạnh màu/đậm của một dòng lưu vết theo bảng §2.3 (UI ánh xạ sang ThemeTokens).</summary>
    public enum ActivityRowTone
    {
        Default = 0,
        Danger = 1,
        DangerBold = 2,
        Info = 3,
        SuccessBold = 4
    }

    /// <summary>Ánh xạ số PHANLOAI (0–16) sang tên hiển thị (§2.5, Q9) và tông màu (§2.3).</summary>
    public static class ActivityKindInfo
    {
        public static bool IsKnown(int phanLoai)
        {
            return phanLoai >= 0 && phanLoai <= 16;
        }

        /// <summary>Tên hiển thị theo cột "Ý nghĩa" bảng §2.5; số ngoài 0–16 coi như "Khác", không báo lỗi.</summary>
        public static string DisplayName(int phanLoai)
        {
            switch (phanLoai)
            {
                case (int)ActivityKind.MoBan: return "Mở bàn";
                case (int)ActivityKind.DongBan: return "Đóng bàn";
                case (int)ActivityKind.InBill: return "In bill";
                case (int)ActivityKind.InCheBien: return "In chế biến";
                case (int)ActivityKind.ThemMon: return "Thêm món";
                case (int)ActivityKind.XoaMon: return "Xóa món";
                case (int)ActivityKind.GiamMon: return "Giảm món";
                case (int)ActivityKind.ChuyenBan: return "Chuyển bàn";
                case (int)ActivityKind.GopBan: return "Gộp bàn";
                case (int)ActivityKind.TangMon: return "Tăng món";
                case (int)ActivityKind.DatSoLuong: return "Đặt số lượng";
                case (int)ActivityKind.DoiGiaBan: return "Đổi giá bán";
                case (int)ActivityKind.DatGiamGiaMatHang: return "Đặt giảm giá mặt hàng";
                case (int)ActivityKind.HuyBill: return "Hủy bill";
                case (int)ActivityKind.Khac: return "Khác";
                case (int)ActivityKind.ChuyenMatHang: return "Chuyển mặt hàng";
                case (int)ActivityKind.TraDo: return "Trả đồ";
                default: return "Khác";
            }
        }

        public static string DisplayName(ActivityKind kind)
        {
            return DisplayName((int)kind);
        }

        /// <summary>Tông màu/đậm theo bảng §2.3; số ngoài 0–16 trả về mặc định (không màu).</summary>
        public static ActivityRowTone ToneOf(int phanLoai)
        {
            switch (phanLoai)
            {
                case (int)ActivityKind.HuyBill:
                    return ActivityRowTone.DangerBold;
                case (int)ActivityKind.ChuyenBan:
                    return ActivityRowTone.Info;
                case (int)ActivityKind.GopBan:
                    return ActivityRowTone.SuccessBold;
                case (int)ActivityKind.XoaMon:
                case (int)ActivityKind.GiamMon:
                case (int)ActivityKind.DoiGiaBan:
                case (int)ActivityKind.DatGiamGiaMatHang:
                    return ActivityRowTone.Danger;
                default:
                    return ActivityRowTone.Default;
            }
        }
    }

    /// <summary>Hằng số định danh và cấu hình của chức năng Lưu vết hoạt động (docs/specs/luu-vet-hoat-dong.md).</summary>
    public static class ActivityConstants
    {
        public const string MenuId = "b022d2c4-d61c-479a-9ebc-9c3eedfc18df";
        public const string FunctionId = "863ecff5-3010-400c-930e-549e4f8d1f96";
        public const string FunctionName = "Lưu vết hoạt động";
        public const string ConfigKey = "KichHoatLuuVetHoatDong";
        public const int ConfigEnabledValue = 30;
        public const int MaxAccountLength = 36;
        public const int MaxFunctionLength = 36;
        public const string DeletePassword = "9876";

        public const string FuncSuDungDichVu = "Sử dụng dịch vụ";
        public const string FuncQuanLyBanHang = "Quản lý bán hàng";
        public const string FuncDieuChinhHoaDon = "Điều chỉnh hóa đơn";
        public const string FuncPosCamUng = "POS cảm ứng";

        public const string DisabledWarningLine1 = "Chức năng lưu vết hoạt động hiện không được kích hoạt";
        public const string DisabledWarningLine2 = "Bạn có thể kích hoạt trong menu 'Quản trị | Cấu hình toàn hệ thống'";
        public static readonly string DisabledWarningText = DisabledWarningLine1 + Environment.NewLine + DisabledWarningLine2;
    }

    /// <summary>Một dòng trên lưới trái "HÓA ĐƠN" (ghép TDONHANG LOAI=0 và TDONHANGHUY).</summary>
    public sealed class ActivityBillItem
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public string TableName { get; set; }
        public ActivityBillKind Kind { get; set; }
        public string StatusText { get; set; }
    }

    /// <summary>Một dòng lưu vết trong bảng TLUUVET.</summary>
    public sealed class ActivityLogEntry
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public DateTime Time { get; set; }
        /// <summary>SODONHANG (ID hóa đơn: TDONHANG.ID hoặc TDONHANGHUY.TDONHANGID).</summary>
        public string BillId { get; set; }
        /// <summary>Số phiếu hiển thị: COALESCE(TDONHANG.NAME, TDONHANGHUY.NAME).</summary>
        public string BillNumber { get; set; }
        public string Note { get; set; }
        public string Account { get; set; }
        public string Device { get; set; }
        public string TableName { get; set; }
        public string FunctionName { get; set; }
        /// <summary>Giá trị số nguyên lưu trong TLUUVET.PHANLOAI (có thể nằm ngoài 0–16 với dữ liệu lạ).</summary>
        public int KindValue { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string CreatedById { get; set; }

        public string KindDisplayName
        {
            get { return ActivityKindInfo.DisplayName(KindValue); }
        }

        public ActivityRowTone Tone
        {
            get { return ActivityKindInfo.ToneOf(KindValue); }
        }
    }

    /// <summary>Đầu vào để ghi một dòng lưu vết (dùng cho cả ghi đơn và ghi lô trong 1 transaction).</summary>
    public sealed class ActivityLogRequest
    {
        public string FunctionName { get; set; }
        public ActivityKind Kind { get; set; }
        public string BillId { get; set; }
        public string TableName { get; set; }
        public string Note { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        /// <summary>Tùy chọn: thời điểm thao tác (null = lấy giờ máy chủ DB khi ghi).</summary>
        public DateTime? Time { get; set; }
        /// <summary>Tùy chọn: tên đăng nhập ghi đè (null = lấy từ phiên hiện tại).</summary>
        public string Account { get; set; }
        /// <summary>Tùy chọn: USERCREATEDID ghi đè (null = lấy từ phiên hiện tại).</summary>
        public string UserId { get; set; }
        /// <summary>Tùy chọn: tên thiết bị ghi đè (null = Environment.MachineName).</summary>
        public string Device { get; set; }
    }

    /// <summary>Một tài khoản SUSER hiển thị trong bộ lọc "Tài khoản" của màn Lưu vết hoạt động.</summary>
    public sealed class ActivityAccountItem
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        /// <summary>Giá trị so với TLUUVET.TAIKHOAN (rỗng = Tất cả).</summary>
        public string FilterValue { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(FilterValue))
                return DisplayName ?? "";
            return FilterValue;
        }
    }
}
