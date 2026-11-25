using System;

namespace Kazuko_FnB.Services
{
    /// <summary>Hạn dùng thử, tính theo ngày lịch kể từ ngày cài app.</summary>
    public static class TrialRules
    {
        public const int TrialDays = 30;

        /// <summary>Ngày cài còn đủ TrialDays, mỗi ngày qua giảm 1, luôn nằm trong [0, TrialDays].</summary>
        public static int DaysLeft(DateTime installDate, DateTime today)
        {
            int left = TrialDays - (today.Date - installDate.Date).Days;
            return Math.Max(0, Math.Min(TrialDays, left));
        }
    }
}
