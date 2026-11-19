using System;
using System.Drawing;
using System.IO;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>Đọc/kiểm tra ảnh từ byte[] hoặc file (jpg, gif, bmp, png).</summary>
    internal static class ImageUtil
    {
        public const string FileFilter = "Image Files|*.jpg;*.gif;*.bmp;*.png;*.jpeg|All Files|*.*";
        public const string LoadError = "Không thể tải file, có thể file không tồn tại hoặc không đúng định dạng!";

        /// <summary>Ảnh độc lập với luồng nguồn (có thể dispose luồng). Ném lỗi nếu không phải ảnh.</summary>
        public static Bitmap FromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;
            using (var ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
                return new Bitmap(img);
        }

        /// <summary>Đọc file ảnh thành byte[] (đã kiểm tra mở được). Ném lỗi nếu không hợp lệ.</summary>
        public static byte[] ReadValidated(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            using (Bitmap b = FromBytes(data))
            {
                if (b == null)
                    throw new InvalidDataException("empty");
            }
            return data;
        }

        public static Bitmap Thumb(byte[] data, int size)
        {
            Bitmap src;
            try
            {
                src = FromBytes(data);
            }
            catch (Exception)
            {
                src = null;
            }
            var bmp = new Bitmap(size, size);
            if (src == null)
                return bmp;
            using (src)
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float k = Math.Min((float)size / src.Width, (float)size / src.Height);
                float w = src.Width * k, h = src.Height * k;
                g.DrawImage(src, (size - w) / 2f, (size - h) / 2f, w, h);
            }
            return bmp;
        }
    }
}
