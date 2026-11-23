using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// TabControl vẽ theo theme cho các pane dưới lưới (Thông tin + các tab liên quan): tab chọn nổi lên với vạch
    /// màu chính ở trên, tab thường nhạt, chữ có lề đủ, không khung nét đứt. Nút cuộn khi quá nhiều tab vẫn là nút
    /// của hệ điều hành (TabControl không cho vẽ lại).
    /// </summary>
    internal sealed class ThemedTabControl : TabControl
    {
        public ThemedTabControl()
        {
            // UserPaint: tự vẽ cả dải tab (kể cả phần trống bên phải các tab) để không lộ nền xám mặc định.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DrawMode = TabDrawMode.OwnerDrawFixed;
            float s = DeviceDpi / 96f;
            SizeMode = TabSizeMode.Normal;
            Padding = new Point((int)(14 * s), (int)(5 * s));
            ItemSize = new Size(0, (int)(30 * s));
            Font = new Font("Segoe UI", 9.5f);
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemeTokens t = ThemeManager.Current;
            Graphics g = e.Graphics;
            g.Clear(t.Background);
            // Khung quanh vùng nội dung của tab (dải tab ở trên).
            Rectangle page = DisplayRectangle;
            page.Inflate(2, 2);
            // Cùng màu viền với lưới/cây chính (đen mảnh) thay vì viền cam → hết hai kiểu viền cạnh nhau.
            using (var border = new Pen(SystemColors.WindowFrame))
                g.DrawRectangle(border, page.Left, page.Top, page.Width - 1, page.Height - 1);
            for (int i = 0; i < TabCount; i++)
            {
                if (i != SelectedIndex)
                    DrawTab(g, t, i);
            }
            if (SelectedIndex >= 0)
                DrawTab(g, t, SelectedIndex); // tab chọn vẽ sau cùng để đè lên khung
        }

        private void DrawTab(Graphics g, ThemeTokens t, int index)
        {
            bool selected = index == SelectedIndex;
            Rectangle r = GetTabRect(index);
            using (var back = new SolidBrush(selected ? t.Background : Draw.Mix(t.Background, t.Primary, 0.12f)))
                g.FillRectangle(back, r);
            using (var line = new Pen(SystemColors.WindowFrame))
            {
                g.DrawLine(line, r.Left, r.Top, r.Left, r.Bottom);
                g.DrawLine(line, r.Right - 1, r.Top, r.Right - 1, r.Bottom);
                if (!selected)
                    g.DrawLine(line, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            }
            if (selected)
            {
                using (var accent = new SolidBrush(t.Primary))
                    g.FillRectangle(accent, r.Left, r.Top, r.Width, Math.Max(2, (int)(3 * DeviceDpi / 96f)));
            }
            using (var font = new Font(Font, selected ? FontStyle.Bold : FontStyle.Regular))
            {
                TextRenderer.DrawText(g, TabPages[index].Text, font, r,
                    selected ? t.Text : Draw.Mix(t.Text, t.TextMuted, 0.35f), // tab chưa chọn đủ tương phản, không nhìn như bị mờ
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }

    /// <summary>Thanh kéo đổi chiều cao pane dưới lưới; tô màu viền theo theme.</summary>
    internal sealed class PaneSplitter : Splitter
    {
        public PaneSplitter()
        {
            Dock = DockStyle.Bottom;
            Width = 5;
            Height = Math.Max(5, (int)(6 * DeviceDpi / 96f));
            MinExtra = 120;
            MinSize = 90;
            Cursor = Cursors.HSplit;
            ApplyTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            BackColor = Draw.Mix(ThemeManager.Current.Background, ThemeManager.Current.Border, 0.55f);
        }

        /// <summary>Vẽ 5 chấm ở giữa thanh để người dùng thấy đây là chỗ kéo được.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = DeviceDpi / 96f;
            int d = Math.Max(2, (int)(3 * s));
            int gap = (int)(4 * s);
            int count = 5;
            int total = count * d + (count - 1) * gap;
            int x = (Width - total) / 2;
            int y = (Height - d) / 2;
            using (var brush = new SolidBrush(Draw.Mix(BackColor, ThemeManager.Current.TextMuted, 0.7f)))
            {
                for (int i = 0; i < count; i++)
                    e.Graphics.FillEllipse(brush, x + i * (d + gap), y, d, d);
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
