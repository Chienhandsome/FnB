using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Phạm vi hóa đơn bán hàng đã thanh toán dùng chung cho các màn báo cáo chỉ xem
    /// (Thống kê mặt hàng bán, Thống kê doanh thu, ...):
    /// TDONHANG.LOAI = 0, DATHANHTOAN = 30, STATUS = 30, NGAY thuộc [FromDate, ToDate + 1 ngày),
    /// và điều kiện cửa hàng / ràng buộc user (TNGUOIDUNGTHEOCUAHANG).
    /// </summary>
    public sealed class BillReportScope
    {
        public const int SaleOrderLoai = 0;
        public const int PaidFlagValue = 30;
        public const int ActiveStatus = 30;

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>ID cửa hàng người dùng chọn; rỗng/null = không chọn (xem theo phạm vi user/admin).</summary>
        public string StoreId { get; set; }
        public string UserId { get; set; }
        public bool IsAdmin { get; set; }
    }
}
