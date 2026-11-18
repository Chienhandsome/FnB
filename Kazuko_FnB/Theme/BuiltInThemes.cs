using System.Collections.Generic;
using System.Drawing;

namespace Kazuko_FnB.Theme
{
    /// <summary>
    /// Bảng màu dựng sẵn. Spec cần 10 bảng (đặt tên riêng cho quán trà sữa,
    /// không dùng lại tên/mô tả của KAZUKO GOLD). Hiện có 3 bảng làm mẫu; bổ sung ở G5.
    /// </summary>
    public static class BuiltInThemes
    {
        public static readonly ThemeTokens Orange = new ThemeTokens
        {
            Name = "Cam",
            IsDark = false,
            Background = Color.FromArgb(250, 244, 238),
            Surface = Color.FromArgb(255, 251, 247),
            Text = Color.FromArgb(46, 33, 24),
            TextMuted = Color.FromArgb(120, 98, 84),
            Primary = Color.FromArgb(234, 102, 24),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(196, 140, 60),
            Border = Color.FromArgb(232, 214, 198),
            Danger = Color.FromArgb(192, 40, 28),
            Success = Color.FromArgb(28, 128, 52)
        };

        public static readonly ThemeTokens Light = new ThemeTokens
        {
            Name = "Sáng",
            IsDark = false,
            Background = Color.FromArgb(245, 245, 245),
            Surface = Color.White,
            Text = Color.FromArgb(32, 32, 32),
            TextMuted = Color.FromArgb(110, 110, 110),
            Primary = Color.FromArgb(32, 120, 150),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(230, 150, 40),
            Border = Color.FromArgb(220, 220, 220),
            Danger = Color.FromArgb(192, 40, 28),
            Success = Color.FromArgb(28, 128, 52)
        };

        public static readonly ThemeTokens Dark = new ThemeTokens
        {
            Name = "Nền tối",
            IsDark = true,
            Background = Color.FromArgb(24, 26, 27),
            Surface = Color.FromArgb(36, 38, 40),
            Text = Color.FromArgb(235, 235, 235),
            TextMuted = Color.FromArgb(165, 165, 165),
            Primary = Color.FromArgb(240, 128, 48),
            OnPrimary = Color.FromArgb(24, 18, 12),
            Accent = Color.FromArgb(230, 170, 70),
            Border = Color.FromArgb(70, 72, 74),
            Danger = Color.FromArgb(255, 120, 110),
            Success = Color.FromArgb(110, 205, 125)
        };

        public static ThemeTokens Default
        {
            get { return Orange; }
        }

        public static IEnumerable<ThemeTokens> All()
        {
            yield return Orange;
            yield return Light;
            yield return Dark;
        }

        public static ThemeTokens Find(string name)
        {
            foreach (ThemeTokens t in All())
                if (t.Name == name)
                    return t;
            return Default;
        }
    }
}
