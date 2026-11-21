using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật của chức năng "Danh mục bàn khu vực" (docs/specs/danh-muc-ban-khu-vuc.md), viết thuần để kiểm thử được.
    /// Không đụng DB, không đụng giao diện. Thông báo nguyên văn ở RoomText (Domain) để Data dùng chung.
    /// Quyết định của Chiến 21/11 đã áp ở đây: bỏ thùng rác cả app (xóa cứng); Thêm nhanh TUSO = DENSO sinh
    /// đúng 1 bàn, TUSO &gt; DENSO báo lỗi; nút Excel/In/Tổng ra khỏi v1 ("chưa hỗ trợ").
    /// </summary>
    public static class RoomRules
    {
        /// <summary>Tên chức năng dùng cho kiểm quyền (SFUNCTION "Danh mục bàn khu vực", spec §4).</summary>
        public const string FunctionName = "Danh mục bàn khu vực";

        // ------------------------------------------------------------------ Thêm nhanh (2.2/3.11)

        /// <summary>Số chữ số của một số (0 → 1). Dùng cho ô "Chiều dài số" (mặc định = số chữ số của DENSO lớn nhất).</summary>
        public static int DigitsOf(int n)
        {
            if (n <= 0)
                return 1;
            int d = 0;
            while (n > 0)
            {
                d++;
                n /= 10;
            }
            return d;
        }

        /// <summary>
        /// Kiểm khoảng số Thêm nhanh (Q6): TUSO &lt;= DENSO thì hợp lệ (TUSO = DENSO sinh đúng 1 bàn);
        /// TUSO &gt; DENSO → lỗi "Khoảng số không hợp lệ". Trả null khi hợp lệ.
        /// </summary>
        public static string ValidateRange(int from, int to)
        {
            return from > to ? RoomText.InvalidRange : null;
        }

        /// <summary>
        /// Sinh tên bàn: tiền tố + số đệm 0 đến đủ chiều dài (3.11). Giả thiết khoảng đã hợp lệ
        /// (ValidateRange trả null). Số lượng tên = to - from + 1.
        /// </summary>
        public static List<string> GenerateNames(string prefix, int from, int to, int digits)
        {
            var names = new List<string>(Math.Max(0, to - from + 1));
            string head = prefix ?? "";
            int width = Math.Max(1, digits);
            for (int n = from; n <= to; n++)
                names.Add(head + n.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width, '0'));
            return names;
        }

        /// <summary>
        /// Ví dụ xem trước ở cột DULIEU (1.4): một tên thì hiện tên đó, nhiều tên thì "đầu ... cuối".
        /// </summary>
        public static string PreviewNames(List<string> names)
        {
            if (names == null || names.Count == 0)
                return "";
            if (names.Count == 1)
                return names[0];
            return names[0] + " ... " + names[names.Count - 1];
        }

        /// <summary>Tên đầu tiên bị trùng trong chính lần sinh (không phân biệt hoa/thường); null nếu không trùng.</summary>
        public static string FindBatchDuplicate(List<string> names)
        {
            if (names == null)
                return null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in names)
            {
                string name = ProductText.NormalizeName(raw);
                if (name.Length == 0)
                    continue;
                if (!seen.Add(name))
                    return name;
            }
            return null;
        }

        // ------------------------------------------------------------------ Cách tính giờ (Q8)

        /// <summary>Chữ hiển thị của CACHTINHGIO trên lưới (chỉ hiện khi CoTienGio bật). Giá trị lạ → ghi rõ số để không giấu dữ liệu.</summary>
        public static string CalcModeText(int value)
        {
            switch (value)
            {
                case RoomCalc.ByArea: return "Theo khu vực";
                case RoomCalc.ByHour: return "Theo giờ";
                case RoomCalc.ByPriceList: return "Theo bảng giá";
                default: return "Không rõ (" + value + ")";
            }
        }

        /// <summary>
        /// Radio nào tương ứng với giá trị CACHTINHGIO đã lưu: "area" / "hour" / "pricelist"; null khi giá trị
        /// không nằm trong ba hằng đã biết (dữ liệu cũ lạ — dialog hiện nhãn chỉ-xem và giữ nguyên khi lưu,
        /// như cách ProductEditDialog giữ loại mặt hàng cũ 4/5/6/7).
        /// </summary>
        public static string CalcModeKey(int value)
        {
            switch (value)
            {
                case RoomCalc.ByArea: return "area";
                case RoomCalc.ByHour: return "hour";
                case RoomCalc.ByPriceList: return "pricelist";
                default: return null;
            }
        }

        /// <summary>Giá trị CACHTINHGIO của radio được chọn (key từ CalcModeKey).</summary>
        public static int CalcValueForKey(string key)
        {
            switch (key)
            {
                case "hour": return RoomCalc.ByHour;
                case "pricelist": return RoomCalc.ByPriceList;
                default: return RoomCalc.ByArea;
            }
        }

        // ------------------------------------------------------------------ Kiểm tra khi lưu

        /// <summary>Kiểm tra bàn khi lưu (theo khuôn 2.1): tên trống, chưa chọn khu vực. Trả null khi hợp lệ.</summary>
        public static string ValidateRoom(string name, string areaId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return RoomText.RoomNameEmpty;
            if (string.IsNullOrEmpty(areaId))
                return RoomText.RoomAreaEmpty;
            return null;
        }

        /// <summary>Kiểm tra khu vực khi lưu: tên trống. Trả null khi hợp lệ.</summary>
        public static string ValidateArea(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return RoomText.AreaNameEmpty;
            return null;
        }

        /// <summary>
        /// Lọc combo "Giá mặt hàng" của khu vực (3.5): luôn có '0' (Hiển thị để lựa chọn) và '1' (Giá chung);
        /// '2'/'3'/'4' chỉ hiện khi SuDungGia2/3/4 bật. Giá trị đang dùng của khu vực vẫn giữ lại dù bị lọc.
        /// </summary>
        public static List<LookupItem> FilterPriceLevels(List<LookupItem> all, bool useGia2, bool useGia3,
            bool useGia4, string currentId)
        {
            var list = new List<LookupItem>();
            if (all == null)
                return list;
            foreach (LookupItem item in all)
            {
                string id = item == null ? null : item.Id;
                if (id == "2" && !useGia2 && !string.Equals(id, currentId, StringComparison.Ordinal))
                    continue;
                if (id == "3" && !useGia3 && !string.Equals(id, currentId, StringComparison.Ordinal))
                    continue;
                if (id == "4" && !useGia4 && !string.Equals(id, currentId, StringComparison.Ordinal))
                    continue;
                list.Add(item);
            }
            return list;
        }
    }
}
