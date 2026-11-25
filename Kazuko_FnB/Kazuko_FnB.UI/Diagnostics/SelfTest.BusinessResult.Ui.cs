using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void BusinessResultUiTests(string db, BusinessResultFixture fx)
        {
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var repo = new BusinessResultRepository(db);
            DateTime dbToday = repo.GetServerDate();
            DateTime firstOfMonth = new DateTime(dbToday.Year, dbToday.Month, 1);

            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // 1. Kiểm màn Admin thật (Show ngoài màn hình): mặc định ngày, 9 cột, không sắp xếp, số mục, đậm LOAI 0/2/9, tỉ lệ, ▲/▼, thiếu ngày, F5, In, đổi theme.
            using (var host = MakeOffscreenHost())
            {
                var view = new BusinessResultView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView g = view.GridForTest;

                    bool openOk = view.FromDateBoxForTest.NullableValue == firstOfMonth
                        && view.ToDateBoxForTest.NullableValue == dbToday
                        && view.StoreComboForTest.SelectedIndex == 0
                        && view.LoadCountForTest == 1;
                    Check("KQKD UI (§7 mở màn): Từ=01 tháng hiện tại, Đến=hôm nay theo DB, tự tải báo cáo, nhiều CH để trống", openOk);

                    // Kiểm đúng 9 cột, thứ tự, tiêu đề, không cột ẩn, không cho sắp xếp, chỉ đọc, dòng cao cảm ứng, không cắt tiêu đề.
                    string[] expectedHeaders = { "Mục", "Nội dung", "%/DT", "Giá trị", "%", "%/CP", "▲/▼", "Tăng giảm so với tháng trước", "KQKD tháng trước" };
                    bool headersOk = CheckExactHeaders(g, view.BoldFontForTest, expectedHeaders);
                    bool notSortableOk = true;
                    foreach (DataGridViewColumn col in g.Columns)
                        if (col.SortMode != DataGridViewColumnSortMode.NotSortable)
                            notSortableOk = false;
                    bool noExcelOrCopyOk = g.ContextMenuStrip == null;
                    foreach (Control c in view.TopBarForTest.Controls)
                    {
                        if (c.Text != null && c.Text.IndexOf("Excel", StringComparison.OrdinalIgnoreCase) >= 0)
                            noExcelOrCopyOk = false;
                    }
                    Check("KQKD UI (§7/C11 cấu trúc lưới 9 cột, không cột ẩn, không sắp xếp, không xuất Excel/menu sao chép, chỉ đọc, dòng cảm ứng >= 32px)",
                        headersOk && notSortableOk && noExcelOrCopyOk && g.ReadOnly && g.RowTemplate.Height >= 32,
                        "hdr=" + headersOk + ", nosort=" + notSortableOk + ", noEx=" + noExcelOrCopyOk + ", h=" + g.RowTemplate.Height);

                    // Đổi sang khoảng 01/04/2005–30/04/2005 và bấm nút Tải dữ liệu thật (PerformClick).
                    view.FromDateBoxForTest.Inner.Text = "01/04/2005";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "30/04/2005";
                    view.ToDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    Application.DoEvents();

                    // Kiểm số mục, thứ tự dòng, nhãn, không có Tiền giờ, không dòng chú thích.
                    int idxDt = FindRowByCellText(g, "colNoiDung", "DOANH THU");
                    int idxRevNg = FindRowByCellText(g, "colNoiDung", "(Chưa phân nhóm)");
                    int idxRevCf = FindRowByCellText(g, "colNoiDung", "KQKD-01-Cà phê");
                    int idxGg = FindRowByCellText(g, "colNoiDung", "Giảm giá");
                    int idxPdv = FindRowByCellText(g, "colNoiDung", "Phí dịch vụ");
                    int idxThue = FindRowByCellText(g, "colNoiDung", "Thuế");
                    int idxCp = FindRowByCellText(g, "colNoiDung", "CHI PHÍ");
                    int idxDp = FindRowByCellText(g, "colNoiDung", "Định phí");
                    int idxDp1 = FindRowAfter(g, idxDp, "colNoiDung", fx.FixedReason1Name);
                    int idxDp2 = FindRowAfter(g, idxDp, "colNoiDung", fx.FixedReason2Name);
                    int idxDp3 = FindRowAfter(g, idxDp, "colNoiDung", fx.FixedReason3Name);
                    int idxBp = FindRowByCellText(g, "colNoiDung", "Biến phí");
                    int idxVarCf = FindRowAfter(g, idxBp, "colNoiDung", "KQKD-01-Cà phê");
                    int idxCk = FindRowByCellText(g, "colNoiDung", "Chi khác");
                    int idxCk1 = FindRowAfter(g, idxCk, "colNoiDung", fx.OtherReason1Name);
                    int idxCk2 = FindRowAfter(g, idxCk, "colNoiDung", fx.OtherReason2Name);
                    int idxPl = FindRowByCellText(g, "colNoiDung", "LÃI/LỖ");

                    bool orderAndNumbersOk = idxDt == 0
                        && CellText(g, idxDt, "colMuc") == "I."
                        && idxRevNg == 1 && CellText(g, idxRevNg, "colMuc") == "1"
                        && idxRevCf == 2 && CellText(g, idxRevCf, "colMuc") == "2"
                        && idxGg == 5 && CellText(g, idxGg, "colMuc") == "5"
                        && idxPdv == 6 && CellText(g, idxPdv, "colMuc") == "6"
                        && idxThue == 7 && CellText(g, idxThue, "colMuc") == "7"
                        && idxCp == 8 && CellText(g, idxCp, "colMuc") == "II."
                        && idxDp == 9 && CellText(g, idxDp, "colMuc") == "A."
                        && idxDp1 > idxDp && idxDp1 < idxBp
                        && idxDp2 > idxDp && idxDp2 < idxBp
                        && idxDp3 > idxDp && idxDp3 < idxBp
                        && idxBp > idxDp && CellText(g, idxBp, "colMuc") == "B."
                        && idxVarCf > idxBp && idxVarCf < idxCk
                        && idxCk > idxBp && CellText(g, idxCk, "colMuc") == "C."
                        && idxCk1 > idxCk && idxCk1 < idxPl
                        && idxCk2 > idxCk && idxCk2 < idxPl
                        && idxPl == g.Rows.Count - 1 && CellText(g, idxPl, "colMuc") == "III."
                        && FindRowByCellText(g, "colNoiDung", "Tiền giờ") < 0;

                    Check("KQKD UI (§7/C8/Q14 số mục I./1..7/II./A./B./C./III., thứ tự dòng LOAI 0..9, không có Tiền giờ)", orderAndNumbersOk);

                    // Kiểm giá trị thật trong ô lưới + đủ 3 cột tỉ lệ từng loại dòng (§2.1), gồm cả 2 dòng LOAI 4 và cả 2 dòng LOAI 8!
                    // Mẫu số kỳ này: DT_tong = 2.200.000, N_tong = 1.980.000, CP_tong = 1.644.000, DP_tong = 750.000, BP_tong = 614.000, CK_tong = 280.000, PL = 556.000.
                    // Phép tính tay (Math.Round(val * 100 / mẫu, ToEven)):
                    // - RevCF (1.090k): /N = 1.090k/1.980k = 55,05% → 55%
                    // - CP    (1.644k): /DT = 1.644k/2.200k = 74,73% → 75%; col3 = 100%
                    // - DP      (750k): /DT = 750k/2.200k = 34,09% → 34%; /CP = 750k/1.644k = 45,62% → 46%; col3 = 100%
                    // - DP1     (500k): /DT = 500k/2.200k = 22,73% → 23%; /CP = 500k/1.644k = 30,41% → 30%; /DP = 500k/750k = 66,67% → 67%
                    // - DP2     (250k): /DT = 250k/2.200k = 11,36% → 11%; /CP = 250k/1.644k = 15,21% → 15%; /DP = 250k/750k = 33,33% → 33%
                    // - DP3     (0/0) : /DT = "-", /CP = "-", /DP = "-", trend = ""
                    // - BP      (614k): /DT = 614k/2.200k = 27,91% → 28%; /CP = 614k/1.644k = 37,35% → 37%; col3 = 100%
                    // - VarCF   (380k): /DT = 380k/2.200k = 17,27% → 17%; /CP = 380k/1.644k = 23,11% → 23%; /BP = 380k/614k = 61,89% → 62%
                    // - CK      (280k): /DT = 280k/2.200k = 12,73% → 13%; /CP = 280k/1.644k = 17,03% → 17%; col3 = 100%
                    // - CK1     (200k): /DT = 200k/2.200k =  9,09% →  9%; /CP = 200k/1.644k = 12,17% → 12%; /CK = 200k/280k = 71,43% → 71%
                    // - CK2      (80k): /DT =  80k/2.200k =  3,64% →  4%; /CP =  80k/1.644k =  4,87% →  5%; /CK =  80k/280k = 28,57% → 29%
                    // - PL      (556k): /DT trống; col % = 556k/2.200k = 25,27% → 25%; col3 = 556k/1.644k = 33,82% → 34%
                    bool cellsAndRatiosOk =
                        CellText(g, idxDt, "colPctDt") == "" && CellText(g, idxDt, "colGiaTri") == 2200000m.ToString("n0", vi)
                        && CellText(g, idxDt, "colPct") == "100%" && CellText(g, idxDt, "colPctCp") == ""
                        && CellText(g, idxDt, "colTrend") == "▼" && CellText(g, idxDt, "colTangGiam") == (-510000m).ToString("n0", vi)
                        && CellText(g, idxDt, "colThangTruoc") == 2710000m.ToString("n0", vi)
                        && CellText(g, idxRevCf, "colPctDt") == "" && CellText(g, idxRevCf, "colPct") == "55%" && CellText(g, idxRevCf, "colPctCp") == ""
                        && CellText(g, idxRevCf, "colTrend") == "▲"
                        && CellText(g, idxCp, "colPctDt") == "75%" && CellText(g, idxCp, "colPct") == "" && CellText(g, idxCp, "colPctCp") == "100%"
                        && CellText(g, idxCp, "colTrend") == "▲"
                        && CellText(g, idxDp, "colPctDt") == "34%" && CellText(g, idxDp, "colPct") == "46%" && CellText(g, idxDp, "colPctCp") == "100%"
                        && CellText(g, idxDp1, "colPctDt") == "23%" && CellText(g, idxDp1, "colPct") == "30%" && CellText(g, idxDp1, "colPctCp") == "67%"
                        && CellText(g, idxDp2, "colPctDt") == "11%" && CellText(g, idxDp2, "colPct") == "15%" && CellText(g, idxDp2, "colPctCp") == "33%"
                        && CellText(g, idxDp2, "colTrend") == "▲"
                        && CellText(g, idxDp3, "colPctDt") == "-" && CellText(g, idxDp3, "colPct") == "-" && CellText(g, idxDp3, "colPctCp") == "-"
                        && CellText(g, idxDp3, "colTrend") == ""
                        && CellText(g, idxBp, "colPctDt") == "28%" && CellText(g, idxBp, "colPct") == "37%" && CellText(g, idxBp, "colPctCp") == "100%"
                        && CellText(g, idxVarCf, "colPctDt") == "17%" && CellText(g, idxVarCf, "colPct") == "23%" && CellText(g, idxVarCf, "colPctCp") == "62%"
                        && CellText(g, idxCk, "colPctDt") == "13%" && CellText(g, idxCk, "colPct") == "17%" && CellText(g, idxCk, "colPctCp") == "100%"
                        && CellText(g, idxCk1, "colPctDt") == "9%" && CellText(g, idxCk1, "colPct") == "12%" && CellText(g, idxCk1, "colPctCp") == "71%"
                        && CellText(g, idxCk1, "colTrend") == "▲"
                        && CellText(g, idxCk2, "colPctDt") == "4%" && CellText(g, idxCk2, "colPct") == "5%" && CellText(g, idxCk2, "colPctCp") == "29%"
                        && CellText(g, idxCk2, "colTrend") == "▼"
                        && CellText(g, idxPl, "colPctDt") == "" && CellText(g, idxPl, "colGiaTri") == 556000m.ToString("n0", vi)
                        && CellText(g, idxPl, "colPct") == "25%" && CellText(g, idxPl, "colPctCp") == "34%"
                        && CellText(g, idxPl, "colTrend") == "▼" && CellText(g, idxPl, "colTangGiam") == (-524000m).ToString("n0", vi);

                    Check("KQKD UI (§7/§2.1/C7 giá trị thật, n0, đủ 3 cột tỉ lệ gồm cả 2 dòng LOAI 4 & 2 dòng LOAI 8 khác 100%, và ▲/▼/rỗng trên dòng chi phí)",
                        cellsAndRatiosOk);

                    // Kiểm font ĐẬM thật (InheritedStyle.Font.Bold == true) trên ô dòng LOAI 0, 2, 9 và KHÔNG đậm trên dòng chi tiết.
                    bool boldBeforeTheme = IsRowBold(g, idxDt) && IsRowBold(g, idxCp) && IsRowBold(g, idxPl)
                        && !IsRowBold(g, idxRevCf) && !IsRowBold(g, idxDp1);

                    // Đổi theme lúc chạy sang Ocean → kiểm màu thật trên nền, ô header, ô dòng đậm, màu chữ colTrend trùng màu dòng (không tô xanh/đỏ riêng) VÀ dòng đậm vẫn đậm!
                    ThemeManager.Apply(BuiltInThemes.Ocean);
                    Application.DoEvents();
                    ThemeTokens ocean = BuiltInThemes.Ocean;
                    bool boldAfterTheme = IsRowBold(g, idxDt) && IsRowBold(g, idxCp) && IsRowBold(g, idxPl);
                    bool themeColorsOk = view.BackColor == ocean.Background
                        && g.ColumnHeadersDefaultCellStyle.BackColor == Draw.Mix(ocean.Surface, ocean.Primary, 0.18f)
                        && g.Rows[idxPl].DefaultCellStyle.ForeColor == ocean.Primary
                        && g.Rows[idxRevCf].Cells["colTrend"].InheritedStyle.ForeColor == g.Rows[idxRevCf].Cells["colNoiDung"].InheritedStyle.ForeColor
                        && g.Rows[idxDp2].Cells["colTrend"].InheritedStyle.ForeColor == g.Rows[idxDp2].Cells["colNoiDung"].InheritedStyle.ForeColor
                        && g.Rows[idxPl].Cells["colTrend"].InheritedStyle.ForeColor == g.Rows[idxPl].Cells["colNoiDung"].InheritedStyle.ForeColor
                        && g.Rows[idxCk2].Cells["colTrend"].InheritedStyle.ForeColor == g.Rows[idxCk2].Cells["colNoiDung"].InheritedStyle.ForeColor;

                    Check("KQKD UI (§7 dòng đậm LOAI 0/2/9 dùng Font.Bold thật & đổi theme lúc chạy giữ đậm, màu ô đổi theo token và colTrend cùng màu chữ với dòng)",
                        boldBeforeTheme && boldAfterTheme && themeColorsOk,
                        "bold1=" + boldBeforeTheme + ", bold2=" + boldAfterTheme + ", clr=" + themeColorsOk);

                    // Kiểm thiếu ngày → cảnh báo, KHÔNG chạy truy vấn (LoadCount không tăng, dữ liệu lưới không đổi).
                    int countBeforeMissing = view.LoadCountForTest;
                    string plBeforeMissing = CellText(g, idxPl, "colGiaTri");
                    view.FromDateBoxForTest.Inner.Text = "";
                    view.FromDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    bool missingFromOk = view.LastWarningMessageForTest == BusinessResultConstants.MissingDateMessage
                        && view.LoadCountForTest == countBeforeMissing
                        && CellText(g, idxPl, "colGiaTri") == plBeforeMissing;

                    view.FromDateBoxForTest.Inner.Text = "01/04/2005";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "";
                    view.ToDateBoxForTest.Commit();
                    view.LoadButtonForTest.PerformClick();
                    bool missingToOk = view.LastWarningMessageForTest == BusinessResultConstants.MissingDateMessage
                        && view.LoadCountForTest == countBeforeMissing;

                    Check("KQKD UI (§7 thiếu Từ ngày hoặc Đến ngày): cảnh báo và không chạy truy vấn",
                        missingFromOk && missingToOk);

                    // Khôi phục ngày, chọn Store A, gửi phím F5 thật qua ProcessCmdKey → tải lại đúng số Store A (LÃI/LỖ = 586.000).
                    view.ToDateBoxForTest.Inner.Text = "30/04/2005";
                    view.ToDateBoxForTest.Commit();
                    SelectStoreById(view.StoreComboForTest, fx.StoreAId);
                    bool f5Handled = view.DispatchCmdKeyForTest(Keys.F5);
                    Application.DoEvents();
                    int idxPlA = FindRowByCellText(g, "colNoiDung", "LÃI/LỖ");
                    bool f5Ok = f5Handled && idxPlA >= 0
                        && CellText(g, idxPlA, "colGiaTri") == 586000m.ToString("n0", vi)
                        && view.LoadCountForTest == countBeforeMissing + 1;

                    // Nút In báo "chưa hỗ trợ" (không bật hộp thoại chặn selftest).
                    view.PrintButtonForTest.PerformClick();
                    bool printOk = view.LastInfoMessageForTest == BusinessResultConstants.PrintUnsupportedMessage;

                    Check("KQKD UI (§7 phím F5 tải lại đúng dữ liệu mới & nút In báo chưa hỗ trợ không chặn selftest)",
                        f5Ok && printOk, "f5=" + f5Ok + ", pr=" + printOk);
                }
                finally { host.Close(); }
            }

            // 2. C9 chọn sẵn cửa hàng trên UI thật: 0 cửa hàng (trống), 1 cửa hàng (chọn sẵn StoreA), 2 cửa hàng (trống).
            CheckStorePreselect(db, fx.User0StoreId, "kqkd_u0", 1, 0, "");
            CheckStorePreselect(db, fx.User1StoreId, "kqkd_u1", 2, 1, fx.StoreAId);
            CheckStorePreselect(db, fx.User2StoresId, "kqkd_u2", 3, 0, "");

            // 3. preview.businessresult với option bottom: cuộn lưới xuống hiển thị dòng cuối LÃI/LỖ!
            using (Form pvForm = Dialogs.ScreenFactory.Create("preview.businessresult", adminSession))
            {
                var pv = pvForm as Dialogs.BusinessResultPreviewForm;
                bool pvOk = false;
                string pvDetail = "null";
                if (pv != null)
                {
                    pv.StartPosition = FormStartPosition.Manual;
                    pv.ShowInTaskbar = false;
                    pv.Location = new Point(-20000, -20000);
                    pv.Show();
                    Application.DoEvents();
                    try
                    {
                        pv.Preview("from=01/04/2005");
                        pv.Preview("to=30/04/2005");
                        pv.Preview("bottom");
                        pv.PerformLayout();
                        Application.DoEvents();

                        DataGridView g = pv.ViewForTest.GridForTest;
                        int lastIdx = g.Rows.Count - 1;
                        Rectangle lastRect = lastIdx >= 0 ? g.GetRowDisplayRectangle(lastIdx, false) : Rectangle.Empty;
                        pvOk = lastIdx > 0
                            && g.FirstDisplayedScrollingRowIndex > 0
                            && CellText(g, lastIdx, "colNoiDung") == "LÃI/LỖ"
                            && g.Rows[lastIdx].Displayed
                            && lastRect.Height >= g.Rows[lastIdx].Height;
                        pvDetail = "rows=" + g.Rows.Count + ", first=" + g.FirstDisplayedScrollingRowIndex + ", lastH=" + lastRect.Height;
                    }
                    finally { pv.Close(); }
                }
                Check("KQKD (preview.businessresult + option bottom): cuộn lưới xuống dòng cuối III. LÃI/LỖ hiển thị trọn vẹn",
                    pvOk, pvDetail);
            }
        }

        private static void CheckStorePreselect(
            string db,
            string userId,
            string userName,
            int expectedItemCount,
            int expectedSelectedIndex,
            string expectedStoreId)
        {
            var session = new UserSession
            {
                UserId = userId, UserName = userName, DisplayName = userName,
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host = MakeOffscreenHost())
            {
                var view = new BusinessResultView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    var selected = view.StoreComboForTest.SelectedItem as StoreRecord;
                    string actualId = selected == null ? "" : (selected.Id ?? "");
                    Check("KQKD UI (§7/C9 chọn sẵn cửa hàng user " + userName + ": items=" + expectedItemCount + ", idx=" + expectedSelectedIndex + ")",
                        view.StoreComboForTest.Items.Count == expectedItemCount
                        && view.StoreComboForTest.SelectedIndex == expectedSelectedIndex
                        && actualId == expectedStoreId,
                        "items=" + view.StoreComboForTest.Items.Count + ", idx=" + view.StoreComboForTest.SelectedIndex + ", id=" + actualId);
                }
                finally { host.Close(); }
            }
        }

        private static bool IsRowBold(DataGridView grid, int rowIdx)
        {
            if (rowIdx < 0 || rowIdx >= grid.Rows.Count)
                return false;
            Font f = grid.Rows[rowIdx].Cells["colNoiDung"].InheritedStyle.Font;
            return f != null && f.Bold;
        }

        private static int FindRowAfter(DataGridView grid, int startExclusive, string colName, string text)
        {
            for (int r = startExclusive + 1; r < grid.Rows.Count; r++)
                if (CellText(grid, r, colName) == text)
                    return r;
            return -1;
        }

        private static void SelectStoreById(ComboBox combo, string storeId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var s = combo.Items[i] as StoreRecord;
                if (s != null && s.Id == storeId)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
