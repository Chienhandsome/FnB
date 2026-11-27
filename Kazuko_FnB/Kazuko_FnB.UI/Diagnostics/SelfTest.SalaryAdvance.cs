using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void SalaryAdvanceTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tu",
                    UserName = "selftest_tu",
                    DisplayName = "Selftest TamUng",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                SalaryAdvanceRulesPureTests();
                SalaryAdvanceServiceAndFundTests(db);
                SalaryAdvanceUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void SalaryAdvanceRulesPureTests()
        {
            var valid = new VoucherRow
            {
                Name = "PC26/00001",
                Date = DateTime.Today,
                Kind = VoucherKind.Expense,
                IsAdvance = true,
                ObjectType = PhieuThuRules.Employee,
                EmployeeId = "E1",
                StoreId = "S1",
                Amount = 500000m,
                Transfer = false
            };
            Check("Tạm ứng lương §4 #6 (luật thuần): phiếu hợp lệ qua Validate",
                SalaryAdvanceRules.Validate(valid) == null);

            string errNoEmp = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", StoreId = "S1", Amount = 10000m });
            string errNoStore = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", EmployeeId = "E1", Amount = 10000m });
            string errZero = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", EmployeeId = "E1", StoreId = "S1", Amount = 0m });
            string errNeg = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", EmployeeId = "E1", StoreId = "S1", Amount = -500m });
            string errBig = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", EmployeeId = "E1", StoreId = "S1", Amount = 1000000000m });
            string errTransfer = SalaryAdvanceRules.Validate(new VoucherRow { Name = "PC01", EmployeeId = "E1", StoreId = "S1", Amount = 10000m, Transfer = true, BankAccountId = "B1" });

            Check("Tạm ứng lương §4 #7 (luật thuần): chặn thiếu NV, thiếu cửa hàng, tiền <= 0, quá 9 số, chuyển khoản",
                errNoEmp == SalaryAdvanceText.EmployeeRequired
                && errNoStore == VoucherConstants.StoreRequired
                && errZero == VoucherConstants.AmountMustBePositive
                && errNeg == VoucherConstants.AmountMustBePositive
                && errBig == SalaryAdvanceText.AmountTooLarge
                && errTransfer == SalaryAdvanceText.TransferNotAllowed,
                "emp=" + SingleLine(errNoEmp) + ", st=" + SingleLine(errNoStore) + ", ck=" + SingleLine(errTransfer));

            Check("Tạm ứng lương §4 #9 (luật thuần IsEditableAdvance): chỉ mở cho phiếu chi LATAMUNG=30 không gắn TBANGLUONGID",
                SalaryAdvanceRules.IsEditableAdvance(valid)
                && !SalaryAdvanceRules.IsEditableAdvance(new VoucherRow { Kind = VoucherKind.Expense, IsAdvance = false })
                && !SalaryAdvanceRules.IsEditableAdvance(new VoucherRow { Kind = VoucherKind.Expense, IsAdvance = true, PayrollId = "BL1" })
                && !SalaryAdvanceRules.IsEditableAdvance(new VoucherRow { Kind = VoucherKind.Income, IsAdvance = true }));
        }

        private static void SalaryAdvanceServiceAndFundTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string empAId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string seedIncomeId = DbHelper.NewId();
            string normalExpId = DbHelper.NewId();
            string payrollExpId = DbHelper.NewId();
            string payrollId = DbHelper.NewId();
            var createdVoucherIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tu')",
                    "@I", storeId, "@N", "CH TamUng " + tag);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 150000, 0, 0, 30, 30, 'selftest_tu', @T)",
                    "@I", empAId, "@N", "Trần Văn Ứng " + tag, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 2, 30, 'selftest_tu')",
                    "@I", expReasonId, "@N", "Chi tạm ứng ST " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_tu')",
                    "@I", incReasonId, "@N", "Thu khác ST " + tag);

                // Tạo quỹ tiền mặt ban đầu 1.000.000 cho cửa hàng test qua phiếu thu tiền mặt.
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, LATAMUNG, " +
                    "DCUAHANGID, DLYDOTHUCHIID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 0, 0, 0, 0, @S, @R, 1000000, 0, 0, 30, 'selftest_tu', @T)",
                    "@I", seedIncomeId, "@N", "PT-SEED-TU-" + tag, "@D", DateTime.Today, "@S", storeId, "@R", incReasonId, "@T", DbHelper.Now());
                createdVoucherIds.Add(seedIncomeId);

                var svc = new SalaryAdvanceService(AppSession.Current);
                var voucherSvc = new VoucherService(AppSession.Current);

                decimal cash0 = ReadStoreCashBalance(db, storeId);

                // #6: Thêm tạm ứng 500.000 cho NV A -> LOAI=1, LATAMUNG=30, CHI=500000, LOAIDOITUONG=1, DNHANVIENID=A, CHUYENKHOAN=0, STATUS=30; tồn quỹ TM -500.000; số PCyy/.
                string autoPc1 = svc.GenerateNumber();
                var adv1 = new VoucherRow
                {
                    Name = autoPc1,
                    Date = DateTime.Today,
                    EmployeeId = empAId,
                    StoreId = storeId,
                    ReasonId = expReasonId,
                    Amount = 500000m,
                    Description = SalaryAdvanceText.DefaultDescription,
                    Note = "Tạm ứng giữa tháng " + tag
                };
                VoucherSaveResult rAdd = svc.Save(adv1);
                if (rAdd.Ok) createdVoucherIds.Add(rAdd.SavedId);
                VoucherRow back1 = rAdd.Ok ? voucherSvc.LoadById(rAdd.SavedId) : null;
                decimal cash1 = ReadStoreCashBalance(db, storeId);
                string autoPc2 = svc.GenerateNumber();

                Check("Tạm ứng lương §4 #6 (thêm tạm ứng 500.000): ghi TTHUCHI LOAI=1 LATAMUNG=30 CHI=500000 CHUYENKHOAN=0, quỹ TM giảm 500.000, số PC tăng tiếp",
                    rAdd.Ok && back1 != null && back1.Kind == VoucherKind.Expense && back1.IsAdvance
                    && back1.Amount == 500000m && back1.ObjectType == PhieuThuRules.Employee
                    && back1.EmployeeId == empAId && back1.ObjectName == "Trần Văn Ứng " + tag
                    && !back1.Transfer && back1.Status == 30
                    && cash1 - cash0 == -500000m
                    && autoPc1.StartsWith("PC", StringComparison.OrdinalIgnoreCase) && autoPc2 != autoPc1,
                    "err=" + SingleLine(rAdd.ErrorMessage) + ", dcash=" + (cash1 - cash0) + ", pc1=" + autoPc1 + ", pc2=" + autoPc2);

                // #7: Chặn thiếu NV, thiếu cửa hàng, số tiền <= 0, lý do thu (LALYDOTHU = 30).
                VoucherSaveResult rNoEmp = svc.Save(new VoucherRow { Name = "PC-NOEMP-" + tag, Date = DateTime.Today, StoreId = storeId, Amount = 100000m });
                VoucherSaveResult rNoStore = svc.Save(new VoucherRow { Name = "PC-NOST-" + tag, Date = DateTime.Today, EmployeeId = empAId, Amount = 100000m });
                VoucherSaveResult rZero = svc.Save(new VoucherRow { Name = "PC-ZERO-" + tag, Date = DateTime.Today, EmployeeId = empAId, StoreId = storeId, Amount = 0m });
                VoucherSaveResult rIncReason = svc.Save(new VoucherRow { Name = "PC-INCR-" + tag, Date = DateTime.Today, EmployeeId = empAId, StoreId = storeId, ReasonId = incReasonId, Amount = 100000m });
                Check("Tạm ứng lương §4 #7 (chặn tầng Service): thiếu NV, thiếu CH, tiền <= 0, lý do thu (LALYDOTHU=30) đều bị chặn",
                    !rNoEmp.Ok && !rNoStore.Ok && !rZero.Ok && !rIncReason.Ok && rIncReason.ErrorMessage == SalaryAdvanceText.IncomeReasonNotAllowed,
                    "emp=" + rNoEmp.Ok + ", st=" + rNoStore.Ok + ", zero=" + rZero.Ok + ", incR=" + SingleLine(rIncReason.ErrorMessage));

                // #8a: Chi vượt quỹ (còn 500.000, chi thêm 800.000) -> chặn bằng đúng thông báo của Phiếu chi thường.
                VoucherSaveResult rOver = svc.Save(new VoucherRow
                {
                    Name = "PC-OVER-TU-" + tag, Date = DateTime.Today, EmployeeId = empAId, StoreId = storeId, Amount = 800000m
                });
                string expectedOverMsg = VoucherConstants.CashOverdraftBlocked(cash1, 800000m);
                Check("Tạm ứng lương §4 #8 (chi vượt quỹ): chặn với đúng thông báo của Phiếu chi thường và không đổi quỹ",
                    !rOver.Ok && rOver.ErrorMessage == expectedOverMsg && ReadStoreCashBalance(db, storeId) == cash1,
                    "msg=" + SingleLine(rOver.ErrorMessage));

                // #8b: Sửa số tiền 500.000 -> 300.000: quỹ TM tăng lại đúng 200.000, giữ người/giờ tạo, ghi sửa.
                DateTime? origCreatedAt = back1.CreatedAt;
                string origCreatedBy = back1.CreatedById;
                back1.Amount = 300000m;
                VoucherSaveResult rEdit = svc.Save(back1);
                VoucherRow afterEdit = voucherSvc.LoadById(back1.Id);
                decimal cashAfterEdit = ReadStoreCashBalance(db, storeId);
                Check("Tạm ứng lương §4 #8 (sửa 500.000 -> 300.000): quỹ tiền mặt tăng lại +200.000, giữ tạo và ghi sửa",
                    rEdit.Ok && afterEdit != null && afterEdit.Amount == 300000m
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.ModifiedById == "selftest_tu" && afterEdit.ModifiedAt.HasValue
                    && cashAfterEdit - cash1 == 200000m && cashAfterEdit - cash0 == -300000m,
                    "err=" + SingleLine(rEdit.ErrorMessage) + ", dcash=" + (cashAfterEdit - cash1));

                // #9: Phiếu LATAMUNG=30 không sửa/xóa được qua VoucherService (Danh mục phiếu chi);
                // phiếu chi thường và phiếu có TBANGLUONGID không hiện ở màn Tạm ứng lương.
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DCUAHANGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 0, 0, @S, 0, 50000, 0, 30, 'selftest_tu', @T)",
                    "@I", normalExpId, "@N", "PC-NORM-" + tag, "@D", DateTime.Today, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, DCUAHANGID, TBANGLUONGID, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 1, 1, 30, @E, @S, @BL, 0, 70000, 0, 30, 'selftest_tu', @T)",
                    "@I", payrollExpId, "@N", "PC-BL-" + tag, "@D", DateTime.Today, "@E", empAId, "@S", storeId, "@BL", payrollId, "@T", DbHelper.Now());
                createdVoucherIds.Add(normalExpId);
                createdVoucherIds.Add(payrollExpId);

                VoucherSaveResult rEditViaVoucher = voucherSvc.Save(afterEdit);
                VoucherDeleteResult rDelViaVoucher = voucherSvc.Delete(afterEdit.Id);
                List<VoucherRow> advList = svc.LoadList(new SalaryAdvanceFilter
                {
                    FromDate = DateTime.Today.AddDays(-1),
                    ToDate = DateTime.Today.AddDays(1),
                    SearchText = tag
                });
                bool hasAdv1 = advList.Exists(x => x.Id == afterEdit.Id);
                bool hasNormal = advList.Exists(x => x.Id == normalExpId);
                bool hasPayroll = advList.Exists(x => x.Id == payrollExpId);
                VoucherDeleteResult rDelPayrollViaAdv = svc.Delete(payrollExpId);

                Check("Tạm ứng lương §4 #9 (tách biệt với Danh mục phiếu chi & bảng lương): LATAMUNG=30 chặn sửa/xóa ở Phiếu chi; phiếu thường & phiếu có TBANGLUONGID không hiện ở Tạm ứng",
                    !rEditViaVoucher.Ok && !rDelViaVoucher.Ok
                    && hasAdv1 && !hasNormal && !hasPayroll && !rDelPayrollViaAdv.Ok,
                    "vEdit=" + rEditViaVoucher.Ok + ", vDel=" + rDelViaVoucher.Ok + ", adv=" + hasAdv1 + ", norm=" + hasNormal + ", bl=" + hasPayroll);

                // #10 transaction rollback: lỗi giữa chừng -> không có dòng phiếu và không đổi quỹ.
                decimal cashBeforeRb = ReadStoreCashBalance(db, storeId);
                bool rolledBack = false;
                var rbAdv = new VoucherRow
                {
                    Name = "PC-RB-TU-" + tag, Date = DateTime.Today, EmployeeId = empAId, StoreId = storeId, Amount = 100000m
                };
                try
                {
                    svc.Save(rbAdv, null, delegate { throw new InvalidOperationException("Lỗi giả lập tạm ứng"); });
                }
                catch (InvalidOperationException)
                {
                    rolledBack = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE NAME = @N", "@N", "PC-RB-TU-" + tag) == 0
                        && string.IsNullOrEmpty(rbAdv.Id)
                        && ReadStoreCashBalance(db, storeId) == cashBeforeRb;
                }
                Check("Tạm ứng lương §4 #10 (một transaction rollback): lỗi giữa chừng không tạo dòng và không đổi quỹ",
                    rolledBack);

                // #8c: Xóa tạm ứng -> quỹ trả về đúng như trước khi tạo (trừ 2 phiếu seed normal/payroll đã thêm sau đó -> khôi phục +300.000).
                decimal cashBeforeDel = ReadStoreCashBalance(db, storeId);
                int logsBeforeDel = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", back1.Id);
                VoucherDeleteResult rDel = svc.Delete(back1.Id);
                decimal cashAfterDel = ReadStoreCashBalance(db, storeId);
                int logsAfterDel = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", back1.Id);
                Check("Tạm ứng lương §4 #8 (xóa tạm ứng): xóa cứng khỏi TTHUCHI, quỹ tăng lại đúng 300.000 và có lưu vết",
                    rDel.Ok && voucherSvc.LoadById(back1.Id) == null
                    && cashAfterDel - cashBeforeDel == 300000m
                    && logsAfterDel == logsBeforeDel + 1,
                    "del=" + rDel.Ok + ", dcash=" + (cashAfterDel - cashBeforeDel) + ", dlog=" + (logsAfterDel - logsBeforeDel));
            }
            finally
            {
                foreach (string vid in createdVoucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DNHANVIEN", empAId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
