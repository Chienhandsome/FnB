using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP1EmbeddedTests(string db)
        {
            Dictionary<string, string> srepBefore = SnapshotSreport(db);
            Dictionary<string, string> stplBefore = SnapshotSreportTemplate(db);
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                TestEmbeddedReportTemplateLoading(db);
                TestDailyActivityEmbeddedEndToEnd(db);
                TestBusinessResultEmbeddedEndToEnd(db);
                TestEmbeddedCommonBehaviors(db);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
            }

            Dictionary<string, string> srepAfter = SnapshotSreport(db);
            Dictionary<string, string> stplAfter = SnapshotSreportTemplate(db);
            Check("In P1b Nhúng (T5): bảng SREPORT và SREPORTTEMPLATE (số dòng và băm từng blob) không đổi sau khi dùng",
                SnapshotsEqual(srepBefore, srepAfter) && SnapshotsEqual(stplBefore, stplAfter),
                "srep=" + srepBefore.Count + "->" + srepAfter.Count + ", stpl=" + stplBefore.Count + "->" + stplAfter.Count);
        }

        private static void TestEmbeddedReportTemplateLoading(string db)
        {
            var repo = new PrintTemplateRepository(db);
            string errDaily;
            PrintTemplateRecord tplDaily = repo.LoadReportTemplate(
                PrintConstants.DailyActivityReportId, DailyActivityConstants.FunctionName, out errDaily);
            bool dailyOk = tplDaily != null
                && string.IsNullOrEmpty(errDaily)
                && string.Equals(tplDaily.Id, "765b861a-e8d6-4739-b65d-d2b19b47b205", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tplDaily.Name, "Mẫu đầy đủ", StringComparison.OrdinalIgnoreCase)
                && tplDaily.TemplateXml.Length > 1000;

            string errKqkd;
            PrintTemplateRecord tplKqkd = repo.LoadReportTemplate(
                PrintConstants.BusinessResultReportId, BusinessResultConstants.FunctionName, out errKqkd);
            bool kqkdOk = tplKqkd != null
                && string.IsNullOrEmpty(errKqkd)
                && tplKqkd.TemplateXml.Length > 1000
                && tplKqkd.TemplateXml.IndexOf("_StartReport", StringComparison.Ordinal) >= 0;

            string errMissing;
            PrintTemplateRecord tplMissing = repo.LoadReportTemplate(
                "00000000-0000-0000-0000-000000000000", "Báo cáo không tồn tại", out errMissing);
            bool missingOk = tplMissing == null
                && !string.IsNullOrEmpty(errMissing)
                && errMissing.IndexOf("Báo cáo không tồn tại", StringComparison.Ordinal) >= 0;

            string msgNoLastId = PrintTemplateRepository.FormatMissingLastTemplateIdError(DailyActivityConstants.FunctionName);
            string msgNoTplRec = PrintTemplateRepository.FormatMissingReportTemplateRecordError(BusinessResultConstants.FunctionName, "missing-id");
            bool formatErrOk = msgNoLastId.IndexOf(DailyActivityConstants.FunctionName, StringComparison.Ordinal) >= 0
                && msgNoTplRec.IndexOf(BusinessResultConstants.FunctionName, StringComparison.Ordinal) >= 0;

            Check("In P1b Nhúng (SREPORT.LASTTEMPLATEID → SREPORTTEMPLATE): đọc đúng mẫu 765b861a (Chi tiết HĐ ngày) & KQKD; thiếu bản ghi báo lỗi nêu tên báo cáo",
                dailyOk && kqkdOk && missingOk && formatErrOk,
                "daily=" + (tplDaily == null ? errDaily : tplDaily.Id) + ", kqkd=" + (tplKqkd == null ? errKqkd : tplKqkd.Id) + ", missing=" + errMissing);
        }

        private static void TestDailyActivityEmbeddedEndToEnd(string db)
        {
            string store1Id = DbHelper.NewId();
            string store2Id = DbHelper.NewId();
            string wh1Id = DbHelper.NewId();
            string wh2Id = DbHelper.NewId();
            string cashier1Id = DbHelper.NewId();
            string cashier2Id = DbHelper.NewId();
            string custId = DbHelper.NewId();
            string suppId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();
            string prod3Id = DbHelper.NewId();
            string bill1Id = DbHelper.NewId();
            string bill2Id = DbHelper.NewId();
            string billS2Id = DbHelper.NewId();
            string pur1Id = DbHelper.NewId();
            var lineIds = new List<string>();
            var voucherIds = new List<string>();
            string tempDir = Path.Combine(Path.GetTempPath(), "kazuko-emb-daily-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempDir);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", store1Id, "@N", "EMB-CH1");
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", store2Id, "@N", "EMB-CH2");
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @C, 30, 'selftest')", "@I", wh1Id, "@N", "EMB-KHO1", "@C", store1Id);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @C, 30, 'selftest')", "@I", wh2Id, "@N", "EMB-KHO2", "@C", store2Id);
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", cashier1Id, "@N", "EMB Thu ngân Lan", "@U", "emb_tn1");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')", "@I", cashier2Id, "@N", "EMB Thu ngân Minh", "@U", "emb_tn2");
                BookingExec(db, "INSERT INTO DKHACHHANG (ID, NAME, MAKHACH, STATUS, USERCREATEDID) VALUES (@I, @N, 'EMB-KH1', 30, 'selftest')", "@I", custId, "@N", "EMB Khách Nguyễn");
                BookingExec(db, "INSERT INTO DNHACUNGCAP (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", suppId, "@N", "EMB NCC Trà");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", unitId, "@N", "Ly");
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'EMB-P1', 'Cà phê đặc biệt', @U, '0', 100000, 30, 'selftest')", "@I", prod1Id, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'EMB-P2', 'Topping kem', @U, '0', 10000, 30, 'selftest')", "@I", prod2Id, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'EMB-P3', 'Trà đào cam sả', @U, '0', 40000, 30, 'selftest')", "@I", prod3Id, "@U", unitId);

                // Bill B1 (CH1, TN1): 90.000 (TM 50k, CK 40k, hàng chưa giảm 110k, giảm trên món 10k, giảm bill 10k).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                    "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                    "VALUES (@ID, 'EMB-B1', 0, 30, 30, @NG, @GTT, @CH, @KH, @UTT, @UTT, 10000, 90000, 50000, 40000, 0, 0, 0, 0, 0, 0)",
                    "@ID", bill1Id, "@NG", new DateTime(2006, 6, 15, 9, 0, 0), "@GTT", new DateTime(2006, 6, 15, 9, 30, 0),
                    "@CH", store1Id, "@KH", custId, "@UTT", cashier1Id);

                // Bill B2 (CH1, TN2): 75.000 (Thẻ 20k, Voucher 10k, Thẻ trả trước 7k, Trừ tích lũy 3k, Đặt trước 10k, Nợ 25k, giảm bill 5k).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                    "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                    "VALUES (@ID, 'EMB-B2', 0, 30, 30, @NG, @GTT, @CH, NULL, @UTT, @UTT, 5000, 75000, 0, 0, 20000, 10000, 3000, 7000, 25000, 10000)",
                    "@ID", bill2Id, "@NG", new DateTime(2006, 6, 16, 14, 0, 0), "@GTT", new DateTime(2006, 6, 16, 14, 15, 0),
                    "@CH", store1Id, "@UTT", cashier2Id);

                // Bill cửa hàng 2 (CH2, TN2): 50.000 tiền mặt để kiểm lọc cửa hàng.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, DKHACHHANGID, " +
                    "USERTHANHTOANID, USERCREATEDID, TIENGIAMGIA, TONGCONG, TIENMAT, CHUYENKHOAN, THE, VOUCHER, TRUTICHLUY, THETRATRUOC, CONGNO, DATTRUOC) " +
                    "VALUES (@ID, 'EMB-B-CH2', 0, 30, 30, @NG, @GTT, @CH, NULL, @UTT, @UTT, 0, 50000, 50000, 0, 0, 0, 0, 0, 0, 0)",
                    "@ID", billS2Id, "@NG", new DateTime(2006, 6, 15, 11, 0, 0), "@GTT", new DateTime(2006, 6, 15, 11, 5, 0),
                    "@CH", store2Id, "@UTT", cashier2Id);

                Action<string, string, string, decimal, decimal, decimal, decimal> addLine =
                    delegate (string bid, string pid, string ten, decimal sl, decimal dg, decimal ck, decimal tt)
                    {
                        string lid = DbHelper.NewId();
                        lineIds.Add(lid);
                        BookingExec(db,
                            "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                            "SLXUATCHUAQUYDOI, SLNHAPCHUAQUYDOI, DONGIA, TILEGIAMGIA, THANHTIEN, XUATVATTU, COMBOPARENTID, STATUS, USERCREATEDID) " +
                            "VALUES (@ID, @B, @P, @T, @U, @SL, 0, @DG, @CK, @TT, 0, '', 30, 'selftest')",
                            "@ID", lid, "@B", bid, "@P", pid, "@T", ten, "@U", unitId, "@SL", sl, "@DG", dg, "@CK", ck, "@TT", tt);
                    };
                // B1: 100k (-10% = 90k) + 10k = chưa giảm 110k, giảm món 10k, thành tiền 100k.
                addLine(bill1Id, prod1Id, "Cà phê đặc biệt", 1m, 100000m, 10m, 90000m);
                addLine(bill1Id, prod2Id, "Topping kem", 1m, 10000m, 0m, 10000m);
                // B2: 2 * 40k = 80k. Tổng chưa giảm CH1 = 190k, giảm món 10k, giảm bill 15k, tổng cộng 165k.
                addLine(bill2Id, prod3Id, "Trà đào cam sả", 2m, 40000m, 0m, 80000m);
                addLine(billS2Id, prod3Id, "Trà đào cam sả", 1m, 50000m, 0m, 50000m);

                // Phiếu nhập CH1 (TN1): 300.000 trả 100.000 (còn nợ 200.000).
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DKHONHAPID, DNHACUNGCAPID, USERCREATEDID, " +
                    "TONGCONG, TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, LOAITHANHTOAN) " +
                    "VALUES (@ID, 'EMB-PN1', 1, 0, 30, @NG, @KH, @NCC, @UCR, 300000, 100000, 100000, 0, 0)",
                    "@ID", pur1Id, "@NG", new DateTime(2006, 6, 15, 10, 0, 0), "@KH", wh1Id, "@NCC", suppId, "@UCR", cashier1Id);

                // Thu tiền mặt 11.000 (CH1, TN1) và chi chuyển khoản 6.000 (CH1, TN2).
                string vInId = DbHelper.NewId();
                string vOutId = DbHelper.NewId();
                voucherIds.Add(vInId);
                voucherIds.Add(vOutId);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, DIENGIAI, TENDOITUONG, THU, CHI, CHUYENKHOAN, DCUAHANGID, STATUS, USERCREATEDID) " +
                    "VALUES (@ID, 'EMB-PT1', @NG, @NG, 'Thu mặt thử', 'Khách lẻ', 11000, 0, 0, @CH, 30, @UCR)",
                    "@ID", vInId, "@NG", new DateTime(2006, 6, 15, 12, 0, 0), "@CH", store1Id, "@UCR", cashier1Id);
                BookingExec(db,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, DIENGIAI, TENDOITUONG, THU, CHI, CHUYENKHOAN, DCUAHANGID, STATUS, USERCREATEDID) " +
                    "VALUES (@ID, 'EMB-PC1', @NG, @NG, 'Chi CK thử', 'Điện lực', 0, 6000, 1, @CH, 30, @UCR)",
                    "@ID", vOutId, "@NG", new DateTime(2006, 6, 16, 15, 0, 0), "@CH", store1Id, "@UCR", cashier2Id);

                var adminSession = new UserSession
                {
                    UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };

                var repo = new DailyActivityRepository(db);
                DateTime dbToday = repo.GetServerDate();
                DateTime firstOfMonth = new DateTime(dbToday.Year, dbToday.Month, 1);

                using (var host = MakeOffscreenHost())
                {
                    var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        // 1. Mở màn: ngày mặc định, không có nút In hay dải ghi chú, PreviewControl nhúng đã dựng trang.
                        bool noPrintOrNote = true;
                        foreach (Control c in view.TopBarForTest.Controls)
                            if (c.Text != null && c.Text.IndexOf("In báo cáo", StringComparison.OrdinalIgnoreCase) >= 0)
                                noPrintOrNote = false;
                        List<KeyValuePair<string, string>> initTexts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                        bool initOk = view.FromDateBoxForTest.Value.Date == firstOfMonth
                            && view.ToDateBoxForTest.Value.Date == dbToday
                            && view.CashierComboForTest.SelectedIndex == 0
                            && view.StoreComboForTest.SelectedIndex == 0
                            && noPrintOrNote
                            && view.Controls.Contains(view.PreviewControlForTest)
                            && view.CurrentReportForTest != null
                            && view.CurrentReportForTest.PreparedPages.Count > 0
                            && FindObjectText(initTexts, "txtTitle") == DailyActivityConstants.ReportTitle;
                        Check("In P1b Chi tiết HĐ ngày (mở màn trình xem FastReport nhúng): Từ=01 tháng, Đến=hôm nay DB, không nút In/ghi chú, dựng trang thành công",
                            initOk, "pages=" + (view.CurrentReportForTest == null ? 0 : view.CurrentReportForTest.PreparedPages.Count));

                        // 2. Chọn CH1, gõ ngày 15/06/2006..16/06/2006, bấm nút Tải dữ liệu thật (PerformClick).
                        view.FromDateBoxForTest.Inner.Text = "15/06/2006";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "16/06/2006";
                        view.ToDateBoxForTest.Commit();
                        SelectStoreInCombo(view.StoreComboForTest, store1Id);
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();

                        VerifyDailyActivityPreparedCells(view.CurrentReportForTest, tempDir);

                        // 3. Đổi ngày trong ô nhưng CHƯA bấm Tải → trang báo cáo không đổi.
                        Report rptBeforeEdit = view.CurrentReportForTest;
                        view.FromDateBoxForTest.Inner.Text = "01/01/2001";
                        view.FromDateBoxForTest.Commit();
                        bool unchangedBeforeLoad = ReferenceEquals(view.CurrentReportForTest, rptBeforeEdit)
                            && FindObjectText(FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest), "txtFilter")
                                .IndexOf("15/06/2006", StringComparison.Ordinal) >= 0;
                        view.FromDateBoxForTest.Inner.Text = "15/06/2006";
                        view.FromDateBoxForTest.Commit();

                        // 4. Lọc theo Thu ngân 1 (EMB Thu ngân Lan) → tiêu đề và số trên trang đổi đúng (chỉ B1 90k, PN1 100k, PT1 11k → TM -39k, CK 40k).
                        SelectCashierById(view.CashierComboForTest, cashier1Id);
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();
                        List<KeyValuePair<string, string>> tn1Texts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                        string tn1Filter = FindObjectText(tn1Texts, "txtFilter");
                        string s1, s2, s3;
                        bool cashierFilterOk = tn1Filter.IndexOf("Thu ngân: EMB Thu ngân Lan", StringComparison.Ordinal) >= 0
                            && CheckPreparedCellNumber(tn1Texts, "Cell29", 90000m, out s1)
                            && CheckPreparedCellNumber(tn1Texts, "Cell107", -39000m, out s2)
                            && CheckPreparedCellNumber(tn1Texts, "Cell112", 40000m, out s3);

                        // 5. Bỏ lọc thu ngân, chọn cửa hàng 2 (CH2) và bấm F5 thật → số trên trang đổi sang CH2 (50k), chèn phiếu thu 4k rồi F5 lại → 54k.
                        view.CashierComboForTest.SelectedIndex = 0;
                        SelectStoreInCombo(view.StoreComboForTest, store2Id);
                        bool f5StoreHandled = view.DispatchCmdKeyForTest(Keys.F5);
                        Application.DoEvents();
                        List<KeyValuePair<string, string>> ch2Texts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                        string c2Bill, c2Cash;
                        bool storeFilterOk = f5StoreHandled
                            && CheckPreparedCellNumber(ch2Texts, "Cell29", 50000m, out c2Bill)
                            && CheckPreparedCellNumber(ch2Texts, "Cell107", 50000m, out c2Cash);

                        string vF5Id = DbHelper.NewId();
                        voucherIds.Add(vF5Id);
                        BookingExec(db,
                            "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, DIENGIAI, TENDOITUONG, THU, CHI, CHUYENKHOAN, DCUAHANGID, STATUS, USERCREATEDID) " +
                            "VALUES (@ID, 'EMB-PT-F5', @NG, @NG, 'Thu F5', 'F5', 4000, 0, 0, @CH, 30, @UCR)",
                            "@ID", vF5Id, "@NG", new DateTime(2006, 6, 16, 10, 0, 0), "@CH", store2Id, "@UCR", cashier2Id);
                        bool f5ReloadHandled = view.DispatchCmdKeyForTest(Keys.F5);
                        Application.DoEvents();
                        List<KeyValuePair<string, string>> ch2AfterF5 = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                        string c2CashF5;
                        bool f5UpdateOk = f5ReloadHandled && CheckPreparedCellNumber(ch2AfterF5, "Cell107", 54000m, out c2CashF5);

                        Check("In P1b Chi tiết HĐ ngày (lọc thu ngân, lọc cửa hàng, F5 tải lại số mới, chưa tải không đổi trang)",
                            unchangedBeforeLoad && cashierFilterOk && storeFilterOk && f5UpdateOk,
                            "unchg=" + unchangedBeforeLoad + ", tn1=" + cashierFilterOk + " (" + tn1Filter + "), ch2=" + storeFilterOk + ", f5=" + f5UpdateOk);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                foreach (string lid in lineIds)
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
                BookingExec(db, "DELETE FROM TDONHANG WHERE ID IN (@A, @B, @C, @P)",
                    "@A", bill1Id, "@B", bill2Id, "@C", billS2Id, "@P", pur1Id);
                foreach (string vid in voucherIds)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
                BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B, @C)", "@A", prod1Id, "@B", prod2Id, "@C", prod3Id);
                BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId);
                BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", custId);
                BookingExec(db, "DELETE FROM DNHACUNGCAP WHERE ID = @I", "@I", suppId);
                BookingExec(db, "DELETE FROM DKHOHANG WHERE ID IN (@A, @B)", "@A", wh1Id, "@B", wh2Id);
                BookingExec(db, "DELETE FROM SUSER WHERE ID IN (@A, @B)", "@A", cashier1Id, "@B", cashier2Id);
                BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", store1Id, "@B", store2Id);
            }
        }

        private static void VerifyDailyActivityPreparedCells(Report report, string tempDir)
        {
            List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(report);
            string filterText = FindObjectText(texts, "txtFilter");
            bool headerOk = filterText.IndexOf("15/06/2006", StringComparison.Ordinal) >= 0
                && filterText.IndexOf("16/06/2006", StringComparison.Ordinal) >= 0
                && filterText.IndexOf("Thu ngân: Tất cả", StringComparison.Ordinal) >= 0;

            // Khối Bán hàng (mẫu 765b861a: Tổng cộng, Tiền mặt, Thẻ ATM, Đặt trước, Còn nợ; KHÔNG có cột Chuyển khoản/Voucher ở bảng Bán hàng).
            string h1 = FindObjectText(texts, "Cell11");
            string h2 = FindObjectText(texts, "Cell72");
            string h3 = FindObjectText(texts, "Cell75");
            string h4 = FindObjectText(texts, "Cell76");
            string h5 = FindObjectText(texts, "Cell81");
            bool saleHeadersOk = h1 == "Tổng cộng" && h2 == "Tiền mặt" && h3 == "Thẻ ATM" && h4 == "Đặt trước" && h5 == "Còn nợ";

            string t;
            bool saleSumsOk = CheckPreparedCellNumber(texts, "Cell29", 165000m, out t)
                && CheckPreparedCellNumber(texts, "Cell74", 50000m, out t)
                && CheckPreparedCellNumber(texts, "Cell79", 20000m, out t)
                && CheckPreparedCellNumber(texts, "Cell80", 10000m, out t)
                && CheckPreparedCellNumber(texts, "Cell83", 25000m, out t);

            // Khối Mặt hàng bán: 190.000 / 10.000 / 15.000 / 165.000.
            bool itemSumsOk = CheckPreparedCellNumber(texts, "Cell51", 190000m, out t)
                && CheckPreparedCellNumber(texts, "Cell162", 10000m, out t)
                && CheckPreparedCellNumber(texts, "Cell146", 15000m, out t)
                && CheckPreparedCellNumber(texts, "Cell154", 165000m, out t);

            // Khối Nhập hàng: 300.000 / 100.000 / 200.000.
            bool purSumsOk = CheckPreparedCellNumber(texts, "Cell103", 300000m, out t)
                && CheckPreparedCellNumber(texts, "Cell104", 100000m, out t)
                && CheckPreparedCellNumber(texts, "Cell105", 200000m, out t);

            // Khối Thu-chi: 11.000 / 0 / 0 / 6.000.
            bool cfSumsOk = CheckPreparedCellNumber(texts, "Cell70", 11000m, out t)
                && CheckPreparedCellNumber(texts, "Cell71", 0m, out t)
                && CheckPreparedCellNumber(texts, "Cell140", 0m, out t)
                && CheckPreparedCellNumber(texts, "Cell141", 6000m, out t);

            // Khối tổng hợp cuối trang (Table4): TIỀN MẶT -39k, CK 34k, THẺ 20k, VOUCHER 10k, THẺ TT 7k, TÍCH LŨY 3k, KH NỢ 25k, NỢ NCC 200k.
            string ovCash = "", ovCk = "", ovCard = "", ovVou = "", ovPre = "", ovLoy = "", ovCust = "", ovSupp = "";
            bool overallOk = CheckPreparedCellNumber(texts, "Cell107", -39000m, out ovCash)
                && CheckPreparedCellNumber(texts, "Cell112", 34000m, out ovCk)
                && CheckPreparedCellNumber(texts, "Cell117", 20000m, out ovCard)
                && CheckPreparedCellNumber(texts, "Cell167", 10000m, out ovVou)
                && CheckPreparedCellNumber(texts, "Cell169", 7000m, out ovPre)
                && CheckPreparedCellNumber(texts, "Cell171", 3000m, out ovLoy)
                && CheckPreparedCellNumber(texts, "Cell122", 25000m, out ovCust)
                && CheckPreparedCellNumber(texts, "Cell127", 200000m, out ovSupp);

            Check("In P1b Chi tiết HĐ ngày (T1/T3 đối chiếu app gốc đúng từng ô mẫu 765b861a): 4 khối và 8 dòng tổng hợp (-39k/34k/20k/10k/7k/3k/25k/200k)",
                headerOk && saleHeadersOk && saleSumsOk && itemSumsOk && purSumsOk && cfSumsOk && overallOk,
                "hdr=" + headerOk + ", sHdr=" + saleHeadersOk + ", sale=" + saleSumsOk + ", item=" + itemSumsOk
                + ", pur=" + purSumsOk + ", cf=" + cfSumsOk + ", ov=" + overallOk
                + " [" + ovCash + "/" + ovCk + "/" + ovCard + "/" + ovVou + "/" + ovPre + "/" + ovLoy + "/" + ovCust + "/" + ovSupp + "]");

            // T2/T7: Xuất Excel và đọc lại khớp tiếng Việt & dòng tổng.
            string xlsxPath = Path.Combine(tempDir, "ChiTietHoatDongNgay.xlsx");
            FastReportEngine.ExportExcel(report, xlsxPath);
            List<List<ExcelCellVal>> xlRows = ReadExcelAllRows(xlsxPath);
            List<ExcelCellVal> xlCash = FindRowContaining(xlRows, "TIỀN MẶT");
            List<ExcelCellVal> xlCk = FindRowContaining(xlRows, "CHUYỂN KHOẢN");
            List<ExcelCellVal> xlSupp = FindRowContaining(xlRows, "NỢ NHÀ CUNG CẤP");
            bool xlOk = FindRowContaining(xlRows, "Cà phê đặc biệt") != null
                && xlCash != null && RowHasNumber(xlCash, -39000m)
                && xlCk != null && RowHasNumber(xlCk, 34000m)
                && xlSupp != null && RowHasNumber(xlSupp, 200000m)
                && SheetHasNumber(xlRows, 165000m);
            Check("In P1b Chi tiết HĐ ngày (T2/T7 xuất Excel): đọc lại .xlsx khớp tiếng Việt và các dòng tổng (-39k, 34k, 200k, 165k)",
                xlOk, "cash=" + FormatExcelRow(xlCash) + ", ck=" + FormatExcelRow(xlCk) + ", supp=" + FormatExcelRow(xlSupp));
        }
    }
}
