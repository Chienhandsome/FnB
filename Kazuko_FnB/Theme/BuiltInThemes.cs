using System.Collections.Generic;
using System.Drawing;

namespace Kazuko_FnB.Theme
{
    /// <summary>
    /// Khung bảng màu dựng sẵn. Spec cần ~10 bảng (đặt tên riêng cho quán trà sữa,
    /// không dùng lại tên/mô tả của KAZUKO GOLD). Hiện có 1 sáng + 1 tối làm mẫu;
    /// bổ sung dần ở G5.
    /// </summary>
    public static class BuiltInThemes
    {
        public static readonly ThemeTokens Light = new ThemeTokens
        {
            Name = "Sáng",
            IsDark = false,
            Background = Color.FromArgb(245, 245, 245),
            Surface = Color.White,
            Text = Color.FromArgb(32, 32, 32),
            TextMuted = Color.FromArgb(110, 110, 110),
            Primary = Color.FromArgb(43, 145, 175),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(230, 150, 40),
            Border = Color.FromArgb(220, 220, 220)
        };

        public static readonly ThemeTokens Dark = new ThemeTokens
        {
            Name = "Nền tối",
            IsDark = true,
            Background = Color.FromArgb(24, 26, 27),
            Surface = Color.FromArgb(36, 38, 40),
            Text = Color.FromArgb(235, 235, 235),
            TextMuted = Color.FromArgb(160, 160, 160),
            Primary = Color.FromArgb(70, 160, 190),
            OnPrimary = Color.Black,
            Accent = Color.FromArgb(230, 160, 60),
            Border = Color.FromArgb(60, 62, 64)
        };

        public static ThemeTokens Default
        {
            get { return Light; }
        }

        public static IEnumerable<ThemeTokens> All()
        {
            yield return Light;
            yield return Dark;
        }
    }
}
