using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Thẻ "Truy cập nhanh": tiêu đề nhóm (icon + tên) và các hàng chức năng bấm được.
    /// Vẽ toàn bộ trong một control để bo góc, viền đậm và đổi màu theo theme.
    /// </summary>
    internal sealed class QuickAccessCard : Control
    {
        private readonly List<MenuNode> _items = new List<MenuNode>();
        private int _hover = -1;
        private readonly Font _titleFont;
        private readonly Font _rowFont;
        private readonly Font _iconFont;
        private readonly Font _badgeFont;

        public event EventHandler<MenuNode> ItemClicked;

        public string Title { get; set; }
        public string Glyph { get; set; }

        public Color SurfaceColor { get; set; }
        public Color PageColor { get; set; }
        public Color HeaderColor { get; set; }
        public Color BorderColor { get; set; }
        public Color RowColor { get; set; }
        public Color RowHoverColor { get; set; }
        public Color TextColor { get; set; }
        public Color AccentColor { get; set; }

        /// <summary>Hai đầu dải màu riêng của thẻ (gradient tiêu đề, badge, viền).</summary>
        public Color GradientFrom { get; set; }
        public Color GradientTo { get; set; }

        /// <summary>Hệ số nén chiều cao hàng (1 = đầy đủ) để vừa màn hình, không cần cuộn dọc.</summary>
        public float Density { get; set; }

        public QuickAccessCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _titleFont = new Font("Segoe UI Semibold", 12.5f);
            _rowFont = new Font("Segoe UI Semibold", 10.5f);
            _iconFont = new Font(Draw.IconFont, 13f);
            _badgeFont = new Font(Draw.IconFont, 11f);
            SurfaceColor = Color.White;
            PageColor = Color.White;
            HeaderColor = Color.Khaki;
            BorderColor = Color.Gray;
            RowColor = Color.White;
            RowHoverColor = Color.LightGray;
            TextColor = Color.Black;
            AccentColor = Color.OrangeRed;
            GradientFrom = Color.OrangeRed;
            GradientTo = Color.DeepPink;
            Density = 1f;
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        public void SetItems(IEnumerable<MenuNode> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _hover = -1;
            Invalidate();
        }

        /// <summary>Chiều cao cần để hiện hết các hàng.</summary>
        public int RequiredHeight(float s)
        {
            return HeaderHeight(s) + TopGap(s) + _items.Count * (RowHeight(s) + RowGap(s)) + (int)(6 * s * Density);
        }

        private int HeaderHeight(float s)
        {
            return (int)(48 * s * (0.75f + 0.25f * Density));
        }

        private int TopGap(float s)
        {
            return (int)(14 * s * Density);
        }

        private int RowGap(float s)
        {
            return (int)(8 * s * Density);
        }

        private int RowHeight(float s)
        {
            return (int)(44 * s * Density);
        }

        private Rectangle RowRect(int i, float s)
        {
            int pad = (int)(14 * s);
            int y = HeaderHeight(s) + TopGap(s) + i * (RowHeight(s) + RowGap(s));
            return new Rectangle(pad, y, Width - pad * 2, RowHeight(s));
        }

        private int HitRow(Point p)
        {
            float s = DpiScale;
            for (int i = 0; i < _items.Count; i++)
                if (RowRect(i, s).Contains(p))
                    return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitRow(e.Location);
            if (h != _hover)
            {
                _hover = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;
            int h = HitRow(e.Location);
            if (h >= 0)
            {
                EventHandler<MenuNode> handler = ItemClicked;
                if (handler != null)
                    handler(this, _items[h]);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(PageColor);

            float radius = 14 * s;
            var outer = new RectangleF(1.2f * s, 1.2f * s, Width - 2.4f * s - 0.5f, Height - 2.4f * s - 0.5f);
            using (GraphicsPath card = Draw.Rounded(outer, radius))
            {
                using (var b = new SolidBrush(SurfaceColor))
                    g.FillPath(b, card);

                // Dải tiêu đề gradient: cắt theo hình thẻ để góc trên bo đúng.
                int hh = HeaderHeight(s);
                var old = g.Clip;
                g.SetClip(card, CombineMode.Intersect);
                using (var lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), hh + 1),
                    GradientFrom, GradientTo, LinearGradientMode.Horizontal))
                    g.FillRectangle(lb, 0, 0, Width, hh);
                // Vệt sáng phía trên cho cảm giác bóng.
                using (var gloss = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), hh / 2 + 1),
                    Color.FromArgb(70, Color.White), Color.FromArgb(0, Color.White), LinearGradientMode.Vertical))
                    g.FillRectangle(gloss, 0, 0, Width, hh / 2);
                g.Clip = old;

                // Viền thẻ: gradient cùng tông, đậm.
                using (var pen = new Pen(Draw.Mix(GradientFrom, BorderColor, 0.25f), 2.2f * s))
                    g.DrawPath(pen, card);
            }

            // Tiêu đề: icon (trong vòng tròn mờ) + tên, căn giữa
            string title = Title ?? "";
            Size ts = TextRenderer.MeasureText(title, _titleFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int iconW = (int)(34 * s);
            int totalW = iconW + ts.Width;
            int x = Math.Max((int)(12 * s), (Width - totalW) / 2);
            int hdr = HeaderHeight(s);
            int dia = (int)(28 * s);
            var circle = new Rectangle(x, (hdr - dia) / 2, dia, dia);
            using (var cb = new SolidBrush(Color.FromArgb(70, Color.White)))
                g.FillEllipse(cb, circle);
            TextRenderer.DrawText(g, Glyph ?? "", _iconFont, circle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            var titleRect = new Rectangle(x + iconW, 0, Math.Max(0, Width - x - iconW - (int)(10 * s)), hdr);
            TextRenderer.DrawText(g, title, _titleFont, titleRect, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            for (int i = 0; i < _items.Count; i++)
            {
                Rectangle r = RowRect(i, s);
                var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.5f, r.Height - 1.5f);
                bool hot = i == _hover;
                using (GraphicsPath p = Draw.Rounded(rf, 9 * s))
                {
                    if (hot)
                    {
                        // Hover: nền gradient nhẹ + quầng sáng quanh viền.
                        using (var hb = new LinearGradientBrush(r, Draw.Mix(SurfaceColor, GradientFrom, 0.22f),
                            Draw.Mix(SurfaceColor, GradientTo, 0.22f), LinearGradientMode.Horizontal))
                            g.FillPath(hb, p);
                        using (var glow = new Pen(Color.FromArgb(70, GradientFrom), 5f * s))
                            g.DrawPath(glow, p);
                        using (var pen = new Pen(GradientFrom, 1.8f * s))
                            g.DrawPath(pen, p);
                    }
                    else
                    {
                        using (var b = new SolidBrush(RowColor))
                            g.FillPath(b, p);
                        using (var pen = new Pen(Draw.Mix(BorderColor, GradientFrom, 0.35f), 1.4f * s))
                            g.DrawPath(pen, p);
                    }
                }

                int bs = Math.Min((int)(30 * s), r.Height - (int)(6 * s));
                var badge = new Rectangle(r.X + (int)(10 * s), r.Y + (r.Height - bs) / 2, bs, bs);
                using (GraphicsPath bp = Draw.Rounded(badge, 8 * s))
                using (var bb = new LinearGradientBrush(badge, GradientFrom, GradientTo, LinearGradientMode.ForwardDiagonal))
                    g.FillPath(bb, bp);
                TextRenderer.DrawText(g, GlyphMap.ForItem(_items[i].Title), _badgeFont, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var textRect = new Rectangle(badge.Right + (int)(12 * s), r.Y, r.Right - badge.Right - (int)(20 * s), r.Height);
                TextRenderer.DrawText(g, _items[i].Title, _rowFont, textRect, TextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _rowFont.Dispose();
                _iconFont.Dispose();
                _badgeFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
