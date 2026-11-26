using System;
using System.Collections.Generic;
using System.Globalization;
using Aspose.Cells;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Dữ liệu bảng thô đọc từ sheet đầu tiên của file Excel (.xlsx/.xls), giữ cờ kiểu số từng ô.</summary>
    internal sealed class QuoteExcelSheetData
    {
        public List<string> Headers { get; set; }
        public List<QuoteExcelCell[]> Rows { get; set; }

        public QuoteExcelSheetData()
        {
            Headers = new List<string>();
            Rows = new List<QuoteExcelCell[]>();
        }
    }

    /// <summary>
    /// Đọc file Excel bằng Aspose.Cells bản có sẵn trong lib/ (dùng Workbook.Open + FileFormatType).
    /// Phân biệt rõ ô số (IsNumeric = true, chuỗi InvariantCulture) và ô chữ (IsNumeric = false).
    /// </summary>
    internal static class QuoteExcelReader
    {
        public static QuoteExcelSheetData ReadFirstSheet(string filePath)
        {
            var data = new QuoteExcelSheetData();
            if (string.IsNullOrEmpty(filePath))
                return data;

            var wb = new Workbook();
            if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                wb.Open(filePath, FileFormatType.Excel2007Xlsx);
            else
                wb.Open(filePath);

            if (wb.Worksheets.Count == 0)
                return data;

            Cells cells = wb.Worksheets[0].Cells;
            int maxRow = cells.MaxDataRow;
            int maxCol = cells.MaxDataColumn;
            if (maxRow < 0 || maxCol < 0)
                return data;

            int colCount = maxCol + 1;
            for (int c = 0; c < colCount; c++)
            {
                string header = ReadCell(cells[0, c]).Text.Trim();
                if (header.Length == 0)
                    header = "Cột " + (c + 1);
                data.Headers.Add(header);
            }

            for (int r = 1; r <= maxRow; r++)
            {
                var row = new QuoteExcelCell[colCount];
                bool anyNonEmpty = false;
                for (int c = 0; c < colCount; c++)
                {
                    QuoteExcelCell cell = ReadCell(cells[r, c]);
                    row[c] = cell;
                    if (!string.IsNullOrWhiteSpace(cell.Text))
                        anyNonEmpty = true;
                }
                if (anyNonEmpty)
                    data.Rows.Add(row);
            }
            return data;
        }

        private static QuoteExcelCell ReadCell(Cell cell)
        {
            if (cell == null || cell.Value == null)
                return QuoteExcelCell.TextCell("");
            if (cell.Type == CellValueType.IsNumeric)
                return new QuoteExcelCell
                {
                    Text = Convert.ToDecimal(cell.DoubleValue).ToString(CultureInfo.InvariantCulture),
                    IsNumeric = true
                };
            return QuoteExcelCell.TextCell(cell.StringValue ?? "");
        }
    }
}
