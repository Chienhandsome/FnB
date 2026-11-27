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
        private static void WarehouseTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_kho",
                    UserName = "selftest_kho",
                    DisplayName = "Selftest Kho",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                WarehouseRulesPureTests();
                WarehouseServiceAndRepoTests(db);
                WarehouseUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void WarehouseRulesPureTests()
        {
            // Tiêu chí trùng tên UNICODE_CI_AI: bỏ phân biệt hoa thường & dấu (kể cả đ/Đ), không cắt khoảng trắng.
            Check("Kho §7 (so trùng UNICODE_CI_AI): Kho A = kho a = khó á, Kho Đỏ = kho do, giữ khoảng trắng",
                WarehouseRules.AreDuplicate("Kho A", "kho a")
                && WarehouseRules.AreDuplicate("Kho A", "khó á")
                && WarehouseRules.AreDuplicate("Kho Đỏ", "kho do")
                && !WarehouseRules.AreDuplicate("  Kho A  ", "Kho A"));

            // Tiêu chí bắt buộc Tên và Cửa hàng, độ dài 255.
            WarehouseSaveResult rEmptyName = WarehouseRules.Validate(new WarehouseRow { Name = "   ", StoreId = "S1" }, false);
            WarehouseSaveResult rEmptyStore = WarehouseRules.Validate(new WarehouseRow { Name = "Kho 1", StoreId = "" }, false);
            WarehouseSaveResult rDup = WarehouseRules.Validate(new WarehouseRow { Name = "Kho 1", StoreId = "S1" }, true);
            WarehouseSaveResult rLong = WarehouseRules.Validate(new WarehouseRow { Name = new string('ă', 256), StoreId = "S1" }, false);
            Check("Kho §7 (luật kiểm tra thuần): chặn trống Tên, trống Cửa hàng, trùng Tên, quá 255 ký tự",
                !rEmptyName.Ok && rEmptyName.HasNameError && rEmptyName.ErrorMessage == WarehouseText.NameEmpty
                && !rEmptyStore.Ok && rEmptyStore.HasStoreError && rEmptyStore.ErrorMessage == WarehouseText.StoreEmpty
                && !rDup.Ok && rDup.HasNameError
                && !rLong.Ok && rLong.HasNameError);

            // Tiêu chí Q2: kho mới lưu 0, sửa giữ nguyên giá trị cũ.
            Check("Kho §7/Q2 (cờ âm kho thuần): kho mới = 0, sửa giữ nguyên 30 hoặc 0",
                WarehouseRules.AllowNegativeOnCreate() == 0
                && WarehouseRules.AllowNegativeOnEdit(30) == 30
                && WarehouseRules.AllowNegativeOnEdit(0) == 0);

            // Tiêu chí quyền theo SFUNCTION (112 Thủ kho, 240 Quản lý/Kế toán, 16 Chỉ xem, 0 Khóa).
            var perms112 = new FunctionPermissions(false);
            perms112.Add(WarehouseText.FunctionId, WarehouseText.FunctionName, RoleModes.View | RoleModes.Edit | RoleModes.Add);
            var sess112 = new UserSession { UserId = "u112", IsAdmin = false, Permissions = perms112 };
            var sess0 = new UserSession { UserId = "u0", IsAdmin = false, Permissions = new FunctionPermissions(false) };
            Check("Kho §7 (quyền SFUNCTION mức 112 và 0): mức 112 có Xem/Thêm/Sửa không Xóa; mức 0 khóa hết",
                WarehouseRules.Can(sess112, RoleModes.View)
                && WarehouseRules.Can(sess112, RoleModes.Add)
                && WarehouseRules.Can(sess112, RoleModes.Edit)
                && !WarehouseRules.Can(sess112, RoleModes.Remove)
                && !WarehouseRules.Can(sess0, RoleModes.View)
                && !WarehouseRules.Can(sess0, RoleModes.Add));
        }

        private static void WarehouseServiceAndRepoTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            int oldAuditCfg = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            var adminSess = new UserSession
            {
                UserId = "selftest_kho",
                UserName = "selftest_kho",
                DisplayName = "Selftest Kho",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            var svc = new WarehouseService(adminSess);
            var repo = new WarehouseRepository(db);

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeA = DbHelper.NewId();
            string storeB = DbHelper.NewId();
            string imgId = DbHelper.NewId();
            var createdWarehouseIds = new List<string>();

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 }
                }, "selftest_kho");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kho')",
                    "@I", storeA, "@N", "CH Kho A " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kho')",
                    "@I", storeB, "@N", "CH Kho B " + tag);
                BookingExec(db, "INSERT INTO SIMAGE (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kho')",
                    "@I", imgId, "@N", "IMG Kho " + tag);

                // AC1: Thêm kho có Tên và Cửa hàng (+ Ảnh, Ghi chú): STATUS=30, GUID, người/giờ tạo, CHOPHEPAMKHO=0, ghi lưu vết.
                var w1 = WarehouseRow.NewDefault();
                w1.Name = "Kho Bán Hàng Thử " + tag;
                w1.StoreId = storeA;
                w1.ImageId = imgId;
                w1.Note = "Ghi chú UTF-8 thử nghiệm";
                w1.AllowNegativeStock = 30; // Q2: kho mới luôn lưu 0, không nhận 30 từ đầu vào.

                WarehouseSaveResult r1 = svc.Save(w1);
                if (r1.Ok) createdWarehouseIds.Add(r1.SavedId);
                WarehouseRow loaded1 = r1.Ok ? repo.LoadById(r1.SavedId) : null;
                Guid parsedGuid;
                int addLogCount = r1.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                        "@I", r1.SavedId, "@F", WarehouseText.FunctionName)
                    : 0;

                Check("Kho §7 (AC1 thêm kho mới): STATUS=30, GUID, người/giờ tạo đúng, Cửa hàng/Ảnh/Ghi chú khớp, CHOPHEPAMKHO=0, ghi lưu vết",
                    r1.Ok && loaded1 != null
                    && Guid.TryParse(loaded1.Id, out parsedGuid)
                    && loaded1.Status == 30
                    && loaded1.CreatedById == "selftest_kho"
                    && loaded1.CreatedAt.HasValue
                    && string.IsNullOrEmpty(loaded1.ModifiedById)
                    && !loaded1.ModifiedAt.HasValue
                    && loaded1.StoreId == storeA && loaded1.StoreName == "CH Kho A " + tag
                    && loaded1.ImageId == imgId
                    && loaded1.Note == "Ghi chú UTF-8 thử nghiệm"
                    && loaded1.AllowNegativeStock == 0
                    && addLogCount >= 1,
                    SingleLine(r1.ErrorMessage ?? ("logs=" + addLogCount + ", neg=" + (loaded1 == null ? -1 : loaded1.AllowNegativeStock))));

                // AC2: Trống Tên / Trống Cửa hàng / Cửa hàng không tồn tại bị chặn, không ghi dòng.
                int countBeforeInvalid = CountTableRows(db, "DKHOHANG");
                WarehouseSaveResult rEmptyName = svc.Save(new WarehouseRow { Name = "   ", StoreId = storeA });
                WarehouseSaveResult rEmptyStore = svc.Save(new WarehouseRow { Name = "Kho Không CH " + tag, StoreId = "" });
                WarehouseSaveResult rBadStore = svc.Save(new WarehouseRow { Name = "Kho CH Lạ " + tag, StoreId = "khong-ton-tai-" + tag });
                int countAfterInvalid = CountTableRows(db, "DKHOHANG");

                Check("Kho §7 (AC2 bắt buộc Tên và Cửa hàng): trống Tên, trống/sai Cửa hàng đều bị chặn, không ghi dòng",
                    !rEmptyName.Ok && rEmptyName.HasNameError
                    && !rEmptyStore.Ok && rEmptyStore.HasStoreError
                    && !rBadStore.Ok && rBadStore.HasStoreError
                    && countBeforeInvalid == countAfterInvalid,
                    "before=" + countBeforeInvalid + ", after=" + countAfterInvalid);

                // AC2: Trùng tên theo UNICODE_CI_AI bị chặn; giữ khoảng trắng; 255 ký tự OK, 256 chặn; sửa chính nó không báo trùng.
                WarehouseSaveResult rDupAccent = svc.Save(new WarehouseRow { Name = "kho ban hang thu " + tag, StoreId = storeA });
                var padded = new WarehouseRow { Name = "  " + w1.Name + "  ", StoreId = storeA };
                WarehouseSaveResult rPadded = svc.Save(padded);
                if (rPadded.Ok) createdWarehouseIds.Add(rPadded.SavedId);

                string name255 = "Kho Tiếng Việt " + new string('ê', 255 - 15);
                var w255 = new WarehouseRow { Name = name255, StoreId = storeA };
                WarehouseSaveResult r255 = svc.Save(w255);
                if (r255.Ok) createdWarehouseIds.Add(r255.SavedId);
                WarehouseRow loaded255 = r255.Ok ? repo.LoadById(r255.SavedId) : null;
                WarehouseSaveResult r256 = svc.Save(new WarehouseRow { Name = new string('ă', 256), StoreId = storeA });

                DateTime? origCreatedAt = loaded1.CreatedAt;
                string origCreatedBy = loaded1.CreatedById;
                loaded1.StoreId = storeB;
                loaded1.Note = "Đã sửa kho";
                WarehouseSaveResult rSelfEdit = svc.Save(loaded1);
                WarehouseRow afterEdit = repo.LoadById(loaded1.Id);
                int editLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", loaded1.Id, "@F", WarehouseText.FunctionName);

                Check("Kho §7 (AC2 trùng tên UNICODE_CI_AI, độ dài 255/256, sửa chính mình): chặn trùng dấu, 255 OK/256 chặn, sửa chính mình cập nhật đúng và ghi lưu vết",
                    !rDupAccent.Ok && rDupAccent.HasNameError
                    && rPadded.Ok
                    && r255.Ok && loaded255 != null && loaded255.Name == name255
                    && !r256.Ok
                    && rSelfEdit.Ok && afterEdit != null
                    && afterEdit.StoreId == storeB && afterEdit.Note == "Đã sửa kho"
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_kho" && afterEdit.ModifiedAt.HasValue
                    && editLogCount >= 2,
                    SingleLine(rSelfEdit.ErrorMessage ?? ("dup=" + rDupAccent.Ok + ", logs=" + editLogCount)));

                // AC3 (Q2): Sửa kho cũ có sẵn CHOPHEPAMKHO = 30 phải giữ nguyên giá trị 30.
                string legacyId = DbHelper.NewId();
                createdWarehouseIds.Add(legacyId);
                BookingExec(db,
                    "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @S, 30, 30, 'selftest_kho', @T)",
                    "@I", legacyId, "@N", "Kho Cũ Âm 30 " + tag, "@S", storeA, "@T", DbHelper.Now());
                WarehouseRow legacy = repo.LoadById(legacyId);
                legacy.Note = "Sửa kho cũ vẫn giữ 30";
                WarehouseSaveResult rEditLegacy = svc.Save(legacy);
                WarehouseRow legacyAfter = repo.LoadById(legacyId);
                Check("Kho §7/Q2 (AC3 cờ âm kho khi sửa kho cũ): kho có sẵn CHOPHEPAMKHO=30 sau khi sửa giữ nguyên 30",
                    rEditLegacy.Ok && legacyAfter != null && legacyAfter.AllowNegativeStock == 30 && legacyAfter.IsAllowNegativeStock,
                    "neg=" + (legacyAfter == null ? -1 : legacyAfter.AllowNegativeStock));

                // AC4 (Q1): Chặn xóa kho đang được tham chiếu ở TỪNG cột trong 6 cột tham chiếu kho, báo đúng số tham chiếu.
                CheckWarehouseDeleteBlockedOnAllSixColumns(db, svc, repo, w1.Id, tag);

                // AC4: Xóa cứng kho chưa tham chiếu (khi còn > 1 kho active) thành công và ghi lưu vết.
                // Giữ padded.Id trong createdWarehouseIds để finally dọn sạch các dòng TLUUVET của nó.
                WarehouseDeleteResult delUnused = svc.Delete(padded.Id);
                bool goneAfterDelete = repo.LoadById(padded.Id) == null;
                int delLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @F",
                    "@I", padded.Id, "@F", WarehouseText.FunctionName);
                Check("Kho §7/Q1 (AC4 xóa cứng kho không tham chiếu): xóa hẳn khỏi DKHOHANG và ghi lưu vết",
                    delUnused.Ok && goneAfterDelete && delLogCount >= 2,
                    SingleLine(delUnused.ErrorMessage ?? ("gone=" + goneAfterDelete + ", logs=" + delLogCount)));

                // AC4 (Q1): Chặn xóa kho cuối cùng (0 tham chiếu nhưng chỉ còn 1 kho STATUS=30).
                CheckLastWarehouseDeleteBlocked(db, svc, repo, w255.Id);

                // AC5: Kiểm quyền ở tầng Services (mức 112 thêm/sửa OK nhưng xóa bị chặn; mức 16 chỉ xem).
                CheckWarehouseServicePermissions(db, storeA, tag, createdWarehouseIds);
            }
            finally
            {
                foreach (string wid in createdWarehouseIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", wid);
                    SafeDeleteById(db, "DKHOHANG", wid);
                }
                SafeDeleteById(db, "SIMAGE", imgId);
                SafeDeleteById(db, "DCUAHANG", storeA);
                SafeDeleteById(db, "DCUAHANG", storeB);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = oldAuditCfg }
                }, "selftest_kho");
                SystemConfig.Reload(db);
            }
        }

        private static void CheckWarehouseDeleteBlockedOnAllSixColumns(
            string db, WarehouseService svc, WarehouseRepository repo, string warehouseId, string tag)
        {
            var failed = new List<string>();
            foreach (WarehouseReferenceColumn rc in WarehouseRepository.ReferenceColumns)
            {
                string refId = DbHelper.NewId();
                try
                {
                    InsertWarehouseRefRow(db, rc.TableName, rc.ColumnName, refId, warehouseId, "REF-KHO-" + rc.ColumnName + "-" + tag);
                    WarehouseDeleteResult del = svc.Delete(warehouseId);
                    bool stillExists = repo.LoadById(warehouseId) != null;
                    bool hasExactBlock = del.Blocks != null && del.Blocks.Exists(b =>
                        string.Equals(b.TableName, rc.TableName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(b.ColumnName, rc.ColumnName, StringComparison.OrdinalIgnoreCase)
                        && b.Count > 0);
                    bool msgHasCount = !string.IsNullOrEmpty(del.ErrorMessage) && del.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0;
                    if (del.Ok || !stillExists || del.ReferenceCount != 1 || !hasExactBlock || !msgHasCount)
                        failed.Add(rc.TableName + "." + rc.ColumnName + "(ok=" + del.Ok + ",refs=" + del.ReferenceCount + ",blk=" + hasExactBlock + ")");
                }
                catch (Exception ex)
                {
                    failed.Add(rc.TableName + "." + rc.ColumnName + "(" + SingleLine(ex.Message) + ")");
                }
                finally
                {
                    SafeDeleteById(db, rc.TableName, refId);
                }
            }
            Check("Kho §7/Q1 (AC4 chặn xóa theo 6 cột tham chiếu): chặn đủ DCUAHANG, DKHUVUC, TDONHANG(nhập/xuất), TDONHANGCHITIET, TLUUTAM và báo số tham chiếu",
                failed.Count == 0,
                failed.Count == 0 ? "6/6 cột chặn đúng" : SingleLine(string.Join("; ", failed.ToArray())));
        }

        private static void CheckLastWarehouseDeleteBlocked(string db, WarehouseService svc, WarehouseRepository repo, string soloWarehouseId)
        {
            var hiddenIds = new List<string>();
            try
            {
                DbHelper.Read(db, conn =>
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT ID FROM DKHOHANG WHERE STATUS = 30 AND ID <> @I", "@I", soloWarehouseId);
                    foreach (DataRow r in dt.Rows)
                        hiddenIds.Add(DbHelper.Str(r["ID"]));
                    return 0;
                });

                foreach (string otherId in hiddenIds)
                    BookingExec(db, "UPDATE DKHOHANG SET STATUS = 0 WHERE ID = @I", "@I", otherId);

                WarehouseDeleteResult delLast = svc.Delete(soloWarehouseId);
                bool stillExists = repo.LoadById(soloWarehouseId) != null;
                Check("Kho §7/Q1 (AC4 chặn xóa kho cuối cùng): 0 tham chiếu nhưng là kho cuối vẫn bị chặn",
                    !delLast.Ok && delLast.IsLastWarehouse && stillExists && delLast.ErrorMessage == WarehouseText.LastWarehouseBlocked,
                    SingleLine(delLast.ErrorMessage));
            }
            finally
            {
                foreach (string otherId in hiddenIds)
                    BookingExec(db, "UPDATE DKHOHANG SET STATUS = 30 WHERE ID = @I", "@I", otherId);
            }
        }

        private static void CheckWarehouseServicePermissions(string db, string storeId, string tag, List<string> createdWarehouseIds)
        {
            var perms112 = new FunctionPermissions(false);
            perms112.Add(WarehouseText.FunctionId, WarehouseText.FunctionName, RoleModes.View | RoleModes.Edit | RoleModes.Add);
            var sess112 = new UserSession { UserId = "u_thukho", UserName = "thukho", IsAdmin = false, DatabasePath = db, Permissions = perms112 };
            var svc112 = new WarehouseService(sess112);

            var row = new WarehouseRow { Name = "Kho Thủ Kho " + tag, StoreId = storeId };
            WarehouseSaveResult add112 = svc112.Save(row);
            if (add112.Ok) createdWarehouseIds.Add(add112.SavedId);
            row.Note = "Thủ kho sửa";
            WarehouseSaveResult edit112 = add112.Ok ? svc112.Save(row) : WarehouseSaveResult.Fail("skip");
            WarehouseDeleteResult del112 = add112.Ok ? svc112.Delete(add112.SavedId) : WarehouseDeleteResult.Fail("skip");

            var permsView = new FunctionPermissions(false);
            permsView.Add(WarehouseText.FunctionId, WarehouseText.FunctionName, RoleModes.View);
            var svcView = new WarehouseService(new UserSession { UserId = "u_view", IsAdmin = false, DatabasePath = db, Permissions = permsView });
            WarehouseSaveResult addView = svcView.Save(new WarehouseRow { Name = "Kho Chỉ Xem " + tag, StoreId = storeId });
            WarehouseDeleteResult delView = add112.Ok ? svcView.Delete(add112.SavedId) : WarehouseDeleteResult.Fail("skip");

            Check("Kho §7 (AC5 quyền tầng Services): mức 112 thêm/sửa OK nhưng xóa bị chặn; mức 16 chỉ xem bị chặn thêm/xóa",
                add112.Ok && edit112.Ok && !del112.Ok && del112.ErrorMessage == WarehouseText.NoPermission
                && !addView.Ok && !delView.Ok,
                "add112=" + add112.Ok + ", edit112=" + edit112.Ok + ", del112=" + del112.Ok);
        }

        private static void InsertWarehouseRefRow(string db, string table, string refColumn, string id, string warehouseId, string name)
        {
            DbHelper.InTransaction(db, (conn, tx) =>
            {
                DataTable meta = DbHelper.Table(conn, tx,
                    "SELECT TRIM(rf.RDB$FIELD_NAME) AS COL, f.RDB$FIELD_TYPE AS FTYPE, COALESCE(rf.RDB$NULL_FLAG, 0) AS NOTNULL " +
                    "FROM RDB$RELATION_FIELDS rf " +
                    "JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE " +
                    "WHERE rf.RDB$RELATION_NAME = @T",
                    "@T", table);

                var cols = new List<string>();
                var vals = new List<string>();
                var args = new List<object>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DataRow r in meta.Rows)
                {
                    string col = DbHelper.Str(r["COL"]);
                    if (string.IsNullOrEmpty(col) || !seen.Add(col))
                        continue;
                    bool required = DbHelper.Int(r["NOTNULL"]) == 1
                        || string.Equals(col, "ID", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(col, "NAME", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(col, "STATUS", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(col, "USERCREATEDID", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(col, refColumn, StringComparison.OrdinalIgnoreCase);
                    if (!required)
                        continue;

                    int ftype = DbHelper.Int(r["FTYPE"]);
                    string param = "@P" + cols.Count;
                    cols.Add("\"" + col + "\"");
                    vals.Add(param);
                    args.Add(param);
                    args.Add(DefaultWarehouseRefValue(col, refColumn, ftype, id, warehouseId, name));
                }

                string sql = "INSERT INTO \"" + table + "\" (" + string.Join(", ", cols.ToArray()) +
                             ") VALUES (" + string.Join(", ", vals.ToArray()) + ")";
                DbHelper.Exec(conn, tx, sql, args.ToArray());
            });
        }

        private static object DefaultWarehouseRefValue(string col, string refColumn, int ftype, string id, string warehouseId, string name)
        {
            if (string.Equals(col, refColumn, StringComparison.OrdinalIgnoreCase))
                return warehouseId;
            switch (col.ToUpperInvariant())
            {
                case "ID": return id;
                case "NAME": return name;
                case "STATUS": return 30;
                case "USERCREATEDID": return "selftest_kho";
            }
            if (ftype == 7 || ftype == 8 || ftype == 16 || ftype == 10 || ftype == 27)
                return 0;
            if (ftype == 12 || ftype == 35)
                return DateTime.Today;
            if (ftype == 13)
                return TimeSpan.Zero;
            return "";
        }
    }
}
