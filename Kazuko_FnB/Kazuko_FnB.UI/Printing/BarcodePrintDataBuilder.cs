using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Xml;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>Khai báo một cột trong Table0 của Dictionary mẫu tem mã vạch.</summary>
    internal sealed class BarcodeTemplateColumnSpec
    {
        public string Name { get; set; }
        public string DataTypeName { get; set; }
        public Type ClrType { get; set; }
    }

    /// <summary>
    /// Dựng DataSet ("Data" / "Table0") và tham số công ty cho các mẫu in mã vạch (N tem trên một hàng).
    /// Suy số tem một hàng N từ các cột NAME0, NAME1,... trong Dictionary của mẫu; không suy được thì báo lỗi nêu tên mẫu.
    /// Một mặt hàng số lượng q chiếm q tem liên tiếp; hàng cuối thiếu để trống ("" cho chữ, 0 cho số).
    /// Quy tắc N3: khi canViewCost = false luôn điền 0 cho GIANHAP, GIAVON, HOAHONG.
    /// </summary>
    internal static class BarcodePrintDataBuilder
    {
        private static readonly string[] KnownBaseFields =
        {
            "CODE",
            "NAME",
            "NOTE",
            "DNHOMMATHANG_NAME",
            "DDONVITINH_NAME",
            "GIABAN",
            "GIABAN2",
            "GIABAN3",
            "GIABAN4",
            "GIANHAP",
            "GIAVON",
            "HOAHONG",
            "TONTOITHIEU",
            "TONTOIDA",
            "BAOHANH",
            "MASANCO",
            "DHANGSANXUAT_NAME",
            "DDONVITINHCHANID_NAME",
            "QUYDOI",
            "GIABANCHAN",
            "DDOITACKYGUI_NAME",
            "MACDINHGIAMGIA",
            "MACDINHGIAMTIEN"
        };

        /// <summary>
        /// Đọc Dictionary/Table0 của mẫu tem và suy số tem một hàng N từ NAME0..NAME&lt;N-1&gt;.
        /// Nếu mẫu thiếu, XML hỏng hoặc không suy được N liên tục từ 0 sẽ trả false kèm câu lỗi nêu tên mẫu.
        /// </summary>
        public static bool TryInferLayout(
            PrintTemplateRecord template,
            out int labelsPerRow,
            out List<BarcodeTemplateColumnSpec> columns,
            out string errorMessage)
        {
            labelsPerRow = 0;
            columns = new List<BarcodeTemplateColumnSpec>();
            errorMessage = null;

            string templateName = FastReportEngine.DisplayTemplateName(template);
            string xml = ResolveTemplateXml(template);
            if (string.IsNullOrWhiteSpace(xml))
            {
                errorMessage = "Mẫu in '" + templateName + "' bị thiếu hoặc không có nội dung.";
                return false;
            }

            XmlDocument doc;
            try
            {
                doc = new XmlDocument();
                doc.LoadXml(xml);
            }
            catch (XmlException ex)
            {
                errorMessage = FastReportEngine.FormatError(templateName, "đọc cấu trúc mẫu mã vạch", ex);
                return false;
            }

            XmlNode tableNode = FindTable0Node(doc);
            if (tableNode == null)
            {
                errorMessage = "Mẫu in '" + templateName + "' thiếu nguồn dữ liệu Table0 trong Dictionary nên không xác định được số tem mỗi hàng.";
                return false;
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nameIndices = new HashSet<int>();
            int minIdx = int.MaxValue;
            int maxIdx = -1;

            foreach (XmlNode child in tableNode.ChildNodes)
            {
                if (!string.Equals(child.Name, "Column", StringComparison.OrdinalIgnoreCase))
                    continue;
                string colName = ReadAttrIgnoreCase(child, "Name").Trim();
                if (colName.Length == 0 || !seenNames.Add(colName))
                    continue;

                string dtName = ReadAttrIgnoreCase(child, "DataType").Trim();
                columns.Add(new BarcodeTemplateColumnSpec
                {
                    Name = colName,
                    DataTypeName = dtName,
                    ClrType = ResolveClrType(dtName)
                });

                int idx;
                if (TryParseNameSlotIndex(colName, out idx))
                {
                    nameIndices.Add(idx);
                    if (idx < minIdx) minIdx = idx;
                    if (idx > maxIdx) maxIdx = idx;
                }
            }

            if (nameIndices.Count == 0 || minIdx != 0 || maxIdx < 0 || nameIndices.Count != maxIdx + 1)
            {
                errorMessage = "Mẫu in '" + templateName + "' không xác định được số tem trên một hàng (thiếu chuỗi cột NAME0, NAME1,... hợp lệ trong Dictionary).";
                return false;
            }

            labelsPerRow = maxIdx + 1;
            return true;
        }

        public static bool TryBuild(
            PrintTemplateRecord template,
            IList<BarcodePrintEntry> entries,
            CompanyPrintInfo companyInfo,
            bool canViewCost,
            out PrintJobData job,
            out string errorMessage)
        {
            job = null;
            int labelsPerRow;
            List<BarcodeTemplateColumnSpec> columns;
            if (!TryInferLayout(template, out labelsPerRow, out columns, out errorMessage))
                return false;

            var flatLabels = new List<BarcodeProductRecord>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BarcodePrintEntry entry = entries[i];
                    if (entry == null || entry.Product == null || entry.Quantity <= 0)
                        continue;
                    for (int q = 0; q < entry.Quantity; q++)
                        flatLabels.Add(entry.Product);
                }
            }

            var table = new DataTable("Table0");
            for (int i = 0; i < columns.Count; i++)
                table.Columns.Add(columns[i].Name, columns[i].ClrType);

            Dictionary<string, KeyValuePair<string, int>> slotMap = BuildColumnSlotMap(labelsPerRow);

            int rowCount = flatLabels.Count == 0 ? 0 : (flatLabels.Count + labelsPerRow - 1) / labelsPerRow;
            for (int r = 0; r < rowCount; r++)
            {
                DataRow dr = table.NewRow();
                int rowBase = r * labelsPerRow;
                for (int c = 0; c < columns.Count; c++)
                {
                    BarcodeTemplateColumnSpec col = columns[c];
                    KeyValuePair<string, int> mapping;
                    if (slotMap.TryGetValue(col.Name, out mapping))
                    {
                        int labelIndex = rowBase + mapping.Value;
                        if (labelIndex < flatLabels.Count)
                        {
                            object raw = GetFieldValue(flatLabels[labelIndex], mapping.Key, canViewCost);
                            dr[col.Name] = CoerceToColumnType(raw, col.ClrType);
                        }
                        else
                        {
                            dr[col.Name] = DefaultEmptyValue(col.ClrType);
                        }
                    }
                    else
                    {
                        dr[col.Name] = DefaultEmptyValue(col.ClrType);
                    }
                }
                table.Rows.Add(dr);
            }

            var ds = new DataSet("Data");
            ds.Tables.Add(table);

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = PrintConstants.BarcodeDefaultFileName,
                PreviewTitle = PrintConstants.BarcodePreviewTitle
            };
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";
            return true;
        }

        private static Dictionary<string, KeyValuePair<string, int>> BuildColumnSlotMap(int labelsPerRow)
        {
            var map = new Dictionary<string, KeyValuePair<string, int>>(StringComparer.OrdinalIgnoreCase);
            for (int f = 0; f < KnownBaseFields.Length; f++)
            {
                string field = KnownBaseFields[f];
                for (int k = 0; k < labelsPerRow; k++)
                {
                    string colName = field + k.ToString(CultureInfo.InvariantCulture);
                    if (!map.ContainsKey(colName))
                        map[colName] = new KeyValuePair<string, int>(field, k);
                }
            }
            return map;
        }

        private static object GetFieldValue(BarcodeProductRecord p, string field, bool canViewCost)
        {
            switch (field)
            {
                case "CODE": return p.Code ?? "";
                case "NAME": return p.Name ?? "";
                case "NOTE": return p.Note ?? "";
                case "DNHOMMATHANG_NAME": return p.GroupName ?? "";
                case "DDONVITINH_NAME": return p.UnitName ?? "";
                case "GIABAN": return p.SellPrice;
                case "GIABAN2": return p.SellPrice2;
                case "GIABAN3": return p.SellPrice3;
                case "GIABAN4": return p.SellPrice4;
                case "GIANHAP": return canViewCost ? p.BuyPrice : 0m;
                case "GIAVON": return canViewCost ? p.CostPrice : 0m;
                case "HOAHONG": return canViewCost ? p.Commission : 0m;
                case "TONTOITHIEU": return p.MinStock;
                case "TONTOIDA": return p.MaxStock;
                case "BAOHANH": return p.Warranty ?? "";
                case "MASANCO": return p.Barcode ?? "";
                case "DHANGSANXUAT_NAME": return p.ManufacturerName ?? "";
                case "DDONVITINHCHANID_NAME": return p.BigUnitName ?? "";
                case "QUYDOI": return p.QuyDoi;
                case "GIABANCHAN": return p.BigSellPrice;
                case "DDOITACKYGUI_NAME": return p.ConsignmentName ?? "";
                case "MACDINHGIAMGIA": return p.DefaultDiscountRate;
                case "MACDINHGIAMTIEN": return p.DefaultDiscountAmount;
                default: return "";
            }
        }

        private static object CoerceToColumnType(object value, Type targetType)
        {
            if (value == null || value is DBNull)
                return DefaultEmptyValue(targetType);
            if (targetType == typeof(string))
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (targetType == typeof(decimal))
                return value is decimal ? value : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(int))
                return value is int ? value : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return value is double ? value : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return value is bool ? value : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            return value;
        }

        private static object DefaultEmptyValue(Type targetType)
        {
            if (targetType == typeof(string)) return "";
            if (targetType == typeof(decimal)) return 0m;
            if (targetType == typeof(int)) return 0;
            if (targetType == typeof(double)) return 0d;
            if (targetType == typeof(bool)) return false;
            return DBNull.Value;
        }

        private static Type ResolveClrType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return typeof(string);
            switch (typeName.Trim())
            {
                case "System.Decimal": return typeof(decimal);
                case "System.Int32": return typeof(int);
                case "System.Int64": return typeof(long);
                case "System.Double": return typeof(double);
                case "System.Single": return typeof(float);
                case "System.Boolean": return typeof(bool);
                case "System.DateTime": return typeof(DateTime);
                default: return typeof(string);
            }
        }

        private static bool TryParseNameSlotIndex(string columnName, out int slotIndex)
        {
            slotIndex = -1;
            if (string.IsNullOrEmpty(columnName)
                || columnName.Length <= 4
                || !columnName.StartsWith("NAME", StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = columnName.Substring(4);
            for (int i = 0; i < suffix.Length; i++)
            {
                if (suffix[i] < '0' || suffix[i] > '9')
                    return false;
            }
            return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out slotIndex);
        }

        private static XmlNode FindTable0Node(XmlDocument doc)
        {
            XmlNodeList nodes = doc.GetElementsByTagName("TableDataSource");
            for (int i = 0; i < nodes.Count; i++)
            {
                string name = ReadAttrIgnoreCase(nodes[i], "Name").Trim();
                if (string.Equals(name, "Table0", StringComparison.OrdinalIgnoreCase))
                    return nodes[i];
            }
            return null;
        }

        private static string ResolveTemplateXml(PrintTemplateRecord template)
        {
            if (template == null)
                return "";
            string xml = template.TemplateXml;
            if (string.IsNullOrWhiteSpace(xml) && template.TemplateBytes != null && template.TemplateBytes.Length > 0)
            {
                int offset = template.TemplateBytes.Length >= 3
                    && template.TemplateBytes[0] == 0xEF
                    && template.TemplateBytes[1] == 0xBB
                    && template.TemplateBytes[2] == 0xBF ? 3 : 0;
                xml = Encoding.UTF8.GetString(template.TemplateBytes, offset, template.TemplateBytes.Length - offset);
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
