using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Ghép menu chính: "Hệ thống" (cố định trong mã, như app gốc) + các nhóm từ SMENU + "Trợ giúp".
    /// Lọc theo quyền người dùng. Dựng luôn thanh công cụ (SMENU.TOOLBAR = 30) và các thẻ "Truy cập nhanh".
    /// Menu "Báo cáo" của app gốc chưa làm (thuộc G4).
    /// </summary>
    public static class MainMenuBuilder
    {
        private const int QuickItemsPerGroup = 4;

        public static MenuModel Build(UserSession session)
        {
            var model = new MenuModel();
            FunctionPermissions perms;
            List<MenuNode> dbRoots = new List<MenuNode>();
            Dictionary<string, int> configById = null;
            int? prepaidCardConfigVal = null;

            if (!string.IsNullOrEmpty(session.DatabasePath))
            {
                try
                {
                    var users = new UserRepository(session.DatabasePath);
                    perms = session.Permissions ?? users.GetFunctionPermissions(session.UserId, session.IsAdmin);
                    var menus = new MenuRepository(session.DatabasePath);
                    dbRoots = menus.LoadTree();
                    // Mục không có SMENU.SFUNCTIONID thì lấy quyền từ STABLEDESC (vd. "Khách đặt hàng").
                    menus.FillFunctionIds(dbRoots);
                    configById = menus.LoadConfigIntValuesById();
                    ConfigValue pcCfg;
                    if (new ConfigRepository(session.DatabasePath).LoadValues().TryGetValue(PrepaidCardText.ConfigKey, out pcCfg))
                        prepaidCardConfigVal = pcCfg.IntValue;
                }
                catch (Kazuko_FnB.Data.DataAccessException)
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
                MenuNode pruned = Prune(root, perms, configById, prepaidCardConfigVal);
                if (pruned == null || pruned.IsSeparator)
                    continue;
                // Chỉ lược khỏi menu thả xuống; thanh công cụ / Truy cập nhanh vẫn dùng dữ liệu đầy đủ.
                HideFromMenu(pruned);
                if (string.Equals(pruned.Title, "Quản trị", StringComparison.OrdinalIgnoreCase))
                    ArrangeAdminMenu(pruned, perms);
                if (HasRealItem(pruned.Children))
                    model.Roots.Add(pruned);
            }
            model.Roots.Add(BuildReportMenu());
            model.Roots.Add(BuildHelpMenu());

            // Thanh công cụ và thẻ Truy cập nhanh chỉ lấy từ nhóm DB.
            var dbPruned = new List<MenuNode>();
            foreach (MenuNode root in dbRoots)
            {
                MenuNode p = Prune(root, perms, configById, prepaidCardConfigVal);
                if (p != null)
                    dbPruned.Add(p);
            }
            BuildToolbar(model, dbRoots, perms, configById, prepaidCardConfigVal);
            BuildQuickGroups(model, dbPruned);
            return model;
        }

        // ------------------------------------------------------------------ lọc quyền

        /// <summary>
        /// Kiểm tra cờ cấu hình hiện menu cho riêng Danh mục cửa hàng (NhieuCuaHang = 30)
        /// và Danh mục thẻ trả trước (SuDungTheTraTruoc = 30). Các menu khác giữ nguyên hành vi cũ.
        /// </summary>
        private static bool IsVisibleByConfig(MenuNode n, Dictionary<string, int> configById, int? prepaidCardConfigVal)
        {
            if (n == null)
                return true;
            if (IsStoreCatalogMenuNode(n))
            {
                if (string.IsNullOrEmpty(n.VisibleConfigId) || configById == null)
                    return true;
                int val;
                return !configById.TryGetValue(n.VisibleConfigId, out val) || val == SystemConfig.On;
            }
            if (IsPrepaidCardCatalogMenuNode(n))
            {
                if (!string.IsNullOrEmpty(n.VisibleConfigId) && configById != null)
                {
                    int val;
                    if (configById.TryGetValue(n.VisibleConfigId, out val))
                        return val == SystemConfig.On;
                }
                return !prepaidCardConfigVal.HasValue || prepaidCardConfigVal.Value == SystemConfig.On;
            }
            return true;
        }

        private static bool IsStoreCatalogMenuNode(MenuNode n)
        {
            if (n == null)
                return false;
            if (string.Equals(n.FunctionId, StoreText.FunctionId, StringComparison.OrdinalIgnoreCase))
                return true;
            return Dialogs.ScreenFactory.IsStoreCatalogMenu(n, null);
        }

        private static bool IsPrepaidCardCatalogMenuNode(MenuNode n)
        {
            if (n == null)
                return false;
            if (string.Equals(n.FunctionId, PrepaidCardText.FunctionId, StringComparison.OrdinalIgnoreCase))
                return true;
            return Dialogs.ScreenFactory.IsPrepaidCardCatalogMenu(n, null);
        }

        private static bool AllowsLeafPermission(MenuNode n, FunctionPermissions perms)
        {
            if (n == null || perms == null)
                return false;
            if (Dialogs.ScreenFactory.IsEmployeeCatalogMenu(n, null))
                return AllowsByIdOrName(perms, n.FunctionId, EmployeeText.FunctionName);
            if (Dialogs.ScreenFactory.IsShiftCatalogMenu(n, null))
                return AllowsByIdOrName(perms, n.FunctionId, ShiftText.FunctionName);
            if (Dialogs.ScreenFactory.IsRewardPenaltyMenu(n, null))
                return AllowsByIdOrName(perms, n.FunctionId, RewardPenaltyText.FunctionName)
                    || AllowsByIdOrName(perms, n.FunctionId, RewardPenaltyText.AltFunctionName);
            if (Dialogs.ScreenFactory.IsSalaryAdvanceMenu(n))
                return AllowsByIdOrName(perms, string.IsNullOrEmpty(n.FunctionId) ? SalaryAdvanceText.FunctionId : n.FunctionId, SalaryAdvanceText.FunctionName);
            if (Dialogs.ScreenFactory.IsPayrollMenu(n))
                return AllowsByIdOrName(perms, n.FunctionId, PayrollText.PayrollFunctionName);
            if (Dialogs.ScreenFactory.IsAttendanceMenu(n))
                return AllowsByIdOrName(perms, string.IsNullOrEmpty(n.FunctionId) ? PayrollText.AttendanceFunctionId : n.FunctionId, PayrollText.AttendanceFunctionName);
            if (Dialogs.ScreenFactory.IsRepairNoticeMenu(n, null))
                return AllowsByIdOrName(perms, n.FunctionId, RepairNoticeText.FunctionName);
            if (Dialogs.ScreenFactory.IsWifiPasswordMenu(n, null))
                return AllowsByIdOrName(perms, n.FunctionId, WifiPasswordText.FunctionName);
            if (Dialogs.ScreenFactory.IsPrepaidCardCatalogMenu(n, null))
                return AllowsByIdOrName(perms, string.IsNullOrEmpty(n.FunctionId) ? PrepaidCardText.FunctionId : n.FunctionId, PrepaidCardText.FunctionName);
            if (Dialogs.ScreenFactory.IsAllWarehousesInventoryMenu(n))
                return AllowsByIdOrName(perms, n.FunctionId, InventoryConstants.FunctionName);
            if (Dialogs.ScreenFactory.IsOrderControlMenu(n))
                return AllowsByIdOrName(perms, string.IsNullOrEmpty(n.FunctionId) ? OrderControlConstants.FunctionId : n.FunctionId, OrderControlConstants.FunctionName);
            return perms.AllowsId(n.FunctionId);
        }

        private static bool AllowsByIdOrName(FunctionPermissions perms, string functionId, string functionName)
        {
            if (perms.IsAdmin)
                return true;
            if (!string.IsNullOrEmpty(functionId) && perms.ModeById(functionId) != RoleModes.Locked)
                return true;
            return perms.ModeByName(functionName) != RoleModes.Locked;
        }

        /// <summary>Bỏ mục ẩn / ẩn theo VISIBLECONFIGID / không đủ quyền; nhóm rỗng cũng bị bỏ; dọn dòng kẻ thừa.</summary>
        private static MenuNode Prune(MenuNode n, FunctionPermissions perms, Dictionary<string, int> configById, int? prepaidCardConfigVal)
        {
            if (n.IsSeparator)
                return n;
            if (n.Hidden || !IsVisibleByConfig(n, configById, prepaidCardConfigVal))
                return null;

            if (n.IsLeaf)
                return AllowsLeafPermission(n, perms) ? n : null;

            var copy = Clone(n);
            foreach (MenuNode c in n.Children)
            {
                MenuNode pc = Prune(c, perms, configById, prepaidCardConfigVal);
                if (pc != null)
                    copy.Children.Add(pc);
            }
            TrimSeparators(copy.Children);
            return HasRealItem(copy.Children) ? copy : null;
        }

        // Mục có trong dữ liệu nhưng không đưa vào menu thả xuống (theo menu mẫu).
        private static readonly HashSet<string> MenuHiddenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dịch vụ MTB",
            // Menu Hoạt động của app đích (POS TAP) không có các mục này (Chiến gửi ảnh menu, 2026-11-19).
            "Danh mục bảng giá",
            "Danh mục loại phòng"
        };

        private static void HideFromMenu(MenuNode n)
        {
            for (int i = n.Children.Count - 1; i >= 0; i--)
            {
                MenuNode c = n.Children[i];
                if (!c.IsSeparator && c.Title != null && MenuHiddenTitles.Contains(c.Title.Trim()))
                {
                    n.Children.RemoveAt(i);
                    continue;
                }
                HideFromMenu(c);
            }
            TrimSeparators(n.Children);
        }

        /// <summary>
        /// Menu "Quản trị" theo menu mẫu: một số mục lấy từ DB (Dữ liệu ban đầu, Thiết kế giao diện màn cảm ứng),
        /// số còn lại cố định trong mã như app gốc. Mục DB không có trong mẫu (vd. cấu hình máy in) bị lược.
        /// </summary>
        private static void ArrangeAdminMenu(MenuNode admin, FunctionPermissions perms)
        {
            MenuNode initial = FindChild(admin, "Dữ liệu ban đầu");
            MenuNode design = FindChild(admin, "Thiết kế giao diện màn cảm ứng");

            var ordered = new List<MenuNode>();
            AddIfAllowed(ordered, perms, Item("admin.images", "Thư viện ảnh...", "Thư viện ảnh", MenuAction.OpenTab));
            if (initial != null)
                ordered.Add(initial);
            if (design != null)
                ordered.Add(design);
            AddIfAllowed(ordered, perms, Item("admin.wipe", "Xóa dữ liệu", "Xóa dữ liệu", MenuAction.OpenTab));
            AddIfAllowed(ordered, perms, Item("admin.devtools", "Công cụ nhà phát triển", "Công cụ nhà phát triển", MenuAction.OpenTab));
            AddIfAllowed(ordered, perms, Item("admin.audit", "Lịch sử tương tác hệ thống", "Lịch sử tương tác hệ thống", MenuAction.OpenTab));
            AddIfAllowed(ordered, perms, Item("admin.users", "Người dùng và phân quyền", "Quản lý người dùng", MenuAction.OpenTab));
            AddIfAllowed(ordered, perms, Item("admin.config", "Cấu hình toàn hệ thống", "Cấu hình toàn hệ thống", MenuAction.OpenTab));
            // Đổi bảng màu chỉ lưu cục bộ theo người dùng Windows (không đụng DB) nên không gắn quyền chức năng.
            ordered.Add(Item("admin.themes", "Giao diện chương trình", null, MenuAction.OpenTab));

            admin.Children.Clear();
            admin.Children.AddRange(ordered);
        }

        private static void AddIfAllowed(List<MenuNode> into, FunctionPermissions perms, MenuNode n)
        {
            if (perms.AllowsName(n.FunctionName))
                into.Add(n);
        }

        private static MenuNode FindChild(MenuNode parent, string title)
        {
            foreach (MenuNode c in parent.Children)
                if (!c.IsSeparator && c.Title != null && string.Equals(c.Title.Trim(), title, StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
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
                Action = n.Action,
                StableDescId = n.StableDescId,
                FormId = n.FormId,
                Operation = n.Operation,
                Loai = n.Loai,
                VisibleConfigId = n.VisibleConfigId
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
            // Cơ sở dữ liệu... là mục cha có 3 mục con (giống app gốc).
            var db = Item("sys.db", "Cơ sở dữ liệu...", null, MenuAction.OpenTab);
            foreach (MenuNode c in new[]
            {
                Item("sys.db.new", "Tạo mới cơ sở dữ liệu", "Tạo mới cơ sở dữ liệu", MenuAction.OpenTab),
                Item("sys.db.backup", "Sao lưu cơ sở dữ liệu", "Sao lưu cơ sở dữ liệu", MenuAction.OpenTab),
                Item("sys.db.restore", "Phục hồi cơ sở dữ liệu", "Phục hồi cơ sở dữ liệu", MenuAction.OpenTab)
            })
                if (perms.AllowsName(c.FunctionName))
                    db.Children.Add(c);

            var logout = Item("sys.logout", "Đăng xuất khỏi hệ thống", null, MenuAction.Logout);
            logout.Shortcut = (int)(Keys.Control | Keys.Shift | Keys.L);
            var exit = Item("sys.exit", "Thoát khỏi hệ thống", null, MenuAction.Exit);

            var all = new List<MenuNode>
            {
                db,
                Item("sys.password", "Đổi mật khẩu đăng nhập", null, MenuAction.OpenTab),
                Sep("sys.sep1"),
                logout,
                exit
            };
            foreach (MenuNode n in all)
            {
                if (n == db && db.Children.Count == 0)
                    continue;
                if (n.IsSeparator || n == db || perms.AllowsName(n.FunctionName))
                    m.Children.Add(n);
            }
            TrimSeparators(m.Children);
            return m;
        }

        // Menu "Báo cáo" cố định trong mã (theo menu mẫu của app gốc). Báo cáo chưa làm: mở tab tạm.
        // Báo cáo kho hàng và các nhóm con của "Báo cáo bán hàng" (theo nhân viên, khu vực, thu ngân, khách hàng,
        // khác) chưa có nội dung mẫu nên chưa liệt kê.
        private static readonly string[][] ReportGroups =
        {
            new[] { "Báo cáo quỹ",
                "Danh sách phiếu thu theo ngày", "Danh sách phiếu thu theo lý do thu chi",
                "Danh sách phiếu chi theo ngày", "Danh sách phiếu chi theo lý do thu chi",
                "Tổng hợp thu chi theo ngày", "Tổng hợp thu chi theo lý do", "Báo cáo tồn quỹ" },
            new[] { "Báo cáo danh mục",
                "Danh sách khách hàng theo nhóm", "Danh sách khách hàng theo nhân viên",
                "Danh sách nhà cung cấp theo nhóm", "Danh sách đợt khuyến mại",
                "Danh sách mặt hàng theo nhóm", "Danh sách mặt hàng theo hãng sản xuất",
                "Khách hàng đến ngày sinh nhật", "Báo cáo cấu hình bàn khu vực",
                "Công thức định lượng", "Báo cáo chi tiết phân quyền hệ thống" },
            new[] { "Báo cáo bán hàng",
                "Tổng hợp bán hàng theo ngày", "Tổng hợp mặt hàng bán theo ngày",
                "Báo cáo chi tiết bán hàng theo ngày", "Tổng hợp doanh thu theo loại đồ",
                "Tổng hợp doanh thu chưa thanh toán", "Báo cáo bán hàng theo ngày" },
            new[] { "Báo cáo đặt hàng",
                "Danh sách đặt hàng theo ngày", "Danh sách đặt hàng theo khách hàng",
                "Tổng hợp đặt hàng theo ngày", "Tổng hợp đặt hàng theo khách hàng",
                "Tổng hợp mặt hàng đặt theo khách hàng", "Tổng hợp mặt hàng đặt theo ngày" },
            new[] { "Báo cáo công nợ",
                "Đối chiếu công nợ nhà cung cấp", "Báo cáo công nợ nhà cung cấp",
                "Đối chiếu công nợ khách hàng", "Tổng hợp công nợ khách hàng",
                "Tổng hợp công nợ nhà cung cấp", "Báo cáo công nợ khách hàng" },
            new[] { "Báo cáo quản trị",
                "Tổng hợp lãi gộp theo mặt hàng (giá vốn)", "Tổng hợp lãi gộp theo mặt hàng (giá nhập)",
                "Chi tiết lãi theo hóa đơn (giá nhập)", "Chi tiết lãi theo hóa đơn (giá vốn)",
                "Báo cáo kết quả kinh doanh", "Chi tiết hoạt động trong ngày",
                "Báo cáo 20 mặt hàng bán chạy nhất", "Phân tích tình hình bán hàng",
                "Báo cáo bán hàng theo giờ", "Danh sách món xóa, giảm, trả lại" },
            new[] { "Báo cáo biểu đồ",
                "Biểu đồ doanh số theo nhóm", "Biểu đồ doanh thu ngày trong tháng",
                "Biểu đồ doanh thu tháng trong năm", "Biểu đồ theo nhân viên kinh doanh" }
        };

        private static MenuNode BuildReportMenu()
        {
            var m = new MenuNode { Id = "rpt", Title = "Báo cáo", RawName = "BÁO CÁO" };
            for (int g = 0; g < ReportGroups.Length; g++)
            {
                string[] def = ReportGroups[g];
                MenuNode group = Item("rpt." + g, def[0], null, MenuAction.OpenTab);
                for (int i = 1; i < def.Length; i++)
                    group.Children.Add(Item("rpt." + g + "." + i, def[i], null, MenuAction.OpenTab));
                m.Children.Add(group);
            }
            return m;
        }

        private static MenuNode BuildHelpMenu()
        {
            var m = new MenuNode { Id = "help", Title = "Trợ giúp", RawName = "TRỢ GIÚP" };
            m.Children.Add(Item("help.license", "Đăng ký bản quyền...", null, MenuAction.OpenTab));
            m.Children.Add(Item("help.about", "Thông tin phần mềm", null, MenuAction.OpenTab));
            return m;
        }

        // ------------------------------------------------------------------ thanh công cụ

        private static void BuildToolbar(MenuModel model, List<MenuNode> dbRoots, FunctionPermissions perms, Dictionary<string, int> configById, int? prepaidCardConfigVal)
        {
            var found = new List<MenuNode>();
            foreach (MenuNode root in dbRoots)
                CollectToolbar(root, perms, configById, prepaidCardConfigVal, found);
            found.Sort(delegate (MenuNode a, MenuNode b) { return a.ToolbarIndex.CompareTo(b.ToolbarIndex); });

            foreach (MenuNode n in found)
            {
                string full = n.IsSeparator ? "" : DisplayToolbarTitle(n);
                if (!n.IsSeparator && ToolbarHiddenTitles.Contains((n.Title ?? "").Trim()))
                    continue;
                var tb = new MenuNode
                {
                    Id = n.Id,
                    Title = n.IsSeparator ? "" : ShortToolbarTitle(full),
                    FullTitle = full,
                    RawName = n.RawName,
                    IsSeparator = n.IsSeparator,
                    FunctionId = n.FunctionId,
                    Shortcut = n.Shortcut,
                    Action = n.Action,
                    StableDescId = n.StableDescId,
                    FormId = n.FormId,
                    Operation = n.Operation,
                    Loai = n.Loai,
                    VisibleConfigId = n.VisibleConfigId
                };
                model.Toolbar.Add(tb);
            }
            AddExtraToolbarButtons(model, dbRoots, perms, configById, prepaidCardConfigVal);
            TrimSeparators(model.Toolbar);
        }

        // Mục có cờ TOOLBAR trong DB nhưng thanh công cụ của app đích (POS TAP) không hiện
        // (Chiến gửi ảnh thanh công cụ, 2026-11-19). Chỉ lược khỏi thanh công cụ; menu và Truy cập nhanh giữ nguyên.
        private static readonly HashSet<string> ToolbarHiddenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dịch vụ MTB",
            "Theo dõi đặt phòng"
        };

        // Nhãn rút gọn cho nút thanh công cụ (chỉ hiển thị; tên đầy đủ vẫn ở tooltip và FullTitle, DB không đổi).
        private static readonly Dictionary<string, string> ToolbarShortTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sử dụng dịch vụ", "Dịch vụ" },
            { "Quản lý bán hàng", "Quản lý" },
            { "Thống kê doanh thu", "Doanh thu" },
            { "Thống kê mặt hàng bán", "Mặt hàng" },
            { "Chi tiết hoạt động", "Hoạt động" },
            { "Tổng hợp KQKD", "KQKD" }
        };

        private static string ShortToolbarTitle(string full)
        {
            string shortTitle;
            return ToolbarShortTitles.TryGetValue(full ?? "", out shortTitle) ? shortTitle : full;
        }

        // Nút cố định trong mã của app đích, nằm sau các nút lấy từ DB: "|" là dải kẻ ngăn nhóm.
        // "@note", "@report", "@exit" là nút không có mục trong SMENU. Tên khác = mục menu có sẵn (theo phân quyền).
        private static readonly string[] ExtraToolbar =
        {
            "|",
            "@note", "@report", "@exit"
        };

        private static void AddExtraToolbarButtons(MenuModel model, List<MenuNode> dbRoots, FunctionPermissions perms, Dictionary<string, int> configById, int? prepaidCardConfigVal)
        {
            int sep = 0;
            foreach (string key in ExtraToolbar)
            {
                if (key == "|")
                {
                    model.Toolbar.Add(new MenuNode { Id = "tb.sep." + (sep++), Title = "", IsSeparator = true });
                    continue;
                }
                if (key == "@note")
                {
                    model.Toolbar.Add(new MenuNode { Id = "tb.note", Title = "Ghi chú", FullTitle = "Ghi chú", Action = MenuAction.OpenTab });
                    continue;
                }
                if (key == "@report")
                {
                    model.Toolbar.Add(new MenuNode { Id = "tb.report", Title = "Báo cáo", FullTitle = "Báo cáo", Action = MenuAction.OpenTab });
                    continue;
                }
                if (key == "@exit")
                {
                    model.Toolbar.Add(new MenuNode { Id = "tb.exit", Title = "Thoát", FullTitle = "Thoát khỏi hệ thống", Action = MenuAction.Exit });
                    continue;
                }

                MenuNode n = null;
                foreach (MenuNode root in dbRoots)
                {
                    n = FindLeafByTitle(root, key, perms, configById, prepaidCardConfigVal);
                    if (n != null)
                        break;
                }
                if (n == null)
                    continue;
                // Đã có sẵn trên thanh công cụ (do cờ DB) thì không thêm lần nữa.
                bool dup = false;
                foreach (MenuNode e in model.Toolbar)
                    if (e.Id == n.Id)
                        dup = true;
                if (dup)
                    continue;
                model.Toolbar.Add(new MenuNode
                {
                    Id = n.Id,
                    Title = n.Title,
                    FullTitle = n.Title,
                    RawName = n.RawName,
                    FunctionId = n.FunctionId,
                    Shortcut = n.Shortcut,
                    Action = n.Action,
                    StableDescId = n.StableDescId,
                    FormId = n.FormId,
                    Operation = n.Operation,
                    Loai = n.Loai,
                    VisibleConfigId = n.VisibleConfigId
                });
            }
        }

        private static MenuNode FindLeafByTitle(MenuNode n, string title, FunctionPermissions perms, Dictionary<string, int> configById, int? prepaidCardConfigVal)
        {
            if (n.Hidden || n.IsSeparator || !IsVisibleByConfig(n, configById, prepaidCardConfigVal))
                return null;
            if (n.IsLeaf)
                return string.Equals((n.Title ?? "").Trim(), title, StringComparison.OrdinalIgnoreCase) && AllowsLeafPermission(n, perms)
                    ? n : null;
            foreach (MenuNode c in n.Children)
            {
                MenuNode f = FindLeafByTitle(c, title, perms, configById, prepaidCardConfigVal);
                if (f != null)
                    return f;
            }
            return null;
        }

        private static string DisplayToolbarTitle(MenuNode n)
        {
            return string.IsNullOrWhiteSpace(n.ToolbarTitle)
                ? n.Title
                : MenuRepository.DisplayText(n.ToolbarTitle);
        }

        private static void CollectToolbar(MenuNode n, FunctionPermissions perms, Dictionary<string, int> configById, int? prepaidCardConfigVal, List<MenuNode> into)
        {
            if (!IsVisibleByConfig(n, configById, prepaidCardConfigVal))
                return;
            if (n.InToolbar && (n.IsSeparator || (n.IsLeaf && AllowsLeafPermission(n, perms))))
                into.Add(n);
            foreach (MenuNode c in n.Children)
                CollectToolbar(c, perms, configById, prepaidCardConfigVal, into);
        }

        // ------------------------------------------------------------------ Truy cập nhanh

        // Mục quan trọng theo nhóm menu gốc của app (tên đúng như trong menu). Quán cà phê/POS: bán hàng, kho, quỹ, công nợ, nhân sự.
        private static readonly Dictionary<string, string[]> QuickPreferred = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hoạt động", new[] { "Sử dụng dịch vụ", "Quản lý bán hàng", "Danh mục mặt hàng", "Danh mục bảng giá" } },
            { "Khách hàng", new[] { "Danh mục khách hàng", "Danh mục đợt khuyến mại", "Khách hàng thân thiết", "Danh mục voucher" } },
            { "Kho hàng", new[] { "Tồn kho", "Nhập hàng vào kho", "Kiểm kê kho", "Danh mục nhà cung cấp" } },
            { "Công nợ", new[] { "Công nợ khách hàng", "Công nợ nhà cung cấp" } },
            { "Quỹ", new[] { "Tạo phiếu thu", "Tạo phiếu chi", "Tồn quỹ", "Danh mục lý do thu chi" } },
            { "Nhân sự", new[] { "Danh mục nhân viên", "Chấm công", "Tính lương", "Tạm ứng lương" } },
            { "Quản trị", new[] { "Cấu hình máy in hóa đơn theo khu vực", "Nhập dữ liệu từ hệ thống cũ", "Tồn kho ban đầu" } }
        };

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

                // Mục quan trọng chọn tay cho từng nhóm (theo thứ tự liệt kê); chỉ lấy mục người dùng có quyền.
                string[] preferred;
                if (QuickPreferred.TryGetValue((root.Title ?? "").Trim(), out preferred))
                {
                    foreach (string want in preferred)
                    {
                        if (group.Items.Count >= QuickItemsPerGroup)
                            break;
                        MenuNode hit = leaves.Find(delegate (MenuNode l)
                        {
                            return string.Equals((l.Title ?? "").Trim(), want, StringComparison.OrdinalIgnoreCase);
                        });
                        if (hit != null && !group.Items.Contains(hit))
                            group.Items.Add(hit);
                    }
                }
                // Chưa đủ thì bổ sung theo quy tắc cũ, bỏ qua mục ngoài phạm vi (karaoke/đặt phòng, MTB).
                foreach (MenuNode l in ordered)
                {
                    if (group.Items.Count >= QuickItemsPerGroup)
                        break;
                    if (group.Items.Contains(l) || ToolbarHiddenTitles.Contains((l.Title ?? "").Trim()))
                        continue;
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
