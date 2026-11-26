using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của "Danh mục khách hàng" (docs/specs/danh-muc-khach-hang.md §4, §9.0, §9.0.2).
    /// Không đụng DB, không đụng giao diện — dùng chung cho UI và kiểm thử tự động.
    /// </summary>
    public static class CustomerRules
    {
        /// <summary>
        /// Sinh mã khách kế tiếp (§9.0.2 Q3): số lớn nhất trong các mã dạng KH + chữ số, cộng 1, đệm 5 chữ số.
        /// Mã khác dạng (vd. "001", "VIP1") bị bỏ qua; bảng trống → "KH00001".
        /// </summary>
        public static string NextCode(IEnumerable<string> existingCodes)
        {
            long max = 0;
            if (existingCodes != null)
            {
                foreach (string code in existingCodes)
                {
                    long n;
                    if (TryParseKhNumber(code, out n) && n > max)
                        max = n;
                }
            }
            return "KH" + (max + 1).ToString("D5", CultureInfo.InvariantCulture);
        }

        private static bool TryParseKhNumber(string code, out long number)
        {
            number = 0;
            if (string.IsNullOrEmpty(code) || code.Length < 3)
                return false;
            if ((code[0] != 'K' && code[0] != 'k') || (code[1] != 'H' && code[1] != 'h'))
                return false;
            for (int i = 2; i < code.Length; i++)
            {
                if (code[i] < '0' || code[i] > '9')
                    return false;
            }
            return long.TryParse(code.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>So trùng theo collation UNICODE_CI_AI (Q4, §9.0.2): bỏ phân biệt hoa thường &amp; dấu, không cắt khoảng trắng.</summary>
        public static bool AreDuplicate(string left, string right)
        {
            return string.Equals(CustomerText.FoldNoTrim(left), CustomerText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>Điểm tích lũy ban đầu (Q5, §9.0): chỉ nhận số nguyên từ 0 trở lên.</summary>
        public static bool IsValidInitialPoints(decimal value)
        {
            return value >= 0m && value <= int.MaxValue && value == Math.Truncate(value);
        }

        /// <summary>Kiểm tra dữ liệu khách hàng trước khi lưu (lỗi Tên trước, Mã sau; gộp chung một thông báo — §9.0.2).</summary>
        public static CustomerSaveResult ValidateCustomer(
            CustomerRow row,
            bool duplicateName,
            bool duplicateCode,
            bool allowDuplicateName)
        {
            var result = new CustomerSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu khách hàng.";
                return result;
            }

            var errors = new List<string>();
            string name = row.Name ?? "";
            string code = row.Code ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(CustomerText.NameEmpty);
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                result.HasCodeError = true;
                errors.Add(CustomerText.CodeEmpty);
            }

            CheckLength(errors, name, "Tên khách hàng", result, true, false);
            CheckLength(errors, code, "Mã khách", result, false, true);
            CheckLength(errors, row.Address, "Địa chỉ", result, false, false);
            CheckLength(errors, row.Phone, "Điện thoại", result, false, false);
            CheckLength(errors, row.Email, "Email", result, false, false);
            CheckLength(errors, row.TaxCode, "Mã số thuế", result, false, false);
            CheckLength(errors, row.Facebook, "Facebook", result, false, false);
            CheckLength(errors, row.Note, "Ghi chú", result, false, false);

            if (row.InitialPoints < 0)
                errors.Add(CustomerText.PointsInvalid);
            if (row.PriceType < 0 || row.PriceType >= CustomerText.PriceTypeNames.Length)
                errors.Add(CustomerText.PriceTypeInvalid);

            if (errors.Count == 0)
            {
                if (!allowDuplicateName && duplicateName)
                {
                    result.HasNameError = true;
                    errors.Add(CustomerText.NameDuplicate(name));
                }
                if (duplicateCode)
                {
                    result.HasCodeError = true;
                    errors.Add(CustomerText.CodeDuplicate(code));
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
            CustomerSaveResult result, bool isName, bool isCode)
        {
            if (value != null && value.Length > CustomerText.MaxTextLength)
            {
                if (isName) result.HasNameError = true;
                if (isCode) result.HasCodeError = true;
                errors.Add(CustomerText.FieldTooLong(label, CustomerText.MaxTextLength));
            }
        }

        /// <summary>Kiểm tra dữ liệu nhóm khách hàng (§4.2, §9.0 Q9: tỉ lệ giảm trong 0..100%).</summary>
        public static CustomerGroupSaveResult ValidateGroup(CustomerGroupRow row, bool duplicateName)
        {
            var result = new CustomerGroupSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu nhóm khách hàng.";
                return result;
            }
            string name = row.Name ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                result.ErrorMessage = CustomerText.GroupNameEmpty;
                return result;
            }
            if (name.Length > CustomerText.MaxTextLength)
            {
                result.ErrorMessage = CustomerText.FieldTooLong("Tên nhóm khách hàng", CustomerText.MaxTextLength);
                return result;
            }
            if (row.Note != null && row.Note.Length > CustomerText.MaxTextLength)
            {
                result.ErrorMessage = CustomerText.FieldTooLong("Ghi chú", CustomerText.MaxTextLength);
                return result;
            }
            if (row.LoyaltyPoints < 0m)
            {
                result.ErrorMessage = CustomerText.GroupPointsInvalid;
                return result;
            }
            string rateErr = CheckRate(row.DiscountTotal, "Tỉ lệ giảm giá")
                ?? CheckRate(row.DiscountGoods, "Tỉ lệ giảm giá tiền hàng")
                ?? CheckRate(row.DiscountHour, "Tỉ lệ giảm giá tiền giờ")
                ?? CheckRate(row.DiscountFood, "Tỉ lệ giảm đồ ăn")
                ?? CheckRate(row.DiscountDrink, "Tỉ lệ giảm đồ uống")
                ?? CheckRate(row.DiscountService, "Tỉ lệ giảm dịch vụ")
                ?? CheckRate(row.DiscountOther, "Tỉ lệ giảm đồ khác");
            if (rateErr != null)
            {
                result.ErrorMessage = rateErr;
                return result;
            }
            if (duplicateName)
            {
                result.ErrorMessage = CustomerText.GroupNameDuplicate(name);
                return result;
            }
            result.Ok = true;
            return result;
        }

        private static string CheckRate(decimal value, string label)
        {
            return value < 0m || value > 100m ? CustomerText.DiscountOutOfRange(label) : null;
        }

        /// <summary>
        /// Quy tắc ẩn/hiện các ô tỉ lệ giảm trên dialog nhóm (§4.2 + §9.0 A2):
        /// - "Tỉ lệ giảm giá" (giảm tổng): LUÔN hiện (§9.0 A2 thắng §4.2).
        /// - "Tỉ lệ giảm giá tiền giờ": chỉ hiện khi CoTienGio = true và GopChung = false.
        /// - "Tỉ lệ giảm giá tiền hàng": ẩn khi CoTienGio = true và GopChung = true; ngược lại hiện.
        /// </summary>
        public static CustomerGroupDiscountVisibility GetDiscountVisibility(bool coTienGio, bool gopChung)
        {
            return new CustomerGroupDiscountVisibility
            {
                ShowTotalDiscount = true,
                ShowGoodsDiscount = !(coTienGio && gopChung),
                ShowHourDiscount = coTienGio && !gopChung
            };
        }

        /// <summary>Kiểm quyền theo SFUNCTION "Danh mục khách hàng" (tra cả theo ID và tên chức năng).</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if ((session.Permissions.ModeById(CustomerText.FunctionId) & bit) == bit)
                return true;
            return session.Permissions.Can(CustomerText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }
    }
}
