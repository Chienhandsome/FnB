using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>Màn "Truy cập nhanh chức năng": các thẻ theo nhóm menu, tự xếp 1 đến 4 cột theo chiều rộng.</summary>
    internal sealed class QuickAccessView : Panel
    {
        private readonly List<QuickAccessCard> _cards = new List<QuickAccessCard>();
        private readonly Label _headingIcon = new Label();
        private readonly Label _heading = new Label();
        private int _lastContentHeight = -1;

        public event EventHandler<MenuNode> ItemClicked;

        public QuickAccessView(IEnumerable<QuickGroup> groups)
        {
            AutoScroll = true;
            DoubleBuffered = true;

            _headingIcon.Text = "";
            _headingIcon.Font = new Font(Draw.IconFont, 16f);
            _headingIcon.AutoSize = false;
            _headingIcon.TextAlign = ContentAlignment.MiddleCenter;
            _heading.Text = "Truy cập nhanh chức năng";
            _heading.Font = new Font("Segoe UI Semibold", 14f);
            _heading.AutoSize = false;
            _heading.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_headingIcon);
            Controls.Add(_heading);

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
            ApplyTheme(ThemeManager.Current);
        }

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
            _headingIcon.BackColor = _heading.BackColor = page;
            _headingIcon.ForeColor = t.Primary;
            _heading.ForeColor = t.Text;
            foreach (QuickAccessCard c in _cards)
            {
                c.PageColor = page;
                c.SurfaceColor = t.IsDark ? t.Surface : Color.White;
                c.HeaderColor = t.IsDark ? Draw.Mix(t.Surface, t.Accent, 0.22f)
                    : Draw.Mix(Color.White, Color.FromArgb(255, 222, 120), 0.40f);
                c.BorderColor = strong;
                c.RowColor = t.IsDark ? Draw.Mix(t.Surface, t.Background, 0.45f) : t.Background;
                c.RowHoverColor = Draw.Mix(t.Surface, t.Primary, 0.16f);
                c.TextColor = t.Text;
                c.AccentColor = t.Primary;
                c.GradientFrom = Draw.GradFrom(t);
                c.GradientTo = Draw.GradTo(t);
                c.Invalidate();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = S;
            int pad = (int)(24 * s);
            var bar = new Rectangle(pad + AutoScrollPosition.X, (int)(54 * s) + AutoScrollPosition.Y, (int)(150 * s), Math.Max(3, (int)(4 * s)));
            ThemeTokens t = ThemeManager.Current;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(bar, Draw.GradFrom(t), Draw.GradTo(t), System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            using (var path = Draw.Rounded(bar, bar.Height / 2f))
                e.Graphics.FillPath(b, path);
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
            _headingIcon.SetBounds(pad + ox, (int)(14 * s) + oy, (int)(34 * s), (int)(40 * s));
            _heading.SetBounds(pad + (int)(38 * s) + ox, (int)(14 * s) + oy, Math.Max(10, width - pad * 2 - (int)(38 * s)), (int)(40 * s));

            int avail = width - pad * 2;
            int cols = Math.Max(1, Math.Min(4, (avail + gap) / ((int)(300 * s) + gap)));
            cols = Math.Min(cols, Math.Max(1, _cards.Count));
            int cardW = (avail - gap * (cols - 1)) / cols;

            int top = (int)(68 * s);
            int fitH = Math.Max(0, ClientSize.Height);

            // Nén dần chiều cao hàng cho tới khi vừa khung nhìn: không cần cuộn dọc.
            float density = 1f;
            int contentH = PlaceCards(cols, cardW, pad, gap, top, density, 0, 0, false);
            while (contentH > fitH && density > 0.62f)
            {
                density -= 0.04f;
                contentH = PlaceCards(cols, cardW, pad, gap, top, density, 0, 0, false);
            }
            PlaceCards(cols, cardW, pad, gap, top, density, ox, oy, true);

            // Chỉ cuộn khi nén tối đa vẫn không đủ chỗ.
            int scrollH = contentH > fitH ? contentH : 0;
            if (scrollH != _lastContentHeight)
            {
                _lastContentHeight = scrollH;
                AutoScrollMinSize = new Size(0, scrollH);
            }
        }

        private int PlaceCards(int cols, int cardW, int pad, int gap, int top, float density, int ox, int oy, bool apply)
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
                y += rowH + gap;
                i += n;
            }
            return y + pad - gap;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headingIcon.Font.Dispose();
                _heading.Font.Dispose();
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
