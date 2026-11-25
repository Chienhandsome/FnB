using System;
using System.Collections.Generic;
using System.Drawing;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.Theme
{
    /// <summary>Một cặp màu cần kiểm tương phản (WCAG 2.x).</summary>
    public sealed class ContrastPair
    {
        public string Name { get; set; }
        public double Ratio { get; set; }
        public double Minimum { get; set; }
        public bool Pass
        {
            get { return Ratio >= Minimum; }
        }
    }

    /// <summary>
    /// Kiểm tương phản chữ/nền của một bảng màu theo WCAG 2.x. Chữ thường cần tối thiểu 4,5 (mức AA);
    /// thành phần giao diện không phải chữ (icon, viền) cần tối thiểu 3. SelfTest dùng để chặn bảng màu thiếu tương phản.
    /// </summary>
    public static class ContrastChecker
    {
        public const double TextMinimum = 4.5;
        public const double UiMinimum = 3.0;

        public static double Ratio(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        public static double Luminance(Color c)
        {
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        private static double Linear(int channel)
        {
            double v = channel / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        /// <summary>Các cặp màu chữ/nền mà giao diện thật sự dùng, kèm ngưỡng.</summary>
        public static IList<ContrastPair> Evaluate(ThemeTokens t)
        {
            var list = new List<ContrastPair>();
            Add(list, "Chữ / Nền", t.Text, t.Background, TextMinimum);
            Add(list, "Chữ / Bề mặt", t.Text, t.Surface, TextMinimum);
            Add(list, "Chữ phụ / Nền", t.TextMuted, t.Background, TextMinimum);
            Add(list, "Chữ phụ / Bề mặt", t.TextMuted, t.Surface, TextMinimum);
            Add(list, "Chữ trên màu chính", t.OnPrimary, t.Primary, TextMinimum);
            Add(list, "Màu cảnh báo / Bề mặt", t.Danger, t.Surface, TextMinimum);
            Add(list, "Màu cảnh báo / Nền", t.Danger, t.Background, TextMinimum);
            Add(list, "Màu thành công / Bề mặt", t.Success, t.Surface, TextMinimum);
            Add(list, "Màu thành công / Nền", t.Success, t.Background, TextMinimum);
            Add(list, "Màu thông tin / Bề mặt", t.Info, t.Surface, TextMinimum);
            Add(list, "Màu thông tin / Nền", t.Info, t.Background, TextMinimum);
            Add(list, "Màu chính / Nền (icon, viền)", t.Primary, t.Background, UiMinimum);
            return list;
        }

        private static void Add(List<ContrastPair> list, string name, Color fg, Color bg, double min)
        {
            list.Add(new ContrastPair { Name = name, Ratio = Ratio(fg, bg), Minimum = min });
        }
    }
}
