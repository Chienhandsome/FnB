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
        private static void SupplierTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_ncc",
                    UserName = "selftest_ncc",
                    DisplayName = "Selftest NCC",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                SupplierRulesPureTests();
                SupplierServiceAndRepoTests(db);
                SupplierUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void SupplierRulesPureTests()
        {
            // AC2 (Q2): sinh mã tự động NCC + 5 chữ số; bảng trống -> NCC00001; bỏ qua mã nhập tay khác dạng.
            Check("NCC §7/Q2 (AC2 sinh mã thuần): trống->NCC00001, sau NCC00005 và mã tay 001/SUP99 vẫn ra NCC00006, không phân biệt hoa thường",
                SupplierRules.NextCode(new string[0]) == "NCC00001"
                && SupplierRules.NextCode(new[] { "NCC00001", "NCC00005", "001", "SUP99", "NCC-A" }) == "NCC00006"
                && SupplierRules.NextCode(new[] { "ncc00012" }) == "NCC00013");

            // AC3 (Q2): so trùng collation UNICODE_CI_AI, bỏ hoa/thường & dấu (kể cả đ/Đ), không cắt khoảng trắng.
            Check("NCC §7/Q2 (AC3 so trùng UNICODE_CI_AI thuần): an=AN=ân, Đặng=dang, giữ khoảng trắng đầu/cuối",
                SupplierRules.AreDuplicate("an", "AN")
                && SupplierRules.AreDuplicate("an", "ân")
                && SupplierRules.AreDuplicate("Đặng", "dang")
                && !SupplierRules.AreDuplicate("  an  ", "an")
                && !SupplierRules.AreDuplicate("an  ", "an"));

            // AC3: bắt buộc Tên & Mã, gộp lỗi trùng Tên trước Mã sau, chặn chuỗi > 255 ký tự, cắt log <= 255.
            SupplierSaveResult emptyName = SupplierRules.ValidateSupplier(new SupplierRow { Name = "   ", Code = "NCC00001" }, false, false);
            SupplierSaveResult emptyCode = SupplierRules.ValidateSupplier(new SupplierRow { Name = "NCC A", Code = "" }, false, false);
            SupplierSaveResult dupBoth = SupplierRules.ValidateSupplier(new SupplierRow { Name = "NCC A", Code = "NCC00001" }, true, true);
            string expectedBoth = SupplierText.NameDuplicate("NCC A") + Environment.NewLine + SupplierText.CodeDuplicate("NCC00001");
            SupplierSaveResult tooLong = SupplierRules.ValidateSupplier(new SupplierRow { Name = new string('ă', 256), Code = "NCC00001" }, false, false);
            SupplierGroupSaveResult grpEmpty = SupplierRules.ValidateGroup(new SupplierGroupRow { Name = "  " }, false);
            SupplierGroupSaveResult grpDup = SupplierRules.ValidateGroup(new SupplierGroupRow { Name = "Nhóm A" }, true);
            string clipped = SupplierRules.ClipLogText(new string('x', 300));

            Check("NCC §7/Q2 (AC3 luật kiểm tra thuần): chặn trống Tên/Mã, gộp lỗi trùng Tên trước Mã sau, chặn >255, cắt log 255",
                !emptyName.Ok && emptyName.HasNameError && emptyName.ErrorMessage == SupplierText.NameEmpty
                && !emptyCode.Ok && emptyCode.HasCodeError && emptyCode.ErrorMessage == SupplierText.CodeEmpty
                && !dupBoth.Ok && dupBoth.HasNameError && dupBoth.HasCodeError && dupBoth.ErrorMessage == expectedBoth
                && !tooLong.Ok && tooLong.HasNameError
                && !grpEmpty.Ok && grpEmpty.ErrorMessage == SupplierText.GroupNameEmpty
                && !grpDup.Ok && grpDup.ErrorMessage == SupplierText.GroupNameDuplicate("Nhóm A")
                && clipped.Length == 255,
                SingleLine(dupBoth.ErrorMessage));

            // AC7: quyền theo SFUNCTION (240 Quản lý/Kế toán, 80 Thủ kho = Xem+Thêm, 16 Chỉ xem, 0 Phục vụ).
            var perms80 = new FunctionPermissions(false);
            perms80.Add(SupplierText.FunctionId, SupplierText.FunctionName, RoleModes.View | RoleModes.Add);
            var sess80 = new UserSession { UserId = "u80", IsAdmin = false, Permissions = perms80 };
            var sess0 = new UserSession { UserId = "u0", IsAdmin = false, Permissions = new FunctionPermissions(false) };

            Check("NCC §7 (AC7 quyền SFUNCTION thuần mức 80 và 0): mức 80 có Xem+Thêm không Sửa/Xóa; mức 0 khóa hết",
                SupplierRules.Can(sess80, RoleModes.View)
                && SupplierRules.Can(sess80, RoleModes.Add)
                && !SupplierRules.Can(sess80, RoleModes.Edit)
                && !SupplierRules.Can(sess80, RoleModes.Remove)
                && !SupplierRules.Can(sess0, RoleModes.View)
                && !SupplierRules.Can(sess0, RoleModes.Add));
        }

        private static void SupplierServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            int oldAuditCfg = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            var adminSess = new UserSession
            {
                UserId = "selftest_ncc",
                UserName = "selftest_ncc",
                DisplayName = "Selftest NCC",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            var svc = new SupplierService(adminSess);
            var grpSvc = new SupplierGroupService(adminSess);
            var repo = new SupplierRepository(db);
            var grpRepo = new SupplierGroupRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            var createdSupplierIds = new List<string>();
            var createdGroupIds = new List<string>();

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 }
                }, "selftest_ncc");
                SystemConfig.Reload(db);

                // AC1: Thêm NCC chỉ có Mã và Tên: STATUS=30, GUID, người/giờ tạo đúng, ghi lưu vết.
                bool tableWasEmpty = CountTableRows(db, "DNHACUNGCAP") == 0;
                string initialNext = svc.NextCode();
                var minRow = SupplierRow.NewDefault();
                minRow.Code = initialNext;
                minRow.Name = "NCC Tối Thiểu " + tag;
                SupplierSaveResult rMin = svc.Save(minRow);
                if (rMin.Ok) createdSupplierIds.Add(rMin.SavedId);
                SupplierRow loadedMin = rMin.Ok ? repo.LoadById(rMin.SavedId) : null;
                Guid parsedGuid;
                int addLogCount = rMin.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", rMin.SavedId, "@F", SupplierText.FunctionName)
                    : 0;

                Check("NCC §7 (AC1 thêm tối thiểu Mã và Tên): STATUS=30, GUID, người/giờ tạo đúng, ghi lưu vết",
                    rMin.Ok && loadedMin != null
                    && Guid.TryParse(loadedMin.Id, out parsedGuid)
                    && loadedMin.Status == 30
                    && loadedMin.CreatedById == "selftest_ncc"
                    && loadedMin.CreatedAt.HasValue
                    && string.IsNullOrEmpty(loadedMin.ModifiedById)
                    && !loadedMin.ModifiedAt.HasValue
                    && addLogCount >= 1,
                    SingleLine(rMin.ErrorMessage ?? ("code=" + initialNext + ", logs=" + addLogCount)));

                // AC2: Sinh mã tự động trên DB: bảng trống ra NCC00001, sau NCC98005 ra NCC98006, mã tay không làm nhảy số.
                var ncc5 = SupplierRow.NewDefault();
                ncc5.Code = "NCC98005";
                ncc5.Name = "NCC Mã 98005 " + tag;
                SupplierSaveResult r5 = svc.Save(ncc5);
                if (r5.Ok) createdSupplierIds.Add(r5.SavedId);
                string nextAfter5 = svc.NextCode();

                var manual = SupplierRow.NewDefault();
                manual.Code = "001-" + tag;
                manual.Name = "NCC Mã Tay " + tag;
                SupplierSaveResult rManual = svc.Save(manual);
                if (rManual.Ok) createdSupplierIds.Add(rManual.SavedId);
                string nextAfterManual = svc.NextCode();

                Check("NCC §7/Q2 (AC2 sinh mã trên DB): trống->NCC00001, sau NCC98005 ra NCC98006, thêm mã nhập tay vẫn giữ NCC98006",
                    (!tableWasEmpty || initialNext == "NCC00001")
                    && r5.Ok && rManual.Ok && nextAfter5 == "NCC98006" && nextAfterManual == "NCC98006",
                    "init=" + initialNext + ", after5=" + nextAfter5 + ", afterManual=" + nextAfterManual);

                // AC3: Trống Mã hoặc Tên bị chặn, không ghi dòng.
                int countBeforeInvalid = CountTableRows(db, "DNHACUNGCAP");
                SupplierSaveResult rEmptyName = svc.Save(new SupplierRow { Code = "NCCE1" + tag, Name = "   " });
                SupplierSaveResult rEmptyCode = svc.Save(new SupplierRow { Code = "", Name = "NCC Không Mã " + tag });
                int countAfterInvalid = CountTableRows(db, "DNHACUNGCAP");
                Check("NCC §7 (AC3 bắt buộc Tên và Mã): để trống Mã hoặc Tên bị chặn, không ghi dòng",
                    !rEmptyName.Ok && rEmptyName.HasNameError && rEmptyName.ErrorMessage == SupplierText.NameEmpty
                    && !rEmptyCode.Ok && rEmptyCode.HasCodeError && rEmptyCode.ErrorMessage == SupplierText.CodeEmpty
                    && countBeforeInvalid == countAfterInvalid,
                    "before=" + countBeforeInvalid + ", after=" + countAfterInvalid);

                // AC3: Trùng Mã/Tên UNICODE_CI_AI (bỏ dấu & hoa thường, không cắt khoảng trắng); gộp lỗi Tên trước Mã sau; sửa chính nó OK.
                var baseSup = SupplierRow.NewDefault();
                baseSup.Code = "NCC_B_" + tag;
                baseSup.Name = "an " + tag;
                SupplierSaveResult rBase = svc.Save(baseSup);
                if (rBase.Ok) createdSupplierIds.Add(rBase.SavedId);

                SupplierSaveResult dupCode = svc.Save(new SupplierRow { Code = "ncc_b_" + tag, Name = "Tên khác " + tag });
                SupplierSaveResult dupName = svc.Save(new SupplierRow { Code = "NCC_D1_" + tag, Name = "ÂN " + tag });
                SupplierSaveResult dupBoth = svc.Save(new SupplierRow { Code = "NCC_B_" + tag, Name = "AN " + tag });

                var padded = SupplierRow.NewDefault();
                padded.Code = "NCC_P_" + tag;
                padded.Name = "  an " + tag + "  ";
                SupplierSaveResult rPadded = svc.Save(padded);
                if (rPadded.Ok) createdSupplierIds.Add(rPadded.SavedId);

                SupplierRow origBase = repo.LoadById(baseSup.Id);
                DateTime? origCreatedAt = origBase.CreatedAt;
                string origCreatedBy = origBase.CreatedById;
                origBase.Phone = "0909888777";
                SupplierSaveResult rSelfEdit = svc.Save(origBase);
                SupplierRow afterEdit = repo.LoadById(baseSup.Id);
                int editLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", baseSup.Id, "@F", SupplierText.FunctionName);

                Check("NCC §7/Q2 (AC3 kiểm trùng UNICODE_CI_AI và sửa chính mình): chặn trùng dấu/hoa thường, giữ khoảng trắng, Tên trước Mã sau, sửa chính mình giữ Created và cập nhật Modified + lưu vết",
                    rBase.Ok && !dupCode.Ok && dupCode.HasCodeError
                    && !dupName.Ok && dupName.HasNameError
                    && !dupBoth.Ok
                    && dupBoth.ErrorMessage.IndexOf("Tên nhà cung cấp", StringComparison.Ordinal) < dupBoth.ErrorMessage.IndexOf("Mã nhà cung cấp", StringComparison.Ordinal)
                    && rPadded.Ok && rSelfEdit.Ok && afterEdit != null
                    && afterEdit.Phone == "0909888777"
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_ncc" && afterEdit.ModifiedAt.HasValue
                    && editLogCount >= 2,
                    SingleLine(dupBoth.ErrorMessage ?? ("logs=" + editLogCount)));

                // AC3 biên độ dài 255 ký tự (kể cả TLUUVET.BAN/NOTE VARCHAR 255) & 256 ký tự bị chặn.
                string str255 = "NCC Tiếng Việt " + tag + " " + new string('ê', 255 - 16 - tag.Length);
                string code255 = "NCC" + tag + new string('9', 255 - 3 - tag.Length);
                var row255 = new SupplierRow
                {
                    Code = code255,
                    Name = str255,
                    Address = new string('Đ', 255),
                    Phone = new string('8', 255),
                    Email = new string('e', 245) + "@kazuko.vn",
                    Website = "https://" + new string('w', 243) + ".com",
                    Note = "Ghi chú " + new string('ư', 255 - 8)
                };
                SupplierSaveResult r255 = svc.Save(row255);
                if (r255.Ok) createdSupplierIds.Add(r255.SavedId);
                SupplierRow loaded255 = r255.Ok ? repo.LoadById(r255.SavedId) : null;
                SupplierSaveResult r256Name = svc.Save(new SupplierRow { Code = "NCC256A" + tag, Name = new string('ă', 256) });
                SupplierSaveResult r256Code = svc.Save(new SupplierRow { Code = new string('9', 256), Name = "NCC Mã 256 " + tag });
                SupplierSaveResult r256Web = svc.Save(new SupplierRow { Code = "NCC256W" + tag, Name = "NCC Web 256 " + tag, Website = new string('w', 256) });

                Check("NCC §7 (AC3 biên chuỗi 255/256 ký tự kể cả lưu vết TLUUVET): lưu & đọc đúng mọi cột dài đúng 255 ký tự UTF-8, chặn 256 ký tự",
                    r255.Ok && loaded255 != null
                    && loaded255.Name == str255 && loaded255.Code == code255
                    && loaded255.Address.Length == 255 && loaded255.Phone.Length == 255
                    && loaded255.Email.Length == 255 && loaded255.Website.Length == 255
                    && loaded255.Note.Length == 255
                    && !r256Name.Ok && !r256Code.Ok && !r256Web.Ok,
                    SingleLine(r255.ErrorMessage ?? ("lenName=" + (loaded255 == null ? -1 : loaded255.Name.Length))));

                // AC4: Nhập đủ 8 trường (kèm nhóm), lưu, mở lại khớp 100%, và LoadLookup trả đúng Tên/Địa chỉ/Điện thoại.
                var grp = SupplierGroupRow.NewDefault();
                grp.Name = "Nhóm Đồ Khô " + tag;
                grp.Note = "Ghi chú nhóm " + tag;
                SupplierGroupSaveResult rGrp = grpSvc.Save(grp);
                if (rGrp.Ok) createdGroupIds.Add(rGrp.SavedId);

                var full = new SupplierRow
                {
                    Code = "NCCF8" + tag,
                    Name = "Công ty TNHH Nguyên Liệu " + tag,
                    GroupId = rGrp.SavedId,
                    Address = "12 Nguyễn Trãi, Q.5",
                    Phone = "02838112233",
                    Email = "sales@ncc.vn",
                    Website = "https://ncc.vn",
                    Note = "Giao hàng thứ 2 và thứ 5"
                };
                SupplierSaveResult rFull = svc.Save(full);
                if (rFull.Ok) createdSupplierIds.Add(rFull.SavedId);
                SupplierRow loadedFull = rFull.Ok ? repo.LoadById(rFull.SavedId) : null;
                SupplierLookupItem lk = svc.LoadLookup().Find(x => x.Id == full.Id);

                Check("NCC §7 (AC4 nhập đủ 8 trường lưu mở lại và LoadLookup): mọi trường khớp 100%",
                    rGrp.Ok && rFull.Ok && loadedFull != null
                    && loadedFull.Code == full.Code && loadedFull.Name == full.Name
                    && loadedFull.GroupId == rGrp.SavedId && loadedFull.GroupName == grp.Name
                    && loadedFull.Address == full.Address && loadedFull.Phone == full.Phone
                    && loadedFull.Email == full.Email && loadedFull.Website == full.Website
                    && loadedFull.Note == full.Note
                    && lk != null && lk.Name == full.Name && lk.Address == full.Address && lk.Phone == full.Phone,
                    SingleLine(rFull.ErrorMessage ?? "ok"));

                // AC6: Chặn xóa NCC khi có phát sinh ở TDONHANG và TTHUCHI, báo số tham chiếu.
                CheckSupplierDeleteBlockedOnBothTables(db, svc, repo, full.Id, tag);

                // AC6: Xóa cứng NCC chưa có phát sinh thành công và ghi lưu vết.
                SupplierDeleteResult delUnused = svc.Delete(padded.Id);
                bool goneAfterDel = repo.LoadById(padded.Id) == null;
                int delLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", padded.Id, "@F", SupplierText.FunctionName);
                Check("NCC §7/Q1 (AC6 xóa cứng NCC không tham chiếu): xóa hẳn khỏi DNHACUNGCAP và ghi lưu vết",
                    delUnused.Ok && goneAfterDel && delLogCount >= 2,
                    SingleLine(delUnused.ErrorMessage ?? ("gone=" + goneAfterDel + ", logs=" + delLogCount)));

                // AC6 + Q5: Quản lý nhóm NCC (thêm cha/con, tên trùng/255 ký tự, chặn xóa khi còn nhóm con hoặc còn NCC, xóa cứng khi sạch).
                CheckSupplierGroupCrudAndDeleteRules(db, svc, grpSvc, grpRepo, tag, createdSupplierIds, createdGroupIds);

                // AC7: Quyền ở tầng Services (mức 80 Thủ kho: thêm OK, sửa/xóa chặn; mức 16 Chỉ xem: thêm/sửa/xóa chặn).
                CheckSupplierServicePermissions(db, tag, createdSupplierIds, createdGroupIds);
            }
            finally
            {
                foreach (string sid in createdSupplierIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", sid);
                    SafeDeleteById(db, "DNHACUNGCAP", sid);
                }
                for (int i = createdGroupIds.Count - 1; i >= 0; i--)
                {
                    string gid = createdGroupIds[i];
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMNHACUNGCAP", gid);
                }
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = oldAuditCfg }
                }, "selftest_ncc");
                SystemConfig.Reload(db);
            }
        }

        private static void CheckSupplierDeleteBlockedOnBothTables(
            string db, SupplierService svc, SupplierRepository repo, string supplierId, string tag)
        {
            var failed = new List<string>();
            foreach (string table in SupplierRepository.ReferenceTables)
            {
                string refId = DbHelper.NewId();
                try
                {
                    InsertWarehouseRefRow(db, table, "DNHACUNGCAPID", refId, supplierId, "REF-NCC-" + table + "-" + tag);
                    SupplierDeleteResult del = svc.Delete(supplierId);
                    bool stillExists = repo.LoadById(supplierId) != null;
                    bool hasBlock = HasBlock(del.Blocks, table);
                    bool msgHasCount = !string.IsNullOrEmpty(del.ErrorMessage)
                        && del.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0;
                    if (del.Ok || !stillExists || del.ReferenceCount != 1 || !hasBlock || !msgHasCount)
                        failed.Add(table + "(ok=" + del.Ok + ",refs=" + del.ReferenceCount + ",blk=" + hasBlock + ")");
                }
                catch (Exception ex)
                {
                    failed.Add(table + "(" + SingleLine(ex.Message) + ")");
                }
                finally
                {
                    SafeDeleteById(db, table, refId);
                }
            }
            Check("NCC §7/Q1 (AC6 chặn xóa NCC có phát sinh): chặn đủ TDONHANG và TTHUCHI, báo số tham chiếu",
                failed.Count == 0,
                failed.Count == 0 ? "2/2 bảng chặn đúng" : SingleLine(string.Join("; ", failed.ToArray())));
        }

        private static void CheckSupplierGroupCrudAndDeleteRules(
            string db, SupplierService svc, SupplierGroupService grpSvc, SupplierGroupRepository grpRepo,
            string tag, List<string> createdSupplierIds, List<string> createdGroupIds)
        {
            var parent = SupplierGroupRow.NewDefault();
            parent.Name = "Nhóm Cha NCC " + tag;
            SupplierGroupSaveResult rParent = grpSvc.Save(parent);
            if (rParent.Ok) createdGroupIds.Add(rParent.SavedId);

            SupplierGroupSaveResult rEmpty = grpSvc.Save(SupplierGroupRow.NewDefault());
            SupplierGroupSaveResult rDup = grpSvc.Save(new SupplierGroupRow { Name = "nhom cha ncc " + tag });

            string grp255Name = "Nhóm 255 " + tag + " " + new string('ơ', 255 - 10 - tag.Length);
            var grp255 = SupplierGroupRow.NewDefault();
            grp255.Name = grp255Name;
            grp255.Note = new string('g', 255);
            SupplierGroupSaveResult rGrp255 = grpSvc.Save(grp255);
            if (rGrp255.Ok) createdGroupIds.Add(rGrp255.SavedId);
            SupplierGroupRow loaded255 = rGrp255.Ok ? grpRepo.LoadById(rGrp255.SavedId) : null;

            var child = SupplierGroupRow.NewDefault();
            child.Name = "Nhóm Con NCC " + tag;
            child.ParentId = rParent.SavedId;
            SupplierGroupSaveResult rChild = grpSvc.Save(child);
            if (rChild.Ok) createdGroupIds.Add(rChild.SavedId);

            SupplierGroupDeleteResult delParentWithChild = grpSvc.Delete(rParent.SavedId);

            var member = SupplierRow.NewDefault();
            member.Code = "NCCM" + tag;
            member.Name = "NCC Thuộc Nhóm Con " + tag;
            member.GroupId = rChild.SavedId;
            SupplierSaveResult rMem = svc.Save(member);
            if (rMem.Ok) createdSupplierIds.Add(rMem.SavedId);

            SupplierGroupDeleteResult delChildWithMember = grpSvc.Delete(rChild.SavedId);

            SupplierDeleteResult delMemOk = svc.Delete(rMem.SavedId);
            SupplierGroupDeleteResult delChildOk = grpSvc.Delete(rChild.SavedId);
            SupplierGroupDeleteResult delParentOk = grpSvc.Delete(rParent.SavedId);

            Check("NCC §7/Q1/Q5 (AC6 quản lý nhóm NCC): chặn trống/trùng, 255 ký tự + lưu vết OK, chặn xóa khi còn nhóm con/còn NCC (báo số), xóa cứng khi rỗng",
                rParent.Ok && !rEmpty.Ok && !rDup.Ok
                && rGrp255.Ok && loaded255 != null && loaded255.Name == grp255Name
                && rChild.Ok && !delParentWithChild.Ok && delParentWithChild.ChildGroupCount == 1
                && delParentWithChild.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0
                && rMem.Ok && !delChildWithMember.Ok && delChildWithMember.SupplierCount == 1
                && delChildWithMember.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0
                && delMemOk.Ok && delChildOk.Ok && delParentOk.Ok
                && grpRepo.LoadById(rChild.SavedId) == null && grpRepo.LoadById(rParent.SavedId) == null,
                SingleLine(delChildWithMember.ErrorMessage ?? "ok"));
        }

        private static void CheckSupplierServicePermissions(
            string db, string tag, List<string> createdSupplierIds, List<string> createdGroupIds)
        {
            // Mức 80 (Thủ kho): Xem + Thêm; không Sửa, không Xóa.
            var perms80 = new FunctionPermissions(false);
            perms80.Add(SupplierText.FunctionId, SupplierText.FunctionName, RoleModes.View | RoleModes.Add);
            var sess80 = new UserSession { UserId = "u_thukho", UserName = "thukho", IsAdmin = false, DatabasePath = db, Permissions = perms80 };
            var svc80 = new SupplierService(sess80);
            var grp80 = new SupplierGroupService(sess80);

            var sRow = new SupplierRow { Code = "NCC80" + tag, Name = "NCC Thủ Kho " + tag };
            SupplierSaveResult addSup80 = svc80.Save(sRow);
            if (addSup80.Ok) createdSupplierIds.Add(addSup80.SavedId);
            sRow.Note = "Sửa bởi Thủ kho";
            SupplierSaveResult editSup80 = addSup80.Ok ? svc80.Save(sRow) : SupplierSaveResult.Fail("skip");
            SupplierDeleteResult delSup80 = addSup80.Ok ? svc80.Delete(addSup80.SavedId) : SupplierDeleteResult.Fail("skip");

            var gRow = SupplierGroupRow.NewDefault();
            gRow.Name = "Nhóm Thủ Kho " + tag;
            SupplierGroupSaveResult addGrp80 = grp80.Save(gRow);
            if (addGrp80.Ok) createdGroupIds.Add(addGrp80.SavedId);
            gRow.Note = "Sửa nhóm bởi Thủ kho";
            SupplierGroupSaveResult editGrp80 = addGrp80.Ok ? grp80.Save(gRow) : SupplierGroupSaveResult.Fail("skip");
            SupplierGroupDeleteResult delGrp80 = addGrp80.Ok ? grp80.Delete(addGrp80.SavedId) : SupplierGroupDeleteResult.Fail("skip");

            // Mức 16 (Chỉ xem): chặn thêm/sửa/xóa.
            var perms16 = new FunctionPermissions(false);
            perms16.Add(SupplierText.FunctionId, SupplierText.FunctionName, RoleModes.View);
            var svc16 = new SupplierService(new UserSession { UserId = "u_view", IsAdmin = false, DatabasePath = db, Permissions = perms16 });
            SupplierSaveResult addView = svc16.Save(new SupplierRow { Code = "NCC16" + tag, Name = "NCC Chỉ Xem " + tag });
            SupplierDeleteResult delView = addSup80.Ok ? svc16.Delete(addSup80.SavedId) : SupplierDeleteResult.Fail("skip");

            Check("NCC §7 (AC7 quyền tầng Services): mức 80 Thủ kho thêm OK nhưng sửa/xóa bị chặn (cả NCC và nhóm); mức 16 chỉ xem chặn thêm/xóa",
                addSup80.Ok && !editSup80.Ok && editSup80.ErrorMessage == SupplierText.NoPermission
                && !delSup80.Ok && delSup80.ErrorMessage == SupplierText.NoPermission
                && addGrp80.Ok && !editGrp80.Ok && !delGrp80.Ok
                && !addView.Ok && !delView.Ok,
                "add80=" + addSup80.Ok + ", edit80=" + editSup80.Ok + ", del80=" + delSup80.Ok);
        }
    }
}
