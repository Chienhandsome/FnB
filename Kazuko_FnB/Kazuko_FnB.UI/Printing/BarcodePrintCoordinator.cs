using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in mã vạch từ khung "IN MÃ VẠCH":
    /// kiểm tra danh sách rỗng, khóa nút chống bấm lặp, đọc 6 mẫu từ STEMPLATE (BARCODE = 30),
    /// mở hộp "MẪU IN MÃ VẠCH" (PrintTemplateDialog), đọc lại dữ liệu mặt hàng từ CSDL lúc bấm Chấp nhận
    /// (áp quyền Xem giá nhập N3, bỏ qua mặt hàng đã xóa và báo cuối quá trình), dựng DataSet và mở xem trước.
    /// </summary>
    internal sealed class BarcodePrintCoordinator
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
        }

        public bool Print(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            IList<BarcodePrintRequestItem> requestItems,
            bool? canViewCostOverride,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            if (_isBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();

            List<BarcodePrintRequestItem> activeItems = CollectActiveItems(requestItems);
            if (activeItems.Count == 0)
            {
                NotifyInfo(output, owner, recordMessage, PrintConstants.NoBarcodeItemsMessage);
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

                var tplRepo = new PrintTemplateRepository(databasePath);
                List<PrintTemplateRecord> templates;
                try
                {
                    templates = templateLoaderOverride != null
                        ? templateLoaderOverride(PrintConstants.ProductTableDescId)
                        : tplRepo.LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);
                }
                catch (DataAccessException ex)
                {
                    NotifyError(output, owner, recordMessage, "Không đọc được danh sách mẫu in mã vạch: " + ex.Message);
                    return false;
                }

                if (templates == null || templates.Count == 0)
                {
                    NotifyInfo(output, owner, recordMessage, PrintConstants.NoTemplateFoundMessage);
                    return false;
                }

                PrintDialogOptions options = PrintDialogOptions.ForBarcodeDialog();
                int defaultIdx;
                List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(templates, options, out defaultIdx);

                PrintSelectionResult selection;
                if (templatePickerOverride != null)
                {
                    selection = templatePickerOverride(owner, ordered);
                }
                else
                {
                    if (hostControl != null)
                        hostControl.Cursor = oldCursor;
                    using (var dlg = new PrintTemplateDialog(ordered, options))
                    {
                        selection = dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Selection : null;
                    }
                    if (hostControl != null)
                        hostControl.Cursor = Cursors.WaitCursor;
                }

                if (selection == null || selection.Template == null)
                    return false;

                // Hộp MẪU IN MÃ VẠCH luôn mở xem trước (không in thẳng).
                var previewSelection = new PrintSelectionResult
                {
                    Template = selection.Template,
                    Action = PrintActionKind.Preview,
                    PrinterName = null
                };

                bool canViewCost = canViewCostOverride.HasValue
                    ? canViewCostOverride.Value
                    : AppSession.Can("Xem giá nhập", RoleModes.View);

                Dictionary<string, BarcodeProductRecord> dbProducts;
                try
                {
                    var ids = new List<string>(activeItems.Count);
                    for (int i = 0; i < activeItems.Count; i++)
                        ids.Add(activeItems[i].ProductId);
                    dbProducts = new BarcodePrintRepository(databasePath).LoadProductsByIds(ids, canViewCost);
                }
                catch (DataAccessException ex)
                {
                    NotifyError(output, owner, recordMessage, "Không đọc được dữ liệu mặt hàng để in mã vạch: " + ex.Message);
                    return false;
                }

                var resolvedEntries = new List<BarcodePrintEntry>();
                var missingNames = new List<string>();
                for (int i = 0; i < activeItems.Count; i++)
                {
                    BarcodePrintRequestItem req = activeItems[i];
                    BarcodeProductRecord rec;
                    if (!string.IsNullOrEmpty(req.ProductId) && dbProducts.TryGetValue(req.ProductId, out rec))
                    {
                        resolvedEntries.Add(new BarcodePrintEntry { Product = rec, Quantity = req.Quantity });
                    }
                    else
                    {
                        string label = !string.IsNullOrWhiteSpace(req.Name)
                            ? req.Name.Trim()
                            : (!string.IsNullOrWhiteSpace(req.Code) ? req.Code.Trim() : req.ProductId);
                        missingNames.Add(label);
                    }
                }

                if (resolvedEntries.Count == 0)
                {
                    string missingMsg = FormatMissingProductsMessage(missingNames);
                    NotifyInfo(output, owner, recordMessage, missingMsg);
                    return false;
                }

                CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(databasePath);
                PrintJobData job;
                string buildError;
                if (!BarcodePrintDataBuilder.TryBuild(previewSelection.Template, resolvedEntries, company, canViewCost, out job, out buildError))
                {
                    NotifyError(output, owner, recordMessage, buildError);
                    return false;
                }

                bool executed;
                using (job)
                {
                    try
                    {
                        job.ConfigVisibilityById = tplRepo.LoadConfigVisibilityForTemplate(previewSelection.Template);
                    }
                    catch (DataAccessException)
                    {
                        job.ConfigVisibilityById = previewSelection.Template.ConfigVisibilityById;
                    }
                    executed = PrintWorkflowService.Execute(owner, previewSelection, job, output);
                }

                if (missingNames.Count > 0)
                {
                    string missingMsg = FormatMissingProductsMessage(missingNames);
                    NotifyInfo(output, owner, recordMessage, missingMsg);
                }
                return executed;
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

        private static List<BarcodePrintRequestItem> CollectActiveItems(IList<BarcodePrintRequestItem> items)
        {
            var list = new List<BarcodePrintRequestItem>();
            if (items == null)
                return list;
            for (int i = 0; i < items.Count; i++)
            {
                BarcodePrintRequestItem item = items[i];
                if (item != null && item.Quantity > 0)
                    list.Add(item);
            }
            return list;
        }

        internal static string FormatMissingProductsMessage(IList<string> missingNames)
        {
            if (missingNames == null || missingNames.Count == 0)
                return "";
            var joined = new string[missingNames.Count];
            for (int i = 0; i < missingNames.Count; i++)
                joined[i] = missingNames[i] ?? "";
            return "Đã bỏ qua mặt hàng đã xóa hoặc không còn tồn tại: " + string.Join(", ", joined) + ".";
        }

        private static void NotifyInfo(IPrintOutputHandler output, IWin32Window owner, Action<string> record, string msg)
        {
            if (record != null)
                record(msg);
            output.ShowInfo(owner, msg);
        }

        private static void NotifyError(IPrintOutputHandler output, IWin32Window owner, Action<string> record, string msg)
        {
            if (record != null)
                record(msg);
            output.ShowError(owner, msg);
        }
    }
}
