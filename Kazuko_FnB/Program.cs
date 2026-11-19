using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI;

namespace Kazuko_FnB
{
    internal static class Program
    {
        /// <summary>
        /// Entry point. Lệnh kiểm tra (không mở giao diện):
        ///   --db-smoke     &lt;db&gt; [file kết quả]
        ///   --login-test   &lt;db&gt; &lt;tài khoản&gt; &lt;mật khẩu&gt; &lt;file kết quả&gt;
        ///   --render-login &lt;file.png&gt; [rộng cao] [login|db|dbok=&lt;db&gt;] [tên theme]
        ///   --render-main  &lt;file.png&gt; [rộng cao] [tên theme] [vai trò] [đường dẫn DATA.FDB]
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            args = args ?? new string[0];

            if (args.Length >= 2 && args[0] == "--db-smoke")
                return RunDbSmoke(args);

            if (args.Length >= 5 && args[0] == "--login-test")
            {
                LoginResult r = new LoginService().Login(args[1], args[2], args[3]);
                File.WriteAllText(args[4], r.Success
                    ? "OK user=" + r.Session.UserName + " admin=" + r.Session.IsAdmin + " stores=" + r.Session.Stores.Count
                    : r.Code + ": " + r.Message);
                return r.Success ? 0 : 2;
            }

            if (args.Length >= 3 && args[0] == "--selftest")
                return Data.SelfTest.Run(args[1], args[2]);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppSettings settings = AppSettings.Load();
            Data.KazukoConnection.Configure(settings.FbUser, settings.GetFbPassword());
            ThemeManager.Apply(BuiltInThemes.Find(settings.ThemeName));

            if (args.Length >= 2 && args[0] == "--render-login")
                return RenderLogin(args, settings);
            if (args.Length >= 2 && args[0] == "--render-main")
                return RenderMain(args);
            if (args.Length >= 3 && args[0] == "--render-screen")
                return RenderScreen(args);

            // Vòng lặp: đăng nhập → khung chính → (Đăng xuất) quay lại đăng nhập.
            // Tự động đăng nhập chỉ áp dụng lần mở app đầu tiên, không áp dụng sau khi bấm Đăng xuất.
            bool firstRun = true;
            while (true)
            {
                UserSession session = null;
                string autoError = null;
                if (firstRun && settings.AutoLogin && settings.RememberLogin && !string.IsNullOrEmpty(settings.UserName)
                    && File.Exists(settings.DatabasePath ?? ""))
                {
                    LoginResult r = new LoginService().Login(settings.DatabasePath, settings.UserName, settings.GetPassword() ?? "");
                    if (r.Success)
                        session = r.Session;
                    else
                        autoError = "Tự động đăng nhập không thành công: " + r.Message;
                }
                firstRun = false;

                if (session == null)
                {
                    using (var login = new LoginForm(settings, autoError))
                    {
                        if (login.ShowDialog() != DialogResult.OK)
                            return 0;
                        session = login.Session;
                    }
                }

                using (var main = new MainForm(session))
                {
                    Application.Run(main);
                    if (!main.LogoutRequested)
                        return 0;
                }
            }
        }

        private static int RunDbSmoke(string[] args)
        {
            string dbPath = args[1];
            string outFile = args.Length >= 3 ? args[2] : null;
            try
            {
                string report = Data.DbSmokeTest.Run(dbPath);
                if (outFile != null) File.WriteAllText(outFile, report);
                Console.Write(report);
                return 0;
            }
            catch (Exception ex)
            {
                string msg = "SMOKE FAIL: " + ex;
                if (outFile != null) File.WriteAllText(outFile, msg);
                Console.Error.WriteLine(msg);
                return 1;
            }
        }

