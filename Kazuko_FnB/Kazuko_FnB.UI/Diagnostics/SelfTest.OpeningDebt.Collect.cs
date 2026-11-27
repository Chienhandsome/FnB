using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Công nợ ban đầu — phần Thu nợ theo đơn, chặn giảm dưới đã thu, rollback, lưu vết 255 &amp; quyền (spec §4 #7–#8).</summary>
    public static partial class SelfTest
    {
        private static void OpeningDebtCollectAndGuardDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string custA = DbHelper.NewId();
            string custB = DbHelper.NewId();
            var voucherIds = new List<string>();

            try
            {
                DateTime now = DbHelper.Now();
                string custAName = "Khách Thu Nợ Đầu " + tag;
                string custBName = "Khách Kèm Lô " + tag;

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cnd')",
                    "@I", storeId, "@N", "CH CND7 " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '15 Lý Thường Kiệt', 30, 'selftest_cnd', @T)",
                    "@I", custA, "@C", "KH7A-" + tag, "@N", custAName, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '25 Lý Thường Kiệt', 30, 'selftest_cnd', @T)",
                    "@I", custB, "@C", "KH7B-" + tag, "@N", custBName, "@T", now);

                var svc = new OpeningDebtService(AppSession.Current);
                var debtSvc = new DebtService(AppSession.Current);
                var payRepo = new ServicePaymentRepository(db);

                // Lưu nợ đầu 500.000 cho khách A.
                OpeningDebtSaveResult rInit = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = 500000m }
                    }
                });
                string openOrderId = rInit.SavedOrderIds.Count > 0 ? rInit.SavedOrderIds[0] : "";

                // Spec §4 #7: Phiếu LOAI = 99 xuất hiện trong LoadOpenCustomerBills -> Thu nợ theo đơn thu 200.000 vào nợ đầu.
                List<DebtDocumentRow> openBefore = debtSvc.LoadOpenCustomerBills(custA);
                DebtDocumentRow openRow = openBefore.Find(b => b.Id == openOrderId);
                bool appearsInOpenBills = openRow != null && openRow.Loai == 99
                    && openRow.RemainingDebt == 500000m && openRow.DocTypeLabel == "Nợ đầu kỳ";

                DebtCollectResult collectRes = debtSvc.CollectCustomerDebt(new DebtCollectRequest
                {
                    CustomerId = custA,
                    VoucherNumber = "PT-CND7-" + tag,
                    Date = DateTime.Today,
                    StoreId = storeId,
                    Transfer = false,
                    Description = "Thu 200k vào nợ đầu " + tag,
                    Allocations = new List<DebtAllocationLine>
                    {
                        new DebtAllocationLine
                        {
                            BillId = openOrderId,
                            BillNumber = OpeningDebtConstants.OrderName,
                            BillDate = new DateTime(2025, 12, 31),
                            BillTotal = 500000m,
                            BillRemaining = 500000m,
                            PayAmount = 200000m
                        }
                    }
                });
                if (collectRes.Ok && !string.IsNullOrEmpty(collectRes.SavedVoucherId))
                    voucherIds.Add(collectRes.SavedVoucherId);

                decimal debtAfterCollect = debtSvc.GetCustomerDebt(custA);
                decimal oldDebtAfterCollect = payRepo.GetOldDebt(custA);
                List<DebtDocumentRow> openAfterCollect = debtSvc.LoadOpenCustomerBills(custA);
                DebtDocumentRow openRowAfter = openAfterCollect.Find(b => b.Id == openOrderId);
                bool afterCollectOk = collectRes.Ok
                    && debtAfterCollect == 300000m && oldDebtAfterCollect == 300000m
                    && openRowAfter != null && openRowAfter.AllocatedPaid == 200000m && openRowAfter.RemainingDebt == 300000m;

                // Giảm xuống 100.000 (< 200.000 đã thu) -> CHẶN với câu spec, và KHÔNG ghi dở khách B cùng lô.
                string expectedErr = OpeningDebtConstants.BelowCollectedBlocked(custAName, 200000m);
                OpeningDebtSaveResult rReduceBlocked = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custB, Name = custBName, OpeningDebt = 400000m },
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = 100000m }
                    }
                });
                bool reduceBlockedOk = !rReduceBlocked.Ok
                    && rReduceBlocked.ErrorMessage == expectedErr
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Customer, custA) == 500000m
                    && ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custB).Rows.Count == 0;

                // Xóa nợ đầu (đưa về 0 < 200.000 đã thu) -> cũng CHẶN với câu spec.
                OpeningDebtSaveResult rDeleteBlocked = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = 0m }
                    }
                });
                bool deleteBlockedOk = !rDeleteBlocked.Ok
                    && rDeleteBlocked.ErrorMessage == expectedErr
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Customer, custA) == 500000m;

                // Nâng lên 600.000 (>= 200.000) -> ĐƯỢC phép; nợ còn lại = 400.000.
                OpeningDebtSaveResult rRaiseOk = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = new DateTime(2025, 12, 31),
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = 600000m }
                    }
                });
                decimal debtAfterRaise = debtSvc.GetCustomerDebt(custA);
                DebtDocumentRow openRowRaised = debtSvc.LoadOpenCustomerBills(custA).Find(b => b.Id == openOrderId);
                bool raiseOk = rRaiseOk.Ok
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Customer, custA) == 600000m
                    && debtAfterRaise == 400000m
                    && openRowRaised != null && openRowRaised.RemainingDebt == 400000m;

                Check("Công nợ đầu P2 §4 #7 (Thu nợ theo đơn thu 200k vào nợ đầu 500k -> còn 300k; giảm xuống 100k & xóa về 0 bị chặn đúng thông báo; nâng lên 600k được -> còn 400k): đạt toàn bộ",
                    appearsInOpenBills && afterCollectOk && reduceBlockedOk && deleteBlockedOk && raiseOk,
                    SingleLine("open=" + appearsInOpenBills + ", col=" + afterCollectOk
                        + ", blk100=" + reduceBlockedOk + ", blk0=" + deleteBlockedOk + ", up600=" + raiseOk
                        + ", err=" + rReduceBlocked.ErrorMessage));
            }
            finally
            {
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TTHUCHIID = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Customer, new[] { custA, custB });
                SafeDeleteById(db, "DKHACHHANG", custA);
                SafeDeleteById(db, "DKHACHHANG", custB);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static void OpeningDebtRollbackLogAndPermDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { "KichHoatLuuVetHoatDong" });

            string tag = DbHelper.NewId().Substring(0, 8);
            string custA = DbHelper.NewId();
            string custB = DbHelper.NewId();
            string custLong = DbHelper.NewId();
            string supA = DbHelper.NewId();

            try
            {
                SetOpeningConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);
                DateTime now = DbHelper.Now();
                string longName = "KH_" + tag + "_" + new string('X', 255 - ("KH_" + tag + "_").Length);

                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest_cnd', @T)",
                    "@I", custA, "@C", "KH8A-" + tag, "@N", "Khách Lô A " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest_cnd', @T)",
                    "@I", custB, "@C", "KH8B-" + tag, "@N", "Khách Lô B " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest_cnd', @T)",
                    "@I", custLong, "@C", "KH255-" + tag, "@N", longName, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest_cnd', @T)",
                    "@I", supA, "@C", "NCC8A-" + tag, "@N", "NCC Lưu Nhiều Lần " + tag, "@T", now);

                var svc = new OpeningDebtService(AppSession.Current);
                DateTime openDate = new DateTime(2025, 12, 31);

                // Mỗi đối tác chỉ một phiếu dù lưu nhiều lần (cả KH & NCC).
                OpeningDebtSaveResult k1 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 500000m } }
                });
                OpeningDebtSaveResult k2 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 700000m } }
                });
                OpeningDebtSaveResult s1 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = supA, Name = "NCC Lưu Nhiều Lần " + tag, OpeningDebt = 300000m } }
                });
                OpeningDebtSaveResult s2 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = supA, Name = "NCC Lưu Nhiều Lần " + tag, OpeningDebt = 450000m } }
                });
                bool singleOrderOk = k1.Ok && k2.Ok && k1.SavedOrderIds[0] == k2.SavedOrderIds[0]
                    && ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custA).Rows.Count == 1
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Customer, custA) == 700000m
                    && s1.Ok && s2.Ok && s1.SavedOrderIds[0] == s2.SavedOrderIds[0]
                    && ReadOpeningDebtOrders(db, DebtPartyKind.Supplier, supA).Rows.Count == 1
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Supplier, supA) == 450000m;

                // Một transaction cho nhiều đối tác: hook lỗi thì KHÔNG đối tác nào đổi & KHÔNG ghi thêm lưu vết.
                int logsBeforeRb = BookingCount(db,
                    "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F AND (BAN = @A OR BAN = @B)",
                    "@F", OpeningDebtConstants.CustomerFunctionName,
                    "@A", "Khách Lô A " + tag,
                    "@B", "Khách Lô B " + tag);
                bool rbCaught = false;
                try
                {
                    svc.Save(new OpeningDebtSaveRequest
                    {
                        PartyKind = DebtPartyKind.Customer,
                        Date = openDate,
                        Items = new List<OpeningDebtPartnerRow>
                        {
                            new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 999000m },
                            new OpeningDebtPartnerRow { PartnerId = custB, Name = "Khách Lô B " + tag, OpeningDebt = 350000m }
                        }
                    }, delegate { throw new InvalidOperationException("Giả lập lỗi rollback nhiều đối tác"); });
                }
                catch (InvalidOperationException)
                {
                    rbCaught = true;
                }
                int logsAfterRb = BookingCount(db,
                    "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F AND (BAN = @A OR BAN = @B)",
                    "@F", OpeningDebtConstants.CustomerFunctionName,
                    "@A", "Khách Lô A " + tag,
                    "@B", "Khách Lô B " + tag);
                bool rollbackOk = rbCaught
                    && ReadOpeningDebtAmount(db, DebtPartyKind.Customer, custA) == 700000m
                    && ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custB).Rows.Count == 0
                    && logsAfterRb == logsBeforeRb;

                // Lưu vết từng đối tác thay đổi (không ghi lại đối tác không đổi) + tên 255 ký tự không tràn BAN/NOTE.
                OpeningDebtSaveResult rLog = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 700000m }, // không đổi
                        new OpeningDebtPartnerRow { PartnerId = custB, Name = "Khách Lô B " + tag, OpeningDebt = 250000m }, // mới
                        new OpeningDebtPartnerRow { PartnerId = custLong, Name = longName, OpeningDebt = 150000m }          // mới 255 ký tự
                    }
                });
                DataTable logLong = rLog.Ok && rLog.SavedOrderIds.Count >= 2
                    ? DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                        "SELECT FIRST 1 BAN, NOTE FROM TLUUVET WHERE SODONHANG = @I ORDER BY TIMECREATED DESC",
                        "@I", rLog.SavedOrderIds[1]))
                    : new DataTable();
                int logsB = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE CHUCNANG = @F AND BAN = @B",
                    "@F", OpeningDebtConstants.CustomerFunctionName, "@B", "Khách Lô B " + tag);
                bool log255Ok = rLog.Ok && rLog.ChangedCount == 2 && logsB == 1
                    && logLong.Rows.Count == 1
                    && (DbHelper.Str(logLong.Rows[0]["BAN"]) ?? "").Length <= 255
                    && (DbHelper.Str(logLong.Rows[0]["NOTE"]) ?? "").Length <= 255;

                // Quyền chỉ Xem không lưu được; có Sửa nhưng không có quyền đổi ngày thì không đổi ngày phiếu.
                var viewPerms = new FunctionPermissions(false);
                viewPerms.Add("fn_cnd_kh", OpeningDebtConstants.CustomerFunctionName, RoleModes.View);
                viewPerms.Add("fn_cnd_ncc", OpeningDebtConstants.SupplierFunctionName, RoleModes.View);
                var viewSvc = new OpeningDebtService(new UserSession
                {
                    UserId = "u_cnd_ro", UserName = "u_cnd_ro", DisplayName = "Chỉ xem CND",
                    IsAdmin = false, DatabasePath = db, Permissions = viewPerms
                });
                OpeningDebtSaveResult denyKh = viewSvc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 800000m } }
                });
                OpeningDebtSaveResult denyNcc = viewSvc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow> { new OpeningDebtPartnerRow { PartnerId = supA, Name = "NCC Lưu Nhiều Lần " + tag, OpeningDebt = 500000m } }
                });

                var editNoDatePerms = new FunctionPermissions(false);
                editNoDatePerms.Add("fn_cnd_kh", OpeningDebtConstants.CustomerFunctionName, RoleModes.View | RoleModes.Edit);
                var editNoDateSvc = new OpeningDebtService(new UserSession
                {
                    UserId = "u_cnd_nd", UserName = "u_cnd_nd", DisplayName = "Sửa CND không đổi ngày",
                    IsAdmin = false, DatabasePath = db, Permissions = editNoDatePerms
                });
                OpeningDebtSaveResult rNoDate = editNoDateSvc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = new DateTime(2026, 6, 15),
                    DateChanged = true,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = "Khách Lô A " + tag, OpeningDebt = 750000m }
                    }
                });
                DateTime dateAfter = Convert.ToDateTime(ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custA).Rows[0]["NGAY"]).Date;
                bool permOk = !viewSvc.CanEdit(DebtPartyKind.Customer) && !viewSvc.CanEdit(DebtPartyKind.Supplier)
                    && !denyKh.Ok && denyKh.ErrorMessage == OpeningDebtConstants.NoPermission
                    && !denyNcc.Ok && denyNcc.ErrorMessage == OpeningDebtConstants.NoPermission
                    && !editNoDateSvc.CanChangeDate && rNoDate.Ok && dateAfter == openDate;

                Check("Công nợ đầu P2 §4 #8 (mỗi đối tác 1 phiếu dù lưu nhiều lần; rollback hook nhiều đối tác; lưu vết từng đối tác & cắt 255; chỉ Xem không lưu & khóa đổi ngày): đạt toàn bộ",
                    singleOrderOk && rollbackOk && log255Ok && permOk,
                    SingleLine("single=" + singleOrderOk + ", rb=" + rollbackOk + ", log255=" + log255Ok + ", perm=" + permOk));
            }
            finally
            {
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Customer, new[] { custA, custB, custLong });
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Supplier, new[] { supA });
                SafeDeleteById(db, "DKHACHHANG", custA);
                SafeDeleteById(db, "DKHACHHANG", custB);
                SafeDeleteById(db, "DKHACHHANG", custLong);
                SafeDeleteById(db, "DNHACUNGCAP", supA);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
        }
    }
}
