using System.Drawing;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Thanh trạng thái dưới cùng: lời chào + giờ, màn đang xem, đường dẫn CSDL.</summary>
    internal sealed class AppStatusBar : Control
    {
        private string _left = "";
        private string _middle = "";
        private string _right = "";
        private readonly Font _font;
        private readonly Font _iconFont;

        public Color BarColor { get; set; }
        public Color BorderColor { get; set; }
        public Color TextColor { get; set; }
        public Color MutedColor { get; set; }

        public AppStatusBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _font = new Font("Segoe UI", 9f);
            _iconFont = new Font(Draw.IconFont, 9f);
            BarColor = Color.White;
            BorderColor = Color.Gray;
            TextColor = Color.Black;
            MutedColor = Color.Gray;
        }

        public string LeftText
        {
            get { return _left; }
            set
            {
                if (_left != value)
                {
                    _left = value;
                    Invalidate();
                }
            }
        }

        public string MiddleText
        {
            get { return _middle; }
            set
            {
                if (_middle != value)
                {
                    _middle = value;
                    Invalidate();
                }
            }
        }

        public string RightText
        {
            get { return _right; }
            set
            {
                if (_right != value)
                {
                    _right = value;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DeviceDpi / 96f;
            g.Clear(BarColor);
            using (var pen = new Pen(BorderColor, 1.4f * s))
                g.DrawLine(pen, 0, 0, Width, 0);

            int pad = (int)(12 * s);
            int rightW = (int)(Width * 0.42);
            int leftW = (int)(Width * 0.32);
            const TextFormatFlags f = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                      TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, _left, _font, new Rectangle(pad, 0, leftW, Height), TextColor, f | TextFormatFlags.Left);
            TextRenderer.DrawText(g, _middle, _font, new Rectangle(pad + leftW, 0, Width - leftW - rightW - pad * 2, Height),
                TextColor, f | TextFormatFlags.HorizontalCenter);

            var rr = new Rectangle(Width - rightW - pad, 0, rightW, Height);
            TextRenderer.DrawText(g, "", _iconFont, new Rectangle(rr.X, 0, (int)(18 * s), Height), MutedColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _right, _font, new Rectangle(rr.X + (int)(22 * s), 0, rr.Width - (int)(22 * s), Height),
                MutedColor, f | TextFormatFlags.Right | TextFormatFlags.PathEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
