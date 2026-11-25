using System;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh và thông báo của màn "Danh mục hóa đơn hủy" (docs/specs/danh-muc-hoa-don-huy.md).</summary>
    public static class CancelledBillConstants
    {
        public const string MenuId = "7d169f76-9897-4111-ba6d-f3cbd4c4e3f7";
        public const string FunctionId = "f1a22c9e-774f-4dce-ad95-d1c392a9ba51";
        public const string FunctionName = "Danh mục hóa đơn hủy";
        public const int PaidFlagValue = 30;
        public const string DiscountNote = "Lưu ý: Bản hủy cũ có thể ghi Tiền giảm giá bằng tỉ lệ phần trăm giảm giá.";
    }

    /// <summary>Một bản ghi trong bảng TDONHANGHUY (chỉ đọc; hiển thị đủ 22 cột lưới trên theo spec §1).</summary>
    public sealed class CancelledBillRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public DateTime? Ngay { get; set; }
        public string KhachHang { get; set; }
        public string NhanVien { get; set; }
        public string ThuNgan { get; set; }
        public DateTime? GioHuy { get; set; }
        public DateTime? NgayHuy { get; set; }
        public decimal? DoiTra { get; set; }
        public int? DaThanhToan { get; set; }
        public DateTime? GioThanhToan { get; set; }
        public decimal? TraLai { get; set; }
        public decimal? TienHang { get; set; }
        public decimal? TiLeThue { get; set; }
        public decimal? TienThue { get; set; }
        public decimal? TiLeGiamGia { get; set; }
        public decimal? TienGiamGia { get; set; }
        public decimal? PhiVanChuyen { get; set; }
        public string ThanhToanBoi { get; set; }
        public string LyDoHuy { get; set; }
        public decimal? TienGio { get; set; }
        public decimal? PhiDichVu { get; set; }
        public string OriginalOrderId { get; set; }
        public int? Status { get; set; }

        public bool IsPaid
        {
            get { return DaThanhToan == CancelledBillConstants.PaidFlagValue; }
        }
    }

    /// <summary>Một dòng chi tiết trong bảng TDONHANGHUYCHITIET (7 cột lưới dưới theo spec §1).</summary>
    public sealed class CancelledBillLine
    {
        public string Id { get; set; }
        public string CancelledBillId { get; set; }
        public string Note { get; set; }
        public string MaHang { get; set; }
        public string TenHang { get; set; }
        public string Dvt { get; set; }
        public decimal? SoLuong { get; set; }
        public decimal? DonGia { get; set; }
        public decimal? ThanhTien { get; set; }
        public DateTime? TimeCreated { get; set; }
    }

    /// <summary>Luật lọc trong bộ nhớ cho ô Lọc lưới chi tiết (spec §2.3, Q2/Q13).</summary>
    public static class CancelledBillFilter
    {
        private static readonly CompareInfo ViCompare = CultureInfo.GetCultureInfo("vi-VN").CompareInfo;

        /// <summary>
        /// Khớp chứa chuỗi trên TENHANG hoặc MAHANG, không phân biệt hoa thường (kể cả chữ Việt).
        /// Chuỗi rỗng hoặc chỉ khoảng trắng khớp mọi dòng.
        /// </summary>
        public static bool MatchesLine(CancelledBillLine line, string query)
        {
            if (line == null)
                return false;
            string q = (query ?? "").Trim();
            if (q.Length == 0)
                return true;
            return ContainsIgnoreCase(line.TenHang, q) || ContainsIgnoreCase(line.MaHang, q);
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            if (string.IsNullOrEmpty(source))
                return false;
            return ViCompare.IndexOf(source, value, CompareOptions.IgnoreCase) >= 0;
        }
    }
}
