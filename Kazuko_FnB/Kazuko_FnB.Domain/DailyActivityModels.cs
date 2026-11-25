using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền, nhãn và thông báo của báo cáo "Chi tiết hoạt động ngày"
    /// (docs/specs/chi-tiet-hoat-dong-ngay.md).
    /// </summary>
    public static class DailyActivityConstants
    {
        public const string MenuId = "b2555738-5bc9-4f84-9e6a-80188bbfac99";
        public const string FunctionId = "5d90c04e-ca76-4941-8f48-575a07e73e19";
        public const string FunctionName = "Chi tiết hoạt động ngày";
        public const string ReportTitle = "CHI TIẾT HOẠT ĐỘNG TRONG NGÀY";

        public const int PurchaseOrderLoai = 1;

        public const string PrintUnsupportedMessage = "Chức năng in báo cáo chưa hỗ trợ trong phiên bản này.";
        public const string CashierNote =
            "Ghi chú: Thu ngân lọc hóa đơn bán theo người thanh toán; thu-chi và phiếu nhập theo người tạo phiếu.";
        public const string AllLabel = "Tất cả";

        public const string SectionSalesTitle = "1. BÁN HÀNG";
        public const string SectionItemsTitle = "2. MẶT HÀNG BÁN";
        public const string SectionPurchasesTitle = "3. NHẬP HÀNG";
        public const string SectionCashFlowTitle = "4. THU-CHI";
        public const string SectionOverallTitle = "TỔNG HỢP";

        public const string TotalCaption = "TỔNG CỘNG";
        public const string ItemGrossCaption = "TIỀN HÀNG CHƯA GIẢM GIÁ";
        public const string ItemDiscountCaption = "GIẢM GIÁ TRÊN MẶT HÀNG";
        public const string BillDiscountCaption = "GIẢM GIÁ TỔNG BILL";
        public const string ItemNetTotalCaption = "TỔNG CỘNG";
    }

    /// <summary>Bộ lọc chạy báo cáo Chi tiết hoạt động ngày.</summary>
    public sealed class DailyActivityFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>ID thu ngân (SUSER.ID); rỗng/null = tất cả.</summary>
        public string CashierId { get; set; }
        /// <summary>ID cửa hàng người dùng chọn; rỗng/null = không chọn.</summary>
        public string StoreId { get; set; }
        /// <summary>ID người dùng đang đăng nhập (dùng ràng buộc cửa hàng khi không phải admin).</summary>
        public string UserId { get; set; }
        public bool IsAdmin { get; set; }

        public BillReportScope ToBillScope()
        {
            return new BillReportScope
            {
                FromDate = FromDate.Date,
                ToDate = ToDate.Date,
                StoreId = StoreId ?? "",
                UserId = UserId ?? "",
                IsAdmin = IsAdmin
            };
        }
    }

    /// <summary>Thông tin công ty hiển thị đầu báo cáo (đọc từ SCONFIG).</summary>
    public sealed class DailyCompanyInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    /// <summary>Một mục chọn Thu ngân (SUSER) trên thanh lọc.</summary>
    public sealed class CashierRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
                return Name;
            return UserName ?? "";
        }
    }

    /// <summary>Khối 1 — Một hóa đơn bán hàng đã thanh toán theo Mẫu đầy đủ (12 cột hiển thị).</summary>
    public sealed class DailySaleBillRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime? Ngay { get; set; }
        public DateTime? PaidTime { get; set; }
        public string CustomerName { get; set; }

        public decimal TongCong { get; set; }
        public decimal TienMat { get; set; }
        public decimal ChuyenKhoan { get; set; }
        public decimal The { get; set; }
        public decimal Voucher { get; set; }
        public decimal TruTichLuy { get; set; }
        public decimal TheTraTruoc { get; set; }
        public decimal CongNo { get; set; }
        /// <summary>Đặt trước trên bill (không hiển thị ở Mẫu đầy đủ và không tham gia khối tổng hợp §2.7).</summary>
        public decimal DatTruoc { get; set; }
    }

    /// <summary>Khối 2 — Một dòng mặt hàng bán đã gộp theo §2.2 (8 cột hiển thị).</summary>
    public sealed class DailySoldItemRow
    {
        public string ProductCode { get; set; }
        public string ItemName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Khối 3 — Một phiếu nhập kho (TDONHANG.LOAI = 1) theo §2.4 và Q12.</summary>
    public sealed class DailyPurchaseRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime? Ngay { get; set; }
        public string SupplierName { get; set; }
        public decimal TongCong { get; set; }
        public decimal TienThanhToan { get; set; }
        /// <summary>Cột CHUYENKHOAN trên TDONHANG: khác 0 là trả chuyển khoản, còn lại (0/NULL) là tiền mặt (Q12).</summary>
        public decimal ChuyenKhoan { get; set; }
        /// <summary>Cột LOAITHANHTOAN trên TDONHANG (chỉ giữ để đối chiếu công thức gốc trong selftest).</summary>
        public decimal LoaiThanhToan { get; set; }
    }

    /// <summary>Khối 4 — Một phiếu thu-chi (TTHUCHI) đã tách tiền mặt / chuyển khoản theo §2.3.</summary>
    public sealed class DailyCashFlowRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime? Ngay { get; set; }
        public string Description { get; set; }
        public string ObjectName { get; set; }
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal TransferIn { get; set; }
        public decimal TransferOut { get; set; }
    }

    /// <summary>Toàn bộ dữ liệu thô tải về trong một lượt chạy báo cáo.</summary>
    public sealed class DailyActivityReportData
    {
        public DailyCompanyInfo Company { get; set; }
        public List<DailySaleBillRow> SaleBills { get; set; }
        public List<DailySoldItemRow> SoldItems { get; set; }
        /// <summary>GiamGiaTongBill = Σ TIENGIAMGIA của bill bán cùng phạm vi (truy vấn riêng).</summary>
        public decimal BillTotalDiscount { get; set; }
        public List<DailyPurchaseRow> Purchases { get; set; }
        public List<DailyCashFlowRow> CashFlows { get; set; }

        public DailyActivityReportData()
        {
            Company = new DailyCompanyInfo();
            SaleBills = new List<DailySaleBillRow>();
            SoldItems = new List<DailySoldItemRow>();
            Purchases = new List<DailyPurchaseRow>();
            CashFlows = new List<DailyCashFlowRow>();
        }
    }

    /// <summary>Dòng TỔNG CỘNG khối 1 (BÁN HÀNG).</summary>
    public sealed class DailySaleBillTotals
    {
        public decimal TongCong { get; set; }
        public decimal TienMat { get; set; }
        public decimal ChuyenKhoan { get; set; }
        public decimal The { get; set; }
        public decimal Voucher { get; set; }
        public decimal TruTichLuy { get; set; }
        public decimal TheTraTruoc { get; set; }
        public decimal CongNo { get; set; }
    }

    /// <summary>4 dòng tổng khối 2 (MẶT HÀNG BÁN) theo §2.5.</summary>
    public sealed class DailySoldItemTotals
    {
        public decimal TotalQuantity { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal ItemDiscount { get; set; }
        public decimal BillDiscount { get; set; }
        public decimal NetTotal { get; set; }
    }

    /// <summary>Tổng khối 3 (NHẬP HÀNG) và phần tách tiền thanh toán theo quỹ (Q12).</summary>
    public sealed class DailyPurchaseTotals
    {
        public decimal TongCong { get; set; }
        public decimal TienThanhToan { get; set; }
        public decimal RemainingDebt { get; set; }
        public decimal PaidCash { get; set; }
        public decimal PaidTransfer { get; set; }
    }

    /// <summary>Dòng TỔNG CỘNG khối 4 (THU-CHI).</summary>
    public sealed class DailyCashFlowTotals
    {
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal TransferIn { get; set; }
        public decimal TransferOut { get; set; }
    }

    /// <summary>Khối tổng hợp 8 dòng ở cuối báo cáo theo đúng thứ tự §2.7.</summary>
    public sealed class DailyActivityOverallSummary
    {
        public decimal Cash { get; set; }
        public decimal BankTransfer { get; set; }
        public decimal Card { get; set; }
        public decimal Voucher { get; set; }
        public decimal PrepaidCard { get; set; }
        public decimal LoyaltyDeduction { get; set; }
        public decimal CustomerDebt { get; set; }
        public decimal SupplierDebt { get; set; }
    }

    /// <summary>Kết quả tính tổng toàn báo cáo (thuần decimal).</summary>
    public sealed class DailyActivitySummaryResult
    {
        public DailySaleBillTotals SaleTotals { get; set; }
        public DailySoldItemTotals ItemTotals { get; set; }
        public DailyPurchaseTotals PurchaseTotals { get; set; }
        public DailyCashFlowTotals CashFlowTotals { get; set; }
        public DailyActivityOverallSummary Overall { get; set; }

        public DailyActivitySummaryResult()
        {
            SaleTotals = new DailySaleBillTotals();
            ItemTotals = new DailySoldItemTotals();
            PurchaseTotals = new DailyPurchaseTotals();
            CashFlowTotals = new DailyCashFlowTotals();
            Overall = new DailyActivityOverallSummary();
        }
    }
}
