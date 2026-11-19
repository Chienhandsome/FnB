using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Dải chữ chạy ngang (ticker) từ phải sang trái, lặp vô tận. Tự dừng khi bị ẩn.</summary>
    internal sealed class MarqueeBar : Control
    {
        private readonly Timer _timer = new Timer();
        private readonly Font _font;
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private long _lastMs;
        private float _offset;
        private int _textWidth;

        public Color FromColor { get; set; }
        public Color ToColor { get; set; }
        public Color TextColor { get; set; }

        /// <summary>Tốc độ, px (ở 96 DPI) mỗi giây.</summary>
        public int Speed { get; set; }

        public MarqueeBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _font = new Font("Segoe UI", 11f, FontStyle.Bold);
            FromColor = Color.OrangeRed;
            ToColor = Color.DarkOrange;
            TextColor = Color.White;
            Speed = 50;
            _timer.Interval = 16;
            // Tính theo thời gian thực đã trôi qua (không theo số tick) để chuyển động đều, không giật khi timer trễ nhịp.
            _timer.Tick += delegate
            {
                long now = _clock.ElapsedMilliseconds;
                float dt = Math.Min(100, now - _lastMs) / 1000f;
                _lastMs = now;
                _offset += Speed * DeviceDpi / 96f * dt;
                Invalidate();
            };
        }

        public override string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;
                _textWidth = 0;
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _lastMs = _clock.ElapsedMilliseconds;
            _timer.Enabled = Visible && !DesignMode;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DeviceDpi / 96f;
            if (Width <= 0 || Height <= 0)
                return;
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, Height + 1), FromColor, ToColor, LinearGradientMode.Horizontal))
                g.FillRectangle(b, 0, 0, Width, Height);
            using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                g.DrawLine(pen, 0, 0, Width, 0);

            string text = Text ?? "";
            if (text.Length == 0)
                return;
            string unit = text + "      •      ";
            if (_textWidth <= 0)
                _textWidth = Math.Max(1, TextRenderer.MeasureText(unit, _font, new Size(int.MaxValue, Height),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);

            if (_offset >= _textWidth)
                _offset -= _textWidth;
            // Vẽ lặp lại để luôn phủ kín chiều ngang; lùi lại một đơn vị cho mép trái liền mạch.
            for (float x = -_offset; x < Width; x += _textWidth)
                TextRenderer.DrawText(g, unit, _font, new Rectangle((int)x, 0, _textWidth + 4, Height), TextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
