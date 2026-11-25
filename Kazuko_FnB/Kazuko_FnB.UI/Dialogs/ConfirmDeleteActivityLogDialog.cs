using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "XÁC NHẬN XÓA LƯU VẾT" (docs/specs/luu-vet-hoat-dong.md §3.4, Q1):
    /// nút Chấp nhận tắt cho tới khi nhập đúng mật khẩu "9876" (so chuỗi thô).
    /// </summary>
    internal sealed class ConfirmDeleteActivityLogDialog : ThemedDialog
    {
        private readonly Label _lblPassword = new Label { AutoSize = false, Text = "Mật khẩu", TextAlign = ContentAlignment.MiddleLeft };
        private readonly InputBox _txtPassword = new InputBox { IsPassword = true };
        private readonly FlatButton _btnAccept;
        private readonly FlatButton _btnCancel;
        private readonly Font _bodyFont;

        public ConfirmDeleteActivityLogDialog()
            : base("XÁC NHẬN XÓA LƯU VẾT", "Xóa toàn bộ lưu vết hoạt động (không thể hoàn tác)", 360, 200)
        {
            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            _btnCancel = AddButton("Hủy bỏ", null, delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            _btnAccept = AddButton("Chấp nhận", "danger", delegate
            {
                if (!ActivityLogger.IsDeletePasswordValid(_txtPassword.Text))
                    return;
                DialogResult = DialogResult.OK;
                Close();
            });
            _btnAccept.Enabled = false;
            AcceptButton = _btnAccept;
            CancelButton = _btnCancel;

            FitBody(360, 76);

            _lblPassword.Font = _bodyFont;
            _lblPassword.SetBounds((int)(16 * s), (int)(22 * s), (int)(84 * s), (int)(32 * s));

            _txtPassword.Inner.Font = _bodyFont;
            _txtPassword.SetBounds((int)(104 * s), (int)(20 * s), (int)(240 * s), (int)(36 * s));
            _txtPassword.TextChanged += delegate
            {
                _btnAccept.Enabled = ActivityLogger.IsDeletePasswordValid(_txtPassword.Text);
            };

            Body.Controls.Add(_lblPassword);
            Body.Controls.Add(_txtPassword);

            Shown += delegate { _txtPassword.Focus(); };
        }

        public string EnteredPassword
        {
            get { return _txtPassword.Text; }
        }

        internal bool IsAcceptEnabled
        {
            get { return _btnAccept.Enabled; }
        }

        internal void SetPasswordForTest(string password)
        {
            _txtPassword.Text = password ?? "";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _bodyFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
