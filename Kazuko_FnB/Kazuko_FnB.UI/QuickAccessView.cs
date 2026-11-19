using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>Màn "Truy cập nhanh chức năng": các thẻ theo nhóm menu, tự xếp 1 đến 4 cột theo chiều rộng.</summary>
    internal sealed class QuickAccessView : Panel
    {
        private readonly List<QuickAccessCard> _cards = new List<QuickAccessCard>();
        private readonly Image _banner = BrandLogo.Banner;
        private Bitmap _bannerCache;
        private Rectangle _bannerBase;      // vị trí banner (chưa cộng độ lệch cuộn)
        private int _lastContentHeight = -1;

        public event EventHandler<MenuNode> ItemClicked;

        public QuickAccessView(IEnumerable<QuickGroup> groups)
        {
            AutoScroll = true;
            DoubleBuffered = true;


            foreach (QuickGroup g in groups)
            {
                var card = new QuickAccessCard { Title = g.Title, Glyph = g.Glyph };
                card.SetItems(g.Items);
                card.ItemClicked += delegate (object sender, MenuNode node)
                {
                    EventHandler<MenuNode> h = ItemClicked;
                    if (h != null)
                        h(this, node);
                };
                _cards.Add(card);
                Controls.Add(card);
            }
            // Ô thống kê cuối trang (chỉ tiêu cho quán FnB). Số liệu để trống ("—"), sẽ bổ sung sau khi có spec.
            foreach (string[] def in StatDefs)
            {
                var tile = new StatTile { Caption = def[0], Icon = IconCatalog.Named(def[1]) };
                _tiles.Add(tile);
                Controls.Add(tile);
            }
            ApplyTheme(ThemeManager.Current);
        }

        private static readonly string[][] StatDefs =
        {
            new[] { "Doanh thu hôm nay", "bar_chart" },
            new[] { "Số đơn hôm nay", "todo_list" },
            new[] { "Bàn đang phục vụ", "grid" },
            new[] { "Món bán chạy", "rating" },
            new[] { "Tồn kho sắp hết", "package" },
            new[] { "Công nợ phải thu", "debt" }
        };

        private readonly List<StatTile> _tiles = new List<StatTile>();

        private float S
        {
            get { return DeviceDpi / 96f; }
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ApplyTheme(t, t.Background);
        }

        public void ApplyTheme(ThemeTokens t, Color page)
        {
            Color strong = Draw.StrongBorder(t);
            BackColor = page;
            foreach (QuickAccessCard c in _cards)
            {
                c.PageColor = page;
                c.SurfaceColor = Color.White;
                c.HeaderColor = Draw.Mix(Color.White, Color.FromArgb(255, 222, 120), 0.40f);
                c.BorderColor = strong;
                c.RowColor = t.Background;
                c.RowHoverColor = Draw.Mix(t.Surface, t.Primary, 0.16f);
                c.TextColor = t.Text;
                c.AccentColor = t.Primary;
                c.GradientFrom = Draw.GradFrom(t);
                c.GradientTo = Draw.GradTo(t);
                c.Invalidate();
            }
            foreach (StatTile st in _tiles)
            {
                st.PageColor = page;
                st.SurfaceColor = Color.White;
                st.BorderColor = strong;
                st.CaptionColor = t.TextMuted;
                st.ValueColor = t.Text;
                st.AccentFrom = Draw.GradFrom(t);
                st.AccentTo = Draw.GradTo(t);
                st.Invalidate();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_banner == null || _bannerBase.Width <= 0 || _bannerBase.Height <= 0)
                return;
            float s = S;
            Graphics g = e.Graphics;
            var r = new Rectangle(_bannerBase.X + AutoScrollPosition.X, _bannerBase.Y + AutoScrollPosition.Y,
                _bannerBase.Width, _bannerBase.Height);

            // Banner rất dài: cắt kiểu "cover", giữ phần bên trái (logo và chữ), cắt bớt phần nền vàng bên phải.
            if (_bannerCache == null || _bannerCache.Size != r.Size)
            {
                if (_bannerCache != null)
                    _bannerCache.Dispose();
                _bannerCache = new Bitmap(r.Width, r.Height);
                using (Graphics cg = Graphics.FromImage(_bannerCache))
                {
                    cg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    cg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    float target = r.Width / (float)r.Height;
                    float srcRatio = _banner.Width / (float)_banner.Height;
                    RectangleF src = target < srcRatio
                        ? new RectangleF(0, 0, _banner.Height * target, _banner.Height)
                        : new RectangleF(0, (_banner.Height - _banner.Width / target) / 2f, _banner.Width, _banner.Width / target);
                    cg.DrawImage(_banner, new Rectangle(0, 0, r.Width, r.Height), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
                }
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rf = new RectangleF(r.X + 1f, r.Y + 1f, r.Width - 2f, r.Height - 2f);
            using (var path = Draw.Rounded(rf, 12 * s))
            {
                var oldClip = g.Clip;
                g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
                g.DrawImageUnscaled(_bannerCache, r.Location);
                g.Clip = oldClip;
                using (var pen = new Pen(Draw.StrongBorder(ThemeManager.Current), 1.8f * s))
                    g.DrawPath(pen, path);
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutCards();
        }

        private void LayoutCards()
        {
            float s = S;
            int pad = (int)(24 * s);
            int gap = (int)(18 * s);
            int width = ClientSize.Width;
            if (width <= 0)
                return;

            int ox = AutoScrollPosition.X, oy = AutoScrollPosition.Y;

            int avail = width - pad * 2;
            int cols = Math.Max(1, Math.Min(4, (avail + gap) / ((int)(300 * s) + gap)));
            cols = Math.Min(cols, Math.Max(1, _cards.Count));
            int cardW = (avail - gap * (cols - 1)) / cols;

            int fitH = Math.Max(0, ClientSize.Height);
            int top0 = (int)(14 * s);

            // Banner (thay cho tiêu đề): cao tối đa ~104px, co lại khi thiếu chỗ.
            float ratio = _banner == null ? 0f : _banner.Width / (float)_banner.Height;
            int bannerMax = _banner == null ? 0 : Math.Min((int)(avail / ratio), (int)(104 * s));
            int bannerMin = _banner == null ? 0 : (int)(56 * s);
            int bannerH = bannerMax;

            // Không cuộn dọc: nén theo từng bậc cho tới khi vừa khung nhìn:
            // 1) hàng trong thẻ  2) banner  3) ô thống kê + khoảng cách  4) hàng trong thẻ thêm nữa  5) ẩn banner.
            float density = 1f, tileScale = 1f;
            int vgap = gap;
            int stage = 1;
            Func<int> measure = delegate
            {
                return PlaceCards(cols, cardW, pad, gap, vgap, tileScale, TopBelowBanner(top0, bannerH, gap), density, 0, 0, false);
            };
            int contentH = measure();
            while (contentH > fitH && stage <= 5)
            {
                switch (stage)
                {
                    case 1:
                        if (density > 0.62f) density -= 0.04f; else stage++;
                        break;
                    case 2:
                        if (bannerH > bannerMin) bannerH = Math.Max(bannerMin, bannerH - (int)(6 * s)); else stage++;
                        break;
                    case 3:
                        if (tileScale > 0.72f) { tileScale -= 0.04f; vgap = Math.Max((int)(10 * s), vgap - (int)(2 * s)); } else stage++;
                        break;
                    case 4:
                        if (density > 0.5f) density -= 0.04f; else stage++;
                        break;
                    default:
                        bannerH = 0;
                        stage++;
                        break;
                }
                contentH = measure();
            }
            var newBase = new Rectangle(pad, top0, avail, bannerH);
            if (newBase != _bannerBase)
            {
                _bannerBase = newBase;
                Invalidate();
            }
            int top = TopBelowBanner(top0, bannerH, gap);
            PlaceCards(cols, cardW, pad, gap, vgap, tileScale, top, density, ox, oy, true);

            // Chỉ cuộn khi nén tối đa vẫn không đủ chỗ.
            int scrollH = contentH > fitH ? contentH : 0;
            if (scrollH != _lastContentHeight)
            {
                _lastContentHeight = scrollH;
                AutoScrollMinSize = new Size(0, scrollH);
            }
        }

        private static int TopBelowBanner(int top0, int bannerH, int gap)
        {
            return bannerH > 0 ? top0 + bannerH + gap : top0;
        }

        private int PlaceCards(int cols, int cardW, int pad, int gap, int vgap, float tileScale, int top, float density, int ox, int oy, bool apply)
        {
            float s = S;
            int y = top;
            int i = 0;
            foreach (QuickAccessCard c in _cards)
                c.Density = density;
            while (i < _cards.Count)
            {
                int rowH = 0;
                int n = Math.Min(cols, _cards.Count - i);
                for (int k = 0; k < n; k++)
                    rowH = Math.Max(rowH, _cards[i + k].RequiredHeight(s));
                if (apply)
                    for (int k = 0; k < n; k++)
                        _cards[i + k].SetBounds(pad + k * (cardW + gap) + ox, y + oy, cardW, rowH);
                y += rowH + vgap;
                i += n;
            }

            // Hàng ô thống kê nhỏ ở cuối: 6 ô một hàng; màn hẹp thì 3 ô x 2 hàng.
            int avail = cardW * cols + gap * (cols - 1);
            int tileCols = avail >= (int)(900 * s) ? 6 : 3;
            int tileRows = (_tiles.Count + tileCols - 1) / tileCols;
            int tileGap = (int)(12 * s);
            int tileH = (int)(76 * s * tileScale);
            if (apply)
            {
                int tw = (avail - tileGap * (tileCols - 1)) / tileCols;
                for (int t = 0; t < _tiles.Count; t++)
                {
                    int row = t / tileCols, col = t % tileCols;
                    _tiles[t].SetBounds(pad + col * (tw + tileGap) + ox, y + row * (tileH + tileGap) + oy, tw, tileH);
                }
            }
            return y + tileRows * tileH + (tileRows - 1) * tileGap + pad;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bannerCache != null)
                    _bannerCache.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Nội dung tạm cho chức năng chưa làm: hiện tên chức năng và ghi chú.</summary>
    internal sealed class PlaceholderView : Panel
    {
        private readonly Label _title = new Label();
        private readonly Label _note = new Label();

        public PlaceholderView(string title)
        {
            _title.Text = title;
            _title.Font = new Font("Segoe UI Semibold", 18f);
            _title.AutoSize = false;
            _title.TextAlign = ContentAlignment.BottomCenter;
            _note.Text = "Chức năng này sẽ được bổ sung ở các bước sau.";
            _note.Font = new Font("Segoe UI", 11f);
            _note.AutoSize = false;
            _note.TextAlign = ContentAlignment.TopCenter;
            Controls.Add(_title);
            Controls.Add(_note);
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ApplyTheme(t, t.Background);
        }

        public void ApplyTheme(ThemeTokens t, Color page)
        {
            BackColor = page;
            _title.BackColor = _note.BackColor = page;
            _title.ForeColor = t.Text;
            _note.ForeColor = t.TextMuted;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int h = (int)(60 * DeviceDpi / 96f);
            int mid = ClientSize.Height / 2;
            _title.SetBounds(0, mid - h, ClientSize.Width, h);
            _note.SetBounds(0, mid + 4, ClientSize.Width, h);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _title.Font.Dispose();
                _note.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
