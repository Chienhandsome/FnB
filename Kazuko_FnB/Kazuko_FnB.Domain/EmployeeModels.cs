using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Loại dòng trong bảng DNHANVIEN (spec docs/specs/nhan-su-cay.md §1).</summary>
    public static class EmployeeItemType
    {
        public const int Employee = 0;
        public const int Folder = 1;
        public const int Separator = 2;
    }

    /// <summary>Cách tính lương của nhân viên (DNHANVIEN.CACHTINHLUONG, docs/specs/nhan-su-danh-muc.md §2).</summary>
    public static class EmployeeSalaryMode
    {
        public const int ByMonth = 0;
        public const int ByShift = 30;
        public const int MonthByShift = 60;
    }

    /// <summary>Một dòng trong bảng DNHANVIEN: nhân viên, thư mục (ITEMTYPE=1) hoặc phân cách (ITEMTYPE=2).</summary>
    public sealed class EmployeeRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string ShiftId { get; set; }
        public int SalaryMode { get; set; }
        public decimal ShiftSalary { get; set; }
        public decimal MonthlySalary { get; set; }
        /// <summary>Nghỉ chủ nhật (NGHICHUNHAT: 30 = có, 0 = không).</summary>
        public int OffSunday { get; set; }
        /// <summary>Nghỉ thứ 7 (NGHITHU7: 30 = có, 0 = không).</summary>
        public int OffSaturday { get; set; }
        /// <summary>Cột chết LUONGTHEOCA (T3): không có ô nhập, dòng mới để trống, sửa giữ nguyên giá trị cũ.</summary>
        public decimal? LegacyShiftSalary { get; set; }
        /// <summary>Ảnh (SIMAGEID): không làm ô nhập, giữ nguyên giá trị cũ khi sửa.</summary>
        public string ImageId { get; set; }
        public string Note { get; set; }

        /// <summary>Loại dòng: null/0 = nhân viên, 1 = thư mục, 2 = phân cách (spec nhan-su-cay.md §1).</summary>
        public int? ItemType { get; set; }
        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int AutoId { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string ShiftName { get; set; }
        public string ParentName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public bool IsFolder { get { return ItemType.HasValue && ItemType.Value == EmployeeItemType.Folder; } }
        public bool IsSeparator { get { return ItemType.HasValue && ItemType.Value == EmployeeItemType.Separator; } }
        public bool IsEmployee { get { return !ItemType.HasValue || ItemType.Value == EmployeeItemType.Employee; } }

        public bool IsOffSunday
        {
            get { return OffSunday == EmployeeText.FlagOn; }
            set { OffSunday = value ? EmployeeText.FlagOn : EmployeeText.FlagOff; }
        }

        public bool IsOffSaturday
        {
            get { return OffSaturday == EmployeeText.FlagOn; }
            set { OffSaturday = value ? EmployeeText.FlagOn : EmployeeText.FlagOff; }
        }

        public static EmployeeRow NewDefault()
        {
            return new EmployeeRow
            {
                Code = "",
                Name = "",
                Phone = "",
                Address = "",
                ShiftId = null,
                SalaryMode = EmployeeSalaryMode.ByShift,
                ShiftSalary = 0m,
                MonthlySalary = 0m,
                OffSunday = EmployeeText.FlagOff,
                OffSaturday = EmployeeText.FlagOn,
                LegacyShiftSalary = null,
                ImageId = null,
                Note = "",
                ItemType = null,
                ParentId = "",
                ParentDir = "",
                SortOrder = "",
                AutoId = 0,
                Status = EmployeeText.ActiveStatus
            };
        }

        public static EmployeeRow NewFolder(string parentId)
        {
            return new EmployeeRow
            {
                Name = "",
                ItemType = EmployeeItemType.Folder,
                ParentId = string.IsNullOrEmpty(parentId) ? "" : parentId,
                ParentDir = "",
                SortOrder = "",
                Status = EmployeeText.ActiveStatus
            };
        }
    }

    /// <summary>Một cặp (Bảng, Cột, Nhãn tiếng Việt) tham chiếu tới DNHANVIEN.ID.</summary>
    public sealed class EmployeeReferenceColumn
    {
        public EmployeeReferenceColumn(string tableName, string columnName, string displayLabel)
        {
            TableName = tableName;
            ColumnName = columnName;
            DisplayLabel = displayLabel;
        }

        public string TableName { get; private set; }
        public string ColumnName { get; private set; }
        public string DisplayLabel { get; private set; }
    }

    /// <summary>Kết quả lưu nhân viên.</summary>
    public sealed class EmployeeSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasCodeError { get; set; }
        public bool HasPhoneError { get; set; }
        public bool HasAddressError { get; set; }
        public bool HasShiftError { get; set; }
        public bool HasShiftSalaryError { get; set; }
        public bool HasMonthlySalaryError { get; set; }
        public bool HasNoteError { get; set; }

        public static EmployeeSaveResult Fail(
            string message,
            bool hasNameError = false,
            bool hasCodeError = false,
            bool hasShiftError = false)
        {
            return new EmployeeSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError,
                HasCodeError = hasCodeError,
                HasShiftError = hasShiftError
            };
        }
    }

    /// <summary>Kết quả xóa cứng nhân viên (kèm số tham chiếu từng bảng).</summary>
    public sealed class EmployeeDeleteResult
    {
        public EmployeeDeleteResult()
        {
            Blocks = new List<ReferenceBlock>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }

        public static EmployeeDeleteResult Fail(string message)
        {
            return new EmployeeDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả khôi phục mục từ Thùng rác.</summary>
    public sealed class EmployeeRestoreResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public bool RestoredToRoot { get; set; }

        public static EmployeeRestoreResult Fail(string message)
        {
            return new EmployeeRestoreResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng của Danh mục nhân viên (DNHANVIEN).</summary>
    public static class EmployeeText
    {
        public const string FunctionName = "Danh mục nhân viên";
        public const string TableName = "DNHANVIEN";
        public const string ConfigUseTimekeeperKey = "TinhLuongSuDungMayChamCong";
        public const string SeparatorName = "___________";

        public const int ActiveStatus = 30;
        public const int TrashStatus = 0;
        public const int FlagOn = 30;
        public const int FlagOff = 0;
        public const int MaxTextLength = 255;
        public const decimal MaxSalary = 999999999.99m;

        public const string NameEmpty = "Trường \"Tên nhân viên\" không được trống!";
        public const string FolderNameEmpty = "Trường \"Tên thư mục\" không được trống!";
        public const string ParentFolderGone = "Thư mục cha không còn tồn tại.";
        public const string CodeEmpty = "Trường \"Mã nhân viên\" không được trống!";
        public const string ShiftEmpty = "Trường \"Ca làm việc\" không được trống!";
        public const string ShiftInvalid = "Ca làm việc không tồn tại hoặc đã ngừng sử dụng!";
        public const string SalaryModeInvalid = "Cách tính lương không hợp lệ!";
        public const string ShiftSalaryInvalid = "Trường \"Lương ca\" phải là số không âm, tối đa 9 chữ số phần nguyên!";
        public const string MonthlySalaryInvalid = "Trường \"Lương tháng\" phải là số không âm, tối đa 9 chữ số phần nguyên!";
        public const string NotFound = "Nhân viên không còn tồn tại.";
        public const string FolderNotFound = "Thư mục không còn tồn tại.";
        public const string ItemNotFound = "Dữ liệu không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string SoftDeleteConfirm(int count)
        {
            return string.Format(
                "Bạn có muốn chuyển ({0}) dữ liệu đang chọn tới thùng rác không? " +
                "Lưu ý: Dữ liệu chưa bị xóa ra khỏi hệ thống, để xóa hẳn ra khỏi hệ thống bạn bấm Shift + Delete",
                count);
        }

        public static string HardDeleteConfirm(string name)
        {
            return string.Format(
                "Bạn có chắc chắn muốn xóa vĩnh viễn \"{0}\" khỏi hệ thống không? " +
                "Lưu ý: Dữ liệu đã xóa vĩnh viễn sẽ không thể khôi phục được.",
                name ?? "");
        }

        public static string FolderNotEmpty(string folderName, int count)
        {
            return string.Format("Không thể xóa thư mục '{0}'. Thư mục còn {1} mục bên trong.",
                folderName ?? "", count);
        }

        public static string FolderNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên thư mục\" đã tồn tại trong cùng thư mục cha!", name ?? "");
        }

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhân viên\" đã tồn tại!", name ?? "");
        }

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã nhân viên\" đã tồn tại!", code ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string ItemTypeDisplayText(int? itemType)
        {
            if (itemType.HasValue && itemType.Value == EmployeeItemType.Folder)
                return "Thư mục";
            if (itemType.HasValue && itemType.Value == EmployeeItemType.Separator)
                return "Phân cách";
            return "Nhân viên";
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột DNHANVIEN.NAME:
        /// không phân biệt hoa/thường và dấu tiếng Việt (kể cả đ/Đ), KHÔNG cắt khoảng trắng đầu/cuối.
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
