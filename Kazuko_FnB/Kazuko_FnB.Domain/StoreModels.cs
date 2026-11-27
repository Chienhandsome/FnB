using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng cửa hàng (DCUAHANG, spec docs/specs/danh-muc-cua-hang/).</summary>
    public sealed class StoreRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        /// <summary>Ảnh cửa hàng (SIMAGEID): không có ô nhập trên hộp thoại, giữ nguyên khi sửa.</summary>
        public string ImageId { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string WarehouseId { get; set; }
        public string TemplateId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string WarehouseName { get; set; }
        public string TemplateName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static StoreRow NewDefault()
        {
            return new StoreRow
            {
                Code = "",
                Name = "",
                ImageId = null,
                Address = "",
                Phone = "",
                WarehouseId = null,
                TemplateId = null,
                Note = "",
                Status = StoreText.ActiveStatus
            };
        }
    }

    /// <summary>Một cặp (Bảng, Cột, Nhãn tiếng Việt) tham chiếu tới DCUAHANG.ID.</summary>
    public sealed class StoreReferenceColumn
    {
        public StoreReferenceColumn(string tableName, string columnName, string displayLabel)
        {
            TableName = tableName;
            ColumnName = columnName;
            DisplayLabel = displayLabel;
        }

        public string TableName { get; private set; }
        public string ColumnName { get; private set; }
        public string DisplayLabel { get; private set; }
    }

    /// <summary>Kết quả lưu cửa hàng.</summary>
    public sealed class StoreSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasCodeError { get; set; }
        public bool HasNameError { get; set; }
        public bool HasAddressError { get; set; }
        public bool HasPhoneError { get; set; }
        public bool HasNoteError { get; set; }

        public static StoreSaveResult Fail(string message, bool hasCodeError = false, bool hasNameError = false)
        {
            return new StoreSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasCodeError = hasCodeError,
                HasNameError = hasNameError
            };
        }
    }

    /// <summary>Kết quả xóa cứng cửa hàng (kèm số tham chiếu từng bảng hoặc cờ cửa hàng cuối cùng).</summary>
    public sealed class StoreDeleteResult
    {
        public StoreDeleteResult()
        {
            Blocks = new List<ReferenceBlock>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
        public bool IsLastStore { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }

        public static StoreDeleteResult Fail(string message)
        {
            return new StoreDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng của Danh mục cửa hàng (DCUAHANG).</summary>
    public static class StoreText
    {
        public const string FunctionId = "8fdde6f3-568b-439d-9c42-92b622228ff5";
        public const string FunctionName = "Danh mục cửa hàng";
        public const string TableName = "DCUAHANG";
        public const string StableDescId = "71442d6c-3236-44af-b312-21042b8ea94f";
        public const string InvoiceTemplateFormId = "5f029a5b-8ec2-4229-bd1d-03fed9569db2";
        public const string ConfigMultiStoreKey = "NhieuCuaHang";

        public const int ActiveStatus = 30;
        public const int MaxCodeLength = 36;
        public const int MaxTextLength = 255;

        public const string CodeEmpty = "Trường \"Mã cửa hàng\" không được trống!";
        public const string NameEmpty = "Trường \"Tên cửa hàng\" không được trống!";
        public const string WarehouseInvalid = "Kho mặc định không tồn tại hoặc đã ngừng sử dụng!";
        public const string TemplateInvalid = "Mẫu hóa đơn không hợp lệ!";
        public const string NotFound = "Cửa hàng không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string LastStoreBlocked = "Không thể xóa cửa hàng cuối cùng trong hệ thống!";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã cửa hàng\" đã tồn tại!", code ?? "");
        }

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên cửa hàng\" đã tồn tại!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột DCUAHANG.NAME:
        /// không phân biệt hoa/thường và dấu tiếng Việt (kể cả đ/Đ), KHÔNG cắt khoảng trắng đầu/cuối.
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
