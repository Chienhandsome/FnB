using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void RepairNoticeTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_sc",
                    UserName = "selftest_sc",
                    DisplayName = "Selftest SuaChua",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                RepairNoticePureRuleTests();
                RepairNoticeServiceAndRepoTests(db);
                RepairNoticeUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void RepairNoticePureRuleTests()
        {
            var valid = new RepairNoticeRow
            {
                Name = "SC26/00001",
                Date = new DateTime(2026, 5, 10),
                RoomId = "R1",
                Content = "Hỏng điều hòa",
                IsFixed = false,
                IsUsable = true,
                Note = "Ghi chú"
            };
            Check("Thông báo sửa chữa §5 #1 (luật thuần): phiếu hợp lệ qua Validate, mặc định CONSUDUNGDUOC = true và DASUAXONG = false",
                RepairNoticeRules.Validate(valid).Ok
                && !RepairNoticeRow.NewDefault().IsFixed
                && RepairNoticeRow.NewDefault().IsUsable);

            RepairNoticeSaveResult rNoNum = RepairNoticeRules.Validate(new RepairNoticeRow { Name = "  ", RoomId = "R1", Content = "Hỏng đèn" });
            RepairNoticeSaveResult rNoRoom = RepairNoticeRules.Validate(new RepairNoticeRow { Name = "SC26/00001", RoomId = "", Content = "Hỏng đèn" });
            RepairNoticeSaveResult rNoContent = RepairNoticeRules.Validate(new RepairNoticeRow { Name = "SC26/00001", RoomId = "R1", Content = "   " });
            RepairNoticeSaveResult rContent256 = RepairNoticeRules.Validate(new RepairNoticeRow { Name = "SC26/00001", RoomId = "R1", Content = new string('C', 256) });
            RepairNoticeSaveResult rNote256 = RepairNoticeRules.Validate(new RepairNoticeRow { Name = "SC26/00001", RoomId = "R1", Content = "Hỏng đèn", Note = new string('N', 256) });
            RepairNoticeSaveResult rNum256 = RepairNoticeRules.Validate(new RepairNoticeRow { Name = new string('S', 256), RoomId = "R1", Content = "Hỏng đèn" });

            Check("Thông báo sửa chữa §5 #2 (luật thuần): chặn thiếu Số phiếu, thiếu Bàn, thiếu Nội dung, Nội dung/Ghi chú/Số phiếu 256 ký tự",
                !rNoNum.Ok && rNoNum.HasNumberError
                && !rNoRoom.Ok && rNoRoom.HasRoomError
                && !rNoContent.Ok && rNoContent.HasContentError
                && !rContent256.Ok && rContent256.HasContentError
                && !rNote256.Ok && rNote256.HasNoteError
                && !rNum256.Ok && rNum256.HasNumberError
                && RepairNoticeRules.Clip(new string('A', 300)).Length == RepairNoticeText.MaxTextLength,
                "noNum=" + rNoNum.HasNumberError + ", noRoom=" + rNoRoom.HasRoomError + ", noNd=" + rNoContent.HasContentError
                + ", nd256=" + rContent256.HasContentError + ", nt256=" + rNote256.HasNoteError);

            var permsView = new FunctionPermissions(false);
            permsView.Add("", RepairNoticeText.FunctionName, RoleModes.View);
            var sessView = new UserSession { UserId = "u_sc_v", IsAdmin = false, Permissions = permsView };
            var sessAdm = new UserSession { UserId = "u_sc_a", IsAdmin = true, Permissions = new FunctionPermissions(false) };
            Check("Thông báo sửa chữa §5 #3 (quyền thuần): admin đủ quyền, chỉ Xem chỉ có View và không có quyền đổi ngày",
                RepairNoticeRules.Can(sessAdm, RoleModes.Remove) && RepairNoticeRules.CanChangeDate(sessAdm)
                && RepairNoticeRules.Can(sessView, RoleModes.View) && !RepairNoticeRules.Can(sessView, RoleModes.Add)
                && !RepairNoticeRules.Can(sessView, RoleModes.Edit) && !RepairNoticeRules.Can(sessView, RoleModes.Remove)
                && !RepairNoticeRules.CanChangeDate(sessView));
        }

        private static void RepairNoticeServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });
            UserSession adminSess = AppSession.Current;
            var svc = new RepairNoticeService(adminSess);
            var repo = new RepairNoticeRepository(db);
            var tableSvc = new TableService(adminSess);

            string tag = DbHelper.NewId().Substring(0, 8);
            string areaId = DbHelper.NewId();
            string roomTypeId = DbHelper.NewId();
            string roomBId = DbHelper.NewId();
            string empId = DbHelper.NewId();
            var createdIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                BookingExec(db,
                    "INSERT INTO DKHUVUC (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_sc', @T)",
                    "@I", areaId, "@N", "Khu Vực SC " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DLOAIPHONG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_sc', @T)",
                    "@I", roomTypeId, "@N", "Loại VIP SC " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DBAN (ID, NAME, DKHUVUCID, DLOAIPHONGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @A, @LP, 30, 'selftest_sc', @T)",
                    "@I", roomBId, "@N", "Bàn SC " + tag, "@A", areaId, "@LP", roomTypeId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 30, 30, 'selftest_sc', @T)",
                    "@I", empId, "@N", "Nhân viên SC " + tag, "@T", DbHelper.Now());

                // §5 #1: Số phiếu tự sinh SC26/00001 rồi SC26/00002 (và kiểm trạng thái bàn Sửa chữa -> bình thường).
                DateTime date2026 = new DateTime(2026, 5, 10);
                int existing26 = BookingCount(db,
                    "SELECT COUNT(*) FROM TSUACHUA WHERE NAME STARTING WITH 'SC26/' AND COALESCE(STATUS, 0) <> -30");
                string auto1 = svc.GenerateNumber(date2026);
                int seq1 = DocumentNumber.ExtractSequence(auto1, "SC26/");

                ServiceTableInfo tableBefore = tableSvc.LoadTables(areaId).Find(t => t.Id == roomBId);
                bool beforeEmpty = tableBefore != null && tableBefore.Status == TableStatus.Empty;

                var row1 = RepairNoticeRow.NewDefault();
                row1.Name = auto1;
                row1.Date = date2026;
                row1.RoomId = roomBId;
                row1.RoomTypeId = roomTypeId;
                row1.EmployeeId = empId;
                row1.Content = "Hỏng máy lạnh " + tag;
                row1.Note = "Cần kiểm tra sớm";
                RepairNoticeSaveResult r1 = svc.Save(row1);
                if (r1.Ok) createdIds.Add(r1.SavedId);

                string auto2 = svc.GenerateNumber(date2026);
                int seq2 = DocumentNumber.ExtractSequence(auto2, "SC26/");
                bool exactSeqCheck = existing26 == 0
                    ? (auto1 == "SC26/00001" && auto2 == "SC26/00002")
                    : (auto1.StartsWith("SC26/", StringComparison.Ordinal) && seq1 >= 1 && seq2 == seq1 + 1);

                RepairNoticeRow back1 = r1.Ok ? repo.LoadById(r1.SavedId) : null;
                int rawFixed1 = r1.Ok ? BookingCount(db, "SELECT DASUAXONG FROM TSUACHUA WHERE ID = @I", "@I", r1.SavedId) : -1;
                int rawUsable1 = r1.Ok ? BookingCount(db, "SELECT CONSUDUNGDUOC FROM TSUACHUA WHERE ID = @I", "@I", r1.SavedId) : -1;
                int log1Count = r1.Ok ? CountActivityLogsForBill(db, r1.SavedId) : 0;

                // Kiểm bàn B chuyển sang UnderRepair ("Sửa chữa") bằng đúng hàm trạng thái bàn đã có.
                ServiceTableInfo tableUnderRepair = tableSvc.LoadTables(areaId).Find(t => t.Id == roomBId);
                bool isUnderRepair = tableUnderRepair != null
                    && tableUnderRepair.UnderRepairCount == 1
                    && tableUnderRepair.Status == TableStatus.UnderRepair;

                // Sửa phiếu thành Đã sửa xong -> DASUAXONG = 30, giữ người/giờ tạo, ghi người/giờ sửa, bàn B hết Sửa chữa.
                DateTime? origCreatedAt = back1 == null ? null : back1.CreatedAt;
                string origCreatedBy = back1 == null ? null : back1.CreatedById;
                back1.IsFixed = true;
                back1.IsUsable = false;
                back1.Note = "Đã thay tụ lạnh";
                RepairNoticeSaveResult rEditSelf = svc.Save(back1);
                RepairNoticeRow afterEdit = repo.LoadById(back1.Id);
                int rawFixed2 = BookingCount(db, "SELECT DASUAXONG FROM TSUACHUA WHERE ID = @I", "@I", back1.Id);
                int rawUsable2 = BookingCount(db, "SELECT CONSUDUNGDUOC FROM TSUACHUA WHERE ID = @I", "@I", back1.Id);
                ServiceTableInfo tableAfterFix = tableSvc.LoadTables(areaId).Find(t => t.Id == roomBId);
                bool backToEmpty = tableAfterFix != null
                    && tableAfterFix.UnderRepairCount == 0
                    && tableAfterFix.Status == TableStatus.Empty;

                Guid parsedId;
                Check("Thông báo sửa chữa §5 #1 (thêm/sửa, tự sinh SC26/00001->00002, cờ 0/30, bàn Sửa chữa -> bình thường): đạt",
                    beforeEmpty && r1.Ok && back1 != null && Guid.TryParse(back1.Id, out parsedId)
                    && back1.Status == 30 && rawFixed1 == 0 && rawUsable1 == 30
                    && back1.CreatedById == "selftest_sc" && back1.CreatedAt.HasValue && log1Count >= 1
                    && exactSeqCheck && isUnderRepair
                    && rEditSelf.Ok && afterEdit != null && afterEdit.IsFixed && !afterEdit.IsUsable
                    && rawFixed2 == 30 && rawUsable2 == 0
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_sc" && afterEdit.ModifiedAt.HasValue
                    && backToEmpty,
                    "a1=" + auto1 + ", a2=" + auto2 + ", underRep=" + isUnderRepair + ", afterFix=" + backToEmpty
                    + ", raw1=" + rawFixed1 + "/" + rawUsable1 + ", raw2=" + rawFixed2 + "/" + rawUsable2 + ", log=" + log1Count);

                // §5 #2: Chặn trùng số phiếu (khác hoa thường & dấu UNICODE_CI_AI), thiếu trường, quá 255; biên 255 không tràn TLUUVET.
                var rowAccent = RepairNoticeRow.NewDefault();
                rowAccent.Name = "SC-ĐẸP-" + tag;
                rowAccent.Date = date2026;
                rowAccent.RoomId = roomBId;
                rowAccent.Content = "Kiểm trùng số phiếu";
                RepairNoticeSaveResult rAccent = svc.Save(rowAccent);
                if (rAccent.Ok) createdIds.Add(rAccent.SavedId);

                RepairNoticeSaveResult rDupCase = svc.Save(new RepairNoticeRow
                {
                    Name = auto1.ToLowerInvariant(), Date = date2026, RoomId = roomBId, Content = "Trùng hoa thường"
                });
                RepairNoticeSaveResult rDupAccent = svc.Save(new RepairNoticeRow
                {
                    Name = "sc-dep-" + tag, Date = date2026, RoomId = roomBId, Content = "Trùng không dấu"
                });
                RepairNoticeSaveResult rMissingNum = svc.Save(new RepairNoticeRow { Name = "", Date = date2026, RoomId = roomBId, Content = "A" });
                RepairNoticeSaveResult rMissingRoom = svc.Save(new RepairNoticeRow { Name = "SC-NR-" + tag, Date = date2026, RoomId = "", Content = "A" });
                RepairNoticeSaveResult rMissingContent = svc.Save(new RepairNoticeRow { Name = "SC-NC-" + tag, Date = date2026, RoomId = roomBId, Content = "" });
                RepairNoticeSaveResult rContent256 = svc.Save(new RepairNoticeRow { Name = "SC-C256-" + tag, Date = date2026, RoomId = roomBId, Content = new string('X', 256) });
                RepairNoticeSaveResult rNote256 = svc.Save(new RepairNoticeRow { Name = "SC-N256-" + tag, Date = date2026, RoomId = roomBId, Content = "A", Note = new string('Y', 256) });

                string num255 = ("SC255-" + tag + "-").PadRight(255, 'A');
                var row255 = new RepairNoticeRow
                {
                    Name = num255,
                    Date = date2026,
                    RoomId = roomBId,
                    Content = new string('N', 255),
                    Note = new string('G', 255),
                    IsFixed = false,
                    IsUsable = true
                };
                RepairNoticeSaveResult r255 = svc.Save(row255);
                if (r255.Ok) createdIds.Add(r255.SavedId);
                int log255 = r255.Ok ? CountActivityLogsForBill(db, r255.SavedId) : 0;

                // Đọc cờ khác 0 (giá trị 1) vẫn hiểu là true.
                BookingExec(db, "UPDATE TSUACHUA SET DASUAXONG = 1, CONSUDUNGDUOC = 1 WHERE ID = @I", "@I", r255.SavedId);
                RepairNoticeRow nonZeroBack = repo.LoadById(r255.SavedId);
                bool nonZeroAsTrue = nonZeroBack != null && nonZeroBack.IsFixed && nonZeroBack.IsUsable;

                Check("Thông báo sửa chữa §5 #2 (chặn thiếu/trùng hoa thường & dấu, 256 ký tự; biên 255 không tràn TLUUVET; đọc cờ != 0 là Có): đạt",
                    rAccent.Ok && !rDupCase.Ok && rDupCase.HasNumberError && !rDupAccent.Ok && rDupAccent.HasNumberError
                    && !rMissingNum.Ok && !rMissingRoom.Ok && !rMissingContent.Ok
                    && !rContent256.Ok && !rNote256.Ok
                    && r255.Ok && log255 >= 1 && nonZeroAsTrue,
                    "dupC=" + !rDupCase.Ok + ", dupA=" + !rDupAccent.Ok + ", mNum=" + !rMissingNum.Ok
                    + ", mRoom=" + !rMissingRoom.Ok + ", mNd=" + !rMissingContent.Ok
                    + ", c256=" + !rContent256.Ok + ", n256=" + !rNote256.Ok + ", r255=" + r255.Ok + ", log255=" + log255 + ", nz=" + nonZeroAsTrue);

                // §5 #3: Xóa cứng có lưu vết & Rollback nguyên tử khi lỗi giữa transaction.
                int logsBeforeDel = CountActivityLogsForBill(db, rAccent.SavedId);
                RepairNoticeDeleteResult rDel = svc.Delete(rAccent.SavedId);
                bool delGone = repo.LoadById(rAccent.SavedId) == null;
                int logsAfterDel = CountActivityLogsForBill(db, rAccent.SavedId);

                bool rolledBack = false;
                var rbRow = RepairNoticeRow.NewDefault();
                rbRow.Name = "SC-RB-" + tag;
                rbRow.Date = date2026;
                rbRow.RoomId = roomBId;
                rbRow.Content = "Kiểm rollback";
                try
                {
                    svc.Save(rbRow, delegate { throw new InvalidOperationException("sim-sc-rollback"); });
                }
                catch (InvalidOperationException)
                {
                    rolledBack = BookingCount(db, "SELECT COUNT(*) FROM TSUACHUA WHERE NAME = @N", "@N", "SC-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbRow.Id);
                }

                // Tầng Service khóa ngày khi thiếu quyền "Thay đổi ngày trong các phiếu".
                var permsNoDate = new FunctionPermissions(false);
                permsNoDate.Add("", RepairNoticeText.FunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
                var svcNoDate = new RepairNoticeService(new UserSession
                {
                    UserId = "u_sc_nodate", UserName = "u_sc_nodate", IsAdmin = false, DatabasePath = db, Permissions = permsNoDate
                });
                var noDateRow = RepairNoticeRow.NewDefault();
                noDateRow.Name = "SC-ND-" + tag;
                noDateRow.Date = new DateTime(2020, 1, 1);
                noDateRow.RoomId = roomBId;
                noDateRow.Content = "Kiểm khóa ngày";
                RepairNoticeSaveResult rNoDate = svcNoDate.Save(noDateRow);
                if (rNoDate.Ok) createdIds.Add(rNoDate.SavedId);
                RepairNoticeRow noDateBack = rNoDate.Ok ? repo.LoadById(rNoDate.SavedId) : null;
                bool dateForcedToday = noDateBack != null && noDateBack.Date.Date == DateTime.Today;

                Check("Thông báo sửa chữa §5 #3 (xóa cứng + TLUUVET, rollback transaction, Service khóa ngày khi thiếu quyền): đạt",
                    rDel.Ok && delGone && logsAfterDel == logsBeforeDel + 1 && rolledBack && rNoDate.Ok && dateForcedToday,
                    "del=" + rDel.Ok + ", gone=" + delGone + ", dlog=" + (logsAfterDel - logsBeforeDel) + ", rb=" + rolledBack + ", dtLock=" + dateForcedToday);
            }
            finally
            {
                foreach (string id in createdIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", id);
                    SafeDeleteById(db, "TSUACHUA", id);
                }
                SafeDeleteById(db, "DBAN", roomBId);
                SafeDeleteById(db, "DLOAIPHONG", roomTypeId);
                SafeDeleteById(db, "DKHUVUC", areaId);
                SafeDeleteById(db, "DNHANVIEN", empId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
