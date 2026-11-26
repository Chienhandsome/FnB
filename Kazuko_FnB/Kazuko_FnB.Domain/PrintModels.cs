using System;
using System.Collections.Generic;
using System.Data;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số dùng chung cho chức năng in và xuất báo cáo FastReport (docs/specs/in-xuat-excel/).</summary>
    public static class PrintConstants
    {
        /// <summary>SFORMID của màn "Thống kê mặt hàng bán" trong bảng STEMPLATE.</summary>
        public const string ItemSalesFormId = "67500401-1f1e-4c18-a0fc-f487c43f4b0c";

        public const int ActiveStatus = 30;

        public const string ConfigCompanyName = "CompanyName";
        public const string ConfigCompanyAddress = "CompanyAddress";
        public const string ConfigCompanyPhone = "CompanyPhone";
        public const string ConfigCompanyEmail = "CompanyEmail";

        public const string DefaultPreviewTitle = "IN PHIẾU";
        public const string SelectTemplateDialogTitle = "CHỌN MẪU IN";
        public const string ConfirmOpenExportedFileMessage = "Bạn có muốn mở file đã xuất không?";
        public const string NoTemplateFoundMessage = "Không tìm thấy mẫu in nào đang hoạt động cho màn hình này.";
        public const string NoCostPermissionPrintMessage = "Bạn không có quyền Xem giá nhập để in báo cáo này.";
    }

    /// <summary>Bốn tùy chọn thực hiện trong hộp "CHỌN MẪU IN" (không có Thiết kế mẫu in theo Q8).</summary>
    public enum PrintActionKind
    {
        Preview = 0,
        Print = 1,
        ExportExcel = 2,
        ExportPdf = 3
    }

    /// <summary>Một mẫu in đọc từ bảng STEMPLATE (chỉ đọc).</summary>
    public sealed class PrintTemplateRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FormId { get; set; }
        public byte[] TemplateBytes { get; set; }
        public string TemplateXml { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Thông tin công ty đọc từ bảng SCONFIG (DATATYPE = 1, cột TEXTVALUE) truyền làm tham số mẫu in.</summary>
    public sealed class CompanyPrintInfo
    {
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyPhone { get; set; }
        public string CompanyEmail { get; set; }

        public CompanyPrintInfo()
        {
            CompanyName = "";
            CompanyAddress = "";
            CompanyPhone = "";
            CompanyEmail = "";
        }
    }

    /// <summary>Kết quả người dùng chọn trong hộp "CHỌN MẪU IN".</summary>
    public sealed class PrintSelectionResult
    {
        public PrintTemplateRecord Template { get; set; }
        public PrintActionKind Action { get; set; }
        public string PrinterName { get; set; }
    }

    /// <summary>Bộ dữ liệu truyền vào FastReport (DataSet tên "Data" chứa Table0 và các tham số đúng kiểu).</summary>
    public sealed class PrintJobData : IDisposable
    {
        public DataSet DataSet { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string DefaultFileName { get; set; }
        public string PreviewTitle { get; set; }

        public PrintJobData()
        {
            Parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            PreviewTitle = PrintConstants.DefaultPreviewTitle;
        }

        public void Dispose()
        {
            if (DataSet != null)
            {
                DataSet.Dispose();
                DataSet = null;
            }
        }
    }
}
