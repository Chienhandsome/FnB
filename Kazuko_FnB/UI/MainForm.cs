using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Khung chính sau đăng nhập. Hiện mới có appbar (tên app, người dùng + vai trò, Đăng xuất,
    /// thu nhỏ / phóng to / đóng). Menu, thanh công cụ và tab tài liệu làm ở G1.3.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        private readonly UserSession _session;
        private readonly AppBar _bar = new AppBar();
        private readonly Label _placeholder = new Label();

        /// <summary>true nếu người dùng bấm Đăng xuất (quay lại màn đăng nhập), false nếu đóng app.</summary>
        public bool LogoutRequested { get; private set; }

        public MainForm(UserSession session)
        {
            _session = session;

            Text = "KAZUKO TEA - " + session.DisplayName +
                   (session.CurrentStore != null ? " - " + session.CurrentStore.Name : "");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(960, 600);
            Size = new Size(1280, 760);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 10f);

            _bar.Text = "PHẦN MỀM QUẢN LÝ TRÀ SỮA KAZUKO TEA  v" + Application.ProductVersion;
            _bar.ShowUser = true;
            _bar.ShowMaximize = true;
            _bar.EnableDrag = true;
            _bar.SetUser(session.DisplayName, session.RoleText);
            _bar.LogoutClicked += delegate { LogoutRequested = true; Close(); };

            _placeholder.Text = "Đăng nhập thành công. Menu và các chức năng sẽ được bổ sung ở bước tiếp theo.";
            _placeholder.TextAlign = ContentAlignment.MiddleCenter;
            _placeholder.Font = new Font("Segoe UI", 12f);

            Controls.Add(_placeholder);
            Controls.Add(_bar);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
            WindowState = FormWindowState.Maximized;
        }

        private float S
        {
            get { return DeviceDpi / 96f; }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            BackColor = t.Background;
            _bar.BarColor = t.Primary;
            _bar.BarTextColor = t.OnPrimary;
            // Nút Đăng xuất: màu cảnh báo, chữ chọn theo độ sáng để đọc được trên cả nền sáng lẫn tối.
            _bar.LogoutColor = t.IsDark ? t.Danger : Color.FromArgb(196, 43, 28);
            _bar.LogoutTextColor = t.IsDark ? Color.FromArgb(40, 12, 8) : Color.White;
            _bar.Invalidate();
            _placeholder.BackColor = t.Background;
            _placeholder.ForeColor = t.TextMuted;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int barH = (int)(48 * S);
            _bar.SetBounds(0, 0, ClientSize.Width, barH);
            _placeholder.SetBounds(0, barH, ClientSize.Width, Math.Max(0, ClientSize.Height - barH));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _bar.Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateMaximizedBounds();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (WindowState == FormWindowState.Normal)
                UpdateMaximizedBounds();
        }

        /// <summary>Phóng to đúng vùng làm việc để không che thanh tác vụ Windows.</summary>
        private void UpdateMaximizedBounds()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MaximizedBounds = new Rectangle(area.X, area.Y, area.Width, area.Height);
        }

        // Cửa sổ không viền nên tự trả mã hit-test ở 4 cạnh/góc để kéo đổi kích thước.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                int x = unchecked((short)((long)m.LParam & 0xFFFF));
                int y = unchecked((short)(((long)m.LParam >> 16) & 0xFFFF));
                Point p = PointToClient(new Point(x, y));
                int grip = (int)(6 * S);
                bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
                bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
                int hit = top && left ? HTTOPLEFT
                    : top && right ? HTTOPRIGHT
                    : bottom && left ? HTBOTTOMLEFT
                    : bottom && right ? HTBOTTOMRIGHT
                    : left ? HTLEFT
                    : right ? HTRIGHT
                    : top ? HTTOP
                    : bottom ? HTBOTTOM
                    : 0;
                if (hit != 0)
                {
                    m.Result = (IntPtr)hit;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
