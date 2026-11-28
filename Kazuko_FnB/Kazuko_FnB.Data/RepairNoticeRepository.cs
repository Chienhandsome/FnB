using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2).
    /// Mọi câu SQL tham số hóa; chỉ dùng cột có thật trên schema Firebird; xóa cứng; không DDL.
    /// DASUAXONG và CONSUDUNGDUOC ghi 30 khi tích, 0 khi không; đọc coi khác 0 là có.
    /// </summary>
    public sealed class RepairNoticeRepository
    {
        public const int ActiveStatus = RepairNoticeText.ActiveStatus;
        public const int DeletedStatus = RepairNoticeText.DeletedStatus;

        private const string SelectSql =
            "SELECT T.ID, T.NAME, T.NGAY, T.DBANID, T.DLOAIPHONGID, T.DNHANVIENID, " +
            "T.NOIDUNG, COALESCE(T.DASUAXONG, 0) AS DASUAXONG, " +
            "COALESCE(T.CONSUDUNGDUOC, 0) AS CONSUDUNGDUOC, T.NOTE, " +
            "COALESCE(T.STATUS, 0) AS STATUS, " +
            "T.USERCREATEDID, T.TIMECREATED, T.USERMODIFIEDID, T.TIMEMODIFIED, " +
            "COALESCE(B.NAME, '') AS ROOMNAME, " +
            "COALESCE(LP.NAME, '') AS ROOMTYPENAME, " +
            "COALESCE(NV.NAME, '') AS EMPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, " +
            "COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM TSUACHUA T " +
            "LEFT JOIN DBAN B ON B.ID = T.DBANID " +
            "LEFT JOIN DLOAIPHONG LP ON LP.ID = T.DLOAIPHONGID " +
            "LEFT JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
            "LEFT JOIN SUSER UC ON UC.ID = T.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = T.USERMODIFIEDID ";

        private readonly string _databasePath;

        public RepairNoticeRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public List<RepairNoticeRow> LoadList(RepairNoticeFilter filter)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var args = new List<object> { "@ST", ActiveStatus };
                string where = "WHERE T.STATUS = @ST ";

                if (filter != null)
                {
                    if (filter.FromDate.HasValue)
                    {
                        where += "AND T.NGAY >= @From ";
                        args.Add("@From");
                        args.Add(filter.FromDate.Value.Date);
                    }
                    if (filter.ToDate.HasValue)
                    {
                        where += "AND T.NGAY < @ToExcl ";
                        args.Add("@ToExcl");
                        args.Add(filter.ToDate.Value.Date.AddDays(1));
                    }
                    if (filter.UnfixedOnly)
                    {
                        where += "AND COALESCE(T.DASUAXONG, 0) = 0 ";
                    }
                }

                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + where +
                    "ORDER BY CASE WHEN COALESCE(T.DASUAXONG, 0) = 0 THEN 0 ELSE 1 END, " +
                    "T.NGAY DESC, T.TIMECREATED DESC, T.NAME DESC",
                    args.ToArray());

                var list = new List<RepairNoticeRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        public RepairNoticeRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static RepairNoticeRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE T.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        /// <summary>Danh sách Phòng/Bàn đang dùng (STATUS = 30) kèm DLOAIPHONGID để tự điền Loại phòng.</summary>
        public List<RepairRoomLookup> LoadActiveRooms()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, DLOAIPHONGID FROM DBAN WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<RepairRoomLookup>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new RepairRoomLookup
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        RoomTypeId = DbHelper.Str(r["DLOAIPHONGID"])
                    });
                }
                return list;
            });
        }

        public List<LookupItem> LoadActiveRoomTypes()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLOAIPHONG WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DNHANVIEN WHERE STATUS = @S AND (ITEMTYPE IS NULL OR ITEMTYPE = 0) ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public static bool RoomExistsIn(FbConnection conn, FbTransaction tx, string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return false;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DBAN WHERE ID = @I AND STATUS = @S",
                "@I", roomId, "@S", ActiveStatus));
            return n > 0;
        }

        public static string LoadRoomTypeOfRoomIn(FbConnection conn, FbTransaction tx, string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return null;
            return DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT DLOAIPHONGID FROM DBAN WHERE ID = @I", "@I", roomId));
        }

        /// <summary>Tự sinh số phiếu sửa chữa: SC + yy + / + 5 chữ số tăng dần (giống RewardPenalty TP).</summary>
        public string GenerateNumber(DateTime? forDate = null)
        {
            DateTime d = forDate.HasValue ? forDate.Value.Date : DateTime.Today;
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, d));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, DateTime date)
        {
            string template = RepairNoticeText.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TSUACHUA WHERE NAME IS NOT NULL AND COALESCE(STATUS, 0) <> @Del",
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

        /// <summary>Kiểm tra trùng số phiếu theo UNICODE_CI_AI (không phân biệt hoa/thường và dấu).</summary>
        public static bool IsNumberTakenIn(FbConnection conn, FbTransaction tx, string number, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(number))
                return false;
            string target = RepairNoticeText.FoldNoTrim(number.Trim());
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM TSUACHUA WHERE NAME IS NOT NULL AND COALESCE(STATUS, 0) <> @Del",
                "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                string existing = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                if (existing.Length > 0 && string.Equals(RepairNoticeText.FoldNoTrim(existing), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, RepairNoticeRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TSUACHUA (ID, NAME, NGAY, DBANID, DLOAIPHONGID, DNHANVIENID, NOIDUNG, " +
                "DASUAXONG, CONSUDUNGDUOC, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NGAY, @BAN, @LP, @NV, @ND, @FIXED, @USABLE, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@BAN", NullIfEmpty(row.RoomId),
                "@LP", NullIfEmpty(row.RoomTypeId),
                "@NV", NullIfEmpty(row.EmployeeId),
                "@ND", row.Content ?? "",
                "@FIXED", row.IsFixed ? RepairNoticeText.FlagOn : RepairNoticeText.FlagOff,
                "@USABLE", row.IsUsable ? RepairNoticeText.FlagOn : RepairNoticeText.FlagOff,
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, RepairNoticeRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TSUACHUA SET NAME = @NAME, NGAY = @NGAY, DBANID = @BAN, DLOAIPHONGID = @LP, " +
                "DNHANVIENID = @NV, NOIDUNG = @ND, DASUAXONG = @FIXED, CONSUDUNGDUOC = @USABLE, " +
                "NOTE = @NOTE, USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@BAN", NullIfEmpty(row.RoomId),
                "@LP", NullIfEmpty(row.RoomTypeId),
                "@NV", NullIfEmpty(row.EmployeeId),
                "@ND", row.Content ?? "",
                "@FIXED", row.IsFixed ? RepairNoticeText.FlagOn : RepairNoticeText.FlagOff,
                "@USABLE", row.IsUsable ? RepairNoticeText.FlagOn : RepairNoticeText.FlagOff,
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TSUACHUA WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static RepairNoticeRow RowFrom(DataRow r)
        {
            return new RepairNoticeRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                RoomId = DbHelper.Str(r["DBANID"]),
                RoomTypeId = DbHelper.Str(r["DLOAIPHONGID"]),
                EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                Content = DbHelper.Str(r["NOIDUNG"]) ?? "",
                IsFixed = DbHelper.Int(r["DASUAXONG"]) != 0,
                IsUsable = DbHelper.Int(r["CONSUDUNGDUOC"]) != 0,
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                RoomName = DbHelper.Str(r["ROOMNAME"]) ?? "",
                RoomTypeName = DbHelper.Str(r["ROOMTYPENAME"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
