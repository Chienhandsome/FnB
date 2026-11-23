using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Khung bo góc cùng kiểu với <see cref="InputBox"/> (viền mảnh, viền màu chính khi đang focus) bọc quanh một
    /// control có sẵn như DateTimePicker hoặc ComboBox, để các ô lọc cùng một giao diện. Viền gốc của control con
    /// (DateTimePicker luôn vẽ viền riêng) được giấu bằng cách đặt con lấn ra ngoài vùng khung vài pixel.
    /// </summary>
    internal class FieldFrame : Control
    {
        private readonly Control _child;
        private readonly int _hide;
        private bool _focused;

        /// <summary>Chừa bên phải cho lớp dẫn xuất tự vẽ (vd. nút lịch của DateBox), px ở 96 DPI.</summary>
        protected int RightReserve { get; set; }

        public Color FieldColor { get; set; }
        public Color BorderColor { get; set; }
        public Color FocusColor { get; set; }
        public Color SurfaceColor { get; set; }

        /// <param name="child">Control được bọc.</param>
        /// <param name="hideChildBorder">Số pixel con lấn ra ngoài để che viền gốc (0 với ComboBox phẳng).</param>
        public FieldFrame(Control child, int hideChildBorder)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _child = child;
            _hide = hideChildBorder;
            FieldColor = Color.White;
            BorderColor = Color.Gainsboro;
            FocusColor = Color.OrangeRed;
            SurfaceColor = Color.White;
            Controls.Add(child);
            child.Enter += delegate { _focused = true; Invalidate(); };
            child.Leave += delegate { _focused = false; Invalidate(); };
        }

        public void ApplyTheme(ThemeTokens t, Color surface)
        {
            SurfaceColor = surface;
            FieldColor = Color.White;
            BorderColor = Draw.StrongBorder(t);
            FocusColor = t.Primary;
            _child.BackColor = FieldColor;
            _child.ForeColor = t.Text;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            float s = DeviceDpi / 96f;
            int pad = (int)(8 * s);
            int h = _child.PreferredSize.Height > 0 && !(_child is ComboBox) ? _child.PreferredSize.Height : _child.Height;
            int y = Math.Max(-_hide, (Height - h) / 2);
            _child.SetBounds(pad - _hide, y, Math.Max(10, Width - pad - (int)(4 * s) - (int)(RightReserve * s) + _hide * 2), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DeviceDpi / 96f;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(SurfaceColor);
            float inset = 1.3f * s;
            var rect = new RectangleF(inset, inset, Width - inset * 2 - 0.5f, Height - inset * 2 - 0.5f);
            using (GraphicsPath path = Draw.Rounded(rect, 8 * s))
            {
                using (var b = new SolidBrush(FieldColor))
                    g.FillPath(b, path);
                using (var pen = new Pen(_focused ? FocusColor : BorderColor, (_focused ? 2.4f : 1.8f) * s))
                    g.DrawPath(pen, path);
            }
        }
    }
}
