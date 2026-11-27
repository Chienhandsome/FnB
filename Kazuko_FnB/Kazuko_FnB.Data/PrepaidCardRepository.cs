using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục thẻ trả trước (DTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2–§3).
    /// Số dư thẻ dùng đúng một nguồn duy nhất từ <see cref="ServicePaymentRepository"/>;
    /// kiểm trùng Mã thẻ theo UNICODE_CI_AI; đếm đủ tham chiếu TTHUCHI/TDONHANG/DKHACHHANG; không DDL.
    /// </summary>
    public sealed class PrepaidCardRepository
    {
        public const int ActiveStatus = PrepaidCardText.ActiveStatus;
        public const int DeletedStatus = PrepaidCardText.DeletedStatus;

        private const string SelectSql =
            "SELECT C.ID, C.NAME, C.NOTE, C.DNHOMTHETRATRUOCID, COALESCE(C.KHOA, 0) AS KHOA, " +
            "C.NGAYHETHAN, COALESCE(C.STATUS, 0) AS STATUS, " +
            "C.USERCREATEDID, C.TIMECREATED, C.USERMODIFIEDID, C.TIMEMODIFIED, " +
            "COALESCE(G.NAME, '') AS GROUPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DTHETRATRUOC C " +
            "LEFT JOIN DNHOMTHETRATRUOC G ON G.ID = C.DNHOMTHETRATRUOCID " +
            "LEFT JOIN SUSER UC ON UC.ID = C.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = C.USERMODIFIEDID ";

        private readonly string _databasePath;

        public PrepaidCardRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null).Date);
        }

        /// <summary>Mọi thẻ trả trước đang hoạt động (STATUS = 30), kèm tên nhóm, khách đang giữ và 3 cột tiền.</summary>
        public List<PrepaidCardRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE C.STATUS = @S ORDER BY C.NAME",
                    "@S", ActiveStatus);

                Dictionary<string, PrepaidCardBalanceSummary> balances =
                    ServicePaymentRepository.LoadAllPrepaidCardBalancesIn(conn, null);
                Dictionary<string, List<PrepaidCardHolderInfo>> holdersByCard = LoadAllHoldersIn(conn, null);

                var list = new List<PrepaidCardRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    PrepaidCardRow row = RowFrom(r);
                    PrepaidCardBalanceSummary bal;
                    if (balances.TryGetValue(row.Id, out bal))
                    {
                        row.TotalLoaded = bal.Loaded;
                        row.TotalUsed = bal.Used;
                    }
                    List<PrepaidCardHolderInfo> holders;
                    if (holdersByCard.TryGetValue(row.Id, out holders))
                    {
                        row.Holders = holders;
                        row.HolderNames = JoinHolderNames(holders);
                    }
                    list.Add(row);
                }
                return list;
            });
        }

        /// <summary>Một thẻ theo ID (mọi STATUS), kèm khách giữ thẻ và số dư từ hàm chuẩn; null nếu không tồn tại.</summary>
        public PrepaidCardRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static PrepaidCardRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE C.ID = @I", "@I", id);
            if (dt.Rows.Count == 0)
                return null;
            PrepaidCardRow row = RowFrom(dt.Rows[0]);
            PrepaidCardBalanceSummary bal = ServicePaymentRepository.GetPrepaidCardBalanceSummaryIn(conn, tx, id, null);
            row.TotalLoaded = bal.Loaded;
            row.TotalUsed = bal.Used;
            row.Holders = LoadHoldersIn(conn, tx, id);
            row.HolderNames = JoinHolderNames(row.Holders);
            return row;
        }

        public static List<PrepaidCardHolderInfo> LoadHoldersIn(FbConnection conn, FbTransaction tx, string cardId)
        {
            var list = new List<PrepaidCardHolderInfo>();
            if (string.IsNullOrEmpty(cardId))
                return list;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, DIACHI FROM DKHACHHANG WHERE DTHETRATRUOCID = @C AND STATUS = @S ORDER BY NAME",
                "@C", cardId, "@S", ActiveStatus);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PrepaidCardHolderInfo
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Address = DbHelper.Str(r["DIACHI"]) ?? ""
                });
            }
            return list;
        }

        /// <summary>Lịch sử nạp tiền &amp; chi trả (TTHUCHI gắn thẻ, trừ STATUS = -30), sắp ngày giảm dần.</summary>
        public List<PrepaidCardVoucherItem> LoadVoucherHistory(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return new List<PrepaidCardVoucherItem>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT T.ID, T.NAME, T.NGAY, COALESCE(T.LOAI, 0) AS LOAI, " +
                    "COALESCE(T.THU, 0) AS THU, COALESCE(T.CHI, 0) AS CHI, " +
                    "COALESCE(T.CHUYENKHOAN, 0) AS CHUYENKHOAN, T.DIENGIAI, " +
                    "COALESCE(TK.NAME, '') AS BANKNAME " +
                    "FROM TTHUCHI T " +
                    "LEFT JOIN DTAIKHOANNGANHANG TK ON TK.ID = T.DTAIKHOANNGANHANGID " +
                    "WHERE T.DTHETRATRUOCID = @C AND COALESCE(T.STATUS, 0) <> @Del " +
                    "ORDER BY T.NGAY DESC, T.TIMECREATED DESC, T.NAME DESC",
                    "@C", cardId, "@Del", DeletedStatus);
                var list = new List<PrepaidCardVoucherItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    int loai = DbHelper.Int(r["LOAI"]);
                    list.Add(new PrepaidCardVoucherItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Number = DbHelper.Str(r["NAME"]) ?? "",
                        Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                        Kind = loai == 1 ? VoucherKind.Expense : VoucherKind.Income,
                        IncomeAmount = DbHelper.Dec(r["THU"]),
                        ExpenseAmount = DbHelper.Dec(r["CHI"]),
                        Transfer = DbHelper.Int(r["CHUYENKHOAN"]) != 0,
                        BankAccountName = DbHelper.Str(r["BANKNAME"]) ?? "",
                        Description = DbHelper.Str(r["DIENGIAI"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Lịch sử sử dụng thẻ trên hóa đơn (TDONHANG gắn thẻ, trừ STATUS = -30), sắp ngày giảm dần.</summary>
        public List<PrepaidCardUsageItem> LoadUsageHistory(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return new List<PrepaidCardUsageItem>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, NGAY, COALESCE(THETRATRUOC, 0) AS THETRATRUOC " +
                    "FROM TDONHANG WHERE DTHETRATRUOCID = @C AND COALESCE(STATUS, 0) <> @Del " +
                    "ORDER BY NGAY DESC, TIMECREATED DESC, NAME DESC",
                    "@C", cardId, "@Del", DeletedStatus);
                var list = new List<PrepaidCardUsageItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new PrepaidCardUsageItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Number = DbHelper.Str(r["NAME"]) ?? "",
                        Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                        UsedAmount = DbHelper.Dec(r["THETRATRUOC"])
                    });
                }
                return list;
            });
        }

        /// <summary>Lý do thu/chi của phiếu nạp hoặc chi trả thẻ gần nhất (để điền mặc định theo spec §3).</summary>
        public string LoadLatestCardVoucherReasonId(VoucherKind kind)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string reasonFilter = kind == VoucherKind.Income
                    ? "AND COALESCE(L.LALYDOTHU, 0) = 30 "
                    : "AND COALESCE(L.LALYDOTHU, 0) <> 30 ";
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 T.DLYDOTHUCHIID FROM TTHUCHI T " +
                    "LEFT JOIN DLYDOTHUCHI L ON L.ID = T.DLYDOTHUCHIID " +
                    "WHERE T.DTHETRATRUOCID IS NOT NULL AND T.DTHETRATRUOCID <> '' " +
                    "AND COALESCE(T.LOAI, 0) = @Loai " +
                    "AND COALESCE(T.STATUS, 0) = @St " +
                    "AND T.DLYDOTHUCHIID IS NOT NULL AND T.DLYDOTHUCHIID <> '' " +
                    reasonFilter +
                    "ORDER BY T.TIMECREATED DESC",
                    "@Loai", (int)kind, "@St", ActiveStatus);
                return DbHelper.Str(v);
            });
        }

        public static bool IsReasonMatchingKindIn(FbConnection conn, FbTransaction tx, string reasonId, VoucherKind kind)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
                return true;
            string sql = kind == VoucherKind.Income
                ? "SELECT COUNT(*) FROM DLYDOTHUCHI WHERE ID = @I AND COALESCE(LALYDOTHU, 0) = 30"
                : "SELECT COUNT(*) FROM DLYDOTHUCHI WHERE ID = @I AND COALESCE(LALYDOTHU, 0) <> 30";
            return DbHelper.Int(DbHelper.Scalar(conn, tx, sql, "@I", reasonId)) > 0;
        }

        public bool IsCodeTaken(string code, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            return DbHelper.Read(_databasePath, conn => IsCodeTakenIn(conn, null, code, excludeId));
        }

        public static bool IsCodeTakenIn(FbConnection conn, FbTransaction tx, string code, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            string target = PrepaidCardText.FoldNoTrim(code);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DTHETRATRUOC WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(PrepaidCardText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Đếm số phiếu thu chi (STATUS &lt;&gt; -30), hóa đơn và khách hàng tham chiếu thẻ (spec §2).</summary>
        public static PrepaidCardReferenceCounts CountReferencesIn(FbConnection conn, FbTransaction tx, string cardId)
        {
            var counts = new PrepaidCardReferenceCounts();
            if (string.IsNullOrEmpty(cardId))
                return counts;
            counts.VoucherCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TTHUCHI WHERE DTHETRATRUOCID = @I AND COALESCE(STATUS, 0) <> @Del",
                "@I", cardId, "@Del", DeletedStatus));
            counts.OrderCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TDONHANG WHERE DTHETRATRUOCID = @I",
                "@I", cardId));
            counts.CustomerCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DKHACHHANG WHERE DTHETRATRUOCID = @I",
                "@I", cardId));
            return counts;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, PrepaidCardRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DTHETRATRUOC (ID, NAME, NOTE, DNHOMTHETRATRUOCID, KHOA, NGAYHETHAN, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NOTE, @GRP, @KHOA, @EXP, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Code ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@KHOA", row.Locked ? ActiveStatus : 0,
                "@EXP", row.ExpiryDate.HasValue ? (object)row.ExpiryDate.Value.Date : null,
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, PrepaidCardRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DTHETRATRUOC SET NAME = @NAME, NOTE = @NOTE, DNHOMTHETRATRUOCID = @GRP, " +
                "KHOA = @KHOA, NGAYHETHAN = @EXP, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Code ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@GRP", NullIfEmpty(row.GroupId),
                "@KHOA", row.Locked ? ActiveStatus : 0,
                "@EXP", row.ExpiryDate.HasValue ? (object)row.ExpiryDate.Value.Date : null,
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DTHETRATRUOC WHERE ID = @I", "@I", id);
        }

        private static Dictionary<string, List<PrepaidCardHolderInfo>> LoadAllHoldersIn(FbConnection conn, FbTransaction tx)
        {
            var map = new Dictionary<string, List<PrepaidCardHolderInfo>>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, DIACHI, DTHETRATRUOCID FROM DKHACHHANG " +
                "WHERE DTHETRATRUOCID IS NOT NULL AND TRIM(DTHETRATRUOCID) <> '' AND STATUS = @S ORDER BY NAME",
                "@S", ActiveStatus);
            foreach (DataRow r in dt.Rows)
            {
                string cid = DbHelper.Str(r["DTHETRATRUOCID"]) ?? "";
                if (cid.Length == 0)
                    continue;
                List<PrepaidCardHolderInfo> list;
                if (!map.TryGetValue(cid, out list))
                    map[cid] = list = new List<PrepaidCardHolderInfo>();
                list.Add(new PrepaidCardHolderInfo
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Address = DbHelper.Str(r["DIACHI"]) ?? ""
                });
            }
            return map;
        }

        private static string JoinHolderNames(List<PrepaidCardHolderInfo> holders)
        {
            if (holders == null || holders.Count == 0)
                return "";
            var names = new List<string>(holders.Count);
            foreach (PrepaidCardHolderInfo h in holders)
            {
                string n = (h.Name ?? "").Trim();
                if (n.Length > 0)
                    names.Add(n);
            }
            return string.Join(", ", names.ToArray());
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static DateTime? ReadDate(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }

        private static PrepaidCardRow RowFrom(DataRow r)
        {
            return new PrepaidCardRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMTHETRATRUOCID"]),
                Locked = DbHelper.Int(r["KHOA"]) != 0,
                ExpiryDate = ReadDate(r["NGAYHETHAN"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = ReadDate(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = ReadDate(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                HolderNames = "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
