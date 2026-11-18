using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Ghép menu chính: "Hệ thống" (cố định trong mã, như app gốc) + các nhóm từ SMENU + "Trợ giúp".
    /// Lọc theo quyền người dùng. Dựng luôn thanh công cụ (SMENU.TOOLBAR = 30) và các thẻ "Truy cập nhanh".
    /// Menu "Báo cáo" của app gốc chưa làm (thuộc G4).
    /// </summary>
    public static class MainMenuBuilder
    {
        private const int QuickItemsPerGroup = 6;

        public static MenuModel Build(UserSession session)
        {
            var model = new MenuModel();
            FunctionPermissions perms;
            List<MenuNode> dbRoots = new List<MenuNode>();

            if (!string.IsNullOrEmpty(session.DatabasePath))
            {
                try
                {
                    var users = new UserRepository(session.DatabasePath);
                    perms = users.GetFunctionPermissions(session.UserId, session.IsAdmin);
                    dbRoots = new MenuRepository(session.DatabasePath).LoadTree();
                }
                catch (FirebirdSql.Data.FirebirdClient.FbException)
                {
                    // Không đọc được menu: vẫn cho vào app với menu cố định.
                    perms = new FunctionPermissions(session.IsAdmin);
                }
            }
            else
            {
                perms = new FunctionPermissions(session.IsAdmin);
            }

            model.Roots.Add(BuildSystemMenu(perms));
            foreach (MenuNode root in dbRoots)
            {
                MenuNode pruned = Prune(root, perms);
                if (pruned != null && !pruned.IsSeparator)
                    model.Roots.Add(pruned);
            }
            model.Roots.Add(BuildHelpMenu());

            // Thanh công cụ và thẻ Truy cập nhanh chỉ lấy từ nhóm DB.
            var dbPruned = new List<MenuNode>();
            foreach (MenuNode root in dbRoots)
            {
                MenuNode p = Prune(root, perms);
                if (p != null)
                    dbPruned.Add(p);
            }
            BuildToolbar(model, dbRoots, perms);
            BuildQuickGroups(model, dbPruned);
            return model;
        }

        // ------------------------------------------------------------------ lọc quyền

        /// <summary>Bỏ mục ẩn / không đủ quyền; nhóm rỗng cũng bị bỏ; dọn dòng kẻ thừa.</summary>
        private static MenuNode Prune(MenuNode n, FunctionPermissions perms)
        {
            if (n.IsSeparator)
                return n;
            if (n.Hidden)
                return null;

            if (n.IsLeaf)
                return perms.AllowsId(n.FunctionId) ? n : null;

            var copy = Clone(n);
            foreach (MenuNode c in n.Children)
            {
                MenuNode pc = Prune(c, perms);
                if (pc != null)
                    copy.Children.Add(pc);
            }
            TrimSeparators(copy.Children);
            return HasRealItem(copy.Children) ? copy : null;
        }

        private static MenuNode Clone(MenuNode n)
        {
            return new MenuNode
            {
                Id = n.Id,
                Title = n.Title,
                RawName = n.RawName,
                SortOrder = n.SortOrder,
                ParentId = n.ParentId,
                IsSeparator = n.IsSeparator,
                Hidden = n.Hidden,
                FunctionId = n.FunctionId,
                FunctionName = n.FunctionName,
                Shortcut = n.Shortcut,
                InToolbar = n.InToolbar,
                ToolbarIndex = n.ToolbarIndex,
                ToolbarTitle = n.ToolbarTitle,
                Action = n.Action
            };
        }

        private static bool HasRealItem(List<MenuNode> items)
        {
            foreach (MenuNode i in items)
                if (!i.IsSeparator)
                    return true;
            return false;
        }

        /// <summary>Bỏ dòng kẻ ở đầu, cuối và các dòng kẻ liền nhau.</summary>
        private static void TrimSeparators(List<MenuNode> items)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                bool bad = items[i].IsSeparator &&
                           (i == 0 || i == items.Count - 1 || items[i - 1].IsSeparator);
                if (bad)
                    items.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ menu cố định

        private static MenuNode Item(string id, string title, string functionName, MenuAction action)
        {
            return new MenuNode { Id = id, Title = title, RawName = title, FunctionName = functionName, Action = action };
        }

        private static MenuNode Sep(string id)
        {
            return new MenuNode { Id = id, Title = "", IsSeparator = true };
        }

        private static MenuNode BuildSystemMenu(FunctionPermissions perms)
        {
            var m = new MenuNode { Id = "sys", Title = "Hệ thống", RawName = "HỆ THỐNG" };
            var all = new List<MenuNode>
            {
                Item("sys.db", "Cơ sở dữ liệu...", null, MenuAction.OpenTab),
                Item("sys.db.new", "Tạo mới cơ sở dữ liệu", "Tạo mới cơ sở dữ liệu", MenuAction.OpenTab),
                Item("sys.db.backup", "Sao lưu cơ sở dữ liệu", "Sao lưu cơ sở dữ liệu", MenuAction.OpenTab),
                Item("sys.db.restore", "Phục hồi cơ sở dữ liệu", "Phục hồi cơ sở dữ liệu", MenuAction.OpenTab),
                Sep("sys.sep1"),
                Item("sys.users", "Người dùng và phân quyền", "Quản lý người dùng", MenuAction.OpenTab),
                Item("sys.images", "Thư viện ảnh...", "Thư viện ảnh", MenuAction.OpenTab),
                Item("sys.config", "Cấu hình toàn hệ thống", "Cấu hình toàn hệ thống", MenuAction.OpenTab),
                Item("sys.password", "Đổi mật khẩu đăng nhập", null, MenuAction.OpenTab),
                Sep("sys.sep2"),
                Item("sys.logout", "Đăng xuất khỏi hệ thống", null, MenuAction.Logout),
                Item("sys.exit", "Thoát khỏi hệ thống", null, MenuAction.Exit)
            };
            foreach (MenuNode n in all)
                if (n.IsSeparator || perms.AllowsName(n.FunctionName))
                    m.Children.Add(n);
            TrimSeparators(m.Children);
            return m;
        }

        private static MenuNode BuildHelpMenu()
        {
            var m = new MenuNode { Id = "help", Title = "Trợ giúp", RawName = "TRỢ GIÚP" };
            m.Children.Add(Item("help.guide", "Hướng dẫn sử dụng", null, MenuAction.OpenTab));
            m.Children.Add(Item("help.start", "Bắt đầu sử dụng phần mềm", null, MenuAction.OpenTab));
            m.Children.Add(Item("help.tips", "Mẹo vặt khi sử dụng phần mềm", null, MenuAction.OpenTab));
            m.Children.Add(Item("help.terms", "Thỏa thuận sử dụng phần mềm", null, MenuAction.OpenTab));
            m.Children.Add(Sep("help.sep1"));
            m.Children.Add(Item("help.about", "Thông tin phần mềm", null, MenuAction.OpenTab));
            return m;
        }

        // ------------------------------------------------------------------ thanh công cụ

        private static void BuildToolbar(MenuModel model, List<MenuNode> dbRoots, FunctionPermissions perms)
        {
            var found = new List<MenuNode>();
            foreach (MenuNode root in dbRoots)
                CollectToolbar(root, perms, found);
            found.Sort(delegate (MenuNode a, MenuNode b) { return a.ToolbarIndex.CompareTo(b.ToolbarIndex); });

            foreach (MenuNode n in found)
            {
                var tb = new MenuNode
                {
                    Id = n.Id,
                    Title = n.IsSeparator ? "" : DisplayToolbarTitle(n),
                    RawName = n.RawName,
                    IsSeparator = n.IsSeparator,
                    FunctionId = n.FunctionId,
                    Shortcut = n.Shortcut,
                    Action = n.Action
                };
                model.Toolbar.Add(tb);
            }
            TrimSeparators(model.Toolbar);
        }

        private static string DisplayToolbarTitle(MenuNode n)
        {
            return string.IsNullOrWhiteSpace(n.ToolbarTitle)
                ? n.Title
                : MenuRepository.DisplayText(n.ToolbarTitle);
        }

        private static void CollectToolbar(MenuNode n, FunctionPermissions perms, List<MenuNode> into)
        {
            if (n.InToolbar && (n.IsSeparator || (n.IsLeaf && perms.AllowsId(n.FunctionId))))
                into.Add(n);
            foreach (MenuNode c in n.Children)
                CollectToolbar(c, perms, into);
        }

        // ------------------------------------------------------------------ Truy cập nhanh

        private static void BuildQuickGroups(MenuModel model, List<MenuNode> prunedRoots)
        {
            foreach (MenuNode root in prunedRoots)
            {
                var leaves = new List<MenuNode>();
                Flatten(root, leaves);
                // Ưu tiên mục có trên thanh công cụ (chức năng dùng nhiều), rồi tới thứ tự gốc.
                var ordered = new List<MenuNode>();
                var toolbarFirst = new List<MenuNode>();
                foreach (MenuNode l in leaves)
                    if (l.InToolbar)
                        toolbarFirst.Add(l);
                toolbarFirst.Sort(delegate (MenuNode a, MenuNode b) { return a.ToolbarIndex.CompareTo(b.ToolbarIndex); });
                ordered.AddRange(toolbarFirst);
                foreach (MenuNode l in leaves)
                    if (!l.InToolbar)
                        ordered.Add(l);

                var group = new QuickGroup { Title = root.Title, Glyph = GlyphMap.ForGroup(root.Title) };
                foreach (MenuNode l in ordered)
                {
                    if (group.Items.Count >= QuickItemsPerGroup)
                        break;
                    group.Items.Add(l);
                }
                if (group.Items.Count > 0)
                    model.QuickGroups.Add(group);
            }
        }

        private static void Flatten(MenuNode n, List<MenuNode> into)
        {
            foreach (MenuNode c in n.Children)
            {
                if (c.IsSeparator)
                    continue;
                if (c.IsLeaf)
                    into.Add(c);
                else
                    Flatten(c, into);
            }
        }
    }
}
