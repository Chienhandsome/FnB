using System;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật làm tròn tiền chung cho nhà hàng và bán lẻ: làm tròn tới bội của LamTronTien
    /// (hiện 1000), kiểu AwayFromZero. Chỉ là khung thuần (không đọc DB, chưa nối UI).
    /// </summary>
    public static class MoneyRounding
    {
        public static decimal RoundToStep(decimal amount, decimal step)
        {
            if (step <= 0m)
                throw new ArgumentOutOfRangeException("step", "Bước làm tròn phải > 0.");
            decimal units = Math.Round(amount / step, 0, MidpointRounding.AwayFromZero);
            return units * step;
        }
    }
}
