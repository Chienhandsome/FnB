using System;
using System.Reflection;
using FastReport.Utils;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Đăng ký các hàm mở rộng dùng trong biểu thức mẫu FastReport (vd. [ToVndWords([TONGCONG])])
    /// một lần duy nhất cho cả tiến trình, an toàn khi gọi lặp và đa luồng.
    /// </summary>
    internal static class FastReportFunctions
    {
        private static readonly object SyncRoot = new object();
        private static bool _registered;

        public static bool IsRegistered
        {
            get { return _registered; }
        }

        /// <summary>
        /// Đăng ký No1Lib.Sys.DocSo.ToVndWords(decimal) vào nhóm "No1" của FastReport.
        /// Không sửa đổi chuỗi kết quả trả về của hàm gốc.
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            lock (SyncRoot)
            {
                if (_registered)
                    return;

                MethodInfo method = typeof(No1Lib.Sys.DocSo).GetMethod(
                    "ToVndWords",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(decimal) },
                    null);
                if (method == null)
                    throw new MissingMethodException("No1Lib.Sys.DocSo", "ToVndWords(decimal)");

                RegisteredObjects.AddFunction(method, "No1");
                _registered = true;
            }
        }
    }
}
