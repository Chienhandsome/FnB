using System;
using System.Data;
using System.IO;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Ghi/đọc phiếu thu (TTHUCHI) cho luồng đặt tiền trước (§3.5 của spec Khách đặt hàng).
    /// Chỉ phục vụ chế độ cọc của PhieuThuDialog; dialog Phiếu thu độc lập vẫn là bản thử (không ghi).
    /// Số phiếu theo SFORM "Phiếu thu".NOTEMPLATE ("PT(yy)/(*****)").
    /// </summary>
    public sealed class PhieuThuRepository
    {
        private readonly string _databasePath;

        public PhieuThuRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Phiếu thu cọc của một phiếu đặt (dòng đầu theo TDATHANGID); null nếu chưa có.</summary>
        public BookingDepositInfo LoadDeposit(string bookingId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT FIRST 1 \"ID\", \"NAME\", \"THU\" FROM \"TTHUCHI\" " +
                    "WHERE \"TDATHANGID\" = @id AND \"STATUS\" = 30 ORDER BY \"TIMECREATED\"",
                    "@id", bookingId);
                if (t.Rows.Count == 0)
                    return null;
                DataRow r = t.Rows[0];
                return new BookingDepositInfo
                {
                    Id = DbHelper.Str(r["ID"]),
                    Number = DbHelper.Str(r["NAME"]),
                    Amount = DbHelper.Dec(r["THU"])
                };
            });
        }

        /// <summary>Lý do của phiếu thu đặt trước gần nhất (điền sẵn khi lập cọc mới, §3.5).</summary>
        public string LoadLatestDepositReasonId()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 \"DLYDOTHUCHIID\" FROM \"TTHUCHI\" " +
                    "WHERE \"TDATHANGID\" IS NOT NULL AND \"DLYDOTHUCHIID\" IS NOT NULL " +
                    "ORDER BY \"TIMECREATED\" DESC");
                return DbHelper.Str(v);
            });
        }

        /// <summary>Một phiếu thu để nạp lại sửa (khi đã có cọc). Null nếu không còn.</summary>
        public BookingDeposit LoadReceipt(string receiptId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT \"ID\", \"NAME\", \"NGAY\", \"TDATHANGID\", \"DKHACHHANGID\", \"DNHANVIENID\", " +
                    "\"DCUAHANGID\", \"DLYDOTHUCHIID\", \"DIENGIAI\", \"THU\", \"CHUYENKHOAN\", " +
                    "\"DTAIKHOANNGANHANGID\", \"TENDOITUONG\", \"DIACHI\" " +
                    "FROM \"TTHUCHI\" WHERE \"ID\" = @id AND \"STATUS\" = 30", "@id", receiptId);
                if (t.Rows.Count == 0)
                    return null;
                DataRow r = t.Rows[0];
                return new BookingDeposit
                {
                    Id = DbHelper.Str(r["ID"]),
                    Number = DbHelper.Str(r["NAME"]),
                    Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                    BookingId = DbHelper.Str(r["TDATHANGID"]),
                    CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                    EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                    StoreId = DbHelper.Str(r["DCUAHANGID"]),
                    ReasonId = DbHelper.Str(r["DLYDOTHUCHIID"]),
                    Note = DbHelper.Str(r["DIENGIAI"]),
                    Amount = DbHelper.Dec(r["THU"]),
                    Transfer = DbHelper.Int(r["CHUYENKHOAN"]) == CatalogRepository.ActiveStatus,
                    BankAccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"]),
                    CustomerName = DbHelper.Str(r["TENDOITUONG"]),
                    Address = DbHelper.Str(r["DIACHI"])
                };
            });
        }

        /// <summary>Số phiếu thu tiếp theo theo mẫu của SFORM "Phiếu thu".</summary>
        public string GenerateNumber()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object tpl = DbHelper.Scalar(conn, null,
                    "SELECT \"NOTEMPLATE\" FROM \"SFORM\" WHERE \"NAME\" = 'Phiếu thu' AND \"STATUS\" = 30");
                string template = DbHelper.Str(tpl);
                if (string.IsNullOrWhiteSpace(template))
                    throw new InvalidDataException("SFORM \"Phiếu thu\" chưa có NOTEMPLATE.");
                DateTime today = DateTime.Today;
                string prefix = DocumentNumber.LiteralPrefix(template, today);
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT \"NAME\" FROM \"TTHUCHI\" WHERE \"NAME\" STARTING WITH @p", "@p", prefix);
                var names = new string[t.Rows.Count];
                for (int i = 0; i < t.Rows.Count; i++)
                    names[i] = DbHelper.Str(t.Rows[i]["NAME"]);
                return DocumentNumber.Format(template, today, DocumentNumber.NextSequence(names, prefix));
            });
        }

        /// <summary>
        /// Lưu phiếu thu cọc: lập mới (Id rỗng, sinh số nếu chưa có) hoặc cập nhật phiếu đã có.
        /// Trả Id đã lưu; null khi số phiếu bị trùng hoặc phiếu cập nhật không còn.
        /// LOAI = 0 (phiếu thu thường); cọc không tính vào công nợ khách (TDATHANGID gắn kèm, §3.5).
        /// </summary>
        public string SaveDeposit(BookingDeposit deposit, string userId)
        {
            string savedId = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DateTime now = DbHelper.Now();
                bool isNew = string.IsNullOrEmpty(deposit.Id);
                if (isNew)
                {
                    if (string.IsNullOrWhiteSpace(deposit.Number))
                        deposit.Number = GenerateNumberInTx(conn, tx);
                    object dup = DbHelper.Scalar(conn, tx,
                        "SELECT FIRST 1 \"ID\" FROM \"TTHUCHI\" WHERE \"NAME\" = @n", "@n", deposit.Number);
                    if (dup != null)
                        return;
                    deposit.Id = DbHelper.NewId();
                    // LOAIDOITUONG: 0 Khác, 1 Nhân viên, 2 Khách hàng, 3 Nhà cung cấp (theo PhieuThuRules).
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO \"TTHUCHI\" (\"ID\", \"NAME\", \"NGAY\", \"LOAI\", \"LOAIDOITUONG\", " +
                        "\"LAPHIEUTHUCONGNO\", \"KHONGTHAYDOICONGNO\", \"LATAMUNG\", \"TENDOITUONG\", \"DIACHI\", " +
                        "\"DKHACHHANGID\", \"DNHANVIENID\", \"DCUAHANGID\", \"DLYDOTHUCHIID\", \"DIENGIAI\", " +
                        "\"THU\", \"CHI\", \"CHUYENKHOAN\", \"DTAIKHOANNGANHANGID\", \"TDATHANGID\", " +
                        "\"STATUS\", \"USERCREATEDID\", \"TIMECREATED\") " +
                        "VALUES (@id, @name, @ngay, 0, @doituong, 0, 0, 0, @ten, @diachi, @cust, @emp, " +
                        "@store, @reason, @note, @thu, 0, @ck, @bank, @booking, 30, @user, @now)",
                        "@id", deposit.Id, "@name", deposit.Number, "@ngay", deposit.Date,
                        "@doituong", deposit.ObjectType,
                        "@ten", deposit.CustomerName, "@diachi", deposit.Address,
                        "@cust", deposit.CustomerId, "@emp", deposit.EmployeeId,
                        "@store", deposit.StoreId, "@reason", deposit.ReasonId,
                        "@note", deposit.Note, "@thu", deposit.Amount,
                        "@ck", deposit.Transfer ? 30 : 0, "@bank", deposit.BankAccountId,
                        "@booking", deposit.BookingId, "@user", userId, "@now", now);
                    savedId = deposit.Id;
                }
                else
                {
                    int n = DbHelper.Exec(conn, tx,
                        "UPDATE \"TTHUCHI\" SET \"NGAY\" = @ngay, \"LOAIDOITUONG\" = @doituong, \"TENDOITUONG\" = @ten, \"DIACHI\" = @diachi, " +
                        "\"DKHACHHANGID\" = @cust, \"DNHANVIENID\" = @emp, \"DCUAHANGID\" = @store, " +
                        "\"DLYDOTHUCHIID\" = @reason, \"DIENGIAI\" = @note, \"THU\" = @thu, " +
                        "\"CHUYENKHOAN\" = @ck, \"DTAIKHOANNGANHANGID\" = @bank, " +
                        "\"USERMODIFIEDID\" = @user, \"TIMEMODIFIED\" = @now WHERE \"ID\" = @id",
                        "@ngay", deposit.Date, "@doituong", deposit.ObjectType, "@ten", deposit.CustomerName, "@diachi", deposit.Address,
                        "@cust", deposit.CustomerId, "@emp", deposit.EmployeeId,
                        "@store", deposit.StoreId, "@reason", deposit.ReasonId,
                        "@note", deposit.Note, "@thu", deposit.Amount,
                        "@ck", deposit.Transfer ? 30 : 0, "@bank", deposit.BankAccountId,
                        "@user", userId, "@now", now, "@id", deposit.Id);
                    if (n > 0)
                        savedId = deposit.Id;
                }
            });
            return savedId;
        }

        private static string GenerateNumberInTx(FirebirdSql.Data.FirebirdClient.FbConnection conn, FirebirdSql.Data.FirebirdClient.FbTransaction tx)
        {
            // Sinh số trong transaction: đọc mẫu + số lớn nhất hiện có (chưa có phiếu nào khác chen vào
            // vì app chạy đơn lẻ; trùng vẫn bị chặn ở bước kiểm tra của SaveDeposit).
            object tpl = DbHelper.Scalar(conn, tx,
                "SELECT \"NOTEMPLATE\" FROM \"SFORM\" WHERE \"NAME\" = 'Phiếu thu' AND \"STATUS\" = 30");
            string template = DbHelper.Str(tpl);
            if (string.IsNullOrWhiteSpace(template))
                throw new InvalidDataException("SFORM \"Phiếu thu\" chưa có NOTEMPLATE.");
            DateTime today = DateTime.Today;
            string prefix = DocumentNumber.LiteralPrefix(template, today);
            DataTable t = DbHelper.Table(conn, tx,
                "SELECT \"NAME\" FROM \"TTHUCHI\" WHERE \"NAME\" STARTING WITH @p", "@p", prefix);
            var names = new string[t.Rows.Count];
            for (int i = 0; i < t.Rows.Count; i++)
                names[i] = DbHelper.Str(t.Rows[i]["NAME"]);
            return DocumentNumber.Format(template, today, DocumentNumber.NextSequence(names, prefix));
        }
    }
}
