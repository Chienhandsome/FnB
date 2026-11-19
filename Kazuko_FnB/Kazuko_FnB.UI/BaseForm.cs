using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Form cha cho mọi form của app: font/icon/chế độ scale chung, tự đăng ký và hủy <see cref="ThemeManager.ThemeChanged"/>
    /// rồi gọi <see cref="ApplyFormTheme"/>. Lớp con chỉ ghi đè ApplyFormTheme để tô màu; thời điểm tô lần đầu do lớp con quyết định
    /// (dialog tô khi Load, form chính tô ở cuối constructor).
    /// </summary>
    internal class BaseForm : Form
    {
        public BaseForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 10f);
            if (BrandLogo.AppIcon != null)
                Icon = BrandLogo.AppIcon;
            ThemeManager.ThemeChanged += HandleThemeChanged;
        }

        /// <summary>Tô lại form theo theme hiện tại. Mặc định chỉ đặt màu nền/chữ; lớp con ghi đè để tô cây control.</summary>
        protected virtual void ApplyFormTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            BackColor = t.Background;
            ForeColor = t.Text;
        }

        private void HandleThemeChanged(object sender, EventArgs e)
        {
            ApplyFormTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= HandleThemeChanged;
            base.Dispose(disposing);
        }
    }
}
