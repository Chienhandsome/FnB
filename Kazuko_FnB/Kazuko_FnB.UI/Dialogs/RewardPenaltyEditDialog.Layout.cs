using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần dựng bố cục, tô màu theme và hook kiểm thử UI cho <see cref="RewardPenaltyEditDialog"/>.</summary>
    internal sealed partial class RewardPenaltyEditDialog
    {
        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NumberBoxForTest { get { return _number; } }
        internal DateTimePicker DatePickerForTest { get { return _date; } }
        internal LookupCombo EmployeeComboForTest { get { return _employee; } }
        internal NumberBox BonusBoxForTest { get { return _bonus; } }
        internal NumberBox PenaltyBoxForTest { get { return _penalty; } }
        internal LookupCombo ReasonComboForTest { get { return _reason; } }
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
            string[] captions = { "Số phiếu *", "Ngày *", "Nhân viên *", "Thưởng", "Lý do", "Ghi chú" };
            int maxCap = (int)(108 * s);
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
            int step = (int)(34 * s);

            int y = (int)(10 * s);
            int tab = 0;

            PlaceRow("Số phiếu *", _number, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            PlaceRow("Ngày *", _date, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            PlaceRow("Nhân viên *", _employee, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            int lblPenW = TextRenderer.MeasureText("Phạt", Font).Width + (int)(14 * s);
            int midGap = (int)(14 * s);
            int numW = Math.Max((int)(120 * s), (editW - lblPenW - midGap - gap) / 2);
            PlaceRow("Thưởng", _bonus, leftPad, y, maxCap, editX, numW, rowH, ref tab);
            int penLblX = editX + numW + midGap;
            int penEditX = penLblX + lblPenW + gap;
            int penEditW = Math.Max((int)(120 * s), bodyW - leftPad - penEditX);
            PlaceRow("Phạt", _penalty, penLblX, y, lblPenW, penEditX, penEditW, rowH, ref tab);
            y += step;

            PlaceRow("Lý do", _reason, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            int noteH = (int)(56 * s);
            PlaceRow("Ghi chú", _note, leftPad, y, maxCap, editX, editW, noteH, ref tab);
            y += noteH + (int)(8 * s);

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

            _employee.BackColor = _employee.Enabled ? t.Surface : disabledBack;
            _employee.ForeColor = t.Text;
            _reason.BackColor = _reason.Enabled ? t.Surface : disabledBack;
            _reason.ForeColor = t.Text;
            _bonus.BackColor = _bonus.Enabled ? t.Surface : disabledBack;
            _bonus.ForeColor = t.Text;
            _penalty.BackColor = _penalty.Enabled ? t.Surface : disabledBack;
            _penalty.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
