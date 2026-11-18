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
                : ConfigurationManager.AppSettings["DatabasePath"];
            s.RememberLogin = d.TryGetValue("RememberLogin", out v) && v == "1";
            s.AutoLogin = d.TryGetValue("AutoLogin", out v) && v == "1";
            s.UserName = d.TryGetValue("UserName", out v) ? v : null;
            s.ProtectedPassword = d.TryGetValue("Password", out v) && v.Length > 0 ? v : null;
            s.ThemeName = d.TryGetValue("Theme", out v) ? v : null;
            return s;
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
                "Theme=" + (ThemeName ?? "")
            };
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(false));
        }

        public string GetPassword()
        {
            if (string.IsNullOrEmpty(ProtectedPassword))
                return null;
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(ProtectedPassword), Entropy, DataProtectionScope.CurrentUser);
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

        public void SetPassword(string password)
        {
            ProtectedPassword = string.IsNullOrEmpty(password)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));
        }
    }
}
