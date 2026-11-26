using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc dữ liệu phục vụ Thanh toán (docs/specs/su-dung-dich-vu.md §3.7, §6.5, §6.6):
    /// đặt trước/tạm ứng, nợ cũ, điểm tích lũy, thẻ trả trước, voucher.
    /// Tra cứu tài khoản/nhân viên/nhóm bàn ở ServicePaymentRepository.Lookups.cs; phần ghi ở ServicePaymentRepository.Write.cs.
    /// </summary>
    public sealed partial class ServicePaymentRepository
    {
        private const int ActiveStatus = 30;
        private const int DeletedStatus = -30;
        private readonly string _databasePath;

        public ServicePaymentRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public T Read<T>(Func<FbConnection, T> work)
        {
            return DbHelper.Read(_databasePath, work);
        }

        /// <summary>
        /// Tiền đặt trước / tạm ứng của một bill (§3.7.2):
        /// SUM(THU) của phiếu thu (LOAI = 0, STATUS &lt;&gt; -30) gắn TDONHANGID = bill hoặc TDATHANGID = phiếu đặt.
        /// Bill không có phiếu đặt chỉ tính tạm ứng khi includeOrderAdvance = true (SuDungChucNangTamUngTrongDonHang = 30).
        /// </summary>
        public static decimal GetDepositIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string bookingId,
            bool includeOrderAdvance)
        {
            bool hasBooking = !string.IsNullOrEmpty(bookingId);
            bool hasBill = !string.IsNullOrEmpty(billId);
            if (!hasBooking && (!includeOrderAdvance || !hasBill))
                return 0m;

            if (hasBooking && includeOrderAdvance && hasBill)
            {
                return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(THU, 0)), 0) FROM TTHUCHI " +
                    "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 0 " +
                    "AND (TDONHANGID = @B OR TDATHANGID = @D)",
                    "@Del", DeletedStatus, "@B", billId, "@D", bookingId));
            }
            if (hasBooking)
            {
                return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(THU, 0)), 0) FROM TTHUCHI " +
                    "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 0 AND TDATHANGID = @D",
                    "@Del", DeletedStatus, "@D", bookingId));
            }
            return DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(THU, 0)), 0) FROM TTHUCHI " +
                "WHERE COALESCE(STATUS, 0) <> @Del AND COALESCE(LOAI, 0) = 0 AND TDONHANGID = @B",
                "@Del", DeletedStatus, "@B", billId));
        }

        /// <summary>
        /// Nợ cũ của khách hàng (GetNoCu, §3.7.2 &amp; pos-tap-nghiep-vu.md §1.6):
        /// Σ(TONGCONG − TIENTHANHTOAN) trên TDONHANG (DATHANHTOAN = 30, STATUS &lt;&gt; -30, trừ bill hiện tại)
        /// − Σ(LOAI = 0 ? THU : LOAI = 1 ? −CHI : 0) trên TTHUCHI (KHONGTHAYDOICONGNO = 0, TDATHANGID rỗng, STATUS &lt;&gt; -30).
        /// </summary>
        public static decimal GetOldDebtIn(FbConnection conn, FbTransaction tx, string customerId, IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;

            var billArgs = new List<object> { "@C", customerId, "@Del", DeletedStatus };
            string excludeSql = BuildNotInClause("ID", excludeBillIds, billArgs, "@EX");

            decimal billDebt = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(TONGCONG, 0) - COALESCE(TIENTHANHTOAN, 0)), 0) " +
                "FROM TDONHANG WHERE DKHACHHANGID = @C AND COALESCE(DATHANHTOAN, 0) = 30 " +
                "AND COALESCE(STATUS, 0) <> @Del" + excludeSql,
                billArgs.ToArray()));

            decimal voucherPaid = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(CASE WHEN COALESCE(LOAI, 0) = 0 THEN COALESCE(THU, 0) " +
                "WHEN COALESCE(LOAI, 0) = 1 THEN -COALESCE(CHI, 0) ELSE 0 END), 0) " +
                "FROM TTHUCHI WHERE DKHACHHANGID = @C AND COALESCE(KHONGTHAYDOICONGNO, 0) = 0 " +
                "AND (TDATHANGID IS NULL OR TDATHANGID = '') AND COALESCE(STATUS, 0) <> @Del",
                "@C", customerId, "@Del", DeletedStatus));

            return billDebt - voucherPaid;
        }

        public decimal GetOldDebt(string customerId, IList<string> excludeBillIds = null)
        {
            return DbHelper.Read(_databasePath, conn => GetOldDebtIn(conn, null, customerId, excludeBillIds));
        }

        /// <summary>Kiểm tra khách hàng có hồ sơ hợp lệ (STATUS = 30) trong DKHACHHANG (§6.6).</summary>
        public static bool CustomerExistsIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            out string name,
            out string address,
            out string prepaidCardId,
            out string groupId)
        {
            name = "";
            address = "";
            prepaidCardId = null;
            groupId = null;
            if (string.IsNullOrEmpty(customerId))
                return false;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME, DIACHI, DTHETRATRUOCID, DNHOMKHACHHANGID FROM DKHACHHANG WHERE ID = @I AND STATUS = @S",
                "@I", customerId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return false;
            DataRow r = dt.Rows[0];
            name = DbHelper.Str(r["NAME"]) ?? "";
            address = DbHelper.Str(r["DIACHI"]) ?? "";
            prepaidCardId = DbHelper.Str(r["DTHETRATRUOCID"]);
            groupId = DbHelper.Str(r["DNHOMKHACHHANGID"]);
            return true;
        }

        /// <summary>
        /// Điểm tích lũy hiện có của khách (pos-tap-nghiep-vu.md §1.8):
        /// - CachTinhDiem = 0 (tổng doanh số): DIEMTICHLUYBANDAU + Floor(Σ TONGCONG / DoanhSo1Diem) − Σ DIEMGIAM + TTANGGIAMDIEM.
        /// - CachTinhDiem != 0 (theo từng bill): DIEMTICHLUYBANDAU + Σ(DIEM − DIEMGIAM) + TTANGGIAMDIEM.
        /// </summary>
        public static decimal GetCustomerPointsIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            int pointCalcMode,
            int revenuePerPoint,
            IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;

            decimal initial = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(DIEMTICHLUYBANDAU, 0) FROM DKHACHHANG WHERE ID = @C",
                "@C", customerId));

            decimal manual = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(DIEMTANG, 0) - COALESCE(DIEMGIAM, 0)), 0) " +
                "FROM TTANGGIAMDIEM WHERE DKHACHHANGID = @C AND COALESCE(STATUS, 0) <> @Del",
                "@C", customerId, "@Del", DeletedStatus));

            var billArgs = new List<object> { "@C", customerId, "@Del", DeletedStatus };
            string excludeSql = BuildNotInClause("ID", excludeBillIds, billArgs, "@EX");

            decimal fromBills;
            if (pointCalcMode == 0)
            {
                DataTable dt = DbHelper.Table(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(TONGCONG, 0)), 0) AS REV, " +
                    "COALESCE(SUM(COALESCE(DIEMGIAM, 0)), 0) AS USED " +
                    "FROM TDONHANG WHERE DKHACHHANGID = @C AND COALESCE(LOAI, 0) = 0 " +
                    "AND COALESCE(DATHANHTOAN, 0) = 30 AND COALESCE(STATUS, 0) <> @Del" + excludeSql,
                    billArgs.ToArray());
                decimal rev = dt.Rows.Count > 0 ? DbHelper.Dec(dt.Rows[0]["REV"]) : 0m;
                decimal used = dt.Rows.Count > 0 ? DbHelper.Dec(dt.Rows[0]["USED"]) : 0m;
                decimal earned = revenuePerPoint > 0 ? Math.Floor(rev / revenuePerPoint) : 0m;
                fromBills = earned - used;
            }
            else
            {
                fromBills = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(DIEM, 0) - COALESCE(DIEMGIAM, 0)), 0) " +
                    "FROM TDONHANG WHERE DKHACHHANGID = @C AND COALESCE(LOAI, 0) = 0 " +
                    "AND COALESCE(DATHANHTOAN, 0) = 30 AND COALESCE(STATUS, 0) <> @Del" + excludeSql,
                    billArgs.ToArray()));
            }

            return initial + fromBills + manual;
        }

        /// <summary>
        /// Số dư thẻ trả trước (công thức desktop chuẩn, trừ cả CHI — khach-hang.md §2.1):
        /// Σ(THU − CHI) trên TTHUCHI (STATUS &lt;&gt; -30) − Σ THETRATRUOC trên TDONHANG (STATUS &lt;&gt; -30, trừ bill hiện tại).
        /// </summary>
        public static decimal GetPrepaidCardBalanceIn(
            FbConnection conn,
            FbTransaction tx,
            string cardId,
            IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(cardId))
                return 0m;

            decimal loaded = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(THU, 0) - COALESCE(CHI, 0)), 0) " +
                "FROM TTHUCHI WHERE DTHETRATRUOCID = @ID AND COALESCE(STATUS, 0) <> @Del",
                "@ID", cardId, "@Del", DeletedStatus));

            var billArgs = new List<object> { "@ID", cardId, "@Del", DeletedStatus };
            string excludeSql = BuildNotInClause("ID", excludeBillIds, billArgs, "@EX");

            decimal spent = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(THETRATRUOC, 0)), 0) " +
                "FROM TDONHANG WHERE DTHETRATRUOCID = @ID AND COALESCE(STATUS, 0) <> @Del" + excludeSql,
                billArgs.ToArray()));

            return loaded - spent;
        }

        public static PrepaidCardInfo ValidatePrepaidCardByCodeIn(
            FbConnection conn,
            FbTransaction tx,
            string code,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            string trimmed = (code ?? "").Trim();
            if (trimmed.Length == 0)
                return new PrepaidCardInfo { Error = ServicePaymentConstants.PrepaidCodeEmpty };

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME, NGAYHETHAN, COALESCE(KHOA, 0) AS KHOA " +
                "FROM DTHETRATRUOC WHERE NAME = @N AND STATUS = @S",
                "@N", trimmed, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return new PrepaidCardInfo { Code = trimmed, Error = ServicePaymentConstants.PrepaidNotFound(trimmed) };

            return BuildValidatedPrepaidCardIn(conn, tx, dt.Rows[0], serverDate, excludeBillIds);
        }

        public static PrepaidCardInfo ValidatePrepaidCardByIdIn(
            FbConnection conn,
            FbTransaction tx,
            string cardId,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(cardId))
                return new PrepaidCardInfo { Error = ServicePaymentConstants.PrepaidCodeEmpty };

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME, NGAYHETHAN, COALESCE(KHOA, 0) AS KHOA " +
                "FROM DTHETRATRUOC WHERE ID = @I AND STATUS = @S",
                "@I", cardId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return new PrepaidCardInfo { Id = cardId, Error = ServicePaymentConstants.PrepaidNotFound(cardId) };

            return BuildValidatedPrepaidCardIn(conn, tx, dt.Rows[0], serverDate, excludeBillIds);
        }

        private static PrepaidCardInfo BuildValidatedPrepaidCardIn(
            FbConnection conn,
            FbTransaction tx,
            DataRow r,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            string id = DbHelper.Str(r["ID"]) ?? "";
            string name = DbHelper.Str(r["NAME"]) ?? "";
            DateTime? expiry = ReadDateTime(r["NGAYHETHAN"]);
            bool locked = DbHelper.Int(r["KHOA"]) == ActiveStatus;
            decimal balance = GetPrepaidCardBalanceIn(conn, tx, id, excludeBillIds);

            var info = new PrepaidCardInfo
            {
                Id = id,
                Code = name,
                ExpiryDate = expiry,
                Locked = locked,
                Balance = balance
            };

            if (expiry.HasValue && expiry.Value.Date < serverDate.Date)
                info.Error = ServicePaymentConstants.PrepaidExpired;
            else if (locked)
                info.Error = ServicePaymentConstants.PrepaidLocked;
            else if (balance <= 0m)
                info.Error = ServicePaymentConstants.PrepaidEmptyBalance;
            return info;
        }

        /// <summary>Kiểm tra voucher theo mã (DVOUCHER.NAME) trong transaction/kết nối hiện tại.</summary>
        public static VoucherInfo ValidateVoucherByCodeIn(
            FbConnection conn,
            FbTransaction tx,
            string code,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            string trimmed = (code ?? "").Trim();
            if (trimmed.Length == 0)
                return new VoucherInfo { Error = ServicePaymentConstants.VoucherCodeEmpty };

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME, COALESCE(GIATRI, 0) AS GIATRI, HANSUDUNG " +
                "FROM DVOUCHER WHERE NAME = @N AND STATUS = @S",
                "@N", trimmed, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return new VoucherInfo { Code = trimmed, Error = ServicePaymentConstants.VoucherNotFound(trimmed) };

            return BuildValidatedVoucherIn(conn, tx, dt.Rows[0], serverDate, excludeBillIds);
        }

        public static VoucherInfo ValidateVoucherByIdIn(
            FbConnection conn,
            FbTransaction tx,
            string voucherId,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(voucherId))
                return new VoucherInfo { Error = ServicePaymentConstants.VoucherCodeEmpty };

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME, COALESCE(GIATRI, 0) AS GIATRI, HANSUDUNG " +
                "FROM DVOUCHER WHERE ID = @I AND STATUS = @S",
                "@I", voucherId, "@S", ActiveStatus);
            if (dt.Rows.Count == 0)
                return new VoucherInfo { Id = voucherId, Error = ServicePaymentConstants.VoucherNotFound(voucherId) };

            return BuildValidatedVoucherIn(conn, tx, dt.Rows[0], serverDate, excludeBillIds);
        }

        private static VoucherInfo BuildValidatedVoucherIn(
            FbConnection conn,
            FbTransaction tx,
            DataRow r,
            DateTime serverDate,
            IList<string> excludeBillIds)
        {
            string id = DbHelper.Str(r["ID"]) ?? "";
            string name = DbHelper.Str(r["NAME"]) ?? "";
            decimal val = DbHelper.Dec(r["GIATRI"]);
            DateTime? expiry = ReadDateTime(r["HANSUDUNG"]);

            var info = new VoucherInfo { Id = id, Code = name, Value = val, ExpiryDate = expiry };
            if (expiry.HasValue && expiry.Value.Date < serverDate.Date)
            {
                info.Error = ServicePaymentConstants.VoucherExpired;
                return info;
            }

            var usedArgs = new List<object> { "@V", id, "@Del", DeletedStatus };
            string excludeSql = BuildNotInClause("ID", excludeBillIds, usedArgs, "@EX");
            int usedCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TDONHANG WHERE DVOUCHERID = @V " +
                "AND COALESCE(DATHANHTOAN, 0) = 30 AND COALESCE(STATUS, 0) <> @Del" + excludeSql,
                usedArgs.ToArray()));
            if (usedCount > 0)
                info.Error = ServicePaymentConstants.VoucherAlreadyUsed;
            return info;
        }

        public static string BuildNotInClause(string columnName, IList<string> excludeIds, List<object> args, string paramPrefix)
        {
            if (excludeIds == null || excludeIds.Count == 0)
                return "";
            var names = new List<string>(excludeIds.Count);
            int idx = 0;
            foreach (string id in excludeIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                string p = paramPrefix + idx++;
                names.Add(p);
                args.Add(p);
                args.Add(id);
            }
            if (names.Count == 0)
                return "";
            return " AND " + columnName + " NOT IN (" + string.Join(", ", names.ToArray()) + ")";
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            return Convert.ToDateTime(v);
        }
    }
}
