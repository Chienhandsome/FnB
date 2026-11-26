using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in báo cáo cho <see cref="ItemSalesView"/> (tách riêng để giữ ItemSalesView gọn).
    /// Bảo đảm: chặn quyền Xem giá nhập trước khi dựng dữ liệu, dùng chung <see cref="FormPrintCoordinator"/>.
    /// </summary>
    internal sealed class ItemSalesPrintCoordinator
    {
        private readonly FormPrintCoordinator _core = new FormPrintCoordinator();

        public bool IsBusy
        {
            get { return _core.IsBusy; }
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
            if (_core.IsBusy)
                return false;

            if (!canViewCost)
            {
                IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
                IWin32Window owner = hostControl == null ? null : hostControl.FindForm();
                if (recordMessage != null)
                    recordMessage(PrintConstants.NoCostPermissionPrintMessage);
                output.ShowInfo(owner, PrintConstants.NoCostPermissionPrintMessage);
                return false;
            }

            return _core.Print(
                hostControl,
                triggerButton,
                databasePath,
                PrintConstants.ItemSalesFormId,
                delegate (CompanyPrintInfo company)
                {
                    return ItemSalesPrintDataBuilder.Build(
                        visibleRows, groupNames, summary, fromDate, toDate, company, canViewCost);
                },
                outputOverride,
                templatePickerOverride,
                recordMessage);
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

            CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(databasePath);
            return ItemSalesPrintDataBuilder.Build(
                visibleRows, groupNames, summary, fromDate, toDate, company, canViewCost);
        }
    }
}
