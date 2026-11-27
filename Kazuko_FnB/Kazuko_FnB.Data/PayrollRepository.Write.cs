using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Phần ghi/xóa trong transaction và đọc độ chính xác cột tiền từ RDB$RELATION_FIELDS/RDB$FIELDS
    /// cho <see cref="PayrollRepository"/>. Mọi phương thức là public static để tầng Services gọi qua assembly.
    /// </summary>
    public sealed partial class PayrollRepository
    {
        /// <summary>Đọc số chữ số lẻ cho từng cột số của một bảng từ RDB$FIELDS (BIGINT -> 0, NUMERIC(18,2) -> 2, FLOAT/DOUBLE -> -1).</summary>
        public static Dictionary<string, int> ReadNumericScalesIn(FbConnection conn, FbTransaction tx, string tableName)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT TRIM(rf.RDB$FIELD_NAME) AS COL, f.RDB$FIELD_TYPE AS FTYPE, COALESCE(f.RDB$FIELD_SCALE, 0) AS FSCALE " +
                "FROM RDB$RELATION_FIELDS rf " +
                "JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE " +
                "WHERE rf.RDB$RELATION_NAME = @T",
                "@T", tableName);
            foreach (DataRow r in dt.Rows)
            {
                string col = DbHelper.Str(r["COL"]);
                if (string.IsNullOrEmpty(col))
                    continue;
                int ftype = DbHelper.Int(r["FTYPE"]);
                int fscale = DbHelper.Int(r["FSCALE"]);
                if (ftype == 10 || ftype == 27)
                    map[col] = -1; // FLOAT / DOUBLE PRECISION: không cắt số lẻ
                else
                    map[col] = fscale < 0 ? -fscale : 0;
            }
            return map;
        }

        public static decimal RoundForColumn(decimal value, Dictionary<string, int> scales, string columnName)
        {
            int scale;
            if (scales != null && scales.TryGetValue(columnName, out scale))
            {
                if (scale < 0)
                    return value;
                return Math.Round(value, scale, MidpointRounding.AwayFromZero);
            }
            return Math.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Thêm kỳ bảng lương mới vào TBANGLUONG (không chạm cột chết CHITIET).</summary>
        public static void InsertPeriodIn(FbConnection conn, FbTransaction tx, PayrollPeriodRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TBANGLUONG (ID, NAME, THANG, NAM, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@Id, @Name, @M, @Y, @Note, @St, @Uc, @Tc, NULL, NULL)",
                "@Id", row.Id,
                "@Name", PayrollText.Clip(row.Name),
                "@M", row.Month,
                "@Y", row.Year,
                "@Note", NullIfEmpty(PayrollText.Clip(row.Note)),
                "@St", row.Status,
                "@Uc", row.CreatedById ?? "",
                "@Tc", row.CreatedAt ?? DbHelper.Now());
        }

        /// <summary>Xóa sạch cả ba bảng TBANGLUONGCHITIET, TBANGLUONGTONGHOP, TBANGLUONG của một kỳ trong cùng transaction.</summary>
        public static int DeletePeriodCascadeIn(FbConnection conn, FbTransaction tx, string payrollId)
        {
            DbHelper.Exec(conn, tx, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @Id", "@Id", payrollId);
            DbHelper.Exec(conn, tx, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @Id", "@Id", payrollId);
            return DbHelper.Exec(conn, tx, "DELETE FROM TBANGLUONG WHERE ID = @Id", "@Id", payrollId);
        }

        /// <summary>Đọc mọi dòng TBANGLUONGTONGHOP hiện có của bảng lương, tách dòng đầu tiên và các dòng trùng nhân viên (nếu có).</summary>
        public static Dictionary<string, string> LoadSummaryIdsByEmployeeIn(
            FbConnection conn,
            FbTransaction tx,
            string payrollId,
            out List<string> duplicateSummaryIds)
        {
            duplicateSummaryIds = new List<string>();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, DNHANVIENID FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @Id ORDER BY TIMECREATED, ID",
                "@Id", payrollId);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["ID"]) ?? "";
                string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                if (id.Length == 0 || empId.Length == 0)
                    continue;
                if (!map.ContainsKey(empId))
                    map[empId] = id;
                else
                    duplicateSummaryIds.Add(id);
            }
            return map;
        }

        /// <summary>Thêm mới một dòng tổng hợp lương (không chạm SOGIOLAM, SOGIOTANGCA, LUONGTHEOCA; bảng không có cột NAME).</summary>
        public static string InsertSummaryIn(
            FbConnection conn,
            FbTransaction tx,
            string payrollId,
            PayrollEmployeeEntry entry,
            Dictionary<string, int> scales,
            string userId,
            DateTime now)
        {
            string id = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO TBANGLUONGTONGHOP (" +
                "ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, " +
                "TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED" +
                ") VALUES (" +
                "@Id, @Bl, @Nv, @LCa, @LTh, @Mode, @Tong, @Thuong, @Phat, @TamUng, @ThucNhan, NULL, @St, @Uc, @Tc, NULL, NULL)",
                "@Id", id,
                "@Bl", payrollId,
                "@Nv", entry.EmployeeId,
                "@LCa", RoundForColumn(entry.ShiftSalary, scales, "LUONGCA"),
                "@LTh", RoundForColumn(entry.MonthlySalary, scales, "LUONGTHANG"),
                "@Mode", entry.SalaryMode,
                "@Tong", RoundForColumn(entry.TotalSalary, scales, "TONGLUONG"),
                "@Thuong", RoundForColumn(entry.Bonus, scales, "THUONG"),
                "@Phat", RoundForColumn(entry.Penalty, scales, "PHAT"),
                "@TamUng", RoundForColumn(entry.Advance, scales, "TAMUNG"),
                "@ThucNhan", RoundForColumn(entry.NetSalary, scales, "THUCNHAN"),
                "@St", ActiveStatus,
                "@Uc", userId ?? "",
                "@Tc", now);
            return id;
        }

        /// <summary>Cập nhật một dòng tổng hợp lương (không chạm SOGIOLAM, SOGIOTANGCA, LUONGTHEOCA, USERCREATEDID, TIMECREATED).</summary>
        public static int UpdateSummaryIn(
            FbConnection conn,
            FbTransaction tx,
            string summaryId,
            PayrollEmployeeEntry entry,
            Dictionary<string, int> scales,
            string userId,
            DateTime now)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TBANGLUONGTONGHOP SET " +
                "LUONGCA = @LCa, LUONGTHANG = @LTh, CACHTINHLUONG = @Mode, " +
                "TONGLUONG = @Tong, THUONG = @Thuong, PHAT = @Phat, TAMUNG = @TamUng, THUCNHAN = @ThucNhan, " +
                "USERMODIFIEDID = @Um, TIMEMODIFIED = @Tm " +
                "WHERE ID = @Id",
                "@LCa", RoundForColumn(entry.ShiftSalary, scales, "LUONGCA"),
                "@LTh", RoundForColumn(entry.MonthlySalary, scales, "LUONGTHANG"),
                "@Mode", entry.SalaryMode,
                "@Tong", RoundForColumn(entry.TotalSalary, scales, "TONGLUONG"),
                "@Thuong", RoundForColumn(entry.Bonus, scales, "THUONG"),
                "@Phat", RoundForColumn(entry.Penalty, scales, "PHAT"),
                "@TamUng", RoundForColumn(entry.Advance, scales, "TAMUNG"),
                "@ThucNhan", RoundForColumn(entry.NetSalary, scales, "THUCNHAN"),
                "@Um", userId ?? "",
                "@Tm", now,
                "@Id", summaryId);
        }

        public static int DeleteSummaryByIdIn(FbConnection conn, FbTransaction tx, string summaryId)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TBANGLUONGTONGHOP WHERE ID = @Id", "@Id", summaryId);
        }

        /// <summary>Đọc toàn bộ dòng chi tiết chấm công của bảng lương theo thứ tự ổn định (TIMECREATED, ID).</summary>
        public static List<PayrollDetailRecord> LoadDetailRowsIn(FbConnection conn, FbTransaction tx, string payrollId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, DNHANVIENID, DCALAMVIECID, NGAY, COALESCE(TRANGTHAI, 0) AS TRANGTHAI " +
                "FROM TBANGLUONGCHITIET " +
                "WHERE TBANGLUONGID = @Id " +
                "ORDER BY TIMECREATED, ID",
                "@Id", payrollId);
            var list = new List<PayrollDetailRecord>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                if (r["NGAY"] is DBNull)
                    continue;
                int rawSt = DbHelper.Int(r["TRANGTHAI"]);
                AttendanceStatus st = rawSt == 1 ? AttendanceStatus.Present
                    : rawSt == 2 ? AttendanceStatus.PaidLeave
                    : rawSt == 3 ? AttendanceStatus.UnpaidLeave
                    : AttendanceStatus.Empty;
                list.Add(new PayrollDetailRecord
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    EmployeeId = DbHelper.Str(r["DNHANVIENID"]) ?? "",
                    ShiftId = DbHelper.Str(r["DCALAMVIECID"]) ?? "",
                    Date = Convert.ToDateTime(r["NGAY"]).Date,
                    Status = st
                });
            }
            return list;
        }

        /// <summary>Thêm mới một dòng chi tiết chấm công (bảng TBANGLUONGCHITIET không có cột NAME).</summary>
        public static string InsertDetailIn(
            FbConnection conn,
            FbTransaction tx,
            string payrollId,
            string employeeId,
            string shiftId,
            DateTime date,
            AttendanceStatus status,
            string userId,
            DateTime now)
        {
            string id = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO TBANGLUONGCHITIET (" +
                "ID, TBANGLUONGID, DNHANVIENID, DCALAMVIECID, NGAY, TRANGTHAI, NOTE, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED" +
                ") VALUES (" +
                "@Id, @Bl, @Nv, @Ca, @Ngay, @Tr, NULL, @St, @Uc, @Tc, NULL, NULL)",
                "@Id", id,
                "@Bl", payrollId,
                "@Nv", employeeId,
                "@Ca", shiftId,
                "@Ngay", date.Date,
                "@Tr", (int)status,
                "@St", ActiveStatus,
                "@Uc", userId ?? "",
                "@Tc", now);
            return id;
        }

        public static int UpdateDetailStatusIn(
            FbConnection conn,
            FbTransaction tx,
            string detailId,
            AttendanceStatus status,
            string userId,
            DateTime now)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TBANGLUONGCHITIET SET TRANGTHAI = @Tr, USERMODIFIEDID = @Um, TIMEMODIFIED = @Tm WHERE ID = @Id",
                "@Tr", (int)status,
                "@Um", userId ?? "",
                "@Tm", now,
                "@Id", detailId);
        }

        public static int DeleteDetailByIdIn(FbConnection conn, FbTransaction tx, string detailId)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TBANGLUONGCHITIET WHERE ID = @Id", "@Id", detailId);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
