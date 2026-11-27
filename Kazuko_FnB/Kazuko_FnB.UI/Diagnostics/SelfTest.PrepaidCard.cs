using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrepaidCardTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_ttt", UserName = "selftest_ttt", DisplayName = "Selftest TheTraTruoc",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };

                PrepaidCardPureRuleTests();
                PrepaidCardCrudAndGroupDbTests(db);
                PrepaidCardTransactionsAndFundDbTests(db);
                PrepaidCardUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void PrepaidCardPureRuleTests()
        {
            DateTime today = new DateTime(2026, 6, 15);
            var validCard = new PrepaidCardRow { Code = "TTT-001", Note = "Ghi chú" };
            bool validOk = PrepaidCardRules.ValidateCard(validCard, false).Ok;
            bool emptyBlocked = !PrepaidCardRules.ValidateCard(new PrepaidCardRow { Code = "  " }, false).Ok;
            bool dupBlocked = !PrepaidCardRules.ValidateCard(validCard, true).Ok;
            bool code256Blocked = !PrepaidCardRules.ValidateCard(new PrepaidCardRow { Code = new string('A', 256) }, false).Ok;
            bool note256Blocked = !PrepaidCardRules.ValidateCard(new PrepaidCardRow { Code = "OK", Note = new string('B', 256) }, false).Ok;
            bool clip255Ok = PrepaidCardRules.ClipLogText(new string('X', 300)).Length == 255;

            string delMsg = PrepaidCardRules.FormatDeleteBlockedMessage(
                new PrepaidCardReferenceCounts { VoucherCount = 3, OrderCount = 2, CustomerCount = 1 });
            string warnLocked = PrepaidCardRules.BuildLockedOrExpiredWarning(new PrepaidCardRow { Locked = true }, today);
            string warnExpired = PrepaidCardRules.BuildLockedOrExpiredWarning(new PrepaidCardRow { ExpiryDate = today.AddDays(-1) }, today);
            string warnNormal = PrepaidCardRules.BuildLockedOrExpiredWarning(new PrepaidCardRow { ExpiryDate = today.AddDays(5) }, today);
            string overMaxErr = PrepaidCardRules.ValidateVoucher(
                new VoucherRow { PrepaidCardId = "C1", Name = "PT01", StoreId = "S1", Amount = 1000000000m });
            bool fmtOk = PrepaidCardRules.FormatMoney(1200000m) == "1.200.000"
                && PrepaidCardRules.FormatMoney(1234.5m) == "1.234,5";

            Check("Thẻ trả trước §5 #1/#7 (luật thuần, Chiến chốt 28/11 không chặn số dư): mã bắt buộc/trùng/256, thông báo xóa 3 loại, cảnh báo khóa/hết hạn, hạn mức 9 chữ số, định dạng tiền độc lập culture",
                validOk && emptyBlocked && dupBlocked && code256Blocked && note256Blocked && clip255Ok
                && delMsg == "Thẻ đã có 3 phiếu thu chi, 2 hóa đơn, 1 khách đang giữ, không xóa được"
                && warnLocked != null && warnExpired != null && warnNormal == null
                && overMaxErr == PrepaidCardText.AmountTooLarge && fmtOk,
                SingleLine("val=" + validOk + ", delMsg=" + SingleLine(delMsg) + ", fmt=" + fmtOk));
        }

        private static void PrepaidCardCrudAndGroupDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });

            var cardSvc = new PrepaidCardService(AppSession.Current);
            var grpSvc = new PrepaidCardGroupService(AppSession.Current);
            var cardRepo = new PrepaidCardRepository(db);
            var grpRepo = new PrepaidCardGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = cardSvc.GetServerDate();

            string parentId = null, childId = null, emptyGrpId = null;
            string card1Id = null, card255Id = null, cardRefId = null;
            string custId = null, billId = null, v1Id = null, v2Id = null, vDelId = null;

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);

                // §5 #1: Thêm thẻ hợp lệ, trùng UNICODE_CI_AI, sửa chính nó, khóa/mở khóa, hạn dùng, mã 255 ký tự không tràn TLUUVET.
                var c1 = new PrepaidCardRow { Code = "TTT-ĐẸP-" + tag, Note = "Thẻ thử" };
                PrepaidCardSaveResult r1 = cardSvc.Save(c1);
                card1Id = r1.SavedId;
                PrepaidCardRow loaded1 = cardSvc.LoadById(card1Id);

                PrepaidCardSaveResult rDup = cardSvc.Save(new PrepaidCardRow { Code = "ttt-dep-" + tag });
                PrepaidCardSaveResult rEmpty = cardSvc.Save(new PrepaidCardRow { Code = "  " });
                PrepaidCardSaveResult r256 = cardSvc.Save(new PrepaidCardRow { Code = new string('K', 256) });

                PrepaidCardRow toLock = cardSvc.LoadById(card1Id);
                toLock.Locked = true;
                toLock.ExpiryDate = today.AddDays(60);
                toLock.Note = "Đã khóa";
                PrepaidCardSaveResult rLock = cardSvc.Save(toLock);
                PrepaidCardRow afterLock = cardSvc.LoadById(card1Id);

                PrepaidCardRow toUnlock = cardSvc.LoadById(card1Id);
                toUnlock.Locked = false;
                toUnlock.Code = "TTT-ĐẸP-SỬA-" + tag;
                PrepaidCardSaveResult rUnlock = cardSvc.Save(toUnlock);
                PrepaidCardRow afterUnlock = cardSvc.LoadById(card1Id);

                string code255 = ("T255_" + tag + "_").PadRight(255, 'A');
                PrepaidCardSaveResult r255 = cardSvc.Save(new PrepaidCardRow { Code = code255, Note = new string('N', 255) });
                card255Id = r255.SavedId;
                int logs255 = CountActivityLogsForBill(db, card255Id);

                string lockExpStr = afterLock != null && afterLock.ExpiryDate.HasValue
                    ? afterLock.ExpiryDate.Value.ToString("yyyy-MM-dd") : "null";
                Check("Thẻ trả trước §5 #1 (CRUD DB & TLUUVET 255): thêm mới STATUS=30 KHOA=0 audit đúng, chặn trùng UNICODE_CI_AI/rỗng/256, sửa chính nó, khóa/mở khóa/hạn dùng & 255 ký tự không tràn lưu vết",
                    r1.Ok && loaded1 != null && loaded1.Status == 30 && !loaded1.Locked
                    && loaded1.CreatedById == "selftest_ttt" && loaded1.CreatedAt.HasValue
                    && !rDup.Ok && !rEmpty.Ok && !r256.Ok
                    && rLock.Ok && afterLock != null && afterLock.Locked
                    && afterLock.ExpiryDate.HasValue && afterLock.ExpiryDate.Value.Date == today.AddDays(60).Date
                    && afterLock.Note == "Đã khóa"
                    && rUnlock.Ok && afterUnlock != null && !afterUnlock.Locked && afterUnlock.Code == "TTT-ĐẸP-SỬA-" + tag
                    && r255.Ok && logs255 >= 1,
                    SingleLine("r1=" + r1.Ok + ", dup=" + !rDup.Ok
                        + ", rLock.Ok=" + rLock.Ok + ", rLock.Err=" + SingleLine(rLock.ErrorMessage)
                        + ", afterLock.Locked=" + (afterLock != null && afterLock.Locked)
                        + ", afterLock.Exp=" + lockExpStr
                        + ", afterLock.Note=" + SingleLine(afterLock == null ? null : afterLock.Note)
                        + ", rUnlock.Ok=" + rUnlock.Ok + ", unlock=" + (afterUnlock != null && !afterUnlock.Locked)
                        + ", log255=" + logs255));

                // §5 #2: Cây nhóm cha/con, chặn trùng cùng cha, chặn xóa còn thẻ/còn con, xóa nhóm rỗng.
                var parent = PrepaidCardGroupRow.NewDefault();
                parent.Name = "Nhóm Thẻ Cha " + tag;
                parentId = grpSvc.Save(parent).SavedId;

                var dupSame = PrepaidCardGroupRow.NewDefault();
                dupSame.Name = "nhom the cha " + tag;
                PrepaidCardGroupSaveResult dupSameRes = grpSvc.Save(dupSame);

                var child = PrepaidCardGroupRow.NewDefault();
                child.Name = "Nhóm Thẻ Cha " + tag; // khác cha -> hợp lệ
                child.ParentId = parentId;
                PrepaidCardGroupSaveResult childRes = grpSvc.Save(child);
                childId = childRes.SavedId;

                afterUnlock.GroupId = childId;
                cardSvc.Save(afterUnlock);

                PrepaidCardGroupDeleteResult delChildBlocked = grpSvc.Delete(childId);
                PrepaidCardGroupDeleteResult delParentBlocked = grpSvc.Delete(parentId);

                var emptyGrp = PrepaidCardGroupRow.NewDefault();
                emptyGrp.Name = "Nhóm Thẻ Trống " + tag;
                emptyGrpId = grpSvc.Save(emptyGrp).SavedId;
                PrepaidCardGroupDeleteResult delEmptyOk = grpSvc.Delete(emptyGrpId);
                bool emptyGone = grpRepo.LoadById(emptyGrpId) == null;
                emptyGrpId = null;

                Check("Thẻ trả trước §5 #2 (cây nhóm DB): thêm cha/con, chặn trùng cùng cha, chặn xóa nhóm còn thẻ & nhóm còn con, xóa nhóm trống được",
                    !string.IsNullOrEmpty(parentId) && !dupSameRes.Ok && childRes.Ok
                    && !delChildBlocked.Ok && delChildBlocked.CardCount == 1
                    && !delParentBlocked.Ok && delParentBlocked.ChildGroupCount == 1
                    && delEmptyOk.Ok && emptyGone,
                    SingleLine("childBlk=" + delChildBlocked.CardCount + ", parentBlk=" + delParentBlocked.ChildGroupCount + ", emptyDel=" + (delEmptyOk.Ok && emptyGone)));

                // §5 #7: Chặn xóa thẻ nêu đúng số lượng từng loại (2 phiếu thu chi active + 1 phiếu -30 không đếm, 1 hóa đơn, 1 khách); thẻ trống xóa cứng + lưu vết.
                cardRefId = cardSvc.Save(new PrepaidCardRow { Code = "TTT-REF-" + tag }).SavedId;
                DateTime now = DbHelper.Now();
                v1Id = DbHelper.NewId();
                v2Id = DbHelper.NewId();
                vDelId = DbHelper.NewId();
                const string insVoucherSql = "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, DTHETRATRUOCID, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @D, @L, @TH, @CH, @C, @S, 'selftest_ttt', @T)";
                BookingExec(db, insVoucherSql, "@I", v1Id, "@N", "PT-REF1-" + tag, "@D", today, "@L", 0, "@TH", 100000m, "@CH", 0m, "@C", cardRefId, "@S", 30, "@T", now);
                BookingExec(db, insVoucherSql, "@I", v2Id, "@N", "PC-REF2-" + tag, "@D", today, "@L", 1, "@TH", 0m, "@CH", 20000m, "@C", cardRefId, "@S", 30, "@T", now);
                BookingExec(db, insVoucherSql, "@I", vDelId, "@N", "PT-REFDEL-" + tag, "@D", today, "@L", 0, "@TH", 50000m, "@CH", 0m, "@C", cardRefId, "@S", -30, "@T", now);
                billId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, TONGCONG, THETRATRUOC, DTHETRATRUOCID, DATHANHTOAN, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @D, 0, 50000, 50000, @C, 30, 30, 'selftest_ttt', @T)",
                    "@I", billId, "@N", "HD-REF-" + tag, "@D", today, "@C", cardRefId, "@T", now);
                custId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DTHETRATRUOCID, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @M, @N, @C, 30, 'selftest_ttt', @T)",
                    "@I", custId, "@M", "KHTTT" + tag, "@N", "Khách Giữ Thẻ " + tag, "@C", cardRefId, "@T", now);

                PrepaidCardDeleteResult delRefRes = cardSvc.Delete(cardRefId);
                bool cardRefStillThere = cardRepo.LoadById(cardRefId) != null;

                // Xóa cứng thẻ trống (card255Id) -> thành công + ghi TLUUVET.
                int logsBeforeDel = CountActivityLogsForBill(db, card255Id);
                PrepaidCardDeleteResult delEmptyCard = cardSvc.Delete(card255Id);
                bool card255Gone = cardRepo.LoadById(card255Id) == null;
                int logsAfterDel = CountActivityLogsForBill(db, card255Id);
                if (delEmptyCard.Ok) card255Id = null;

                // Rollback thẻ & nhóm bằng hook trước commit.
                var rbCard = new PrepaidCardRow { Code = "TTT-RB-" + tag };
                bool rbCardOk = false;
                try { cardSvc.Save(rbCard, delegate { throw new InvalidOperationException("sim-rb-card"); }); }
                catch (InvalidOperationException) { rbCardOk = string.IsNullOrEmpty(rbCard.Id) && !cardRepo.IsCodeTaken("TTT-RB-" + tag, ""); }

                var rbGrp = PrepaidCardGroupRow.NewDefault();
                rbGrp.Name = "Nhóm RB " + tag;
                bool rbGrpOk = false;
                try { grpSvc.Save(rbGrp, delegate { throw new InvalidOperationException("sim-rb-grp"); }); }
                catch (InvalidOperationException) { rbGrpOk = string.IsNullOrEmpty(rbGrp.Id) && !grpRepo.IsNameTaken("Nhóm RB " + tag, "", ""); }

                Check("Thẻ trả trước §5 #7/#10 (chặn xóa nêu đúng 3 số lượng, xóa cứng thẻ trống + TLUUVET, rollback hook): đạt",
                    !delRefRes.Ok && cardRefStillThere
                    && delRefRes.VoucherCount == 2 && delRefRes.OrderCount == 1 && delRefRes.CustomerCount == 1
                    && delRefRes.ErrorMessage == "Thẻ đã có 2 phiếu thu chi, 1 hóa đơn, 1 khách đang giữ, không xóa được"
                    && delEmptyCard.Ok && card255Gone && logsAfterDel == logsBeforeDel + 1
                    && rbCardOk && rbGrpOk,
                    SingleLine("refMsg=" + SingleLine(delRefRes.ErrorMessage) + ", delEmpty=" + card255Gone + ", rb=" + rbCardOk + "/" + rbGrpOk));
            }
            finally
            {
                foreach (string vid in new[] { v1Id, v2Id, vDelId })
                    if (vid != null) SafeDeleteById(db, "TTHUCHI", vid);
                if (billId != null) SafeDeleteById(db, "TDONHANG", billId);
                if (custId != null) SafeDeleteById(db, "DKHACHHANG", custId);
                foreach (string cid in new[] { card1Id, card255Id, cardRefId })
                {
                    if (cid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", cid);
                    SafeDeleteById(db, "DTHETRATRUOC", cid);
                }
                foreach (string gid in new[] { emptyGrpId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMTHETRATRUOC", gid);
                }
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void PrepaidCardTransactionsAndFundDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { ActivityConstants.ConfigKey });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string emptyStoreId = DbHelper.NewId();
            string bankAId = DbHelper.NewId();
            string incReasonId = DbHelper.NewId();
            string expReasonId = DbHelper.NewId();
            string cardAId = null, cardBId = null, cardLockId = null, cardExpId = null;
            string billUseId = null, billCancelId = null, billBId = null;
            var createdVoucherIds = new List<string>();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, ActivityConstants.ConfigKey, 30);
                DateTime now = DbHelper.Now();
                DateTime today = now.Date;

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ttt')",
                    "@I", storeId, "@N", "CH TTT " + tag);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ttt')",
                    "@I", emptyStoreId, "@N", "CH TTT Rỗng " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ttt')",
                    "@I", bankAId, "@N", "TK A TTT " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_ttt')",
                    "@I", incReasonId, "@N", "Nạp thẻ ST " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, -1, 30, 'selftest_ttt')",
                    "@I", expReasonId, "@N", "Chi trả thẻ ST " + tag);

                var cardSvc = new PrepaidCardService(AppSession.Current);
                var pcVoucherSvc = new PrepaidCardVoucherService(AppSession.Current);
                var voucherSvc = new VoucherService(AppSession.Current);
                var tableSvc = new TableService(AppSession.Current);

                cardAId = cardSvc.Save(new PrepaidCardRow { Code = "TTT-A-" + tag }).SavedId;
                cardBId = cardSvc.Save(new PrepaidCardRow { Code = "TTT-B-" + tag }).SavedId;
                cardLockId = cardSvc.Save(new PrepaidCardRow { Code = "TTT-LOCK-" + tag, Locked = true }).SavedId;
                cardExpId = cardSvc.Save(new PrepaidCardRow { Code = "TTT-EXP-" + tag, ExpiryDate = today.AddDays(-2) }).SavedId;

                decimal cash0 = ReadStoreCashBalance(db, storeId);
                decimal bank0 = ReadStoreBankNet(db, storeId, bankAId);

                // §5 #3a: Nạp 1.000.000 tiền mặt -> LOAI=0, THU=1000000, quỹ TM +1.000.000, Tổng nạp 1.000.000, Còn lại 1.000.000.
                string pt1No = pcVoucherSvc.GenerateNumber(VoucherKind.Income);
                VoucherSaveResult rTop1 = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = pt1No, Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardAId,
                    StoreId = storeId, ReasonId = incReasonId, Amount = 1000000m, Transfer = false,
                    Description = PrepaidCardText.DefaultTopUpDescription("TTT-A-" + tag)
                });
                if (rTop1.Ok) createdVoucherIds.Add(rTop1.SavedId);
                PrepaidCardRow cardA1 = cardSvc.LoadById(cardAId);
                decimal cash1 = ReadStoreCashBalance(db, storeId);

                // §5 #3b: Nạp chuyển khoản 500.000 qua tài khoản A -> quỹ TK A +500.000, Tổng nạp 1.500.000, Còn lại 1.500.000; thiếu TK bị chặn.
                string pt2No = pcVoucherSvc.GenerateNumber(VoucherKind.Income);
                VoucherSaveResult rTop2 = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = pt2No, Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardAId,
                    StoreId = storeId, ReasonId = incReasonId, Amount = 500000m, Transfer = true, BankAccountId = bankAId,
                    Description = PrepaidCardText.DefaultTopUpDescription("TTT-A-" + tag)
                });
                if (rTop2.Ok) createdVoucherIds.Add(rTop2.SavedId);
                VoucherSaveResult rNoBank = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = "PT-NOBK-" + tag, Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardAId,
                    StoreId = storeId, Amount = 100000m, Transfer = true, BankAccountId = null
                });
                PrepaidCardRow cardA2 = cardSvc.LoadById(cardAId);
                decimal bank2 = ReadStoreBankNet(db, storeId, bankAId);

                Check("Thẻ trả trước §5 #3 (nạp 1.000.000 TM & 500.000 CK qua TK A): TTHUCHI/quỹ TM +1.000.000/quỹ TK A +500.000, Tổng nạp 1.500.000, Còn lại 1.500.000, chặn CK thiếu TK",
                    rTop1.Ok && pt1No.StartsWith("PT", StringComparison.OrdinalIgnoreCase)
                    && cash1 - cash0 == 1000000m && cardA1.TotalLoaded == 1000000m && cardA1.Remaining == 1000000m
                    && rTop2.Ok && bank2 - bank0 == 500000m && ReadStoreCashBalance(db, storeId) == cash1
                    && cardA2.TotalLoaded == 1500000m && cardA2.Remaining == 1500000m
                    && !rNoBank.Ok && rNoBank.ErrorMessage == VoucherConstants.BankAccountMissing
                    && pcVoucherSvc.LoadLatestReasonId(VoucherKind.Income) == incReasonId,
                    SingleLine("dcash1=" + (cash1 - cash0) + ", dbank2=" + (bank2 - bank0)
                        + ", rem2=" + cardA2.Remaining + ", noBk=" + !rNoBank.Ok));

                int noDebtFlag = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COALESCE(KHONGTHAYDOICONGNO, 0) FROM TTHUCHI WHERE ID = @I", "@I", rTop1.SavedId)));
                Check("Thẻ trả trước (Chiến chốt 28/11): phiếu nạp/chi thẻ không ép KHONGTHAYDOICONGNO (ghi 0, có tính vào công nợ như phiếu thường)",
                    rTop1.Ok && noDebtFlag == 0, "flag=" + noDebtFlag);

                // §5 #4: Hóa đơn dùng thẻ 300.000 (STATUS=30) + hóa đơn hủy 999.000 (STATUS=-30) -> Tổng sử dụng 300.000, Còn lại 1.200.000.
                billUseId = DbHelper.NewId();
                billCancelId = DbHelper.NewId();
                const string insOrderSql = "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, TONGCONG, THETRATRUOC, DTHETRATRUOCID, DATHANHTOAN, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @D, 0, @A, @A, @C, 30, @S, 'selftest_ttt', @T)";
                BookingExec(db, insOrderSql, "@I", billUseId, "@N", "HD-USE-" + tag, "@D", today, "@A", 300000m, "@C", cardAId, "@S", 30, "@T", now);
                BookingExec(db, insOrderSql, "@I", billCancelId, "@N", "HD-CXL-" + tag, "@D", today, "@A", 999000m, "@C", cardAId, "@S", -30, "@T", now);

                PrepaidCardRow cardA3 = cardSvc.LoadById(cardAId);
                PrepaidCardInfo payValid = tableSvc.ValidatePrepaidCardCode("TTT-A-" + tag, null);
                PrepaidCardInfo payLocked = tableSvc.ValidatePrepaidCardCode("TTT-LOCK-" + tag, null);
                PrepaidCardInfo payExpired = tableSvc.ValidatePrepaidCardCode("TTT-EXP-" + tag, null);

                Check("Thẻ trả trước §5 #4/#9 (dùng thẻ 300.000, bỏ bill STATUS=-30, khớp số dư thanh toán & chặn khóa/hết hạn): Tổng sử dụng 300.000, Còn lại 1.200.000",
                    cardA3.TotalLoaded == 1500000m && cardA3.TotalUsed == 300000m && cardA3.Remaining == 1200000m
                    && payValid.IsValid && payValid.Balance == 1200000m
                    && !payLocked.IsValid && payLocked.Error == ServicePaymentConstants.PrepaidLocked
                    && !payExpired.IsValid && payExpired.Error == ServicePaymentConstants.PrepaidExpired,
                    SingleLine("loaded=" + cardA3.TotalLoaded + ", used=" + cardA3.TotalUsed + ", rem=" + cardA3.Remaining
                        + ", payBal=" + payValid.Balance + ", lockErr=" + SingleLine(payLocked.Error) + ", expErr=" + SingleLine(payExpired.Error)));

                // §5 #5: Chi trả 200.000 tiền mặt -> LOAI=1, CHI=200000, quỹ TM -200.000, Tổng nạp = 1.300.000, Còn lại = 1.000.000;
                // chi lớn hơn Còn lại bị chặn; chi vượt quỹ tiền mặt cửa hàng bị chặn.
                string pc1No = pcVoucherSvc.GenerateNumber(VoucherKind.Expense);
                VoucherSaveResult rWd1 = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = pc1No, Date = today, Kind = VoucherKind.Expense, PrepaidCardId = cardAId,
                    StoreId = storeId, ReasonId = expReasonId, Amount = 200000m, Transfer = false,
                    Description = PrepaidCardText.DefaultWithdrawDescription("TTT-A-" + tag)
                });
                if (rWd1.Ok) createdVoucherIds.Add(rWd1.SavedId);
                PrepaidCardRow cardA4 = cardSvc.LoadById(cardAId);
                decimal cashAfterWd = ReadStoreCashBalance(db, storeId);

                // Chiến chốt 28/11: chi trả 1.100.000 (Còn lại 1.000.000) vẫn được -> -100.000; nạp 2.000.000 TM vào thẻ khóa làm nguồn quỹ.
                VoucherSaveResult rCashSrc = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = "PT-CASHSRC-" + tag, Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardLockId,
                    StoreId = storeId, ReasonId = incReasonId, Amount = 2000000m, Transfer = false
                });
                if (rCashSrc.Ok) createdVoucherIds.Add(rCashSrc.SavedId);
                decimal cashBeforeOver = ReadStoreCashBalance(db, storeId);
                VoucherSaveResult rWdOverCard = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = "PC-OVER-CARD-" + tag, Date = today, Kind = VoucherKind.Expense, PrepaidCardId = cardAId,
                    StoreId = storeId, ReasonId = expReasonId, Amount = 1100000m, Transfer = false
                });
                if (rWdOverCard.Ok) createdVoucherIds.Add(rWdOverCard.SavedId);
                PrepaidCardRow cardAOver = cardSvc.LoadById(cardAId);
                decimal cashAfterOver = ReadStoreCashBalance(db, storeId);
                VoucherDeleteResult rDelOver = rWdOverCard.Ok ? pcVoucherSvc.Delete(rWdOverCard.SavedId, cardAId) : null;
                PrepaidCardRow cardABackFromOver = cardSvc.LoadById(cardAId);
                VoucherSaveResult rWdOverFund = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = "PC-OVER-FUND-" + tag, Date = today, Kind = VoucherKind.Expense, PrepaidCardId = cardAId,
                    StoreId = emptyStoreId, ReasonId = expReasonId, Amount = 50000m, Transfer = false
                });

                Check("Thẻ trả trước §5 #5 (chi trả 200.000 TM; Chiến chốt 28/11: chi trả 1.100.000 vượt Còn lại 1.000.000 vẫn được, Còn lại -100.000, xóa phiếu thì trở lại; vẫn chặn chi vượt quỹ): quỹ TM -200.000, Tổng nạp 1.300.000, Còn lại 1.000.000",
                    rWd1.Ok && pc1No.StartsWith("PC", StringComparison.OrdinalIgnoreCase)
                    && cashAfterWd - cash1 == -200000m
                    && cardA4.TotalLoaded == 1300000m && cardA4.TotalUsed == 300000m && cardA4.Remaining == 1000000m
                    && rCashSrc.Ok && rWdOverCard.Ok && cashAfterOver - cashBeforeOver == -1100000m
                    && cardAOver.TotalLoaded == 200000m && cardAOver.Remaining == -100000m
                    && rDelOver != null && rDelOver.Ok && cardABackFromOver.Remaining == 1000000m
                    && !rWdOverFund.Ok && rWdOverFund.ErrorMessage == VoucherConstants.CashOverdraftBlocked(0m, 50000m),
                    SingleLine("dcash=" + (cashAfterWd - cash1) + ", rem=" + cardA4.Remaining + ", cashSrc=" + rCashSrc.Ok
                        + ", over=" + rWdOverCard.Ok + "/" + SingleLine(rWdOverCard.ErrorMessage) + ", remOver=" + cardAOver.Remaining
                        + ", dcashOver=" + (cashAfterOver - cashBeforeOver) + ", back=" + cardABackFromOver.Remaining
                        + ", overFund=" + SingleLine(rWdOverFund.ErrorMessage)));

                // §5 #6: Sửa/xóa phiếu nạp gây số dư âm bị chặn; sửa hợp lệ cập nhật quỹ; xóa phiếu chi trả trả lại số dư & quỹ;
                // Danh mục phiếu thu/chi (VoucherService) chặn sửa/xóa phiếu gắn DTHETRATRUOCID.
                VoucherSaveResult rBTop = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = pcVoucherSvc.GenerateNumber(VoucherKind.Income), Date = today, Kind = VoucherKind.Income,
                    PrepaidCardId = cardBId, StoreId = storeId, ReasonId = incReasonId, Amount = 1000000m, Transfer = false
                });
                if (rBTop.Ok) createdVoucherIds.Add(rBTop.SavedId);
                billBId = DbHelper.NewId();
                BookingExec(db, insOrderSql, "@I", billBId, "@N", "HD-USEB-" + tag, "@D", today, "@A", 300000m, "@C", cardBId, "@S", 30, "@T", now);

                decimal cashBeforeEditB = ReadStoreCashBalance(db, storeId);
                VoucherRow editB = pcVoucherSvc.LoadById(rBTop.SavedId, cardBId);
                editB.Amount = 200000m; // sửa 1.000.000 -> 200.000: được; Còn lại = 200.000 - 300.000 = -100.000
                VoucherSaveResult rEditNeg = pcVoucherSvc.Save(editB);
                PrepaidCardRow cardBAfterEditNeg = cardSvc.LoadById(cardBId);
                editB.Amount = 800000m; // -> Còn lại 500.000, quỹ TM so với ban đầu giảm 200.000
                VoucherSaveResult rEditOk = pcVoucherSvc.Save(editB);
                decimal cashAfterEditB = ReadStoreCashBalance(db, storeId);
                PrepaidCardRow cardBAfterEdit = cardSvc.LoadById(cardBId);

                VoucherDeleteResult rDelNeg = pcVoucherSvc.Delete(rBTop.SavedId, cardBId);
                decimal cashAfterDelB = ReadStoreCashBalance(db, storeId);
                PrepaidCardRow cardBAfterDel = cardSvc.LoadById(cardBId);

                // Xóa phiếu chi trả 200.000 của thẻ A -> Còn lại 1.200.000, quỹ TM tăng lại +200.000.
                decimal cashBeforeDelWd = ReadStoreCashBalance(db, storeId);
                VoucherDeleteResult rDelWd = pcVoucherSvc.Delete(rWd1.SavedId, cardAId);
                decimal cashAfterDelWd = ReadStoreCashBalance(db, storeId);
                PrepaidCardRow cardAAfterDelWd = cardSvc.LoadById(cardAId);

                // Danh mục phiếu thu/chi (VoucherService) vẫn chặn sửa & xóa phiếu gắn DTHETRATRUOCID.
                VoucherRow loadedInVoucherSvc = voucherSvc.LoadById(rTop1.SavedId);
                loadedInVoucherSvc.Amount = 900000m;
                VoucherSaveResult rVoucherEditBlocked = voucherSvc.Save(loadedInVoucherSvc);
                VoucherDeleteResult rVoucherDelBlocked = voucherSvc.Delete(rTop1.SavedId);

                // Rollback Save & Delete phiếu thẻ bằng hook + quyền chỉ Xem bị chặn.
                decimal cashBeforeRb = ReadStoreCashBalance(db, storeId);
                var rbVoucher = new VoucherRow
                {
                    Name = "PT-RB-TTT-" + tag, Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardAId,
                    StoreId = storeId, Amount = 123456m, Transfer = false
                };
                bool rbSaveV = false;
                try { pcVoucherSvc.Save(rbVoucher, delegate { throw new InvalidOperationException("sim-rb-vsave"); }); }
                catch (InvalidOperationException)
                {
                    rbSaveV = string.IsNullOrEmpty(rbVoucher.Id)
                        && BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE NAME = @N", "@N", "PT-RB-TTT-" + tag) == 0
                        && ReadStoreCashBalance(db, storeId) == cashBeforeRb;
                }

                bool rbDelV = false;
                try { pcVoucherSvc.Delete(rTop2.SavedId, cardAId, delegate { throw new InvalidOperationException("sim-rb-vdel"); }); }
                catch (InvalidOperationException)
                {
                    rbDelV = voucherSvc.LoadById(rTop2.SavedId) != null
                        && ReadStoreBankNet(db, storeId, bankAId) == bank2;
                }

                var roPerms = new FunctionPermissions(false);
                roPerms.Add(PrepaidCardText.FunctionId, PrepaidCardText.FunctionName, RoleModes.View);
                var roSess = new UserSession { UserId = "u_ttt_ro", IsAdmin = false, DatabasePath = db, Permissions = roPerms };
                var roCardSvc = new PrepaidCardService(roSess);
                var roVoucherSvc = new PrepaidCardVoucherService(roSess);
                bool roBlocked = !roCardSvc.CanAdd && !roCardSvc.CanEdit && !roCardSvc.CanRemove
                    && !roCardSvc.Save(new PrepaidCardRow { Code = "RO-" + tag }).Ok
                    && !roCardSvc.Delete(cardAId).Ok
                    && !roVoucherSvc.Save(new VoucherRow { Name = "PT-RO", Date = today, Kind = VoucherKind.Income, PrepaidCardId = cardAId, StoreId = storeId, Amount = 10000m }).Ok
                    && !roVoucherSvc.Delete(rTop1.SavedId, cardAId).Ok;

                Check("Thẻ trả trước §5 #6/#10 (sửa/xóa phiếu nạp/chi, Chiến chốt 28/11 không chặn số dư âm, hoàn quỹ, chặn ở Danh mục phiếu thu/chi, rollback hook & quyền chỉ Xem): đạt",
                    rEditNeg.Ok && cardBAfterEditNeg.Remaining == -100000m
                    && rDelNeg.Ok && cardBAfterDel.Remaining == -300000m && cashAfterDelB - cashAfterEditB == -800000m
                    && rEditOk.Ok && cardBAfterEdit.Remaining == 500000m && cashAfterEditB - cashBeforeEditB == -200000m
                    && rDelWd.Ok && cardAAfterDelWd.Remaining == 1200000m && cashAfterDelWd - cashBeforeDelWd == 200000m
                    && !rVoucherEditBlocked.Ok && !rVoucherDelBlocked.Ok
                    && rbSaveV && rbDelV && roBlocked,
                    SingleLine("edNeg=" + rEditNeg.Ok + "/" + cardBAfterEditNeg.Remaining + ", delNeg=" + rDelNeg.Ok + "/" + cardBAfterDel.Remaining
                        + ", edCash=" + (cashAfterEditB - cashBeforeEditB) + ", delWdCash=" + (cashAfterDelWd - cashBeforeDelWd)
                        + ", vBlk=" + (!rVoucherEditBlocked.Ok && !rVoucherDelBlocked.Ok) + ", rb=" + rbSaveV + "/" + rbDelV + ", ro=" + roBlocked));
            }
            finally
            {
                foreach (string vid in createdVoucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                foreach (string bid in new[] { billUseId, billCancelId, billBId })
                    if (bid != null) SafeDeleteById(db, "TDONHANG", bid);
                foreach (string cid in new[] { cardAId, cardBId, cardLockId, cardExpId })
                {
                    if (cid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", cid);
                    SafeDeleteById(db, "DTHETRATRUOC", cid);
                }
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankAId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteById(db, "DCUAHANG", emptyStoreId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
