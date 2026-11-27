using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của Danh mục kho hàng (DKHOHANG, docs/specs/danh-muc-kho-hang/).
    /// Không đụng DB, không đụng UI — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class WarehouseRules
    {
        /// <summary>So trùng tên kho theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, không cắt khoảng trắng).</summary>
        public static bool AreDuplicate(string left, string right)
        {
            return string.Equals(WarehouseText.FoldNoTrim(left), WarehouseText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>
        /// Giá trị CHOPHEPAMKHO khi thêm mới kho theo quyết định Chiến chốt 26/11 (Q2):
        /// hộp thoại không có ô cờ âm kho, kho mới luôn lưu 0.
        /// </summary>
        public static int AllowNegativeOnCreate()
        {
            return WarehouseText.AllowNegativeOff;
        }

        /// <summary>Giá trị CHOPHEPAMKHO khi sửa kho cũ theo Q2: giữ nguyên giá trị đang có trong DB.</summary>
        public static int AllowNegativeOnEdit(int existingValue)
        {
            return existingValue;
        }

        /// <summary>Kiểm tra dữ liệu kho hàng trước khi lưu (Tên bắt buộc, Cửa hàng bắt buộc, độ dài <= 255, không trùng Tên).</summary>
        public static WarehouseSaveResult Validate(WarehouseRow row, bool duplicateName)
        {
            var result = new WarehouseSaveResult();
            if (row == null)
                return WarehouseSaveResult.Fail("Không có dữ liệu kho hàng.");

            var errors = new List<string>();
            string name = row.Name ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(WarehouseText.NameEmpty);
            }
            if (string.IsNullOrWhiteSpace(row.StoreId))
            {
                result.HasStoreError = true;
                errors.Add(WarehouseText.StoreEmpty);
            }
            if (name.Length > WarehouseText.MaxTextLength)
            {
                result.HasNameError = true;
                errors.Add(WarehouseText.FieldTooLong("Tên kho hàng", WarehouseText.MaxTextLength));
            }
            if (row.Note != null && row.Note.Length > WarehouseText.MaxTextLength)
            {
                errors.Add(WarehouseText.FieldTooLong("Ghi chú", WarehouseText.MaxTextLength));
            }

            if (errors.Count == 0 && duplicateName)
            {
                result.HasNameError = true;
                errors.Add(WarehouseText.NameDuplicate(name));
            }

            if (errors.Count > 0)
            {
                result.ErrorMessage = string.Join(Environment.NewLine, errors.ToArray());
                return result;
            }
            result.Ok = true;
            return result;
        }

        /// <summary>Kiểm quyền theo SFUNCTION "Danh mục kho hàng" (ID 49cc3a6a-afc6-4efb-ad5d-af9da000f4c7 hoặc tên chức năng).</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if ((session.Permissions.ModeById(WarehouseText.FunctionId) & bit) == bit)
                return true;
            return session.Permissions.Can(WarehouseText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }
    }
}
