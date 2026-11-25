using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Kazuko_FnB.Infrastructure
{
    /// <summary>
    /// Ngày cài app trên máy = ngày chạy app lần đầu, lưu ở %PROGRAMDATA%\KazukoFnB\install-date.txt
    /// (dùng chung cho mọi tài khoản Windows). Xóa file này thì ngày cài được ghi lại.
    /// </summary>
    public static class InstallDate
    {
        private const string Format = "yyyy-MM-dd";

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KazukoFnB", "install-date.txt");
            }
        }

        /// <summary>Đọc ngày cài; chưa có (hoặc file hỏng) thì ghi hôm nay. Không ghi được thì trả về hôm nay.</summary>
        public static DateTime GetOrCreate()
        {
            try
            {
                DateTime saved;
                if (File.Exists(FilePath) &&
                    DateTime.TryParseExact(File.ReadAllText(FilePath, Encoding.UTF8).Trim(), Format,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out saved))
                    return saved;

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, DateTime.Today.ToString(Format, CultureInfo.InvariantCulture),
                    new UTF8Encoding(false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return DateTime.Today;
        }
    }
}
