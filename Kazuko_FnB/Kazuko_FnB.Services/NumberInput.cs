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
        private static readonly NumberFormatInfo ViNumberFormat = CreateViNumberFormat();

        /// <summary>Định dạng số kiểu vi-VN (ngăn nghìn '.', thập phân ',') độc lập CultureInfo của Windows.</summary>
        public static NumberFormatInfo ViFormat
        {
            get { return ViNumberFormat; }
        }

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
            return value.ToString(EditFormat, ViNumberFormat);
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

        /// <summary>
        /// Kiểm tra khi người dùng gõ thêm một chữ số vào ô lưới đang sửa (N4):
        /// chỉ chặn khi chuỗi kết quả có ĐÚNG MỘT dấu thập phân, đọc được theo N1 và số chữ số lẻ vượt quá <paramref name="maxDecimalPlaces"/>.
        /// Chuỗi có 0 hoặc từ 2 dấu trở lên KHÔNG bị chặn để "1.500.000" luôn giữ đủ hai dấu và rời ô bị coi là không đọc được.
        /// </summary>
        public static bool WouldExceedGridDecimalPlaces(
            string currentText,
            int selectionStart,
            int selectionLength,
            char digit,
            int maxDecimalPlaces)
        {
            if (digit < '0' || digit > '9')
                return false;
            string s = currentText ?? "";
            int start = selectionStart < 0 ? 0 : (selectionStart > s.Length ? s.Length : selectionStart);
            int len = selectionLength < 0 ? 0 : (start + selectionLength > s.Length ? s.Length - start : selectionLength);
            string next = NormalizeEditInput(s.Substring(0, start) + digit + s.Substring(start + len));
            int commaIdx;
            if (CountCommas(next, out commaIdx) != 1)
                return false;
            decimal ignored;
            if (!TryParse(next, out ignored))
                return false;
            int end = TrimEndIndex(next);
            int fracDigits = end - (commaIdx + 1);
            int dp = maxDecimalPlaces < 0 ? 0 : maxDecimalPlaces;
            return fracDigits > dp;
        }

        /// <summary>
        /// Chuẩn hóa chuỗi đang sửa trên ô lưới (N1/N4):
        /// - Đổi mọi '.' sang ','.
        /// - Chỉ cắt (không làm tròn) phần chữ số lẻ vượt <paramref name="maxDecimalPlaces"/> khi chuỗi có ĐÚNG MỘT dấu
        ///   và là chuỗi số hợp lệ theo N1.
        /// - Chuỗi có từ hai dấu trở lên (vd. "1.500.000") hoặc chứa chữ cái giữ nguyên đủ dấu để rời ô báo không đọc được.
        /// </summary>
        public static string NormalizeGridEditText(string text, int maxDecimalPlaces)
        {
            string normalized = NormalizeEditInput(text);
            if (string.IsNullOrEmpty(normalized))
                return normalized;
            int commaIdx;
            if (CountCommas(normalized, out commaIdx) != 1)
                return normalized;
            decimal ignored;
            if (!TryParse(normalized, out ignored))
                return normalized;
            int end = TrimEndIndex(normalized);
            int fracDigits = end - (commaIdx + 1);
            int dp = maxDecimalPlaces < 0 ? 0 : maxDecimalPlaces;
            if (fracDigits <= dp)
                return normalized;
            if (dp == 0)
                return normalized.Substring(0, commaIdx) + normalized.Substring(end);
            return normalized.Substring(0, commaIdx + 1 + dp) + normalized.Substring(end);
        }

        private static int CountCommas(string s, out int firstIndex)
        {
            int count = 0;
            firstIndex = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == ',')
                {
                    if (count == 0)
                        firstIndex = i;
                    count++;
                }
            }
            return count;
        }

        private static int TrimEndIndex(string s)
        {
            int end = s.Length;
            while (end > 0 && char.IsWhiteSpace(s[end - 1]))
                end--;
            return end;
        }

        private static decimal NormalizeZeroAndScale(decimal v)
        {
            if (v == 0m)
                return 0m;
            return v / 1.0000000000000000000000000000m;
        }
    }
}
