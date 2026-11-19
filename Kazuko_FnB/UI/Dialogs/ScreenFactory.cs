using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Bảng tra: id mục menu (MenuNode.Id) → hộp thoại chức năng thật.
    /// Mục chưa đăng ký vẫn mở tab tạm "sẽ được bổ sung". Mỗi chức năng đăng ký một dòng ở đây.
    /// </summary>
    internal static class ScreenFactory
    {
        private static readonly Dictionary<string, Func<UserSession, Form>> Map =
            new Dictionary<string, Func<UserSession, Form>>(StringComparer.Ordinal)
            {
                { "admin.config", session => new ConfigDialog(session) },
                { "admin.images", session => new ImageLibraryDialog(session) },
                { "admin.users", session => new UserManagerDialog(session) },
                { "sys.password", session => new ChangePasswordDialog(session) },
                // Chỉ để chụp thử bằng --render-screen (không có mục menu):
                { "preview.useredit", session => new UserEditDialog(session, null, null, null) },
                { "preview.groupedit", session => new GroupEditDialog(session, null) }
            };

        public static bool Has(string id)
        {
            return id != null && Map.ContainsKey(id);
        }

        /// <summary>Tạo form cho id (null nếu chưa có). Dùng cho --render-screen.</summary>
        public static Form Create(string id, UserSession session)
        {
            Func<UserSession, Form> f;
            return id != null && Map.TryGetValue(id, out f) ? f(session) : null;
        }

        /// <summary>Mở hộp thoại (modal) nếu mục này đã có màn thật; trả về true nếu đã xử lý.</summary>
        public static bool TryOpen(IWin32Window owner, MenuNode node, UserSession session)
        {
            using (Form form = Create(node.Id, session))
            {
                if (form == null)
                    return false;
                form.ShowDialog(owner);
                return true;
            }
        }
    }
}
