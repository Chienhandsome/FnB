using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Ô số cho DataGridView: hiển thị giữ nguyên định dạng có dấu ngăn nghìn của cột,
    /// nhưng khi vào chế độ sửa thì hiện số không dấu ngăn nghìn và giữ đủ phần thập phân (NumberInput.FormatForEdit).
    /// </summary>
    public sealed class NumericEditGridCell : DataGridViewTextBoxCell
    {
        public NumericEditGridCell()
        {
            ValueType = typeof(decimal);
        }

        public override void InitializeEditingControl(
            int rowIndex,
            object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            object raw = rowIndex >= 0 ? GetValue(rowIndex) : initialFormattedValue;
            base.InitializeEditingControl(rowIndex, NumberInput.FormatForEdit(raw), dataGridViewCellStyle);
        }
    }
}
