using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Aspose.Cells;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>
        /// Interface giả thay thế IPrintOutputHandler trong selftest: không bao giờ in ra máy in thật,
        /// không bật SaveFileDialog/PreviewForm/Process.Start, chỉ ghi nhận lựa chọn và gọi hàm cần kiểm.
        /// </summary>
        internal sealed class PrintOutputRecorder : IPrintOutputHandler
        {
            public int PreviewCount { get; private set; }
            public int PrintCount { get; private set; }
            public int ExcelCount { get; private set; }
            public int PdfCount { get; private set; }
            public int ConfirmOpenCount { get; private set; }
            public int OpenFileCount { get; private set; }

            public PrintTemplateRecord LastTemplate { get; private set; }
            public PrintActionKind? LastAction { get; private set; }
            public string LastPrinterName { get; private set; }
            public string LastPreviewTitle { get; private set; }
            public string LastExportPath { get; private set; }
            public string LastOpenedPath { get; private set; }
            public int LastPreparedPageCount { get; private set; }

            public List<string> ErrorMessages { get; private set; }
            public List<string> InfoMessages { get; private set; }

            public string NextSavePath { get; set; }
            public bool ConfirmOpenResult { get; set; }
            public bool ThrowOnExport { get; set; }
            public bool RunRealExport { get; set; }

            public PrintOutputRecorder()
            {
                ErrorMessages = new List<string>();
                InfoMessages = new List<string>();
            }

            public void ShowPreview(IWin32Window owner, Report preparedReport, PrintTemplateRecord template, string title)
            {
                PreviewCount++;
                LastTemplate = template;
                LastAction = PrintActionKind.Preview;
                LastPreviewTitle = title;
                LastPreparedPageCount = preparedReport != null && preparedReport.PreparedPages != null
                    ? preparedReport.PreparedPages.Count
                    : 0;
            }

            public void PrintToPrinter(Report preparedReport, PrintTemplateRecord template, string printerName)
            {
                PrintCount++;
                LastTemplate = template;
                LastAction = PrintActionKind.Print;
                LastPrinterName = printerName;
                LastPreparedPageCount = preparedReport != null && preparedReport.PreparedPages != null
                    ? preparedReport.PreparedPages.Count
                    : 0;
            }

            public string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName)
            {
                return NextSavePath;
            }

            public void ExportExcel(Report preparedReport, PrintTemplateRecord template, string filePath)
            {
                if (ThrowOnExport)
                    throw new IOException("Giả lập lỗi ghi file Excel.");
                ExcelCount++;
                LastTemplate = template;
                LastAction = PrintActionKind.ExportExcel;
                LastExportPath = filePath;
                if (RunRealExport)
                    FastReportEngine.ExportExcel(preparedReport, filePath);
            }

            public void ExportPdf(Report preparedReport, PrintTemplateRecord template, string filePath)
            {
                if (ThrowOnExport)
                    throw new IOException("Giả lập lỗi ghi file PDF.");
                PdfCount++;
                LastTemplate = template;
                LastAction = PrintActionKind.ExportPdf;
                LastExportPath = filePath;
                if (RunRealExport)
                    FastReportEngine.ExportPdf(preparedReport, filePath);
            }

            public bool ConfirmOpenFile(IWin32Window owner, string filePath)
            {
                ConfirmOpenCount++;
                return ConfirmOpenResult;
            }

            public void OpenFile(string filePath)
            {
                OpenFileCount++;
                LastOpenedPath = filePath;
            }

            public void ShowError(IWin32Window owner, string message)
            {
                ErrorMessages.Add(message ?? "");
            }

            public void ShowInfo(IWin32Window owner, string message)
            {
                InfoMessages.Add(message ?? "");
            }
        }

        private sealed class ExcelCellVal
        {
            public string Text;
            public decimal? NumericValue;
        }

        private static Dictionary<string, string> SnapshotStemplate(string db)
        {
            return DbHelper.Read(db, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, SFORMID, STATUS, TEMPLATE FROM STEMPLATE ORDER BY ID");
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                using (SHA256 sha = SHA256.Create())
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string id = DbHelper.Str(r["ID"]) ?? "";
                        object raw = r["TEMPLATE"];
                        byte[] bytes;
                        if (raw == null || raw is DBNull)
                            bytes = new byte[0];
                        else if (raw is byte[])
                            bytes = (byte[])raw;
                        else
                            bytes = Encoding.UTF8.GetBytes(Convert.ToString(raw) ?? "");
                        string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                        map[id] = DbHelper.Int(r["STATUS"]) + "|" + (DbHelper.Str(r["NAME"]) ?? "") + "|" +
                                  (DbHelper.Str(r["SFORMID"]) ?? "") + "|" + bytes.Length + "|" + hash;
                    }
                }
                return map;
            });
        }

        private static bool SnapshotsEqual(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (KeyValuePair<string, string> kv in a)
            {
                string other;
                if (!b.TryGetValue(kv.Key, out other) || !string.Equals(kv.Value, other, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool IsValidPdfFile(string pdfPath)
        {
            if (!File.Exists(pdfPath))
                return false;
            byte[] bytes = File.ReadAllBytes(pdfPath);
            return bytes.Length > 500
                && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D'
                && bytes[3] == (byte)'F' && bytes[4] == (byte)'-';
        }

        private static string FindObjectText(List<KeyValuePair<string, string>> items, string objectName)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Key, objectName, StringComparison.Ordinal))
                    return items[i].Value ?? "";
            }
            return "";
        }

        private static bool PageTextsContain(List<KeyValuePair<string, string>> items, string expected)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if ((items[i].Value ?? "").IndexOf(expected, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static List<List<ExcelCellVal>> ReadExcelAllRows(string filePath)
        {
            var result = new List<List<ExcelCellVal>>();
            var wb = new Workbook();
            wb.Open(filePath, FileFormatType.Excel2007Xlsx);
            if (wb.Worksheets.Count == 0)
                return result;

            Cells cells = wb.Worksheets[0].Cells;
            int maxRow = cells.MaxDataRow;
            int maxCol = cells.MaxDataColumn;
            for (int r = 0; r <= maxRow; r++)
            {
                var row = new List<ExcelCellVal>();
                for (int c = 0; c <= maxCol; c++)
                {
                    Cell cell = cells[r, c];
                    if (cell == null || cell.Value == null)
                        continue;
                    if (cell.Type == CellValueType.IsNumeric)
                    {
                        decimal num = Convert.ToDecimal(cell.DoubleValue);
                        row.Add(new ExcelCellVal { Text = num.ToString(CultureInfo.InvariantCulture), NumericValue = num });
                    }
                    else
                    {
                        string txt = (cell.StringValue ?? "").Trim();
                        if (txt.Length > 0)
                            row.Add(new ExcelCellVal { Text = txt });
                    }
                }
                if (row.Count > 0)
                    result.Add(row);
            }
            return result;
        }

        private static string FormatExcelRow(List<ExcelCellVal> row)
        {
            if (row == null)
                return "null";
            var parts = new List<string>(row.Count);
            for (int i = 0; i < row.Count; i++)
                parts.Add(row[i].Text ?? "");
            return string.Join("|", parts.ToArray());
        }

        private static List<ExcelCellVal> FindRowContaining(List<List<ExcelCellVal>> rows, string text)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (RowHasText(rows[i], text))
                    return rows[i];
            }
            return null;
        }

        private static bool RowHasText(List<ExcelCellVal> row, string text)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if ((row[i].Text ?? "").IndexOf(text, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static bool RowHasNumber(List<ExcelCellVal> row, decimal expected)
        {
            return RowHasNumberApprox(row, expected, 0.001m);
        }

        private static bool RowHasNumberApprox(List<ExcelCellVal> row, decimal expected, decimal tolerance)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i].NumericValue.HasValue && Math.Abs(row[i].NumericValue.Value - expected) <= tolerance)
                    return true;
                decimal parsed;
                if (!row[i].NumericValue.HasValue && TryParseReportNumber(row[i].Text, out parsed) && Math.Abs(parsed - expected) <= tolerance)
                    return true;
            }
            return false;
        }

        private static bool SheetHasNumber(List<List<ExcelCellVal>> rows, decimal expected)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (RowHasNumber(rows[i], expected))
                    return true;
            }
            return false;
        }

        private static bool TryParseReportNumber(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string s = raw.Trim();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
                return true;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return true;
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out value);
        }
    }
}
