using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// TextBox tự vẽ chữ gợi ý (placeholder) bằng màu chọn được. Cue banner của Windows (EM_SETCUEBANNER) luôn xám
    /// hệ thống đậm, nhìn như chữ đã nhập và không theo theme; ở đây chữ gợi ý nhạt theo <see cref="PlaceholderColor"/>.
    /// Hiện khi ô trống (kể cả đang focus, giống cue banner wParam = 1) và biến mất ngay khi gõ.
    /// </summary>
    internal sealed class PlaceholderTextBox : TextBox
    {
        private const int WM_PAINT = 0x000F;
        private const int WM_PRINT = 0x0317;
        private const int WM_PRINTCLIENT = 0x0318;
        private string _placeholder = "";
        private Color _placeholderColor = SystemColors.GrayText;

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                Invalidate();
            }
        }

        public Color PlaceholderColor
        {
            get { return _placeholderColor; }
            set
            {
                _placeholderColor = value;
                Invalidate();
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Chỉ xét Text sau khi biết là thông điệp vẽ: đọc Text gửi WM_GETTEXT ngược lại vào WndProc này
            // (kiểm Text trước cho mọi thông điệp gây đệ quy vô hạn → tràn stack).
            bool paint = m.Msg == WM_PAINT;
            bool print = (m.Msg == WM_PRINT || m.Msg == WM_PRINTCLIENT) && m.WParam != IntPtr.Zero;
            if (!paint && !print)
                return;
            if (_placeholder.Length == 0 || ReadOnly || TextLength != 0)
                return;
            if (paint)
            {
                using (Graphics g = CreateGraphics())
                    DrawHint(g);
            }
            else if ((m.Msg == WM_PRINT || m.Msg == WM_PRINTCLIENT) && m.WParam != IntPtr.Zero)
            {
                // Chụp ảnh control (DrawToBitmap / --render-screen) đi qua WM_PRINT chứ không qua WM_PAINT.
                using (Graphics g = Graphics.FromHdc(m.WParam))
                    DrawHint(g);
            }
        }

        private void DrawHint(Graphics g)
        {
            var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            TextRenderer.DrawText(g, _placeholder, Font, rect, _placeholderColor, BackColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
