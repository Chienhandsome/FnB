using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest Công nợ ban đầu khách hàng &amp; nhà cung cấp (docs/specs/so-du-dau-ky.md P2, §4 #6–#9).</summary>
    public static partial class SelfTest
    {
        private static void OpeningDebtTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_cnd",
                    UserName = "selftest_cnd",
                    DisplayName = "Selftest CongNoDau",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                OpeningDebtPureRuleTests();
                OpeningDebtCustomerAndSupplierDbTests(db);
                OpeningDebtCollectAndGuardDbTests(db);
                OpeningDebtRollbackLogAndPermDbTests(db);
                OpeningDebtUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void OpeningDebtPureRuleTests()
        {
            DateTime defDate = OpeningDebtRules.DefaultOpeningDate(new DateTime(2026, 11, 27));
            var items = new List<OpeningDebtPartnerRow>
            {
                new OpeningDebtPartnerRow { PartnerId = "K1", Code = "KH01", Name = "Nguyễn Văn An", Address = "12 Lê Lợi", Phone = "090111222", OpeningDebt = 500000m },
                new OpeningDebtPartnerRow { PartnerId = "K2", Code = "KH02", Name = "Trần Thị Bình", Address = "34 Hai Bà Trưng", Phone = "090333444", OpeningDebt = 0m },
                new OpeningDebtPartnerRow { PartnerId = "K3", Code = "KH03", Name = "Lê Hoàng Cường", Address = "56 Nguyễn Huệ", Phone = "090555666", OpeningDebt = 250000m }
            };

            OpeningDebtTotals totals = OpeningDebtRules.ComputeTotals(items);
            bool onlyDebtOk = OpeningDebtRules.MatchesFilter(items[0], new OpeningDebtFilter { OnlyWithDebt = true })
                && !OpeningDebtRules.MatchesFilter(items[1], new OpeningDebtFilter { OnlyWithDebt = true });
            bool searchOk = OpeningDebtRules.MatchesFilter(items[0], new OpeningDebtFilter { SearchText = "nguyen van an" })
                && OpeningDebtRules.MatchesFilter(items[1], new OpeningDebtFilter { SearchText = "090333444" })
                && OpeningDebtRules.MatchesFilter(items[2], new OpeningDebtFilter { SearchText = "nguyen hue" })
                && !OpeningDebtRules.MatchesFilter(items[1], new OpeningDebtFilter { SearchText = "le loi" });

            string errNeg = OpeningDebtRules.Validate(new OpeningDebtSaveRequest
            {
                PartyKind = DebtPartyKind.Customer,
                Items = new List<OpeningDebtPartnerRow>
                {
                    new OpeningDebtPartnerRow { PartnerId = "K1", Name = "Nguyễn Văn An", OpeningDebt = -10000m }
                }
            });
            string validOk = OpeningDebtRules.Validate(new OpeningDebtSaveRequest
            {
                PartyKind = DebtPartyKind.Customer,
                Items = items
            });

            Check("Công nợ đầu P2 (luật thuần): ngày mặc định 31/12 năm trước, tổng 750.000, lọc chỉ có nợ & tìm không dấu, chặn âm nêu tên đối tác",
                defDate == new DateTime(2025, 12, 31)
                && totals.PartnerCount == 3 && totals.WithDebtCount == 2 && totals.TotalOpeningDebt == 750000m
                && onlyDebtOk && searchOk
                && errNeg == OpeningDebtConstants.NegativeDebtBlocked("Nguyễn Văn An")
                && validOk == null,
                SingleLine("def=" + defDate.ToString("yyyy-MM-dd") + ", tot=" + totals.TotalOpeningDebt + ", neg=" + errNeg));
        }

        private static void OpeningDebtCustomerAndSupplierDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string custA = DbHelper.NewId();
            string custB = DbHelper.NewId();
            string custOff = DbHelper.NewId();
            string supA = DbHelper.NewId();
            string supB = DbHelper.NewId();
            var extraOrders = new List<string>();
            var extraVouchers = new List<string>();

            try
            {
                DateTime now = DbHelper.Now();
                string custAName = "Khách Nợ Đầu A " + tag;
                string custBName = "Khách Nợ Đầu B " + tag;
                string supAName = "NCC Nợ Đầu A " + tag;
                string supBName = "NCC Nợ Đầu B " + tag;

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cnd')",
                    "@I", storeId, "@N", "CH CND " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '10 Lê Lợi', '090100100', 30, 'selftest_cnd', @T)",
                    "@I", custA, "@C", "KHA-" + tag, "@N", custAName, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '20 Lê Lợi', '090200200', 30, 'selftest_cnd', @T)",
                    "@I", custB, "@C", "KHB-" + tag, "@N", custBName, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 0, 'selftest_cnd', @T)",
                    "@I", custOff, "@C", "KHOFF-" + tag, "@N", "Khách Ngừng Dùng " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '30 Trần Hưng Đạo', '090300300', 30, 'selftest_cnd', @T)",
                    "@I", supA, "@C", "NCCA-" + tag, "@N", supAName, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '40 Trần Hưng Đạo', '090400400', 30, 'selftest_cnd', @T)",
                    "@I", supB, "@C", "NCCB-" + tag, "@N", supBName, "@T", now);

                var svc = new OpeningDebtService(AppSession.Current);
                var debtSvc = new DebtService(AppSession.Current);
                var payRepo = new ServicePaymentRepository(db);
                DateTime openDate = new DateTime(2025, 12, 31);

                // Nạp chỉ lấy đối tác đang dùng (STATUS = 30).
                OpeningDebtDocument initDoc = svc.LoadDocument(DebtPartyKind.Customer);
                bool activeFilterOk = initDoc.Items.Exists(x => x.PartnerId == custA)
                    && initDoc.Items.Exists(x => x.PartnerId == custB)
                    && !initDoc.Items.Exists(x => x.PartnerId == custOff);

                // Spec §4 #6 (Khách): A = 500.000, B = 300.000 -> tạo 2 phiếu LOAI = 99; sau đó đưa B về 0 -> xóa phiếu B.
                OpeningDebtSaveResult rCust1 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = 500000m },
                        new OpeningDebtPartnerRow { PartnerId = custB, Name = custBName, OpeningDebt = 300000m }
                    }
                });
                DataTable hdrA = ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custA);
                bool custAHdrOk = rCust1.Ok && hdrA.Rows.Count == 1
                    && DbHelper.Int(hdrA.Rows[0]["LOAI"]) == 99
                    && DbHelper.Str(hdrA.Rows[0]["NAME"]) == "CONGNODAU"
                    && DbHelper.Int(hdrA.Rows[0]["STATUS"]) == 30
                    && DbHelper.Int(hdrA.Rows[0]["DATHANHTOAN"]) == 30
                    && DbHelper.Dec(hdrA.Rows[0]["TONGCONG"]) == 500000m
                    && DbHelper.Dec(hdrA.Rows[0]["TIENHANG"]) == 500000m
                    && DbHelper.Dec(hdrA.Rows[0]["TIENTHANHTOAN"]) == 0m
                    && DbHelper.Dec(hdrA.Rows[0]["TILETHUE"]) == 0m
                    && DbHelper.Str(hdrA.Rows[0]["DKHACHHANGID"]) == custA
                    && string.IsNullOrEmpty(DbHelper.Str(hdrA.Rows[0]["DNHACUNGCAPID"]));

                // Thêm phát sinh bill bán 200.000 (thanh toán 50.000 -> nợ thêm 150.000) -> tổng công nợ A = 650.000.
                extraOrders.Add(InsertDebtOrder(db, "HD-CND-" + tag, 0, 30, 30, custA, null, storeId, 200000m, 50000m, DateTime.Today));
                decimal custADebt = debtSvc.GetCustomerDebt(custA);
                decimal custAOldDebt = payRepo.GetOldDebt(custA);
                DebtPartnerRow custAListRow = debtSvc.LoadPartnerDebts(DebtPartyKind.Customer).Find(x => x.Id == custA);

                // Đưa B về 0 -> xóa phiếu LOAI = 99 của B; âm bị chặn nêu tên đối tác.
                OpeningDebtSaveResult rCustDelB = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custB, Name = custBName, OpeningDebt = 0m }
                    }
                });
                bool custBDeletedOk = rCustDelB.Ok && rCustDelB.DeletedCount == 1
                    && ReadOpeningDebtOrders(db, DebtPartyKind.Customer, custB).Rows.Count == 0
                    && debtSvc.GetCustomerDebt(custB) == 0m;

                OpeningDebtSaveResult rCustNeg = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Customer,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = custA, Name = custAName, OpeningDebt = -50000m }
                    }
                });
                bool custNegBlocked = !rCustNeg.Ok && rCustNeg.ErrorMessage != null
                    && rCustNeg.ErrorMessage.IndexOf(custAName, StringComparison.Ordinal) >= 0;

                // Spec §4 #6 (Nhà cung cấp): S1 = 500.000 (LOAI = 98), S2 = 250.000 rồi về 0 xóa phiếu, âm bị chặn.
                OpeningDebtSaveResult rSup1 = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = supA, Name = supAName, OpeningDebt = 500000m },
                        new OpeningDebtPartnerRow { PartnerId = supB, Name = supBName, OpeningDebt = 250000m }
                    }
                });
                DataTable hdrSupA = ReadOpeningDebtOrders(db, DebtPartyKind.Supplier, supA);
                bool supAHdrOk = rSup1.Ok && hdrSupA.Rows.Count == 1
                    && DbHelper.Int(hdrSupA.Rows[0]["LOAI"]) == 98
                    && DbHelper.Str(hdrSupA.Rows[0]["NAME"]) == "CONGNODAU"
                    && DbHelper.Int(hdrSupA.Rows[0]["STATUS"]) == 30
                    && DbHelper.Int(hdrSupA.Rows[0]["DATHANHTOAN"]) == 30
                    && DbHelper.Dec(hdrSupA.Rows[0]["TONGCONG"]) == 500000m
                    && DbHelper.Dec(hdrSupA.Rows[0]["TIENHANG"]) == 500000m
                    && DbHelper.Dec(hdrSupA.Rows[0]["TIENTHANHTOAN"]) == 0m
                    && DbHelper.Dec(hdrSupA.Rows[0]["TILETHUE"]) == 0m
                    && DbHelper.Str(hdrSupA.Rows[0]["DNHACUNGCAPID"]) == supA
                    && string.IsNullOrEmpty(DbHelper.Str(hdrSupA.Rows[0]["DKHACHHANGID"]));

                // Phát sinh NCC: nhập kho 1.000.000 + chi trả 200.000 -> nợ NCC S1 = 500k + 1.000k - 200k = 1.300.000.
                extraOrders.Add(InsertDebtOrder(db, "PN-CND-" + tag, 1, 30, 0, null, supA, storeId, 1000000m, 0m, DateTime.Today));
                extraVouchers.Add(InsertDebtVoucher(db, "PC-CND-" + tag, 1, 0, 0, null, supA, storeId, 0m, 200000m, null, 30, DateTime.Today));
                decimal supADebt = debtSvc.GetSupplierDebt(supA);
                DebtPartnerRow supAListRow = debtSvc.LoadPartnerDebts(DebtPartyKind.Supplier).Find(x => x.Id == supA);
                DebtPartnerDetail supADetail = debtSvc.LoadPartnerDetail(DebtPartyKind.Supplier, supA);
                bool supDetailHas98 = supADetail.Documents.Exists(d => d.Loai == 98 && d.TotalAmount == 500000m);

                OpeningDebtSaveResult rSupDelB = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = supB, Name = supBName, OpeningDebt = 0m }
                    }
                });
                bool supBDeletedOk = rSupDelB.Ok && ReadOpeningDebtOrders(db, DebtPartyKind.Supplier, supB).Rows.Count == 0;

                OpeningDebtSaveResult rSupNeg = svc.Save(new OpeningDebtSaveRequest
                {
                    PartyKind = DebtPartyKind.Supplier,
                    Date = openDate,
                    Items = new List<OpeningDebtPartnerRow>
                    {
                        new OpeningDebtPartnerRow { PartnerId = supB, Name = supBName, OpeningDebt = -10000m }
                    }
                });
                bool supNegBlocked = !rSupNeg.Ok && rSupNeg.ErrorMessage != null
                    && rSupNeg.ErrorMessage.IndexOf(supBName, StringComparison.Ordinal) >= 0;

                Check("Công nợ đầu P2 §4 #6 (KH A=500k + phát sinh 150k = 650k khớp GetNoCu, B về 0 xóa phiếu, âm chặn; NCC LOAI=98 500k + nhập 1tr - chi 200k = 1,3tr, về 0 xóa, âm chặn): đạt toàn bộ",
                    activeFilterOk && custAHdrOk && custADebt == 650000m && custAOldDebt == 650000m
                    && custAListRow != null && custAListRow.RemainingDebt == 650000m
                    && custBDeletedOk && custNegBlocked
                    && supAHdrOk && supADebt == 1300000m
                    && supAListRow != null && supAListRow.RemainingDebt == 1300000m && supDetailHas98
                    && supBDeletedOk && supNegBlocked,
                    SingleLine("cust=" + custADebt + "/" + custAOldDebt + ", delB=" + custBDeletedOk
                        + ", sup=" + supADebt + ", det98=" + supDetailHas98 + ", neg=" + custNegBlocked + "/" + supNegBlocked));
            }
            finally
            {
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Customer, new[] { custA, custB });
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Supplier, new[] { supA, supB });
                foreach (string vid in extraVouchers) SafeDeleteById(db, "TTHUCHI", vid);
                foreach (string oid in extraOrders) SafeDeleteById(db, "TDONHANG", oid);
                SafeDeleteById(db, "DKHACHHANG", custA);
                SafeDeleteById(db, "DKHACHHANG", custB);
                SafeDeleteById(db, "DKHACHHANG", custOff);
                SafeDeleteById(db, "DNHACUNGCAP", supA);
                SafeDeleteById(db, "DNHACUNGCAP", supB);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static DataTable ReadOpeningDebtOrders(string db, DebtPartyKind kind, string partnerId)
        {
            int loai = OpeningDebtRules.OrderType(kind);
            string col = kind == DebtPartyKind.Customer ? "DKHACHHANGID" : "DNHACUNGCAPID";
            return DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHACHHANGID, DNHACUNGCAPID, " +
                "TIENHANG, TONGCONG, TIENTHANHTOAN, TILETHUE " +
                "FROM TDONHANG WHERE " + col + " = @P AND COALESCE(LOAI, 0) = @L AND COALESCE(STATUS, 0) = 30",
                "@P", partnerId, "@L", loai));
        }

        private static decimal ReadOpeningDebtAmount(string db, DebtPartyKind kind, string partnerId)
        {
            DataTable dt = ReadOpeningDebtOrders(db, kind, partnerId);
            return dt.Rows.Count == 0 ? 0m : DbHelper.Dec(dt.Rows[0]["TONGCONG"]);
        }

        private static void CleanupOpeningDebtPartnerOrders(string db, DebtPartyKind kind, IEnumerable<string> partnerIds)
        {
            int loai = OpeningDebtRules.OrderType(kind);
            string col = kind == DebtPartyKind.Customer ? "DKHACHHANGID" : "DNHACUNGCAPID";
            foreach (string pid in partnerIds)
            {
                if (string.IsNullOrEmpty(pid))
                    continue;
                DataTable dt = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                    "SELECT ID FROM TDONHANG WHERE " + col + " = @P AND COALESCE(LOAI, 0) = @L",
                    "@P", pid, "@L", loai));
                foreach (DataRow r in dt.Rows)
                {
                    string oid = DbHelper.Str(r["ID"]);
                    if (string.IsNullOrEmpty(oid)) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TDONHANGID = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
            }
        }
    }
}
