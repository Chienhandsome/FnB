using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Xml;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số dùng chung cho chức năng in và xuất báo cáo FastReport (docs/specs/in-xuat-excel/).</summary>
    public static class PrintConstants
    {
        /// <summary>SFORMID của màn "Thống kê mặt hàng bán" trong bảng STEMPLATE.</summary>
        public const string ItemSalesFormId = "67500401-1f1e-4c18-a0fc-f487c43f4b0c";

        /// <summary>SFORMID của màn "Thống kê doanh thu" trong bảng STEMPLATE.</summary>
        public const string RevenueReportFormId = "d913c8d9-4395-476c-9612-8ca7f35b6268";

        public const int ActiveStatus = 30;
        /// <summary>Giá trị INTVALUE bật cấu hình trong bảng SCONFIG (cùng quy ước SystemConfig.On = 30).</summary>
        public const int ConfigEnabledValue = 30;

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
        /// <summary>Trạng thái bật/tắt (INTVALUE = 30) của các SCONFIG.ID được mẫu dùng qua VisibleByConfigID.</summary>
        public Dictionary<string, bool> ConfigVisibilityById { get; set; }

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
        /// <summary>
        /// Trạng thái bật/tắt cấu hình theo SCONFIG.ID cho lần in hiện tại (nếu khác null sẽ ưu tiên hơn
        /// <see cref="PrintTemplateRecord.ConfigVisibilityById"/>).
        /// </summary>
        public Dictionary<string, bool> ConfigVisibilityById { get; set; }

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

    /// <summary>Quy tắc ẩn/hiện một TableColumn trong mẫu .frx theo thuộc tính VisibleByConfigID.</summary>
    public sealed class PrintColumnVisibilityRule
    {
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public int ColumnIndex { get; set; }
        public string ConfigId { get; set; }
    }

    /// <summary>
    /// Đọc các thuộc tính VisibleByConfigID trên thẻ TableColumn trong XML mẫu .frx (thuần XML, không SQL).
    /// Dùng chung giữa tầng Data (lấy danh sách ID cần truy vấn SCONFIG) và FastReportEngine (ẩn cột trước Prepare).
    /// </summary>
    public static class PrintTemplateVisibilityParser
    {
        public static List<PrintColumnVisibilityRule> ParseColumnRules(string templateXml, byte[] templateBytes)
        {
            var rules = new List<PrintColumnVisibilityRule>();
            string xml = ResolveXml(templateXml, templateBytes);
            if (string.IsNullOrWhiteSpace(xml)
                || xml.IndexOf("VisibleByConfigID", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return rules;
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNodeList colNodes = doc.GetElementsByTagName("TableColumn");
                var indexByParent = new Dictionary<XmlNode, int>();
                for (int i = 0; i < colNodes.Count; i++)
                {
                    XmlNode colNode = colNodes[i];
                    XmlNode parent = colNode.ParentNode;
                    int colIndex = 0;
                    if (parent != null)
                    {
                        indexByParent.TryGetValue(parent, out colIndex);
                        indexByParent[parent] = colIndex + 1;
                    }

                    string cfgId = ReadAttrIgnoreCase(colNode, "VisibleByConfigID").Trim();
                    if (cfgId.Length == 0)
                        continue;

                    rules.Add(new PrintColumnVisibilityRule
                    {
                        TableName = ReadAttrIgnoreCase(parent, "Name"),
                        ColumnName = ReadAttrIgnoreCase(colNode, "Name"),
                        ColumnIndex = colIndex,
                        ConfigId = cfgId
                    });
                }
            }
            catch (XmlException)
            {
                // Mẫu XML hỏng: để FastReportEngine.TryPrepare bắt và báo lỗi kèm tên mẫu.
                rules.Clear();
            }
            return rules;
        }

        public static List<string> ExtractDistinctConfigIds(IEnumerable<PrintTemplateRecord> templates)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (templates == null)
                return ids;

            foreach (PrintTemplateRecord tpl in templates)
            {
                if (tpl == null)
                    continue;
                List<PrintColumnVisibilityRule> rules = ParseColumnRules(tpl.TemplateXml, tpl.TemplateBytes);
                for (int i = 0; i < rules.Count; i++)
                {
                    string id = rules[i].ConfigId;
                    if (!string.IsNullOrEmpty(id) && seen.Add(id))
                        ids.Add(id);
                }
            }
            return ids;
        }

        private static string ResolveXml(string templateXml, byte[] templateBytes)
        {
            string xml = templateXml;
            if (string.IsNullOrWhiteSpace(xml) && templateBytes != null && templateBytes.Length > 0)
            {
                int offset = templateBytes.Length >= 3
                    && templateBytes[0] == 0xEF && templateBytes[1] == 0xBB && templateBytes[2] == 0xBF ? 3 : 0;
                xml = Encoding.UTF8.GetString(templateBytes, offset, templateBytes.Length - offset);
            }
            if (!string.IsNullOrEmpty(xml) && xml[0] == '\uFEFF')
                xml = xml.Substring(1);
            return xml ?? "";
        }

        private static string ReadAttrIgnoreCase(XmlNode node, string name)
        {
            if (node == null || node.Attributes == null)
                return "";
            XmlAttribute exact = node.Attributes[name];
            if (exact != null)
                return exact.Value ?? "";
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                if (string.Equals(node.Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return node.Attributes[i].Value ?? "";
            }
            return "";
        }
    }
}
