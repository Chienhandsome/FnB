using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền, nhãn và thông báo của báo cáo "Tổng hợp kết quả kinh doanh"
    /// (docs/specs/tong-hop-ket-qua-kinh-doanh.md).
    /// </summary>
    public static class BusinessResultConstants
    {
        public const string MenuId = "eb49f730-19d4-435e-b852-05b553600e88";
        public const string FunctionId = "1866efc8-397a-45ad-9ef4-fc8640e3d7c0";
        public const string FunctionName = "Tổng hợp kết quả kinh doanh";
        public const string ReportTitle = "BÁO CÁO KẾT QUẢ KINH DOANH";

        public const int BillLoai5 = 5;
        public const int BillLoai6 = 6;
        public const int ReasonLoaiFixedCost = 1;
        public const int ReasonLoaiOtherCost = 2;

        // Cấp dòng báo cáo (thứ tự hiển thị = LOAI tăng dần 0..9).
        public const int LoaiRevenue = 0;
        public const int LoaiRevenueDetail = 1;
        public const int LoaiCost = 2;
        public const int LoaiFixedCost = 3;
        public const int LoaiFixedCostDetail = 4;
        public const int LoaiVariableCost = 5;
        public const int LoaiVariableCostDetail = 6;
        public const int LoaiOtherCost = 7;
        public const int LoaiOtherCostDetail = 8;
        public const int LoaiProfitLoss = 9;

        // Nhãn dòng theo spec §1, §2.
        public const string LabelRevenue = "DOANH THU";
        public const string LabelDiscount = "Giảm giá";
        public const string LabelServiceFee = "Phí dịch vụ";
        public const string LabelTax = "Thuế";
        public const string LabelCost = "CHI PHÍ";
        public const string LabelFixedCost = "Định phí";
        public const string LabelVariableCost = "Biến phí";
        public const string LabelOtherCost = "Chi khác";
        public const string LabelProfitLoss = "LÃI/LỖ";
        public const string UngroupedName = "(Chưa phân nhóm)";

        // Biểu tượng tăng/giảm Unicode (C7).
        public const string TrendUp = "▲";
        public const string TrendDown = "▼";
        public const string RatioDash = "-";
        public const string RatioFull = "100%";

        public const string AllLabel = "Tất cả";
        public const string PrintUnsupportedMessage = "Chức năng in báo cáo chưa hỗ trợ trong phiên bản này.";
        public const string MissingDateMessage = "Vui lòng nhập đầy đủ Từ ngày và Đến ngày.";
    }

    /// <summary>Bộ lọc chạy báo cáo Tổng hợp kết quả kinh doanh.</summary>
    public sealed class BusinessResultFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>ID cửa hàng người dùng chọn; rỗng/null = không chọn (áp ràng buộc user khi không phải admin).</summary>
        public string StoreId { get; set; }
        public string UserId { get; set; }
        public bool IsAdmin { get; set; }

        public BillReportScope ToBillScope(DateTime periodFrom, DateTime periodTo)
        {
            return new BillReportScope
            {
                FromDate = periodFrom.Date,
                ToDate = periodTo.Date,
                StoreId = StoreId ?? "",
                UserId = UserId ?? "",
                IsAdmin = IsAdmin
            };
        }
    }

    /// <summary>Khoảng ngày của một kỳ báo cáo (tính trọn ngày cuối).</summary>
    public sealed class BusinessResultPeriodRange
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    /// <summary>Tổng hợp bill trong một kỳ (DOANH THU = TONGCONG, Giảm giá = TIENGIAMGIATONG, Phí = PHIDICHVU, Thuế = TIENTHUE).</summary>
    public sealed class BusinessResultBillTotals
    {
        public decimal Revenue { get; set; }
        public decimal Discount { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal Tax { get; set; }
    }

    /// <summary>Một mục nhóm mặt hàng hoặc lý do chi kèm số tiền kỳ này và kỳ trước.</summary>
    public sealed class BusinessResultNamedAmount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal CurrentAmount { get; set; }
        public decimal PreviousAmount { get; set; }
    }

    /// <summary>Dữ liệu thô đọc từ CSDL cho cả hai kỳ để <c>BusinessResultCalculator</c> dựng bảng kết quả.</summary>
    public sealed class BusinessResultRawData
    {
        public BusinessResultPeriodRange CurrentPeriod { get; set; }
        public BusinessResultPeriodRange PreviousPeriod { get; set; }
        public BusinessResultBillTotals CurrentBills { get; set; }
        public BusinessResultBillTotals PreviousBills { get; set; }
        public List<BusinessResultNamedAmount> RevenueGroups { get; set; }
        public List<BusinessResultNamedAmount> VariableCostGroups { get; set; }
        public List<BusinessResultNamedAmount> FixedCostReasons { get; set; }
        public List<BusinessResultNamedAmount> OtherCostReasons { get; set; }

        public BusinessResultRawData()
        {
            CurrentPeriod = new BusinessResultPeriodRange();
            PreviousPeriod = new BusinessResultPeriodRange();
            CurrentBills = new BusinessResultBillTotals();
            PreviousBills = new BusinessResultBillTotals();
            RevenueGroups = new List<BusinessResultNamedAmount>();
            VariableCostGroups = new List<BusinessResultNamedAmount>();
            FixedCostReasons = new List<BusinessResultNamedAmount>();
            OtherCostReasons = new List<BusinessResultNamedAmount>();
        }
    }

    /// <summary>Một dòng kết quả đã tính đủ giá trị, 3 cột tỉ lệ (§2.1), tăng/giảm và biểu tượng.</summary>
    public sealed class BusinessResultRow
    {
        public int Loai { get; set; }
        public string ItemNumber { get; set; }
        public string Content { get; set; }
        /// <summary>Cột %/DT (bên trái cột Giá trị).</summary>
        public string PercentOfRevenue { get; set; }
        /// <summary>Giá trị kỳ này (tháng này).</summary>
        public decimal CurrentValue { get; set; }
        /// <summary>Cột % (tỉ lệ chính).</summary>
        public string PercentMain { get; set; }
        /// <summary>Cột tỉ lệ thứ ba (%/CP hoặc % nhóm).</summary>
        public string PercentThird { get; set; }
        /// <summary>Biểu tượng xu hướng: ▲ (&gt; 0), ▼ (&lt; 0), rỗng (= 0).</summary>
        public string TrendSymbol { get; set; }
        /// <summary>Tăng giảm so với tháng trước = CurrentValue − PreviousValue.</summary>
        public decimal ChangeAmount { get; set; }
        /// <summary>KQKD tháng trước.</summary>
        public decimal PreviousValue { get; set; }
        /// <summary>Dòng cấp 0, 2, 9 hiển thị chữ đậm.</summary>
        public bool IsBold { get; set; }
        /// <summary>Dòng nhóm chi phí (3 Định phí, 5 Biến phí, 7 Chi khác).</summary>
        public bool IsGroupHeader { get; set; }
    }
}
