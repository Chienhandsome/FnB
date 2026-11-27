using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của Danh mục cửa hàng (DCUAHANG, docs/specs/danh-muc-cua-hang/).
    /// Không đụng DB, không đụng UI — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class StoreRules
    {
        /// <summary>So trùng mã cửa hàng theo collation UTF8: CÓ phân biệt hoa/thường và dấu, không cắt khoảng trắng (Q2).</summary>
        public static bool AreCodesDuplicate(string left, string right)
        {
            return string.Equals(left ?? "", right ?? "", StringComparison.Ordinal);
        }

        /// <summary>So trùng tên cửa hàng theo collation UNICODE_CI_AI: không phân biệt hoa/thường và dấu, không cắt khoảng trắng (Q2).</summary>
        public static bool AreNamesDuplicate(string left, string right)
        {
            return string.Equals(StoreText.FoldNoTrim(left), StoreText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>
        /// Kiểm tra dữ liệu cửa hàng trước khi lưu:
        /// Mã (bắt buộc, <= 36), Tên (bắt buộc, <= 255), Địa chỉ/Điện thoại/Ghi chú (<= 255), không trùng Mã/Tên.
        /// </summary>
        public static StoreSaveResult Validate(StoreRow row, bool duplicateCode, bool duplicateName)
        {
            var result = new StoreSaveResult();
            if (row == null)
                return StoreSaveResult.Fail("Không có dữ liệu cửa hàng.");

            var errors = new List<string>();
            string code = row.Code ?? "";
            string name = row.Name ?? "";

            if (string.IsNullOrWhiteSpace(code))
            {
                result.HasCodeError = true;
                errors.Add(StoreText.CodeEmpty);
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(StoreText.NameEmpty);
            }
            if (code.Length > StoreText.MaxCodeLength)
            {
                result.HasCodeError = true;
                errors.Add(StoreText.FieldTooLong("Mã cửa hàng", StoreText.MaxCodeLength));
            }
            if (name.Length > StoreText.MaxTextLength)
            {
                result.HasNameError = true;
                errors.Add(StoreText.FieldTooLong("Tên cửa hàng", StoreText.MaxTextLength));
            }
            if (row.Address != null && row.Address.Length > StoreText.MaxTextLength)
            {
                result.HasAddressError = true;
                errors.Add(StoreText.FieldTooLong("Địa chỉ", StoreText.MaxTextLength));
            }
            if (row.Phone != null && row.Phone.Length > StoreText.MaxTextLength)
            {
                result.HasPhoneError = true;
                errors.Add(StoreText.FieldTooLong("Điện thoại", StoreText.MaxTextLength));
            }
            if (row.Note != null && row.Note.Length > StoreText.MaxTextLength)
            {
                result.HasNoteError = true;
                errors.Add(StoreText.FieldTooLong("Ghi chú", StoreText.MaxTextLength));
            }

            if (errors.Count == 0)
            {
                if (duplicateCode)
                {
                    result.HasCodeError = true;
                    errors.Add(StoreText.CodeDuplicate(code));
                }
                if (duplicateName)
                {
                    result.HasNameError = true;
                    errors.Add(StoreText.NameDuplicate(name));
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

        /// <summary>Thông báo chặn xóa cửa hàng nêu rõ từng nơi đang tham chiếu và số lượng ("còn N kho, M hóa đơn...").</summary>
        public static string BuildReferenceBlockedMessage(string storeName, IList<ReferenceBlock> blocks)
        {
            var parts = new List<string>();
            int total = 0;
            if (blocks != null)
            {
                foreach (ReferenceBlock b in blocks)
                {
                    if (b == null || b.Count <= 0)
                        continue;
                    total += b.Count;
                    parts.Add(b.Count + " " + LabelForTable(b.TableName));
                }
            }
            if (parts.Count == 0)
                return ReferenceChecker.BlockedMessage("cửa hàng", storeName ?? "", total);
            return string.Format("Không thể xóa cửa hàng '{0}' vì đang được sử dụng (còn {1}).",
                storeName ?? "", string.Join(", ", parts.ToArray()));
        }

        private static string LabelForTable(string tableName)
        {
            foreach (StoreReferenceColumn rc in StoreRepository.ReferenceColumns)
            {
                if (string.Equals(rc.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                    return rc.DisplayLabel;
            }
            return string.IsNullOrEmpty(tableName) ? "dữ liệu liên kết" : tableName;
        }

        /// <summary>Kiểm quyền theo SFUNCTION "Danh mục cửa hàng" (ID 8fdde6f3-568b-439d-9c42-92b622228ff5 hoặc tên chức năng).</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            if ((session.Permissions.ModeById(StoreText.FunctionId) & bit) == bit)
                return true;
            return AppSession.Can(session, StoreText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }
    }
}
