using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class DailyActivityFixture
        {
            public string StoreAId = DbHelper.NewId();
            public string StoreBId = DbHelper.NewId();
            public string WhAId = DbHelper.NewId();
            public string WhBId = DbHelper.NewId();
            public string Cashier1Id = DbHelper.NewId();
            public string Cashier2Id = DbHelper.NewId();
            public string UserNoStoreId = DbHelper.NewId();
            public string Link1AId = DbHelper.NewId();
            public string Link2AId = DbHelper.NewId();
            public string Link2BId = DbHelper.NewId();
            public string CustAId = DbHelper.NewId();
            public string SuppAId = DbHelper.NewId();
            public string UnitId = DbHelper.NewId();
            /// <summary>Đơn vị tính thứ 2 CÙNG TÊN "CTHD-Ly" nhưng khác ID để kiểm gộp theo DDONVITINHID.</summary>
            public string Unit2Id = DbHelper.NewId();
            public string ProdCoffeeId = DbHelper.NewId();
            /// <summary>Mặt hàng thứ 2 khác CODE ("CTHD-CF2") nhưng dòng chi tiết cùng TENHANG "CTHD-Cà phê".</summary>
            public string ProdCoffee2Id = DbHelper.NewId();
            public string ProdTeaId = DbHelper.NewId();
            public string ProdMatId = DbHelper.NewId();

            public string Bill1Id = DbHelper.NewId();
            public string Bill2Id = DbHelper.NewId();
            public string Bill3Id = DbHelper.NewId();
            public List<string> ExtraOrderIds = new List<string>();
            public List<string> LineIds = new List<string>();
            public List<string> VoucherIds = new List<string>();

            public string Pur1Id = DbHelper.NewId();
            public string Pur2Id = DbHelper.NewId();
            public string Pur3Id = DbHelper.NewId();
            public string Pur4Id = DbHelper.NewId();
            public string Pur5Id = DbHelper.NewId();
            public string PurOutId = DbHelper.NewId();

            public DateTime From = new DateTime(2004, 7, 10);
            public DateTime To = new DateTime(2004, 7, 11);
        }

        private static void DailyActivityTests(string db)
        {
            DailyActivitySummaryUnitTests();
            DailyActivityPermissionTests(db);

            var fx = new DailyActivityFixture();
            Theme.ThemeTokens savedTheme = Theme.ThemeManager.Current;
            try
            {
                SeedDailyActivityFixture(db, fx);
                DailyActivityRepositoryTests(db, fx);
                DailyActivityUiTests(db, fx);
                DailyActivityLayoutAndScrollTests(db, fx);
            }
            finally
            {
                Theme.ThemeManager.Apply(savedTheme);
                CleanupDailyActivityFixture(db, fx);
            }
        }

        // ------------------------------------------------------------------ 1. Kiểm công thức thuần (Services)

        private static void DailyActivitySummaryUnitTests()
        {
            // Ca 1–4: Còn nợ phiếu nhập P1/Q1 so với công thức gốc (§2.4, §3-1, Q1).
            decimal debtNormalNew = DailyActivitySummary.CalculatePurchaseRemainingDebt(100000m, 40000m);
            decimal debtNormalLegacy = DailyActivitySummary.CalculatePurchaseRemainingDebtLegacy(100000m, 40000m, 0m);
            Check("Chi tiết HĐ ngày (§7/Q1 còn nợ phiếu nhập thường: bản mới=60.000, gốc=60.000)",
                debtNormalNew == 60000m && debtNormalLegacy == 60000m);

            decimal debtPaidNew = DailyActivitySummary.CalculatePurchaseRemainingDebt(100000m, 100000m);
            decimal debtPaidLegacy = DailyActivitySummary.CalculatePurchaseRemainingDebtLegacy(100000m, 100000m, 0m);
            Check("Chi tiết HĐ ngày (§7/Q1 còn nợ phiếu nhập trả đủ: bản mới=0, gốc=0)",
                debtPaidNew == 0m && debtPaidLegacy == 0m);

            // Trả dư: công thức gốc ra âm (-25.000), bản mới P1/Q1 ra 0.
            decimal debtOverNew = DailyActivitySummary.CalculatePurchaseRemainingDebt(100000m, 125000m);
            decimal debtOverLegacy = DailyActivitySummary.CalculatePurchaseRemainingDebtLegacy(100000m, 125000m, 0m);
            Check("Chi tiết HĐ ngày (§7/P1/Q1 phiếu nhập trả dư: gốc=-25.000, bản mới=0)",
                debtOverNew == 0m && debtOverLegacy == -25000m,
                "new=" + debtOverNew + ", legacy=" + debtOverLegacy);

            // Lỗi so sai cột LOAITHANHTOAN của gốc (§3-1): gốc ra 0 dù còn nợ 40.
            decimal debtBugNew = DailyActivitySummary.CalculatePurchaseRemainingDebt(50m, 10m);
            decimal debtBugLegacy = DailyActivitySummary.CalculatePurchaseRemainingDebtLegacy(50m, 10m, 50m);
            Check("Chi tiết HĐ ngày (§7/P1/§3-1 lỗi LOAITHANHTOAN>=TONGCONG: gốc=0, bản mới=40)",
                debtBugNew == 40m && debtBugLegacy == 0m);

            // Ca 5: Tách hình thức trả phiếu nhập Q12 (lấy TIENTHANHTOAN, không lấy cột CHUYENKHOAN).
            var purCash = new DailyPurchaseRow { TongCong = 120000m, TienThanhToan = 50000m, ChuyenKhoan = 0m };
            var purTransfer = new DailyPurchaseRow { TongCong = 90000m, TienThanhToan = 30000m, ChuyenKhoan = 1m };
            var purLegacy = new DailyPurchaseRow { TongCong = 45000m, TienThanhToan = 17000m, ChuyenKhoan = 0m };
            var purOver = new DailyPurchaseRow { TongCong = 30000m, TienThanhToan = 40000m, ChuyenKhoan = 0m };
            DailyPurchaseTotals pt = DailyActivitySummary.CalculatePurchaseTotals(new[] { purCash, purTransfer, purLegacy, purOver });
            Check("Chi tiết HĐ ngày (§7/Q12 tổng nhập & tách quỹ theo TIENTHANHTOAN): TM=107.000, CK=30.000, nợ P1=158.000",
                pt.TongCong == 285000m && pt.TienThanhToan == 137000m
                && pt.PaidCash == 107000m && pt.PaidTransfer == 30000m
                && pt.RemainingDebt == 158000m);

            // Ca 6: 4 dòng tổng Khối 2 (§2.5).
            var items = new[]
            {
                new DailySoldItemRow { Quantity = 5m, UnitPrice = 40000m, DiscountRate = 10m, Amount = 180000m },
                new DailySoldItemRow { Quantity = 1.5m, UnitPrice = 20000m, DiscountRate = 0m, Amount = 30000m }
            };
            DailySoldItemTotals it = DailyActivitySummary.CalculateItemTotals(items, 12000m);
            Check("Chi tiết HĐ ngày (§7/§2.5 4 dòng tổng khối Mặt hàng bán): SL=6.5, chưa GG=230k, GG MH=20k, GG bill=12k, tổng=198k",
                it.TotalQuantity == 6.5m && it.GrossAmount == 230000m
                && it.ItemDiscount == 20000m && it.BillDiscount == 12000m && it.NetTotal == 198000m);

            // Ca 7: 8 dòng tổng hợp §2.7 khớp số tính tay (8 số khác 0, đôi một khác nhau, DATTRUOC không ảnh hưởng).
            var bills = new[]
            {
                new DailySaleBillRow
                {
                    TongCong = 507000m, TienMat = 200000m, ChuyenKhoan = 110000m, The = 70000m,
                    Voucher = 35000m, TruTichLuy = 25000m, TheTraTruoc = 40000m, CongNo = 27000m, DatTruoc = 95000m
                },
                new DailySaleBillRow
                {
                    TongCong = 300000m, TienMat = 150000m, ChuyenKhoan = 80000m, The = 30000m,
                    Voucher = 10000m, TruTichLuy = 5000m, TheTraTruoc = 15000m, CongNo = 10000m, DatTruoc = 55000m
                }
            };
            var cashFlows = new[]
            {
                new DailyCashFlowRow { CashIn = 85000m, CashOut = 22000m, TransferIn = 65000m, TransferOut = 19000m }
            };
            DailyActivitySummaryResult res = DailyActivitySummary.Calculate(bills, items, 12000m, new[] { purCash, purTransfer, purLegacy, purOver }, cashFlows);
            Check("Chi tiết HĐ ngày (§7/§2.7 8 dòng tổng hợp thuần khớp số tính tay, bỏ qua DATTRUOC)",
                res.Overall.Cash == 306000m
                && res.Overall.BankTransfer == 206000m
                && res.Overall.Card == 100000m
                && res.Overall.Voucher == 45000m
                && res.Overall.PrepaidCard == 55000m
                && res.Overall.LoyaltyDeduction == 30000m
                && res.Overall.CustomerDebt == 37000m
                && res.Overall.SupplierDebt == 158000m);

            // Ca 8: Định dạng 8 dòng tổng hợp (§2.7): 2 dòng đầu luôn hiện số kể cả 0, 6 dòng sau trống khi 0.
            bool fmtZeroOk = DailyActivitySummary.FormatOverallValue(0, 0m) == "0"
                && DailyActivitySummary.FormatOverallValue(1, 0m) == "0"
                && DailyActivitySummary.FormatOverallValue(2, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(3, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(4, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(5, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(6, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(7, 0m) == ""
                && DailyActivitySummary.FormatOverallValue(2, 113000m) == 113000m.ToString("n0", CultureInfo.GetCultureInfo("vi-VN"));
            Check("Chi tiết HĐ ngày (§7/§2.7 định dạng 8 dòng tổng hợp): 2 dòng đầu hiện 0, 6 dòng sau trống khi bằng 0", fmtZeroOk);
        }

        // ------------------------------------------------------------------ 2. Kiểm quyền & route menu

        private static void DailyActivityPermissionTests(string db)
        {
            var permsAllow = new FunctionPermissions(false);
            permsAllow.Add(DailyActivityConstants.FunctionId, DailyActivityConstants.FunctionName, RoleModes.View);
            var permsDeny = new FunctionPermissions(false);

            MenuModel menuAllow = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_cthd_allow", UserName = "u_cthd_allow", DisplayName = "Cho xem", IsAdmin = false,
                DatabasePath = db, Permissions = permsAllow
            });
            MenuModel menuDeny = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_cthd_deny", UserName = "u_cthd_deny", DisplayName = "Chặn", IsAdmin = false,
                DatabasePath = db, Permissions = permsDeny
            });
            Check("Chi tiết HĐ ngày (§7/§2.6 phân quyền menu): hiện khi có quyền SFUNCTION 5d90c04e và ẩn khi không có quyền",
                MenuTreeContainsId(menuAllow.Roots, DailyActivityView.MenuId)
                && !MenuTreeContainsId(menuDeny.Roots, DailyActivityView.MenuId));

            bool roleMapOk = false;
            string roleDetail = "";
            using (var conn = KazukoConnection.Open(db))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT G.NAME, R.MODE FROM SGROUPROLE R " +
                    "INNER JOIN SGROUPUSER G ON G.ID = R.SGROUPUSERID " +
                    "WHERE R.SFUNCTIONID = @FnId",
                    "@FnId", DailyActivityConstants.FunctionId);
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
            Check("Chi tiết HĐ ngày (§7/§2.6 mapping quyền DB SGROUPROLE): Quản lý/Kế toán có Xem, Phục vụ bị khóa (0)",
                roleMapOk, roleDetail);

            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "selftest",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };
            using (var routed = Dialogs.ScreenFactory.CreateView(
                new MenuNode { Id = DailyActivityView.MenuId, Title = DailyActivityConstants.FunctionName, Operation = 5 },
                adminSession))
            {
                Check("Chi tiết HĐ ngày (ScreenFactory.CreateView OPERATION=5): định tuyến đúng sang DailyActivityView",
                    routed is DailyActivityView);
            }
        }

        // ------------------------------------------------------------------ 3. Seed & dọn dữ liệu mẫu

        private static void SeedDailyActivityFixture(string db, DailyActivityFixture fx)
        {
            BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.StoreAId, "@N", "CTHD-CH-A");
            BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.StoreBId, "@N", "CTHD-CH-B");
            BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @C, 30, 'selftest')", "@I", fx.WhAId, "@N", "CTHD-Kho-A", "@C", fx.StoreAId);
            BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @C, 30, 'selftest')", "@I", fx.WhBId, "@N", "CTHD-Kho-B", "@C", fx.StoreBId);

            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.Cashier1Id, "@N", "CTHD Thu ngân 1", "@U", "cthd_tn1");
            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.Cashier2Id, "@N", "CTHD Thu ngân 2", "@U", "cthd_tn2");
            BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", fx.UserNoStoreId, "@N", "CTHD Không CH", "@U", "cthd_nostore");

            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.Link1AId, "@U", fx.Cashier1Id, "@C", fx.StoreAId);
            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.Link2AId, "@U", fx.Cashier2Id, "@C", fx.StoreAId);
            BookingExec(db, "INSERT INTO TNGUOIDUNGTHEOCUAHANG (ID, SUSERID, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @U, @C, 30, 'selftest')", "@I", fx.Link2BId, "@U", fx.Cashier2Id, "@C", fx.StoreBId);

            BookingExec(db, "INSERT INTO DKHACHHANG (ID, NAME, MAKHACH, STATUS, USERCREATEDID) VALUES (@I, @N, 'CTHD-KH01', 30, 'selftest')", "@I", fx.CustAId, "@N", "CTHD Khách A");
            BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.SuppAId, "@N", "CTHD NCC A");
            BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.UnitId, "@N", "CTHD-Ly");
            BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", fx.Unit2Id, "@N", "CTHD-Ly");

            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'CTHD-CF', 'CTHD-Cà phê', @U, '0', 40000, 30, 'selftest')", "@I", fx.ProdCoffeeId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'CTHD-CF2', 'CTHD-Cà phê bản 2', @U, '0', 40000, 30, 'selftest')", "@I", fx.ProdCoffee2Id, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'CTHD-TR', 'CTHD-Trà sữa', @U, '0', 35000, 30, 'selftest')", "@I", fx.ProdTeaId, "@U", fx.UnitId);
            BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'CTHD-NL', 'CTHD-Mật ong NL', @U, '2', 10000, 30, 'selftest')", "@I", fx.ProdMatId, "@U", fx.UnitId);

            // 3 bill bán hợp lệ (Bill1 đủ 8 cột tiền khác 0, đôi một khác nhau; Bill2 giờ lẻ ngày cuối; Bill3 Store B).
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                "VALUES (@ID, 'CTHD-HD01', 0, 30, 30, @NG, @GTT, @CH, @KH, @UTT, @UCR, 15000, 507000, 200000, 110000, 70000, 35000, 25000, 40000, 27000, 95000)",
                "@ID", fx.Bill1Id, "@NG", new DateTime(2004, 7, 10, 9, 0, 0), "@GTT", new DateTime(2004, 7, 10, 9, 15, 0),
                "@CH", fx.StoreAId, "@KH", fx.CustAId, "@UTT", fx.Cashier1Id, "@UCR", fx.Cashier2Id);

            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                "VALUES (@ID, 'CTHD-HD02', 0, 30, 30, @NG, @GTT, @CH, NULL, @UTT, @UCR, 10000, 300000, 150000, 80000, 30000, 10000, 5000, 15000, 10000, 55000)",
                "@ID", fx.Bill2Id, "@NG", new DateTime(2004, 7, 11, 16, 45, 0), "@GTT", new DateTime(2004, 7, 11, 16, 50, 0),
                "@CH", fx.StoreAId, "@UTT", fx.Cashier2Id, "@UCR", fx.Cashier1Id);

            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                "VALUES (@ID, 'CTHD-HD00', 0, 30, 30, @NG, @GTT, @CH, NULL, @UTT, @UCR, 5000, 120000, 60000, 40000, 13000, 7000, 0, 0, 0, 0)",
                "@ID", fx.Bill3Id, "@NG", new DateTime(2004, 7, 10, 9, 0, 0), "@GTT", new DateTime(2004, 7, 10, 9, 5, 0),
                "@CH", fx.StoreBId, "@UTT", fx.Cashier1Id, "@UCR", fx.Cashier1Id);

            Action<string, int, int, DateTime> addBadBill = delegate(string name, int paid, int status, DateTime ngay)
            {
                string id = DbHelper.NewId();
                fx.ExtraOrderIds.Add(id);
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DCUAHANGID, USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT) " +
                    "VALUES (@ID, @N, 0, @P, @S, @NG, @CH, @UTT, 'selftest', 99000, 999000, 999000)",
                    "@ID", id, "@N", name, "@P", paid, "@S", status, "@NG", ngay, "@CH", fx.StoreAId, "@UTT", fx.Cashier1Id);
            };
            addBadBill("CTHD-UNPAID", 0, 30, new DateTime(2004, 7, 10, 10, 0, 0));
            addBadBill("CTHD-ST0", 30, 0, new DateTime(2004, 7, 10, 10, 15, 0));
            addBadBill("CTHD-STDEL", 30, -30, new DateTime(2004, 7, 10, 10, 30, 0));
            addBadBill("CTHD-BEFORE", 30, 30, new DateTime(2004, 7, 9, 23, 59, 0));
            addBadBill("CTHD-AFTER", 30, 30, new DateTime(2004, 7, 12, 0, 0, 0));

            // Chi tiết mặt hàng bán: kiểm đủ mọi khóa GROUP BY (TENHANG, CODE, DDONVITINHID cùng tên khác ID, DONGIA, TILEGIAMGIA),
            // gộp trùng khóa, trừ hàng trả, giữ nguyên liệu, loại combo con & XVT.
            Action<string, string, string, string, decimal, decimal, decimal, decimal, decimal, int, string> addLine =
                delegate(string bid, string pid, string ten, string unitId, decimal slXuat, decimal slNhap, decimal dg, decimal ck, decimal tt, int xvt, string combo)
                {
                    string lid = DbHelper.NewId();
                    fx.LineIds.Add(lid);
                    BookingExec(db,
                        "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                        "SLXUATCHUAQUYDOI, SLNHAPCHUAQUYDOI, DONGIA, TILEGIAMGIA, THANHTIEN, XUATVATTU, COMBOPARENTID, STATUS, USERCREATEDID) " +
                        "VALUES (@ID, @B, @P, @T, @U, @SX, @SN, @DG, @CK, @TT, @XVT, @CP, 30, 'selftest')",
                        "@ID", lid, "@B", bid, "@P", pid, "@T", ten, "@U", unitId,
                        "@SX", slXuat, "@SN", slNhap, "@DG", dg, "@CK", ck, "@TT", tt, "@XVT", xvt, "@CP", combo);
                };
            // Nhóm gốc (CTHD-Cà phê, CTHD-CF, UnitId, 40k, 10%): 2 dòng trên Bill1 (trả 1) + 1 dòng trên Bill2 → gộp SL=5, TT=180k.
            addLine(fx.Bill1Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.UnitId, 3m, 1m, 40000m, 10m, 72000m, 0, "");
            addLine(fx.Bill1Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.UnitId, 2m, 0m, 40000m, 10m, 72000m, 0, "");
            addLine(fx.Bill2Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.UnitId, 1m, 0m, 40000m, 10m, 36000m, 0, "");
            // Chỉ khác DDONVITINHID (Unit2Id cùng tên "CTHD-Ly"): SL=1, TT=36k.
            addLine(fx.Bill1Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.Unit2Id, 1m, 0m, 40000m, 10m, 36000m, 0, "");
            // Chỉ khác TILEGIAMGIA (20% thay vì 10%): SL=1, TT=32k.
            addLine(fx.Bill1Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.UnitId, 1m, 0m, 40000m, 20m, 32000m, 0, "");
            // Chỉ khác DMATHANG.CODE (ProdCoffee2Id CODE="CTHD-CF2", cùng TENHANG="CTHD-Cà phê"): SL=1, TT=36k.
            addLine(fx.Bill1Id, fx.ProdCoffee2Id, "CTHD-Cà phê", fx.UnitId, 1m, 0m, 40000m, 10m, 36000m, 0, "");
            // Khác DONGIA (50k): SL=1, TT=50k.
            addLine(fx.Bill1Id, fx.ProdCoffeeId, "CTHD-Cà phê", fx.UnitId, 1m, 0m, 50000m, 0m, 50000m, 0, "");
            // Nguyên liệu (giữ), combo con & XVT (loại), Trà sữa ở Store B.
            addLine(fx.Bill1Id, fx.ProdMatId, "CTHD-Mật ong NL", fx.UnitId, 2m, 0m, 10000m, 0m, 20000m, 0, "");
            addLine(fx.Bill1Id, fx.ProdTeaId, "CTHD-ComboCon", fx.UnitId, 5m, 0m, 30000m, 0m, 150000m, 0, "combo-parent-cthd");
            addLine(fx.Bill1Id, fx.ProdTeaId, "CTHD-XuatVT", fx.UnitId, 5m, 0m, 30000m, 0m, 150000m, 30, "");
            addLine(fx.Bill3Id, fx.ProdTeaId, "CTHD-Trà sữa", fx.UnitId, 2m, 0m, 35000m, 0m, 70000m, 0, "");

            // 5 phiếu nhập hợp lệ + 1 phiếu nhập ngoài khoảng.
            Action<string, string, DateTime, string, string, int, decimal, decimal, decimal?, decimal?> addPur =
                delegate(string id, string name, DateTime ngay, string whId, string creatorId, int status, decimal tong, decimal tt, decimal? tm, decimal? ck)
                {
                    BookingExec(db,
                        "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DKHONHAPID, DNHACUNGCAPID, USERCREATEDID, " +
                        "TONGCONG, TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, LOAITHANHTOAN) " +
                        "VALUES (@ID, @N, 1, 0, @ST, @NG, @KH, @NCC, @UCR, @TC, @TT, @TM, @CK, 0)",
                        "@ID", id, "@N", name, "@ST", status, "@NG", ngay, "@KH", whId, "@NCC", fx.SuppAId,
                        "@UCR", creatorId, "@TC", tong, "@TT", tt, "@TM", tm, "@CK", ck);
                };
            addPur(fx.Pur1Id, "CTHD-PN01", new DateTime(2004, 7, 10, 11, 0, 0), fx.WhAId, fx.Cashier2Id, 30, 120000m, 50000m, 50000m, 0m);
            addPur(fx.Pur2Id, "CTHD-PN02", new DateTime(2004, 7, 11, 18, 20, 0), fx.WhAId, fx.Cashier1Id, -30, 90000m, 30000m, 0m, 30000m);
            addPur(fx.Pur3Id, "CTHD-PN03", new DateTime(2004, 7, 10, 13, 0, 0), fx.WhBId, fx.Cashier1Id, 0, 45000m, 17000m, null, null);
            addPur(fx.Pur4Id, "CTHD-PN04", new DateTime(2004, 7, 10, 14, 0, 0), fx.WhAId, fx.Cashier1Id, 30, 30000m, 40000m, 40000m, 0m);
            addPur(fx.Pur5Id, "CTHD-PN05-NULLKHO", new DateTime(2004, 7, 10, 15, 0, 0), null, fx.Cashier2Id, 30, 25000m, 11000m, 11000m, 0m);
            addPur(fx.PurOutId, "CTHD-PN-OUT", new DateTime(2004, 7, 12, 0, 0, 0), fx.WhAId, fx.Cashier1Id, 30, 888000m, 888000m, 888000m, 0m);

            // 4 phiếu thu-chi hợp lệ + 1 ngoài khoảng.
            Action<string, DateTime, string, string, decimal?, decimal?, int?, int, int> addTc =
                delegate(string name, DateTime ngay, string storeId, string creatorId, decimal? thu, decimal? chi, int? ck, int tamUng, int status)
                {
                    string vid = DbHelper.NewId();
                    fx.VoucherIds.Add(vid);
                    BookingExec(db,
                        "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, DIENGIAI, TENDOITUONG, THU, CHI, CHUYENKHOAN, LATAMUNG, DCUAHANGID, STATUS, USERCREATEDID) " +
                        "VALUES (@ID, @N, @NG, @NG, @DG, @DT, @THU, @CHI, @CK, @TU, @CH, @ST, @UCR)",
                        "@ID", vid, "@N", name, "@NG", ngay, "@DG", "Diễn giải " + name, "@DT", "Đối tượng " + name,
                        "@THU", thu, "@CHI", chi, "@CK", ck, "@TU", tamUng, "@CH", storeId, "@ST", status, "@UCR", creatorId);
                };
            addTc("CTHD-PT01", new DateTime(2004, 7, 10, 8, 30, 0), fx.StoreAId, fx.Cashier2Id, 85000m, 0m, null, 30, 0);
            addTc("CTHD-PC01", new DateTime(2004, 7, 11, 19, 10, 0), fx.StoreAId, fx.Cashier1Id, 0m, 22000m, 0, 0, 30);
            addTc("CTHD-PT02", new DateTime(2004, 7, 10, 12, 0, 0), fx.StoreAId, fx.Cashier1Id, 65000m, 0m, 1, 0, 30);
            addTc("CTHD-PC02", new DateTime(2004, 7, 10, 16, 0, 0), fx.StoreBId, fx.Cashier2Id, 0m, 19000m, 30, 0, -30);
            addTc("CTHD-TC-OUT", new DateTime(2004, 7, 12, 0, 0, 0), fx.StoreAId, fx.Cashier1Id, 777000m, 0m, 0, 0, 30);
        }

        private static void CleanupDailyActivityFixture(string db, DailyActivityFixture fx)
        {
            foreach (string lid in fx.LineIds)
                BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
            foreach (string oid in fx.ExtraOrderIds)
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", oid);
            BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @P1, @P2, @P3, @P4, @P5, @P6)",
                "@A", fx.Bill1Id, "@B", fx.Bill2Id, "@C", fx.Bill3Id,
                "@P1", fx.Pur1Id, "@P2", fx.Pur2Id, "@P3", fx.Pur3Id, "@P4", fx.Pur4Id, "@P5", fx.Pur5Id, "@P6", fx.PurOutId);
            foreach (string vid in fx.VoucherIds)
                BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
            BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @A2, @B, @C)",
                "@A", fx.ProdCoffeeId, "@A2", fx.ProdCoffee2Id, "@B", fx.ProdTeaId, "@C", fx.ProdMatId);
            BookingExec(db, "DELETE FROM DDONVITINH WHERE ID IN (@U1, @U2)", "@U1", fx.UnitId, "@U2", fx.Unit2Id);
            BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", fx.CustAId);
            BookingExec(db, "DELETE FROM DNHACUNGCAP WHERE ID = @I", "@I", fx.SuppAId);
            BookingExec(db, "DELETE FROM TNGUOIDUNGTHEOCUAHANG WHERE ID IN (@A, @B, @C)", "@A", fx.Link1AId, "@B", fx.Link2AId, "@C", fx.Link2BId);
            BookingExec(db, "DELETE FROM DKHOHANG WHERE ID IN (@A, @B)", "@A", fx.WhAId, "@B", fx.WhBId);
            BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B, @C)", "@A", fx.Cashier1Id, "@B", fx.Cashier2Id, "@C", fx.UserNoStoreId);
            BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", fx.StoreAId, "@B", fx.StoreBId);
        }
    }
}
