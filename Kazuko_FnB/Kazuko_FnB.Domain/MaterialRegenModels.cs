using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số và thông báo của chức năng Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md).</summary>
    public static class MaterialRegenConstants
    {
        /// <summary>Tên chức năng/menu và tên quyền (SFUNCTION).</summary>
        public const string FunctionName = "Xuất lại định lượng";

        /// <summary>Từ xác nhận bắt buộc gõ trước khi chạy (không phân biệt hoa thường).</summary>
        public const string ConfirmWord = "XACNHAN";

        public const int ActiveStatus = 30;
        public const int SaleOrderLoai = 0;
        public const int PaidFlagValue = 30;
        public const int MaterialExportFlag = 30;
        public const int MaxNoteLength = 255;

        public const string NoViewPermission = "Bạn không có quyền xem chức năng xuất lại định lượng.";
        public const string NoPermission = "Bạn không có quyền xuất lại định lượng.";
        public const string DateRequired = "Vui lòng chọn đầy đủ Từ ngày và Đến ngày.";
        public const string DateRangeInvalid = "Từ ngày không được sau Đến ngày.";
        public const string ConfirmWordInvalid = "Vui lòng gõ đúng từ XACNHAN để xác nhận.";
        public const string ConfirmWarning =
            "Thao tác này thay đổi số liệu tồn kho và giá vốn, không hoàn tác được. Nên sao lưu dữ liệu trước.";
        public const string CostRecalcFailedWarning =
            "Đã xuất lại định lượng nhưng chưa tính lại được giá vốn, hãy chạy Tính lại giá vốn.";
        public const string CloseWhileRunningWarning =
            "Đang xuất lại định lượng, vui lòng chờ hoàn tất trước khi đóng.";
        public const string AllStoresLabel = "Tất cả cửa hàng";
    }

    /// <summary>Tham số đầu vào cho Xem trước và Thực hiện Xuất lại định lượng.</summary>
    public sealed class MaterialRegenRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        /// <summary>ID cửa hàng được chọn; rỗng/null = mọi cửa hàng trong phạm vi người dùng (admin = tất cả).</summary>
        public string StoreId { get; set; }
        /// <summary>Tên cửa hàng hiển thị (dùng cho lưu vết/tóm tắt; cắt tối đa 255 ký tự khi ghi).</summary>
        public string StoreName { get; set; }
        /// <summary>Chuỗi xác nhận người dùng gõ (chỉ bắt buộc khi Thực hiện, phải khớp XACNHAN không phân biệt hoa thường).</summary>
        public string ConfirmText { get; set; }
    }

    /// <summary>Kết quả Xem trước (chỉ đọc, không ghi gì vào CSDL).</summary>
    public sealed class MaterialRegenPreview
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int EligibleBillCount { get; set; }
        public int ExistingExportLineCount { get; set; }
        public int NewExportLineCount { get; set; }

        public static MaterialRegenPreview Fail(string message)
        {
            return new MaterialRegenPreview { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Kết quả Thực hiện Xuất lại định lượng.</summary>
    public sealed class MaterialRegenResult
    {
        public MaterialRegenResult()
        {
            NegativeStockSampleNames = new List<string>();
        }

        public bool Ok { get; set; }
        public string Message { get; set; }
        /// <summary>Cảnh báo khi bước tính lại giá vốn sau commit bị lỗi (không làm mất kết quả đã ghi).</summary>
        public string CostWarning { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int ProcessedBillCount { get; set; }
        public int DeletedLineCount { get; set; }
        public int InsertedLineCount { get; set; }
        /// <summary>Số mặt hàng (phân biệt theo DMATHANGID) có tồn kho âm sau khi xuất lại.</summary>
        public int NegativeStockProductCount { get; set; }
        public List<string> NegativeStockSampleNames { get; private set; }

        public static MaterialRegenResult Fail(string message)
        {
            return new MaterialRegenResult { Ok = false, Message = message };
        }
    }
}
