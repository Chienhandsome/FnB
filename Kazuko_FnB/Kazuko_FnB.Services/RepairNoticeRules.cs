using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho Thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2).
    /// Không chạm DB, không chạm UI — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class RepairNoticeRules
    {
        public static RepairNoticeSaveResult Validate(RepairNoticeRow row)
        {
            if (row == null)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.NotFound);

            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.NumberRequired, hasNumberError: true);
            if (number.Length > RepairNoticeText.MaxTextLength)
                return RepairNoticeSaveResult.Fail(
                    RepairNoticeText.FieldTooLong("Số phiếu", RepairNoticeText.MaxTextLength),
                    hasNumberError: true);

            if (string.IsNullOrWhiteSpace(row.RoomId))
                return RepairNoticeSaveResult.Fail(RepairNoticeText.RoomRequired, hasRoomError: true);

            string content = (row.Content ?? "").Trim();
            if (content.Length == 0)
                return RepairNoticeSaveResult.Fail(RepairNoticeText.ContentRequired, hasContentError: true);
            if (row.Content.Length > RepairNoticeText.MaxTextLength)
                return RepairNoticeSaveResult.Fail(
                    RepairNoticeText.FieldTooLong("Nội dung", RepairNoticeText.MaxTextLength),
                    hasContentError: true);

            if (row.Note != null && row.Note.Length > RepairNoticeText.MaxTextLength)
                return RepairNoticeSaveResult.Fail(
                    RepairNoticeText.FieldTooLong("Ghi chú", RepairNoticeText.MaxTextLength),
                    hasNoteError: true);

            return new RepairNoticeSaveResult { Ok = true };
        }

        public static bool MatchesSearch(RepairNoticeRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.RoomName ?? "",
                row.EmployeeName ?? "",
                row.Content ?? "",
                row.Note ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(RepairNoticeText.FunctionName, bit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(RepairNoticeText.FunctionDateChange, RoleModes.View);
        }

        /// <summary>Cắt chuỗi ghi lưu vết TLUUVET vừa giới hạn VARCHAR(255) để không tràn cột.</summary>
        public static string Clip(string text, int maxLength = RepairNoticeText.MaxTextLength)
        {
            string s = text ?? "";
            return maxLength > 0 && s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }
    }
}
