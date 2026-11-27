using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void RewardPenaltyTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tp",
                    UserName = "selftest_tp",
                    DisplayName = "Selftest ThuongPhat",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                RewardPenaltyRulesPureTests();
                RewardPenaltyServiceAndRepoTests(db);
                RewardPenaltyUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void RewardPenaltyRulesPureTests()
        {
            var valid = new RewardPenaltyRow { Name = "TP26/00001", Date = new DateTime(2026, 3, 15), EmployeeId = "E1", Bonus = 200000m, Penalty = 0m };
            Check("Thưởng phạt §4 #1 (luật thuần): phiếu hợp lệ qua Validate",
                RewardPenaltyRules.Validate(valid).Ok);

            RewardPenaltySaveResult rNoNum = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = " ", EmployeeId = "E1", Bonus = 1000m });
            RewardPenaltySaveResult rNoEmp = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "", Bonus = 1000m });
            RewardPenaltySaveResult rZero = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "E1", Bonus = 0m, Penalty = 0m });
            RewardPenaltySaveResult rNegB = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "E1", Bonus = -1m, Penalty = 1000m });
            RewardPenaltySaveResult rNegP = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "E1", Bonus = 1000m, Penalty = -5m });
            RewardPenaltySaveResult rBig = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "E1", Bonus = 1000000000m });
            RewardPenaltySaveResult rNote256 = RewardPenaltyRules.Validate(new RewardPenaltyRow { Name = "TP01", EmployeeId = "E1", Bonus = 1000m, Note = new string('x', 256) });

            Check("Thưởng phạt §4 #2 (luật thuần): chặn thiếu số phiếu, thiếu NV, thưởng=phạt=0, số âm, quá 9 chữ số, ghi chú 256 ký tự",
                !rNoNum.Ok && rNoNum.HasNumberError
                && !rNoEmp.Ok && rNoEmp.HasEmployeeError
                && !rZero.Ok && rZero.ErrorMessage == RewardPenaltyText.AmountRequired
                && !rNegB.Ok && !rNegP.Ok && !rBig.Ok && rBig.HasAmountError
                && !rNote256.Ok && rNote256.HasNoteError,
                "noNum=" + rNoNum.HasNumberError + ", noEmp=" + rNoEmp.HasEmployeeError
                + ", zeroMsg=" + SingleLine(rZero.ErrorMessage) + ", negB=" + !rNegB.Ok + ", big=" + rBig.HasAmountError + ", n256=" + rNote256.HasNoteError);

            var limit = new RewardPenaltyMonthLimit(3, 2004);
            RewardPenaltySaveResult rOut = RewardPenaltyRules.Validate(
                new RewardPenaltyRow { Name = "TP04/00001", Date = new DateTime(2004, 4, 5), EmployeeId = "E1", Bonus = 10000m }, limit);
            RewardPenaltySaveResult rIn = RewardPenaltyRules.Validate(
                new RewardPenaltyRow { Name = "TP04/00002", Date = new DateTime(2004, 3, 15), EmployeeId = "E1", Bonus = 10000m }, limit);
            Check("Thưởng phạt §4 #3 (luật thuần giới hạn tháng 03/2004): 05/04/2004 bị chặn nêu tháng 3 năm 2004, 15/03/2004 đạt",
                !rOut.Ok && rOut.HasDateError && rOut.ErrorMessage == "Ngày phải nằm trong tháng 3 năm 2004" && rIn.Ok,
                "outMsg=" + SingleLine(rOut.ErrorMessage) + ", inOk=" + rIn.Ok);

            var permsView = new FunctionPermissions(false);
            permsView.Add("", RewardPenaltyText.FunctionName, RoleModes.View);
            var sessView = new UserSession { UserId = "u_tp_v", IsAdmin = false, Permissions = permsView };
            var sessAdm = new UserSession { UserId = "u_tp_a", IsAdmin = true, Permissions = new FunctionPermissions(false) };
            Check("Thưởng phạt §4 #5 (quyền thuần): admin đủ quyền, chỉ Xem chỉ có View và không có đổi ngày",
                RewardPenaltyRules.Can(sessAdm, RoleModes.Remove) && RewardPenaltyRules.CanChangeDate(sessAdm)
                && RewardPenaltyRules.Can(sessView, RoleModes.View) && !RewardPenaltyRules.Can(sessView, RoleModes.Add)
                && !RewardPenaltyRules.Can(sessView, RoleModes.Edit) && !RewardPenaltyRules.Can(sessView, RoleModes.Remove)
                && !RewardPenaltyRules.CanChangeDate(sessView));
        }

        private static void RewardPenaltyServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });
            var adminSess = new UserSession
            {
                UserId = "selftest_tp",
                UserName = "selftest_tp",
                DisplayName = "Selftest ThuongPhat",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSess;
            var svc = new RewardPenaltyService(adminSess);
            var repo = new RewardPenaltyRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            string empAId = DbHelper.NewId();
            string reasonId = DbHelper.NewId();
            var createdIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 30, 30, 'selftest_tp', @T)",
                    "@I", empAId, "@N", "Nguyễn Văn Thưởng " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DLYDOTHUONGPHAT (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_tp', @T)",
                    "@I", reasonId, "@N", "Thưởng doanh số " + tag, "@T", DbHelper.Now());

                // #1: Thêm thưởng 200.000, số tự sinh liên tiếp TPyy/00001 -> TPyy/00002, phạt 50.000, cả hai 30.000/10.000.
                DateTime testYearDate = new DateTime(2026, 5, 10);
                string expectedPrefix = "TP26/";
                string auto1 = svc.GenerateNumber(testYearDate);
                var row1 = new RewardPenaltyRow
                {
                    Name = auto1,
                    Date = testYearDate,
                    EmployeeId = empAId,
                    Bonus = 200000m,
                    Penalty = 0m,
                    ReasonId = reasonId,
                    Note = "Thưởng tháng 5 " + tag
                };
                RewardPenaltySaveResult r1 = svc.Save(row1);
                if (r1.Ok) createdIds.Add(r1.SavedId);
                RewardPenaltyRow back1 = r1.Ok ? repo.LoadById(r1.SavedId) : null;

                string auto2 = svc.GenerateNumber(testYearDate);
                int seq1 = DocumentNumber.ExtractSequence(auto1, expectedPrefix);
                int seq2 = DocumentNumber.ExtractSequence(auto2, expectedPrefix);

                var row2 = new RewardPenaltyRow
                {
                    Name = auto2,
                    Date = testYearDate,
                    EmployeeId = empAId,
                    Bonus = 0m,
                    Penalty = 50000m,
                    Note = "Phạt đi muộn " + tag
                };
                RewardPenaltySaveResult r2 = svc.Save(row2);
                if (r2.Ok) createdIds.Add(r2.SavedId);
                RewardPenaltyRow back2 = r2.Ok ? repo.LoadById(r2.SavedId) : null;

                var row3 = new RewardPenaltyRow
                {
                    Name = svc.GenerateNumber(testYearDate),
                    Date = testYearDate,
                    EmployeeId = empAId,
                    Bonus = 30000m,
                    Penalty = 10000m,
                    Note = "Vừa thưởng vừa phạt " + tag
                };
                RewardPenaltySaveResult r3 = svc.Save(row3);
                if (r3.Ok) createdIds.Add(r3.SavedId);
                RewardPenaltyRow back3 = r3.Ok ? repo.LoadById(r3.SavedId) : null;

                Guid parsedId;
                int addLogs = r1.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", r1.SavedId, "@F", RewardPenaltyText.FunctionName)
                    : 0;

                Check("Thưởng phạt §4 #1 (thêm thưởng 200k, phạt 50k, cả hai 30k/10k, số phiếu tự sinh TP26 liên tiếp): lưu khớp TTHUONGPHAT và lưu vết",
                    r1.Ok && back1 != null && Guid.TryParse(back1.Id, out parsedId)
                    && back1.Bonus == 200000m && back1.Penalty == 0m && back1.Status == 30
                    && back1.CreatedById == "selftest_tp" && back1.CreatedAt.HasValue && addLogs >= 1
                    && auto1.StartsWith(expectedPrefix, StringComparison.Ordinal) && seq1 >= 1 && seq2 == seq1 + 1
                    && r2.Ok && back2 != null && back2.Bonus == 0m && back2.Penalty == 50000m
                    && r3.Ok && back3 != null && back3.Bonus == 30000m && back3.Penalty == 10000m,
                    "a1=" + auto1 + ", a2=" + auto2 + ", r1=" + r1.Ok + ", r2=" + r2.Ok + ", r3=" + r3.Ok + ", log=" + addLogs);

                // #2: Chặn trùng số phiếu khác hoa/thường, thiếu NV, bằng 0, âm, >9 số, 256 ký tự; biên 255 ký tự không tràn lưu vết.
                RewardPenaltySaveResult rDup = svc.Save(new RewardPenaltyRow { Name = auto1.ToLowerInvariant(), Date = testYearDate, EmployeeId = empAId, Bonus = 10000m });
                RewardPenaltySaveResult rNoEmp = svc.Save(new RewardPenaltyRow { Name = "TP-NOEMP-" + tag, Date = testYearDate, EmployeeId = "", Bonus = 10000m });
                RewardPenaltySaveResult rZero = svc.Save(new RewardPenaltyRow { Name = "TP-ZERO-" + tag, Date = testYearDate, EmployeeId = empAId, Bonus = 0m, Penalty = 0m });
                RewardPenaltySaveResult rNeg = svc.Save(new RewardPenaltyRow { Name = "TP-NEG-" + tag, Date = testYearDate, EmployeeId = empAId, Bonus = -100m });
                RewardPenaltySaveResult rBig = svc.Save(new RewardPenaltyRow { Name = "TP-BIG-" + tag, Date = testYearDate, EmployeeId = empAId, Penalty = 1000000000m });
                RewardPenaltySaveResult r256 = svc.Save(new RewardPenaltyRow { Name = "TP-256-" + tag, Date = testYearDate, EmployeeId = empAId, Bonus = 1000m, Note = new string('G', 256) });

                string name255 = "TP255-" + tag + "-" + new string('A', 255 - 7 - tag.Length);
                var row255 = new RewardPenaltyRow
                {
                    Name = name255,
                    Date = testYearDate,
                    EmployeeId = empAId,
                    Bonus = 15000m,
                    Note = new string('N', 255)
                };
                RewardPenaltySaveResult r255 = svc.Save(row255);
                if (r255.Ok) createdIds.Add(r255.SavedId);
                int log255 = r255.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", r255.SavedId)
                    : 0;

                Check("Thưởng phạt §4 #2 (chặn trùng số không phân biệt hoa thường, thiếu NV, 0, âm, >9 số, 256; biên 255 không tràn TLUUVET): đạt",
                    !rDup.Ok && rDup.HasNumberError && !rNoEmp.Ok && !rZero.Ok && !rNeg.Ok && !rBig.Ok && !r256.Ok
                    && r255.Ok && log255 >= 1,
                    "dup=" + rDup.Ok + ", noEmp=" + rNoEmp.Ok + ", zero=" + rZero.Ok + ", neg=" + rNeg.Ok
                    + ", big=" + rBig.Ok + ", n256=" + r256.Ok + ", n255=" + r255.Ok + ", log255=" + log255);

                // #3: Giới hạn tháng 03/2004 trên Service.
                var limit0304 = new RewardPenaltyMonthLimit(3, 2004);
                RewardPenaltySaveResult rOut = svc.Save(new RewardPenaltyRow
                {
                    Name = "TP04-OUT-" + tag, Date = new DateTime(2004, 4, 5), EmployeeId = empAId, Bonus = 20000m
                }, limit0304);
                var rowIn = new RewardPenaltyRow
                {
                    Name = "TP04-IN-" + tag, Date = new DateTime(2004, 3, 15), EmployeeId = empAId, Bonus = 20000m
                };
                RewardPenaltySaveResult rIn = svc.Save(rowIn, limit0304);
                if (rIn.Ok) createdIds.Add(rIn.SavedId);
                Check("Thưởng phạt §4 #3 (giới hạn tháng 03/2004 tầng Service): chặn 05/04/2004 và lưu được 15/03/2004",
                    !rOut.Ok && rOut.ErrorMessage == "Ngày phải nằm trong tháng 3 năm 2004"
                    && rIn.Ok && repo.LoadById(rIn.SavedId) != null,
                    "out=" + SingleLine(rOut.ErrorMessage) + ", in=" + rIn.Ok);

                // #5: Sửa giữ người/giờ tạo, ghi người/giờ sửa; xóa cứng có lưu vết; rollback khi lỗi giữa transaction.
                DateTime? origCreatedAt = back1.CreatedAt;
                string origCreatedBy = back1.CreatedById;
                back1.Bonus = 250000m;
                back1.Note = "Đã sửa thưởng " + tag;
                RewardPenaltySaveResult rEdit = svc.Save(back1);
                RewardPenaltyRow afterEdit = repo.LoadById(back1.Id);

                int logsBeforeDel = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", back2.Id);
                RewardPenaltyDeleteResult rDel = svc.Delete(back2.Id);
                bool delGone = repo.LoadById(back2.Id) == null;
                int logsAfterDel = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", back2.Id);

                bool rolledBack = false;
                var rbRow = new RewardPenaltyRow { Name = "TP-RB-" + tag, Date = testYearDate, EmployeeId = empAId, Bonus = 11111m };
                try
                {
                    svc.Save(rbRow, null, delegate { throw new InvalidOperationException("Lỗi giả lập thưởng phạt"); });
                }
                catch (InvalidOperationException)
                {
                    rolledBack = BookingCount(db, "SELECT COUNT(*) FROM TTHUONGPHAT WHERE NAME = @N", "@N", "TP-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbRow.Id);
                }

                Check("Thưởng phạt §4 #5 (sửa giữ tạo/ghi sửa, xóa cứng + TLUUVET, rollback transaction): đạt",
                    rEdit.Ok && afterEdit != null && afterEdit.Bonus == 250000m
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_tp" && afterEdit.ModifiedAt.HasValue
                    && rDel.Ok && delGone && logsAfterDel == logsBeforeDel + 1 && rolledBack,
                    "edit=" + rEdit.Ok + ", del=" + rDel.Ok + ", gone=" + delGone + ", dlog=" + (logsAfterDel - logsBeforeDel) + ", rb=" + rolledBack);
            }
            finally
            {
                foreach (string id in createdIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", id);
                    SafeDeleteById(db, "TTHUONGPHAT", id);
                }
                SafeDeleteById(db, "DLYDOTHUONGPHAT", reasonId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
