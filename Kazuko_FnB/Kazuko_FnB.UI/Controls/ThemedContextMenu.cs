using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Menu chuột phải dùng chung cho mọi màn hình: cùng renderer với menu chính (<see cref="ThemedMenuRenderer"/>), khung bo góc,
    /// tự đổi màu khi đổi theme. Dùng AddItem/AddSeparator thay vì tự tạo ToolStripMenuItem rồi tô màu.
    /// Gán vào control bằng <c>control.ContextMenuStrip = menu</c>.
    /// </summary>
    internal sealed class ThemedContextMenu : ContextMenuStrip
    {
        public ThemedContextMenu()
        {
            float s = DeviceDpi / 96f;
            Font = new Font("Segoe UI", 10f);
            ShowImageMargin = false;
            ShowCheckMargin = false;
            DropShadowEnabled = true;
            Padding = new Padding((int)(6 * s));
            Renderer = new ThemedMenuRenderer(ThemeManager.Current);
            ThemeManager.ThemeChanged += OnThemeChanged;
            Resize += delegate { ApplyRoundedRegion(); };
        }

        /// <summary>Thêm một mục. onClick có thể null (gán sau).</summary>
        public ToolStripMenuItem AddItem(string text, EventHandler onClick)
        {
            return AddItem(text, Keys.None, onClick);
        }

        /// <summary>Thêm một mục kèm phím tắt hiển thị (shortcut = Keys.None thì không hiện).</summary>
        public ToolStripMenuItem AddItem(string text, Keys shortcut, EventHandler onClick)
        {
            float s = DeviceDpi / 96f;
            var item = new ToolStripMenuItem(text)
            {
                Font = Font,
                Padding = new Padding((int)(6 * s), (int)(5 * s), (int)(14 * s), (int)(5 * s))
            };
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
                item.ShowShortcutKeys = true;
            }
            if (onClick != null)
                item.Click += onClick;
            Items.Add(item);
            return item;
        }

        public void AddSeparator()
        {
            Items.Add(new ToolStripSeparator());
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Renderer = new ThemedMenuRenderer(ThemeManager.Current);
            Invalidate();
        }

        // Bo góc cả khung (giống menu thả xuống của menu chính).
        private void ApplyRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;
            float s = DeviceDpi / 96f;
            using (GraphicsPath gp = Draw.Rounded(new RectangleF(0, 0, Width, Height), 12 * s))
            {
                Region old = Region;
                Region = new Region(gp);
                if (old != null)
                    old.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
