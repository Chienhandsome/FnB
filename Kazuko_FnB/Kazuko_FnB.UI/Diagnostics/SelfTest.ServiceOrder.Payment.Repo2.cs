using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PaymentDebtAndDepositDbTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            var svc = new TableService(admin);
            var payRepo = new ServicePaymentRepository(db);
            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;

            // 1. Nợ khách: ChoPhepKhachNo=0 chặn; bật 30 mà chưa chọn khách hoặc khách không có hồ sơ DKHACHHANG -> chặn (§6.6).
            SetTestConfigInt(db, cfgRepo, fx, "ChoPhepKhachNo", 0);

            ServiceOrderHeader bDebt = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 3m, 100000m); // 300k
            PaymentExecutionResult blockedOff = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDebt.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer1Id,
                CashTendered = 200000m,
                ConfirmDebt = true
            });

            SetTestConfigInt(db, cfgRepo, fx, "ChoPhepKhachNo", 30);

            PaymentExecutionResult blockedNoCust = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDebt.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 200000m,
                ConfirmDebt = true
            });
            PaymentExecutionResult blockedGhost = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDebt.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = "khong-ton-tai-dkhachhang",
                CashTendered = 200000m,
                ConfirmDebt = true
            });
            PaymentExecutionResult debtOk = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDebt.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer1Id,
                CashTendered = 200000m,
                ConfirmDebt = true
            });
            decimal oldDebtAfter1 = payRepo.GetOldDebt(fx.Customer1Id);

            // Bill tiếp theo 200k, thu kèm nợ cũ 100k (trả 300k) -> GetNoCu về 0.
            ServiceOrderHeader bPayOld = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 2m, 100000m);
            PaymentExecutionResult payOldOk = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bPayOld.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer1Id,
                CashTendered = 300000m
            });
            decimal oldDebtAfter2 = payRepo.GetOldDebt(fx.Customer1Id);

            Check("SDDV ThanhToan DB §6.6/§7 Nợ khách: chặn khi tắt nợ, chặn khi chưa chọn khách/khách lạ; nợ 100k -> GetNoCu=100k -> thu kèm nợ cũ -> GetNoCu=0",
                !blockedOff.Ok && blockedOff.ErrorMessage == ServicePaymentConstants.DebtDisabled
                && !blockedNoCust.Ok && blockedNoCust.ErrorMessage == ServicePaymentConstants.CustomerRequiredForDebt
                && !blockedGhost.Ok && blockedGhost.ErrorMessage == ServicePaymentConstants.CustomerNotFound
                && debtOk.Ok && oldDebtAfter1 == 100000m
                && payOldOk.Ok && oldDebtAfter2 == 0m,
                "debt1=" + oldDebtAfter1 + ", debt2=" + oldDebtAfter2);

            // 2. Q16 Bill đơn cọc dư trên DB: bill 300k cọc 400k -> TIENTHANHTOAN=300k, GetNoCu=0, phiếu chi 100k trừ quỹ 1 lần.
            string tag = DbHelper.NewId().Substring(0, 5);
            string bkQ16 = DbHelper.NewId();
            fx.CreatedBookingIds.Add(bkQ16);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, DKHACHHANGID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, @C, 'Khach Q16', 30, 'selftest')",
                "@I", bkQ16, "@G", DbHelper.NewId(), "@N", "DH-Q16-" + tag, "@D", today, "@T", fx.Table1Id, "@C", fx.Customer2Id);
            string depQ16Id = new PhieuThuRepository(db).SaveDeposit(new BookingDeposit
            {
                BookingId = bkQ16,
                Number = "PT-Q16-" + tag,
                Date = today,
                ObjectType = 2,
                CustomerId = fx.Customer2Id,
                CustomerName = "Khach Q16",
                StoreId = fx.StoreId,
                Amount = 400000m,
                Transfer = false
            }, "selftest");
            fx.CreatedVoucherIds.Add(depQ16Id);

            ServiceOrderHeader bQ16 = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 3m, 100000m, bkQ16);
            PaymentExecutionResult rQ16 = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bQ16.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer2Id
            });
            decimal paidColQ16 = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                "SELECT TIENTHANHTOAN FROM TDONHANG WHERE ID = @I", "@I", bQ16.Id)));
            decimal debtCust2 = payRepo.GetOldDebt(fx.Customer2Id);

            RefundVoucherResult refundQ16 = svc.SaveRefundDepositVoucher(new RefundVoucherInput
            {
                Number = "PC-Q16-" + tag,
                Date = today,
                PrimaryBillId = bQ16.Id,
                CustomerId = fx.Customer2Id,
                CustomerName = "Khach Q16",
                StoreId = fx.StoreId,
                Note = ServicePaymentConstants.RefundDepositNote(bQ16.Name),
                Amount = rQ16.ExcessDeposit,
                Transfer = false
            });
            if (refundQ16.Ok && refundQ16.SavedId != null)
                fx.CreatedVoucherIds.Add(refundQ16.SavedId);

            FundData fdQ16 = new FundRepository(db).Load(today);
            List<FundSource> q16Sources = fdQ16.Sources.FindAll(s => s.Number == "PT-Q16-" + tag || s.Number == "PC-Q16-" + tag || s.Number == bQ16.Name);
            FundLedger q16Cash = FundLedgerRules.Build(q16Sources, new FundQuery
            {
                From = today, To = today, Node = FundNodeKind.Cash,
                KnownAccountIds = new List<string> { fx.BankId },
                Scope = new FundStoreScope { CoversAll = true, StoreIds = new List<string>() }
            });
            Check("SDDV ThanhToan DB §7 Q16 Bill 300k cọc 400k: TIENTHANHTOAN=300k, GetNoCu=0, phiếu chi 100k -> quỹ Tiền mặt ròng 300k",
                rQ16.Ok && rQ16.ExcessDeposit == 100000m && paidColQ16 == 300000m && debtCust2 == 0m
                && refundQ16.Ok && q16Cash.ClosingBalance == 300000m && payRepo.GetOldDebt(fx.Customer2Id) == 0m,
                "paid=" + paidColQ16 + ", cash=" + q16Cash.ClosingBalance);
        }

        private static void PaymentStockAndMaterialExportDbTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            var svc = new TableService(admin);
            // Tồn Bot Tra = 5. Tra Dao ('1') -> Cot Tra ('1') -> 2 Bot Tra ('2').
            // Tắt âm kho (ChoPhepAmKho = 0): gọi 2 Tra Dao (= 4 Bot Tra <= 5), rồi hạ tồn xuống 3 trước khi thanh toán.
            SetTestConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 0);

            ServiceOrderHeader bBrew = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdBrewId, 2m); // cần 4 Bot Tra
            // Thêm dòng TINCHEBIEN để kiểm tra thanh toán xóa sạch TINCHEBIEN.
            BookingExec(db,
                "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, STATUS, USERCREATEDID) VALUES (@I, @B, 'PAY Tra Dao', 2, 30, 'selftest')",
                "@I", DbHelper.NewId(), "@B", bBrew.Id);

            // Hạ tồn Bot Tra từ 5 xuống 3 -> thanh toán cần 4 phải bị CHẶN (nêu tên nguyên liệu & tồn 3).
            BookingExec(db, "UPDATE TDONHANGCHITIET SET SLNHAP = 3, SLNHAPCHUAQUYDOI = 3 WHERE TDONHANGID = @O AND DMATHANGID = @P",
                "@O", fx.StockOrderId, "@P", fx.ProdRawMatId);
            PaymentExecutionResult blockedStock = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bBrew.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 100000m
            });
            int paidFlagBlocked = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT DATHANHTOAN FROM TDONHANG WHERE ID = @I", "@I", bBrew.Id)));

            // Khôi phục tồn Bot Tra = 5 -> thanh toán thành công, sinh dòng XUATVATTU=30 (SL=4), tồn còn 1, TINCHEBIEN = 0.
            BookingExec(db, "UPDATE TDONHANGCHITIET SET SLNHAP = 5, SLNHAPCHUAQUYDOI = 5 WHERE TDONHANGID = @O AND DMATHANGID = @P",
                "@O", fx.StockOrderId, "@P", fx.ProdRawMatId);
            PaymentExecutionResult brewPaid = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bBrew.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 100000m
            });
            decimal exportedQty = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                "SELECT COALESCE(SUM(SLXUAT), 0) FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND XUATVATTU = 30 AND DMATHANGID = @M",
                "@B", bBrew.Id, "@M", fx.ProdRawMatId)));
            int kitchenLeft = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TINCHEBIEN WHERE TDONHANGID = @B", "@B", bBrew.Id)));
            decimal stockLeft = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, fx.ProdRawMatId, fx.WarehouseId, ""));

            // Bật ChoPhepAmKho = 30 -> bán thêm 1 Tra Dao (cần 2, tồn 1) -> được phép, tồn về -1.
            SetTestConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 30);
            ServiceOrderHeader bNeg = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdBrewId, 1m);
            PaymentExecutionResult negOk = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bNeg.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 50000m
            });
            decimal stockAfterNeg = DbHelper.Read(db, conn => ServiceOrderRepository.GetStockBalanceIn(conn, null, fx.ProdRawMatId, fx.WarehouseId, ""));
            SetTestConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 0);

            Check("SDDV ThanhToan DB §3.7/§6.6 Xuất vật tư 2 tầng & chặn âm kho (tồn 3 cần 4 chặn; tồn 5 xuất 4 còn 1 & xóa TINCHEBIEN; ChoPhepAmKho=30 cho xuống -1)",
                !blockedStock.Ok && blockedStock.ErrorMessage != null && blockedStock.ErrorMessage.Contains("PAY Bot Tra") && blockedStock.ErrorMessage.Contains("3")
                && paidFlagBlocked == 0
                && brewPaid.Ok && exportedQty == 4m && kitchenLeft == 0 && stockLeft == 1m
                && negOk.Ok && stockAfterNeg == -1m,
                "exp=" + exportedQty + ", left=" + stockLeft + ", neg=" + stockAfterNeg);
        }

        private static void PaymentLoyaltyVoucherCardAndDateDbTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            var svc = new TableService(admin);
            SetTestConfigInt(db, cfgRepo, fx, "CachTinhDiem", 1);
            SetTestConfigInt(db, cfgRepo, fx, "DoanhSoTuongUngVoi1Diem", 20000);
            SetTestConfigInt(db, cfgRepo, fx, "QuyDoi1DiemSangTien", 1000);
            SetTestConfigInt(db, cfgRepo, fx, "TuDongNangCapThanhVienKhiDatHanMuc", 30);

            // Voucher quá hạn bị chặn; thẻ trả trước số dư 200k (300 nạp - 100 chi).
            VoucherInfo vExp = svc.ValidateVoucherCode(fx.VoucherExpiredCode, null);
            VoucherInfo vOk = svc.ValidateVoucherCode(fx.VoucherValidCode, null);
            PrepaidCardInfo cardInfo = svc.ValidatePrepaidCardCode(fx.PrepaidCardCode, null);

            // Thanh toán bill 250k bằng Voucher 50k + Thẻ trả trước 150k + Trừ tích lũy 10k (10 điểm) + Tiền mặt 40k.
            ServiceOrderHeader bMix = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 1m, 250000m);
            PaymentExecutionResult cardOver = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bMix.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer1Id,
                PrepaidCard = 250000m,
                PrepaidCardId = fx.PrepaidCardId
            });
            PaymentExecutionResult mixOk = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bMix.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.Customer1Id,
                Voucher = 50000m,
                VoucherId = fx.VoucherValidId,
                PrepaidCard = 150000m,
                PrepaidCardId = fx.PrepaidCardId,
                PointsMoney = 10000m,
                CashTendered = 40000m
            });
            VoucherInfo vReused = svc.ValidateVoucherCode(fx.VoucherValidCode, null);
            PrepaidCardInfo cardAfter = svc.ValidatePrepaidCardCode(fx.PrepaidCardCode, null);
            string cust1GroupAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT DNHOMKHACHHANGID FROM DKHACHHANG WHERE ID = @I", "@I", fx.Customer1Id)));

            Check("SDDV ThanhToan DB §3.7 Voucher (hết hạn chặn, dùng 1 lần khóa), Thẻ trả trước (300-100=200k, vượt số dư chặn, tiêu 150k còn 50k), Điểm & tự nâng nhóm VIP",
                !vExp.IsValid && vExp.Error == ServicePaymentConstants.VoucherExpired
                && vOk.IsValid && cardInfo.IsValid && cardInfo.Balance == 200000m
                && !cardOver.Ok && cardOver.ErrorMessage == ServicePaymentConstants.PrepaidOverBalance
                && mixOk.Ok && !vReused.IsValid && vReused.Error == ServicePaymentConstants.VoucherAlreadyUsed
                && cardAfter.IsValid && cardAfter.Balance == 50000m
                && cust1GroupAfter == fx.CustomerGroupVipId,
                "cardBal=" + cardAfter.Balance + ", grp=" + cust1GroupAfter);

            // CachChonNgayGiaoDich = 0 và 1 trên DB (§6.5/§7): mở ngày D (hôm qua), trả hôm nay (D+1).
            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
            DateTime yesterday = today.AddDays(-1);

            SetTestConfigInt(db, cfgRepo, fx, "CachChonNgayGiaoDich", 0);
            ServiceOrderHeader bDate0 = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 1m, 100000m);
            BookingExec(db, "UPDATE TDONHANG SET NGAY = @D WHERE ID = @I", "@D", yesterday, "@I", bDate0.Id);
            PaymentExecutionResult rDate0 = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDate0.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 100000m
            });
            DateTime savedDate0 = DbHelper.Read(db, conn => Convert.ToDateTime(DbHelper.Scalar(conn, null,
                "SELECT NGAY FROM TDONHANG WHERE ID = @I", "@I", bDate0.Id))).Date;

            SetTestConfigInt(db, cfgRepo, fx, "CachChonNgayGiaoDich", 1);
            ServiceOrderHeader bDate1 = OpenSeededBill(svc, fx, fx.Table1Id, fx.ProdNormalId, 1m, 100000m);
            BookingExec(db, "UPDATE TDONHANG SET NGAY = @D WHERE ID = @I", "@D", yesterday, "@I", bDate1.Id);
            PaymentExecutionResult rDate1Warn = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDate1.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 100000m,
                ConfirmDateTransfer = false
            });
            PaymentExecutionResult rDate1Ok = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = bDate1.Id,
                PrimaryTableId = fx.Table1Id,
                CashTendered = 100000m,
                ConfirmDateTransfer = true
            });
            DateTime savedDate1 = DbHelper.Read(db, conn => Convert.ToDateTime(DbHelper.Scalar(conn, null,
                "SELECT NGAY FROM TDONHANG WHERE ID = @I", "@I", bDate1.Id))).Date;

            Check("SDDV ThanhToan DB §6.5/§7 CachChonNgayGiaoDich=0 giữ NGAY=D (không cảnh báo), mode=1 cảnh báo rồi chuyển NGAY=D+1",
                rDate0.Ok && savedDate0 == yesterday
                && !rDate1Warn.Ok && rDate1Warn.NeedsDateTransferConfirmation
                && rDate1Ok.Ok && savedDate1 == today,
                "d0=" + savedDate0.ToString("dd/MM") + ", d1=" + savedDate1.ToString("dd/MM"));
        }

        private static void PaymentGroupAndRollbackDbTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            var svc = new TableService(admin);
            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
            string tag = DbHelper.NewId().Substring(0, 5);

            // 1. Gán nhóm -> gỡ nhóm khi hủy thanh toán chung (§3.7.3/§7).
            string bkG1 = DbHelper.NewId();
            fx.CreatedBookingIds.Add(bkG1);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, 'Khach Nhom', 30, 'selftest')",
                "@I", bkG1, "@G", DbHelper.NewId(), "@N", "DH-G1-" + tag, "@D", today, "@T", fx.Table2Id);
            string depG1Id = new PhieuThuRepository(db).SaveDeposit(new BookingDeposit
            {
                BookingId = bkG1,
                Number = "PT-G1-" + tag,
                Date = today,
                ObjectType = 0,
                CustomerName = "Khach Nhom",
                StoreId = fx.StoreId,
                Amount = 400000m,
                Transfer = false
            }, "selftest");
            fx.CreatedVoucherIds.Add(depG1Id);

            ServiceOrderHeader g1 = OpenSeededBill(svc, fx, fx.Table2Id, fx.ProdNormalId, 3m, 100000m, bkG1); // 300k, cọc 400k
            ServiceOrderHeader g2 = OpenSeededBill(svc, fx, fx.Table3Id, fx.ProdNormalId, 2m, 100000m);       // 200k, cọc 0
            // Đảm bảo thứ tự BATDAU: g1 < g2.
            BookingExec(db, "UPDATE TDONHANG SET BATDAU = @B WHERE ID = @I", "@B", today.AddHours(9), "@I", g1.Id);
            BookingExec(db, "UPDATE TDONHANG SET BATDAU = @B WHERE ID = @I", "@B", today.AddHours(10), "@I", g2.Id);

            GroupAssignResult assigned = svc.AssignPaymentGroup(new[] { g1.Id, g2.Id });
            svc.RestorePaymentGroup(assigned.PreviousGroupGuids);
            string g1AfterRestore = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NHOMGUID FROM TDONHANG WHERE ID = @I", "@I", g1.Id)));
            string g2AfterRestore = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NHOMGUID FROM TDONHANG WHERE ID = @I", "@I", g2.Id)));
            Check("SDDV ThanhToan DB §3.7.3/§7 Thanh toán chung: gán NHOMGUID và đóng không trả thì gỡ NHOMGUID, các bàn như cũ",
                assigned.Ok && !string.IsNullOrEmpty(assigned.GroupGuid) && string.IsNullOrEmpty(g1AfterRestore) && string.IsNullOrEmpty(g2AfterRestore));

            // 2. Gán lại nhóm & thử lỗi giữa chừng -> rollback sạch cả 2 bill!
            GroupAssignResult assigned2 = svc.AssignPaymentGroup(new[] { g1.Id, g2.Id });
            bool caughtMidFail = false;
            try
            {
                svc.ExecutePayment(new PaymentRequest
                {
                    PrimaryBillId = g1.Id,
                    PrimaryTableId = fx.Table2Id,
                    GroupGuid = assigned2.GroupGuid,
                    CashTendered = 100000m,
                    TestHookAfterBillUpdate = delegate(string bid)
                    {
                        if (bid == g1.Id)
                            throw new InvalidOperationException("Simulated mid-group crash");
                    }
                });
            }
            catch (InvalidOperationException)
            {
                caughtMidFail = true;
            }
            int g1PaidAfterCrash = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT DATHANHTOAN FROM TDONHANG WHERE ID = @I", "@I", g1.Id)));
            int g2PaidAfterCrash = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT DATHANHTOAN FROM TDONHANG WHERE ID = @I", "@I", g2.Id)));
            Check("SDDV ThanhToan DB §7/Quỹ §7 Thanh toán chung lỗi giữa chừng -> rollback sạch transaction, không bill nào đổi",
                caughtMidFail && g1PaidAfterCrash == 0 && g2PaidAfterCrash == 0);

            // 3. Thanh toán chung Q15 thành công (300k cọc 400k + 200k -> thu thêm 100k, không phiếu chi, cả 2 bill đóng sạch).
            PaymentExecutionResult gOk = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = g1.Id,
                PrimaryTableId = fx.Table2Id,
                GroupGuid = assigned2.GroupGuid,
                CashTendered = 100000m
            });
            FundData fdGroup = new FundRepository(db).Load(today);
            List<FundSource> groupOnly = fdGroup.Sources.FindAll(s => s.Number == g1.Name || s.Number == g2.Name);
            FundLedger groupCash = FundLedgerRules.Build(groupOnly, new FundQuery
            {
                From = today, To = today, Node = FundNodeKind.Cash,
                KnownAccountIds = new List<string> { fx.BankId },
                Scope = new FundStoreScope { CoversAll = true, StoreIds = new List<string>() }
            });
            Check("SDDV ThanhToan DB §7 Q15 Thanh toán chung (300k cọc 400k + 200k): chỉ thu thêm 100k, ExcessDeposit=0, cả 2 bill đóng, quỹ tăng thêm 100k",
                gOk.Ok && gOk.PaidBillIds.Count == 2 && gOk.ExcessDeposit == 0m && groupCash.ClosingBalance == 100000m,
                "paidCnt=" + gOk.PaidBillIds.Count + ", cashAdded=" + groupCash.ClosingBalance);
        }
    }
}
