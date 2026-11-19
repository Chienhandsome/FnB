using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Controls
{
    internal sealed class DocTab
    {
        public string Key;
        public string Title;
        /// <summary>Tab cố định (Truy cập nhanh) không đóng được; tab "ghim" không bị "Tắt tất cả".</summary>
        public bool Fixed;
        public bool Pinned;
        public Rectangle Bounds;
        public Rectangle CloseBounds;
    }

    /// <summary>Dải tab tài liệu: tab bo góc trên, nút ×, và hai nút bên phải "Ghim" / "Tắt tất cả".</summary>
    internal sealed class DocumentTabs : Control
    {
        private readonly List<DocTab> _tabs = new List<DocTab>();
        private string _selectedKey;
        private int _hoverTab = -1;
        private bool _hoverClose;
        private int _hoverButton = -1; // 0 = Ghim, 1 = Tắt tất cả
        private Rectangle _pinRect, _closeAllRect;
        private readonly Font _font;
        private readonly Font _boldFont;
        private readonly Font _smallFont;
        private readonly Font _iconFont;

        public event EventHandler SelectedChanged;
        /// <summary>Người dùng yêu cầu đóng tab này (bấm ×, chuột giữa); form quyết định và gọi Remove.</summary>
        public event EventHandler<string> TabCloseRequested;
        public event EventHandler CloseAllRequested;
        public event EventHandler PinToggleRequested;

        public Color BarColor { get; set; }
        public Color TabColor { get; set; }
        public Color SelectedTabColor { get; set; }
        public Color BorderColor { get; set; }
        public Color AccentColor { get; set; }
        public Color TextColor { get; set; }
        public Color MutedTextColor { get; set; }
        public Color DangerColor { get; set; }

        public DocumentTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _font = new Font("Segoe UI", 10f);
            _boldFont = new Font("Segoe UI Semibold", 10f);
            _smallFont = new Font("Segoe UI Semibold", 9.5f);
            _iconFont = new Font(Draw.IconFont, 9f);
            BarColor = Color.White;
            TabColor = Color.Gainsboro;
            SelectedTabColor = Color.White;
            BorderColor = Color.Gray;
            AccentColor = Color.OrangeRed;
            TextColor = Color.Black;
            MutedTextColor = Color.Gray;
            DangerColor = Color.Red;
        }

        private float DpiScale
        {
            get { return DeviceDpi / 96f; }
        }

        public string SelectedKey
        {
            get { return _selectedKey; }
        }

        public DocTab SelectedTab
        {
            get { return Find(_selectedKey); }
        }

        public IList<DocTab> Tabs
        {
            get { return _tabs; }
        }

        public DocTab Find(string key)
        {
            foreach (DocTab t in _tabs)
                if (t.Key == key)
                    return t;
            return null;
        }

        public DocTab Add(string key, string title, bool isFixed)
        {
            var t = new DocTab { Key = key, Title = title, Fixed = isFixed };
            _tabs.Add(t);
            ComputeLayout();
            Invalidate();
            return t;
        }

        public void Remove(string key)
        {
            int idx = _tabs.FindIndex(delegate (DocTab t) { return t.Key == key; });
            if (idx < 0)
                return;
            bool wasSelected = _selectedKey == key;
            _tabs.RemoveAt(idx);
            ComputeLayout();
            if (wasSelected)
            {
                // Chọn tab kế bên (ưu tiên tab liền trước).
                int next = Math.Max(0, Math.Min(idx - 1, _tabs.Count - 1));
                Select(_tabs.Count == 0 ? null : _tabs[next].Key);
            }
            Invalidate();
        }

        public void Select(string key)
        {
            if (_selectedKey == key)
                return;
            _selectedKey = key;
            Invalidate();
            EventHandler h = SelectedChanged;
            if (h != null)
                h(this, EventArgs.Empty);
        }

        public void SetPinned(string key, bool pinned)
        {
            DocTab t = Find(key);
            if (t == null || t.Fixed)
                return;
            t.Pinned = pinned;
            ComputeLayout();
            Invalidate();
        }

        public void Refresh(string key, string title)
        {
            DocTab t = Find(key);
            if (t != null)
            {
                t.Title = title;
                ComputeLayout();
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeLayout();
        }

        private void ComputeLayout()
        {
            if (Width <= 0 || Height <= 0)
                return;
            float s = DpiScale;

            // Hai nút bên phải
            int bh = (int)(30 * s);
            int by = Height - bh - (int)(6 * s);
            int closeAllW = TextRenderer.MeasureText("Tắt tất cả", _smallFont).Width + (int)(40 * s);
            int pinW = TextRenderer.MeasureText("Ghim", _smallFont).Width + (int)(40 * s);
            int gap = (int)(8 * s);
            int right = Width - (int)(10 * s);
            _closeAllRect = new Rectangle(right - closeAllW, by, closeAllW, bh);
            _pinRect = new Rectangle(_closeAllRect.Left - gap - pinW, by, pinW, bh);

            // Tab
            int avail = _pinRect.Left - (int)(16 * s) - (int)(10 * s);
            int minW = (int)(90 * s), maxW = (int)(210 * s);
            int[] widths = new int[_tabs.Count];
            int total = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                int tw = TextRenderer.MeasureText(_tabs[i].Title, _boldFont).Width + (int)(_tabs[i].Fixed ? 36 : 62) * 1;
                widths[i] = Math.Max(minW, Math.Min(maxW, tw));
                total += widths[i] + (int)(4 * s);
            }
            if (total > avail && _tabs.Count > 0)
            {
                // Quá chật: co đều các tab xuống, không nhỏ hơn mức đọc được.
                int each = Math.Max((int)(64 * s), avail / _tabs.Count - (int)(4 * s));
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = Math.Min(widths[i], each);
            }

            int x = (int)(10 * s);
            int ty = (int)(8 * s);
            int th = Height - ty;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Bounds = new Rectangle(x, ty, widths[i], th);
                int cs = (int)(18 * s);
                _tabs[i].CloseBounds = _tabs[i].Fixed || _tabs[i].Pinned
                    ? Rectangle.Empty
                    : new Rectangle(_tabs[i].Bounds.Right - cs - (int)(8 * s), ty + (th - cs) / 2 + (int)(1 * s), cs, cs);
                x += widths[i] + (int)(4 * s);
            }
        }

        private int HitTab(Point p)
        {
            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].Bounds.Contains(p))
                    return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int t = HitTab(e.Location);
            bool close = t >= 0 && !_tabs[t].CloseBounds.IsEmpty && _tabs[t].CloseBounds.Contains(e.Location);
            int btn = _pinRect.Contains(e.Location) ? 0 : _closeAllRect.Contains(e.Location) ? 1 : -1;
            if (t != _hoverTab || close != _hoverClose || btn != _hoverButton)
            {
                _hoverTab = t;
                _hoverClose = close;
                _hoverButton = btn;
                Cursor = (t >= 0 || btn >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverTab = -1;
            _hoverClose = false;
            _hoverButton = -1;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_pinRect.Contains(e.Location))
                {
                    EventHandler h = PinToggleRequested;
                    if (h != null)
                        h(this, EventArgs.Empty);
                    return;
                }
                if (_closeAllRect.Contains(e.Location))
                {
                    EventHandler h = CloseAllRequested;
                    if (h != null)
                        h(this, EventArgs.Empty);
                    return;
                }
            }

            int t = HitTab(e.Location);
            if (t < 0)
                return;
            DocTab tab = _tabs[t];
            bool onClose = !tab.CloseBounds.IsEmpty && tab.CloseBounds.Contains(e.Location);
            if ((e.Button == MouseButtons.Left && onClose) ||
                (e.Button == MouseButtons.Middle && !tab.Fixed && !tab.Pinned))
            {
                EventHandler<string> h = TabCloseRequested;
                if (h != null)
                    h(this, tab.Key);
            }
            else if (e.Button == MouseButtons.Left)
            {
                Select(tab.Key);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float s = DpiScale;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BarColor);

            // Đường kẻ dưới dải tab (tab đang chọn che đoạn của nó).
            using (var pen = new Pen(BorderColor, 1.6f * s))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            DocTab selected = null;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Key == _selectedKey)
                {
                    selected = _tabs[i];
                    continue;
                }
                PaintTab(g, _tabs[i], false, i == _hoverTab, s);
            }
            if (selected != null)
                PaintTab(g, selected, true, false, s);

            PaintButton(g, _pinRect, "", "Ghim", _hoverButton == 0, IsSelectedPinned(), false, s);
            PaintButton(g, _closeAllRect, "", "Tắt tất cả", _hoverButton == 1, false, true, s);
        }

        private bool IsSelectedPinned()
        {
            DocTab t = SelectedTab;
            return t != null && t.Pinned;
        }

        private void PaintTab(Graphics g, DocTab tab, bool selected, bool hover, float s)
        {
            Rectangle b = tab.Bounds;
            var r = new RectangleF(b.X + 0.5f, b.Y + 0.5f, b.Width - 1.5f, b.Height + 4);
            using (GraphicsPath p = Draw.Rounded(r, 8 * s))
            {
                Color fill = selected ? SelectedTabColor : (hover ? Draw.Mix(TabColor, SelectedTabColor, 0.4f) : TabColor);
                using (var br = new SolidBrush(fill))
                    g.FillPath(br, p);
                using (var pen = new Pen(selected ? BorderColor : Draw.Mix(BorderColor, BarColor, 0.35f), 1.6f * s))
                    g.DrawPath(pen, p);
            }
            if (selected)
            {
                // Che đường kẻ dưới và vẽ vạch nhấn ở trên.
                using (var br = new SolidBrush(SelectedTabColor))
                    g.FillRectangle(br, b.X + 1.5f, Height - 2.5f * s, b.Width - 3, 3f * s);
                using (var pen = new Pen(AccentColor, 3f * s))
                    g.DrawLine(pen, b.X + 8 * s, b.Y + 1.5f * s, b.Right - 8 * s, b.Y + 1.5f * s);
            }

            int left = b.X + (int)(14 * s);
            int right = tab.CloseBounds.IsEmpty ? b.Right - (int)(10 * s) : tab.CloseBounds.Left - (int)(4 * s);
            if (tab.Pinned)
            {
                TextRenderer.DrawText(g, "", _iconFont, new Rectangle(left, b.Y, (int)(16 * s), b.Height),
                    AccentColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                left += (int)(18 * s);
            }
            var textRect = new Rectangle(left, b.Y + (int)(1 * s), Math.Max(0, right - left), b.Height);
            TextRenderer.DrawText(g, tab.Title, selected || tab.Fixed ? _boldFont : _font, textRect,
                selected ? TextColor : MutedTextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (!tab.CloseBounds.IsEmpty)
            {
                bool hot = hover && _hoverClose;
                if (hot)
                    using (var br = new SolidBrush(Color.FromArgb(40, DangerColor)))
                        g.FillEllipse(br, tab.CloseBounds);
                TextRenderer.DrawText(g, "", _iconFont, tab.CloseBounds, hot ? DangerColor : MutedTextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void PaintButton(Graphics g, Rectangle r, string glyph, string text, bool hover, bool active, bool danger, float s)
        {
            Color accent = danger ? DangerColor : AccentColor;
            Color fill = active ? Draw.Mix(SelectedTabColor, accent, 0.18f)
                : hover ? Draw.Mix(SelectedTabColor, accent, 0.10f)
                : SelectedTabColor;
            var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.5f, r.Height - 1.5f);
            using (GraphicsPath p = Draw.Rounded(rf, 8 * s))
            {
                using (var br = new SolidBrush(fill))
                    g.FillPath(br, p);
                using (var pen = new Pen(danger ? DangerColor : BorderColor, 1.5f * s))
                    g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, glyph, _iconFont, new Rectangle(r.X + (int)(10 * s), r.Y, (int)(18 * s), r.Height),
                accent, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, text, _smallFont, new Rectangle(r.X + (int)(30 * s), r.Y, r.Width - (int)(34 * s), r.Height),
                danger ? DangerColor : TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font.Dispose();
                _boldFont.Dispose();
                _smallFont.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
