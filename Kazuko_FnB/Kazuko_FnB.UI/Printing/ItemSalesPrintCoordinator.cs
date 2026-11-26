using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in báo cáo cho <see cref="ItemSalesView"/> (tách riêng để giữ ItemSalesView gọn).
    /// Bảo đảm: chặn quyền Xem giá nhập, khóa nút + đổi con trỏ chờ tránh bấm hai lần, đọc mẫu từ STEMPLATE.
    /// </summary>
    internal sealed class ItemSalesPrintCoordinator
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
            bool canViewCost,
            IList<ItemSalesRow> visibleRows,
            IDictionary<string, string> groupNames,
            ItemSalesSummary summary,
            DateTime fromDate,
            DateTime toDate,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Action<string> recordMessage)
        {
            if (_isBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();

            if (!canViewCost)
            {
                if (recordMessage != null)
                    recordMessage(PrintConstants.NoCostPermissionPrintMessage);
                output.ShowInfo(owner, PrintConstants.NoCostPermissionPrintMessage);
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
                    templates = repo.LoadTemplatesForForm(PrintConstants.ItemSalesFormId);
                }
                catch (DataAccessException ex)
                {
                    string msg = "Không đọc được danh sách mẫu in: " + ex.Message;
                    if (recordMessage != null)
                        recordMessage(msg);
                    output.ShowError(owner, msg);
                    return false;
                }

                if (templates == null || templates.Count == 0)
                {
                    if (recordMessage != null)
                        recordMessage(PrintConstants.NoTemplateFoundMessage);
                    output.ShowInfo(owner, PrintConstants.NoTemplateFoundMessage);
                    return false;
                }

                PrintSelectionResult selection;
                if (templatePickerOverride != null)
                {
                    selection = templatePickerOverride(owner, templates);
                }
                else
                {
                    if (hostControl != null)
                        hostControl.Cursor = oldCursor;
                    using (var dlg = new PrintTemplateDialog(templates))
                    {
                        selection = dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Selection : null;
                    }
                    if (hostControl != null)
                        hostControl.Cursor = Cursors.WaitCursor;
                }

                if (selection == null || selection.Template == null)
                    return false;

                CompanyPrintInfo company;
                try
                {
                    company = repo.LoadCompanyInfo();
                }
                catch (DataAccessException)
                {
                    company = new CompanyPrintInfo();
                }

                using (PrintJobData job = ItemSalesPrintDataBuilder.Build(
                    visibleRows, groupNames, summary, fromDate, toDate, company, canViewCost))
                {
                    return PrintWorkflowService.Execute(owner, selection, job, output);
                }
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

        public PrintJobData BuildJobData(
            string databasePath,
            bool canViewCost,
            IList<ItemSalesRow> visibleRows,
            IDictionary<string, string> groupNames,
            ItemSalesSummary summary,
            DateTime fromDate,
            DateTime toDate)
        {
            if (!canViewCost)
                throw new UnauthorizedAccessException(PrintConstants.NoCostPermissionPrintMessage);

            CompanyPrintInfo company;
            try
            {
                company = new PrintTemplateRepository(databasePath).LoadCompanyInfo();
            }
            catch (DataAccessException)
            {
                company = new CompanyPrintInfo();
            }

            return ItemSalesPrintDataBuilder.Build(
                visibleRows, groupNames, summary, fromDate, toDate, company, canViewCost);
        }
    }
}
