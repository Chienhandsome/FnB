using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Thanh công cụ: nút vuông bo góc, icon trên chữ dưới, có vạch ngăn nhóm.</summary>
    internal sealed class IconToolbar : Control
    {
        private sealed class Entry
        {
            public MenuNode Node;
            public string Glyph;
            public Rectangle Bounds;
            public string Label;
            public Image Icon;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _hover = -1;
        private int _down = -1;
        private readonly Font _iconFont;
        private Font _textFont;
        private readonly ToolTip _tip = new ToolTip();

        public event EventHandler<MenuNode> ItemClicked;

        public Color BarColor { get; set; }
        public Color ButtonColor { get; set; }
        public Color ButtonBorderColor { get; set; }
        public Color HoverColor { get; set; }
        public Color TextColor { get; set; }
        public Color IconColor { get; set; }
        public Color SeparatorColor { get; set; }
        public Color BottomBorderColor { get; set; }

        public IconToolbar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _iconFont = new Font(Draw.IconFont, 18f);
            _textFont = new Font("Segoe UI Semibold", 9f);
            BarColor = Color.White;
            ButtonColor = Color.White;
            ButtonBorderColor = Color.Gray;
            HoverColor = Color.LightGray;
            TextColor = Color.Black;
            IconColor = Color.OrangeRed;
            SeparatorColor = Color.Gray;
            BottomBorderColor = Color.Gray;
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        public int PreferredBarHeight
        {
            get { return (int)(92 * DpiScale); }
        }

        public void SetItems(IEnumerable<MenuNode> nodes)
        {
            _entries.Clear();
            foreach (MenuNode n in nodes)
                _entries.Add(new Entry { Node = n, Glyph = n.IsSeparator ? null : GlyphMap.ForItem(n.FullTitle ?? n.Title),
                    Icon = n.IsSeparator ? null : IconCatalog.ForItem(n.FullTitle ?? n.Title) });
            _hover = _down = -1;
            ComputeLayout();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeLayout();
        }

        private void ComputeLayout()
        {
            float s = DpiScale;
            int left = (int)(10 * s);
            int top = (int)(7 * s);
            int h = Height - top - (int)(8 * s);
            int gap = (int)(6 * s);       // khoảng cách đều giữa nút - nút và nút - dải kẻ
            int sepW = (int)(2 * s);

            int buttons = 0, seps = 0;
            foreach (Entry en in _entries)
            {
                if (en.Node.IsSeparator)
                    seps++;
                else
                    buttons++;
            }

            // Mọi nút cùng một chiều rộng; cửa sổ hẹp thì thu nhỏ đều tất cả (không nhỏ hơn mức tối thiểu).
            int nominal = (int)(112 * s), min = (int)(80 * s);
            int fixedSpace = left * 2 + seps * (sepW + gap * 2) + Math.Max(0, buttons - 1) * gap;
            int w = buttons == 0 ? nominal : Math.Max(min, Math.Min(nominal, (Width - fixedSpace) / buttons));

            FitTextFont(w - (int)(6 * s));

            int x = left;
            bool first = true;
            foreach (Entry en in _entries)
            {
                if (en.Node.IsSeparator)
                {
                    if (!first)
                        x += gap;       // khoảng cách nút trước - dải kẻ bằng khoảng cách giữa các nút
                    en.Bounds = new Rectangle(x, top + (int)(6 * s), sepW, Math.Max(1, h - (int)(12 * s)));
                    x += sepW + gap;
                    first = true;      // sau dải kẻ đã có đủ khoảng cách, không cộng thêm
                    continue;
                }
                if (!first)
                    x += gap;
                first = false;
                en.Bounds = new Rectangle(x, top, w, h);
                en.Label = BalancedWrap(en.Node.Title, w - (int)(6 * s));
                x += w;
            }
        }

        /// <summary>Chọn cỡ chữ lớn nhất (9 → 8pt) để nhãn dài nhất vẫn vừa một dòng trong nút; cả thanh dùng chung một cỡ.</summary>
        private void FitTextFont(int maxWidth)
        {
            float[] sizes = { 9f, 8.5f, 8f };
            Size none = new Size(int.MaxValue, int.MaxValue);
            float chosen = sizes[sizes.Length - 1];
            foreach (float size in sizes)
            {
                int widest = 0;
                using (var f = new Font("Segoe UI Semibold", size))
                    foreach (Entry en in _entries)
                        if (!en.Node.IsSeparator)
                            widest = Math.Max(widest, TextRenderer.MeasureText(en.Node.Title ?? "", f, none,
                                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
                if (widest <= maxWidth)
                {
                    chosen = size;
                    break;
                }
            }
            if (Math.Abs(_textFont.Size - chosen) > 0.01f)
            {
                _textFont.Dispose();
                _textFont = new Font("Segoe UI Semibold", chosen);
            }
        }

        /// <summary>Chữ vừa một dòng thì giữ nguyên; không thì ngắt đôi tại khoảng trắng cho hai dòng cân nhau.</summary>
        private string BalancedWrap(string text, int maxWidth)
        {
            text = text ?? "";
            Size none = new Size(int.MaxValue, int.MaxValue);
            const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            if (TextRenderer.MeasureText(text, _textFont, none, f).Width <= maxWidth)
                return text;

            string best = text;
            int bestWidth = int.MaxValue;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ')
                    continue;
                string a = text.Substring(0, i), b = text.Substring(i + 1);
                int wa = TextRenderer.MeasureText(a, _textFont, none, f).Width;
                int wb = TextRenderer.MeasureText(b, _textFont, none, f).Width;
                int widest = Math.Max(wa, wb);
                if (widest < bestWidth)
                {
                    bestWidth = widest;
                    best = a + "\n" + b;
                }
            }
            return best;
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (!_entries[i].Node.IsSeparator && _entries[i].Bounds.Contains(p))
                    return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != _hover)
            {
                _hover = h;
                Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
                // Tooltip tên đầy đủ khi nút đang hiện tên rút gọn.
                string tip = h >= 0 ? _entries[h].Node.FullTitle : null;
                _tip.SetToolTip(this, string.IsNullOrEmpty(tip) || tip == _entries[h].Node.Title ? null : tip);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = _down = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _down = HitTest(e.Location);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int up = HitTest(e.Location);
            int was = _down;
            _down = -1;
            Invalidate();
            if (e.Button == MouseButtons.Left && up >= 0 && up == was)
            {
                EventHandler<MenuNode> h = ItemClicked;
                if (h != null)
                    h(this, _entries[up].Node);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BarColor);

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry en = _entries[i];
                if (en.Node.IsSeparator)
                {
                    using (var b = new SolidBrush(SeparatorColor))
                        g.FillRectangle(b, en.Bounds);
                    continue;
                }

                Color fill = i == _down ? Draw.Mix(HoverColor, IconColor, 0.18f)
                    : i == _hover ? HoverColor
                    : ButtonColor;
                var r = new RectangleF(en.Bounds.X + 0.5f, en.Bounds.Y + 0.5f, en.Bounds.Width - 1.5f, en.Bounds.Height - 1.5f);
                using (GraphicsPath p = Draw.Rounded(r, 9 * s))
                {
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, p);
                    using (var pen = new Pen(i == _hover ? IconColor : ButtonBorderColor, 1.6f * s))
                        g.DrawPath(pen, p);
                }

                var iconRect = new Rectangle(en.Bounds.X, en.Bounds.Y + (int)(6 * s), en.Bounds.Width, (int)(30 * s));
                if (en.Icon != null)
                {
                    // Icon màu (PNG 96px) thu về ~28px theo DPI.
                    int sz = (int)(28 * s);
                    var ir = new Rectangle(iconRect.X + (iconRect.Width - sz) / 2, iconRect.Y + (iconRect.Height - sz) / 2, sz, sz);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(en.Icon, ir);
                }
                else
                {
                    TextRenderer.DrawText(g, en.Glyph, _iconFont, iconRect, IconColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                // Vùng chữ rộng hơn ngưỡng đo (Bounds.Width - 6s) để DrawText không tự ngắt thêm dòng.
                var textRect = new Rectangle(en.Bounds.X + (int)(1 * s), en.Bounds.Y + (int)(38 * s),
                    en.Bounds.Width - (int)(2 * s), en.Bounds.Height - (int)(40 * s));
                // Vùng chữ cố định cho 2 dòng, căn giữa dọc: chữ 1 dòng và 2 dòng đều nằm đối xứng.
                TextRenderer.DrawText(g, en.Label ?? en.Node.Title, _textFont, textRect, TextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            using (var pen = new Pen(BottomBorderColor, 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _iconFont.Dispose();
                _textFont.Dispose();
                _tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
