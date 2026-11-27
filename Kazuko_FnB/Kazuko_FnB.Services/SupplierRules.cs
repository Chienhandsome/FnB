using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của Danh mục nhà cung cấp (DNHACUNGCAP / DNHOMNHACUNGCAP,
    /// spec docs/specs/danh-muc-nha-cung-cap/). Không đụng DB, không đụng UI.
    /// </summary>
    public static class SupplierRules
    {
        /// <summary>
        /// Sinh mã nhà cung cấp kế tiếp (theo danh-muc-khach-hang.md §9.0.2, tiền tố NCC):
        /// số lớn nhất trong các mã dạng NCC + chữ số, cộng 1, đệm 5 chữ số; mã khác dạng bị bỏ qua.
        /// Bảng trống → NCC00001.
        /// </summary>
        public static string NextCode(IEnumerable<string> existingCodes)
        {
            long max = 0;
            if (existingCodes != null)
            {
                foreach (string code in existingCodes)
                {
                    long n;
                    if (TryParseNccNumber(code, out n) && n > max)
                        max = n;
                }
            }
            return SupplierText.CodePrefix + (max + 1).ToString("D5", CultureInfo.InvariantCulture);
        }

        private static bool TryParseNccNumber(string code, out long number)
        {
            number = 0;
            if (string.IsNullOrEmpty(code) || code.Length < 4)
                return false;
            if ((code[0] != 'N' && code[0] != 'n')
                || (code[1] != 'C' && code[1] != 'c')
                || (code[2] != 'C' && code[2] != 'c'))
                return false;
            for (int i = 3; i < code.Length; i++)
            {
                if (code[i] < '0' || code[i] > '9')
                    return false;
            }
            return long.TryParse(code.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>So trùng theo collation UNICODE_CI_AI: bỏ phân biệt hoa thường &amp; dấu (kể cả đ/Đ), không cắt khoảng trắng.</summary>
        public static bool AreDuplicate(string left, string right)
        {
            return string.Equals(SupplierText.FoldNoTrim(left), SupplierText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>
        /// Kiểm tra dữ liệu nhà cung cấp trước khi lưu: bắt buộc Tên &amp; Mã, độ dài &lt;= 255 cho mọi cột chữ,
        /// trùng Tên và/hoặc Mã (Tên trước, Mã sau, gộp chung một thông báo).
        /// </summary>
        public static SupplierSaveResult ValidateSupplier(SupplierRow row, bool duplicateName, bool duplicateCode)
        {
            var result = new SupplierSaveResult();
            if (row == null)
                return SupplierSaveResult.Fail("Không có dữ liệu nhà cung cấp.");

            var errors = new List<string>();
            string name = row.Name ?? "";
            string code = row.Code ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(SupplierText.NameEmpty);
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                result.HasCodeError = true;
                errors.Add(SupplierText.CodeEmpty);
            }

            CheckLength(errors, name, "Tên nhà cung cấp", result, true, false);
            CheckLength(errors, code, "Mã nhà cung cấp", result, false, true);
            CheckLength(errors, row.Address, "Địa chỉ", result, false, false);
            CheckLength(errors, row.Phone, "Điện thoại", result, false, false);
            CheckLength(errors, row.Email, "Email", result, false, false);
            CheckLength(errors, row.Website, "Website", result, false, false);
            CheckLength(errors, row.Note, "Ghi chú", result, false, false);

            if (errors.Count == 0)
            {
                if (duplicateName)
                {
                    result.HasNameError = true;
                    errors.Add(SupplierText.NameDuplicate(name));
                }
                if (duplicateCode)
                {
                    result.HasCodeError = true;
                    errors.Add(SupplierText.CodeDuplicate(code));
                }
            }

            if (errors.Count > 0)
            {
                result.ErrorMessage = string.Join(Environment.NewLine, errors.ToArray());
                return result;
            }
            result.Ok = true;
            return result;
        }

        private static void CheckLength(List<string> errors, string value, string label,
            SupplierSaveResult result, bool isName, bool isCode)
        {
            if (value != null && value.Length > SupplierText.MaxTextLength)
            {
                if (isName) result.HasNameError = true;
                if (isCode) result.HasCodeError = true;
                errors.Add(SupplierText.FieldTooLong(label, SupplierText.MaxTextLength));
            }
        }

        /// <summary>Kiểm tra dữ liệu nhóm nhà cung cấp: tên bắt buộc, độ dài &lt;= 255, không trùng tên.</summary>
        public static SupplierGroupSaveResult ValidateGroup(SupplierGroupRow row, bool duplicateName)
        {
            if (row == null)
                return SupplierGroupSaveResult.Fail("Không có dữ liệu nhóm nhà cung cấp.");

            string name = row.Name ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return SupplierGroupSaveResult.Fail(SupplierText.GroupNameEmpty);
            if (name.Length > SupplierText.MaxTextLength)
                return SupplierGroupSaveResult.Fail(SupplierText.FieldTooLong("Tên nhóm nhà cung cấp", SupplierText.MaxTextLength));
            if (row.Note != null && row.Note.Length > SupplierText.MaxTextLength)
                return SupplierGroupSaveResult.Fail(SupplierText.FieldTooLong("Ghi chú", SupplierText.MaxTextLength));
            if (duplicateName)
                return SupplierGroupSaveResult.Fail(SupplierText.GroupNameDuplicate(name));

            return new SupplierGroupSaveResult { Ok = true };
        }

        /// <summary>Kiểm quyền theo SFUNCTION "Danh mục nhà cung cấp" (ID 7ffea55c-2b6e-4b08-9470-4fe5b6b5f0b9 hoặc tên chức năng).</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if ((session.Permissions.ModeById(SupplierText.FunctionId) & bit) == bit)
                return true;
            return session.Permissions.Can(SupplierText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }

        /// <summary>Cắt chuỗi ghi lưu vết cho vừa cột VARCHAR(255) (TLUUVET.BAN, TLUUVET.NOTE).</summary>
        public static string ClipLogText(string text)
        {
            text = text ?? "";
            return text.Length > SupplierText.MaxTextLength
                ? text.Substring(0, SupplierText.MaxTextLength)
                : text;
        }
    }
}
