using System;
using System.Globalization;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>Luật thuần của chức năng Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md): kiểm tra ngày, từ xác nhận, định dạng chuỗi và cắt độ dài cột.</summary>
    public static class MaterialRegenRules
    {
        /// <summary>Khoảng ngày mặc định khi mở màn: từ ngày đầu tháng đến ngày cuối tháng hiện tại.</summary>
        public static void DefaultDateRange(DateTime today, out DateTime fromDate, out DateTime toDate)
        {
            DateTime d = today.Date;
            fromDate = new DateTime(d.Year, d.Month, 1);
            toDate = new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
        }

        /// <summary>Kiểm tra khoảng ngày bắt buộc (T1): cả hai không được trống và Từ ngày không sau Đến ngày.</summary>
        public static string ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue || !toDate.HasValue)
                return MaterialRegenConstants.DateRequired;
            if (fromDate.Value.Date > toDate.Value.Date)
                return MaterialRegenConstants.DateRangeInvalid;
            return null;
        }

        /// <summary>Kiểm tra chuỗi xác nhận: phải khớp đúng "XACNHAN" (không phân biệt hoa thường, bỏ khoảng trắng hai đầu).</summary>
        public static bool IsConfirmWordValid(string text)
        {
            return string.Equals((text ?? "").Trim(), MaterialRegenConstants.ConfirmWord, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Cắt chuỗi cho vừa cột DB có giới hạn độ dài.</summary>
        public static string Clip(string text, int maxLength)
        {
            string s = text ?? "";
            if (maxLength <= 0 || s.Length <= maxLength)
                return s;
            return s.Substring(0, maxLength);
        }

        /// <summary>Nội dung ghi vào TLUUVET.NOTE (cắt tối đa 255 ký tự): khoảng ngày, số hóa đơn, số dòng xóa và sinh lại.</summary>
        public static string BuildActivityNote(
            DateTime fromDate,
            DateTime toDate,
            string storeName,
            int billCount,
            int deletedLines,
            int insertedLines,
            int negativeStockCount)
        {
            var sb = new StringBuilder();
            sb.Append("Xuất lại định lượng ");
            sb.Append(fromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            sb.Append(" - ");
            sb.Append(toDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(storeName))
            {
                sb.Append(" (");
                sb.Append(storeName.Trim());
                sb.Append(")");
            }
            sb.Append(": ");
            sb.Append(billCount).Append(" hóa đơn, xóa ").Append(deletedLines)
              .Append(" dòng, sinh lại ").Append(insertedLines).Append(" dòng");
            if (negativeStockCount > 0)
                sb.Append(", tồn âm ").Append(negativeStockCount).Append(" mặt hàng");
            sb.Append(".");
            return Clip(sb.ToString(), MaterialRegenConstants.MaxNoteLength);
        }

        /// <summary>Chuỗi hiển thị vùng kết quả sau khi Xem trước.</summary>
        public static string BuildPreviewText(MaterialRegenPreview preview, string storeLabel)
        {
            if (preview == null)
                return "";
            if (!preview.Ok)
                return preview.ErrorMessage ?? "";

            string scope = string.IsNullOrWhiteSpace(storeLabel) ? MaterialRegenConstants.AllStoresLabel : storeLabel.Trim();
            return string.Format(
                "Kết quả xem trước ({0:dd/MM/yyyy} – {1:dd/MM/yyyy}, {2}):\r\n" +
                "• Số hóa đơn đủ điều kiện: {3}\r\n" +
                "• Số dòng xuất vật tư sẽ xóa: {4}\r\n" +
                "• Số dòng xuất vật tư sẽ sinh lại: {5}",
                preview.FromDate,
                preview.ToDate,
                scope,
                preview.EligibleBillCount,
                preview.ExistingExportLineCount,
                preview.NewExportLineCount);
        }

        /// <summary>Chuỗi tóm tắt kết quả sau khi Thực hiện xong (gồm báo tồn âm và cảnh báo giá vốn nếu có).</summary>
        public static string BuildResultMessage(MaterialRegenResult result)
        {
            if (result == null)
                return "";
            if (!result.Ok)
                return result.Message ?? "";

            var sb = new StringBuilder();
            sb.AppendFormat(
                "Đã xuất lại định lượng ({0:dd/MM/yyyy} – {1:dd/MM/yyyy}): {2} hóa đơn, xóa {3} dòng, sinh lại {4} dòng.",
                result.FromDate,
                result.ToDate,
                result.ProcessedBillCount,
                result.DeletedLineCount,
                result.InsertedLineCount);

            if (result.NegativeStockProductCount > 0)
            {
                sb.Append("\r\nCảnh báo tồn âm: có ")
                  .Append(result.NegativeStockProductCount)
                  .Append(" mặt hàng có tồn kho âm sau khi xuất lại");
                if (result.NegativeStockSampleNames != null && result.NegativeStockSampleNames.Count > 0)
                {
                    sb.Append(" (").Append(string.Join(", ", result.NegativeStockSampleNames.ToArray()));
                    if (result.NegativeStockProductCount > result.NegativeStockSampleNames.Count)
                        sb.Append(", ...");
                    sb.Append(")");
                }
                sb.Append(".");
            }

            if (!string.IsNullOrEmpty(result.CostWarning))
                sb.Append("\r\n").Append(result.CostWarning);

            return sb.ToString();
        }
    }
}
