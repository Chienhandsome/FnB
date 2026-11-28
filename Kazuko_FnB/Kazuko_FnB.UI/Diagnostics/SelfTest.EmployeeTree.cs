using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void EmployeeTreeTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                var adminSess = new UserSession
                {
                    UserId = "selftest_nvcay",
                    UserName = "selftest_nvcay",
                    DisplayName = "Selftest NhanVienCay",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };
                AppSession.Current = adminSess;

                EmployeeTreeRepoAndServiceTests(db, adminSess);
                EmployeeTreeUiTests(db, adminSess);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void EmployeeTreeRepoAndServiceTests(string db, UserSession adminSess)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { EmployeeText.ConfigUseTimekeeperKey, ActivityConstants.ConfigKey });
            var svc = new EmployeeService(adminSess);
            var repo = new EmployeeRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            string shiftId = DbHelper.NewId();
            string refVoucherId = DbHelper.NewId();
            string periodId = null;
            var createdIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);

                BookingExec(db,
                    "INSERT INTO DCALAMVIEC (ID, NAME, TILELUONG, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 100, 30, 'selftest_nvcay', @T)",
                    "@I", shiftId, "@N", "Ca Cay " + tag, "@T", DbHelper.Now());

                int maxAutoBefore = BookingCount(db, "SELECT COALESCE(MAX(AUTOID), 0) FROM DNHANVIEN");

                // §5 #1: Thêm thư mục gốc, thư mục con, phân cách, nhân viên trong thư mục con -> kiểm ITEMTYPE, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS.
                EmployeeRow folderRoot = EmployeeRow.NewFolder(null);
                folderRoot.Name = "Phòng Pha Chế " + tag;
                EmployeeSaveResult rFolder1 = svc.SaveFolder(folderRoot);
                if (rFolder1.Ok) createdIds.Add(rFolder1.SavedId);
                EmployeeRow f1 = rFolder1.Ok ? repo.LoadById(rFolder1.SavedId) : null;

                EmployeeRow folderSub = EmployeeRow.NewFolder(rFolder1.SavedId);
                folderSub.Name = "Ca Sáng " + tag;
                EmployeeSaveResult rFolder2 = svc.SaveFolder(folderSub);
                if (rFolder2.Ok) createdIds.Add(rFolder2.SavedId);
                EmployeeRow f2 = rFolder2.Ok ? repo.LoadById(rFolder2.SavedId) : null;

                EmployeeSaveResult rSep = svc.AddSeparator(rFolder1.SavedId);
                if (rSep.Ok) createdIds.Add(rSep.SavedId);
                EmployeeRow sep = rSep.Ok ? repo.LoadById(rSep.SavedId) : null;

                EmployeeRow empInSub = EmployeeRow.NewDefault();
                empInSub.Name = "NV Cây Con " + tag;
                empInSub.Code = "NVC01_" + tag;
                empInSub.ParentId = rFolder2.SavedId;
                empInSub.ShiftSalary = 210000m;
                EmployeeSaveResult rEmpSub = svc.Save(empInSub);
                if (rEmpSub.Ok) createdIds.Add(rEmpSub.SavedId);
                EmployeeRow eSub = rEmpSub.Ok ? repo.LoadById(rEmpSub.SavedId) : null;

                EmployeeRow empRoot = EmployeeRow.NewDefault();
                empRoot.Name = "NV Cây Gốc " + tag;
                empRoot.ParentId = "";
                EmployeeSaveResult rEmpRoot = svc.Save(empRoot);
                if (rEmpRoot.Ok) createdIds.Add(rEmpRoot.SavedId);
                EmployeeRow eRoot = rEmpRoot.Ok ? repo.LoadById(rEmpRoot.SavedId) : null;

                string expectedF2Dir = f1 == null ? "" : f1.AutoId.ToString(CultureInfo.InvariantCulture) + ":";
                string expectedSubEmpDir = f2 == null ? "" : expectedF2Dir + f2.AutoId.ToString(CultureInfo.InvariantCulture) + ":";
                bool c1Ok = rFolder1.Ok && f1 != null && f1.ItemType == 1 && f1.ParentId == "" && f1.ParentDir == ""
                    && f1.AutoId == maxAutoBefore + 1 && f1.Status == 30 && !string.IsNullOrEmpty(f1.SortOrder)
                    && rFolder2.Ok && f2 != null && f2.ItemType == 1 && f2.ParentId == f1.Id && f2.ParentDir == expectedF2Dir
                    && f2.AutoId == f1.AutoId + 1 && f2.Status == 30
                    && rSep.Ok && sep != null && sep.ItemType == 2 && sep.Name == EmployeeText.SeparatorName
                    && sep.ParentId == f1.Id && sep.ParentDir == expectedF2Dir && sep.AutoId == f2.AutoId + 1
                    && string.CompareOrdinal(sep.SortOrder, f2.SortOrder) > 0 && sep.Status == 30
                    && rEmpSub.Ok && eSub != null && !eSub.ItemType.HasValue && eSub.ParentId == f2.Id
                    && eSub.ParentDir == expectedSubEmpDir && eSub.AutoId == sep.AutoId + 1 && eSub.Status == 30
                    && rEmpRoot.Ok && eRoot != null && !eRoot.ItemType.HasValue && eRoot.ParentId == "" && eRoot.ParentDir == ""
                    && string.CompareOrdinal(eRoot.SortOrder, f1.SortOrder) > 0;

                Check("Nhân sự cây §5 #1 (thêm thư mục, thư mục con, phân cách, NV trong thư mục): đúng ITEMTYPE, PARENTID, PARENTDIR, SORTORDER, AUTOID, STATUS=30",
                    c1Ok,
                    SingleLine("f1=" + (f1 == null ? "null" : f1.ItemType + "/" + f1.AutoId + "/" + f1.SortOrder)
                        + ", f2=" + (f2 == null ? "null" : f2.ParentDir + "/" + f2.SortOrder)
                        + ", sep=" + (sep == null ? "null" : sep.ItemType + "/" + sep.SortOrder)
                        + ", eSub=" + (eSub == null ? "null" : (eSub.ItemType.HasValue ? eSub.ItemType.ToString() : "NULL") + "/" + eSub.ParentDir)));

                // §5 #4: Xóa mềm NV đang được dùng -> STATUS = 0, dữ liệu khác nguyên vẹn; thư mục còn mục bên trong bị chặn.
                EmployeeRow empUsed = EmployeeRow.NewDefault();
                empUsed.Name = "NV Đang Dùng " + tag;
                empUsed.Code = "NVUSED_" + tag;
                empUsed.Phone = "0909888777";
                empUsed.ParentId = f1.Id;
                empUsed.ShiftSalary = 300000m;
                EmployeeSaveResult rEmpUsed = svc.Save(empUsed);
                if (rEmpUsed.Ok) createdIds.Add(rEmpUsed.SavedId);
                EmployeeRow beforeSoft = repo.LoadById(rEmpUsed.SavedId);
                InsertWarehouseRefRow(db, "TTHUCHI", "DNHANVIENID", refVoucherId, rEmpUsed.SavedId, "TC-CAY-" + tag);

                EmployeeDeleteResult rSoftUsed = svc.SoftDelete(rEmpUsed.SavedId);
                EmployeeRow afterSoft = repo.LoadById(rEmpUsed.SavedId);
                bool untouched = afterSoft != null && beforeSoft != null
                    && afterSoft.Status == 0
                    && afterSoft.Name == beforeSoft.Name
                    && afterSoft.Code == beforeSoft.Code
                    && afterSoft.Phone == beforeSoft.Phone
                    && afterSoft.ParentId == beforeSoft.ParentId
                    && afterSoft.ParentDir == beforeSoft.ParentDir
                    && afterSoft.SortOrder == beforeSoft.SortOrder
                    && afterSoft.AutoId == beforeSoft.AutoId
                    && afterSoft.ItemType == beforeSoft.ItemType
                    && afterSoft.ShiftSalary == beforeSoft.ShiftSalary
                    && afterSoft.ModifiedById == beforeSoft.ModifiedById
                    && afterSoft.ModifiedAt == beforeSoft.ModifiedAt;

                // Thư mục f1 còn f2, sep, eSub -> chặn xóa mềm nêu rõ số mục bên trong (3 mục).
                EmployeeDeleteResult rSoftNonEmpty = svc.SoftDelete(f1.Id);
                bool blockedMsgOk = !rSoftNonEmpty.Ok && rSoftNonEmpty.ReferenceCount == 3
                    && (rSoftNonEmpty.ErrorMessage ?? "").IndexOf("Thư mục còn 3 mục bên trong", StringComparison.Ordinal) >= 0;

                // Xóa mềm phân cách thành công.
                EmployeeDeleteResult rSoftSep = svc.SoftDelete(sep.Id);
                EmployeeRow sepAfterSoft = repo.LoadById(sep.Id);

                Check("Nhân sự cây §5 #4 (xóa mềm): STATUS=0 giữ nguyên cột khác, NV đang dùng vẫn vào Thùng rác, thư mục không rỗng bị chặn đúng số lượng",
                    rSoftUsed.Ok && untouched && blockedMsgOk && rSoftSep.Ok && sepAfterSoft != null && sepAfterSoft.Status == 0,
                    SingleLine("softUsed=" + rSoftUsed.Ok + ", unt=" + untouched + ", blk=" + blockedMsgOk + ", msg=" + rSoftNonEmpty.ErrorMessage));

                // §5 #3: Mọi truy vấn chọn/lọc nhân viên ở nơi khác KHÔNG trả thư mục, phân cách, NV STATUS=0.
                periodId = CheckAllOtherEmployeeLookups(db, adminSess, eSub.Id, f1.Id, sep.Id, rEmpUsed.SavedId, tag);

                // §5 #6: Trùng Tên/Mã không tính NV trong Thùng rác; trùng tên thư mục anh em bị chặn (không phân biệt hoa thường/dấu), khác cha cho phép.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 30);
                EmployeeRow dupOfTrashed = EmployeeRow.NewDefault();
                dupOfTrashed.Name = "nv dang dung " + tag; // trùng không dấu với empUsed đang ở STATUS = 0
                dupOfTrashed.Code = "nvused_" + tag;      // trùng mã với empUsed đang ở STATUS = 0
                dupOfTrashed.ShiftId = shiftId;
                EmployeeSaveResult rReuseTrashed = svc.Save(dupOfTrashed);
                if (rReuseTrashed.Ok) createdIds.Add(rReuseTrashed.SavedId);

                EmployeeRow dupSiblingFolder = EmployeeRow.NewFolder(null);
                dupSiblingFolder.Name = "phong pha che " + tag; // trùng anh em cùng gốc với f1
                EmployeeSaveResult rDupSib = svc.SaveFolder(dupSiblingFolder);

                EmployeeRow sameNameOtherParent = EmployeeRow.NewFolder(f1.Id);
                sameNameOtherParent.Name = "Phòng Pha Chế " + tag; // khác cha -> cho phép
                EmployeeSaveResult rOtherParent = svc.SaveFolder(sameNameOtherParent);
                if (rOtherParent.Ok) createdIds.Add(rOtherParent.SavedId);

                Check("Nhân sự cây §5 #6 (trùng Tên/Mã bỏ qua Thùng rác & chặn trùng tên thư mục anh em): đạt",
                    rReuseTrashed.Ok && !rDupSib.Ok && rOtherParent.Ok,
                    SingleLine("reuse=" + rReuseTrashed.Ok + ", dupSib=" + rDupSib.Ok + ", otherP=" + rOtherParent.Ok));

                // §5 #5: Thùng rác — chặn khôi phục khi trùng Tên/Mã; khôi phục đúng vị trí cũ; khôi phục lên gốc khi cha vào Thùng rác hoặc bị xóa hẳn; xóa vĩnh viễn.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, EmployeeText.ConfigUseTimekeeperKey, 0);

                // Khôi phục empUsed bị chặn vì dupOfTrashed đang dùng (STATUS = 30) có cùng Tên.
                EmployeeRestoreResult rRestoreDupName = svc.Restore(rEmpUsed.SavedId);

                // Đổi tên dupOfTrashed sang tên khác nhưng vẫn giữ cùng Mã -> khôi phục empUsed vẫn bị chặn vì trùng Mã.
                EmployeeRow editDupForCode = repo.LoadById(rReuseTrashed.SavedId);
                editDupForCode.Name = "NV Khác Tên Cùng Mã " + tag;
                svc.Save(editDupForCode);
                EmployeeRestoreResult rRestoreDupCode = svc.Restore(rEmpUsed.SavedId);

                // Xóa hẳn dupOfTrashed (chưa dùng ở bảng nào) -> được; sau đó khôi phục empUsed về đúng thư mục f1.
                EmployeeDeleteResult rDelDup = svc.Delete(rReuseTrashed.SavedId);
                EmployeeRestoreResult rRestoreBack = svc.Restore(rEmpUsed.SavedId);
                EmployeeRow empRestored = repo.LoadById(rEmpUsed.SavedId);

                // Xóa vĩnh viễn NV đang có TTHUCHI bị chặn và nêu '1 phiếu thu chi'.
                EmployeeDeleteResult rHardUsed = svc.Delete(rEmpUsed.SavedId);

                // Thử khôi phục lên gốc khi thư mục cha vào Thùng rác / bị xóa hẳn.
                EmployeeRow tempFolder = EmployeeRow.NewFolder(null);
                tempFolder.Name = "Thư Mục Tạm " + tag;
                EmployeeSaveResult rTmpF = svc.SaveFolder(tempFolder);
                if (rTmpF.Ok) createdIds.Add(rTmpF.SavedId);

                EmployeeRow empInTmp = EmployeeRow.NewDefault();
                empInTmp.Name = "NV Trong Tạm " + tag;
                empInTmp.ParentId = rTmpF.SavedId;
                EmployeeSaveResult rEmpTmp = svc.Save(empInTmp);
                if (rEmpTmp.Ok) createdIds.Add(rEmpTmp.SavedId);

                // Xóa mềm NV -> thư mục tạm thành rỗng -> xóa mềm thư mục tạm -> khôi phục NV -> phải lên gốc!
                EmployeeDeleteResult rSoftEmpTmp = svc.SoftDelete(rEmpTmp.SavedId);
                EmployeeDeleteResult rSoftTmpF = svc.SoftDelete(rTmpF.SavedId);
                EmployeeRestoreResult rRestoreToRoot = svc.Restore(rEmpTmp.SavedId);
                EmployeeRow empTmpAfter = repo.LoadById(rEmpTmp.SavedId);

                // Xóa vĩnh viễn thư mục tạm (đang ở Thùng rác, rỗng) và phân cách (đang ở Thùng rác) -> thành công.
                EmployeeDeleteResult rHardFolder = svc.Delete(rTmpF.SavedId);
                EmployeeDeleteResult rHardSep = svc.Delete(sep.Id);

                // Kiểm quyền: không có quyền Sửa thì chặn Khôi phục; không có quyền Xóa thì chặn SoftDelete & LoadTrash.
                var permsReadOnly = new FunctionPermissions(false);
                permsReadOnly.Add("", EmployeeText.FunctionName, RoleModes.View);
                var svcRo = new EmployeeService(new UserSession
                {
                    UserId = "u_cay_ro", IsAdmin = false, DatabasePath = db, Permissions = permsReadOnly
                });
                EmployeeRestoreResult rRestoreNoPerm = svcRo.Restore(rEmpSub.SavedId);
                EmployeeDeleteResult rSoftNoPerm = svcRo.SoftDelete(rEmpSub.SavedId);

                Check("Nhân sự cây §5 #5 (Thùng rác khôi phục vị trí cũ, khôi phục lên gốc khi cha mất, chặn trùng Tên/Mã, xóa vĩnh viễn chặn NV đang dùng & xóa thư mục rỗng/phân cách): đạt",
                    !rRestoreDupName.Ok && !rRestoreDupCode.Ok
                    && rDelDup.Ok && rRestoreBack.Ok && !rRestoreBack.RestoredToRoot
                    && empRestored != null && empRestored.Status == 30 && empRestored.ParentId == f1.Id
                    && !rHardUsed.Ok && (rHardUsed.ErrorMessage ?? "").IndexOf("1 phiếu thu chi", StringComparison.Ordinal) >= 0
                    && rSoftEmpTmp.Ok && rSoftTmpF.Ok && rRestoreToRoot.Ok && rRestoreToRoot.RestoredToRoot
                    && empTmpAfter != null && empTmpAfter.Status == 30 && empTmpAfter.ParentId == "" && empTmpAfter.ParentDir == ""
                    && rHardFolder.Ok && repo.LoadById(rTmpF.SavedId) == null
                    && rHardSep.Ok && repo.LoadById(sep.Id) == null
                    && !rRestoreNoPerm.Ok && rRestoreNoPerm.ErrorMessage == EmployeeText.NoPermission
                    && !rSoftNoPerm.Ok && rSoftNoPerm.ErrorMessage == EmployeeText.NoPermission,
                    SingleLine("dupN=" + !rRestoreDupName.Ok + ", dupC=" + !rRestoreDupCode.Ok + ", back=" + rRestoreBack.Ok + ", hardUsed=" + !rHardUsed.Ok
                        + ", toRoot=" + rRestoreToRoot.RestoredToRoot + ", hF=" + rHardFolder.Ok + ", hS=" + rHardSep.Ok));
            }
            finally
            {
                SafeDeleteById(db, "TTHUCHI", refVoucherId);
                if (!string.IsNullOrEmpty(periodId))
                {
                    BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                    BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", periodId);
                    SafeDeleteById(db, "TBANGLUONG", periodId);
                }
                foreach (string eid in createdIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", eid);
                    SafeDeleteById(db, "DNHANVIEN", eid);
                }
                SafeDeleteById(db, "DCALAMVIEC", shiftId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static string CheckAllOtherEmployeeLookups(
            string db, UserSession adminSess, string activeEmpId, string folderId, string sepId, string trashedEmpId, string tag)
        {
            Func<IEnumerable<string>, bool> onlyValid = ids =>
            {
                bool hasActive = false;
                foreach (string id in ids)
                {
                    if (id == folderId || id == sepId || id == trashedEmpId)
                        return false;
                    if (id == activeEmpId)
                        hasActive = true;
                }
                return hasActive;
            };

            bool okEmpRepo = onlyValid(new EmployeeRepository(db).LoadAll().ConvertAll(x => x.Id));
            bool okFormRepo = onlyValid(new FormRepository(db).LoadAll("DNHANVIEN").ConvertAll(x => x.Id));
            bool okRp = onlyValid(new RewardPenaltyRepository(db).LoadActiveEmployees().ConvertAll(x => x.Id));
            bool okAdv = onlyValid(new SalaryAdvanceRepository(db).LoadActiveEmployees().ConvertAll(x => x.Id));
            bool okRn = onlyValid(new RepairNoticeRepository(db).LoadActiveEmployees().ConvertAll(x => x.Id));
            bool okSp = onlyValid(new ServicePaymentRepository(db).LoadEmployees().ConvertAll(x => x.Id));
            bool okUsr = onlyValid(new UserAdminRepository(db).LoadEmployees().ConvertAll(x => x.Key));

            string empTableId = new CatalogRepository(db).FindIdByTable(EmployeeText.TableName);
            bool okCfg = string.IsNullOrEmpty(empTableId)
                || onlyValid(new ConfigRepository(db).LoadLookup(empTableId, null).ConvertAll(x => x.Key));

            // Kiểm tra PayrollRepository (Tính lương & Chấm công) cũng không trả thư mục/phân cách/NV STATUS=0.
            var payrollSvc = new PayrollService(adminSess, PayrollMode.Payroll);
            PayrollCreateResult pr = payrollSvc.CreatePeriod(2, 2099, "Selftest cây " + tag);
            string periodId = pr.Ok && pr.Created != null ? pr.Created.Id : null;
            bool okPayroll = true;
            if (!string.IsNullOrEmpty(periodId))
            {
                var pRepo = new PayrollRepository(db);
                PayrollSheetData shPay = pRepo.LoadSheetForPayroll(periodId);
                PayrollSheetData shAtt = pRepo.LoadSheetForAttendance(periodId);
                okPayroll = shPay != null && onlyValid(shPay.Employees.ConvertAll(x => x.EmployeeId))
                    && shAtt != null && onlyValid(shAtt.Employees.ConvertAll(x => x.EmployeeId));
            }

            // Kiểm tra các hàm EmployeeExistsIn / LoadActiveEmployeeNameIn chặn thư mục, phân cách, NV STATUS=0.
            bool okValidateRefs = true;
            DbHelper.Read(db, conn =>
            {
                okValidateRefs = RewardPenaltyRepository.EmployeeExistsIn(conn, null, activeEmpId)
                    && !RewardPenaltyRepository.EmployeeExistsIn(conn, null, folderId)
                    && !RewardPenaltyRepository.EmployeeExistsIn(conn, null, sepId)
                    && !RewardPenaltyRepository.EmployeeExistsIn(conn, null, trashedEmpId)
                    && SalaryAdvanceRepository.LoadActiveEmployeeNameIn(conn, null, activeEmpId) != null
                    && SalaryAdvanceRepository.LoadActiveEmployeeNameIn(conn, null, folderId) == null
                    && SalaryAdvanceRepository.LoadActiveEmployeeNameIn(conn, null, sepId) == null
                    && SalaryAdvanceRepository.LoadActiveEmployeeNameIn(conn, null, trashedEmpId) == null;
                return 0;
            });

            Check("Nhân sự cây §5 #3 (mọi truy vấn chọn/lọc NV ở nơi khác không trả thư mục, phân cách, NV STATUS=0): đạt trên 9 nguồn",
                okEmpRepo && okFormRepo && okRp && okAdv && okRn && okSp && okUsr && okCfg && okPayroll && okValidateRefs,
                SingleLine("emp=" + okEmpRepo + ", form=" + okFormRepo + ", rp=" + okRp + ", adv=" + okAdv
                    + ", rn=" + okRn + ", sp=" + okSp + ", usr=" + okUsr + ", cfg=" + okCfg
                    + ", pay=" + okPayroll + ", ref=" + okValidateRefs));

            return periodId;
        }
    }
}
