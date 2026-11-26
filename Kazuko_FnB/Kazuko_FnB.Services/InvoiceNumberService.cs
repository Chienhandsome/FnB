using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ sinh số hóa đơn nhà hàng (docs/specs/su-dung-dich-vu.md §3.3, §6.5).
    /// Đọc kiểu quay vòng từ SCONFIG.SoHoaDonQuayVongTheo (0 ngày, 1 tháng, 2 năm, 3 không quay vòng).
    /// </summary>
    public sealed class InvoiceNumberService
    {
        private readonly InvoiceNumberRepository _repo;

        public InvoiceNumberService(string databasePath)
        {
            _repo = new InvoiceNumberRepository(databasePath);
        }

        public DateTime GetServerTimestamp()
        {
            return _repo.GetServerTimestamp();
        }

        public DateTime GetServerDate()
        {
            return _repo.GetServerTimestamp().Date;
        }

        public InvoiceCycleMode CurrentCycleMode()
        {
            return InvoiceNumberFormat.NormalizeMode(SystemConfig.SoHoaDonQuayVongTheo);
        }

        public string AllocateNext(DateTime billDate, string actorId, out int sequence)
        {
            return _repo.AllocateNext(CurrentCycleMode(), billDate, actorId, out sequence);
        }

        public string AllocateNext(InvoiceCycleMode mode, DateTime billDate, string actorId, out int sequence)
        {
            return _repo.AllocateNext(mode, billDate, actorId, out sequence);
        }

        public static string Format(InvoiceCycleMode mode, DateTime date, int sequence)
        {
            return InvoiceNumberFormat.Format(mode, date, sequence);
        }

        public static DateTime PeriodKey(InvoiceCycleMode mode, DateTime date)
        {
            return InvoiceNumberFormat.PeriodKey(mode, date);
        }
    }
}
