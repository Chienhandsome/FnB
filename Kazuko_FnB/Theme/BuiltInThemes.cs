using System.Collections.Generic;
using System.Drawing;

namespace Kazuko_FnB.Theme
{
    /// <summary>
    /// 4 bảng màu dựng sẵn, đều nền sáng (không có nền tối). Tên, nhãn, mô tả đặt theo F&amp;B.
    /// Mọi cặp chữ/nền chính đạt tương phản tối thiểu AA (kiểm trong SelfTest bằng ContrastChecker).
    /// Chỉnh màu thì chạy lại selftest để kiểm tương phản.
    /// </summary>
    public static class BuiltInThemes
    {
        public static readonly ThemeTokens Orange = new ThemeTokens
        {
            Name = "Cam Nắng",
            Tag = "ẤM ÁP",
            Audience = "Quán trà sữa, cà phê, tiệm bánh",
            Description = "Cam ấm như nắng sớm, nền kem dịu. Thân thiện, gợi cảm giác muốn gọi món.",
            Background = Color.FromArgb(255, 243, 235),
            Surface = Color.FromArgb(254, 252, 251),
            Text = Color.FromArgb(46, 31, 20),
            TextMuted = Color.FromArgb(118, 99, 86),
            Primary = Color.FromArgb(189, 82, 5),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(180, 150, 45),
            Border = Color.FromArgb(251, 211, 182),
            Danger = Color.FromArgb(176, 32, 22),
            Success = Color.FromArgb(22, 116, 44)
        };

        public static readonly ThemeTokens Coffee = new ThemeTokens
        {
            Name = "Cà Phê",
            Tag = "TRUYỀN THỐNG",
            Audience = "Quán cà phê, quán nhậu",
            Description = "Nâu cà phê rang đậm, nền be nhạt. Đằm, bền mắt khi làm việc cả ngày.",
            Background = Color.FromArgb(249, 244, 240),
            Surface = Color.FromArgb(254, 252, 251),
            Text = Color.FromArgb(45, 31, 22),
            TextMuted = Color.FromArgb(118, 99, 86),
            Primary = Color.FromArgb(166, 95, 48),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(180, 139, 45),
            Border = Color.FromArgb(234, 213, 200),
            Danger = Color.FromArgb(176, 32, 22),
            Success = Color.FromArgb(22, 116, 44)
        };

        public static readonly ThemeTokens Matcha = new ThemeTokens
        {
            Name = "Matcha",
            Tag = "TƯƠI MÁT",
            Audience = "Quán trà, nước ép, đồ healthy",
            Description = "Xanh lá trà dịu mắt, nền kem xanh nhạt, gợi cảm giác tươi và sạch.",
            Background = Color.FromArgb(244, 250, 239),
            Surface = Color.FromArgb(252, 254, 251),
            Text = Color.FromArgb(31, 45, 22),
            TextMuted = Color.FromArgb(96, 115, 84),
            Primary = Color.FromArgb(64, 126, 22),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(45, 180, 81),
            Border = Color.FromArgb(213, 234, 200),
            Danger = Color.FromArgb(176, 32, 22),
            Success = Color.FromArgb(22, 116, 44)
        };

        public static readonly ThemeTokens Ocean = new ThemeTokens
        {
            Name = "Xanh Biển",
            Tag = "CHUYÊN NGHIỆP",
            Audience = "Chuỗi cửa hàng, văn phòng",
            Description = "Xanh dương đậm, gọn và đáng tin. Hợp môi trường làm việc nghiêm túc.",
            Background = Color.FromArgb(238, 244, 252),
            Surface = Color.FromArgb(251, 252, 254),
            Text = Color.FromArgb(20, 32, 46),
            TextMuted = Color.FromArgb(86, 101, 118),
            Primary = Color.FromArgb(23, 111, 211),
            OnPrimary = Color.White,
            Accent = Color.FromArgb(49, 45, 180),
            Border = Color.FromArgb(194, 215, 240),
            Danger = Color.FromArgb(176, 32, 22),
            Success = Color.FromArgb(22, 116, 44)
        };

        /// <summary>Bảng màu mặc định (Cam Nắng).</summary>
        public static ThemeTokens Default
        {
            get { return Orange; }
        }

        public static IEnumerable<ThemeTokens> All()
        {
            yield return Orange;
            yield return Coffee;
            yield return Matcha;
            yield return Ocean;
        }

        /// <summary>Tìm theo tên; không thấy (kể cả tên cũ "Cam", "Sáng", "Nền tối") thì trả bảng mặc định.</summary>
        public static ThemeTokens Find(string name)
        {
            foreach (ThemeTokens t in All())
                if (t.Name == name)
                    return t;
            return Default;
        }
    }
}
