using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng mật khẩu wifi (DMATKHAUWIFI, spec docs/specs/quan-tri-nho.md §3).</summary>
    public sealed class WifiPasswordRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi trực tiếp vào DMATKHAUWIFI).
        public string GroupName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static WifiPasswordRow NewDefault()
        {
            return new WifiPasswordRow
            {
                Name = "",
                GroupId = null,
                Note = "",
                Status = WifiPasswordText.ActiveStatus
            };
        }
    }

    /// <summary>Một dòng nhóm wifi (DNHOMWIFI — bảng cây chuẩn).</summary>
    public sealed class WifiGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
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

        public static WifiGroupRow NewDefault()
        {
            return new WifiGroupRow
            {
                Name = "",
                Note = "",
                ParentId = "",
                ParentDir = "",
                SortOrder = "ZZZZ",
                ItemType = 0,
                Status = WifiPasswordText.ActiveStatus
            };
        }
    }

    /// <summary>Kết quả lưu mật khẩu wifi (DMATKHAUWIFI).</summary>
    public sealed class WifiPasswordSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasNoteError { get; set; }

        public static WifiPasswordSaveResult Fail(
            string message,
            bool hasNameError = false,
            bool hasNoteError = false)
        {
            return new WifiPasswordSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError,
                HasNoteError = hasNoteError
            };
        }
    }

    /// <summary>Kết quả xóa cứng mật khẩu wifi (DMATKHAUWIFI).</summary>
    public sealed class WifiPasswordDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static WifiPasswordDeleteResult Fail(string message)
        {
            return new WifiPasswordDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả lưu nhóm wifi (DNHOMWIFI).</summary>
    public sealed class WifiGroupSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }

        public static WifiGroupSaveResult Fail(string message)
        {
            return new WifiGroupSaveResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả xóa cứng nhóm wifi (DNHOMWIFI).</summary>
    public sealed class WifiGroupDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int PasswordCount { get; set; }
        public int ChildGroupCount { get; set; }

        public static WifiGroupDeleteResult Fail(string message)
        {
            return new WifiGroupDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng cho Danh mục mật khẩu wifi (DMATKHAUWIFI / DNHOMWIFI).</summary>
    public static class WifiPasswordText
    {
        public const string FunctionName = "Danh mục mật khẩu wifi";
        public const string TableName = "DMATKHAUWIFI";
        public const string GroupTableName = "DNHOMWIFI";
        public const int ActiveStatus = 30;
        public const int MaxTextLength = 255;

        public const string NameEmpty = "Trường \"Wifi\" không được trống!";
        public const string GroupNameEmpty = "Trường \"Tên nhóm wifi\" không được trống!";
        public const string NotFound = "Mật khẩu wifi không còn tồn tại.";
        public const string GroupNotFound = "Nhóm wifi không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NameDuplicate(string value)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Wifi\" đã tồn tại!", value ?? "");
        }

        public static string GroupNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm wifi\" đã tồn tại trong cùng nhóm cha!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string GroupHasPasswords(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm wifi '{0}' vì nhóm còn {1} mật khẩu.", groupName ?? "", count);
        }

        public static string GroupHasChildren(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm wifi '{0}' vì nhóm còn {1} nhóm con.", groupName ?? "", count);
        }

        public static string DeleteGroupConfirm(string groupName)
        {
            return string.Format("Bạn có chắc chắn xóa nhóm wifi '{0}' không?", groupName ?? "");
        }

        /// <summary>So trùng theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, kể cả đ/Đ; không cắt khoảng trắng).</summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
