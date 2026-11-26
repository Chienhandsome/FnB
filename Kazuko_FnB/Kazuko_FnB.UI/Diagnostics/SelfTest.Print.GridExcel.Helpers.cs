using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Aspose.Cells;
using Kazuko_FnB.Data;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>
        /// Interface giả thay thế <see cref="IGridExportOutput"/> trong selftest:
        /// không bật SaveFileDialog/MessageBox/Process.Start thật, chỉ ghi nhận lượt gọi và trả đường dẫn tạm.
        /// </summary>
        internal sealed class GridExportOutputRecorder : IGridExportOutput
        {
            public int PromptSaveCount { get; private set; }
            public int ConfirmOpenCount { get; private set; }
            public int OpenFileCount { get; private set; }

            public string LastDefaultFileName { get; private set; }
            public string LastConfirmPath { get; private set; }
            public string LastOpenedPath { get; private set; }
            public List<string> ErrorMessages { get; private set; }

            public string NextSavePath { get; set; }
            public bool ConfirmOpenResult { get; set; }
            public bool ThrowOnOpen { get; set; }

            public GridExportOutputRecorder()
            {
                ErrorMessages = new List<string>();
            }

            public string PromptSaveFile(IWin32Window owner, string defaultFileName)
            {
                PromptSaveCount++;
                LastDefaultFileName = defaultFileName;
                return NextSavePath;
            }

            public bool ConfirmOpenFile(IWin32Window owner, string filePath)
            {
                ConfirmOpenCount++;
                LastConfirmPath = filePath;
                return ConfirmOpenResult;
            }

            public void OpenFile(string filePath)
            {
                if (ThrowOnOpen)
                    throw new IOException("Giả lập lỗi mở file đã xuất.");
                OpenFileCount++;
                LastOpenedPath = filePath;
            }

            public void ShowError(IWin32Window owner, string message)
            {
                ErrorMessages.Add(SingleLineDetail(message));
            }
        }

        private sealed class XlsCellInfo
        {
            public CellValueType Type;
            public object RawValue;
            public string StringValue;
            public double? DoubleValue;
            public bool? BoolValue;
            public DateTime? DateTimeValue;
            public string NumberFormat;
        }

        private sealed class XlsSheetSnapshot
        {
            public int RowCount;
            public int ColCount;
            public int WorksheetCount;
            public List<string> WorksheetNames = new List<string>();
            public List<XlsCellInfo[]> Rows = new List<XlsCellInfo[]>();
        }

        private static XlsSheetSnapshot ReadFirstXlsSheet(string filePath)
        {
            var snap = new XlsSheetSnapshot();
            var wb = new Workbook();
            wb.Open(filePath);
            snap.WorksheetCount = wb.Worksheets.Count;
            for (int i = 0; i < wb.Worksheets.Count; i++)
                snap.WorksheetNames.Add(wb.Worksheets[i].Name ?? "");
            if (wb.Worksheets.Count == 0)
                return snap;

            Cells cells = wb.Worksheets[0].Cells;
            int maxRow = cells.MaxDataRow;
            int maxCol = cells.MaxDataColumn;
            if (maxRow < 0 || maxCol < 0)
                return snap;

            snap.RowCount = maxRow + 1;
            snap.ColCount = maxCol + 1;
            for (int r = 0; r <= maxRow; r++)
            {
                var row = new XlsCellInfo[snap.ColCount];
                for (int c = 0; c <= maxCol; c++)
                {
                    Cell cell = cells[r, c];
                    Style st = cell.GetStyle();
                    string fmt = st == null ? "" : (st.Custom ?? "");
                    var info = new XlsCellInfo
                    {
                        Type = cell.Type,
                        RawValue = cell.Value,
                        StringValue = cell.Value == null ? "" : (cell.StringValue ?? ""),
                        NumberFormat = fmt
                    };
                    if (cell.Type == CellValueType.IsNumeric)
                    {
                        info.DoubleValue = cell.DoubleValue;
                        if (fmt.IndexOf("dd", StringComparison.OrdinalIgnoreCase) >= 0
                            || fmt.IndexOf("yyyy", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try { info.DateTimeValue = cell.DateTimeValue; } catch { }
                        }
                    }
                    else if (cell.Type == CellValueType.IsBool)
                    {
                        info.BoolValue = cell.BoolValue;
                    }
                    else if (cell.Type == CellValueType.IsDateTime)
                    {
                        info.DateTimeValue = cell.DateTimeValue;
                    }
                    row[c] = info;
                }
                snap.Rows.Add(row);
            }
            return snap;
        }

        private static bool XlsSheetContainsSubstring(XlsSheetSnapshot snap, string needle)
        {
            if (snap == null || string.IsNullOrEmpty(needle))
                return false;
            for (int r = 0; r < snap.Rows.Count; r++)
            {
                XlsCellInfo[] row = snap.Rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    XlsCellInfo cell = row[c];
                    if (cell == null)
                        continue;
                    if (!string.IsNullOrEmpty(cell.StringValue)
                        && cell.StringValue.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (cell.RawValue != null)
                    {
                        string raw = Convert.ToString(cell.RawValue, CultureInfo.InvariantCulture) ?? "";
                        if (raw.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            return false;
        }

        private static List<string> VisibleGridHeaders(DataGridView grid)
        {
            var cols = new List<DataGridViewColumn>();
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                DataGridViewColumn col = grid.Columns[i];
                if (!col.Visible || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                    continue;
                cols.Add(col);
            }
            cols.Sort(delegate (DataGridViewColumn a, DataGridViewColumn b)
            {
                int d = a.DisplayIndex.CompareTo(b.DisplayIndex);
                return d != 0 ? d : a.Index.CompareTo(b.Index);
            });
            var headers = new List<string>(cols.Count);
            for (int i = 0; i < cols.Count; i++)
                headers.Add(cols[i].HeaderText ?? "");
            return headers;
        }

        private static bool HeadersMatch(XlsSheetSnapshot snap, List<string> expectedHeaders)
        {
            if (snap == null || expectedHeaders == null || snap.RowCount == 0 || snap.ColCount != expectedHeaders.Count)
                return false;
            for (int c = 0; c < expectedHeaders.Count; c++)
            {
                if (!string.Equals(snap.Rows[0][c].StringValue, expectedHeaders[c], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static int CountVisibleGridRows(DataGridView grid)
        {
            int count = 0;
            for (int r = 0; r < grid.Rows.Count; r++)
            {
                if (!grid.Rows[r].IsNewRow && grid.Rows[r].Visible)
                    count++;
            }
            return count;
        }

        private static string SnapshotP3aDbState(string db)
        {
            return DbHelper.Read(db, conn =>
            {
                string[] tables =
                {
                    "TDATHANG", "TDATHANGCHITIET", "TBAOGIA", "TBAOGIACHITIET",
                    "DMATHANG", "DBAN", "STEMPLATE", "SREPORTTEMPLATE"
                };
                var parts = new List<string>(tables.Length);
                for (int i = 0; i < tables.Length; i++)
                {
                    DataTable dt = DbHelper.Table(conn, null, "SELECT COUNT(*) FROM \"" + tables[i] + "\"");
                    parts.Add(tables[i] + "=" + DbHelper.Int(dt.Rows[0][0]));
                }
                return string.Join(";", parts.ToArray());
            });
        }

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Bỏ qua lỗi dọn file tạm.
            }
        }

        private static bool RunQuoteListExcelQuickCheck(QuoteListView view)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "kazuko-quote-ui-" + Guid.NewGuid().ToString("N") + ".xls");
            var rec = new GridExportOutputRecorder { NextSavePath = tempPath, ConfirmOpenResult = false };
            view.GridExportOutputForTest = rec;
            try
            {
                view.ExcelButtonForTest.PerformClick();
                if (rec.PromptSaveCount != 1 || rec.ConfirmOpenCount != 1 || rec.OpenFileCount != 0 || !File.Exists(tempPath))
                    return false;
                XlsSheetSnapshot snap = ReadFirstXlsSheet(tempPath);
                return HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                    && snap.RowCount == CountVisibleGridRows(view.GridForTest) + 1;
            }
            finally
            {
                view.GridExportOutputForTest = null;
                SafeDeleteFile(tempPath);
            }
        }
    }
}
