using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi lưu vết hoạt động (TLUUVET) và danh sách hóa đơn hai nhánh TDONHANG + TDONHANGHUY
    /// theo docs/specs/luu-vet-hoat-dong.md. SQL tham số hóa 100%, không đổi cấu trúc DB.
    /// </summary>
    public sealed class ActivityLogRepository
    {
        private readonly string _databasePath;

        public ActivityLogRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Thời điểm hiện tại của máy chủ DB (Firebird CURRENT_TIMESTAMP).</summary>
        public DateTime GetServerTime()
        {
            return DbHelper.Read(_databasePath, conn => GetServerTime(conn, null));
        }

        public static DateTime GetServerTime(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
            return v == null || v is DBNull ? DbHelper.Now() : Convert.ToDateTime(v);
        }

        /// <summary>Đọc trực tiếp cờ SCONFIG.KichHoatLuuVetHoatDong (= 30 là bật).</summary>
        public bool IsLoggingEnabled()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT INTVALUE FROM SCONFIG WHERE NAME = @Name",
                    "@Name", ActivityConstants.ConfigKey);
                return DbHelper.Int(v) == ActivityConstants.ConfigEnabledValue;
            });
        }

        /// <summary>
        /// Danh sách hóa đơn cho lưới trái (spec §2.1): UNION ALL giữa TDONHANG (LOAI = 0) và TDONHANGHUY,
        /// sắp theo NAME. Khoảng ngày tính trọn ngày "Đến" (NGAY &gt;= Từ AND NGAY &lt; Đến + 1 ngày).
        /// Khi có Số HĐ: thêm OR NAME = @Bill vào cả hai nhánh (tham số hóa, P1) nên bill ngoài khoảng ngày vẫn hiện.
        /// </summary>
        public List<ActivityBillItem> LoadBills(DateTime fromDate, DateTime toDate, string billNumber)
        {
            DateTime from = fromDate.Date;
            DateTime toExclusive = toDate.Date.AddDays(1);
            string number = (billNumber ?? "").Trim();
            bool hasNumber = number.Length > 0;

            return DbHelper.Read(_databasePath, conn =>
            {
                string cond1 = hasNumber
                    ? "((H.NGAY >= @From1 AND H.NGAY < @To1) OR H.NAME = @Bill1)"
                    : "(H.NGAY >= @From1 AND H.NGAY < @To1)";
                string cond2 = hasNumber
                    ? "((Y.NGAY >= @From2 AND Y.NGAY < @To2) OR Y.NAME = @Bill2)"
                    : "(Y.NGAY >= @From2 AND Y.NGAY < @To2)";

                string sql =
                    "SELECT H.ID AS ID, H.NAME AS NAME, COALESCE(B.NAME, '') AS BAN, " +
                    "CASE WHEN H.DATHANHTOAN = 30 THEN 0 ELSE 2 END AS LOAI, " +
                    "CAST('' AS VARCHAR(255)) AS TRANGTHAI " +
                    "FROM TDONHANG H " +
                    "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                    "WHERE H.LOAI = 0 AND " + cond1 + " " +
                    "UNION ALL " +
                    "SELECT Y.TDONHANGID AS ID, Y.NAME AS NAME, COALESCE(Y.BAN, '') AS BAN, " +
                    "1 AS LOAI, " +
                    "CAST(COALESCE(Y.LYDOHUY, '') AS VARCHAR(255)) AS TRANGTHAI " +
                    "FROM TDONHANGHUY Y " +
                    "WHERE " + cond2 + " " +
                    "ORDER BY 2";

                DataTable dt = hasNumber
                    ? DbHelper.Table(conn, null, sql,
                        "@From1", from, "@To1", toExclusive, "@Bill1", number,
                        "@From2", from, "@To2", toExclusive, "@Bill2", number)
                    : DbHelper.Table(conn, null, sql,
                        "@From1", from, "@To1", toExclusive,
                        "@From2", from, "@To2", toExclusive);

                var list = new List<ActivityBillItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    int loai = DbHelper.Int(r["LOAI"]);
                    ActivityBillKind kind = loai == 1
                        ? ActivityBillKind.Cancelled
                        : (loai == 2 ? ActivityBillKind.InUse : ActivityBillKind.Completed);
                    string statusText;
                    if (kind == ActivityBillKind.Completed)
                        statusText = "Kết thúc";
                    else if (kind == ActivityBillKind.InUse)
                        statusText = "Đang sử dụng";
                    else
                        statusText = DbHelper.Str(r["TRANGTHAI"]) ?? "";

                    list.Add(new ActivityBillItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Number = DbHelper.Str(r["NAME"]) ?? "",
                        TableName = DbHelper.Str(r["BAN"]) ?? "",
                        Kind = kind,
                        StatusText = statusText
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Danh sách lưu vết của một hóa đơn (spec §2.2), sắp tăng dần theo CAST(NGAY AS DATE), GIO.
        /// Nếu chưa chọn hóa đơn (billId rỗng) trả về danh sách trống.
        /// </summary>
        public List<ActivityLogEntry> LoadLogs(string billId, string accountName)
        {
            if (string.IsNullOrEmpty(billId))
                return new List<ActivityLogEntry>();

            string account = (accountName ?? "").Trim();
            bool filterAccount = account.Length > 0;

            return DbHelper.Read(_databasePath, conn =>
            {
                string sql =
                    "SELECT L.ID, L.NGAY, L.GIO, L.SODONHANG, " +
                    "COALESCE(" +
                    "(SELECT FIRST 1 H.NAME FROM TDONHANG H WHERE H.ID = L.SODONHANG), " +
                    "(SELECT FIRST 1 Y.NAME FROM TDONHANGHUY Y WHERE Y.TDONHANGID = L.SODONHANG), " +
                    "'') AS BILLNAME, " +
                    "L.NOTE, L.TAIKHOAN, L.THIETBI, L.BAN, L.CHUCNANG, L.PHANLOAI, " +
                    "L.TENHANG, L.SOLUONG, L.DONGIA, L.THANHTIEN, L.USERCREATEDID " +
                    "FROM TLUUVET L " +
                    "WHERE L.SODONHANG = @BillId" +
                    (filterAccount ? " AND L.TAIKHOAN = @Account" : "") +
                    " ORDER BY CAST(L.NGAY AS DATE), L.GIO";

                DataTable dt = filterAccount
                    ? DbHelper.Table(conn, null, sql, "@BillId", billId, "@Account", account)
                    : DbHelper.Table(conn, null, sql, "@BillId", billId);

                var list = new List<ActivityLogEntry>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    DateTime? gio = ReadTime(r["GIO"]);
                    DateTime? ngay = ReadTime(r["NGAY"]);
                    DateTime timeVal = gio ?? ngay ?? DateTime.MinValue;
                    DateTime dateVal = ngay.HasValue ? ngay.Value.Date : timeVal.Date;

                    list.Add(new ActivityLogEntry
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Date = dateVal,
                        Time = timeVal,
                        BillId = DbHelper.Str(r["SODONHANG"]) ?? "",
                        BillNumber = DbHelper.Str(r["BILLNAME"]) ?? "",
                        Note = DbHelper.Str(r["NOTE"]) ?? "",
                        Account = DbHelper.Str(r["TAIKHOAN"]) ?? "",
                        Device = DbHelper.Str(r["THIETBI"]) ?? "",
                        TableName = DbHelper.Str(r["BAN"]) ?? "",
                        FunctionName = DbHelper.Str(r["CHUCNANG"]) ?? "",
                        KindValue = DbHelper.Int(r["PHANLOAI"]),
                        ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                        Quantity = DbHelper.Dec(r["SOLUONG"]),
                        UnitPrice = DbHelper.Dec(r["DONGIA"]),
                        Amount = DbHelper.Dec(r["THANHTIEN"]),
                        CreatedById = DbHelper.Str(r["USERCREATEDID"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Danh sách tài khoản SUSER (kể cả Admin) cho ô lọc Tài khoản.</summary>
        public List<ActivityAccountItem> LoadAccounts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, USERNAME, NAME FROM SUSER WHERE STATUS = @S ORDER BY USERNAME, NAME",
                    "@S", CatalogRepository.ActiveStatus);
                var list = new List<ActivityAccountItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string userName = (DbHelper.Str(r["USERNAME"]) ?? "").Trim();
                    string displayName = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                    string filterValue = userName.Length > 0 ? userName : displayName;
                    if (filterValue.Length == 0)
                        continue;
                    list.Add(new ActivityAccountItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        UserName = userName,
                        DisplayName = displayName,
                        FilterValue = filterValue
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc tên bàn của hóa đơn (TDONHANG JOIN DBAN, nếu đã hủy thì lấy TDONHANGHUY.BAN).</summary>
        public static string ResolveBillTableName(FbConnection conn, FbTransaction tx, string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return "";
            string name = DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 COALESCE(B.NAME, '') FROM TDONHANG H LEFT JOIN DBAN B ON B.ID = H.DBANID WHERE H.ID = @Id",
                "@Id", billId));
            if (!string.IsNullOrEmpty(name))
                return name;
            string cancelled = DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 COALESCE(Y.BAN, '') FROM TDONHANGHUY Y WHERE Y.TDONHANGID = @Id",
                "@Id", billId));
            return cancelled ?? "";
        }

        /// <summary>Ghi một dòng lưu vết trong một transaction.</summary>
        public void Insert(ActivityLogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            InsertBatch(new[] { entry });
        }

        /// <summary>Ghi nhiều dòng lưu vết trong MỘT transaction (spec §2.5).</summary>
        public int InsertBatch(IEnumerable<ActivityLogEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            var list = new List<ActivityLogEntry>(entries);
            if (list.Count == 0)
                return 0;

            int count = 0;
            DbHelper.InTransaction(_databasePath, delegate(FbConnection conn, FbTransaction tx)
            {
                count = InsertBatch(conn, tx, list);
            });
            return count;
        }

        /// <summary>Ghi nhiều dòng lưu vết trong transaction đã mở sẵn (cho màn lưu bill gọi chung transaction).</summary>
        public static int InsertBatch(FbConnection conn, FbTransaction tx, IEnumerable<ActivityLogEntry> entries)
        {
            if (conn == null) throw new ArgumentNullException("conn");
            if (entries == null) throw new ArgumentNullException("entries");

            DateTime? serverTime = null;
            int count = 0;
            foreach (ActivityLogEntry entry in entries)
            {
                if (entry == null)
                    continue;
                if (entry.Time == DateTime.MinValue)
                {
                    if (!serverTime.HasValue)
                        serverTime = GetServerTime(conn, tx);
                    entry.Time = serverTime.Value;
                }
                entry.Date = entry.Time.Date;
                if (string.IsNullOrEmpty(entry.Id))
                    entry.Id = DbHelper.NewId();
                if (string.IsNullOrWhiteSpace(entry.TableName) && !string.IsNullOrEmpty(entry.BillId))
                    entry.TableName = ResolveBillTableName(conn, tx, entry.BillId);

                const string sql =
                    "INSERT INTO TLUUVET (" +
                    "ID, STATUS, USERCREATEDID, TIMECREATED, " +
                    "NGAY, GIO, SODONHANG, TAIKHOAN, THIETBI, " +
                    "PHANLOAI, BAN, CHUCNANG, NOTE, " +
                    "TENHANG, SOLUONG, DONGIA, THANHTIEN" +
                    ") VALUES (" +
                    "@Id, @Status, @UserCreatedId, @TimeCreated, " +
                    "@Ngay, @Gio, @SoDonHang, @TaiKhoan, @ThietBi, " +
                    "@PhanLoai, @Ban, @ChucNang, @Note, " +
                    "@TenHang, @SoLuong, @DonGia, @ThanhTien" +
                    ")";

                DbHelper.Exec(conn, tx, sql,
                    "@Id", entry.Id,
                    "@Status", CatalogRepository.ActiveStatus,
                    "@UserCreatedId", entry.CreatedById ?? "",
                    "@TimeCreated", entry.Time,
                    "@Ngay", entry.Date,
                    "@Gio", entry.Time,
                    "@SoDonHang", entry.BillId ?? "",
                    "@TaiKhoan", entry.Account ?? "",
                    "@ThietBi", entry.Device ?? "",
                    "@PhanLoai", entry.KindValue,
                    "@Ban", entry.TableName ?? "",
                    "@ChucNang", entry.FunctionName ?? "",
                    "@Note", entry.Note ?? "",
                    "@TenHang", entry.ItemName ?? "",
                    "@SoLuong", entry.Quantity,
                    "@DonGia", entry.UnitPrice,
                    "@ThanhTien", entry.Amount);
                count++;
            }
            return count;
        }

        /// <summary>Xóa toàn bộ bảng TLUUVET không điều kiện (spec §3.4).</summary>
        public int DeleteAll()
        {
            int affected = 0;
            DbHelper.InTransaction(_databasePath, delegate(FbConnection conn, FbTransaction tx)
            {
                affected = DbHelper.Exec(conn, tx, "DELETE FROM TLUUVET");
            });
            return affected;
        }

        private static DateTime? ReadTime(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
