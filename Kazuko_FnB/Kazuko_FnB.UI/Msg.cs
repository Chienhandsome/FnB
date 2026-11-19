using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>Hộp thông báo thống nhất (cùng kiểu hộp thoại Windows như app gốc: Cảnh báo, Xác nhận, Thông báo).</summary>
    internal static class Msg
    {
        public static void Warning(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Info(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Error(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>Hỏi Yes/No (mặc định No).</summary>
        public static bool Confirm(IWin32Window owner, string text)
        {
            return MessageBox.Show(owner, text, "Xác nhận", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
    }
}
