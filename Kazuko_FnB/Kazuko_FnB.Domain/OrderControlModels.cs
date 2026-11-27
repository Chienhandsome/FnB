using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền, nhãn và thông báo chuẩn cho màn "Kiểm soát order"
    /// (docs/specs/kiem-soat-order.md). Màn chỉ đọc, không ghi DB, không ghi lưu vết.
    /// </summary>
    public static class OrderControlConstants
    {
        public const string FunctionId = "abc261a7-ac27-452e-b1e2-7398f5a285fa";
        public const string FunctionName = "Kiểm soát order";

        public const int SaleOrderLoai = 0;
        public const int ActiveStatus = 30;

        public const string EmptyDataMessage = "Không có order nào trong khoảng ngày";
        public const string InvalidDateRangeMessage = "Từ ngày không được sau Đến ngày.";
        public const string NoPermissionMessage = "Bạn không có quyền thực hiện chức năng này.";

        public const string IssueTypeInvalid = "Viết sai";
        public const string IssueTypeDuplicate = "Trùng";
        public const string IssueTypeMissing = "Thiếu";
    }

    /// <summary>Một hóa đơn đầu vào cho bộ phân tích số order giấy (đọc từ TDONHANG).</summary>
    public sealed class OrderControlBillRow
    {
        public string BillId { get; set; }
        public string BillNumber { get; set; }
        public DateTime BillDate { get; set; }
        public string OrderNumbers { get; set; }
    }

    /// <summary>Phân loại dòng bất thường trên lưới "Cần kiểm tra".</summary>
    public enum OrderControlIssueKind
    {
        Invalid = 0,
        Duplicate = 1,
        Missing = 2
    }

    /// <summary>Một dòng trên lưới "Đã dùng": mỗi số order hợp lệ xuất hiện đúng một lần, sắp tăng dần.</summary>
    public sealed class OrderControlUsedRow
    {
        public int OrderNumber { get; set; }
        public string FirstBillId { get; set; }
        public string FirstBillNumber { get; set; }
        public DateTime FirstBillDate { get; set; }
        public bool IsDuplicate { get; set; }
        public string BillDisplay { get; set; }
    }

    /// <summary>Một dòng trên lưới "Cần kiểm tra": lỗi viết sai, trùng hoặc thiếu (đơn lẻ / dải).</summary>
    public sealed class OrderControlIssueRow
    {
        public OrderControlIssueKind Kind { get; set; }
        public string TypeDisplay { get; set; }
        public string Message { get; set; }
        public string DateOrBillDisplay { get; set; }
        public string BillId { get; set; }
        public string BillNumber { get; set; }
        public DateTime? BillDate { get; set; }
        public int? MissingStart { get; set; }
        public int? MissingEnd { get; set; }
        public long MissingCount { get; set; }
    }

    /// <summary>Các chỉ số tóm tắt phía trên màn Kiểm soát order.</summary>
    public sealed class OrderControlSummary
    {
        public bool HasUsedOrders { get; set; }
        public int? MaxOrder { get; set; }
        public int? MinOrder { get; set; }
        public int UsedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int InvalidCount { get; set; }
        public long MissingCount { get; set; }
    }

    /// <summary>Kết quả phân tích hoàn chỉnh (thuần, không phụ thuộc DB/UI).</summary>
    public sealed class OrderControlAnalysisResult
    {
        public List<OrderControlUsedRow> UsedRows { get; set; }
        public List<OrderControlIssueRow> IssueRows { get; set; }
        public OrderControlSummary Summary { get; set; }

        public OrderControlAnalysisResult()
        {
            UsedRows = new List<OrderControlUsedRow>();
            IssueRows = new List<OrderControlIssueRow>();
            Summary = new OrderControlSummary();
        }

        public bool HasAnyData
        {
            get
            {
                return (UsedRows != null && UsedRows.Count > 0)
                    || (IssueRows != null && IssueRows.Count > 0);
            }
        }
    }
}
