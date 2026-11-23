using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật thuần của Khách đặt hàng (docs/specs/khach-dat-hang.md): kiểm tra khi lưu §2.1,
    /// tính tiền §3.3, kế hoạch đoàn §3.1. Không đụng DB, không đụng giao diện.
    /// </summary>
    public static class BookingRules
    {
        /// <summary>Nhãn bàn/phòng trên dialog (§5.7): đọc cấu hình, mặc định "Phòng" như app POS.</summary>
        public static string RoomLabel
        {
            get { return SystemConfig.GetText("TenPhongBan", "Phòng"); }
        }

        /// <summary>App có phải loại HOTEL không (UngDung = HOTEL thì hiện Từ/Đến ngày thay cho Giờ vào/ra).</summary>
        public static bool IsHotelApp
        {
            get { return string.Equals(SystemConfig.GetText("UngDung", "1"), "HOTEL", StringComparison.OrdinalIgnoreCase); }
        }

        // ------------------------------------------------------------------ Kiểm tra §2.1

        public sealed class BookingValidationInput
        {
            public List<string> RoomIds { get; set; }
            public string Number { get; set; }
            public string TenKhach { get; set; }
            public DateTime? TuGio { get; set; }
            public DateTime? DenGio { get; set; }
            public DateTime? GioDat { get; set; }
        }

        public sealed class BookingValidationResult
        {
            public bool IsValid { get; set; }
            public string Error { get; set; }
        }

        /// <summary>
        /// Kiểm tra khi lưu, đúng thứ tự §2.1 (+ bắt buộc theo SCOLUMN).
        /// Trả lỗi đầu tiên gặp; null Error = hợp lệ. Không kiểm trùng số phiếu (repository kiểm).
        /// </summary>
        public static BookingValidationResult ValidateBeforeSave(BookingValidationInput input)
        {
            if (input == null)
                return new BookingValidationResult { IsValid = false, Error = BookingText.SelectRoomFirst };
            // #1: chưa chọn bàn nào.
            if (input.RoomIds == null || input.RoomIds.Count == 0)
                return new BookingValidationResult { IsValid = false, Error = BookingText.SelectRoomFirst };
            if (string.IsNullOrWhiteSpace(input.Number))
                return new BookingValidationResult { IsValid = false, Error = BookingText.Required("Số phiếu") };
            if (string.IsNullOrWhiteSpace(input.TenKhach))
                return new BookingValidationResult { IsValid = false, Error = BookingText.Required("Người đặt") };
            // Q2 (mặc định tạm): ở POS, Giờ vào + Giờ ra thực tế bắt buộc (TUNGAY/DENNGAY suy ra từ đó).
            if (input.TuGio == null)
                return new BookingValidationResult { IsValid = false, Error = BookingText.Required("Giờ vào") };
            if (input.DenGio == null)
                return new BookingValidationResult { IsValid = false, Error = BookingText.Required("Giờ ra") };
            if (input.GioDat == null)
                return new BookingValidationResult { IsValid = false, Error = BookingText.Required("Đặt lúc") };
            // #5 theo quyết định Chiến 21/11 (§5.4): so CẢ ngày+giờ, câu đã sửa.
            if (input.DenGio.Value < input.TuGio.Value)
                return new BookingValidationResult { IsValid = false, Error = BookingText.DateOrder };
            return new BookingValidationResult { IsValid = true };
        }

        /// <summary>BeforeSave #3/#4: TUNGAY/DENNGAY = ngày của TUGIO/DENGIO lúc 00:00.</summary>
        public static void DeriveDates(DateTime? tuGio, DateTime? denGio, out DateTime tuNgay, out DateTime denNgay)
        {
            tuNgay = tuGio == null ? DateTime.Today : tuGio.Value.Date;
            denNgay = denGio == null ? DateTime.Today : denGio.Value.Date;
        }

        // ------------------------------------------------------------------ Tính tiền §3.3

        public sealed class BookingTotals
        {
            public decimal TienHang { get; set; }
            public decimal TileGiamGia { get; set; }
            public decimal TienGiamGia { get; set; }
            public decimal TienThue { get; set; }
            public decimal TongCong { get; set; }
        }

        /// <summary>Thành tiền một dòng chi tiết (mặc định tạm Q7).</summary>
        public static decimal CalcLineAmount(decimal quantity, decimal price, decimal discountPct)
        {
            return quantity * price * (1m - discountPct / 100m);
        }

        /// <summary>
        /// Tính tiền cả phiếu (§3.3). Giảm theo % thì TIENGIAMGIA làm tròn theo luật LamTron
        /// (quyết định Chiến 21/11, Q1 — ghi đè mặc định tạm "bội 100 như app gốc");
        /// giảm theo tiền thì suy ngược TILEGIAMGIA. roundStep &lt;= 0 = không làm tròn.
        /// </summary>
        public static BookingTotals CalcTotals(IEnumerable<BookingLine> lines, decimal tileGiamGia,
            decimal tienGiamGia, bool giamTheoTien, decimal tileThue, decimal phiVanChuyen, decimal roundStep)
        {
            decimal tienHang = 0m;
            if (lines != null)
            {
                foreach (BookingLine l in lines)
                    tienHang += l.Amount;
            }
            decimal tile = tileGiamGia;
            decimal tienGiam = tienGiamGia;
            if (giamTheoTien)
            {
                tile = tienHang == 0m ? 0m : tienGiam * 100m / tienHang;
            }
            else
            {
                decimal raw = tienHang * tile / 100m;
                tienGiam = roundStep > 0m ? MoneyRounding.RoundToStep(raw, roundStep) : raw;
            }
            decimal tienThue = tileThue * (tienHang - tienGiam) / 100m;
            return new BookingTotals
            {
                TienHang = tienHang,
                TileGiamGia = tile,
                TienGiamGia = tienGiam,
                TienThue = tienThue,
                TongCong = tienHang - tienGiam + tienThue + phiVanChuyen
            };
        }

        /// <summary>
        /// Thêm một dòng hàng vào danh sách chi tiết: trùng mặt hàng + ĐVT thì cộng dồn số lượng
        /// (khi NhapMotMatHangNhieuLanTrongPhieu = 0); ngược lại thêm dòng mới. Trả true nếu đã gộp.
        /// </summary>
        public static bool TryMergeLine(List<BookingLine> lines, BookingLine added, bool mergeAllowed)
        {
            if (!mergeAllowed || lines == null || added == null)
                return false;
            foreach (BookingLine l in lines)
            {
                if (string.Equals(l.ProductId, added.ProductId, StringComparison.Ordinal)
                    && string.Equals(l.UnitId, added.UnitId, StringComparison.Ordinal))
                {
                    l.Quantity += added.Quantity;
                    l.Amount = CalcLineAmount(l.Quantity, l.Price, l.DiscountPct);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ Đoàn §3.1

        /// <summary>
        /// Lập kế hoạch thay đổi đoàn từ danh sách bàn mới chọn. Thuần, không đụng DB.
        /// - Dòng đang sửa giữ bàn cũ nếu còn được chọn, ngược lại lấy bàn đầu tiên (BeforeSave #2);
        ///   nếu bàn đầu đã có dòng khác giữ thì dòng kia bị xóa (một bàn chỉ một dòng trong đoàn).
        /// - Dòng của bàn bị bỏ được tái dùng cho bàn mới (giữ số phiếu cũ); hết thì tạo dòng mới.
        /// </summary>
        public static GroupChangePlan PlanGroupChanges(List<GroupRowRef> existing, string currentId, List<string> roomIds)
        {
            return Kazuko_FnB.Data.BookingGroupPlanner.Plan(existing, currentId, roomIds);
        }
    }
}
