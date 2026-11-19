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
            BackColor = t.Background;
            ForeColor = t.Text;
            Renderer = new ThemedMenuRenderer(t);
            Invalidate();
        }

        /// <summary>Gọi sau khi đã thêm mục: bỏ lề icon/tick và đặt kích thước theo DPI.</summary>
        public void StyleItems(float scale)
        {
            Padding = new Padding((int)(14 * scale), (int)(2 * scale), (int)(8 * scale), (int)(2 * scale));
            foreach (ToolStripItem top in Items)
            {
                top.Padding = new Padding((int)(4 * scale), (int)(6 * scale), (int)(20 * scale), (int)(6 * scale));
                top.Margin = new Padding((int)(3 * scale), 0, (int)(3 * scale), 0);
                var mi = top as ToolStripMenuItem;
                if (mi != null)
                {
                    // Ảnh trong suốt giữ chỗ cho icon nhóm; renderer vẽ icon font đè lên.
                    mi.ImageScaling = ToolStripItemImageScaling.None;
                    // Ảnh rộng hơn icon để có khoảng cách giữa viền trái và icon (icon vẽ căn giữa ảnh).
                    mi.Image = Placeholder((int)(36 * scale), (int)(18 * scale));
                    mi.ImageAlign = ContentAlignment.MiddleLeft;
                    mi.TextImageRelation = TextImageRelation.ImageBeforeText;
                    // Rê chuột vào mục là mở menu thả xuống (MenuStrip chuẩn chỉ mở khi bấm).
                    mi.MouseEnter += OpenOnHover;
                    StyleDropDown(mi, scale, Font);
                }
            }
        }

        // ---- tự đóng menu khi chuột rời khỏi mục và toàn bộ menu thả xuống (kể cả menu con)

        private readonly Timer _leaveTimer = new Timer { Interval = 150 };
        private int _outsideTicks;
        private bool _leaveHooked;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_leaveHooked)
            {
                _leaveHooked = true;
                _leaveTimer.Tick += LeaveTimerTick;
            }
            _leaveTimer.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _leaveTimer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _leaveTimer.Stop();
                _leaveTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void LeaveTimerTick(object sender, EventArgs e)
        {
            ToolStripMenuItem open = null;
            foreach (ToolStripItem it in Items)
            {
                var mi = it as ToolStripMenuItem;
                if (mi != null && mi.HasDropDownItems && mi.DropDown.Visible)
                {
                    open = mi;
                    break;
                }
            }
            if (open == null)
            {
                _outsideTicks = 0;
                return;
            }

            Point p = Control.MousePosition;
            bool inside = RectangleToScreen(open.Bounds).Contains(p) || InsideDropDownChain(open, p);
            if (inside)
            {
                _outsideTicks = 0;
                return;
            }
            // Chờ 2 nhịp (~0.3 giây) để kịp di chuột từ mục xuống menu mà không bị đóng.
            if (++_outsideTicks >= 2)
            {
                _outsideTicks = 0;
                open.HideDropDown();
            }
        }

        private static bool InsideDropDownChain(ToolStripDropDownItem item, Point screenPoint)
        {
            if (!item.DropDown.Visible)
                return false;
            if (item.DropDown.Bounds.Contains(screenPoint))
                return true;
            foreach (ToolStripItem sub in item.DropDown.Items)
            {
                var child = sub as ToolStripDropDownItem;
                if (child != null && InsideDropDownChain(child, screenPoint))
                    return true;
            }
            return false;
        }

        private void OpenOnHover(object sender, EventArgs e)
        {
            var hovered = sender as ToolStripMenuItem;
            if (hovered == null || !hovered.HasDropDownItems || hovered.DropDown.Visible)
                return;
            foreach (ToolStripItem other in Items)
            {
                var om = other as ToolStripMenuItem;
                if (om != null && om != hovered && om.DropDown.Visible)
                    om.HideDropDown();
            }
            hovered.ShowDropDown();
        }

        private static void StyleDropDown(ToolStripMenuItem item, float scale, Font font)
        {
            var dd = item.DropDown as ToolStripDropDownMenu;
            if (dd != null)
            {
                // Dùng lề ảnh chuẩn của WinForms (ảnh trong suốt giữ chỗ) để nó tự tính chỗ chữ/phím tắt/mũi tên;
                // ô icon gradient do renderer vẽ đè lên đúng chỗ ảnh.
                dd.ShowImageMargin = true;
                dd.ShowCheckMargin = false;
                dd.Padding = new Padding((int)(6 * scale));
                dd.DropShadowEnabled = true;
                dd.Font = font;
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
                sub.Padding = new Padding((int)(6 * scale), (int)(5 * scale), (int)(14 * scale), (int)(5 * scale));
                var smi = sub as ToolStripMenuItem;
                if (smi != null)
                {
                    smi.Font = font;
                    smi.ImageScaling = ToolStripItemImageScaling.None;
                    smi.Image = Placeholder((int)(26 * scale));
                    // Khoảng cách chữ - phím tắt.
                    smi.ShortcutKeyDisplayString = smi.ShortcutKeys == Keys.None ? null : smi.ShortcutKeyDisplayString;
                    StyleDropDown(smi, scale, font);
                }
            }
        }

        private static readonly System.Collections.Generic.Dictionary<long, Bitmap> Placeholders =
            new System.Collections.Generic.Dictionary<long, Bitmap>();

        /// <summary>Ảnh trong suốt giữ chỗ cho ô icon (dùng chung theo kích thước).</summary>
        private static Bitmap Placeholder(int size)
        {
            return Placeholder(size, size);
        }

        private static Bitmap Placeholder(int width, int height)
        {
            long key = ((long)width << 32) | (uint)height;
            Bitmap bmp;
            if (!Placeholders.TryGetValue(key, out bmp))
            {
                bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                Placeholders[key] = bmp;
            }
            return bmp;
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
            get { return Draw.Mix(_t.Border, _t.Text, 0.38f); }
        }

        private static Font _iconFont;

        private static Font _topIconFont;

        private static Font TopIconFont
        {
            get { return _topIconFont ?? (_topIconFont = new Font(Draw.IconFont, 12f)); }
        }

        private static Font IconFont
        {
            get { return _iconFont ?? (_iconFont = new Font(Draw.IconFont, 11f)); }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(e.ToolStrip is ToolStripDropDown ? _t.Surface : _t.Background))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
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
            // Lề icon: chỉ một đường kẻ dọc mảnh ở mép phải, giống menu gốc.
            Rectangle m = e.AffectedBounds;
            float k = e.Graphics.DpiX / 96f;
            using (var pen = new Pen(Draw.Mix(_t.Border, _t.Surface, 0.2f), 1f))
                e.Graphics.DrawLine(pen, m.Right, m.Top + (int)(8 * k), m.Right, m.Bottom - (int)(8 * k));
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (!(e.Item.Owner is ToolStripDropDown))
            {
                // Thanh menu ngang: icon nhóm (theo tên nhóm) bằng font, màu chủ đạo.
                Image groupIcon = Kazuko_FnB.Services.IconCatalog.ForGroup(e.Item.Text);
                if (groupIcon != null)
                {
                    bool hotTop = e.Item.Selected || e.Item.Pressed;
                    Graphics tg = e.Graphics;
                    tg.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle ar = e.ImageRectangle;
                    int sz = Math.Min(ar.Height, (int)(20 * tg.DpiX / 96f));
                    var ir = new Rectangle(ar.X + (ar.Width - sz) / 2, ar.Y + (ar.Height - sz) / 2, sz, sz);
                    if (hotTop)
                    {
                        // Nền gradient cam khi hover: lót tròn trắng sau icon để icon màu vẫn nổi.
                        Rectangle halo = Rectangle.Inflate(ir, (int)(3 * tg.DpiX / 96f), (int)(3 * tg.DpiX / 96f));
                        using (var hb = new SolidBrush(Color.White))
                            tg.FillEllipse(hb, halo);
                    }
                    tg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    tg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    tg.DrawImage(groupIcon, ir);
                    return;
                }
                string glyph = Kazuko_FnB.Services.GlyphMap.ForGroup(e.Item.Text);
                if (!string.IsNullOrEmpty(glyph))
                    TextRenderer.DrawText(e.Graphics, glyph, TopIconFont, e.ImageRectangle,
                        e.Item.Selected || e.Item.Pressed ? Color.White : _t.Primary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }
            // Ô icon gradient bo góc, đặt giữa vùng ảnh giữ chỗ.
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = g.DpiX / 96f;
            Rectangle r = e.ImageRectangle;
            int bs = Math.Min(r.Width, r.Height);
            var badge = new Rectangle(r.X + (r.Width - bs) / 2, r.Y + (r.Height - bs) / 2, bs, bs);
            Image itemIcon = Kazuko_FnB.Services.IconCatalog.ForItem(e.Item.Text);
            if (itemIcon != null)
            {
                // Icon màu trực tiếp trên ô trắng viền nhạt.
                using (GraphicsPath tp = Draw.Rounded(badge, 7 * k))
                {
                    using (var tb = new SolidBrush(Color.White))
                        g.FillPath(tb, tp);
                    using (var tpen = new Pen(Draw.Mix(_t.Border, Color.White, 0.4f), 1.2f))
                        g.DrawPath(tpen, tp);
                }
                int isz = (int)(bs * 0.78f);
                var ir = new Rectangle(badge.X + (bs - isz) / 2, badge.Y + (bs - isz) / 2, isz, isz);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(itemIcon, ir);
                return;
            }
            using (GraphicsPath bp = Draw.Rounded(badge, 7 * k))
            using (var bb = new LinearGradientBrush(badge, Draw.GradFrom(_t), Draw.GradTo(_t), LinearGradientMode.ForwardDiagonal))
                g.FillPath(bb, bp);
            TextRenderer.DrawText(g, Kazuko_FnB.Services.GlyphMap.ForItem(e.Item.Text), IconFont, badge, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
                return;
            }

            // Thanh menu ngang: luôn có viền bo góc; hover / mở thì đổi màu nền và viền.
            bool hot = item.Selected || item.Pressed;
            {
                var r = new RectangleF(1, 2, item.Width - 3, item.Height - 5);
                using (GraphicsPath p = Draw.Rounded(r, 8 * k))
                {
                    if (hot)
                    {
                        // Hover / đang mở: gradient cam đậm, chữ và icon trắng.
                        using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width + 1, r.Height + 1),
                            Draw.GradFrom(_t), Draw.GradTo(_t), LinearGradientMode.Horizontal))
                            g.FillPath(b, p);
                        using (var pen = new Pen(Draw.Darken(_t.Primary, 0.1f), 1.6f))
                            g.DrawPath(pen, p);
                    }
                    else
                    {
                        using (var b = new SolidBrush(Color.White))
                            g.FillPath(b, p);
                        using (var pen = new Pen(Draw.Mix(_t.Border, _t.Primary, 0.45f), 1.4f))
                            g.DrawPath(pen, p);
                    }
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _t.Text : _t.TextMuted;
            if (!(e.Item.Owner is ToolStripDropDown) && (e.Item.Selected || e.Item.Pressed))
                e.TextColor = Color.White;
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
