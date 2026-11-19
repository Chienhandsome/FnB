using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn đăng nhập toàn màn hình: appbar tự vẽ, ảnh nền (tạm: gradient theo theme),
    /// khung bên phải gồm 2 tab Đăng nhập / Cơ sở dữ liệu (chế độ máy này).
    /// </summary>
    internal sealed class LoginForm : Form
    {
        private readonly AppSettings _settings;
        private readonly LoginService _login = new LoginService();
        private readonly DatabaseService _db = new DatabaseService();
        private Image _background;
        private Bitmap _bgCache;
        private Rectangle _card;

        private readonly AppBar _bar = new AppBar();
        private readonly Label _title = new Label();
        private readonly SegmentedTabs _tabs = new SegmentedTabs();
        private readonly Label _version = new Label();
        private readonly MarqueeBar _marquee = new MarqueeBar();

        private readonly Panel _pageLogin = new Panel();
        private readonly Label _lblUser = new Label();
        private readonly InputBox _user = new InputBox();
        private readonly Label _lblPass = new Label();
        private readonly InputBox _pass = new InputBox();
        private readonly CheckBox _remember = new CheckBox();
        private readonly CheckBox _auto = new CheckBox();
        private readonly Label _error = new Label();
        private readonly FlatButton _btnLogin = new FlatButton();

        private readonly Panel _pageDb = new Panel();
        private readonly Label _lblConn = new Label();
        private readonly RadioButton _rbLocal = new RadioButton();
        private readonly RadioButton _rbServer = new RadioButton();
        private readonly Label _lblHost = new Label();
        private readonly InputBox _host = new InputBox();
        private readonly Label _lblPort = new Label();
        private readonly InputBox _port = new InputBox();
        private readonly InputBox _dbPath = new InputBox();
        private readonly Label _lblFbUser = new Label();
        private readonly InputBox _fbUser = new InputBox();
        private readonly Label _lblFbPass = new Label();
        private readonly InputBox _fbPass = new InputBox();
        private readonly FlatButton _btnCreate = new FlatButton();
        private readonly FlatButton _btnPick = new FlatButton();
        private readonly FlatButton _btnConnect = new FlatButton();
        private readonly Label _dbStatus = new Label();

        private enum StatusKind
        {
            Info,
            Error,
            Success
        }

        public UserSession Session { get; private set; }

        public LoginForm(AppSettings settings, string initialError)
        {
            _settings = settings;

            Text = "KAZUKO FNB - Đăng nhập";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 10f);
            if (Kazuko_FnB.Services.BrandLogo.AppIcon != null)
                Icon = Kazuko_FnB.Services.BrandLogo.AppIcon;

            BuildControls();
            LoadBackground();

            _user.Text = settings.UserName ?? "";
            _remember.Checked = settings.RememberLogin;
            _auto.Checked = settings.RememberLogin && settings.AutoLogin;
            if (settings.RememberLogin)
                _pass.Text = settings.GetPassword() ?? "";
            _dbPath.Text = settings.DatabasePath ?? "";
            _fbUser.Text = string.IsNullOrWhiteSpace(settings.FbUser) ? AppSettings.DefaultFbUser : settings.FbUser;
            _fbPass.Text = settings.GetFbPassword();
            _host.Text = settings.ServerHost ?? "";
            _port.Text = string.IsNullOrWhiteSpace(settings.ServerPort) ? AppSettings.DefaultServerPort : settings.ServerPort;
            _rbServer.Checked = settings.UseServer;
            _rbLocal.Checked = !settings.UseServer;
            OnConnectionModeChanged();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
            AcceptButton = _btnLogin;

            if (!string.IsNullOrEmpty(initialError))
                ShowError(initialError);
            if (string.IsNullOrWhiteSpace(settings.DatabasePath) || !File.Exists(settings.DatabasePath))
            {
                _tabs.SelectedIndex = 1;
                SetDbStatus("Chưa có tệp dữ liệu. Chọn dữ liệu có sẵn hoặc tạo dữ liệu mới.", StatusKind.Error);
            }
        }

        private float S
        {
            get { return DeviceDpi / 96f; }
        }

        /// <summary>Dùng cho lệnh --render-login: điền đường dẫn rồi bấm KẾT NỐI.</summary>
        internal void PreviewConnect(string path)
        {
            SelectTab(1);
            _dbPath.Text = path;
            DoConnect();
        }

        /// <summary>Dùng cho lệnh --render-login: chọn chế độ "Máy chủ Firebird".</summary>
        internal void PreviewServerMode()
        {
            SelectTab(1);
            _rbServer.Checked = true;
        }

        /// <summary>Chọn tab (0 = Đăng nhập, 1 = Cơ sở dữ liệu).</summary>
        internal void SelectTab(int index)
        {
            _tabs.SelectedIndex = index;
            OnTabChanged();
        }

        // ------------------------------------------------------------------ dựng control

        private void BuildControls()
        {
            _bar.Text = "KAZUKO FNB";
            _title.Text = "Đăng nhập";
            _title.Font = new Font("Segoe UI Semibold", 20f);
            _title.AutoSize = false;

            _tabs.Items = new[] { "Đăng nhập", "Cơ sở dữ liệu" };
            _tabs.SelectedIndexChanged += delegate { OnTabChanged(); };

            _version.Text = "KAZUKO FNB v" + Application.ProductVersion;
            _version.Font = new Font("Segoe UI", 8.5f);
            _version.TextAlign = ContentAlignment.MiddleRight;

            // Trang đăng nhập
            SetupLabel(_lblUser, "Tên tài khoản");
            _user.IconGlyph = "";
            _user.IconImage = Kazuko_FnB.Services.IconCatalog.Named("businessman");
            _user.Placeholder = "Nhập tên tài khoản";
            SetupLabel(_lblPass, "Mật khẩu");
            _pass.IconGlyph = "";
            _pass.IconImage = Kazuko_FnB.Services.IconCatalog.Named("key");
            _pass.Placeholder = "Nhập mật khẩu";
            _pass.IsPassword = true;

            _remember.Text = "Ghi nhớ đăng nhập";
            _auto.Text = "Tự động đăng nhập";
            _remember.AutoSize = _auto.AutoSize = true;
            _remember.CheckedChanged += delegate { if (!_remember.Checked) _auto.Checked = false; };
            _auto.CheckedChanged += delegate { if (_auto.Checked) _remember.Checked = true; };

            _error.AutoSize = false;
            _error.TextAlign = ContentAlignment.MiddleLeft;

            _btnLogin.Text = "ĐĂNG NHẬP";
            _btnLogin.Font = new Font("Segoe UI Semibold", 11f);
            _btnLogin.Click += delegate { DoLogin(); };

            _pageLogin.Controls.AddRange(new Control[]
            {
                _lblUser, _user, _lblPass, _pass, _remember, _auto, _error, _btnLogin
            });

            // Trang cơ sở dữ liệu
            SetupLabel(_lblConn, "Kết nối:");
            _lblConn.TextAlign = ContentAlignment.MiddleLeft;
            _rbLocal.Text = "Máy này (localhost)";
            _rbServer.Text = "Máy chủ Firebird";
            _rbLocal.AutoSize = _rbServer.AutoSize = true;
            _rbLocal.Checked = true;
            _rbLocal.CheckedChanged += delegate { OnConnectionModeChanged(); };
            _rbServer.CheckedChanged += delegate { OnConnectionModeChanged(); };

            SetupLabel(_lblHost, "IP máy chủ:");
            _host.IconGlyph = "";
            _host.IconImage = Kazuko_FnB.Services.IconCatalog.Named("globe");
            _host.Placeholder = "192.168.1.10";
            SetupLabel(_lblPort, "Cổng:");
            _port.IconGlyph = "";
            _port.IconImage = Kazuko_FnB.Services.IconCatalog.Named("link");
            _port.Placeholder = AppSettings.DefaultServerPort;

            _dbPath.IconGlyph = "";

            _dbPath.IconImage = Kazuko_FnB.Services.IconCatalog.Named("database");
            _dbPath.Placeholder = "Đường dẫn tệp .FDB";

            SetupLabel(_lblFbUser, "Tài khoản Firebird:");
            _fbUser.IconGlyph = "";
            _fbUser.IconImage = Kazuko_FnB.Services.IconCatalog.Named("businessman");
            _fbUser.Placeholder = AppSettings.DefaultFbUser;
            SetupLabel(_lblFbPass, "Mật khẩu:");
            _fbPass.IconGlyph = "";
            _fbPass.IconImage = Kazuko_FnB.Services.IconCatalog.Named("key");
            _fbPass.Placeholder = "Mật khẩu Firebird";
            _fbPass.IsPassword = true;
            _btnCreate.Text = "TẠO DATA";
            _btnPick.Text = "CHỌN DATA";
            _btnConnect.Text = "KẾT NỐI";
            foreach (FlatButton b in new[] { _btnCreate, _btnPick, _btnConnect })
                b.Font = new Font("Segoe UI Semibold", 10f);
            _btnCreate.Click += delegate { DoCreateDatabase(); };
            _btnPick.Click += delegate { DoPickDatabase(); };
            _btnConnect.Click += delegate { DoConnect(); };
            _dbStatus.AutoSize = false;
            _dbStatus.TextAlign = ContentAlignment.MiddleLeft;

            _pageDb.Controls.AddRange(new Control[]
            {
                _lblConn, _rbLocal, _rbServer, _lblHost, _host, _lblPort, _port, _dbPath,
                _lblFbUser, _fbUser, _lblFbPass, _fbPass, _dbStatus, _btnCreate, _btnPick, _btnConnect
            });
            _lblHost.Visible = _host.Visible = _lblPort.Visible = _port.Visible = false;
            _pageDb.Visible = false;

            _marquee.Text = "Chào mừng đến với KAZUKO FNB  –  Giải pháp quản lý FnB: bán hàng, kho, công nợ, nhân sự  –  " +
                            "Mỗi đơn hàng đúng giờ, mỗi khách hàng hài lòng!";
            Controls.AddRange(new Control[] { _bar, _title, _tabs, _pageLogin, _pageDb, _version, _marquee });

            // Thứ tự Tab
            _tabs.TabIndex = 0;
            _pageLogin.TabIndex = 1;
            _pageDb.TabIndex = 2;
            int i = 0;
            foreach (Control c in new Control[] { _user, _pass, _remember, _auto, _btnLogin })
                c.TabIndex = i++;
            i = 0;
            foreach (Control c in new Control[] { _rbLocal, _rbServer, _host, _port, _dbPath, _fbUser, _fbPass, _btnCreate, _btnPick, _btnConnect })
                c.TabIndex = i++;
        }

        private static void SetupLabel(Label l, string text)
        {
            l.Text = text;
            l.AutoSize = false;
            l.Font = new Font("Segoe UI Semibold", 10f);
            l.TextAlign = ContentAlignment.BottomLeft;
        }

        private void LoadBackground()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            foreach (string name in new[] { "login-bg.jpg", "login-bg.png" })
            {
                string path = Path.Combine(dir, name);
                if (!File.Exists(path))
                    continue;
                try
                {
                    // Đọc qua bộ nhớ để không khóa file ảnh.
                    using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                    using (Image tmp = Image.FromStream(ms))
                        _background = new Bitmap(tmp);
                    return;
                }
                catch (ArgumentException)
                {
                    // Ảnh hỏng: dùng nền gradient.
                }
                catch (IOException)
                {
                }
            }
        }

        // ------------------------------------------------------------------ bố cục

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutAll();
        }

        /// <summary>Tab đăng nhập: 500; tab CSDL cao hơn, thêm hàng IP/Cổng khi chọn máy chủ.</summary>
        private int CardHeight(float s)
        {
            if (_tabs.SelectedIndex == 0)
                return (int)(500 * s);
            int pad = 32, header = 52 + 62, footer = 22 + 8;
            int page = 26 + 34 + 54 + 26 + 54 + 46 + 46;   // nhãn + radio + đường dẫn + tài khoản + trạng thái + nút
            if (_rbServer.Checked)
                page += 26 + 54;                           // hàng IP / Cổng
            return (int)((pad + header + page + footer + pad) * s);
        }

        private void LayoutAll()
        {
            float s = S;
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W <= 0 || H <= 0)
                return;

            int barH = (int)(40 * s);
            _bar.SetBounds(0, 0, W, barH);
            int marqueeH = (int)(34 * s);
            _marquee.SetBounds(0, H - marqueeH, W, marqueeH);

            int cardW = (int)(440 * s);
            int cardH = CardHeight(s);
            int margin = Math.Max((int)(48 * s), (int)(W * 0.07));
            int x = Math.Max((int)(16 * s), W - cardW - margin);
            int y = barH + Math.Max((int)(16 * s), (H - barH - marqueeH - cardH) / 2);
            _card = new Rectangle(x, y, cardW, cardH);

            int pad = (int)(32 * s);
            int cx = x + pad, cw = cardW - pad * 2, cy = y + pad;
            _title.SetBounds(cx, cy - (int)(4 * s), cw, (int)(50 * s));
            cy += (int)(52 * s);
            _tabs.SetBounds(cx, cy, cw, (int)(46 * s));
            cy += (int)(62 * s);
            int footerH = (int)(22 * s);
            int pageH = y + cardH - pad - footerH - (int)(8 * s) - cy;
            _pageLogin.SetBounds(cx, cy, cw, pageH);
            _pageDb.SetBounds(cx, cy, cw, pageH);
            _version.SetBounds(cx, y + cardH - pad / 2 - footerH, cw, footerH);

            // Trang đăng nhập (toạ độ trong panel)
            int py = 0;
            _lblUser.SetBounds(0, py, cw, (int)(22 * s)); py += (int)(26 * s);
            _user.SetBounds(0, py, cw, (int)(44 * s)); py += (int)(56 * s);
            _lblPass.SetBounds(0, py, cw, (int)(22 * s)); py += (int)(26 * s);
            _pass.SetBounds(0, py, cw, (int)(44 * s)); py += (int)(54 * s);
            _remember.Location = new Point(0, py);
            _auto.Location = new Point(Math.Max(0, cw - _auto.PreferredSize.Width), py);
            py += (int)(30 * s);
            _error.SetBounds(0, py, cw, (int)(38 * s)); py += (int)(42 * s);
            _btnLogin.SetBounds(0, py, cw, (int)(48 * s)); _btnLogin.Radius = (int)(10 * s);

            // Trang cơ sở dữ liệu: Kết nối (radio) → [IP/Cổng] → đường dẫn → tài khoản/mật khẩu
            // Firebird → dòng trạng thái → hàng nút.
            py = 0;
            _lblConn.SetBounds(0, py, cw, (int)(24 * s)); py += (int)(26 * s);
            int half = cw / 2;
            _rbLocal.Location = new Point(0, py);
            _rbServer.Location = new Point(half, py);
            py += (int)(34 * s);

            bool server = _rbServer.Checked;
            int colGap = (int)(12 * s);
            if (server)
            {
                int hostW = (int)(cw * 0.62);
                _lblHost.SetBounds(0, py, hostW, (int)(22 * s));
                _lblPort.SetBounds(hostW + colGap, py, cw - hostW - colGap, (int)(22 * s));
                py += (int)(26 * s);
                _host.SetBounds(0, py, hostW, (int)(44 * s));
                _port.SetBounds(hostW + colGap, py, cw - hostW - colGap, (int)(44 * s));
                py += (int)(54 * s);
            }
            _dbPath.SetBounds(0, py, cw, (int)(44 * s)); py += (int)(54 * s);

            // Cột mật khẩu rộng hơn một chút để không cắt dấu chấm khi mật khẩu dài.
            int colW = (int)((cw - colGap) * 0.44);
            _lblFbUser.SetBounds(0, py, colW, (int)(22 * s));
            _lblFbPass.SetBounds(colW + colGap, py, cw - colW - colGap, (int)(22 * s));
            py += (int)(26 * s);
            _fbUser.SetBounds(0, py, colW, (int)(44 * s));
            _fbPass.SetBounds(colW + colGap, py, cw - colW - colGap, (int)(44 * s));
            py += (int)(54 * s);

            _dbStatus.SetBounds(0, py, cw, (int)(40 * s)); py += (int)(46 * s);
            int gap = (int)(10 * s);
            int bw = (cw - gap * 2) / 3;
            _btnCreate.SetBounds(0, py, bw, (int)(46 * s));
            _btnPick.SetBounds(bw + gap, py, bw, (int)(46 * s));
            _btnConnect.SetBounds((bw + gap) * 2, py, cw - (bw + gap) * 2, (int)(46 * s));
            foreach (FlatButton b in new[] { _btnCreate, _btnPick, _btnConnect })
                b.Radius = (int)(10 * s);

            Invalidate();
        }

        // ------------------------------------------------------------------ theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            Color card = t.Surface;
            Color strongBorder = StrongBorder(t);

            _bar.BarColor = t.Primary;
            _bar.BarTextColor = t.OnPrimary;
            _bar.Invalidate();

            _marquee.FromColor = Draw.GradFrom(t);
            _marquee.ToColor = Draw.GradTo(t);
            _marquee.TextColor = Color.White;
            _marquee.Invalidate();

            foreach (Control c in new Control[] { _title, _version, _pageLogin, _pageDb })
                c.BackColor = card;
            _title.ForeColor = t.Text;
            _version.ForeColor = t.TextMuted;

            _tabs.SurfaceColor = card;
            _tabs.TrackColor = Draw.Mix(t.Background, t.Border, 0.6f);
            _tabs.PillColor = Color.White;
            _tabs.SelectedTextColor = t.Text;
            _tabs.TextColor = t.TextMuted;
            _tabs.BorderColor = strongBorder;
            _tabs.Invalidate();

            foreach (Label l in new[] { _lblUser, _lblPass, _lblConn, _lblHost, _lblPort, _lblFbUser, _lblFbPass })
            {
                l.BackColor = card;
                l.ForeColor = t.Text;
            }
            foreach (InputBox box in new[] { _user, _pass, _dbPath, _host, _port, _fbUser, _fbPass })
            {
                box.SurfaceColor = card;
                box.FieldColor = Color.White;
                box.BorderColor = strongBorder;
                box.FocusColor = t.Primary;
                box.TextColor = t.Text;
                box.MutedColor = t.TextMuted;
                box.ApplyColors();
            }
            foreach (CheckBox cb in new[] { _remember, _auto })
            {
                cb.BackColor = card;
                cb.ForeColor = t.Text;
            }
            foreach (RadioButton rb in new[] { _rbLocal, _rbServer })
            {
                rb.BackColor = card;
                rb.ForeColor = rb.Enabled ? t.Text : t.TextMuted;
            }
            _error.BackColor = card;
            _error.ForeColor = t.Danger;
            _dbStatus.BackColor = card;

            foreach (FlatButton b in new[] { _btnLogin, _btnCreate, _btnConnect })
            {
                b.SurfaceColor = card;
                b.FillColor = t.Primary;
                b.TextColor = t.OnPrimary;
                b.Invalidate();
            }
            _btnPick.SurfaceColor = card;
            _btnPick.FillColor = Draw.Darken(t.Primary, 0.6f);
            _btnPick.TextColor = Color.White;
            _btnPick.Invalidate();

            Invalidate();
        }

        /// <summary>Viền đậm (sếp yêu cầu): đậm hơn màu Border của theme.</summary>
        private static Color StrongBorder(ThemeTokens t)
        {
            return Draw.Mix(t.Border, t.Text, 0.38f);
        }

        // ------------------------------------------------------------------ vẽ nền + khung

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            ThemeTokens t = ThemeManager.Current;
            float s = S;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Vùng nền nằm dưới appbar để ảnh không bị appbar che mất phần trên.
            // Chừa dải chữ chạy ở đáy: ảnh nền chỉ vẽ phía trên dải, không bị dải đè lên.
            Rectangle area = new Rectangle(0, _bar.Bottom, ClientSize.Width, Math.Max(1, ClientSize.Height - _bar.Bottom - _marquee.Height));
            if (_background != null)
            {
                // Ảnh 4K: thu sẵn đúng kích thước cửa sổ một lần, các lần vẽ sau chỉ chép lại.
                if (_bgCache == null || _bgCache.Size != area.Size)
                {
                    if (_bgCache != null)
                        _bgCache.Dispose();
                    _bgCache = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height));
                    using (Graphics cg = Graphics.FromImage(_bgCache))
                        DrawFitWithBlur(cg, _background, new Rectangle(Point.Empty, _bgCache.Size));
                }
                g.DrawImageUnscaled(_bgCache, area.Location);
            }
            else
            {
                Color c1 = Draw.Lighten(t.Primary, 0.55f);
                Color c2 = t.Primary;
                using (var lg = new LinearGradientBrush(area, c1, c2, 35f))
                    g.FillRectangle(lg, area);
                using (var bubble = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                {
                    g.FillEllipse(bubble, -area.Width * 0.12f, area.Height * 0.45f, area.Width * 0.5f, area.Width * 0.5f);
                    g.FillEllipse(bubble, area.Width * 0.25f, -area.Height * 0.3f, area.Width * 0.35f, area.Width * 0.35f);
                }
                // Tạm thời: tên sản phẩm bên trái, sẽ thay bằng ảnh nền.
                using (var big = new Font("Segoe UI", 44f, FontStyle.Bold))
                using (var sub = new Font("Segoe UI", 16f))
                {
                    var r = new Rectangle((int)(area.Width * 0.08), 0, Math.Max(10, _card.Left - (int)(area.Width * 0.12)), area.Height);
                    Color fg = Color.White;
                    TextRenderer.DrawText(g, "KAZUKO FNB", big, new Rectangle(r.X, area.Height / 2 - (int)(70 * s), r.Width, (int)(80 * s)), fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, "Phần mềm quản lý FnB", sub, new Rectangle(r.X, area.Height / 2 + (int)(10 * s), r.Width, (int)(40 * s)),
                        Color.FromArgb(220, fg), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }

            if (_card.Width <= 0)
                return;
            float radius = 20 * s;
            for (int i = 1; i <= 6; i++)
            {
                RectangleF sh = RectangleF.Inflate(_card, i * 2 * s, i * 2 * s);
                sh.Offset(0, 4 * s);
                using (GraphicsPath p = Draw.Rounded(sh, radius + i * 2 * s))
                using (var b = new SolidBrush(Color.FromArgb(9, 0, 0, 0)))
                    g.FillPath(b, p);
            }
            using (GraphicsPath p = Draw.Rounded(_card, radius))
            {
                using (var b = new SolidBrush(t.Surface))
                    g.FillPath(b, p);
                using (var pen = new Pen(StrongBorder(t), 2f * s))
                    g.DrawPath(pen, p);
            }
        }

        /// <summary>
        /// Hiện trọn ảnh nền (vừa chiều cao), phần thừa hai bên lấp bằng chính ảnh đó phóng to và làm mờ,
        /// mép ảnh mờ dần vào nền mờ nên không thấy đường cắt. Vùng quá rộng hoặc quá cao so với ảnh thì phủ kín như cũ.
        /// </summary>
        private static void DrawFitWithBlur(Graphics g, Image img, Rectangle area)
        {
            float areaRatio = area.Width / (float)area.Height;
            float imgRatio = img.Width / (float)img.Height;
            if (areaRatio <= imgRatio || areaRatio > imgRatio * 1.15f)
            {
                DrawCover(g, img, area);
                return;
            }

            // Nền mờ: thu nhỏ rất nhiều rồi phóng lại.
            using (var small = new Bitmap(Math.Max(8, area.Width / 24), Math.Max(8, area.Height / 24)))
            {
                using (Graphics sg = Graphics.FromImage(small))
                    DrawCover(sg, img, new Rectangle(Point.Empty, small.Size));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(small, area);
            }

            // Ảnh chính vừa chiều cao, hai mép mờ dần (alpha 0 → 255) để hòa vào nền mờ.
            float scale = area.Height / (float)img.Height;
            int w = (int)Math.Round(img.Width * scale), h = area.Height;
            int x = area.X + (area.Width - w) / 2;
            using (var fit = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics fg = Graphics.FromImage(fit))
                {
                    fg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    fg.DrawImage(img, new Rectangle(0, 0, w, h));
                }
                int feather = Math.Max(4, Math.Min(48, w / 16));
                var data = fit.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var row = new byte[data.Stride];
                    for (int yy = 0; yy < h; yy++)
                    {
                        IntPtr ptr = data.Scan0 + yy * data.Stride;
                        System.Runtime.InteropServices.Marshal.Copy(ptr, row, 0, data.Stride);
                        for (int xx = 0; xx < feather; xx++)
                        {
                            byte a = (byte)(255 * xx / feather);
                            row[xx * 4 + 3] = a;
                            row[(w - 1 - xx) * 4 + 3] = a;
                        }
                        System.Runtime.InteropServices.Marshal.Copy(row, 0, ptr, data.Stride);
                    }
                }
                finally
                {
                    fit.UnlockBits(data);
                }
                g.DrawImageUnscaled(fit, x, area.Y);
            }
        }

        private static void DrawCover(Graphics g, Image img, Rectangle area)
        {
            float scale = Math.Max(area.Width / (float)img.Width, area.Height / (float)img.Height);
            float w = img.Width * scale, h = img.Height * scale;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        }

        // ------------------------------------------------------------------ hành động

        private void OnTabChanged()
        {
            bool login = _tabs.SelectedIndex == 0;
            _pageLogin.Visible = login;
            _pageDb.Visible = !login;
            _title.Text = login ? "Đăng nhập" : "Cơ sở dữ liệu";
            AcceptButton = login ? _btnLogin : _btnConnect;
            LayoutAll();
        }

        /// <summary>Đổi giữa "Máy này" và "Máy chủ Firebird". Chế độ máy chủ chưa được hỗ trợ.</summary>
        private void OnConnectionModeChanged()
        {
            bool server = _rbServer.Checked;
            _lblHost.Visible = _host.Visible = _lblPort.Visible = _port.Visible = server;
            _btnCreate.Enabled = _btnPick.Enabled = !server;
            // Firebird embedded bỏ qua tài khoản/mật khẩu nên khóa chỉ đọc; chỉ sửa được ở chế độ máy chủ.
            _fbUser.ReadOnly = _fbPass.ReadOnly = !server;
            if (server)
                SetDbStatus(ServerNotSupported, StatusKind.Error);
            else
                SetDbStatus("", StatusKind.Info);
            LayoutAll();
        }

        private const string ServerNotSupported = "Chế độ máy chủ Firebird chưa được hỗ trợ trong phiên bản này.";

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_tabs.SelectedIndex == 0)
            {
                if (string.IsNullOrEmpty(_user.Text))
                    _user.Focus();
                else
                    _pass.Focus();
            }
        }

        private void ShowError(string message)
        {
            _error.Text = message;
        }

        private void SetDbStatus(string message, StatusKind kind)
        {
            ThemeTokens t = ThemeManager.Current;
            _dbStatus.Text = message;
            _dbStatus.ForeColor = kind == StatusKind.Error ? t.Danger
                : kind == StatusKind.Success ? t.Success
                : t.Text;
            _dbStatus.Font = kind == StatusKind.Success ? _statusBold : Font;
        }

        private readonly Font _statusBold = new Font("Segoe UI Semibold", 10f);

        private void DoLogin()
        {
            ShowError("");
            if (_rbServer.Checked)
            {
                _tabs.SelectedIndex = 1;
                SetDbStatus(ServerNotSupported, StatusKind.Error);
                return;
            }
            Data.KazukoConnection.Configure(_fbUser.Text, _fbPass.Text);
            string db = _dbPath.Text.Trim();
            if (db.Length == 0 || !File.Exists(db))
            {
                _tabs.SelectedIndex = 1;
                SetDbStatus("Chưa có tệp dữ liệu. Chọn hoặc tạo dữ liệu trước khi đăng nhập.", StatusKind.Error);
                return;
            }
            string user = _user.Text.Trim();
            if (user.Length == 0)
            {
                ShowError("Nhập tên tài khoản.");
                _user.Focus();
                return;
            }

            LoginResult result;
            Cursor = Cursors.WaitCursor;
            _btnLogin.Enabled = false;
            try
            {
                result = _login.Login(db, user, _pass.Text);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnLogin.Enabled = true;
            }

            if (!result.Success)
            {
                ShowError(result.Message);
                _pass.Focus();
                _pass.Inner.SelectAll();
                return;
            }

            _settings.DatabasePath = db;
            SaveFirebirdSettings();
            _settings.UserName = user;
            _settings.RememberLogin = _remember.Checked;
            _settings.AutoLogin = _remember.Checked && _auto.Checked;
            _settings.SetPassword(_remember.Checked ? _pass.Text : null);
            try
            {
                _settings.Save();
            }
            catch (IOException)
            {
                // Không lưu được cấu hình thì vẫn cho đăng nhập.
            }
            catch (UnauthorizedAccessException)
            {
            }

            Session = result.Session;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DoPickDatabase()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn tệp dữ liệu";
                dlg.Filter = "Dữ liệu Firebird (*.fdb)|*.fdb|Tất cả tệp (*.*)|*.*";
                string cur = _dbPath.Text.Trim();
                if (cur.Length > 0 && Directory.Exists(Path.GetDirectoryName(cur) ?? ""))
                    dlg.InitialDirectory = Path.GetDirectoryName(cur);
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _dbPath.Text = dlg.FileName;
                SetDbStatus("Đã chọn tệp. Bấm KẾT NỐI để kiểm tra.", StatusKind.Info);
            }
        }

        private void DoCreateDatabase()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Tạo dữ liệu mới từ mẫu";
                dlg.Filter = "Dữ liệu Firebird (*.fdb)|*.fdb";
                dlg.FileName = "DATA.FDB";
                dlg.OverwritePrompt = false;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                string err = _db.CreateFromTemplate(dlg.FileName);
                if (err != null)
                {
                    SetDbStatus(err, StatusKind.Error);
                    return;
                }
                _dbPath.Text = dlg.FileName;
                SetDbStatus("Đã tạo dữ liệu mới từ mẫu. Bấm KẾT NỐI để dùng.", StatusKind.Info);
            }
        }

        private void SaveFirebirdSettings()
        {
            _settings.FbUser = _fbUser.Text.Trim();
            _settings.SetFbPassword(_fbPass.Text);
            _settings.UseServer = _rbServer.Checked;
            _settings.ServerHost = _host.Text.Trim();
            _settings.ServerPort = _port.Text.Trim();
        }

        private void DoConnect()
        {
            if (_rbServer.Checked)
            {
                SetDbStatus(ServerNotSupported, StatusKind.Error);
                return;
            }
            Data.KazukoConnection.Configure(_fbUser.Text, _fbPass.Text);
            string path = _dbPath.Text.Trim();
            Cursor = Cursors.WaitCursor;
            string err;
            try
            {
                err = _db.TestConnection(path);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            if (err != null)
            {
                SetDbStatus(err, StatusKind.Error);
                return;
            }
            _settings.DatabasePath = path;
            SaveFirebirdSettings();
            try
            {
                _settings.Save();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            // Ở lại tab này để thấy dòng xanh; người dùng tự chuyển sang tab Đăng nhập.
            SetDbStatus("Kết nối thành công.", StatusKind.Success);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_background != null)
                    _background.Dispose();
                if (_bgCache != null)
                    _bgCache.Dispose();
                _statusBold.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
