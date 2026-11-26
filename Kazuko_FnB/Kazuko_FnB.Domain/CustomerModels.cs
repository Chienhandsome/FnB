using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng khách hàng (DKHACHHANG — 21 cột theo DB POS TAP thật, không có CMND).</summary>
    public sealed class CustomerRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string TaxCode { get; set; }
        public string EmployeeId { get; set; }
        public DateTime? Birthday { get; set; }
        public int InitialPoints { get; set; }
        public int PriceType { get; set; }
        public string ProvinceId { get; set; }
        public string Facebook { get; set; }
        public string PrepaidCardId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string GroupName { get; set; }
        public string EmployeeName { get; set; }
        public string ProvinceName { get; set; }
        public string PrepaidCardName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static CustomerRow NewDefault()
        {
            return new CustomerRow
            {
                Code = "",
                Name = "",
                GroupId = null,
                Address = "",
                Phone = "",
                Email = "",
                TaxCode = "",
                EmployeeId = null,
                Birthday = null,
                InitialPoints = 0,
                PriceType = 0,
                ProvinceId = null,
                Facebook = "",
                PrepaidCardId = null,
                Note = "",
                Status = 30
            };
        }
    }

    /// <summary>Dòng tra cứu khách hàng dùng chung (SHOWONLOOKUP = 30: Mã, Tên, Địa chỉ, Điện thoại).</summary>
    public sealed class CustomerLookupItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string GroupId { get; set; }
        public int PriceType { get; set; }
        public string PrepaidCardId { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Một dòng nhóm khách hàng (DNHOMKHACHHANG — 22 cột).</summary>
    public sealed class CustomerGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ImageId { get; set; }
        public decimal LoyaltyPoints { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal DiscountGoods { get; set; }
        public decimal DiscountHour { get; set; }
        public decimal DiscountFood { get; set; }
        public decimal DiscountDrink { get; set; }
        public decimal DiscountService { get; set; }
        public decimal DiscountOther { get; set; }

        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int ItemType { get; set; }
        public int AutoId { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public static CustomerGroupRow NewDefault()
        {
            return new CustomerGroupRow
            {
                Name = "",
                Note = "",
                ImageId = null,
                LoyaltyPoints = 0m,
                DiscountTotal = 0m,
                DiscountGoods = 0m,
                DiscountHour = 0m,
                DiscountFood = 0m,
                DiscountDrink = 0m,
                DiscountService = 0m,
                DiscountOther = 0m,
                ParentId = "",
                ParentDir = "",
                SortOrder = "ZZZZ",
                ItemType = 0,
                Status = 30
            };
        }
    }

    /// <summary>Tùy chọn lưu khách hàng (đọc từ SCONFIG ở tầng gọi).</summary>
    public sealed class CustomerSaveOptions
    {
        /// <summary>Cho phép trùng tên khách hàng (ChoPhepTrungTenKhachHang = 30).</summary>
        public bool AllowDuplicateName { get; set; }
    }

    /// <summary>Kết quả lưu khách hàng.</summary>
    public sealed class CustomerSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasCodeError { get; set; }
    }

    /// <summary>Kết quả xóa khách hàng (kèm chi tiết từng bảng tham chiếu đang chặn).</summary>
    public sealed class CustomerDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }
    }

    /// <summary>Kết quả lưu nhóm khách hàng.</summary>
    public sealed class CustomerGroupSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
    }

    /// <summary>Kết quả xóa nhóm khách hàng.</summary>
    public sealed class CustomerGroupDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int CustomerCount { get; set; }
        public int ChildGroupCount { get; set; }
    }

    /// <summary>Trạng thái hiển thị các ô tỉ lệ giảm trên dialog nhóm khách hàng (§4.2, §9.0 A2).</summary>
    public sealed class CustomerGroupDiscountVisibility
    {
        public bool ShowTotalDiscount { get; set; }
        public bool ShowGoodsDiscount { get; set; }
        public bool ShowHourDiscount { get; set; }
    }

    /// <summary>
    /// Chuỗi thông báo nguyên văn, hằng số và chuẩn hóa so trùng của Danh mục khách hàng
    /// (docs/specs/danh-muc-khach-hang.md §4.1, §4.2, §4.3, §9.0, §9.0.2).
    /// </summary>
    public static class CustomerText
    {
        public const string FunctionId = "36c3bad1-9d2e-4916-98e0-efb6c0e681e2";
        public const string FunctionName = "Danh mục khách hàng";
        public const int MaxTextLength = 255;

        public static readonly string[] PriceTypeNames =
        {
            "Giá theo danh mục",
            "Giá bán 2",
            "Giá bán 3",
            "Giá bán 4",
            "Giá gần nhất"
        };

        public const string NameEmpty = "Trường \"Tên khách hàng\" không được trống!";
        public const string CodeEmpty = "Trường \"Mã khách\" không được trống!";
        public const string GroupNameEmpty = "Trường \"Tên nhóm khách hàng\" không được trống!";
        public const string PointsInvalid = "Điểm tích lũy ban đầu chỉ nhận số nguyên từ 0 trở lên!";
        public const string GroupPointsInvalid = "Điểm tích lũy của nhóm không được âm!";
        public const string PriceTypeInvalid = "Loại giá bán không hợp lệ!";

        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên khách hàng\" đã tồn tại!", name ?? "");
        }

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã khách\" đã tồn tại!", code ?? "");
        }

        public static string GroupNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm khách hàng\" đã tồn tại!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string DiscountOutOfRange(string label)
        {
            return string.Format("Trường \"{0}\" phải nằm trong khoảng 0 đến 100%!", label);
        }

        public static string DeleteGroupConfirm(string groupName)
        {
            return string.Format("Bạn có chắc chắn xóa nhóm khách hàng '{0}' không?", groupName ?? "");
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột (Q4, §9.0.2): không phân biệt hoa/thường và dấu,
        /// KHÔNG cắt khoảng trắng đầu/cuối ("an", "AN", "ân" trùng nhau; "  an  " khác "an").
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark)
                    continue;
                if (ch == 'đ' || ch == 'Đ')
                    sb.Append('d');
                else
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
