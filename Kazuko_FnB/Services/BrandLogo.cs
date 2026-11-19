using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Logo ứng dụng (Assets\logo.png, nền trong suốt). Mark = chữ K không kèm dòng chữ bên dưới (đủ rõ khi nhỏ);
    /// MarkWhite = cùng hình nhưng tô trắng để đặt lên nền cam của thanh tiêu đề.
    /// </summary>
    public static class BrandLogo
    {
        // Vùng chữ K trong logo.png (đo từ ảnh gốc 894x1041; dòng chữ "KAZUKO VIỆT NAM" nằm bên dưới).
        private static readonly Rectangle MarkBounds = new Rectangle(68, 42, 769, 779);

        private static Image _full, _mark, _markWhite;
        private static Icon _appIcon;
        private static bool _loaded;

        /// <summary>Logo đầy đủ (chữ K + dòng chữ), null nếu thiếu file.</summary>
        public static Image Full
        {
            get { Ensure(); return _full; }
        }

        private static Image _banner;
        private static bool _bannerLoaded;

        /// <summary>Banner màn Truy cập nhanh (Assets\banner.png, tỉ lệ rất dài); null nếu thiếu file.</summary>
        public static Image Banner
        {
            get
            {
                if (!_bannerLoaded)
                {
                    _bannerLoaded = true;
                    try
                    {
                        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "banner.png");
                        if (File.Exists(p))
                        {
                            using (var ms = new MemoryStream(File.ReadAllBytes(p)))
                            using (Image tmp = Image.FromStream(ms))
                                _banner = new Bitmap(tmp);
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
                return _banner;
            }
        }

        /// <summary>Chữ K màu cam.</summary>
        public static Image Mark
        {
            get { Ensure(); return _mark; }
        }

        /// <summary>Chữ K tô trắng (cho nền cam).</summary>
        public static Image MarkWhite
        {
            get { Ensure(); return _markWhite; }
        }

        /// <summary>Icon cửa sổ / thanh tác vụ, lấy từ Assets\app.ico; null nếu thiếu.</summary>
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    try
                    {
                        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                        if (File.Exists(p))
                            _appIcon = new Icon(p);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
                return _appIcon;
            }
        }

        private static void Ensure()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");
                if (!File.Exists(p))
                    return;
                using (var ms = new MemoryStream(File.ReadAllBytes(p)))
                using (Image tmp = Image.FromStream(ms))
                {
                    _full = new Bitmap(tmp);
                    var r = Rectangle.Intersect(MarkBounds, new Rectangle(0, 0, tmp.Width, tmp.Height));
                    var mark = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(mark))
                        g.DrawImage(tmp, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
                    _mark = mark;
                    _markWhite = Tint(mark, Color.White);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static Image Tint(Image src, Color color)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            var m = new ColorMatrix(new[]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 }
            });
            using (var ia = new ImageAttributes())
            using (Graphics g = Graphics.FromImage(dst))
            {
                ia.SetColorMatrix(m);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return dst;
        }
    }
}
