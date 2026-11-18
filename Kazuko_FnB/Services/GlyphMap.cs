using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Chọn icon Segoe MDL2 / Segoe Fluent Icons cho mục menu theo tên. Chỉ là bảng tra, dễ chỉnh.
    /// Icon vẽ bằng font nên đổi màu được theo theme.
    /// </summary>
    public static class GlyphMap
    {
        public const string Fallback = "";   // Document

        // Thứ tự quan trọng: mẫu đứng trước thắng.
        private static readonly KeyValuePair<string, string>[] Rules =
        {
            P("tạo phiếu", ""),
            P("ca làm việc", ""),
            P("chấm công", ""),
            P("thưởng", ""),
            P("ngân hàng", ""),
            P("công nợ", ""),
            P("mtb", ""),                  // máy tính bảng
            P("sử dụng dịch vụ", ""),
            P("bán hàng", ""),
            P("đặt phòng", ""),
            P("đặt hàng", ""),
            P("doanh thu", ""),
            P("mặt hàng bán", ""),
            P("hoạt động", ""),
            P("kết quả kinh doanh", ""),
            P("kqkd", ""),
            P("tồn kho", ""),
            P("tồn quỹ", ""),
            P("khách hàng", ""),
            P("nhà cung cấp", ""),
            P("nhân viên", ""),
            P("bảng giá", ""),
            P("mặt hàng", ""),
            P("khuyến mại", ""),
            P("voucher", ""),
            P("phiếu thu", ""),
            P("phiếu chi", ""),
            P("công nợ", ""),
            P("lương", ""),
            P("kiểm kê", ""),
            P("nhập", ""),
            P("xuất", ""),
            P("kho", ""),
            P("cấu hình", ""),
            P("máy in", ""),
            P("cửa hàng", "")
        };

        private static readonly KeyValuePair<string, string>[] GroupRules =
        {
            P("hoạt động", ""),
            P("khách hàng", ""),
            P("kho hàng", ""),
            P("công nợ", ""),
            P("quỹ", ""),
            P("nhân sự", ""),
            P("quản trị", "")
        };

        private static KeyValuePair<string, string> P(string key, string glyph)
        {
            return new KeyValuePair<string, string>(key, glyph);
        }

        public static string ForItem(string title)
        {
            return Find(Rules, title, Fallback);
        }

        public static string ForGroup(string title)
        {
            return Find(GroupRules, title, "");
        }

        private static string Find(KeyValuePair<string, string>[] rules, string title, string fallback)
        {
            string t = (title ?? "").ToLowerInvariant();
            foreach (KeyValuePair<string, string> r in rules)
                if (t.IndexOf(r.Key, StringComparison.Ordinal) >= 0)
                    return r.Value;
            return fallback;
        }
    }
}
