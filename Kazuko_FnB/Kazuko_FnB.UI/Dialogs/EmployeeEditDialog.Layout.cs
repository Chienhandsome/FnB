using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần dựng bố cục, tô màu theme và hook kiểm thử UI cho <see cref="EmployeeEditDialog"/>.</summary>
    internal sealed partial class EmployeeEditDialog
    {
        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal bool UseTimekeeperForTest { get { return _useTimekeeper; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal TextBox PhoneBoxForTest { get { return _phone; } }
        internal TextBox AddressBoxForTest { get { return _address; } }
        internal LookupCombo ShiftComboForTest { get { return _shift; } }
        internal RadioButton ByShiftRadioForTest { get { return _rdByShift; } }
        internal RadioButton ByMonthRadioForTest { get { return _rdByMonth; } }
        internal RadioButton MonthByShiftRadioForTest { get { return _rdMonthByShift; } }
        internal CheckBox OffSaturdayCheckForTest { get { return _chkOffSaturday; } }
        internal CheckBox OffSundayCheckForTest { get { return _chkOffSunday; } }
        internal NumberBox ShiftSalaryBoxForTest { get { return _shiftSalary; } }
        internal NumberBox MonthlySalaryBoxForTest { get { return _monthlySalary; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        private void BuildFields()
        {
            float s = Dpi;
            string[] captions =
            {
                "Tên nhân viên *",
                "Mã nhân viên *",
                "Điện thoại",
                "Địa chỉ",
                "Ca làm việc *",
                "Lương ca",
                "Ghi chú"
            };
            int maxCap = (int)(118 * s);
            foreach (string cap in captions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > maxCap)
                    maxCap = need;
            }
            int leftPad = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = leftPad + maxCap + gap;
            int bodyW = (int)(BodyWidth96 * s);
            int editW = Math.Max((int)(240 * s), bodyW - editX - leftPad);
            int rowH = (int)(26 * s);
            int step = (int)(32 * s);

            int y = (int)(10 * s);
            int tab = 0;

            PlaceRow("Tên nhân viên *", _name, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            _lblCode = PlaceRow("Mã nhân viên *", _code, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            _lblCode.Visible = _useTimekeeper;
            _code.Visible = _useTimekeeper;
            if (_useTimekeeper)
                y += step;

            PlaceRow("Điện thoại", _phone, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            PlaceRow("Địa chỉ", _address, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            _lblShift = PlaceRow("Ca làm việc *", _shift, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            _lblShift.Visible = _useTimekeeper;
            _shift.Visible = _useTimekeeper;
            if (_useTimekeeper)
                y += step;

            // Nhóm Cách tính lương (3 radio + 2 checkbox nghỉ thứ 7 / chủ nhật).
            int grpH = (int)(72 * s);
            _grpSalary.SetBounds(leftPad, y, bodyW - 2 * leftPad, grpH);
            _grpSalary.TabStop = false;
            int rx = (int)(14 * s);
            int ry1 = (int)(22 * s);
            int ry2 = (int)(46 * s);
            _rdByShift.Location = new Point(rx, ry1);
            _rdByShift.TabIndex = tab++;
            int rd1W = TextRenderer.MeasureText(_rdByShift.Text, Font).Width + (int)(32 * s);
            _rdByMonth.Location = new Point(rx + rd1W, ry1);
            _rdByMonth.TabIndex = tab++;
            int rd2W = TextRenderer.MeasureText(_rdByMonth.Text, Font).Width + (int)(32 * s);
            _rdMonthByShift.Location = new Point(_rdByMonth.Left + rd2W, ry1);
            _rdMonthByShift.TabIndex = tab++;

            _chkOffSaturday.Location = new Point(rx, ry2);
            _chkOffSaturday.TabIndex = tab++;
            int chk1W = TextRenderer.MeasureText(_chkOffSaturday.Text, Font).Width + (int)(32 * s);
            _chkOffSunday.Location = new Point(rx + chk1W, ry2);
            _chkOffSunday.TabIndex = tab++;

            _grpSalary.Controls.Add(_rdByShift);
            _grpSalary.Controls.Add(_rdByMonth);
            _grpSalary.Controls.Add(_rdMonthByShift);
            _grpSalary.Controls.Add(_chkOffSaturday);
            _grpSalary.Controls.Add(_chkOffSunday);
            Body.Controls.Add(_grpSalary);
            y += grpH + (int)(8 * s);

            // Hàng Lương ca & Lương tháng nằm ngang để giữ hộp thoại gọn không cuộn dọc.
            int lblMonthW = TextRenderer.MeasureText("Lương tháng", Font).Width + (int)(12 * s);
            int midGap = (int)(12 * s);
            int numW = Math.Max((int)(120 * s), (editW - lblMonthW - midGap - gap) / 2);
            PlaceRow("Lương ca", _shiftSalary, leftPad, y, maxCap, editX, numW, rowH, ref tab);
            int monthLblX = editX + numW + midGap;
            int monthEditX = monthLblX + lblMonthW + gap;
            int monthEditW = Math.Max((int)(120 * s), bodyW - leftPad - monthEditX);
            PlaceRow("Lương tháng", _monthlySalary, monthLblX, y, lblMonthW, monthEditX, monthEditW, rowH, ref tab);
            y += step;

            int noteH = (int)(48 * s);
            PlaceRow("Ghi chú", _note, leftPad, y, maxCap, editX, editW, noteH, ref tab);
            y += noteH + (int)(6 * s);

            int errH = (int)(22 * s);
            _lblError.SetBounds(leftPad, y, bodyW - 2 * leftPad, errH);
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);

            int bodyHeight96 = (int)Math.Ceiling((y + errH + (int)(8 * s)) / s);
            FitBody(BodyWidth96, bodyHeight96);
        }

        private Label PlaceRow(string caption, Control editor, int x, int y, int labelW, int editX, int editW, int h, ref int tabIndex)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(x, y, labelW, Math.Min(h, (int)(26 * Dpi)));
            editor.SetBounds(editX, y, editW, h);
            editor.TabIndex = tabIndex++;
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
            return lbl;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            _grpSalary.BackColor = t.Background;
            _grpSalary.ForeColor = t.Text;
            _shift.BackColor = _shift.Enabled ? t.Surface : disabledBack;
            _shift.ForeColor = t.Text;
            _shiftSalary.BackColor = _shiftSalary.Enabled ? t.Surface : disabledBack;
            _shiftSalary.ForeColor = t.Text;
            _monthlySalary.BackColor = _monthlySalary.Enabled ? t.Surface : disabledBack;
            _monthlySalary.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
