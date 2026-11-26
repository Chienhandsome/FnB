using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Ô nhập số dùng chung cho mọi cột số sửa được trên DataGridView (docs/specs/nhap-so/, N1–N4, N6):
    /// - Hiển thị kiểu vi-VN (ngăn nghìn '.', thập phân ',') độc lập CultureInfo của Windows, tối đa MaxDecimalPlaces.
    /// - Khi vào chế độ sửa: hiện số không có dấu ngăn nghìn, đủ phần lẻ (NumberInput.FormatForEdit).
    /// - Khi gõ phím: đổi '.' thành ','; không chặn dấu thứ hai (1.500.000 giữ đủ hai dấu để rời ô báo không đọc được);
    ///   chặn chữ số lẻ vượt MaxDecimalPlaces khi chuỗi chỉ có đúng một dấu.
    /// - Khi dán hoặc gõ dấu vào giữa số làm phần lẻ vượt mức: đổi '.' thành ',' và cắt phần lẻ thừa (không làm tròn).
    /// - Gỡ handler khi rời ô (DetachEditingControl) để dùng lại DataGridViewTextBoxEditingControl an toàn cho cột chữ.
    /// </summary>
    public sealed class NumericEditGridCell : DataGridViewTextBoxCell
    {
        private int _maxDecimalPlaces;
        private DataGridViewTextBoxEditingControl _hookedControl;
        private bool _normalizingText;

        public NumericEditGridCell()
            : this(2)
        {
        }

        public NumericEditGridCell(int maxDecimalPlaces)
        {
            _maxDecimalPlaces = maxDecimalPlaces < 0 ? 0 : maxDecimalPlaces;
            ValueType = typeof(decimal);
        }

        public int MaxDecimalPlaces
        {
            get { return _maxDecimalPlaces; }
            set { _maxDecimalPlaces = value < 0 ? 0 : value; }
        }

        public override object Clone()
        {
            var copy = (NumericEditGridCell)base.Clone();
            copy._maxDecimalPlaces = _maxDecimalPlaces;
            return copy;
        }

        protected override object GetFormattedValue(
            object value,
            int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter,
            TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            if (value is decimal)
                return NumberInput.FormatForDisplay((decimal)value, _maxDecimalPlaces, true);
            return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
        }

        public override void InitializeEditingControl(
            int rowIndex,
            object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            UnhookEditingControl();
            object raw = rowIndex >= 0 ? GetValue(rowIndex) : initialFormattedValue;
            base.InitializeEditingControl(rowIndex, NumberInput.FormatForEdit(raw), dataGridViewCellStyle);
            var tb = DataGridView != null ? DataGridView.EditingControl as DataGridViewTextBoxEditingControl : null;
            HookEditingControl(tb);
        }

        public override void DetachEditingControl()
        {
            UnhookEditingControl();
            base.DetachEditingControl();
        }

        private void HookEditingControl(DataGridViewTextBoxEditingControl tb)
        {
            UnhookEditingControl();
            if (tb == null)
                return;
            _hookedControl = tb;
            tb.KeyPress += OnEditingControlKeyPress;
            tb.TextChanged += OnEditingControlTextChanged;
        }

        private void UnhookEditingControl()
        {
            if (_hookedControl == null)
                return;
            _hookedControl.KeyPress -= OnEditingControlKeyPress;
            _hookedControl.TextChanged -= OnEditingControlTextChanged;
            _hookedControl = null;
        }

        private void OnEditingControlKeyPress(object sender, KeyPressEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null || tb.ReadOnly)
                return;

            char c = e.KeyChar;
            if (char.IsControl(c) || (Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
                return;

            if (c == '.')
            {
                c = ',';
                e.KeyChar = ',';
            }

            if (c == ',' || c == '-')
                return;

            if (c >= '0' && c <= '9')
            {
                if (NumberInput.WouldExceedGridDecimalPlaces(tb.Text, tb.SelectionStart, tb.SelectionLength, c, _maxDecimalPlaces))
                    e.Handled = true;
                return;
            }

            e.Handled = true;
        }

        private void OnEditingControlTextChanged(object sender, EventArgs e)
        {
            if (_normalizingText)
                return;
            var tb = sender as TextBox;
            if (tb == null || tb.ReadOnly)
                return;
            string raw = tb.Text;
            string normalized = NumberInput.NormalizeGridEditText(raw, _maxDecimalPlaces);
            if (string.Equals(raw, normalized, StringComparison.Ordinal))
                return;
            int selStart = tb.SelectionStart;
            int selLen = tb.SelectionLength;
            _normalizingText = true;
            try
            {
                tb.Text = normalized;
                int start = Math.Min(selStart, normalized.Length);
                tb.SelectionStart = start;
                tb.SelectionLength = Math.Min(selLen, normalized.Length - start);
            }
            finally
            {
                _normalizingText = false;
            }
        }

        /// <summary>Gắn CellTemplate, ValueType và định dạng vi-VN độc lập Windows cho một cột số sửa được.</summary>
        public static void ConfigureColumn(DataGridViewTextBoxColumn column, int maxDecimalPlaces)
        {
            if (column == null)
                return;
            int dp = maxDecimalPlaces < 0 ? 0 : maxDecimalPlaces;
            column.CellTemplate = new NumericEditGridCell(dp);
            column.ValueType = typeof(decimal);
            column.DefaultCellStyle.Format = dp > 0 ? "#,##0." + new string('#', dp) : "#,##0";
            column.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        public static bool IsNumericCell(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (grid == null || rowIndex < 0 || rowIndex >= grid.Rows.Count
                || columnIndex < 0 || columnIndex >= grid.Columns.Count)
                return false;
            return grid.Rows[rowIndex].Cells[columnIndex] is NumericEditGridCell;
        }

        /// <summary>
        /// Xử lý CurrentCellDirtyStateChanged cho lưới có cả cột chọn (combo/checkbox) và cột số:
        /// cột chọn commit ngay như cũ; cột số KHÔNG commit từng phím (chỉ báo dirty để bật nút Lưu).
        /// </summary>
        public static void HandleDirtyStateChanged(DataGridView grid, Action onNumericDirty)
        {
            if (grid == null || !grid.IsCurrentCellDirty)
                return;
            if (grid.CurrentCell is NumericEditGridCell)
            {
                if (onNumericDirty != null)
                    onNumericDirty();
                return;
            }
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        /// <summary>
        /// Đọc số ở CellParsing theo NumberInput.TryParse: không đọc được thì khôi phục số cũ trên ô
        /// và đánh dấu ParsingApplied = true để không bao giờ phát sinh DataError.
        /// </summary>
        public static bool ParseCell(DataGridView grid, DataGridViewCellParsingEventArgs e, decimal fallbackIfEmpty)
        {
            if (!IsNumericCell(grid, e.RowIndex, e.ColumnIndex))
                return false;
            decimal oldVal = fallbackIfEmpty;
            decimal cur;
            if (NumberInput.TryParse(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, out cur))
                oldVal = cur;
            decimal parsed;
            if (NumberInput.TryParse(e.Value, out parsed))
            {
                e.Value = parsed;
                e.ParsingApplied = true;
                return true;
            }
            e.Value = oldVal;
            e.ParsingApplied = true;
            return false;
        }

        /// <summary>Ghi nhận chi tiết DataError cho selftest (e.Context, tên cột, chỉ số dòng, thông điệp ngoại lệ), giữ đủ nhiều lần.</summary>
        public static string AppendDataErrorDetail(string existing, DataGridView grid, DataGridViewDataErrorEventArgs e)
        {
            if (e == null)
                return existing ?? "";
            string colName = (grid != null && e.ColumnIndex >= 0 && e.ColumnIndex < grid.Columns.Count)
                ? grid.Columns[e.ColumnIndex].Name
                : e.ColumnIndex.ToString(CultureInfo.InvariantCulture);
            string msg = e.Exception != null ? e.Exception.Message : "null";
            string entry = string.Format(CultureInfo.InvariantCulture,
                "{0} col={1} row={2} msg={3}", e.Context, colName, e.RowIndex, msg);
            return string.IsNullOrEmpty(existing) ? entry : existing + "; " + entry;
        }
    }
}
