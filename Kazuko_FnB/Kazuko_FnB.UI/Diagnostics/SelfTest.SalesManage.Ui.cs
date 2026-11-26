using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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
        private static void SalesManageUiTests(string db, ConfigRepository cfgRepo, UserSession admin, SalesManageTestFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                // 1. ScreenFactory & MainMenuBuilder phân quyền hiển thị 2 menu (§1, §7)
                Control manageCtrl = ScreenFactory.CreateView(new MenuNode { Id = SalesManageView.ManageMenuId }, admin);
                Control adjustCtrl = ScreenFactory.CreateView(new MenuNode { Id = SalesManageView.AdjustMenuId }, admin);
                Form pManage = ScreenFactory.Create("preview.salesmanage", admin);
                Form pAdjust = ScreenFactory.Create("preview.billadjust", admin);
                bool factoryOk = manageCtrl is SalesManageView && ((SalesManageView)manageCtrl).ModeForTest == BillManageMode.Manage
                    && adjustCtrl is SalesManageView && ((SalesManageView)adjustCtrl).ModeForTest == BillManageMode.Adjust
                    && pManage is IScreenPreview && pAdjust is IScreenPreview;
                if (manageCtrl != null) manageCtrl.Dispose();
                if (adjustCtrl != null) adjustCtrl.Dispose();
                if (pManage != null) pManage.Dispose();
                if (pAdjust != null) pAdjust.Dispose();

                var emptyPerms = new UserSession { UserId = "u-menu-none", UserName = "none", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuModel mNone = MainMenuBuilder.Build(emptyPerms);
                MenuModel mAdmin = MainMenuBuilder.Build(admin);
                bool menuVisibilityOk = !MenuContainsId(mNone.Roots, SalesManageConstants.ManageMenuId)
                    && !MenuContainsId(mNone.Roots, SalesManageConstants.AdjustMenuId)
                    && MenuContainsId(mAdmin.Roots, SalesManageConstants.ManageMenuId)
                    && MenuContainsId(mAdmin.Roots, SalesManageConstants.AdjustMenuId);

                Check("QLBH UI §1/§7 ScreenFactory tạo đúng 2 màn/preview & MainMenuBuilder ẩn/hiện 2 menu theo quyền SFUNCTION",
                    factoryOk && menuVisibilityOk,
                    "factory=" + factoryOk + ", menu=" + menuVisibilityOk);

                // 2. Quản lý bán hàng thật trên Form offscreen: theme tô hàng chẵn, debounce tải bill, sửa trong bộ nhớ không đổi DB, P3 cảnh báo rời bill/đóng tab, bill nhóm chỉ xem, In lại bill.
                SetSalesManageConfigInt(db, cfgRepo, fx, "HienThiTruocKhiIn", 0);
                SetSalesManageConfigInt(db, cfgRepo, fx, "LuaChonMauKhiIn", 0);
                SetSalesManageConfigInt(db, cfgRepo, fx, "SoLanIn", 1);

                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1400, 780);
                    var view = new SalesManageView(admin, BillManageMode.Manage) { Dock = DockStyle.Fill, SuppressPopup = true };
                    var printRec = new ServicePrintOutputRecorder();
                    view.PrintOutputHandlerForTest = printRec;
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        bool manageControlsOk = view.FromDateBoxForTest.Enabled && view.ToDateBoxForTest.Enabled
                            && view.CancelBillButtonForTest.Visible && view.MenuPickerForTest.Visible
                            && view.PlusButtonForTest.Visible && view.ReprintButtonForTest.Visible
                            && view.BillGridForTest.AllowUserToOrderColumns;

                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        Color expectedAlt = SalesManageView.ResolveAlternatingRowColor(BuiltInThemes.Ocean);
                        bool altColorOk = view.BillGridForTest.AlternatingRowsDefaultCellStyle.BackColor == expectedAlt;

                        // Bỏ chọn hết -> pane phải khóa.
                        view.BillGridForTest.ClearSelection();
                        Application.DoEvents();
                        bool emptyLocked = !view.RightPaneForTest.Enabled;

                        // Chọn Bill1 -> đợi timer 120ms nạp bill vào pane phải.
                        SelectBillRow(view.BillGridForTest, fx.Bill1Id);
                        PumpUntil(() => view.WorkingBillForTest != null && view.WorkingBillForTest.Id == fx.Bill1Id, 1200);
                        bool bill1Loaded = view.RightPaneForTest.Enabled
                            && view.WorkingBillForTest != null && view.WorkingBillForTest.Id == fx.Bill1Id
                            && view.DetailGridForTest.Rows.Count > 0;

                        // Bấm nút + trên giao diện -> chỉ sửa trong bộ nhớ, DB không đổi.
                        decimal dbBeforePlus = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT TONGCONG FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));
                        decimal uiBeforePlus = view.TongCongBoxForTest.Value;
                        view.DetailGridForTest.Rows[0].Selected = true;
                        view.DetailGridForTest.CurrentCell = view.DetailGridForTest.Rows[0].Cells["colItemName"];
                        view.PlusButtonForTest.PerformClick();
                        Application.DoEvents();
                        decimal dbAfterPlus = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT TONGCONG FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));
                        bool editInMemOk = view.IsDirtyForTest && view.TongCongBoxForTest.Value > uiBeforePlus && dbAfterPlus == dbBeforePlus;

                        // P3: chuyển sang Bill2 khi đang dirty và người dùng chọn Cancel -> giữ nguyên Bill1; ConfirmClose trả false.
                        view.ConfirmUnsavedOverrideForTest = () => false;
                        SelectBillRow(view.BillGridForTest, fx.Bill2Id);
                        Application.DoEvents();
                        bool stayedOnBill1 = view.UnsavedPromptTriggeredForTest && view.WorkingBillForTest.Id == fx.Bill1Id;
                        bool closeBlocked = !view.ConfirmClose();

                        // Bấm In lại bill thật -> gọi ServiceInvoicePrintCoordinator với bản in chính thức.
                        view.ReprintButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool reprintOk = printRec.PrintCalls.Count == 1
                            && PageTextsContain(printRec.PrintCalls[0].PageTexts, ServicePrintConstants.InvoiceTitleOfficial);

                        // Chấp nhận bỏ sửa -> chuyển sang GroupBill -> hiện banner, khóa sửa/hủy/cập nhật nhưng vẫn bật In lại bill.
                        view.ConfirmUnsavedOverrideForTest = () => true;
                        SelectBillRow(view.BillGridForTest, fx.GroupBillId);
                        PumpUntil(() => view.WorkingBillForTest != null && view.WorkingBillForTest.Id == fx.GroupBillId, 1200);
                        bool groupUiOk = view.GroupBannerForTest.Visible
                            && !view.UpdateButtonForTest.Enabled && !view.CancelBillButtonForTest.Enabled
                            && !view.PlusButtonForTest.Enabled && view.ReprintButtonForTest.Enabled;

                        Check("QLBH UI §1/§2/§5 P3/§6.3/§7 Màn Quản lý bán hàng thật: điều khiển theo chế độ, màu hàng chẵn theo theme, sửa trong bộ nhớ không đổi DB, cảnh báo rời bill/đóng tab, in lại bill và bill nhóm chỉ xem",
                            manageControlsOk && altColorOk && emptyLocked && bill1Loaded && editInMemOk
                            && stayedOnBill1 && closeBlocked && reprintOk && groupUiOk,
                            "ctrl=" + manageControlsOk + ", alt=" + altColorOk + ", mem=" + editInMemOk + ", p3=" + (stayedOnBill1 && closeBlocked) + ", prt=" + reprintOk + ", grp=" + groupUiOk);
                    }
                    finally { host.Close(); }
                }

                // 3. PaymentDialog (nạp sẵn CK/tài khoản cũ trên Bill1 & ẩn In thử bill) + RefundDepositDialog (§6.4 cảnh báo đã chi đặt trước).
                var adjSvc = new BillAdjustService(admin);
                var tblSvc = new TableService(admin);
                PaidBillDetail d1 = adjSvc.LoadPaidBillDetail(fx.Bill1Id);
                PaymentDialogContext editCtx = adjSvc.PrepareUpdatePayment(d1.Header, BillManageMode.Manage);
                bool payDlgOk = false;
                using (var pDlg = new PaymentDialog(tblSvc, admin, editCtx))
                {
                    pDlg.SuppressPopup = true;
                    pDlg.StartPosition = FormStartPosition.Manual;
                    pDlg.Location = new Point(-3000, -3000);
                    pDlg.Show();
                    Application.DoEvents();
                    try
                    {
                        payDlgOk = !pDlg.TempPrintButtonForTest.Visible
                            && pDlg.BankComboForTest.SelectedId == fx.BankId
                            && pDlg.TransferBoxForTest.Value == 50000m;
                    }
                    finally { if (!pDlg.IsDisposed) pDlg.Close(); }
                }

                PaidBillDetail d2 = adjSvc.LoadPaidBillDetail(fx.Bill2Id);

                var refundPay = new PaymentExecutionResult
                {
                    Ok = true,
                    PrimaryBillId = fx.Bill2Id,
                    PaidBillNames = d2.Header.Name,
                    PaymentDate = fx.BusinessDate,
                    CustomerId = fx.Customer1Id,
                    CustomerName = "Khach Bac " + fx.Tag,
                    StoreId = fx.StoreId,
                    ExcessDeposit = 150000m,
                    AlreadyRefundedDeposit = 100000m,
                    RefundDepositWarning = SalesManageConstants.AlreadyRefundedDepositWarning(100000m, 150000m)
                };
                bool refundDlgOk = false;
                using (var rDlg = new RefundDepositDialog(tblSvc, refundPay, editCtx.BankAccounts))
                {
                    rDlg.SuppressPopup = true;
                    rDlg.ConfirmDismissOverrideForTest = () => true;
                    rDlg.StartPosition = FormStartPosition.Manual;
                    rDlg.Location = new Point(-3000, -3000);
                    rDlg.Show();
                    Application.DoEvents();
                    try
                    {
                        refundDlgOk = rDlg.AmountBoxForTest.Value == 150000m
                            && rDlg.RefundWarningVisibleForTest
                            && rDlg.RefundWarningTextForTest.Contains("100.000");
                    }
                    finally { if (!rDlg.IsDisposed) rDlg.Close(); }
                }

                Check("QLBH UI §2.2/§6.4/§7 PaymentDialog sửa bill nạp sẵn tài khoản & ẩn In thử bill; RefundDepositDialog hiện số tiền cọc dư và cảnh báo đã chi trước đó",
                    payDlgOk && refundDlgOk,
                    "payDlg=" + payDlgOk + ", refundDlg=" + refundDlgOk);

                // 4. Điều chỉnh hóa đơn thật: khóa ngày, ẩn Hủy & In lại (F8 không chạy), ẩn MenuPicker & nút thêm/xóa, mở Trả đồ, bật/tắt Khách hàng theo quyền.
                var pAdjCust = new FunctionPermissions(false);
                pAdjCust.Add(SalesManageConstants.FuncDieuChinhHoaDon, "Điều chỉnh hóa đơn", RoleModes.View | RoleModes.Edit);
                pAdjCust.Add(SalesManageConstants.FuncThayDoiKhachHangDieuChinh, "Đổi KH", RoleModes.View | RoleModes.Edit);
                pAdjCust.Add(ServiceOrderConstants.FuncXemDonGia, "Xem đơn giá", RoleModes.View);
                var sAdjCust = new UserSession { UserId = "u-ui-adj1", UserName = "ui_adj1", IsAdmin = false, DatabasePath = db, Permissions = pAdjCust };

                var pAdjNoCust = new FunctionPermissions(false);
                pAdjNoCust.Add(SalesManageConstants.FuncDieuChinhHoaDon, "Điều chỉnh hóa đơn", RoleModes.View | RoleModes.Edit);
                pAdjNoCust.Add(ServiceOrderConstants.FuncXemDonGia, "Xem đơn giá", RoleModes.View);
                var sAdjNoCust = new UserSession { UserId = "u-ui-adj2", UserName = "ui_adj2", IsAdmin = false, DatabasePath = db, Permissions = pAdjNoCust };

                bool adjWithCustOk = false;
                bool adjNoCustOk = false;
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var v1 = new SalesManageView(sAdjCust, BillManageMode.Adjust) { Dock = DockStyle.Fill, SuppressPopup = true };
                    var recAdj = new ServicePrintOutputRecorder();
                    v1.PrintOutputHandlerForTest = recAdj;
                    host.Controls.Add(v1);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        SelectBillRow(v1.BillGridForTest, fx.Bill1Id);
                        PumpUntil(() => v1.WorkingBillForTest != null && v1.WorkingBillForTest.Id == fx.Bill1Id, 1200);
                        v1.DispatchKeyForTest(Keys.F8);
                        Application.DoEvents();
                        adjWithCustOk = !v1.FromDateBoxForTest.Enabled && !v1.ToDateBoxForTest.Enabled
                            && !v1.CancelBillButtonForTest.Visible && !v1.ReprintButtonForTest.Visible
                            && recAdj.PrintCalls.Count == 0
                            && !v1.MenuPickerForTest.Visible && !v1.PlusButtonForTest.Visible
                            && !v1.TileGiamBoxForTest.Enabled && v1.ReturnButtonForTest.Visible
                            && v1.CustomerComboForTest.Enabled;
                    }
                    finally { host.Close(); }
                }
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var v2 = new SalesManageView(sAdjNoCust, BillManageMode.Adjust) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(v2);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        SelectBillRow(v2.BillGridForTest, fx.Bill1Id);
                        PumpUntil(() => v2.WorkingBillForTest != null && v2.WorkingBillForTest.Id == fx.Bill1Id, 1200);
                        adjNoCustOk = !v2.CustomerComboForTest.Enabled;
                    }
                    finally { host.Close(); }
                }

                Check("QLBH UI §1.1/§7 Màn Điều chỉnh hóa đơn thật: khóa ngày, ẩn Hủy/In lại (F8 không in), ẩn thêm-xóa/tổng tiền, mở Trả đồ, bật/tắt Khách hàng đúng quyền",
                    adjWithCustOk && adjNoCustOk,
                    "withCust=" + adjWithCustOk + ", noCust=" + adjNoCustOk);

                // 5. ReturnItemsDialog thật: sửa ô SL trả lại / vượt SL gọi -> tự cân bằng, không DataError.
                var retRows = new List<ReturnableItemRow>
                {
                    new ReturnableItemRow { ProductId = "p1", ItemName = "Bia", UnitName = "Lon", UnitPrice = 50000m, DiscountRate = 0m, OrderQty = 5m, UsedQty = 5m, ReturnQty = 0m }
                };
                bool returnDlgOk = false;
                using (var dlg = new ReturnItemsDialog("HD-RET", retRows, true))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        dlg.CommitCellEditForTest(0, "colReturn", "2");
                        bool step1 = retRows[0].ReturnQty == 2m && retRows[0].UsedQty == 3m;
                        dlg.CommitCellEditForTest(0, "colReturn", "99");
                        bool step2 = retRows[0].ReturnQty == 0m && retRows[0].UsedQty == 5m;
                        dlg.CommitCellEditForTest(0, "colUsed", "3");
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        returnDlgOk = step1 && step2 && dlg.DataErrorCountForTest == 0
                            && dlg.ConfirmedReturns.Count == 1 && dlg.ConfirmedReturns[0].ReturnQty == 2m;
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                Check("QLBH UI §2.3/§7 ReturnItemsDialog thật: cân bằng SL sử dụng + SL trả lại, nhập vượt về 0, DataError = 0",
                    returnDlgOk);

                // 6. Thiếu quyền XemDonGiaTrongHoaDonBanHang: ẩn mọi cột tiền và không rò giá trị vào ô lưới trái/phải.
                var pNoPrice = new FunctionPermissions(false);
                pNoPrice.Add(SalesManageConstants.FuncQuanLyBanHang, "Quản lý bán hàng", RoleModes.View | RoleModes.Edit);
                var sNoPrice = new UserSession { UserId = "u-ui-noprice", UserName = "ui_noprice", IsAdmin = false, DatabasePath = db, Permissions = pNoPrice };
                bool hidePriceOk = false;
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var vNoPrice = new SalesManageView(sNoPrice, BillManageMode.Manage) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(vNoPrice);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        SelectBillRow(vNoPrice.BillGridForTest, fx.Bill1Id);
                        PumpUntil(() => vNoPrice.WorkingBillForTest != null && vNoPrice.WorkingBillForTest.Id == fx.Bill1Id, 1200);
                        bool leftColsHidden = !vNoPrice.BillGridForTest.Columns["colTongCong"].Visible
                            && !vNoPrice.BillGridForTest.Columns["colTienHang"].Visible;
                        bool leftCellsBlank = true;
                        foreach (DataGridViewRow r in vNoPrice.BillGridForTest.Rows)
                        {
                            if (!string.IsNullOrEmpty(Convert.ToString(r.Cells["colTongCong"].Value)))
                                leftCellsBlank = false;
                        }
                        bool detailColsHidden = !vNoPrice.DetailGridForTest.Columns["colPrice"].Visible
                            && !vNoPrice.DetailGridForTest.Columns["colAmount"].Visible;
                        bool detailCellsBlank = true;
                        foreach (DataGridViewRow r in vNoPrice.DetailGridForTest.Rows)
                        {
                            if (!string.IsNullOrEmpty(Convert.ToString(r.Cells["colPrice"].Value))
                                || !string.IsNullOrEmpty(Convert.ToString(r.Cells["colAmount"].Value)))
                                detailCellsBlank = false;
                        }
                        hidePriceOk = leftColsHidden && leftCellsBlank && detailColsHidden && detailCellsBlank;
                    }
                    finally { host.Close(); }
                }

                Check("QLBH UI §1/§7 Thiếu quyền XemDonGiaTrongHoaDonBanHang: ẩn mọi cột tiền và để trống giá trị ô ở cả lưới trái lẫn lưới chi tiết",
                    hidePriceOk);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
            }
        }

        private static bool MenuContainsId(List<MenuNode> nodes, string menuId)
        {
            if (nodes == null)
                return false;
            foreach (MenuNode n in nodes)
            {
                if (string.Equals(n.Id, menuId, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (MenuContainsId(n.Children, menuId))
                    return true;
            }
            return false;
        }

        private static void SelectBillRow(DataGridView grid, string billId)
        {
            grid.ClearSelection();
            foreach (DataGridViewRow row in grid.Rows)
            {
                var item = row.Tag as PaidBillListItem;
                if (item != null && string.Equals(item.Id, billId, StringComparison.Ordinal))
                {
                    row.Selected = true;
                    if (grid.Columns.Count > 0 && grid.Columns[0].Visible)
                        grid.CurrentCell = row.Cells[0];
                    return;
                }
            }
        }

        private static void PumpUntil(Func<bool> predicate, int timeoutMs)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!predicate() && DateTime.UtcNow < end)
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }
            Application.DoEvents();
        }
    }
}
