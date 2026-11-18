using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Ô nhập bo góc tự vẽ: TextBox không viền bên trong, icon bên trái, nút hiện mật khẩu bên phải.
    /// </summary>
    internal class InputBox : UserControl
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TextBox _box = new TextBox();
        private bool _focused;
        private bool _isPassword;
        private bool _reveal;
        private string _placeholder;
        private Rectangle _eyeRect;

        public Color FieldColor { get; set; }
        public Color BorderColor { get; set; }
        public Color FocusColor { get; set; }
        public Color TextColor { get; set; }
        public Color MutedColor { get; set; }
        public Color SurfaceColor { get; set; }
        private string _iconGlyph;

        /// <summary>Ký tự icon (font Segoe MDL2 Assets), rỗng = không icon.</summary>
        public string IconGlyph
        {
            get { return _iconGlyph; }
            set
            {
                _iconGlyph = value;
                LayoutBox();
                Invalidate();
            }
        }

        public InputBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FieldColor = Color.White;
            BorderColor = Color.Gainsboro;
            FocusColor = Color.OrangeRed;
            TextColor = Color.Black;
            MutedColor = Color.Gray;
            SurfaceColor = Color.White;

            _box.BorderStyle = BorderStyle.None;
            _box.Font = new Font("Segoe UI", 11f);
            _box.GotFocus += delegate { _focused = true; Invalidate(); };
            _box.LostFocus += delegate { _focused = false; Invalidate(); };
            _box.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
            _box.HandleCreated += delegate { ApplyPlaceholder(); };
            Controls.Add(_box);
            Cursor = Cursors.IBeam;
        }

        public TextBox Inner
        {
            get { return _box; }
        }

        [Browsable(true)]
        public override string Text
        {
            get { return _box.Text; }
            set { _box.Text = value; }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value;
                ApplyPlaceholder();
            }
        }

        public bool IsPassword
        {
            get { return _isPassword; }
            set
            {
                _isPassword = value;
                _box.UseSystemPasswordChar = value && !_reveal;
                LayoutBox();
                Invalidate();
            }
        }

        public bool ReadOnly
        {
            get { return _box.ReadOnly; }
            set { _box.ReadOnly = value; }
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        public void ApplyColors()
        {
            _box.BackColor = FieldColor;
            _box.ForeColor = TextColor;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _box.Focus();
        }

        // Đặt TextBox trong OnLayout (chạy sau bộ sắp xếp anchor mặc định) để không bị đẩy về vị trí cũ.
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutBox();
        }

        private void LayoutBox()
        {
            float s = DpiScale;
            int pad = (int)(12 * s);
            int iconW = string.IsNullOrEmpty(IconGlyph) ? 0 : (int)(28 * s);
            int eyeW = _isPassword ? (int)(36 * s) : 0;
            _box.Location = new Point(pad + iconW, Math.Max(0, (Height - _box.Height) / 2));
            _box.Width = Math.Max(10, Width - pad * 2 - iconW - eyeW);
            _eyeRect = new Rectangle(Width - pad / 2 - eyeW, 0, eyeW, Height);
        }

        private void ApplyPlaceholder()
        {
            if (_box.IsHandleCreated)
                SendMessage(_box.Handle, EM_SETCUEBANNER, (IntPtr)1, _placeholder ?? "");
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_isPassword && _eyeRect.Contains(e.Location))
            {
                _reveal = !_reveal;
                _box.UseSystemPasswordChar = !_reveal;
                Invalidate();
            }
            _box.Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _isPassword && _eyeRect.Contains(e.Location) ? Cursors.Hand : Cursors.IBeam;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
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

            using (var iconFont = new Font(Draw.IconFont, 11f))
            {
                if (!string.IsNullOrEmpty(IconGlyph))
                {
                    var iconRect = new Rectangle((int)(12 * s), 0, (int)(24 * s), Height);
                    TextRenderer.DrawText(g, IconGlyph, iconFont, iconRect, MutedColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                }
                if (_isPassword)
                {
                    // E7B3 = hiện, ED1A = ẩn
                    TextRenderer.DrawText(g, _reveal ? "" : "", iconFont, _eyeRect, MutedColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                }
            }
        }
    }
}
