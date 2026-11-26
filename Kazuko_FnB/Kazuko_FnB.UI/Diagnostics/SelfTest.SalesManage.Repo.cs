using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class SalesManageTestFixture
        {
            public string Tag;
            public DateTime BusinessDate;
            public string StoreId;
            public string UnitId;
            public string WarehouseId;
            public string AreaId;
            public string BankId;
            public string CreatedBankId;
            public string GroupDrinkId;
            public string GroupFoodId;
            public string ProdNormalId;
            public string ProdLowStockId;
            public string ProdRawMatId;
            public string ProdBrewId;
            public string ProdComboId;
            public string ProdComboChildId;
            public string StockOrderId;
            public string CustGroupSilverId;
            public string CustGroupGoldId;
            public string Customer1Id;
            public string Customer2Id;
            public string Table1Id;
            public string Table2Id;
            public string Table3Id;
            public string Bill1Id;
            public string Bill2Id;
            public string Bill3Id;
            public string OpenBillId;
            public string GroupBillId;
            public string Bill2BookingId;
            public string Bill2DepositId;
            public string Bill2DeletedDepositId;
            public string Bill2PriorRefundId;
            public string Bill3RelatedVoucherId;
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<string> CreatedVoucherIds = new List<string>();
            public readonly List<string> CreatedCancelIds = new List<string>();
            public readonly List<string> CreatedConfigIds = new List<string>();
            public readonly Dictionary<string, int> SavedConfigInts = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static void SalesManageRepoAndUiTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var admin = new UserSession
            {
                UserId = "selftest-qlbh-1",
                UserName = "qlbh_admin",
                DisplayName = "Thu Ngan QLBH",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            SalesManageTestFixture fx = SeedSalesManageFixture(db);
            try
            {
                SetSalesManageConfigInt(db, cfgRepo, fx, "KichHoatLuuVetHoatDong", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "KichHoatChucNangKiemDo", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiNgayTrenHoaDon", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "CoThanhToanChuyenKhoan", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "CoThanhToanThe", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "CoThanhToanVoucher", 30);
                SetSalesManageConfigInt(db, cfgRepo, fx, "BatBuocInKhiThanhToan", 0);

                SalesManageListAndUpdateDbTests(db, admin, fx);
                SalesManageStockDebtAndAdjustDbTests(db, cfgRepo, admin, fx);
                SalesManageDepositAndCancelDbTests(db, cfgRepo, admin, fx);
                SalesManageUiTests(db, cfgRepo, admin, fx);
            }
            finally
            {
                CleanupSalesManageFixture(db, fx);
                RestoreSalesManageConfigs(db, cfgRepo, fx);
            }
        }

        private static void SetSalesManageConfigInt(string db, ConfigRepository cfgRepo, SalesManageTestFixture fx, string name, int value)
        {
            bool exists = DbHelper.Read(db, conn => DbHelper.Scalar(conn, null,
                "SELECT FIRST 1 ID FROM SCONFIG WHERE NAME = @N AND STATUS <> -30", "@N", name) != null);
            if (exists && !fx.SavedConfigInts.ContainsKey(name))
            {
                int oldVal = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 INTVALUE FROM SCONFIG WHERE NAME = @N AND STATUS <> -30", "@N", name)));
                fx.SavedConfigInts[name] = oldVal;
            }
            int updated = cfgRepo.Save(new[] { new ConfigChange { Name = name, DataType = 3, IntValue = value } }, "selftest");
            if (updated == 0)
            {
                string id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, @V, 30, 'selftest', @T)",
                    "@I", id, "@N", name, "@V", value, "@T", DbHelper.Now());
                fx.CreatedConfigIds.Add(id);
            }
            SystemConfig.Reload(db);
        }

        private static void RestoreSalesManageConfigs(string db, ConfigRepository cfgRepo, SalesManageTestFixture fx)
        {
            if (fx.SavedConfigInts.Count == 0)
                return;
            var list = new List<ConfigChange>();
            foreach (KeyValuePair<string, int> kv in fx.SavedConfigInts)
                list.Add(new ConfigChange { Name = kv.Key, DataType = 3, IntValue = kv.Value });
            cfgRepo.Save(list, "selftest");
            SystemConfig.Reload(db);
        }

        private static SalesManageTestFixture SeedSalesManageFixture(string db)
        {
            var fx = new SalesManageTestFixture();
            fx.Tag = DbHelper.NewId().Substring(0, 6);
            DateTime now = new InvoiceNumberRepository(db).GetServerTimestamp();
            fx.BusinessDate = now.Date;

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DDONVITINH WHERE STATUS = 30 ORDER BY NAME"));
                fx.BankId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DTAIKHOANNGANHANG WHERE STATUS = 30 ORDER BY NAME"));
                if (string.IsNullOrEmpty(fx.BankId))
                {
                    fx.CreatedBankId = DbHelper.NewId();
                    fx.BankId = fx.CreatedBankId;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest', @T)",
                        "@I", fx.BankId, "@N", "TK QLBH " + fx.Tag, "@T", now);
                }

                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO QLBH " + fx.Tag, "@S", fx.StoreId, "@T", now);

                fx.AreaId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZQ1', 'selftest', @T)",
                    "@I", fx.AreaId, "@N", "KV QLBH " + fx.Tag, "@W", fx.WarehouseId, "@T", now);

                fx.Table1Id = InsertTestTable(conn, tx, "QLBH-B1-" + fx.Tag, fx.AreaId, 0, now);
                fx.Table2Id = InsertTestTable(conn, tx, "QLBH-B2-" + fx.Tag, fx.AreaId, 0, now);
                fx.Table3Id = InsertTestTable(conn, tx, "QLBH-B3-" + fx.Tag, fx.AreaId, 0, now);

                fx.GroupDrinkId = DbHelper.NewId();
                fx.GroupFoodId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '1', '', '', 'ZZQ1', 30, 'selftest', @T)",
                    "@I", fx.GroupDrinkId, "@N", "Nhom Uong QLBH " + fx.Tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '0', '', '', 'ZZQ2', 30, 'selftest', @T)",
                    "@I", fx.GroupFoodId, "@N", "Nhom An QLBH " + fx.Tag, "@T", now);

                fx.ProdNormalId = InsertTestProduct(conn, tx, "QLBH Bia " + fx.Tag, fx.GroupDrinkId, fx.UnitId, ProductRules.TypeDual, 50000m, 50000m, 0, now);
                fx.ProdLowStockId = InsertTestProduct(conn, tx, "QLBH Ruou It " + fx.Tag, fx.GroupDrinkId, fx.UnitId, ProductRules.TypeDual, 40000m, 40000m, 0, now);
                fx.ProdRawMatId = InsertTestProduct(conn, tx, "QLBH Bot Cafe " + fx.Tag, fx.GroupDrinkId, fx.UnitId, ProductRules.TypeDual, 10000m, 10000m, 0, now);
                fx.ProdBrewId = InsertTestProduct(conn, tx, "QLBH Cafe Pha " + fx.Tag, fx.GroupDrinkId, fx.UnitId, ProductRules.TypeBrew, 30000m, 30000m, 0, now);
                fx.ProdComboChildId = InsertTestProduct(conn, tx, "QLBH Banh Con " + fx.Tag, fx.GroupFoodId, fx.UnitId, ProductRules.TypeDual, 15000m, 15000m, 0, now);
                fx.ProdComboId = InsertTestProduct(conn, tx, "QLBH Combo " + fx.Tag, fx.GroupFoodId, fx.UnitId, ProductRules.TypeCombo, 100000m, 100000m, 0, now);

                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest', @T, @P, 2, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdBrewId, "@M", fx.ProdRawMatId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest', @T, @P, 1, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdComboId, "@M", fx.ProdComboChildId);

                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-QLBH-" + fx.Tag, "@D", fx.BusinessDate, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdNormalId, fx.UnitId, fx.WarehouseId, 50m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdLowStockId, fx.UnitId, fx.WarehouseId, 2m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdRawMatId, fx.UnitId, fx.WarehouseId, 50m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdComboChildId, fx.UnitId, fx.WarehouseId, 50m, now);

                fx.CustGroupSilverId = DbHelper.NewId();
                fx.CustGroupGoldId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMKHACHHANG (ID, NAME, PARENTID, PARENTDIR, SORTORDER, STATUS, DIEMTICHLUY, TILEGIAMDOUONG, TILEGIAMGIATIENHANG, TILEGIAMGIA, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '', 'ZZ1', 30, 50000, 10, 0, 0, 'selftest', @T)",
                    "@I", fx.CustGroupSilverId, "@N", "Bac QLBH " + fx.Tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMKHACHHANG (ID, NAME, PARENTID, PARENTDIR, SORTORDER, STATUS, DIEMTICHLUY, TILEGIAMDOUONG, TILEGIAMGIATIENHANG, TILEGIAMGIA, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '', 'ZZ2', 30, 100000, 15, 5, 5, 'selftest', @T)",
                    "@I", fx.CustGroupGoldId, "@N", "Vang QLBH " + fx.Tag, "@T", now);

                fx.Customer1Id = DbHelper.NewId();
                fx.Customer2Id = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHACHHANG (ID, NAME, DIACHI, DNHOMKHACHHANGID, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 'HN', @G, 30, 'selftest', @T)",
                    "@I", fx.Customer1Id, "@N", "Khach Bac " + fx.Tag, "@G", fx.CustGroupSilverId, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHACHHANG (ID, NAME, DIACHI, DNHOMKHACHHANGID, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 'HCM', @G, 30, 'selftest', @T)",
                    "@I", fx.Customer2Id, "@N", "Khach Vang " + fx.Tag, "@G", fx.CustGroupGoldId, "@T", now);

                // Bill 1: 2x Bia (100k) + 1x Cafe Pha (30k) -> tổng 130k, xuất vật tư 2x Bot Cafe.
                fx.Bill1Id = InsertSeedPaidBill(conn, tx, fx, "QLBH-HD1-" + fx.Tag, fx.Table1Id, now.AddMinutes(-30), 130000m, 0m, 130000m, 0m, 0m, null, null, null, "");
                InsertSeedBillLine(conn, tx, fx.Bill1Id, fx.ProdNormalId, "QLBH Bia " + fx.Tag, fx.UnitId, fx.WarehouseId, 2m, 50000m, 0m, 0, now);
                InsertSeedBillLine(conn, tx, fx.Bill1Id, fx.ProdBrewId, "QLBH Cafe Pha " + fx.Tag, fx.UnitId, fx.WarehouseId, 1m, 30000m, 0m, 0, now);
                InsertSeedBillLine(conn, tx, fx.Bill1Id, fx.ProdRawMatId, "QLBH Bot Cafe " + fx.Tag, fx.UnitId, fx.WarehouseId, 2m, 0m, 0m, 30, now);

                // Bill 2: 6x Bia = 300k, CK 200k + TM 100k, gắn phiếu đặt TDATHANG có cọc 400k + cọc xóa mềm 500k + đã chi trước 100k.
                fx.Bill2BookingId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, DKHACHHANGID, TENKHACH, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @G, @N, @D, @D, @D, @T, @TB, @C, @CN, 30, 'selftest', @T)",
                    "@I", fx.Bill2BookingId, "@G", DbHelper.NewId(), "@N", "DH-QLBH2-" + fx.Tag,
                    "@D", fx.BusinessDate, "@T", now, "@TB", fx.Table2Id, "@C", fx.Customer1Id, "@CN", "Khach Bac " + fx.Tag);
                fx.Bill2Id = InsertSeedPaidBill(conn, tx, fx, "QLBH-HD2-" + fx.Tag, fx.Table2Id, now.AddMinutes(-20), 300000m, 0m, 100000m, 200000m, 0m, fx.BankId, fx.Customer1Id, null, "", fx.Bill2BookingId);
                InsertSeedBillLine(conn, tx, fx.Bill2Id, fx.ProdNormalId, "QLBH Bia " + fx.Tag, fx.UnitId, fx.WarehouseId, 6m, 50000m, 0m, 0, now);
                fx.Bill2DepositId = InsertSeedVoucher(conn, tx, fx, "PT-COC-QLBH-" + fx.Tag, 0, 400000m, 0m, null, fx.Bill2BookingId, 30, now);
                fx.Bill2DeletedDepositId = InsertSeedVoucher(conn, tx, fx, "PT-XOA-QLBH-" + fx.Tag, 0, 500000m, 0m, null, fx.Bill2BookingId, -30, now);
                fx.Bill2PriorRefundId = InsertSeedVoucher(conn, tx, fx, "PC-TRA-QLBH-" + fx.Tag, 1, 0m, 100000m, fx.Bill2Id, null, 30, now);

                // Bill 3: 2x Combo (200k), giảm 10% bill = 20.000 -> tổng 180k, có combo con + vật tư + TINCHEBIEN + TTHUCHI liên quan.
                fx.Bill3Id = InsertSeedPaidBill(conn, tx, fx, "QLBH-HD3-" + fx.Tag, fx.Table3Id, now.AddMinutes(-10), 200000m, 10m, 180000m, 0m, 0m, null, null, "Ghi chu HD3", "");
                string comboParentId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, TILEGIAMGIA, THANHTIEN, COMBOID, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @B, @P, @N, @U, @W, 2, 2, 100000, 0, 200000, @CI, 0, 30, 'selftest', @T)",
                    "@I", DbHelper.NewId(), "@B", fx.Bill3Id, "@P", fx.ProdComboId, "@N", "QLBH Combo " + fx.Tag, "@U", fx.UnitId, "@W", fx.WarehouseId, "@CI", comboParentId, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, THANHTIEN, COMBOPARENTID, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @B, @P, @N, @U, @W, 2, 2, 0, 0, @CP, 0, 30, 'selftest', @T)",
                    "@I", DbHelper.NewId(), "@B", fx.Bill3Id, "@P", fx.ProdComboChildId, "@N", "QLBH Banh Con " + fx.Tag, "@U", fx.UnitId, "@W", fx.WarehouseId, "@CP", comboParentId, "@T", now);
                InsertSeedBillLine(conn, tx, fx.Bill3Id, fx.ProdRawMatId, "QLBH Bot Cafe " + fx.Tag, fx.UnitId, fx.WarehouseId, 1m, 0m, 0m, 30, now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, DDONVITINHID, DLOAIDOID, LANSO, DONGIA, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @B, @N, 2, @U, '0', 1, 100000, 30, 'selftest', @T)",
                    "@I", DbHelper.NewId(), "@B", fx.Bill3Id, "@N", "QLBH Combo " + fx.Tag, "@U", fx.UnitId, "@T", now);
                fx.Bill3RelatedVoucherId = InsertSeedVoucher(conn, tx, fx, "PT-LQ-QLBH-" + fx.Tag, 0, 50000m, 0m, fx.Bill3Id, null, 30, now);

                // Bill đang mở (DATHANHTOAN = 0)
                fx.OpenBillId = DbHelper.NewId();
                fx.CreatedBillIds.Add(fx.OpenBillId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, BATDAU, DBANID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 0, 30, 0, @D, @T, @TB, @S, 'selftest', @T)",
                    "@I", fx.OpenBillId, "@N", "QLBH-OPEN-" + fx.Tag, "@D", fx.BusinessDate, "@T", now, "@TB", fx.Table1Id, "@S", fx.StoreId);

                // Bill thanh toán chung (NHOMGUID != '')
                fx.GroupBillId = InsertSeedPaidBill(conn, tx, fx, "QLBH-GRP-" + fx.Tag, fx.Table1Id, now.AddMinutes(-5), 50000m, 0m, 50000m, 0m, 0m, null, null, null, "GRP-" + fx.Tag);
                InsertSeedBillLine(conn, tx, fx.GroupBillId, fx.ProdNormalId, "QLBH Bia " + fx.Tag, fx.UnitId, fx.WarehouseId, 1m, 50000m, 0m, 0, now);
            });

            return fx;
        }

        private static string InsertSeedPaidBill(
            FirebirdSql.Data.FirebirdClient.FbConnection conn,
            FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            SalesManageTestFixture fx,
            string name, string tableId, DateTime payTime, decimal goodsTotal, decimal billDiscRate,
            decimal cash, decimal transfer, decimal card, string bankId, string custId, string note, string groupGuid, string bookingId = null)
        {
            string id = DbHelper.NewId();
            fx.CreatedBillIds.Add(id);
            decimal discMoney = Math.Round(goodsTotal * billDiscRate / 100m, 0, MidpointRounding.AwayFromZero);
            decimal grand = goodsTotal - discMoney;
            decimal paid = cash + transfer + card;
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, BATDAU, GIOTHANHTOAN, DBANID, DCUAHANGID, " +
                "DKHACHHANGID, TDATHANGID, NOTE, NHOMGUID, TIENHANG, TILEGIAMGIA, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, " +
                "TIENTHANHTOAN, DTAIKHOANNGANHANGID, USERTHANHTOANID, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, 0, 30, 30, @D, @B, @G, @TB, @S, @C, @BKID, @NT, @GR, @TH, @TD, @DM, @TC, @TM, @CK, @TH2, @TT, @BK, 'orig-cashier', 'selftest', @B)",
                "@I", id, "@N", name, "@D", fx.BusinessDate, "@B", payTime.AddMinutes(-20), "@G", payTime,
                "@TB", tableId, "@S", fx.StoreId, "@C", custId, "@BKID", bookingId, "@NT", note, "@GR", groupGuid ?? "",
                "@TH", goodsTotal, "@TD", billDiscRate, "@DM", discMoney, "@TC", grand, "@TM", cash, "@CK", transfer, "@TH2", card,
                "@TT", paid, "@BK", bankId);
            return id;
        }

        private static void InsertSeedBillLine(
            FirebirdSql.Data.FirebirdClient.FbConnection conn,
            FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string billId, string prodId, string name, string unitId, string whId,
            decimal qty, decimal price, decimal discRate, int isMatExport, DateTime now)
        {
            decimal lineTotal = Math.Round(qty * price * (1m - discRate / 100m), 0, MidpointRounding.AwayFromZero);
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, TILEGIAMGIA, THANHTIEN, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @B, @P, @N, @U, @W, @Q, @Q, @PR, @DR, @TT, @XM, 30, 'selftest', @T)",
                "@I", DbHelper.NewId(), "@B", billId, "@P", prodId, "@N", name, "@U", unitId, "@W", whId,
                "@Q", qty, "@PR", price, "@DR", discRate, "@TT", lineTotal, "@XM", isMatExport, "@T", now);
        }

        private static string InsertSeedVoucher(
            FirebirdSql.Data.FirebirdClient.FbConnection conn,
            FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            SalesManageTestFixture fx,
            string name, int loai, decimal thu, decimal chi, string billId, string bookingId, int status, DateTime now)
        {
            string id = DbHelper.NewId();
            fx.CreatedVoucherIds.Add(id);
            DbHelper.Exec(conn, tx,
                "INSERT INTO TTHUCHI (ID, NAME, LOAI, NGAY, TIMECREATED, THU, CHI, TDONHANGID, TDATHANGID, DCUAHANGID, STATUS, USERCREATEDID) " +
                "VALUES (@I, @N, @L, @D, @T, @TH, @CH, @B, @BK, @S, @ST, 'selftest')",
                "@I", id, "@N", name, "@L", loai, "@D", fx.BusinessDate, "@T", now,
                "@TH", thu, "@CH", chi, "@B", billId, "@BK", bookingId, "@S", fx.StoreId, "@ST", status);
            return id;
        }

        private static void CleanupSalesManageFixture(string db, SalesManageTestFixture fx)
        {
            if (fx == null)
                return;
            try
            {
                foreach (string cfgId in fx.CreatedConfigIds)
                    BookingExec(db, "DELETE FROM SCONFIG WHERE ID = @I", "@I", cfgId);
                foreach (string bid in fx.CreatedBillIds)
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", bid);
                foreach (string cid in fx.CreatedCancelIds)
                {
                    BookingExec(db, "DELETE FROM TDONHANGHUYCHITIET WHERE TDONHANGHUYID = @I", "@I", cid);
                    BookingExec(db, "DELETE FROM TDONHANGHUY WHERE ID = @I", "@I", cid);
                }
                foreach (string vid in fx.CreatedVoucherIds)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
                foreach (string bid in fx.CreatedBillIds)
                {
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bid);
                }
                if (!string.IsNullOrEmpty(fx.Bill2BookingId))
                {
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE TDATHANGID = @I", "@I", fx.Bill2BookingId);
                    BookingExec(db, "DELETE FROM TDATHANG WHERE ID = @I", "@I", fx.Bill2BookingId);
                }
                if (!string.IsNullOrEmpty(fx.StockOrderId))
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", fx.StockOrderId);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", fx.StockOrderId);
                }
                foreach (string pid in new[] { fx.ProdBrewId, fx.ProdComboId })
                {
                    if (!string.IsNullOrEmpty(pid))
                        BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", pid);
                }
                foreach (string pid in new[] { fx.ProdNormalId, fx.ProdLowStockId, fx.ProdRawMatId, fx.ProdBrewId, fx.ProdComboId, fx.ProdComboChildId })
                {
                    if (!string.IsNullOrEmpty(pid))
                        BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", pid);
                }
                foreach (string gid in new[] { fx.GroupDrinkId, fx.GroupFoodId })
                {
                    if (!string.IsNullOrEmpty(gid))
                        BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", gid);
                }
                foreach (string cid in new[] { fx.Customer1Id, fx.Customer2Id })
                {
                    if (!string.IsNullOrEmpty(cid))
                        BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", cid);
                }
                foreach (string cgid in new[] { fx.CustGroupSilverId, fx.CustGroupGoldId })
                {
                    if (!string.IsNullOrEmpty(cgid))
                        BookingExec(db, "DELETE FROM DNHOMKHACHHANG WHERE ID = @I", "@I", cgid);
                }
                foreach (string tid in new[] { fx.Table1Id, fx.Table2Id, fx.Table3Id })
                {
                    if (!string.IsNullOrEmpty(tid))
                        BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tid);
                }
                if (!string.IsNullOrEmpty(fx.AreaId))
                    BookingExec(db, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", fx.AreaId);
                if (!string.IsNullOrEmpty(fx.WarehouseId))
                    BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", fx.WarehouseId);
                if (!string.IsNullOrEmpty(fx.CreatedBankId))
                    BookingExec(db, "DELETE FROM DTAIKHOANNGANHANG WHERE ID = @I", "@I", fx.CreatedBankId);
            }
            catch
            {
                // Tránh che lấp lỗi assert chính nếu cleanup gặp ràng buộc.
            }
        }
    }
}
