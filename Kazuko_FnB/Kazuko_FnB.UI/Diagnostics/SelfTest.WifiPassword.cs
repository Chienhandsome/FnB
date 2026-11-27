using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void WifiPasswordTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_wf",
                    UserName = "selftest_wf",
                    DisplayName = "Selftest Wifi",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                WifiPasswordPureRuleTests();
                WifiPasswordGroupAndCrudDbTests(db);
                WifiPasswordUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void WifiPasswordPureRuleTests()
        {
            var valid = new WifiPasswordRow { Name = "KAZUKO-2026", Note = "Tầng 1" };
            Check("Mật khẩu wifi §5 #4 (luật thuần): mật khẩu hợp lệ qua Validate",
                WifiPasswordRules.ValidatePassword(valid, false).Ok);

            WifiPasswordSaveResult rEmpty = WifiPasswordRules.ValidatePassword(new WifiPasswordRow { Name = "   " }, false);
            WifiPasswordSaveResult rDup = WifiPasswordRules.ValidatePassword(valid, true);
            WifiPasswordSaveResult rName256 = WifiPasswordRules.ValidatePassword(new WifiPasswordRow { Name = new string('W', 256) }, false);
            WifiPasswordSaveResult rNote256 = WifiPasswordRules.ValidatePassword(new WifiPasswordRow { Name = "W1", Note = new string('N', 256) }, false);
            WifiGroupSaveResult gEmpty = WifiPasswordRules.ValidateGroup(new WifiGroupRow { Name = "" }, false);
            WifiGroupSaveResult gDup = WifiPasswordRules.ValidateGroup(new WifiGroupRow { Name = "Tầng 1" }, true);

            Check("Mật khẩu wifi §5 #4 (luật thuần): chặn mật khẩu/nhóm trống, trùng, quá 255 ký tự; ClipLogText cắt 255",
                !rEmpty.Ok && rEmpty.HasNameError
                && !rDup.Ok && rDup.HasNameError
                && !rName256.Ok && rName256.HasNameError
                && !rNote256.Ok && rNote256.HasNoteError
                && !gEmpty.Ok && !gDup.Ok
                && WifiPasswordRules.ClipLogText(new string('A', 300)).Length == WifiPasswordText.MaxTextLength,
                "empty=" + rEmpty.HasNameError + ", dup=" + rDup.HasNameError + ", n256=" + rName256.HasNameError
                + ", nt256=" + rNote256.HasNoteError + ", gE=" + !gEmpty.Ok + ", gD=" + !gDup.Ok);

            var permsView = new FunctionPermissions(false);
            permsView.Add("", WifiPasswordText.FunctionName, RoleModes.View);
            var sessView = new UserSession { UserId = "u_wf_v", IsAdmin = false, Permissions = permsView };
            var sessAdm = new UserSession { UserId = "u_wf_a", IsAdmin = true, Permissions = new FunctionPermissions(false) };
            Check("Mật khẩu wifi §5 #4 (quyền thuần): admin đủ quyền, chỉ Xem chỉ có View",
                WifiPasswordRules.Can(sessAdm, RoleModes.Remove)
                && WifiPasswordRules.Can(sessView, RoleModes.View)
                && !WifiPasswordRules.Can(sessView, RoleModes.Add)
                && !WifiPasswordRules.Can(sessView, RoleModes.Edit)
                && !WifiPasswordRules.Can(sessView, RoleModes.Remove));
        }

        private static void WifiPasswordGroupAndCrudDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });
            var svc = new WifiPasswordService(AppSession.Current);
            var grpSvc = new WifiGroupService(AppSession.Current);
            var repo = new WifiPasswordRepository(db);
            var grpRepo = new WifiGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);

            string parentId = null;
            string childId = null;
            string longGroupId = null;
            string emptyGrpId = null;
            string w1Id = null;
            string w255Id = null;

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                // 1. Nhóm cha / con: cột cây PARENTID, SORTORDER, PARENTDIR, ITEMTYPE, AUTOID; chặn trùng cùng cha, cho phép khác cha.
                var parent = WifiGroupRow.NewDefault();
                parent.Name = "Nhóm Wifi Cha " + tag;
                parent.Note = "Ghi chú cha";
                WifiGroupSaveResult pRes = grpSvc.Save(parent);
                parentId = pRes.SavedId;

                var dupSameParent = WifiGroupRow.NewDefault();
                dupSameParent.Name = "nhom wifi cha " + tag;
                WifiGroupSaveResult dupSameRes = grpSvc.Save(dupSameParent);

                var child = WifiGroupRow.NewDefault();
                child.Name = "Nhóm Wifi Cha " + tag; // cùng tên nhưng khác cha -> hợp lệ
                child.ParentId = parentId;
                WifiGroupSaveResult cRes = grpSvc.Save(child);
                childId = cRes.SavedId;

                var dupChild = WifiGroupRow.NewDefault();
                dupChild.Name = "NHOM WIFI CHA " + tag;
                dupChild.ParentId = parentId;
                WifiGroupSaveResult dupChildRes = grpSvc.Save(dupChild);

                WifiGroupRow childBack = childId == null ? null : grpRepo.LoadById(childId);
                bool treeColsOk = childBack != null
                    && childBack.Status == 30
                    && childBack.ParentId == parentId
                    && childBack.ParentDir == parentId + ":"
                    && childBack.AutoId > 0
                    && !string.IsNullOrEmpty(childBack.SortOrder);

                Check("Mật khẩu wifi §5 #4 (nhóm DB): thêm cha/con đúng cột cây, chặn trùng UNICODE_CI_AI trong cùng cha, cho phép khác cha",
                    pRes.Ok && !dupSameRes.Ok && cRes.Ok && !dupChildRes.Ok && treeColsOk,
                    "p=" + pRes.Ok + ", dupSame=" + !dupSameRes.Ok + ", c=" + cRes.Ok + ", dupC=" + !dupChildRes.Ok + ", tree=" + treeColsOk);

                // 2. Biên 255 ký tự cho nhóm và mật khẩu không tràn TLUUVET.
                string gName255 = ("WG255_" + tag + "_").PadRight(255, 'X');
                var longGrp = WifiGroupRow.NewDefault();
                longGrp.Name = gName255;
                WifiGroupSaveResult longGrpRes = grpSvc.Save(longGrp);
                longGroupId = longGrpRes.SavedId;

                string wName255 = ("WF255_" + tag + "_").PadRight(255, 'P');
                WifiPasswordSaveResult w255Res = svc.Save(new WifiPasswordRow
                {
                    Name = wName255,
                    GroupId = longGroupId,
                    Note = new string('G', 255)
                });
                w255Id = w255Res.SavedId;
                int w255Logs = CountActivityLogsForBill(db, w255Id);
                int g255Logs = CountActivityLogsForBill(db, longGroupId);
                Check("Mật khẩu wifi §5 #4 (biên 255 ký tự): lưu nhóm và mật khẩu dài 255 ký tự ghi TLUUVET không tràn",
                    longGrpRes.Ok && w255Res.Ok && w255Logs >= 1 && g255Logs >= 1,
                    "gOk=" + longGrpRes.Ok + ", wOk=" + w255Res.Ok + ", logs=" + w255Logs + "/" + g255Logs);

                // 3. Thêm mật khẩu hợp lệ, chặn trống và trùng khác hoa thường/dấu, sửa chính nó không báo trùng.
                var w1 = new WifiPasswordRow
                {
                    Name = "WIFI-ĐẸP-" + tag,
                    GroupId = childId,
                    Note = "Mật khẩu tầng 2"
                };
                WifiPasswordSaveResult w1Save = svc.Save(w1);
                w1Id = w1Save.SavedId;

                WifiPasswordSaveResult wEmpty = svc.Save(new WifiPasswordRow { Name = "  " });
                WifiPasswordSaveResult wDupAccent = svc.Save(new WifiPasswordRow { Name = "wifi-dep-" + tag, GroupId = parentId });

                WifiPasswordRow loaded1 = svc.LoadById(w1Id);
                loaded1.Note = "Đã sửa ghi chú";
                WifiPasswordSaveResult w1EditSelf = svc.Save(loaded1);

                // Chặn xóa nhóm còn mật khẩu và nhóm còn nhóm con.
                WifiGroupDeleteResult delChildBlocked = grpSvc.Delete(childId);
                WifiGroupDeleteResult delParentBlocked = grpSvc.Delete(parentId);

                // Xóa cứng mật khẩu -> thành công + TLUUVET; xóa nhóm rỗng -> thành công + TLUUVET.
                int logsBeforeDelW1 = CountActivityLogsForBill(db, w1Id);
                WifiPasswordDeleteResult delW1 = svc.Delete(w1Id);
                bool w1Gone = repo.LoadById(w1Id) == null;
                int logsAfterDelW1 = CountActivityLogsForBill(db, w1Id);
                w1Id = null;

                var emptyGrp = WifiGroupRow.NewDefault();
                emptyGrp.Name = "Nhóm Wifi Rỗng " + tag;
                emptyGrpId = grpSvc.Save(emptyGrp).SavedId;
                int logsBeforeDelG = CountActivityLogsForBill(db, emptyGrpId);
                WifiGroupDeleteResult delEmpty = grpSvc.Delete(emptyGrpId);
                bool emptyGone = grpRepo.LoadById(emptyGrpId) == null;
                int logsAfterDelG = CountActivityLogsForBill(db, emptyGrpId);
                emptyGrpId = null;

                Check("Mật khẩu wifi §5 #4 (CRUD + chặn xóa nhóm): thêm/sửa, chặn trống & trùng hoa thường/dấu, chặn xóa nhóm còn MK/nhóm con, xóa cứng MK & nhóm trống + TLUUVET",
                    w1Save.Ok && !wEmpty.Ok && !wDupAccent.Ok && w1EditSelf.Ok
                    && !delChildBlocked.Ok && delChildBlocked.PasswordCount == 1
                    && delChildBlocked.ErrorMessage.IndexOf("nhóm còn 1 mật khẩu", StringComparison.Ordinal) >= 0
                    && !delParentBlocked.Ok && delParentBlocked.ChildGroupCount == 1
                    && delParentBlocked.ErrorMessage.IndexOf("nhóm còn 1 nhóm con", StringComparison.Ordinal) >= 0
                    && delW1.Ok && w1Gone && logsAfterDelW1 == logsBeforeDelW1 + 1
                    && delEmpty.Ok && emptyGone && logsAfterDelG == logsBeforeDelG + 1,
                    SingleLine((delChildBlocked.ErrorMessage ?? "") + " | " + (delParentBlocked.ErrorMessage ?? "")));

                // 4. Rollback nguyên tử khi lỗi trước commit.
                var wRb = new WifiPasswordRow { Name = "WF-RB-" + tag };
                bool wThrew = false;
                try { svc.Save(wRb, delegate { throw new InvalidOperationException("sim-wf-rb"); }); }
                catch (InvalidOperationException) { wThrew = true; }
                bool wRbClean = wThrew && string.IsNullOrEmpty(wRb.Id) && !repo.IsNameTaken("WF-RB-" + tag, "");

                var gRb = WifiGroupRow.NewDefault();
                gRb.Name = "Nhóm WF RB " + tag;
                bool gThrew = false;
                try { grpSvc.Save(gRb, delegate { throw new InvalidOperationException("sim-wfg-rb"); }); }
                catch (InvalidOperationException) { gThrew = true; }
                bool gRbClean = gThrew && string.IsNullOrEmpty(gRb.Id) && !grpRepo.IsNameTaken("Nhóm WF RB " + tag, "", "");

                Check("Mật khẩu wifi §5 #4 (rollback transaction): lỗi trước commit hoàn tác toàn bộ bản ghi lẫn TLUUVET",
                    wRbClean && gRbClean,
                    "wRb=" + wRbClean + ", gRb=" + gRbClean);
            }
            finally
            {
                foreach (string wid in new[] { w1Id, w255Id })
                {
                    if (wid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", wid);
                    SafeDeleteById(db, "DMATKHAUWIFI", wid);
                }
                foreach (string gid in new[] { emptyGrpId, longGroupId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMWIFI", gid);
                }
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
