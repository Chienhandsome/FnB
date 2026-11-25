using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Khung chính sau đăng nhập: appbar → menu → thanh công cụ → tab tài liệu → nội dung → thanh trạng thái.
    /// Mở chức năng = mở một tab; các chức năng chưa làm hiện nội dung tạm.
    /// </summary>
    internal sealed class MainForm : BaseForm
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        private const string HomeKey = "home";

        private readonly UserSession _session;
        private readonly MenuModel _model;
        private readonly AppBar _bar = new AppBar();
        private readonly ThemedMenuStrip _menu = new ThemedMenuStrip();
        private readonly IconToolbar _toolbar = new IconToolbar();
        private readonly DocumentTabs _tabs = new DocumentTabs();
        private readonly Panel _content = new Panel();
        private readonly AppStatusBar _status = new AppStatusBar();
        private readonly Timer _clock = new Timer();
        private readonly Dictionary<string, Control> _views = new Dictionary<string, Control>();
        private readonly DateTime _installDate = InstallDate.GetOrCreate();
        private QuickAccessView _home;
        private bool _closingConfirmed;

        /// <summary>true nếu người dùng chọn Đăng xuất (quay lại màn đăng nhập), false nếu thoát app.</summary>
        public bool LogoutRequested { get; private set; }

        /// <summary>Chỉ dùng cho lệnh --render-main: bỏ hộp thoại xác nhận khi đóng.</summary>
        internal bool SkipConfirm { get; set; }

        public MainForm(UserSession session)
        {
            _session = session;
            AppSession.Current = session;
            _model = MainMenuBuilder.Build(session);

            Text = "KAZUKO FNB - " + session.DisplayName +
                   (session.CurrentStore != null ? " - " + session.CurrentStore.Name : "");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(960, 600);
            Size = new Size(1280, 760);
            DoubleBuffered = true;
            // Font, icon và đăng ký ThemeChanged do BaseForm lo.

            _bar.Text = "PHẦN MỀM QUẢN LÝ KAZUKO FNB  v" + Application.ProductVersion;
            _bar.ShowUser = true;
            _bar.ShowMaximize = true;
            _bar.EnableDrag = true;
            _bar.SetUser(session.DisplayName, session.RoleText);
            _bar.LogoutClicked += delegate { DoLogout(); };

            BuildMenu();
            _toolbar.SetItems(_model.Toolbar);
            _toolbar.ItemClicked += delegate (object s, MenuNode n) { HandleNode(n); };

            _tabs.SelectedChanged += delegate { ShowSelectedView(); };
            _tabs.TabCloseRequested += delegate (object s, string key) { CloseTab(key); };
            _tabs.CloseAllRequested += delegate { CloseAllTabs(); };
            _tabs.PinToggleRequested += delegate { TogglePin(); };

            _home = new QuickAccessView(_model.QuickGroups);
            _home.ItemClicked += delegate (object s, MenuNode n) { HandleNode(n); };
            _home.Dock = DockStyle.Fill;
            _views[HomeKey] = _home;
            _content.Controls.Add(_home);
            _tabs.Add(HomeKey, "Truy cập nhanh", true);
            _tabs.Select(HomeKey);

            Controls.Add(_content);
            Controls.Add(_status);
            Controls.Add(_tabs);
            Controls.Add(_toolbar);
            Controls.Add(_menu);
            Controls.Add(_bar);
            MainMenuStrip = _menu;

            _clock.Interval = 1000;
            _clock.Tick += delegate { UpdateStatus(); };
            _clock.Start();

            ApplyFormTheme();
            UpdateStatus();
            WindowState = FormWindowState.Maximized;
        }

        private float S
        {
            get { return DeviceDpi / 96f; }
        }

        /// <summary>Dùng cho --render-main: mở vài chức năng đầu trên thanh công cụ thành tab.</summary>
        internal void PreviewOpenTabs(int count)
        {
            int opened = 0;
            foreach (MenuNode n in _model.Toolbar)
            {
                if (n.IsSeparator)
                    continue;
                OpenTab(n);
                if (++opened >= count)
                    break;
            }
        }

        /// <summary>Dùng cho --render-main: mở menu thả xuống thứ index rồi chụp ra ảnh.</summary>
        internal Bitmap PreviewDropDown(int index)
        {
            var top = _menu.Items[index] as ToolStripMenuItem;
            if (top == null)
                return null;
            top.ShowDropDown();
            Application.DoEvents();
            ToolStripDropDown dd = top.DropDown;
            var bmp = new Bitmap(Math.Max(1, dd.Width), Math.Max(1, dd.Height));
            dd.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            top.HideDropDown();
            return bmp;
        }

        // ------------------------------------------------------------------ menu

        private void BuildMenu()
        {
            var usedShortcuts = new HashSet<Keys>();
            foreach (MenuNode root in _model.Roots)
            {
                var top = new ToolStripMenuItem(root.Title) { Tag = root };
                AddChildren(top, root, usedShortcuts);
                _menu.Items.Add(top);
            }
            _menu.StyleItems(S);
        }

        private void AddChildren(ToolStripMenuItem parent, MenuNode node, HashSet<Keys> usedShortcuts)
        {
            foreach (MenuNode c in node.Children)
            {
                if (c.IsSeparator)
                {
                    parent.DropDownItems.Add(new ToolStripSeparator());
                    continue;
                }
                var item = new ToolStripMenuItem(c.Title) { Tag = c };
                if (c.IsLeaf)
                {
                    MenuNode captured = c;
                    item.Click += delegate { HandleNode(captured); };
                    // DB có vài mục trùng phím tắt: chỉ gán cho mục đầu tiên.
                    Keys shortcut = (Keys)c.Shortcut;
                    if (shortcut != Keys.None && usedShortcuts.Add(shortcut))
                    {
                        try
                        {
                            item.ShortcutKeys = shortcut;
                            item.ShowShortcutKeys = true;
                        }
                        catch (InvalidEnumArgumentException)
                        {
                            // Mã phím tắt không hợp lệ: bỏ qua phím tắt, giữ mục menu.
                        }
                    }
                }
                else
                {
                    AddChildren(item, c, usedShortcuts);
                }
                parent.DropDownItems.Add(item);
            }
        }

        // ------------------------------------------------------------------ hành động

        private void HandleNode(MenuNode node)
        {
            switch (node.Action)
            {
                case MenuAction.Logout:
                    DoLogout();
                    break;
                case MenuAction.Exit:
                    DoExit();
                    break;
                default:
                    // Chức năng đã có màn thật (hộp thoại) thì mở; còn lại mở tab tạm.
                    if (!Dialogs.ScreenFactory.TryOpen(this, node, _session))
                        OpenTab(node);
                    break;
            }
        }

        private void OpenTab(MenuNode node)
        {
            string key = node.Id;
            if (_tabs.Find(key) == null)
            {
                // Mục có màn thật (danh mục dựa vào bảng nhập liệu) thì dựng màn đó; không thì tab tạm.
                Control view = Dialogs.ScreenFactory.CreateView(node, _session) ?? new PlaceholderView(node.Title);
                view.Dock = DockStyle.Fill;
                view.Visible = false;
                _views[key] = view;
                _content.Controls.Add(view);
                _tabs.Add(key, node.Title, false);
            }
            _tabs.Select(key);
        }

        private void CloseTab(string key)
        {
            DocTab t = _tabs.Find(key);
            if (t == null || t.Fixed)
                return;
            Control view;
            if (_views.TryGetValue(key, out view))
            {
                _views.Remove(key);
                _content.Controls.Remove(view);
                view.Dispose();
            }
            _tabs.Remove(key);
        }

        private void CloseAllTabs()
        {
            var toClose = new List<string>();
            foreach (DocTab t in _tabs.Tabs)
                if (!t.Fixed && !t.Pinned)
                    toClose.Add(t.Key);
            foreach (string k in toClose)
                CloseTab(k);
        }

        private void TogglePin()
        {
            DocTab t = _tabs.SelectedTab;
            if (t != null && !t.Fixed)
                _tabs.SetPinned(t.Key, !t.Pinned);
        }

        private void ShowSelectedView()
        {
            string key = _tabs.SelectedKey;
            foreach (KeyValuePair<string, Control> kv in _views)
                kv.Value.Visible = kv.Key == key;
            Control shown;
            if (key != null && _views.TryGetValue(key, out shown))
                shown.BringToFront();
            UpdateStatus();
        }

        private void DoLogout()
        {
            if (!Confirm("Bạn có muốn đăng xuất khỏi hệ thống không?", "Đăng xuất"))
                return;
            LogoutRequested = true;
            _closingConfirmed = true;
            Close();
        }

        private void DoExit()
        {
            if (!Confirm("Bạn có muốn thoát khỏi hệ thống không?", "Thoát"))
                return;
            _closingConfirmed = true;
            Close();
        }

        private bool Confirm(string message, string caption)
        {
            if (SkipConfirm)
                return true;
            return MessageBox.Show(this, message, caption, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Đóng bằng nút ✕ hoặc Alt+F4 cũng hỏi xác nhận, giống app gốc.
            if (!_closingConfirmed && e.CloseReason == CloseReason.UserClosing)
            {
                if (!Confirm("Bạn có muốn thoát khỏi hệ thống không?", "Thoát"))
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        private void UpdateStatus()
        {
            // Trái: màn đang xem + giờ; giữa: bản quyền (mới có hiển thị hạn dùng thử, chưa chặn); phải: CSDL.
            DocTab t = _tabs.SelectedTab;
            _status.LeftText = (t == null ? "" : "Đang xem: " + t.Title + "    ") +
                               DateTime.Now.ToString("HH:mm:ss - dd/MM/yyyy", CultureInfo.InvariantCulture);
            _status.MiddleText = "Đăng ký bản quyền: Dùng thử, còn " +
                                 TrialRules.DaysLeft(_installDate, DateTime.Today) + " ngày";
            _status.RightText = "CSDL: " + (_session.DatabasePath ?? "");
        }

        // ------------------------------------------------------------------ theme

        protected override void ApplyFormTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            Color strong = Draw.StrongBorder(t);
            BackColor = t.Background;

            _bar.BarColor = t.Primary;
            _bar.BarTextColor = t.OnPrimary;
            // Nút Đăng xuất: màu cảnh báo, chữ chọn để đọc được trên cả nền sáng lẫn tối.
            _bar.LogoutColor = Color.FromArgb(196, 43, 28);
            _bar.LogoutTextColor = Color.White;
            _bar.Invalidate();

            _menu.ApplyTheme(t);

            // Nền thanh/trang giữ màu nền theme (kem); chỉ nút toolbar, tab đang chọn, thân thẻ là trắng.
            Color pane = t.Background;
            Color white = Color.White;
            _toolbar.BarColor = pane;
            _toolbar.ButtonColor = white;
            _toolbar.ButtonBorderColor = strong;
            _toolbar.HoverColor = Draw.Mix(t.Surface, t.Primary, 0.14f);
            _toolbar.TextColor = t.Text;
            _toolbar.IconColor = t.Primary;
            _toolbar.SeparatorColor = Draw.Mix(t.Accent, t.Background, 0.2f);
            _toolbar.BottomBorderColor = strong;
            _toolbar.Invalidate();

            _tabs.BarColor = pane;
            _tabs.TabColor = Draw.Mix(pane, t.Primary, 0.22f);
            _tabs.SelectedTabColor = white;
            _tabs.BorderColor = strong;
            _tabs.AccentColor = t.Primary;
            _tabs.TextColor = t.Text;
            _tabs.MutedTextColor = t.TextMuted;
            _tabs.DangerColor = t.Danger;
            _tabs.Invalidate();

            _content.BackColor = pane;
            foreach (Control v in _views.Values)
            {
                var q = v as QuickAccessView;
                if (q != null)
                    q.ApplyTheme(t, pane);
                var p = v as PlaceholderView;
                if (p != null)
                    p.ApplyTheme(t, pane);
                var cv = v as CatalogView;
                if (cv != null)
                    cv.ApplyTheme(t, pane);
            }

            _status.BarColor = t.Background;
            _status.BorderColor = strong;
            _status.TextColor = t.Text;
            _status.MutedColor = t.TextMuted;
            _status.Invalidate();
        }

        // ------------------------------------------------------------------ bố cục và cửa sổ không viền

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            float s = S;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w <= 0 || h <= 0)
                return;
            int y = 0;
            _bar.SetBounds(0, y, w, (int)(48 * s)); y += _bar.Height;
            _menu.SetBounds(0, y, w, (int)(38 * s)); y += _menu.Height;
            _toolbar.SetBounds(0, y, w, _toolbar.PreferredBarHeight); y += _toolbar.Height;
            _tabs.SetBounds(0, y, w, (int)(44 * s)); y += _tabs.Height;
            int statusH = (int)(28 * s);
            _status.SetBounds(0, h - statusH, w, statusH);
            _content.SetBounds(0, y, w, Math.Max(0, h - statusH - y));
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
            {
                _clock.Stop();
                _clock.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
