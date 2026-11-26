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
        private sealed class PaymentTestFixture
        {
            public string StoreId;
            public string UnitId;
            public string WarehouseId;
            public string AreaId;
            public string BankId;
            public string CreatedBankId;
            public string GroupDrinkId;
            public string ProdNormalId;
            public string ProdRawMatId;
            public string ProdSubBrewId;
            public string ProdBrewId;
            public string StockOrderId;
            public string CustomerGroupVipId;
            public string Customer1Id;
            public string Customer2Id;
            public string VoucherValidId;
            public string VoucherValidCode;
            public string VoucherExpiredId;
            public string VoucherExpiredCode;
            public string PrepaidCardId;
            public string PrepaidCardCode;
            public string Table1Id;
            public string Table2Id;
            public string Table3Id;
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<string> CreatedBookingIds = new List<string>();
            public readonly List<string> CreatedVoucherIds = new List<string>();
            public readonly List<string> CreatedTableIds = new List<string>();
            public readonly List<string> CreatedConfigIds = new List<string>();
        }

        private static void ServiceOrderPaymentDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            bool oldDebt = SystemConfig.ChoPhepKhachNo;
            bool oldNegStock = SystemConfig.ChoPhepAmKho;
            int oldDateMode = SystemConfig.CachChonNgayGiaoDich;
            bool oldCard = SystemConfig.CoThanhToanThe;
            bool oldCk = SystemConfig.CoThanhToanChuyenKhoan;
            bool oldVou = SystemConfig.CoThanhToanVoucher;
            bool oldPrepaid = SystemConfig.SuDungTheTraTruoc;
            bool oldPts = SystemConfig.SuDungDiemTichLuyDeThanhToan;
            bool oldUpgrade = SystemConfig.TuDongNangCapThanhVienKhiDatHanMuc;
            int oldPointMode = SystemConfig.CachTinhDiem;
            int oldRevPerPt = SystemConfig.DoanhSoTuongUngVoi1Diem;
            decimal oldPtRate = SystemConfig.QuyDoi1DiemSangTien;
            bool oldAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            bool oldMustPrint = SystemConfig.BatBuocInKhiThanhToan;

            var admin = new UserSession
            {
                UserId = "selftest-pay-1",
                UserName = "pay_admin",
                DisplayName = "Pay Admin",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            PaymentTestFixture fx = SeedPaymentFixture(db);
            try
            {
                PaymentSingleAndFundDbTests(db, cfgRepo, admin, fx);
                PaymentDebtAndDepositDbTests(db, cfgRepo, admin, fx);
                PaymentStockAndMaterialExportDbTests(db, cfgRepo, admin, fx);
                PaymentLoyaltyVoucherCardAndDateDbTests(db, cfgRepo, admin, fx);
                PaymentGroupAndRollbackDbTests(db, cfgRepo, admin, fx);
                ServiceOrderPaymentUiTests(db, cfgRepo, admin, fx);
            }
            finally
            {
                CleanupPaymentFixture(db, fx);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "ChoPhepKhachNo", DataType = 3, IntValue = oldDebt ? 30 : 0 },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = oldNegStock ? 30 : 0 },
                    new ConfigChange { Name = "CachChonNgayGiaoDich", DataType = 3, IntValue = oldDateMode },
                    new ConfigChange { Name = "CoThanhToanThe", DataType = 3, IntValue = oldCard ? 30 : 0 },
                    new ConfigChange { Name = "CoThanhToanChuyenKhoan", DataType = 3, IntValue = oldCk ? 30 : 0 },
                    new ConfigChange { Name = "CoThanhToanVoucher", DataType = 3, IntValue = oldVou ? 30 : 0 },
                    new ConfigChange { Name = "SuDungTheTraTruoc", DataType = 3, IntValue = oldPrepaid ? 30 : 0 },
                    new ConfigChange { Name = "SuDungDiemTichLuyDeThanhToan", DataType = 3, IntValue = oldPts ? 30 : 0 },
                    new ConfigChange { Name = "TuDongNangCapThanhVienKhiDatHanMuc", DataType = 3, IntValue = oldUpgrade ? 30 : 0 },
                    new ConfigChange { Name = "CachTinhDiem", DataType = 3, IntValue = oldPointMode },
                    new ConfigChange { Name = "DoanhSoTuongUngVoi1Diem", DataType = 3, IntValue = oldRevPerPt },
                    new ConfigChange { Name = "QuyDoi1DiemSangTien", DataType = 3, IntValue = (int)oldPtRate },
                    new ConfigChange { Name = "SuDungChucNangTamUngTrongDonHang", DataType = 3, IntValue = oldAdvance ? 30 : 0 },
                    new ConfigChange { Name = "BatBuocInKhiThanhToan", DataType = 3, IntValue = oldMustPrint ? 30 : 0 }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        /// <summary>
        /// Ghi giá trị nguyên cho một khóa SCONFIG trong selftest: nếu khóa chưa có dòng trong SCONFIG
        /// (như ChoPhepAmKho ở DB mẫu hiện tại) thì chèn tạm và đánh dấu để dọn sạch trong finally.
        /// </summary>
        private static void SetTestConfigInt(string db, ConfigRepository cfgRepo, PaymentTestFixture fx, string key, int intValue)
        {
            int updated = cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 3, IntValue = intValue } }, "selftest");
            if (updated == 0)
            {
                string id = DbHelper.NewId();
                DateTime now = DbHelper.Now();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, @V, 30, 'selftest', @T)",
                    "@I", id, "@N", key, "@V", intValue, "@T", now);
                fx.CreatedConfigIds.Add(id);
            }
            SystemConfig.Reload(db);
        }

        private static PaymentTestFixture SeedPaymentFixture(string db)
        {
            var fx = new PaymentTestFixture();
            string tag = DbHelper.NewId().Substring(0, 6);
            DateTime now = new InvoiceNumberRepository(db).GetServerTimestamp();

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DDONVITINH WHERE STATUS = 30 ORDER BY NAME"));
                fx.BankId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DTAIKHOANNGANHANG WHERE STATUS = 30 ORDER BY NAME"));
                if (string.IsNullOrEmpty(fx.BankId))
                {
                    fx.BankId = DbHelper.NewId();
                    fx.CreatedBankId = fx.BankId;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest', @T)",
                        "@I", fx.BankId, "@N", "TK PAY " + tag, "@T", now);
                }

                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO PAY " + tag, "@S", fx.StoreId, "@T", now);

                fx.AreaId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZP', 'selftest', @T)",
                    "@I", fx.AreaId, "@N", "KV PAY " + tag, "@W", fx.WarehouseId, "@T", now);

                fx.Table1Id = InsertTestTable(conn, tx, "PAY-B1-" + tag, fx.AreaId, 0, now);
                fx.Table2Id = InsertTestTable(conn, tx, "PAY-B2-" + tag, fx.AreaId, 0, now);
                fx.Table3Id = InsertTestTable(conn, tx, "PAY-B3-" + tag, fx.AreaId, 0, now);
                fx.CreatedTableIds.AddRange(new[] { fx.Table1Id, fx.Table2Id, fx.Table3Id });

                fx.GroupDrinkId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, '', '1', '', '', 'ZZZP', 30, 'selftest', @T)",
                    "@I", fx.GroupDrinkId, "@N", "Nhom PAY " + tag, "@T", now);

                fx.ProdNormalId = InsertTestProduct(conn, tx, "PAY Cafe " + tag, fx.GroupDrinkId, fx.UnitId, "0", 100000m, 100000m, 0, now);
                fx.ProdRawMatId = InsertTestProduct(conn, tx, "PAY Bot Tra " + tag, fx.GroupDrinkId, fx.UnitId, "2", 0m, 0m, 0, now);
                fx.ProdSubBrewId = InsertTestProduct(conn, tx, "PAY Cot Tra " + tag, fx.GroupDrinkId, fx.UnitId, "1", 0m, 0m, 0, now);
                fx.ProdBrewId = InsertTestProduct(conn, tx, "PAY Tra Dao " + tag, fx.GroupDrinkId, fx.UnitId, "1", 50000m, 50000m, 0, now);

                // Định lượng 2 tầng: Tra Dao ('1') -> 1 Cot Tra ('1') -> 2 Bot Tra ('2').
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest', @T, @P, 1, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdBrewId, "@M", fx.ProdSubBrewId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest', @T, @P, 2, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdSubBrewId, "@M", fx.ProdRawMatId);

                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-PAY-" + tag, "@D", now.Date, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdNormalId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdRawMatId, fx.UnitId, fx.WarehouseId, 5m, now);

                // Ngưỡng điểm cao (999900) để không bị trùng hoặc che bởi các nhóm khách sẵn có trong DB.
                fx.CustomerGroupVipId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMKHACHHANG (ID, NAME, STATUS, USERCREATEDID, TIMECREATED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, DIEMTICHLUY) " +
                    "VALUES (@I, @N, 30, 'selftest', @T, 'ZZZP', '', '', 0, 9981, 999900)",
                    "@I", fx.CustomerGroupVipId, "@N", "VIP PAY " + tag, "@T", now);

                fx.Customer1Id = DbHelper.NewId();
                fx.Customer2Id = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIEMTICHLUYBANDAU, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @M, @N, '123 Test', 999900, 0, 30, 'selftest', @T)",
                    "@I", fx.Customer1Id, "@M", "KHP1-" + tag, "@N", "Khach Pay 1 " + tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIEMTICHLUYBANDAU, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @M, @N, '456 Test', 0, 0, 30, 'selftest', @T)",
                    "@I", fx.Customer2Id, "@M", "KHP2-" + tag, "@N", "Khach Pay 2 " + tag, "@T", now);

                fx.VoucherValidId = DbHelper.NewId();
                fx.VoucherValidCode = "VOU-OK-" + tag;
                fx.VoucherExpiredId = DbHelper.NewId();
                fx.VoucherExpiredCode = "VOU-EXP-" + tag;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DVOUCHER (ID, NAME, GIATRI, HANSUDUNG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 50000, @H, 30, 'selftest', @T)",
                    "@I", fx.VoucherValidId, "@N", fx.VoucherValidCode, "@H", now.Date.AddDays(30), "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DVOUCHER (ID, NAME, GIATRI, HANSUDUNG, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 50000, @H, 30, 'selftest', @T)",
                    "@I", fx.VoucherExpiredId, "@N", fx.VoucherExpiredCode, "@H", now.Date.AddDays(-2), "@T", now);

                fx.PrepaidCardId = DbHelper.NewId();
                fx.PrepaidCardCode = "TTT-" + tag;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DTHETRATRUOC (ID, NAME, NGAYHETHAN, KHOA, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @H, 0, 30, 'selftest', @T)",
                    "@I", fx.PrepaidCardId, "@N", fx.PrepaidCardCode, "@H", now.Date.AddDays(30), "@T", now);

                // Nạp thẻ 300k, chi trả thẻ 100k -> số dư chuẩn desktop = 200k.
                string loadVid = DbHelper.NewId();
                string drawVid = DbHelper.NewId();
                fx.CreatedVoucherIds.Add(loadVid);
                fx.CreatedVoucherIds.Add(drawVid);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, CHUYENKHOAN, KHONGTHAYDOICONGNO, DTHETRATRUOCID, DCUAHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 300000, 0, 0, 30, @C, @S, 30, 'selftest', @T)",
                    "@I", loadVid, "@N", "PT-NAP-" + tag, "@D", now.Date, "@C", fx.PrepaidCardId, "@S", fx.StoreId, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, CHUYENKHOAN, KHONGTHAYDOICONGNO, DTHETRATRUOCID, DCUAHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 0, 100000, 0, 30, @C, @S, 30, 'selftest', @T)",
                    "@I", drawVid, "@N", "PC-TRA-" + tag, "@D", now.Date, "@C", fx.PrepaidCardId, "@S", fx.StoreId, "@T", now);
            });

            return fx;
        }

        private static ServiceOrderHeader OpenSeededBill(TableService svc, PaymentTestFixture fx, string tableId, string prodId, decimal qty, decimal? price = null, string bookingId = null)
        {
            ServiceOpResult open = svc.OpenTable(tableId, true, bookingId);
            if (!open.Ok || open.Bill == null)
                throw new InvalidOperationException("OpenTable failed: " + open.ErrorMessage);
            fx.CreatedBillIds.Add(open.Bill.Id);
            ServiceOpResult add = svc.AddItem(open.Bill.Id, tableId, prodId, qty, price);
            if (!add.Ok || add.Bill == null)
                throw new InvalidOperationException("AddItem failed: " + add.ErrorMessage);
            return add.Bill;
        }

        private static void PaymentSingleAndFundDbTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            var svc = new TableService(admin);
            var fundRepo = new FundRepository(db);

            // 1. Bill 480k chuyển khoản 500k -> CHUYENKHOAN=500k, TIENMAT=-20k, TRALAI=20k, DATHANHTOAN=30, SOTT=MAX+1, bàn trống, Tồn quỹ khớp.
            ServiceOrderHeader b1 = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 1m, 480000m);
            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
            int maxSottBefore = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COALESCE(MAX(SOTT), 0) FROM TDONHANG WHERE COALESCE(LOAI, 0) = 0 AND COALESCE(DATHANHTOAN, 0) = 30 AND NGAY = @D", "@D", today)));

            PaymentExecutionResult r1 = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = b1.Id,
                PrimaryTableId = fx.Table1Id,
                Transfer = 500000m,
                AccountId = fx.BankId
            });

            DataRow row1 = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT DATHANHTOAN, SOTT, TIENMAT, CHUYENKHOAN, TRALAI, TIENTHANHTOAN, CONLAI, CONGNO, DTAIKHOANNGANHANGID FROM TDONHANG WHERE ID = @I",
                "@I", b1.Id).Rows[0]);
            string tblLinkAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.Table1Id)));

            FundData fd = fundRepo.Load(today);
            List<FundSource> onlyB1 = fd.Sources.FindAll(s => s.Number == b1.Name);
            FundLedger bankLed = FundLedgerRules.Build(onlyB1, new FundQuery
            {
                From = today, To = today, Node = FundNodeKind.Account, AccountId = fx.BankId,
                KnownAccountIds = new List<string> { fx.BankId },
                Scope = new FundStoreScope { CoversAll = true, StoreIds = new List<string>() }
            });
            FundLedger cashLed = FundLedgerRules.Build(onlyB1, new FundQuery
            {
                From = today, To = today, Node = FundNodeKind.Cash,
                KnownAccountIds = new List<string> { fx.BankId },
                Scope = new FundStoreScope { CoversAll = true, StoreIds = new List<string>() }
            });
            Check("SDDV ThanhToan DB §7 Bill 480k CK 500k: cột TDONHANG + SOTT=MAX+1 + bàn trống + Tồn quỹ (TK +500k, TM -20k)",
                r1.Ok && DbHelper.Int(row1["DATHANHTOAN"]) == 30 && DbHelper.Int(row1["SOTT"]) == maxSottBefore + 1
                && DbHelper.Dec(row1["CHUYENKHOAN"]) == 500000m && DbHelper.Dec(row1["TIENMAT"]) == -20000m
                && DbHelper.Dec(row1["TRALAI"]) == 20000m && DbHelper.Dec(row1["TIENTHANHTOAN"]) == 480000m
                && DbHelper.Dec(row1["CONGNO"]) == 0m && string.IsNullOrEmpty(tblLinkAfter)
                && bankLed.ClosingBalance == 500000m && cashLed.ClosingBalance == -20000m,
                "tk=" + bankLed.ClosingBalance + ", tm=" + cashLed.ClosingBalance);

            // 2. Đặt trước 200k (tiền mặt) + đóng bill 480k trả TM 280k -> tổng Tiền mặt tăng đúng 480k (không 680k).
            string bookingId = DbHelper.NewId();
            fx.CreatedBookingIds.Add(bookingId);
            string tag = DbHelper.NewId().Substring(0, 5);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, 'Khach Coc', 30, 'selftest')",
                "@I", bookingId, "@G", DbHelper.NewId(), "@N", "DH-COC-" + tag, "@D", today, "@T", fx.Table1Id);

            string depReceiptNum = "PT-COC-" + tag;
            string depReceiptId = new PhieuThuRepository(db).SaveDeposit(new BookingDeposit
            {
                BookingId = bookingId,
                Number = depReceiptNum,
                Date = today,
                ObjectType = 0,
                CustomerName = "Khach Coc",
                StoreId = fx.StoreId,
                Amount = 200000m,
                Transfer = false
            }, "selftest");
            fx.CreatedVoucherIds.Add(depReceiptId);

            ServiceOrderHeader b2 = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 1m, 480000m, bookingId);
            PaymentExecutionResult r2 = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = b2.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 280000m
            });
            FundData fd2 = fundRepo.Load(today);
            List<FundSource> depAndB2 = fd2.Sources.FindAll(s => s.Number == depReceiptNum || s.Number == b2.Name);
            FundLedger cashLed2 = FundLedgerRules.Build(depAndB2, new FundQuery
            {
                From = today, To = today, Node = FundNodeKind.Cash,
                KnownAccountIds = new List<string> { fx.BankId },
                Scope = new FundStoreScope { CoversAll = true, StoreIds = new List<string>() }
            });
            Check("SDDV ThanhToan DB §7/Quỹ §7 Đặt trước 200k + đóng bill 480k trả TM 280k -> quỹ Tiền mặt tăng đúng 480k (không tính đôi 680k)",
                r2.Ok && cashLed2.ClosingBalance == 480000m,
                "cash=" + cashLed2.ClosingBalance);

            // 3. §8 bước 5: sửa phiếu thu coi CHUYENKHOAN != 0 là chuyển khoản (khớp FundRules.ForVoucher).
            string ckOneId = DbHelper.NewId();
            fx.CreatedVoucherIds.Add(ckOneId);
            BookingExec(db,
                "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, DCUAHANGID, STATUS, USERCREATEDID) " +
                "VALUES (@I, @N, @D, 0, 50000, 0, 1, @B, @S, 30, 'selftest')",
                "@I", ckOneId, "@N", "PT-CK1-" + tag, "@D", today, "@B", fx.BankId, "@S", fx.StoreId);
            BookingDeposit loadedCk1 = new PhieuThuRepository(db).LoadReceipt(ckOneId);
            Check("SDDV §8(5) Phiếu thu đọc CHUYENKHOAN = 1 (khác 0) là chuyển khoản, khớp FundRules",
                loadedCk1 != null && loadedCk1.Transfer);
        }

        private static void CleanupPaymentFixture(string db, PaymentTestFixture fx)
        {
            if (fx == null)
                return;
            try
            {
                foreach (string cid in fx.CreatedConfigIds)
                    BookingExec(db, "DELETE FROM SCONFIG WHERE ID = @I", "@I", cid);
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
                foreach (string vid in new[] { fx.VoucherValidId, fx.VoucherExpiredId })
                    if (vid != null) BookingExec(db, "DELETE FROM DVOUCHER WHERE ID = @I", "@I", vid);
                if (fx.PrepaidCardId != null)
                    BookingExec(db, "DELETE FROM DTHETRATRUOC WHERE ID = @I", "@I", fx.PrepaidCardId);
                foreach (string cid in new[] { fx.Customer1Id, fx.Customer2Id })
                    if (cid != null) BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", cid);
                if (fx.CustomerGroupVipId != null)
                    BookingExec(db, "DELETE FROM DNHOMKHACHHANG WHERE ID = @I", "@I", fx.CustomerGroupVipId);
                foreach (string pid in new[] { fx.ProdBrewId, fx.ProdSubBrewId })
                    if (pid != null) BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", pid);
                foreach (string pid in new[] { fx.ProdBrewId, fx.ProdSubBrewId, fx.ProdRawMatId, fx.ProdNormalId })
                    if (pid != null) BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", pid);
                if (fx.GroupDrinkId != null)
                    BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", fx.GroupDrinkId);
                foreach (string tid in fx.CreatedTableIds)
                    BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tid);
                if (fx.AreaId != null)
                    BookingExec(db, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", fx.AreaId);
                if (fx.WarehouseId != null)
                    BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", fx.WarehouseId);
                if (fx.CreatedBankId != null)
                    BookingExec(db, "DELETE FROM DTAIKHOANNGANHANG WHERE ID = @I", "@I", fx.CreatedBankId);
            }
            catch
            {
            }
        }
    }
}
