using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class ServiceOrderTestFixture
        {
            public string AreaTopId;
            public string AreaBottomId;
            public string WarehouseId;
            public string StoreId;
            public string UnitId;
            public string GroupFoodId;
            public string GroupDrinkId;
            public string ProdNormalId;
            public string ProdLockedId;
            public string ProdLowStockId;
            public string ProdComboId;
            public string ProdComboChildId;
            public string StockOrderId;
            public string RepairId;
            public string BookingId;
            public string SeedBillInUseId;
            public string SeedBillTempPrintedId;
            public string TableEmptyId;
            public string TableInUseId;
            public string TableTempPrintedId;
            public string TableNeedCleanId;
            public string TableCleaningId;
            public string TableRepairId;
            public string TableReservedId;
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<DateTime> CreatedCounterDates = new List<DateTime>();
        }

        private static void ServiceOrderRepoAndUiTests(string db)
        {
            var configRepo = new ConfigRepository(db);
            bool oldMulti = SystemConfig.HeThongChayNhieuMayTram;
            bool oldMultiLine = SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;
            bool oldAllowAfterTemp = SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh;
            bool oldBlockAfterKitchen = SystemConfig.KhongDuocGiamDoSauKhiInPhaChe;
            bool oldReducePass = SystemConfig.NhapMatKhauGiamDo;
            int oldCycle = SystemConfig.SoHoaDonQuayVongTheo;
            bool oldFee = SystemConfig.CoPhiDichVu;
            bool oldTax = SystemConfig.CoThueSuat;

            var adminSession = new UserSession
            {
                UserId = "selftest-sddv-1",
                UserName = "sddv_user1",
                DisplayName = "User 1",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            var secondSession = new UserSession
            {
                UserId = "selftest-sddv-2",
                UserName = "sddv_user2",
                DisplayName = "User 2",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            ServiceOrderTestFixture fx = SeedServiceOrderFixture(db, adminSession);
            try
            {
                ServiceOrderTableStatusDbTests(adminSession, fx);
                ServiceOrderInvoiceNumberDbTests(db, adminSession, fx);
                ServiceOrderOpenAndLockDbTests(db, configRepo, adminSession, secondSession, fx);
                ServiceOrderOrderingAndStockDbTests(db, configRepo, adminSession, fx);
                ServiceOrderReduceDeletePermissionDbTests(db, configRepo, adminSession, fx);
                ServiceOrderUiTests(db, adminSession, fx);
            }
            finally
            {
                CleanupServiceOrderFixture(db, fx);
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "HeThongChayNhieuMayTram", DataType = 3, IntValue = oldMulti ? 30 : 0 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = oldMultiLine ? 30 : 0 },
                    new ConfigChange { Name = "ChoPhepThayDoiHoaDonSauKhiInTamTinh", DataType = 3, IntValue = oldAllowAfterTemp ? 30 : 0 },
                    new ConfigChange { Name = "KhongDuocGiamDoSauKhiInPhaChe", DataType = 3, IntValue = oldBlockAfterKitchen ? 30 : 0 },
                    new ConfigChange { Name = "NhapMatKhauGiamDo", DataType = 3, IntValue = oldReducePass ? 30 : 0 },
                    new ConfigChange { Name = "SoHoaDonQuayVongTheo", DataType = 3, IntValue = oldCycle },
                    new ConfigChange { Name = "CoPhiDichVu", DataType = 3, IntValue = oldFee ? 30 : 0 },
                    new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = oldTax ? 30 : 0 }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        private static ServiceOrderTestFixture SeedServiceOrderFixture(string db, UserSession session)
        {
            var fx = new ServiceOrderTestFixture();
            string tag = DbHelper.NewId().Substring(0, 6);
            DateTime serverNow = new InvoiceNumberRepository(db).GetServerTimestamp();

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DDONVITINH WHERE STATUS = 30 ORDER BY NAME"));
                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO SDDV " + tag, "@S", fx.StoreId, "@T", serverNow);

                fx.AreaTopId = DbHelper.NewId();
                fx.AreaBottomId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZ1', 'selftest', @T)",
                    "@I", fx.AreaTopId, "@N", "KV SDDV Tren " + tag, "@W", fx.WarehouseId, "@T", serverNow);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '2', 1, 1, 30, '', '', 'ZZZ2', 'selftest', @T)",
                    "@I", fx.AreaBottomId, "@N", "KV SDDV Duoi " + tag, "@W", fx.WarehouseId, "@T", serverNow);

                fx.TableEmptyId = InsertTestTable(conn, tx, "SDDV-B1-Trong-" + tag, fx.AreaTopId, 0, serverNow);
                fx.TableInUseId = InsertTestTable(conn, tx, "SDDV-B2-Dung-" + tag, fx.AreaTopId, 0, serverNow);
                fx.TableTempPrintedId = InsertTestTable(conn, tx, "SDDV-B3-InTam-" + tag, fx.AreaTopId, 0, serverNow);
                fx.TableNeedCleanId = InsertTestTable(conn, tx, "SDDV-B4-BaoDon-" + tag, fx.AreaTopId, 1, serverNow);
                fx.TableCleaningId = InsertTestTable(conn, tx, "SDDV-B5-DonDep-" + tag, fx.AreaTopId, 2, serverNow);
                fx.TableRepairId = InsertTestTable(conn, tx, "SDDV-B6-SuaChua-" + tag, fx.AreaTopId, 0, serverNow);
                fx.TableReservedId = InsertTestTable(conn, tx, "SDDV-B7-Dat-" + tag, fx.AreaBottomId, 0, serverNow);

                fx.SeedBillInUseId = DbHelper.NewId();
                fx.CreatedBillIds.Add(fx.SeedBillInUseId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, SOHD, LOAI, STATUS, DATHANHTOAN, NGAY, BATDAU, DBANID, DCUAHANGID, SOLANINTAMTINH, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 1, 0, 30, 0, @D, @B, @T, @S, 0, 'selftest', @B)",
                    "@I", fx.SeedBillInUseId, "@N", "SDDV-USE-" + tag, "@D", serverNow.Date, "@B", serverNow, "@T", fx.TableInUseId, "@S", fx.StoreId);

                fx.SeedBillTempPrintedId = DbHelper.NewId();
                fx.CreatedBillIds.Add(fx.SeedBillTempPrintedId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, SOHD, LOAI, STATUS, DATHANHTOAN, NGAY, BATDAU, DBANID, DCUAHANGID, SOLANINTAMTINH, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 2, 0, 30, 0, @D, @B, @T, @S, 1, 'selftest', @B)",
                    "@I", fx.SeedBillTempPrintedId, "@N", "SDDV-TMP-" + tag, "@D", serverNow.Date, "@B", serverNow, "@T", fx.TableTempPrintedId, "@S", fx.StoreId);

                fx.RepairId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TSUACHUA (ID, NAME, NGAY, DBANID, DASUAXONG, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, @T, 0, 30, 'selftest', @B)",
                    "@I", fx.RepairId, "@N", "SC-" + tag, "@D", serverNow.Date, "@T", fx.TableRepairId, "@B", serverNow);

                fx.BookingId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, TUGIO, DENGIO, GIODAT, DBANID, TENKHACH, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @G, @N, @D, @D, @D, @TG, @DG, @B, @T, 'Khach Dat', '0901234567', 30, 'selftest', @B)",
                    "@I", fx.BookingId, "@G", DbHelper.NewId(), "@N", "DH-SDDV-" + tag, "@D", serverNow.Date,
                    "@TG", serverNow.AddMinutes(10), "@DG", serverNow.AddMinutes(90), "@B", serverNow, "@T", fx.TableReservedId);

                fx.GroupFoodId = DbHelper.NewId();
                fx.GroupDrinkId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '0', '', '', 'ZZZ1', 30, 'selftest', @T)",
                    "@I", fx.GroupFoodId, "@N", "Nhom Do An " + tag, "@T", serverNow);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '1', '', '', 'ZZZ2', 30, 'selftest', @T)",
                    "@I", fx.GroupDrinkId, "@N", "Nhom Do Uong " + tag, "@T", serverNow);

                fx.ProdNormalId = InsertTestProduct(conn, tx, "SDDV Tra Sua " + tag, fx.GroupDrinkId, fx.UnitId, "0", 30000m, 35000m, 0, serverNow);
                fx.ProdLockedId = InsertTestProduct(conn, tx, "SDDV Mon Khoa " + tag, fx.GroupFoodId, fx.UnitId, "0", 20000m, 20000m, 30, serverNow);
                fx.ProdLowStockId = InsertTestProduct(conn, tx, "SDDV Ton It " + tag, fx.GroupDrinkId, fx.UnitId, "0", 25000m, 25000m, 0, serverNow);
                fx.ProdComboChildId = InsertTestProduct(conn, tx, "SDDV Banh Con " + tag, fx.GroupFoodId, fx.UnitId, "0", 15000m, 15000m, 0, serverNow);
                fx.ProdComboId = InsertTestProduct(conn, tx, "SDDV Combo " + tag, fx.GroupFoodId, fx.UnitId, "4", 55000m, 55000m, 0, serverNow);

                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) " +
                    "VALUES (@I, 30, 'selftest', @T, @P, 2, @M)",
                    "@I", DbHelper.NewId(), "@T", serverNow, "@P", fx.ProdComboId, "@M", fx.ProdComboChildId);

                // Tồn đầu kỳ (LOAI = 97) cho các mặt hàng theo dõi tồn kho.
                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-SDDV-" + tag, "@D", serverNow.Date, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", serverNow);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdNormalId, fx.UnitId, fx.WarehouseId, 100m, serverNow);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdComboChildId, fx.UnitId, fx.WarehouseId, 100m, serverNow);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdLowStockId, fx.UnitId, fx.WarehouseId, 2m, serverNow);
            });

            return fx;
        }

        private static string InsertTestTable(FirebirdSql.Data.FirebirdClient.FbConnection conn, FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string name, string areaId, int tableFlag, DateTime now)
        {
            string id = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO DBAN (ID, NAME, DKHUVUCID, CACHTINHGIO, DONGIA, TIENMOBAN, TRANGTHAI, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @A, 0, 0, 0, @F, 30, 'selftest', @T)",
                "@I", id, "@N", name, "@A", areaId, "@F", tableFlag, "@T", now);
            return id;
        }

        private static string InsertTestProduct(FirebirdSql.Data.FirebirdClient.FbConnection conn, FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string name, string groupId, string unitId, string typeId, decimal p1, decimal p2, int tempLocked, DateTime now)
        {
            string id = DbHelper.NewId();
            DbHelper.Exec(conn, tx,
                "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, GIABAN2, GIAVON, GIANHAP, TAMKHOA, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, '', @N, @G, @U, @TY, @P1, @P2, 10000, 10000, @L, 30, 'selftest', @T)",
                "@I", id, "@N", name, "@G", groupId, "@U", unitId, "@TY", typeId, "@P1", p1, "@P2", p2, "@L", tempLocked, "@T", now);
            return id;
        }

        private static void InsertOpeningStockLine(FirebirdSql.Data.FirebirdClient.FbConnection conn, FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string orderId, string prodId, string unitId, string warehouseId, decimal qty, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @O, @P, 'Ton dau', @U, @W, @Q, @Q, 0, 0, 30, 'selftest', @T)",
                "@I", DbHelper.NewId(), "@O", orderId, "@P", prodId, "@U", unitId, "@W", warehouseId, "@Q", qty, "@T", now);
        }

        private static void ServiceOrderTableStatusDbTests(UserSession session, ServiceOrderTestFixture fx)
        {
            var svc = new TableService(session);
            List<ServiceTableInfo> tables = svc.LoadTables();
            Func<string, TableStatus> st = id =>
            {
                ServiceTableInfo t = tables.Find(x => x.Id == id);
                return t == null ? (TableStatus)(-1) : t.Status;
            };
            bool all7Ok =
                st(fx.TableTempPrintedId) == TableStatus.TempPrinted
                && st(fx.TableInUseId) == TableStatus.InUse
                && st(fx.TableNeedCleanId) == TableStatus.NeedCleaning
                && st(fx.TableCleaningId) == TableStatus.Cleaning
                && st(fx.TableRepairId) == TableStatus.UnderRepair
                && st(fx.TableReservedId) == TableStatus.Reserved
                && st(fx.TableEmptyId) == TableStatus.Empty;
            ServiceTableInfo resTable = tables.Find(x => x.Id == fx.TableReservedId);
            Check("SDDV §3.1/§7 Dựng đủ 7 trạng thái bàn trên DB theo đúng thứ tự ưu tiên + nhãn bàn Đặt",
                all7Ok && resTable != null && resTable.ReservationText.Contains("Khach Dat"),
                "all7=" + all7Ok + ", res=" + (resTable != null ? resTable.ReservationText : "null"));
        }

        private static void ServiceOrderInvoiceNumberDbTests(string db, UserSession session, ServiceOrderTestFixture fx)
        {
            // Dùng ngày năm 2002 để không đụng dữ liệu thật và kiểm tra tiếp nối số có sẵn (§6.5/§7).
            DateTime d = new DateTime(2002, 5, 14);
            string seedBillId = DbHelper.NewId();
            fx.CreatedBillIds.Add(seedBillId);
            fx.CreatedCounterDates.Add(new DateTime(2002, 5, 14));
            fx.CreatedCounterDates.Add(new DateTime(2002, 5, 1));
            fx.CreatedCounterDates.Add(new DateTime(2002, 1, 1));
            fx.CreatedCounterDates.Add(new DateTime(2000, 1, 1));

            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, SOHD, LOAI, STATUS, DATHANHTOAN, NGAY, USERCREATEDID) VALUES (@I, '14050009', 9, 0, 30, 30, @D, 'selftest')",
                "@I", seedBillId, "@D", d);

            var numSvc = new InvoiceNumberService(db);
            int seqDay, seqMonth, seqYear, seqNone;
            string nDay = numSvc.AllocateNext(InvoiceCycleMode.ByDay, d, session.UserId, out seqDay);
            string nMonth = numSvc.AllocateNext(InvoiceCycleMode.ByMonth, d, session.UserId, out seqMonth);
            string nYear = numSvc.AllocateNext(InvoiceCycleMode.ByYear, d, session.UserId, out seqYear);
            string nNone = numSvc.AllocateNext(InvoiceCycleMode.NoCycle, d, session.UserId, out seqNone);

            Check("SDDV §6.5/§7 InvoiceNumberService trên DB: đủ 4 kiểu quay vòng và tiếp nối từ SOHD=9 -> 10",
                seqDay == 10 && nDay == "14050010"
                && seqMonth == 10 && nMonth == "050200010"
                && seqYear == 10 && nYear == "02000010"
                && seqNone >= 10 && nNone.Length == 7,
                nDay + " / " + nMonth + " / " + nYear + " / " + nNone);
        }

        private static void ServiceOrderOpenAndLockDbTests(string db, ConfigRepository configRepo,
            UserSession s1, UserSession s2, ServiceOrderTestFixture fx)
        {
            configRepo.Save(new[] { new ConfigChange { Name = "HeThongChayNhieuMayTram", DataType = 3, IntValue = 30 } }, "selftest");
            SystemConfig.Reload(db);

            var svc1 = new TableService(s1);
            var svc2 = new TableService(s2);

            // Mở bàn đang sửa chữa (P7, §6.4/§7): trả NeedsConfirmation trước, xác nhận mới mở.
            ServiceOpResult repAsk = svc1.OpenTable(fx.TableRepairId, false);
            Check("SDDV §6.4/§7 P7 mở bàn đang sửa chữa có cảnh báo xác nhận trước khi mở",
                !repAsk.Ok && repAsk.NeedsConfirmation && repAsk.ErrorMessage.Contains("sửa chữa"),
                repAsk.ErrorMessage);

            // Mở bàn trống (§3.3, P3): tạo 1 TDONHANG, BATDAU làm tròn phút (Second=0), trigger tự gắn DBAN.TDONHANGID.
            ServiceOpResult opened = svc1.OpenTable(fx.TableEmptyId, false);
            if (opened.Ok && opened.Bill != null)
                fx.CreatedBillIds.Add(opened.Bill.Id);
            string linkedBillId = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.TableEmptyId)));
            Check("SDDV §3.3/§7 Mở bàn trống: tạo 1 TDONHANG (DATHANHTOAN=0, BATDAU tròn phút, trigger gắn DBAN.TDONHANGID)",
                opened.Ok && opened.Bill != null && opened.Bill.PaidFlag == 0
                && opened.Bill.BatDau.Second == 0 && opened.Bill.SoHd > 0
                && linkedBillId == opened.Bill.Id,
                opened.ErrorMessage ?? ("linked=" + linkedBillId));

            // Mở lần 2 trên cùng bàn -> "Bàn này đang có khách".
            ServiceOpResult open2 = svc1.OpenTable(fx.TableEmptyId, false);
            Check("SDDV §3.3/§7 Mở lần hai trên cùng bàn báo 'Bàn này đang có khách'",
                !open2.Ok && open2.ErrorMessage == ServiceOrderConstants.TableOccupied,
                open2.ErrorMessage);

            // Hai phiên cùng mở một bàn (§3.2/§6.5/§7): phiên 2 bị chặn bằng "ĐANG ĐƯỢC THAO TÁC BỞI".
            ServiceOpResult sel2Blocked = svc2.SelectTable(fx.TableEmptyId);
            Check("SDDV §3.2/§6.5/§7 Hai phiên cùng mở một bàn: phiên 2 bị chặn bằng 'ĐANG ĐƯỢC THAO TÁC BỞI'",
                !sel2Blocked.Ok && sel2Blocked.ErrorMessage != null && sel2Blocked.ErrorMessage.Contains("ĐANG ĐƯỢC THAO TÁC BỞI"),
                sel2Blocked.ErrorMessage);

            // Hết hạn 120 giây (§6.5/§7): lùi GIULUC > 120s -> phiên 2 chọn được; nhả khóa -> chọn được.
            BookingExec(db, "UPDATE DBAN SET GIULUC = DATEADD(-130 SECOND TO CURRENT_TIMESTAMP) WHERE ID = @I", "@I", fx.TableEmptyId);
            ServiceOpResult sel2Expired = svc2.SelectTable(fx.TableEmptyId);
            svc2.UnlockTable(fx.TableEmptyId);
            ServiceOpResult sel1Back = svc1.SelectTable(fx.TableEmptyId);
            Check("SDDV §6.5/§7 Giữ bàn hết hạn sau 120 giây cho phiên khác vào và UnlockTable nhả khóa",
                sel2Expired.Ok && sel1Back.Ok && ServiceOrderConstants.TableLockRenewSeconds == 20 && ServiceOrderConstants.TableLockExpireSeconds == 120);
        }

        private static void ServiceOrderOrderingAndStockDbTests(string db, ConfigRepository configRepo, UserSession session, ServiceOrderTestFixture fx)
        {
            var svc = new TableService(session);
            ServiceOrderHeader bill = svc.SelectTable(fx.TableEmptyId).Bill;

            // Mặt hàng TAMKHOA = 30 bị chặn (§3.4/§7).
            ServiceOpResult lockedRes = svc.AddItem(bill.Id, fx.TableEmptyId, fx.ProdLockedId, 1m);
            Check("SDDV §3.4/§7 Gọi món TAMKHOA=30 bị chặn đúng thông báo",
                !lockedRes.Ok && lockedRes.ErrorMessage == ServiceOrderConstants.ProductTempLocked,
                lockedRes.ErrorMessage);

            // Âm kho mặc định chặn (§6.6): tồn = 2, gọi 3 -> chặn, nêu tên mặt hàng và tồn hiện có.
            ServiceOpResult lowRes = svc.AddItem(bill.Id, fx.TableEmptyId, fx.ProdLowStockId, 3m);
            Check("SDDV §6.6 Âm kho mặc định chặn gọi vượt tồn kho (nêu tên hàng và tồn hiện có 2)",
                !lowRes.Ok && lowRes.ErrorMessage != null && lowRes.ErrorMessage.Contains("SDDV Ton It") && lowRes.ErrorMessage.Contains("2"),
                lowRes.ErrorMessage);

            // Gộp dòng khi NhapMotMatHangNhieuLanTrongPhieu = 0 (§3.4/§7).
            configRepo.Save(new[] { new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 0 } }, "selftest");
            SystemConfig.Reload(db);
            ServiceOpResult add1 = svc.AddItem(bill.Id, fx.TableEmptyId, fx.ProdNormalId, 2m);
            ServiceOpResult add2 = svc.AddItem(bill.Id, fx.TableEmptyId, fx.ProdNormalId, 3m);
            Check("SDDV §3.4/§7 Gọi món: NhapMotMatHangNhieuLanTrongPhieu=0 gộp dòng (2+3=5, 1 dòng, tổng 150.000)",
                add1.Ok && add2.Ok && add2.Bill.Lines.Count == 1 && add2.Bill.Lines[0].Quantity == 5m && add2.Bill.TongCong == 150000m);

            // Combo sinh dòng con đơn giá 0, đổi SL cha cập nhật SL con, xóa cha xóa con (§3.4/§3.5/§7).
            ServiceOpResult addCombo = svc.AddItem(bill.Id, fx.TableEmptyId, fx.ProdComboId, 2m);
            ServiceOrderLine comboParent = addCombo.Ok ? addCombo.Bill.Lines.Find(l => l.ProductId == fx.ProdComboId && !l.IsComboChild) : null;
            ServiceOrderLine comboChild = comboParent != null ? addCombo.Bill.Lines.Find(l => l.ComboParentId == comboParent.ComboId) : null;
            bool comboCreatedOk = comboParent != null && comboChild != null && comboChild.UnitPrice == 0m && comboChild.Quantity == 4m;

            ServiceOpResult comboQtyUp = comboParent != null ? svc.SetLineQty(bill.Id, fx.TableEmptyId, comboParent.Id, 3m) : ServiceOpResult.Fail("no combo");
            ServiceOrderLine comboChildAfter = comboQtyUp.Ok ? comboQtyUp.Bill.Lines.Find(l => l.ComboParentId == comboParent.ComboId) : null;
            bool comboSyncOk = comboChildAfter != null && comboChildAfter.Quantity == 6m;

            ServiceOpResult comboDel = comboParent != null ? svc.DeleteLine(bill.Id, fx.TableEmptyId, comboParent.Id, "test") : ServiceOpResult.Fail("no combo");
            bool comboDelOk = comboDel.Ok && !comboDel.Bill.Lines.Exists(l => l.Id == comboParent.Id || l.ComboParentId == comboParent.ComboId);
            Check("SDDV §3.4/§7 Combo: sinh dòng con đơn giá 0 (SL=4), đổi SL cha 3 -> con=6, xóa cha xóa luôn con",
                comboCreatedOk && comboSyncOk && comboDelOk,
                "create=" + comboCreatedOk + ", sync=" + comboSyncOk + ", del=" + comboDelOk);
        }

        private static void ServiceOrderReduceDeletePermissionDbTests(string db, ConfigRepository configRepo, UserSession adminSession, ServiceOrderTestFixture fx)
        {
            var svc = new TableService(adminSession);
            ServiceOrderHeader bill = svc.SelectTable(fx.TableEmptyId).Bill;
            ServiceOrderLine line = bill.Lines[0]; // SL = 5

            // Giảm >> vượt SL hiện có (5 < 5 + 1) -> đúng thông báo §3.5.
            ServiceOpResult overReduce = svc.ReduceLineQty(bill.Id, fx.TableEmptyId, line.Id, 5m, false);
            Check("SDDV §3.5/§7 Nút Giảm >> khi SL < val+1 báo đúng nguyên văn",
                !overReduce.Ok && overReduce.ErrorMessage == ServiceOrderConstants.CannotReduceExceedsCurrent(5m, 5m),
                overReduce.ErrorMessage);

            // Đã in bếp (TINCHEBIEN): không giảm dưới SL đã in, không xóa (§3.5/§7).
            configRepo.Save(new[] { new ConfigChange { Name = "KhongDuocGiamDoSauKhiInPhaChe", DataType = 3, IntValue = 30 } }, "selftest");
            SystemConfig.Reload(db);
            string kitchenId = DbHelper.NewId();
            BookingExec(db,
                "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, STATUS, USERCREATEDID) VALUES (@I, @B, @N, 3, 30, 'selftest')",
                "@I", kitchenId, "@B", bill.Id, "@N", line.ItemName);

            ServiceOpResult belowKitchen = svc.SetLineQty(bill.Id, fx.TableEmptyId, line.Id, 2m);
            ServiceOpResult delKitchen = svc.DeleteLine(bill.Id, fx.TableEmptyId, line.Id, "test");
            BookingExec(db, "DELETE FROM TINCHEBIEN WHERE ID = @I", "@I", kitchenId);
            Check("SDDV §3.5/§7 Đã in bếp SL=3: chặn giảm xuống 2 và chặn xóa đúng nguyên văn",
                !belowKitchen.Ok && belowKitchen.ErrorMessage == ServiceOrderConstants.CannotReduceBelowKitchenPrint(3m)
                && !delKitchen.Ok && delKitchen.ErrorMessage == ServiceOrderConstants.CannotDeleteAfterKitchenPrint,
                belowKitchen.ErrorMessage + " | " + delKitchen.ErrorMessage);

            // Bill đã in tạm tính & ChoPhepThayDoiHoaDonSauKhiInTamTinh = 0 -> chặn đúng thông báo §3.5 (cho user không có quyền DuocGiamDoSauKhiInTamTinh).
            configRepo.Save(new[] { new ConfigChange { Name = "ChoPhepThayDoiHoaDonSauKhiInTamTinh", DataType = 3, IntValue = 0 } }, "selftest");
            SystemConfig.Reload(db);
            var staffPerms = new FunctionPermissions(false);
            staffPerms.Add(ServiceOrderConstants.FuncXoaGiamMon, "Xóa giảm món", RoleModes.View);
            var staffSession = new UserSession { UserId = "u-staff", UserName = "staff", IsAdmin = false, DatabasePath = db, Permissions = staffPerms };
            var staffSvc = new TableService(staffSession);

            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 1 WHERE ID = @I", "@I", bill.Id);
            ServiceOpResult tmpReduce = staffSvc.ReduceLineQty(bill.Id, fx.TableEmptyId, line.Id, 1m, true);
            ServiceOpResult tmpSet = staffSvc.SetLineQty(bill.Id, fx.TableEmptyId, line.Id, 2m);
            ServiceOpResult tmpDel = staffSvc.DeleteLine(bill.Id, fx.TableEmptyId, line.Id, "test");
            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 0 WHERE ID = @I", "@I", bill.Id);
            Check("SDDV §3.5/§7 Bill đã in tạm tính: chặn giảm/đặt SL/xóa đúng 3 thông báo nguyên văn",
                tmpReduce.ErrorMessage == ServiceOrderConstants.CannotReduceTempPrinted
                && tmpSet.ErrorMessage == ServiceOrderConstants.CannotSetQtyTempPrinted
                && tmpDel.ErrorMessage == ServiceOrderConstants.CannotDeleteTempPrinted);

            // §6.4/§7: Quyền chưa cấu hình -> tài khoản không phải admin không làm được; NhapMatKhauGiamDo=30 -> chặn giảm.
            var noPermSession = new UserSession { UserId = "u-none", UserName = "none", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
            var noPermSvc = new TableService(noPermSession);
            ServiceOpResult noPermReduce = noPermSvc.ReduceLineQty(bill.Id, fx.TableEmptyId, line.Id, 1m, true);
            ServiceOpResult noPermPrice = noPermSvc.ChangeLinePrice(bill.Id, fx.TableEmptyId, line.Id, 10000m);

            configRepo.Save(new[] { new ConfigChange { Name = "NhapMatKhauGiamDo", DataType = 3, IntValue = 30 } }, "selftest");
            SystemConfig.Reload(db);
            ServiceOpResult passBlocked = svc.ReduceLineQty(bill.Id, fx.TableEmptyId, line.Id, 1m, true);
            configRepo.Save(new[] { new ConfigChange { Name = "NhapMatKhauGiamDo", DataType = 3, IntValue = 0 } }, "selftest");
            SystemConfig.Reload(db);
            Check("SDDV §6.4/§7 Quyền chưa cấu hình chặn non-admin và NhapMatKhauGiamDo=30 chặn giảm món",
                !noPermReduce.Ok && noPermReduce.NeedsReLogin && !noPermPrice.Ok && noPermPrice.NeedsReLogin
                && !passBlocked.Ok && passBlocked.ErrorMessage == ServiceOrderConstants.ReducePasswordBlocked);
        }

        private static void CleanupServiceOrderFixture(string db, ServiceOrderTestFixture fx)
        {
            if (fx == null)
                return;
            try
            {
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
                foreach (DateTime dt in fx.CreatedCounterDates)
                    BookingExec(db, "DELETE FROM TSOHOADON WHERE NGAY = @D AND USERCREATEDID = 'selftest-sddv-1'", "@D", dt);
                if (fx.RepairId != null)
                    BookingExec(db, "DELETE FROM TSUACHUA WHERE ID = @I", "@I", fx.RepairId);
                if (fx.BookingId != null)
                    BookingExec(db, "DELETE FROM TDATHANG WHERE ID = @I", "@I", fx.BookingId);
                if (fx.ProdComboId != null)
                    BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", fx.ProdComboId);
                foreach (string pid in new[] { fx.ProdComboId, fx.ProdComboChildId, fx.ProdNormalId, fx.ProdLockedId, fx.ProdLowStockId })
                    if (pid != null) BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", pid);
                foreach (string gid in new[] { fx.GroupFoodId, fx.GroupDrinkId })
                    if (gid != null) BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", gid);
                foreach (string tid in new[] { fx.TableEmptyId, fx.TableInUseId, fx.TableTempPrintedId, fx.TableNeedCleanId, fx.TableCleaningId, fx.TableRepairId, fx.TableReservedId })
                    if (tid != null) BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tid);
                foreach (string aid in new[] { fx.AreaTopId, fx.AreaBottomId })
                    if (aid != null) BookingExec(db, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", aid);
                if (fx.WarehouseId != null)
                    BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", fx.WarehouseId);
            }
            catch
            {
            }
        }
    }
}
