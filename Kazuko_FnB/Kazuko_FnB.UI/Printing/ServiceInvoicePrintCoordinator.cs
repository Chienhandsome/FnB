using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in hóa đơn bán hàng/nhà hàng cho 4 luồng patch (d):
    /// (1) Thanh toán &amp; In (in thẳng ra máy in mặc định / theo khu vực),
    /// (2) In tạm tính (PHIẾU TẠM TÍNH, theo cấu hình LuaChonMauKhiIn / HienThiTruocKhiIn / InHoaDonTheoKhuVuc),
    /// (3) In lại bill đã thanh toán (không đổi dữ liệu),
    /// (4) In thử bill F8 trong hộp thanh toán (ShowPreview, không lưu).
    /// Khóa bấm hai lần (_isBusy), không văng khi thiếu máy in hay mẫu hỏng, dùng IPrintOutputHandler để test.
    /// </summary>
    internal sealed class ServiceInvoicePrintCoordinator
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
        }

        public bool PrintAfterPayment(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            ServiceInvoicePrintDocument doc,
            IKitchenPrinterStore printerStore,
            IPrintOutputHandler outputOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteInvoicePrint(
                hostControl,
                triggerButton,
                databasePath,
                session,
                doc,
                isTempPrint: false,
                isReprint: false,
                forcePreviewOnly: false,
                forceDirectPrint: true,
                printerStore: printerStore,
                outputOverride: outputOverride,
                templatePickerOverride: null,
                templateLoaderOverride: templateLoaderOverride,
                recordMessage: recordMessage);
        }

        public bool PrintTempBill(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            ServiceInvoicePrintDocument doc,
            IKitchenPrinterStore printerStore,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteInvoicePrint(
                hostControl,
                triggerButton,
                databasePath,
                session,
                doc,
                isTempPrint: true,
                isReprint: false,
                forcePreviewOnly: false,
                forceDirectPrint: false,
                printerStore: printerStore,
                outputOverride: outputOverride,
                templatePickerOverride: templatePickerOverride,
                templateLoaderOverride: templateLoaderOverride,
                recordMessage: recordMessage);
        }

        public bool ReprintPaidBill(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            ServiceInvoicePrintDocument doc,
            IKitchenPrinterStore printerStore,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteInvoicePrint(
                hostControl,
                triggerButton,
                databasePath,
                session,
                doc,
                isTempPrint: false,
                isReprint: true,
                forcePreviewOnly: false,
                forceDirectPrint: false,
                printerStore: printerStore,
                outputOverride: outputOverride,
                templatePickerOverride: templatePickerOverride,
                templateLoaderOverride: templateLoaderOverride,
                recordMessage: recordMessage);
        }

        public bool PreviewBillOnly(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            ServiceInvoicePrintDocument doc,
            IPrintOutputHandler outputOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteInvoicePrint(
                hostControl,
                triggerButton,
                databasePath,
                session,
                doc,
                isTempPrint: true,
                isReprint: false,
                forcePreviewOnly: true,
                forceDirectPrint: false,
                printerStore: null,
                outputOverride: outputOverride,
                templatePickerOverride: null,
                templateLoaderOverride: templateLoaderOverride,
                recordMessage: recordMessage);
        }

        private bool ExecuteInvoicePrint(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            ServiceInvoicePrintDocument doc,
            bool isTempPrint,
            bool isReprint,
            bool forcePreviewOnly,
            bool forceDirectPrint,
            IKitchenPrinterStore printerStore,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            if (_isBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();
            if (doc == null)
            {
                ReportError(owner, output, recordMessage, ServicePrintConstants.BillNotFoundForPrint);
                return false;
            }

            _isBusy = true;
            bool oldEnabled = triggerButton != null && triggerButton.Enabled;
            Cursor oldCursor = hostControl != null ? hostControl.Cursor : Cursors.Default;
            try
            {
                if (triggerButton != null)
                    triggerButton.Enabled = false;
                if (hostControl != null)
                    hostControl.Cursor = Cursors.WaitCursor;

                var repo = new PrintTemplateRepository(databasePath);
                List<PrintTemplateRecord> templates;
                try
                {
                    templates = templateLoaderOverride != null
                        ? templateLoaderOverride(PrintConstants.SalesInvoiceFormId)
                        : repo.LoadTemplatesForForm(PrintConstants.SalesInvoiceFormId);
                }
                catch (DataAccessException ex)
                {
                    ReportError(owner, output, recordMessage, "Không đọc được danh sách mẫu in hóa đơn: " + ex.Message);
                    return false;
                }

                if (templates == null || templates.Count == 0)
                {
                    if (recordMessage != null)
                        recordMessage(PrintConstants.NoTemplateFoundMessage);
                    output.ShowInfo(owner, PrintConstants.NoTemplateFoundMessage);
                    return false;
                }

                string preferredTemplate = !string.IsNullOrWhiteSpace(doc.AreaTemplateId)
                    ? doc.AreaTemplateId
                    : SystemConfig.MauHoaDon;
                PrintDialogOptions dialogOptions = PrintDialogOptions.ForSalesInvoiceReportDialog(preferredTemplate);
                int defaultIdx;
                List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(templates, dialogOptions, out defaultIdx);
                PrintTemplateRecord defaultTemplate = defaultIdx >= 0 && defaultIdx < ordered.Count ? ordered[defaultIdx] : ordered[0];

                bool showSelectTemplate = !forcePreviewOnly && !forceDirectPrint
                    && (SystemConfig.LuaChonMauKhiIn
                        || (Control.ModifierKeys == (Keys.Control | Keys.Shift))
                        || templatePickerOverride != null);
                bool showPreview = forcePreviewOnly || (!forceDirectPrint && SystemConfig.HienThiTruocKhiIn);

                string defaultPrinter = GetDefaultWindowsPrinter();
                string areaPrinter = printerStore != null && !string.IsNullOrEmpty(doc.AreaId)
                    ? printerStore.GetConfiguredPrinter(KitchenPrintRules.AreaInvoicePrinterKey(doc.AreaId))
                    : "";
                string secondCopyPrinter;
                string primaryPrinter = KitchenPrintRules.ResolveInvoicePrinter(
                    (InvoiceAreaPrintMode)SystemConfig.InHoaDonTheoKhuVuc,
                    isTempPrint,
                    isReprint,
                    defaultPrinter,
                    areaPrinter,
                    out secondCopyPrinter);

                PrintSelectionResult selection;
                if (showSelectTemplate)
                {
                    if (templatePickerOverride != null)
                    {
                        selection = templatePickerOverride(owner, ordered);
                    }
                    else
                    {
                        if (hostControl != null)
                            hostControl.Cursor = oldCursor;
                        using (var dlg = new PrintTemplateDialog(ordered, dialogOptions))
                        {
                            selection = dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Selection : null;
                        }
                        if (hostControl != null)
                            hostControl.Cursor = Cursors.WaitCursor;
                    }
                }
                else if (showPreview)
                {
                    selection = new PrintSelectionResult
                    {
                        Template = defaultTemplate,
                        Action = PrintActionKind.Preview
                    };
                }
                else
                {
                    selection = new PrintSelectionResult
                    {
                        Template = defaultTemplate,
                        Action = PrintActionKind.Print,
                        PrinterName = primaryPrinter
                    };
                }

                if (selection == null || selection.Template == null)
                    return false;

                Dictionary<string, bool> configFlags;
                try
                {
                    configFlags = repo.LoadConfigVisibilityForTemplate(selection.Template);
                }
                catch (DataAccessException ex)
                {
                    ReportError(owner, output, recordMessage, "Không đọc được cấu hình hiển thị cột của mẫu in: " + ex.Message);
                    return false;
                }

                CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(databasePath);
                var buildOpt = new InvoicePrintBuildOptions
                {
                    IsTempPrint = isTempPrint,
                    GopChungChietKhau = SystemConfig.GopChungGiamGiaTienGioVaTienHang,
                    ShowCustomerPoints = SystemConfig.HienThiDiemCuaKhachHangTrenHoaDon,
                    LoiCamOn = SystemConfig.LoiCamOn,
                    PrintedBy = DocumentPrintCoordinator.ResolvePrintedBy(session)
                };

                using (PrintJobData job = SalesInvoicePrintDataBuilder.Build(doc, company, buildOpt))
                {
                    job.ConfigVisibilityById = configFlags;

                    int copies = (selection.Action == PrintActionKind.Print && !isTempPrint)
                        ? Math.Max(1, Math.Min(5, SystemConfig.SoLanIn))
                        : 1;

                    bool ok = true;
                    for (int c = 0; c < copies; c++)
                    {
                        if (!PrintWorkflowService.Execute(owner, selection, job, output))
                        {
                            ok = false;
                            break;
                        }
                    }

                    // goc-PrintInvoice-nhahang dòng 180: InMotLienTaiQuayMotLienTheoKhuVuc && !rePrint -> in thêm liên theo khu vực.
                    if (ok && selection.Action == PrintActionKind.Print && !showSelectTemplate
                        && !string.IsNullOrWhiteSpace(secondCopyPrinter))
                    {
                        var areaSel = new PrintSelectionResult
                        {
                            Template = selection.Template,
                            Action = PrintActionKind.Print,
                            PrinterName = secondCopyPrinter
                        };
                        ok = PrintWorkflowService.Execute(owner, areaSel, job, output);
                    }

                    return ok;
                }
            }
            catch (Exception ex)
            {
                ReportError(owner, output, recordMessage, "Không thể in hóa đơn: " + ex.Message);
                return false;
            }
            finally
            {
                if (triggerButton != null)
                    triggerButton.Enabled = oldEnabled;
                if (hostControl != null)
                    hostControl.Cursor = oldCursor;
                _isBusy = false;
            }
        }

        private static void ReportError(
            IWin32Window owner,
            IPrintOutputHandler output,
            Action<string> recordMessage,
            string message)
        {
            if (recordMessage != null)
                recordMessage(message);
            if (output != null)
                output.ShowError(owner, message);
        }

        internal static string GetDefaultWindowsPrinter()
        {
            try
            {
                var settings = new PrinterSettings();
                return settings.PrinterName ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
