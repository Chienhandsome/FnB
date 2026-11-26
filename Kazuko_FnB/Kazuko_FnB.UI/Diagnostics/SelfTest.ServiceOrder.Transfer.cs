using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class TransferTestFixture
        {
            public string StoreId;
            public string UnitId;
            public string WarehouseId;
            public string Area1Id;
            public string Area2Id;
            public string TableA1Id;
            public string TableA2Id;
            public string TableB1Id;
            public string TableB2Id;
            public string GroupDrinkId;
            public string GroupFoodId;
            public string ProdDrinkId;
            public string ProdFoodId;
            public string ProdComboId;
            public string ProdComboChildId;
            public string StockOrderId;
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<string> CreatedCancelIds = new List<string>();
            public readonly List<string> CreatedVoucherIds = new List<string>();
            public readonly List<string> CreatedBookingIds = new List<string>();
            public readonly Dictionary<string, int> SavedConfigInts = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static void ServiceOrderTransferTests(string db)
        {
            ServiceOrderTransferPureTests();

            var cfgRepo = new ConfigRepository(db);
            var admin = new UserSession
            {
                UserId = "selftest-trf-1",
                UserName = "trf_admin1",
                DisplayName = "Thu Ngan Chuyen 1",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            var secondUser = new UserSession
            {
                UserId = "selftest-trf-2",
                UserName = "trf_admin2",
                DisplayName = "Thu Ngan Chuyen 2",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            TransferTestFixture fx = SeedTransferFixture(db);
            try
            {
                ServiceOrderMoveTableDbTests(db, cfgRepo, admin, secondUser, fx);
                ServiceOrderMergeTableDbTests(db, cfgRepo, admin, fx);
                ServiceOrderTransferItemDbTests(db, cfgRepo, admin, fx);
                ServiceOrderCancelBillDbTests(db, cfgRepo, admin, fx);
                ServiceOrderTransferUiTests(db, cfgRepo, admin, fx);
            }
            finally
            {
                CleanupTransferFixture(db, cfgRepo, fx);
            }
        }

        private static void SetTransferConfigInt(string db, ConfigRepository cfgRepo, TransferTestFixture fx, string key, int value)
        {
            if (!fx.SavedConfigInts.ContainsKey(key))
                fx.SavedConfigInts[key] = SystemConfig.GetInt(key);
            cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 3, IntValue = value } }, "selftest");
            SystemConfig.Reload(db);
        }

        // ------------------------------------------------------------------ A. Luật thuần TableTransferRules (§3.8)

        private static void ServiceOrderTransferPureTests()
        {
            // 1. GiaMatHangKhiChuyenBan: 0=chỉ nâng, 1=luôn cập nhật, 2=giữ nguyên; hàng mở/thời giá luôn giữ.
            decimal r0Up = TableTransferRules.ResolveTransferredUnitPrice(30000m, 40000m, MoveTablePriceMode.RaiseOnly, false, false);
            decimal r0Down = TableTransferRules.ResolveTransferredUnitPrice(40000m, 30000m, MoveTablePriceMode.RaiseOnly, false, false);
            decimal r1Down = TableTransferRules.ResolveTransferredUnitPrice(40000m, 30000m, MoveTablePriceMode.AlwaysUpdate, false, false);
            decimal r2Keep = TableTransferRules.ResolveTransferredUnitPrice(30000m, 40000m, MoveTablePriceMode.KeepCurrent, false, false);
            decimal rOpen = TableTransferRules.ResolveTransferredUnitPrice(15000m, 40000m, MoveTablePriceMode.AlwaysUpdate, true, false);
            decimal rTime = TableTransferRules.ResolveTransferredUnitPrice(25000m, 40000m, MoveTablePriceMode.AlwaysUpdate, false, true);
            Check("SDDV ChuyenGop §3.8 TableTransferRules giá theo khu vực: mode 0 chỉ nâng, mode 1 luôn đổi, mode 2 giữ nguyên, hàng mở/thời giá giữ nguyên",
                r0Up == 40000m && r0Down == 40000m && r1Down == 30000m && r2Keep == 30000m && rOpen == 15000m && rTime == 25000m,
                r0Up + "/" + r0Down + "/" + r1Down + "/" + r2Keep);

            // 2. GetChenhLechInCheBienChuyenDo: đã in 4, SL 5 chuyển 2 (còn 3) -> diff = 1; đã in 2 còn 3 -> diff = 0; chuyển hết -> diff = 4; tắt in -> 0.
            decimal dPart = TableTransferRules.CalcKitchenTransferDiff(4m, 3m, 2m, true, true);
            decimal dNone = TableTransferRules.CalcKitchenTransferDiff(2m, 3m, 2m, true, true);
            decimal dAll = TableTransferRules.CalcKitchenTransferDiff(4m, 0m, 5m, true, true);
            decimal dOff = TableTransferRules.CalcKitchenTransferDiff(4m, 0m, 5m, false, true);
            Check("SDDV ChuyenGop §3.8 TableTransferRules điều chỉnh in bếp khi chuyển món: in4/còn3->1, in2/còn3->0, chuyển hết->4, tắt in->0",
                dPart == 1m && dNone == 0m && dAll == 4m && dOff == 0m,
                dPart + "/" + dNone + "/" + dAll + "/" + dOff);

            // 3. Thời hạn hủy bill (ThoiGianChoPhepHuyBill = 50 phút).
            DateTime t0 = new DateTime(2026, 11, 26, 10, 0, 0);
            Check("SDDV ChuyenGop §3.8 TableTransferRules giới hạn hủy bill 50 phút: 50p đạt, 51p chặn",
                TableTransferRules.IsWithinCancelWindow(t0, t0.AddMinutes(50), 50)
                && !TableTransferRules.IsWithinCancelWindow(t0, t0.AddMinutes(51), 50));
        }

        // ------------------------------------------------------------------ B. Chuyển bàn trên DB (§3.8, §4, §7)

        private static void ServiceOrderMoveTableDbTests(string db, ConfigRepository cfgRepo, UserSession admin, UserSession second, TransferTestFixture fx)
        {
            SetTransferConfigInt(db, cfgRepo, fx, "HeThongChayNhieuMayTram", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiHoaDonSauKhiInTamTinh", 0);
            SetTransferConfigInt(db, cfgRepo, fx, "GiaMatHangKhiChuyenBan", 0);
            SetTransferConfigInt(db, cfgRepo, fx, "KichHoatLuuVetHoatDong", 30);

            var svc = new TableService(admin);
            var noPerm = new TableService(new UserSession
            {
                UserId = "u-trf-noperm",
                UserName = "trf_noperm",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });

            // Tạo booking cho bill nguồn để kiểm giữ TDATHANGID & NHOMGUID sau khi chuyển bàn.
            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
            string tag = DbHelper.NewId().Substring(0, 5);
            string bookingId = DbHelper.NewId();
            fx.CreatedBookingIds.Add(bookingId);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, 'Khach Move', 30, 'selftest')",
                "@I", bookingId, "@G", DbHelper.NewId(), "@N", "DH-MV-" + tag, "@D", today, "@T", fx.TableA1Id);

            ServiceOrderHeader b1 = OpenTransferBill(svc, fx, fx.TableA1Id, fx.ProdDrinkId, 2m, bookingId); // Area1 giá 30k -> 60k
            ServiceOrderHeader bBusy = OpenTransferBill(svc, fx, fx.TableA2Id, fx.ProdDrinkId, 1m, null);  // bàn đang dùng
            string keepGroupGuid = DbHelper.NewId();
            BookingExec(db, "UPDATE TDONHANG SET NHOMGUID = @G WHERE ID = @I", "@G", keepGroupGuid, "@I", b1.Id);

            // Chặn: thiếu quyền, trùng bàn, đích đang có khách, đích bị máy khác giữ, bill đã in tạm tính.
            TableTransferResult rNoPerm = noPerm.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id });
            TableTransferResult rSame = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA1Id });
            TableTransferResult rBusy = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });

            BookingExec(db, "UPDATE DBAN SET TAIKHOANGIU = @U, GIULUC = CURRENT_TIMESTAMP WHERE ID = @T", "@U", second.UserName, "@T", fx.TableB1Id);
            TableTransferResult rLocked = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id });
            BookingExec(db, "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @T", "@T", fx.TableB1Id);

            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 1 WHERE ID = @I", "@I", b1.Id);
            TableTransferResult rTemp = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id });
            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 0 WHERE ID = @I", "@I", b1.Id);

            Check("SDDV ChuyenBan §3.8/§4/§7 Điều kiện chặn: thiếu quyền, trùng bàn, đích đang dùng, đích bị giữ, đã in tạm tính đúng thông báo",
                !rNoPerm.Ok && rNoPerm.NeedsReLogin && rNoPerm.RequiredFunctionId == ServiceOrderConstants.FuncChuyenBan
                && !rSame.Ok && rSame.ErrorMessage == ServiceTransferConstants.ChooseDifferentTable
                && !rBusy.Ok && rBusy.ErrorMessage == ServiceTransferConstants.DestTableInUseForMove
                && !rLocked.Ok && rLocked.ErrorMessage != null && rLocked.ErrorMessage.Contains("đang bị giữ bởi")
                && !rTemp.Ok && rTemp.ErrorMessage == ServiceTransferConstants.TableTempPrintedBlocked,
                (rBusy.ErrorMessage ?? "") + " | " + (rTemp.ErrorMessage ?? ""));

            // Lỗi giữa chừng -> rollback sạch.
            bool caughtMoveCrash = false;
            try
            {
                svc.MoveTable(new MoveTableRequest
                {
                    SourceBillId = b1.Id,
                    SourceTableId = fx.TableA1Id,
                    TargetTableId = fx.TableB1Id,
                    TestHookBeforeCommit = delegate { throw new InvalidOperationException("Simulated move fail"); }
                });
            }
            catch (InvalidOperationException) { caughtMoveCrash = true; }
            string tableAfterCrash = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT DBANID FROM TDONHANG WHERE ID = @I", "@I", b1.Id)));

            // Chuyển bàn thành công từ Area1 (giá 1 = 30k) sang Area2 TableB1 (giá 2 = 40k), GiaMatHangKhiChuyenBan=0 -> nâng 40k -> 80k!
            TableTransferResult moved = svc.MoveTable(new MoveTableRequest
            {
                SourceBillId = b1.Id,
                SourceTableId = fx.TableA1Id,
                TargetTableId = fx.TableB1Id
            });
            string oldTableBill = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.TableA1Id)));
            string newTableBill = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.TableB1Id)));
            string oldLock = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TAIKHOANGIU FROM DBAN WHERE ID = @I", "@I", fx.TableA1Id)));
            string newLock = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TAIKHOANGIU FROM DBAN WHERE ID = @I", "@I", fx.TableB1Id)));
            int moveLogs = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", b1.Id, "@K", (int)ActivityKind.ChuyenBan)));

            Check("SDDV ChuyenBan §3.8/§7 Chuyển bàn thành công + rollback: trigger đổi DBAN.TDONHANGID, giữ NHOMGUID & TDATHANGID, nâng giá 30k->40k (tổng 80k), chuyển khóa bàn, ghi TLUUVET",
                caughtMoveCrash && tableAfterCrash == fx.TableA1Id
                && moved.Ok && moved.TargetBill != null
                && string.IsNullOrEmpty(oldTableBill) && newTableBill == b1.Id
                && moved.TargetBill.GroupGuid == keepGroupGuid && moved.TargetBill.BookingId == bookingId
                && moved.TargetBill.Lines[0].UnitPrice == 40000m && moved.TargetBill.TongCong == 80000m
                && string.IsNullOrEmpty(oldLock) && newLock == admin.UserName
                && moveLogs >= 1,
                "tot=" + (moved.TargetBill != null ? moved.TargetBill.TongCong.ToString() : "null") + ", newLock=" + newLock);

            // Chuyển ngược về TableA1 với GiaMatHangKhiChuyenBan=2 (giữ 40k), rồi chuyển sang TableB1 với mode=1 và ngược lại TableA1 (hạ về 30k -> 60k).
            SetTransferConfigInt(db, cfgRepo, fx, "GiaMatHangKhiChuyenBan", 2);
            TableTransferResult moveKeep = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableB1Id, TargetTableId = fx.TableA1Id });
            SetTransferConfigInt(db, cfgRepo, fx, "GiaMatHangKhiChuyenBan", 1);
            TableTransferResult moveBackB1 = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id });
            TableTransferResult moveLowerA1 = svc.MoveTable(new MoveTableRequest { SourceBillId = b1.Id, SourceTableId = fx.TableB1Id, TargetTableId = fx.TableA1Id });
            Check("SDDV ChuyenBan §3.8 GiaMatHangKhiChuyenBan: mode 2 giữ nguyên 40k, mode 1 cập nhật theo khu vực mới (hạ 40k->30k, tổng 60k)",
                moveKeep.Ok && moveKeep.TargetBill.TongCong == 80000m
                && moveBackB1.Ok && moveLowerA1.Ok && moveLowerA1.TargetBill.TongCong == 60000m);

            // Dọn 2 bill thử bằng HardDelete để giải phóng bàn cho phần test tiếp theo.
            CleanupSingleBill(db, b1.Id, fx.TableA1Id);
            CleanupSingleBill(db, bBusy.Id, fx.TableA2Id);
        }

        private static ServiceOrderHeader OpenTransferBill(TableService svc, TransferTestFixture fx, string tableId, string prodId, decimal qty, string bookingId)
        {
            ServiceOpResult open = svc.OpenTable(tableId, true, bookingId);
            if (!open.Ok || open.Bill == null)
                throw new InvalidOperationException("OpenTransferBill failed: " + open.ErrorMessage);
            fx.CreatedBillIds.Add(open.Bill.Id);
            if (!string.IsNullOrEmpty(prodId) && qty > 0m)
            {
                ServiceOpResult add = svc.AddItem(open.Bill.Id, tableId, prodId, qty);
                if (!add.Ok || add.Bill == null)
                    throw new InvalidOperationException("AddItem failed: " + add.ErrorMessage);
                return add.Bill;
            }
            return open.Bill;
        }

        private static void CleanupSingleBill(string db, string billId, string tableId)
        {
            if (!string.IsNullOrEmpty(billId))
            {
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", billId);
                BookingExec(db, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", billId);
                BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", billId);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", billId);
            }
            if (!string.IsNullOrEmpty(tableId))
                BookingExec(db, "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @I", "@I", tableId);
        }

        private static TransferTestFixture SeedTransferFixture(string db)
        {
            var fx = new TransferTestFixture();
            string tag = DbHelper.NewId().Substring(0, 6);
            DateTime now = new InvoiceNumberRepository(db).GetServerTimestamp();

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DDONVITINH WHERE STATUS = 30 ORDER BY NAME"));
                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO TRF " + tag, "@S", fx.StoreId, "@T", now);

                fx.Area1Id = DbHelper.NewId();
                fx.Area2Id = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZT1', 'selftest', @T)",
                    "@I", fx.Area1Id, "@N", "KV TRF1 " + tag, "@W", fx.WarehouseId, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '2', 1, 0, 30, '', '', 'ZZT2', 'selftest', @T)",
                    "@I", fx.Area2Id, "@N", "KV TRF2 " + tag, "@W", fx.WarehouseId, "@T", now);

                fx.TableA1Id = InsertTestTable(conn, tx, "TRF-A1-" + tag, fx.Area1Id, 0, now);
                fx.TableA2Id = InsertTestTable(conn, tx, "TRF-A2-" + tag, fx.Area1Id, 0, now);
                fx.TableB1Id = InsertTestTable(conn, tx, "TRF-B1-" + tag, fx.Area2Id, 0, now);
                fx.TableB2Id = InsertTestTable(conn, tx, "TRF-B2-" + tag, fx.Area2Id, 0, now);

                fx.GroupFoodId = DbHelper.NewId();
                fx.GroupDrinkId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '0', '', '', 'ZZT1', 30, 'selftest', @T)",
                    "@I", fx.GroupFoodId, "@N", "Nhom An TRF " + tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '1', '', '', 'ZZT2', 30, 'selftest', @T)",
                    "@I", fx.GroupDrinkId, "@N", "Nhom Uong TRF " + tag, "@T", now);

                // ProdDrink: giá1 = 30k, giá2 = 40k; ProdFood: giá1 = 50k, giá2 = 60k; Combo: 55k gồm 2 ComboChild.
                fx.ProdDrinkId = InsertCodedTestProduct(conn, tx, "TRF-DU", "TRF Tra Dao " + tag, fx.GroupDrinkId, fx.UnitId, "0", 30000m, 40000m, now);
                fx.ProdFoodId = InsertCodedTestProduct(conn, tx, "TRF-DA", "TRF Ga Ran " + tag, fx.GroupFoodId, fx.UnitId, "0", 50000m, 60000m, now);
                fx.ProdComboChildId = InsertCodedTestProduct(conn, tx, "TRF-CC", "TRF Banh Con " + tag, fx.GroupFoodId, fx.UnitId, "0", 15000m, 15000m, now);
                fx.ProdComboId = InsertCodedTestProduct(conn, tx, "TRF-CB", "TRF Combo " + tag, fx.GroupFoodId, fx.UnitId, "4", 55000m, 55000m, now);

                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) " +
                    "VALUES (@I, 30, 'selftest', @T, @P, 2, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdComboId, "@M", fx.ProdComboChildId);

                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-TRF-" + tag, "@D", now.Date, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdDrinkId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdFoodId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdComboChildId, fx.UnitId, fx.WarehouseId, 200m, now);
            });

            return fx;
        }

        private static string InsertCodedTestProduct(FirebirdSql.Data.FirebirdClient.FbConnection conn, FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string code, string name, string groupId, string unitId, string typeId, decimal p1, decimal p2, DateTime now)
        {
            string id = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, GIABAN2, GIAVON, GIANHAP, TAMKHOA, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @C, @N, @G, @U, @TY, @P1, @P2, 10000, 10000, 0, 30, 'selftest', @T)",
                "@I", id, "@C", code, "@N", name, "@G", groupId, "@U", unitId, "@TY", typeId, "@P1", p1, "@P2", p2, "@T", now);
            return id;
        }

        private static void CleanupTransferFixture(string db, ConfigRepository cfgRepo, TransferTestFixture fx)
        {
            if (fx == null)
                return;
            try
            {
                foreach (string cid in fx.CreatedCancelIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGHUYCHITIET WHERE TDONHANGHUYID = @I", "@I", cid);
                    BookingExec(db, "DELETE FROM TDONHANGHUY WHERE ID = @I", "@I", cid);
                }
                foreach (string vid in fx.CreatedVoucherIds)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
                foreach (string bid in fx.CreatedBillIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bid);
                }
                if (fx.StockOrderId != null)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", fx.StockOrderId);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", fx.StockOrderId);
                }
                foreach (string bk in fx.CreatedBookingIds)
                    BookingExec(db, "DELETE FROM TDATHANG WHERE ID = @I", "@I", bk);
                if (fx.ProdComboId != null)
                    BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", fx.ProdComboId);
                foreach (string pid in new[] { fx.ProdComboId, fx.ProdComboChildId, fx.ProdDrinkId, fx.ProdFoodId })
                    if (pid != null) BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", pid);
                foreach (string gid in new[] { fx.GroupFoodId, fx.GroupDrinkId })
                    if (gid != null) BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", gid);
                foreach (string tid in new[] { fx.TableA1Id, fx.TableA2Id, fx.TableB1Id, fx.TableB2Id })
                    if (tid != null) BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tid);
                foreach (string aid in new[] { fx.Area1Id, fx.Area2Id })
                    if (aid != null) BookingExec(db, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", aid);
                if (fx.WarehouseId != null)
                    BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", fx.WarehouseId);
            }
            catch { }

            if (fx.SavedConfigInts.Count > 0)
            {
                var changes = new List<ConfigChange>(fx.SavedConfigInts.Count);
                foreach (KeyValuePair<string, int> kv in fx.SavedConfigInts)
                    changes.Add(new ConfigChange { Name = kv.Key, DataType = 3, IntValue = kv.Value });
                cfgRepo.Save(changes.ToArray(), "selftest");
                SystemConfig.Reload(db);
            }
        }
    }
}
