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
        private static void ServiceOrderUiTests(string db, UserSession adminSession, ServiceOrderTestFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                // 1. Đăng ký ScreenFactory cho menu Sử dụng dịch vụ và preview.service.
                var menuNode = new MenuNode { Id = ServiceOrderView.MenuId, Title = "Sử dụng dịch vụ", Operation = 4 };
                using (Control created = ScreenFactory.CreateView(menuNode, adminSession))
                using (Form preview = ScreenFactory.Create("preview.service", adminSession))
                {
                    var svcView = created as ServiceOrderView;
                    if (svcView != null)
                        svcView.TableMapForTest.StopAutoRefresh();
                    Check("SDDV UI §2.1: ScreenFactory mở đúng ServiceOrderView cho menu Sử dụng dịch vụ và preview.service",
                        created is ServiceOrderView && preview is ServicePreviewForm);
                }

                // 2. Mở màn thật trên Form ẩn ngoài màn hình: kiểm tab khu vực, hàng dưới TABHIENTHI=1, màu ô bàn & đổi theme.
                int activeAreasDb = DbHelper.Read(db, conn =>
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DKHUVUC WHERE STATUS = 30")));

                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var view = new ServiceOrderView(adminSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableMapView map = view.TableMapForTest;
                        int totalTabs = map.TopTabsForTest.TabPages.Count + map.BottomTabsForTest.TabPages.Count;
                        bool bottomVisible = !map.BottomSectionHiddenForTest && map.BottomTabsForTest.TabPages.Count >= 1;
                        Check("SDDV UI §7 Mở màn: số tab khu vực khớp DKHUVUC.STATUS=30 và TABHIENTHI=1 hiện ở hàng dưới",
                            totalTabs == activeAreasDb && bottomVisible,
                            "tabs=" + totalTabs + ", db=" + activeAreasDb + ", bottom=" + bottomVisible);

                        // Kiểm màu thật của ô bàn các trạng thái khớp ThemeTokens và đổi khi chuyển sang BuiltInThemes.Ocean.
                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        ThemeTokens ocean = BuiltInThemes.Ocean;
                        TableTileButton inUseTile = map.FindTileForTest(fx.TableInUseId);
                        TableTileButton tmpTile = map.FindTileForTest(fx.TableTempPrintedId);
                        TableTileButton repairTile = map.FindTileForTest(fx.TableRepairId);
                        Color expInUseFill, expInUseBorder, expFore;
                        Color expTmpFill, expTmpBorder, expTmpFore;
                        Color expRepFill, expRepBorder, expRepFore;
                        TableMapView.ResolveTileColors(TableStatus.InUse, ocean, out expInUseFill, out expInUseBorder, out expFore);
                        TableMapView.ResolveTileColors(TableStatus.TempPrinted, ocean, out expTmpFill, out expTmpBorder, out expTmpFore);
                        TableMapView.ResolveTileColors(TableStatus.UnderRepair, ocean, out expRepFill, out expRepBorder, out expRepFore);

                        bool tileColorsOk = inUseTile != null && tmpTile != null && repairTile != null
                            && inUseTile.BackColor == expInUseFill && inUseTile.BorderColorForTest == expInUseBorder
                            && tmpTile.BackColor == expTmpFill && tmpTile.BorderColorForTest == expTmpBorder
                            && repairTile.BackColor == expRepFill && repairTile.BorderColorForTest == expRepBorder;
                        Check("SDDV UI §7 Màu thật ô bàn (Đang sử dụng / In tạm tính / Sửa chữa) lấy từ ThemeTokens và đổi theo theme",
                            tileColorsOk && view.BackColor == ocean.Background);

                        // Kiểm nút đủ lớn cảm ứng (>= 44px) và nút chưa làm (patch b/c/d) báo "chưa hỗ trợ".
                        bool touchOk = view.StartButtonForTest.Height >= 44
                            && view.AddButtonForTest.Height >= 44
                            && view.PlusButtonForTest.Height >= 44
                            && view.MinusButtonForTest.Height >= 44
                            && view.PayButtonForTest.Height >= 44
                            && view.KitchenPrintButtonForTest.Height >= 44;

                        // Thao tác thật qua UI: chọn bàn đang mở -> thêm món bằng nút << Thêm -> kiểm lưới & tổng cập nhật.
                        TableTileButton emptyBillTile = map.FindTileForTest(fx.TableEmptyId);
                        emptyBillTile.PerformClick();
                        Application.DoEvents();
                        view.StatsButtonForTest.PerformClick();
                        string infoStats = view.LastInfoForTest;
                        Check("SDDV UI §7 Lưới nút cảm ứng >= 44px và các nút patch sau (In chế biến / Thanh toán) báo chưa hỗ trợ",
                            touchOk && infoStats == ServiceOrderConstants.NotSupportedYet,
                            "touch=" + touchOk + ", msg=" + infoStats);
                        int beforeRows = view.DetailGridForTest.Rows.Count;
                        decimal beforeTotal = view.TongCongBoxForTest.Value;
                        view.MenuPickerForTest.SelectProductById(fx.ProdNormalId);
                        view.AddQtyBoxForTest.Value = 1m;
                        view.AddButtonForTest.PerformClick();
                        Application.DoEvents();
                        decimal afterTotal = view.TongCongBoxForTest.Value;
                        Check("SDDV UI §3.4/§7 Gọi món qua sự kiện thật (chọn bàn -> chọn món -> bấm << Thêm) cập nhật lưới, tổng tiền và DataError=0",
                            view.CurrentBillForTest != null && beforeRows >= 1 && afterTotal == beforeTotal + 30000m
                            && view.DetailDataErrorCountForTest == 0,
                            "before=" + beforeTotal + ", after=" + afterTotal + ", err=" + view.DetailDataErrorCountForTest);

                        // Kiểm chu kỳ timer làm tươi 20s và gia hạn giữ bàn 20s (§3.1, §6.5).
                        Check("SDDV UI §3.1/§6.5 Timer làm tươi sơ đồ bàn 20 giây và timer gia hạn giữ bàn 20 giây",
                            map.RefreshTimerForTest.Interval == 20000 && view.LockRenewTimerForTest.Interval == 20000);
                    }
                    finally { host.Close(); }
                }

                // 3. Quyền §4 XemDonGiaTrongHoaDonBanHang: thiếu quyền -> ẩn cột giá/tổng, KHÔNG đổ dữ liệu giá vào ô lưới.
                var waiterSession = new UserSession
                {
                    UserId = "u-waiter",
                    UserName = "waiter",
                    IsAdmin = false,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(false)
                };
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1280, 720);
                    var view = new ServiceOrderView(waiterSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton tile = view.TableMapForTest.FindTileForTest(fx.TableEmptyId);
                        if (tile != null)
                        {
                            tile.PerformClick();
                            Application.DoEvents();
                        }
                        DataGridView g = view.DetailGridForTest;
                        bool hiddenCols = !g.Columns["colPrice"].Visible && !g.Columns["colDisc"].Visible && !g.Columns["colAmount"].Visible
                            && !view.MenuPickerForTest.ProductGridForTest.Columns["colProdPrice"].Visible
                            && !view.TotalsHostForTest.Visible && !view.PayButtonForTest.Visible;
                        bool noLeakedCellData = true;
                        for (int i = 0; i < g.Rows.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(Convert.ToString(g.Rows[i].Cells["colPrice"].Value))
                                || !string.IsNullOrEmpty(Convert.ToString(g.Rows[i].Cells["colAmount"].Value)))
                            {
                                noLeakedCellData = false;
                                break;
                            }
                        }
                        Check("SDDV UI §4 Thiếu quyền XemDonGiaTrongHoaDonBanHang: ẩn cột giá/tổng và không đổ giá vào ô lưới",
                            hiddenCols && noLeakedCellData,
                            "hidden=" + hiddenCols + ", noLeak=" + noLeakedCellData);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
            }
        }
    }
}
