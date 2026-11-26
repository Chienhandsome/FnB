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
        private static readonly NumberFormatInfo ViNumberFormat = CreateViNumberFormat();

        private static NumberFormatInfo CreateViNumberFormat()
        {
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberGroupSizes = new[] { 3 };
            nfi.NegativeSign = "-";
            return NumberFormatInfo.ReadOnly(nfi);
        }

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

        /// <summary>
        /// Đổi số sang chữ khi vào chế độ sửa cho ô nhập số ngoài lưới: không dấu ngăn nghìn,
        /// thập phân ',', chỉ hiện chữ số lẻ đang có, tối đa <paramref name="maxDecimalPlaces"/> chữ số lẻ.
        /// </summary>
        public static string FormatForEdit(decimal value, int maxDecimalPlaces)
        {
            return FormatForDisplay(value, maxDecimalPlaces, false);
        }

        /// <summary>
        /// Định dạng hiển thị kiểu vi-VN (ngăn nghìn '.', thập phân ',') độc lập CultureInfo.CurrentCulture:
        /// chỉ hiện số chữ số lẻ đang có (15.000 không phải 15.000,00; 2,5 không phải 2,50), tối đa <paramref name="maxDecimalPlaces"/>.
        /// </summary>
        public static string FormatForDisplay(decimal value, int maxDecimalPlaces, bool thousandsSeparator)
        {
            int dp = maxDecimalPlaces < 0 ? 0 : (maxDecimalPlaces > 28 ? 28 : maxDecimalPlaces);
            decimal rounded = Math.Round(value, dp, MidpointRounding.AwayFromZero);
            if (rounded == 0m)
                return "0";
            string prefix = thousandsSeparator ? "#,##0" : "0";
            string fmt = dp > 0 ? prefix + "." + new string('#', dp) : prefix;
            return rounded.ToString(fmt, ViNumberFormat);
        }

        /// <summary>
        /// Đọc chuỗi người dùng gõ vào ô ngoài lưới theo N1/N4/N5:
        /// - Không đọc được theo N1 -> false.
        /// - Ô chỉ nhận số nguyên (N5, <paramref name="integerOnly"/> = true): có phần lẻ khác 0 -> false (giữ số cũ, không làm tròn).
        /// - Ô số thập phân (N4): làm tròn AwayFromZero về tối đa <paramref name="maxDecimalPlaces"/> chữ số lẻ.
        /// </summary>
        public static bool TryParseForBox(string text, int maxDecimalPlaces, bool integerOnly, out decimal value)
        {
            value = 0m;
            decimal raw;
            if (!TryParse(text, out raw))
                return false;
            if (integerOnly)
            {
                if (raw != Math.Truncate(raw))
                    return false;
                decimal truncated = Math.Truncate(raw);
                value = truncated == 0m ? 0m : truncated;
                return true;
            }
            int dp = maxDecimalPlaces < 0 ? 0 : (maxDecimalPlaces > 28 ? 28 : maxDecimalPlaces);
            decimal rounded = Math.Round(raw, dp, MidpointRounding.AwayFromZero);
            value = NormalizeZeroAndScale(rounded);
            return true;
        }

        /// <summary>Đổi mọi dấu '.' sang ',' khi người dùng gõ hoặc dán vào ô đang sửa (không nuốt dấu phẩy thứ hai).</summary>
        public static string NormalizeEditInput(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('.') < 0)
                return text;
            return text.Replace('.', ',');
        }

        private static decimal NormalizeZeroAndScale(decimal v)
        {
            if (v == 0m)
                return 0m;
            return v / 1.0000000000000000000000000000m;
        }
    }
}
