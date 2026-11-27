using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void ShiftTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_ca",
                    UserName = "selftest_ca",
                    DisplayName = "Selftest CaLamViec",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                ShiftRulesPureTests();
                ShiftServiceAndRepoTests(db);
                ShiftUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void ShiftRulesPureTests()
        {
            DateTime? norm = ShiftRules.NormalizeShiftTime(new DateTime(2026, 11, 27, 22, 15, 45));
            Check("Nhân sự §5 #6 (chuẩn hóa giờ ca & so trùng tên): gắn ngày 2000-01-01 bỏ giây, tên theo UNICODE_CI_AI",
                norm.HasValue && norm.Value == new DateTime(2000, 1, 1, 22, 15, 0)
                && ShiftRules.FormatShiftTime(norm) == "22:15"
                && ShiftRules.AreNamesDuplicate("Ca Sáng Đặc Biệt", "ca sang dac biet")
                && !ShiftRules.AreNamesDuplicate(" Ca 1 ", "Ca 1"));

            ShiftSaveResult rEmpty = ShiftRules.Validate(new ShiftRow { Name = "  ", SalaryRate = 100m }, false, false);
            ShiftSaveResult rNeg = ShiftRules.Validate(new ShiftRow { Name = "Ca", SalaryRate = -1m }, false, false);
            ShiftSaveResult rOver = ShiftRules.Validate(new ShiftRow { Name = "Ca", SalaryRate = 1000.01m }, false, false);
            ShiftSaveResult rZeroRate = ShiftRules.Validate(new ShiftRow { Name = "Ca", SalaryRate = 0m }, false, false);
            ShiftSaveResult rNeedTime = ShiftRules.Validate(new ShiftRow { Name = "Ca", SalaryRate = 100m }, true, false);
            ShiftSaveResult rSameTime = ShiftRules.Validate(new ShiftRow
            {
                Name = "Ca",
                SalaryRate = 100m,
                FromTime = new DateTime(2000, 1, 1, 8, 0, 0),
                ToTime = new DateTime(2000, 1, 1, 8, 0, 0)
            }, true, false);
            ShiftSaveResult rNight = ShiftRules.Validate(new ShiftRow
            {
                Name = "Ca đêm",
                SalaryRate = 150m,
                FromTime = new DateTime(2000, 1, 1, 22, 0, 0),
                ToTime = new DateTime(2000, 1, 1, 6, 0, 0)
            }, true, false);

            Check("Nhân sự §5 #6/#7 (luật kiểm tra thuần ca): chặn trống tên, tỉ lệ <0/>1000, thiếu giờ/trùng giờ khi bật máy; cho phép 0% và ca qua đêm",
                !rEmpty.Ok && rEmpty.HasNameError
                && !rNeg.Ok && rNeg.HasSalaryRateError
                && !rOver.Ok && rOver.HasSalaryRateError
                && rZeroRate.Ok
                && !rNeedTime.Ok && rNeedTime.HasFromTimeError && rNeedTime.HasToTimeError
                && !rSameTime.Ok && rSameTime.HasToTimeError
                && rNight.Ok,
                "e=" + rEmpty.HasNameError + ", n=" + rNeg.HasSalaryRateError + ", o=" + rOver.HasSalaryRateError
                + ", z=" + rZeroRate.Ok + ", nt=" + rNeedTime.HasFromTimeError + ", st=" + rSameTime.HasToTimeError + ", night=" + rNight.Ok);
        }

        private static void ShiftServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ShiftText.ConfigUseTimekeeperKey, ActivityConstants.ConfigKey });
            var adminSess = new UserSession
            {
                UserId = "selftest_ca",
                UserName = "selftest_ca",
                DisplayName = "Selftest CaLamViec",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSess;
            var svc = new ShiftService(adminSess);
            var repo = new ShiftRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            var createdShiftIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 0);

                // #6a: Thêm ca khi tắt máy chấm công -> mặc định 100%, TUGIO/DENGIO rỗng, tỉ lệ 0 vẫn lưu được.
                ShiftRow shDefault = ShiftRow.NewDefault();
                shDefault.Name = "Ca Sáng " + tag;
                ShiftSaveResult rDef = svc.Save(shDefault);
                if (rDef.Ok) createdShiftIds.Add(rDef.SavedId);
                ShiftRow backDef = rDef.Ok ? repo.LoadById(rDef.SavedId) : null;

                ShiftRow shZero = ShiftRow.NewDefault();
                shZero.Name = "Ca Tỉ Lệ 0 " + tag;
                shZero.SalaryRate = 0m;
                ShiftSaveResult rZero = svc.Save(shZero);
                if (rZero.Ok) createdShiftIds.Add(rZero.SavedId);
                ShiftRow backZero = rZero.Ok ? repo.LoadById(rZero.SavedId) : null;

                Check("Nhân sự §5 #6 (thêm ca tắt máy chấm công): mặc định TILELUONG=100, giờ rỗng, giá trị 0 vẫn lưu được",
                    rDef.Ok && backDef != null && backDef.Status == 30 && backDef.SalaryRate == 100m
                    && !backDef.FromTime.HasValue && !backDef.ToTime.HasValue
                    && rZero.Ok && backZero != null && backZero.SalaryRate == 0m,
                    "def=" + rDef.Ok + ", rate=" + (backDef == null ? -1m : backDef.SalaryRate) + ", zero=" + (backZero == null ? -1m : backZero.SalaryRate));

                // #6b: Bật máy chấm công -> bắt buộc cả hai giờ, hai giờ bằng nhau chặn, ca qua đêm (22:00 - 06:00) lưu và đọc đúng HH:mm.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 30);
                ShiftSaveResult rMissingTo = svc.Save(new ShiftRow
                {
                    Name = "Ca Thiếu Giờ " + tag,
                    FromTime = new DateTime(2000, 1, 1, 8, 0, 0),
                    ToTime = null,
                    SalaryRate = 100m
                });
                ShiftSaveResult rEqual = svc.Save(new ShiftRow
                {
                    Name = "Ca 0 Phút " + tag,
                    FromTime = new DateTime(2000, 1, 1, 9, 30, 0),
                    ToTime = new DateTime(2000, 1, 1, 9, 30, 0),
                    SalaryRate = 100m
                });

                ShiftRow shNight = ShiftRow.NewDefault();
                shNight.Name = "Ca Đêm " + tag;
                shNight.FromTime = new DateTime(2026, 11, 27, 22, 0, 19);
                shNight.ToTime = new DateTime(2026, 11, 27, 6, 0, 55);
                shNight.SalaryRate = 130m;
                ShiftSaveResult rNight = svc.Save(shNight);
                if (rNight.Ok) createdShiftIds.Add(rNight.SavedId);
                ShiftRow backNight = rNight.Ok ? repo.LoadById(rNight.SavedId) : null;

                // Tắt máy chấm công rồi sửa Ca Đêm -> TUGIO/DENGIO cũ phải được giữ nguyên.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ShiftText.ConfigUseTimekeeperKey, 0);
                ShiftRow editNight = repo.LoadById(shNight.Id);
                editNight.FromTime = null;
                editNight.ToTime = null;
                editNight.Note = "Sửa khi tắt máy";
                ShiftSaveResult rKeepTime = svc.Save(editNight);
                ShiftRow backKeep = repo.LoadById(shNight.Id);

                Check("Nhân sự §5 #6 (bật máy chấm công: bắt buộc 2 giờ, chặn giờ bằng nhau, ca qua đêm 22:00-06:00, tắt máy giữ giờ cũ): đạt",
                    !rMissingTo.Ok && rMissingTo.HasToTimeError
                    && !rEqual.Ok && rEqual.HasToTimeError
                    && rNight.Ok && backNight != null
                    && backNight.FromTime == new DateTime(2000, 1, 1, 22, 0, 0)
                    && backNight.ToTime == new DateTime(2000, 1, 1, 6, 0, 0)
                    && ShiftRules.FormatShiftTime(backNight.FromTime) == "22:00"
                    && ShiftRules.FormatShiftTime(backNight.ToTime) == "06:00"
                    && rKeepTime.Ok && backKeep != null
                    && backKeep.FromTime == new DateTime(2000, 1, 1, 22, 0, 0)
                    && backKeep.ToTime == new DateTime(2000, 1, 1, 6, 0, 0),
                    "miss=" + rMissingTo.HasToTimeError + ", eq=" + rEqual.HasToTimeError + ", night=" + rNight.Ok
                    + ", keep=" + rKeepTime.Ok + ", f=" + ShiftRules.FormatShiftTime(backKeep == null ? (DateTime?)null : backKeep.FromTime));

                // #7a: Trùng tên chặn, sửa chính mình OK, tỉ lệ ngoài 0..1000 chặn, biên 255 ký tự + lưu vết không tràn.
                ShiftSaveResult rDup = svc.Save(new ShiftRow { Name = "ca sang " + tag, SalaryRate = 100m });
                ShiftSaveResult rRateNeg = svc.Save(new ShiftRow { Name = "Ca Âm " + tag, SalaryRate = -5m });
                ShiftSaveResult rRateHigh = svc.Save(new ShiftRow { Name = "Ca Cao " + tag, SalaryRate = 1001m });

                string name255 = "CA255 " + tag + " " + new string('ê', 255 - 7 - tag.Length);
                ShiftRow sh255 = ShiftRow.NewDefault();
                sh255.Name = name255;
                sh255.Note = new string('G', 255);
                ShiftSaveResult r255 = svc.Save(sh255);
                if (r255.Ok) createdShiftIds.Add(r255.SavedId);
                int log255 = r255.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", r255.SavedId, "@F", ShiftText.FunctionName)
                    : 0;

                Check("Nhân sự §5 #7 (trùng tên, tỉ lệ ngoài 0..1000, biên 255 ký tự không tràn lưu vết): đạt",
                    !rDup.Ok && rDup.HasNameError && !rRateNeg.Ok && !rRateHigh.Ok && r255.Ok && log255 >= 1,
                    "dup=" + rDup.HasNameError + ", neg=" + rRateNeg.Ok + ", high=" + rRateHigh.Ok + ", r255=" + r255.Ok + ", log=" + log255);

                // #7b: Chặn xóa khi còn nhân viên và dòng chấm công tham chiếu; xóa ca trống thành công + lưu vết.
                CheckShiftDeleteBlockedOnBothTables(db, svc, repo, shDefault.Id, tag);

                ShiftDeleteResult rDelEmpty = svc.Delete(shZero.Id);
                bool gone = repo.LoadById(shZero.Id) == null;
                int delLogs = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", shZero.Id, "@F", ShiftText.FunctionName);
                Check("Nhân sự §5 #7 (xóa cứng ca trống): xóa khỏi DCALAMVIEC và ghi lưu vết",
                    rDelEmpty.Ok && gone && delLogs >= 2,
                    SingleLine(rDelEmpty.ErrorMessage ?? ("gone=" + gone + ", logs=" + delLogs)));

                // #7c: Quyền tầng Services.
                CheckShiftServicePermissions(db, shDefault.Id, tag);
            }
            finally
            {
                foreach (string sid in createdShiftIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", sid);
                    SafeDeleteById(db, "DCALAMVIEC", sid);
                }
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void CheckShiftDeleteBlockedOnBothTables(
            string db, ShiftService svc, ShiftRepository repo, string shiftId, string tag)
        {
            string emp1 = DbHelper.NewId();
            string emp2 = DbHelper.NewId();
            string cc1 = DbHelper.NewId();
            try
            {
                InsertWarehouseRefRow(db, "DNHANVIEN", "DCALAMVIECID", emp1, shiftId, "REF-NV1-" + tag);
                InsertWarehouseRefRow(db, "DNHANVIEN", "DCALAMVIECID", emp2, shiftId, "REF-NV2-" + tag);
                InsertWarehouseRefRow(db, "TBANGLUONGCHITIET", "DCALAMVIECID", cc1, shiftId, "REF-CC1-" + tag);

                ShiftDeleteResult del = svc.Delete(shiftId);
                string msg = del.ErrorMessage ?? "";
                bool stillThere = repo.LoadById(shiftId) != null;
                bool hasNv = msg.IndexOf("2 nhân viên", StringComparison.Ordinal) >= 0;
                bool hasCc = msg.IndexOf("1 dòng chấm công", StringComparison.Ordinal) >= 0;

                Check("Nhân sự §5 #7 (chặn xóa ca còn NV & dòng chấm công): chặn xóa và nêu đúng '2 nhân viên, 1 dòng chấm công'",
                    !del.Ok && stillThere && del.ReferenceCount == 3 && hasNv && hasCc,
                    SingleLine("refs=" + del.ReferenceCount + ", msg=" + msg));
            }
            finally
            {
                SafeDeleteById(db, "TBANGLUONGCHITIET", cc1);
                SafeDeleteById(db, "DNHANVIEN", emp2);
                SafeDeleteById(db, "DNHANVIEN", emp1);
            }
        }

        private static void CheckShiftServicePermissions(string db, string existingId, string tag)
        {
            var permsView = new FunctionPermissions(false);
            permsView.Add("", ShiftText.FunctionName, RoleModes.View);
            var svcView = new ShiftService(new UserSession { UserId = "u_ca_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView });

            bool canRead = svcView.LoadAll().Count > 0;
            ShiftSaveResult addView = svcView.Save(new ShiftRow { Name = "Ca Chỉ Xem " + tag, SalaryRate = 100m });
            ShiftRow loaded = svcView.LoadById(existingId);
            ShiftSaveResult editView = loaded != null ? svcView.Save(loaded) : ShiftSaveResult.Fail("skip");
            ShiftDeleteResult delView = svcView.Delete(existingId);

            var svcNone = new ShiftService(new UserSession { UserId = "u_ca_none", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) });
            bool noneBlocked = svcNone.LoadAll().Count == 0 && svcNone.LoadById(existingId) == null;

            Check("Nhân sự §5 #7 (quyền tầng Services ca): chỉ Xem đọc được nhưng chặn Thêm/Sửa/Xóa; không quyền chặn cả đọc",
                canRead && !addView.Ok && addView.ErrorMessage == ShiftText.NoPermission
                && !editView.Ok && editView.ErrorMessage == ShiftText.NoPermission
                && !delView.Ok && delView.ErrorMessage == ShiftText.NoPermission
                && noneBlocked,
                "read=" + canRead + ", add=" + addView.Ok + ", edit=" + editView.Ok + ", del=" + delView.Ok + ", none=" + noneBlocked);
        }
    }
}
