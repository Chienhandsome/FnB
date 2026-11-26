using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void QuoteUiTests(string db, UserSession adminSession, QuoteTestFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            var configRepo = new ConfigRepository(db);
            bool oldTaxCfg = SystemConfig.CoThueSuat;
            bool oldTwoUnitCfg = SystemConfig.SuDung2DonViTinh;

            // Tạo 2 phiếu báo giá mẫu cho màn danh sách (1 có khách, 1 vãng lai).
            var svc = new QuoteService(adminSession);
            var q1 = QuoteRow.NewDefault();
            q1.Name = "BG26/99001";
            q1.Ngay = DateTime.Today.AddDays(-2);
            q1.CustomerId = fx.CustomerId;
            q1.TenKhach = "Khách hàng BG Thử";
            q1.DiaChi = "123 Lê Lợi";
            q1.DienThoai = "0909123456";
            q1.Email = "bg@example.com";
            q1.TienHang = 235000m;
            q1.TienGiamGia = 16000m;
            q1.TongCong = 219000m;
            QuoteSaveResult s1 = svc.SaveQuote(q1, new List<QuoteLine>
            {
                new QuoteLine { ProductId = fx.Product1Id, UnitId = fx.UnitOddId, Quantity = 1m, Price = 235000m, Amount = 235000m }
            });
            if (s1.Ok) fx.CreatedQuoteIds.Add(s1.SavedId);

            var q2 = QuoteRow.NewDefault();
            q2.Name = "BG26/99002";
            q2.Ngay = DateTime.Today;
            q2.TenKhach = "Khách lẻ BG";
            q2.TienHang = 30000m;
            q2.TongCong = 30000m;
            QuoteSaveResult s2 = svc.SaveQuote(q2, new List<QuoteLine>
            {
                new QuoteLine { ProductId = fx.Product2Id, UnitId = fx.UnitOddId, Quantity = 2m, Price = 15000m, Amount = 30000m }
            });
            if (s2.Ok) fx.CreatedQuoteIds.Add(s2.SavedId);

            try
            {
                QuoteListViewTests(adminSession, fx);
                QuoteEditDialogTests(db, adminSession, configRepo, fx);
                QuotePreviewFormTests(adminSession, s1.SavedId);
                QuoteLayoutTests(adminSession, s1.SavedId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = oldTaxCfg ? 30 : 0 },
                    new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = oldTwoUnitCfg ? 30 : 0 }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        private static void QuoteListViewTests(UserSession adminSession, QuoteTestFixture fx)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new QuoteListView(adminSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView g = view.GridForTest;

                    // Mặc định lọc 30 ngày gần nhất + thứ tự 12 cột.
                    bool dateRangeOk = view.FromDateBoxForTest.Value.Date == DateTime.Today.AddDays(-30)
                        && view.ToDateBoxForTest.Value.Date == DateTime.Today;
                    string[] expectedCols =
                    {
                        "Ngày", "Số phiếu", "Tên khách", "Địa chỉ", "Điện thoại",
                        "Tiền hàng", "Tiền giảm giá", "Tổng cộng", "Email", "Thuế", "Phí vận chuyển", "Ghi chú"
                    };
                    bool colsOk = g.Columns.Count == expectedCols.Length;
                    for (int i = 0; colsOk && i < expectedCols.Length; i++)
                        colsOk = g.Columns[i].HeaderText == expectedCols[i];
                    Check("Báo giá UI §7 danh sách: mặc định 30 ngày gần nhất & đúng thứ tự 12 cột",
                        dateRangeOk && colsOk && g.Rows.Count >= 2,
                        "range=" + dateRangeOk + ", cols=" + colsOk + ", rows=" + g.Rows.Count);

                    // Lọc theo Số phiếu & Khách hàng qua nút Lọc (PerformClick).
                    view.NumberBoxForTest.Text = "BG26/99001";
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filterNumOk = g.Rows.Count == 1 && Convert.ToString(g.Rows[0].Cells["colNumber"].Value) == "BG26/99001";

                    view.NumberBoxForTest.Text = "";
                    view.CustomerComboForTest.SelectId(fx.CustomerId);
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool filterCustOk = g.Rows.Count == 1 && Convert.ToString(g.Rows[0].Cells["colCustomer"].Value) == "Khách hàng BG Thử";
                    Check("Báo giá UI §7 bộ lọc Số phiếu & Khách hàng hoạt động qua sự kiện thật",
                        filterNumOk && filterCustOk, "num=" + filterNumOk + ", cust=" + filterCustOk);

                    // Nút Xuất excel và In báo "chưa hỗ trợ"; nút đủ lớn cảm ứng (>= 36px ở 96 DPI).
                    view.ExcelButtonForTest.PerformClick();
                    string excelMsg = view.LastInfoMessageForTest;
                    view.PrintButtonForTest.PerformClick();
                    string printMsg = view.LastInfoMessageForTest;
                    bool touchHeightOk = view.AddButtonForTest.Height >= 36 && view.PrintButtonForTest.Height >= 36;
                    Check("Báo giá UI §7 nút Xuất excel/In báo chưa hỗ trợ & nút đủ lớn cảm ứng",
                        excelMsg == QuoteText.Unsupported && printMsg == QuoteText.Unsupported && touchHeightOk);

                    // Đổi theme lúc chạy -> kiểm màu thật trên nền view và header/ô lưới khớp ThemeTokens.
                    ThemeManager.Apply(BuiltInThemes.Ocean);
                    Application.DoEvents();
                    ThemeTokens ocean = BuiltInThemes.Ocean;
                    bool themeOk = view.BackColor == ocean.Background
                        && g.BackgroundColor == ocean.Surface
                        && g.ColumnHeadersDefaultCellStyle.BackColor == Draw.Mix(ocean.Surface, ocean.Primary, 0.18f)
                        && g.Rows[0].Cells["colNumber"].InheritedStyle.ForeColor == ocean.Text
                        && g.Rows[0].Cells["colNumber"].InheritedStyle.SelectionBackColor == ocean.Primary;
                    Check("Báo giá UI §7 đổi theme cập nhật màu thật trên nền và ô lưới từ ThemeTokens", themeOk);
                }
                finally { host.Close(); }
            }

            // Kiểm quyền chỉ Xem trên UI: khóa nút Thêm/Sửa/Xóa.
            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add(QuoteConstants.FunctionId, QuoteConstants.FunctionName, RoleModes.View);
            var viewOnlySession = new UserSession
            {
                UserId = "u_view", UserName = "u_view", IsAdmin = false,
                DatabasePath = adminSession.DatabasePath, Permissions = viewOnlyPerms
            };
            using (var host = MakeOffscreenHost())
            {
                var view = new QuoteListView(viewOnlySession) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    Check("Báo giá UI §7 quyền chỉ Xem: khóa nút Thêm mới, Chỉnh sửa, Xóa",
                        !view.AddButtonForTest.Enabled && !view.EditButtonForTest.Enabled && !view.RemoveButtonForTest.Enabled);
                }
                finally { host.Close(); }
            }
        }

        private static void QuoteEditDialogTests(string db, UserSession adminSession, ConfigRepository configRepo, QuoteTestFixture fx)
        {
            // Tắt CoThueSuat = 0 để kiểm khóa 2 ô thuế (§5.0.1).
            configRepo.Save(new[]
            {
                new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 30 }
            }, "selftest");
            SystemConfig.Reload(db);

            using (var dlg = new QuoteEditDialog(adminSession, null, null))
            {
                bool defaultMaximized = dlg.WindowState == FormWindowState.Maximized;
                bool enterNotSave = dlg.AcceptButton == null;
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    Check("Báo giá Dialog §1.2/§5.0.1: mở tối đa hóa, Enter không lưu, CoThueSuat=0 khóa 2 ô thuế",
                        defaultMaximized && enterNotSave
                        && !dlg.TileThueBoxForTest.Enabled && !dlg.TienThueBoxForTest.Enabled);

                    // Chọn khách -> tự điền và khóa 4 ô; bỏ chọn -> mở khóa và xóa trống; nhập tay khách vãng lai.
                    dlg.CustomerComboForTest.SelectId(fx.CustomerId);
                    Application.DoEvents();
                    bool lockedOk = dlg.TenKhachBoxForTest.Text == "Khách hàng BG Thử"
                        && dlg.DiaChiBoxForTest.Text == "123 Lê Lợi"
                        && dlg.DienThoaiBoxForTest.Text == "0909123456"
                        && dlg.EmailBoxForTest.Text == "bg@example.com"
                        && !dlg.TenKhachBoxForTest.Enabled && !dlg.DiaChiBoxForTest.Enabled
                        && !dlg.DienThoaiBoxForTest.Enabled && !dlg.EmailBoxForTest.Enabled;

                    dlg.CustomerComboForTest.SelectedIndex = 0;
                    Application.DoEvents();
                    bool unlockedOk = dlg.TenKhachBoxForTest.Text == "" && dlg.DiaChiBoxForTest.Text == ""
                        && dlg.DienThoaiBoxForTest.Text == "" && dlg.EmailBoxForTest.Text == ""
                        && dlg.TenKhachBoxForTest.Enabled && dlg.DiaChiBoxForTest.Enabled
                        && dlg.DienThoaiBoxForTest.Enabled && dlg.EmailBoxForTest.Enabled;

                    dlg.TenKhachBoxForTest.Text = "Khách vãng lai nhập tay";
                    Check("Báo giá Dialog §7 chọn khách tự điền & khóa 4 ô; bỏ chọn mở khóa & xóa trống; nhập tay được",
                        lockedOk && unlockedOk && dlg.TenKhachBoxForTest.Text == "Khách vãng lai nhập tay");

                    // Kiểm hộp QuoteQuantityDialog thật: khóa ô chiết khấu (§2.1).
                    var twoUnitItem = new QuoteSaleItem
                    {
                        Id = fx.Product2Id, Name = "Mặt hàng BG 2 ĐVT",
                        UnitId = fx.UnitOddId, UnitName = "LonBG", SellPrice = 15000m,
                        BigUnitId = fx.UnitEvenId, BigUnitName = "ThùngBG", BigSellPrice = 340000m
                    };
                    using (var qdlg = new QuoteQuantityDialog(twoUnitItem, 2m))
                    {
                        qdlg.StartPosition = FormStartPosition.Manual;
                        qdlg.Location = new Point(-20000, -20000);
                        qdlg.Show();
                        Application.DoEvents();
                        bool discLocked = !qdlg.DiscountBoxForTest.Enabled && qdlg.DiscountBoxForTest.ReadOnly
                            && qdlg.OddBoxForTest.Value == 2m && qdlg.EvenBoxForTest.Enabled;
                        qdlg.Close();
                        Check("Báo giá Dialog §2.1 hộp Nhập số lượng 2 ĐVT khóa ô chiết khấu", discLocked);
                    }

                    // Thêm mặt hàng 2 ĐVT đi qua dialog (outer=2, lẻ hộp=5, chẵn hộp=1) -> dòng lẻ lấy ô SL=2, dòng chẵn=1.
                    SelectProductRow(dlg.ProductGridForTest, fx.Product2Id);
                    dlg.QtyBoxForTest.Value = 2m;
                    dlg.QuantityPromptOverrideForTest = (it, q) => new QuoteQuantityPromptResult
                    {
                        Confirmed = true, OddQuantity = 5m, EvenQuantity = 1m
                    };
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool twoUnitAddedOk = dlg.LinesForTest.Count == 2
                        && dlg.LinesForTest[0].UnitId == fx.UnitOddId && dlg.LinesForTest[0].Quantity == 2m
                        && dlg.LinesForTest[1].UnitId == fx.UnitEvenId && dlg.LinesForTest[1].Quantity == 1m;
                    Check("Báo giá Dialog §5.0.2 thêm hàng 2 ĐVT qua UI thật: dòng lẻ=2 (ô SL), dòng chẵn=1", twoUnitAddedOk);

                    // Sửa trực tiếp trên lưới chi tiết SL = 0 và SL = -5 -> bị chặn và trả về giá trị cũ (2).
                    dlg.CommitDetailCellEditForTest(0, "d_qty", 0m);
                    bool zeroBlocked = dlg.LinesForTest[0].Quantity == 2m
                        && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 2m
                        && dlg.LastWarningMessageForTest == QuoteText.EnterQuantity;
                    dlg.CommitDetailCellEditForTest(0, "d_qty", -5m);
                    bool negBlocked = dlg.LinesForTest[0].Quantity == 2m
                        && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 2m;
                    Check("Báo giá Dialog §5.0 P3 sửa trực tiếp lưới chi tiết SL <= 0 bị chặn và khôi phục giá trị cũ",
                        zeroBlocked && negBlocked);

                    // Nhập từ Excel KHÔNG gộp với dòng có sẵn.
                    var imp = new QuoteExcelImportResult
                    {
                        Ok = true,
                        Lines = new List<QuoteLine>
                        {
                            new QuoteLine
                            {
                                ProductId = fx.Product2Id, ProductCode = "BGITEM2", ProductName = "Mặt hàng BG 2 ĐVT",
                                UnitId = fx.UnitOddId, UnitName = "LonBG", Quantity = 3m, Price = 15000m, Amount = 45000m
                            }
                        }
                    };
                    dlg.ApplyExcelImportResult(imp);
                    Check("Báo giá Dialog §F dòng nhập từ Excel không gộp với dòng có sẵn (2 -> 3 dòng)",
                        dlg.LinesForTest.Count == 3 && dlg.LinesForTest[0].Quantity == 2m && dlg.LinesForTest[2].Quantity == 3m);
                }
                finally { dlg.Close(); }
            }

            // r4: phiếu có dòng lẻ 2, hộp trả lẻ 0 chẵn 0, ô SL 2 -> dòng thành 4, Tiền hàng & Tổng cộng cập nhật đúng.
            using (var dlgZ = new QuoteEditDialog(adminSession, null, null))
            {
                dlgZ.SuppressPopup = true;
                dlgZ.WindowState = FormWindowState.Normal;
                dlgZ.StartPosition = FormStartPosition.Manual;
                dlgZ.Location = new Point(-20000, -20000);
                dlgZ.Show();
                Application.DoEvents();
                try
                {
                    SelectProductRow(dlgZ.ProductGridForTest, fx.Product2Id);
                    dlgZ.QtyBoxForTest.Value = 2m;
                    dlgZ.QuantityPromptOverrideForTest = (it, q) => new QuoteQuantityPromptResult { Confirmed = true, OddQuantity = 2m, EvenQuantity = 0m };
                    dlgZ.AddProductButtonForTest.PerformClick();
                    dlgZ.QuantityPromptOverrideForTest = (it, q) => new QuoteQuantityPromptResult { Confirmed = true, OddQuantity = 0m, EvenQuantity = 0m };
                    dlgZ.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();
                    Check("Báo giá Dialog (r4 lẻ 0 chẵn 0 khi đã có dòng lẻ 2, ô SL 2): thành 4, Tiền hàng & Tổng cộng = 60000",
                        dlgZ.LinesForTest.Count == 1 && dlgZ.LinesForTest[0].Quantity == 4m
                        && dlgZ.TienHangBoxForTest.Value == 60000m && dlgZ.TongCongBoxForTest.Value == 60000m
                        && dlgZ.LastWarningMessageForTest == null,
                        "cnt=" + dlgZ.LinesForTest.Count + ", th=" + dlgZ.TienHangBoxForTest.Value + ", tc=" + dlgZ.TongCongBoxForTest.Value);
                }
                finally { dlgZ.Close(); }
            }

            // Bật CoThueSuat = 30 -> kiểm ca §5.0 qua control thật trên QuoteEditDialog: 235000, giảm 7%, thuế 8% -> 16000 / 18000 / 237000.
            configRepo.Save(new[]
            {
                new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = 30 }
            }, "selftest");
            SystemConfig.Reload(db);

            using (var dlg = new QuoteEditDialog(adminSession, null, null))
            {
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    SelectProductRow(dlg.ProductGridForTest, fx.Product1Id);
                    // SL = 0 -> cảnh báo "Mời bạn nhập số lượng"
                    dlg.QtyBoxForTest.Value = 0m;
                    dlg.AddProductButtonForTest.PerformClick();
                    bool sl0Warn = dlg.LinesForTest.Count == 0 && dlg.LastWarningMessageForTest == QuoteText.EnterQuantity;

                    dlg.QtyBoxForTest.Value = 1m;
                    dlg.AddProductButtonForTest.PerformClick();
                    dlg.TileGiamGiaBoxForTest.Value = 7m;
                    dlg.TileThueBoxForTest.Value = 8m;
                    dlg.PhiVanChuyenBoxForTest.Value = 0m;
                    Application.DoEvents();

                    bool calcOk = dlg.TileThueBoxForTest.Enabled
                        && dlg.TienHangBoxForTest.Value == 235000m
                        && dlg.TienGiamGiaBoxForTest.Value == 16000m
                        && dlg.TienThueBoxForTest.Value == 18000m
                        && dlg.TongCongBoxForTest.Value == 237000m
                        && !dlg.DiscountByMoneyForTest;

                    // Chuyển sang giảm theo tiền -> GIAMTHEOTIEN = true (30), lưu và mở lại kiểm chế độ giữ nguyên.
                    dlg.TienGiamGiaBoxForTest.Value = 20000m;
                    Application.DoEvents();
                    bool switchedToMoney = dlg.DiscountByMoneyForTest;
                    bool savedOk = dlg.DoSave();
                    if (savedOk && dlg.SavedId != null)
                        fx.CreatedQuoteIds.Add(dlg.SavedId);

                    bool reopenedMoneyMode = false;
                    if (savedOk && dlg.SavedId != null)
                    {
                        using (var reopen = new QuoteEditDialog(adminSession, dlg.SavedId, null))
                        {
                            reopen.SuppressPopup = true;
                            reopenedMoneyMode = reopen.DiscountByMoneyForTest && reopen.TienGiamGiaBoxForTest.Value == 20000m;
                        }
                    }

                    Check("Báo giá Dialog §5.0 bật CoThueSuat=30: SL=0 cảnh báo, 235000/7%/8%->16000/18000/237000, lưu & mở lại đúng cờ GIAMTHEOTIEN=30",
                        sl0Warn && calcOk && switchedToMoney && savedOk && reopenedMoneyMode,
                        "sl0=" + sl0Warn + ", calc=" + calcOk + ", sw=" + switchedToMoney + ", save=" + savedOk + ", reopen=" + reopenedMoneyMode);
                }
                finally { dlg.Close(); }
            }

            // r2 #4: ca 235000 giảm tiền 300000, thuế 8% qua dialog thật -> không ép thuế/tổng âm về 0, không ép % về 100;
            // mở lại hiện đúng số âm/tỉ lệ thật và không tự đổi số (Dirty = false); kiểm luôn số vượt cỡ ô không ném ngoại lệ.
            using (var dlgNeg = new QuoteEditDialog(adminSession, null, null))
            {
                dlgNeg.SuppressPopup = true;
                dlgNeg.WindowState = FormWindowState.Normal;
                dlgNeg.StartPosition = FormStartPosition.Manual;
                dlgNeg.Location = new Point(-20000, -20000);
                dlgNeg.Show();
                Application.DoEvents();
                try
                {
                    SelectProductRow(dlgNeg.ProductGridForTest, fx.Product1Id);
                    dlgNeg.QtyBoxForTest.Value = 1m;
                    dlgNeg.AddProductButtonForTest.PerformClick();
                    dlgNeg.TienGiamGiaBoxForTest.Value = 300000m;
                    dlgNeg.TileThueBoxForTest.Value = 8m;
                    dlgNeg.PhiVanChuyenBoxForTest.Value = 0m;
                    Application.DoEvents();

                    QuoteTotals expected = QuoteCalculator.CalcTotalsFromSubtotal(235000m, 0m, 300000m, true, 8m, 0m, SystemConfig.LamTronTien);
                    decimal expectedPct2 = Math.Round(expected.TileGiamGia, 2, MidpointRounding.AwayFromZero);

                    bool uiDisplayOk = dlgNeg.DiscountByMoneyForTest
                        && dlgNeg.TienGiamGiaBoxForTest.Value == expected.TienGiamGia
                        && dlgNeg.TienThueBoxForTest.Value == expected.TienThue
                        && dlgNeg.TongCongBoxForTest.Value == expected.TongCong
                        && Math.Round(dlgNeg.TileGiamGiaBoxForTest.Value, 2, MidpointRounding.AwayFromZero) == expectedPct2;

                    bool saveNegOk = dlgNeg.DoSave();
                    if (saveNegOk && dlgNeg.SavedId != null)
                        fx.CreatedQuoteIds.Add(dlgNeg.SavedId);

                    QuoteRow dbNeg = saveNegOk ? new QuoteService(adminSession).LoadQuote(dlgNeg.SavedId) : null;
                    bool dbMatchOk = dbNeg != null
                        && dbNeg.GiamTheoTien
                        && dbNeg.TienHang == expected.TienHang
                        && dbNeg.TienGiamGia == expected.TienGiamGia
                        && dbNeg.TienThue == expected.TienThue
                        && dbNeg.TongCong == expected.TongCong
                        && Math.Abs(dbNeg.TileGiamGia - expectedPct2) <= 0.01m;

                    bool reopenNegOk = false;
                    if (saveNegOk && dlgNeg.SavedId != null)
                    {
                        using (var reopenNeg = new QuoteEditDialog(adminSession, dlgNeg.SavedId, null))
                        {
                            reopenNeg.SuppressPopup = true;
                            reopenNeg.WindowState = FormWindowState.Normal;
                            reopenNeg.StartPosition = FormStartPosition.Manual;
                            reopenNeg.Location = new Point(-20000, -20000);
                            reopenNeg.Show();
                            Application.DoEvents();
                            reopenNegOk = reopenNeg.DiscountByMoneyForTest
                                && !reopenNeg.DirtyForTest
                                && reopenNeg.TienThueBoxForTest.Value == expected.TienThue
                                && reopenNeg.TongCongBoxForTest.Value == expected.TongCong
                                && Math.Abs(reopenNeg.TileGiamGiaBoxForTest.Value - expectedPct2) <= 0.01m;
                            reopenNeg.Close();
                        }
                    }

                    // Kiểm số quá cỡ NumericUpDown không ném ngoại lệ và biến _totals vẫn giữ giá trị thật.
                    bool overflowSafe = false;
                    try
                    {
                        dlgNeg.CommitDetailCellEditForTest(0, "d_price", 2000000000000m);
                        overflowSafe = dlgNeg.TotalsForTest.TienHang == 2000000000000m;
                    }
                    catch (Exception)
                    {
                        overflowSafe = false;
                    }

                    Check("Báo giá Dialog §5.0 giảm tiền 300000 trên 235000 thuế 8%: lưu/đọc/mở lại đúng số âm & tỉ lệ thật, quá cỡ không ném lỗi",
                        uiDisplayOk && saveNegOk && dbMatchOk && reopenNegOk && overflowSafe,
                        "ui=" + uiDisplayOk + ", save=" + saveNegOk + ", db=" + dbMatchOk + ", reopen=" + reopenNegOk + ", ovf=" + overflowSafe);
                }
                finally { dlgNeg.Close(); }
            }
        }

        private static void QuotePreviewFormTests(UserSession adminSession, string sampleQuoteId)
        {
            using (Form pv = ScreenFactory.Create("preview.quote", adminSession))
            {
                var qpv = pv as QuotePreviewForm;
                bool ok = false;
                if (qpv != null)
                {
                    qpv.StartPosition = FormStartPosition.Manual;
                    qpv.Location = new Point(-20000, -20000);
                    qpv.Show();
                    qpv.Preview("select=0");
                    Application.DoEvents();
                    ok = qpv.ViewForTest.GridForTest.Rows.Count >= 2;
                    qpv.Close();
                }
                Check("Báo giá preview.quote dựng được màn danh sách cho --render-screen", ok);
            }

            using (Form pvEdit = ScreenFactory.Create("preview.quoteedit", adminSession))
            {
                var dlg = pvEdit as QuoteEditDialog;
                bool ok = false;
                if (dlg != null)
                {
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-20000, -20000);
                    dlg.Show();
                    if (!string.IsNullOrEmpty(sampleQuoteId))
                        dlg.Preview("id=" + sampleQuoteId);
                    Application.DoEvents();
                    ok = dlg.NumberBoxForTest.Text == "BG26/99001" && dlg.LinesForTest.Count == 1;
                    dlg.Close();
                }
                Check("Báo giá preview.quoteedit dựng được dialog sửa cho --render-screen", ok);
            }
        }

        private static void SelectProductRow(DataGridView grid, string productId)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var item = grid.Rows[i].Tag as QuoteSaleItem;
                if (item != null && item.Id == productId)
                {
                    grid.ClearSelection();
                    grid.Rows[i].Selected = true;
                    grid.CurrentCell = grid.Rows[i].Cells[0];
                    return;
                }
            }
        }
    }
}
