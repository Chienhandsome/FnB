using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PayrollTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tl",
                    UserName = "selftest_tl",
                    DisplayName = "Selftest TinhLuong",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                PayrollSchemaInspectTest(db);
                PayrollFormulaPureTests();
                PayrollServiceAndRepoTests(db);
                PayrollUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void PayrollSchemaInspectTest(string db)
        {
            var requiredByTable = new Dictionary<string, string[]>
            {
                { "TBANGLUONG", new[] { "ID", "NAME", "THANG", "NAM", "NOTE", "STATUS", "USERCREATEDID", "TIMECREATED" } },
                { "TBANGLUONGTONGHOP", new[] { "ID", "TBANGLUONGID", "DNHANVIENID", "LUONGCA", "LUONGTHANG", "CACHTINHLUONG", "TONGLUONG", "PHAT", "THUONG", "TAMUNG", "THUCNHAN", "STATUS", "USERCREATEDID" } },
                { "TBANGLUONGCHITIET", new[] { "ID", "TBANGLUONGID", "DNHANVIENID", "DCALAMVIECID", "NGAY", "TRANGTHAI", "STATUS", "USERCREATEDID" } }
            };

            var missing = new List<string>();
            string scaleInfo = "";
            DbHelper.Read(db, conn =>
            {
                foreach (KeyValuePair<string, string[]> kv in requiredByTable)
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT TRIM(rf.RDB$FIELD_NAME) AS COL, f.RDB$FIELD_TYPE AS FTYPE, COALESCE(f.RDB$FIELD_SCALE, 0) AS FSCALE " +
                        "FROM RDB$RELATION_FIELDS rf " +
                        "JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE " +
                        "WHERE rf.RDB$RELATION_NAME = @T",
                        "@T", kv.Key);
                    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (DataRow r in dt.Rows)
                        cols.Add(DbHelper.Str(r["COL"]) ?? "");
                    foreach (string req in kv.Value)
                    {
                        if (!cols.Contains(req))
                            missing.Add(kv.Key + "." + req);
                    }
                }

                Dictionary<string, int> scales = PayrollRepository.ReadNumericScalesIn(conn, null, PayrollText.SummaryTableName);
                int sTong, sThuc;
                scales.TryGetValue("TONGLUONG", out sTong);
                scales.TryGetValue("THUCNHAN", out sThuc);
                scaleInfo = "scales(TONGLUONG=" + sTong + ",THUCNHAN=" + sThuc + ")";
                return 0;
            });

            Check("Tính lương P3a §1 (kiểm cột thật qua RDB$RELATION_FIELDS/RDB$FIELDS): đủ cột cho 3 bảng lương và đọc được scale tiền",
                missing.Count == 0,
                missing.Count == 0 ? scaleInfo : "thiếu: " + SingleLine(string.Join(",", missing.ToArray())));
        }

        private static void PayrollFormulaPureTests()
        {
            var shifts = new List<PayrollShiftInfo>
            {
                new PayrollShiftInfo { Id = "CA_SANG", Name = "Sáng", SalaryRate = 100m },
                new PayrollShiftInfo { Id = "CA_TOI", Name = "Tối", SalaryRate = 50m },
                new PayrollShiftInfo { Id = "CA_ZERO", Name = "Ca 0%", SalaryRate = 0m },
                new PayrollShiftInfo { Id = "CA_NEG", Name = "Ca âm", SalaryRate = -50m }
            };

            // §6 #2: Theo ca (30): NV A LUONGCA=100.000; 10 ngày Sáng Đi làm, 4 ngày Tối Đi làm,
            // 2 ngày Sáng Nghỉ có phép, 1 ngày Sáng Nghỉ không phép -> soCa = 10*1 + 4*0.5 + 2*1 + 0 = 14, TONGLUONG = 1.400.000.
            var empA = new PayrollEmployeeEntry
            {
                EmployeeId = "A",
                EmployeeName = "NV A",
                SalaryMode = EmployeeSalaryMode.ByShift,
                ShiftSalary = 100000m
            };
            for (int d = 1; d <= 10; d++)
                empA.SetStatus("CA_SANG", new DateTime(2004, 3, d), AttendanceStatus.Present);
            for (int d = 1; d <= 4; d++)
                empA.SetStatus("CA_TOI", new DateTime(2004, 3, d), AttendanceStatus.Present);
            empA.SetStatus("CA_SANG", new DateTime(2004, 3, 11), AttendanceStatus.PaidLeave);
            empA.SetStatus("CA_SANG", new DateTime(2004, 3, 12), AttendanceStatus.PaidLeave);
            empA.SetStatus("CA_SANG", new DateTime(2004, 3, 13), AttendanceStatus.UnpaidLeave);

            PayrollCalculationResult resA = PayrollCalculator.CalculateEmployee(2004, 3, empA, shifts);
            bool rateZeroAndNegOk = PayrollCalculator.EffectiveShiftRate(0m) == 1m
                && PayrollCalculator.EffectiveShiftRate(-50m) == 0m
                && PayrollCalculator.EffectiveShiftRate(50m) == 0.5m;
            Check("Tính lương P3a §6 #2 (công thức theo ca 30 + T5 TILELUONG 0/âm): soCa=14, soNgay=12, TONGLUONG=1.400.000, nghỉ không phép=0",
                resA.TotalShifts == 14m && resA.TotalDays == 12 && resA.BaseSalary == 100000m
                && resA.TotalSalary == 1400000m && resA.NetSalary == 1400000m && rateZeroAndNegOk,
                "ca=" + resA.TotalShifts + ", ngay=" + resA.TotalDays + ", tong=" + resA.TotalSalary + ", t5=" + rateZeroAndNegOk);

            // §6 #3: Theo tháng (0), tháng 3/2004 (01/03/2004 Thứ Hai, 31 ngày, 4 CN, 4 T7): NV B LUONGTHANG=3.100.000, 20 ngày chấm công.
            int sunMar2004 = PayrollCalculator.CountDayOfWeek(2004, 3, DayOfWeek.Sunday);
            int satMar2004 = PayrollCalculator.CountDayOfWeek(2004, 3, DayOfWeek.Saturday);

            Func<bool, bool, PayrollCalculationResult> calcB = (offSun, offSat) =>
            {
                var empB = new PayrollEmployeeEntry
                {
                    EmployeeId = "B",
                    EmployeeName = "NV B",
                    SalaryMode = EmployeeSalaryMode.ByMonth,
                    MonthlySalary = 3100000m,
                    OffSunday = offSun,
                    OffSaturday = offSat
                };
                for (int d = 1; d <= 20; d++)
                    empB.SetStatus("CA_SANG", new DateTime(2004, 3, d), AttendanceStatus.Present);
                return PayrollCalculator.CalculateEmployee(2004, 3, empB, shifts);
            };

            PayrollCalculationResult bNone = calcB(false, false);
            PayrollCalculationResult bSun = calcB(true, false);
            PayrollCalculationResult bSat = calcB(false, true);
            PayrollCalculationResult bBoth = calcB(true, true);

            // Số tính tay tuyệt đối (không lấy từ hàm đang test):
            // 20 * 3.100.000 / 31 = 2.000.000
            // 20 * 3.100.000 / 27 = 62.000.000 / 27 (làm tròn hiển thị = 2.296.296)
            // 20 * 3.100.000 / 23 = 62.000.000 / 23 (làm tròn hiển thị = 2.695.652)
            decimal expectedDiv27 = 62000000m / 27m;
            decimal expectedDiv23 = 62000000m / 23m;

            Check("Tính lương P3a §6 #3 (theo tháng 0, 3/2004 có 31 ngày / 4 CN / 4 T7): mẫu 31->2.000.000, mẫu 27(CN/T7)->2.296.296, mẫu 23(cả hai)->2.695.652",
                sunMar2004 == 4 && satMar2004 == 4
                && bNone.WorkingDaysInMonth == 31 && bNone.TotalSalary == 2000000m
                && bSun.WorkingDaysInMonth == 27 && bSun.TotalSalary == expectedDiv27 && PayrollCalculator.RoundForDisplay(bSun.TotalSalary) == 2296296m
                && bSat.WorkingDaysInMonth == 27 && bSat.TotalSalary == expectedDiv27 && PayrollCalculator.RoundForDisplay(bSat.TotalSalary) == 2296296m
                && bBoth.WorkingDaysInMonth == 23 && bBoth.TotalSalary == expectedDiv23 && PayrollCalculator.RoundForDisplay(bBoth.TotalSalary) == 2695652m,
                "cn=" + sunMar2004 + ", t7=" + satMar2004 + ", n31=" + bNone.TotalSalary + ", n27=" + PayrollCalculator.RoundForDisplay(bSun.TotalSalary) + ", n23=" + PayrollCalculator.RoundForDisplay(bBoth.TotalSalary));

            // §6 #4: Tháng theo ca (60): NV C LUONGTHANG=3.100.000, 10 ca trong 3/2004 -> 10 * 3.100.000 / 31 = 1.000.000.
            var empC = new PayrollEmployeeEntry
            {
                EmployeeId = "C",
                EmployeeName = "NV C",
                SalaryMode = EmployeeSalaryMode.MonthByShift,
                MonthlySalary = 3100000m
            };
            for (int d = 1; d <= 10; d++)
                empC.SetStatus("CA_SANG", new DateTime(2004, 3, d), AttendanceStatus.Present);
            PayrollCalculationResult resC = PayrollCalculator.CalculateEmployee(2004, 3, empC, shifts);

            Check("Tính lương P3a §6 #4 (tháng theo ca 60, 3/2004): 10 ca * 3.100.000 / 31 = 1.000.000",
                resC.TotalShifts == 10m && resC.BaseSalary == 3100000m && resC.TotalSalary == 1000000m,
                "ca=" + resC.TotalShifts + ", tong=" + resC.TotalSalary);

            // §6 #5 thuần: Thưởng 200.000, Phạt 50.000, Tạm ứng 500.000 -> THUCNHAN = 1.400.000 + 200.000 - 50.000 - 500.000 = 1.050.000; và trường hợp thực nhận âm.
            empA.Bonus = 200000m;
            empA.Penalty = 50000m;
            empA.Advance = 500000m;
            PayrollCalculationResult resAWithAdj = PayrollCalculator.CalculateEmployee(2004, 3, empA, shifts);
            empA.Advance = 2000000m;
            PayrollCalculationResult resANeg = PayrollCalculator.CalculateEmployee(2004, 3, empA, shifts);

            Check("Tính lương P3a §6 #5 (luật thuần thưởng/phạt/tạm ứng & thực nhận âm): 1.400.000+200k-50k-500k=1.050.000 và thực nhận âm=-450.000",
                resAWithAdj.NetSalary == 1050000m && resANeg.NetSalary == -450000m,
                "net1=" + resAWithAdj.NetSalary + ", netNeg=" + resANeg.NetSalary);
        }

        private static void PayrollServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string shiftSangId = DbHelper.NewId();
            string shiftToiId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string empCId = DbHelper.NewId();
            string empEmptyId = DbHelper.NewId();
            string empNewId = DbHelper.NewId();
            string tpInMonthId = DbHelper.NewId();
            string tpOtherMonthId = DbHelper.NewId();
            string advExpId = DbHelper.NewId();
            string advIncId = DbHelper.NewId();
            string payoutVoucherId = DbHelper.NewId();

            var createdPeriodIds = new List<string>();
            var createdVoucherIds = new List<string>();
            var createdTpIds = new List<string>();
            var createdEmpIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tl')",
                    "@I", storeId, "@N", "CH TL " + tag);
                BookingExec(db, "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 100, 30, 'selftest_tl', @T)",
                    "@I", shiftSangId, "@N", "Ca Sáng TL " + tag, "@T", now);
                BookingExec(db, "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 50, 30, 'selftest_tl', @T)",
                    "@I", shiftToiId, "@N", "Ca Tối TL " + tag, "@T", now);

                Action<string, string, int, decimal, decimal, int, int> seedEmp = (id, name, mode, lCa, lTh, cn, t7) =>
                {
                    createdEmpIds.Add(id);
                    BookingExec(db,
                        "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @M, @CA, @TH, @CN, @T7, 30, 'selftest_tl', @T)",
                        "@I", id, "@N", name, "@M", mode, "@CA", lCa, "@TH", lTh, "@CN", cn, "@T7", t7, "@T", now);
                };
                seedEmp(empAId, "NV A TL " + tag, EmployeeSalaryMode.ByShift, 100000m, 0m, 0, 0);
                seedEmp(empBId, "NV B TL " + tag, EmployeeSalaryMode.ByMonth, 0m, 3100000m, 0, 0);
                seedEmp(empCId, "NV C TL " + tag, EmployeeSalaryMode.MonthByShift, 0m, 3100000m, 0, 0);
                seedEmp(empEmptyId, "NV Trống TL " + tag, EmployeeSalaryMode.ByShift, 100000m, 0m, 0, 0);

                // Thưởng 200.000, Phạt 50.000 trong tháng 3/2004; Phạt 40.000 ở tháng 4/2004 (không tính vào 3/2004).
                BookingExec(db,
                    "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @E, 200000, 50000, 30, 'selftest_tl', @T)",
                    "@I", tpInMonthId, "@N", "TP-IN-" + tag, "@D", new DateTime(2004, 3, 15), "@E", empAId, "@T", now);
                createdTpIds.Add(tpInMonthId);
                BookingExec(db,
                    "INSERT INTO TTHUONGPHAT (ID, NAME, NGAY, DNHANVIENID, THUONG, PHAT, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @E, 0, 40000, 30, 'selftest_tl', @T)",
                    "@I", tpOtherMonthId, "@N", "TP-OUT-" + tag, "@D", new DateTime(2004, 4, 2), "@E", empAId, "@T", now);
                createdTpIds.Add(tpOtherMonthId);

                // Tạm ứng chi 500.000 (LOAI=1, LATAMUNG=30) và phiếu thu 100.000 (LOAI=0, LATAMUNG=30 -> KHÔNG trừ vào TAMUNG).
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, DCUAHANGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 1, 30, @E, @S, 0, 500000, 0, 30, 'selftest_tl', @T)",
                    "@I", advExpId, "@N", "PC-TU-" + tag, "@D", new DateTime(2004, 3, 20), "@E", empAId, "@S", storeId, "@T", now);
                createdVoucherIds.Add(advExpId);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, DCUAHANGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 1, 30, @E, @S, 100000, 0, 0, 30, 'selftest_tl', @T)",
                    "@I", advIncId, "@N", "PT-TU-" + tag, "@D", new DateTime(2004, 3, 21), "@E", empAId, "@S", storeId, "@T", now);
                createdVoucherIds.Add(advIncId);

                var payrollSvc = new PayrollService(AppSession.Current, PayrollMode.Payroll);

                // Xóa sẵn kỳ 3/2004, 4/2004, 1/2005 nếu có từ dữ liệu lạ để test sạch (chỉ nếu trống).
                // §6 #1: Thêm Tháng 3/2004, chặn trùng, chặn tháng 13 / năm 3000, sắp mới nhất trước.
                PayrollCreateResult cMar = payrollSvc.CreatePeriod(3, 2004, "Kỳ test " + tag);
                if (cMar.Ok) createdPeriodIds.Add(cMar.Created.Id);
                PayrollCreateResult cDup = payrollSvc.CreatePeriod(3, 2004);
                PayrollCreateResult cBadMonth = payrollSvc.CreatePeriod(13, 2004);
                PayrollCreateResult cBadMonth0 = payrollSvc.CreatePeriod(0, 2004);
                PayrollCreateResult cBadYear = payrollSvc.CreatePeriod(3, 3000);
                PayrollCreateResult cBadYearNeg = payrollSvc.CreatePeriod(3, -1);
                PayrollCreateResult cApr = payrollSvc.CreatePeriod(4, 2004);
                if (cApr.Ok) createdPeriodIds.Add(cApr.Created.Id);
                PayrollCreateResult cJan2005 = payrollSvc.CreatePeriod(1, 2005);
                if (cJan2005.Ok) createdPeriodIds.Add(cJan2005.Created.Id);

                List<PayrollPeriodRow> allPeriods = payrollSvc.LoadPeriods();
                int idx2005 = allPeriods.FindIndex(p => cJan2005.Ok && p.Id == cJan2005.Created.Id);
                int idxApr = allPeriods.FindIndex(p => cApr.Ok && p.Id == cApr.Created.Id);
                int idxMar = allPeriods.FindIndex(p => cMar.Ok && p.Id == cMar.Created.Id);
                bool sortedNewestFirst = idx2005 >= 0 && idxApr > idx2005 && idxMar > idxApr;

                Check("Tính lương P3a §6 #1 (thêm kỳ 3/2004, chặn trùng & tháng 13/năm 3000, sắp mới trước): đạt",
                    cMar.Ok && cMar.Created.Month == 3 && cMar.Created.Year == 2004 && cMar.Created.Name == "Tháng 3/2004"
                    && !cDup.Ok && cDup.ErrorMessage == PayrollText.PeriodDuplicate(3, 2004)
                    && !cBadMonth.Ok && !cBadMonth0.Ok && !cBadYear.Ok && !cBadYearNeg.Ok
                    && sortedNewestFirst,
                    "mar=" + cMar.Ok + ", dup=" + SingleLine(cDup.ErrorMessage) + ", ord=" + idx2005 + "/" + idxApr + "/" + idxMar);

                if (!cMar.Ok)
                    return;
                string periodId = cMar.Created.Id;

                // §6 #5 (DB) + #6 (lưu): nạp kỳ 3/2004, chấm công NV A (theo ca), NV B (theo tháng), NV C (tháng theo ca); giữ NV Trống không chấm công/không tiền.
                PayrollSheetData sheet = payrollSvc.LoadSheet(periodId);
                PayrollEmployeeEntry rowA = sheet.Employees.Find(e => e.EmployeeId == empAId);
                PayrollEmployeeEntry rowB = sheet.Employees.Find(e => e.EmployeeId == empBId);
                PayrollEmployeeEntry rowC = sheet.Employees.Find(e => e.EmployeeId == empCId);

                bool adjLoadedOk = rowA != null && rowA.Bonus == 200000m && rowA.Penalty == 50000m && rowA.Advance == 500000m;
                Check("Tính lương P3a §6 #5 (DB thưởng/phạt/tạm ứng theo tháng): thưởng 200k, phạt 50k (bỏ 40k tháng khác), tạm ứng 500k (bỏ phiếu thu 100k)",
                    adjLoadedOk,
                    rowA == null ? "null" : ("th=" + rowA.Bonus + ", ph=" + rowA.Penalty + ", tu=" + rowA.Advance));

                for (int d = 1; d <= 10; d++)
                    rowA.SetStatus(shiftSangId, new DateTime(2004, 3, d), AttendanceStatus.Present);
                for (int d = 1; d <= 4; d++)
                    rowA.SetStatus(shiftToiId, new DateTime(2004, 3, d), AttendanceStatus.Present);
                rowA.SetStatus(shiftSangId, new DateTime(2004, 3, 11), AttendanceStatus.PaidLeave);
                rowA.SetStatus(shiftSangId, new DateTime(2004, 3, 12), AttendanceStatus.PaidLeave);
                rowA.SetStatus(shiftSangId, new DateTime(2004, 3, 13), AttendanceStatus.UnpaidLeave);

                for (int d = 1; d <= 20; d++)
                    rowB.SetStatus(shiftSangId, new DateTime(2004, 3, d), AttendanceStatus.Present);
                for (int d = 1; d <= 10; d++)
                    rowC.SetStatus(shiftSangId, new DateTime(2004, 3, d), AttendanceStatus.Present);

                // Lưu lần 1 (17 ô NV A + 20 ô NV B + 10 ô NV C = 47 ô chi tiết; 3 dòng tổng hợp; NV Trống KHÔNG có dòng tổng hợp).
                PayrollSaveResult save1 = payrollSvc.SaveSheet(sheet);
                int sumCount1 = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID IN (@A, @B, @C, @Z)",
                    "@I", periodId, "@A", empAId, "@B", empBId, "@C", empCId, "@Z", empEmptyId);
                int emptyEmpSum = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @Z",
                    "@I", periodId, "@Z", empEmptyId);
                int detCount1 = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                decimal dbNetA = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT THUCNHAN FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));

                // Lưu lần 2 -> không nhân đôi dòng; xóa 1 ô (ngày 13 NV A) -> dòng chi tiết đó bị xóa khỏi DB.
                PayrollSaveResult save2 = payrollSvc.SaveSheet(sheet);
                int sumCount2 = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID IN (@A, @B, @C, @Z)",
                    "@I", periodId, "@A", empAId, "@B", empBId, "@C", empCId, "@Z", empEmptyId);
                int detCount2 = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);

                rowA.SetStatus(shiftSangId, new DateTime(2004, 3, 13), AttendanceStatus.Empty);
                // Chèn 1 dòng trùng khóa (NV A, Sáng, 01/03/2004) vào DB -> lưu phải dọn dòng trùng.
                string dupDetId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO TBANGLUONGCHITIET (ID, TBANGLUONGID, DNHANVIENID, DCALAMVIECID, NGAY, TRANGTHAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @B, @E, @C, @D, 2, 30, 'selftest_tl', @T)",
                    "@I", dupDetId, "@B", periodId, "@E", empAId, "@C", shiftSangId, "@D", new DateTime(2004, 3, 1), "@T", now.AddMinutes(5));
                PayrollSaveResult save3 = payrollSvc.SaveSheet(sheet);
                int detCount3 = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);

                Check("Tính lương P3a §6 #6 (lưu 1 transaction, không tạo tổng hợp cho NV trống, lưu lần 2 không nhân đôi, ô trống xóa chi tiết, dọn dòng trùng khóa): đạt",
                    save1.Ok && sumCount1 == 3 && emptyEmpSum == 0 && detCount1 == 47 && dbNetA == 1050000m
                    && save2.Ok && sumCount2 == 3 && detCount2 == 47
                    && save3.Ok && detCount3 == 46 && save3.DeletedDetailCount == 1 && save3.RemovedDuplicateDetailCount == 1,
                    "s1=" + sumCount1 + "/" + detCount1 + "(empty=" + emptyEmpSum + ",netA=" + dbNetA + "), s2=" + sumCount2 + "/" + detCount2 + ", s3=" + detCount3 + "(del=" + save3.DeletedDetailCount + ",dup=" + save3.RemovedDuplicateDetailCount + ")");

                // §6 #7 (DB & quyền): user chỉ có Chấm công không đọc/ghi được ở chế độ Tính lương; lưu ở chế độ Chấm công KHÔNG chạm TBANGLUONGTONGHOP.
                var attPerms = new FunctionPermissions(false);
                attPerms.Add(PayrollText.AttendanceFunctionId, PayrollText.AttendanceFunctionName, RoleModes.View | RoleModes.Edit);
                var attSess = new UserSession
                {
                    UserId = "u_cc_only", UserName = "u_cc_only", DisplayName = "Chỉ chấm công",
                    IsAdmin = false, DatabasePath = db, Permissions = attPerms
                };
                var deniedPayrollSvc = new PayrollService(attSess, PayrollMode.Payroll);
                var attSvc = new PayrollService(attSess, PayrollMode.Attendance);

                bool blockedMoneyRead = !deniedPayrollSvc.CanView && deniedPayrollSvc.LoadSheet(periodId) == null;
                PayrollSaveResult blockedMoneyWrite = deniedPayrollSvc.SaveSheet(sheet);
                PayrollSheetData attSheet = attSvc.LoadSheet(periodId);
                PayrollEmployeeEntry attRowA = attSheet == null ? null : attSheet.Employees.Find(e => e.EmployeeId == empAId);
                // Chiến chốt 28/11: Chấm công nạp tiền đã lưu như chế độ Tính lương (lương ca 100.000, tổng 1.400.000, thực nhận 1.050.000).
                bool attLoadsMoney = attRowA != null && attRowA.BaseSalary == 100000m && attRowA.TotalSalary == 1400000m && attRowA.NetSalary == 1050000m;

                // Đổi chấm công ngày 15 của NV A ở chế độ Chấm công -> DB chi tiết tăng 1 dòng và TBANGLUONGTONGHOP của NV A cập nhật như Tính lương.
                int sumBeforeAtt = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                decimal tongBeforeAtt = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT TONGLUONG FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));
                if (attRowA != null)
                    attRowA.SetStatus(shiftSangId, new DateTime(2004, 3, 15), AttendanceStatus.Present);
                PayrollSaveResult attSave = attSvc.SaveSheet(attSheet);
                int sumAfterAtt = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                decimal tongAfterAtt = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT TONGLUONG FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));
                int detAfterAtt = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                decimal netAfterAtt = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT THUCNHAN FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I AND DNHANVIENID = @E", "@I", periodId, "@E", empAId)));

                // 15 ca x 100.000 = 1.500.000; thực nhận = 1.500.000 + 200.000 - 50.000 - 500.000 = 1.150.000.
                Check("Tính lương P3a §6 #7 (chế độ Chấm công & phân quyền, Chiến chốt 28/11): không có quyền Tính lương thì chặn đọc/ghi ở chế độ Tính lương; Chấm công nạp và Cập nhật ghi cả TBANGLUONGCHITIET lẫn TBANGLUONGTONGHOP như gốc",
                    blockedMoneyRead && !blockedMoneyWrite.Ok && attLoadsMoney
                    && attSave.Ok && sumAfterAtt == sumBeforeAtt && tongBeforeAtt == 1400000m && tongAfterAtt == 1500000m && netAfterAtt == 1150000m && detAfterAtt == 47,
                    "blkR=" + blockedMoneyRead + ", blkW=" + !blockedMoneyWrite.Ok + ", loadM=" + attLoadsMoney
                    + ", sum=" + sumBeforeAtt + "->" + sumAfterAtt + ", tong=" + tongBeforeAtt + "->" + tongAfterAtt + ", net=" + netAfterAtt + ", det=" + detAfterAtt);

                // §6 #8: Tải lại toàn bảng & tải lại 1 nhân viên: đổi LUONGCA NV A -> 150.000, thêm NV mới -> giữ chấm công (15 ca), đổi TONGLUONG = 2.250.000, NV mới xuất hiện.
                PayrollSheetData reloadedSheet = payrollSvc.LoadSheet(periodId);
                BookingExec(db, "UPDATE DNHANVIEN SET LUONGCA = 150000 WHERE ID = @I", "@I", empAId);
                seedEmp(empNewId, "NV Mới TL " + tag, EmployeeSalaryMode.ByShift, 120000m, 0m, 0, 0);
                string reloadErr;
                bool reloadAllOk = payrollSvc.ReloadAllFromCatalog(reloadedSheet, out reloadErr);
                PayrollEmployeeEntry reloadedA = reloadedSheet.Employees.Find(e => e.EmployeeId == empAId);
                bool hasNewEmp = reloadedSheet.Employees.Exists(e => e.EmployeeId == empNewId);

                // Thử tải lại 1 nhân viên (đổi LUONGCA NV A -> 200.000).
                BookingExec(db, "UPDATE DNHANVIEN SET LUONGCA = 200000 WHERE ID = @I", "@I", empAId);
                string reloadOneErr;
                bool reloadOneOk = payrollSvc.ReloadEmployeeFromCatalog(reloadedSheet, empAId, out reloadOneErr);

                Check("Tính lương P3a §6 #8 (Tải lại toàn bảng & một nhân viên): cập nhật LUONGCA mới (150k->2.25M, 200k->3M), giữ nguyên 15 ca chấm công, thêm NV mới",
                    reloadAllOk && hasNewEmp && reloadedA != null && reloadedA.TotalShifts == 15m
                    && reloadOneOk && reloadedA.BaseSalary == 200000m && reloadedA.TotalSalary == 3000000m,
                    "all=" + reloadAllOk + ", newEmp=" + hasNewEmp + ", ca=" + (reloadedA == null ? -1 : reloadedA.TotalShifts) + ", tong=" + (reloadedA == null ? -1 : reloadedA.TotalSalary));

                // §6 #9: Xóa bảng lương có phiếu chi lương bị chặn nêu số phiếu; rollback transaction khi lỗi giữa chừng; xóa sạch 3 bảng + lưu vết + chuỗi 255 ký tự không tràn.
                string payoutNo = "PC04/99" + tag.Substring(0, 3);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, DCUAHANGID, TBANGLUONGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 1, 0, @E, @S, @BL, 0, 1000000, 0, 30, 'selftest_tl', @T)",
                    "@I", payoutVoucherId, "@N", payoutNo, "@D", new DateTime(2004, 3, 31), "@E", empAId, "@S", storeId, "@BL", periodId, "@T", now);
                createdVoucherIds.Add(payoutVoucherId);

                PayrollDeleteResult delBlocked = payrollSvc.DeletePeriod(periodId);
                bool blockedWithVoucherNo = !delBlocked.Ok
                    && delBlocked.BlockingVoucherNumbers.Contains(payoutNo)
                    && delBlocked.ErrorMessage != null
                    && delBlocked.ErrorMessage.IndexOf(payoutNo, StringComparison.Ordinal) >= 0;

                // Gỡ phiếu chi lương -> thử rollback xóa giữa chừng.
                SafeDeleteById(db, "TTHUCHI", payoutVoucherId);
                bool delRolledBack = false;
                try
                {
                    payrollSvc.DeletePeriod(periodId, delegate { throw new InvalidOperationException("Lỗi giả lập xóa bảng lương"); });
                }
                catch (InvalidOperationException)
                {
                    delRolledBack = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONG WHERE ID = @I", "@I", periodId) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId) == sumAfterAtt
                        && BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId) == detAfterAtt;
                }

                // Kiểm lưu vết chuỗi 255 ký tự khi lưu & xóa (đặt NAME dài đúng 255 ký tự).
                string name255 = "Tháng 3/2004 " + new string('ê', 255 - 13);
                BookingExec(db, "UPDATE TBANGLUONG SET NAME = @N WHERE ID = @I", "@N", name255, "@I", periodId);
                int logsBeforeSave255 = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", periodId);
                PayrollSaveResult save255 = payrollSvc.SaveSheet(reloadedSheet);
                int logsAfterSave255 = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", periodId);

                // Thử rollback SaveSheet khi lỗi giữa chừng.
                int logsBeforeSaveRb = logsAfterSave255;
                bool saveRolledBack = false;
                try
                {
                    payrollSvc.SaveSheet(reloadedSheet, delegate { throw new InvalidOperationException("Lỗi giả lập lưu bảng lương"); });
                }
                catch (InvalidOperationException)
                {
                    saveRolledBack = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", periodId) == logsBeforeSaveRb;
                }

                PayrollDeleteResult delOk = payrollSvc.DeletePeriod(periodId);
                int remPeriod = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONG WHERE ID = @I", "@I", periodId);
                int remSum = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                int remDet = BookingCount(db, "SELECT COUNT(*) FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                int logsAfterDel = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", periodId);

                Check("Tính lương P3a §6 #9 (chặn xóa có phiếu chi nêu số phiếu, rollback 1 transaction, xóa sạch 3 bảng, 1 dòng lưu vết & tên 255 ký tự không tràn): đạt",
                    blockedWithVoucherNo && delRolledBack && saveRolledBack
                    && save255.Ok && logsAfterSave255 == logsBeforeSave255 + 1
                    && delOk.Ok && remPeriod == 0 && remSum == 0 && remDet == 0 && logsAfterDel == logsAfterSave255 + 1,
                    "blk=" + blockedWithVoucherNo + ", rbDel=" + delRolledBack + ", rbSave=" + saveRolledBack
                    + ", s255=" + save255.Ok + ", del=" + delOk.Ok + ", rem=" + remPeriod + "/" + remSum + "/" + remDet
                    + ", logs=" + logsBeforeSave255 + "->" + logsAfterSave255 + "->" + logsAfterDel);
            }
            finally
            {
                foreach (string pid in createdPeriodIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", pid);
                    SafeDeleteById(db, "TBANGLUONG", pid);
                }
                foreach (string vid in createdVoucherIds)
                    SafeDeleteById(db, "TTHUCHI", vid);
                foreach (string tid in createdTpIds)
                    SafeDeleteById(db, "TTHUONGPHAT", tid);
                foreach (string eid in createdEmpIds)
                    SafeDeleteById(db, "DNHANVIEN", eid);
                SafeDeleteById(db, "DCALAMVIEC", shiftSangId);
                SafeDeleteById(db, "DCALAMVIEC", shiftToiId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
