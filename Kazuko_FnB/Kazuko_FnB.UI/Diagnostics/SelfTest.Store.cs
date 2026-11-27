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
        private static void StoreTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_ch",
                    UserName = "selftest_ch",
                    DisplayName = "Selftest CuaHang",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                StoreRulesPureTests();
                StoreServiceAndRepoTests(db);
                StoreUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void StoreRulesPureTests()
        {
            // Q2: CODE theo UTF8 (phân biệt hoa/thường, giữ khoảng trắng); NAME theo UNICODE_CI_AI (không phân biệt hoa/thường & dấu, giữ khoảng trắng).
            Check("Cửa hàng §7/Q2 (so trùng thuần theo collation): CODE phân biệt hoa thường (CH1 != ch1), NAME bỏ hoa thường và dấu (Cửa Hàng Đỏ = cua hang do)",
                StoreRules.AreCodesDuplicate("CH1", "CH1")
                && !StoreRules.AreCodesDuplicate("CH1", "ch1")
                && !StoreRules.AreCodesDuplicate(" CH1 ", "CH1")
                && StoreRules.AreNamesDuplicate("Cửa Hàng Đỏ", "cua hang do")
                && StoreRules.AreNamesDuplicate("Trụ Sở Chính", "TRU SO CHINH")
                && !StoreRules.AreNamesDuplicate("  CH A  ", "CH A"));

            // Bắt buộc Mã, Tên; quá độ dài Mã > 36 hoặc trường chữ > 255; trùng Mã / Tên.
            StoreSaveResult rEmptyCode = StoreRules.Validate(new StoreRow { Code = "  ", Name = "CH 1" }, false, false);
            StoreSaveResult rEmptyName = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "  " }, false, false);
            StoreSaveResult rCode37 = StoreRules.Validate(new StoreRow { Code = new string('A', 37), Name = "CH 1" }, false, false);
            StoreSaveResult rName256 = StoreRules.Validate(new StoreRow { Code = "CH1", Name = new string('ê', 256) }, false, false);
            StoreSaveResult rAddr256 = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "CH 1", Address = new string('a', 256) }, false, false);
            StoreSaveResult rPhone256 = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "CH 1", Phone = new string('9', 256) }, false, false);
            StoreSaveResult rNote256 = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "CH 1", Note = new string('n', 256) }, false, false);
            StoreSaveResult rDupCode = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "CH 1" }, true, false);
            StoreSaveResult rDupName = StoreRules.Validate(new StoreRow { Code = "CH1", Name = "CH 1" }, false, true);

            Check("Cửa hàng §7 (luật kiểm tra thuần): chặn trống Mã/Tên, Mã 37 ký tự, chữ 256 ký tự, trùng Mã/Tên",
                !rEmptyCode.Ok && rEmptyCode.HasCodeError && rEmptyCode.ErrorMessage == StoreText.CodeEmpty
                && !rEmptyName.Ok && rEmptyName.HasNameError && rEmptyName.ErrorMessage == StoreText.NameEmpty
                && !rCode37.Ok && rCode37.HasCodeError
                && !rName256.Ok && rName256.HasNameError
                && !rAddr256.Ok && rAddr256.HasAddressError
                && !rPhone256.Ok && rPhone256.HasPhoneError
                && !rNote256.Ok && rNote256.HasNoteError
                && !rDupCode.Ok && rDupCode.HasCodeError
                && !rDupName.Ok && rDupName.HasNameError,
                "ec=" + rEmptyCode.HasCodeError + ", en=" + rEmptyName.HasNameError + ", c37=" + rCode37.HasCodeError + ", n256=" + rName256.HasNameError);

            // Quyền SFUNCTION (Quản lý 240, Chỉ xem 16, Phục vụ 0, Admin luôn được).
            var perms240 = new FunctionPermissions(false);
            perms240.Add(StoreText.FunctionId, StoreText.FunctionName, RoleModes.View | RoleModes.Edit | RoleModes.Add | RoleModes.Remove);
            var sess240 = new UserSession { UserId = "u240", IsAdmin = false, Permissions = perms240 };

            var perms16 = new FunctionPermissions(false);
            perms16.Add(StoreText.FunctionId, StoreText.FunctionName, RoleModes.View);
            var sess16 = new UserSession { UserId = "u16", IsAdmin = false, Permissions = perms16 };

            var sess0 = new UserSession { UserId = "u0", IsAdmin = false, Permissions = new FunctionPermissions(false) };
            var sessAdmin = new UserSession { UserId = "adm", IsAdmin = true, Permissions = new FunctionPermissions(false) };

            Check("Cửa hàng §7 (quyền SFUNCTION mức 240/16/0 và Admin): 240 & Admin đủ 4 quyền, 16 chỉ xem, 0 khóa hết",
                StoreRules.Can(sess240, RoleModes.View) && StoreRules.Can(sess240, RoleModes.Add)
                && StoreRules.Can(sess240, RoleModes.Edit) && StoreRules.Can(sess240, RoleModes.Remove)
                && StoreRules.Can(sess16, RoleModes.View) && !StoreRules.Can(sess16, RoleModes.Add)
                && !StoreRules.Can(sess16, RoleModes.Edit) && !StoreRules.Can(sess16, RoleModes.Remove)
                && !StoreRules.Can(sess0, RoleModes.View) && !StoreRules.Can(sess0, RoleModes.Add)
                && StoreRules.Can(sessAdmin, RoleModes.Remove));
        }

        private static void StoreServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            int oldAuditCfg = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            var adminSess = new UserSession
            {
                UserId = "selftest_ch",
                UserName = "selftest_ch",
                DisplayName = "Selftest CuaHang",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true),
                Stores = new List<StoreRecord>()
            };
            AppSession.Current = adminSess;
            var svc = new StoreService(adminSess);
            var repo = new StoreRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            string wh1Id = DbHelper.NewId();
            string wh2Id = DbHelper.NewId();
            string tplGoodId = DbHelper.NewId();
            string tplOtherId = DbHelper.NewId();
            string imgId = DbHelper.NewId();
            var createdStoreIds = new List<string>();

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 }
                }, "selftest_ch");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ch')",
                    "@I", wh1Id, "@N", "Kho Mặc Định 1 " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ch')",
                    "@I", wh2Id, "@N", "Kho Mặc Định 2 " + tag);
                BookingExec(db, "INSERT INTO SIMAGE (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ch')",
                    "@I", imgId, "@N", "Ảnh CH " + tag);
                InsertStoreTemplateRow(db, tplGoodId, "Mẫu HĐ Đúng " + tag, StoreText.InvoiceTemplateFormId);
                InsertStoreTemplateRow(db, tplOtherId, "Mẫu Form Khác " + tag, "11111111-1111-1111-1111-111111111111");

                // AC2: Thêm hợp lệ chỉ Mã + Tên -> GUID, STATUS=30, người/giờ tạo đúng, ghi lưu vết, nạp lại phiên.
                var sMin = StoreRow.NewDefault();
                sMin.Code = "CHMIN_" + tag;
                sMin.Name = "Cửa Hàng Tối Thiểu " + tag;
                StoreSaveResult rMin = svc.Save(sMin);
                if (rMin.Ok) createdStoreIds.Add(rMin.SavedId);
                StoreRow loadedMin = rMin.Ok ? repo.LoadById(rMin.SavedId) : null;
                Guid parsedGuid;
                int addLogCount = rMin.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", rMin.SavedId, "@F", StoreText.FunctionName)
                    : 0;
                bool sessionHasMin = adminSess.Stores != null && adminSess.Stores.Exists(x => x.Id == rMin.SavedId);

                Check("Cửa hàng §7 (AC2 thêm chỉ Mã+Tên): lưu được, GUID, STATUS=30, người/giờ tạo đúng, ghi lưu vết, nạp lại phiên",
                    rMin.Ok && loadedMin != null
                    && Guid.TryParse(loadedMin.Id, out parsedGuid)
                    && loadedMin.Status == 30
                    && loadedMin.Code == sMin.Code && loadedMin.Name == sMin.Name
                    && loadedMin.CreatedById == "selftest_ch" && loadedMin.CreatedAt.HasValue
                    && string.IsNullOrEmpty(loadedMin.ModifiedById) && !loadedMin.ModifiedAt.HasValue
                    && addLogCount >= 1 && sessionHasMin,
                    SingleLine(rMin.ErrorMessage ?? ("logs=" + addLogCount + ", sess=" + sessionHasMin)));

                // AC3: Trống Mã / Trống Tên / Mã 37 ký tự / trường chữ 256 ký tự bị chặn, không ghi dòng.
                int countBeforeInvalid = CountTableRows(db, "DCUAHANG");
                StoreSaveResult rEmptyCode = svc.Save(new StoreRow { Code = "   ", Name = "CH Không Mã " + tag });
                StoreSaveResult rEmptyName = svc.Save(new StoreRow { Code = "CHNO_" + tag, Name = "   " });
                StoreSaveResult rCode37 = svc.Save(new StoreRow { Code = new string('X', 37), Name = "CH Mã 37 " + tag });
                StoreSaveResult rNote256 = svc.Save(new StoreRow { Code = "CHNT_" + tag, Name = "CH Note 256 " + tag, Note = new string('n', 256) });
                int countAfterInvalid = CountTableRows(db, "DCUAHANG");

                Check("Cửa hàng §7 (AC3 trống Mã/Tên và quá độ dài): chặn trống Mã, trống Tên, Mã 37 ký tự, Ghi chú 256 ký tự, không ghi dòng",
                    !rEmptyCode.Ok && rEmptyCode.HasCodeError
                    && !rEmptyName.Ok && rEmptyName.HasNameError
                    && !rCode37.Ok && rCode37.HasCodeError
                    && !rNote256.Ok && rNote256.HasNoteError
                    && countBeforeInvalid == countAfterInvalid,
                    "before=" + countBeforeInvalid + ", after=" + countAfterInvalid);

                // AC3/Q2: Trùng Tên khác hoa thường/dấu bị chặn; trùng Mã y hệt bị chặn;
                // trùng Mã khác hoa thường (UTF8 phân biệt hoa thường) LƯU ĐƯỢC; sửa chính nó không báo trùng.
                StoreSaveResult rDupNameAccent = svc.Save(new StoreRow { Code = "CHDIFF_" + tag, Name = "cua hang toi thieu " + tag });
                StoreSaveResult rDupCodeExact = svc.Save(new StoreRow { Code = sMin.Code, Name = "Cửa Hàng Khác Tên " + tag });

                var sCaseCode = new StoreRow
                {
                    Code = "chmin_" + tag, // khác hoa thường với CHMIN_ -> được theo collation UTF8!
                    Name = "Cửa Hàng Mã Chữ Thường " + tag
                };
                StoreSaveResult rCaseCode = svc.Save(sCaseCode);
                if (rCaseCode.Ok) createdStoreIds.Add(rCaseCode.SavedId);

                DateTime? origCreatedAt = loadedMin.CreatedAt;
                string origCreatedBy = loadedMin.CreatedById;
                BookingExec(db, "UPDATE DCUAHANG SET SIMAGEID = @IMG WHERE ID = @I", "@IMG", imgId, "@I", loadedMin.Id);

                StoreRow editMin = repo.LoadById(loadedMin.Id);
                editMin.Address = "123 Lê Lợi, Q1";
                editMin.Phone = "0909123456";
                editMin.WarehouseId = wh1Id;
                editMin.TemplateId = tplGoodId;
                editMin.Note = "Cập nhật đủ trường";
                StoreSaveResult rSelfEdit = svc.Save(editMin);
                StoreRow afterEdit = repo.LoadById(loadedMin.Id);
                int editLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", loadedMin.Id, "@F", StoreText.FunctionName);

                Check("Cửa hàng §7/Q2 (AC3/AC5 trùng collation, sửa chính mình, giữ SIMAGEID): chặn trùng Tên bỏ dấu & trùng Mã y hệt, cho phép Mã khác hoa thường, sửa chính mình giữ tạo & SIMAGEID, ghi lưu vết",
                    !rDupNameAccent.Ok && rDupNameAccent.HasNameError
                    && !rDupCodeExact.Ok && rDupCodeExact.HasCodeError
                    && rCaseCode.Ok
                    && rSelfEdit.Ok && afterEdit != null
                    && afterEdit.Address == "123 Lê Lợi, Q1" && afterEdit.Phone == "0909123456"
                    && afterEdit.WarehouseId == wh1Id && afterEdit.WarehouseName == "Kho Mặc Định 1 " + tag
                    && afterEdit.TemplateId == tplGoodId && afterEdit.TemplateName == "Mẫu HĐ Đúng " + tag
                    && afterEdit.Note == "Cập nhật đủ trường"
                    && afterEdit.ImageId == imgId
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_ch" && afterEdit.ModifiedAt.HasValue
                    && editLogCount >= 2,
                    SingleLine(rSelfEdit.ErrorMessage ?? ("dupN=" + rDupNameAccent.Ok + ", dupC=" + rDupCodeExact.Ok + ", caseC=" + rCaseCode.Ok + ", img=" + (afterEdit == null ? "" : afterEdit.ImageId) + ", logs=" + editLogCount)));

                // AC5 biên 255 ký tự: Tên/Địa chỉ/Điện thoại/Ghi chú 255 ký tự lưu được, lưu vết cắt vừa 255 không tràn.
                string name255 = "Cửa Hàng 255 " + tag + " " + new string('ê', 255 - 14 - tag.Length);
                var s255 = new StoreRow
                {
                    Code = new string('M', 36),
                    Name = name255,
                    Address = new string('Đ', 255),
                    Phone = new string('0', 255),
                    Note = new string('G', 255)
                };
                StoreSaveResult r255 = svc.Save(s255);
                if (r255.Ok) createdStoreIds.Add(r255.SavedId);
                StoreRow loaded255 = r255.Ok ? repo.LoadById(r255.SavedId) : null;
                int log255 = r255.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", r255.SavedId, "@F", StoreText.FunctionName)
                    : 0;
                Check("Cửa hàng §7 (AC5 biên Mã 36 và chuỗi 255 ký tự): lưu đủ độ dài tối đa và ghi lưu vết không tràn VARCHAR(255)",
                    r255.Ok && loaded255 != null
                    && loaded255.Code.Length == 36 && loaded255.Name == name255
                    && loaded255.Address.Length == 255 && loaded255.Phone.Length == 255 && loaded255.Note.Length == 255
                    && log255 >= 1,
                    SingleLine(r255.ErrorMessage ?? ("len=" + (loaded255 == null ? -1 : loaded255.Name.Length) + ", logs=" + log255)));

                // AC4: Danh sách Mẫu hóa đơn chỉ gồm mẫu có SFORMID = 5f029a5b-8ec2-4229-bd1d-03fed9569db2.
                List<LookupItem> tpls = svc.LoadInvoiceTemplates();
                bool hasGoodTpl = tpls.Exists(t => t.Id == tplGoodId);
                bool hasOtherTpl = tpls.Exists(t => t.Id == tplOtherId);
                bool allRightForm = true;
                foreach (LookupItem t in tpls)
                {
                    string formId = DbHelper.Read(db, conn =>
                        DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT SFORMID FROM STEMPLATE WHERE ID = @I", "@I", t.Id)));
                    if (!string.Equals(formId, StoreText.InvoiceTemplateFormId, StringComparison.OrdinalIgnoreCase))
                        allRightForm = false;
                }
                StoreSaveResult rBadTpl = svc.Save(new StoreRow
                {
                    Code = "CHBTPL_" + tag,
                    Name = "CH Mẫu Sai " + tag,
                    TemplateId = tplOtherId
                });
                Check("Cửa hàng §7 (AC4 Mẫu hóa đơn lọc đúng SFORMID): chỉ gồm mẫu đúng SFORMID, loại mẫu khác, chặn lưu mẫu sai",
                    hasGoodTpl && !hasOtherTpl && allRightForm && !rBadTpl.Ok,
                    "good=" + hasGoodTpl + ", other=" + hasOtherTpl + ", allRight=" + allRightForm + ", badSaved=" + rBadTpl.Ok);

                // AC6: Sau khi lưu/xóa nạp lại cửa hàng và kho mặc định của phiên theo §3 (Admin theo SORTORDER).
                CheckSessionReloadAfterSaveAndDelete(db, svc, repo, loadedMin.Id, wh2Id);

                // AC7/Q1: Chặn xóa khi còn tham chiếu trên đủ 4 bảng (DKHOHANG, TDONHANG, TTHUCHI, TNGUOIDUNGTHEOCUAHANG) + nêu rõ nơi dùng & số lượng.
                CheckStoreDeleteBlockedOnAllFourTables(db, svc, repo, loadedMin.Id, tag);

                // AC7/Q1: Xóa cửa hàng trống thành công, ghi lưu vết, xóa khỏi UserSession.Stores.
                StoreDeleteResult delEmpty = svc.Delete(sCaseCode.Id);
                bool goneAfterDel = repo.LoadById(sCaseCode.Id) == null;
                int delLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", sCaseCode.Id, "@F", StoreText.FunctionName);
                bool removedFromSession = adminSess.Stores != null && !adminSess.Stores.Exists(x => x.Id == sCaseCode.Id);
                Check("Cửa hàng §7/Q1 (AC7 xóa cứng cửa hàng trống): xóa hẳn khỏi DCUAHANG, ghi lưu vết, gỡ khỏi phiên",
                    delEmpty.Ok && goneAfterDel && delLogCount >= 2 && removedFromSession,
                    SingleLine(delEmpty.ErrorMessage ?? ("gone=" + goneAfterDel + ", logs=" + delLogCount + ", remSess=" + removedFromSession)));

                // AC7/Q1: Chặn xóa cửa hàng cuối cùng (0 tham chiếu nhưng chỉ còn 1 cửa hàng STATUS = 30).
                CheckLastStoreDeleteBlocked(db, svc, repo, s255.Id);

                // AC8: Quyền tầng Services (chỉ Xem chặn lưu/xóa; mức 0 chặn cả đọc).
                CheckStoreServicePermissions(db, loadedMin.Id, tag);
            }
            finally
            {
                foreach (string sid in createdStoreIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", sid);
                    SafeDeleteById(db, "DCUAHANG", sid);
                }
                SafeDeleteById(db, "STEMPLATE", tplGoodId);
                SafeDeleteById(db, "STEMPLATE", tplOtherId);
                SafeDeleteById(db, "SIMAGE", imgId);
                SafeDeleteById(db, "DKHOHANG", wh1Id);
                SafeDeleteById(db, "DKHOHANG", wh2Id);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = oldAuditCfg }
                }, "selftest_ch");
                SystemConfig.Reload(db);
            }
        }

        private static void CheckSessionReloadAfterSaveAndDelete(
            string db, StoreService svc, StoreRepository repo, string storeId, string newWarehouseId)
        {
            // Đặt SORTORDER = '0' tạm cho storeId để nó đứng đầu theo §3 (Admin: cửa hàng đầu tiên theo SORTORDER).
            string origSort = DbHelper.Read(db, conn =>
                DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT SORTORDER FROM DCUAHANG WHERE ID = @I", "@I", storeId)));
            try
            {
                BookingExec(db, "UPDATE DCUAHANG SET SORTORDER = '0' WHERE ID = @I", "@I", storeId);
                StoreRow r = repo.LoadById(storeId);
                r.WarehouseId = newWarehouseId;
                StoreSaveResult saved = svc.Save(r);
                StoreRow back = repo.LoadById(storeId);
                UserSession cur = AppSession.Current;
                bool savedOk = saved != null && saved.Ok;
                bool dbWhOk = back != null && back.WarehouseId == newWarehouseId;
                bool curStoreOk = cur != null && cur.CurrentStore != null && cur.CurrentStore.Id == storeId;
                bool defWhOk = cur != null && cur.CurrentStore != null && cur.CurrentStore.DefaultWarehouseId == newWarehouseId;
                bool curWhOk = cur != null && cur.CurrentWarehouseId == newWarehouseId;
                Check("Cửa hàng §7/§3 (AC6 nạp lại cửa hàng & kho phiên sau khi lưu): CurrentStore và CurrentWarehouseId cập nhật đúng theo SORTORDER",
                    savedOk && dbWhOk && curStoreOk && defWhOk && curWhOk,
                    "saved=" + savedOk + ", dbWh=" + dbWhOk + ", curStoreOk=" + curStoreOk
                    + ", defWhOk=" + defWhOk + ", curWhOk=" + curWhOk
                    + ", gotStore=" + (cur == null || cur.CurrentStore == null ? "null" : cur.CurrentStore.Id)
                    + ", gotWh=" + (cur == null ? "null" : (cur.CurrentWarehouseId ?? "null")));
            }
            finally
            {
                BookingExec(db, "UPDATE DCUAHANG SET SORTORDER = @S WHERE ID = @I", "@S", origSort, "@I", storeId);
            }
        }

        private static void CheckStoreDeleteBlockedOnAllFourTables(
            string db, StoreService svc, StoreRepository repo, string storeId, string tag)
        {
            var failed = new List<string>();
            foreach (StoreReferenceColumn rc in StoreRepository.ReferenceColumns)
            {
                string refId = DbHelper.NewId();
                try
                {
                    InsertWarehouseRefRow(db, rc.TableName, rc.ColumnName, refId, storeId, "REF-CH-" + rc.TableName + "-" + tag);
                    StoreDeleteResult del = svc.Delete(storeId);
                    bool stillExists = repo.LoadById(storeId) != null;
                    bool hasExactBlock = del.Blocks != null && del.Blocks.Exists(b =>
                        string.Equals(b.TableName, rc.TableName, StringComparison.OrdinalIgnoreCase)
                        && b.Count == 1);
                    string expectedPart = "1 " + rc.DisplayLabel;
                    bool msgHasDetail = !string.IsNullOrEmpty(del.ErrorMessage)
                        && del.ErrorMessage.IndexOf(expectedPart, StringComparison.Ordinal) >= 0;
                    if (del.Ok || !stillExists || del.ReferenceCount != 1 || !hasExactBlock || !msgHasDetail)
                        failed.Add(rc.TableName + "(ok=" + del.Ok + ",refs=" + del.ReferenceCount + ",msg=" + SingleLine(del.ErrorMessage) + ")");
                }
                catch (Exception ex)
                {
                    failed.Add(rc.TableName + "(" + SingleLine(ex.Message) + ")");
                }
                finally
                {
                    SafeDeleteById(db, rc.TableName, refId);
                }
            }

            // Kiểm thông báo tổ hợp nhiều nơi: 2 kho + 1 hóa đơn -> nêu "2 kho" và "1 hóa đơn".
            string refWh1 = DbHelper.NewId();
            string refWh2 = DbHelper.NewId();
            string refBill = DbHelper.NewId();
            bool comboOk = false;
            string comboMsg = "";
            try
            {
                InsertWarehouseRefRow(db, "DKHOHANG", "DCUAHANGID", refWh1, storeId, "REF-K1-" + tag);
                InsertWarehouseRefRow(db, "DKHOHANG", "DCUAHANGID", refWh2, storeId, "REF-K2-" + tag);
                InsertWarehouseRefRow(db, "TDONHANG", "DCUAHANGID", refBill, storeId, "REF-HD-" + tag);
                StoreDeleteResult delCombo = svc.Delete(storeId);
                comboMsg = delCombo.ErrorMessage ?? "";
                comboOk = !delCombo.Ok && delCombo.ReferenceCount == 3
                    && comboMsg.IndexOf("2 kho", StringComparison.Ordinal) >= 0
                    && comboMsg.IndexOf("1 hóa đơn", StringComparison.Ordinal) >= 0;
            }
            finally
            {
                SafeDeleteById(db, "TDONHANG", refBill);
                SafeDeleteById(db, "DKHOHANG", refWh1);
                SafeDeleteById(db, "DKHOHANG", refWh2);
            }

            Check("Cửa hàng §7/Q1 (AC7 chặn xóa theo 4 bảng tham chiếu & nêu số lượng): chặn đủ DKHOHANG, TDONHANG, TTHUCHI, TNGUOIDUNGTHEOCUAHANG và báo rõ '2 kho, 1 hóa đơn'",
                failed.Count == 0 && comboOk,
                failed.Count == 0 ? SingleLine("4/4 bảng + combo: " + comboMsg) : SingleLine(string.Join("; ", failed.ToArray())));
        }

        private static void CheckLastStoreDeleteBlocked(string db, StoreService svc, StoreRepository repo, string soloStoreId)
        {
            var hiddenIds = new List<string>();
            try
            {
                DbHelper.Read(db, conn =>
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT ID FROM DCUAHANG WHERE STATUS = 30 AND ID <> @I", "@I", soloStoreId);
                    foreach (DataRow r in dt.Rows)
                        hiddenIds.Add(DbHelper.Str(r["ID"]));
                    return 0;
                });

                foreach (string otherId in hiddenIds)
                    BookingExec(db, "UPDATE DCUAHANG SET STATUS = 0 WHERE ID = @I", "@I", otherId);

                StoreDeleteResult delLast = svc.Delete(soloStoreId);
                bool stillExists = repo.LoadById(soloStoreId) != null;
                Check("Cửa hàng §7/Q1 (AC7 chặn xóa cửa hàng cuối cùng): 0 tham chiếu nhưng chỉ còn 1 cửa hàng STATUS=30 vẫn bị chặn",
                    !delLast.Ok && delLast.IsLastStore && stillExists && delLast.ErrorMessage == StoreText.LastStoreBlocked,
                    SingleLine(delLast.ErrorMessage));
            }
            finally
            {
                foreach (string otherId in hiddenIds)
                    BookingExec(db, "UPDATE DCUAHANG SET STATUS = 30 WHERE ID = @I", "@I", otherId);
            }
        }

        private static void CheckStoreServicePermissions(string db, string existingStoreId, string tag)
        {
            var permsView = new FunctionPermissions(false);
            permsView.Add(StoreText.FunctionId, StoreText.FunctionName, RoleModes.View);
            var svcView = new StoreService(new UserSession { UserId = "u_ch_view", IsAdmin = false, DatabasePath = db, Permissions = permsView });

            bool canRead = svcView.LoadAll().Count > 0;
            StoreSaveResult addView = svcView.Save(new StoreRow { Code = "CHV_" + tag, Name = "CH Chỉ Xem " + tag });
            StoreRow loaded = svcView.LoadById(existingStoreId);
            StoreSaveResult editView = loaded != null ? svcView.Save(loaded) : StoreSaveResult.Fail("skip");
            StoreDeleteResult delView = svcView.Delete(existingStoreId);

            var svcNone = new StoreService(new UserSession { UserId = "u_ch_none", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) });
            bool noneBlockedRead = svcNone.LoadAll().Count == 0 && svcNone.LoadById(existingStoreId) == null;

            Check("Cửa hàng §7 (AC8 quyền tầng Services): chỉ Xem đọc được nhưng chặn Thêm/Sửa/Xóa; không quyền chặn cả đọc",
                canRead && !addView.Ok && addView.ErrorMessage == StoreText.NoPermission
                && !editView.Ok && editView.ErrorMessage == StoreText.NoPermission
                && !delView.Ok && delView.ErrorMessage == StoreText.NoPermission
                && noneBlockedRead,
                "read=" + canRead + ", add=" + addView.Ok + ", edit=" + editView.Ok + ", del=" + delView.Ok + ", none=" + noneBlockedRead);
        }

        private static void InsertStoreTemplateRow(string db, string id, string name, string formId)
        {
            InsertWarehouseRefRow(db, "STEMPLATE", "SFORMID", id, formId, name);
        }
    }
}
