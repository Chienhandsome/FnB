using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Ô thống kê nhỏ ở cuối màn Truy cập nhanh: icon + tên chỉ tiêu + giá trị ("—" khi chưa có số liệu).</summary>
    internal sealed class StatTile : Control
    {
        private readonly Font _captionFont;
        private readonly Font _valueFont;
        private string _value = "—";

        public string Caption { get; set; }
        public Image Icon { get; set; }

        public Color PageColor { get; set; }
        public Color SurfaceColor { get; set; }
        public Color BorderColor { get; set; }
        public Color CaptionColor { get; set; }
        public Color ValueColor { get; set; }
        public Color AccentFrom { get; set; }
        public Color AccentTo { get; set; }

        /// <summary>Giá trị hiển thị; để trống/null thì hiện "—". Số liệu thật sẽ gán sau.</summary>
        public string Value
        {
            get { return _value; }
            set
            {
                _value = string.IsNullOrEmpty(value) ? "—" : value;
                Invalidate();
            }
        }

        public StatTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _captionFont = new Font("Segoe UI", 10f, FontStyle.Bold);
            _valueFont = new Font("Segoe UI", 14f, FontStyle.Bold);
            PageColor = Color.White;
            SurfaceColor = Color.White;
            BorderColor = Color.Gray;
            CaptionColor = Color.Black;
            ValueColor = Color.Black;
            AccentFrom = Color.Orange;
            AccentTo = Color.OrangeRed;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DeviceDpi / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(PageColor);

            var r = new RectangleF(1.2f * s, 1.2f * s, Width - 2.4f * s - 0.5f, Height - 2.4f * s - 0.5f);
            using (GraphicsPath p = Draw.Rounded(r, 12 * s))
            {
                using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height + 1),
                    SurfaceColor, Draw.Mix(SurfaceColor, AccentFrom, 0.10f), LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                using (var pen = new Pen(BorderColor, 1.8f * s))
                    g.DrawPath(pen, p);
            }

            int pad = (int)(14 * s);
            int isz = (int)(30 * s);
            if (Icon != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(Icon, new Rectangle(pad, (Height - isz) / 2, isz, isz));
            }

            int tx = pad + isz + (int)(12 * s);
            int tw = Math.Max(0, Width - tx - pad);
            const TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.SingleLine |
                                      TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int capH = (int)(20 * s);
            int valH = (int)(26 * s);
            int top = (Height - capH - valH - (int)(2 * s)) / 2;
            TextRenderer.DrawText(g, Caption ?? "", _captionFont, new Rectangle(tx, top, tw, capH), CaptionColor, f | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, _value, _valueFont, new Rectangle(tx, top + capH + (int)(2 * s), tw, valH), ValueColor, f | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _captionFont.Dispose();
                _valueFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
