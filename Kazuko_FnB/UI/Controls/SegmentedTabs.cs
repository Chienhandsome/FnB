using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Thanh chọn tab dạng "viên thuốc" (Đăng nhập / Cơ sở dữ liệu).</summary>
    internal class SegmentedTabs : Control
    {
        private string[] _items = new string[0];
        private int _selected;

        public event EventHandler SelectedIndexChanged;

        public Color TrackColor { get; set; }
        public Color PillColor { get; set; }
        public Color SelectedTextColor { get; set; }
        public Color TextColor { get; set; }
        public Color SurfaceColor { get; set; }
        public Color BorderColor { get; set; }

        public SegmentedTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10.5f);
            TrackColor = Color.Gainsboro;
            PillColor = Color.White;
            SelectedTextColor = Color.Black;
            TextColor = Color.Gray;
            SurfaceColor = Color.White;
        }

        public string[] Items
        {
            get { return _items; }
            set
            {
                _items = value ?? new string[0];
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _items.Length || value == _selected)
                    return;
                _selected = value;
                Invalidate();
                EventHandler h = SelectedIndexChanged;
                if (h != null)
                    h(this, EventArgs.Empty);
            }
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        private RectangleF Segment(int index, float pad)
        {
            float w = (Width - pad * 2) / Math.Max(1, _items.Length);
            return new RectangleF(pad + w * index, pad, w, Height - pad * 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            float pad = 4 * DpiScale;
            for (int i = 0; i < _items.Length; i++)
                if (Segment(i, pad).Contains(e.Location))
                    SelectedIndex = i;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left)
                SelectedIndex = Math.Max(0, _selected - 1);
            else if (e.KeyCode == Keys.Right)
                SelectedIndex = Math.Min(_items.Length - 1, _selected + 1);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
            float pad = 4 * s;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(SurfaceColor);

            float inset = 1.2f * s;
            using (GraphicsPath track = Draw.Rounded(new RectangleF(inset, inset, Width - inset * 2 - 0.5f, Height - inset * 2 - 0.5f), 10 * s))
            {
                using (var b = new SolidBrush(TrackColor))
                    g.FillPath(b, track);
                if (!BorderColor.IsEmpty)
                    using (var pen = new Pen(BorderColor, 1.8f * s))
                        g.DrawPath(pen, track);
            }

            for (int i = 0; i < _items.Length; i++)
            {
                RectangleF r = Segment(i, pad);
                bool sel = i == _selected;
                if (sel)
                {
                    var shadow = new RectangleF(r.X, r.Y + 1.5f * s, r.Width, r.Height);
                    using (GraphicsPath sp = Draw.Rounded(shadow, 8 * s))
                    using (var sb = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                        g.FillPath(sb, sp);
                    using (GraphicsPath pill = Draw.Rounded(r, 8 * s))
                    {
                        using (var pb = new SolidBrush(PillColor))
                            g.FillPath(pb, pill);
                        if (!BorderColor.IsEmpty)
                            using (var pen = new Pen(BorderColor, 1.4f * s))
                                g.DrawPath(pen, pill);
                    }
                    if (Focused && ShowFocusCues)
                    {
                        using (var pen = new Pen(Draw.Mix(SelectedTextColor, PillColor, 0.5f)) { DashStyle = DashStyle.Dot })
                        using (GraphicsPath fp = Draw.Rounded(RectangleF.Inflate(r, -3 * s, -3 * s), 6 * s))
                            g.DrawPath(pen, fp);
                    }
                }
                TextRenderer.DrawText(g, _items[i], Font, Rectangle.Round(r), sel ? SelectedTextColor : TextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }
}
