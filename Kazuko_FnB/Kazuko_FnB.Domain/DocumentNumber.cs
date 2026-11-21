using System;
using System.Text;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Sinh số chứng từ theo mẫu NOTEMPLATE, vd. "DH(yy)/(*****)" → "DH26/00001",
    /// "PT(yy)/(*****)" → "PT26/00001". Thuần (không đụng DB) để kiểm thử được.
    /// Quy ước mẫu: "(yy)" = 2 số cuối năm; dãy "*" = số thứ tự đệm 0 đủ dài.
    /// </summary>
    public static class DocumentNumber
    {
        /// <summary>Định dạng số từ mẫu với ngày và số thứ tự cho trước.
        /// "(yy)" → 2 số cuối của năm; nhóm "(*****)" là placeholder của dãy số — bỏ ngoặc bao
        /// quanh dãy khi thay số (số thật của app gốc là "DH26/00001", không còn ngoặc, xem spec §1.2/§6.2 A3).</summary>
        public static string Format(string template, DateTime date, int sequence)
        {
            if (string.IsNullOrEmpty(template))
                return "";
            string s = template.Replace("(yy)", date.ToString("yy"));
            int first = s.IndexOf('*');
            if (first < 0)
                return s;
            int last = first;
            while (last + 1 < s.Length && s[last + 1] == '*')
                last++;
            int width = last - first + 1;
            string digits = sequence.ToString();
            if (digits.Length < width)
                digits = new string('0', width - digits.Length) + digits;
            bool grouped = first > 0 && s[first - 1] == '(' && last + 1 < s.Length && s[last + 1] == ')';
            int head = grouped ? first - 1 : first;
            int tail = grouped ? last + 2 : last + 1;
            return s.Substring(0, head) + digits + s.Substring(tail);
        }

        /// <summary>
        /// Phần cố định đứng trước dãy số trong mẫu với ngày cho trước,
        /// vd. "DH26/" của "DH(yy)/(*****)". Dùng để tìm các số đã có (NAME STARTING WITH).
        /// Bỏ "(" mở nhóm placeholder khỏi tiền tố (như Format).
        /// </summary>
        public static string LiteralPrefix(string template, DateTime date)
        {
            if (string.IsNullOrEmpty(template))
                return "";
            string s = template.Replace("(yy)", date.ToString("yy"));
            int star = s.IndexOf('*');
            if (star < 0)
                return s;
            if (star > 0 && s[star - 1] == '(')
                return s.Substring(0, star - 1);
            return s.Substring(0, star);
        }

        /// <summary>Số chữ số của dãy "*" trong mẫu (0 nếu mẫu không có dãy số).</summary>
        public static int StarCount(string template)
        {
            if (string.IsNullOrEmpty(template))
                return 0;
            int first = template.IndexOf('*');
            if (first < 0)
                return 0;
            int n = 0;
            while (first + n < template.Length && template[first + n] == '*')
                n++;
            return n;
        }

        /// <summary>
        /// Số thứ tự ở cuối tên (phần số sau tiền tố); -1 nếu tên không khớp mẫu
        /// (không bắt đầu bằng tiền tố hoặc không kết thúc bằng số).
        /// </summary>
        public static int ExtractSequence(string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix)
                || !name.StartsWith(prefix, StringComparison.Ordinal))
                return -1;
            int end = name.Length;
            int start = end;
            while (start > prefix.Length && char.IsDigit(name[start - 1]))
                start--;
            if (start == end)
                return -1;
            int seq;
            return int.TryParse(name.Substring(start, end - start), out seq) ? seq : -1;
        }

        /// <summary>Số thứ tự tiếp theo sau số lớn nhất đã dùng (0 nếu chưa có số nào).</summary>
        public static int NextSequence(string[] existingNames, string prefix)
        {
            int max = 0;
            if (existingNames != null)
            {
                foreach (string n in existingNames)
                {
                    int seq = ExtractSequence(n, prefix);
                    if (seq > max)
                        max = seq;
                }
            }
            return max + 1;
        }
    }
}
