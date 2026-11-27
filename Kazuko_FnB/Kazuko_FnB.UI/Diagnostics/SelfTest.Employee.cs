using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void EmployeeTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_nv",
                    UserName = "selftest_nv",
                    DisplayName = "Selftest NhanVien",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                EmployeeRulesPureTests();
                EmployeeServiceAndRepoTests(db);
                EmployeeUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void EmployeeRulesPureTests()
        {
            Check("Nhân sự §5 #2 (so trùng thuần): tên theo UNICODE_CI_AI không cắt khoảng trắng, mã không phân biệt hoa thường",
                EmployeeRules.AreNamesDuplicate("Nguyễn Văn Đạt", "nguyen van dat")
                && !EmployeeRules.AreNamesDuplicate(" An ", "An")
                && EmployeeRules.AreCodesDuplicate("NV01", "nv01")
                && !EmployeeRules.AreCodesDuplicate("", ""));

            EmployeeSaveResult rEmptyName = EmployeeRules.Validate(new EmployeeRow { Name = "  ", SalaryMode = 30 }, false, false, false);
            EmployeeSaveResult rNegShift = EmployeeRules.Validate(new EmployeeRow { Name = "A", SalaryMode = 30, ShiftSalary = -1m }, false, false, false);
            EmployeeSaveResult rBigMonth = EmployeeRules.Validate(new EmployeeRow { Name = "A", SalaryMode = 0, MonthlySalary = 1000000000m }, false, false, false);
            EmployeeSaveResult rName256 = EmployeeRules.Validate(new EmployeeRow { Name = new string('a', 256), SalaryMode = 30 }, false, false, false);
            EmployeeSaveResult rNote256 = EmployeeRules.Validate(new EmployeeRow { Name = "A", SalaryMode = 30, Note = new string('n', 256) }, false, false, false);
            EmployeeSaveResult rNeedCode = EmployeeRules.Validate(new EmployeeRow { Name = "A", SalaryMode = 30, ShiftId = "s1" }, true, false, false);
            EmployeeSaveResult rNeedShift = EmployeeRules.Validate(new EmployeeRow { Name = "A", Code = "NV1", SalaryMode = 30 }, true, false, false);

            Check("Nhân sự §5 #2/#3 (luật kiểm tra thuần NV): chặn trống Tên, lương âm, lương 10 chữ số, chuỗi 256, thiếu Mã/Ca khi bật máy chấm công",
                !rEmptyName.Ok && rEmptyName.HasNameError
                && !rNegShift.Ok && rNegShift.HasShiftSalaryError
                && !rBigMonth.Ok && rBigMonth.HasMonthlySalaryError
                && !rName256.Ok && rName256.HasNameError
                && !rNote256.Ok && rNote256.HasNoteError
                && !rNeedCode.Ok && rNeedCode.HasCodeError && rNeedCode.ErrorMessage.IndexOf("Mã nhân viên", StringComparison.Ordinal) >= 0
                && !rNeedShift.Ok && rNeedShift.HasShiftError && rNeedShift.ErrorMessage.IndexOf("Ca làm việc", StringComparison.Ordinal) >= 0,
                "en=" + rEmptyName.HasNameError + ", ns=" + rNegShift.HasShiftSalaryError + ", bm=" + rBigMonth.HasMonthlySalaryError
                + ", n256=" + rName256.HasNameError + ", nc=" + rNeedCode.HasCodeError + ", nsh=" + rNeedShift.HasShiftError);

            var permsView = new FunctionPermissions(false);
            permsView.Add("", EmployeeText.FunctionName, RoleModes.View);
            var sessView = new UserSession { UserId = "u_nv_v", IsAdmin = false, Permissions = permsView };
            var sessNone = new UserSession { UserId = "u_nv_0", IsAdmin = false, Permissions = new FunctionPermissions(false) };
            var sessAdm = new UserSession { UserId = "u_nv_a", IsAdmin = true, Permissions = new FunctionPermissions(false) };

            Check("Nhân sự §5 #5 (quyền thuần NV theo tên chức năng): Admin đủ quyền, chỉ Xem chỉ có View, không quyền khóa hết",
                EmployeeRules.Can(sessAdm, RoleModes.Remove)
                && EmployeeRules.Can(sessView, RoleModes.View) && !EmployeeRules.Can(sessView, RoleModes.Add)
                && !EmployeeRules.Can(sessView, RoleModes.Edit) && !EmployeeRules.Can(sessView, RoleModes.Remove)
                && !EmployeeRules.Can(sessNone, RoleModes.View));
        }

        private static void EmployeeServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { EmployeeText.ConfigUseTimekeeperKey, ActivityConstants.ConfigKey });
            var adminSess = new UserSession
            {
                UserId = "selftest_nv",
                UserName = "selftest_nv",
                DisplayName = "Selftest NhanVien",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSess;
            var svc = new EmployeeService(adminSess);
            var repo = new EmployeeRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            string shiftId = DbHelper.NewId();
            string imgId = DbHelper.NewId();
            var createdEmpIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);

                BookingExec(db,
                    "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 100, 30, 'selftest_nv', @T)",
                    "@I", shiftId, "@N", "Ca Sáng NV " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO SIMAGE (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_nv')",
                    "@I", imgId, "@N", "Ảnh NV " + tag);

                // #1a: Thêm hợp lệ chỉ Tên (tắt máy chấm công) -> GUID, STATUS=30, CACHTINHLUONG=30, NGHITHU7=30, NGHICHUNHAT=0.
                EmployeeRow eMin = EmployeeRow.NewDefault();
                eMin.Name = "Nguyễn An " + tag;
                EmployeeSaveResult rMin = svc.Save(eMin);
                if (rMin.Ok) createdEmpIds.Add(rMin.SavedId);
                EmployeeRow backMin = rMin.Ok ? repo.LoadById(rMin.SavedId) : null;
                Guid parsedGuid;
                int addLog = rMin.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", rMin.SavedId, "@F", EmployeeText.FunctionName)
                    : 0;

                // #1b: Thêm đủ trường Theo tháng (4.000.000, Nghỉ CN + T7) và Tháng theo ca (60).
                EmployeeRow eMonth = EmployeeRow.NewDefault();
                eMonth.Name = "Trần Bình " + tag;
                eMonth.Phone = "0901234567";
                eMonth.Address = "12 Lê Lợi";
                eMonth.SalaryMode = EmployeeSalaryMode.ByMonth;
                eMonth.MonthlySalary = 4000000m;
                eMonth.IsOffSunday = true;
                eMonth.IsOffSaturday = true;
                eMonth.Note = "Lương tháng";
                EmployeeSaveResult rMonth = svc.Save(eMonth);
                if (rMonth.Ok) createdEmpIds.Add(rMonth.SavedId);
                EmployeeRow backMonth = rMonth.Ok ? repo.LoadById(rMonth.SavedId) : null;

                EmployeeRow eCombo = EmployeeRow.NewDefault();
                eCombo.Name = "Lê Cúc " + tag;
                eCombo.SalaryMode = EmployeeSalaryMode.MonthByShift;
                eCombo.ShiftSalary = 150000m;
                eCombo.MonthlySalary = 3500000m;
                EmployeeSaveResult rCombo = svc.Save(eCombo);
                if (rCombo.Ok) createdEmpIds.Add(rCombo.SavedId);
                EmployeeRow backCombo = rCombo.Ok ? repo.LoadById(rCombo.SavedId) : null;

                Check("Nhân sự §5 #1 (thêm NV chỉ Tên mặc định, Theo tháng 4tr + 2 cờ, Tháng theo ca 60): lưu và đọc lại khớp đủ trường",
                    rMin.Ok && backMin != null && Guid.TryParse(backMin.Id, out parsedGuid)
                    && backMin.Status == 30 && backMin.SalaryMode == 30
                    && backMin.OffSaturday == 30 && backMin.OffSunday == 0
                    && backMin.CreatedById == "selftest_nv" && backMin.CreatedAt.HasValue && addLog >= 1
                    && rMonth.Ok && backMonth != null && backMonth.SalaryMode == 0
                    && backMonth.MonthlySalary == 4000000m && backMonth.OffSunday == 30 && backMonth.OffSaturday == 30
                    && rCombo.Ok && backCombo != null && backCombo.SalaryMode == 60
                    && backCombo.ShiftSalary == 150000m && backCombo.MonthlySalary == 3500000m,
                    "min=" + rMin.Ok + ", mode=" + (backMin == null ? -1 : backMin.SalaryMode)
                    + ", t7=" + (backMin == null ? -1 : backMin.OffSaturday) + ", cn=" + (backMin == null ? -1 : backMin.OffSunday)
                    + ", mon=" + rMonth.Ok + ", cmb=" + rCombo.Ok + ", log=" + addLog);

                // #2: Trống tên, trùng tên khác hoa thường/dấu, sửa chính nó OK, lương âm/quá 9 chữ số, chuỗi 256 chặn, chuỗi 255 + lưu vết OK.
                EmployeeSaveResult rEmpty = svc.Save(new EmployeeRow { Name = "  ", SalaryMode = 30 });
                EmployeeSaveResult rDupName = svc.Save(new EmployeeRow { Name = "nguyen an " + tag, SalaryMode = 30 });
                EmployeeSaveResult rNeg = svc.Save(new EmployeeRow { Name = "NV Âm " + tag, SalaryMode = 30, ShiftSalary = -10m });
                EmployeeSaveResult rBig = svc.Save(new EmployeeRow { Name = "NV To " + tag, SalaryMode = 0, MonthlySalary = 1000000000m });
                EmployeeSaveResult rLong256 = svc.Save(new EmployeeRow { Name = new string('X', 256), SalaryMode = 30 });

                DateTime? origCreatedAt = backMin.CreatedAt;
                string origCreatedBy = backMin.CreatedById;
                backMin.Note = "Sửa chính mình";
                EmployeeSaveResult rSelf = svc.Save(backMin);
                EmployeeRow afterSelf = repo.LoadById(backMin.Id);

                string name255 = "NV255 " + tag + " " + new string('ê', 255 - 7 - tag.Length);
                var e255 = EmployeeRow.NewDefault();
                e255.Name = name255;
                e255.Phone = new string('9', 255);
                e255.Address = new string('Đ', 255);
                e255.Note = new string('G', 255);
                EmployeeSaveResult r255 = svc.Save(e255);
                if (r255.Ok) createdEmpIds.Add(r255.SavedId);
                EmployeeRow back255 = r255.Ok ? repo.LoadById(r255.SavedId) : null;
                int log255 = r255.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", r255.SavedId, "@F", EmployeeText.FunctionName)
                    : 0;

                Check("Nhân sự §5 #2 (chặn trống/trùng tên/lương âm/quá 9 số/256, sửa chính mình giữ tạo, biên 255 không tràn lưu vết): đạt",
                    !rEmpty.Ok && !rDupName.Ok && rDupName.HasNameError
                    && !rNeg.Ok && rNeg.HasShiftSalaryError && !rBig.Ok && rBig.HasMonthlySalaryError
                    && !rLong256.Ok && rLong256.HasNameError
                    && rSelf.Ok && afterSelf != null && afterSelf.Note == "Sửa chính mình"
                    && afterSelf.CreatedById == origCreatedBy && afterSelf.CreatedAt == origCreatedAt
                    && afterSelf.ModifiedById == "selftest_nv" && afterSelf.ModifiedAt.HasValue
                    && r255.Ok && back255 != null && back255.Name == name255 && log255 >= 1,
                    "empty=" + rEmpty.Ok + ", dup=" + rDupName.Ok + ", neg=" + rNeg.Ok + ", big=" + rBig.Ok
                    + ", l256=" + rLong256.Ok + ", self=" + rSelf.Ok + ", r255=" + r255.Ok + ", log255=" + log255);

                // #3: Bật máy chấm công -> Mã và Ca bắt buộc, trùng Mã (khác hoa thường) chặn;
                // tắt -> sửa giữ nguyên CODE, DCALAMVIECID, LUONGTHEOCA, SIMAGEID, và giữ Lương tháng cũ khi chuyển sang Theo ca.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 30);
                EmployeeSaveResult rNoCode = svc.Save(new EmployeeRow { Name = "NV Thiếu Mã " + tag, ShiftId = shiftId, SalaryMode = 30 });
                EmployeeSaveResult rNoShift = svc.Save(new EmployeeRow { Name = "NV Thiếu Ca " + tag, Code = "MCC_" + tag, SalaryMode = 30 });

                EmployeeRow eTk = EmployeeRow.NewDefault();
                eTk.Name = "NV Chấm Công " + tag;
                eTk.Code = "MCC01_" + tag;
                eTk.ShiftId = shiftId;
                eTk.SalaryMode = EmployeeSalaryMode.ByMonth;
                eTk.MonthlySalary = 5500000m;
                EmployeeSaveResult rTk = svc.Save(eTk);
                if (rTk.Ok) createdEmpIds.Add(rTk.SavedId);

                EmployeeSaveResult rDupCode = svc.Save(new EmployeeRow
                {
                    Name = "NV Trùng Mã " + tag,
                    Code = "mcc01_" + tag,
                    ShiftId = shiftId,
                    SalaryMode = 30
                });

                // Gán giá trị LUONGTHEOCA và SIMAGEID trong DB rồi tắt máy chấm công và chuyển sang Theo ca.
                BookingExec(db, "UPDATE DNHANVIEN SET LUONGTHEOCA = 99000, SIMAGEID = @IMG WHERE ID = @I",
                    "@IMG", imgId, "@I", eTk.Id);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);

                EmployeeRow editTk = repo.LoadById(eTk.Id);
                editTk.Code = "";
                editTk.ShiftId = null;
                editTk.SalaryMode = EmployeeSalaryMode.ByShift;
                editTk.ShiftSalary = 220000m;
                editTk.MonthlySalary = 0m; // ô Lương tháng bị khóa -> service phải giữ nguyên 5.500.000 từ DB
                EmployeeSaveResult rPreserve = svc.Save(editTk);
                EmployeeRow afterPreserve = repo.LoadById(eTk.Id);

                Check("Nhân sự §5 #3 (bật/tắt máy chấm công, trùng Mã không phân biệt hoa thường, giữ CODE/DCALAMVIECID/LUONGTHEOCA/LUONGTHANG cũ): đạt",
                    !rNoCode.Ok && rNoCode.HasCodeError && rNoCode.ErrorMessage.IndexOf("Mã nhân viên", StringComparison.Ordinal) >= 0
                    && !rNoShift.Ok && rNoShift.HasShiftError && rNoShift.ErrorMessage.IndexOf("Ca làm việc", StringComparison.Ordinal) >= 0
                    && rTk.Ok && !rDupCode.Ok && rDupCode.HasCodeError
                    && rPreserve.Ok && afterPreserve != null
                    && afterPreserve.Code == "MCC01_" + tag && afterPreserve.ShiftId == shiftId
                    && afterPreserve.LegacyShiftSalary == 99000m && afterPreserve.ImageId == imgId
                    && afterPreserve.SalaryMode == 30 && afterPreserve.ShiftSalary == 220000m
                    && afterPreserve.MonthlySalary == 5500000m,
                    "noC=" + rNoCode.HasCodeError + ", noS=" + rNoShift.HasShiftError + ", dupC=" + rDupCode.HasCodeError
                    + ", pres=" + rPreserve.Ok + ", code=" + (afterPreserve == null ? "" : afterPreserve.Code)
                    + ", ltc=" + (afterPreserve == null ? "" : Convert.ToString(afterPreserve.LegacyShiftSalary))
                    + ", msal=" + (afterPreserve == null ? 0m : afterPreserve.MonthlySalary));

                // #4: Chặn xóa khi có tham chiếu ở >= 3 bảng thật (5 bảng) với số lượng đúng, xóa NV chưa dùng thành công + lưu vết.
                CheckEmployeeDeleteBlockedOnMultipleTables(db, svc, repo, eMonth.Id, tag);

                EmployeeDeleteResult rDelEmpty = svc.Delete(eCombo.Id);
                bool gone = repo.LoadById(eCombo.Id) == null;
                int delLogs = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", eCombo.Id, "@F", EmployeeText.FunctionName);
                Check("Nhân sự §5 #4 (xóa cứng NV chưa dùng): xóa khỏi DNHANVIEN và ghi lưu vết",
                    rDelEmpty.Ok && gone && delLogs >= 2,
                    SingleLine(rDelEmpty.ErrorMessage ?? ("gone=" + gone + ", logs=" + delLogs)));

                // #5 tầng Services: chỉ Xem đọc được nhưng chặn Thêm/Sửa/Xóa; không quyền chặn cả đọc.
                CheckEmployeeServicePermissions(db, eMin.Id, tag);
            }
            finally
            {
                foreach (string eid in createdEmpIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", eid);
                    SafeDeleteById(db, "DNHANVIEN", eid);
                }
                SafeDeleteById(db, "SIMAGE", imgId);
                SafeDeleteById(db, "DCALAMVIEC", shiftId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void CheckEmployeeDeleteBlockedOnMultipleTables(
            string db, EmployeeService svc, EmployeeRepository repo, string empId, string tag)
        {
            string tc1 = DbHelper.NewId();
            string tc2 = DbHelper.NewId();
            string tp1 = DbHelper.NewId();
            string cc1 = DbHelper.NewId();
            string kh1 = DbHelper.NewId();
            string us1 = DbHelper.NewId();
            try
            {
                InsertWarehouseRefRow(db, "TTHUCHI", "DNHANVIENID", tc1, empId, "REF-TC1-" + tag);
                InsertWarehouseRefRow(db, "TTHUCHI", "DNHANVIENID", tc2, empId, "REF-TC2-" + tag);
                InsertWarehouseRefRow(db, "TTHUONGPHAT", "DNHANVIENID", tp1, empId, "REF-TP1-" + tag);
                InsertWarehouseRefRow(db, "TBANGLUONGCHITIET", "DNHANVIENID", cc1, empId, "REF-CC1-" + tag);
                InsertWarehouseRefRow(db, "DKHACHHANG", "DNHANVIENID", kh1, empId, "REF-KH1-" + tag);
                InsertWarehouseRefRow(db, "SUSER", "DNHANVIENID", us1, empId, "REF-US1-" + tag);

                EmployeeDeleteResult del = svc.Delete(empId);
                string msg = del.ErrorMessage ?? "";
                bool stillThere = repo.LoadById(empId) != null;
                bool hasTc = msg.IndexOf("2 phiếu thu chi", StringComparison.Ordinal) >= 0;
                bool hasTp = msg.IndexOf("1 phiếu thưởng phạt", StringComparison.Ordinal) >= 0;
                bool hasCc = msg.IndexOf("1 dòng chấm công", StringComparison.Ordinal) >= 0;
                bool hasKh = msg.IndexOf("1 khách hàng", StringComparison.Ordinal) >= 0;
                bool hasUs = msg.IndexOf("1 tài khoản", StringComparison.Ordinal) >= 0;

                Check("Nhân sự §5 #4 (chặn xóa NV đang dùng ở 5 bảng thật): chặn xóa và nêu đúng số lượng từng nơi",
                    !del.Ok && stillThere && del.ReferenceCount == 6 && hasTc && hasTp && hasCc && hasKh && hasUs,
                    SingleLine("refs=" + del.ReferenceCount + ", msg=" + msg));
            }
            finally
            {
                SafeDeleteById(db, "SUSER", us1);
                SafeDeleteById(db, "DKHACHHANG", kh1);
                SafeDeleteById(db, "TBANGLUONGCHITIET", cc1);
                SafeDeleteById(db, "TTHUONGPHAT", tp1);
                SafeDeleteById(db, "TTHUCHI", tc2);
                SafeDeleteById(db, "TTHUCHI", tc1);
            }
        }

        private static void CheckEmployeeServicePermissions(string db, string existingId, string tag)
        {
            var permsView = new FunctionPermissions(false);
            permsView.Add("", EmployeeText.FunctionName, RoleModes.View);
            var svcView = new EmployeeService(new UserSession { UserId = "u_nv_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView });

            bool canRead = svcView.LoadAll().Count > 0;
            EmployeeSaveResult addView = svcView.Save(new EmployeeRow { Name = "NV Chỉ Xem " + tag, SalaryMode = 30 });
            EmployeeRow loaded = svcView.LoadById(existingId);
            EmployeeSaveResult editView = loaded != null ? svcView.Save(loaded) : EmployeeSaveResult.Fail("skip");
            EmployeeDeleteResult delView = svcView.Delete(existingId);

            var svcNone = new EmployeeService(new UserSession { UserId = "u_nv_none", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) });
            bool noneBlocked = svcNone.LoadAll().Count == 0 && svcNone.LoadById(existingId) == null;

            Check("Nhân sự §5 #5 (quyền tầng Services NV): chỉ Xem đọc được nhưng chặn Thêm/Sửa/Xóa; không quyền chặn cả đọc",
                canRead && !addView.Ok && addView.ErrorMessage == EmployeeText.NoPermission
                && !editView.Ok && editView.ErrorMessage == EmployeeText.NoPermission
                && !delView.Ok && delView.ErrorMessage == EmployeeText.NoPermission
                && noneBlocked,
                "read=" + canRead + ", add=" + addView.Ok + ", edit=" + editView.Ok + ", del=" + delView.Ok + ", none=" + noneBlocked);
        }
    }
}
