using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Tô màu cho cả cây control của một dialog/màn hình từ token của theme (không hard-code màu ở màn nghiệp vụ).
    /// Dùng thẻ chuỗi trong Control.Tag để chọn kiểu:
    ///   "surface" (Panel/Label nền thẻ), "muted" (chữ phụ), "primary" (nút chính), "danger" (nút nguy hiểm),
    ///   "header" (thanh tiêu đề nền Primary), "footer" (thanh nút cuối).
    /// Gọi lại khi ThemeManager.ThemeChanged.
    /// </summary>
    internal static class ThemeApplier
    {
        public static void Apply(Control root, ThemeTokens t)
        {
            ApplyOne(root, t);
            // InputBox tự quản TextBox bên trong (không viền); không tô đè lên nó.
            // FieldFrame/DateBox cũng tự quản control con (khung bo góc + ô nhập không viền).
            if (root is InputBox || root is FieldFrame)
                return;
            foreach (Control c in root.Controls)
                Apply(c, t);
        }

        private static string TagOf(Control c)
        {
            return c.Tag as string;
        }

        private static void ApplyOne(Control c, ThemeTokens t)
        {
            string tag = TagOf(c);
            Color surface = t.Surface;
            Color strong = Draw.StrongBorder(t);

            var btn = c as FlatButton;
            if (btn != null)
            {
                ApplyButton(btn, t, tag);
                return;
            }

            // Nút duy nhất của app là FlatButton; Button thô (nếu lỡ có) vẫn được tô theo theme thay vì để màu hệ thống.
            var plain = c as Button;
            if (plain != null)
            {
                plain.FlatStyle = FlatStyle.Flat;
                plain.BackColor = surface;
                plain.ForeColor = t.Text;
                plain.FlatAppearance.BorderColor = strong;
                return;
            }

            var input = c as InputBox;
            if (input != null)
            {
                ApplyInput(input, t, input.Parent != null ? input.Parent.BackColor : t.Background, strong);
                return;
            }

            var grid = c as DataGridView;
            if (grid != null)
            {
                ApplyGrid(grid, t);
                return;
            }

            var tree = c as TreeView;
            if (tree != null)
            {
                tree.BackColor = surface;
                tree.ForeColor = t.Text;
                tree.LineColor = strong;
                tree.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var list = c as ListView;
            if (list != null)
            {
                list.BackColor = surface;
                list.ForeColor = t.Text;
                list.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var text = c as TextBoxBase;
            if (text != null)
            {
                text.BackColor = text.ReadOnly ? Draw.Mix(surface, t.Background, 0.5f) : surface;
                text.ForeColor = t.Text;
                text.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var combo = c as ComboBox;
            if (combo != null)
            {
                combo.BackColor = surface;
                combo.ForeColor = t.Text;
                combo.FlatStyle = FlatStyle.Flat;
                return;
            }

            var num = c as NumericUpDown;
            if (num != null)
            {
                num.BackColor = surface;
                num.ForeColor = t.Text;
                num.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (c is CheckBox || c is RadioButton)
            {
                c.BackColor = Color.Transparent;
                c.ForeColor = t.Text;
                return;
            }

            if (c is Label)
            {
                c.BackColor = Color.Transparent;
                c.ForeColor = tag == "muted" ? t.TextMuted : tag == "header" ? t.OnPrimary : t.Text;
                return;
            }

            if (c is PictureBox)
            {
                c.BackColor = surface;
                return;
            }

            if (c is Panel || c is Form || c is UserControl || c is SplitContainer || c is TableLayoutPanel || c is FlowLayoutPanel)
            {
                switch (tag)
                {
                    case "surface":
                        c.BackColor = surface;
                        break;
                    case "header":
                        c.BackColor = t.Primary;
                        c.ForeColor = t.OnPrimary;
                        break;
                    case "footer":
                        c.BackColor = Draw.Mix(t.Background, t.Primary, 0.06f);
                        break;
                    default:
                        c.BackColor = t.Background;
                        break;
                }
                if (!(c is Form))
                    c.ForeColor = t.Text;
            }
        }

        public static void ApplyButton(FlatButton b, ThemeTokens t, string tag)
        {
            b.SurfaceColor = b.Parent != null ? b.Parent.BackColor : t.Background;
            if (tag == "primary")
            {
                b.FillColor = t.Primary;
                b.TextColor = t.OnPrimary;
                b.BorderColor = Color.Empty;
            }
            else if (tag == "danger")
            {
                b.FillColor = t.Danger;
                b.TextColor = t.Surface;
                b.BorderColor = Color.Empty;
            }
            else
            {
                b.FillColor = t.Surface;
                b.TextColor = t.Text;
                b.BorderColor = Draw.StrongBorder(t);
            }
            b.Invalidate();
        }

        /// <summary>Tô một ô nhập bo góc: surface = màu nền phía sau (lộ ra ở góc bo), border = màu viền khi chưa focus.</summary>
        public static void ApplyInput(InputBox box, ThemeTokens t, Color surface, Color border)
        {
            box.SurfaceColor = surface;
            box.FieldColor = Color.White;
            box.BorderColor = border;
            box.FocusColor = t.Primary;
            box.TextColor = t.Text;
            box.MutedColor = t.TextMuted;
            box.ApplyColors();
        }

        public static void ApplyGrid(DataGridView g, ThemeTokens t)
        {
            g.EnableHeadersVisualStyles = false;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.BackgroundColor = t.Surface;
            g.GridColor = Draw.Mix(t.Border, t.Surface, 0.4f);
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            g.ColumnHeadersDefaultCellStyle.BackColor = Draw.Mix(t.Surface, t.Primary, 0.18f);
            g.ColumnHeadersDefaultCellStyle.ForeColor = t.Text;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = g.ColumnHeadersDefaultCellStyle.BackColor;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = t.Text;
            g.RowHeadersDefaultCellStyle.BackColor = Draw.Mix(t.Surface, t.Primary, 0.10f);
            g.DefaultCellStyle.BackColor = t.Surface;
            g.DefaultCellStyle.ForeColor = t.Text;
            g.DefaultCellStyle.SelectionBackColor = t.Primary;
            g.DefaultCellStyle.SelectionForeColor = t.OnPrimary;
            g.AlternatingRowsDefaultCellStyle.BackColor = Draw.Mix(t.Surface, t.Primary, 0.05f);
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = t.Primary;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = t.OnPrimary;
            g.ForeColor = t.Text;
            g.Invalidate();
        }
    }
}
