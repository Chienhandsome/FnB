using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Thanh tiêu đề tự vẽ cho cửa sổ không viền.
    /// Màn đăng nhập: tên app + thu nhỏ / đóng.
    /// Khung chính (ShowUser = true): thêm chip người dùng (avatar, tên, vai trò), nút Đăng xuất,
    /// nút phóng to; kéo thanh để di chuyển cửa sổ, nhấp đúp để phóng to / thu lại.
    /// </summary>
    internal class AppBar : Control
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 2;

        private const int HotNone = -1;
        private const int HotMin = 0;
        private const int HotMax = 1;
        private const int HotClose = 2;
        private const int HotLogout = 3;

        private Rectangle _minRect, _maxRect, _closeRect, _logoutRect, _chipRect, _avatarRect, _roleRect, _nameRect;
        private int _hot = HotNone;
        private bool _showMaximize;
        private bool _showUser;
        private string _userName = "";
        private string _userRole = "";
        private Font _boldFont;
        private Font _smallFont;
        private Font _iconFont;

        public event EventHandler LogoutClicked;

        public Color BarColor { get; set; }
        public Color BarTextColor { get; set; }
        public Color LogoutColor { get; set; }
        public Color LogoutTextColor { get; set; }
        /// <summary>Cho phép kéo thanh để di chuyển cửa sổ và nhấp đúp để phóng to.</summary>
        public bool EnableDrag { get; set; }

        public AppBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _boldFont = new Font("Segoe UI Semibold", 10f);
            _smallFont = new Font("Segoe UI", 8.5f);
            _iconFont = new Font(Draw.IconFont, 9f);
            BarColor = Color.OrangeRed;
            BarTextColor = Color.White;
            LogoutColor = Color.FromArgb(196, 43, 28);
            LogoutTextColor = Color.White;
        }

        public bool ShowMaximize
        {
            get { return _showMaximize; }
            set
            {
                _showMaximize = value;
                ComputeLayout();
                Invalidate();
            }
        }

        public bool ShowUser
        {
            get { return _showUser; }
            set
            {
                _showUser = value;
                ComputeLayout();
                Invalidate();
            }
        }

        public void SetUser(string name, string role)
        {
            _userName = name ?? "";
            _userRole = role ?? "";
            ComputeLayout();
            Invalidate();
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeLayout();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ComputeLayout();
        }

        private void ComputeLayout()
        {
            if (Width <= 0 || Height <= 0)
                return;
            float s = DpiScale;
            int btnW = (int)(46 * s);
            int x = Width;

            x -= btnW;
            _closeRect = new Rectangle(x, 0, btnW, Height);
            if (_showMaximize)
            {
                x -= btnW;
                _maxRect = new Rectangle(x, 0, btnW, Height);
            }
            else
            {
                _maxRect = Rectangle.Empty;
            }
            x -= btnW;
            _minRect = new Rectangle(x, 0, btnW, Height);

            _logoutRect = _chipRect = _avatarRect = _roleRect = _nameRect = Rectangle.Empty;
            if (!_showUser)
                return;

            int gap = (int)(12 * s);

            // Nút Đăng xuất
            Size t = TextRenderer.MeasureText("Đăng xuất", _boldFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int lw = (int)(34 * s) + t.Width + (int)(14 * s);
            int lh = (int)(30 * s);
            x -= gap;
            x -= lw;
            _logoutRect = new Rectangle(x, (Height - lh) / 2, lw, lh);

            // Chip người dùng: avatar + tên + huy hiệu vai trò
            Size nameSize = TextRenderer.MeasureText(_userName, _boldFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            Size roleSize = TextRenderer.MeasureText(_userRole, _smallFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int ch = (int)(36 * s);
            int av = (int)(28 * s);
            int pad = (int)(5 * s);
            int roleW = string.IsNullOrEmpty(_userRole) ? 0 : roleSize.Width + (int)(16 * s);
            int cw = pad + av + (int)(8 * s) + nameSize.Width + (roleW > 0 ? (int)(8 * s) + roleW : 0) + (int)(10 * s);
            x -= gap;
            x -= cw;
            _chipRect = new Rectangle(x, (Height - ch) / 2, cw, ch);
            _avatarRect = new Rectangle(_chipRect.X + pad, _chipRect.Y + (ch - av) / 2, av, av);
            _nameRect = new Rectangle(_avatarRect.Right + (int)(8 * s), _chipRect.Y, nameSize.Width, ch);
            if (roleW > 0)
                _roleRect = new Rectangle(_nameRect.Right + (int)(8 * s), _chipRect.Y + (ch - (int)(20 * s)) / 2, roleW, (int)(20 * s));
        }

        private int Hit(Point p)
        {
            if (_closeRect.Contains(p)) return HotClose;
            if (!_maxRect.IsEmpty && _maxRect.Contains(p)) return HotMax;
            if (_minRect.Contains(p)) return HotMin;
            if (!_logoutRect.IsEmpty && _logoutRect.Contains(p)) return HotLogout;
            return HotNone;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = Hit(e.Location);
            if (hot != _hot)
            {
                _hot = hot;
                Cursor = hot != HotNone ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hot = HotNone;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !EnableDrag || Hit(e.Location) != HotNone)
                return;
            Form form = FindForm();
            if (form == null || e.Clicks > 1)
                return;
            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (EnableDrag && _showMaximize && e.Button == MouseButtons.Left && Hit(e.Location) == HotNone)
                ToggleMaximize();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Form form = FindForm();
            if (form == null || e.Button != MouseButtons.Left)
                return;
            switch (Hit(e.Location))
            {
                case HotClose:
                    form.Close();
                    break;
                case HotMin:
                    form.WindowState = FormWindowState.Minimized;
                    break;
                case HotMax:
                    ToggleMaximize();
                    break;
                case HotLogout:
                    EventHandler h = LogoutClicked;
                    if (h != null)
                        h(this, EventArgs.Empty);
                    break;
            }
        }

        private void ToggleMaximize()
        {
            Form form = FindForm();
            if (form == null)
                return;
            form.WindowState = form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BarColor);

            // Tên ứng dụng bên trái, nhường chỗ cho vùng bên phải.
            int right = _showUser && !_chipRect.IsEmpty ? _chipRect.Left : _minRect.Left;
            int left = (int)(16 * s);
            Image logo = Kazuko_FnB.UI.BrandLogo.MarkWhite;
            if (logo != null)
            {
                // Logo K trắng bên trái tên ứng dụng.
                int lh = (int)(28 * s);
                int lw = (int)(lh * logo.Width / (float)logo.Height);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(logo, new Rectangle(left, (Height - lh) / 2, lw, lh));
                left += lw + (int)(12 * s);
            }
            var titleRect = new Rectangle(left, 0, Math.Max(0, right - left - (int)(12 * s)), Height);
            TextRenderer.DrawText(g, Text, Font, titleRect, BarTextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (_showUser)
                PaintUser(g, s);

            // Nút cửa sổ
            if (_hot == HotMin)
                using (var b = new SolidBrush(Draw.Lighten(BarColor, 0.18f)))
                    g.FillRectangle(b, _minRect);
            if (_hot == HotMax && !_maxRect.IsEmpty)
                using (var b = new SolidBrush(Draw.Lighten(BarColor, 0.18f)))
                    g.FillRectangle(b, _maxRect);
            if (_hot == HotClose)
                using (var b = new SolidBrush(Color.FromArgb(196, 43, 28)))
                    g.FillRectangle(b, _closeRect);

            const TextFormatFlags c = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, "", _iconFont, _minRect, BarTextColor, c);
            if (!_maxRect.IsEmpty)
            {
                Form form = FindForm();
                bool maximized = form != null && form.WindowState == FormWindowState.Maximized;
                TextRenderer.DrawText(g, maximized ? "" : "", _iconFont, _maxRect, BarTextColor, c);
            }
            TextRenderer.DrawText(g, "", _iconFont, _closeRect, _hot == HotClose ? Color.White : BarTextColor, c);
        }

        private void PaintUser(Graphics g, float s)
        {
            if (!_chipRect.IsEmpty)
            {
                using (GraphicsPath chip = Draw.Rounded(_chipRect, _chipRect.Height / 2f))
                {
                    using (var b = new SolidBrush(Color.FromArgb(46, BarTextColor)))
                        g.FillPath(b, chip);
                    using (var pen = new Pen(Color.FromArgb(110, BarTextColor), 1.4f * s))
                        g.DrawPath(pen, chip);
                }

                // Avatar tròn: chữ cái đầu của tên
                using (var b = new SolidBrush(BarTextColor))
                    g.FillEllipse(b, _avatarRect);
                string initial = _userName.Length > 0 ? _userName.Substring(0, 1).ToUpperInvariant() : "?";
                TextRenderer.DrawText(g, initial, _boldFont, _avatarRect, BarColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(g, _userName, _boldFont, _nameRect, BarTextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

                if (!_roleRect.IsEmpty)
                {
                    using (GraphicsPath role = Draw.Rounded(_roleRect, _roleRect.Height / 2f))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(70, BarTextColor)))
                            g.FillPath(b, role);
                    }
                    TextRenderer.DrawText(g, _userRole, _smallFont, _roleRect, BarTextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            if (!_logoutRect.IsEmpty)
            {
                Color fill = _hot == HotLogout ? Draw.Darken(LogoutColor, 0.15f) : LogoutColor;
                using (GraphicsPath btn = Draw.Rounded(_logoutRect, 8 * s))
                {
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, btn);
                    using (var pen = new Pen(Draw.Darken(fill, 0.3f), 1.4f * s))
                        g.DrawPath(pen, btn);
                }
                var iconRect = new Rectangle(_logoutRect.X + (int)(8 * s), _logoutRect.Y, (int)(22 * s), _logoutRect.Height);
                TextRenderer.DrawText(g, "", _iconFont, iconRect, LogoutTextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                var textRect = new Rectangle(iconRect.Right + (int)(2 * s), _logoutRect.Y,
                    _logoutRect.Right - iconRect.Right - (int)(10 * s), _logoutRect.Height);
                TextRenderer.DrawText(g, "Đăng xuất", _boldFont, textRect, LogoutTextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _boldFont.Dispose();
                _smallFont.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
