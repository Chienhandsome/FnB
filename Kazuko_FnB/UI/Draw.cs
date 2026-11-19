using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Kazuko_FnB.UI
{
    /// <summary>Hàm vẽ dùng chung cho control tự vẽ.</summary>
    internal static class Draw
    {
        public const string IconFont = "Segoe MDL2 Assets";

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            if (radius <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Viền đậm dùng chung cho mọi component (yêu cầu của sếp): đậm hơn Border của theme.</summary>
        public static Color StrongBorder(Theme.ThemeTokens t)
        {
            // Theme sáng: viền đậm nghiêng về màu chủ đạo (cam) thay vì nâu xám.
            return Mix(t.Border, t.Primary, 0.45f);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>
        /// Cặp màu gradient suy ra từ màu chủ đạo của theme: đầu sáng hơn và lệch sang tông ấm,
        /// cuối đậm hơn và lệch sang tông lạnh (với cam: vàng cam → đỏ cam). Không còn cố định màu cam.
        /// </summary>
        public static Color GradFrom(Theme.ThemeTokens t)
        {
            return ShiftHsl(t.Primary, 14f, 0.10f);
        }

        public static Color GradTo(Theme.ThemeTokens t)
        {
            return ShiftHsl(t.Primary, -10f, -0.05f);
        }

        /// <summary>Dịch màu trong không gian HSL: đổi sắc độ (độ) và độ sáng (0..1), giữ độ bão hòa.</summary>
        public static Color ShiftHsl(Color c, float hueDegrees, float lightnessDelta)
        {
            float h = (c.GetHue() + hueDegrees) % 360f;
            if (h < 0f) h += 360f;
            float s = c.GetSaturation();
            float l = Math.Max(0f, Math.Min(1f, c.GetBrightness() + lightnessDelta));
            return FromHsl(h, s, l);
        }

        public static Color FromHsl(float hue, float saturation, float lightness)
        {
            float q = lightness < 0.5f ? lightness * (1f + saturation) : lightness + saturation - lightness * saturation;
            float p = 2f * lightness - q;
            float hk = hue / 360f;
            return Color.FromArgb(
                (int)Math.Round(255 * HueToRgb(p, q, hk + 1f / 3f)),
                (int)Math.Round(255 * HueToRgb(p, q, hk)),
                (int)Math.Round(255 * HueToRgb(p, q, hk - 1f / 3f)));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        public static Color Darken(Color c, float t)
        {
            return Mix(c, Color.Black, t);
        }

        public static Color Lighten(Color c, float t)
        {
            return Mix(c, Color.White, t);
        }
    }
}
