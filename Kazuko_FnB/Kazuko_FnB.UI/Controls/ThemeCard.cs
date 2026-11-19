using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Theme;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Thẻ xem trước một bảng màu: tên, nhãn phong cách, đối tượng, 4 chấm màu, mô tả.
    /// Thẻ tự vẽ bằng chính màu của bảng màu đó (không theo theme đang chạy) để người dùng so sánh.
    /// Trạng thái chọn thể hiện bằng viền dày và dấu tích (không chỉ bằng màu). Bấm chuột, Enter hoặc Space = chọn.
    /// </summary>
    internal sealed class ThemeCard : Control
    {
        private readonly ThemeTokens _t;
        private bool _selected;
        private bool _inUse;
        private bool _hot;
        private readonly Font _titleFont = new Font("Segoe UI Semibold", 11.5f);
        private readonly Font _tagFont = new Font("Segoe UI Semibold", 8f);
        private readonly Font _smallFont = new Font("Segoe UI", 9f);
        private readonly Font _descFont = new Font("Segoe UI", 8.5f);
        private readonly Font _iconFont = new Font(Draw.IconFont, 10f);

        public event EventHandler Chosen;

        public ThemeCard(ThemeTokens tokens)
        {
            _t = tokens;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = tokens.Name;
            AccessibleRole = AccessibleRole.RadioButton;
        }

        public ThemeTokens Tokens
        {
            get { return _t; }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        /// <summary>Bảng màu đang được lưu làm mặc định cho người dùng này (hiện nhãn "ĐANG DÙNG").</summary>
        public bool InUse
        {
            get { return _inUse; }
            set
            {
                if (_inUse == value) return;
                _inUse = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hot = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hot = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            Raise();
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                Raise();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        private void Raise()
        {
            var h = Chosen;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;

            using (var back = new SolidBrush(Parent != null ? Parent.BackColor : _t.Background))
                g.FillRectangle(back, ClientRectangle);

            var rect = new RectangleF(2f * s, 2f * s, Width - 4f * s, Height - 4f * s);
            using (GraphicsPath p = Draw.Rounded(rect, 10f * s))
            {
                using (var b = new SolidBrush(_t.Surface))
                    g.FillPath(b, p);
                Color bc = _selected ? _t.Primary : (_hot ? Draw.Mix(_t.Border, _t.Primary, 0.5f) : _t.Border);
                using (var pen = new Pen(bc, _selected ? 2.6f * s : 1.2f * s))
                    g.DrawPath(pen, p);
            }

            int pad = (int)(14 * s);
            int x = pad;
            int w = Width - 2 * pad;
            int y = pad;
            const TextFormatFlags oneLine = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

            // Tên + dấu tích khi được chọn.
            int badge = (int)(22 * s);
            int titleH = (int)(24 * s);
            TextRenderer.DrawText(g, _t.Name, _titleFont, new Rectangle(x, y, w - badge - (int)(4 * s), titleH), _t.Text, oneLine);
            if (_selected)
            {
                var c = new Rectangle(x + w - badge, y + (titleH - badge) / 2, badge, badge);
                using (var b = new SolidBrush(_t.Primary))
                    g.FillEllipse(b, c);
                TextRenderer.DrawText(g, "", _iconFont, c, _t.OnPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            y += titleH + (int)(4 * s);

            // Nhãn phong cách (viên thuốc nền màu chính).
            int pillH = (int)(18 * s);
            Size tagSize = TextRenderer.MeasureText(g, _t.Tag ?? "", _tagFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            var pill = new Rectangle(x, y, Math.Min(w, tagSize.Width + (int)(16 * s)), pillH);
            using (GraphicsPath pp = Draw.Rounded(pill, pillH / 2f))
            using (var b = new SolidBrush(_t.Primary))
                g.FillPath(b, pp);
            TextRenderer.DrawText(g, _t.Tag ?? "", _tagFont, pill, _t.OnPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            y += pillH + (int)(6 * s);

            // Đối tượng dùng (tối đa 2 dòng).
            int audH = (int)(40 * s);
            TextRenderer.DrawText(g, _t.Audience ?? "", _smallFont, new Rectangle(x, y, w, audH), _t.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            y += audH + (int)(2 * s);

            // 4 chấm màu: chính, nhấn, nền, chữ.
            int d = (int)(16 * s);
            int gap = (int)(4 * s);
            Color[] dots = { _t.Primary, _t.Accent, _t.Background, _t.Text };
            for (int i = 0; i < dots.Length; i++)
            {
                var r = new Rectangle(x + i * (d + gap), y, d, d);
                using (var b = new SolidBrush(dots[i]))
                    g.FillEllipse(b, r);
                using (var pen = new Pen(_t.Border, 1.2f * s))
                    g.DrawEllipse(pen, r);
            }

            // Nhãn "ĐANG DÙNG" cùng hàng chấm màu, căn phải.
            if (_inUse)
            {
                const string text = "ĐANG DÙNG";
                Size ts = TextRenderer.MeasureText(g, text, _tagFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                int bw = ts.Width + (int)(12 * s);
                var ur = new Rectangle(x + w - bw, y + (d - pillH) / 2, bw, pillH);
                using (GraphicsPath up = Draw.Rounded(ur, pillH / 2f))
                using (var pen = new Pen(_t.Success, 1.4f * s))
                    g.DrawPath(pen, up);
                TextRenderer.DrawText(g, text, _tagFont, ur, _t.Success,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
            y += d + (int)(8 * s);

            // Mô tả.
            int descH = Height - pad - y;
            if (descH > 0)
                TextRenderer.DrawText(g, _t.Description ?? "", _descFont, new Rectangle(x, y, w, descH), _t.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // Vòng focus cho bàn phím.
            if (Focused && ShowFocusCues)
            {
                var fr = new RectangleF(6f * s, 6f * s, Width - 12f * s, Height - 12f * s);
                using (GraphicsPath fp = Draw.Rounded(fr, 7f * s))
                using (var pen = new Pen(_t.Primary, 1.2f * s) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, fp);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _tagFont.Dispose();
                _smallFont.Dispose();
                _descFont.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
