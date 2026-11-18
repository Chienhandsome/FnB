using System;
using System.IO;
using System.Windows.Forms;

namespace Kazuko_FnB
{
    internal static class Program
    {
        /// <summary>
        /// Entry point. Hỗ trợ chế độ kiểm tra DB (G0):
        ///   Kazuko_FnB.exe --db-smoke &lt;đường dẫn DATA.FDB&gt; [file kết quả]
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args != null && args.Length >= 2 && args[0] == "--db-smoke")
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            return 0;
        }
    }
}