        private static int RenderMain(string[] args)
        {
            int w = args.Length >= 4 ? int.Parse(args[2]) : 1600;
            int h = args.Length >= 4 ? int.Parse(args[3]) : 900;
            if (args.Length >= 5)
                ThemeManager.Apply(BuiltInThemes.Find(args[4]));
            var session = new UserSession
            {
                UserId = "demo",
                UserName = "admin",
                DisplayName = "Quản trị",
                RoleText = args.Length >= 6 ? args[5] : "Quản trị viên",
                IsAdmin = true,
                DatabasePath = args.Length >= 7 ? args[6] : null
            };
            using (var form = new MainForm(session))
            {
                form.SkipConfirm = true;
                form.WindowState = FormWindowState.Normal;
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Size = new Size(w, h);
                form.Show();
                form.Size = new Size(w, h);
                form.PerformLayout();
                Application.DoEvents();

                // Tuỳ chọn thêm: tabs=N mở N chức năng; menu=I chụp menu thả xuống thứ I (file .menu.png).
                for (int i = 7; i < args.Length; i++)
                {
                    if (args[i].StartsWith("tabs=", StringComparison.Ordinal))
                    {
                        form.PreviewOpenTabs(int.Parse(args[i].Substring(5)));
                        form.PerformLayout();
                        Application.DoEvents();
                    }
                    else if (args[i].StartsWith("menu=", StringComparison.Ordinal))
                    {
                        using (Bitmap mb = form.PreviewDropDown(int.Parse(args[i].Substring(5))))
                            if (mb != null)
                                mb.Save(args[1] + ".menu.png", ImageFormat.Png);
                    }
                }

                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(args[1], ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        /// <summary>
        /// --render-screen &lt;file.png&gt; &lt;id màn hình&gt; [tên theme] [đường dẫn DATA.FDB] [tùy chọn...]
        /// Dựng hộp thoại chức năng ngoài màn hình rồi chụp ra PNG (kiểm tra giao diện, không cần người bấm).
        /// Tùy chọn thêm cho dialog: "tab=N", "select=...", xem từng màn.
        /// </summary>
        private static int RenderScreen(string[] args)
        {
            if (args.Length >= 4)
                ThemeManager.Apply(BuiltInThemes.Find(args[3]));
            var session = new UserSession
            {
                UserId = "demo",
                UserName = "admin",
                DisplayName = "Quản trị",
                RoleText = "Quản trị viên",
                IsAdmin = true,
                DatabasePath = args.Length >= 5 ? args[4] : null
            };
            AppSession.Current = session;
            using (Form form = UI.Dialogs.ScreenFactory.Create(args[2], session))
            {
                if (form == null)
                {
                    Console.Error.WriteLine("Chưa có màn hình: " + args[2]);
                    return 3;
                }
                form.StartPosition = FormStartPosition.Manual;
                form.ShowInTaskbar = false;
                form.Location = new Point(-20000, -20000);
                form.Show();
                for (int i = 5; i < args.Length; i++)
                {
                    var pv = form as UI.Dialogs.IScreenPreview;
                    if (pv != null)
                        pv.Preview(args[i]);
                }
                form.PerformLayout();
                Application.DoEvents();
                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(args[1], ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int RenderLogin(string[] args, AppSettings settings)
        {
            int w = args.Length >= 4 ? int.Parse(args[2]) : 1600;
            int h = args.Length >= 4 ? int.Parse(args[3]) : 900;
            if (args.Length >= 6)
                ThemeManager.Apply(BuiltInThemes.Find(args[5]));
            using (var form = new LoginForm(settings, null))
            {
                // Mở ngoài vùng màn hình để control con được tạo, rồi chụp bằng WM_PRINT.
                form.WindowState = FormWindowState.Normal;
                form.ShowInTaskbar = false;
                form.Location = new Point(-20000, -20000);
                form.Size = new Size(w, h);
                form.Show();
                form.Size = new Size(w, h);
                if (args.Length >= 5 && args[4].StartsWith("dbok=", StringComparison.Ordinal))
                    form.PreviewConnect(args[4].Substring(5));
                else if (args.Length >= 5 && args[4] == "server")
                    form.PreviewServerMode();
                else if (args.Length >= 5 && args[4] == "db")
                    form.SelectTab(1);
                else
                    form.SelectTab(0);
                form.PerformLayout();
                Application.DoEvents();
                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(args[1], ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }
    }
}
