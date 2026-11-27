using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần dựng bố cục, tô màu theme và hook kiểm thử UI cho <see cref="RepairNoticeEditDialog"/>.</summary>
    internal sealed partial class RepairNoticeEditDialog
    {
        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NumberBoxForTest { get { return _number; } }
        internal DateTimePicker DatePickerForTest { get { return _date; } }
        internal LookupCombo RoomComboForTest { get { return _room; } }
        internal LookupCombo RoomTypeComboForTest { get { return _roomType; } }
        internal LookupCombo EmployeeComboForTest { get { return _employee; } }
        internal TextBox ContentBoxForTest { get { return _content; } }
        internal CheckBox FixedCheckForTest { get { return _chkFixed; } }
        internal CheckBox UsableCheckForTest { get { return _chkUsable; } }
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
            string[] leftCaptions = { "Số phiếu *", "Phòng/Bàn *", "Nhân viên", "Nội dung *", "Ghi chú" };
            int maxCap = (int)(102 * s);
            foreach (string cap in leftCaptions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > maxCap)
                    maxCap = need;
            }
            string[] rightCaptions = { "Ngày *", "Loại phòng" };
            int rightCap = (int)(82 * s);
            foreach (string cap in rightCaptions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > rightCap)
                    rightCap = need;
            }

            int leftPad = (int)(14 * s);
            int gap = (int)(8 * s);
            int midGap = (int)(14 * s);
            int editX = leftPad + maxCap + gap;
            int bodyW = (int)(BodyWidth96 * s);
            int editW = Math.Max((int)(280 * s), bodyW - editX - leftPad);
            int leftEditW = Math.Max((int)(140 * s), (editW - rightCap - midGap - gap) / 2);
            int rightLblX = editX + leftEditW + midGap;
            int rightEditX = rightLblX + rightCap + gap;
            int rightEditW = Math.Max((int)(140 * s), bodyW - leftPad - rightEditX);

            int rowH = (int)(26 * s);
            int step = (int)(32 * s);

            int y = (int)(10 * s);
            int tab = 0;

            // Ghép cặp ngang (Số phiếu | Ngày) và (Phòng/Bàn | Loại phòng) để hộp thoại gọn vừa 1366×768 ở 150% DPI.
            PlaceRow("Số phiếu *", _number, leftPad, y, maxCap, editX, leftEditW, rowH, ref tab);
            PlaceRow("Ngày *", _date, rightLblX, y, rightCap, rightEditX, rightEditW, rowH, ref tab);
            y += step;

            PlaceRow("Phòng/Bàn *", _room, leftPad, y, maxCap, editX, leftEditW, rowH, ref tab);
            PlaceRow("Loại phòng", _roomType, rightLblX, y, rightCap, rightEditX, rightEditW, rowH, ref tab);
            y += step;

            PlaceRow("Nhân viên", _employee, leftPad, y, maxCap, editX, editW, rowH, ref tab);
            y += step;

            int contentH = (int)(48 * s);
            PlaceRow("Nội dung *", _content, leftPad, y, maxCap, editX, editW, contentH, ref tab);
            y += contentH + (int)(6 * s);

            _chkFixed.Location = new Point(editX, y);
            _chkFixed.TabIndex = tab++;
            Body.Controls.Add(_chkFixed);

            Size fixedSz = _chkFixed.GetPreferredSize(Size.Empty);
            _chkUsable.Location = new Point(editX + fixedSz.Width + (int)(24 * s), y);
            _chkUsable.TabIndex = tab++;
            Body.Controls.Add(_chkUsable);
            y += Math.Max((int)(24 * s), fixedSz.Height) + (int)(6 * s);

            int noteH = (int)(44 * s);
            PlaceRow("Ghi chú", _note, leftPad, y, maxCap, editX, editW, noteH, ref tab);
            y += noteH + (int)(6 * s);

            int errH = (int)(20 * s);
            _lblError.SetBounds(leftPad, y, bodyW - 2 * leftPad, errH);
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);

            int bodyHeight96 = (int)Math.Ceiling((y + errH + (int)(6 * s)) / s);
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

            _number.BackColor = _number.ReadOnly ? disabledBack : t.Surface;
            _room.BackColor = _room.Enabled ? t.Surface : disabledBack;
            _room.ForeColor = t.Text;
            _roomType.BackColor = _roomType.Enabled ? t.Surface : disabledBack;
            _roomType.ForeColor = t.Text;
            _employee.BackColor = _employee.Enabled ? t.Surface : disabledBack;
            _employee.ForeColor = t.Text;
            _content.BackColor = _content.ReadOnly ? disabledBack : t.Surface;
            _note.BackColor = _note.ReadOnly ? disabledBack : t.Surface;
            _chkFixed.BackColor = Color.Transparent;
            _chkFixed.ForeColor = t.Text;
            _chkUsable.BackColor = Color.Transparent;
            _chkUsable.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
