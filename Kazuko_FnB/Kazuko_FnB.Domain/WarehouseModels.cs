using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng kho hàng (DKHOHANG, spec docs/specs/danh-muc-kho-hang/).</summary>
    public sealed class WarehouseRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StoreId { get; set; }
        /// <summary>Cờ cho phép âm kho lưu trong DB (CHOPHEPAMKHO: 0 = không, 30 = có).</summary>
        public int AllowNegativeStock { get; set; }
        public string ImageId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị từ bảng liên kết (không ghi xuống DB).
        public string StoreName { get; set; }
        public string ImageName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public bool IsAllowNegativeStock
        {
            get { return AllowNegativeStock != 0; }
        }

        public static WarehouseRow NewDefault()
        {
            return new WarehouseRow
            {
                Name = "",
                StoreId = null,
                AllowNegativeStock = WarehouseText.AllowNegativeOff,
                ImageId = null,
                Note = "",
                Status = WarehouseText.ActiveStatus
            };
        }
    }

    /// <summary>Một cặp (Bảng, Cột) tham chiếu tới DKHOHANG.ID.</summary>
    public sealed class WarehouseReferenceColumn
    {
        public WarehouseReferenceColumn(string tableName, string columnName)
        {
            TableName = tableName;
            ColumnName = columnName;
        }

        public string TableName { get; private set; }
        public string ColumnName { get; private set; }
    }

    /// <summary>Kết quả lưu kho hàng.</summary>
    public sealed class WarehouseSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasStoreError { get; set; }

        public static WarehouseSaveResult Fail(string message, bool hasNameError = false, bool hasStoreError = false)
        {
            return new WarehouseSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError,
                HasStoreError = hasStoreError
            };
        }
    }

    /// <summary>Kết quả xóa cứng kho hàng (kèm số tham chiếu từng cột hoặc cờ kho cuối cùng).</summary>
    public sealed class WarehouseDeleteResult
    {
        public WarehouseDeleteResult()
        {
            Blocks = new List<ReferenceBlock>();
        }

        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
        public bool IsLastWarehouse { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }

        public static WarehouseDeleteResult Fail(string message)
        {
            return new WarehouseDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng của Danh mục kho hàng (DKHOHANG).</summary>
    public static class WarehouseText
    {
        public const string FunctionId = "49cc3a6a-afc6-4efb-ad5d-af9da000f4c7";
        public const string FunctionName = "Danh mục kho hàng";
        public const string TableName = "DKHOHANG";
        public const int ActiveStatus = 30;
        public const int AllowNegativeOn = 30;
        public const int AllowNegativeOff = 0;
        public const int MaxTextLength = 255;

        public const string NameEmpty = "Trường \"Tên kho hàng\" không được trống!";
        public const string StoreEmpty = "Trường \"Cửa hàng\" không được trống!";
        public const string NotFound = "Kho hàng không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string LastWarehouseBlocked = "Không thể xóa kho hàng cuối cùng trong hệ thống!";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên kho hàng\" đã tồn tại!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        /// <summary>
        /// So trùng theo collation UNICODE_CI_AI của cột DKHOHANG.NAME:
        /// không phân biệt hoa/thường và dấu tiếng Việt (kể cả đ/Đ), KHÔNG cắt khoảng trắng đầu/cuối.
        /// </summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
