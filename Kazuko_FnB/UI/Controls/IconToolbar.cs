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
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _hover = -1;
        private int _down = -1;
        private readonly Font _iconFont;
        private readonly Font _textFont;

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
            get { return (int)(98 * DpiScale); }
        }

        public void SetItems(IEnumerable<MenuNode> nodes)
        {
            _entries.Clear();
            foreach (MenuNode n in nodes)
                _entries.Add(new Entry { Node = n, Glyph = n.IsSeparator ? null : GlyphMap.ForItem(n.Title) });
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
            int x = (int)(10 * s);
            int top = (int)(7 * s);
            int h = Height - top - (int)(8 * s);
            int maxText = (int)(104 * s);
            foreach (Entry en in _entries)
            {
                if (en.Node.IsSeparator)
                {
                    en.Bounds = new Rectangle(x + (int)(4 * s), top + (int)(6 * s), (int)(2 * s), Math.Max(1, h - (int)(12 * s)));
                    x += (int)(14 * s);
                    continue;
                }
                Size ts = TextRenderer.MeasureText(en.Node.Title, _textFont, new Size(maxText, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                // Rộng hơn chữ đo được một chút để chữ không bị ngắt thành 3 dòng.
                int w = Math.Max((int)(86 * s), Math.Min(ts.Width + (int)(34 * s), (int)(136 * s)));
                en.Bounds = new Rectangle(x, top, w, h);
                x += w + (int)(8 * s);
            }
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

                var iconRect = new Rectangle(en.Bounds.X, en.Bounds.Y + (int)(8 * s), en.Bounds.Width, (int)(32 * s));
                TextRenderer.DrawText(g, en.Glyph, _iconFont, iconRect, IconColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var textRect = new Rectangle(en.Bounds.X + (int)(4 * s), en.Bounds.Y + (int)(42 * s),
                    en.Bounds.Width - (int)(8 * s), en.Bounds.Height - (int)(44 * s));
                TextRenderer.DrawText(g, en.Node.Title, _textFont, textRect, TextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak |
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
            }
            base.Dispose(disposing);
        }
    }
}
