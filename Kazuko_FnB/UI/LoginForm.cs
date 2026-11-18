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
        private Rectangle _card;

        private readonly AppBar _bar = new AppBar();
        private readonly Label _title = new Label();
        private readonly SegmentedTabs _tabs = new SegmentedTabs();
        private readonly Label _version = new Label();

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
        private readonly Label _lblDb = new Label();
        private readonly InputBox _dbPath = new InputBox();
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

            Text = "KAZUKO TEA - Đăng nhập";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 10f);

            BuildControls();
            LoadBackground();

            _user.Text = settings.UserName ?? "";
            _remember.Checked = settings.RememberLogin;
            _auto.Checked = settings.RememberLogin && settings.AutoLogin;
            if (settings.RememberLogin)
                _pass.Text = settings.GetPassword() ?? "";
            _dbPath.Text = settings.DatabasePath ?? "";

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

        /// <summary>Chọn tab (0 = Đăng nhập, 1 = Cơ sở dữ liệu).</summary>
        internal void SelectTab(int index)
        {
            _tabs.SelectedIndex = index;
            OnTabChanged();
        }

        // ------------------------------------------------------------------ dựng control

        private void BuildControls()
        {
            _bar.Text = "KAZUKO TEA";
            _title.Text = "Đăng nhập";
            _title.Font = new Font("Segoe UI Semibold", 20f);
            _title.AutoSize = false;

            _tabs.Items = new[] { "Đăng nhập", "Cơ sở dữ liệu" };
            _tabs.SelectedIndexChanged += delegate { OnTabChanged(); };

            _version.Text = "KAZUKO TEA v" + Application.ProductVersion;
            _version.Font = new Font("Segoe UI", 8.5f);
            _version.TextAlign = ContentAlignment.MiddleRight;

            // Trang đăng nhập
            SetupLabel(_lblUser, "Tên tài khoản");
            _user.IconGlyph = "";
            _user.Placeholder = "Nhập tên tài khoản";
            SetupLabel(_lblPass, "Mật khẩu");
            _pass.IconGlyph = "";
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
            SetupLabel(_lblDb, "Tệp dữ liệu (máy này)");
            _dbPath.IconGlyph = "";
            _dbPath.Placeholder = "Đường dẫn tệp .FDB";
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
                _lblDb, _dbPath, _dbStatus, _btnCreate, _btnPick, _btnConnect
            });
            _pageDb.Visible = false;

            Controls.AddRange(new Control[] { _bar, _title, _tabs, _pageLogin, _pageDb, _version });

            // Thứ tự Tab
            _tabs.TabIndex = 0;
            _pageLogin.TabIndex = 1;
            _pageDb.TabIndex = 2;
            int i = 0;
            foreach (Control c in new Control[] { _user, _pass, _remember, _auto, _btnLogin })
                c.TabIndex = i++;
            i = 0;
            foreach (Control c in new Control[] { _dbPath, _btnCreate, _btnPick, _btnConnect })
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

        private void LayoutAll()
        {
            float s = S;
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W <= 0 || H <= 0)
                return;

            int barH = (int)(40 * s);
            _bar.SetBounds(0, 0, W, barH);

            int cardW = (int)(440 * s);
            int cardH = (int)(500 * s);
            int margin = Math.Max((int)(48 * s), (int)(W * 0.07));
            int x = Math.Max((int)(16 * s), W - cardW - margin);
            int y = barH + Math.Max((int)(16 * s), (H - barH - cardH) / 2);
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

            // Trang cơ sở dữ liệu: dòng trạng thái nằm trên hàng nút.
            py = 0;
            _lblDb.SetBounds(0, py, cw, (int)(22 * s)); py += (int)(26 * s);
            _dbPath.SetBounds(0, py, cw, (int)(44 * s)); py += (int)(50 * s);
            _dbStatus.SetBounds(0, py, cw, (int)(48 * s)); py += (int)(56 * s);
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

            foreach (Control c in new Control[] { _title, _version, _pageLogin, _pageDb })
                c.BackColor = card;
            _title.ForeColor = t.Text;
            _version.ForeColor = t.TextMuted;

            _tabs.SurfaceColor = card;
            _tabs.TrackColor = Draw.Mix(t.Background, t.Border, 0.6f);
            _tabs.PillColor = t.IsDark ? Draw.Lighten(card, 0.18f) : Color.White;
            _tabs.SelectedTextColor = t.Text;
            _tabs.TextColor = t.TextMuted;
            _tabs.BorderColor = strongBorder;
            _tabs.Invalidate();

            foreach (Label l in new[] { _lblUser, _lblPass, _lblDb })
            {
                l.BackColor = card;
                l.ForeColor = t.Text;
            }
            foreach (InputBox box in new[] { _user, _pass, _dbPath })
            {
                box.SurfaceColor = card;
                box.FieldColor = t.IsDark ? Draw.Lighten(card, 0.05f) : Color.White;
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
            _btnPick.FillColor = t.IsDark ? Draw.Lighten(card, 0.18f) : Draw.Darken(t.Primary, 0.6f);
            _btnPick.TextColor = Color.White;
            _btnPick.Invalidate();

            Invalidate();
        }

        /// <summary>Viền đậm (sếp yêu cầu): đậm hơn màu Border của theme.</summary>
        private static Color StrongBorder(ThemeTokens t)
        {
            return Draw.Mix(t.Border, t.Text, t.IsDark ? 0.30f : 0.38f);
        }

        // ------------------------------------------------------------------ vẽ nền + khung

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            ThemeTokens t = ThemeManager.Current;
            float s = S;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle area = ClientRectangle;
            if (_background != null)
            {
                DrawCover(g, _background, area);
            }
            else
            {
                Color c1 = t.IsDark ? Draw.Mix(t.Background, t.Primary, 0.25f) : Draw.Lighten(t.Primary, 0.55f);
                Color c2 = t.IsDark ? t.Background : t.Primary;
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
                    Color fg = t.IsDark ? t.Text : Color.White;
                    TextRenderer.DrawText(g, "KAZUKO TEA", big, new Rectangle(r.X, area.Height / 2 - (int)(70 * s), r.Width, (int)(80 * s)), fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, "Phần mềm quản lý quán trà sữa", sub, new Rectangle(r.X, area.Height / 2 + (int)(10 * s), r.Width, (int)(40 * s)),
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
        }

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

        private void DoConnect()
        {
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
                _statusBold.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
