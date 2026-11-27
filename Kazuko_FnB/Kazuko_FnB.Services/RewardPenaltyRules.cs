using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho màn Thưởng phạt (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2).
    /// Không đụng DB, không đụng giao diện — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class RewardPenaltyRules
    {
        public static bool AreNumbersDuplicate(string left, string right)
        {
            string a = (left ?? "").Trim();
            string b = (right ?? "").Trim();
            if (a.Length == 0 || b.Length == 0)
                return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static RewardPenaltySaveResult Validate(
            RewardPenaltyRow row,
            RewardPenaltyMonthLimit monthLimit = null)
        {
            if (row == null)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.NotFound);

            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.NumberRequired, hasNumberError: true);
            if (number.Length > RewardPenaltyText.MaxTextLength)
                return RewardPenaltySaveResult.Fail(
                    RewardPenaltyText.FieldTooLong("Số phiếu", RewardPenaltyText.MaxTextLength),
                    hasNumberError: true);

            if (monthLimit != null && monthLimit.Month >= 1 && monthLimit.Month <= 12 && monthLimit.Year > 0)
            {
                if (row.Date.Month != monthLimit.Month || row.Date.Year != monthLimit.Year)
                {
                    return RewardPenaltySaveResult.Fail(
                        RewardPenaltyText.MonthLimitBlocked(monthLimit.Month, monthLimit.Year),
                        hasDateError: true);
                }
            }

            if (string.IsNullOrWhiteSpace(row.EmployeeId))
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.EmployeeRequired, hasEmployeeError: true);

            if (row.Bonus < 0m || row.Bonus > RewardPenaltyText.MaxAmount
                || row.Penalty < 0m || row.Penalty > RewardPenaltyText.MaxAmount)
            {
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.AmountInvalid, hasAmountError: true);
            }

            if (row.Bonus <= 0m && row.Penalty <= 0m)
                return RewardPenaltySaveResult.Fail(RewardPenaltyText.AmountRequired, hasAmountError: true);

            if (row.Note != null && row.Note.Length > RewardPenaltyText.MaxTextLength)
            {
                return RewardPenaltySaveResult.Fail(
                    RewardPenaltyText.FieldTooLong("Ghi chú", RewardPenaltyText.MaxTextLength),
                    hasNoteError: true);
            }

            return new RewardPenaltySaveResult { Ok = true };
        }

        public static bool MatchesSearch(RewardPenaltyRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.EmployeeName ?? "",
                row.ReasonName ?? "",
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
            if (session.Permissions == null)
                return false;
            return session.Permissions.Can(RewardPenaltyText.FunctionName, bit)
                || session.Permissions.Can(RewardPenaltyText.AltFunctionName, bit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(RewardPenaltyText.FunctionDateChange, RoleModes.View);
        }

        public static string Clip(string text, int maxLength = RewardPenaltyText.MaxTextLength)
        {
            string s = text ?? "";
            return maxLength > 0 && s.Length > maxLength ? s.Substring(0, maxLength) : s;
        }
    }
}
