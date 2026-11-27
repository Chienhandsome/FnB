using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Đọc cây menu từ SMENU (chỉ đọc). Chỉ lấy dòng đang bật (STATUS = 30).</summary>
    public sealed class MenuRepository
    {
        private readonly string _databasePath;

        public MenuRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Trả về các nút gốc, con đã sắp theo SORTORDER.</summary>
        public List<MenuNode> LoadTree()
        {
            return DbHelper.Guard(LoadTreeCore);
        }

        private List<MenuNode> LoadTreeCore()
        {
            var all = new Dictionary<string, MenuNode>();
            var ordered = new List<MenuNode>();
            using (var conn = KazukoConnection.Open(_databasePath))
            {
                bool hasVisibleConfig = CatalogRepository.ReadTableColumns(conn, "SMENU").Contains("VISIBLECONFIGID");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT ID, NAME, PARENTID, SORTORDER, ITEMTYPE, TOOLBAR, TOOLBARINDEX, " +
                        "TOOLBARTITLTE, SHORTCUT, HIDEINMENU, SFUNCTIONID, STABLEDESCID, OPERATION, LOAI, SFORMID" +
                        (hasVisibleConfig ? ", VISIBLECONFIGID " : " ") +
                        "FROM SMENU WHERE STATUS = 30 ORDER BY SORTORDER";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string raw = Str(r[1]).Trim();
                            var n = new MenuNode
                            {
                                Id = Str(r[0]),
                                RawName = raw,
                                Title = DisplayText(raw),
                                ParentId = Str(r[2]),
                                SortOrder = Str(r[3]),
                                IsSeparator = Int(r[4]) == 2 || raw.StartsWith("___", StringComparison.Ordinal),
                                InToolbar = Int(r[5]) == 30,
                                ToolbarIndex = Int(r[6]),
                                ToolbarTitle = Str(r[7]).Trim(),
                                Shortcut = Int(r[8]),
                                Hidden = Int(r[9]) == 30,
                                FunctionId = Str(r[10]),
                                StableDescId = Str(r[11]),
                                Operation = Int(r[12]),
                                Loai = Int(r[13]),
                                FormId = Str(r[14]),
                                VisibleConfigId = hasVisibleConfig ? Str(r[15]).Trim() : ""
                            };
                            all[n.Id] = n;
                            ordered.Add(n);
                        }
                    }
                }
            }

            var roots = new List<MenuNode>();
            foreach (MenuNode n in ordered)
            {
                MenuNode parent;
                if (!string.IsNullOrEmpty(n.ParentId) && all.TryGetValue(n.ParentId, out parent))
                    parent.Children.Add(n);
                else
                    roots.Add(n);
            }
            return roots;
        }

        /// <summary>
        /// Mục menu không có SMENU.SFUNCTIONID (vd. "Khách đặt hàng": SFUNCTIONID = NULL) thì
        /// lấy quyền từ STABLEDESC.SFUNCTIONID của bảng nhập liệu mà mục đó trỏ tới (§4 spec
        /// Khách đặt hàng: SFUNCTION "Đặt hàng" dùng chung với "Theo dõi đặt phòng").
        /// Một truy vấn cho cả cây; chỉ điền chỗ đang trống.
        /// </summary>
        public void FillFunctionIds(List<MenuNode> roots)
        {
            if (roots == null)
                return;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var formMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DbHelper.Guard(delegate
            {
                using (var conn = KazukoConnection.Open(_databasePath))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT \"ID\", \"SFUNCTIONID\" FROM \"STABLEDESC\" " +
                            "WHERE \"SFUNCTIONID\" IS NOT NULL AND \"SFUNCTIONID\" <> ''";
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string id = Str(r[0]);
                                string fn = Str(r[1]);
                                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(fn))
                                    map[id] = fn;
                            }
                        }
                    }
                    if (CatalogRepository.ReadTableColumns(conn, "SFORM").Contains("SFUNCTIONID"))
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT \"ID\", \"SFUNCTIONID\" FROM \"SFORM\" " +
                                "WHERE \"SFUNCTIONID\" IS NOT NULL AND \"SFUNCTIONID\" <> ''";
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string id = Str(r[0]);
                                    string fn = Str(r[1]);
                                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(fn))
                                        formMap[id] = fn;
                                }
                            }
                        }
                    }
                }
                return 0;
            });
            foreach (MenuNode root in roots)
                FillNode(root, map, formMap);
        }

        /// <summary>Đọc bản đồ SCONFIG.ID → INTVALUE để lọc các mục SMENU có VISIBLECONFIGID (30 = hiện, khác 30 = ẩn).</summary>
        public Dictionary<string, int> LoadConfigIntValuesById()
        {
            return DbHelper.Guard(delegate
            {
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                using (var conn = KazukoConnection.Open(_databasePath))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, INTVALUE FROM SCONFIG WHERE ID IS NOT NULL";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string id = Str(r[0]).Trim();
                            if (!string.IsNullOrEmpty(id))
                                map[id] = Int(r[1]);
                        }
                    }
                }
                return map;
            });
        }

        private static void FillNode(MenuNode n, Dictionary<string, string> map, Dictionary<string, string> formMap)
        {
            if (string.IsNullOrEmpty(n.FunctionId) && !string.IsNullOrEmpty(n.StableDescId))
            {
                string fn;
                if (map.TryGetValue(n.StableDescId, out fn))
                    n.FunctionId = fn;
            }
            if (string.IsNullOrEmpty(n.FunctionId) && !string.IsNullOrEmpty(n.FormId))
            {
                string fn;
                if (formMap.TryGetValue(n.FormId, out fn))
                    n.FunctionId = fn;
            }
            foreach (MenuNode c in n.Children)
                FillNode(c, map, formMap);
        }

        /// <summary>
        /// DB lưu tên menu bằng CHỮ HOA ("HOẠT ĐỘNG"). Đổi thành chữ thường đầu câu ("Hoạt động")
        /// cho hợp giao diện; tên đã viết thường/hoa lẫn lộn thì giữ nguyên. Bỏ '&amp;' của mnemonic cũ.
        /// </summary>
        public static string DisplayText(string name)
        {
            string s = (name ?? "").Replace("&", "").Trim();
            if (s.Length < 2)
                return s;
            CultureInfo vi = new CultureInfo("vi-VN");
            if (s == s.ToUpper(vi))
                s = char.ToUpper(s[0], vi) + s.Substring(1).ToLower(vi);
            return s;
        }

        private static string Str(object v)
        {
            return v == null || v is DBNull ? "" : v.ToString();
        }

        private static int Int(object v)
        {
            if (v == null || v is DBNull)
                return 0;
            try
            {
                return Convert.ToInt32(v);
            }
            catch (FormatException)
            {
                return 0;
            }
        }
    }
}
