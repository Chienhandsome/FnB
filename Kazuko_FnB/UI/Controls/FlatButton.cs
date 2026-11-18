using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Nút bo góc tự vẽ. Kế thừa Button để giữ bàn phím (Enter/Space, AcceptButton).</summary>
    internal class FlatButton : Button
    {
        private bool _hover;
        private bool _down;

        public Color FillColor { get; set; }
        public Color TextColor { get; set; }
        /// <summary>Màu nền phía sau (lộ ra ở góc bo).</summary>
        public Color SurfaceColor { get; set; }
        public int Radius { get; set; }
        /// <summary>Màu viền; Empty = tự lấy màu nền tối hơn.</summary>
        public Color BorderColor { get; set; }

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            FillColor = Color.FromArgb(234, 102, 24);
            TextColor = Color.White;
            SurfaceColor = Color.White;
            Radius = 8;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _down = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(SurfaceColor);

            Color fill = !Enabled ? Draw.Mix(FillColor, SurfaceColor, 0.5f)
                : _down ? Draw.Darken(FillColor, 0.18f)
                : _hover ? Draw.Darken(FillColor, 0.08f)
                : FillColor;

            float s = DeviceDpi / 96f;
            float inset = 1.2f * s;
            using (GraphicsPath path = Draw.Rounded(new RectangleF(inset, inset, Width - inset * 2 - 0.5f, Height - inset * 2 - 0.5f), Radius))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                Color border = BorderColor.IsEmpty ? Draw.Darken(fill, 0.28f) : BorderColor;
                using (var pen = new Pen(border, 1.8f * s))
                    g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                using (var pen = new Pen(Draw.Mix(TextColor, fill, 0.35f), 1f) { DashStyle = DashStyle.Dot })
                using (GraphicsPath inner = Draw.Rounded(new RectangleF(3.5f, 3.5f, Width - 8f, Height - 8f), Math.Max(0, Radius - 3)))
                    g.DrawPath(pen, inner);
            }
        }
    }
}
