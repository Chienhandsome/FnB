using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void DailyActivityUiTests(string db, DailyActivityFixture fx)
        {
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var repo = new DailyActivityRepository(db);
            DateTime dbToday = repo.GetServerDate();
            DateTime firstOfMonth = new DateTime(dbToday.Year, dbToday.Month, 1);

            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // 0. Kiểm đầu báo cáo đọc đúng 4 khóa thật trong SCONFIG (CompanyName, CompanyAddress, CompanyPhone, CompanyEmail) và hoàn lại trong finally.
            DailyActivityCompanyHeaderUiTest(db, adminSession);

            // 1. Màn Admin (có >= 2 cửa hàng): mặc định 01 tháng..hôm nay, Q15 để trống CH, đầu báo cáo chỉ đổi sau khi bấm Tải, kiểm đủ 4 khối + 8 dòng tổng hợp + F5 + In + cuộn + theme.
            using (var host = MakeOffscreenHost())
            {
                var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DailyActivitySections body = view.BodyForTest;
                    string initialHeaderFilter = body.FilterSummaryLabel.Text;

                    bool openOk = view.FromDateBoxForTest.Value.Date == firstOfMonth
                        && view.ToDateBoxForTest.Value.Date == dbToday
                        && view.CashierComboForTest.SelectedIndex == 0
                        && view.StoreComboForTest.SelectedIndex == 0
                        && body.ReportTitleLabel.Text == DailyActivityConstants.ReportTitle
                        && view.CashierNoteLabelForTest.Text == DailyActivityConstants.CashierNote
                        && initialHeaderFilter.IndexOf(firstOfMonth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal) >= 0
                        && initialHeaderFilter.IndexOf("Tất cả", StringComparison.Ordinal) >= 0;
                    Check("Chi tiết HĐ ngày UI (§7 mở màn, Q15 nhiều CH, P4 ghi chú): Từ=01 tháng, Đến=hôm nay DB, CH trống, tiêu đề & ghi chú đúng", openOk, initialHeaderFilter);

                    // Đổi ô lọc NHƯNG CHƯA bấm Tải dữ liệu → dòng đầu báo cáo KHÔNG được đổi!
                    view.FromDateBoxForTest.Inner.Text = "10/07/2004";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "11/07/2004";
                    view.ToDateBoxForTest.Commit();
                    bool headerUnchangedBeforeLoad = body.FilterSummaryLabel.Text == initialHeaderFilter;

                    // Bấm nút Tải dữ liệu (PerformClick thật).
                    view.LoadButtonForTest.PerformClick();
                    Application.DoEvents();

                    bool headerUpdatedAfterLoad = body.FilterSummaryLabel.Text.IndexOf("10/07/2004", StringComparison.Ordinal) >= 0
                        && body.FilterSummaryLabel.Text.IndexOf("11/07/2004", StringComparison.Ordinal) >= 0
                        && body.FilterSummaryLabel.Text.IndexOf("Thu ngân: Tất cả", StringComparison.Ordinal) >= 0
                        && body.FilterSummaryLabel.Text.IndexOf("Cửa hàng: Tất cả", StringComparison.Ordinal) >= 0;
                    Check("Chi tiết HĐ ngày UI (đầu báo cáo phản ánh dữ liệu đã tải): chưa bấm Tải không đổi, bấm Tải mới cập nhật",
                        headerUnchangedBeforeLoad && headerUpdatedAfterLoad, body.FilterSummaryLabel.Text);

                    // Kiểm đúng cột, nhãn, thứ tự, không cột ẩn, không cắt chữ trên cả 4 lưới.
                    bool salesColsOk = CheckExactHeaders(body.SalesGrid, view.HeadingFontForTest,
                        new[] { "STT", "Số HĐ", "Giờ tt", "Khách hàng", "Tổng cộng", "Tiền mặt", "Chuyển khoản", "Thẻ ATM", "Voucher", "Trừ tích lũy", "Thẻ trả trước", "Còn nợ" });
                    bool itemColsOk = CheckExactHeaders(body.ItemsGrid, view.HeadingFontForTest,
                        new[] { "STT", "Mã hàng", "Tên hàng", "ĐVT", "Số lượng", "Đơn giá", "% G.giá", "Thành tiền" });
                    bool purColsOk = CheckExactHeaders(body.PurchasesGrid, view.HeadingFontForTest,
                        new[] { "STT", "Số phiếu", "Nhà cung cấp", "Tổng cộng", "Tiền thanh toán", "Còn nợ" });
                    bool cfColsOk = CheckExactHeaders(body.CashFlowGrid, view.HeadingFontForTest,
                        new[] { "STT", "Số phiếu", "Diễn giải", "Tên đối tượng", "Thu TM", "Chi TM", "Thu CK", "Chi CK" });
                    Check("Chi tiết HĐ ngày UI (§7/Q9 đúng 12+8+6+8 cột không cột ẩn không cắt chữ, Trừ tích lũy trước Thẻ trả trước)",
                        salesColsOk && itemColsOk && purColsOk && cfColsOk,
                        "s=" + salesColsOk + ", i=" + itemColsOk + ", p=" + purColsOk + ", c=" + cfColsOk);

                    // Kiểm ĐỦ mọi ô tiền (8 ô) của bill CTHD-HD01 (8 số khác 0, đôi một khác nhau) và đủ 8 ô tiền dòng TỔNG CỘNG Khối 1.
                    bool salesCellsOk = body.SalesGrid.Rows.Count == 4
                        && CellText(body.SalesGrid, 0, "colSoHd") == "CTHD-HD00"
                        && CellText(body.SalesGrid, 1, "colSoHd") == "CTHD-HD01"
                        && CellText(body.SalesGrid, 1, "colGioTt") == "09:15"
                        && CellText(body.SalesGrid, 1, "colKhachHang") == "CTHD Khách A"
                        && CellText(body.SalesGrid, 1, "colTongCong") == 507000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colTienMat") == 200000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colChuyenKhoan") == 110000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colTheAtm") == 70000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colVoucher") == 35000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colTruTichLuy") == 25000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colTheTraTruoc") == 40000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 1, "colConNo") == 27000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 2, "colSoHd") == "CTHD-HD02"
                        && CellText(body.SalesGrid, 3, "colSoHd") == "TỔNG CỘNG"
                        && CellText(body.SalesGrid, 3, "colTongCong") == 927000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colTienMat") == 410000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colChuyenKhoan") == 230000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colTheAtm") == 113000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colVoucher") == 52000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colTruTichLuy") == 30000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colTheTraTruoc") == 55000m.ToString("n0", vi)
                        && CellText(body.SalesGrid, 3, "colConNo") == 37000m.ToString("n0", vi);

                    // Khối 2: 7 dòng dữ liệu gộp + 4 dòng tổng §2.5 (11 dòng).
                    bool itemsCellsOk = body.ItemsGrid.Rows.Count == 11
                        && CellText(body.ItemsGrid, 7, "colTenHang") == DailyActivityConstants.ItemGrossCaption
                        && CellText(body.ItemsGrid, 7, "colSoLuong") == 13m.ToString("#,##0.##", vi)
                        && CellText(body.ItemsGrid, 7, "colThanhTien") == 460000m.ToString("n0", vi)
                        && CellText(body.ItemsGrid, 8, "colTenHang") == DailyActivityConstants.ItemDiscountCaption
                        && CellText(body.ItemsGrid, 8, "colThanhTien") == 36000m.ToString("n0", vi)
                        && CellText(body.ItemsGrid, 9, "colTenHang") == DailyActivityConstants.BillDiscountCaption
                        && CellText(body.ItemsGrid, 9, "colThanhTien") == 30000m.ToString("n0", vi)
                        && CellText(body.ItemsGrid, 10, "colTenHang") == DailyActivityConstants.ItemNetTotalCaption
                        && CellText(body.ItemsGrid, 10, "colThanhTien") == 394000m.ToString("n0", vi);

                    // Khối 3: kiểm đủ Nhà cung cấp, Tổng cộng, Tiền thanh toán, Còn nợ của CTHD-PN01, trả dư CTHD-PN04 (=0) và đủ 3 ô dòng tổng.
                    int pn1Row = FindRowByCellText(body.PurchasesGrid, "colPurSoPhieu", "CTHD-PN01");
                    int pn4Row = FindRowByCellText(body.PurchasesGrid, "colPurSoPhieu", "CTHD-PN04");
                    bool purCellsOk = body.PurchasesSection.Visible && body.PurchasesGrid.Rows.Count == 6
                        && pn1Row >= 0
                        && CellText(body.PurchasesGrid, pn1Row, "colNhaCungCap") == "CTHD NCC A"
                        && CellText(body.PurchasesGrid, pn1Row, "colPurTongCong") == 120000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, pn1Row, "colPurThanhToan") == 50000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, pn1Row, "colPurConNo") == 70000m.ToString("n0", vi)
                        && pn4Row >= 0
                        && CellText(body.PurchasesGrid, pn4Row, "colPurTongCong") == 30000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, pn4Row, "colPurThanhToan") == 40000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, pn4Row, "colPurConNo") == "0"
                        && CellText(body.PurchasesGrid, 5, "colPurSoPhieu") == "TỔNG CỘNG"
                        && CellText(body.PurchasesGrid, 5, "colPurTongCong") == 310000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, 5, "colPurThanhToan") == 148000m.ToString("n0", vi)
                        && CellText(body.PurchasesGrid, 5, "colPurConNo") == 172000m.ToString("n0", vi);

                    // Khối 4: kiểm đủ Diễn giải, Tên đối tượng và 4 ô tiền của phiếu thu TM (CTHD-PT01), phiếu thu CK (CTHD-PT02), phiếu chi CK (CTHD-PC02) và dòng tổng.
                    int pt1Row = FindRowByCellText(body.CashFlowGrid, "colTcSoPhieu", "CTHD-PT01");
                    int pt2Row = FindRowByCellText(body.CashFlowGrid, "colTcSoPhieu", "CTHD-PT02");
                    int pc2Row = FindRowByCellText(body.CashFlowGrid, "colTcSoPhieu", "CTHD-PC02");
                    bool cfCellsOk = body.CashFlowGrid.Rows.Count == 5
                        && pt1Row >= 0
                        && CellText(body.CashFlowGrid, pt1Row, "colDienGiai") == "Diễn giải CTHD-PT01"
                        && CellText(body.CashFlowGrid, pt1Row, "colTenDoiTuong") == "Đối tượng CTHD-PT01"
                        && CellText(body.CashFlowGrid, pt1Row, "colThuTm") == 85000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, pt1Row, "colChiTm") == "0"
                        && CellText(body.CashFlowGrid, pt1Row, "colThuCk") == "0"
                        && CellText(body.CashFlowGrid, pt1Row, "colChiCk") == "0"
                        && pt2Row >= 0
                        && CellText(body.CashFlowGrid, pt2Row, "colDienGiai") == "Diễn giải CTHD-PT02"
                        && CellText(body.CashFlowGrid, pt2Row, "colTenDoiTuong") == "Đối tượng CTHD-PT02"
                        && CellText(body.CashFlowGrid, pt2Row, "colThuTm") == "0"
                        && CellText(body.CashFlowGrid, pt2Row, "colChiTm") == "0"
                        && CellText(body.CashFlowGrid, pt2Row, "colThuCk") == 65000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, pt2Row, "colChiCk") == "0"
                        && pc2Row >= 0
                        && CellText(body.CashFlowGrid, pc2Row, "colDienGiai") == "Diễn giải CTHD-PC02"
                        && CellText(body.CashFlowGrid, pc2Row, "colTenDoiTuong") == "Đối tượng CTHD-PC02"
                        && CellText(body.CashFlowGrid, pc2Row, "colThuTm") == "0"
                        && CellText(body.CashFlowGrid, pc2Row, "colChiTm") == "0"
                        && CellText(body.CashFlowGrid, pc2Row, "colThuCk") == "0"
                        && CellText(body.CashFlowGrid, pc2Row, "colChiCk") == 19000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, 4, "colTcSoPhieu") == "TỔNG CỘNG"
                        && CellText(body.CashFlowGrid, 4, "colThuTm") == 85000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, 4, "colChiTm") == 22000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, 4, "colThuCk") == 65000m.ToString("n0", vi)
                        && CellText(body.CashFlowGrid, 4, "colChiCk") == 19000m.ToString("n0", vi);

                    Check("Chi tiết HĐ ngày UI (§7 đủ mọi ô tiền & chữ thật trên 4 lưới và dòng tổng): Khối 1 (8 cột tiền), Khối 2 (4 dòng tổng), Khối 3, Khối 4",
                        salesCellsOk && itemsCellsOk && purCellsOk && cfCellsOk,
                        "s=" + salesCellsOk + ", i=" + itemsCellsOk + ", p=" + purCellsOk + ", c=" + cfCellsOk);

                    // Kiểm khối tổng hợp 8 dòng ở cuối khớp số tính tay.
                    string[] expectedOverallVals =
                    {
                        355000m.ToString("n0", vi), 246000m.ToString("n0", vi), 113000m.ToString("n0", vi),
                        52000m.ToString("n0", vi), 55000m.ToString("n0", vi), 30000m.ToString("n0", vi),
                        37000m.ToString("n0", vi), 172000m.ToString("n0", vi)
                    };
                    bool overallOk = true;
                    for (int i = 0; i < 8; i++)
                    {
                        if (body.OverallCaptionLabels[i].Text != DailyActivitySummary.OverallCaptions[i]
                            || body.OverallValueLabels[i].Text != expectedOverallVals[i])
                            overallOk = false;
                    }
                    Check("Chi tiết HĐ ngày UI (§7/§2.7 khối tổng hợp 8 dòng cuối báo cáo): đúng nhãn, đúng thứ tự, khớp 8 số tính tay", overallOk);

                    // Kiểm thứ tự bố cục dọc, không cuộn dọc lồng lưới, cảm ứng >= 32px, lăn chuột trên lưới cuộn được trang.
                    bool layoutOrderOk = body.SalesSection.Top < body.ItemsSection.Top
                        && body.ItemsSection.Top < body.PurchasesSection.Top
                        && body.PurchasesSection.Top < body.CashFlowSection.Top
                        && body.CashFlowSection.Top < body.OverallSection.Top;
                    bool gridsConfigOk = body.SalesGrid.ReadOnly && body.ItemsGrid.ReadOnly && body.PurchasesGrid.ReadOnly && body.CashFlowGrid.ReadOnly
                        && body.SalesGrid.ScrollBars == ScrollBars.Horizontal
                        && body.ItemsGrid.ScrollBars == ScrollBars.Horizontal
                        && body.PurchasesGrid.ScrollBars == ScrollBars.Horizontal
                        && body.CashFlowGrid.ScrollBars == ScrollBars.Horizontal
                        && body.SalesGrid.RowTemplate.Height >= 32 && body.ItemsGrid.RowTemplate.Height >= 32;

                    body.ScrollToTop();
                    int yBeforeWheel = -body.ScrollHost.AutoScrollPosition.Y;
                    body.SalesGrid.SimulateMouseWheelForTest(-240);
                    int yAfterWheel = -body.ScrollHost.AutoScrollPosition.Y;
                    Check("Chi tiết HĐ ngày UI (§7 bố cục dọc 1→2→3→4→Tổng hợp, chỉ đọc, không cuộn dọc trong lưới, lăn chuột cuộn trang)",
                        layoutOrderOk && gridsConfigOk && yAfterWheel > yBeforeWheel,
                        "ord=" + layoutOrderOk + ", cfg=" + gridsConfigOk + ", wheel=" + yBeforeWheel + "->" + yAfterWheel);

                    // Đổi theme lúc chạy → kiểm màu thật của nền, tiêu đề, ô header & ô tổng trên lưới đổi theo theme mới!
                    ThemeManager.Apply(BuiltInThemes.Ocean);
                    Application.DoEvents();
                    ThemeTokens ocean = BuiltInThemes.Ocean;
                    bool themeOk = view.BackColor == ocean.Background
                        && body.ReportTitleLabel.ForeColor == ocean.Primary
                        && body.OverallValueLabels[0].ForeColor == ocean.Primary
                        && body.SalesGrid.ColumnHeadersDefaultCellStyle.BackColor == Draw.Mix(ocean.Surface, ocean.Primary, 0.18f)
                        && body.SalesGrid.Rows[3].DefaultCellStyle.ForeColor == ocean.Primary;
                    Check("Chi tiết HĐ ngày UI (§7 đổi theme lúc chạy): màu thật trên nền, tiêu đề, tổng hợp và ô lưới đổi theo token", themeOk);

                    // Chọn Thu ngân = Cashier1 qua ComboBox + PerformClick; rồi chèn phiếu thu 11.000 và nhấn F5 qua ProcessCmdKey!
                    SelectCashierById(view.CashierComboForTest, fx.Cashier1Id);
                    view.LoadButtonForTest.PerformClick();
                    bool cashierFilterUiOk = body.FilterSummaryLabel.Text.IndexOf("CTHD Thu ngân 1", StringComparison.Ordinal) >= 0
                        && body.SalesGrid.Rows.Count == 3
                        && body.PurchasesGrid.Rows.Count == 4
                        && body.CashFlowGrid.Rows.Count == 3;

                    string cashBeforeF5 = body.OverallValueLabels[0].Text;
                    string f5VoucherId = DbHelper.NewId();
                    fx.VoucherIds.Add(f5VoucherId);
                    BookingExec(db,
                        "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, DIENGIAI, TENDOITUONG, THU, CHI, CHUYENKHOAN, DCUAHANGID, STATUS, USERCREATEDID) " +
                        "VALUES (@ID, 'CTHD-PT-F5', @NG, @NG, 'Thu F5', 'F5', 11000, 0, 0, @CH, 30, @UCR)",
                        "@ID", f5VoucherId, "@NG", new DateTime(2004, 7, 10, 17, 0, 0), "@CH", fx.StoreAId, "@UCR", fx.Cashier1Id);
                    bool f5Handled = view.DispatchCmdKeyForTest(Keys.F5);
                    string cashAfterF5 = body.OverallValueLabels[0].Text;
                    bool f5CashExactOk = cashBeforeF5 == 181000m.ToString("n0", vi)
                        && cashAfterF5 == 192000m.ToString("n0", vi);

                    view.PrintButtonForTest.PerformClick();
                    bool printOk = view.LastInfoMessageForTest == DailyActivityConstants.PrintUnsupportedMessage;

                    // Chọn khoảng ngày rỗng → ẩn cả Khối 3 NHẬP HÀNG, 2 dòng đầu tổng hợp hiện "0", 6 dòng sau trống.
                    view.FromDateBoxForTest.Inner.Text = "01/01/2001";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "02/01/2001";
                    view.ToDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    bool hidePurAndZeroOk = !body.PurchasesSection.Visible
                        && body.SalesSection.Visible && body.ItemsSection.Visible && body.CashFlowSection.Visible && body.OverallSection.Visible
                        && body.OverallValueLabels[0].Text == "0"
                        && body.OverallValueLabels[1].Text == "0"
                        && body.OverallValueLabels[2].Text == ""
                        && body.OverallValueLabels[7].Text == "";

                    Check("Chi tiết HĐ ngày UI (§7 lọc Thu ngân, phím F5 tải lại số mới, nút In, ẩn Khối 3 khi rỗng & 6 dòng tổng trống khi 0)",
                        cashierFilterUiOk && f5Handled && f5CashExactOk && printOk && hidePurAndZeroOk,
                        "tn=" + cashierFilterUiOk + ", f5=" + cashBeforeF5 + "->" + cashAfterF5 + ", pr=" + printOk + ", hide=" + hidePurAndZeroOk);
                }
                finally { host.Close(); }
            }

            // 2. Q15 & P2 trên UI thật: User thường 1 CH (chọn sẵn Store A; xóa chọn vẫn chỉ thấy Store A) & User 2 CH (để trống).
            var user1Session = new UserSession
            {
                UserId = fx.Cashier1Id, UserName = "cthd_tn1", DisplayName = "CTHD Thu ngân 1",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host1 = MakeOffscreenHost())
            {
                var view1 = new DailyActivityView(user1Session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host1.Controls.Add(view1);
                host1.Show();
                Application.DoEvents();
                try
                {
                    var selectedStore = view1.StoreComboForTest.SelectedItem as StoreRecord;
                    bool q15SingleOk = view1.StoreComboForTest.Items.Count == 2
                        && view1.StoreComboForTest.SelectedIndex == 1
                        && selectedStore != null && selectedStore.Id == fx.StoreAId;

                    view1.StoreComboForTest.SelectedIndex = 0;
                    view1.FromDateBoxForTest.Inner.Text = "10/07/2004";
                    view1.FromDateBoxForTest.Commit();
                    view1.ToDateBoxForTest.Inner.Text = "11/07/2004";
                    view1.ToDateBoxForTest.Commit();
                    view1.LoadButtonForTest.PerformClick();

                    bool p2ClearedStillStoreA = view1.BodyForTest.SalesGrid.Rows.Count == 3
                        && view1.BodyForTest.PurchasesGrid.Rows.Count == 4;
                    Check("Chi tiết HĐ ngày UI (§7/Q15/P2 user thường 1 CH): mở màn chọn sẵn Store A, xóa chọn vẫn bị ràng buộc Store A",
                        q15SingleOk && p2ClearedStillStoreA,
                        "q15=" + q15SingleOk + ", p2=" + p2ClearedStillStoreA);
                }
                finally { host1.Close(); }
            }

            var user2Session = new UserSession
            {
                UserId = fx.Cashier2Id, UserName = "cthd_tn2", DisplayName = "CTHD Thu ngân 2",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host2 = MakeOffscreenHost())
            {
                var view2 = new DailyActivityView(user2Session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host2.Controls.Add(view2);
                host2.Show();
                Application.DoEvents();
                try
                {
                    Check("Chi tiết HĐ ngày UI (§7/Q15 user thường 2 CH): mở màn để trống ô Cửa hàng",
                        view2.StoreComboForTest.Items.Count == 3 && view2.StoreComboForTest.SelectedIndex == 0);
                }
                finally { host2.Close(); }
            }
        }

        private static void DailyActivityCompanyHeaderUiTest(string db, UserSession adminSession)
        {
            var cfgRepo = new ConfigRepository(db);
            Dictionary<string, ConfigValue> oldMap = cfgRepo.LoadValues();
            string oldName = ReadCfgText(oldMap, "CompanyName");
            string oldAddr = ReadCfgText(oldMap, "CompanyAddress");
            string oldPhone = ReadCfgText(oldMap, "CompanyPhone");
            string oldEmail = ReadCfgText(oldMap, "CompanyEmail");

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = "CÔNG TY THỬ CTHD" },
                    new ConfigChange { Name = "CompanyAddress", DataType = 1, TextValue = "99 Trần Hưng Đạo, Q1" },
                    new ConfigChange { Name = "CompanyPhone", DataType = 1, TextValue = "0909123456" },
                    new ConfigChange { Name = "CompanyEmail", DataType = 1, TextValue = "cthd@kazuko.vn" }
                }, "selftest");
                SystemConfig.Reload(db);

                using (var host = MakeOffscreenHost())
                {
                    var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        DailyActivitySections body = view.BodyForTest;
                        string details = body.CompanyDetailsLabel.Text;
                        bool companyOk = body.CompanyNameLabel.Visible
                            && body.CompanyNameLabel.Text == "CÔNG TY THỬ CTHD"
                            && body.CompanyDetailsLabel.Visible
                            && details.IndexOf("99 Trần Hưng Đạo, Q1", StringComparison.Ordinal) >= 0
                            && details.IndexOf("0909123456", StringComparison.Ordinal) >= 0
                            && details.IndexOf("cthd@kazuko.vn", StringComparison.Ordinal) >= 0;
                        Check("Chi tiết HĐ ngày UI (đầu báo cáo đọc 4 khóa thật SCONFIG CompanyName/Address/Phone/Email)",
                            companyOk, body.CompanyNameLabel.Text + " | " + details);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = oldName },
                    new ConfigChange { Name = "CompanyAddress", DataType = 1, TextValue = oldAddr },
                    new ConfigChange { Name = "CompanyPhone", DataType = 1, TextValue = oldPhone },
                    new ConfigChange { Name = "CompanyEmail", DataType = 1, TextValue = oldEmail }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        private static string ReadCfgText(Dictionary<string, ConfigValue> map, string key)
        {
            ConfigValue v;
            return map != null && map.TryGetValue(key, out v) && v != null ? (v.TextValue ?? "") : "";
        }

        private static Form MakeOffscreenHost()
        {
            return new Form
            {
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                Location = new Point(-20000, -20000),
                ClientSize = new Size(1400, 780)
            };
        }

        private static bool CheckExactHeaders(DataGridView grid, Font headerFont, string[] expected)
        {
            if (grid.Columns.Count != expected.Length)
                return false;
            for (int i = 0; i < expected.Length; i++)
            {
                DataGridViewColumn col = grid.Columns[i];
                if (!col.Visible || col.HeaderText != expected[i])
                    return false;
                int needW = TextRenderer.MeasureText(col.HeaderText, headerFont).Width;
                if (col.Width < needW)
                    return false;
            }
            return true;
        }

        private static string CellText(DataGridView grid, int rowIdx, string colName)
        {
            return Convert.ToString(grid.Rows[rowIdx].Cells[colName].FormattedValue);
        }

        private static int FindRowByCellText(DataGridView grid, string colName, string text)
        {
            for (int r = 0; r < grid.Rows.Count; r++)
                if (CellText(grid, r, colName) == text)
                    return r;
            return -1;
        }

        private static void SelectCashierById(ComboBox combo, string cashierId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var c = combo.Items[i] as CashierRecord;
                if (c != null && c.Id == cashierId)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
