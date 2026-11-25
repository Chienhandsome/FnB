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
        private sealed class BusinessResultFixture
        {
            public string StoreAId = DbHelper.NewId();
            public string StoreBId = DbHelper.NewId();
            public string User1StoreId = DbHelper.NewId();
            public string User2StoresId = DbHelper.NewId();
            public string User0StoreId = DbHelper.NewId();
            public string LinkU1A = DbHelper.NewId();
            public string LinkU2A = DbHelper.NewId();
            public string LinkU2B = DbHelper.NewId();

            public string GrpCoffeeId = DbHelper.NewId();
            public string GrpTeaId = DbHelper.NewId();
            public string GrpPrevOnlyId = DbHelper.NewId();
            public string GrpZeroId = DbHelper.NewId();

            public string UnitId = DbHelper.NewId();
            public string ProdCoffeeId = DbHelper.NewId();
            public string ProdTeaId = DbHelper.NewId();
            public string ProdCakeId = DbHelper.NewId();
            public string ProdZeroId = DbHelper.NewId();
            public string ProdUngroupedId = DbHelper.NewId();

            public string FixedReason1Id;
            public string FixedReason1Name;
            public string FixedReason2Id;
            public string FixedReason2Name;
            public string FixedReason3Name;
            public string OtherReason1Id;
            public string OtherReason1Name;
            public string OtherReason2Id;
            public string OtherReason2Name;
            public int FixedReasonTotalCount;
            public int OtherReasonTotalCount;
            public string IgnoredReasonId;
            public bool SeededIgnoredReason;

            public List<string> OrderIds = new List<string>();
            public List<string> LineIds = new List<string>();
            public List<string> VoucherIds = new List<string>();

            public DateTime From = new DateTime(2005, 4, 1);
            public DateTime To = new DateTime(2005, 4, 30);
        }

        private static void BusinessResultTests(string db)
        {
            BusinessResultCalculatorUnitTests();
            BusinessResultPermissionTests(db);

            var fx = new BusinessResultFixture();
            Theme.ThemeTokens savedTheme = Theme.ThemeManager.Current;
            try
            {
                SeedBusinessResultFixture(db, fx);
                BusinessResultRepositoryTests(db, fx);
                BusinessResultUiTests(db, fx);
            }
            finally
            {
                Theme.ThemeManager.Apply(savedTheme);
                CleanupBusinessResultFixture(db, fx);
            }
        }

        // ------------------------------------------------------------------ 1. Kiểm công thức thuần (Services)

        private static void BusinessResultCalculatorUnitTests()
        {
            // Ca C2: đủ 7 khoảng ngày bắt buộc trong spec §7.
            CheckPrevRange("01/04–30/04 → 01/03–31/03",
                new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
                new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
            CheckPrevRange("01/02–28/02/2027 → 01/01–31/01/2027",
                new DateTime(2027, 2, 1), new DateTime(2027, 2, 28),
                new DateTime(2027, 1, 1), new DateTime(2027, 1, 31));
            CheckPrevRange("01/02–29/02/2028 (năm nhuận) → 01/01–31/01/2028",
                new DateTime(2028, 2, 1), new DateTime(2028, 2, 29),
                new DateTime(2028, 1, 1), new DateTime(2028, 1, 31));
            CheckPrevRange("01/02–28/02/2028 (28 không phải cuối tháng) → 01/01–28/01/2028",
                new DateTime(2028, 2, 1), new DateTime(2028, 2, 28),
                new DateTime(2028, 1, 1), new DateTime(2028, 1, 28));
            CheckPrevRange("01/03–31/03/2027 → 01/02–28/02/2027",
                new DateTime(2027, 3, 1), new DateTime(2027, 3, 31),
                new DateTime(2027, 2, 1), new DateTime(2027, 2, 28));
            CheckPrevRange("30/04–30/04 (1 ngày cuối tháng) → 30/03–31/03",
                new DateTime(2026, 4, 30), new DateTime(2026, 4, 30),
                new DateTime(2026, 3, 30), new DateTime(2026, 3, 31));
            CheckPrevRange("01/11–25/11 → 01/10–25/10",
                new DateTime(2026, 11, 1), new DateTime(2026, 11, 25),
                new DateTime(2026, 10, 1), new DateTime(2026, 10, 25));

            // Ca C1: LÃI/LỖ tăng giảm = +1.000.000, ▲ (không phải -9.000.000 như công thức gốc).
            var rawC1 = new BusinessResultRawData
            {
                CurrentBills = new BusinessResultBillTotals { Revenue = 10000000m },
                PreviousBills = new BusinessResultBillTotals { Revenue = 8000000m },
                FixedCostReasons = new List<BusinessResultNamedAmount>
                {
                    new BusinessResultNamedAmount { Name = "Tiền nhà", CurrentAmount = 6000000m, PreviousAmount = 5000000m }
                }
            };
            List<BusinessResultRow> rowsC1 = BusinessResultCalculator.BuildRows(rawC1);
            BusinessResultRow plC1 = rowsC1.Find(r => r.Loai == BusinessResultConstants.LoaiProfitLoss);
            Check("KQKD (§7/C1 Tăng giảm LÃI/LỖ): DT 10tr/CP 6tr vs 8tr/5tr → +1.000.000, ▲ (không ra -9.000.000)",
                plC1 != null && plC1.CurrentValue == 4000000m && plC1.PreviousValue == 3000000m
                && plC1.ChangeAmount == 1000000m && plC1.TrendSymbol == "▲"
                && plC1.Content == "LÃI/LỖ" && plC1.ItemNumber == "III.",
                plC1 == null ? "null" : ("chg=" + plC1.ChangeAmount + ", sym=" + plC1.TrendSymbol));

            // Ca C7: biểu tượng ▲ (>0), ▼ (<0), trống (=0).
            Check("KQKD (§7/C7 biểu tượng tăng/giảm): >0 ▲, <0 ▼, =0 trống",
                BusinessResultCalculator.TrendSymbolOf(1m) == "▲"
                && BusinessResultCalculator.TrendSymbolOf(-1m) == "▼"
                && BusinessResultCalculator.TrendSymbolOf(0m) == "");

            // Ca C4: N_tong gồm cả Giảm giá số dương (900k / 100k / giảm giá 100k → mẫu 1.100k → 82%, 9%, 9%).
            var rawC4 = new BusinessResultRawData
            {
                CurrentBills = new BusinessResultBillTotals { Revenue = 900000m, Discount = 100000m },
                RevenueGroups = new List<BusinessResultNamedAmount>
                {
                    new BusinessResultNamedAmount { Name = "Cà phê", CurrentAmount = 900000m },
                    new BusinessResultNamedAmount { Name = "Trà", CurrentAmount = 100000m }
                }
            };
            List<BusinessResultRow> rowsC4 = BusinessResultCalculator.BuildRows(rawC4);
            BusinessResultRow rCf = rowsC4.Find(r => r.Loai == BusinessResultConstants.LoaiRevenueDetail && r.Content == "Cà phê");
            BusinessResultRow rTr = rowsC4.Find(r => r.Loai == BusinessResultConstants.LoaiRevenueDetail && r.Content == "Trà");
            BusinessResultRow rGg = rowsC4.Find(r => r.Loai == BusinessResultConstants.LoaiRevenueDetail && r.Content == "Giảm giá");
            Check("KQKD (§7/C4 mẫu số % doanh thu cộng Giảm giá): 900k/100k/GG 100k → 82%, 9%, 9%",
                rCf != null && rCf.PercentMain == "82%"
                && rTr != null && rTr.PercentMain == "9%"
                && rGg != null && rGg.PercentMain == "9%",
                (rCf == null ? "" : rCf.PercentMain) + "/" + (rTr == null ? "" : rTr.PercentMain) + "/" + (rGg == null ? "" : rGg.PercentMain));

            // Ca §2.1: làm tròn về chẵn (ToEven) cho đúng 0.5%, 1.5%, 2.5%, 3.5% và giá trị != 0 làm tròn về 0 vẫn hiện 0%.
            string r05 = BusinessResultCalculator.FormatRatio(5m, 1000m);
            string r15 = BusinessResultCalculator.FormatRatio(15m, 1000m);
            string r25 = BusinessResultCalculator.FormatRatio(25m, 1000m);
            string r35 = BusinessResultCalculator.FormatRatio(35m, 1000m);
            string rSmall = BusinessResultCalculator.FormatRatio(1m, 1000m);
            Check("KQKD (§7/§2.1 làm tròn về chẵn ToEven): 0.5%→0%, 1.5%→2%, 2.5%→2%, 3.5%→4%, 0.1%→0%",
                r05 == "0%" && r15 == "2%" && r25 == "2%" && r35 == "4%" && rSmall == "0%",
                r05 + "," + r15 + "," + r25 + "," + r35 + "," + rSmall);

            // Ca §2.1: "-" khi giá trị = 0 hoặc mẫu số = 0; ô "100%" điền cứng vẫn hiện khi giá trị = 0; ô không công thức để trống.
            bool dashOk = BusinessResultCalculator.FormatRatio(0m, 100m) == "-"
                && BusinessResultCalculator.FormatRatio(100m, 0m) == "-"
                && BusinessResultCalculator.FormatRatio(0m, 0m) == "-";
            List<BusinessResultRow> zeroRows = BusinessResultCalculator.BuildRows(new BusinessResultRawData
            {
                FixedCostReasons = new List<BusinessResultNamedAmount> { new BusinessResultNamedAmount { Name = "DP1" } },
                OtherCostReasons = new List<BusinessResultNamedAmount> { new BusinessResultNamedAmount { Name = "CK1" } }
            });
            BusinessResultRow zDt = zeroRows.Find(r => r.Loai == 0);
            BusinessResultRow zGg = zeroRows.Find(r => r.Loai == 1 && r.Content == "Giảm giá");
            BusinessResultRow zCp = zeroRows.Find(r => r.Loai == 2);
            BusinessResultRow zDp = zeroRows.Find(r => r.Loai == 3);
            BusinessResultRow zDp1 = zeroRows.Find(r => r.Loai == 4);
            BusinessResultRow zBp = zeroRows.Find(r => r.Loai == 5);
            BusinessResultRow zCk = zeroRows.Find(r => r.Loai == 7);
            BusinessResultRow zCk1 = zeroRows.Find(r => r.Loai == 8);
            BusinessResultRow zPl = zeroRows.Find(r => r.Loai == 9);
            bool zeroTableOk = zDt.PercentOfRevenue == "" && zDt.PercentMain == "100%" && zDt.PercentThird == ""
                && zGg.PercentOfRevenue == "" && zGg.PercentMain == "-" && zGg.PercentThird == ""
                && zCp.PercentOfRevenue == "-" && zCp.PercentMain == "" && zCp.PercentThird == "100%"
                && zDp.PercentOfRevenue == "-" && zDp.PercentMain == "-" && zDp.PercentThird == "100%"
                && zDp1.PercentOfRevenue == "-" && zDp1.PercentMain == "-" && zDp1.PercentThird == "-"
                && zBp.PercentOfRevenue == "-" && zBp.PercentMain == "-" && zBp.PercentThird == "100%"
                && zCk.PercentOfRevenue == "-" && zCk.PercentMain == "-" && zCk.PercentThird == "100%"
                && zCk1.PercentOfRevenue == "-" && zCk1.PercentMain == "-" && zCk1.PercentThird == "-"
                && zPl.PercentOfRevenue == "" && zPl.PercentMain == "-" && zPl.PercentThird == "-"
                && !zeroRows.Exists(r => r.Content == "Tiền giờ");
            Check("KQKD (§7/§2.1 ô '-' khi 0, ô '100%' cứng hiện cả khi 0, ô trống không hiện '-', không có Tiền giờ)",
                dashOk && zeroTableOk);
        }

        private static void CheckPrevRange(string label, DateTime from, DateTime to, DateTime expFrom, DateTime expTo)
        {
            BusinessResultPeriodRange got = BusinessResultCalculator.CalculatePreviousPeriod(from, to);
            Check("KQKD (§7/C2 kỳ trước " + label + ")",
                got.FromDate == expFrom && got.ToDate == expTo,
                got.FromDate.ToString("dd/MM/yyyy") + "–" + got.ToDate.ToString("dd/MM/yyyy"));
        }

        // ------------------------------------------------------------------ 2. Kiểm quyền & route menu

        private static void BusinessResultPermissionTests(string db)
        {
            var permsAllow = new FunctionPermissions(false);
            permsAllow.Add(BusinessResultConstants.FunctionId, BusinessResultConstants.FunctionName, RoleModes.View);
            var permsDeny = new FunctionPermissions(false);

            MenuModel menuAllow = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_kqkd_allow", UserName = "u_kqkd_allow", DisplayName = "Cho xem", IsAdmin = false,
                DatabasePath = db, Permissions = permsAllow
            });
            MenuModel menuDeny = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_kqkd_deny", UserName = "u_kqkd_deny", DisplayName = "Chặn", IsAdmin = false,
                DatabasePath = db, Permissions = permsDeny
            });
            Check("KQKD (§7/§2.0 phân quyền menu): hiện khi có quyền SFUNCTION 1866efc8 và ẩn khi không có quyền",
                MenuTreeContainsId(menuAllow.Roots, BusinessResultView.MenuId)
                && !MenuTreeContainsId(menuDeny.Roots, BusinessResultView.MenuId));

            bool roleMapOk = false;
            string roleDetail = "";
            using (var conn = KazukoConnection.Open(db))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT G.NAME, R.MODE FROM SGROUPROLE R " +
                    "INNER JOIN SGROUPUSER G ON G.ID = R.SGROUPUSERID " +
                    "WHERE R.SFUNCTIONID = @FnId",
                    "@FnId", BusinessResultConstants.FunctionId);
                int ql = -1, kt = -1, pv = -1;
                foreach (DataRow r in dt.Rows)
                {
                    string gname = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                    int mode = DbHelper.Int(r["MODE"]);
                    if (gname == "Quản lý") ql = mode;
                    else if (gname == "Kế toán") kt = mode;
                    else if (gname == "Phục vụ") pv = mode;
                }
                roleMapOk = (ql & RoleModes.View) != 0 && (kt & RoleModes.View) != 0 && pv == 0;
                roleDetail = "QL=" + ql + ", KT=" + kt + ", PV=" + pv;
            }
            Check("KQKD (§7/§2.0 mapping quyền DB SGROUPROLE): Quản lý/Kế toán có Xem, Phục vụ bị khóa (0)",
                roleMapOk, roleDetail);

            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "selftest",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };
            using (var routed = Dialogs.ScreenFactory.CreateView(
                new MenuNode { Id = BusinessResultView.MenuId, Title = BusinessResultConstants.FunctionName, Operation = 5 },
                adminSession))
            {
                Check("KQKD (ScreenFactory.CreateView OPERATION=5): định tuyến đúng sang BusinessResultView",
                    routed is BusinessResultView);
            }
        }

        // ------------------------------------------------------------------ 3. Seed & dọn dữ liệu mẫu

        private static void SeedBusinessResultFixture(string db, BusinessResultFixture fx)
        {
            using (var conn = KazukoConnection.Open(db))
            {
                DataTable dtFixed = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLYDOTHUCHI WHERE LOAILYDO = 1");
                fx.FixedReasonTotalCount = dtFixed.Rows.Count;
                if (dtFixed.Rows.Count > 0)
                {
                    fx.FixedReason1Id = DbHelper.Str(dtFixed.Rows[0]["ID"]);
                    fx.FixedReason1Name = DbHelper.Str(dtFixed.Rows[0]["NAME"]);
                }
                if (dtFixed.Rows.Count > 1)
                {
                    fx.FixedReason2Id = DbHelper.Str(dtFixed.Rows[1]["ID"]);
                    fx.FixedReason2Name = DbHelper.Str(dtFixed.Rows[1]["NAME"]);
                }
                if (dtFixed.Rows.Count > 2)
                    fx.FixedReason3Name = DbHelper.Str(dtFixed.Rows[2]["NAME"]);

                DataTable dtOther = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DLYDOTHUCHI WHERE LOAILYDO = 2");
                fx.OtherReasonTotalCount = dtOther.Rows.Count;
                if (dtOther.Rows.Count > 0)
                {
                    fx.OtherReason1Id = DbHelper.Str(dtOther.Rows[0]["ID"]);
                    fx.OtherReason1Name = DbHelper.Str(dtOther.Rows[0]["NAME"]);
                }
                if (dtOther.Rows.Count > 1)
                {
                    fx.OtherReason2Id = DbHelper.Str(dtOther.Rows[1]["ID"]);
                    fx.OtherReason2Name = DbHelper.Str(dtOther.Rows[1]["NAME"]);
                }

                DataTable dtIgnored = DbHelper.Table(conn, null,
                    "SELECT FIRST 1 ID FROM DLYDOTHUCHI WHERE COALESCE(LOAILYDO, 0) NOT IN (1, 2)");
                if (dtIgnored.Rows.Count > 0)
                {
                    fx.IgnoredReasonId = DbHelper.Str(dtIgnored.Rows[0]["ID"]);
                    fx.SeededIgnoredReason = false;
                }
            }

            BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.StoreAId, "@N", "KQKD-CH-A");
            BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.StoreBId, "@N", "KQKD-CH-B");

            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.User1StoreId, "@N", "KQKD User 1 CH", "@U", "kqkd_u1");
            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.User2StoresId, "@N", "KQKD User 2 CH", "@U", "kqkd_u2");
            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.User0StoreId, "@N", "KQKD User 0 CH", "@U", "kqkd_u0");

            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.LinkU1A, "@U", fx.User1StoreId, "@C", fx.StoreAId);
            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.LinkU2A, "@U", fx.User2StoresId, "@C", fx.StoreAId);
            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.LinkU2B, "@U", fx.User2StoresId, "@C", fx.StoreBId);

            BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0001', 30, 'selftest')", "@I", fx.GrpCoffeeId, "@N", "KQKD-01-Cà phê");
            BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0002', 30, 'selftest')", "@I", fx.GrpTeaId, "@N", "KQKD-02-Trà");
            BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '0', '', '0003', 30, 'selftest')", "@I", fx.GrpPrevOnlyId, "@N", "KQKD-03-Bánh cũ");
            BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '0', '', '0004', 30, 'selftest')", "@I", fx.GrpZeroId, "@N", "KQKD-04-Nhóm 0");

            BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-Ly', 30, 'selftest')", "@I", fx.UnitId);

            // Đặt DMATHANG.GIAVON khác hẳn TDONHANGCHITIET.GIAVON để chắc chắn biến phí lấy giá vốn trên dòng chi tiết.
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-CF', 'KQKD Cà phê', @G, @U, '0', 999999, 50000, 30, 'selftest')", "@I", fx.ProdCoffeeId, "@G", fx.GrpCoffeeId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-TR', 'KQKD Trà', @G, @U, '0', 888888, 40000, 30, 'selftest')", "@I", fx.ProdTeaId, "@G", fx.GrpTeaId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-BK', 'KQKD Bánh', @G, @U, '0', 777777, 30000, 30, 'selftest')", "@I", fx.ProdCakeId, "@G", fx.GrpPrevOnlyId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-Z0', 'KQKD Nhóm 0', @G, @U, '0', 666666, 20000, 30, 'selftest')", "@I", fx.ProdZeroId, "@G", fx.GrpZeroId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-NG', 'KQKD Chưa nhóm', NULL, @U, '0', 555555, 25000, 30, 'selftest')", "@I", fx.ProdUngroupedId, "@U", fx.UnitId);

            if (string.IsNullOrEmpty(fx.IgnoredReasonId))
            {
                fx.IgnoredReasonId = DbHelper.NewId();
                fx.SeededIgnoredReason = true;
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, 'KQKD-Lý do loại khác', 99, 30, 'selftest')", "@I", fx.IgnoredReasonId);
            }

            SeedBusinessResultTransactions(db, fx);
        }

        private static void SeedBusinessResultTransactions(string db, BusinessResultFixture fx)
        {
            Func<string, int, int, int, DateTime, string, decimal, decimal, decimal, decimal, string> addBill =
                delegate(string name, int loai, int paid, int status, DateTime ngay, string storeId,
                    decimal tongCong, decimal giamGiaTong, decimal phiDv, decimal thue)
                {
                    string id = DbHelper.NewId();
                    fx.OrderIds.Add(id);
                    BookingExec(db,
                        "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DCUAHANGID, " +
                        "TONGCONG, TIENGIAMGIATONG, PHIDICHVU, TIENTHUE, USERCREATEDID) " +
                        "VALUES (@ID, @N, @L, @P, @S, @NG, @CH, @TC, @GG, @PDV, @TH, 'selftest')",
                        "@ID", id, "@N", name, "@L", loai, "@P", paid, "@S", status, "@NG", ngay, "@CH", storeId,
                        "@TC", tongCong, "@GG", giamGiaTong, "@PDV", phiDv, "@TH", thue);
                    return id;
                };

            Action<string, string, decimal, decimal, decimal, decimal, int, string> addLine =
                delegate(string bid, string pid, decimal thanhTien, decimal slXuat, decimal slNhap, decimal giaVon, int xvt, string combo)
                {
                    string lid = DbHelper.NewId();
                    fx.LineIds.Add(lid);
                    BookingExec(db,
                        "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                        "THANHTIEN, SLXUAT, SLNHAP, GIAVON, XUATVATTU, COMBOPARENTID, STATUS, USERCREATEDID) " +
                        "VALUES (@ID, @B, @P, 'KQKD-Line', @U, @TT, @SX, @SN, @GV, @XVT, @CP, 30, 'selftest')",
                        "@ID", lid, "@B", bid, "@P", pid, "@U", fx.UnitId,
                        "@TT", thanhTien, "@SX", slXuat, "@SN", slNhap, "@GV", giaVon, "@XVT", xvt, "@CP", combo);
                };

            // Kỳ này (01/04–30/04/2005) — Store A:
            string curA1 = addBill("KQKD-C-A1", 0, 30, 30, new DateTime(2005, 4, 10, 9, 0, 0), fx.StoreAId, 1070000m, 50000m, 40000m, 80000m);
            addLine(curA1, fx.ProdCoffeeId, 600000m, 10m, 2m, 25000m, 0, "");
            addLine(curA1, fx.ProdCoffeeId, 110000m, 5m, 0m, 30000m, 30, "");
            addLine(curA1, fx.ProdTeaId, 90000m, 4m, 0m, 20000m, 0, "CB-PARENT");
            addLine(curA1, fx.ProdUngroupedId, 200000m, 5m, 1m, 15000m, 0, "");

            // Bill 15:30 ngày cuối kỳ này (30/04/2005 15:30) — phải tính!
            string curA2 = addBill("KQKD-C-A2", 0, 30, 30, new DateTime(2005, 4, 30, 15, 30, 0), fx.StoreAId, 315000m, 15000m, 10000m, 20000m);
            addLine(curA2, fx.ProdTeaId, 300000m, 6m, 0m, 18000m, 0, "");

            // Bill LOAI 5 và LOAI 6 (vào DOANH THU + biến phí, KHÔNG vào nhóm doanh thu):
            string curA5 = addBill("KQKD-C-A5", 5, 0, 30, new DateTime(2005, 4, 15, 11, 0, 0), fx.StoreAId, 250000m, 5000m, 3000m, 2000m);
            addLine(curA5, fx.ProdCoffeeId, 250000m, 2m, 0m, 40000m, 0, "");
            string curA6 = addBill("KQKD-C-A6", 6, 0, 30, new DateTime(2005, 4, 16, 12, 0, 0), fx.StoreAId, 165000m, 0m, 7000m, 8000m);
            addLine(curA6, fx.ProdTeaId, 165000m, 3m, 0m, 22000m, 0, "");

            // Các bill bị loại: chưa thanh toán, STATUS=0, STATUS=-30, LOAI=1, ngoài khoảng ngày.
            string badUnpaid = addBill("KQKD-BAD-UNP", 0, 0, 30, new DateTime(2005, 4, 11, 10, 0, 0), fx.StoreAId, 999000m, 99000m, 99000m, 99000m);
            addLine(badUnpaid, fx.ProdZeroId, 999000m, 10m, 0m, 50000m, 0, "");
            string badSt0 = addBill("KQKD-BAD-ST0", 0, 30, 0, new DateTime(2005, 4, 11, 11, 0, 0), fx.StoreAId, 888000m, 88000m, 88000m, 88000m);
            addLine(badSt0, fx.ProdCoffeeId, 888000m, 10m, 0m, 50000m, 0, "");
            string badStDel = addBill("KQKD-BAD-DEL", 5, 30, -30, new DateTime(2005, 4, 11, 12, 0, 0), fx.StoreAId, 777000m, 77000m, 77000m, 77000m);
            addLine(badStDel, fx.ProdTeaId, 777000m, 10m, 0m, 50000m, 0, "");
            string badL1 = addBill("KQKD-BAD-L1", 1, 30, 30, new DateTime(2005, 4, 11, 13, 0, 0), fx.StoreAId, 666000m, 66000m, 66000m, 66000m);
            addLine(badL1, fx.ProdCoffeeId, 666000m, 10m, 0m, 50000m, 0, "");
            addBill("KQKD-BAD-AFTER", 0, 30, 30, new DateTime(2005, 5, 1, 0, 0, 0), fx.StoreAId, 555000m, 55000m, 55000m, 55000m);

            // Kỳ này — Store B:
            string curB1 = addBill("KQKD-C-B1", 0, 30, 30, new DateTime(2005, 4, 12, 14, 0, 0), fx.StoreBId, 400000m, 20000m, 15000m, 25000m);
            addLine(curB1, fx.ProdCoffeeId, 380000m, 5m, 0m, 20000m, 0, "");

            // Kỳ trước (01/03–31/03/2005) — Store A (có nhóm chỉ xuất hiện ở kỳ trước & bill 31/03 15:30 giá 2.000.000):
            string prevA1 = addBill("KQKD-P-A1", 0, 30, 30, new DateTime(2005, 3, 10, 10, 0, 0), fx.StoreAId, 500000m, 25000m, 12000m, 33000m);
            addLine(prevA1, fx.ProdCoffeeId, 350000m, 5m, 0m, 24000m, 0, "");
            addLine(prevA1, fx.ProdCakeId, 150000m, 3m, 0m, 10000m, 0, "");
            string prevA2 = addBill("KQKD-P-A2-31MAR", 0, 30, 30, new DateTime(2005, 3, 31, 15, 30, 0), fx.StoreAId, 2000000m, 0m, 0m, 0m);
            addLine(prevA2, fx.ProdTeaId, 2000000m, 10m, 0m, 50000m, 0, "");

            // Kỳ trước — Store B:
            string prevB1 = addBill("KQKD-P-B1", 0, 30, 30, new DateTime(2005, 3, 15, 11, 0, 0), fx.StoreBId, 210000m, 10000m, 5000m, 15000m);
            addLine(prevB1, fx.ProdUngroupedId, 200000m, 2m, 0m, 35000m, 0, "");

            // Ca C3 hai kỳ trùng ngày (xem 01/10–25/11/2006, 1 bill 10/10/2006 bán 300.000 giá vốn 100.000):
            string c3Bill = addBill("KQKD-C3-BILL", 0, 30, 30, new DateTime(2006, 10, 10, 10, 0, 0), fx.StoreAId, 300000m, 0m, 0m, 0m);
            addLine(c3Bill, fx.ProdCoffeeId, 300000m, 2m, 0m, 50000m, 0, "");

            // Phiếu chi TTHUCHI: kiểm không lọc STATUS/LOAI/LATAMUNG, tính 15:30 ngày cuối, lọc cửa hàng, bỏ lý do LOAILYDO != 1/2.
            Action<string, DateTime, string, string, decimal, int, int> addChi =
                delegate(string name, DateTime ngay, string storeId, string reasonId, decimal chi, int status, int tamUng)
                {
                    string vid = DbHelper.NewId();
                    fx.VoucherIds.Add(vid);
                    BookingExec(db,
                        "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, CHI, THU, DCUAHANGID, DLYDOTHUCHIID, STATUS, LATAMUNG, USERCREATEDID) " +
                        "VALUES (@ID, @N, @NG, @NG, @CHI, 0, @CH, @LD, @ST, @TU, 'selftest')",
                        "@ID", vid, "@N", name, "@NG", ngay, "@CHI", chi, "@CH", storeId, "@LD", reasonId, "@ST", status, "@TU", tamUng);
                };

            if (!string.IsNullOrEmpty(fx.FixedReason1Id))
            {
                addChi("KQKD-PC-FA1", new DateTime(2005, 4, 5, 8, 0, 0), fx.StoreAId, fx.FixedReason1Id, 300000m, 30, 0);
                addChi("KQKD-PC-FA2", new DateTime(2005, 4, 30, 15, 30, 0), fx.StoreAId, fx.FixedReason1Id, 50000m, 0, 30);
                addChi("KQKD-PC-FB1", new DateTime(2005, 4, 12, 10, 0, 0), fx.StoreBId, fx.FixedReason1Id, 150000m, 30, 0);
                addChi("KQKD-PC-PFA1", new DateTime(2005, 3, 15, 10, 0, 0), fx.StoreAId, fx.FixedReason1Id, 400000m, 30, 0);
                addChi("KQKD-PC-PFB1", new DateTime(2005, 3, 20, 10, 0, 0), fx.StoreBId, fx.FixedReason1Id, 100000m, 30, 0);
            }
            if (!string.IsNullOrEmpty(fx.FixedReason2Id))
            {
                // Lý do định phí thứ 2: kỳ này 250k (A 180k + B 70k), kỳ trước 120k (A 90k + B 30k) → tăng +130k (▲).
                addChi("KQKD-PC-F2A1", new DateTime(2005, 4, 8, 9, 0, 0), fx.StoreAId, fx.FixedReason2Id, 180000m, 30, 0);
                addChi("KQKD-PC-F2B1", new DateTime(2005, 4, 14, 10, 0, 0), fx.StoreBId, fx.FixedReason2Id, 70000m, 30, 0);
                addChi("KQKD-PC-PF2A1", new DateTime(2005, 3, 12, 9, 0, 0), fx.StoreAId, fx.FixedReason2Id, 90000m, 30, 0);
                addChi("KQKD-PC-PF2B1", new DateTime(2005, 3, 18, 10, 0, 0), fx.StoreBId, fx.FixedReason2Id, 30000m, 30, 0);
            }
            if (!string.IsNullOrEmpty(fx.OtherReason1Id))
            {
                addChi("KQKD-PC-OA1", new DateTime(2005, 4, 20, 9, 0, 0), fx.StoreAId, fx.OtherReason1Id, 120000m, -30, 0);
                addChi("KQKD-PC-OB1", new DateTime(2005, 4, 12, 11, 0, 0), fx.StoreBId, fx.OtherReason1Id, 80000m, 30, 0);
                addChi("KQKD-PC-POA1", new DateTime(2005, 3, 31, 15, 30, 0), fx.StoreAId, fx.OtherReason1Id, 90000m, 30, 0);
                addChi("KQKD-PC-POB1", new DateTime(2005, 3, 20, 11, 0, 0), fx.StoreBId, fx.OtherReason1Id, 40000m, 30, 0);
            }
            if (!string.IsNullOrEmpty(fx.OtherReason2Id))
            {
                // Lý do chi khác thứ 2: kỳ này 80k (A 50k + B 30k), kỳ trước 160k (A 110k + B 50k) → giảm -80k (▼).
                addChi("KQKD-PC-O2A1", new DateTime(2005, 4, 22, 9, 0, 0), fx.StoreAId, fx.OtherReason2Id, 50000m, 30, 0);
                addChi("KQKD-PC-O2B1", new DateTime(2005, 4, 24, 11, 0, 0), fx.StoreBId, fx.OtherReason2Id, 30000m, 30, 0);
                addChi("KQKD-PC-PO2A1", new DateTime(2005, 3, 25, 9, 0, 0), fx.StoreAId, fx.OtherReason2Id, 110000m, 30, 0);
                addChi("KQKD-PC-PO2B1", new DateTime(2005, 3, 26, 11, 0, 0), fx.StoreBId, fx.OtherReason2Id, 50000m, 30, 0);
            }
            addChi("KQKD-PC-IGN", new DateTime(2005, 4, 10, 9, 0, 0), fx.StoreAId, fx.IgnoredReasonId, 444000m, 30, 0);
        }

        private static void CleanupBusinessResultFixture(string db, BusinessResultFixture fx)
        {
            foreach (string lid in fx.LineIds)
                BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
            foreach (string oid in fx.OrderIds)
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", oid);
            foreach (string vid in fx.VoucherIds)
                BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
            if (fx.SeededIgnoredReason && !string.IsNullOrEmpty(fx.IgnoredReasonId))
                BookingExec(db, "DELETE FROM DLYDOTHUCHI WHERE ID = @I", "@I", fx.IgnoredReasonId);
            BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B, @C, @D, @E)",
                "@A", fx.ProdCoffeeId, "@B", fx.ProdTeaId, "@C", fx.ProdCakeId, "@D", fx.ProdZeroId, "@E", fx.ProdUngroupedId);
            BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", fx.UnitId);
            BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID IN (@A, @B, @C, @D)",
                "@A", fx.GrpCoffeeId, "@B", fx.GrpTeaId, "@C", fx.GrpPrevOnlyId, "@D", fx.GrpZeroId);
            BookingExec(db, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE ID IN (@A, @B, @C)",
                "@A", fx.LinkU1A, "@B", fx.LinkU2A, "@C", fx.LinkU2B);
            BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B, @C)",
                "@A", fx.User1StoreId, "@B", fx.User2StoresId, "@C", fx.User0StoreId);
            BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)",
                "@A", fx.StoreAId, "@B", fx.StoreBId);
        }
    }
}
