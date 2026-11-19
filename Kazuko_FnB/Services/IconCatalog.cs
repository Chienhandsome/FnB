using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Icon màu (bộ Flat Color Icons, đã đổi sẵn sang PNG 96px trong Assets\icons) cho mục menu / nút / thẻ.
    /// Tra theo tên hiển thị bằng bảng quy tắc (mẫu đứng trước thắng). Thiếu file ảnh thì trả null để nơi gọi
    /// quay về icon font (GlyphMap).
    /// </summary>
    public static class IconCatalog
    {
        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();
        private static readonly object Gate = new object();

        private static KeyValuePair<string, string> P(string key, string icon)
        {
            return new KeyValuePair<string, string>(key, icon);
        }

        private static readonly KeyValuePair<string, string>[] ItemRules =
        {
            // Báo cáo (nhóm trước, mục con sau)
            P("báo cáo quỹ", "safe"), P("báo cáo danh mục", "todo_list"), P("báo cáo bán hàng", "sales_performance"),
            P("báo cáo đặt hàng", "planner"), P("báo cáo kho hàng", "package"), P("báo cáo công nợ", "debt"),
            P("báo cáo quản trị", "manager"), P("báo cáo biểu đồ", "pie_chart"),
            P("biểu đồ doanh số", "pie_chart"), P("biểu đồ doanh thu ngày", "line_chart"),
            P("biểu đồ doanh thu tháng", "bar_chart"), P("biểu đồ", "bar_chart"),
            P("báo cáo", "statistics"),

            // Hệ thống
            P("tạo mới cơ sở dữ liệu", "add_database"), P("sao lưu", "data_backup"), P("phục hồi", "data_recovery"),
            P("dữ liệu ban đầu", "database"), P("cơ sở dữ liệu", "database"),
            P("đổi mật khẩu", "key"), P("đăng xuất", "export"), P("thoát", "cancel"),
            P("đăng ký bản quyền", "data_protection"), P("thông tin phần mềm", "info"),

            // Quản trị
            P("thư viện ảnh", "gallery"), P("thiết kế giao diện", "touchscreen_smartphone"),
            P("xóa dữ liệu", "delete_database"), P("công cụ nhà phát triển", "command_line"),
            P("lịch sử tương tác", "timeline"), P("người dùng", "manager"), P("cấu hình toàn", "settings"),
            P("máy in", "print"), P("nhập dữ liệu", "import"),

            // Hoạt động
            P("dịch vụ mtb", "tablet_android"), P("mtb", "tablet_android"), P("sử dụng dịch vụ", "services"),
            P("bảng giá", "data_sheet"), P("cửa hàng", "shop"), P("loại phòng", "department"),
            P("bàn khu vực", "grid"), P("cấu hình bàn", "grid"), P("thông báo sửa chữa", "support"),
            P("khách đặt hàng", "planner"), P("đặt phòng", "calendar"), P("đặt hàng", "planner"),
            P("điều chỉnh hóa đơn", "view_details"), P("quản lý bán hàng", "calculator"),
            P("lưu vết", "timeline"), P("chi tiết hoạt động", "timeline"),
            P("thống kê doanh thu", "bar_chart"), P("thống kê mặt hàng bán", "combo_chart"),
            P("kết quả kinh doanh", "sales_performance"), P("kqkd", "sales_performance"),
            P("hóa đơn hủy", "delete_row"), P("thống kê", "statistics"), P("tổng hợp", "statistics"),

            // Khách hàng
            P("thân thiết", "vip"), P("gửi tin nhắn", "sms"), P("khuyến mại", "advertising"),
            P("voucher", "like"), P("thẻ trả trước", "paid"),

            // Công nợ / quỹ
            P("công nợ", "debt"), P("tồn quỹ", "safe"), P("tạo phiếu thu", "plus"), P("tạo phiếu chi", "minus"),
            P("phiếu", "document"), P("thu chi", "todo_list"), P("ngân hàng", "money_transfer"),

            // Nhân sự
            P("nhân viên", "businessman"), P("ca làm việc", "clock"), P("chấm công", "overtime"),
            P("tạm ứng", "paid"), P("thưởng", "rating"), P("lương", "money_transfer"),

            // Kho
            P("tồn kho", "package"), P("mặt hàng", "icons8_cup"), P("nhà cung cấp", "business_contact"),
            P("nhập", "import"), P("xuất lại", "refresh"), P("xuất", "export"), P("chuyển kho", "in_transit"),
            P("kiểm kê", "inspection"), P("giá vốn", "synchronize"), P("kho", "filing_cabinet"),

            P("khách hàng", "contacts"), P("hoạt động", "timeline")
        };

        private static readonly KeyValuePair<string, string>[] GroupRules =
        {
            P("hệ thống", "settings"), P("hoạt động", "shop"), P("khách hàng", "contacts"), P("kho hàng", "package"),
            P("công nợ", "debt"), P("quỹ", "safe"), P("nhân sự", "businessman"), P("quản trị", "manager"),
            P("báo cáo", "statistics"), P("trợ giúp", "faq")
        };

        /// <summary>Icon cho mục menu / chức năng; null nếu chưa có file ảnh.</summary>
        public static Image ForItem(string title)
        {
            return Load(Find(ItemRules, title, "document"));
        }

        /// <summary>Icon cho nhóm menu (Hệ thống, Hoạt động, ...); null nếu chưa có file ảnh.</summary>
        public static Image ForGroup(string title)
        {
            return Load(Find(GroupRules, title, "document"));
        }

        /// <summary>Icon theo tên file (không đuôi), vd. "flash_on".</summary>
        public static Image Named(string name)
        {
            return Load(name);
        }

        private static string Find(KeyValuePair<string, string>[] rules, string title, string fallback)
        {
            string t = (title ?? "").ToLowerInvariant();
            foreach (KeyValuePair<string, string> r in rules)
                if (t.IndexOf(r.Key, StringComparison.Ordinal) >= 0)
                    return r.Value;
            return fallback;
        }

        private static Image Load(string name)
        {
            lock (Gate)
            {
                Image img;
                if (Cache.TryGetValue(name, out img))
                    return img;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icons", name + ".png");
                img = null;
                if (File.Exists(path))
                {
                    try
                    {
                        // Đọc qua bộ nhớ để không khóa file.
                        using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                        using (Image tmp = Image.FromStream(ms))
                            img = new Bitmap(tmp);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
                Cache[name] = img;
                return img;
            }
        }
    }
}
