using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void DebtTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_cn",
                    UserName = "selftest_cn",
                    DisplayName = "Selftest CongNo",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                DebtRulesPureTests();
                DebtCustomerAndSupplierFormulaDbTests(db);
                DebtCollectAndSupplierPayDbTests(db);
                DebtUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void DebtRulesPureTests()
        {
            // Phân bổ từ đơn cũ nhất (spec §6 #5 & §7 T1): 2 đơn 200.000 và 150.000, thu 250.000 -> 200.000 và 50.000.
            var lines = new List<DebtAllocationLine>
            {
                new DebtAllocationLine { BillId = "B1", BillNumber = "HD01", BillRemaining = 200000m },
                new DebtAllocationLine { BillId = "B2", BillNumber = "HD02", BillRemaining = 150000m }
            };
            decimal alloc = DebtRules.AllocateOldestFirst(lines, 250000m);
            bool allocOk = alloc == 250000m && lines[0].PayAmount == 200000m && lines[1].PayAmount == 50000m;

            // Validate chặn thu quá nợ một đơn, chặn tổng <= 0, chặn CK thiếu tài khoản, chặn âm.
            decimal totalOut;
            var overReq = new DebtCollectRequest
            {
                CustomerId = "C1", VoucherNumber = "PT26/00001", StoreId = "S1", Date = DateTime.Today,
                Allocations = new List<DebtAllocationLine>
                {
                    new DebtAllocationLine { BillId = "B1", BillNumber = "HD01", BillRemaining = 100000m, PayAmount = 120000m }
                }
            };
            string errOver = DebtRules.ValidateCollectRequest(overReq, out totalOut);

            var noBankReq = new DebtCollectRequest
            {
                CustomerId = "C1", VoucherNumber = "PT26/00001", StoreId = "S1", Date = DateTime.Today,
                Transfer = true, BankAccountId = "",
                Allocations = new List<DebtAllocationLine>
                {
                    new DebtAllocationLine { BillId = "B1", BillNumber = "HD01", BillRemaining = 100000m, PayAmount = 50000m }
                }
            };
            string errBank = DebtRules.ValidateCollectRequest(noBankReq, out totalOut);

            var zeroReq = new DebtCollectRequest
            {
                CustomerId = "C1", VoucherNumber = "PT26/00001", StoreId = "S1", Date = DateTime.Today,
                Allocations = new List<DebtAllocationLine>
                {
                    new DebtAllocationLine { BillId = "B1", BillNumber = "HD01", BillRemaining = 100000m, PayAmount = 0m }
                }
            };
            string errZero = DebtRules.ValidateCollectRequest(zeroReq, out totalOut);

            // Lọc chỉ còn nợ, tìm không dấu, cây nhóm cha/con.
            var pDebt = new DebtPartnerRow { Id = "1", Code = "KH01", Name = "Đặng Văn An", GroupId = "G_CON", RemainingDebt = 150000m };
            var pZero = new DebtPartnerRow { Id = "2", Code = "KH02", Name = "Trần Bình", GroupId = "G_CHA", RemainingDebt = 0m };
            var groups = new List<DebtGroupNode>
            {
                new DebtGroupNode { Id = "G_CHA", Name = "Cha", ParentId = "" },
                new DebtGroupNode { Id = "G_CON", Name = "Con", ParentId = "G_CHA" }
            };
            HashSet<string> sub = DebtRules.CollectGroupSubtree(groups, "G_CHA");
            bool filterOk = DebtRules.MatchesFilter(pDebt, new DebtFilter { OnlyWithDebt = true })
                && !DebtRules.MatchesFilter(pZero, new DebtFilter { OnlyWithDebt = true })
                && DebtRules.MatchesFilter(pDebt, new DebtFilter { SearchText = "dang van an" })
                && DebtRules.MatchesFilter(pDebt, new DebtFilter { GroupIds = sub });

            Check("Công nợ §6 #5/#6 (luật thuần): phân bổ từ đơn cũ nhất, chặn vượt nợ/CK thiếu TK/tổng 0, lọc nhóm/tìm/chỉ còn nợ",
                allocOk && errOver != null && errBank == VoucherConstants.BankAccountMissing
                && errZero == DebtConstants.CollectTotalMustBePositive && filterOk,
                SingleLine("alloc=" + alloc + ", over=" + (errOver != null) + ", bank=" + (errBank != null) + ", filt=" + filterOk));
        }

        private static void DebtCustomerAndSupplierFormulaDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string custId = DbHelper.NewId();
            string supId = DbHelper.NewId();
            string bookingId = DbHelper.NewId();
            var orderIds = new List<string>();
            var voucherIds = new List<string>();

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cn')",
                    "@I", storeId, "@N", "CH CN " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '12 Lê Lợi', '090111222', 30, 'selftest_cn', @T)",
                    "@I", custId, "@C", "KHCN" + tag, "@N", "Khách Công Nợ " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '88 Hai Bà Trưng', '090333444', 30, 'selftest_cn', @T)",
                    "@I", supId, "@C", "NCCCN" + tag, "@N", "NCC Công Nợ " + tag, "@T", now);

                // §6 #1: Bill 300.000 thanh toán 100.000 -> còn 200.000.
                orderIds.Add(InsertDebtOrder(db, "HD-CN1-" + tag, 0, 30, 30, custId, null, storeId, 300000m, 100000m, DateTime.Today.AddDays(-2)));
                // Bill chưa thanh toán (DATHANHTOAN = 0) & bill đã xóa mềm (STATUS = -30) -> KHÔNG tính.
                orderIds.Add(InsertDebtOrder(db, "HD-UNP-" + tag, 0, 30, 0, custId, null, storeId, 999000m, 0m, DateTime.Today.AddDays(-1)));
                orderIds.Add(InsertDebtOrder(db, "HD-DEL-" + tag, 0, -30, 30, custId, null, storeId, 888000m, 0m, DateTime.Today.AddDays(-1)));
                // §6 #2: Khách vãng lai (DKHACHHANGID NULL và rỗng) -> KHÔNG xuất hiện.
                orderIds.Add(InsertDebtOrder(db, "HD-VL1-" + tag, 0, 30, 30, null, null, storeId, 500000m, 0m, DateTime.Today));
                orderIds.Add(InsertDebtOrder(db, "HD-VL2-" + tag, 0, 30, 30, "", null, storeId, 250000m, 0m, DateTime.Today));

                var svc = new DebtService(AppSession.Current);
                var payRepo = new ServicePaymentRepository(db);

                decimal d1 = svc.GetCustomerDebt(custId);
                // Thêm phiếu thu 50.000 gắn khách -> còn 150.000.
                voucherIds.Add(InsertDebtVoucher(db, "PT-CN1-" + tag, 0, 0, 0, custId, null, storeId, 50000m, 0m, null, 30, DateTime.Today.AddDays(-1)));
                decimal d2 = svc.GetCustomerDebt(custId);

                // Phiếu thu có TDATHANGID (20.000) & phiếu tích KHONGTHAYDOICONGNO (30.000) & phiếu đã xóa (-30) -> KHÔNG tính.
                voucherIds.Add(InsertDebtVoucher(db, "PT-COC-" + tag, 0, 0, 0, custId, null, storeId, 20000m, 0m, bookingId, 30, DateTime.Today));
                voucherIds.Add(InsertDebtVoucher(db, "PT-NDC-" + tag, 0, 0, 30, custId, null, storeId, 30000m, 0m, null, 30, DateTime.Today));
                voucherIds.Add(InsertDebtVoucher(db, "PT-DEL-" + tag, 0, 0, 0, custId, null, storeId, 77000m, 0m, null, -30, DateTime.Today));
                decimal d3 = svc.GetCustomerDebt(custId);

                // Khớp GetOldDebt (Nợ cũ của Sử dụng dịch vụ) + danh sách tổng hợp không chứa khách vãng lai.
                decimal oldDebt = payRepo.GetOldDebt(custId);
                List<DebtPartnerRow> custList = svc.LoadPartnerDebts(DebtPartyKind.Customer);
                DebtPartnerRow custRow = custList.Find(r => r.Id == custId);
                bool noWalkIn = !custList.Exists(r => string.IsNullOrEmpty(r.Id));

                Check("Công nợ §6 #1/#2/#4 (công thức KH, bỏ TDATHANGID & KHONGTHAYDOICONGNO, bỏ khách vãng lai, khớp GetNoCu): 200k -> 150k, khớp GetOldDebt",
                    d1 == 200000m && d2 == 150000m && d3 == 150000m && oldDebt == 150000m
                    && custRow != null && custRow.RemainingDebt == 150000m && noWalkIn,
                    SingleLine("d1=" + d1 + ", d2=" + d2 + ", d3=" + d3 + ", oldDebt=" + oldDebt + ", noVL=" + noWalkIn));

                // §6 #3 (T2, T3): NCC nhập 10 triệu (TIENTHANHTOAN = 0), chi 4 triệu -> nợ 6 triệu; xuất trả 1 triệu -> nợ 5 triệu.
                orderIds.Add(InsertDebtOrder(db, "PN-CN1-" + tag, 1, 30, 0, null, supId, storeId, 10000000m, 0m, DateTime.Today.AddDays(-3)));
                voucherIds.Add(InsertDebtVoucher(db, "PC-CN1-" + tag, 1, 0, 0, null, supId, storeId, 0m, 4000000m, null, 30, DateTime.Today.AddDays(-2)));
                decimal supAfterPay = svc.GetSupplierDebt(supId);

                orderIds.Add(InsertDebtOrder(db, "PX-CN1-" + tag, 2, 30, 0, null, supId, storeId, 1000000m, 0m, DateTime.Today.AddDays(-1)));
                decimal supAfterReturn = svc.GetSupplierDebt(supId);

                // Kiểm thêm T3: xuất trả 500.000 đã thu lại 200.000 -> đảo dấu cả TIENTHANHTOAN: -(500k - 200k) = -300k -> nợ 4.700.000.
                orderIds.Add(InsertDebtOrder(db, "PX-CN2-" + tag, 2, 30, 0, null, supId, storeId, 500000m, 200000m, DateTime.Today));
                // Phiếu chi đánh dấu KHONGTHAYDOICONGNO = 30 -> không tính vào nợ NCC.
                voucherIds.Add(InsertDebtVoucher(db, "PC-NDC-" + tag, 1, 0, 30, null, supId, storeId, 0m, 900000m, null, 30, DateTime.Today));
                decimal supAfterT3 = svc.GetSupplierDebt(supId);
                List<DebtPartnerRow> supList = svc.LoadPartnerDebts(DebtPartyKind.Supplier);
                DebtPartnerRow supRow = supList.Find(r => r.Id == supId);

                Check("Công nợ §6 #3 (T2/T3 NCC): nhập 10tr chi 4tr = 6tr, xuất trả 1tr = 5tr, xuất trả 500k/TT 200k đảo dấu = 4,7tr",
                    supAfterPay == 6000000m && supAfterReturn == 5000000m && supAfterT3 == 4700000m
                    && supRow != null && supRow.RemainingDebt == 4700000m,
                    SingleLine("afterPay=" + supAfterPay + ", afterRet=" + supAfterReturn + ", afterT3=" + supAfterT3));
            }
            finally
            {
                foreach (string vid in voucherIds) SafeDeleteById(db, "TTHUCHI", vid);
                foreach (string oid in orderIds) SafeDeleteById(db, "TDONHANG", oid);
                SafeDeleteById(db, "DKHACHHANG", custId);
                SafeDeleteById(db, "DNHACUNGCAP", supId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static void DebtCollectAndSupplierPayDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string custId = DbHelper.NewId();
            string supId = DbHelper.NewId();
            var orderIds = new List<string>();
            var voucherIds = new List<string>();

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cn')",
                    "@I", storeId, "@N", "CH ThuNo " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cn')",
                    "@I", bankId, "@N", "TK ThuNo " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '50 Quang Trung', 30, 'selftest_cn', @T)",
                    "@I", custId, "@C", "KHTN" + tag, "@N", "Khách Thu Nợ " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '60 Nguyễn Trãi', 30, 'selftest_cn', @T)",
                    "@I", supId, "@C", "NCCTN" + tag, "@N", "NCC Trả Nợ " + tag, "@T", now);

                // §6 #5: Khách nợ 2 bill: 200.000 (cũ hơn) và 150.000 (mới hơn), TIENTHANHTOAN = 0.
                string bill1 = InsertDebtOrder(db, "HD-TN1-" + tag, 0, 30, 30, custId, null, storeId, 200000m, 0m, DateTime.Today.AddDays(-3));
                string bill2 = InsertDebtOrder(db, "HD-TN2-" + tag, 0, 30, 30, custId, null, storeId, 150000m, 0m, DateTime.Today.AddDays(-1));
                orderIds.Add(bill1);
                orderIds.Add(bill2);

                var svc = new DebtService(AppSession.Current);
                var voucherSvc = new VoucherService(AppSession.Current);
                var payRepo = new ServicePaymentRepository(db);

                decimal cash0 = ReadStoreCashBalance(db, storeId);
                List<DebtDocumentRow> openBefore = svc.LoadOpenCustomerBills(custId);
                var allocLines = new List<DebtAllocationLine>();
                foreach (DebtDocumentRow b in openBefore)
                {
                    allocLines.Add(new DebtAllocationLine
                    {
                        BillId = b.Id, BillNumber = b.Number, BillDate = b.Date,
                        BillTotal = b.TotalAmount, BillRemaining = b.RemainingDebt
                    });
                }
                DebtRules.AllocateOldestFirst(allocLines, 250000m);

                string ptNum = svc.GenerateDebtReceiptNumber();
                DebtCollectResult res = svc.CollectCustomerDebt(new DebtCollectRequest
                {
                    CustomerId = custId,
                    VoucherNumber = ptNum,
                    Date = DateTime.Today,
                    StoreId = storeId,
                    Transfer = false,
                    Description = "Thu nợ theo đơn " + tag,
                    Note = "Selftest thu nợ 250k",
                    Allocations = allocLines
                });
                if (res.Ok && !string.IsNullOrEmpty(res.SavedVoucherId))
                    voucherIds.Add(res.SavedVoucherId);

                VoucherRow savedVoucher = res.Ok ? voucherSvc.LoadById(res.SavedVoucherId) : null;
                List<DebtAllocationLine> savedAllocs = res.Ok ? svc.LoadAllocationsByVoucher(res.SavedVoucherId) : new List<DebtAllocationLine>();
                DebtPartnerDetail detailAfter = svc.LoadPartnerDetail(DebtPartyKind.Customer, custId);
                DebtDocumentRow b1After = detailAfter.Documents.Find(x => x.Id == bill1);
                DebtDocumentRow b2After = detailAfter.Documents.Find(x => x.Id == bill2);
                decimal debtAfter = svc.GetCustomerDebt(custId);
                decimal oldDebtAfter = payRepo.GetOldDebt(custId);
                decimal cash1 = ReadStoreCashBalance(db, storeId);

                // TIENTHANHTOAN trên TDONHANG của cả 2 bill phải giữ nguyên = 0.
                decimal rawPaid1 = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT COALESCE(TIENTHANHTOAN, 0) FROM TDONHANG WHERE ID = @I", "@I", bill1)));
                decimal rawPaid2 = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                    "SELECT COALESCE(TIENTHANHTOAN, 0) FROM TDONHANG WHERE ID = @I", "@I", bill2)));

                // Phiếu thu công nợ (LAPHIEUTHUCONGNO = 30) phải bị VoucherService chặn sửa/xóa ở màn Quỹ.
                VoucherDeleteResult delBlocked = res.Ok ? voucherSvc.Delete(res.SavedVoucherId) : VoucherDeleteResult.Fail("skip");
                VoucherSaveResult editBlocked = savedVoucher != null ? voucherSvc.Save(savedVoucher) : VoucherSaveResult.Fail("skip");

                Check("Công nợ §6 #5 (T1 thu nợ theo đơn 250k trên 2 bill 200k + 150k): 1 TTHUCHI LAPHIEUTHUCONGNO=30 + 2 TCHITIETTHANHTOAN, bill1 hết nợ, bill2 còn 100k, tổng nợ 100k khớp GetNoCu, quỹ +250k, TIENTHANHTOAN giữ nguyên, màn Quỹ chặn sửa/xóa",
                    res.Ok && savedVoucher != null && savedVoucher.Kind == VoucherKind.Income && savedVoucher.IsDebtReceipt
                    && savedVoucher.Amount == 250000m && savedVoucher.CustomerId == custId
                    && savedAllocs.Count == 2
                    && savedAllocs.Exists(a => a.BillId == bill1 && a.PayAmount == 200000m)
                    && savedAllocs.Exists(a => a.BillId == bill2 && a.PayAmount == 50000m)
                    && b1After != null && b1After.RemainingDebt == 0m
                    && b2After != null && b2After.RemainingDebt == 100000m
                    && debtAfter == 100000m && oldDebtAfter == 100000m
                    && cash1 - cash0 == 250000m
                    && rawPaid1 == 0m && rawPaid2 == 0m
                    && !delBlocked.Ok && !editBlocked.Ok,
                    SingleLine("ok=" + res.Ok + ", allocs=" + savedAllocs.Count + ", debt=" + debtAfter + ", cash=" + (cash1 - cash0) + ", err=" + res.ErrorMessage));

                // Chặn thu quá nợ một đơn (bill2 còn 100.000, cố thu 120.000) & rollback khi lỗi giữa transaction.
                int countBeforeErr = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE DKHACHHANGID = @C", "@C", custId);
                DebtCollectResult overRes = svc.CollectCustomerDebt(new DebtCollectRequest
                {
                    CustomerId = custId,
                    VoucherNumber = "PT-OVER-" + tag,
                    Date = DateTime.Today,
                    StoreId = storeId,
                    Allocations = new List<DebtAllocationLine>
                    {
                        new DebtAllocationLine { BillId = bill2, BillNumber = "HD-TN2-" + tag, BillRemaining = 150000m, PayAmount = 120000m }
                    }
                });

                bool rolledBack = false;
                try
                {
                    svc.CollectCustomerDebt(new DebtCollectRequest
                    {
                        CustomerId = custId,
                        VoucherNumber = "PT-RB-" + tag,
                        Date = DateTime.Today,
                        StoreId = storeId,
                        Allocations = new List<DebtAllocationLine>
                        {
                            new DebtAllocationLine { BillId = bill2, BillNumber = "HD-TN2-" + tag, BillRemaining = 100000m, PayAmount = 40000m }
                        }
                    }, delegate { throw new InvalidOperationException("Giả lập lỗi giữa transaction thu nợ"); });
                }
                catch (InvalidOperationException)
                {
                    int vAfterRb = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE NAME = @N", "@N", "PT-RB-" + tag);
                    int ctAfterRb = BookingCount(db, "SELECT COUNT(*) FROM TCHITIETTHANHTOAN WHERE TDONHANGID = @B", "@B", bill2);
                    rolledBack = vAfterRb == 0 && ctAfterRb == 1 && svc.GetCustomerDebt(custId) == 100000m;
                }
                int countAfterErr = BookingCount(db, "SELECT COUNT(*) FROM TTHUCHI WHERE DKHACHHANGID = @C", "@C", custId);

                Check("Công nợ §6 #5 (chặn thu quá nợ đơn & rollback khi lỗi giữa chừng): từ chối thu 120k cho đơn còn 100k, lỗi giữa transaction rollback sạch TTHUCHI + TCHITIETTHANHTOAN",
                    !overRes.Ok && countAfterErr == countBeforeErr && rolledBack,
                    SingleLine("overErr=" + overRes.ErrorMessage + ", rb=" + rolledBack));

                // Chi trả nợ NCC qua VoucherService: chặn chi TM vượt quỹ (500k > 250k), cho chi TM hợp lệ 200k -> nợ NCC giảm 200k.
                orderIds.Add(InsertDebtOrder(db, "PN-TN1-" + tag, 1, 30, 0, null, supId, storeId, 600000m, 0m, DateTime.Today));
                VoucherSaveResult supOverCash = voucherSvc.Save(new VoucherRow
                {
                    Name = "PC-OVER-" + tag, Date = DateTime.Today, Kind = VoucherKind.Expense,
                    ObjectType = PhieuThuRules.Supplier, SupplierId = supId, ObjectName = "NCC Trả Nợ " + tag,
                    StoreId = storeId, Amount = 500000m, Transfer = false
                });
                VoucherSaveResult supPayOk = voucherSvc.Save(new VoucherRow
                {
                    Name = "PC-OK-" + tag, Date = DateTime.Today, Kind = VoucherKind.Expense,
                    ObjectType = PhieuThuRules.Supplier, SupplierId = supId, ObjectName = "NCC Trả Nợ " + tag,
                    StoreId = storeId, Amount = 200000m, Transfer = false
                });
                if (supPayOk.Ok) voucherIds.Add(supPayOk.SavedId);
                decimal supDebtAfter = svc.GetSupplierDebt(supId);
                decimal cash2 = ReadStoreCashBalance(db, storeId);

                Check("Công nợ §6 #5 (chi trả nợ NCC & chặn chi tiền mặt vượt quỹ): chặn chi 500k khi quỹ TM 250k; chi 200k giảm nợ NCC 600k -> 400k và quỹ TM còn 50k",
                    !supOverCash.Ok && supPayOk.Ok && supDebtAfter == 400000m && cash2 - cash0 == 50000m,
                    SingleLine("over=" + supOverCash.ErrorMessage + ", supDebt=" + supDebtAfter + ", cash=" + (cash2 - cash0)));
            }
            finally
            {
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TTHUCHIID = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                foreach (string oid in orderIds)
                {
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DKHACHHANG", custId);
                SafeDeleteById(db, "DNHACUNGCAP", supId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static string InsertDebtOrder(
            string db, string name, int loai, int status, int paidFlag,
            string customerId, string supplierId, string storeId,
            decimal total, decimal paid, DateTime date)
        {
            string id = DbHelper.NewId();
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHACHHANGID, DNHACUNGCAPID, DCUAHANGID, TONGCONG, TIENTHANHTOAN, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @L, @S, @P, @D, @KH, @NCC, @CH, @TC, @TT, 'selftest_cn', @T)",
                "@I", id, "@N", name, "@L", loai, "@S", status, "@P", paidFlag,
                "@D", date.Date, "@KH", customerId, "@NCC", supplierId, "@CH", storeId,
                "@TC", total, "@TT", paid, "@T", DbHelper.Now());
            return id;
        }

        private static string InsertDebtVoucher(
            string db, string name, int loai, int isDebtReceipt, int noDebtChange,
            string customerId, string supplierId, string storeId,
            decimal thu, decimal chi, string bookingId, int status, DateTime date)
        {
            string id = DbHelper.NewId();
            BookingExec(db,
                "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, DKHACHHANGID, DNHACUNGCAPID, DCUAHANGID, THU, CHI, TDATHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @D, @L, @CN, @NDC, @KH, @NCC, @CH, @THU, @CHI, @BK, @ST, 'selftest_cn', @T)",
                "@I", id, "@N", name, "@D", date.Date, "@L", loai, "@CN", isDebtReceipt, "@NDC", noDebtChange,
                "@KH", customerId, "@NCC", supplierId, "@CH", storeId, "@THU", thu, "@CHI", chi,
                "@BK", bookingId, "@ST", status, "@T", DbHelper.Now());
            return id;
        }
    }
}
