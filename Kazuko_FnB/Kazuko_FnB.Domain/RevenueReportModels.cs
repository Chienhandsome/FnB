using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh, quyền, nhãn và thông báo của màn "Thống kê doanh thu" (docs/specs/thong-ke-doanh-thu.md).</summary>
    public static class RevenueReportConstants
    {
        public const string MenuId = "c5164013-298f-4565-8594-819bc7ce992a";
        public const string FunctionId = "b2a025da-9c16-4578-a864-bf32c4eb2d09";
        public const string FunctionName = "Thống kê doanh thu";

        /// <summary>Mã loại mặt hàng nguyên liệu (DMATHANG.DLOAIMATHANGID = '2', bị loại khỏi lưới chi tiết).</summary>
        public const string MaterialProductTypeId = "2";

        public const string PrintUnsupportedMessage = "Chức năng in báo cáo chưa hỗ trợ trong phiên bản này.";

        public const string NetActualReceiptCaption = "TỔNG THỰC THU (TIỀN MẶT + THẺ + CHUYỂN KHOẢN + THU - CHI)";

        /// <summary>Tooltip P2 trên dòng TỔNG THỰC THU: liệt kê khoản được cộng/trừ và khoản không cộng.</summary>
        public const string NetActualReceiptTooltip =
            "Các khoản được cộng/trừ: TIỀN MẶT + TIỀN THẺ + CHUYỂN KHOẢN + THU KHÁC − CHI KHÁC\n" +
            "Các khoản KHÔNG cộng vào thực thu: VOUCHER, TRỪ TÍCH LŨY, THẺ TRẢ TRƯỚC, CÔNG NỢ";
    }

    /// <summary>
    /// Một hóa đơn bán hàng đã thanh toán trên lưới chính màn "Thống kê doanh thu".
    /// Mang đủ các cột hiển thị trên lưới và toàn bộ cột tiền mà khối tổng cần (NULL được quy về 0).
    /// </summary>
    public sealed class RevenueBillRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime? Ngay { get; set; }
        public string TableName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? PaidTime { get; set; }
        public string CustomerName { get; set; }
        public string PaidBy { get; set; }
        public string Note { get; set; }
        public string CustomerAddress { get; set; }
        public string CustomerCode { get; set; }

        // Các cột tiền trên TDONHANG (NULL = 0m):
        public decimal TienHangChuaGiam { get; set; }
        public decimal TienGio { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal GiamGiaMatHang { get; set; }
        public decimal TienGiamGiaGio { get; set; }
        public decimal PhiDichVu { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }

        // Các cột hình thức thanh toán (không hiện thành cột lưới, chỉ dùng cho khối tổng):
        public decimal TienMat { get; set; }
        public decimal Voucher { get; set; }
        public decimal The { get; set; }
        public decimal ChuyenKhoan { get; set; }
        public decimal TruTichLuy { get; set; }
        public decimal TheTraTruoc { get; set; }
        public decimal CongNo { get; set; }
    }

    /// <summary>Một dòng chi tiết hóa đơn (TDONHANGCHITIET) ở lưới phải của bill đang chọn.</summary>
    public sealed class RevenueBillLine
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        /// <summary>SLXUATCHUAQUYDOI.</summary>
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        /// <summary>TILEGIAMGIA (%).</summary>
        public decimal? DiscountRate { get; set; }
        public decimal? Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Tổng THU KHÁC và CHI KHÁC đọc từ TTHUCHI theo khoảng ngày và cửa hàng (không lọc STATUS/LOAI/LATAMUNG/CHUYENKHOAN).</summary>
    public sealed class RevenueCashFlowTotals
    {
        public decimal OtherIncome { get; set; }
        public decimal OtherExpense { get; set; }
    }

    /// <summary>Khối tổng cộng 3 nhóm (Doanh thu, Hình thức thanh toán, Thu chi) thuần decimal theo spec §1 và §2.3.</summary>
    public sealed class RevenueSummary
    {
        // Nhóm 1: Doanh thu
        /// <summary>TIỀN HÀNG = Σ TIENHANGCHUAGIAM.</summary>
        public decimal GoodsAmount { get; set; }
        /// <summary>TIỀN GIỜ = Σ TIENGIO (ẩn trên UI theo Q7).</summary>
        public decimal TimeCharge { get; set; }
        /// <summary>GIẢM GIÁ TIỀN HÀNG = Σ TIENGIAMGIA + Σ GIAMGIAMATHANG.</summary>
        public decimal GoodsDiscount { get; set; }
        /// <summary>GIẢM GIÁ TIỀN GIỜ = Σ TIENGIAMGIAGIO (ẩn trên UI theo Q7, vẫn cộng vào TỔNG GIẢM GIÁ).</summary>
        public decimal TimeDiscount { get; set; }
        /// <summary>TỔNG GIẢM GIÁ = GIẢM GIÁ TIỀN HÀNG + GIẢM GIÁ TIỀN GIỜ.</summary>
        public decimal TotalDiscount { get; set; }
        /// <summary>PHÍ DỊCH VỤ = Σ PHIDICHVU.</summary>
        public decimal ServiceFee { get; set; }
        /// <summary>THUẾ = Σ TIENTHUE.</summary>
        public decimal Tax { get; set; }
        /// <summary>TỔNG DOANH THU = Σ TONGCONG.</summary>
        public decimal TotalRevenue { get; set; }

        // Nhóm 2: Hình thức thanh toán
        /// <summary>TIỀN MẶT = Σ TIENMAT.</summary>
        public decimal Cash { get; set; }
        /// <summary>TIỀN THẺ = Σ THE.</summary>
        public decimal Card { get; set; }
        /// <summary>CHUYỂN KHOẢN = Σ CHUYENKHOAN.</summary>
        public decimal BankTransfer { get; set; }
        /// <summary>VOUCHER = Σ VOUCHER.</summary>
        public decimal Voucher { get; set; }
        /// <summary>TRỪ TÍCH LŨY = Σ TRUTICHLUY.</summary>
        public decimal LoyaltyDeduction { get; set; }
        /// <summary>THẺ TRẢ TRƯỚC = Σ THETRATRUOC.</summary>
        public decimal PrepaidCard { get; set; }
        /// <summary>CÔNG NỢ = Σ CONGNO.</summary>
        public decimal Debt { get; set; }

        // Nhóm 3: Thu chi
        /// <summary>THU KHÁC = Σ COALESCE(TTHUCHI.THU, 0).</summary>
        public decimal OtherIncome { get; set; }
        /// <summary>CHI KHÁC = Σ COALESCE(TTHUCHI.CHI, 0).</summary>
        public decimal OtherExpense { get; set; }
        /// <summary>TỔNG THỰC THU = TIỀN MẶT + TIỀN THẺ + CHUYỂN KHOẢN + THU KHÁC − CHI KHÁC.</summary>
        public decimal NetActualReceipt { get; set; }
    }
}
