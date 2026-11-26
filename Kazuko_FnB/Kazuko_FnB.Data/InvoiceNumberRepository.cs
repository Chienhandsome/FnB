using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc giờ máy chủ Firebird và cấp phát số hóa đơn nhà hàng qua bảng TSOHOADON (spec §3.3, §6.5).
    /// Hỗ trợ đủ 4 kiểu quay vòng (Ngày/Tháng/Năm/Không quay vòng), tiếp nối đúng dữ liệu có sẵn trong TDONHANG.
    /// Thử lại có giới hạn khi xung đột thay vì đệ quy vô hạn như app gốc (B10).
    /// </summary>
    public sealed class InvoiceNumberRepository
    {
        private const int ActiveStatus = 30;
        private const int MaxRetries = 5;
        private readonly string _databasePath;

        public InvoiceNumberRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Thời điểm hiện tại của máy chủ Firebird (CURRENT_TIMESTAMP).</summary>
        public DateTime GetServerTimestamp()
        {
            return DbHelper.Read(_databasePath, conn => GetServerTimestamp(conn, null));
        }

        public static DateTime GetServerTimestamp(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
            return v == null || v is DBNull ? DbHelper.Now() : Convert.ToDateTime(v);
        }

        /// <summary>Cấp số hóa đơn mới trong một transaction độc lập (dùng cho kiểm thử hoặc gọi rời).</summary>
        public string AllocateNext(InvoiceCycleMode mode, DateTime billDate, string actorId, out int sequence)
        {
            int seq = 0;
            string formatted = "";
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    DbHelper.InTransaction(_databasePath, (conn, tx) =>
                    {
                        AllocateNextInTransaction(conn, tx, mode, billDate, actorId, out seq, out formatted);
                    });
                    sequence = seq;
                    return formatted;
                }
                catch (DataAccessException)
                {
                    if (attempt == MaxRetries - 1)
                        throw;
                }
            }
            sequence = seq;
            return formatted;
        }

        /// <summary>
        /// Cấp phát số hóa đơn tiếp theo trong transaction hiện có (dùng chung khi mở bàn tạo TDONHANG).
        /// Lấy MAX giữa TSOHOADON.SO của kỳ và MAX(TDONHANG.SOHD) của các bill LOAI=0 trong cùng kỳ, cộng 1.
        /// </summary>
        public static void AllocateNextInTransaction(
            FbConnection conn,
            FbTransaction tx,
            InvoiceCycleMode mode,
            DateTime billDate,
            string actorId,
            out int sequence,
            out string formattedNumber)
        {
            DateTime periodKey = InvoiceNumberFormat.PeriodKey(mode, billDate);
            DateTime? periodEnd = InvoiceNumberFormat.PeriodEndExclusive(mode, periodKey);
            DateTime now = GetServerTimestamp(conn, tx);

            DataTable existing = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, COALESCE(SO, 0) AS SO FROM TSOHOADON WHERE NGAY = @N ORDER BY TIMECREATED DESC, ID",
                "@N", periodKey);

            string counterId = null;
            int counterSo = 0;
            if (existing.Rows.Count > 0)
            {
                counterId = DbHelper.Str(existing.Rows[0]["ID"]);
                counterSo = DbHelper.Int(existing.Rows[0]["SO"]);
            }

            int maxBillSo;
            if (periodEnd.HasValue)
            {
                maxBillSo = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(MAX(SOHD), 0) FROM TDONHANG WHERE COALESCE(LOAI, 0) = 0 AND NGAY >= @S AND NGAY < @E",
                    "@S", periodKey, "@E", periodEnd.Value));
            }
            else
            {
                maxBillSo = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(MAX(SOHD), 0) FROM TDONHANG WHERE COALESCE(LOAI, 0) = 0"));
            }

            sequence = Math.Max(counterSo, maxBillSo) + 1;
            formattedNumber = InvoiceNumberFormat.Format(mode, billDate, sequence);

            if (string.IsNullOrEmpty(counterId))
            {
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TSOHOADON (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, NGAY, SO) " +
                    "VALUES (@ID, @NAME, NULL, @ST, @U, @T, NULL, NULL, @NGAY, @SO)",
                    "@ID", DbHelper.NewId(),
                    "@NAME", formattedNumber,
                    "@ST", ActiveStatus,
                    "@U", actorId ?? "",
                    "@T", now,
                    "@NGAY", periodKey,
                    "@SO", sequence);
            }
            else
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE TSOHOADON SET SO = @SO, NAME = @NAME, USERMODIFIEDID = @U, TIMEMODIFIED = @T WHERE ID = @ID",
                    "@SO", sequence,
                    "@NAME", formattedNumber,
                    "@U", actorId ?? "",
                    "@T", now,
                    "@ID", counterId);
            }
        }
    }
}
