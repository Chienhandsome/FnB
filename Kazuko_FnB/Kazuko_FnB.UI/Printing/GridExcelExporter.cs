using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using Aspose.Cells;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Interface trừu tượng hóa hộp Lưu file, hỏi mở file, mở file và báo lỗi khi xuất lưới ra Excel (.xls).
    /// Tách riêng khỏi <see cref="IPrintOutputHandler"/> để không ảnh hưởng các ca test in FastReport hiện có.
    /// </summary>
    internal interface IGridExportOutput
    {
        string PromptSaveFile(IWin32Window owner, string defaultFileName);
        bool ConfirmOpenFile(IWin32Window owner, string filePath);
        void OpenFile(string filePath);
        void ShowError(IWin32Window owner, string message);
    }

    /// <summary>Triển khai mặc định trên WinForms cho xuất lưới ra file Excel (.xls).</summary>
    internal sealed class WinFormsGridExportOutput : IGridExportOutput
    {
        public string PromptSaveFile(IWin32Window owner, string defaultFileName)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Lưu file Excel";
                dlg.Filter = "Excel 97-2003 (*.xls)|*.xls";
                dlg.DefaultExt = "xls";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                dlg.FileName = defaultFileName ?? "";
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.FileName : null;
            }
        }

        public bool ConfirmOpenFile(IWin32Window owner, string filePath)
        {
            return Msg.Confirm(owner, PrintConstants.ConfirmOpenExportedFileMessage);
        }

        public void OpenFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        public void ShowError(IWin32Window owner, string message)
        {
            Msg.Error(owner, message);
        }
    }

    /// <summary>
    /// Hàm dùng chung xuất nội dung đang hiển thị của một <see cref="DataGridView"/> ra file Excel 97-2003 (.xls)
    /// bằng Aspose.Cells. Chỉ đọc các cột Visible (trừ nút bấm/ảnh) theo đúng DisplayIndex và các dòng đang hiện
    /// theo thứ tự hiện tại; không bao giờ đọc giá trị cột ẩn hay cột bị chặn quyền.
    /// </summary>
    internal static class GridExcelExporter
    {
        public static bool Export(
            Control owner,
            DataGridView grid,
            string defaultFileName,
            IGridExportOutput outputOverride,
            Action<string> recordMessage)
        {
            if (grid == null)
                return false;

            IGridExportOutput output = outputOverride ?? new WinFormsGridExportOutput();
            IWin32Window winOwner = ResolveOwner(owner);

            string targetPath = output.PromptSaveFile(winOwner, defaultFileName ?? "");
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            if (!grid.ReadOnly && grid.IsCurrentCellInEditMode)
                grid.EndEdit();

            Cursor oldCursor = owner != null ? owner.Cursor : Cursors.Default;
            try
            {
                if (owner != null)
                    owner.Cursor = Cursors.WaitCursor;
                WriteGridToXls(grid, targetPath);
            }
            catch (Exception ex)
            {
                string msg = FormatError("xuất file Excel", ex);
                if (recordMessage != null)
                    recordMessage(msg);
                output.ShowError(winOwner, msg);
                return false;
            }
            finally
            {
                if (owner != null)
                    owner.Cursor = oldCursor;
            }

            if (output.ConfirmOpenFile(winOwner, targetPath))
            {
                try
                {
                    output.OpenFile(targetPath);
                }
                catch (Exception ex)
                {
                    string msg = FormatError("mở file đã xuất", ex);
                    if (recordMessage != null)
                        recordMessage(msg);
                    output.ShowError(winOwner, msg);
                }
            }

            return true;
        }

        internal static void WriteGridToXls(DataGridView grid, string filePath)
        {
            List<DataGridViewColumn> columns = CollectExportColumns(grid);
            var wb = new Workbook();
            Worksheet sheet = wb.Worksheets[0];
            Cells cells = sheet.Cells;

            for (int c = 0; c < columns.Count; c++)
                cells[0, c].PutValue(columns[c].HeaderText ?? "");

            int excelRow = 1;
            for (int r = 0; r < grid.Rows.Count; r++)
            {
                DataGridViewRow row = grid.Rows[r];
                if (row.IsNewRow || !row.Visible)
                    continue;

                for (int c = 0; c < columns.Count; c++)
                {
                    DataGridViewColumn col = columns[c];
                    object rawValue = row.Cells[col.Index].Value;
                    WriteCellValue(cells[excelRow, c], rawValue, col);
                }
                excelRow++;
            }

            wb.Save(filePath, FileFormatType.Excel97To2003);
        }

        private static List<DataGridViewColumn> CollectExportColumns(DataGridView grid)
        {
            var result = new List<DataGridViewColumn>();
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                DataGridViewColumn col = grid.Columns[i];
                if (!col.Visible)
                    continue;
                if (col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                    continue;
                result.Add(col);
            }
            result.Sort(delegate (DataGridViewColumn a, DataGridViewColumn b)
            {
                int d = a.DisplayIndex.CompareTo(b.DisplayIndex);
                return d != 0 ? d : a.Index.CompareTo(b.Index);
            });
            return result;
        }

        private static void WriteCellValue(Cell cell, object value, DataGridViewColumn column)
        {
            if (value == null || value is DBNull)
                return;

            if (value is bool)
            {
                cell.PutValue((bool)value);
                return;
            }

            if (value is DateTime)
            {
                DateTime dt = (DateTime)value;
                cell.PutValue(dt);
                ApplyDateFormat(cell, ResolveDateFormat(column, dt));
                return;
            }

            if (value is int)
            {
                cell.PutValue((int)value);
                return;
            }

            if (IsNumericValue(value))
            {
                cell.PutValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return;
            }

            string text = value as string;
            if (text == null)
                text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
            cell.PutValue(text);
        }

        private static bool IsNumericValue(object value)
        {
            return value is byte
                || value is sbyte
                || value is short
                || value is ushort
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal;
        }

        private static string ResolveDateFormat(DataGridViewColumn column, DateTime value)
        {
            string fmt = column != null && column.DefaultCellStyle != null
                ? (column.DefaultCellStyle.Format ?? "").Trim()
                : "";
            if (fmt.Length > 0 && IsSupportedDateFormat(fmt))
                return fmt;
            return value.TimeOfDay == TimeSpan.Zero ? "dd/MM/yyyy" : "dd/MM/yyyy HH:mm";
        }

        private static bool IsSupportedDateFormat(string fmt)
        {
            return fmt.IndexOf('d') >= 0
                || fmt.IndexOf('M') >= 0
                || fmt.IndexOf('y') >= 0
                || fmt.IndexOf('H') >= 0
                || fmt.IndexOf('h') >= 0
                || fmt.IndexOf('m') >= 0;
        }

        private static void ApplyDateFormat(Cell cell, string format)
        {
            Style style = cell.GetStyle();
            if (style == null)
                return;
            style.Custom = format;
            cell.SetStyle(style);
        }

        private static IWin32Window ResolveOwner(Control owner)
        {
            if (owner == null)
                return null;
            Form form = owner.FindForm();
            return form != null ? (IWin32Window)form : owner;
        }

        private static string FormatError(string operation, Exception ex)
        {
            string reason = ex == null ? "Lỗi không xác định." : (ex.Message ?? ex.GetType().Name);
            reason = reason.Replace("\r", " ").Replace("\n", " ").Trim();
            if (reason.Length == 0)
                reason = ex == null ? "Lỗi không xác định." : ex.GetType().Name;
            return "Không thể " + operation + ": " + reason;
        }
    }
}
