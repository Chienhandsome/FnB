using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu thưởng phạt nhân viên (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2).
    /// Mọi câu SQL tham số hóa; chỉ dùng cột có thật trên schema Firebird; xóa cứng; không DDL.
    /// </summary>
    public sealed class RewardPenaltyRepository
    {
        public const int ActiveStatus = RewardPenaltyText.ActiveStatus;
        public const int DeletedStatus = RewardPenaltyText.DeletedStatus;

        private const string SelectSql =
            "SELECT T.ID, T.NAME, T.NGAY, T.DNHANVIENID, " +
            "COALESCE(T.THUONG, 0) AS THUONG, COALESCE(T.PHAT, 0) AS PHAT, " +
            "T.DLYDOTHUONGPHATID, T.NOTE, COALESCE(T.STATUS, 0) AS STATUS, " +
            "T.USERCREATEDID, T.TIMECREATED, T.USERMODIFIEDID, T.TIMEMODIFIED, " +
            "COALESCE(NV.NAME, '') AS EMPNAME, " +
            "COALESCE(LD.NAME, '') AS REASONNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, " +
            "COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM TTHUONGPHAT T " +
            "LEFT JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
            "LEFT JOIN DLYDOTHUONGPHAT LD ON LD.ID = T.DLYDOTHUONGPHATID " +
            "LEFT JOIN SUSER UC ON UC.ID = T.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = T.USERMODIFIEDID ";

        private readonly string _databasePath;

        public RewardPenaltyRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public List<RewardPenaltyRow> LoadList(RewardPenaltyFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime from = filter.FromDate.Date;
                DateTime toExcl = filter.ToDate.Date.AddDays(1);
                if (toExcl <= from)
                    toExcl = from.AddDays(1);

                var args = new List<object>
                {
                    "@ST", ActiveStatus,
                    "@From", from,
                    "@ToExcl", toExcl
                };

                string where =
                    "WHERE T.STATUS = @ST " +
                    "AND T.NGAY >= @From AND T.NGAY < @ToExcl ";

                if (!string.IsNullOrEmpty(filter.EmployeeId))
                {
                    where += "AND T.DNHANVIENID = @Emp ";
                    args.Add("@Emp");
                    args.Add(filter.EmployeeId);
                }

                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + where + "ORDER BY T.NGAY DESC, T.TIMECREATED DESC, T.NAME DESC",
                    args.ToArray());

                var list = new List<RewardPenaltyRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        public RewardPenaltyRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static RewardPenaltyRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE T.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DNHANVIEN WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public List<LookupItem> LoadActiveReasons()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLYDOTHUONGPHAT WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public static bool EmployeeExistsIn(FbConnection conn, FbTransaction tx, string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DNHANVIEN WHERE ID = @I AND STATUS = @S",
                "@I", employeeId, "@S", ActiveStatus));
            return n > 0;
        }

        /// <summary>Tự sinh số phiếu thưởng phạt: TP + yy + / + 5 chữ số tăng dần (spec §2, T1).</summary>
        public string GenerateNumber(DateTime? forDate = null)
        {
            DateTime d = forDate.HasValue ? forDate.Value.Date : DateTime.Today;
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, d));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, DateTime date)
        {
            string template = RewardPenaltyText.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TTHUONGPHAT WHERE NAME IS NOT NULL AND COALESCE(STATUS, 0) <> @Del",
                "@Del", DeletedStatus);
            var matched = new List<string>();
            foreach (DataRow r in dt.Rows)
            {
                string raw = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    matched.Add(prefix + raw.Substring(prefix.Length));
            }
            int next = DocumentNumber.NextSequence(matched.ToArray(), prefix);
            return DocumentNumber.Format(template, date, next);
        }

        /// <summary>Kiểm tra trùng số phiếu (không phân biệt hoa/thường).</summary>
        public static bool IsNumberTakenIn(FbConnection conn, FbTransaction tx, string number, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(number))
                return false;
            string target = number.Trim();
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM TTHUONGPHAT WHERE NAME IS NOT NULL AND COALESCE(STATUS, 0) <> @Del",
                "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string existing = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                if (existing.Length > 0 && string.Equals(existing, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, RewardPenaltyRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, " +
                "DLYDOTHUONGPHATID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NGAY, @NV, @TH, @PH, @LD, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@NV", row.EmployeeId,
                "@TH", row.Bonus,
                "@PH", row.Penalty,
                "@LD", NullIfEmpty(row.ReasonId),
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, RewardPenaltyRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TTHUONGPHAT SET NAME = @NAME, NGAY = @NGAY, DNHANVIENID = @NV, " +
                "THUONG = @TH, PHAT = @PH, DLYDOTHUONGPHATID = @LD, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@NV", row.EmployeeId,
                "@TH", row.Bonus,
                "@PH", row.Penalty,
                "@LD", NullIfEmpty(row.ReasonId),
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TTHUONGPHAT WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static RewardPenaltyRow RowFrom(DataRow r)
        {
            return new RewardPenaltyRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                Bonus = DbHelper.Dec(r["THUONG"]),
                Penalty = DbHelper.Dec(r["PHAT"]),
                ReasonId = DbHelper.Str(r["DLYDOTHUONGPHATID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                ReasonName = DbHelper.Str(r["REASONNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
