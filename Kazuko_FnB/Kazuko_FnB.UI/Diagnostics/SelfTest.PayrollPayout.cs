using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PayrollPayoutTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_cl",
                    UserName = "selftest_cl",
                    DisplayName = "Selftest ChiLuong",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                PayrollPayoutRulesPureTests();
                PayrollPayoutServiceAndFundTests(db);
                PayrollPayoutUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void PayrollPayoutRulesPureTests()
        {
            // Số tiền mặc định: Còn lại nếu > 0, ngược lại THUCNHAN (âm/0 -> 0).
            bool defAmtOk = PayrollPayoutRules.DefaultPayoutAmount(1000000m, 1000000m) == 1000000m
                && PayrollPayoutRules.DefaultPayoutAmount(1000000m, 400000m) == 400000m
                && PayrollPayoutRules.DefaultPayoutAmount(1000000m, 0m) == 1000000m
                && PayrollPayoutRules.DefaultPayoutAmount(1000000m, -200000m) == 1000000m
                && PayrollPayoutRules.DefaultPayoutAmount(0m, 0m) == 0m
                && PayrollPayoutRules.DefaultPayoutAmount(-500000m, -500000m) == 0m;

            bool defDateOk = PayrollPayoutRules.DefaultPayoutDate(2004, 3, new DateTime(2026, 11, 28)) == new DateTime(2004, 3, 31)
                && PayrollPayoutRules.DefaultPayoutDate(2004, 3, new DateTime(2004, 3, 15)) == new DateTime(2004, 3, 15);

            var valid = new VoucherRow
            {
                Name = "PC26/00001",
                Date = new DateTime(2004, 3, 25),
                Kind = VoucherKind.Expense,
                IsAdvance = false,
                PayrollId = "BL1",
                ObjectType = PhieuThuRules.Employee,
                EmployeeId = "E1",
                StoreId = "S1",
                Amount = 1000000m,
                Transfer = false
            };
            string errValid = PayrollPayoutRules.Validate(valid, 3, 2004);
            string errOutMonth = PayrollPayoutRules.Validate(new VoucherRow
            {
                Name = "PC01", Date = new DateTime(2004, 4, 1), Kind = VoucherKind.Expense,
                PayrollId = "BL1", EmployeeId = "E1", StoreId = "S1", Amount = 100000m
            }, 3, 2004);
            string errNoBank = PayrollPayoutRules.Validate(new VoucherRow
            {
                Name = "PC01", Date = new DateTime(2004, 3, 10), Kind = VoucherKind.Expense,
                PayrollId = "BL1", EmployeeId = "E1", StoreId = "S1", Amount = 100000m,
                Transfer = true, BankAccountId = ""
            }, 3, 2004);
            string errZero = PayrollPayoutRules.Validate(new VoucherRow
            {
                Name = "PC01", Date = new DateTime(2004, 3, 10), Kind = VoucherKind.Expense,
                PayrollId = "BL1", EmployeeId = "E1", StoreId = "S1", Amount = 0m
            }, 3, 2004);

            bool editableOk = PayrollPayoutRules.IsEditablePayout(valid, "BL1")
                && !PayrollPayoutRules.IsEditablePayout(valid, "BL_OTHER")
                && !PayrollPayoutRules.IsEditablePayout(new VoucherRow { Kind = VoucherKind.Expense, PayrollId = "BL1", IsAdvance = true }, "BL1")
                && !PayrollPayoutRules.IsEditablePayout(new VoucherRow { Kind = VoucherKind.Expense, PayrollId = null }, null)
                && !PayrollPayoutRules.IsEditablePayout(new VoucherRow { Kind = VoucherKind.Income, PayrollId = "BL1" }, "BL1");

            Check("Chi lương P3b §5 (luật thuần): mặc định số tiền/ngày, chặn ngày ngoài tháng, CK thiếu TK, tiền <= 0 và IsEditablePayout",
                defAmtOk && defDateOk && errValid == null
                && errOutMonth == PayrollPayoutText.MonthLimitBlocked(3, 2004)
                && errNoBank == VoucherConstants.BankAccountMissing
                && errZero == VoucherConstants.AmountMustBePositive
                && editableOk,
                SingleLine("defAmt=" + defAmtOk + ", defDate=" + defDateOk
                    + ", outM=" + SingleLine(errOutMonth) + ", noBk=" + SingleLine(errNoBank) + ", edit=" + editableOk));
        }

        private static void PayrollPayoutServiceAndFundTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string seedCashId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string empBId = DbHelper.NewId();
            string empLongId = DbHelper.NewId();
            string periodId = DbHelper.NewId();
            var createdVoucherIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cl')",
                    "@I", storeId, "@N", "CH ChiLuong " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cl')",
                    "@I", bankId, "@N", "TK ChiLuong " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 1, 30, 'selftest_cl')",
                    "@I", expReasonId, "@N", "Chi lương ST " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_cl')",
                    "@I", incReasonId, "@N", "Lý do thu ST CL " + tag);

                // Tạo quỹ tiền mặt ban đầu 2.000.000 cho cửa hàng test để kiểm chi tiền mặt & chi vượt quỹ.
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, LATAMUNG, " +
                    "DCUAHANGID, DLYDOTHUCHIID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 0, 0, 0, 0, @S, @R, 2000000, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", seedCashId, "@N", "PT-SEED-CL-" + tag, "@D", new DateTime(2004, 3, 1), "@S", storeId, "@R", incReasonId, "@T", now);
                createdVoucherIds.Add(seedCashId);

                string longName = "NV Dài CL " + tag + " " + new string('ă', 220);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empAId, "@N", "NV A CL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empBId, "@N", "NV B CL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 100000, 0, 0, 0, 30, 'selftest_cl', @T)",
                    "@I", empLongId, "@N", longName, "@T", now);

                // Tạo bảng lương Tháng 3/2004 và 3 dòng tổng hợp TBANGLUONGTONGHOP (A = 1.000.000, B = 1.000.000, Long = 200.000).
                BookingExec(db,
                    "INSERT INTO TBANGLUONG (ID, NAME, THANG, NAM, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, 2004, 30, 'selftest_cl', @T)",
                    "@I", periodId, "@N", "Tháng 3/2004 CL " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 1000000, 0, 0, 0, 1000000, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empAId, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 1000000, 0, 0, 0, 1000000, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empBId, "@T", now);
                BookingExec(db,
                    "INSERT INTO TBANGLUONGTONGHOP (ID, TBANGLUONGID, DNHANVIENID, LUONGCA, LUONGTHANG, CACHTINHLUONG, TONGLUONG, THUONG, PHAT, TAMUNG, THUCNHAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @BL, @NV, 100000, 0, 30, 200000, 0, 0, 0, 200000, 30, 'selftest_cl', @T)",
                    "@I", DbHelper.NewId(), "@BL", periodId, "@NV", empLongId, "@T", now);

                var payoutSvc = new PayrollPayoutService(AppSession.Current);
                var voucherSvc = new VoucherService(AppSession.Current);
                var advanceSvc = new SalaryAdvanceService(AppSession.Current);

                // §6 #11: A thực nhận 1.000.000 chưa chi -> Đã chi 0, Còn lại 1.000.000.
                PayrollPayoutSheet sheet0 = payoutSvc.LoadPayoutSheet(periodId);
                PayrollPayoutRow rowA0 = sheet0 == null ? null : sheet0.Rows.Find(r => r.EmployeeId == empAId);
                decimal cash0 = ReadStoreCashBalance(db, storeId);
                decimal bank0 = ReadStoreBankNet(db, storeId, bankId);

                string pcA = payoutSvc.GenerateNumber();
                var vA = new VoucherRow
                {
                    Name = pcA,
                    Date = new DateTime(2004, 3, 31),
                    PayrollId = periodId,
                    EmployeeId = empAId,
                    StoreId = storeId,
                    ReasonId = expReasonId,
                    Amount = PayrollPayoutRules.DefaultPayoutAmount(rowA0.NetSalary, rowA0.RemainingAmount),
                    Transfer = false,
                    Description = PayrollPayoutText.DefaultDescription(3, 2004)
                };
                VoucherSaveResult rA = payoutSvc.Save(vA);
                if (rA.Ok) createdVoucherIds.Add(rA.SavedId);

                VoucherRow backA = rA.Ok ? voucherSvc.LoadById(rA.SavedId) : null;
                object rawLatamung = rA.Ok
                    ? DbHelper.Read(db, conn => DbHelper.Scalar(conn, null, "SELECT LATAMUNG FROM TTHUCHI WHERE ID = @I", "@I", rA.SavedId))
                    : null;
                decimal cash1 = ReadStoreCashBalance(db, storeId);
                PayrollPayoutSheet sheet1 = payoutSvc.LoadPayoutSheet(periodId);
                PayrollPayoutRow rowA1 = sheet1 == null ? null : sheet1.Rows.Find(r => r.EmployeeId == empAId);
                string latestReason = payoutSvc.LoadLatestReasonId();

                bool latamungIsZeroNotNull = rawLatamung != null && !(rawLatamung is DBNull) && Convert.ToInt32(rawLatamung) == 0;

                Check("Chi lương P3b §6 #11 (A thực nhận 1.000.000, tạo phiếu chi tiền mặt): TTHUCHI LOAI=1 CHI=1000000 TBANGLUONGID LATAMUNG=0 diễn giải đúng, quỹ TM -1.000.000, Đã chi=1.000.000 Còn lại=0",
                    rowA0 != null && rowA0.NetSalary == 1000000m && rowA0.PaidAmount == 0m && rowA0.RemainingAmount == 1000000m
                    && rA.Ok && backA != null && backA.Kind == VoucherKind.Expense && backA.Amount == 1000000m
                    && backA.PayrollId == periodId && backA.EmployeeId == empAId && backA.ObjectType == PhieuThuRules.Employee
                    && backA.ObjectName == "NV A CL " + tag && !backA.IsAdvance && latamungIsZeroNotNull
                    && backA.Description == "Chi tiền lương tháng 3 năm 2004"
                    && cash1 - cash0 == -1000000m
                    && rowA1 != null && rowA1.PaidAmount == 1000000m && rowA1.RemainingAmount == 0m && rowA1.VoucherNumbersDisplay == pcA
                    && latestReason == expReasonId && pcA.StartsWith("PC", StringComparison.OrdinalIgnoreCase),
                    SingleLine("ok=" + rA.Ok + ", err=" + SingleLine(rA.ErrorMessage) + ", latu=" + rawLatamung
                        + ", dcash=" + (cash1 - cash0) + ", paid=" + (rowA1 == null ? -1 : rowA1.PaidAmount)
                        + ", rem=" + (rowA1 == null ? -1 : rowA1.RemainingAmount) + ", reason=" + latestReason));

                // §6 #12: Chi một phần 600.000 (TM) cho B -> Còn lại 400.000 -> tạo thêm 500.000 (CK qua bankId) -> Đã chi 1.100.000, Còn lại -100.000 (IsOverpaid), nối 2 số phiếu;
                // chặn CK thiếu TK, chặn ngày ngoài tháng, chặn lý do thu; sửa số tiền cập nhật quỹ; xóa trả quỹ.
                string pcB1 = payoutSvc.GenerateNumber();
                var vB1 = new VoucherRow
                {
                    Name = pcB1, Date = new DateTime(2004, 3, 25), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, ReasonId = expReasonId, Amount = 600000m, Transfer = false,
                    Description = PayrollPayoutText.DefaultDescription(3, 2004)
                };
                VoucherSaveResult rB1 = payoutSvc.Save(vB1);
                if (rB1.Ok) createdVoucherIds.Add(rB1.SavedId);

                PayrollPayoutSheet sheetB1 = payoutSvc.LoadPayoutSheet(periodId);
                PayrollPayoutRow rowB1 = sheetB1.Rows.Find(r => r.EmployeeId == empBId);
                decimal defSecondAmt = PayrollPayoutRules.DefaultPayoutAmount(rowB1.NetSalary, rowB1.RemainingAmount);

                // Tạo thêm phiếu 2 chuyển khoản 500.000 -> vượt thực nhận (1.100.000 > 1.000.000, không có trần).
                string pcB2 = payoutSvc.GenerateNumber();
                var vB2 = new VoucherRow
                {
                    Name = pcB2, Date = new DateTime(2004, 3, 28), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, ReasonId = expReasonId, Amount = 500000m, Transfer = true, BankAccountId = bankId,
                    Description = PayrollPayoutText.DefaultDescription(3, 2004)
                };
                VoucherSaveResult rB2 = payoutSvc.Save(vB2);
                if (rB2.Ok) createdVoucherIds.Add(rB2.SavedId);

                PayrollPayoutSheet sheetB2 = payoutSvc.LoadPayoutSheet(periodId);
                PayrollPayoutRow rowB2 = sheetB2.Rows.Find(r => r.EmployeeId == empBId);
                decimal cashAfterB = ReadStoreCashBalance(db, storeId);
                decimal bankAfterB = ReadStoreBankNet(db, storeId, bankId);

                // Chặn CK thiếu TK, chặn ngày ngoài tháng, chặn lý do thu.
                VoucherSaveResult rNoBank = payoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-NBK-" + tag, Date = new DateTime(2004, 3, 20), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, Amount = 100000m, Transfer = true, BankAccountId = null
                });
                VoucherSaveResult rOutMonth = payoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-OUT-" + tag, Date = new DateTime(2004, 4, 1), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, Amount = 100000m, Transfer = false
                });
                VoucherSaveResult rIncReason = payoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-INC-" + tag, Date = new DateTime(2004, 3, 20), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, ReasonId = incReasonId, Amount = 100000m, Transfer = false
                });

                // Sửa số tiền phiếu B1 từ 600.000 -> 450.000 -> quỹ tiền mặt tăng lại +150.000, giữ tạo / ghi sửa.
                VoucherRow editB1 = payoutSvc.LoadById(rB1.SavedId, periodId);
                DateTime? origB1Created = editB1.CreatedAt;
                string origB1User = editB1.CreatedById;
                editB1.Amount = 450000m;
                VoucherSaveResult rEditB1 = payoutSvc.Save(editB1);
                VoucherRow backB1Edit = voucherSvc.LoadById(rB1.SavedId);
                decimal cashAfterEdit = ReadStoreCashBalance(db, storeId);

                // Xóa phiếu B2 (chuyển khoản 500.000) -> quỹ TK trở về bank0, Đã chi B còn 450.000, Còn lại 550.000.
                int logsBeforeDelB2 = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", rB2.SavedId);
                VoucherDeleteResult rDelB2 = payoutSvc.Delete(rB2.SavedId, periodId);
                decimal bankAfterDelB2 = ReadStoreBankNet(db, storeId, bankId);
                int logsAfterDelB2 = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", rB2.SavedId);
                PayrollPayoutSheet sheetAfterDelB2 = payoutSvc.LoadPayoutSheet(periodId);
                PayrollPayoutRow rowBAfterDel = sheetAfterDelB2.Rows.Find(r => r.EmployeeId == empBId);

                Check("Chi lương P3b §6 #12 (chi từng phần 600k->mặc định 400k, CK 500k->vượt thực nhận, nối 2 số phiếu, chặn CK thiếu TK/ngoài tháng/lý do thu, sửa & xóa cập nhật quỹ): đạt",
                    rB1.Ok && rowB1.RemainingAmount == 400000m && defSecondAmt == 400000m
                    && rB2.Ok && rowB2.PaidAmount == 1100000m && rowB2.RemainingAmount == -100000m && rowB2.IsOverpaid
                    && rowB2.VoucherNumbersDisplay == (pcB1 + ", " + pcB2)
                    && cashAfterB - cash1 == -600000m && bankAfterB - bank0 == -500000m
                    && !rNoBank.Ok && rNoBank.ErrorMessage == VoucherConstants.BankAccountMissing
                    && !rOutMonth.Ok && rOutMonth.ErrorMessage == PayrollPayoutText.MonthLimitBlocked(3, 2004)
                    && !rIncReason.Ok && rIncReason.ErrorMessage == PayrollPayoutText.IncomeReasonNotAllowed
                    && rEditB1.Ok && backB1Edit != null && backB1Edit.Amount == 450000m
                    && backB1Edit.CreatedAt == origB1Created && backB1Edit.CreatedById == origB1User
                    && backB1Edit.ModifiedAt.HasValue && backB1Edit.ModifiedById == "selftest_cl"
                    && cashAfterEdit - cashAfterB == 150000m
                    && rDelB2.Ok && voucherSvc.LoadById(rB2.SavedId) == null && bankAfterDelB2 == bank0
                    && logsAfterDelB2 == logsBeforeDelB2 + 1
                    && rowBAfterDel != null && rowBAfterDel.PaidAmount == 450000m && rowBAfterDel.RemainingAmount == 550000m,
                    SingleLine("def2=" + defSecondAmt + ", b2=" + rowB2.PaidAmount + "/" + rowB2.RemainingAmount
                        + ", noBk=" + !rNoBank.Ok + ", outM=" + !rOutMonth.Ok + ", incR=" + !rIncReason.Ok
                        + ", editD=" + (cashAfterEdit - cashAfterB) + ", delBk=" + (bankAfterDelB2 - bank0)));

                // §6 #13: Cô lập (không hiện ở Tạm ứng, không cộng vào TAMUNG của Tính lương, không sửa/xóa ở Danh mục phiếu chi);
                // chi vượt quỹ cùng thông báo; rollback hook cho Save & Delete; lưu vết chuỗi 255 ký tự; quyền chỉ Xem bị chặn.
                List<VoucherRow> advList = advanceSvc.LoadList(new SalaryAdvanceFilter
                {
                    FromDate = new DateTime(2004, 3, 1),
                    ToDate = new DateTime(2004, 3, 31)
                });
                bool notInAdvanceList = !advList.Exists(x => x.Id == rA.SavedId || x.Id == rB1.SavedId);
                VoucherDeleteResult delViaAdvBlocked = advanceSvc.Delete(rA.SavedId);

                // Kiểm TAMUNG trong Tính lương không cộng phiếu chi lương (dùng LoadLiveSnapshots cho tháng 3/2004).
                Dictionary<string, PayrollLiveSnapshot> liveSnaps = new PayrollRepository(db).LoadLiveSnapshots(2004, 3, empAId);
                PayrollLiveSnapshot snapA;
                bool notInPayrollTamUng = liveSnaps.TryGetValue(empAId, out snapA) && snapA.Advance == 0m;

                // Danh mục phiếu chi (VoucherService) chặn sửa/xóa phiếu chi lương.
                VoucherRow loadedForVoucher = voucherSvc.LoadById(rA.SavedId);
                loadedForVoucher.Amount = 900000m;
                VoucherSaveResult editViaVoucherBlocked = voucherSvc.Save(loadedForVoucher);
                VoucherDeleteResult delViaVoucherBlocked = voucherSvc.Delete(rA.SavedId);

                // Chi vượt quỹ tiền mặt (hiện còn 2.000.000 - 1.000.000 - 450.000 = 550.000; chi 700.000 -> chặn bằng đúng thông báo).
                decimal cashBeforeOver = ReadStoreCashBalance(db, storeId);
                VoucherSaveResult rOver = payoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-OVER-" + tag, Date = new DateTime(2004, 3, 29), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, Amount = 700000m, Transfer = false
                });
                string expectedOverMsg = VoucherConstants.CashOverdraftBlocked(cashBeforeOver, 700000m);
                bool overdraftOk = !rOver.Ok && rOver.ErrorMessage == expectedOverMsg
                    && ReadStoreCashBalance(db, storeId) == cashBeforeOver;

                // Rollback Save bằng hook lỗi.
                decimal cashBeforeRb = ReadStoreCashBalance(db, storeId);
                var rbRow = new VoucherRow
                {
                    Name = "PC-CL-RB-" + tag, Date = new DateTime(2004, 3, 29), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, Amount = 100000m, Transfer = false
                };
                bool saveRolledBack = false;
                try
                {
                    payoutSvc.Save(rbRow, delegate { throw new InvalidOperationException("Lỗi giả lập lưu chi lương"); });
                }
                catch (InvalidOperationException)
                {
                    saveRolledBack = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE NAME = @N", "@N", "PC-CL-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbRow.Id)
                        && ReadStoreCashBalance(db, storeId) == cashBeforeRb;
                }

                // Rollback Delete bằng hook lỗi.
                bool delRolledBack = false;
                try
                {
                    payoutSvc.Delete(rB1.SavedId, periodId, delegate { throw new InvalidOperationException("Lỗi giả lập xóa chi lương"); });
                }
                catch (InvalidOperationException)
                {
                    delRolledBack = voucherSvc.LoadById(rB1.SavedId) != null
                        && ReadStoreCashBalance(db, storeId) == cashBeforeRb;
                }

                // Lưu vết chuỗi 255 ký tự (NV tên dài 240+ ký tự -> cắt vừa 255, không tràn).
                VoucherSaveResult rLong = payoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-255-" + tag, Date = new DateTime(2004, 3, 29), PayrollId = periodId, EmployeeId = empLongId,
                    StoreId = storeId, Amount = 50000m, Transfer = false,
                    Description = PayrollPayoutText.DefaultDescription(3, 2004)
                });
                if (rLong.Ok) createdVoucherIds.Add(rLong.SavedId);
                int logsForLong = rLong.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", rLong.SavedId)
                    : 0;

                // Quyền chỉ Xem: không được Save/Delete phiếu chi lương.
                var viewOnlyPerms = new FunctionPermissions(false);
                viewOnlyPerms.Add("fn_cl_ro", PayrollText.PayrollFunctionName, RoleModes.View);
                var viewOnlySess = new UserSession
                {
                    UserId = "u_cl_ro", UserName = "u_cl_ro", DisplayName = "Chỉ xem CL",
                    IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms
                };
                var roPayoutSvc = new PayrollPayoutService(viewOnlySess);
                VoucherSaveResult roSaveBlocked = roPayoutSvc.Save(new VoucherRow
                {
                    Name = "PC-CL-RO-" + tag, Date = new DateTime(2004, 3, 29), PayrollId = periodId, EmployeeId = empBId,
                    StoreId = storeId, Amount = 10000m, Transfer = false
                });
                VoucherDeleteResult roDelBlocked = roPayoutSvc.Delete(rA.SavedId, periodId);

                Check("Chi lương P3b §6 #13 (cô lập Tạm ứng/TAMUNG/Danh mục phiếu chi, chi vượt quỹ, rollback Save/Delete, lưu vết cắt 255, quyền chỉ Xem chặn): đạt",
                    notInAdvanceList && !delViaAdvBlocked.Ok && notInPayrollTamUng
                    && !editViaVoucherBlocked.Ok && !delViaVoucherBlocked.Ok
                    && overdraftOk && saveRolledBack && delRolledBack
                    && rLong.Ok && logsForLong == 1
                    && !roPayoutSvc.CanPayOut && !roSaveBlocked.Ok && !roDelBlocked.Ok,
                    SingleLine("noAdv=" + notInAdvanceList + ", noTamUng=" + notInPayrollTamUng
                        + ", vBlk=" + (!editViaVoucherBlocked.Ok && !delViaVoucherBlocked.Ok)
                        + ", over=" + overdraftOk + ", rb=" + saveRolledBack + "/" + delRolledBack
                        + ", log255=" + (rLong.Ok && logsForLong == 1) + ", roBlk=" + (!roSaveBlocked.Ok && !roDelBlocked.Ok)));
            }
            finally
            {
                foreach (string vid in createdVoucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                BookingExec(db, "DELETE FROM TBANGLUONGCHITIET WHERE TBANGLUONGID = @I", "@I", periodId);
                BookingExec(db, "DELETE FROM TBANGLUONGTONGHOP WHERE TBANGLUONGID = @I", "@I", periodId);
                SafeDeleteById(db, "TBANGLUONG", periodId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DNHANVIEN", empBId);
                SafeDeleteById(db, "DNHANVIEN", empLongId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
