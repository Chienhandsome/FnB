using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using FastReport;
using FastReport.Data;
using FastReport.Export.OoXML;
using FastReport.Export.Pdf;
using FastReport.Table;
using FastReport.Utils;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Engine in FastReport 1.2.47 dùng chung cho các màn báo cáo/phiếu (không chứa SQL, không mở hộp thoại).
    /// Thực hiện: nạp mẫu từ chuỗi/byte, đổi đường dẫn ReferencedAssemblies (No1Lib*), đăng ký Data/Table0,
    /// truyền tham số đúng kiểu, Prepare, xuất xlsx/pdf và in ra máy in.
    /// </summary>
    internal static class FastReportEngine
    {
        /// <summary>
        /// Đổi các tham chiếu trần bắt đầu bằng No1Lib (hoặc DbMapping) sang đường dẫn đầy đủ trong BaseDirectory
        /// để trình biên dịch script của FastReport 1.2.47 không báo CS0006. Không xóa tham chiếu hay script.
        /// </summary>
        public static void ResolveReferencedAssemblies(Report report)
        {
            if (report == null)
                return;
            string[] current = report.ReferencedAssemblies;
            if (current == null || current.Length == 0)
                return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var updated = new string[current.Length];
            for (int i = 0; i < current.Length; i++)
                updated[i] = ResolveAssemblyPath(current[i], baseDir);
            report.ReferencedAssemblies = updated;
        }

        private static string ResolveAssemblyPath(string entry, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return entry;
            string trimmed = entry.Trim();
            if (Path.IsPathRooted(trimmed))
                return trimmed;

            string fileName = Path.GetFileName(trimmed);
            if (fileName.StartsWith("No1Lib", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("DbMapping", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(baseDir, fileName);
            }
            return trimmed;
        }

        /// <summary>
        /// Nạp mẫu từ bộ nhớ (XML chuỗi hoặc mảng byte đọc từ CSDL), đổ dữ liệu/tham số và dựng trang (Prepare).
        /// Mọi lỗi (mẫu thiếu, XML hỏng, script lỗi) đều được bắt và trả về một câu tiếng Việt nêu tên mẫu.
        /// </summary>
        public static bool TryPrepare(
            PrintTemplateRecord template,
            PrintJobData job,
            out Report preparedReport,
            out string errorMessage)
        {
            preparedReport = null;
            errorMessage = null;

            string templateName = DisplayTemplateName(template);
            if (template == null
                || ((template.TemplateBytes == null || template.TemplateBytes.Length == 0)
                    && string.IsNullOrWhiteSpace(template.TemplateXml)))
            {
                errorMessage = "Mẫu in '" + templateName + "' bị thiếu hoặc không có nội dung.";
                return false;
            }

            Report report = null;
            try
            {
                Config.ReportSettings.ShowProgress = false;
                report = new Report();
                LoadTemplateIntoReport(report, template);

                if (!TryApplyColumnVisibility(report, template, job, templateName, out errorMessage))
                {
                    report.Dispose();
                    return false;
                }

                if (job != null && job.DataSet != null)
                {
                    report.RegisterData(job.DataSet, "Data");
                    foreach (DataTable table in job.DataSet.Tables)
                    {
                        DataSourceBase ds = report.GetDataSource(table.TableName);
                        if (ds != null)
                            ds.Enabled = true;
                    }
                }

                if (job != null && job.Parameters != null)
                {
                    RemoveConflictingTotals(report, job.Parameters);
                    foreach (KeyValuePair<string, object> kv in job.Parameters)
                        report.SetParameterValue(kv.Key, kv.Value);
                }

                ResolveReferencedAssemblies(report);

                bool prepared = report.Prepare();
                if (!prepared || report.PreparedPages == null || report.PreparedPages.Count == 0)
                {
                    report.Dispose();
                    errorMessage = "Mẫu in '" + templateName + "' không dựng được trang nào.";
                    return false;
                }

                preparedReport = report;
                return true;
            }
            catch (Exception ex)
            {
                if (report != null)
                {
                    try { report.Dispose(); } catch { }
                }
                errorMessage = FormatError(templateName, "dựng mẫu in", ex);
                return false;
            }
        }

        private static void RemoveConflictingTotals(Report report, IDictionary<string, object> parameters)
        {
            if (report == null || report.Dictionary == null || report.Dictionary.Totals == null || parameters == null)
                return;

            // FastReport 1.2.47: Total trùng tên tham số trong Dictionary gây NullReferenceException ở CalcExpression
            // hoặc ưu tiên in giá trị Total thay vì tham số. Gỡ Total trùng trên đối tượng Report trong bộ nhớ
            // trước SetParameterValue/Prepare; giữ nguyên các Total không trùng (vd. Total, TotalTien của 54mm/80mm).
            foreach (KeyValuePair<string, object> kv in parameters)
            {
                if (string.IsNullOrEmpty(kv.Key))
                    continue;
                Total conflicting = report.Dictionary.Totals.FindByName(kv.Key);
                if (conflicting != null)
                    report.Dictionary.Totals.Remove(conflicting);
            }
        }

        private static bool TryApplyColumnVisibility(
            Report report,
            PrintTemplateRecord template,
            PrintJobData job,
            string templateName,
            out string errorMessage)
        {
            errorMessage = null;
            List<PrintColumnVisibilityRule> rules = PrintTemplateVisibilityParser.ParseColumnRules(
                template.TemplateXml, template.TemplateBytes);
            if (rules.Count == 0)
                return true;

            IDictionary<string, bool> configFlags = (job != null && job.ConfigVisibilityById != null)
                ? job.ConfigVisibilityById
                : template.ConfigVisibilityById;
            if (configFlags == null)
            {
                errorMessage = "Không đọc được cấu hình hiển thị cột cho mẫu in '" + templateName + "'.";
                return false;
            }

            foreach (Base obj in report.AllObjects)
            {
                var table = obj as TableObject;
                if (table == null)
                    continue;
                for (int i = 0; i < rules.Count; i++)
                {
                    PrintColumnVisibilityRule rule = rules[i];
                    if (!string.IsNullOrEmpty(rule.TableName)
                        && !string.Equals(table.Name, rule.TableName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TableColumn col = FindTableColumn(table, rule);
                    if (col == null)
                        continue;

                    bool isEnabled;
                    if (TryGetConfigFlag(configFlags, rule.ConfigId, out isEnabled) && !isEnabled)
                        col.Visible = false;
                }
            }
            return true;
        }

        private static bool TryGetConfigFlag(IDictionary<string, bool> configFlags, string configId, out bool isEnabled)
        {
            if (configFlags.TryGetValue(configId, out isEnabled))
                return true;
            foreach (KeyValuePair<string, bool> kv in configFlags)
            {
                if (string.Equals(kv.Key, configId, StringComparison.OrdinalIgnoreCase))
                {
                    isEnabled = kv.Value;
                    return true;
                }
            }
            isEnabled = true;
            return false;
        }

        private static TableColumn FindTableColumn(TableObject table, PrintColumnVisibilityRule rule)
        {
            if (table == null || table.Columns == null || rule == null)
                return null;
            if (!string.IsNullOrEmpty(rule.ColumnName))
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    TableColumn col = table.Columns[c];
                    if (col != null && string.Equals(col.Name, rule.ColumnName, StringComparison.OrdinalIgnoreCase))
                        return col;
                }
            }
            if (rule.ColumnIndex >= 0 && rule.ColumnIndex < table.Columns.Count)
                return table.Columns[rule.ColumnIndex];
            return null;
        }

        private static void LoadTemplateIntoReport(Report report, PrintTemplateRecord template)
        {
            if (template.TemplateBytes != null && template.TemplateBytes.Length > 0)
            {
                using (var ms = new MemoryStream(template.TemplateBytes))
                    report.Load(ms);
                return;
            }

            string xml = template.TemplateXml ?? "";
            if (xml.Length > 0 && xml[0] == '\uFEFF')
                xml = xml.Substring(1);
            report.LoadFromString(xml);
        }

        /// <summary>Xuất báo cáo đã Prepare ra định dạng Excel 2007 (.xlsx).</summary>
        public static void ExportExcel(Report preparedReport, string filePath)
        {
            if (preparedReport == null)
                throw new ArgumentNullException("preparedReport");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Chưa có đường dẫn file xuất Excel.", "filePath");

            using (var export = new Excel2007Export())
            {
                export.ShowProgress = false;
                export.OpenAfterExport = false;
                preparedReport.Export(export, filePath);
            }
        }

        /// <summary>Xuất báo cáo đã Prepare ra định dạng PDF (.pdf).</summary>
        public static void ExportPdf(Report preparedReport, string filePath)
        {
            if (preparedReport == null)
                throw new ArgumentNullException("preparedReport");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Chưa có đường dẫn file xuất PDF.", "filePath");

            using (var export = new PDFExport())
            {
                export.ShowProgress = false;
                export.OpenAfterExport = false;
                preparedReport.Export(export, filePath);
            }
        }

        /// <summary>Gửi báo cáo đã Prepare tới máy in chỉ định (không mở hộp chọn máy in của Windows).</summary>
        public static void PrintPrepared(Report preparedReport, string printerName)
        {
            if (preparedReport == null)
                throw new ArgumentNullException("preparedReport");

            preparedReport.PrintSettings.ShowDialog = false;
            if (!string.IsNullOrWhiteSpace(printerName))
                preparedReport.PrintSettings.Printer = printerName;
            preparedReport.PrintPrepared();
        }

        /// <summary>
        /// Đọc toàn bộ chữ đã dựng trên các trang (theo tên đối tượng TextObject và TableCell) để kiểm thử nội dung in thật.
        /// </summary>
        public static List<KeyValuePair<string, string>> ExtractPreparedPageTexts(Report preparedReport)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (preparedReport == null || preparedReport.PreparedPages == null)
                return result;

            int count = preparedReport.PreparedPages.Count;
            for (int p = 0; p < count; p++)
            {
                ReportPage page = preparedReport.PreparedPages.GetPage(p);
                if (page == null)
                    continue;
                foreach (Base obj in page.AllObjects)
                {
                    var table = obj as TableObject;
                    if (table != null)
                    {
                        for (int r = 0; r < table.RowCount; r++)
                        {
                            for (int c = 0; c < table.ColumnCount; c++)
                            {
                                TableCell cell = table[c, r];
                                if (cell != null)
                                    result.Add(new KeyValuePair<string, string>(cell.Name ?? "", cell.Text ?? ""));
                            }
                        }
                        continue;
                    }

                    var textObj = obj as TextObject;
                    if (textObj != null && !(textObj is TableCell))
                        result.Add(new KeyValuePair<string, string>(textObj.Name ?? "", textObj.Text ?? ""));
                }
            }
            return result;
        }

        internal static string DisplayTemplateName(PrintTemplateRecord template)
        {
            return template == null || string.IsNullOrWhiteSpace(template.Name)
                ? "(không tên)"
                : template.Name.Trim();
        }

        internal static string FormatError(string templateName, string operation, Exception ex)
        {
            string name = string.IsNullOrWhiteSpace(templateName) ? "(không tên)" : templateName.Trim();
            string reason = ShortReason(ex);
            if (ex is XmlException)
                return "Mẫu in '" + name + "' có nội dung XML không hợp lệ: " + reason;
            if (ex != null && ex.GetType().Name.IndexOf("Compiler", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mẫu in '" + name + "' bị lỗi biên dịch script: " + reason;
            return "Không thể " + operation + " với mẫu in '" + name + "': " + reason;
        }

        private static string ShortReason(Exception ex)
        {
            if (ex == null)
                return "lỗi không xác định.";
            Exception root = ex.InnerException ?? ex;
            string msg = (root.Message ?? ex.Message ?? ex.GetType().Name)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            while (msg.IndexOf("  ", StringComparison.Ordinal) >= 0)
                msg = msg.Replace("  ", " ");
            if (msg.Length > 180)
                msg = msg.Substring(0, 177) + "...";
            return msg.Length == 0 ? root.GetType().Name : msg;
        }
    }
}
