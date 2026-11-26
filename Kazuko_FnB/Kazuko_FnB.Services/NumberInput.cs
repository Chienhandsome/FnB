using System;
using System.Globalization;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc nhập và định dạng sửa dùng chung cho các ô số trong toàn app (Chiến chốt 26/11/2026):
    /// - Dấu '.' hay ',' do người dùng gõ đều là dấu THẬP PHÂN (tối đa một dấu);
    ///   dấu ngăn hàng nghìn chỉ do app thêm khi hiển thị.
    /// - Chuỗi hợp lệ: bỏ khoảng trắng đầu/cuối; tùy chọn dấu '-' ở đầu; chữ số 0-9 và tối đa một dấu '.' hoặc ','.
    /// - Từ hai dấu trở lên (1.500.000, 1,500.5), chữ, trống, null, DBNull -> không đọc được.
    /// - Khi vào chế độ sửa, đổi số sang chuỗi không có dấu ngăn nghìn và giữ đủ phần thập phân đang có.
    /// </summary>
    public static class NumberInput
    {
        private const string EditFormat = "0.############################";
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static bool TryParse(object rawValue, out decimal value)
        {
            value = 0m;
            if (rawValue == null || rawValue is DBNull)
                return false;
            if (rawValue is decimal)
            {
                value = (decimal)rawValue;
                return true;
            }
            string s = Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            return TryParse(s, out value);
        }

        public static bool TryParse(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrEmpty(text))
                return false;
            string s = text.Trim();
            if (s.Length == 0)
                return false;

            int i = 0;
            if (s[0] == '-')
                i = 1;
            if (i >= s.Length)
                return false;

            int sepIndex = -1;
            bool hasDigit = false;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9')
                {
                    hasDigit = true;
                }
                else if (c == '.' || c == ',')
                {
                    if (sepIndex >= 0)
                        return false;
                    sepIndex = i;
                }
                else
                {
                    return false;
                }
            }
            if (!hasDigit)
                return false;

            string normalized = sepIndex >= 0 ? s.Replace(',', '.') : s;
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value);
        }

        public static string FormatForEdit(decimal value)
        {
            if (value == 0m)
                return "0";
            return value.ToString(EditFormat, ViCulture);
        }

        public static string FormatForEdit(object rawValue)
        {
            if (rawValue == null || rawValue is DBNull)
                return "";
            if (rawValue is decimal)
                return FormatForEdit((decimal)rawValue);
            decimal parsed;
            return TryParse(rawValue, out parsed) ? FormatForEdit(parsed) : "";
        }
    }
}
