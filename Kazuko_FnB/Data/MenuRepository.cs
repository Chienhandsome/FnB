using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Services;

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
            var all = new Dictionary<string, MenuNode>();
            var ordered = new List<MenuNode>();
            using (var conn = KazukoConnection.Open(_databasePath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT ID, NAME, PARENTID, SORTORDER, ITEMTYPE, TOOLBAR, TOOLBARINDEX, " +
                    "TOOLBARTITLTE, SHORTCUT, HIDEINMENU, SFUNCTIONID, STABLEDESCID, OPERATION " +
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
                            Shortcut = ToKeys(Int(r[8])),
                            Hidden = Int(r[9]) == 30,
                            FunctionId = Str(r[10]),
                            StableDescId = Str(r[11]),
                            Operation = Int(r[12])
                        };
                        all[n.Id] = n;
                        ordered.Add(n);
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

        private static Keys ToKeys(int value)
        {
            if (value == 0)
                return Keys.None;
            return (Keys)value;
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
