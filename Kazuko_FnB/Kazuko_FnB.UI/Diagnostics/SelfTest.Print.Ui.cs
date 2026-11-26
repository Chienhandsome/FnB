using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP0UiAndPermissionTests(string db, List<PrintTemplateRecord> templates)
        {
            TestPrintTemplateDialogAndActions(templates);
            TestItemSalesViewPrintIntegrationAndPermissions(db, templates);
        }

        // ------------------------------------------------------------------ Hộp CHỌN MẪU IN & 4 tùy chọn + Cancel + Nhấp đúp

        private static void TestPrintTemplateDialogAndActions(List<PrintTemplateRecord> templates)
        {
            var printers = new List<string> { "Máy in nhiệt giả 80mm", "Máy in mặc định giả A4" };
            const string defaultPrinter = "Máy in mặc định giả A4";

            // 1. Kiểm tra danh sách mẫu, chọn sẵn mẫu cuối, 4 tùy chọn, không có Thiết kế, bật/tắt ô máy in, bố cục không cuộn/không cắt.
            using (var dlg = new PrintTemplateDialog(templates, printers, defaultPrinter))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.ShowInTaskbar = false;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.PerformLayout();
                Application.DoEvents();

                bool listOk = dlg.Text == PrintConstants.SelectTemplateDialogTitle
                    && dlg.TemplateListForTest.Items.Count == 3
                    && Convert.ToString(dlg.TemplateListForTest.Items[0]) == "54mm"
                    && Convert.ToString(dlg.TemplateListForTest.Items[1]) == "80mm"
                    && Convert.ToString(dlg.TemplateListForTest.Items[2]) == "A4"
                    && dlg.TemplateListForTest.SelectedIndex == 2;

                bool optionsDefaultOk = dlg.RadioPreviewForTest.Checked
                    && !dlg.RadioPrintForTest.Checked
                    && !dlg.RadioExcelForTest.Checked
                    && !dlg.RadioPdfForTest.Checked
                    && !dlg.PrinterComboForTest.Enabled
                    && string.Equals(dlg.PrinterComboForTest.SelectedItem as string, defaultPrinter, StringComparison.Ordinal);

                bool noDesignOption = !ContainsTextRecursive(dlg, "Thiết kế");

                // Chọn "In ra máy in" -> ô chọn máy in bật; chuyển sang Xuất excel -> tắt lại.
                dlg.RadioPrintForTest.Checked = true;
                bool enabledWhenPrint = dlg.PrinterComboForTest.Enabled;
                dlg.RadioExcelForTest.Checked = true;
                bool disabledWhenExcel = !dlg.PrinterComboForTest.Enabled;
                dlg.RadioPreviewForTest.Checked = true;

                bool noVerticalScroll = !dlg.BodyPanelForTest.AutoScroll && !dlg.BodyPanelForTest.VerticalScroll.Visible;
                bool noClippedText = CheckControlTextNotClipped(dlg.RadioPreviewForTest)
                    && CheckControlTextNotClipped(dlg.RadioPrintForTest)
                    && CheckControlTextNotClipped(dlg.RadioExcelForTest)
                    && CheckControlTextNotClipped(dlg.RadioPdfForTest)
                    && dlg.OkButtonForTest.Height >= 34 && dlg.CancelButtonForTest.Height >= 34;

                Check("In P0 (UI hộp CHỌN MẪU IN): 3 mẫu sắp theo tên chọn sẵn cuối (A4), 4 tùy chọn, không có Thiết kế, máy in bật đúng lúc, không cuộn/cắt chữ",
                    listOk && optionsDefaultOk && noDesignOption && enabledWhenPrint && disabledWhenExcel && noVerticalScroll && noClippedText,
                    "list=" + listOk + ", def=" + optionsDefaultOk + ", noDesign=" + noDesignOption
                    + ", enPrint=" + enabledWhenPrint + ", disExcel=" + disabledWhenExcel
                    + ", noScroll=" + noVerticalScroll + ", noClip=" + noClippedText);

                dlg.Close();
            }

            // 2. Bấm OK thật cho từng tùy chọn trong 4 tùy chọn, nhấp đúp mẫu và bấm Cancel -> interface giả nhận đúng, KHÔNG gọi máy in thật.
            using (PrintJobData emptyJob = ItemSalesPrintDataBuilder.Build(
                new List<ItemSalesRow>(), null, new ItemSalesSummary(), DateTime.Today, DateTime.Today, new CompanyPrintInfo(), true))
            {
                // 2a. Xem trên màn hình (A4)
                PrintSelectionResult selPreview = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.OkButtonForTest.PerformClick();
                });
                var recPreview = new PrintOutputRecorder();
                bool okPreview = PrintWorkflowService.Execute(null, selPreview, emptyJob, recPreview);
                Check("In P0 (UI hộp chọn -> OK Xem trên màn hình): interface giả nhận đúng mẫu A4 và hành động Preview",
                    okPreview && recPreview.PreviewCount == 1 && recPreview.PrintCount == 0
                    && recPreview.LastTemplate != null && recPreview.LastTemplate.Name == "A4"
                    && recPreview.LastPreviewTitle == PrintConstants.DefaultPreviewTitle);

                // 2b. In ra máy in (80mm + máy in chọn)
                PrintSelectionResult selPrint = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.TemplateListForTest.SelectedIndex = 1; // 80mm
                    d.RadioPrintForTest.Checked = true;
                    d.PrinterComboForTest.SelectedIndex = 0; // Máy in nhiệt giả 80mm
                    d.OkButtonForTest.PerformClick();
                });
                var recPrint = new PrintOutputRecorder();
                bool okPrint = PrintWorkflowService.Execute(null, selPrint, emptyJob, recPrint);
                Check("In P0 (UI hộp chọn -> OK In ra máy in): interface giả nhận đúng mẫu 80mm, hành động Print, đúng tên máy in",
                    okPrint && recPrint.PrintCount == 1 && recPrint.PreviewCount == 0
                    && recPrint.LastTemplate != null && recPrint.LastTemplate.Name == "80mm"
                    && recPrint.LastPrinterName == "Máy in nhiệt giả 80mm");

                // 2c. Xuất excel (A4)
                PrintSelectionResult selExcel = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.RadioExcelForTest.Checked = true;
                    d.OkButtonForTest.PerformClick();
                });
                var recExcel = new PrintOutputRecorder
                {
                    NextSavePath = Path.Combine(Path.GetTempPath(), "fake-export.xlsx"),
                    ConfirmOpenResult = false,
                    RunRealExport = false
                };
                bool okExcel = PrintWorkflowService.Execute(null, selExcel, emptyJob, recExcel);
                Check("In P0 (UI hộp chọn -> OK Xuất excel): interface giả nhận đúng mẫu A4, hành động ExportExcel và hỏi mở file",
                    okExcel && recExcel.ExcelCount == 1 && recExcel.ConfirmOpenCount == 1 && recExcel.OpenFileCount == 0
                    && recExcel.LastTemplate != null && recExcel.LastTemplate.Name == "A4");

                // 2d. Xuất pdf (54mm, chọn mở file -> OpenFile được gọi trên giả)
                PrintSelectionResult selPdf = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.TemplateListForTest.SelectedIndex = 0; // 54mm
                    d.RadioPdfForTest.Checked = true;
                    d.OkButtonForTest.PerformClick();
                });
                var recPdf = new PrintOutputRecorder
                {
                    NextSavePath = Path.Combine(Path.GetTempPath(), "fake-export.pdf"),
                    ConfirmOpenResult = true,
                    RunRealExport = false
                };
                bool okPdf = PrintWorkflowService.Execute(null, selPdf, emptyJob, recPdf);
                Check("In P0 (UI hộp chọn -> OK Xuất pdf): interface giả nhận đúng mẫu 54mm, hành động ExportPdf và mở file",
                    okPdf && recPdf.PdfCount == 1 && recPdf.ConfirmOpenCount == 1 && recPdf.OpenFileCount == 1
                    && recPdf.LastTemplate != null && recPdf.LastTemplate.Name == "54mm");

                // 2e. Nhấp đúp tên mẫu = OK
                PrintSelectionResult selDbl = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.TemplateListForTest.SelectedIndex = 0; // 54mm
                    d.TriggerTemplateDoubleClickForTest();
                });
                Check("In P0 (UI hộp chọn nhấp đúp): nhấp đúp tên mẫu hoạt động như bấm OK",
                    selDbl != null && selDbl.Template != null && selDbl.Template.Name == "54mm" && selDbl.Action == PrintActionKind.Preview);

                // 2f. Cancel không làm gì
                PrintSelectionResult selCancel = RunDialogSelection(templates, printers, defaultPrinter, delegate (PrintTemplateDialog d)
                {
                    d.CancelButtonForTest.PerformClick();
                });
                var recCancel = new PrintOutputRecorder();
                bool okCancel = PrintWorkflowService.Execute(null, selCancel, emptyJob, recCancel);
                Check("In P0 (UI hộp chọn -> Cancel): trả null và không thực hiện hành động nào",
                    selCancel == null && !okCancel && recCancel.PreviewCount == 0 && recCancel.PrintCount == 0
                    && recCancel.ExcelCount == 0 && recCancel.PdfCount == 0);
            }
        }

        private static PrintSelectionResult RunDialogSelection(
            List<PrintTemplateRecord> templates,
            List<string> printers,
            string defaultPrinter,
            Action<PrintTemplateDialog> userAction)
        {
            using (var dlg = new PrintTemplateDialog(templates, printers, defaultPrinter))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.ShowInTaskbar = false;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.PerformLayout();
                Application.DoEvents();
                userAction(dlg);
                Application.DoEvents();
                return dlg.DialogResult == DialogResult.OK ? dlg.Selection : null;
            }
        }

        // ------------------------------------------------------------------ Dữ liệu in từ màn thật ItemSalesView & quyền T4

        private static void TestItemSalesViewPrintIntegrationAndPermissions(string db, List<PrintTemplateRecord> templates)
        {
            string storeId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string grpAId = DbHelper.NewId();
            string grpBId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();
            string prod3Id = DbHelper.NewId();
            string billId = DbHelper.NewId();
            var lineIds = new List<string>();

            var cfgRepo = new ConfigRepository(db);
            int origSortOrder = SystemConfig.GetInt(ItemSalesConstants.ConfigSortOrderKey);
            int origUseCode = SystemConfig.GetInt(ItemSalesConstants.ConfigUseCodeKey);
            DateTime testDate = new DateTime(2003, 8, 15);

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeId, "@N", "PRN-CH1");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", unitId, "@N", "PRN-Ly");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0001', 30, 'selftest')", "@I", grpAId, "@N", "PRN-Nhóm Trà");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '0', '', '0002', 30, 'selftest')", "@I", grpBId, "@N", "PRN-Nhóm Bánh");

                // Mã đảo ngược thứ tự với Tên để kiểm thứ tự sắp xếp theo mã vs theo tên.
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, MASANCO, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 10000, 12000, 30000, 'MSC-1', 30, 'selftest')",
                    "@I", prod1Id, "@C", "PRN-03", "@N", "PRN-A Bánh su kem", "@G", grpBId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, MASANCO, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 15000, 16000, 45000, 'MSC-2', 30, 'selftest')",
                    "@I", prod2Id, "@C", "PRN-01", "@N", "PRN-C Trà sen vàng", "@G", grpAId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIANHAP, GIAVON, GIABAN, MASANCO, STATUS, USERCREATEDID) VALUES (@I, @C, @N, @G, @U, '0', 11000, 13000, 35000, 'MSC-3', 30, 'selftest')",
                    "@I", prod3Id, "@C", "PRN-02", "@N", "PRN-B Cà phê trứng", "@G", grpAId, "@U", unitId);

                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, GIOTHANHTOAN, DCUAHANGID, " +
                    "TIENHANG, TONGCONG, TIENGIAMGIATONG, GIAMGIAMATHANG, TIENTHUE, TIENGIO, PHIDICHVU, USERCREATEDID) " +
                    "VALUES (@ID, 'PRN-HD-01', 0, 30, 30, @NGAY, @NGAY, @CH, 215000, 218000, 5000, 9000, 8000, 0, 2000, 'selftest')",
                    "@ID", billId, "@NGAY", new DateTime(2003, 8, 15, 10, 0, 0), "@CH", storeId);

                Action<string, string, decimal, decimal, decimal, decimal, decimal, decimal> addLine =
                    delegate (string pid, string ten, decimal sl, decimal dg, decimal tlGiam, decimal tienGiam, decimal tt, decimal gv)
                    {
                        string lid = DbHelper.NewId();
                        lineIds.Add(lid);
                        BookingExec(db,
                            "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, " +
                            "SLXUATCHUAQUYDOI, SLNHAPCHUAQUYDOI, SLXUAT, SLNHAP, DONGIA, TILEGIAMGIA, TIENGIAMGIA, THANHTIEN, GIAVON, " +
                            "XUATVATTU, COMBOPARENTID, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @BID, @PID, @TEN, @U, @SL, 0, @SL, 0, @DG, @TL, @TG, @TT, @GV, 0, '', 30, 'selftest', @TC)",
                            "@ID", lid, "@BID", billId, "@PID", pid, "@TEN", ten, "@U", unitId,
                            "@SL", sl, "@DG", dg, "@TL", tlGiam, "@TG", tienGiam, "@TT", tt, "@GV", gv,
                            "@TC", new DateTime(2003, 8, 15, 10, 1, 0));
                    };

                addLine(prod1Id, "PRN-A Bánh su kem", 2m, 30000m, 0m, 0m, 60000m, 12000m);
                addLine(prod2Id, "PRN-C Trà sen vàng", 2m, 45000m, 10m, 9000m, 81000m, 16000m);
                addLine(prod3Id, "PRN-B Cà phê trứng", 2m, 35000m, 0m, 0m, 70000m, 13000m);

                // Đảm bảo sắp theo tên trước.
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = 1 },
                    new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = 0 }
                }, "selftest");
                SystemConfig.Reload(db);

                var adminSession = new UserSession
                {
                    UserId = "selftest_print_admin",
                    UserName = "admin",
                    DisplayName = "Admin",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                using (var host = new Form { StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, Location = new Point(-20000, -20000), ClientSize = new Size(1280, 720) })
                using (var view = new ItemSalesView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true })
                {
                    host.Controls.Add(view);
                    host.Show();
                    host.PerformLayout();
                    Application.DoEvents();

                    view.FromDateBoxForTest.Inner.Text = "15/08/2003";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "15/08/2003";
                    view.ToDateBoxForTest.Commit();
                    Application.DoEvents();

                    // 1. Dữ liệu Table0 khi chưa lọc: đúng 3 dòng theo tên (A -> B -> C), đúng NHOMHANG, đúng 17 cột và tham số khối tổng.
                    using (PrintJobData jobAll = view.BuildPrintJobDataForTest())
                    {
                        DataTable t0 = jobAll.DataSet.Tables["Table0"];
                        bool orderNameOk = t0.Rows.Count == 3
                            && Convert.ToString(t0.Rows[0]["TENHANG"]) == "PRN-A Bánh su kem"
                            && Convert.ToString(t0.Rows[0]["NHOMHANG"]) == "PRN-Nhóm Bánh"
                            && Convert.ToString(t0.Rows[1]["TENHANG"]) == "PRN-B Cà phê trứng"
                            && Convert.ToString(t0.Rows[1]["NHOMHANG"]) == "PRN-Nhóm Trà"
                            && Convert.ToString(t0.Rows[2]["TENHANG"]) == "PRN-C Trà sen vàng"
                            && Convert.ToDecimal(t0.Rows[2]["GIAVON"]) == 16000m
                            && Convert.ToDecimal(t0.Rows[2]["DMATHANG_GIAVON"]) == 16000m;

                        bool paramsOk = (DateTime)jobAll.Parameters["TuNgay"] == testDate
                            && (DateTime)jobAll.Parameters["DenNgay"] == testDate
                            && (decimal)jobAll.Parameters["TongTienBan"] == view.CurrentSummaryForTest.TotalSalesBeforeDiscount
                            && (decimal)jobAll.Parameters["TongTienNhap"] == view.CurrentSummaryForTest.TotalCost
                            && (decimal)jobAll.Parameters["TongGiamGia"] == view.CurrentSummaryForTest.TotalDiscount
                            && (decimal)jobAll.Parameters["TongLai"] == view.CurrentSummaryForTest.GrossSalesProfit
                            && (decimal)jobAll.Parameters["ThucLai"] == view.CurrentSummaryForTest.NetProfit;

                        Check("In P0 (màn thật ItemSalesView): Table0 đúng 3 dòng & thứ tự đang hiện, NHOMHANG và tham số khớp khối tổng",
                            orderNameOk && paramsOk,
                            "order=" + orderNameOk + ", params=" + paramsOk);
                    }

                    // 2. Sau lọc cây nhóm (PRN-Nhóm Trà) + ô Lọc ("trà"): Table0 chỉ còn đúng dòng đang hiện; khối tổng giữ nguyên.
                    TreeNode grpANode = view.FindGroupNodeById(grpAId);
                    if (grpANode != null)
                        view.GroupTreeForTest.SelectedNode = grpANode;
                    Application.DoEvents();
                    view.FilterBoxForTest.Text = "trà";
                    Application.DoEvents();

                    using (PrintJobData jobFiltered = view.BuildPrintJobDataForTest())
                    {
                        DataTable t0 = jobFiltered.DataSet.Tables["Table0"];
                        bool filteredOk = t0.Rows.Count == 1
                            && Convert.ToString(t0.Rows[0]["TENHANG"]) == "PRN-C Trà sen vàng"
                            && (decimal)jobFiltered.Parameters["TongTienBan"] == view.CurrentSummaryForTest.TotalSalesBeforeDiscount;
                        Check("In P0 (màn thật sau lọc nhóm & ô Lọc): Table0 đúng dòng đang hiện, tham số vẫn khớp khối tổng đang hiện",
                            filteredOk, "rows=" + t0.Rows.Count);
                    }

                    // 3. Gõ ngày mới vào ô nhưng CHƯA tải lại -> TuNgay/DenNgay vẫn giữ ngày đã tải (15/08/2003).
                    view.FilterBoxForTest.Text = "";
                    TreeNode rootNode = view.FindGroupNodeById(null);
                    if (rootNode != null)
                        view.GroupTreeForTest.SelectedNode = rootNode;
                    view.FromDateBoxForTest.Inner.Text = "01/01/2000";
                    using (PrintJobData jobDates = view.BuildPrintJobDataForTest())
                    {
                        Check("In P0 (TuNgay/DenNgay theo dữ liệu đã tải): gõ ngày chưa tải lại không làm lệch tham số ngày in",
                            (DateTime)jobDates.Parameters["TuNgay"] == testDate
                            && (DateTime)jobDates.Parameters["DenNgay"] == testDate);
                    }
                    view.FromDateBoxForTest.Inner.Text = "15/08/2003";

                    // 4. Đổi sắp xếp theo mã hàng (PRN-01 -> PRN-02 -> PRN-03) -> Table0 đúng thứ tự mới trên lưới.
                    cfgRepo.Save(new[]
                    {
                        new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = 0 },
                        new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = 30 }
                    }, "selftest");
                    SystemConfig.Reload(db);
                    view.ReloadReportData();
                    Application.DoEvents();
                    using (PrintJobData jobByCode = view.BuildPrintJobDataForTest())
                    {
                        DataTable t0 = jobByCode.DataSet.Tables["Table0"];
                        bool codeOrderOk = t0.Rows.Count == 3
                            && Convert.ToString(t0.Rows[0]["DMATHANG_CODE"]) == "PRN-01"
                            && Convert.ToString(t0.Rows[1]["DMATHANG_CODE"]) == "PRN-02"
                            && Convert.ToString(t0.Rows[2]["DMATHANG_CODE"]) == "PRN-03";
                        Check("In P0 (màn thật sau sắp xếp theo mã): Table0 giữ đúng thứ tự lưới đang hiện",
                            codeOrderOk);
                    }

                    // 5. Khóa nút khi đang dựng mẫu: bấm lặp lần hai trong lúc đang xử lý không mở hai lần.
                    int pickerCalls = 0;
                    bool buttonDisabledDuringPrint = false;
                    bool reentrantBlocked = false;
                    var busyRec = new PrintOutputRecorder();
                    view.PrintOutputHandlerForTest = busyRec;
                    view.PrintTemplatePickerForTest = delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
                    {
                        pickerCalls++;
                        buttonDisabledDuringPrint = !view.PrintButtonForTest.Enabled;
                        reentrantBlocked = !view.PrintReport();
                        return new PrintSelectionResult { Template = list[list.Count - 1], Action = PrintActionKind.Preview };
                    };
                    view.PrintButtonForTest.PerformClick();
                    bool restoredAfterPrint = view.PrintButtonForTest.Enabled;
                    Check("In P0 (khóa nút tránh bấm hai lần): nút tắt khi đang dựng, gọi chồng bị chặn, bật lại sau khi xong",
                        pickerCalls == 1 && buttonDisabledDuringPrint && reentrantBlocked && restoredAfterPrint && busyRec.PreviewCount == 1,
                        "calls=" + pickerCalls + ", dis=" + buttonDisabledDuringPrint + ", block=" + reentrantBlocked + ", rest=" + restoredAfterPrint);

                    host.Close();
                }

                // 6. T4: Người không có quyền Xem giá nhập -> nút In ẩn, gọi đường in trực tiếp bị từ chối, không dựng dữ liệu giá vốn.
                var noCostPerms = new FunctionPermissions(false);
                noCostPerms.Add(ItemSalesConstants.FunctionId, ItemSalesConstants.FunctionName, RoleModes.View);
                var noCostSession = new UserSession
                {
                    UserId = "selftest_print_nocost",
                    UserName = "nocost",
                    DisplayName = "NoCost",
                    IsAdmin = false,
                    DatabasePath = db,
                    Permissions = noCostPerms
                };
                using (var hostNo = new Form { StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, Location = new Point(-20000, -20000), ClientSize = new Size(1280, 720) })
                using (var viewNo = new ItemSalesView(noCostSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true })
                {
                    hostNo.Controls.Add(viewNo);
                    hostNo.Show();
                    hostNo.PerformLayout();
                    Application.DoEvents();

                    var deniedRec = new PrintOutputRecorder();
                    viewNo.PrintOutputHandlerForTest = deniedRec;
                    bool printBtnHidden = !viewNo.PrintButtonForTest.Visible;
                    bool directPrintRejected = !viewNo.PrintReport();

                    bool directBuilderBlocked = false;
                    try
                    {
                        viewNo.BuildPrintJobDataForTest();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        directBuilderBlocked = true;
                    }

                    bool staticBuilderBlocked = false;
                    try
                    {
                        ItemSalesPrintDataBuilder.Build(
                            new List<ItemSalesRow> { new ItemSalesRow { ItemName = "A", UnitCost = 10000m } },
                            null, new ItemSalesSummary { TotalCost = 10000m }, testDate, testDate, new CompanyPrintInfo(), false);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        staticBuilderBlocked = true;
                    }

                    Check("In P0 (T4 quyền Xem giá nhập): nút In ẩn, gọi trực tiếp đường in và bộ dựng dữ liệu đều bị từ chối",
                        printBtnHidden && directPrintRejected && directBuilderBlocked && staticBuilderBlocked
                        && deniedRec.PreviewCount == 0 && deniedRec.PrintCount == 0 && deniedRec.ExcelCount == 0 && deniedRec.PdfCount == 0
                        && deniedRec.InfoMessages.Count == 1,
                        "hidden=" + printBtnHidden + ", rej=" + directPrintRejected
                        + ", bld=" + directBuilderBlocked + ", static=" + staticBuilderBlocked);

                    hostNo.Close();
                }
            }
            finally
            {
                try
                {
                    cfgRepo.Save(new[]
                    {
                        new ConfigChange { Name = ItemSalesConstants.ConfigSortOrderKey, DataType = 3, IntValue = origSortOrder },
                        new ConfigChange { Name = ItemSalesConstants.ConfigUseCodeKey, DataType = 3, IntValue = origUseCode }
                    }, "selftest");
                    SystemConfig.Reload(db);
                }
                catch { }

                foreach (string lid in lineIds)
                {
                    try { BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid); } catch { }
                }
                try { BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", billId); } catch { }
                try { BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", prod1Id); } catch { }
                try { BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", prod2Id); } catch { }
                try { BookingExec(db, "DELETE FROM DMATHANG WHERE ID = @I", "@I", prod3Id); } catch { }
                try { BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", grpAId); } catch { }
                try { BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID = @I", "@I", grpBId); } catch { }
                try { BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId); } catch { }
                try { BookingExec(db, "DELETE FROM DCUAHANG WHERE ID = @I", "@I", storeId); } catch { }
            }
        }

        private static bool ContainsTextRecursive(Control root, string needle)
        {
            if (root == null)
                return false;
            if (!string.IsNullOrEmpty(root.Text) && root.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (Control child in root.Controls)
            {
                if (ContainsTextRecursive(child, needle))
                    return true;
            }
            return false;
        }

        private static bool CheckControlTextNotClipped(Control ctrl)
        {
            if (ctrl == null)
                return false;
            int needed = TextRenderer.MeasureText(ctrl.Text ?? "", ctrl.Font).Width + 20;
            return ctrl.Width >= needed && ctrl.Height >= ctrl.Font.Height + 4;
        }
    }
}
