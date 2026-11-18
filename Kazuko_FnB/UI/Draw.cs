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

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
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
