using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Tồn kho ban đầu — phần giá vốn, rollback, chuỗi 255, quyền &amp; trùng phiếu (spec §4 #4–#5).</summary>
    public static partial class SelfTest
    {
        private sealed class OpeningStockConfigBackup
        {
            public bool Existed;
            public int OldIntValue;
            public string CreatedId;
        }

        private static void OpeningStockCostRecalcRollbackAnd255DbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { "TuDongTinhGiaVon", "KichHoatLuuVetHoatDong" });
            var lineSnap = new Dictionary<string, decimal>();
            var prodSnap = new Dictionary<string, decimal>();

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whId = DbHelper.NewId();
            string whLongId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodCost = DbHelper.NewId();
            string prodLong = DbHelper.NewId();
            var touchedWhIds = new[] { whId, whLongId };

            try
            {
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM TDONHANGCHITIET")).Rows)
                    lineSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM DMATHANG")).Rows)
                    prodSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);

                DateTime now = DbHelper.Now();
                string longWhName = "KHO_" + tag + "_" + new string('K', 255 - ("KHO_" + tag + "_").Length);
                string longProdName = "HANG_" + tag + "_" + new string('H', 255 - ("HANG_" + tag + "_").Length);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", storeId, "@N", "CH TD GV " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_td', @T)",
                    "@I", whId, "@N", "Kho TD GV " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_td', @T)",
                    "@I", whLongId, "@N", longWhName, "@S", storeId, "@T", now);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_td')",
                    "@I", unitId, "@N", "Hộp " + tag);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 5000, 25000, 30, 'selftest_td', @T)",
                    "@I", prodCost, "@C", "TDGV-" + tag, "@N", "Nguyên Liệu TD " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 1000, 2000, 30, 'selftest_td', @T)",
                    "@I", prodLong, "@C", "TD255-" + tag, "@N", longProdName, "@U", unitId, "@T", now);

                // Spec §4 #4a: Phiếu 97 tham gia CostRecalcEngine như phiếu nhập (tháng đầu 100.000 / 10 = 10.000; tháng sau 200.000 / 16 = 12.500).
                CostRecalcPlan purePlan = CostRecalcEngine.Compute(
                    new List<CostProduct> { new CostProduct { Id = "P1", TypeId = "2", CurrentCost = 0m } },
                    new List<CostRecipeLine>(),
                    new List<CostLine>
                    {
                        new CostLine { Id = "L97", ProductId = "P1", OrderType = 97, Date = new DateTime(2025, 12, 31), QtyIn = 10m, QtyOut = 0m, Amount = 100000m },
                        new CostLine { Id = "L0", ProductId = "P1", OrderType = 0, Date = new DateTime(2025, 12, 31), QtyIn = 0m, QtyOut = 4m, Amount = 0m },
                        new CostLine { Id = "L1", ProductId = "P1", OrderType = 1, Date = new DateTime(2026, 1, 10), QtyIn = 10m, QtyOut = 0m, Amount = 140000m }
                    },
                    null);
                bool engine97Ok = LineCostOf(purePlan, "L97", -1m) == 10000m
                    && LineCostOf(purePlan, "L0", -1m) == 10000m
                    && LineCostOf(purePlan, "L1", -1m) == 12500m
                    && ProductCostOf(purePlan, "P1", -1m) == 12500m;

                // Spec §4 #4b: TuDongTinhGiaVon TẮT -> lưu tồn đầu không đổi DMATHANG.GIAVON.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "TuDongTinhGiaVon", 0);
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);
                var svc = new OpeningStockService(AppSession.Current);
                OpeningStockSaveResult rOff = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whId,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodCost, Name = "Nguyên Liệu TD " + tag, UnitId = unitId, Quantity = 10m, CostPrice = 15000m }
                    }
                });
                decimal costWhenOff = ReadProductCost(db, prodCost);

                // Spec §4 #4c: TuDongTinhGiaVon BẬT -> sau lưu tồn đầu giá vốn DMATHANG.GIAVON tự đổi thành 15.000.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "TuDongTinhGiaVon", 30);
                OpeningStockSaveResult rOn = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whId,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodCost, Name = "Nguyên Liệu TD " + tag, UnitId = unitId, Quantity = 10m, CostPrice = 15000m }
                    }
                });
                decimal costWhenOn = ReadProductCost(db, prodCost);
                decimal lineCostWhenOn = rOn.Ok ? ReadOrderLineCost(db, rOn.SavedOrderId) : -1m;

                // Spec §4 #4d: Lỗi tính tự động không làm mất phiếu (trả cảnh báo, phiếu vẫn lưu thành công).
                OpeningStockSaveResult rWarn = svc.Save(
                    new OpeningStockSaveRequest
                    {
                        WarehouseId = whId,
                        Date = new DateTime(2025, 12, 31),
                        Items = new List<OpeningStockItemRow>
                        {
                            new OpeningStockItemRow { ProductId = prodCost, Name = "Nguyên Liệu TD " + tag, UnitId = unitId, Quantity = 12m, CostPrice = 16000m }
                        }
                    },
                    null,
                    delegate { throw new InvalidOperationException("Giả lập lỗi tính giá vốn tự động"); });
                bool warnKeepOrderOk = rWarn.Ok && rWarn.Warning == CostRecalcConstants.AutoFailedWarning
                    && ReadOpeningHeaders(db, whId).Rows.Count == 1
                    && new InventoryService(AppSession.Current).GetStockBalance(prodCost, whId) == 12m;

                Check("Tồn đầu P1 §4 #4 (phiếu 97 tham gia CostRecalcEngine, TuDongTinhGiaVon bật/tắt, lỗi tính tự động không mất phiếu): đạt toàn bộ",
                    engine97Ok && rOff.Ok && costWhenOff == 5000m
                    && rOn.Ok && string.IsNullOrEmpty(rOn.Warning) && costWhenOn == 15000m && lineCostWhenOn == 15000m
                    && warnKeepOrderOk,
                    SingleLine("eng=" + engine97Ok + ", off=" + costWhenOff + ", on=" + costWhenOn + "/" + lineCostWhenOn + ", warn=" + warnKeepOrderOk));

                // Spec §4 #5a: Rollback bằng hook + chuỗi 255 ký tự không làm tràn lưu vết TLUUVET + quyền chỉ Xem / khóa ngày + trùng phiếu 97.
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "TuDongTinhGiaVon", 0);
                bool rbCaught = false;
                try
                {
                    svc.Save(
                        new OpeningStockSaveRequest
                        {
                            WarehouseId = whLongId,
                            Date = new DateTime(2025, 12, 31),
                            Items = new List<OpeningStockItemRow>
                            {
                                new OpeningStockItemRow { ProductId = prodLong, Name = longProdName, UnitId = unitId, Quantity = 4m, CostPrice = 1000m }
                            }
                        },
                        delegate { throw new InvalidOperationException("Giả lập lỗi rollback tồn đầu"); });
                }
                catch (InvalidOperationException)
                {
                    rbCaught = true;
                }
                bool rbClean = rbCaught && ReadOpeningHeaders(db, whLongId).Rows.Count == 0
                    && BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F AND BAN = @B",
                        "@F", OpeningStockConstants.FunctionName, "@B", longWhName) == 0;

                // Lưu thật với tên kho & tên hàng dài đúng 255 ký tự -> ghi TDONHANGCHITIET + TLUUVET không tràn.
                OpeningStockSaveResult r255 = svc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whLongId,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodLong, Name = longProdName, UnitId = unitId, Quantity = 4m, CostPrice = 1000m }
                    }
                });
                DataTable logDt = r255.Ok
                    ? DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                        "SELECT FIRST 1 BAN, NOTE FROM TLUUVET WHERE SODONHANG = @I ORDER BY TIMECREATED DESC",
                        "@I", r255.SavedOrderId))
                    : new DataTable();
                bool clip255Ok = r255.Ok && logDt.Rows.Count == 1
                    && (DbHelper.Str(logDt.Rows[0]["BAN"]) ?? "").Length <= 255
                    && (DbHelper.Str(logDt.Rows[0]["NOTE"]) ?? "").Length <= 255;

                // Quyền: chỉ Xem không lưu được; có Sửa nhưng không có quyền đổi ngày thì không đổi được ngày phiếu.
                var viewOnlyPerms = new FunctionPermissions(false);
                viewOnlyPerms.Add("fn_td", OpeningStockConstants.FunctionName, RoleModes.View);
                var viewOnlySvc = new OpeningStockService(new UserSession
                {
                    UserId = "u_td_v", UserName = "u_td_v", DisplayName = "Chỉ xem TD",
                    IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms,
                    Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH" } }
                });
                OpeningStockSaveResult rViewDeny = viewOnlySvc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whLongId, Date = DateTime.Today, Items = new List<OpeningStockItemRow>()
                });

                var editNoDatePerms = new FunctionPermissions(false);
                editNoDatePerms.Add("fn_td", OpeningStockConstants.FunctionName, RoleModes.View | RoleModes.Edit);
                var editNoDateSvc = new OpeningStockService(new UserSession
                {
                    UserId = "u_td_nd", UserName = "u_td_nd", DisplayName = "Không đổi ngày",
                    IsAdmin = false, DatabasePath = db, Permissions = editNoDatePerms,
                    Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH" } }
                });
                OpeningStockSaveResult rNoDate = editNoDateSvc.Save(new OpeningStockSaveRequest
                {
                    WarehouseId = whLongId,
                    Date = new DateTime(2026, 6, 15),
                    Items = new List<OpeningStockItemRow>
                    {
                        new OpeningStockItemRow { ProductId = prodLong, Name = longProdName, UnitId = unitId, Quantity = 5m, CostPrice = 1000m }
                    }
                });
                DateTime dateAfterNoDateSave = Convert.ToDateTime(ReadOpeningHeaders(db, whLongId).Rows[0]["NGAY"]).Date;
                bool permOk = !viewOnlySvc.CanEdit && !rViewDeny.Ok && rViewDeny.ErrorMessage == OpeningStockConstants.NoPermission
                    && !editNoDateSvc.CanChangeDate && rNoDate.Ok && dateAfterNoDateSave == new DateTime(2025, 12, 31);

                // Trùng phiếu LOAI = 97 cùng kho -> dùng phiếu đầu theo ngày và báo cảnh báo (spec §2).
                string dupId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHONHAPID, DCUAHANGID, TONGCONG, TIENHANG, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, 'TONDAU', @D, 97, 30, 30, @W, @S, 0, 0, 'selftest_td', @T)",
                    "@I", dupId, "@D", new DateTime(2026, 1, 15), "@W", whLongId, "@S", storeId, "@T", DbHelper.Now());
                OpeningStockDocument dupDoc = svc.LoadDocument(whLongId);
                bool dupWarnOk = dupDoc.OrderId == r255.SavedOrderId && dupDoc.DuplicateOrderCount == 2
                    && !string.IsNullOrEmpty(dupDoc.Warning);

                Check("Tồn đầu P1 §4 #5 (rollback hook, chuỗi 255 ký tự không tràn lưu vết, quyền chỉ Xem & khóa đổi ngày ở Service, cảnh báo trùng phiếu 97): đạt toàn bộ",
                    rbClean && clip255Ok && permOk && dupWarnOk,
                    SingleLine("rb=" + rbClean + ", c255=" + clip255Ok + ", perm=" + permOk + ", dup=" + dupWarnOk + ", dt=" + dateAfterNoDateSave.ToString("yyyy-MM-dd")));
            }
            finally
            {
                CleanupOpeningWarehouseOrders(db, touchedWhIds);
                SafeDeleteById(db, "DMATHANG", prodCost);
                SafeDeleteById(db, "DMATHANG", prodLong);
                SafeDeleteById(db, "DDONVITINH", unitId);
                SafeDeleteById(db, "DKHOHANG", whId);
                SafeDeleteById(db, "DKHOHANG", whLongId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
                foreach (KeyValuePair<string, decimal> kv in lineSnap)
                    BookingExec(db, "UPDATE TDONHANGCHITIET SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                foreach (KeyValuePair<string, decimal> kv in prodSnap)
                    BookingExec(db, "UPDATE DMATHANG SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
            }
        }

        private static Dictionary<string, OpeningStockConfigBackup> BackupOpeningConfigs(string db, string[] keys)
        {
            var map = new Dictionary<string, OpeningStockConfigBackup>(StringComparer.Ordinal);
            DbHelper.Read(db, conn =>
            {
                foreach (string k in keys)
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT FIRST 1 INTVALUE FROM SCONFIG WHERE NAME = @N", "@N", k);
                    if (dt.Rows.Count > 0)
                        map[k] = new OpeningStockConfigBackup { Existed = true, OldIntValue = DbHelper.Int(dt.Rows[0]["INTVALUE"]) };
                    else
                        map[k] = new OpeningStockConfigBackup { Existed = false };
                }
                return 0;
            });
            return map;
        }

        private static void SetOpeningConfigInt(
            string db, ConfigRepository cfgRepo, Dictionary<string, OpeningStockConfigBackup> backups, string key, int value)
        {
            OpeningStockConfigBackup b;
            if (backups.TryGetValue(key, out b) && !b.Existed && string.IsNullOrEmpty(b.CreatedId))
            {
                string cid = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, @V, 30, 'selftest_td', @T)",
                    "@I", cid, "@N", key, "@V", value, "@T", DbHelper.Now());
                b.CreatedId = cid;
            }
            else
            {
                cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 3, IntValue = value } }, "selftest_td");
            }
            SystemConfig.Reload(db);
        }

        private static void RestoreOpeningConfigs(
            string db, ConfigRepository cfgRepo, Dictionary<string, OpeningStockConfigBackup> backups)
        {
            foreach (KeyValuePair<string, OpeningStockConfigBackup> kv in backups)
            {
                if (kv.Value.Existed)
                    cfgRepo.Save(new[] { new ConfigChange { Name = kv.Key, DataType = 3, IntValue = kv.Value.OldIntValue } }, "selftest_td");
                else if (!string.IsNullOrEmpty(kv.Value.CreatedId))
                    SafeDeleteById(db, "SCONFIG", kv.Value.CreatedId);
            }
            SystemConfig.Reload(db);
        }
    }
}
