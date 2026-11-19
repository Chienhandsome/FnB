using System;

namespace Kazuko_FnB.Services
{
    public enum PasswordCheck
    {
        Ok,
        /// <summary>Mật khẩu cũ nhập sai.</summary>
        WrongOld,
        /// <summary>Mật khẩu mới và ô nhập lại không giống nhau.</summary>
        Mismatch,
        /// <summary>Mật khẩu mới trùng mật khẩu cũ (bị chặn).</summary>
        SameAsOld,
        /// <summary>Mật khẩu mới để trống: cho phép nhưng phải hỏi xác nhận (như app gốc).</summary>
        EmptyNeedsConfirm
    }

    /// <summary>
    /// Quy tắc đổi mật khẩu đăng nhập. Mật khẩu lưu chữ thường như app gốc; không giới hạn độ dài.
    /// Thứ tự kiểm: mật khẩu cũ → nhập lại → trùng mật khẩu cũ → để trống (hỏi xác nhận).
    /// </summary>
    public static class PasswordPolicy
    {
        public static PasswordCheck Check(string currentInDb, string oldInput, string newPassword, string confirm)
        {
            string cur = currentInDb ?? "", old = oldInput ?? "", nw = newPassword ?? "", cf = confirm ?? "";
            if (!string.Equals(cur, old, StringComparison.Ordinal))
                return PasswordCheck.WrongOld;
            if (!string.Equals(nw, cf, StringComparison.Ordinal))
                return PasswordCheck.Mismatch;
            if (string.Equals(nw, old, StringComparison.Ordinal))
                return PasswordCheck.SameAsOld;
            if (nw.Length == 0)
                return PasswordCheck.EmptyNeedsConfirm;
            return PasswordCheck.Ok;
        }

        public static string Message(PasswordCheck c)
        {
            switch (c)
            {
                case PasswordCheck.WrongOld: return "Mật khẩu cũ không đúng.";
                case PasswordCheck.Mismatch: return "Mật khẩu nhập lại không khớp với mật khẩu mới.";
                case PasswordCheck.SameAsOld: return "Mật khẩu mới trùng với mật khẩu cũ, vui lòng nhập mật khẩu khác.";
                case PasswordCheck.EmptyNeedsConfirm:
                    return "Mật khẩu mới của bạn để trống, chúng tôi khuyên bạn không nên dùng mật khẩu này" + Environment.NewLine +
                           "Bạn có muốn tiếp tục không?";
                default: return "";
            }
        }
    }
}
