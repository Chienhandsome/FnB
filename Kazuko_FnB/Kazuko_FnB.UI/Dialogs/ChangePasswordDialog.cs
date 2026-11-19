using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Hệ thống ▸ Đổi mật khẩu đăng nhập cho tài khoản đang đăng nhập.</summary>
    internal sealed class ChangePasswordDialog : ThemedDialog
    {
        private readonly UserSession _session;
        private readonly UserAdminRepository _repo;
        private readonly TextBox _old = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox _new = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox _again = new TextBox { UseSystemPasswordChar = true };

        public ChangePasswordDialog(UserSession session)
            : base("Đổi mật khẩu", "Đổi mật khẩu cho tài khoản: " + session.UserName, 520, 340)
        {
            _session = session;
            _repo = new UserAdminRepository(session.DatabasePath);
            float s = Dpi;

            CancelButton = AddButton("Thoát", null, delegate { Close(); });
            AcceptButton = AddButton("Ghi dữ liệu", "primary", delegate { DoSave(); });

            var help = new Label
            {
                Text = "Để thay đổi mật khẩu, mời bạn nhập mật khẩu cũ, điền mật khẩu mới và nhắc lại mật khẩu mới một lần nữa",
                AutoSize = false,
                Left = 0,
                Top = (int)(4 * s),
                Width = ClientSize.Width - (int)(40 * s),
                Height = (int)(48 * s),
                Tag = "muted"
            };
            Body.Controls.Add(help);
            int x = (int)(150 * s), w = ClientSize.Width - x - (int)(40 * s), y = (int)(64 * s), rowH = (int)(40 * s);
            Add("Mật khẩu cũ:", _old, x, ref y, rowH, w, s);
            Add("Mật khẩu mới:", _new, x, ref y, rowH, w, s);
            Add("Nhập lại:", _again, x, ref y, rowH, w, s);
            Shown += delegate { _old.Focus(); };
        }

        private void Add(string label, TextBox box, int x, ref int y, int rowH, int w, float s)
        {
            Body.Controls.Add(new Label { Text = label, AutoSize = false, Left = 0, Top = y, Width = x - (int)(10 * s), Height = rowH, TextAlign = ContentAlignment.MiddleLeft });
            box.SetBounds(x, y + (int)(6 * s), w, (int)(28 * s));
            Body.Controls.Add(box);
            y += rowH;
        }

        private void DoSave()
        {
            try
            {
                PasswordCheck c = PasswordPolicy.Check(_repo.GetPassword(_session.UserId), _old.Text, _new.Text, _again.Text);
                switch (c)
                {
                    case PasswordCheck.WrongOld:
                        Msg.Warning(this, PasswordPolicy.Message(c));
                        _old.Focus();
                        _old.SelectAll();
                        return;
                    case PasswordCheck.Mismatch:
                        Msg.Warning(this, PasswordPolicy.Message(c));
                        _again.Focus();
                        _again.SelectAll();
                        return;
                    case PasswordCheck.SameAsOld:
                        Msg.Warning(this, PasswordPolicy.Message(c));
                        _new.Focus();
                        _new.SelectAll();
                        return;
                    case PasswordCheck.EmptyNeedsConfirm:
                        if (!Msg.Confirm(this, PasswordPolicy.Message(c)))
                            return;
                        break;
                }
                _repo.SetPassword(_session.UserId, _new.Text, AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }

            // Mật khẩu ghi nhớ trên máy này (nếu có) phải theo mật khẩu mới, nếu không lần mở sau tự đăng nhập sẽ thất bại.
            try
            {
                AppSettings st = AppSettings.Load();
                if (st.RememberLogin && string.Equals(st.UserName, _session.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    st.SetPassword(_new.Text);
                    st.Save();
                }
            }
            catch (Exception)
            {
                // Không lưu được cài đặt cục bộ không ảnh hưởng việc đã đổi mật khẩu.
            }
            Msg.Info(this, "Đã đổi mật khẩu thành công.");
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
