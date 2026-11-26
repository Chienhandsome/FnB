using System;
using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Ô nhập số dùng chung thay NumericUpDown trực tiếp (docs/specs/nhap-so/, N1-N6):
    /// - Không phụ thuộc CultureInfo.CurrentCulture của Windows.
    /// - Không đang sửa: hiển thị kiểu vi-VN (ngăn nghìn '.', thập phân ','), chỉ hiện chữ số lẻ đang có, tối đa DecimalPlaces.
    /// - Đang sửa (có focus, không chỉ đọc): hiện không có dấu ngăn nghìn; phím '.' và chuỗi dán/gán đổi '.' thành ',';
    ///   không chặn dấu phẩy thứ hai (khi rời ô coi là không đọc được và giữ số cũ).
    /// - Rời ô, Enter, nút mũi tên, hoặc code đọc .Value khi đang gõ dở: đọc theo N1; làm tròn AwayFromZero về DecimalPlaces (N4);
    ///   ô số nguyên (DecimalPlaces == 0) gõ số lẻ coi là không đọc được và giữ số cũ (N5).
    /// - Giá trị do code gán qua .Value được giữ nguyên từng chữ số; vào rồi ra không sửa chữ không đổi Value và không gọi ValueChanged.
    /// </summary>
    internal class NumberBox : NumericUpDown
    {
        private TextBox _editBox;
        private bool _editHooked;
        private bool _isEditing;
        private bool _internalTextChange;
        private bool _userModifiedText;
        private bool _validating;
        private bool _committing;
        private bool _hasEnterText;
        private string _enterText;

        public NumberBox()
        {
            ThousandsSeparator = true;
            HookEditBox();
            UpdateEditText();
        }

        /// <summary>TextBox con bên trong NumericUpDown (dùng nội bộ và kiểm thử gửi WM_CHAR/gán Text).</summary>
        internal TextBox InnerEditBox
        {
            get { return GetEditBox(); }
        }

        private TextBox GetEditBox()
        {
            if (_editBox != null)
                return _editBox;
            foreach (Control c in Controls)
            {
                var tb = c as TextBox;
                if (tb != null)
                {
                    _editBox = tb;
                    break;
                }
            }
            return _editBox;
        }

        private void HookEditBox()
        {
            if (_editHooked)
                return;
            TextBox edit = GetEditBox();
            if (edit == null)
                return;
            edit.GotFocus += OnEditBoxGotFocus;
            _editHooked = true;
        }

        private void OnEditBoxGotFocus(object sender, EventArgs e)
        {
            EnterEditMode(false);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            EnterEditMode(true);
        }

        protected override void OnLeave(EventArgs e)
        {
            LeaveEditMode();
            base.OnLeave(e);
        }

        protected override void OnTextBoxLostFocus(object source, EventArgs e)
        {
            if (!ContainsFocus)
                LeaveEditMode();
            base.OnTextBoxLostFocus(source, e);
        }

        private void EnterEditMode(bool selectAllIfKeyboard)
        {
            if (IsDisposed || Disposing || ReadOnly || _isEditing)
                return;
            _isEditing = true;
            if (!UserEdit && !_userModifiedText)
            {
                string editText = NumberInput.FormatForEdit(base.Value, DecimalPlaces);
                _enterText = editText;
                _hasEnterText = true;
                bool changed = SetTextInternal(editText);
                if (changed && selectAllIfKeyboard && MouseButtons == MouseButtons.None)
                    Select(0, editText.Length);
            }
        }

        private void LeaveEditMode()
        {
            if (IsDisposed || Disposing)
                return;
            bool wasEditing = _isEditing;
            _isEditing = false;
            if (UserEdit || _userModifiedText)
                ValidateEditText();
            else if (wasEditing)
                UpdateEditText();
            _hasEnterText = false;
            _enterText = null;
        }

        protected override void OnTextBoxKeyPress(object source, KeyPressEventArgs e)
        {
            // Ghi đè hoàn toàn bộ lọc theo CultureInfo.CurrentCulture của NumericUpDown, nhưng vẫn bắn sự kiện KeyPress.
            OnKeyPress(e);
            if (e.Handled || ReadOnly)
                return;

            char c = e.KeyChar;
            if (char.IsControl(c) || (ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
                return;
            if (c >= '0' && c <= '9')
                return;
            if (c == ',' || c == '-')
                return;
            if (c == '.')
            {
                e.KeyChar = ',';
                return;
            }
            e.Handled = true;
        }

        protected override void OnTextBoxKeyDown(object source, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && (UserEdit || _userModifiedText))
                ValidateEditText();
            base.OnTextBoxKeyDown(source, e);
        }

        protected override void OnTextBoxTextChanged(object source, EventArgs e)
        {
            if (_internalTextChange)
            {
                ChangingText = false;
                return;
            }
            if (ChangingText)
            {
                base.OnTextBoxTextChanged(source, e);
                return;
            }

            TextBox edit = GetEditBox();
            if (edit != null)
            {
                string raw = edit.Text;
                if (!string.IsNullOrEmpty(raw) && raw.IndexOf('.') >= 0)
                {
                    int selStart = edit.SelectionStart;
                    int selLen = edit.SelectionLength;
                    string replaced = NumberInput.NormalizeEditInput(raw);
                    _internalTextChange = true;
                    try
                    {
                        edit.Text = replaced;
                        int start = Math.Min(selStart, replaced.Length);
                        edit.SelectionStart = start;
                        edit.SelectionLength = Math.Min(selLen, replaced.Length - start);
                    }
                    finally
                    {
                        _internalTextChange = false;
                    }
                }
            }

            _userModifiedText = true;
            base.OnTextBoxTextChanged(source, e);
        }

        protected override void OnValueChanged(EventArgs e)
        {
            if (!_committing)
            {
                UserEdit = false;
                _userModifiedText = false;
                _hasEnterText = false;
                _enterText = null;
            }
            base.OnValueChanged(e);
        }

        public override void UpButton()
        {
            if (ReadOnly)
                return;
            if (UserEdit || _userModifiedText)
                ValidateEditText();
            base.UpButton();
        }

        public override void DownButton()
        {
            if (ReadOnly)
                return;
            if (UserEdit || _userModifiedText)
                ValidateEditText();
            base.DownButton();
        }

        protected override void ValidateEditText()
        {
            if (IsDisposed || Disposing || _validating)
                return;
            _validating = true;
            try
            {
                bool hadUserEdit = UserEdit || _userModifiedText;
                UserEdit = false;
                _userModifiedText = false;

                if (!hadUserEdit)
                    return;

                TextBox edit = GetEditBox();
                string raw = edit != null ? edit.Text : Text;
                if (_hasEnterText && string.Equals(raw, _enterText, StringComparison.Ordinal))
                {
                    UpdateEditText();
                    return;
                }

                decimal parsed;
                bool integerOnly = DecimalPlaces == 0;
                if (NumberInput.TryParseForBox(raw, DecimalPlaces, integerOnly, out parsed))
                {
                    decimal clamped = ClampToBounds(parsed);
                    if (clamped != base.Value)
                    {
                        _committing = true;
                        try
                        {
                            base.Value = clamped;
                        }
                        finally
                        {
                            _committing = false;
                        }
                    }
                }
                UpdateEditText();
            }
            finally
            {
                _validating = false;
            }
        }

        protected override void UpdateEditText()
        {
            if (IsDisposed || Disposing)
                return;
            if (UserEdit && !_validating)
            {
                ValidateEditText();
                return;
            }

            decimal current = base.Value;
            string formatted = _isEditing && !ReadOnly
                ? NumberInput.FormatForEdit(current, DecimalPlaces)
                : NumberInput.FormatForDisplay(current, DecimalPlaces, ThousandsSeparator);
            SetTextInternal(formatted);
            if (_isEditing && !ReadOnly)
            {
                _enterText = formatted;
                _hasEnterText = true;
            }
        }

        private decimal ClampToBounds(decimal value)
        {
            if (value < Minimum)
                return Minimum;
            if (value > Maximum)
                return Maximum;
            return value;
        }

        private bool SetTextInternal(string text)
        {
            TextBox edit = GetEditBox();
            if (edit == null)
                return false;
            if (string.Equals(edit.Text, text, StringComparison.Ordinal))
                return false;
            _internalTextChange = true;
            ChangingText = true;
            try
            {
                edit.Text = text;
            }
            finally
            {
                ChangingText = false;
                _internalTextChange = false;
            }
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserEdit = false;
                _userModifiedText = false;
                if (_editHooked && _editBox != null)
                {
                    _editBox.GotFocus -= OnEditBoxGotFocus;
                    _editHooked = false;
                }
            }
            base.Dispose(disposing);
        }
    }
}
