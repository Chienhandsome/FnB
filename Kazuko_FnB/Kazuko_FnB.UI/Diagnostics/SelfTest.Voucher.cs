using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void VoucherTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tc",
                    UserName = "selftest_tc",
                    DisplayName = "Selftest ThuChi",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                VoucherRulesPureTests();
                VoucherServiceDbTests(db);
                VoucherCatalogDbTests(db);
                VoucherUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void VoucherRulesPureTests()
        {
            var validCash = new VoucherRow
            {
                Name = "PT26/00001",
                Date = DateTime.Today,
                Kind = VoucherKind.Income,
                StoreId = "CH1",
                Amount = 100000m,
                Transfer = false
            };
            Check("Phiếu thu/chi §4 (luật thuần): phiếu hợp lệ qua Validate",
                VoucherRules.Validate(validCash) == null);

            var noNum = new VoucherRow { Name = "  ", StoreId = "CH1", Amount = 10000m };
            var noStore = new VoucherRow { Name = "PT01", StoreId = "", Amount = 10000m };
            var zeroAmt = new VoucherRow { Name = "PT01", StoreId = "CH1", Amount = 0m };
            var negAmt = new VoucherRow { Name = "PT01", StoreId = "CH1", Amount = -5000m };
            var transferNoBank = new VoucherRow { Name = "PC01", StoreId = "CH1", Amount = 10000m, Transfer = true, BankAccountId = "" };

            Check("Phiếu thu/chi §4 #4 (luật thuần): thiếu số phiếu / cửa hàng / số tiền <= 0 / CK thiếu TK đều bị chặn đúng chữ",
                VoucherRules.Validate(noNum) == VoucherConstants.NumberRequired
                && VoucherRules.Validate(noStore) == VoucherConstants.StoreRequired
                && VoucherRules.Validate(zeroAmt) == VoucherConstants.AmountMustBePositive
                && VoucherRules.Validate(negAmt) == VoucherConstants.AmountMustBePositive
                && VoucherRules.Validate(transferNoBank) == VoucherConstants.BankAccountMissing,
                SingleLine(VoucherRules.Validate(transferNoBank)));

            Check("Phiếu thu/chi §4 #7 (T2 luật thuần): chặn sửa/xóa mọi phiếu liên kết và mở phiếu thường",
                VoucherRules.GetLinkedBlockMessage(new VoucherRow { BookingId = "B1" }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { OrderId = "O1" }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { PayrollId = "P1" }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { PrepaidCardId = "C1" }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { IsDebtReceipt = true }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { IsAdvance = true }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { ReasonId = FundReasons.OpeningBalanceId }) != null
                && VoucherRules.GetLinkedBlockMessage(new VoucherRow { ReasonId = FundReasons.TransferId }) != null
                && VoucherRules.GetLinkedBlockMessage(validCash) == null);

            var expCash = new VoucherRow { Kind = VoucherKind.Expense, Transfer = false, StoreId = "CH1", Amount = 120000m };
            var expOld = new VoucherRow { Kind = VoucherKind.Expense, Transfer = false, StoreId = "CH1", Amount = 50000m };
            var expBank = new VoucherRow { Kind = VoucherKind.Expense, Transfer = true, StoreId = "CH1", Amount = 999999m };
            Check("Phiếu thu/chi §4 #3 (T3 luật thuần): chặn chi TM vượt quỹ, không chặn chi CK, hoàn số cũ khi sửa cùng CH",
                VoucherRules.CheckCashOverdraft(expCash, null, 100000m) != null
                && VoucherRules.CheckCashOverdraft(expBank, null, 0m) == null
                && VoucherRules.CheckCashOverdraft(expCash, expOld, 80000m) == null
                && VoucherRules.CheckCashOverdraft(expCash, expOld, 60000m) != null);

            var norm = new VoucherRow
            {
                Name = " PT01 ",
                ObjectType = PhieuThuRules.Customer,
                EmployeeId = "E1",
                CustomerId = "C1",
                SupplierId = "S1",
                Transfer = false,
                BankAccountId = "BK1"
            };
            VoucherRules.NormalizeLinks(norm);
            Check("Phiếu thu/chi (NormalizeLinks): chỉ giữ ID đúng LOAIDOITUONG và xóa TK khi không chuyển khoản",
                norm.Name == "PT01" && norm.CustomerId == "C1" && norm.EmployeeId == null
                && norm.SupplierId == null && norm.BankAccountId == null);
        }

        private static decimal ReadStoreCashBalance(string db, string storeId)
        {
            DateTime horizon = new DateTime(2099, 12, 30);
            FundData data = new FundRepository(db).Load(horizon);
            FundLedger ledger = FundLedgerRules.Build(data.Sources, new FundQuery
            {
                From = new DateTime(1900, 1, 1),
                To = horizon,
                Node = FundNodeKind.Cash,
                KnownAccountIds = data.Accounts.Select(a => a.Id).ToList(),
                Scope = new FundStoreScope { CoversAll = false, StoreIds = new List<string> { storeId } }
            });
            return ledger.ClosingBalance;
        }

        private static decimal ReadStoreBankNet(string db, string storeId, string bankId)
        {
            DateTime horizon = new DateTime(2099, 12, 30);
            FundData data = new FundRepository(db).Load(horizon);
            FundLedger ledger = FundLedgerRules.Build(data.Sources, new FundQuery
            {
                From = new DateTime(1900, 1, 1),
                To = horizon,
                Node = FundNodeKind.Account,
                AccountId = bankId,
                KnownAccountIds = data.Accounts.Select(a => a.Id).ToList(),
                Scope = new FundStoreScope { CoversAll = false, StoreIds = new List<string> { storeId } }
            });
            return ledger.Income - ledger.Expense;
        }

        private static void VoucherServiceDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string bookingId = DbHelper.NewId();
            string orderId = DbHelper.NewId();
            var voucherIds = new List<string>();
            var cfgRepo = new ConfigRepository(db);
            int origLogFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);

            try
            {
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 } }, "selftest_tc");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tc')",
                    "@I", storeId, "@N", "CH ThuChi " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tc')",
                    "@I", bankId, "@N", "TK ThuChi " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_tc')",
                    "@I", incReasonId, "@N", "LD Thu ST " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 2, 30, 'selftest_tc')",
                    "@I", expReasonId, "@N", "LD Chi ST " + tag);

                var svc = new VoucherService(AppSession.Current);
                decimal cash0 = ReadStoreCashBalance(db, storeId);
                decimal bank0 = ReadStoreBankNet(db, storeId, bankId);

                // #1 + #5: Tự sinh số PTyy/##### và ghi phiếu thu tiền mặt 100.000 -> quỹ TM tăng đúng +100.000.
                string autoPt1 = svc.GenerateNumber(VoucherKind.Income);
                var pt1 = new VoucherRow
                {
                    Name = autoPt1,
                    Date = DateTime.Today,
                    Kind = VoucherKind.Income,
                    ObjectType = PhieuThuRules.OtherObject,
                    ObjectName = "Khách lẻ " + tag,
                    Address = "12 Hàng Bài",
                    StoreId = storeId,
                    ReasonId = incReasonId,
                    Amount = 100000m,
                    Transfer = false,
                    Description = "Thu tiền mặt selftest " + tag
                };
                VoucherSaveResult rPt1 = svc.Save(pt1);
                if (rPt1.Ok) voucherIds.Add(rPt1.SavedId);
                VoucherRow backPt1 = rPt1.Ok ? svc.LoadById(rPt1.SavedId) : null;
                decimal cash1 = ReadStoreCashBalance(db, storeId);
                string autoPt2 = svc.GenerateNumber(VoucherKind.Income);

                Check("Phiếu thu/chi §4 #1 (tạo phiếu thu tiền mặt 100.000): ghi TTHUCHI LOAI=0, THU=100000, STATUS=30 và tồn quỹ TM +100.000",
                    rPt1.Ok && backPt1 != null && backPt1.Kind == VoucherKind.Income && backPt1.Amount == 100000m
                    && !backPt1.Transfer && backPt1.Status == 30 && cash1 - cash0 == 100000m,
                    "err=" + SingleLine(rPt1.ErrorMessage) + ", dcash=" + (cash1 - cash0));

                // #5: Sinh số tự động tăng dần + chặn trùng số phiếu.
                var dupPt = new VoucherRow
                {
                    Name = autoPt1,
                    Date = DateTime.Today,
                    Kind = VoucherKind.Income,
                    StoreId = storeId,
                    Amount = 50000m
                };
                VoucherSaveResult rDup = svc.Save(dupPt);
                string autoPc1 = svc.GenerateNumber(VoucherKind.Expense);
                Check("Phiếu thu/chi §4 #5 (sinh số tự động & chặn trùng số): PT tăng tiếp, PC đúng tiền tố, trùng số bị chặn",
                    autoPt1.StartsWith("PT", StringComparison.OrdinalIgnoreCase)
                    && autoPt2 != autoPt1
                    && autoPc1.StartsWith("PC", StringComparison.OrdinalIgnoreCase)
                    && !rDup.Ok && string.IsNullOrEmpty(dupPt.Id),
                    "pt1=" + autoPt1 + ", pt2=" + autoPt2 + ", pc1=" + autoPc1);

                // #2: Tạo phiếu chi chuyển khoản 40.000 -> quỹ TK giảm -40.000, quỹ TM giữ nguyên.
                var pc1 = new VoucherRow
                {
                    Name = autoPc1,
                    Date = DateTime.Today,
                    Kind = VoucherKind.Expense,
                    ObjectType = PhieuThuRules.OtherObject,
                    ObjectName = "NCC Điện " + tag,
                    StoreId = storeId,
                    ReasonId = expReasonId,
                    Amount = 40000m,
                    Transfer = true,
                    BankAccountId = bankId,
                    Description = "Chi CK selftest " + tag
                };
                VoucherSaveResult rPc1 = svc.Save(pc1);
                if (rPc1.Ok) voucherIds.Add(rPc1.SavedId);
                VoucherRow backPc1 = rPc1.Ok ? svc.LoadById(rPc1.SavedId) : null;
                decimal cash2 = ReadStoreCashBalance(db, storeId);
                decimal bank2 = ReadStoreBankNet(db, storeId, bankId);

                Check("Phiếu thu/chi §4 #2 (tạo phiếu chi chuyển khoản 40.000): ghi LOAI=1, CHI=40000, CHUYENKHOAN=30; quỹ TK -40.000, TM giữ nguyên",
                    rPc1.Ok && backPc1 != null && backPc1.Kind == VoucherKind.Expense && backPc1.Amount == 40000m
                    && backPc1.Transfer && backPc1.BankAccountId == bankId
                    && cash2 == cash1 && bank2 - bank0 == -40000m,
                    "err=" + SingleLine(rPc1.ErrorMessage) + ", dbank=" + (bank2 - bank0));

                // #3: Chi tiền mặt vượt quỹ (200.000 > 100.000) -> bị chặn, báo số dư, DB không đổi.
                int countBeforeOver = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE DCUAHANGID = @S", "@S", storeId);
                var pcOver = new VoucherRow
                {
                    Name = "PC-OVER-" + tag,
                    Date = DateTime.Today,
                    Kind = VoucherKind.Expense,
                    StoreId = storeId,
                    Amount = 200000m,
                    Transfer = false
                };
                VoucherSaveResult rOver = svc.Save(pcOver);
                int countAfterOver = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE DCUAHANGID = @S", "@S", storeId);
                Check("Phiếu thu/chi §4 #3 (T3 chi tiền mặt vượt quỹ): bị chặn, thông báo có số dư hiện có, DB không đổi",
                    !rOver.Ok && rOver.ErrorMessage != null && rOver.ErrorMessage.IndexOf("100.000", StringComparison.Ordinal) >= 0
                    && countBeforeOver == countAfterOver,
                    SingleLine(rOver.ErrorMessage));

                // #4: Kiểm tra bắt buộc trên Service: chuyển khoản thiếu TK, số tiền <= 0.
                VoucherSaveResult rNoBank = svc.Save(new VoucherRow
                {
                    Name = "PC-NOBK-" + tag, Date = DateTime.Today, Kind = VoucherKind.Expense,
                    StoreId = storeId, Amount = 10000m, Transfer = true, BankAccountId = null
                });
                VoucherSaveResult rZero = svc.Save(new VoucherRow
                {
                    Name = "PT-ZERO-" + tag, Date = DateTime.Today, Kind = VoucherKind.Income,
                    StoreId = storeId, Amount = 0m
                });
                Check("Phiếu thu/chi §4 #4 (chặn chuyển khoản thiếu TK & số tiền <= 0 ở Service): đúng thông báo, không ghi DB",
                    !rNoBank.Ok && rNoBank.ErrorMessage == VoucherConstants.BankAccountMissing
                    && !rZero.Ok && rZero.ErrorMessage == VoucherConstants.AmountMustBePositive
                    && BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE DCUAHANGID = @S", "@S", storeId) == countAfterOver);

                // #6: Sửa phiếu thu thường 100.000 -> 150.000: giữ nguyên USERCREATEDID/TIMECREATED, cập nhật MODIFIED & tồn quỹ +50.000.
                DateTime? origCreatedAt = backPt1 == null ? (DateTime?)null : backPt1.CreatedAt;
                string origCreatedBy = backPt1 == null ? null : backPt1.CreatedById;
                backPt1.Amount = 150000m;
                backPt1.Description = "Đã sửa diễn giải " + tag;
                VoucherSaveResult rEdit = svc.Save(backPt1);
                VoucherRow afterEdit = svc.LoadById(backPt1.Id);
                decimal cashAfterEdit = ReadStoreCashBalance(db, storeId);
                Check("Phiếu thu/chi §4 #6 (sửa phiếu thường): giữ nguyên USERCREATEDID/TIMECREATED, cập nhật MODIFIED và tồn quỹ = +150.000",
                    rEdit.Ok && afterEdit != null && afterEdit.Amount == 150000m
                    && afterEdit.Description == "Đã sửa diễn giải " + tag
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && !string.IsNullOrEmpty(afterEdit.ModifiedById) && afterEdit.ModifiedAt.HasValue
                    && cashAfterEdit - cash0 == 150000m,
                    "err=" + SingleLine(rEdit.ErrorMessage) + ", cash=" + (cashAfterEdit - cash0));

                // #7: Phiếu cọc (TDATHANGID) và phiếu gắn hóa đơn (TDONHANGID) bị chặn sửa & xóa, DB không đổi.
                string depId = DbHelper.NewId();
                string ordVoucherId = DbHelper.NewId();
                voucherIds.Add(depId);
                voucherIds.Add(ordVoucherId);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, DCUAHANGID, TDATHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 50000, 0, @S, @B, 30, 'selftest_tc', @T)",
                    "@I", depId, "@N", "PT-COC-" + tag, "@D", DateTime.Today, "@S", storeId, "@B", bookingId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, DCUAHANGID, TDONHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 60000, 0, @S, @O, 30, 'selftest_tc', @T)",
                    "@I", ordVoucherId, "@N", "PT-HD-" + tag, "@D", DateTime.Today, "@S", storeId, "@O", orderId, "@T", DbHelper.Now());

                VoucherRow depRow = svc.LoadById(depId);
                depRow.Amount = 75000m;
                VoucherSaveResult rEditDep = svc.Save(depRow);
                VoucherDeleteResult rDelDep = svc.Delete(depId);
                VoucherDeleteResult rDelOrd = svc.Delete(ordVoucherId);
                VoucherRow depStill = svc.LoadById(depId);
                VoucherRow ordStill = svc.LoadById(ordVoucherId);

                Check("Phiếu thu/chi §4 #7 (T2 chặn sửa/xóa phiếu cọc & phiếu hóa đơn): báo rõ lý do, dữ liệu gốc không đổi",
                    !rEditDep.Ok && !rDelDep.Ok && !rDelOrd.Ok
                    && depStill != null && depStill.Amount == 50000m
                    && ordStill != null && ordStill.Amount == 60000m,
                    SingleLine(rEditDep.ErrorMessage));

                // #8: Xóa cứng phiếu chi thường + ghi lưu vết TLUUVET trong 1 transaction; kiểm rollback khi lỗi giữa chừng.
                int logBefore = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", pc1.Id);
                VoucherDeleteResult rDelPc1 = svc.Delete(pc1.Id);
                int logAfter = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", pc1.Id);
                decimal bankAfterDel = ReadStoreBankNet(db, storeId, bankId);
                bool pc1Gone = svc.LoadById(pc1.Id) == null;

                bool rolledBack = false;
                var rbRow = new VoucherRow
                {
                    Name = "PT-RB-" + tag, Date = DateTime.Today, Kind = VoucherKind.Income,
                    StoreId = storeId, Amount = 12345m
                };
                try
                {
                    svc.Save(rbRow, delegate { throw new InvalidOperationException("Giả lập lỗi giữa transaction"); });
                }
                catch (InvalidOperationException)
                {
                    rolledBack = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE NAME = @N", "@N", "PT-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbRow.Id);
                }

                Check("Phiếu thu/chi §4 #8 (T4 xóa phiếu thường + TLUUVET + rollback transaction): xóa sạch, hoàn tồn quỹ, có lưu vết và rollback trọn vẹn",
                    rDelPc1.Ok && pc1Gone && bankAfterDel == bank0 && logAfter == logBefore + 1 && rolledBack,
                    "gone=" + pc1Gone + ", log=" + (logAfter - logBefore) + ", rb=" + rolledBack);
            }
            finally
            {
                cfgRepo.Save(new[] { new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origLogFlag } }, "selftest_tc");
                SystemConfig.Reload(db);
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static void VoucherCatalogDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            var reasonRepo = new CashReasonRepository(db);
            var bankRepo = new BankAccountRepository(db);
            string reasonId = null;
            string sysReasonId = DbHelper.NewId();
            string bankId = null;
            string refVoucherId = null;

            try
            {
                // 1. Lý do thu chi: thêm, chặn trùng tên (UNICODE_CI_AI), sửa, chặn sửa/xóa lý do hệ thống, chặn xóa còn tham chiếu, xóa sạch.
                var rNew = new CashReasonRow { Name = "Chi điện nước " + tag, IsIncome = false, ReasonType = 1, Note = "Định phí" };
                SimpleCatalogSaveResult sr1 = reasonRepo.Save(rNew, "selftest_tc");
                reasonId = sr1.SavedId;
                SimpleCatalogSaveResult srDup = reasonRepo.Save(new CashReasonRow { Name = "CHI DIEN NUOC " + tag, IsIncome = false, ReasonType = 1 }, "selftest_tc");

                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, 0, 30, 'selftest_tc')",
                    "@I", sysReasonId, "@N", "Tạm ứng ST " + tag);
                CashReasonRow sysLoaded = reasonRepo.LoadById(sysReasonId);
                sysLoaded.ReasonType = 2;
                SimpleCatalogSaveResult srSysEdit = reasonRepo.Save(sysLoaded, "selftest_tc");
                SimpleCatalogDeleteResult drSys = reasonRepo.Delete(sysReasonId);

                // 2. Tài khoản ngân hàng: thêm, chặn trùng tên, tham chiếu từ TTHUCHI (kể cả STATUS = -30) -> chặn xóa kèm số tham chiếu.
                var bNew = new BankAccountRow { Name = "Ngân hàng Á Châu " + tag, Note = "TK test" };
                SimpleCatalogSaveResult sb1 = bankRepo.Save(bNew, "selftest_tc");
                bankId = sb1.SavedId;
                SimpleCatalogSaveResult sbDup = bankRepo.Save(new BankAccountRow { Name = "NGAN HANG A CHAU " + tag }, "selftest_tc");

                refVoucherId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, DLYDOTHUCHIID, STATUS, USERCREATEDID) " +
                    "VALUES (@I, @N, @D, 1, 0, 10000, 30, @BK, @LD, -30, 'selftest_tc')",
                    "@I", refVoucherId, "@N", "PC-REF-" + tag, "@D", DateTime.Today, "@BK", bankId, "@LD", reasonId);

                SimpleCatalogDeleteResult drReasonRef = reasonRepo.Delete(reasonId);
                SimpleCatalogDeleteResult drBankRef = bankRepo.Delete(bankId);

                SafeDeleteById(db, "TTHUCHI", refVoucherId);
                refVoucherId = null;

                SimpleCatalogDeleteResult drReasonOk = reasonRepo.Delete(reasonId);
                if (drReasonOk.Ok) reasonId = null;
                SimpleCatalogDeleteResult drBankOk = bankRepo.Delete(bankId);
                if (drBankOk.Ok) bankId = null;

                Check("Phiếu thu/chi §4 #10 (DLYDOTHUCHI): thêm, chặn trùng tên không dấu, khóa sửa/xóa lý do hệ thống, chặn xóa còn tham chiếu (kèm số đếm), xóa sạch khi hết tham chiếu",
                    sr1.Ok && !srDup.Ok && !srSysEdit.Ok && !drSys.Ok
                    && !drReasonRef.Ok && drReasonRef.ReferenceCount == 1 && drReasonRef.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0
                    && drReasonOk.Ok,
                    "sysDel=" + SingleLine(drSys.ErrorMessage) + ", refDel=" + SingleLine(drReasonRef.ErrorMessage));

                Check("Phiếu thu/chi §4 #10 (DTAIKHOANNGANHANG): thêm, chặn trùng tên không dấu, chặn xóa kể cả dòng STATUS=-30 (kèm số đếm), xóa sạch khi hết tham chiếu",
                    sb1.Ok && !sbDup.Ok
                    && !drBankRef.Ok && drBankRef.ReferenceCount == 1 && drBankRef.ErrorMessage.IndexOf("(1)", StringComparison.Ordinal) >= 0
                    && drBankOk.Ok,
                    "refDel=" + SingleLine(drBankRef.ErrorMessage));
            }
            finally
            {
                SafeDeleteById(db, "TTHUCHI", refVoucherId);
                SafeDeleteById(db, "DLYDOTHUCHI", reasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", sysReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
            }
        }
    }
}
