using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void ServicePrintUiTests(string db, ConfigRepository cfgRepo, UserSession admin, ServicePrintFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                var svc = new TableService(admin);
                SetPrintConfigInt(db, cfgRepo, fx, "SoLanInTamTinhToiDa", 5);
                SetPrintConfigInt(db, cfgRepo, fx, "HienThiTruocKhiIn", 0);
                SetPrintConfigInt(db, cfgRepo, fx, "LuaChonMauKhiIn", 0);
                SetPrintConfigInt(db, cfgRepo, fx, "SoLanIn", 1);
                SetPrintConfigInt(db, cfgRepo, fx, "InHoaDonTheoKhuVuc", 0);
                SetPrintConfigInt(db, cfgRepo, fx, "SuDungChucNangInXuongBep", 30);
                SetPrintConfigInt(db, cfgRepo, fx, "InDoAn", 30);
                SetPrintConfigInt(db, cfgRepo, fx, "InDoUong", 30);
                SetPrintConfigInt(db, cfgRepo, fx, "InRiengDoAnUong", 30);
                SetPrintConfigInt(db, cfgRepo, fx, "InMoiDoRa1To", 0);
                SetPrintConfigInt(db, cfgRepo, fx, "InThem1LienTaiQuay", 0);

                // Tạo bill đang mở trên Table3 cho kiểm thử giao diện thật.
                ServiceOpResult open = svc.OpenTable(fx.Table3Id, true, null);
                fx.CreatedBillIds.Add(open.Bill.Id);
                svc.AddItem(open.Bill.Id, fx.Table3Id, fx.ProdFoodId, 1m, 120000m);
                svc.AddItem(open.Bill.Id, fx.Table3Id, fx.ProdDrinkId, 1m, 40000m);

                // 1. KitchenPrintDialog thật: mở ở chế độ thiết lập khi chưa gán máy in -> bắt chọn máy in -> lưu thiết lập -> sang tab phiếu -> bấm In chế biến.
                var emptyStore = new MemoryKitchenPrinterStore("MAY-IN-BEP", "MAY-IN-BAR");
                var kitchenDlgRec = new ServicePrintOutputRecorder();
                using (var kDlg = new KitchenPrintDialog(svc, admin, open.Bill.Id, emptyStore, true))
                {
                    kDlg.PrintOutputHandlerForTest = kitchenDlgRec;
                    kDlg.StartPosition = FormStartPosition.Manual;
                    kDlg.Location = new Point(-3000, -3000);
                    kDlg.Show();
                    Application.DoEvents();
                    try
                    {
                        bool startedInSetup = kDlg.InSetupModeForTest && kDlg.SetupGridForTest.Rows.Count == 2;
                        kDlg.SaveSetupButtonForTest.PerformClick();
                        Application.DoEvents();
                        string missingMsg = kDlg.LastWarningForTest;

                        kDlg.SetupGridForTest.Rows[0].Cells["colPrinter"].Value = "MAY-IN-BEP";
                        kDlg.SetupGridForTest.Rows[1].Cells["colPrinter"].Value = "MAY-IN-BAR";
                        kDlg.SaveSetupButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool switchedToTabs = !kDlg.InSetupModeForTest && kDlg.TabsForTest.TabPages.Count == 2;

                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        bool themeOk = kDlg.BackColor == BuiltInThemes.Ocean.Background && kDlg.PrintButtonForTest.Height >= 44;

                        kDlg.PrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        int kRows = CountKitchenRows(db, open.Bill.Id);

                        Check("SDDV In UI §1 KitchenPrintDialog thật: chưa gán máy in vào thiết lập, chặn trống máy in, lưu sang tab phiếu, đổi theme và bấm In chế biến ghi TINCHEBIEN",
                            startedInSetup && missingMsg == ServicePrintConstants.KitchenSelectPrinterPrompt
                            && switchedToTabs && themeOk && kDlg.PrintedSuccessfully
                            && kitchenDlgRec.PrintCalls.Count == 2 && kRows == 2,
                            "setup=" + startedInSetup + ", warn=" + SingleLineDetail(missingMsg) + ", tabs=" + kDlg.TabsForTest.TabPages.Count + ", rows=" + kRows);
                    }
                    finally { if (!kDlg.IsDisposed) kDlg.Close(); }
                }

                // 2. PaymentDialog thật: bấm "In thử bill (F8)" gọi ShowPreview, dialog giữ nguyên mở và KHÔNG đổi SOLANINTAMTINH / DATHANHTOAN.
                var previewRec = new ServicePrintOutputRecorder();
                PaymentDialogContext payCtx = svc.PreparePayment(open.Bill.Id, fx.Table3Id);
                string snapBeforePreview = SnapshotSingleBill(db, open.Bill.Id);
                using (var pDlg = new PaymentDialog(svc, admin, payCtx))
                {
                    pDlg.SuppressPopup = true;
                    pDlg.PrintOutputHandlerForTest = previewRec;
                    pDlg.StartPosition = FormStartPosition.Manual;
                    pDlg.Location = new Point(-3000, -3000);
                    pDlg.Show();
                    Application.DoEvents();
                    try
                    {
                        pDlg.TempPrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        string snapAfterPreview = SnapshotSingleBill(db, open.Bill.Id);
                        bool stayedOpen = !pDlg.IsDisposed && pDlg.Visible && pDlg.ExecutionResult == null;
                        Check("SDDV In UI §2 PaymentDialog bấm In thử bill (F8): gọi ShowPreview, giữ dialog mở và không đổi dữ liệu DB",
                            previewRec.PreviewCalls.Count == 1 && stayedOpen && snapBeforePreview == snapAfterPreview,
                            "previews=" + previewRec.PreviewCalls.Count + ", open=" + stayedOpen + ", same=" + (snapBeforePreview == snapAfterPreview));
                    }
                    finally { if (!pDlg.IsDisposed) pDlg.Close(); }
                }

                // 3. ServiceOrderView thật: chọn bàn đang mở -> bấm In tạm tính (đổi trạng thái TempPrinted), thêm món -> bấm In chế biến, và bấm In lại bill (F8).
                svc.AddItem(open.Bill.Id, fx.Table3Id, fx.ProdFoodId, 1m, 120000m);
                var viewRec = new ServicePrintOutputRecorder();
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var view = new ServiceOrderView(admin) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    view.PrintOutputHandlerForTest = viewRec;
                    view.KitchenPrinterStoreForTest = emptyStore;
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton tile3 = view.TableMapForTest.FindTileForTest(fx.Table3Id);
                        if (tile3 != null)
                        {
                            tile3.PerformClick();
                            Application.DoEvents();
                        }

                        bool btnHeightsOk = view.TempPrintButtonForTest.Height >= 44
                            && view.KitchenPrintButtonForTest.Height >= 44
                            && view.ReprintButtonForTest.Height >= 44
                            && view.TempPrintButtonForTest.Enabled;

                        // Bấm In tạm tính.
                        view.TempPrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        int tempCntAfter = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                            "SELECT SOLANINTAMTINH FROM TDONHANG WHERE ID = @I", "@I", open.Bill.Id)));
                        ServiceTableInfo card3 = view.TableMapForTest.FindTable(fx.Table3Id);

                        // Bấm In chế biến (bổ sung +1 Phở).
                        view.KitchenPrintButtonForTest.PerformClick();
                        Application.DoEvents();
                        int kRowsAfterView = CountKitchenRows(db, open.Bill.Id);

                        // Bấm In lại bill (dùng bill đã thanh toán trước đó trong fixture).
                        string paidBillId = fx.CreatedBillIds.Count > 0 ? fx.CreatedBillIds[0] : "";
                        view.LastPaidBillIdForTest = paidBillId;
                        view.ReprintButtonForTest.PerformClick();
                        Application.DoEvents();

                        bool allPrintCallsOk = viewRec.PrintCalls.Count == 3
                            && PageTextsContain(viewRec.PrintCalls[0].PageTexts, ServicePrintConstants.InvoiceTitleTemp)
                            && PageTextsContain(viewRec.PrintCalls[1].PageTexts, "Pho Bo PRT " + fx.Tag)
                            && PageTextsContain(viewRec.PrintCalls[2].PageTexts, ServicePrintConstants.InvoiceTitleOfficial);

                        Check("SDDV In UI §3 ServiceOrderView thật: nút In tạm tính/In chế biến/In lại bill >= 44px, thao tác thật cập nhật TempPrinted, TINCHEBIEN và in đúng tiêu đề",
                            btnHeightsOk && tempCntAfter == 1
                            && card3 != null && card3.Status == TableStatus.TempPrinted
                            && kRowsAfterView == 3 && allPrintCallsOk,
                            "btn=" + btnHeightsOk + ", tmp=" + tempCntAfter + ", kRows=" + kRowsAfterView + ", calls=" + viewRec.PrintCalls.Count);
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
