using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc dữ liệu Bảng lương (TBANGLUONG), Tổng hợp lương (TBANGLUONGTONGHOP), Chi tiết chấm công (TBANGLUONGCHITIET),
    /// nhân viên, ca làm việc, thưởng/phạt và tạm ứng lương. Mọi SQL tham số hóa 100%, không đổi cấu trúc CSDL.
    /// Phần ghi/xóa trong transaction đặt ở <c>PayrollRepository.Write.cs</c>.
    /// </summary>
    public sealed partial class PayrollRepository
    {
        public const int ActiveStatus = PayrollText.ActiveStatus;
        public const int DeletedStatus = PayrollText.DeletedStatus;

        private readonly string _databasePath;

        public PayrollRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Danh sách bảng lương, mới nhất trước (năm giảm, tháng giảm).</summary>
        public List<PayrollPeriodRow> LoadPeriods()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, COALESCE(THANG, 0) AS THANG, COALESCE(NAM, 0) AS NAM, " +
                    "NOTE, COALESCE(STATUS, 30) AS STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED " +
                    "FROM TBANGLUONG " +
                    "WHERE COALESCE(STATUS, 30) <> @Del " +
                    "ORDER BY NAM DESC, THANG DESC, TIMECREATED DESC",
                    "@Del", DeletedStatus);
                var list = new List<PayrollPeriodRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(PeriodFrom(r));
                return list;
            });
        }

        public static PayrollPeriodRow LoadPeriodByIdIn(FbConnection conn, FbTransaction tx, string payrollId)
        {
            if (string.IsNullOrEmpty(payrollId))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, COALESCE(THANG, 0) AS THANG, COALESCE(NAM, 0) AS NAM, " +
                "NOTE, COALESCE(STATUS, 30) AS STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED " +
                "FROM TBANGLUONG WHERE ID = @Id",
                "@Id", payrollId);
            return dt.Rows.Count == 0 ? null : PeriodFrom(dt.Rows[0]);
        }

        public static bool ExistsPeriodIn(FbConnection conn, FbTransaction tx, int month, int year)
        {
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TBANGLUONG WHERE THANG = @M AND NAM = @Y AND COALESCE(STATUS, 30) <> @Del",
                "@M", month, "@Y", year, "@Del", DeletedStatus));
            return n > 0;
        }

        /// <summary>Số phiếu chi lương (TTHUCHI.TBANGLUONGID = bảng lương, STATUS = 30) để chặn xóa.</summary>
        public static List<string> FindSalaryPayoutVoucherNumbersIn(FbConnection conn, FbTransaction tx, string payrollId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TTHUCHI WHERE TBANGLUONGID = @Id AND COALESCE(STATUS, 0) = @St ORDER BY NAME",
                "@Id", payrollId, "@St", ActiveStatus);
            var list = new List<string>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string name = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                list.Add(name.Length > 0 ? name : "(không số)");
            }
            return list;
        }

        /// <summary>Các ca làm việc đang dùng (DCALAMVIEC STATUS = 30), sắp theo tên.</summary>
        public List<PayrollShiftInfo> LoadActiveShifts()
        {
            return DbHelper.Read(_databasePath, conn => LoadActiveShiftsIn(conn, null));
        }

        public static List<PayrollShiftInfo> LoadActiveShiftsIn(FbConnection conn, FbTransaction tx)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME, COALESCE(TILELUONG, 0) AS TILELUONG " +
                "FROM DCALAMVIEC " +
                "WHERE STATUS = @St AND COALESCE(ITEMTYPE, 0) = 0 " +
                "ORDER BY NAME",
                "@St", ActiveStatus);
            var list = new List<PayrollShiftInfo>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PayrollShiftInfo
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    SalaryRate = DbHelper.Dec(r["TILELUONG"])
                });
            }
            return list;
        }

        /// <summary>
        /// Nạp bảng lương cho chế độ Tính lương: đọc snapshot TBANGLUONGTONGHOP cho NV đã lưu,
        /// đọc danh mục DNHANVIEN + thưởng/phạt/tạm ứng thực tế cho NV hoạt động chưa có tổng hợp, và chấm công TBANGLUONGCHITIET.
        /// </summary>
        public PayrollSheetData LoadSheetForPayroll(string payrollId)
        {
            if (string.IsNullOrEmpty(payrollId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                PayrollPeriodRow period = LoadPeriodByIdIn(conn, null, payrollId);
                if (period == null)
                    return null;

                var sheet = new PayrollSheetData
                {
                    Mode = PayrollMode.Payroll,
                    Period = period,
                    Shifts = LoadActiveShiftsIn(conn, null)
                };

                var byEmp = new Dictionary<string, PayrollEmployeeEntry>(StringComparer.Ordinal);

                // Nhánh 1: nhân viên đã có dòng TBANGLUONGTONGHOP (kể cả sau này đã khóa STATUS <> 30).
                DataTable dtSum = DbHelper.Table(conn, null,
                    "SELECT T.ID, T.DNHANVIENID, COALESCE(NV.CODE, '') AS EMPCODE, COALESCE(NV.NAME, '') AS EMPNAME, " +
                    "COALESCE(NV.STATUS, 0) AS EMPSTATUS, " +
                    "COALESCE(T.CACHTINHLUONG, 0) AS CACHTINHLUONG, " +
                    "COALESCE(T.LUONGCA, 0) AS LUONGCA, COALESCE(T.LUONGTHANG, 0) AS LUONGTHANG, " +
                    "COALESCE(NV.NGHICHUNHAT, 0) AS NGHICHUNHAT, COALESCE(NV.NGHITHU7, 0) AS NGHITHU7, " +
                    "COALESCE(T.TONGLUONG, 0) AS TONGLUONG, COALESCE(T.THUONG, 0) AS THUONG, " +
                    "COALESCE(T.PHAT, 0) AS PHAT, COALESCE(T.TAMUNG, 0) AS TAMUNG, COALESCE(T.THUCNHAN, 0) AS THUCNHAN " +
                    "FROM TBANGLUONGTONGHOP T " +
                    "INNER JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
                    "WHERE T.TBANGLUONGID = @Id " +
                    "ORDER BY NV.NAME, T.TIMECREATED, T.ID",
                    "@Id", payrollId);

                foreach (DataRow r in dtSum.Rows)
                {
                    string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                    if (empId.Length == 0 || byEmp.ContainsKey(empId))
                        continue;
                    int mode = DbHelper.Int(r["CACHTINHLUONG"]);
                    decimal lCa = DbHelper.Dec(r["LUONGCA"]);
                    decimal lTh = DbHelper.Dec(r["LUONGTHANG"]);
                    byEmp[empId] = new PayrollEmployeeEntry
                    {
                        EmployeeId = empId,
                        EmployeeCode = DbHelper.Str(r["EMPCODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                        IsActiveEmployee = DbHelper.Int(r["EMPSTATUS"]) == ActiveStatus,
                        SummaryId = DbHelper.Str(r["ID"]) ?? "",
                        SalaryMode = mode,
                        ShiftSalary = lCa,
                        MonthlySalary = lTh,
                        OffSunday = DbHelper.Int(r["NGHICHUNHAT"]) == PayrollText.FlagOn,
                        OffSaturday = DbHelper.Int(r["NGHITHU7"]) == PayrollText.FlagOn,
                        BaseSalary = mode == EmployeeSalaryMode.ByShift ? lCa : lTh,
                        TotalSalary = DbHelper.Dec(r["TONGLUONG"]),
                        Bonus = DbHelper.Dec(r["THUONG"]),
                        Penalty = DbHelper.Dec(r["PHAT"]),
                        Advance = DbHelper.Dec(r["TAMUNG"]),
                        NetSalary = DbHelper.Dec(r["THUCNHAN"])
                    };
                }

                // Nhánh 2: nhân viên hoạt động (STATUS = 30) chưa có dòng tổng hợp của bảng này.
                Dictionary<string, decimal> liveBonus;
                Dictionary<string, decimal> livePenalty;
                Dictionary<string, decimal> liveAdvance;
                LoadMonthAdjustmentsIn(conn, null, period.Year, period.Month, out liveBonus, out livePenalty, out liveAdvance);

                DataTable dtEmp = DbHelper.Table(conn, null,
                    "SELECT ID, COALESCE(CODE, '') AS CODE, COALESCE(NAME, '') AS NAME, " +
                    "COALESCE(CACHTINHLUONG, 30) AS CACHTINHLUONG, " +
                    "COALESCE(LUONGCA, 0) AS LUONGCA, COALESCE(LUONGTHANG, 0) AS LUONGTHANG, " +
                    "COALESCE(NGHICHUNHAT, 0) AS NGHICHUNHAT, COALESCE(NGHITHU7, 0) AS NGHITHU7 " +
                    "FROM DNHANVIEN " +
                    "WHERE STATUS = @St AND COALESCE(ITEMTYPE, 0) = 0 " +
                    "ORDER BY NAME",
                    "@St", ActiveStatus);

                foreach (DataRow r in dtEmp.Rows)
                {
                    string empId = DbHelper.Str(r["ID"]) ?? "";
                    if (empId.Length == 0 || byEmp.ContainsKey(empId))
                        continue;
                    int mode = DbHelper.Int(r["CACHTINHLUONG"]);
                    decimal lCa = DbHelper.Dec(r["LUONGCA"]);
                    decimal lTh = DbHelper.Dec(r["LUONGTHANG"]);
                    byEmp[empId] = new PayrollEmployeeEntry
                    {
                        EmployeeId = empId,
                        EmployeeCode = DbHelper.Str(r["CODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["NAME"]) ?? "",
                        IsActiveEmployee = true,
                        SummaryId = null,
                        SalaryMode = mode,
                        ShiftSalary = lCa,
                        MonthlySalary = lTh,
                        OffSunday = DbHelper.Int(r["NGHICHUNHAT"]) == PayrollText.FlagOn,
                        OffSaturday = DbHelper.Int(r["NGHITHU7"]) == PayrollText.FlagOn,
                        BaseSalary = mode == EmployeeSalaryMode.ByShift ? lCa : lTh,
                        Bonus = LookupDec(liveBonus, empId),
                        Penalty = LookupDec(livePenalty, empId),
                        Advance = LookupDec(liveAdvance, empId)
                    };
                }

                AttachAttendanceDetailsIn(conn, null, payrollId, byEmp, sheet);
                sheet.Employees = SortEmployees(byEmp.Values);
                return sheet;
            });
        }

        /// <summary>
        /// Nạp bảng chấm công cho chế độ Chấm công: TUYỆT ĐỐI KHÔNG đọc bất kỳ cột tiền nào từ DB
        /// (không đọc LUONGCA, LUONGTHANG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN).
        /// </summary>
        public PayrollSheetData LoadSheetForAttendance(string payrollId)
        {
            if (string.IsNullOrEmpty(payrollId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                PayrollPeriodRow period = LoadPeriodByIdIn(conn, null, payrollId);
                if (period == null)
                    return null;

                var sheet = new PayrollSheetData
                {
                    Mode = PayrollMode.Attendance,
                    Period = period,
                    Shifts = LoadActiveShiftsIn(conn, null)
                };

                var byEmp = new Dictionary<string, PayrollEmployeeEntry>(StringComparer.Ordinal);

                // Nhân viên đã có dòng tổng hợp trong bảng lương này (chỉ đọc ID/CODE/NAME/STATUS, không đọc tiền).
                DataTable dtSaved = DbHelper.Table(conn, null,
                    "SELECT DISTINCT NV.ID, COALESCE(NV.CODE, '') AS CODE, COALESCE(NV.NAME, '') AS NAME, " +
                    "COALESCE(NV.STATUS, 0) AS STATUS " +
                    "FROM TBANGLUONGTONGHOP T " +
                    "INNER JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
                    "WHERE T.TBANGLUONGID = @Id " +
                    "ORDER BY NV.NAME",
                    "@Id", payrollId);

                foreach (DataRow r in dtSaved.Rows)
                {
                    string empId = DbHelper.Str(r["ID"]) ?? "";
                    if (empId.Length == 0 || byEmp.ContainsKey(empId))
                        continue;
                    byEmp[empId] = new PayrollEmployeeEntry
                    {
                        EmployeeId = empId,
                        EmployeeCode = DbHelper.Str(r["CODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["NAME"]) ?? "",
                        IsActiveEmployee = DbHelper.Int(r["STATUS"]) == ActiveStatus
                    };
                }

                // Cộng nhân viên đang hoạt động (STATUS = 30).
                DataTable dtActive = DbHelper.Table(conn, null,
                    "SELECT ID, COALESCE(CODE, '') AS CODE, COALESCE(NAME, '') AS NAME " +
                    "FROM DNHANVIEN " +
                    "WHERE STATUS = @St AND COALESCE(ITEMTYPE, 0) = 0 " +
                    "ORDER BY NAME",
                    "@St", ActiveStatus);

                foreach (DataRow r in dtActive.Rows)
                {
                    string empId = DbHelper.Str(r["ID"]) ?? "";
                    if (empId.Length == 0 || byEmp.ContainsKey(empId))
                        continue;
                    byEmp[empId] = new PayrollEmployeeEntry
                    {
                        EmployeeId = empId,
                        EmployeeCode = DbHelper.Str(r["CODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["NAME"]) ?? "",
                        IsActiveEmployee = true
                    };
                }

                AttachAttendanceDetailsIn(conn, null, payrollId, byEmp, sheet);
                sheet.Employees = SortEmployees(byEmp.Values);
                return sheet;
            });
        }

        /// <summary>
        /// Đọc lại thông số lương hiện tại từ DNHANVIEN và Thưởng/Phạt/Tạm ứng của tháng từ DB
        /// (dùng cho nút Tải lại toàn bảng hoặc Tải lại một nhân viên ở chế độ Tính lương).
        /// </summary>
        public Dictionary<string, PayrollLiveSnapshot> LoadLiveSnapshots(int year, int month, string onlyEmployeeId = null)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                Dictionary<string, decimal> bonus;
                Dictionary<string, decimal> penalty;
                Dictionary<string, decimal> advance;
                LoadMonthAdjustmentsIn(conn, null, year, month, out bonus, out penalty, out advance);

                DataTable dtEmp = string.IsNullOrEmpty(onlyEmployeeId)
                    ? DbHelper.Table(conn, null,
                        "SELECT ID, COALESCE(CODE, '') AS CODE, COALESCE(NAME, '') AS NAME, " +
                        "COALESCE(STATUS, 0) AS STATUS, COALESCE(ITEMTYPE, 0) AS ITEMTYPE, " +
                        "COALESCE(CACHTINHLUONG, 30) AS CACHTINHLUONG, " +
                        "COALESCE(LUONGCA, 0) AS LUONGCA, COALESCE(LUONGTHANG, 0) AS LUONGTHANG, " +
                        "COALESCE(NGHICHUNHAT, 0) AS NGHICHUNHAT, COALESCE(NGHITHU7, 0) AS NGHITHU7 " +
                        "FROM DNHANVIEN WHERE (ITEMTYPE IS NULL OR ITEMTYPE = 0) ORDER BY NAME")
                    : DbHelper.Table(conn, null,
                        "SELECT ID, COALESCE(CODE, '') AS CODE, COALESCE(NAME, '') AS NAME, " +
                        "COALESCE(STATUS, 0) AS STATUS, COALESCE(ITEMTYPE, 0) AS ITEMTYPE, " +
                        "COALESCE(CACHTINHLUONG, 30) AS CACHTINHLUONG, " +
                        "COALESCE(LUONGCA, 0) AS LUONGCA, COALESCE(LUONGTHANG, 0) AS LUONGTHANG, " +
                        "COALESCE(NGHICHUNHAT, 0) AS NGHICHUNHAT, COALESCE(NGHITHU7, 0) AS NGHITHU7 " +
                        "FROM DNHANVIEN WHERE ID = @Id",
                        "@Id", onlyEmployeeId);

                var map = new Dictionary<string, PayrollLiveSnapshot>(StringComparer.Ordinal);
                foreach (DataRow r in dtEmp.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    if (id.Length == 0)
                        continue;
                    int status = DbHelper.Int(r["STATUS"]);
                    int itemType = DbHelper.Int(r["ITEMTYPE"]);
                    map[id] = new PayrollLiveSnapshot
                    {
                        EmployeeId = id,
                        EmployeeCode = DbHelper.Str(r["CODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["NAME"]) ?? "",
                        IsActiveEmployee = status == ActiveStatus && itemType == 0,
                        SalaryMode = DbHelper.Int(r["CACHTINHLUONG"]),
                        ShiftSalary = DbHelper.Dec(r["LUONGCA"]),
                        MonthlySalary = DbHelper.Dec(r["LUONGTHANG"]),
                        OffSunday = DbHelper.Int(r["NGHICHUNHAT"]) == PayrollText.FlagOn,
                        OffSaturday = DbHelper.Int(r["NGHITHU7"]) == PayrollText.FlagOn,
                        Bonus = LookupDec(bonus, id),
                        Penalty = LookupDec(penalty, id),
                        Advance = LookupDec(advance, id)
                    };
                }
                return map;
            });
        }

        private static void LoadMonthAdjustmentsIn(
            FbConnection conn,
            FbTransaction tx,
            int year,
            int month,
            out Dictionary<string, decimal> bonus,
            out Dictionary<string, decimal> penalty,
            out Dictionary<string, decimal> advance)
        {
            bonus = new Dictionary<string, decimal>(StringComparer.Ordinal);
            penalty = new Dictionary<string, decimal>(StringComparer.Ordinal);
            advance = new Dictionary<string, decimal>(StringComparer.Ordinal);

            if (year < 1 || month < 1 || month > 12)
                return;

            DateTime from = new DateTime(year, month, 1);
            DateTime toExcl = from.AddMonths(1);

            // TTHUONGPHAT: STATUS = 30, NGAY trong tháng (so theo ngày từ mùng 1 đến hết ngày cuối tháng).
            DataTable dtTp = DbHelper.Table(conn, tx,
                "SELECT DNHANVIENID, COALESCE(SUM(THUONG), 0) AS THUONG, COALESCE(SUM(PHAT), 0) AS PHAT " +
                "FROM TTHUONGPHAT " +
                "WHERE COALESCE(STATUS, 0) = @St " +
                "AND NGAY >= @From AND NGAY < @ToExcl " +
                "AND DNHANVIENID IS NOT NULL " +
                "GROUP BY DNHANVIENID",
                "@St", ActiveStatus, "@From", from, "@ToExcl", toExcl);

            foreach (DataRow r in dtTp.Rows)
            {
                string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                if (empId.Length == 0)
                    continue;
                bonus[empId] = DbHelper.Dec(r["THUONG"]);
                penalty[empId] = DbHelper.Dec(r["PHAT"]);
            }

            // TTHUCHI tạm ứng: LOAI = 1 (chi), LATAMUNG = 30, STATUS = 30, NGAY trong tháng; chỉ SUM(CHI), không trừ THU.
            DataTable dtTu = DbHelper.Table(conn, tx,
                "SELECT DNHANVIENID, COALESCE(SUM(CHI), 0) AS TAMUNG " +
                "FROM TTHUCHI " +
                "WHERE COALESCE(LOAI, 0) = 1 AND COALESCE(LATAMUNG, 0) = 30 " +
                "AND COALESCE(STATUS, 0) = @St " +
                "AND NGAY >= @From AND NGAY < @ToExcl " +
                "AND DNHANVIENID IS NOT NULL " +
                "GROUP BY DNHANVIENID",
                "@St", ActiveStatus, "@From", from, "@ToExcl", toExcl);

            foreach (DataRow r in dtTu.Rows)
            {
                string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                if (empId.Length == 0)
                    continue;
                advance[empId] = DbHelper.Dec(r["TAMUNG"]);
            }
        }

        private static void AttachAttendanceDetailsIn(
            FbConnection conn,
            FbTransaction tx,
            string payrollId,
            Dictionary<string, PayrollEmployeeEntry> byEmp,
            PayrollSheetData sheet)
        {
            List<PayrollDetailRecord> details = LoadDetailRowsIn(conn, tx, payrollId);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            int dupCount = 0;

            foreach (PayrollDetailRecord d in details)
            {
                string fullKey = (d.EmployeeId ?? "") + "|" + PayrollEmployeeEntry.CellKey(d.ShiftId, d.Date.Date);
                if (!seenKeys.Add(fullKey))
                {
                    dupCount++;
                    continue;
                }
                PayrollEmployeeEntry emp;
                if (byEmp.TryGetValue(d.EmployeeId ?? "", out emp))
                    emp.SetStatus(d.ShiftId, d.Date.Date, d.Status);
            }
            sheet.DuplicateDetailCount = dupCount;
        }

        private static List<PayrollEmployeeEntry> SortEmployees(IEnumerable<PayrollEmployeeEntry> source)
        {
            var list = new List<PayrollEmployeeEntry>(source);
            list.Sort(delegate(PayrollEmployeeEntry a, PayrollEmployeeEntry b)
            {
                int cmp = StringComparer.CurrentCultureIgnoreCase.Compare(a.EmployeeName ?? "", b.EmployeeName ?? "");
                if (cmp != 0)
                    return cmp;
                return string.CompareOrdinal(a.EmployeeId ?? "", b.EmployeeId ?? "");
            });
            return list;
        }

        private static decimal LookupDec(Dictionary<string, decimal> map, string key)
        {
            decimal v;
            return map != null && map.TryGetValue(key, out v) ? v : 0m;
        }

        private static PayrollPeriodRow PeriodFrom(DataRow r)
        {
            return new PayrollPeriodRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Month = DbHelper.Int(r["THANG"]),
                Year = DbHelper.Int(r["NAM"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"])
            };
        }
    }
}
