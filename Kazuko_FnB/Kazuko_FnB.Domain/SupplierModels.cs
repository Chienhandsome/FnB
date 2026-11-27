using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng nhà cung cấp (DNHACUNGCAP, spec docs/specs/danh-muc-nha-cung-cap/).</summary>
    public sealed class SupplierRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string GroupName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static SupplierRow NewDefault()
        {
            return new SupplierRow
            {
                Code = "",
                Name = "",
                GroupId = null,
                Address = "",
                Phone = "",
                Email = "",
                Website = "",
                Note = "",
                Status = SupplierText.ActiveStatus
            };
        }
    }

    /// <summary>Dòng tra cứu nhà cung cấp dùng chung (SHOWONLOOKUP: Tên, Địa chỉ, Điện thoại).</summary>
    public sealed class SupplierLookupItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string GroupId { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Một dòng nhóm nhà cung cấp (DNHOMNHACUNGCAP — bảng cây).</summary>
    public sealed class SupplierGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ImageId { get; set; }
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

        public static SupplierGroupRow NewDefault()
        {
            return new SupplierGroupRow
            {
                Name = "",
                Note = "",
                ImageId = null,
                ParentId = "",
                ParentDir = "",
                SortOrder = "ZZZZ",
                ItemType = 0,
                Status = SupplierText.ActiveStatus
            };
        }
    }

    /// <summary>Kết quả lưu nhà cung cấp.</summary>
    public sealed class SupplierSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasCodeError { get; set; }

        public static SupplierSaveResult Fail(string message, bool hasNameError = false, bool hasCodeError = false)
        {
            return new SupplierSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError,
                HasCodeError = hasCodeError
            };
        }
    }

    /// <summary>Kết quả xóa cứng nhà cung cấp (kèm chi tiết từng bảng tham chiếu đang chặn).</summary>
    public sealed class SupplierDeleteResult
    {
        public SupplierDeleteResult()
        {
            Blocks = new List<ReferenceBlock>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }

        public static SupplierDeleteResult Fail(string message)
        {
            return new SupplierDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả lưu nhóm nhà cung cấp.</summary>
    public sealed class SupplierGroupSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }

        public static SupplierGroupSaveResult Fail(string message)
        {
            return new SupplierGroupSaveResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả xóa cứng nhóm nhà cung cấp.</summary>
    public sealed class SupplierGroupDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int SupplierCount { get; set; }
        public int ChildGroupCount { get; set; }

        public static SupplierGroupDeleteResult Fail(string message)
        {
            return new SupplierGroupDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo nguyên văn và chuẩn hóa so trùng của Danh mục nhà cung cấp (DNHACUNGCAP).</summary>
    public static class SupplierText
    {
        public const string FunctionId = "7ffea55c-2b6e-4b08-9470-4fe5b6b5f0b9";
        public const string FunctionName = "Danh mục nhà cung cấp";
        public const string TableName = "DNHACUNGCAP";
        public const string GroupTableName = "DNHOMNHACUNGCAP";
        public const string CodePrefix = "NCC";
        public const int ActiveStatus = 30;
        public const int MaxTextLength = 255;

        public const string NameEmpty = "Trường \"Tên nhà cung cấp\" không được trống!";
        public const string CodeEmpty = "Trường \"Mã nhà cung cấp\" không được trống!";
        public const string GroupNameEmpty = "Trường \"Tên nhóm nhà cung cấp\" không được trống!";
        public const string NotFound = "Nhà cung cấp không còn tồn tại.";
        public const string GroupNotFound = "Nhóm nhà cung cấp không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhà cung cấp\" đã tồn tại!", name ?? "");
        }

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã nhà cung cấp\" đã tồn tại!", code ?? "");
        }

        public static string GroupNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm nhà cung cấp\" đã tồn tại!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string DeleteGroupConfirm(string groupName)
        {
            return string.Format("Bạn có chắc chắn xóa nhóm nhà cung cấp '{0}' không?", groupName ?? "");
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột (không phân biệt hoa/thường và dấu,
        /// kể cả đ/Đ; KHÔNG cắt khoảng trắng đầu/cuối — theo danh-muc-khach-hang.md §9.0.2).
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
