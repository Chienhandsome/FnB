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
        private static void ServiceOrderPaymentUiTests(string db, ConfigRepository cfgRepo, UserSession admin, PaymentTestFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                var svc = new TableService(admin);

                // Tạo 2 bill đang mở trên Table2 & Table3 cho các ca kiểm thử giao diện thật.
                ServiceOrderHeader uiBill1 = OpenSeededBill(svc, fx, fx.Table2Id, fx.ProdNormalId, 1m, 480000m);
                ServiceOrderHeader uiBill2 = OpenSeededBill(svc, fx, fx.Table3Id, fx.ProdNormalId, 1m, 200000m);

                // 1. Nút Thanh toán (F11) & TT chung trên ServiceOrderView thật (không còn báo "chưa hỗ trợ").
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var view = new ServiceOrderView(admin) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton tile = view.TableMapForTest.FindTileForTest(fx.Table2Id);
                        if (tile != null)
                        {
                            tile.PerformClick();
                            Application.DoEvents();
                        }
                        bool btnSizesOk = view.PayButtonForTest.Height >= 44 && view.GroupPayButtonForTest.Height >= 44;
                        view.PayButtonForTest.PerformClick();
                        Application.DoEvents();
                        Check("SDDV ThanhToan UI §3.7: Bấm Thanh toán (F11) chuẩn bị PaymentDialogContext thật (không báo chưa hỗ trợ) và nút TT chung >= 44px",
                            btnSizesOk && view.LastPreparedPaymentForTest != null && view.LastPreparedPaymentForTest.Ok
                            && view.LastPreparedPaymentForTest.PrimaryBillId == uiBill1.Id
                            && view.LastInfoForTest != ServiceOrderConstants.NotSupportedYet);
                    }
                    finally { host.Close(); }
                }

                // 2. PaymentDialog thật: chỉ bật CoThanhToanThe=30, CoThanhToanChuyenKhoan=0 -> ô Tài khoản VẪN HIỆN và bắt chọn (§7).
                SetTestConfigInt(db, cfgRepo, fx, "CoThanhToanThe", 30);
                SetTestConfigInt(db, cfgRepo, fx, "CoThanhToanChuyenKhoan", 0);
                SetTestConfigInt(db, cfgRepo, fx, "BatBuocInKhiThanhToan", 30);

                PaymentDialogContext ctxCard = svc.PreparePayment(uiBill1.Id, fx.Table2Id);
                using (var dlg = new PaymentDialog(svc, admin, ctxCard))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        bool bankVisible = dlg.BankRowVisibleForTest && dlg.BankComboForTest.Visible;
                        bool noPrintDisabled = !dlg.NoPrintButtonForTest.Enabled;
                        dlg.CardBoxForTest.Value = 480000m;
                        dlg.BankComboForTest.SelectId("");
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        string errMissingBank = dlg.LastWarningForTest;

                        dlg.BankComboForTest.SelectId(fx.BankId);
                        dlg.CardBoxForTest.Value = 500000m;
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        string errCardOver = dlg.LastWarningForTest;

                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        bool themeApplied = dlg.NetDueBoxForTest.ForeColor == BuiltInThemes.Ocean.Primary;

                        Check("SDDV ThanhToan UI §7 Chỉ quẹt thẻ (CoThanhToanChuyenKhoan=0): ô Tài khoản vẫn hiện & bắt chọn, chặn quẹt quá bill, BatBuocInKhiThanhToan=30 tắt Đóng không in, đổi theme đúng màu",
                            bankVisible && noPrintDisabled
                            && errMissingBank == FundText.AccountRequired
                            && errCardOver == FundText.CardOverDue
                            && themeApplied,
                            "bankVis=" + bankVisible + ", err1=" + errMissingBank + ", err2=" + errCardOver);
                    }
                    finally { dlg.Close(); }
                }

                // 3. PaymentDialog thật: bill 480k chuyển khoản 500k -> ô Trả lại hiện 20.000 và đóng bill qua nút OK.
                SetTestConfigInt(db, cfgRepo, fx, "CoThanhToanChuyenKhoan", 30);
                SetTestConfigInt(db, cfgRepo, fx, "BatBuocInKhiThanhToan", 0);

                PaymentDialogContext ctxCk = svc.PreparePayment(uiBill1.Id, fx.Table2Id);
                using (var dlg = new PaymentDialog(svc, admin, ctxCk))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        dlg.TransferBoxForTest.Value = 500000m;
                        dlg.CashBoxForTest.Value = 0m;
                        dlg.BankComboForTest.SelectId(fx.BankId);
                        Application.DoEvents();
                        decimal shownChange = dlg.ChangeBoxForTest.Value;
                        dlg.NoPrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        Check("SDDV ThanhToan UI §7 Bill 480k CK 500k: ô Trả lại realtime = 20.000 và bấm Đóng bill không in (F9) ghi DB thành công",
                            shownChange == 20000m && dlg.ExecutionResult != null && dlg.ExecutionResult.Ok,
                            "chg=" + shownChange);
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                // 4. RefundDepositDialog thật: đóng không lưu -> cảnh báo & hỏi xác nhận (từ chối giữ form mở); bấm Lưu ghi thật TTHUCHI.
                DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
                var fakePayRes = new PaymentExecutionResult
                {
                    Ok = true,
                    PrimaryBillId = uiBill1.Id,
                    PaidBillNames = uiBill1.Name,
                    PaymentDate = today,
                    ExcessDeposit = 100000m,
                    CustomerId = fx.Customer1Id,
                    CustomerName = "Khach UI",
                    StoreId = fx.StoreId
                };
                using (var rDlg = new RefundDepositDialog(svc, fakePayRes, new ServicePaymentRepository(db).LoadBankAccounts()))
                {
                    rDlg.SuppressPopup = true;
                    rDlg.StartPosition = FormStartPosition.Manual;
                    rDlg.Location = new Point(-3000, -3000);
                    rDlg.Show();
                    Application.DoEvents();

                    // Đóng không lưu nhưng người dùng chọn Không -> cảnh báo đã hiện, form vẫn mở!
                    rDlg.ConfirmDismissOverrideForTest = () => false;
                    rDlg.CloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool stayedOpenAfterDecline = rDlg.DismissWarningShownForTest && !rDlg.IsDisposed && rDlg.Visible;

                    // Chọn Chuyển khoản thiếu tài khoản -> bị chặn.
                    rDlg.TransferCheckForTest.Checked = true;
                    rDlg.BankComboForTest.SelectId("");
                    rDlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string bankReqErr = rDlg.LastWarningForTest;

                    // Chọn Tiền mặt và bấm Lưu -> ghi thật vào TTHUCHI.
                    rDlg.TransferCheckForTest.Checked = false;
                    rDlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    RefundVoucherResult savedRef = rDlg.SavedResult;
                    if (savedRef != null && !string.IsNullOrEmpty(savedRef.SavedId))
                        fx.CreatedVoucherIds.Add(savedRef.SavedId);

                    Check("SDDV ThanhToan UI §7/Quỹ §7 RefundDepositDialog: đóng không lưu có cảnh báo & giữ form khi chọn Không; CK thiếu TK chặn; bấm Lưu ghi thật phiếu chi",
                        stayedOpenAfterDecline && bankReqErr == FundText.AccountRequired && savedRef != null && savedRef.Ok,
                        "stayed=" + stayedOpenAfterDecline + ", err=" + bankReqErr);
                }

                // 5. GroupPaymentTablePickerDialog thật: bàn đang chọn tick sẵn, < 2 bàn chặn, >= 2 bàn trả đủ BillId.
                var fakeBusy = new List<BusyTableForGroupItem>
                {
                    new BusyTableForGroupItem { TableId = fx.Table2Id, TableName = "B2", AreaName = "KV", BillId = uiBill1.Id, BillName = uiBill1.Name, TongCong = 480000m },
                    new BusyTableForGroupItem { TableId = fx.Table3Id, TableName = "B3", AreaName = "KV", BillId = uiBill2.Id, BillName = uiBill2.Name, TongCong = 200000m }
                };
                using (var picker = new GroupPaymentTablePickerDialog(fakeBusy, fx.Table3Id))
                {
                    picker.SuppressPopup = true;
                    picker.StartPosition = FormStartPosition.Manual;
                    picker.Location = new Point(-3000, -3000);
                    picker.Show();
                    Application.DoEvents();
                    bool preTickedOk = (bool)picker.GridForTest.Rows[1].Cells["colSel"].Value && !(bool)picker.GridForTest.Rows[0].Cells["colSel"].Value;

                    // Mới tick 1 bàn bấm OK -> bị chặn yêu cầu >= 2 bàn.
                    picker.OkButtonForTest.PerformClick();
                    Application.DoEvents();
                    string minErr = picker.LastWarningForTest;

                    // Tick thêm bàn 1 -> bấm OK thành công trả đủ 2 bill.
                    picker.GridForTest.Rows[0].Cells["colSel"].Value = true;
                    picker.OkButtonForTest.PerformClick();
                    Application.DoEvents();
                    Check("SDDV ThanhToan UI §3.7.3 GroupPaymentTablePickerDialog: tick sẵn bàn hiện tại, <2 bàn chặn, >=2 bàn trả đủ BillId",
                        preTickedOk && minErr == ServicePaymentConstants.GroupMinTables && picker.SelectedBillIds.Count == 2);
                }
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
            }
        }
    }
}
