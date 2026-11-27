using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc thuần (không chạm DB) cho Danh mục mật khẩu wifi (DMATKHAUWIFI / DNHOMWIFI,
    /// spec docs/specs/quan-tri-nho.md §3): kiểm quyền, kiểm nhập liệu mật khẩu và nhóm, cắt chuỗi lưu vết.
    /// </summary>
    public static class WifiPasswordRules
    {
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(WifiPasswordText.FunctionName, bit);
        }

        /// <summary>Kiểm tra tính hợp lệ của một dòng mật khẩu wifi (không chạm DB).</summary>
        public static WifiPasswordSaveResult ValidatePassword(WifiPasswordRow row, bool isDuplicateName)
        {
            if (row == null)
                return WifiPasswordSaveResult.Fail("Không có dữ liệu mật khẩu wifi.");

            var errors = new List<string>();
            bool nameErr = false;
            bool noteErr = false;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors.Add(WifiPasswordText.NameEmpty);
                nameErr = true;
            }
            else if (row.Name.Length > WifiPasswordText.MaxTextLength)
            {
                errors.Add(WifiPasswordText.FieldTooLong("Wifi", WifiPasswordText.MaxTextLength));
                nameErr = true;
            }
            else if (isDuplicateName)
            {
                errors.Add(WifiPasswordText.NameDuplicate(row.Name));
                nameErr = true;
            }

            if (row.Note != null && row.Note.Length > WifiPasswordText.MaxTextLength)
            {
                errors.Add(WifiPasswordText.FieldTooLong("Ghi chú", WifiPasswordText.MaxTextLength));
                noteErr = true;
            }

            if (errors.Count > 0)
            {
                return WifiPasswordSaveResult.Fail(
                    string.Join(Environment.NewLine, errors.ToArray()),
                    hasNameError: nameErr,
                    hasNoteError: noteErr);
            }
            return new WifiPasswordSaveResult { Ok = true };
        }

        /// <summary>Kiểm tra tính hợp lệ của một nhóm wifi (không chạm DB).</summary>
        public static WifiGroupSaveResult ValidateGroup(WifiGroupRow row, bool isDuplicateName)
        {
            if (row == null)
                return WifiGroupSaveResult.Fail("Không có dữ liệu nhóm wifi.");

            if (string.IsNullOrWhiteSpace(row.Name))
                return WifiGroupSaveResult.Fail(WifiPasswordText.GroupNameEmpty);
            if (row.Name.Length > WifiPasswordText.MaxTextLength)
                return WifiGroupSaveResult.Fail(
                    WifiPasswordText.FieldTooLong("Tên nhóm wifi", WifiPasswordText.MaxTextLength));
            if (row.Note != null && row.Note.Length > WifiPasswordText.MaxTextLength)
                return WifiGroupSaveResult.Fail(
                    WifiPasswordText.FieldTooLong("Ghi chú", WifiPasswordText.MaxTextLength));
            if (isDuplicateName)
                return WifiGroupSaveResult.Fail(WifiPasswordText.GroupNameDuplicate(row.Name));
            return new WifiGroupSaveResult { Ok = true };
        }

        /// <summary>Cắt chuỗi ghi lưu vết TLUUVET vừa giới hạn VARCHAR(255) để không tràn cột khi tên dài 255 ký tự.</summary>
        public static string ClipLogText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= WifiPasswordText.MaxTextLength
                ? value
                : value.Substring(0, WifiPasswordText.MaxTextLength);
        }
    }
}
