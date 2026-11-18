using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Thanh menu ngang dùng MenuStrip chuẩn (giữ nguyên bàn phím: Alt, mũi tên, phím tắt, đọc màn hình)
    /// nhưng vẽ theo theme bằng renderer riêng.
    /// </summary>
    internal sealed class ThemedMenuStrip : MenuStrip
    {
        public ThemedMenuStrip()
        {
            GripStyle = ToolStripGripStyle.Hidden;
            Dock = DockStyle.None;
            AutoSize = false;
            ShowItemToolTips = false;
            Font = new Font("Segoe UI Semibold", 10.5f);
            Renderer = new ThemedMenuRenderer(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            BackColor = t.Surface;
            ForeColor = t.Text;
            Renderer = new ThemedMenuRenderer(t);
            Invalidate();
        }

        /// <summary>Gọi sau khi đã thêm mục: bỏ lề icon/tick và đặt kích thước theo DPI.</summary>
        public void StyleItems(float scale)
        {
            Padding = new Padding((int)(8 * scale), (int)(2 * scale), (int)(8 * scale), (int)(2 * scale));
            foreach (ToolStripItem top in Items)
            {
                top.Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale));
                var mi = top as ToolStripMenuItem;
                if (mi != null)
                    StyleDropDown(mi, scale);
            }
        }

        private static void StyleDropDown(ToolStripMenuItem item, float scale)
        {
            var dd = item.DropDown as ToolStripDropDownMenu;
            if (dd != null)
            {
                dd.ShowImageMargin = false;
                dd.ShowCheckMargin = false;
                dd.Padding = new Padding((int)(6 * scale));
                dd.DropShadowEnabled = true;
                // Bo góc cả khung dropdown.
                dd.Resize += delegate
                {
                    using (GraphicsPath gp = Draw.Rounded(new RectangleF(0, 0, dd.Width, dd.Height), 12 * scale))
                    {
                        Region old = dd.Region;
                        dd.Region = new Region(gp);
                        if (old != null)
                            old.Dispose();
                    }
                };
            }
            foreach (ToolStripItem sub in item.DropDownItems)
            {
                // Chừa chỗ bên trái cho ô icon do renderer vẽ.
                sub.Padding = new Padding((int)(10 * scale), (int)(8 * scale), (int)(14 * scale), (int)(8 * scale));
                var smi = sub as ToolStripMenuItem;
                if (smi != null)
                {
                    // Rộng = lề icon + chữ (+ phím tắt) + lề phải; không phụ thuộc cách WinForms tự đo.
                    int w = (int)(50 * scale) + TextRenderer.MeasureText(smi.Text, smi.Font ?? SystemFonts.MenuFont).Width + (int)(24 * scale);
                    if (!string.IsNullOrEmpty(smi.ShortcutKeyDisplayString))
                        w += (int)(30 * scale) + TextRenderer.MeasureText(smi.ShortcutKeyDisplayString, smi.Font ?? SystemFonts.MenuFont).Width;
                    else if (smi.DropDownItems.Count > 0)
                        w += (int)(20 * scale);
                    smi.AutoSize = false;
                    smi.Width = w;
                    smi.Height = (int)(38 * scale);
                }
                var child = sub as ToolStripMenuItem;
                if (child != null)
                    StyleDropDown(child, scale);
            }

            // Mọi mục trong cùng dropdown cùng chiều rộng (mục rộng nhất).
            int max = 0;
            foreach (ToolStripItem sub in item.DropDownItems)
                if (sub is ToolStripMenuItem)
                    max = Math.Max(max, sub.Width);
            foreach (ToolStripItem sub in item.DropDownItems)
                if (sub is ToolStripMenuItem)
                    sub.Width = max;
        }
    }

    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemeTokens _t;

        public ThemedMenuRenderer(ThemeTokens t)
            : base(new ProfessionalColorTable())
        {
            _t = t;
            RoundedEdges = false;
        }

        private Color StrongBorder
        {
            get { return Draw.Mix(_t.Border, _t.Text, _t.IsDark ? 0.30f : 0.38f); }
        }

        private static Font _iconFont;

        private static Font IconFont
        {
            get { return _iconFont ?? (_iconFont = new Font(Draw.IconFont, 11f)); }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(_t.Surface))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            if (e.ToolStrip is ToolStripDropDown)
            {
                // Đường kẻ dọc mảnh sau cột icon, giống menu gốc.
                float k = e.Graphics.DpiX / 96f;
                int x = (int)(42 * k);
                using (var pen = new Pen(Draw.Mix(_t.Border, _t.Surface, 0.2f), 1f))
                    e.Graphics.DrawLine(pen, x, (int)(8 * k), x, e.AffectedBounds.Height - (int)(8 * k));
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = e.AffectedBounds;
            if (e.ToolStrip is ToolStripDropDown)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float k = e.Graphics.DpiX / 96f;
                using (GraphicsPath gp = Draw.Rounded(new RectangleF(0.75f, 0.75f, r.Width - 2f, r.Height - 2f), 12 * k))
                using (var pen = new Pen(Draw.Mix(_t.Primary, StrongBorder, 0.45f), 1.5f))
                    e.Graphics.DrawPath(pen, gp);
            }
            else
            {
                using (var pen = new Pen(StrongBorder, 1f))
                    e.Graphics.DrawLine(pen, 0, r.Height - 1, r.Width, r.Height - 1);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Không vẽ lề icon.
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem item = e.Item;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool inDrop = item.Owner is ToolStripDropDown;
            float k = g.DpiX / 96f;

            if (inDrop)
            {
                Color[] pair = { Draw.GradFrom(_t), Draw.GradTo(_t) };
                if (item.Selected || item.Pressed)
                {
                    var rr = new RectangleF(2, 1, item.Width - 5, item.Height - 3);
                    using (GraphicsPath p = Draw.Rounded(rr, 8 * k))
                    using (var b = new LinearGradientBrush(new RectangleF(rr.X, rr.Y, rr.Width + 1, rr.Height + 1),
                        Draw.Mix(_t.Surface, pair[0], 0.20f), Draw.Mix(_t.Surface, pair[1], 0.20f), LinearGradientMode.Horizontal))
                        g.FillPath(b, p);
                }
                // Ô icon gradient bo góc bên trái.
                int bs = Math.Min((int)(26 * k), item.Height - (int)(6 * k));
                var badge = new Rectangle((int)(8 * k), (item.Height - bs) / 2, bs, bs);
                using (GraphicsPath bp = Draw.Rounded(badge, 7 * k))
                using (var bb = new LinearGradientBrush(badge, pair[0], pair[1], LinearGradientMode.ForwardDiagonal))
                    g.FillPath(bb, bp);
                TextRenderer.DrawText(g, Kazuko_FnB.Services.GlyphMap.ForItem(item.Text), IconFont, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }

            if (!item.Selected && !item.Pressed)
                return;
            Color fill = Draw.Mix(_t.Surface, _t.Primary, item.Pressed ? 0.26f : 0.16f);
            {
                var r = new RectangleF(1, 2, item.Width - 3, item.Height - 5);
                using (GraphicsPath p = Draw.Rounded(r, 8))
                {
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, p);
                    using (var pen = new Pen(Draw.Mix(_t.Primary, _t.Surface, 0.35f), 1.2f))
                        g.DrawPath(pen, p);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _t.Text : _t.TextMuted;
            var mi = e.Item as ToolStripMenuItem;
            if (e.Item.Owner is ToolStripDropDown && mi != null && e.Text == mi.Text)
            {
                // Tự đặt chỗ chữ để không đè lên ô icon, bất kể WinForms tính lề thế nào.
                float k = e.Graphics.DpiX / 96f;
                int left = (int)(50 * k);
                e.TextRectangle = new Rectangle(left, 0, Math.Max(0, e.Item.Width - left - (int)(14 * k)), e.Item.Height);
                e.TextFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _t.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Draw.Mix(_t.Border, _t.Text, 0.15f), 1f))
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }
    }
}
