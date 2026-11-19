using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Cấu hình theo người dùng Windows, lưu ở %APPDATA%\KazukoFnB\settings.txt.
    /// Mật khẩu ghi nhớ mã hóa bằng DPAPI (chỉ đúng tài khoản Windows này giải mã được).
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kazuko_FnB.login.v1");

        public string DatabasePath { get; set; }
        public bool RememberLogin { get; set; }
        public bool AutoLogin { get; set; }
        public string UserName { get; set; }
        public string ProtectedPassword { get; set; }
        public string ThemeName { get; set; }

        // Tab "Cơ sở dữ liệu": tài khoản Firebird (mặc định SYSDBA), chế độ máy chủ (chưa hỗ trợ).
        public string FbUser { get; set; }
        public string ProtectedFbPassword { get; set; }
        public bool UseServer { get; set; }
        public string ServerHost { get; set; }
        public string ServerPort { get; set; }

        public const string DefaultFbUser = "SYSDBA";
        public const string DefaultFbPassword = "masterkey";
        public const string DefaultServerPort = "3050";

        public static string SettingsDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KazukoFnB");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(SettingsDirectory, "settings.txt"); }
        }

        public static AppSettings Load()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0)
                            d[line.Substring(0, i).Trim()] = line.Substring(i + 1);
                    }
                }
            }
            catch (IOException)
            {
                // Không đọc được thì dùng mặc định.
            }

            string v;
            var s = new AppSettings();
            s.DatabasePath = d.TryGetValue("DatabasePath", out v) && v.Length > 0
                ? v
                : DefaultDatabasePath();
            s.RememberLogin = d.TryGetValue("RememberLogin", out v) && v == "1";
            s.AutoLogin = d.TryGetValue("AutoLogin", out v) && v == "1";
            s.UserName = d.TryGetValue("UserName", out v) ? v : null;
            s.ProtectedPassword = d.TryGetValue("Password", out v) && v.Length > 0 ? v : null;
            s.ThemeName = d.TryGetValue("Theme", out v) ? v : null;
            s.FbUser = d.TryGetValue("FbUser", out v) && v.Length > 0 ? v : DefaultFbUser;
            // Không có khóa = chưa từng đặt (dùng mặc định); "-" = đã đặt là rỗng.
            s.ProtectedFbPassword = d.TryGetValue("FbPassword", out v) && v.Length > 0 ? v : null;
            s.UseServer = d.TryGetValue("UseServer", out v) && v == "1";
            s.ServerHost = d.TryGetValue("ServerHost", out v) ? v : "";
            s.ServerPort = d.TryGetValue("ServerPort", out v) && v.Length > 0 ? v : DefaultServerPort;
            return s;
        }

        /// <summary>
        /// Đường dẫn DB mặc định khi người dùng chưa chọn: giá trị appSettings "DatabasePath" nếu có;
        /// nếu không thì tìm Database\DATA.fdb (cạnh exe hoặc ở các thư mục cha, để chạy từ bin\Debug khi dev).
        /// </summary>
        public static string DefaultDatabasePath()
        {
            string configured = ConfigurationManager.AppSettings["DatabasePath"];
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "Database", "DATA.fdb");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (IOException)
            {
                // Không tìm được thì để trống, người dùng tự chọn ở tab "Cơ sở dữ liệu".
            }
            return configured;
        }

        public void Save()
        {
            Directory.CreateDirectory(SettingsDirectory);
            var lines = new[]
            {
                "DatabasePath=" + (DatabasePath ?? ""),
                "RememberLogin=" + (RememberLogin ? "1" : "0"),
                "AutoLogin=" + (AutoLogin ? "1" : "0"),
                "UserName=" + (UserName ?? ""),
                "Password=" + (ProtectedPassword ?? ""),
                "Theme=" + (ThemeName ?? ""),
                "FbUser=" + (string.IsNullOrWhiteSpace(FbUser) ? DefaultFbUser : FbUser),
                "FbPassword=" + (ProtectedFbPassword ?? ""),
                "UseServer=" + (UseServer ? "1" : "0"),
                "ServerHost=" + (ServerHost ?? ""),
                "ServerPort=" + (string.IsNullOrWhiteSpace(ServerPort) ? DefaultServerPort : ServerPort)
            };
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(false));
        }

        public string GetPassword()
        {
            return Unprotect(ProtectedPassword);
        }

        public void SetPassword(string password)
        {
            ProtectedPassword = Protect(password);
        }

        private const string EmptyPasswordMarker = "-";

        /// <summary>
        /// Mật khẩu tài khoản Firebird. Chưa từng lưu: dùng mặc định "masterkey".
        /// Đã lưu là rỗng: trả về chuỗi rỗng (không tự điền lại mặc định).
        /// </summary>
        public string GetFbPassword()
        {
            if (ProtectedFbPassword == null)
                return DefaultFbPassword;
            if (ProtectedFbPassword == EmptyPasswordMarker)
                return "";
            return Unprotect(ProtectedFbPassword) ?? DefaultFbPassword;
        }

        public void SetFbPassword(string password)
        {
            ProtectedFbPassword = string.IsNullOrEmpty(password) ? EmptyPasswordMarker : Protect(password);
        }

        private static string Protect(string plain)
        {
            return string.IsNullOrEmpty(plain)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));
        }

        private static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
                return null;
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(protectedValue), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
